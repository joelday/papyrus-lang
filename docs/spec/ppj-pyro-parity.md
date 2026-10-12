# `.ppj` project files and Pyro

How the workspace reads a Papyrus project file, what it leaves to [Pyro](https://github.com/fireundubh/pyro), and how we keep the two from drifting apart.

**Pin:** Pyro `master` @ `82a6faf` (2024-08-15), schema `pyro/PapyrusProject.xsd`. Update this pin and the tables below whenever that schema changes (see the release checklist in `CONTRIBUTING.md`).

## Stance

Pyro stays a perfectly good build tool, and users keep using it if they want to. The extension no longer bundles or runs it. The workspace's only job with a `.ppj` is to reflect it: to know which scripts and imports belong to the project so the editor binds the right things. The workspace is **not** a Pyro re-implementation, and it does not try to reproduce Pyro's path resolution. Where the two disagree, Pyro is right about what Pyro builds, and the workspace says plainly what it did not do.

Two consequences:

1. Anything Pyro finds for you implicitly must be explicit for the editor. The workspace never consults the game install, the registry, or Creation Kit INIs.
2. Pyro-only content in a `.ppj` (packaging, zipping, anonymizing, their events) is skipped and **reported**, never silently dropped: `PapyrusProject.Ignored` lists it, the language server logs it on load, and `papyrusc` prints it as a note.

## What the workspace reads

| `.ppj` content | Workspace | Notes |
| --- | --- | --- |
| `Game` | Read | `sse`, `fo4`, `tesv`/`tes5`, `starfield`/`sf1`. Inferred from `Flags` when absent, as Pyro does. |
| `Flags` | Read | Also used for game inference. |
| `Output` | Read | Resolved relative to the `.ppj` directory. |
| `Optimize`, `Release`, `Final`, `Asm` | Read | Compiler options; honored by `papyrusc`. |
| `<Imports>` / `<Import>` | Read | Each becomes an include directory, resolved relative to the `.ppj` directory. HTTPS remotes are kept as strings and are **not fetched**. |
| `<Folders>` / `<Folder NoRecurse>` | Read | Source folders, same resolution. |
| `<Scripts>` / `<Script>` | Read | Explicit script list. |
| `<Variables>` / `<Variable Name Value>` | Parsed | Stored; `@Name` substitution is not applied by the workspace yet. |
| `${ENV}` tokens | Parsed | Left as written; `papyrusc` expands them at build time, the editor does not. |
| `<PreBuildEvent>`, `<PostBuildEvent>` (and the `<PreBuild>` / `<PostBuild>` aliases) | Parsed | Command text only; `papyrusc` runs them. `Description` and `UseInBuild` are not read. |

## Pyro-only content the workspace ignores

Listed on load. None of it changes which scripts are in scope, so ignoring it cannot make the editor wrong about a script; it only means the editor does not do Pyro's job.

| `.ppj` content | Why it is Pyro's |
| --- | --- |
| `Anonymize`, `Package`, `Zip` attributes | Post-build steps on compiled output. |
| `<Packages>` / `<Package>` / `<Include>` / `<Match>` | BSA/BA2 packaging. |
| `<ZipFiles>` / `<ZipFile>` | Zip packaging. |
| `<PreImportEvent>`, `<PostImportEvent>`, `<PreCompileEvent>`, `<PostCompileEvent>`, `<PreAnonymizeEvent>`, `<PostAnonymizeEvent>`, `<PrePackageEvent>`, `<PostPackageEvent>`, `<PreZipEvent>`, `<PostZipEvent>` | Hooks into Pyro's pipeline stages. Only the build events have an equivalent in `papyrusc`. |

## Known resolution differences

These are the places where "the editor sees X" and "Pyro builds X" can differ for the same file. They are the parity risk, and the reason the workspace says what it ignored.

| Pyro does | Workspace does |
| --- | --- |
| Inserts the `.ppj` directory as the first import path, then the listed imports. | Uses the listed imports and folders. The project directory itself is not an implicit import. |
| Locates the game install through the Windows registry (or `--game-path`) for the flags file and base scripts. | Never looks for the game. Base-game sources come in only through an explicit `<Import>`. |
| Clones GitHub, Bitbucket and Gitea remotes into a cache and adds them as import paths. | Keeps the URL as a string. Nothing is fetched. |
| Expands `@Variables` and `${ENV}` in attributes and paths before resolving them. | Stores both unexpanded. `papyrusc` expands `${ENV}`; the editor expands neither. |
| Resolves `<Folder>` paths against import paths as well as the project directory. | Resolves against the project directory only. |

## Planned: Pyro as an alternative source of truth

Rather than chase Pyro's resolution rules, let Pyro do the resolving for users who build with it. Pyro's existing `--resolve-project` flag prints the project XML with variables and paths substituted (`PapyrusProject.py`, after `_update_attributes`) and exits. That output is a plain `.ppj` the workspace already parses.

Proposed shape:

- A setting, `papyrus.project.resolver`: `builtin` (default) or `pyro`, plus `papyrus.pyro.path`.
- With `pyro`, the language server runs `pyro <project.ppj> --resolve-project --log-level debug` on load and on change, captures the resolved XML from the output, and feeds it to `PpjParser` instead of the file on disk. Failures fall back to the built-in parse with a logged message.
- The same resolved document can be handed to `papyrusc` with a `--resolved-from-pyro` style option for people who want our compiler but Pyro's project semantics.

What `--resolve-project` does not cover today: remote imports are not cloned and the game path is not applied, because Pyro exits before `try_populate_imports` and `try_set_game_path`. Closing that means a small upstream change to Pyro, a JSON dump of `import_paths`, `psc_paths` and the game path after population, which is worth proposing once the first step is in. Pyro is MPL-2.0; running it as a separate process is fine, bundling it again is not on the table.
