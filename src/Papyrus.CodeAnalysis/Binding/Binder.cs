using System;
using Papyrus.CodeAnalysis.Symbols;

namespace Papyrus.CodeAnalysis.Binding;

internal static class Binder
{
    public static BoundFunction BindFunction(IFunctionSymbol function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var compilation = RequireCompilation(function.ContainingScript);
        return new BoundTreeBuilder(compilation, function.ContainingScript, function).BindFunction(function);
    }

    public static BoundScript BindScript(IScriptSymbol script)
    {
        ArgumentNullException.ThrowIfNull(script);
        return new BoundTreeBuilder(RequireCompilation(script), script, function: null).BindScript(script);
    }

    private static PapyrusCompilation RequireCompilation(IScriptSymbol? script)
    {
        if (script is SourceScriptSymbol source)
        {
            return source.Compilation;
        }

        throw new ArgumentException("Symbol is not backed by a source compilation.", nameof(script));
    }
}
