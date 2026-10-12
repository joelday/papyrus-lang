using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Papyrus.Pex;

/// <summary>
/// Canonicalizes incidental PEX parts so a differ can distinguish real
/// divergence from noise. Does not mutate the input.
/// </summary>
public static class PexNormalizer
{
    /// <summary>
    /// Clears source path, compile time, and user/machine names, and rebuilds
    /// the string table in first-use order. Debug info is left as-is.
    /// </summary>
    public static PexFile Normalize(PexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new PexFile
        {
            Magic = file.Magic,
            Version = file.Version,
            RawGameId = file.RawGameId,
            Game = file.Game,
            CompileTime = DateTimeOffset.UnixEpoch,
            SourcePath = "",
            UserName = "",
            MachineName = "",
            StringTable = FirstUseStringTable(file),
            DebugInfo = file.DebugInfo,
            UserFlags = file.UserFlags,
            Objects = file.Objects,
        };
    }

    /// <summary>
    /// SHA-256 (lowercase hex) of <c>Write(Normalize(file))</c>.
    /// </summary>
    public static string Hash(PexFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Convert.ToHexStringLower(SHA256.HashData(PexWriter.Write(Normalize(file))));
    }

    // The in-memory model stores strings, not table indices, so reordering
    // is a remap: Write emits indices from this table. Unused entries drop.
    private static ImmutableArray<string> FirstUseStringTable(PexFile file)
    {
        GameId game = GameIdMapping.Resolve(file.RawGameId, file.Version, file.Game)
                      ?? file.Game;
        bool hasExtendedLayout = game is GameId.Fallout4 or GameId.Fallout76 or GameId.Starfield;
        bool hasGuards = game == GameId.Starfield;

        var strings = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Intern(string? value)
        {
            string text = value ?? "";
            if (seen.Add(text))
                strings.Add(text);
        }

        void InternValue(PexValue value)
        {
            if (value.Type is PexValueType.Identifier or PexValueType.String)
                Intern(value.Text);
        }

        void InternFunction(PexFunction? function, bool named)
        {
            if (function is null)
                return;

            if (named)
                Intern(function.Name);
            Intern(function.ReturnTypeName);
            Intern(function.DocumentationString);

            foreach (PexFunctionParameter parameter in OrEmpty(function.Parameters))
            {
                Intern(parameter.Name);
                Intern(parameter.TypeName);
            }

            foreach (PexLocalVariable local in OrEmpty(function.Locals))
            {
                Intern(local.Name);
                Intern(local.TypeName);
            }

            foreach (PexInstruction instruction in OrEmpty(function.Instructions))
            {
                foreach (PexValue argument in OrEmpty(instruction.Arguments))
                    InternValue(argument);
                foreach (PexValue argument in OrEmpty(instruction.VariadicArguments))
                    InternValue(argument);
            }
        }

        if (file.DebugInfo is { } info)
        {
            foreach (PexDebugFunction function in OrEmpty(info.Functions))
            {
                Intern(function.ObjectName);
                Intern(function.StateName);
                Intern(function.FunctionName);
            }

            if (hasExtendedLayout)
            {
                foreach (PexDebugPropertyGroup group in OrEmpty(info.PropertyGroups))
                {
                    Intern(group.ObjectName);
                    Intern(group.GroupName);
                    Intern(group.DocumentationString);
                    foreach (string name in OrEmpty(group.Properties))
                        Intern(name);
                }

                foreach (PexDebugStructOrder order in OrEmpty(info.StructOrders))
                {
                    Intern(order.ObjectName);
                    Intern(order.StructName);
                    foreach (string member in OrEmpty(order.Members))
                        Intern(member);
                }
            }
        }

        foreach (PexUserFlag flag in OrEmpty(file.UserFlags))
            Intern(flag.Name);

        foreach (PexObject obj in OrEmpty(file.Objects))
        {
            Intern(obj.Name);
            Intern(obj.ParentClassName);
            Intern(obj.DocumentationString);
            Intern(obj.AutoStateName);

            if (hasExtendedLayout)
            {
                foreach (PexStruct struc in OrEmpty(obj.Structs))
                {
                    Intern(struc.Name);
                    foreach (PexStructMember member in OrEmpty(struc.Members))
                    {
                        Intern(member.Name);
                        Intern(member.TypeName);
                        InternValue(member.DefaultValue);
                        Intern(member.DocumentationString);
                    }
                }
            }

            foreach (PexVariable variable in OrEmpty(obj.Variables))
            {
                Intern(variable.Name);
                Intern(variable.TypeName);
                InternValue(variable.DefaultValue);
            }

            if (hasGuards)
            {
                foreach (PexGuard guard in OrEmpty(obj.Guards))
                    Intern(guard.Name);
            }

            foreach (PexProperty property in OrEmpty(obj.Properties))
            {
                Intern(property.Name);
                Intern(property.TypeName);
                Intern(property.DocumentationString);
                if (property.IsAuto)
                {
                    Intern(property.AutoVar);
                }
                else
                {
                    if (property.IsReadable)
                        InternFunction(property.ReadFunction, named: false);
                    if (property.IsWritable)
                        InternFunction(property.WriteFunction, named: false);
                }
            }

            foreach (PexState state in OrEmpty(obj.States))
            {
                Intern(state.Name);
                foreach (PexFunction function in OrEmpty(state.Functions))
                    InternFunction(function, named: true);
            }
        }

        return strings.ToImmutableArray();
    }

    private static ImmutableArray<T> OrEmpty<T>(ImmutableArray<T> array) =>
        array.IsDefault ? ImmutableArray<T>.Empty : array;
}
