# Overhaul implementation status

One-stop tracker for the clean-slate compiler and language-service rewrite specified in [`compiler-and-language-service-overhaul.md`](compiler-and-language-service-overhaul.md).

**Where the work lives:** local branch `execute-plan/b734a463` (not pushed). Tip at last update: `ff38275` (`merge: P4.01 StreamJsonRpc LSP host`). Default branch remains `develop`; the shipping VS Code extension is still the old OmniSharp / PCompiler host.

```
git checkout execute-plan/b734a463
dotnet build Papyrus.sln -c Release
```

**Score:** 60 of 87 plan PRs merged locally. **27 remaining.**

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

The new `Papyrus.LanguageServer` process speaks StreamJsonRpc `initialize` / `shutdown` / `exit` only. The shipping extension does **not** launch it yet.

---

## Done (60)

### Phase 0 — Foundations and PEX oracle (P0.00–P0.12)

Solution layout, CPM, architecture/AOT bans, `Papyrus.Pex` reader/writer/normalizer/PAS/PexReflector, corpus acquire, tier-1 hash harness, spec stubs. `Papyrus.Pex` 0.x pack metadata exists; nuget.org push is disabled.

### Phase 1 — Parser (P1.01–P1.10)

Syntax generator, green/red trees, lexer, script/decls/FO4+/Starfield/statements/expressions, trivia + recovery, dialect tables, `.flg` / `.ppj` parsers, round-trip + prefix + mutation tests, parse benchmarks.

### Phase 2 — Binder (P2.00–P2.11)

`PapyrusWorkspace`, `PapyrusName`, symbol API + PexReflector adapter, `PapyrusCompilation`, binder (members, signatures, K23), assignability, `as`/`is`/`Var`/arrays, diagnostic catalog, `SemanticModel`, dependency-tracked invalidation, dialect binding + negative corpus, observability. No LSP handlers in this phase.

### Phase 3 — Emit + CLI (all except P3.12)

| ID | Title |
|---|---|
| P3.01 | Bound tree |
| P3.02 | Lowering |
| P3.03a | CFG |
| P3.03b | Temp pooling + PEX emit (`PexFunctionBuilder`, `PexEmitter`) |
| P3.03c | Optimizer (Caprica starting rules, default off) |
| P3.04 | Engine-limit diagnostics (`PAP20xx`) |
| P3.05 | `papyrusc compile` (single file; `--game skyrimle`) |
| P3.05b | NativeAOT smoke of `papyrusc` on `windows-latest` / win-x64 |
| P3.06a | Native `.ppj` builds |
| P3.06b | `${ENV}` + pre/post build (CLI default on) |
| P3.07 | Differential compliance + allowlist (`docs/compliance/allowlist.md`) |
| P3.09 | Debug-info emit |
| P3.10 | Champollion tier-3 (optional; skips if binary/corpus missing) |
| P3.11 | Encoding contract tests (Windows-1252 string tables) |

**Not done in phase 3:** P3.12 (pack `Papyrus.CodeAnalysis` 0.x). Cutover still waits on a real corpus compliance number (K3), not just the harness.

### Phase 4 — started

| ID | Title |
|---|---|
| P4.01 | StreamJsonRpc LSP host (`initialize` / `shutdown` / `exit`) |

### Phase 5 — debugger line (partial)

P5.01 vcpkg ports, P5.02 Address Library as data, P5.04a CommonLibSF spike, P5.04b SFSE plugin v1. **Not done:** P5.03 NativeAOT debug adapter (needs P4.05), P5.05a–c DAP features.

### Phase 6

None. P6.05 (`LanguageVersion.Extended`) is gated by K5 until official-syntax compliance is proven; official path must stay untouched.

---

## Remaining (27)

Dependencies are from the accepted PR plan. Ready now means every listed dependency is already on this branch.

### Ready now

| ID | Title | Depends on | Notes |
|---|---|---|---|
| P3.12 | Pack `Papyrus.CodeAnalysis` 0.x | P3.07, P2.09 | Unstable 0.x. **Do not push nuget.org** in this run; pack + CI artifact only, same as `Papyrus.Pex`. |
| P4.02 | Request scheduler | P4.01 | Mutating vs non-mutating LSP queue. Unblocks all P4.03*. |
| P4.05 | NativeAOT publish, three RIDs | P3.05b, P4.01 | win-x64, linux-x64, osx-arm64 for the language server (and papyrusc if cheap). **#198**. |

### Phase 4 — after P4.02

| ID | Title | Depends on |
|---|---|---|
| P4.03a | Semantic tokens | P4.02 |
| P4.03b | Folding + selection ranges | P4.02 |
| P4.03c | Document symbols + `textDocument/syntaxTree` | P4.02 |
| P4.03d | Indent-only formatter (**#39**) | P4.02 |
| P4.03e | Hover + definition | P4.02, P2.09 |
| P4.03f | Completion + signature help | P4.02, P2.09 |
| P4.03g | References + rename (**#73/#74/#75**, **#84**) | P4.02, P2.09 |
| P4.03h | Inlay hints + document highlight | P4.02, P2.09 |
| P4.03i | Remaining LSP (diagnostics, assembly, scriptInfo, projectInfos, pull) | P4.02, P3.05, P2.00 |
| P4.04 | Completion resolve + commit characters (**#181**, **#190**) | P4.03f |
| P4.06a | Extension: `vscode-languageclient` 9, drop RxJS | P4.05 |
| P4.06b | Drop inversify/webpack, launch one LS, new-major VSIX | P4.06a, P4.03i |
| P4.07 | Multi-root path hardening (**#193**, **#205**, **#208**) | P4.06b, P2.00 |
| P4.08 | Diagnostic report + redaction test | P4.07 |
| P4.09 | Delete `DarkId.Papyrus.*` managed tree | P4.06b, P4.08 |

P4.03a–i can run in parallel once P4.02 lands (P4.03e–h also need P2.09, already done; P4.03i also needs P3.05 and P2.00, already done).

### Phase 5 — remaining

| ID | Title | Depends on |
|---|---|---|
| P5.03 | NativeAOT debug adapter (**#180**) | P4.05, P0.04 |
| P5.05a | DAP caps + **#85** / **#72** | P5.03 |
| P5.05b | Interpreted `evaluate` / `setVariable` | P5.05a, P4.03e |
| P5.05c | Conditional / hit-count breakpoints | P5.05b |

### Phase 6

| ID | Title | Depends on |
|---|---|---|
| P6.01 | Code-action infrastructure | P4.03i |
| P6.02 | First built-in fixes | P6.01 |
| P6.03 | Refactorings | P6.02 |
| P6.04 | Call / type hierarchy + workspace symbols | P4.03e, P4.03i |
| P6.05 | `LanguageVersion.Extended` | P1.07, P3.02, **K5** |

P6.05 must not ship official-syntax-breaking extensions until the compliance number exists. DAG deps are already satisfied; the product gate is K5, not the graph.

---

## Layout (new tree)

```
src/Papyrus.CodeAnalysis            syntax, symbols, bind, lower, CFG, emit
src/Papyrus.CodeAnalysis.Generators source generators
src/Papyrus.Pex                     PEX read/write/normalize/PAS
src/Papyrus.Workspaces              .ppj / workspace
src/Papyrus.Compiler                papyrusc
src/Papyrus.LanguageServer          StreamJsonRpc host (lifecycle only)
src/Papyrus.DebugAdapter            stub (P5.03)
src/Papyrus.CodeAnalysis.Testing    corpus / allowlist / hash harness
```

Old `DarkId.Papyrus.*` managed projects stay until P4.09.

---

## How to resume

1. `git checkout execute-plan/b734a463` (this document and the code).
2. Continue the plan from **P3.12**, **P4.02**, and **P4.05** (the three nodes whose dependencies are already on the branch).
3. Keep the no-push constraint unless the owner lifts it.

Plan id for the execute-plan run: `b734a463`. Per-PR branches are `execute-plan/b734a463-pr-N-…`; completed work is fast-forwarded or merge-committed onto `execute-plan/b734a463`.
