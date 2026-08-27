# Corpus

The compiler corpus is a local copy of **your** game scripts. This project does not redistribute Bethesda source or binaries.

## Rules

- **User-owned.** Copy scripts from a game install you have a license to use.
- **Gitignored.** `/corpus/` is ignored. Never commit `.psc` or `.pex` from the games.
- **Required later for compliance.** Round-trip, bind, and emit work against this tree.
- **Public CI uses hashes only.** Baselines live under `docs/compliance/baselines/`. No corpus source, no game output, and no game compiler in public CI. `papyrusc verify --compile` skips (exit 0) when this tree is absent.

Nothing here is downloaded. `papyrusc corpus acquire` only copies files from a path on disk.

## Acquire

```
papyrusc corpus acquire --game skyrimse|fallout4|starfield --source <path> [--dest <path>] [--force]
```

`--source` is a game install root or a scripts directory (typically `Data/Scripts` and `Data/Scripts/Source`). Only `.psc` and `.pex` files are copied.

`--dest` defaults to `./corpus/<game>`. If the destination is inside a git worktree (a `.git` directory or file is found walking parents), the command refuses unless you pass `--force`. The default path is gitignored, but `--force` is still required when you acquire into this repository.
