# Compliance baselines

Tier-1 public CI compares **normalized SHA-256 hashes** of PEX. The repo never contains vanilla `.pex`, Bethesda compiler binaries, or corpus source.

There is no compiler yet. Today the harness hashes **existing** `.pex` via `PexReader` + `PexNormalizer.Hash`. It does not compile and it does not download Bethesda files.

Committed game baselines will live here later:

```
docs/compliance/baselines/skyrimse.json
docs/compliance/baselines/fallout4.json
docs/compliance/baselines/starfield.json
```

Public CI does **not** read those files. It hashes synthetic fixtures only and compares them to `tests/Papyrus.CodeAnalysis.Tests/fixtures/synthetic.hashes`.

## Generate hashes (maintainer, legitimate install)

1. Copy scripts from a game install you have a license to use:

   ```
   papyrusc corpus acquire --game skyrimse|fallout4|starfield --source <game-or-scripts-path>
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
     --source ./corpus/skyrimse \
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
