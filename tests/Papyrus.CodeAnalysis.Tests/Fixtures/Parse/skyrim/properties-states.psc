ScriptName PropertiesStates
Int Count = 0
Int Property Value Auto
Int Property Health
  Int Function Get()
    Return Count
  EndFunction
  Function Set(Int amount)
    Count = amount
  EndFunction
EndProperty
Auto State Default
  Event OnInit()
  EndEvent
EndState
State Busy
  Function Work()
  EndFunction
EndState
