# Contributing

## Formatting

```bash
# Format all C# code (REQUIRED before committing)
dotnet format

# Verify formatting (used in CI)
dotnet format --verify-no-changes
```

All PRs must pass `dotnet format --verify-no-changes` in CI or they will be rejected.

### Anti-Patterns to Avoid

- Don't commit code that fails `dotnet format`
- Don't use real credentials or organization names in examples/docs
- Don't add features without updating documentation
- Don't use tabs for indentation
- Don't ignore logger warnings

## Branching Strategy

This project uses Git Flow with version-specific branches, one pair per supported Umbraco major version.

### Main Branches

- **`develop/v1`** - Main development branch for Umbraco 17 (protected, default branch)
- **`release/v1`** - Stable release branch for production releases (protected)
- **`beta/v1`** - Beta releases (protected, created only once actually needed)

### Supporting Branches

- **`feature/v1/<feature-name>`** - New features
  - Branch from: `develop/v1`
  - Merge back into: `develop/v1`
  - Example: `feature/v1/add-bing-verification`

- **`hotfix/v1/<fix-name>`** - Urgent production fixes
  - Branch from: `release/v1`
  - Merge back into: `release/v1` and `develop/v1`
  - Example: `hotfix/v1/fix-token-regex`

### Branching Workflow

```bash
# Create a feature branch
git checkout develop/v1
git pull origin develop/v1
git checkout -b feature/v1/my-new-feature

# Work on your feature, commit regularly
git add .
git commit -m "feat: add new feature"

# Push your branch
git push -u origin feature/v1/my-new-feature

# Create a pull request to develop/v1
```

### Umbraco 17/18 branch lines (v1/v2) and git worktrees

This repo is designed to eventually support two Umbraco CMS majors from parallel branch lines, once Umbraco
18 support is needed:

- `develop/v1` / `release/v1` — targets Umbraco CMS 17. **All feature and bugfix development happens here.**
- `develop/v2` / `release/v2` — targets Umbraco CMS 18, added later. This line is merge-only — never branch
  a feature or hotfix directly off it; changes only arrive there via `git merge develop/v1` (never
  `git cherry-pick`, so the same change doesn't get re-flagged as a conflict on every later merge).

Switching between `develop/v1` and `develop/v2` with a plain `git checkout` in one working copy leaves stale
artifacts behind: `node_modules` installed against the wrong `@umbraco-cms/backoffice` version, `bin`/`obj`
build output from the other major, and a local runtime database migrated by the wrong Umbraco major. A
[git worktree](https://git-scm.com/docs/git-worktree) avoids this entirely. Once `develop/v2` exists, run
this from inside the `develop/v1` clone:

```bash
# <path> can be any folder that doesn't already exist - a sibling directory
# next to your existing clone is the usual convention, e.g. ../Crumpled.VerifyOwnership-v2
git worktree add <path> develop/v2
```

`cd` into `<path>` to work on `develop/v2` from then on — its own `npm install`, `dotnet build`, and local
database stay completely independent of your `develop/v1` working copy.

Useful commands:
```bash
git worktree list           # see every worktree and which branch it has checked out
git worktree remove <path>  # remove one you no longer need (must be clean - commit or stash first)
```

A handful of files are intended to always diverge between the two branch lines (see `/.gitattributes`
`merge=ours` entries): `Directory.Packages.props` (the `Umbraco.Cms.*` package version pins),
`Directory.Build.props` (`<UmbracoTargetMajor>`), `.releaserc` (each branch line's own semantic-release
config), and `Client/package.json`/`package-lock.json` (the `@umbraco-cms/backoffice` version pin and the
`generate-client` script's hardcoded OpenAPI discovery URL - Umbraco 17 exposes it at
`/umbraco/swagger/<name>/swagger.json`, Umbraco 18 at `/umbraco/openapi/<name>.json`). Run
`git config merge.ours.driver true` once per clone/worktree for this to take effect. The one genuine
code-level difference between the two majors -
OpenAPI document registration (Umbraco 17 uses Swashbuckle, Umbraco 18 uses ASP.NET Core's native OpenAPI) -
is isolated into `Composers/OpenApiRegistration.Umbraco17.cs`/`.Umbraco18.cs`, both present in the source
tree on both branches but conditionally excluded from compilation via `$(UmbracoTargetMajor)` in the
`.csproj`. If a merge from `develop/v1` into `develop/v2` hits a genuine breaking-API conflict elsewhere
(not just a version-range bump), resolve it file-by-file rather than introducing more conditional
compilation — only reconsider that approach if conflicts become frequent.

## Commit Messages

This project uses [Conventional Commits](https://www.conventionalcommits.org/) for automated versioning and
changelog generation via semantic-release.

Format: `<type>[optional scope]: <description>`

Common types: `feat` (minor bump), `fix` (patch bump), `chore`/`refactor`/`style`/`test`/`build`/`ci`/`docs`/`perf`/`deps`
(patch bump). A `BREAKING CHANGE:` footer (or `!` after the type) triggers a major bump.
