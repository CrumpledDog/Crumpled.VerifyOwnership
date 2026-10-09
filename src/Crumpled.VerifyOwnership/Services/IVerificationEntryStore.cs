using Crumpled.VerifyOwnership.Models;

namespace Crumpled.VerifyOwnership.Services
{
    /// <summary>
    /// Persists ownership-verification entries for one or more providers (e.g. "google", a future "bing") in
    /// a single combined document, so a new provider never needs a storage migration. Never throws for
    /// read failures (malformed stored JSON, or the underlying store being transiently unavailable) - both
    /// degrade gracefully to an empty/last-known-good result.
    /// </summary>
    public interface IVerificationEntryStore
    {
        /// <summary>
        /// Returns the entries stored for <paramref name="provider"/>, or <c>null</c> if that provider has
        /// never been written to the document - distinct from an explicitly-saved empty list, which returns
        /// an empty array.
        /// </summary>
        IReadOnlyList<VerificationEntry>? GetEntries(string provider);

        /// <summary>
        /// Replaces <paramref name="provider"/>'s entries, leaving every other provider's entries in the
        /// combined document untouched. For any incoming entry whose <see cref="VerificationEntry.Id"/>
        /// already existed for this provider, the previously stored <see cref="VerificationEntry.DateAdded"/>
        /// is preserved regardless of the value passed in - only ids that are new to this provider keep the
        /// supplied <see cref="VerificationEntry.DateAdded"/>.
        /// </summary>
        void SetEntries(string provider, IEnumerable<VerificationEntry> entries);
    }
}
