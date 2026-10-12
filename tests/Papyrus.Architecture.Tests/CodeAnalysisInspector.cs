using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Papyrus.Architecture.Tests;

/// <summary>
/// IL-level scan of Papyrus.CodeAnalysis. Compiler-generated
/// <c>System.Reflection.Assembly*Attribute</c> metadata is allowed; every other
/// <c>System.Reflection</c> type (including Metadata) is a violation.
/// </summary>
internal static class CodeAnalysisInspector
{
    private static readonly HashSet<string> FileIoTypes = new(StringComparer.Ordinal)
    {
        "System.IO.File",
        "System.IO.Directory",
        "System.IO.FileStream",
        "System.IO.FileSystemWatcher",
        "System.IO.FileInfo",
        "System.IO.DirectoryInfo",
        "System.IO.StreamReader",
        "System.IO.StreamWriter",
    };

    private static readonly HashSet<string> AllowedReflectionTypes = new(StringComparer.Ordinal)
    {
        "System.Reflection.AssemblyCompanyAttribute",
        "System.Reflection.AssemblyConfigurationAttribute",
        "System.Reflection.AssemblyCopyrightAttribute",
        "System.Reflection.AssemblyCultureAttribute",
        "System.Reflection.AssemblyDefaultAliasAttribute",
        "System.Reflection.AssemblyDelaySignAttribute",
        "System.Reflection.AssemblyDescriptionAttribute",
        "System.Reflection.AssemblyFileVersionAttribute",
        "System.Reflection.AssemblyFlagsAttribute",
        "System.Reflection.AssemblyInformationalVersionAttribute",
        "System.Reflection.AssemblyKeyFileAttribute",
        "System.Reflection.AssemblyKeyNameAttribute",
        "System.Reflection.AssemblyMetadataAttribute",
        "System.Reflection.AssemblyProductAttribute",
        "System.Reflection.AssemblySignatureKeyAttribute",
        "System.Reflection.AssemblyTitleAttribute",
        "System.Reflection.AssemblyTrademarkAttribute",
        "System.Reflection.AssemblyVersionAttribute",
    };

    internal static AssemblyDefinition ReadCodeAnalysis()
    {
        var path = Assembly.Load("Papyrus.CodeAnalysis").Location;
        return AssemblyDefinition.ReadAssembly(
            path,
            new ReaderParameters { ReadSymbols = false, InMemory = true });
    }

    internal static IReadOnlyList<string> FindFileIoUsages(AssemblyDefinition assembly)
        => FindTypeUsages(assembly, FileIoTypes.Contains);

    internal static IReadOnlyList<string> FindReflectionUsages(AssemblyDefinition assembly)
    {
        return FindTypeUsages(
            assembly,
            fullName => IsInNamespace(fullName, "System.Reflection")
                        && !AllowedReflectionTypes.Contains(StripGenerics(fullName)));
    }

    internal static IReadOnlyList<string> FindRuntimeLoaderUsages(AssemblyDefinition assembly)
        => FindTypeUsages(assembly, fullName => IsInNamespace(fullName, "System.Runtime.Loader"));

    internal static IReadOnlyList<string> FindRequiresDynamicOrUnreferencedCode(AssemblyDefinition assembly)
    {
        var hits = new List<string>();
        foreach (var (provider, display) in EnumerateAttributeProviders(assembly))
        {
            foreach (var attribute in provider.CustomAttributes)
            {
                var name = attribute.AttributeType.FullName;
                if (name is "System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute"
                    or "System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute")
                {
                    hits.Add(display + " is marked [" + ShortAttributeName(name) + "]");
                }
            }
        }

        return hits;
    }

    internal static IReadOnlyList<string> FindReflectionFreeJsonSerializerCalls(AssemblyDefinition assembly)
    {
        var hits = new List<string>();
        foreach (var (method, instruction) in EnumerateCallInstructions(assembly))
        {
            if (instruction.Operand is not MethodReference called)
            {
                continue;
            }

            if (called.DeclaringType.FullName != "System.Text.Json.JsonSerializer")
            {
                continue;
            }

            if (!HasSourceGeneratedContextParameter(called))
            {
                hits.Add(method.FullName + " uses JsonSerializer." + called.Name
                         + " without a source-generated JsonSerializerContext/JsonTypeInfo");
            }
        }

        return hits;
    }

    internal static IReadOnlyList<string> FindMethodCalls(
        AssemblyDefinition assembly,
        string declaringType,
        string methodName)
    {
        var hits = new List<string>();
        foreach (var (method, instruction) in EnumerateCallInstructions(assembly))
        {
            if (instruction.Operand is not MethodReference called)
            {
                continue;
            }

            if (called.DeclaringType.FullName == declaringType && called.Name == methodName)
            {
                hits.Add(method.FullName + " calls " + declaringType + "." + methodName);
            }
        }

        return hits;
    }

    private static IReadOnlyList<string> FindTypeUsages(
        AssemblyDefinition assembly,
        Func<string, bool> isBanned)
    {
        var hits = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in assembly.Modules)
        {
            foreach (var typeRef in module.GetTypeReferences())
            {
                Record(hits, typeRef, isBanned, "type reference");
            }

            foreach (var memberRef in module.GetMemberReferences())
            {
                if (memberRef.DeclaringType is not null)
                {
                    Record(hits, memberRef.DeclaringType, isBanned, memberRef.FullName);
                }
            }

            foreach (var type in GetAllTypes(module))
            {
                Record(hits, type.BaseType, isBanned, type.FullName);
                foreach (var iface in type.Interfaces)
                {
                    Record(hits, iface.InterfaceType, isBanned, type.FullName);
                }

                foreach (var attribute in type.CustomAttributes)
                {
                    Record(hits, attribute.AttributeType, isBanned, type.FullName);
                }

                foreach (var field in type.Fields)
                {
                    Record(hits, field.FieldType, isBanned, field.FullName);
                }

                foreach (var method in type.Methods)
                {
                    Record(hits, method.ReturnType, isBanned, method.FullName);
                    foreach (var parameter in method.Parameters)
                    {
                        Record(hits, parameter.ParameterType, isBanned, method.FullName);
                    }

                    if (!method.HasBody)
                    {
                        continue;
                    }

                    foreach (var instruction in method.Body.Instructions)
                    {
                        switch (instruction.Operand)
                        {
                            case MethodReference called:
                                Record(hits, called.DeclaringType, isBanned, method.FullName);
                                Record(hits, called.ReturnType, isBanned, method.FullName);
                                break;
                            case FieldReference field:
                                Record(hits, field.DeclaringType, isBanned, method.FullName);
                                Record(hits, field.FieldType, isBanned, method.FullName);
                                break;
                            case TypeReference typeOperand:
                                Record(hits, typeOperand, isBanned, method.FullName);
                                break;
                        }
                    }
                }
            }
        }

        return hits.OrderBy(static h => h, StringComparer.Ordinal).ToList();
    }

    private static void Record(
        HashSet<string> hits,
        TypeReference? type,
        Func<string, bool> isBanned,
        string location)
    {
        if (type is null)
        {
            return;
        }

        foreach (var name in EnumerateTypeNames(type))
        {
            if (isBanned(name))
            {
                hits.Add(location + " uses " + name);
            }
        }
    }

    private static IEnumerable<string> EnumerateTypeNames(TypeReference type)
    {
        yield return type.FullName;

        if (type is GenericInstanceType generic)
        {
            foreach (var argument in generic.GenericArguments)
            {
                foreach (var name in EnumerateTypeNames(argument))
                {
                    yield return name;
                }
            }
        }
        else if (type is TypeSpecification spec && spec.ElementType is not null && !ReferenceEquals(spec.ElementType, type))
        {
            foreach (var name in EnumerateTypeNames(spec.ElementType))
            {
                yield return name;
            }
        }
    }

    private static IEnumerable<(MethodDefinition Method, Instruction Instruction)> EnumerateCallInstructions(
        AssemblyDefinition assembly)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var type in GetAllTypes(module))
            {
                foreach (var method in type.Methods)
                {
                    if (!method.HasBody)
                    {
                        continue;
                    }

                    foreach (var instruction in method.Body.Instructions)
                    {
                        // call/callvirt plus method-group conversions (ldftn / ldvirtftn).
                        if (instruction.Operand is MethodReference
                            && (instruction.OpCode.FlowControl == FlowControl.Call
                                || instruction.OpCode == OpCodes.Ldftn
                                || instruction.OpCode == OpCodes.Ldvirtftn))
                        {
                            yield return (method, instruction);
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<(Mono.Cecil.ICustomAttributeProvider Provider, string Display)> EnumerateAttributeProviders(
        AssemblyDefinition assembly)
    {
        yield return (assembly, assembly.Name.Name);
        foreach (var module in assembly.Modules)
        {
            yield return (module, module.Name);
            foreach (var type in GetAllTypes(module))
            {
                yield return (type, type.FullName);
                foreach (var field in type.Fields)
                {
                    yield return (field, field.FullName);
                }

                foreach (var property in type.Properties)
                {
                    yield return (property, property.FullName);
                }

                foreach (var ev in type.Events)
                {
                    yield return (ev, ev.FullName);
                }

                foreach (var method in type.Methods)
                {
                    yield return (method, method.FullName);
                    foreach (var parameter in method.Parameters)
                    {
                        yield return (parameter, method.FullName + " parameter " + parameter.Name);
                    }
                }
            }
        }
    }

    private static IEnumerable<TypeDefinition> GetAllTypes(ModuleDefinition module)
    {
        foreach (var type in module.Types)
        {
            foreach (var nested in GetTypeAndNested(type))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<TypeDefinition> GetTypeAndNested(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes)
        {
            foreach (var child in GetTypeAndNested(nested))
            {
                yield return child;
            }
        }
    }

    private static bool HasSourceGeneratedContextParameter(MethodReference method)
    {
        foreach (var parameter in method.Parameters)
        {
            foreach (var name in EnumerateTypeNames(parameter.ParameterType))
            {
                var core = StripGenerics(name);
                if (core is "System.Text.Json.Serialization.JsonSerializerContext"
                    or "System.Text.Json.Serialization.Metadata.JsonTypeInfo")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInNamespace(string fullName, string ns)
    {
        var core = StripGenerics(fullName);
        var slash = core.IndexOf('/');
        if (slash >= 0)
        {
            core = core[..slash];
        }

        return core == ns || core.StartsWith(ns + ".", StringComparison.Ordinal);
    }

    private static string StripGenerics(string fullName)
    {
        var tick = fullName.IndexOf('`');
        return tick >= 0 ? fullName[..tick] : fullName;
    }

    private static string ShortAttributeName(string fullName)
    {
        var last = fullName.LastIndexOf('.');
        var name = last >= 0 ? fullName[(last + 1)..] : fullName;
        return name.EndsWith("Attribute", StringComparison.Ordinal)
            ? name[..^"Attribute".Length]
            : name;
    }
}
