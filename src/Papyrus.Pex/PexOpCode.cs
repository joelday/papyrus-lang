using System;

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

/// <summary>
/// Operand widths from Caprica <c>PexInstruction.h</c> / <c>PexFunctionBuilder.h</c> OPCODES.
/// Shared by the reader and writer so the two cannot drift.
/// </summary>
public static class PexOpCodeInfo
{
    public static int FixedArgumentCount(PexOpCode op)
    {
        switch (op)
        {
            case PexOpCode.Nop:
            case PexOpCode.LockGuards:
            case PexOpCode.UnlockGuards:
                return 0;
            case PexOpCode.Jmp:
            case PexOpCode.Return:
            case PexOpCode.StructCreate:
            case PexOpCode.ArrayRemoveLast:
            case PexOpCode.ArrayClear:
            case PexOpCode.TryLockGuards:
                return 1;
            case PexOpCode.Not:
            case PexOpCode.INeg:
            case PexOpCode.FNeg:
            case PexOpCode.Assign:
            case PexOpCode.Cast:
            case PexOpCode.JmpT:
            case PexOpCode.JmpF:
            case PexOpCode.CallParent:
            case PexOpCode.ArrayCreate:
            case PexOpCode.ArrayLength:
                return 2;
            case PexOpCode.IAdd:
            case PexOpCode.FAdd:
            case PexOpCode.ISub:
            case PexOpCode.FSub:
            case PexOpCode.IMul:
            case PexOpCode.FMul:
            case PexOpCode.IDiv:
            case PexOpCode.FDiv:
            case PexOpCode.IMod:
            case PexOpCode.CmpEq:
            case PexOpCode.CmpLt:
            case PexOpCode.CmpLte:
            case PexOpCode.CmpGt:
            case PexOpCode.CmpGte:
            case PexOpCode.CallMethod:
            case PexOpCode.CallStatic:
            case PexOpCode.StrCat:
            case PexOpCode.PropGet:
            case PexOpCode.PropSet:
            case PexOpCode.ArrayGetElement:
            case PexOpCode.ArraySetElement:
            case PexOpCode.Is:
            case PexOpCode.StructGet:
            case PexOpCode.StructSet:
            case PexOpCode.ArrayAdd:
            case PexOpCode.ArrayInsert:
            case PexOpCode.ArrayRemove:
                return 3;
            case PexOpCode.ArrayFindElement:
            case PexOpCode.ArrayRFindElement:
                return 4;
            case PexOpCode.ArrayFindStruct:
            case PexOpCode.ArrayRFindStruct:
                return 5;
            case PexOpCode.ArrayGetAllMatchingStructs:
                return 6;
            default:
                throw new PexFormatException($"Unknown opcode {op}.");
        }
    }

    public static bool HasVariadicArguments(PexOpCode op)
    {
        return op is PexOpCode.CallMethod
            or PexOpCode.CallParent
            or PexOpCode.CallStatic
            or PexOpCode.LockGuards
            or PexOpCode.UnlockGuards
            or PexOpCode.TryLockGuards;
    }

    public static bool IsBranch(PexOpCode op)
        => op is PexOpCode.Jmp or PexOpCode.JmpT or PexOpCode.JmpF;

    public static int BranchTargetArgumentIndex(PexOpCode op)
    {
        switch (op)
        {
            case PexOpCode.Jmp:
                return 0;
            case PexOpCode.JmpT:
            case PexOpCode.JmpF:
                return 1;
            default:
                throw new PexFormatException($"Opcode {op} is not a branch.");
        }
    }

    public static bool TryGetBranchOffset(PexInstruction instruction, out int offset)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        offset = 0;
        if (!IsBranch(instruction.OpCode))
        {
            return false;
        }

        offset = GetBranchOffset(instruction);
        return true;
    }

    public static void SetBranchOffset(PexInstruction instruction, int offset)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        var argIndex = RequireBranchOffsetIndex(instruction);
        instruction.Arguments = instruction.Arguments.SetItem(
            argIndex,
            new PexValue { Type = PexValueType.Integer, Integer = offset });
    }

    private static int GetBranchOffset(PexInstruction instruction)
    {
        var argIndex = RequireBranchOffsetIndex(instruction);
        var value = instruction.Arguments[argIndex];
        if (value.Type != PexValueType.Integer)
        {
            throw new PexFormatException($"Branch opcode {instruction.OpCode} has a non-integer offset.");
        }

        return value.Integer;
    }

    private static int RequireBranchOffsetIndex(PexInstruction instruction)
    {
        var argIndex = BranchTargetArgumentIndex(instruction.OpCode);
        if ((uint)argIndex >= (uint)instruction.Arguments.Length)
        {
            throw new PexFormatException($"Branch opcode {instruction.OpCode} is missing its offset argument.");
        }

        return argIndex;
    }
}
