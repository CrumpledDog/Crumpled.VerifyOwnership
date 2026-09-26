using Crumpled.VerifyOwnership.Models;
using Crumpled.VerifyOwnership.Options;
using Microsoft.Extensions.Options;

namespace Crumpled.VerifyOwnership.Services
{
    /// <summary>
    /// Bing's provider-scoped view over the combined <see cref="IVerificationEntryStore"/> document and the
    /// <see cref="IVerificationRequestTracker"/>. Seeded once from <see cref="BingSiteVerificationOptions"/>
    /// the first time the "bing" provider key has never been written to the document - once seeded, the
    /// backoffice-managed list is authoritative. Unlike Google, tracking here is provider-level (one shared
    /// "last requested" value for the whole file), not per id - see <see cref="IBingSiteVerificationStore"/>.
    /// </summary>
    public class BingSiteVerificationStore : IBingSiteVerificationStore
    {
        private const string Provider = "bing";

        private readonly IVerificationEntryStore _entryStore;
        private readonly IVerificationRequestTracker _requestTracker;
        private readonly IOptionsMonitor<BingSiteVerificationOptions> _options;

        public BingSiteVerificationStore(
            IVerificationEntryStore entryStore,
            IVerificationRequestTracker requestTracker,
            IOptionsMonitor<BingSiteVerificationOptions> options)
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

        public void RecordFileServed() => _requestTracker.RecordProviderRequest(Provider);

        public DateTimeOffset? GetLastRequestedTime() => _requestTracker.GetLastRequestedTime(Provider);
    }
}
