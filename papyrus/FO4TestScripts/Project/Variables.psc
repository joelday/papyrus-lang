Scriptname Variables extends ScriptObject

; BUG: Allowing int literals larger than max int.

int intVariable
int intVariableWithValue = 1
int constIntVariable const
int constIntVariableWithValue = 1 const

int[] intArray
ArrayElementStruct[] structArray

Struct ArrayElementStruct
    int arrayElementStructMember
EndStruct

Function InitializeIntArrayVariable()
    intArray = new int[10]
    structArray = new ArrayElementStruct[1]
EndFunction

Function LocalArray()
    var intArray = new int[10]
    structArray = new ArrayElementStruct[1]
EndFunction
