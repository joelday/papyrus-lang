ScriptName FunctionsEvents
{Adds two ints}
Int Function Add(Int a, Int b = 0) Global
  Return a + b
EndFunction

Function NativeFn() Native

Event OnInit()
  Add(1)
EndEvent
