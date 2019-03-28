﻿Scriptname ScriptObject Native Hidden

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

Function RegisterForCustomEvent(ScriptObject akEventSource, CustomEventName asEventName) native
Function SendCustomEvent(CustomEventName asEventName, Var[] akArgs = None) native