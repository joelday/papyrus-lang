Scriptname CastExpressions extends ExtendingScript

Struct CastStruct
EndStruct

Function AutoCast()
    bool boolValue

    boolValue = 0
    boolValue = 0.1
    boolValue = "string"

    boolValue = self
    boolValue = parent
    boolValue = new ScriptObject[10]

    boolValue = new Structs:StructA

    float intToFloatValue = 1

    ScriptObject obj = self
EndFunction

Function CastToInt()
    int intValue

    intValue = 1.0
    intValue = ""

    intValue = self as var
EndFunction

Function CastToObject()
    ScriptObject objectValue

    objectValue = self
    objectValue = new ScriptObject[10]
    objectValue = objectValue as CastExpressions
    objectValue = self as ExtendingScript
EndFunction

Function CastToString()
    string stringValue = ""

    stringValue = 0 as string
    stringValue = 0.1 as string
    stringValue = "string" as string
    stringValue = self as string
    stringValue = new ScriptObject[10] as string
    stringValue = new Structs:StructA as string
    stringValue = 0 as string
EndFunction

Function ArrayCasting()
    CastExpressions[] objArray = new CastExpressions[10]
    objArray = objArray as ExtendingScript[]
EndFunction
