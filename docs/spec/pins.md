# Caprica pins

Frozen language / PEX reference for this spec:

| Field | Value |
|---|---|
| Repository | [Orvid/Caprica](https://github.com/Orvid/Caprica) |
| Pin | `e4dee08` (`master`, 2024-06-14) |
| Full SHA | `e4dee0860914d75e770d3f9ab374f7aba474b701` |
| License | MIT |

Paths below are relative to the Caprica repository root at that commit. There is no file named `PexOps`; the opcode table lives in `PexInstruction.h`.

| Role | Path at `e4dee08` |
|---|---|
| Lexer | `Caprica/papyrus/parser/PapyrusLexer.h`, `Caprica/papyrus/parser/PapyrusLexer.cpp` |
| Parser | `Caprica/papyrus/parser/PapyrusParser.h`, `Caprica/papyrus/parser/PapyrusParser.cpp` |
| PexOps (opcode table) | `Caprica/pex/PexInstruction.h` |
| GameID | `Caprica/common/GameID.h` |
| PexReader | `Caprica/pex/PexReader.h` |
| PexWriter | `Caprica/pex/PexWriter.h` |
| PexFunctionBuilder | `Caprica/pex/PexFunctionBuilder.h` |
| PapyrusGuard | `Caprica/papyrus/PapyrusGuard.h` |

Additional paths cited by [Appendix A](README.md):

| Role | Path at `e4dee08` |
|---|---|
| Lock parameters | `Caprica/papyrus/PapyrusLockParameter.h` |
| PEX guard records | `Caprica/pex/PexGuard.h` |
| Structs | `Caprica/papyrus/PapyrusStruct.h` |
| Property groups | `Caprica/papyrus/PapyrusPropertyGroup.h` |
| Custom events | `Caprica/papyrus/PapyrusCustomEvent.h` |
| User flags (source) | `Caprica/papyrus/PapyrusUserFlags.h` |
| User flags (PEX) | `Caprica/pex/PexUserFlags.h` |
| Type / conversion matrix | `Caprica/papyrus/PapyrusType.cpp` |
| PEX file | `Caprica/pex/PexFile.h` |
| PEX property emit | `Caprica/pex/PexProperty.cpp` |
| PEX state emit | `Caprica/pex/PexState.cpp` |
| Optimizer | `Caprica/pex/PexOptimizer.cpp` |
| Language extensions | `Caprica/papyrus/statements/` |
| `.flg` lexer / parser | `Caprica/common/parser/CapricaUserFlagsLexer.h`, `Caprica/common/parser/CapricaUserFlagsParser.h` |
