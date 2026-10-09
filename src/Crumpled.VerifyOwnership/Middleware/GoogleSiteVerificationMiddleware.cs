using System.Text.RegularExpressions;
using Crumpled.VerifyOwnership.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger<GoogleSiteVerificationMiddleware> _logger;

        public GoogleSiteVerificationMiddleware(IGoogleSiteVerificationStore store, ILogger<GoogleSiteVerificationMiddleware> logger)
        {
            _store = store;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var match = VerificationFileRoute().Match(context.Request.Path.Value ?? string.Empty);
            if (!match.Success)
            {
                await next(context);
                return;
            }

            var token = match.Groups["token"].Value;
            if (!_store.GetEntries().Any(entry => string.Equals(entry.Id, token, StringComparison.Ordinal)))
            {
                await next(context);
                return;
            }

            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync($"google-site-verification: google{token}.html");

            var userAgent = context.Request.Headers.UserAgent.ToString();

            // User-Agent is a diagnostic hint only, logged for anyone who wants a full audit trail - it is
            // trivially spoofable and must never be treated as a security signal.
            _logger.LogInformation(
                "Served Google site-verification file for id {Id} to User-Agent {UserAgent}.",
                token,
                userAgent);

            // Only persist a "last requested" touch for requests that at least look like Google's crawler -
            // reduces how often the (still throttled) tracking write is even attempted, per request from the
            // team. This is noise reduction, not authentication: it does not change what gets served above.
            if (userAgent.Contains("google", StringComparison.OrdinalIgnoreCase))
            {
                _store.RecordRequestServed(token);
            }
        }

        [GeneratedRegex(@"^/google(?<token>[a-f0-9]+)\.html$")]
        private static partial Regex VerificationFileRoute();
    }
}
