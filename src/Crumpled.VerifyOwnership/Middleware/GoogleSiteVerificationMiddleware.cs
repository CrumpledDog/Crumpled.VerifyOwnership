using System.Text.RegularExpressions;
using Crumpled.VerifyOwnership.Services;
using Microsoft.AspNetCore.Http;

namespace Crumpled.VerifyOwnership.Middleware
{
    /// <summary>
    /// Serves the Google Search Console HTML-file verification method automatically: a request for
    /// <c>/google{token}.html</c> where <c>{token}</c> matches a configured verification code gets the exact
    /// body Google expects, with no file needing to be deployed to wwwroot.
    /// </summary>
    public partial class GoogleSiteVerificationMiddleware : IMiddleware
    {
        private readonly IGoogleSiteVerificationStore _store;

        public GoogleSiteVerificationMiddleware(IGoogleSiteVerificationStore store) => _store = store;

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var match = VerificationFileRoute().Match(context.Request.Path.Value ?? string.Empty);
            if (!match.Success)
            {
                await next(context);
                return;
            }

            var token = match.Groups["token"].Value;
            if (!_store.GetCodes().Contains(token, StringComparer.Ordinal))
            {
                await next(context);
                return;
            }

            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync($"google-site-verification: google{token}.html");
        }

        [GeneratedRegex(@"^/google(?<token>[a-f0-9]+)\.html$")]
        private static partial Regex VerificationFileRoute();
    }
}
