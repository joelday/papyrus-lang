Scriptname Events extends ScriptObject native

; BUG: Able to declare remote event handler on derived script type
; BUG: Event handler signatures not validated to match declaration
; BUG: Remote event handler signatures not validated to match standard signature

Event ScriptEvent(ScriptObject akActivator)
EndEvent

Event ExtendingScript.Custom(ExtendingScript akSender, var[] akArgs)
EndEvent

Function EventCaller()
    ; BUG: Able to get custom event from member access.
    ; BUG: Able to pass script type itself in expressions.

    ScriptObject obj = self
    ; obj.ScriptEvent(self)
    self.ScriptEvent(self)

    ; BUG: Event signatures not flattened
EndFunction
