namespace Crumpled.VerifyOwnership.Options
{
    /// <summary>
    /// Bound from <c>Crumpled:VerifyOwnership:Bing</c>. Used only to seed
    /// <see cref="Services.IBingSiteVerificationStore"/> on first run - once seeded, the backoffice-managed
    /// list in the store is authoritative.
    /// </summary>
    public class BingSiteVerificationOptions
    {
        public IEnumerable<VerificationCodeSeed> VerificationCodes { get; set; } = [];
    }
}
