using Crumpled.VerifyOwnership.Middleware;
using Crumpled.VerifyOwnership.Options;
using Crumpled.VerifyOwnership.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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
                .BindConfiguration("Crumpled:VerifyOwnership");

            builder.Services.AddSingleton<IGoogleSiteVerificationStore, KeyValueGoogleSiteVerificationStore>();
            builder.Services.AddTransient<GoogleSiteVerificationMiddleware>();

            // Auto-registers the middleware into Umbraco's pipeline - no explicit app.Use...() call needed
            // in the consuming site's Program.cs.
            builder.Services.Configure<UmbracoPipelineOptions>(options =>
                options.AddFilter(new UmbracoPipelineFilter(
                    "CrumpledVerifyOwnership",
                    preRouting: app => app.UseMiddleware<GoogleSiteVerificationMiddleware>())));
        }
    }
}
