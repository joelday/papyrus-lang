# Phase 1 formatter style

Extracted from [Appendix B](../design/compiler-and-language-service-overhaul.md#appendix-b--phase-1-formatter-style) of the design document.

Phase 1 ships **indent-only** formatting so `format(format(x)) == x` is true and **#39** gets a real command without a style war.

| Rule | Value |
|---|---|
| Indent unit | 4 spaces (no tabs). A setting `papyrus.format.indent` may be `"4spaces"` (default) or `"tab"`. |
| What is changed | Leading trivia on each line that begins a declaration or statement, plus lines inside `If` / `While` / `State` / `Function` / `Event` / `Struct` / `Group` / `Lock` bodies. |
| What is **not** changed | Keyword casing, operator spacing, blank-line runs, comment text, doc comments, line-continuation placement, string literals, the presence/absence of `\` . |
| Newline after `\` | Left alone. |
| Missing tokens | Not inserted by the formatter (that is a code fix, phase 6). |
| Idempotence | `format(format(x)) == format(x)` is a CI property on the game scripts and the fuzzer. |

Range / on-type format use the same rules on the smallest enclosing statement or declaration. Broader pretty-printing is phase 6.
