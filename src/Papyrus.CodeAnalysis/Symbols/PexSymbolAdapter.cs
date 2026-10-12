using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Papyrus.Pex;

namespace Papyrus.CodeAnalysis.Symbols;

public static class PexSymbolAdapter
{
    public static IScriptSymbol ToScriptSymbol(
        PexScriptMetadata metadata,
        NameTable names,
        PapyrusDialect? dialect = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(names);
        return new Builder(metadata, names, dialect ?? PapyrusDialect.SkyrimSe).Build();
    }

    public static QualifiedName ToQualifiedName(PexQualifiedName name, NameTable names)
    {
        ArgumentNullException.ThrowIfNull(names);
        return QualifiedName.Parse(name.Text, names);
    }

    public static TypeName ToTypeName(PexQualifiedName name, NameTable names)
    {
        ArgumentNullException.ThrowIfNull(names);
        PapyrusName? structName = name.StructName is null ? null : names.Get(name.StructName);
        return new TypeName(ToQualifiedName(name, names), isArray: false, structName);
    }

    private sealed class Builder
    {
        private readonly PexScriptMetadata _metadata;
        private readonly NameTable _names;
        private readonly PapyrusDialect _dialect;
        private readonly Dictionary<TypeName, ITypeSymbol> _types = new();

        public Builder(PexScriptMetadata metadata, NameTable names, PapyrusDialect dialect)
        {
            _metadata = metadata;
            _names = names;
            _dialect = dialect;
        }

        public IScriptSymbol Build()
        {
            var qualified = ToQualifiedName(_metadata.Name, _names);
            var typeName = ToTypeName(_metadata.Name, _names);
            var script = new ScriptSymbol(SimpleName(qualified), qualified, typeName);
            _types[typeName] = script;

            var structs = OrEmpty(_metadata.Structs);
            var structSymbols = new StructSymbol[structs.Length];
            for (var i = 0; i < structs.Length; i++)
            {
                var structMeta = structs[i];
                var structName = _names.Get(structMeta.Name);
                var structTypeName = new TypeName(qualified, isArray: false, structName);
                var struc = new StructSymbol(structName, script, structTypeName);
                structSymbols[i] = struc;
                _types[structTypeName] = struc;
                _types.TryAdd(new TypeName(new QualifiedName(structName)), struc);
            }

            for (var i = 0; i < structSymbols.Length; i++)
            {
                structSymbols[i].SetMembers(MapStructMembers(structs[i], structSymbols[i], script));
            }

            script.BaseType = MapBaseType();

            var members = ImmutableArray.CreateBuilder<ISymbol>();
            foreach (var function in OrEmpty(_metadata.Functions))
            {
                members.Add(MapFunction(function, script, script));
            }

            foreach (var property in OrEmpty(_metadata.Properties))
            {
                members.Add(MapProperty(property, script));
            }

            foreach (var struc in structSymbols)
            {
                members.Add(struc);
            }

            foreach (var guard in OrEmpty(_metadata.Guards))
            {
                members.Add(new GuardSymbol(_names.Get(guard.Name), script));
            }

            foreach (var state in OrEmpty(_metadata.States))
            {
                members.Add(MapState(state, script));
            }

            script.SetMembers(members.ToImmutable());
            return script;
        }

        private IScriptSymbol? MapBaseType()
        {
            if (_metadata.BaseType is not { } baseName || string.IsNullOrEmpty(baseName.Text))
            {
                return null;
            }

            var type = GetType(ToTypeName(baseName, _names));
            return type as IScriptSymbol ?? new ScriptSymbol(
                SimpleName(ToQualifiedName(baseName, _names)),
                ToQualifiedName(baseName, _names),
                ToTypeName(baseName, _names));
        }

        private IStateSymbol MapState(PexStateMetadata metadata, IScriptSymbol script)
        {
            var state = new StateSymbol(_names.Get(metadata.Name), script, metadata.IsAuto);
            var functions = OrEmpty(metadata.Functions);
            var members = ImmutableArray.CreateBuilder<ISymbol>(functions.Length);
            foreach (var function in functions)
            {
                members.Add(MapFunction(function, state, script));
            }

            state.SetMembers(members.ToImmutable());
            return state;
        }

        private IPropertySymbol MapProperty(PexPropertyMetadata metadata, IScriptSymbol script)
        {
            var property = new PropertySymbol(
                _names.Get(metadata.Name),
                script,
                script,
                GetType(metadata.TypeName),
                metadata.IsAuto);

            if (metadata.GetMethod is { } getMethod)
            {
                property.GetMethod = MapFunction(getMethod, property, script);
            }

            if (metadata.SetMethod is { } setMethod)
            {
                property.SetMethod = MapFunction(setMethod, property, script);
            }

            return property;
        }

        private FunctionSymbol MapFunction(
            PexFunctionMetadata metadata,
            ISymbol containing,
            IScriptSymbol script)
        {
            var function = new FunctionSymbol(
                _names.Get(metadata.Name),
                containing,
                script,
                GetType(metadata.ReturnTypeName),
                metadata.IsGlobal,
                metadata.IsNative);

            var parameters = OrEmpty(metadata.Parameters);
            if (!parameters.IsEmpty)
            {
                var builder = ImmutableArray.CreateBuilder<IParameterSymbol>(parameters.Length);
                foreach (var parameter in parameters)
                {
                    builder.Add(new ParameterSymbol(
                        _names.Get(parameter.Name),
                        function,
                        script,
                        GetType(parameter.TypeName),
                        hasDefaultValue: false));
                }

                function.SetParameters(builder.MoveToImmutable());
            }

            return function;
        }

        private ImmutableArray<ISymbol> MapStructMembers(
            PexStructMetadata metadata,
            StructSymbol struc,
            IScriptSymbol script)
        {
            var members = OrEmpty(metadata.Members);
            if (members.IsEmpty)
            {
                return ImmutableArray<ISymbol>.Empty;
            }

            var builder = ImmutableArray.CreateBuilder<ISymbol>(members.Length);
            foreach (var member in members)
            {
                builder.Add(new VariableSymbol(
                    _names.Get(member.Name),
                    struc,
                    script,
                    GetType(member.TypeName),
                    member.IsConst));
            }

            return builder.MoveToImmutable();
        }

        private ITypeSymbol GetType(string typeText)
        {
            if (string.IsNullOrEmpty(typeText))
            {
                return GetIntrinsic("None");
            }

            return GetType(TypeName.Parse(typeText, _names));
        }

        private ITypeSymbol GetType(TypeName typeName)
        {
            if (_types.TryGetValue(typeName, out var existing))
            {
                return existing;
            }

            if (typeName.IsArray)
            {
                var element = GetType(new TypeName(typeName.Name, isArray: false, typeName.StructName));
                var array = new ArrayTypeSymbol(element, _names, _dialect);
                _types[typeName] = array;
                return array;
            }

            if (TryGetIntrinsic(typeName, out var intrinsic))
            {
                _types[typeName] = intrinsic;
                return intrinsic;
            }

            if (typeName.StructName is { } structName)
            {
                var containing = GetScript(typeName.Name);
                var struc = new StructSymbol(structName, containing, typeName);
                _types[typeName] = struc;
                return struc;
            }

            var stub = new ScriptSymbol(SimpleName(typeName.Name), typeName.Name, typeName);
            _types[typeName] = stub;
            return stub;
        }

        private IScriptSymbol GetScript(QualifiedName name)
        {
            var typeName = new TypeName(name);
            if (_types.TryGetValue(typeName, out var existing) && existing is IScriptSymbol script)
            {
                return script;
            }

            var stub = new ScriptSymbol(SimpleName(name), name, typeName);
            _types[typeName] = stub;
            return stub;
        }

        private IntrinsicTypeSymbol GetIntrinsic(string name)
        {
            var typeName = new TypeName(new QualifiedName(_names.Get(name)));
            if (_types.TryGetValue(typeName, out var existing) && existing is IntrinsicTypeSymbol intrinsic)
            {
                return intrinsic;
            }

            intrinsic = IntrinsicTypeSymbol.Create(_names, name);
            _types[typeName] = intrinsic;
            return intrinsic;
        }

        private bool TryGetIntrinsic(TypeName typeName, out IntrinsicTypeSymbol intrinsic)
        {
            intrinsic = null!;
            if (typeName.IsArray || typeName.StructName is not null || typeName.Name.Parts.Length != 1)
            {
                return false;
            }

            var text = typeName.Name.Parts[0].Text;
            if (!IsIntrinsicName(text))
            {
                return false;
            }

            intrinsic = GetIntrinsic(text);
            return true;
        }

        // PEX records CustomEventName/ScriptEventName parameters as String, so the
        // dialect-only event-name types never appear here.
        private static bool IsIntrinsicName(string text) => IntrinsicTypeNames.IsIntrinsic(text, dialect: null);

        private PapyrusName SimpleName(QualifiedName qualified)
        {
            var parts = qualified.Parts.IsDefault ? ImmutableArray<PapyrusName>.Empty : qualified.Parts;
            return parts.IsEmpty ? _names.Get(string.Empty) : parts[^1];
        }

        private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array)
            => array.IsDefault ? ImmutableArray<T>.Empty : array;
    }
}
