using Asp.Versioning;
using Crumpled.VerifyOwnership.Models;
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
        private readonly IGoogleSiteVerificationStore _googleStore;
        private readonly IBingSiteVerificationStore _bingStore;

        public VerifyOwnershipConfigController(IGoogleSiteVerificationStore googleStore, IBingSiteVerificationStore bingStore)
        {
            _googleStore = googleStore;
            _bingStore = bingStore;
        }

        // Named "Entries" rather than "GetEntries" - the operation ID (from the action name, see
        // OpenApiRegistration.Umbraco17.cs's CustomOperationHandler) is combined with the HTTP verb by the
        // generated TypeScript client, so "GetEntries" would produce the redundant "getGetEntries".
        [HttpGet("google/entries")]
        [ProducesResponseType<VerificationEntriesResponseModel>(StatusCodes.Status200OK)]
        public VerificationEntriesResponseModel GoogleEntries() => new()
        {
            Entries = _googleStore.GetEntries()
                .Select(entry => new VerificationEntryModel
                {
                    Id = entry.Id,
                    PersonName = entry.PersonName,
                    DateAdded = entry.DateAdded,
                    LastRequestedAt = _googleStore.GetLastRequestedTime(entry.Id),
                })
                .ToArray(),
        };

        [HttpPut("google/entries")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult SetGoogleEntries(GoogleVerificationEntriesRequestModel model)
        {
            _googleStore.SetEntries(model.Entries.Select(entry => new VerificationEntry
            {
                Id = entry.Id,
                PersonName = entry.PersonName,
                DateAdded = DateTimeOffset.UtcNow,
            }));
            return Ok();
        }

        [HttpGet("bing/entries")]
        [ProducesResponseType<BingVerificationEntriesResponseModel>(StatusCodes.Status200OK)]
        public BingVerificationEntriesResponseModel BingEntries() => new()
        {
            Entries = _bingStore.GetEntries()
                .Select(entry => new VerificationEntryModel
                {
                    Id = entry.Id,
                    PersonName = entry.PersonName,
                    DateAdded = entry.DateAdded,
                    // LastRequestedAt deliberately left null here - Bing has no per-id "last requested"
                    // value; see FileLastRequestedAt below for the one shared value for the whole section.
                })
                .ToArray(),
            FileLastRequestedAt = _bingStore.GetLastRequestedTime(),
        };

        [HttpPut("bing/entries")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult SetBingEntries(BingVerificationEntriesRequestModel model)
        {
            _bingStore.SetEntries(model.Entries.Select(entry => new VerificationEntry
            {
                Id = entry.Id,
                PersonName = entry.PersonName,
                DateAdded = DateTimeOffset.UtcNow,
            }));
            return Ok();
        }
    }
}
