namespace Crumpled.VerifyOwnership.Options
{
    /// <summary>
    /// A single seed verification entry (id + owning person) used to pre-populate a provider's backoffice-
    /// managed list the first time it has never been configured. Shared across every provider's options
    /// class - the seed shape itself is identical between them.
    /// </summary>
    public class VerificationCodeSeed
    {
        public required string Id { get; set; }

        public required string PersonName { get; set; }
    }
}
