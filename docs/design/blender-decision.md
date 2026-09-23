# Blender decision

**Decision: OUT**

Do not implement incremental reparse (`Blender`). The green tree stays blender-capable.

## Method

```
dotnet run --project benchmarks/Papyrus.CodeAnalysis.Benchmarks --configuration Release
```

- API: `SyntaxTree.Parse(SourceText, PapyrusDialect)`.
- Suite: committed `tests/Papyrus.CodeAnalysis.Tests/Fixtures/Parse/**/*.psc` (skyrim / fo4 / starfield).
- Timing: 5 warmup + 21 timed iterations per script; `Stopwatch.GetTimestamp`.
- Lines: newline-separated rows, including a last row without a trailing newline.
- RSS: `Process.WorkingSet64` after one retained parse of the suite. Process-wide (runtime, JIT, fixtures, trees). Also report `GC.GetTotalMemory` and the WorkingSet delta after retaining trees.
- Optional maintainer corpus: `--corpus`, `PAPYRUS_CORPUS`, `./corpus`, or the repo `corpus/` directory when that path contains `.psc` files. Default `dotnet run` uses committed fixtures only.

## Numbers

Recorded 2026-08-17, Release, .NET 10.0.5, Unix 26.5.2. Maintainer corpus not present.

| | |
|---|---|
| Scripts | 12 |
| Lines | min 3, **median 7**, mean 8.33, max 19 |
| Chars | min 44, median 111.5, mean 133.33, max 318 |
| Median of per-script median parse | **0.0110 ms** |
| Mean of per-script mean parse | 0.0152 ms |
| Max per-script median parse | **0.0301 ms** (`skyrim/properties-states.psc`, 19 lines) |
| One-pass parse of all 12 scripts | 0.2348 ms |
| Retained-tree WorkingSet64 | 49.2 MiB (process-wide) |
| WorkingSet64 delta (retain trees) | 0.1 MiB |
| Retained-tree GC heap | 0.4 MiB |

Every committed fixture parsed well under 1 ms.

Committed fixtures are smaller than the hypothesized vanilla median (low hundreds of lines). A linear scale of the 19-line max (~0.030 ms) to a few hundred lines still lands under 1 ms. Re-run with a maintainer corpus when one is available; revisit only if that measurement shows parse as the keystroke bottleneck.

## Rationale

A typical script reparses in well under 1 ms. Incrementality belongs in the symbol layer. The `Blender` stays possible (green tree) and is not built.
