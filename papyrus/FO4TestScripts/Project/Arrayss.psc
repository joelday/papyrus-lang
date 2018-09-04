Scriptname Arrays extends ScriptObject

ArrayElementStruct[] structArray

Struct ArrayElementStruct
    int arrayElementStructMember
EndStruct

Function ArrayFunctionCalls()
    structArray = new ArrayElementStruct[10]
    structArray.Add(new ArrayElementStruct, 2)
    structArray.Clear()
    structArray.Find(structArray[0])
    structArray.FindStruct("arrayElementStructMember", 1, 0)
    structArray.Insert(structArray[0], 0)
    structArray.Remove(0, 1)
    structArray.RemoveLast()
    structArray.RFind(structArray[0])
EndFunction
