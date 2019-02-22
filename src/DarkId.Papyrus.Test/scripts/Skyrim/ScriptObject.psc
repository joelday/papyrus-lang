Scriptname ScriptObject Native

int Function NativeFunction() native
int Function NativeGlobalFunction() native global

Event OnSomething()
EndEvent

Function CallsOnSomethingEvent()
    ;/marker:native-function-body/;
    OnSomething()
EndFunction

ScriptObject Function ReturnsScriptObject()
    return none
EndFunction