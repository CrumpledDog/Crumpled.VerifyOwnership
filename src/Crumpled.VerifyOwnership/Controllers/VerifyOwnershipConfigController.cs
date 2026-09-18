using Asp.Versioning;
using Crumpled.VerifyOwnership.Services;
using Crumpled.VerifyOwnership.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crumpled.VerifyOwnership.Controllers
{
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "Crumpled.VerifyOwnership")]
    public class VerifyOwnershipConfigController : CrumpledVerifyOwnershipApiControllerBase
    {
        private readonly IGoogleSiteVerificationStore _store;

        public VerifyOwnershipConfigController(IGoogleSiteVerificationStore store) => _store = store;

        // Named "Codes" rather than "GetCodes" - the operation ID (from the action name, see
        // OpenApiRegistration.Umbraco17.cs's CustomOperationHandler) is combined with the HTTP verb by the
        // generated TypeScript client, so "GetCodes" would produce the redundant "getGetCodes".
        [HttpGet("codes")]
        [ProducesResponseType<VerificationCodesResponseModel>(StatusCodes.Status200OK)]
        public VerificationCodesResponseModel Codes() => new() { Codes = _store.GetCodes() };

        [HttpPut("codes")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult SetCodes(VerificationCodesRequestModel model)
        {
            _store.SetCodes(model.Codes);
            return Ok();
        }
    }
}
