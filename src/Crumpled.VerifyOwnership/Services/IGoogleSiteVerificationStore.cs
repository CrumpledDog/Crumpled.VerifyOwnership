using Crumpled.VerifyOwnership.Models;

namespace Crumpled.VerifyOwnership.Services
{
    public interface IGoogleSiteVerificationStore
    {
        IReadOnlyList<VerificationEntry> GetEntries();

        void SetEntries(IEnumerable<VerificationEntry> entries);

        /// <summary>Records that <paramref name="id"/> was just served successfully. Never throws.</summary>
        void RecordRequestServed(string id);

        /// <summary>Last-requested timestamp for <paramref name="id"/>, or null if never recorded. Never throws.</summary>
        DateTimeOffset? GetLastRequestedTime(string id);
    }
}
