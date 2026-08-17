ScriptName Statements
Function Run()
  Int x = 1
  x = 2
  x += 3
  If x == 1
    Return
  ElseIf x == 2
    x = 0
  Else
    While x > 0
      x -= 1
    EndWhile
  EndIf
EndFunction
