# Game scripts

The game scripts are a local copy of the scripts from **your** game install. This project does not redistribute Bethesda source or binaries.

## Rules

- **User-owned.** Copy scripts from a game install you have a license to use.
- **Gitignored.** `/game-scripts/` is ignored. Never commit `.psc` or `.pex` from the games.
- **Required later for compliance.** Round-trip, bind, and emit work against this tree.
- **Public CI uses hashes only.** Baselines live under `docs/compliance/baselines/`. No game script source, no game output, and no game compiler in public CI. `papyrusc verify --compile` skips (exit 0) when this tree is absent.

Nothing here is downloaded. `papyrusc scripts acquire` only copies files from a path on disk.

## Acquire

```
papyrusc scripts acquire --game skyrimse|fallout4|starfield --source <path> [--dest <path>] [--force]
```

`--source` is a game install root or a scripts directory (typically `Data/Scripts` and `Data/Scripts/Source`). Only `.psc` and `.pex` files are copied.

`--dest` defaults to `./game-scripts/<game>`. If the destination is inside a git worktree (a `.git` directory or file is found walking parents), the command refuses unless you pass `--force`. The default path is gitignored, but `--force` is still required when you acquire into this repository.

## Champollion round-trip (tier 3)

```
papyrusc verify --champollion [--source <path>] [--game <game>] [--champollion-path <bin>]
```

Decompile `.pex` with a Champollion executable, recompile with `papyrusc --champollion-compat`, compare normalized hashes. `--source` defaults to `./game-scripts/<game>` or `./game-scripts`. Skips (exit 0) when Champollion or the game scripts are missing. Binary path: `--champollion-path`, `PAPYRUSC_CHAMPOLLION`, `PATH`, or `tools/Champollion`. See `docs/spec/README.md`.
