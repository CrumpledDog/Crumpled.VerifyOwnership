using Crumpled.VerifyOwnership.Models;
using Crumpled.VerifyOwnership.Options;
using Microsoft.Extensions.Options;

namespace Crumpled.VerifyOwnership.Services
{
    /// <summary>
    /// Google's provider-scoped view over the combined <see cref="IVerificationEntryStore"/> document.
    /// Seeded once from <see cref="GoogleSiteVerificationOptions"/> the first time the "google" provider key
    /// has never been written to the document - once seeded, the backoffice-managed list is authoritative.
    /// </summary>
    public class GoogleSiteVerificationStore : IGoogleSiteVerificationStore
    {
        private const string Provider = "google";

        private readonly IVerificationEntryStore _entryStore;
        private readonly IVerificationRequestTracker _requestTracker;
        private readonly IOptionsMonitor<GoogleSiteVerificationOptions> _options;

        public GoogleSiteVerificationStore(
            IVerificationEntryStore entryStore,
            IVerificationRequestTracker requestTracker,
            IOptionsMonitor<GoogleSiteVerificationOptions> options)
        {
            _entryStore = entryStore;
            _requestTracker = requestTracker;
            _options = options;
        }

        public IReadOnlyList<VerificationEntry> GetEntries()
        {
            var entries = _entryStore.GetEntries(Provider);
            if (entries is not null)
            {
                return entries;
            }

            var seeded = _options.CurrentValue.VerificationCodes
                .Select(seed => new VerificationEntry { Id = seed.Id, PersonName = seed.PersonName, DateAdded = DateTimeOffset.UtcNow })
                .ToArray();
            _entryStore.SetEntries(Provider, seeded);
            return seeded;
        }

        public void SetEntries(IEnumerable<VerificationEntry> entries) => _entryStore.SetEntries(Provider, entries);

        public void RecordRequestServed(string id) => _requestTracker.RecordRequest(Provider, id);

        public DateTimeOffset? GetLastRequestedTime(string id) => _requestTracker.GetLastRequestedTime(Provider, id);
    }
}
