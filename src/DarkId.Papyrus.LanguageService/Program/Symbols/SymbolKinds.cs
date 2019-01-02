using System;

namespace DarkId.Papyrus.LanguageService.Program.Symbols
{
    [Flags]
    public enum SymbolKinds
    {
        None = 0,

        CustomEvent = 1,
        Event = 2,
        Function = 4,
        Group = 8,
        Import = 16,
        Property = 32,
        State = 64,
        Struct = 128,
        Variable = 256,
        Script = 512,

        All = CustomEvent
            | Event
            | Function
            | Group
            | Import
            | Property
            | State
            | Struct
            | Variable
            | Script
    }
}
