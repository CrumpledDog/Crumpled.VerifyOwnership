using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Crumpled.VerifyOwnership.Services
{
    /// <summary>
    /// A <see cref="IVerificationRequestTracker"/> backed by <see cref="IDistributedCache"/> instead of the
    /// key/value store, used only when a site explicitly opts in (see
    /// <see cref="Composers.CrumpledVerifyOwnershipComposer"/>) and has a distributed cache configured. Each
    /// provider/id pair gets its own independent cache key - unlike the key/value document, there's no
    /// shared state to merge or lock, since key count doesn't matter in a cache the way it does in the
    /// shared <c>umbracoKeyValue</c> table. If the cache is cleared or evicted (a Redis tier change, a
    /// restart without persistence), the only consequence is these keys reading back empty - the next
    /// successful request repopulates them. This never affects <see cref="IVerificationEntryStore"/>'s data,
    /// which always lives in the key/value store regardless of this option.
    /// </summary>
    public class DistributedCacheRequestTracker : IVerificationRequestTracker
    {
        private static readonly TimeSpan DefaultRequestThrottleWindow = TimeSpan.FromHours(1);
        private static readonly TimeSpan EntryLifetime = TimeSpan.FromDays(30);

        private readonly IDistributedCache _cache;
        private readonly ILogger<DistributedCacheRequestTracker> _logger;
        private readonly TimeSpan _requestThrottleWindow;
        private readonly Dictionary<(string Provider, string Id), DateTimeOffset> _lastRequestAttempt = new();
        private readonly Dictionary<string, DateTimeOffset> _lastProviderRequestAttempt = new();

        public DistributedCacheRequestTracker(IDistributedCache cache, ILogger<DistributedCacheRequestTracker> logger)
            : this(cache, logger, DefaultRequestThrottleWindow)
        {
        }

        // Internal seam so tests can use a zero/short window instead of waiting out a real hour - mirrors
        // the equivalent seam on KeyValueVerificationEntryStore.
        internal DistributedCacheRequestTracker(IDistributedCache cache, ILogger<DistributedCacheRequestTracker> logger, TimeSpan requestThrottleWindow)
        {
            _cache = cache;
            _logger = logger;
            _requestThrottleWindow = requestThrottleWindow;
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

                _lastRequestAttempt[key] = now;
            }

            try
            {
                _cache.SetString(
                    CacheKey(provider, id),
                    now.ToString("O", CultureInfo.InvariantCulture),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = EntryLifetime });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record the last-requested time for provider '{Provider}' id '{Id}'.", provider, id);
            }
        }

        public DateTimeOffset? GetLastRequestedTime(string provider, string id)
        {
            try
            {
                var stored = _cache.GetString(CacheKey(provider, id));
                return stored is not null && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read the last-requested time for provider '{Provider}' id '{Id}'.", provider, id);
                return null;
            }
        }

        private static string CacheKey(string provider, string id) => $"crumpled:verifyownership:lastrequested:{provider}:{id}";

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
                _cache.SetString(
                    ProviderCacheKey(provider),
                    now.ToString("O", CultureInfo.InvariantCulture),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = EntryLifetime });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record the last-requested time for provider '{Provider}'.", provider);
            }
        }

        public DateTimeOffset? GetLastRequestedTime(string provider)
        {
            try
            {
                var stored = _cache.GetString(ProviderCacheKey(provider));
                return stored is not null && DateTimeOffset.TryParse(stored, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                    ? parsed
                    : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read the last-requested time for provider '{Provider}'.", provider);
                return null;
            }
        }

        private static string ProviderCacheKey(string provider) => $"crumpled:verifyownership:providerrequested:{provider}";
    }
}
