using Crumpled.VerifyOwnership.Middleware;
using Crumpled.VerifyOwnership.Options;
using Crumpled.VerifyOwnership.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace Crumpled.VerifyOwnership.Composers
{
    public class CrumpledVerifyOwnershipComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.AddOptions<GoogleSiteVerificationOptions>()
                .BindConfiguration("Crumpled:VerifyOwnership:Google");
            builder.Services.AddOptions<BingSiteVerificationOptions>()
                .BindConfiguration("Crumpled:VerifyOwnership:Bing");
            builder.Services.AddOptions<VerifyOwnershipOptions>()
                .BindConfiguration("Crumpled:VerifyOwnership");

            builder.Services.AddMemoryCache();

            // Verification entries (admin config) always live in the key/value store, unconditionally -
            // this is never swapped, regardless of the UseDistributedCache option below.
            builder.Services.AddSingleton<KeyValueVerificationEntryStore>();
            builder.Services.AddSingleton<IVerificationEntryStore>(sp => sp.GetRequiredService<KeyValueVerificationEntryStore>());

            // "Last requested" tracking is the only thing the UseDistributedCache option affects: opting in
            // uses a distributed cache, otherwise it shares the same key/value document the entries store
            // already uses. Trusting the flag alone (no presence-detection) is deliberate - a registered
            // IDistributedCache does not reliably indicate a real, farm-wide-shared cache is configured
            // (e.g. some dependencies register the in-process MemoryDistributedCache as an ambient default
            // even when nothing has been explicitly configured), so auto-detecting "is a real one present"
            // isn't possible. If UseDistributedCache is enabled without a real IDistributedCache registered,
            // resolving IVerificationRequestTracker throws - fail loudly, don't silently under-deliver.
            builder.Services.AddSingleton<DistributedCacheRequestTracker>();
            builder.Services.AddSingleton<IVerificationRequestTracker>(SelectRequestTracker);

            builder.Services.AddSingleton<IGoogleSiteVerificationStore, GoogleSiteVerificationStore>();
            builder.Services.AddSingleton<IBingSiteVerificationStore, BingSiteVerificationStore>();
            builder.Services.AddTransient<GoogleSiteVerificationMiddleware>();
            builder.Services.AddTransient<BingSiteVerificationMiddleware>();

            // Auto-registers the middleware into Umbraco's pipeline - no explicit app.Use...() call needed
            // in the consuming site's Program.cs.
            builder.Services.Configure<UmbracoPipelineOptions>(options =>
                options.AddFilter(new UmbracoPipelineFilter(
                    "CrumpledVerifyOwnership",
                    preRouting: app =>
                    {
                        app.UseMiddleware<GoogleSiteVerificationMiddleware>();
                        app.UseMiddleware<BingSiteVerificationMiddleware>();
                    })));
        }

        // Extracted so the selection logic can be exercised directly in tests without needing a full
        // IUmbracoBuilder - given an IServiceProvider with the same registrations Compose() sets up above,
        // this is exactly what gets called to resolve IVerificationRequestTracker.
        internal static IVerificationRequestTracker SelectRequestTracker(IServiceProvider sp)
        {
            var options = sp.GetRequiredService<IOptionsMonitor<VerifyOwnershipOptions>>().CurrentValue;
            return options.UseDistributedCache
                ? sp.GetRequiredService<DistributedCacheRequestTracker>()
                : sp.GetRequiredService<KeyValueVerificationEntryStore>();
        }
    }
}
