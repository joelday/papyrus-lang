Scriptname DocComments extends ScriptObject
{Comment for Scriptname DocComments}

; line comment

Function DocCommentNativeFunction() native
{Comment for Function DocCommentNativeFunction}

Function DocCommentGlobalNativeFunction() global native ; end of line comment
{Comment for Function DocCommentGlobalNativeFunction}

Function DocCommentGlobalFunction() global
{Comment for Function DocCommentGlobalNativeFunction}
;/ inline comment /;
EndFunction

var Property DocCommentAutoReadOnlyProperty AutoReadOnly
{Comment for Property DocCommentAutoReadOnlyProperty}
