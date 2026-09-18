# Crumpled.VerifyOwnership

Google Search Console site-ownership verification for Umbraco.

Google's HTML-file verification method normally requires uploading a static file to your site's root and
leaving it there forever. This package serves that file automatically instead - configure one or more
verification codes and `/google<code>.html` starts responding immediately, with no file to deploy or forget
about. A Settings-section backoffice panel lets admins add or remove codes at any time, without a redeploy.

<img src="icons/icon-verifyownership.png" width="120" height="120" alt="">

## Installation

```bash
dotnet add package Crumpled.VerifyOwnership
```

Configure under `Crumpled:VerifyOwnership` in `appsettings.json` (optional - codes can also be managed
entirely from the backoffice panel, which is seeded from this list the first time it runs):

```json
{
  "Crumpled": {
    "VerifyOwnership": {
      "VerificationCodes": ["1a2b3c4d5e6f7890"]
    }
  }
}
```

## How it works

Google's [HTML-file verification method](https://support.google.com/webmasters/answer/9008080) asks you to
host a file named `google<code>.html` at your site's root, containing the single line
`google-site-verification: google<code>.html`. This package's middleware intercepts requests matching that
pattern and, if `<code>` is one of the configured verification codes, responds with the exact expected body
- nothing needs to be added to `wwwroot`.

## Backoffice UI

A "Site Verification" panel appears under **Settings** listing the currently configured codes, with the
ability to add or remove them. Changes take effect immediately (no restart needed) - the middleware reads
the current list on every request.

## Requirements

- Umbraco CMS 17 or 18

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the branching strategy and formatting requirements.

## License

[MIT](LICENSE)
