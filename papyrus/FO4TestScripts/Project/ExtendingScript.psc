Scriptname ExtendingScript extends BaseScript


; BUG: Excessive exceptions thrown by LS during incomplete editing.
; Should be resilient and skip failures for multiple result responses.

; BUG: Script name is not validated against file name.
; If a file of the same name exists, type will be resolved to that file.
; TODO: Need better error for missing Scriptname

Function NativeFunction() native

var Function NativeFunctionReturningVar() native

var Function NonNativeFunctionReturningVar()
    return self.NativeFunctionReturningVar()
EndFunction

; BUG: No errors for this event header inside an event (effectively)
Event ScriptEvent(ScriptObject akActivator)

EndEvent

; BUG: Able to declare a custom event with the same name as a function
CustomEvent Custom


var Function NonNativeFunctionReturningVar()
    return parent.NonNativeFunctionReturningVar()
EndFunction

var Function FunctionReturningVar()
    return self.NonNativeFunctionReturningVar()
EndFunction
