using System;
using System.Collections.Immutable;

namespace Papyrus.Pex;

/// <summary>
/// Walks an in-memory <see cref="PexFile"/> into a PEX-owned DTO.
/// This assembly must not reference Papyrus.CodeAnalysis.
/// </summary>
public static class PexReflector
{
    public static PexScriptMetadata Reflect(PexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        ImmutableArray<string> userFlagNames = MapUserFlagNames(file.UserFlags);
        ImmutableArray<PexObject> objects = OrEmpty(file.Objects);
        if (objects.IsEmpty)
        {
            return new PexScriptMetadata
            {
                UserFlagNames = userFlagNames,
            };
        }

        return MapObject(objects[0], userFlagNames);
    }

    private static PexScriptMetadata MapObject(PexObject obj, ImmutableArray<string> userFlagNames)
    {
        string parent = obj.ParentClassName ?? "";
        var functions = ImmutableArray.CreateBuilder<PexFunctionMetadata>();
        var states = ImmutableArray.CreateBuilder<PexStateMetadata>();

        foreach (PexState state in OrEmpty(obj.States))
        {
            ImmutableArray<PexFunctionMetadata> mapped = MapFunctions(state.Functions);
            if (string.IsNullOrEmpty(state.Name))
            {
                functions.AddRange(mapped);
                continue;
            }

            states.Add(new PexStateMetadata
            {
                Name = state.Name,
                IsAuto = string.Equals(state.Name, obj.AutoStateName, StringComparison.Ordinal),
                Functions = mapped,
            });
        }

        return new PexScriptMetadata
        {
            Name = PexQualifiedName.Parse(obj.Name),
            BaseType = string.IsNullOrEmpty(parent) ? null : PexQualifiedName.Parse(parent),
            Functions = functions.ToImmutable(),
            Properties = MapProperties(obj.Properties),
            Structs = MapStructs(obj.Structs),
            Guards = MapGuards(obj.Guards),
            States = states.ToImmutable(),
            UserFlagNames = userFlagNames,
        };
    }

    private static ImmutableArray<string> MapUserFlagNames(ImmutableArray<PexUserFlag> flags)
    {
        if (flags.IsDefaultOrEmpty)
            return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>(flags.Length);
        foreach (PexUserFlag flag in flags)
            builder.Add(flag.Name ?? "");
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<PexFunctionMetadata> MapFunctions(ImmutableArray<PexFunction> functions)
    {
        if (functions.IsDefaultOrEmpty)
            return ImmutableArray<PexFunctionMetadata>.Empty;

        var builder = ImmutableArray.CreateBuilder<PexFunctionMetadata>(functions.Length);
        foreach (PexFunction function in functions)
            builder.Add(MapFunction(function));
        return builder.MoveToImmutable();
    }

    private static PexFunctionMetadata MapFunction(PexFunction function)
    {
        return new PexFunctionMetadata
        {
            Name = function.Name ?? "",
            ReturnTypeName = function.ReturnTypeName ?? "",
            UserFlags = function.UserFlags,
            IsNative = function.IsNative,
            IsGlobal = function.IsGlobal,
            Parameters = MapParameters(function.Parameters),
        };
    }

    private static ImmutableArray<PexParameterMetadata> MapParameters(ImmutableArray<PexFunctionParameter> parameters)
    {
        if (parameters.IsDefaultOrEmpty)
            return ImmutableArray<PexParameterMetadata>.Empty;

        var builder = ImmutableArray.CreateBuilder<PexParameterMetadata>(parameters.Length);
        foreach (PexFunctionParameter parameter in parameters)
        {
            builder.Add(new PexParameterMetadata
            {
                Name = parameter.Name ?? "",
                TypeName = parameter.TypeName ?? "",
            });
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<PexPropertyMetadata> MapProperties(ImmutableArray<PexProperty> properties)
    {
        if (properties.IsDefaultOrEmpty)
            return ImmutableArray<PexPropertyMetadata>.Empty;

        var builder = ImmutableArray.CreateBuilder<PexPropertyMetadata>(properties.Length);
        foreach (PexProperty property in properties)
        {
            builder.Add(new PexPropertyMetadata
            {
                Name = property.Name ?? "",
                TypeName = property.TypeName ?? "",
                UserFlags = property.UserFlags,
                IsAuto = property.IsAuto,
                IsReadable = property.IsReadable,
                IsWritable = property.IsWritable,
                GetMethod = property.ReadFunction is null ? null : MapFunction(property.ReadFunction),
                SetMethod = property.WriteFunction is null ? null : MapFunction(property.WriteFunction),
            });
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<PexStructMetadata> MapStructs(ImmutableArray<PexStruct> structs)
    {
        if (structs.IsDefaultOrEmpty)
            return ImmutableArray<PexStructMetadata>.Empty;

        var builder = ImmutableArray.CreateBuilder<PexStructMetadata>(structs.Length);
        foreach (PexStruct struc in structs)
        {
            builder.Add(new PexStructMetadata
            {
                Name = struc.Name ?? "",
                Members = MapStructMembers(struc.Members),
            });
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<PexStructMemberMetadata> MapStructMembers(ImmutableArray<PexStructMember> members)
    {
        if (members.IsDefaultOrEmpty)
            return ImmutableArray<PexStructMemberMetadata>.Empty;

        var builder = ImmutableArray.CreateBuilder<PexStructMemberMetadata>(members.Length);
        foreach (PexStructMember member in members)
        {
            builder.Add(new PexStructMemberMetadata
            {
                Name = member.Name ?? "",
                TypeName = member.TypeName ?? "",
                UserFlags = member.UserFlags,
                IsConst = member.IsConst,
            });
        }

        return builder.MoveToImmutable();
    }

    private static ImmutableArray<PexGuardMetadata> MapGuards(ImmutableArray<PexGuard> guards)
    {
        if (guards.IsDefaultOrEmpty)
            return ImmutableArray<PexGuardMetadata>.Empty;

        var builder = ImmutableArray.CreateBuilder<PexGuardMetadata>(guards.Length);
        foreach (PexGuard guard in guards)
            builder.Add(new PexGuardMetadata { Name = guard.Name ?? "" });
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array) =>
        array.IsDefault ? ImmutableArray<T>.Empty : array;
}
