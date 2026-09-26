using Crumpled.VerifyOwnership.Models;

namespace Crumpled.VerifyOwnership.Services
{
    public interface IBingSiteVerificationStore
    {
        IReadOnlyList<VerificationEntry> GetEntries();

        void SetEntries(IEnumerable<VerificationEntry> entries);

        /// <summary>Records that BingSiteAuth.xml was just served successfully. Never throws.</summary>
        void RecordFileServed();

        /// <summary>Last-requested timestamp for the shared file, or null if never recorded. Never throws.</summary>
        DateTimeOffset? GetLastRequestedTime();
    }
}
