Scriptname Functions extends ScriptObject

; BUG: Not validating override signatures against ancestors
var Function NonNativeFunctionReturningVar()
    return parent.NativeFunctionReturningVar()
EndFunction

string Function FunctionReturningString()
    return "string"
EndFunction

int Function FunctionReturningInt()
    return 0
EndFunction

int Function FunctionReturningNegativeInt()
    return -1
EndFunction

int Function FunctionReturningHexInt()
    return 0x0001
EndFunction

float Function FunctionReturningFloat()
    return 0.01
EndFunction

float Function FunctionReturningNegativeFloat()
    return -0.01
EndFunction

bool Function FunctionReturningBool()
    return true
EndFunction

ScriptObject Function FunctionReturningScriptObject()
    return self
EndFunction

Functions Function FunctionReturningSelfType()
    return self
EndFunction

Functions Function VoidFunctionWithReturn()
    return
EndFunction

Function GlobalNativeFunction() native global

Function GlobalFunction() global
EndFunction

int Function GlobalFunctionReturningInt() global
    return 0
EndFunction

int Function FunctionWithArgument(int argument)
    return argument
EndFunction

string Function FunctionWithTwoArguments(int argumentA, string argumentB)
    argumentB += argumentA
    return argumentB
EndFunction

int Function FunctionWithOptionalArgument(int argumentA = 1)
    return argumentA
EndFunction

int Function FunctionWithVarArgument(var argumentA)
    argumentA += 1
    return argumentA
EndFunction

int Function FunctionWithNonOptionalAndOptionalArgument(int argumentA, int argumentB = 1)
    return argumentA + argumentB
EndFunction

int Function FunctionWithNonOptionalAndTwoOptionalArguments(int argumentA, int argumentB = 1, int argumentC = 1)
    return argumentA + argumentB + argumentC
EndFunction

Function LocalSelfCallingFunction()
    VoidFunctionWithReturn()
EndFunction

Function SelfCallingFunction()
    self.VoidFunctionWithReturn()
EndFunction

Function LocalCallingParentFunction()
    NativeFunction()
EndFunction

Function ParentCallingParentFunction()
    parent.NativeFunction()
EndFunction

Function OptionalArgumentCaller()
    FunctionWithOptionalArgument(1)
EndFunction

Function OptionalArgumentCallerWithoutArgument()
    FunctionWithOptionalArgument()
EndFunction

Function NonOptionalArgumentCaller()
    FunctionWithNonOptionalAndOptionalArgument(0)
EndFunction

Function FirstOptionalArgumentCaller()
    FunctionWithNonOptionalAndOptionalArgument(0, 1)
EndFunction

Function TwoOptionalArgumentCaller()
    FunctionWithNonOptionalAndTwoOptionalArguments(0, 1, 2)
EndFunction

Function NamedArgumentCaller()
    FunctionWithArgument(argument = 0)
EndFunction

Function NamedOptionalArgumentCaller()
    FunctionWithOptionalArgument(argumentA = 0)
EndFunction

Function NamedFirstOptionalArgumentCaller()
    FunctionWithNonOptionalAndOptionalArgument(0, argumentB = 1)
EndFunction

Function NamedSecondOptionalArgumentCaller()
    FunctionWithNonOptionalAndTwoOptionalArguments(0, argumentC = 1)
EndFunction
