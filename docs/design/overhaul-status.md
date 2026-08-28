# Overhaul implementation status

One-stop tracker for the clean-slate compiler and language-service rewrite specified in [`compiler-and-language-service-overhaul.md`](compiler-and-language-service-overhaul.md).

**Where the work lives:** local branch `rewrite` (not pushed). Topic branches are created from that integration point and merged back manually when a slice is complete. There is intentionally no configured remote. The shipping VS Code extension is still the old OmniSharp / PCompiler host.

```
git checkout rewrite
dotnet build Papyrus.sln -c Release
```

**Score:** 74 of 88 plan items merged or implemented locally. **14 remaining.**

**Hard constraints for this run**

- Local commits and local branches only. Do not push remotes, open GitHub PRs, or publish to nuget.org.
- Do not put vendor names on artifacts.
- `papyrusc compile` is the first user-facing binary. Nothing ships to Marketplace users until emit meets the compliance bar, then one new-major VSIX cutover (P4.06b).

---

## What you can do on this branch today

`papyrusc` (from `src/Papyrus.Compiler`) compiles Papyrus without Bethesda’s `PCompiler.dll`:

```
papyrusc compile <script.psc|project.ppj> [--game skyrimse|skyrimle|fallout4|starfield]
papyrusc corpus acquire --game <game> --source <path>
papyrusc hash <path>
papyrusc verify --compile --game skyrimse
papyrusc verify --champollion
papyrusc --version
```

`--game skyrimle` is supported. `.ppj` builds expand `${ENV}` and run pre/post steps. `--ignore-engine-limits` downgrades `PAP20xx` to warnings. Public CI never needs game files: `verify --compile` skips with exit 0 when `./corpus/<game>` is absent.

The new `Papyrus.LanguageServer` process speaks StreamJsonRpc with a mutating/non-mutating request scheduler and text-document sync. Semantic tokens, folding, selection ranges, document symbols, `textDocument/syntaxTree`, and indent-only formatting are wired. The shipping extension does **not** launch this process.

---

## Done (72)

### Phase 0 — Foundations and PEX oracle (P0.00–P0.12)

Solution layout, CPM, architecture/AOT bans, `Papyrus.Pex` reader/writer/normalizer/PAS/PexReflector, corpus acquire, tier-1 hash harness, spec stubs. `Papyrus.Pex` 0.x pack metadata exists; nuget.org push is disabled.

### Phase 1 — Parser (P1.01–P1.10)

Syntax generator, green/red trees, lexer, script/decls/FO4+/Starfield/statements/expressions, trivia + recovery, dialect tables, `.flg` / `.ppj` parsers, round-trip + prefix + mutation tests, parse benchmarks.

### Phase 2 — Binder (P2.00–P2.11)

`PapyrusWorkspace`, `PapyrusName`, symbol API + PexReflector adapter, `PapyrusCompilation`, binder (members, signatures, K23), assignability, `as`/`is`/`Var`/arrays, diagnostic catalog, `SemanticModel`, dependency-tracked invalidation, dialect binding + negative corpus, observability. No LSP handlers in this phase.

### Phase 3 — Emit + CLI (all except P3.12)

| ID     | Title                                                                |
| ------ | -------------------------------------------------------------------- |
| P3.01  | Bound tree                                                           |
| P3.02  | Lowering                                                             |
| P3.03a | CFG                                                                  |
| P3.03b | Temp pooling + PEX emit (`PexFunctionBuilder`, `PexEmitter`)         |
| P3.03c | Optimizer (Caprica starting rules, default off)                      |
| P3.04  | Engine-limit diagnostics (`PAP20xx`)                                 |
| P3.05  | `papyrusc compile` (single file; `--game skyrimle`)                  |
| P3.05b | NativeAOT smoke of `papyrusc` on `windows-latest` / win-x64          |
| P3.06a | Native `.ppj` builds                                                 |
| P3.06b | `${ENV}` + pre/post build (CLI default on)                           |
| P3.07  | Differential compliance + allowlist (`docs/compliance/allowlist.md`) |
| P3.09  | Debug-info emit                                                      |
| P3.10  | Champollion tier-3 (optional; skips if binary/corpus missing)        |
| P3.11  | Encoding contract tests (Windows-1252 string tables)                 |

**Phase 3 complete,** including P3.12 pack metadata for `Papyrus.CodeAnalysis` 0.x (nuget.org push still disabled). Cutover still waits on a real corpus compliance number (K3), not just the harness.

### Phase 4 — started

| ID     | Title                                                                     |
| ------ | ------------------------------------------------------------------------- |
| P4.01  | StreamJsonRpc LSP host (`initialize` / `shutdown` / `exit`)               |
| P4.02  | Request scheduler (mutating serializes; non-mutating cancel on mutation)  |
| P4.05  | NativeAOT publish smokes: win-x64, linux-x64, osx-arm64 (`papyrusc` + LS) |
| P4.03a | Semantic tokens                                                           |
| P4.03b | Folding + selection ranges                                                |
| P4.03c | Document symbols + `textDocument/syntaxTree`                              |
| P4.03d | Indent-only formatter                                                     |
| P4.03e | Hover + definition                                                        |
| P4.03f | Completion + signature help                                               |
| P4.03g | References + prepare-rename + rename                                      |
| P4.04  | Completion resolve + commit characters (**#181**, **#190**)               |
| P4.03h | Inlay hints + document highlight                                          |
| P4.03i | Diagnostics (push + pull), `scriptInfo`, `projectInfos`                   |
| P4.03j | `textDocument/assembly` (PEX disassembly)                                 |

`PapyrusWorkspace`'s file watcher is now enabled by default in the real server too (the stdio entrypoint's `LspRequestScheduler()` had been overriding it off); out-of-editor edits are picked up and republish diagnostics via the same `WorkspaceChanged` hook used for LSP-driven edits. `textDocument/assembly` emits via `PexEmitter` and disassembles via `PasDisassembler` from `Papyrus.Workspaces`, without consulting a flags file -- it is a read-only diagnostic view of the script, not a build artifact.

### Phase 5 — debugger line (partial)

P5.01 vcpkg ports, P5.02 Address Library as data, P5.04a CommonLibSF spike, P5.04b SFSE plugin v1. **Not done:** P5.03 NativeAOT debug adapter (now unblocked), P5.05a–c DAP features.

### Phase 6

None. P6.05 (`LanguageVersion.Extended`) is gated by K5 until official-syntax compliance is proven; official path must stay untouched.

---

## Remaining (14)

Dependencies are from the accepted PR plan. Ready now means every listed dependency is already on this branch.

### Ready now

P4.02 and P4.05 are done, so P5.03 is unblocked. It remains intentionally deferred until the managed debugger API is stable and Windows/MSVC validation is available.

| ID    | Title                   | Depends on   | Notes    |
| ----- | ----------------------- | ------------ | -------- |
| P5.03 | NativeAOT debug adapter | P4.05, P0.04 | **#180** |

### Phase 4 — after handlers / AOT

| ID     | Title                                                    | Depends on     |
| ------ | -------------------------------------------------------- | -------------- |
| P4.06a | Extension: `vscode-languageclient` 9, drop RxJS          | P4.05          |
| P4.06b | Drop inversify/webpack, launch one LS, new-major VSIX    | P4.06a, P4.03j |
| P4.07  | Multi-root path hardening (**#193**, **#205**, **#208**) | P4.06b, P2.00  |
| P4.08  | Diagnostic report + redaction test                       | P4.07          |
| P4.09  | Delete `DarkId.Papyrus.*` managed tree                   | P4.06b, P4.08  |

P4.06a part 1 (the `vscode-languageclient` 9 bump) is done: `src/server/LanguageClient.ts` and the four `src/server/messages/*.ts` request/notification definitions were updated for the new generic arity and the `/node` entrypoint move, and `engines.vscode`/`@types/vscode` were bumped to `^1.82.0` to match. Dropping RxJS is still outstanding -- it is a much larger, deeply-coupled refactor across ~13 files (`LanguageClientManager`'s `combineLatest`/custom `asyncDisposable` operator, config-as-`Observable`, per-game client lifecycle, tree/codelens/status-bar consumers) and needs an explicit replacement-pattern decision before starting.

### Phase 5 — remaining

| ID     | Title                                  | Depends on     |
| ------ | -------------------------------------- | -------------- |
| P5.05a | DAP caps + **#85** / **#72**           | P5.03          |
| P5.05b | Interpreted `evaluate` / `setVariable` | P5.05a, P4.03e |
| P5.05c | Conditional / hit-count breakpoints    | P5.05b         |

### Phase 6

| ID    | Title                                     | Depends on           |
| ----- | ----------------------------------------- | -------------------- |
| P6.01 | Code-action infrastructure                | P4.03i               |
| P6.02 | First built-in fixes                      | P6.01                |
| P6.03 | Refactorings                              | P6.02                |
| P6.04 | Call / type hierarchy + workspace symbols | P4.03e, P4.03i       |
| P6.05 | `LanguageVersion.Extended`                | P1.07, P3.02, **K5** |

P6.05 must not ship official-syntax-breaking extensions until the compliance number exists. DAG deps are already satisfied; the product gate is K5, not the graph.

---

## Layout (new tree)

```
src/Papyrus.CodeAnalysis            syntax, symbols, bind, lower, CFG, emit
src/Papyrus.CodeAnalysis.Generators source generators
src/Papyrus.Pex                     PEX read/write/normalize/PAS
src/Papyrus.Workspaces              .ppj / workspace
src/Papyrus.Compiler                papyrusc
src/Papyrus.LanguageServer          StreamJsonRpc host + request scheduler + LSP handlers
src/Papyrus.DebugAdapter            stub (P5.03)
src/Papyrus.CodeAnalysis.Testing    corpus / allowlist / hash harness
```

Old `DarkId.Papyrus.*` managed projects stay until P4.09.

---

## How to resume

1. `git checkout rewrite` for the integration point, or create a topic branch from it for the next slice.
2. Continue the plan from **P4.06a**. P5.03 (NativeAOT debug adapter) is unblocked in the dependency graph but intentionally deferred until the managed debugger API is stable and Windows/MSVC validation is available.
3. Keep the no-push constraint unless the owner lifts it.

The work is organized as ordinary local topic branches from `rewrite`; there is no agent-orchestration branch naming convention to recover.
