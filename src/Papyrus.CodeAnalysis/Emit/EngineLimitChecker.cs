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
        var guardCount = Length(obj.Guards);
        if (guardCount > limits.MaxGuards)
        {
            Report(
                DiagnosticCatalog.TooManyGuards,
                severity,
                tree,
                ExcessSpan(obj.Guards, limits.MaxGuards, static guard => guard.Name, script, span),
                diagnostics,
                guardCount,
                limits.MaxGuards);
        }

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

        if (namedStates > limits.MaxNamedStates)
        {
            Report(
                DiagnosticCatalog.TooManyNamedStates,
                severity,
                tree,
                ExcessNamedStateSpan(states, limits.MaxNamedStates, script, span),
                diagnostics,
                namedStates,
                limits.MaxNamedStates);
        }

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
                CountEmitableFlags(flagsFile),
                limits.MaxUserFlags,
                DiagnosticCatalog.TooManyUserFlags,
                severity,
                tree,
                span,
                diagnostics);

            var infos = flagsFile.Diagnostics;
            if (!infos.IsDefaultOrEmpty)
            {
                for (var i = 0; i < infos.Length; i++)
                {
                    diagnostics.Add(new Diagnostic(infos[i], span, tree, severity));
                }
            }
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
            case BoundBinary binary when TryGetInt(binary.Left, out var left) && TryGetInt(binary.Right, out var right):
                return TryEvaluateBinary(binary.OperatorKind, left, right, out value);
            default:
                value = 0;
                return false;
        }
    }

    private static bool TryEvaluateBinary(SyntaxKind operatorKind, int left, int right, out int value)
    {
        switch (operatorKind)
        {
            case SyntaxKind.PlusToken:
                value = unchecked(left + right);
                return true;
            case SyntaxKind.MinusToken:
                value = unchecked(left - right);
                return true;
            case SyntaxKind.AsteriskToken:
                value = unchecked(left * right);
                return true;
            case SyntaxKind.SlashToken when right != 0:
                value = left / right;
                return true;
            case SyntaxKind.PercentToken when right != 0:
                value = left % right;
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

    private static int CountEmitableFlags(FlagsFile flagsFile)
    {
        var flags = flagsFile.Flags;
        if (flags.IsDefaultOrEmpty)
        {
            return 0;
        }

        var count = 0;
        for (var i = 0; i < flags.Length; i++)
        {
            if (flags[i].BitIndex is >= 0 and <= 31)
            {
                count++;
            }
        }

        return count;
    }

    private static TextSpan ExcessSpan<T>(
        ImmutableArray<T> items,
        int max,
        Func<T, string> name,
        IScriptSymbol script,
        TextSpan fallback)
    {
        if (items.IsDefaultOrEmpty || max < 0 || max >= items.Length)
        {
            return fallback;
        }

        return SpanOf(script, name(items[max]));
    }

    private static TextSpan ExcessNamedStateSpan(
        ImmutableArray<PexState> states,
        int max,
        IScriptSymbol script,
        TextSpan fallback)
    {
        if (states.IsDefaultOrEmpty || max < 0)
        {
            return fallback;
        }

        var named = 0;
        for (var i = 0; i < states.Length; i++)
        {
            if (string.IsNullOrEmpty(states[i].Name))
            {
                continue;
            }

            if (named == max)
            {
                return SpanOf(script, states[i].Name);
            }

            named++;
        }

        return fallback;
    }

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
            StateSymbol { Declaration: { } syntax } => syntax.Name.Span,
            GuardSymbol { Declaration: { } syntax } => syntax.Name.Span,
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
