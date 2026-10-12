# Copilot / Agent Instructions — Crumpled.VerifyOwnership

Google Search Console site-ownership verification for Umbraco. See [README.md](../README.md) for the
feature overview and [CONTRIBUTING.md](../CONTRIBUTING.md) for the full branching strategy.

## Cross-Branch Workflow (v1 → v2) — Check This Every Time

**Whenever you commit and push a change to `develop/v1`, before considering the task complete: stop and decide
whether it also belongs on `develop/v2`.** The default answer is yes — nearly everything is shared code, docs,
workflows and tests. Skip only for something genuinely Umbraco-17-only. Concretely: `cd` into the `develop/v2`
worktree (`../Crumpled.VerifyOwnership-v2`), `git merge develop/v1` (never cherry-pick), rebuild if the merge
touched `Client/src/**` (`npm run generate-client && npm run build` against a running v2 DemoSite), push
`develop/v2`, wait for CI, then fast-forward `release/v2` only when actually cutting a v2 release.

Always push `develop/v1` before `develop/v2`, and validate any edited workflow YAML with a parser before pushing —
a malformed `.github/workflows/*.yml` is rejected outright and silently skips CI on every branch it reaches.

## Repo shape

```
src/
  Crumpled.VerifyOwnership/            - the whole package: options, store, middleware, Management API,
                                          Composers, and the embedded Client/ (Lit/TS backoffice UI)
  Crumpled.VerifyOwnership.DemoSite/   - minimal Umbraco site (17 on v1, 18 on v2), CI-only fixture (no starter kit)
tests/
  Crumpled.VerifyOwnership.Tests/
```

Single packable project, generated from Umbraco's own `umbraco-extension` dotnet template (not a
hand-rolled Core/Client split) - the Client folder lives inside the same `.csproj`, not a separate one.

## Two Umbraco majors, one repo

`develop/v1`/`release/v1` (Umbraco 17, package `1.x`) is where all development happens. `develop/v2`/`release/v2`
(Umbraco 18, package `2.x`) only ever receives merges *from* v1 — never branch features directly against v2,
never cherry-pick. Work on v2 in its own git worktree (`../Crumpled.VerifyOwnership-v2`), not by switching
branches in the v1 clone, so `node_modules`, `bin`/`obj` and the local SQLite database never mix between majors.
Run `git config merge.ours.driver true` once per clone/worktree. See CONTRIBUTING.md's "Umbraco 17/18 branch
lines" section for the full workflow and which files are marked `merge=ours` in `.gitattributes`
(`Directory.Packages.props`, `Directory.Build.props`, `.releaserc`, `Client/package.json` + lockfile).

`.github/workflows/*.yml` are **shared**, not `merge=ours` — change them on `develop/v1` and merge forward, and
keep anything that touches an Umbraco-version-specific URL working on both majors (e.g. the health check polls
both `/umbraco/swagger/<name>/swagger.json` and `/umbraco/openapi/<name>.json`). The v2 line's first commit carries
a `BREAKING CHANGE:` footer so semantic-release starts it at 2.0.0 rather than continuing 1.x.

The one genuine code-level difference between majors (Swashbuckle vs native OpenAPI document registration)
lives in `Composers/OpenApiRegistration.Umbraco17.cs`/`.Umbraco18.cs`, conditionally compiled via
`$(UmbracoTargetMajor)` from the repo-root `Directory.Build.props`. Everything else in the Composer/
Controllers/Middleware/Services/Options is version-agnostic and merges cleanly across the two branches.

## Formatting

`dotnet format --verify-no-changes` must pass in CI. Run `dotnet format` before committing.

## API changes & codegen

If you change `VerifyOwnershipConfigController` (routes, request/response models), regenerate the
TypeScript client: start `Crumpled.VerifyOwnership.DemoSite` (`dotnet run`, `https://localhost:44499`),
then `cd src/Crumpled.VerifyOwnership/Client && npm run generate-client`. Never hand-edit `src/api/*.gen.ts`
- it's gitignored and CI always regenerates it. Never use raw `fetch()` in Client code - always the
generated SDK functions from `src/api/index.js`.

## Versioning

No hardcoded versions in the `.csproj` — semantic-release computes the version from Conventional Commits
and injects it at pack time via `/p:PackageVersion=`. Don't hand-edit version numbers anywhere.
