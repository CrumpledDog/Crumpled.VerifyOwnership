namespace Crumpled.VerifyOwnership.Services
{
    public interface IGoogleSiteVerificationStore
    {
        IReadOnlyList<string> GetCodes();

        void SetCodes(IEnumerable<string> codes);
    }
}
