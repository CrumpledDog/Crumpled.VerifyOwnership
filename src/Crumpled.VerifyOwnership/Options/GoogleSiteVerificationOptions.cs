namespace Crumpled.VerifyOwnership.Options
{
    /// <summary>
    /// Bound from <c>Crumpled:VerifyOwnership</c>. Used only to seed <see cref="Services.IGoogleSiteVerificationStore"/>
    /// on first run - once seeded, the backoffice-managed list in the store is authoritative.
    /// </summary>
    public class GoogleSiteVerificationOptions
    {
        public IEnumerable<string> VerificationCodes { get; set; } = [];
    }
}
