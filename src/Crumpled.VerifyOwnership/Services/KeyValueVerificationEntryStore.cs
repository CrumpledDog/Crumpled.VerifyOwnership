using System.Text.Json;
using Crumpled.VerifyOwnership.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Services;

namespace Crumpled.VerifyOwnership.Services
{
    /// <summary>
    /// The internal, per-provider storage shape - carries <see cref="LastRequestedAt"/> alongside the
    /// admin-managed fields so the default (no distributed cache configured) tracking backend can share this
    /// store's single document rather than adding a second row. Deliberately not the public
    /// <see cref="VerificationEntry"/> model: <see cref="IVerificationEntryStore"/> callers never see this
    /// field - only <see cref="IVerificationRequestTracker"/> callers can read it.
    /// </summary>
    internal sealed record StoredVerificationEntry
    {
        public required string Id { get; init; }

        public required string PersonName { get; init; }

        public required DateTimeOffset DateAdded { get; init; }

        public DateTimeOffset? LastRequestedAt { get; init; }
    }

    /// <summary>
    /// Persists every provider's verification entries in a single combined <see cref="IKeyValueService"/>
    /// row, keyed by provider name, so a future provider (e.g. Bing) needs no storage migration. Reads are
    /// served from a short-lived in-memory cache so the request-per-verification-file middleware never hits
    /// the key/value store directly; a failed reload (bad JSON, or the store itself throwing) never wipes an
    /// already-good cached copy. Also serves as the default <see cref="IVerificationRequestTracker"/>
    /// implementation, sharing this same document rather than using a second key/value row.
    /// </summary>
    public class KeyValueVerificationEntryStore : IVerificationEntryStore, IVerificationRequestTracker
    {
        private const string DocumentKey = "crumpled.verifyownership.entries";
        private const string CacheKey = "Crumpled.VerifyOwnership.EntriesDocument";
        private const string ProviderRequestedDocumentKey = "crumpled.verifyownership.providerrequested";
        private const string ProviderRequestedCacheKey = "Crumpled.VerifyOwnership.ProviderRequestedDocument";
        private const int MaxWriteAttempts = 5;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DefaultRequestThrottleWindow = TimeSpan.FromHours(1);
        private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly IKeyValueService _keyValueService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<KeyValueVerificationEntryStore> _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly SemaphoreSlim _providerRequestedWriteLock = new(1, 1);
        private readonly TimeSpan _requestThrottleWindow;
        private readonly Dictionary<(string Provider, string Id), DateTimeOffset> _lastRequestAttempt = new();
        private readonly Dictionary<string, DateTimeOffset> _lastProviderRequestAttempt = new();

        private volatile IReadOnlyDictionary<string, IReadOnlyList<StoredVerificationEntry>> _lastGoodDocument =
            new Dictionary<string, IReadOnlyList<StoredVerificationEntry>>(StringComparer.Ordinal);

        private volatile IReadOnlyDictionary<string, DateTimeOffset> _lastGoodProviderRequestedDocument =
            new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

        public KeyValueVerificationEntryStore(
            IKeyValueService keyValueService,
            IMemoryCache cache,
            ILogger<KeyValueVerificationEntryStore> logger)
            : this(keyValueService, cache, logger, DefaultRequestThrottleWindow)
        {
        }

        // Internal seam so tests can use a zero/short window instead of waiting out a real hour - mirrors
        // the existing internal ReloadDocument()/GetOrLoadDocument() test seam below.
        internal KeyValueVerificationEntryStore(
            IKeyValueService keyValueService,
            IMemoryCache cache,
            ILogger<KeyValueVerificationEntryStore> logger,
            TimeSpan requestThrottleWindow)
        {
            _keyValueService = keyValueService;
            _cache = cache;
            _logger = logger;
            _requestThrottleWindow = requestThrottleWindow;
        }

        public IReadOnlyList<VerificationEntry>? GetEntries(string provider) =>
            GetOrLoadDocument().TryGetValue(provider, out var entries)
                ? entries.Select(e => new VerificationEntry { Id = e.Id, PersonName = e.PersonName, DateAdded = e.DateAdded }).ToArray()
                : null;

        public void SetEntries(string provider, IEnumerable<VerificationEntry> entries)
        {
            var incoming = entries as IReadOnlyCollection<VerificationEntry> ?? entries.ToArray();

            _writeLock.Wait();
            try
            {
                for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
                {
                    var originValue = _keyValueService.GetValue(DocumentKey);
                    var document = ParseDocument(originValue);

                    var existingById = document.TryGetValue(provider, out var current)
                        ? current.ToDictionary(e => e.Id, StringComparer.Ordinal)
                        : new Dictionary<string, StoredVerificationEntry>(StringComparer.Ordinal);

                    // Ids already tracked for this provider keep their original DateAdded/LastRequestedAt,
                    // even if this save's caller supplied a different DateAdded - only ids new to this
                    // provider get stamped, and always start with no LastRequestedAt.
                    var merged = incoming
                        .Select(e => existingById.TryGetValue(e.Id, out var existing)
                            ? new StoredVerificationEntry { Id = e.Id, PersonName = e.PersonName, DateAdded = existing.DateAdded, LastRequestedAt = existing.LastRequestedAt }
                            : new StoredVerificationEntry { Id = e.Id, PersonName = e.PersonName, DateAdded = e.DateAdded })
                        .ToArray();

                    var updatedDocument = new Dictionary<string, IReadOnlyList<StoredVerificationEntry>>(document, StringComparer.Ordinal)
                    {
                        [provider] = merged,
                    };

                    var newValue = JsonSerializer.Serialize(updatedDocument, SerializerOptions);
                    var written = originValue is null
                        ? WriteFirstValue(DocumentKey, newValue)
                        : _keyValueService.TrySetValue(DocumentKey, originValue, newValue);

                    if (written)
                    {
                        _lastGoodDocument = updatedDocument;
                        _cache.Set(CacheKey, (IReadOnlyDictionary<string, IReadOnlyList<StoredVerificationEntry>>)updatedDocument, CacheDuration);
                        return;
                    }

                    // TrySetValue returned false: another writer changed the row since our read above - retry.
                }

                throw new InvalidOperationException(
                    $"Could not save verification entries for provider '{provider}' after {MaxWriteAttempts} attempts due to concurrent writes.");
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void RecordRequest(string provider, string id)
        {
            var key = (provider, id);
            var now = DateTimeOffset.UtcNow;

            lock (_lastRequestAttempt)
            {
                if (_lastRequestAttempt.TryGetValue(key, out var previousAttempt) && now - previousAttempt < _requestThrottleWindow)
                {
                    return;
                }

                // Claim the window optimistically before the slower key/value work, so a burst of concurrent
                // requests for the same id mostly doesn't all fall through together. A benign race at the
                // boundary is fine - the write below is idempotent, and worst case is one extra attempt.
                _lastRequestAttempt[key] = now;
            }

            try
            {
                PersistLastRequested(provider, id, now);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record the last-requested time for provider '{Provider}' id '{Id}'.", provider, id);
            }
        }

        public DateTimeOffset? GetLastRequestedTime(string provider, string id) =>
            GetOrLoadDocument().TryGetValue(provider, out var entries)
                ? entries.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal))?.LastRequestedAt
                : null;

        private void PersistLastRequested(string provider, string id, DateTimeOffset requestedAt)
        {
            _writeLock.Wait();
            try
            {
                for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
                {
                    var originValue = _keyValueService.GetValue(DocumentKey);
                    var document = ParseDocument(originValue);

                    if (!document.TryGetValue(provider, out var current))
                    {
                        return; // Provider not configured at all - nothing to touch.
                    }

                    var index = Array.FindIndex(current.ToArray(), e => string.Equals(e.Id, id, StringComparison.Ordinal));
                    if (index < 0)
                    {
                        return; // Id not configured for this provider - no-op, never create a bogus entry.
                    }

                    var updatedEntries = current.ToArray();
                    updatedEntries[index] = updatedEntries[index] with { LastRequestedAt = requestedAt };

                    var updatedDocument = new Dictionary<string, IReadOnlyList<StoredVerificationEntry>>(document, StringComparer.Ordinal)
                    {
                        [provider] = updatedEntries,
                    };

                    var newValue = JsonSerializer.Serialize(updatedDocument, SerializerOptions);
                    var written = originValue is null
                        ? WriteFirstValue(DocumentKey, newValue)
                        : _keyValueService.TrySetValue(DocumentKey, originValue, newValue);

                    if (written)
                    {
                        _lastGoodDocument = updatedDocument;
                        _cache.Set(CacheKey, (IReadOnlyDictionary<string, IReadOnlyList<StoredVerificationEntry>>)updatedDocument, CacheDuration);
                        return;
                    }
                }

                _logger.LogWarning(
                    "Could not record the last-requested time for provider '{Provider}' id '{Id}' after {Attempts} attempts due to concurrent writes.",
                    provider,
                    id,
                    MaxWriteAttempts);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private bool WriteFirstValue(string key, string newValue)
        {
            // Umbraco's IKeyValueService.TrySetValue doesn't treat a null origin value as "row doesn't
            // exist yet" - it just reports no match (returns false) since there's nothing to compare
            // against, which would otherwise loop through every retry attempt and never succeed. Fall back
            // to a plain SetValue for this very first write - the write-lock above still protects against
            // a same-process race here; a cross-process race on this one-time seed is a low-risk,
            // self-correcting edge case (whichever write lands last simply wins).
            _keyValueService.SetValue(key, newValue);
            return true;
        }

        public void RecordProviderRequest(string provider)
        {
            var now = DateTimeOffset.UtcNow;

            lock (_lastProviderRequestAttempt)
            {
                if (_lastProviderRequestAttempt.TryGetValue(provider, out var previousAttempt) && now - previousAttempt < _requestThrottleWindow)
                {
                    return;
                }

                _lastProviderRequestAttempt[provider] = now;
            }

            try
            {
                PersistProviderRequested(provider, now);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record the last-requested time for provider '{Provider}'.", provider);
            }
        }

        public DateTimeOffset? GetLastRequestedTime(string provider) =>
            GetOrLoadProviderRequestedDocument().TryGetValue(provider, out var timestamp) ? timestamp : null;

        private void PersistProviderRequested(string provider, DateTimeOffset requestedAt)
        {
            _providerRequestedWriteLock.Wait();
            try
            {
                for (var attempt = 1; attempt <= MaxWriteAttempts; attempt++)
                {
                    var originValue = _keyValueService.GetValue(ProviderRequestedDocumentKey);
                    var document = ParseProviderRequestedDocument(originValue);

                    var updatedDocument = new Dictionary<string, DateTimeOffset>(document, StringComparer.Ordinal)
                    {
                        [provider] = requestedAt,
                    };

                    var newValue = JsonSerializer.Serialize(updatedDocument, SerializerOptions);
                    var written = originValue is null
                        ? WriteFirstValue(ProviderRequestedDocumentKey, newValue)
                        : _keyValueService.TrySetValue(ProviderRequestedDocumentKey, originValue, newValue);

                    if (written)
                    {
                        _lastGoodProviderRequestedDocument = updatedDocument;
                        _cache.Set(ProviderRequestedCacheKey, (IReadOnlyDictionary<string, DateTimeOffset>)updatedDocument, CacheDuration);
                        return;
                    }

                    // TrySetValue returned false: another writer changed the row since our read above - retry.
                }

                _logger.LogWarning(
                    "Could not record the last-requested time for provider '{Provider}' after {Attempts} attempts due to concurrent writes.",
                    provider,
                    MaxWriteAttempts);
            }
            finally
            {
                _providerRequestedWriteLock.Release();
            }
        }

        internal IReadOnlyDictionary<string, DateTimeOffset> GetOrLoadProviderRequestedDocument()
        {
            if (_cache.TryGetValue(ProviderRequestedCacheKey, out IReadOnlyDictionary<string, DateTimeOffset>? cached) && cached is not null)
            {
                return cached;
            }

            var document = ReloadProviderRequestedDocument();
            _cache.Set(ProviderRequestedCacheKey, document, CacheDuration);
            return document;
        }

        internal IReadOnlyDictionary<string, DateTimeOffset> ReloadProviderRequestedDocument()
        {
            string? raw;
            try
            {
                raw = _keyValueService.GetValue(ProviderRequestedDocumentKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read provider-level last-requested times from the key/value store; continuing with the last known-good data.");
                return _lastGoodProviderRequestedDocument;
            }

            var document = ParseProviderRequestedDocument(raw);
            _lastGoodProviderRequestedDocument = document;
            return document;
        }

        private Dictionary<string, DateTimeOffset> ParseProviderRequestedDocument(string? raw)
        {
            var result = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            JsonDocument parsed;
            try
            {
                parsed = JsonDocument.Parse(raw);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Stored provider-level last-requested times JSON was malformed; treating as empty.");
                return result;
            }

            using (parsed)
            {
                if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Stored provider-level last-requested times JSON root was not an object; treating as empty.");
                    return result;
                }

                foreach (var providerProperty in parsed.RootElement.EnumerateObject())
                {
                    try
                    {
                        result[providerProperty.Name] = providerProperty.Value.Deserialize<DateTimeOffset>(SerializerOptions);
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning(
                            "Last-requested time for provider '{Provider}' was not a parseable timestamp; dropping this provider only.",
                            providerProperty.Name);
                    }
                }
            }

            return result;
        }

        internal IReadOnlyDictionary<string, IReadOnlyList<StoredVerificationEntry>> GetOrLoadDocument()
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, IReadOnlyList<StoredVerificationEntry>>? cached) && cached is not null)
            {
                return cached;
            }

            var document = ReloadDocument();
            _cache.Set(CacheKey, document, CacheDuration);
            return document;
        }

        internal IReadOnlyDictionary<string, IReadOnlyList<StoredVerificationEntry>> ReloadDocument()
        {
            string? raw;
            try
            {
                raw = _keyValueService.GetValue(DocumentKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read verification entries from the key/value store; continuing with the last known-good data.");
                return _lastGoodDocument;
            }

            var document = ParseDocument(raw);
            _lastGoodDocument = document;
            return document;
        }

        private Dictionary<string, IReadOnlyList<StoredVerificationEntry>> ParseDocument(string? raw)
        {
            var result = new Dictionary<string, IReadOnlyList<StoredVerificationEntry>>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(raw))
            {
                return result;
            }

            JsonDocument parsed;
            try
            {
                parsed = JsonDocument.Parse(raw);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Stored verification entries JSON was malformed; treating as empty.");
                return result;
            }

            using (parsed)
            {
                if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Stored verification entries JSON root was not an object; treating as empty.");
                    return result;
                }

                foreach (var providerProperty in parsed.RootElement.EnumerateObject())
                {
                    if (providerProperty.Value.ValueKind != JsonValueKind.Array)
                    {
                        _logger.LogWarning(
                            "Verification entries for provider '{Provider}' were not a JSON array; dropping this provider only.",
                            providerProperty.Name);
                        continue;
                    }

                    var entries = new List<StoredVerificationEntry>();
                    var dropped = 0;

                    foreach (var element in providerProperty.Value.EnumerateArray())
                    {
                        try
                        {
                            var entry = element.Deserialize<StoredVerificationEntry>(SerializerOptions);
                            if (entry is not null)
                            {
                                entries.Add(entry);
                            }
                        }
                        catch (JsonException)
                        {
                            dropped++;
                        }
                    }

                    if (dropped > 0)
                    {
                        _logger.LogWarning(
                            "Dropped {Count} malformed verification entr{Suffix} for provider '{Provider}'.",
                            dropped,
                            dropped == 1 ? "y" : "ies",
                            providerProperty.Name);
                    }

                    result[providerProperty.Name] = entries;
                }
            }

            return result;
        }
    }
}
