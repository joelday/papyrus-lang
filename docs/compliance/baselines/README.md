# Compliance baselines

Tier-1 public CI compares **normalized SHA-256 hashes** of PEX. The repo never contains vanilla `.pex`, compiler binaries from the games, or game script source.

Two hash paths:

1. **Existing `.pex`.** `papyrusc hash` / `papyrusc verify --source <pex-dir> --baseline <file>` via `PexReader` + `PexNormalizer.Hash`. Does not compile.
2. **Compile vs baseline.** `papyrusc verify --compile` compiles each `.psc` with `PexEmitter` and compares the normalized hash to the committed baseline. **Skips (exit 0)** when the local game scripts are absent — public CI must not require game files.

Committed game baselines will live here later:

```
docs/compliance/baselines/skyrimse.json
docs/compliance/baselines/fallout4.json
docs/compliance/baselines/starfield.json
```

Public CI does **not** read those files. It hashes synthetic fixtures only and compares them to `tests/Papyrus.CodeAnalysis.Tests/fixtures/synthetic.hashes`.

## Compile vs baseline (tier 1)

```
papyrusc verify --compile --game skyrimse|fallout4|starfield \
  [--source ./game-scripts/<game>] \
  [--baseline docs/compliance/baselines/<game>.json] \
  [--allowlist docs/compliance/allowlist.md]
```

`--source` defaults to `./game-scripts/<game>`. If that tree has no `.psc` files, the command prints `allowlist size` and a skip message and exits 0.

`--flags` points at a `.flg`. If omitted, the harness uses `TESV_Papyrus_Flags.flg` / `Institute_Papyrus_Flags.flg` / `Starfield_Papyrus_Flags.flg` in the game scripts root when present (or the sole `.flg` there).

A baseline row is waived only when `allowlistId` names a construct in `docs/compliance/allowlist.md`, or the script is on that construct's explicit `scripts:` list. `status: allowlisted` is metadata and is not a waiver by itself.

Everything not on `docs/compliance/allowlist.md` must be byte-identical. Allowlisted scripts may differ (semantic equivalence only). The command prints the compliance number (`identical: N/M (P%)`) and the allowlist size.

## Tier 2 (CK compiler) — future

```
papyrusc verify --game <g> --ck-compiler <path> --source <dir>
```

Not implemented. That path would compile with both `papyrusc` and a **local** CK / PapyrusCompiler binary the user already has, then normalize and diff. Do not commit that binary. Until it ships, generate hashes from existing `.pex` (below) or from `--compile` against game scripts you acquired yourself.

## Generate hashes (maintainer, legitimate install)

1. Copy scripts from a game install you have a license to use:

   ```
   papyrusc scripts acquire --game skyrimse|fallout4|starfield --source <game-or-scripts-path>
   ```

2. Fill in the attestation template below. Hash the **CK compiler binary** (not its path). Never commit the binary.

   ```
   sha256sum "<CreationKit.exe or PapyrusCompiler.exe>"
   ```

   If you are hashing shipped `.pex` rather than CK emit, set `CK compiler path SHA-256` and `CK file version` to `n/a` and say so in the statement.

3. Write baselines. `--write-baselines` is **maintainer-only** and refuses to run without `--attestation`:

   ```
   papyrusc verify --write-baselines \
     --game skyrimse \
     --source ./game-scripts/skyrimse \
     --out docs/compliance/baselines/skyrimse.json \
     --attestation /path/to/attestation.txt
   ```

4. Open a PR that includes the JSON **and** the attestation (embedded in the JSON `attestation` object). `docs/compliance/**` is CODEOWNERS-protected (`.github/CODEOWNERS`). A PR that adds hashes without this note is rejected.

Hashes produced from a leaked compiler are a supply-chain incident, not a compliance win.

`papyrusc hash <file-or-dir>` prints normalized hashes without writing anything.

## Attestation template

Copy this into a text file and pass it as `--attestation`. Every field is required. `Statement` must include the word `legitimate`.

```
Machine OS: <OS name and version>
papyrusc version: <output of papyrusc --version, or auto>
CK compiler path SHA-256: <sha256 of the CK / PapyrusCompiler binary; not the path>
CK file version: <FileVersion of that binary, or n/a>
Statement: The CK binary / hashed .pex files came from a legitimate game/CK install that I have a license to use.
```

Do not put a filesystem path to the CK in the note.

## CODEOWNERS

`.github/CODEOWNERS` requires a maintainer review on `docs/compliance/**`.
