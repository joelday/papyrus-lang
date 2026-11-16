# Contributing

## Repository layout

| Path | What it is |
| --- | --- |
| `src/Papyrus.CodeAnalysis` | Syntax trees, binder, diagnostics and PEX emit. The compiler proper. |
| `src/Papyrus.Pex` | PEX reader, writer and disassembler. |
| `src/Papyrus.Workspaces` | Projects and solutions: `.ppj` loading through Pyro, imports, incremental workspace state. |
| `external/pyro-dotnet` | [Pyro .NET](https://github.com/fireundubh/pyro-dotnet), as a submodule. It resolves `.ppj` files. |
| `src/Papyrus.LanguageServer` | The LSP server the extension bundles (NativeAOT). |
| `src/Papyrus.Compiler` | `papyrusc`, the command-line compiler (NativeAOT). |
| `src/papyrus-lang-vscode` | The VS Code extension. |
| `src/DarkId.Papyrus.DebugServer`, `src/native` | The native xSE debug plugin for Skyrim SE, Fallout 4 and Starfield. |
| `tools/Papyrus.Compliance` | Development tool that checks `papyrusc` output against the Creation Kit compiler. Not shipped. |
| `tests`, `benchmarks` | xUnit tests and parser benchmarks. |

## Building

Requires the .NET SDK pinned in `global.json` and Node 22. Clone with `--recurse-submodules`, or run `git submodule update --init` in an existing clone.

```
dotnet build Papyrus.sln -c Release
dotnet test Papyrus.sln -c Release
cd src/papyrus-lang-vscode && npm ci && npm run build-server && npm run compile
```

`npm run build-server` publishes the NativeAOT language server for your machine into `bin/<rid>/`, where the extension looks for it. Press F5 in VS Code with `src/papyrus-lang-vscode` open to run the extension in a development host.

The native debug plugin (`DarkId.Papyrus.DebugServer.sln`) builds on Windows with VS 2022 and vcpkg; see [its README](src/DarkId.Papyrus.DebugServer/README.md).

## papyrusc

```
papyrusc compile <script.psc|project.ppj> [--game <skyrimse|skyrimle|fallout4|starfield>] [--output <path>] [--flags <file.flg>] [--import <dir>]...
```

Given a `.ppj`, `papyrusc` loads it with Pyro, compiles the scripts Pyro selects, and runs the import, build and compile events whose `UseInBuild` is on, in Pyro's order. `papyrusc --help` lists the rest of the options.

## Language reference

Where the Creation Kit's behavior isn't documented, the grammar, type conversions and PEX layout follow [Caprica](https://github.com/Orvid/Caprica) at `e4dee08`, with the Creation Kit compiler as the final authority when they disagree. The places where we deliberately differ from the Creation Kit are listed in [`tools/Papyrus.Compliance/allowlist.md`](tools/Papyrus.Compliance/allowlist.md).

## Comparing with the Creation Kit

`tools/Papyrus.Compliance` (`papyrus-compliance`) compiles the vanilla game scripts and compares the normalized PEX hashes with Creation Kit output. It needs a local copy of the game scripts, which stay out of the repository under the gitignored `./game-scripts/<game>`:

```
dotnet run --project tools/Papyrus.Compliance -- scripts acquire --game skyrimse --source "<Skyrim install>"
dotnet run --project tools/Papyrus.Compliance -- verify --compile --game skyrimse
```

`verify --champollion` instead round-trips `.pex` through the [Champollion](https://github.com/Orvid/Champollion) decompiler. Both skip with exit code 0 when the game scripts are absent, which is how CI runs them.

## Project files and Pyro

`.ppj` files are resolved by [Pyro .NET](https://github.com/fireundubh/pyro-dotnet) (`LetsPlayWithFire.Pyro`), built from the `external/pyro-dotnet` submodule on our `papyrus-lang` branch of that repository. Fixes papyrus-lang needs go to that branch first and to fireundubh as pull requests. Switch the reference to the NuGet package once one is published.

`PapyrusProject.Load` calls `Projects.TryLoad`, so a problem in the file becomes a diagnostic rather than an exception. It wraps the result: the import paths (the project folder first, then the imports in order), the folders, the scripts Pyro would compile, the flags file Pyro found, the build events, and Pyro's diagnostics. Variables, environment variables, game detection, folder and script matching, and the flags search all follow Pyro. Nothing is re-implemented here. The installed-game registry lookup is turned off, so a project names its game with `Game` or its flags file.

`PyroInstall` finds the user's Pyro: the `papyrus.pyro.path` setting (passed to the server as `PAPYRUS_PYRO`), `papyrusc --pyro`, then the PATH. Its folder becomes Pyro's program path, so remote imports use that Pyro's `remote` cache and its `.secrets` token, exactly as a Pyro build would. With a token, Pyro downloads missing remotes while the project loads. Without one, they are reported, the language server watches the Pyro folder, and it reloads the project when the downloads appear. With no Pyro at all, the load is a dry run: remote imports are skipped, the server sends `papyrus/projectNeedsPyro`, and the extension offers to get or locate Pyro.

## Branches

`main` is the only long-lived branch. Work happens on short topic branches and lands through pull requests. Every push to `main` is a preview: CI builds the VSIXs, the compiler and the language server, and publishes the VSIXs to the Marketplace **pre-release** channel as `<year>.<month><day>.<run number>`, for example `2026.1012.10`. Users on the release channel are never moved to a preview.

## CI

`.github/workflows/ci.yml` runs on every pull request and every push to `main`:

| Job | What it does |
| --- | --- |
| Managed | `dotnet build` and `dotnet test` of `Papyrus.sln` on Linux and Windows, a check that no game binaries are committed, and the Creation Kit comparison (which skips without game scripts). |
| Server | NativeAOT publish of `papyrusc` and `Papyrus.LanguageServer` for win-x64, linux-x64, osx-x64 and osx-arm64, with a `--version` smoke test. |
| Extension | Type-check, lint and unit tests for `src/papyrus-lang-vscode`. |
| VSIX | One platform-specific VSIX per target, each bundling that platform's language server. |
| Native debug plugin | The C++ xSE plugin for Skyrim SE and Fallout 4. Runs only when native inputs changed (`src/DarkId.Papyrus.DebugServer`, `src/native`, `dependencies`, `vcpkg*.json`), when a release asks for it, or when triggered by hand with the `build_native` input. Built output is cached by input hash. |

## Releases

Releases are cut by hand from the Actions tab: run **Release** on `main` with a version `X.Y.Z`. The workflow builds everything through CI with that version, creates the `vX.Y.Z` tag and a GitHub release holding the VSIXs, the compiler archives and the debug plugin, then publishes the VSIXs to the Marketplace. Nothing in the repo needs editing for a release; `package.json` keeps the next intended version.

Releases are plain `X.Y.Z` and previews are date-based, so the two channels never collide and every preview sorts above every release. VS Code moves users to the highest version in their channel, so pre-release users stay on previews until they switch channels, and nothing has to be bumped after a release. Tick **prerelease** on a manual release only if you want a hand-cut pre-release; untick **publish** for a dry run that only creates a draft GitHub release.

## Commit messages

Use `<type>: <subject>` with one of feat, fix, docs, style, refactor, perf, test or chore. Release notes are generated from merged PR titles.
