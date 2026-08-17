namespace Papyrus.Pex;

/// <summary>
/// Papyrus VM opcodes. Names and values match Caprica
/// <c>Caprica/pex/PexInstruction.h</c> at e4dee08.
/// </summary>
public enum PexOpCode : byte
{
    Nop = 0,
    IAdd = 1,
    FAdd = 2,
    ISub = 3,
    FSub = 4,
    IMul = 5,
    FMul = 6,
    IDiv = 7,
    FDiv = 8,
    IMod = 9,
    Not = 10,
    INeg = 11,
    FNeg = 12,
    Assign = 13,
    Cast = 14,
    CmpEq = 15,
    CmpLt = 16,
    CmpLte = 17,
    CmpGt = 18,
    CmpGte = 19,
    Jmp = 20,
    JmpT = 21,
    JmpF = 22,
    CallMethod = 23,
    CallParent = 24,
    CallStatic = 25,
    Return = 26,
    StrCat = 27,
    PropGet = 28,
    PropSet = 29,
    ArrayCreate = 30,
    ArrayLength = 31,
    ArrayGetElement = 32,
    ArraySetElement = 33,
    ArrayFindElement = 34,
    ArrayRFindElement = 35,
    SkyrimMax = ArrayRFindElement,

    // Fallout 4
    Is = 36,
    StructCreate = 37,
    StructGet = 38,
    StructSet = 39,
    ArrayFindStruct = 40,
    ArrayRFindStruct = 41,
    ArrayAdd = 42,
    ArrayInsert = 43,
    ArrayRemoveLast = 44,
    ArrayRemove = 45,
    ArrayClear = 46,
    Fallout4Max = ArrayClear,

    // Fallout 76
    ArrayGetAllMatchingStructs = 47,
    Fallout76Max = ArrayGetAllMatchingStructs,

    // Starfield
    LockGuards = 48,
    UnlockGuards = 49,
    TryLockGuards = 50,
    StarfieldMax = TryLockGuards,
    Max = TryLockGuards,

    Invalid = 0xFF,
}
