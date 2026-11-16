# pyro-dotnet changes for papyrus-lang

papyrus-lang will use `LetsPlayWithFire.Pyro` as its **only** way of resolving `.ppj` and `.pyroproject` files, behind a thin adapter in the language server and the `papyrusc` compiler. Our own resolver is being deleted, not kept as a fallback. So Pyro has to work well for a host that runs for a long time, is compiled with Native AOT, runs on several platforms, and reloads projects while the user is mid-edit. papyrus-lang is moving to `net11.0`, so no multi-targeting is needed.

If you only take on a few of these first: items 1 and 3 block the editor outright, and 2 blocks shipping it. Everything else we can work around for a while.

## Blockers

1. **Paths that work on macOS and Linux.** `Paths.cs` imitates Python's Windows path module (`ntpath`): it swaps `/` for `\`, and only `C:\…` counts as absolute. The editor runs on macOS and Linux, where `/home/me/mod` gets joined onto the project directory. Asks:
   - Use the host OS's path rules when touching the file system, but still accept `\` in `.ppj` text, since project files are written on Windows.
   - Treat Windows drive paths on other OSes as missing rather than an error.
   - Run CI on ubuntu and macos too, not just `windows-latest`.
2. **Native AOT.** Set `IsAotCompatible=true`, with no trim or AOT warnings. Likely areas: `XmlSchemaSet`, `XDocument`, `PexFiles.Dump` JSON (switch to a source-generated context), and `Regex` (already `[GeneratedRegex]`, which is good).
3. **A lenient load mode.** For example `BuildOptions.Tolerant` or a separate `TryLoad`. It returns a best-effort project plus a list of diagnostics, instead of throwing, for:
   - an import directory that doesn't exist (`Import path does not exist`): common when a `.ppj` names a Windows game install and the editor is on a Mac
   - no imports (`Failed to build list of import paths`) and empty folders (`Failed to build list of script paths`)
   - an unknown game type (return an empty `GameType`)
   - an unknown `@Variable` (leave it as written and report it)
   - a remote fetch that fails or happens offline (use the cache if there is one, and report it)
   - invalid XML or a schema violation (return what could be read)

   Strict mode stays the default for the CLI.

4. **Structured diagnostics.** Each one should carry a stable code, severity, message, file, and a line and column taken from the line info Pyro already records. Expose them through the lenient result. Strict mode can keep throwing `ProjectException`, but with the same data attached.
5. **Async loading with cancellation**, with remote fetches truly async. Projects reload on every save, and a reload that's been superseded must cancel any fetch still running.
6. **Let the host supply HTTP.** Accept an `HttpClient` or `HttpMessageHandler` from the host. The current static client sets `UseProxy = false`, which breaks users behind corporate proxies.
7. **Settings that are tied to where Pyro is installed.** `ProgramDirectory()` is `AppContext.BaseDirectory`, which for a host means the host's own folder. Let the host set:
   - the program path, which feeds `PROGRAM_PATH` and the safety check in `ClearRemoteCache`
   - the `.secrets` path, or a token-resolver callback
   - the remote cache folder (`RemoteTempPath` already does this; just document it as the way a host chooses)

## Needed by the adapter (public API)

8. **Where each import path came from.** For each entry in `ImportPaths`: the text as written, local or remote, the remote URL and cache path, and fetch state (cached, fetched, skipped, failed). For each folder: path as written, resolved path, and `NoRecurse`.
9. **Documented ordering and contents.** Make it part of the API that:
   - `ImportPaths` is the project directory first, then imports in document order, with duplicates removed and folders not included
   - `Scripts` holds only the project's own scripts (from folders and `<Script>`), never scripts that come from imports
   - object names use `:` between namespace parts, as `FixNamespace` produces

   We'll write tests that rely on these.

10. **Files the result depends on**, so the host knows what to watch. These are the project file, the flags file, every local folder and import directory, and the remote cache folders.
11. **Make these public:**
    - `CompilerSwitches`
    - `MissingScripts`
    - build events (name, description, `UseInBuild`, commands)
    - `Packages` / `ZipFiles`, as read-only data. The editor shows them but doesn't act on them.
12. **The resolved flags path.** Expose the flags file Pyro actually picked, after its import-path search. Right now `FlagsPath` holds whatever was written.
13. **Load from text.** Something like `Load(string projectPath, TextReader content, …)` to resolve a `.ppj` with unsaved edits. The path is still used for relative paths.

## Behavior

14. **Safe to call from several threads at once.** One load mustn't leak into another through shared state. `UNIXTIME` and `PROGRAM_PATH` should be worked out per call.
15. **No surprise Windows lookups.** On other OSes, registry and installed-game lookups should do nothing and never throw. Let the host turn them off on every OS, because the editor only uses what the `.ppj` says explicitly.
16. **Fallout 76.** Decide whether `fo76` is accepted, even if it's only resolved and never built. If not, lenient mode should return the paths with an empty game type.
17. **Schema loaded once.** Load the embedded XSD into one shared `XmlSchemaSet` instead of on every load.

## Packaging

18. **NuGet:** publish with SemVer, Source Link, `.snupkg` symbols, and XML docs. Until then, CI-built `.nupkg` files as GitHub release assets would let us use a local feed.
19. **Public API tracking:** use `PublicAPI.Shipped.txt` via `Microsoft.CodeAnalysis.PublicApiAnalyzers`, so changes that break hosts show up in review.
