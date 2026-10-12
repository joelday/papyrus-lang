# Contributing

## Branches

`main` is the only long-lived branch. Work happens on short topic branches and lands through pull requests. Every push to `main` is a preview: CI builds the VSIXs, the compiler and the language server, and publishes the VSIXs to the Marketplace **pre-release** channel as `<preview prefix>.<run number>` (the prefix is `PREVIEW_VERSION_PREFIX` at the top of `ci.yml`, currently `4.1`). Users on the release channel are never moved to a preview.

## CI

`.github/workflows/ci.yml` runs on every pull request and every push to `main`:

| Job | What it does |
| --- | --- |
| Managed | `dotnet build` and `dotnet test` of `Papyrus.sln` on Linux and Windows, plus the committed-binary check and the differential compliance run. |
| Server | NativeAOT publish of `papyrusc` and `Papyrus.LanguageServer` for win-x64, linux-x64, osx-x64 and osx-arm64, with a `--version` smoke test. |
| Extension | Type-check, lint and unit tests for `src/papyrus-lang-vscode`. |
| VSIX | One platform-specific VSIX per target, each bundling that platform's language server. |
| Native debug plugin | The C++ xSE plugin for Skyrim SE and Fallout 4. Runs only when native inputs changed (`src/DarkId.Papyrus.DebugServer`, `src/native`, `dependencies`, `vcpkg*.json`), when a release asks for it, or when triggered by hand with the `build_native` input. Built output is cached by input hash, so an unchanged plugin is never rebuilt. |

Public CI never has game files or Bethesda binaries. `papyrusc verify --compile` skips when `./corpus` is absent, and the binary check fails the build if a `.pex`, `PCompiler.dll` or `Antlr3*.dll` is ever committed.

## Releases

Releases are cut by hand from the Actions tab: run **Release** on `main` with a version `X.Y.Z`. The workflow builds everything through CI with that version, creates the `vX.Y.Z` tag and a GitHub release holding the VSIXs, the compiler archives and the debug plugin, then publishes the VSIXs to the Marketplace. Nothing in the repo needs editing for a release; `package.json` keeps the next intended version.

Releases use an even minor version (4.0.x, 4.2.x) and previews an odd one (4.1.x, 4.3.x), so the two channels never collide and the newest preview always sorts above the latest release. After cutting a release with a new minor, bump `PREVIEW_VERSION_PREFIX` in `ci.yml` to the next odd minor. Tick **prerelease** on a manual release only if you want a hand-cut pre-release; untick **publish** for a dry run that only creates a draft GitHub release.

## Commit messages

Use `<type>: <subject>` with one of feat, fix, docs, style, refactor, perf, test or chore. Release notes are generated from merged PR titles.
