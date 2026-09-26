namespace Crumpled.VerifyOwnership.Services
{
    /// <summary>
    /// Records the most recent time a verification file was successfully served for a given provider/id, as
    /// a best-effort hint for the backoffice dashboard - not a durable record. Backed by a swappable
    /// implementation (see <see cref="Composers.CrumpledVerifyOwnershipComposer"/>): a distributed-cache-backed
    /// implementation when one is configured, otherwise the same storage <see cref="IVerificationEntryStore"/>
    /// already uses. Losing this data (a cache eviction, a restart) is expected and self-heals on the next
    /// successful request - it never affects <see cref="IVerificationEntryStore"/>'s data. Never throws.
    /// </summary>
    public interface IVerificationRequestTracker
    {
        /// <summary>
        /// Records that <paramref name="id"/> was just served successfully for <paramref name="provider"/>.
        /// Internally rate-limited per process/per id - safe to call on every successful match regardless of
        /// request volume. Fire-and-forget: never throws, and the caller cannot observe whether this call
        /// actually persisted anything or was absorbed by the rate limit.
        /// </summary>
        void RecordRequest(string provider, string id);

        /// <summary>
        /// Returns the last-recorded timestamp for <paramref name="id"/> under <paramref name="provider"/>,
        /// or <c>null</c> if it was never recorded (or that record is no longer available).
        /// </summary>
        DateTimeOffset? GetLastRequestedTime(string provider, string id);

        /// <summary>
        /// Records that <paramref name="provider"/>'s shared verification file (not tied to any single
        /// configured id) was just served successfully - for providers whose verification method involves
        /// one file listing every configured id at once (e.g. Bing's <c>BingSiteAuth.xml</c>), rather than
        /// one file per id, the way Google's HTML-file method works. Internally rate-limited per
        /// process/per provider - safe to call on every successful serve regardless of request volume.
        /// Fire-and-forget: never throws, and the caller cannot observe whether this call actually
        /// persisted anything or was absorbed by the rate limit.
        /// </summary>
        void RecordProviderRequest(string provider);

        /// <summary>
        /// Returns the last-recorded timestamp that <paramref name="provider"/>'s shared verification file
        /// was served, or <c>null</c> if it was never recorded (or that record is no longer available).
        /// Distinct from <see cref="GetLastRequestedTime(string, string)"/> - this is one shared value for
        /// the whole provider, not per id.
        /// </summary>
        DateTimeOffset? GetLastRequestedTime(string provider);
    }
}
