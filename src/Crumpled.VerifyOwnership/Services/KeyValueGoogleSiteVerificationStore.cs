using System.Text.Json;
using Crumpled.VerifyOwnership.Options;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;

namespace Crumpled.VerifyOwnership.Services
{
    /// <summary>
    /// Persists the configured Google verification codes via Umbraco's own key/value store, so admins can
    /// add/remove codes from the backoffice without a redeploy. Seeded once from <see cref="GoogleSiteVerificationOptions"/>
    /// the first time no stored value exists yet.
    /// </summary>
    public class KeyValueGoogleSiteVerificationStore : IGoogleSiteVerificationStore
    {
        private const string StoreKey = "crumpled.verifyownership.codes";

        private readonly IKeyValueService _keyValueService;
        private readonly IOptionsMonitor<GoogleSiteVerificationOptions> _options;

        public KeyValueGoogleSiteVerificationStore(IKeyValueService keyValueService, IOptionsMonitor<GoogleSiteVerificationOptions> options)
        {
            _keyValueService = keyValueService;
            _options = options;
        }

        public IReadOnlyList<string> GetCodes()
        {
            var stored = _keyValueService.GetValue(StoreKey);
            if (stored is null)
            {
                var seeded = _options.CurrentValue.VerificationCodes.ToArray();
                SetCodes(seeded);
                return seeded;
            }

            return JsonSerializer.Deserialize<string[]>(stored) ?? [];
        }

        public void SetCodes(IEnumerable<string> codes)
        {
            var normalized = codes
                .Select(code => code.Trim())
                .Where(code => code.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            _keyValueService.SetValue(StoreKey, JsonSerializer.Serialize(normalized));
        }
    }
}
