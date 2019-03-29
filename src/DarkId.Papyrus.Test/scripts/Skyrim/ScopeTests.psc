;/test:ignore-diagnostics/;
Scriptname ScopeTests extends BaseScript

;/marker:script-body/;

int ;/marker:script-variable-name/;localPrivateVariable
int ;/marker:property-keyword/;Property ;/marker:script-property-name/;LocalProperty Auto

; Leading line comment 1
; Leading line comment 2

; Leading line comment 3
int Function ReturningIntFunction(;/marker:function-parameter-type/;int\
    ;/marker:function-parameter-name/;arg)
{ Doc comment }

    parent.;/marker:parent-function-call/;CallsOnSomethingEvent()
    self.;/marker:self-function-call/;CallsOnSomethingEvent()

    int hello

    int[] helloArray = new int[2]
    int len = helloArray.;/marker:array-member-access/;Length

    ScriptObject.;/marker:global-function-call/;NativeGlobalFunction()

    ScriptObject obj = none
    obj.;/marker:nested-function-call/;ReturnsScriptObject().ReturnsScriptObject();

    int ;/marker:local-variable-name/;value = 0 as ;/marker:as-expression/;int
    
    ;/marker:function-body/;
    
    bool ;/marker:incomplete-declaration/;

    FunctionWithOptionalParams(;/marker:first-func-param/;0, 1, ;/marker:named-func-param/;d = 5)

    return arg
EndFunction

Function CallingFunction()
    int a = ;/marker:assignment/;ReturningIntFunction(;/marker:function-call-parameter/;0)
EndFunction

Function LocalGlobalFunction() Global

EndFunction

Function FunctionWithOptionalParams(int a = 1, int b = 2, int c = 3, int d = 4)
EndFunction