using System.ComponentModel.DataAnnotations;

namespace Crumpled.VerifyOwnership.ViewModels
{
    public class VerificationEntriesResponseModel
    {
        public required IReadOnlyList<VerificationEntryModel> Entries { get; init; }
    }

    public class VerificationEntryModel
    {
        public required string Id { get; init; }

        public required string PersonName { get; init; }

        public required DateTimeOffset DateAdded { get; init; }

        /// <summary>
        /// The most recent time this entry's file was successfully served to a request that looked like
        /// Google's crawler, or null if that has never happened (or the tracking backend has no record of
        /// it). A best-effort hint, not a verified fact - see the tracker's documentation. Not populated for
        /// providers (e.g. Bing) whose verification method has no per-id "last requested" concept - see
        /// <see cref="BingVerificationEntriesResponseModel.FileLastRequestedAt"/> instead.
        /// </summary>
        public DateTimeOffset? LastRequestedAt { get; init; }
    }

    public class BingVerificationEntriesResponseModel
    {
        public required IReadOnlyList<VerificationEntryModel> Entries { get; init; }

        /// <summary>
        /// The most recent time BingSiteAuth.xml (the single shared file listing every configured id) was
        /// successfully served to a request that looked like Bing's crawler, or null if that has never
        /// happened. One shared value for the whole section, not per entry - a single fetch of the shared
        /// file isn't meaningfully "about" any one id.
        /// </summary>
        public DateTimeOffset? FileLastRequestedAt { get; init; }
    }

    public class GoogleVerificationEntriesRequestModel
    {
        public required IEnumerable<GoogleVerificationEntryRequestModel> Entries { get; init; }
    }

    public class GoogleVerificationEntryRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        [RegularExpression("^[a-f0-9]+$", ErrorMessage = "Id must be a lowercase hexadecimal verification token.")]
        public required string Id { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(200)]
        public required string PersonName { get; init; }
    }

    public class BingVerificationEntriesRequestModel
    {
        public required IEnumerable<BingVerificationEntryRequestModel> Entries { get; init; }
    }

    public class BingVerificationEntryRequestModel
    {
        [Required(AllowEmptyStrings = false)]
        [RegularExpression("^[a-fA-F0-9]{32}$", ErrorMessage = "Id must be a 32-character hexadecimal verification code.")]
        public required string Id { get; init; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(200)]
        public required string PersonName { get; init; }
    }
}
