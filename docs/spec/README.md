# Papyrus language and PEX spec

Normative language and PEX references extracted from [Appendix A](../design/compiler-and-language-service-overhaul.md#appendix-a--normative-language-and-pex-references) of the design document.

P1.01 **consumes** the productions below; it does not invent them.

Frozen pin: [Orvid/Caprica](https://github.com/Orvid/Caprica) `@ e4dee08` (`master`, 2024-06-14). Path table: [pins.md](pins.md). Formatter: [formatter.md](formatter.md). Project files and Pyro: [ppj-pyro-parity.md](ppj-pyro-parity.md).

## Copy vs. re-derive

| Topic | Normative reference | What we copy | What we re-derive from the CK via tier-2 |
|---|---|---|---|
| Official productions, precedence, newline / `\` | Caprica `Caprica/papyrus/parser/` at `e4dee08`; CK is the oracle when they disagree | Statement / expression shape, keyword sets per game | `SupportsIsOperator`; any production Caprica added only for extensions |
| Starfield `Guard` / `Lock` | Caprica `Caprica/papyrus/PapyrusGuard.h`, `PapyrusLockParameter.h`; `Caprica/pex/PexGuard.h` | Syntax and PEX records | Exact CK diagnostics |
| FO4 namespaces / structs / groups / custom events | Caprica `PapyrusStruct.h`, `PapyrusPropertyGroup.h`, `PapyrusCustomEvent.h`; current `NodeKind` / `Nodes.cs` | Productions | CK accept/reject on the negative corpus |
| `.flg` | Caprica `Caprica/papyrus/PapyrusUserFlags.h`; current `FlagsFile.cs` (PCompiler `FlagsLexer`/`FlagsParser`) | Flag name = bit in declaration order; 31 user bits | Unknown-flag diagnostics |
| Type assignability, `as` / `is`, `Var`, arrays | Caprica `Caprica/papyrus/PapyrusType.cpp` | Starting conversion matrix (below) | CK implicit coercions not in Caprica's four published differences |
| Import order / namespaced paths | Design document K21; current `ProgramOptions.SourceInclude` | First-match; `A:B:Script` → `A/B/Script.psc` | — |
| Auto-property backing names, state emit, native emit | Caprica `Caprica/pex/PexFunctionBuilder.h`, `PexProperty.cpp`, `PexState.cpp` | `::Name_var`; empty vs named states; native = no body | Parent-Auto direct-read (allowlist item 3 — we do **not** copy the CK) |
| User-flag encoding | Caprica `PexUserFlags.h` | 32-bit mask, bit i = i-th `.flg` name | — |
| `Optimize` / `Release` / `Final` | Design document K20 + emit section; Caprica `PexOptimizer.cpp` | Starting optimizer rules | Which rules the CK applies with `-optimize` off |
| PEX binary layout | Caprica `Caprica/pex/PexReader.h`, `PexWriter.h`, `PexFile.h`; Champollion `Champollion/Pex/Binary.hpp` (already in this repo via vcpkg) | Header, string table, objects, debug info | String-table order if it is not first-use |
| Temps | Caprica `PexFunctionBuilder.h` per-type free list | Algorithm | Exact acquire/release order (tier-2) |
| Language extensions | Caprica README + `Caprica/papyrus/statements/` | Only if P6.05 ships them (K5; post-compliance) | — |

## Official productions (CK surface)

Newlines terminate statements. `\` immediately before a newline is trivia that suppresses `EndOfLine`. Identifiers are `[A-Za-z_][A-Za-z0-9_]*`, compared `OrdinalIgnoreCase`.

`Self` and `Parent` are **identifiers**, not keywords. The binder gives them their special meaning (`self` → containing script, `parent` → extended script). A user may not declare a member named `Self` or `Parent` (diagnostic); they may appear as ordinary names in Skyrim scripts that already do (rare; Champollion-compat).

```
name              ::= identifier
qualname          ::= name (':' name)*                         ; FO4+ / Starfield namespaces
typename          ::= qualname ['#' name] ['[]']              ; Script, A:B:Script, Script#Struct, Int[]
script            ::= [doc] 'ScriptName' name ['Extends' typename] flags* eol
                      import* declaration*
import            ::= 'Import' typename eol
declaration       ::= state | function | event | property | variable
                    | struct | group | customevent | guard     ; last four dialect-gated

state             ::= ['Auto'] 'State' name eol (function | event)* 'EndState'
function          ::= [doc] [typename] 'Function' name '(' params ')' flags* eol
                      statement* 'EndFunction'
event             ::= [doc] 'Event' name '(' params ')' flags* eol
                      statement* 'EndEvent'
property          ::= [doc] typename 'Property' name [ '=' literal ] flags* eol
                      [ function* 'EndProperty' ]              ; Auto/AutoReadOnly have no body
variable          ::= [doc] typename name [ '=' literal ] flags* eol
struct            ::= [doc] 'Struct' name eol variable* 'EndStruct'          ; FO4+
group             ::= 'Group' name flags* eol property* 'EndGroup'           ; FO4+
customevent       ::= 'CustomEvent' name eol                                 ; FO4+
guard             ::= 'Guard' name eol                                       ; Starfield; ILLUSTRATIVE
lock-stmt         ::= 'Lock' '(' name (',' name)* ')' eol statement* 'EndLock'
                    | 'TryLock' ... | 'Unlock' ...                           ; ILLUSTRATIVE

params            ::= [ param (',' param)* ]
param             ::= typename name [ '=' literal ]

statement         ::= if | while | return | assign | declare | expr-stmt | lock-stmt
if                ::= 'If' expr eol statement* ('ElseIf' expr eol statement*)* ['Else' eol statement*] 'EndIf'
while             ::= 'While' expr eol statement* 'EndWhile'
return            ::= 'Return' [expr] eol
declare           ::= typename name [ '=' expr ] eol
assign            ::= lvalue ( '=' | '+=' | '-=' | '*=' | '/=' | '%=' ) expr eol
expr-stmt         ::= expr eol

expr              ::= or
or                ::= and ('||' and)*
and               ::= cmp ('&&' cmp)*
cmp               ::= add (('=='| '!=' | '<' | '>' | '<=' | '>=') add)*
add               ::= mul (('+' | '-') mul)*
mul               ::= unary (('*' | '/' | '%') unary)*
unary             ::= ('-' | '!') unary | cast
cast              ::= postfix [ ('As' | 'Is') typename ]
postfix           ::= primary ( '.' name ['(' args ')'] | '[' expr ']' )*
primary           ::= literal | name ['(' args ')'] | 'new' typename ['[' expr ']'] | '(' expr ')'
args              ::= [ arg (',' arg)* ]
arg               ::= [ name '=' ] expr
lvalue            ::= postfix   ; must resolve to variable / property / array element / struct field
literal           ::= int | hex | float | string | 'True' | 'False' | 'None'
doc               ::= '{' .* '}'          ; not trivia; attached to the following declaration
flags             ::= 'Native' | 'Global' | 'Auto' | 'AutoReadOnly' | 'Const'
                    | 'Hidden' | 'Conditional' | 'Mandatory'
                    | 'DebugOnly' | 'BetaOnly' | user-flag-from-flg
```

Expression precedence is the table above (or binds loosest). `Is` / `As` bind tighter than unary minus of an additive expression (`-x As Y` is `(-x) As Y` only if written that way; `x As Y + 1` is `(x As Y) + 1`).

Doc comments `{ ... }` attach to the **next** declaration (script, function, event, property, variable, struct). They are a real token, not trivia.

**Starfield `Guard` / `Lock`:** the BNF lines above are illustrative. If they disagree with Caprica `Caprica/papyrus/PapyrusGuard.h`, `PapyrusLockParameter.h`, and the parser under `Caprica/papyrus/parser/` at `e4dee08`, **those headers win** (including `TryLock` / `Unlock` if present). P1.04d copies Caprica, then the tier-2 harness confirms against the CK.

## `.flg` syntax

```
flag              ::= name [ '{' bit-index '}' ] eol
comment           ::= ';' .* eol | ';/' .* '/;'
```

Honor explicit `{bit-index}`. If omitted, bits are assigned in declaration order starting at **0**. The mask is 32 bits. A bit-index > 31 or a 33rd named flag is `PAP20xx`. Bit 0 is a normal user bit (vanilla `Hidden`), not reserved.

## PEX `gameId:u16` → `GameId`

Copied from Caprica `Caprica/common/GameID.h` at `e4dee08`:

| PEX `gameId` | `PexVersion` | `PexFile.Game` (`GameId`) |
|---|---|---|
| `0` / `0xFFFF` / other | any | unresolved; reader diagnostic; `Game` left default |
| `1` (`Skyrim`) | V31 (`3.1`) | `GameId.Skyrim` (LE vanilla) |
| `1` (`Skyrim`) | V32 (`3.2`) | `GameId.SkyrimSpecialEdition` **by default**. Caller may pass `GameId.Skyrim` to force LE expansions. Path heuristic: if the file lives under an LE install the workspace already resolved, use `Skyrim`. |
| `2` | V39 | `GameId.Fallout4` |
| `3` | V315 | `GameId.Fallout76` |
| `4` | V312 | `GameId.Starfield` |

V32 cannot distinguish LE expansions from SE in the header alone. The default is SE because that is a compile target; LE is the override.

## Conversion matrix (starting point; CK is the oracle)

`S → T` means “value of type S may be used where T is expected.”

| From \ To | Bool | Int | Float | String | None | Script | Struct | Array | Var |
|---|---|---|---|---|---|---|---|---|---|
| Bool | id | — | — | explicit `As` | — | — | — | — | yes |
| Int | implicit | id | implicit | explicit `As` | — | — | — | — | yes |
| Float | implicit | explicit `As` (trunc) | id | explicit `As` | — | — | — | — | yes |
| String | implicit (non-empty) | — | — | id | — | — | — | — | yes |
| None | **allowlist 1** | — | — | **allowlist 1** | id | literal only (allowlist 2) | literal only | literal only | literal only |
| Script A | — | — | — | explicit `As` | implicit | A or parent of A | — | — | yes |
| Struct | — | — | — | — | implicit | — | exact | — | yes |
| Array | — | — | — | — | implicit | — | — | same element | yes |
| Var | explicit `As` | explicit `As` | explicit `As` | explicit `As` | implicit | explicit `As` | explicit `As` | explicit `As` | id |

Arrays are **not** covariant. `is` is true iff the runtime type is T or a descendant; it is a dialect error when `SupportsIsOperator` is false. `as` to an unrelated script yields `None`.

## PEX binary layout (little-endian)

Copied from Caprica `PexReader.h` / Champollion `Binary.hpp`:

1. Magic `0xFA57C0DE`
2. `major:u8`, `minor:u8` (the `PexVersion`)
3. `gameId:u16` (Caprica `GameID`: Skyrim=1, Fallout4=2, Fallout76=3, Starfield=4; mapped as the table above)
4. `compilationTime:u64` (unix seconds; we write 0 unless `TimeProvider` is set)
5. `sourceName`, `userName`, `machineName` — length-prefixed strings in `PapyrusParseOptions.Encoding`
6. String table: `count:u16` + strings
7. Debug info (optional): `hasDebug:u8`, then function line maps
8. User flags: `count:u16` + (name, bit)
9. Objects: `count:u16` + object records (name, size, parent, docstring, user flags, auto-state, variables, properties, structs, guards, states → functions → instructions)

Instruction encoding: `op:u8` + operands as typed in Caprica `PexInstruction.h` (variable-width: ident / integer / float / string-index / none).

## Champollion compatibility (tier 3 / **#84**)

`--champollion-compat` / `PapyrusParseOptions.ChampollionCompat`:

- Accept `::`-prefixed compiler identifiers as names.
- Accept `ObjectName#StructName` as a type name (maps to `TypeName.StructName`).

`papyrusc verify --champollion` decompiles a directory of `.pex`, recompiles with ChampollionCompat, and compares normalized hashes (debug info stripped). It does not need the CK compiler.

The Champollion sources stay in the vcpkg overlay (`dependencies/ports/Champollion/`); they are not vendored into `src/`. Supply a binary via:

- `--champollion-path <bin>`
- `PAPYRUSC_CHAMPOLLION`
- `PATH` (`champollion` / `Champollion` / `Champollion.exe`)
- `tools/Champollion` (cwd or next to `papyrusc`)

Public CI skips (exit 0) when the binary or corpus is missing.
