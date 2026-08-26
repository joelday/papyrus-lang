using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.CodeAnalysis.Binding;
using Papyrus.CodeAnalysis.Symbols;
using Papyrus.CodeAnalysis.Syntax;
using Papyrus.CodeAnalysis.Text;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Emit;

internal static class EngineLimitChecker
{
    public static void Check(
        PexObject obj,
        EngineLimits limits,
        FlagsFile? flagsFile,
        DiagnosticSeverity severity,
        SyntaxTree? tree,
        IScriptSymbol script,
        List<Diagnostic> diagnostics)
    {
        var span = SpanOf(script);
        CheckCount(
            Length(obj.Properties),
            limits.MaxProperties,
            DiagnosticCatalog.TooManyProperties,
            severity,
            tree,
            span,
            diagnostics);
        CheckCount(
            Length(obj.Variables),
            limits.MaxVariables,
            DiagnosticCatalog.TooManyVariables,
            severity,
            tree,
            span,
            diagnostics);
        CheckCount(
            Length(obj.Guards),
            limits.MaxGuards,
            DiagnosticCatalog.TooManyGuards,
            severity,
            tree,
            span,
            diagnostics);

        var initialValues = 0;
        var variables = obj.Variables;
        if (!variables.IsDefaultOrEmpty)
        {
            for (var i = 0; i < variables.Length; i++)
            {
                if (variables[i].DefaultValue.Type != PexValueType.None)
                {
                    initialValues++;
                }
            }
        }

        CheckCount(
            initialValues,
            limits.MaxInitialValues,
            DiagnosticCatalog.TooManyInitialValues,
            severity,
            tree,
            span,
            diagnostics);

        var namedStates = 0;
        var states = obj.States;
        if (!states.IsDefaultOrEmpty)
        {
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (string.IsNullOrEmpty(state.Name))
                {
                    CheckCount(
                        Length(state.Functions),
                        limits.MaxFunctionsInEmptyState,
                        DiagnosticCatalog.TooManyFunctionsInEmptyState,
                        severity,
                        tree,
                        span,
                        diagnostics);
                }
                else
                {
                    namedStates++;
                    if (Length(state.Functions) > limits.MaxFunctionsPerNamedState)
                    {
                        Report(
                            DiagnosticCatalog.TooManyFunctionsInNamedState,
                            severity,
                            tree,
                            SpanOf(script, state.Name),
                            diagnostics,
                            state.Name,
                            Length(state.Functions),
                            limits.MaxFunctionsPerNamedState);
                    }
                }

                CheckFunctions(state.Functions, limits, severity, tree, script, diagnostics);
            }
        }

        CheckCount(
            namedStates,
            limits.MaxNamedStates,
            DiagnosticCatalog.TooManyNamedStates,
            severity,
            tree,
            span,
            diagnostics);

        var properties = obj.Properties;
        if (!properties.IsDefaultOrEmpty)
        {
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (property.ReadFunction is { } getter)
                {
                    CheckFunction(getter, limits, severity, tree, script, diagnostics);
                }

                if (property.WriteFunction is { } setter)
                {
                    CheckFunction(setter, limits, severity, tree, script, diagnostics);
                }
            }
        }

        if (flagsFile is not null)
        {
            CheckCount(
                Length(flagsFile.Flags),
                limits.MaxUserFlags,
                DiagnosticCatalog.TooManyUserFlags,
                severity,
                tree,
                span,
                diagnostics);
        }
    }

    public static void CheckArraySize(
        BoundExpression size,
        EngineLimits limits,
        DiagnosticSeverity severity,
        SyntaxTree? tree,
        TextSpan span,
        List<Diagnostic> diagnostics)
    {
        if (!TryGetInt(size, out var value) || value <= limits.MaxArrayElements)
        {
            return;
        }

        Report(
            DiagnosticCatalog.ArraySizeExceedsLimit,
            severity,
            tree,
            span,
            diagnostics,
            value,
            limits.MaxArrayElements);
    }

    private static void CheckFunctions(
        ImmutableArray<PexFunction> functions,
        EngineLimits limits,
        DiagnosticSeverity severity,
        SyntaxTree? tree,
        IScriptSymbol script,
        List<Diagnostic> diagnostics)
    {
        if (functions.IsDefaultOrEmpty)
        {
            return;
        }

        for (var i = 0; i < functions.Length; i++)
        {
            CheckFunction(functions[i], limits, severity, tree, script, diagnostics);
        }
    }

    private static void CheckFunction(
        PexFunction function,
        EngineLimits limits,
        DiagnosticSeverity severity,
        SyntaxTree? tree,
        IScriptSymbol script,
        List<Diagnostic> diagnostics)
    {
        var parameterCount = Length(function.Parameters);
        if (parameterCount > limits.MaxParameters)
        {
            Report(
                DiagnosticCatalog.TooManyParameters,
                severity,
                tree,
                SpanOf(script, function.Name),
                diagnostics,
                function.Name,
                parameterCount,
                limits.MaxParameters);
        }
    }

    private static bool TryGetInt(BoundExpression expression, out int value)
    {
        switch (expression)
        {
            case BoundLiteral { Value: int integer }:
                value = integer;
                return true;
            case BoundConversion conversion:
                return TryGetInt(conversion.Operand, out value);
            case BoundUnary { OperatorKind: SyntaxKind.PlusToken } unary:
                return TryGetInt(unary.Operand, out value);
            case BoundUnary { OperatorKind: SyntaxKind.MinusToken } unary when TryGetInt(unary.Operand, out var inner):
                value = unchecked(-inner);
                return true;
            default:
                value = 0;
                return false;
        }
    }

    private static void CheckCount(
        int count,
        int max,
        DiagnosticDescriptor descriptor,
        DiagnosticSeverity severity,
        SyntaxTree? tree,
        TextSpan span,
        List<Diagnostic> diagnostics)
    {
        if (count <= max)
        {
            return;
        }

        Report(descriptor, severity, tree, span, diagnostics, count, max);
    }

    private static void Report(
        DiagnosticDescriptor descriptor,
        DiagnosticSeverity severity,
        SyntaxTree? tree,
        TextSpan span,
        List<Diagnostic> diagnostics,
        params object?[] arguments)
    {
        diagnostics.Add(new Diagnostic(new DiagnosticInfo(descriptor, arguments), span, tree, severity));
    }

    private static int Length<T>(ImmutableArray<T> items) => items.IsDefault ? 0 : items.Length;

    private static TextSpan SpanOf(IScriptSymbol script, string? memberName = null)
    {
        if (!string.IsNullOrEmpty(memberName))
        {
            var members = script.GetMembers();
            for (var i = 0; i < members.Length; i++)
            {
                var span = SpanOfMember(members[i], memberName);
                if (span is { } found)
                {
                    return found;
                }
            }
        }

        if (script.DeclaringSyntaxReferences is { IsDefaultOrEmpty: false } refs)
        {
            return refs[0].Span;
        }

        return default;
    }

    private static TextSpan? SpanOfMember(ISymbol member, string name)
    {
        if (string.Equals(member.Name.Text, name, StringComparison.OrdinalIgnoreCase))
        {
            return DeclarationSpan(member) ?? ReferenceSpan(member);
        }

        if (member is IStateSymbol or IPropertySymbol)
        {
            var nested = member.GetMembers();
            for (var i = 0; i < nested.Length; i++)
            {
                var span = SpanOfMember(nested[i], name);
                if (span is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static TextSpan? DeclarationSpan(ISymbol symbol)
    {
        return symbol switch
        {
            FunctionSymbol { Declaration: { } syntax } => syntax.Name.Span,
            EventSymbol { Declaration: { } syntax } => syntax.Name.Span,
            PropertySymbol { Declaration: { } syntax } => syntax.Name.Span,
            VariableSymbol { Declaration: { } syntax } => syntax.Name.Span,
            _ => null,
        };
    }

    private static TextSpan? ReferenceSpan(ISymbol symbol)
    {
        if (symbol.DeclaringSyntaxReferences is { IsDefaultOrEmpty: false } refs)
        {
            return refs[0].Span;
        }

        return null;
    }
}
