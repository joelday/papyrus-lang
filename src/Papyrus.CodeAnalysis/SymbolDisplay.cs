using System;
using System.Collections.Generic;
using System.Text;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;

namespace Papyrus.CodeAnalysis;

/// <summary>
/// Renders symbols the way Papyrus declares them (<c>Int Function Twice(Int a) Global</c>,
/// <c>Base Property Other Auto</c>), for hover, completion, and signature help. The text is
/// valid Papyrus, so editors can syntax-highlight it with the language grammar.
/// </summary>
public static class SymbolDisplay
{
    public static string ToDeclarationString(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        return symbol switch
        {
            IScriptSymbol script => ScriptDeclaration(script),
            IEventSymbol @event => EventDeclaration(@event),
            IFunctionSymbol function => FunctionDeclaration(function),
            IPropertySymbol property => PropertyDeclaration(property),
            IParameterSymbol parameter => ParameterDeclaration(parameter, DefaultValueText(parameter)),
            IVariableSymbol variable => VariableDeclaration(variable),
            IStructSymbol struc => "Struct " + struc.Name.Text,
            IStateSymbol state => (state.IsAuto ? "Auto State " : "State ") + state.Name.Text,
            ICustomEventSymbol customEvent => "CustomEvent " + customEvent.Name.Text,
            IGuardSymbol guard => "Guard " + guard.Name.Text,
            ITypeSymbol type => TypeText(type),
            _ => symbol.Name.Text,
        };
    }

    /// <summary>A parameter as it appears in its function's declaration: <c>Int a = 5</c>.</summary>
    public static string ToParameterString(IParameterSymbol parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        return ParameterDeclaration(parameter, DefaultValueText(parameter));
    }

    /// <summary>
    /// The text of the symbol's doc comment (the <c>{...}</c> after its declaration), without
    /// braces and with common indentation removed, or null when it has none.
    /// </summary>
    public static string? GetDocumentation(ISymbol symbol)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        var docComment = symbol switch
        {
            FunctionSymbol { Declaration: { } syntax } => syntax.DocComment,
            EventSymbol { Declaration: { } syntax } => syntax.DocComment,
            PropertySymbol { Declaration: { } syntax } => syntax.DocComment,
            VariableSymbol { Declaration: { } syntax } => syntax.DocComment,
            IScriptSymbol script => ScriptDocComment(script),
            _ => default,
        };

        return docComment.Kind == SyntaxKind.DocCommentToken ? CleanDocComment(docComment.Text) : null;
    }

    /// <summary>
    /// A type as written in source: <c>Int[]</c>, <c>Hardcore:HC_ManagerScript</c>,
    /// <c>WorkshopDataScript:WorkshopVendorType</c>.
    /// </summary>
    public static string TypeText(ITypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);
        switch (type.TypeKind)
        {
            case TypeKind.Array when type.GetArrayElementType() is { } element:
                return TypeText(element) + "[]";
            case TypeKind.Script when type is IScriptSymbol script:
                return script.QualifiedName.ToString();
            case TypeKind.Struct when type.ContainingScript is { } owner:
                return owner.QualifiedName + ":" + type.Name.Text;
            case TypeKind.Intrinsic or TypeKind.Var:
                return CanonicalIntrinsic(type.Name.Text);
            default:
                return type.Name.Text.Length > 0 ? type.Name.Text : type.TypeName.ToString();
        }
    }

    private static string ScriptDeclaration(IScriptSymbol script)
    {
        var builder = new StringBuilder("ScriptName ").Append(script.QualifiedName);
        if (script.BaseType is { TypeKind: not TypeKind.Error } baseType)
        {
            builder.Append(" Extends ").Append(baseType.QualifiedName);
        }

        if (script.IsNative)
        {
            builder.Append(" Native");
        }

        return builder.ToString();
    }

    private static string FunctionDeclaration(IFunctionSymbol function)
    {
        var builder = new StringBuilder();
        if (!IsNone(function.ReturnType))
        {
            builder.Append(TypeText(function.ReturnType)).Append(' ');
        }

        builder.Append("Function ").Append(function.Name.Text);
        AppendParameters(builder, function);
        if (function.IsGlobal)
        {
            builder.Append(" Global");
        }

        if (function.IsNative)
        {
            builder.Append(" Native");
        }

        return builder.ToString();
    }

    private static string EventDeclaration(IEventSymbol @event)
    {
        var builder = new StringBuilder("Event ");
        if (@event is EventSymbol { Declaration: { RemoteScript: { IsMissing: false } remoteScript } syntax })
        {
            // Stored as ::remote_<Script>_<Event>; show it as written.
            builder.Append(remoteScript.ToFullString().Trim()).Append('.').Append(syntax.Name.Identifier.Text);
        }
        else
        {
            builder.Append(@event.Name.Text);
        }

        AppendParameters(builder, @event);
        if (@event.IsNative)
        {
            builder.Append(" Native");
        }

        return builder.ToString();
    }

    private static string PropertyDeclaration(IPropertySymbol property)
    {
        var builder = new StringBuilder(TypeText(property.Type)).Append(" Property ").Append(property.Name.Text);
        if (property is PropertySymbol { Declaration: { } syntax })
        {
            AppendInitializerAndFlags(builder, syntax.EqualsValue, syntax.Flags);
        }
        else if (property.IsAuto)
        {
            builder.Append(" Auto");
        }

        return builder.ToString();
    }

    private static string VariableDeclaration(IVariableSymbol variable)
    {
        var builder = new StringBuilder(TypeText(variable.Type)).Append(' ').Append(variable.Name.Text);
        if (variable is VariableSymbol { Declaration: { } syntax })
        {
            AppendInitializerAndFlags(builder, syntax.EqualsValue, syntax.Flags);
        }
        else if (variable.IsConst && variable.ContainingSymbol is not IFunctionSymbol)
        {
            builder.Append(" Const");
        }

        return builder.ToString();
    }

    private static string ParameterDeclaration(IParameterSymbol parameter, string? defaultValue)
    {
        var text = TypeText(parameter.Type) + " " + parameter.Name.Text;
        return defaultValue is null ? text : text + " = " + defaultValue;
    }

    private static void AppendParameters(StringBuilder builder, IFunctionSymbol function)
    {
        builder.Append('(');
        for (var i = 0; i < function.Parameters.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(ToParameterString(function.Parameters[i]));
        }

        builder.Append(')');
    }

    private static void AppendInitializerAndFlags(
        StringBuilder builder,
        EqualsValueClauseSyntax? equalsValue,
        SyntaxList<FlagSyntax> flags)
    {
        if (equalsValue is not null)
        {
            builder.Append(" = ").Append(equalsValue.Value.ToFullString().Trim());
        }

        for (var i = 0; i < flags.Count; i++)
        {
            var flag = flags.Get(i).FlagToken.Text;
            if (flag.Length > 0)
            {
                builder.Append(' ').Append(flag);
            }
        }
    }

    // Only source-declared parameters carry their default's text; PEX parameters just
    // record that a default exists.
    private static string? DefaultValueText(IParameterSymbol parameter)
    {
        if (!parameter.HasDefaultValue)
        {
            return null;
        }

        var parameters = parameter.ContainingSymbol switch
        {
            FunctionSymbol { Declaration: { } syntax } => syntax.ParameterList.Parameters,
            EventSymbol { Declaration: { } syntax } => syntax.ParameterList.Parameters,
            _ => default,
        };

        for (var i = 0; i < parameters.Count; i++)
        {
            var syntax = parameters.Get(i);
            if (syntax.Name.Identifier.Text.Equals(parameter.Name.Text, StringComparison.OrdinalIgnoreCase)
                && syntax.EqualsValue is { } equalsValue)
            {
                return equalsValue.Value.ToFullString().Trim();
            }
        }

        return "...";
    }

    private static SyntaxToken ScriptDocComment(IScriptSymbol script)
    {
        foreach (var reference in script.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree is { } tree && tree.TryGetRoot(out var root))
            {
                return root.DocComment;
            }
        }

        return default;
    }

    private static string? CleanDocComment(string text)
    {
        var body = text.Trim();
        if (body.StartsWith('{'))
        {
            body = body[1..];
        }

        if (body.EndsWith('}'))
        {
            body = body[..^1];
        }

        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var indent = int.MaxValue;
        for (var i = 1; i < lines.Length; i++)
        {
            var content = lines[i].TrimStart(' ', '\t');
            if (content.Length > 0)
            {
                indent = Math.Min(indent, lines[i].Length - content.Length);
            }
        }

        var cleaned = new List<string>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var line = i > 0 && indent != int.MaxValue && lines[i].Length >= indent ? lines[i][indent..] : lines[i];
            cleaned.Add(line.TrimEnd());
        }

        var result = string.Join('\n', cleaned).Trim();
        return result.Length == 0 ? null : result;
    }

    private static bool IsNone(ITypeSymbol type)
        => type.TypeKind == TypeKind.Intrinsic && type.Name.Text.Equals("None", StringComparison.OrdinalIgnoreCase);

    private static string CanonicalIntrinsic(string name)
    {
        foreach (var canonical in s_intrinsics)
        {
            if (canonical.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return canonical;
            }
        }

        return name;
    }

    private static readonly string[] s_intrinsics =
    [
        "Int", "Float", "String", "Bool", "Var", "None",
        IntrinsicTypeNames.CustomEventName, IntrinsicTypeNames.ScriptEventName,
    ];
}
