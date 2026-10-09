namespace Crumpled.VerifyOwnership.Models
{
    /// <summary>
    /// A single provider-agnostic ownership-verification entry (e.g. one Google verification code, one
    /// future Bing-listed id). Shared by every provider's store and by the combined document persisted via
    /// <see cref="Services.IVerificationEntryStore"/>.
    /// </summary>
    public sealed record VerificationEntry
    {
        public required string Id { get; init; }

        public required string PersonName { get; init; }

        public required DateTimeOffset DateAdded { get; init; }
    }
}
