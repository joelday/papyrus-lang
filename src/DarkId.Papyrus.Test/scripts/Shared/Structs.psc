Scriptname Structs extends ScriptObject

Struct StructA
    int intMemberWithValue = 1
    int intMember
    int constIntMember const
    int constIntMemberWithValue = 1 const
EndStruct

StructA Function CreateStructInstance()
    return new StructA
EndFunction

StructA Function CreateStructInstance2()
    StructA s = new StructA

    s.intMember = 1
    s.intMemberWithValue = 2

    return s
EndFunction

StructA Function GetAndMutateInstance()
    StructA s = CreateStructInstance()

    s.intMember = 1
    s.intMemberWithValue = 2

    return s
EndFunction
