namespace Crumpled.VerifyOwnership.ViewModels
{
    public class VerificationCodesResponseModel
    {
        public required IReadOnlyList<string> Codes { get; init; }
    }

    public class VerificationCodesRequestModel
    {
        public required IEnumerable<string> Codes { get; init; }
    }
}
