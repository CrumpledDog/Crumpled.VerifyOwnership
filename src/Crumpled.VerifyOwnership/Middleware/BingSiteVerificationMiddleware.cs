using System.Xml.Linq;
using Crumpled.VerifyOwnership.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Crumpled.VerifyOwnership.Middleware
{
    /// <summary>
    /// Serves the Bing Webmaster Tools XML-file verification method automatically: a request for the exact
    /// path <c>/BingSiteAuth.xml</c> gets a shared XML file listing every currently configured verification
    /// id at once - unlike Google's one-file-per-id method, this file is not meaningfully "about" any single
    /// id, so no id is matched or captured from the request.
    /// </summary>
    public class BingSiteVerificationMiddleware : IMiddleware
    {
        private const string FilePath = "/BingSiteAuth.xml";

        private readonly IBingSiteVerificationStore _store;
        private readonly ILogger<BingSiteVerificationMiddleware> _logger;

        public BingSiteVerificationMiddleware(IBingSiteVerificationStore store, ILogger<BingSiteVerificationMiddleware> logger)
        {
            _store = store;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            // Exact, case-sensitive match - some hosts treat file paths case-sensitively, so this deliberately
            // does not use OrdinalIgnoreCase (mirrors GoogleSiteVerificationMiddleware's case-sensitive regex).
            if (!string.Equals(context.Request.Path.Value, FilePath, StringComparison.Ordinal))
            {
                await next(context);
                return;
            }

            var entries = _store.GetEntries();
            if (entries.Count == 0)
            {
                // No configured ids at all - nothing to serve. Deliberately not an empty <users></users>
                // document; falling through lets a real static file (or a 404) take over as normal.
                await next(context);
                return;
            }

            var usersElement = new XElement("users", entries.Select(entry => new XElement("user", entry.Id)));
            var body = "<?xml version=\"1.0\"?>" + usersElement.ToString(SaveOptions.DisableFormatting);

            context.Response.ContentType = "text/xml";
            await context.Response.WriteAsync(body);

            var userAgent = context.Request.Headers.UserAgent.ToString();

            // User-Agent is a diagnostic hint only, logged for anyone who wants a full audit trail - it is
            // trivially spoofable and must never be treated as a security signal. Unlike Google's own
            // documentation (which more directly links Googlebot's identity to fetching verification files),
            // Bing's docs do not confirm that bingbot specifically is what fetches BingSiteAuth.xml - this
            // check is an even less certain heuristic than the equivalent one in
            // GoogleSiteVerificationMiddleware, and should be read that way.
            _logger.LogInformation(
                "Served BingSiteAuth.xml ({Count} user id(s)) to User-Agent {UserAgent}.",
                entries.Count,
                userAgent);

            if (userAgent.Contains("bing", StringComparison.OrdinalIgnoreCase))
            {
                _store.RecordFileServed();
            }
        }
    }
}
