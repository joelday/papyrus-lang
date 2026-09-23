using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class LspJsonContextTests
{
    public static TheoryData<string, Type> RpcPayloadTypes()
    {
        var data = new TheoryData<string, Type>();
        foreach (var method in typeof(LanguageServerRpc).GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (method.GetCustomAttribute<JsonRpcMethodAttribute>() is not { } rpc)
            {
                continue;
            }

            foreach (var type in PayloadTypes(method))
            {
                data.Add(rpc.Name!, type);
            }
        }

        return data;
    }

    /// <summary>
    /// The server serializes only through the source-generated <see cref="LspJsonContext"/>,
    /// so a request or response type missing from it fails at runtime -- and StreamJsonRpc
    /// drops the connection -- even though handler-level tests never notice.
    /// </summary>
    [Theory]
    [MemberData(nameof(RpcPayloadTypes))]
    public void Every_rpc_payload_type_is_in_the_json_context(string method, Type type)
    {
        Assert.True(
            LspJsonContext.Default.GetTypeInfo(type) is not null,
            $"{method}: {type.Name} is not registered in LspJsonContext.");
    }

    private static IEnumerable<Type> PayloadTypes(MethodInfo method)
    {
        foreach (var parameter in method.GetParameters())
        {
            if (parameter.ParameterType != typeof(CancellationToken))
            {
                yield return parameter.ParameterType;
            }
        }

        var returnType = method.ReturnType;
        if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            yield return returnType.GetGenericArguments()[0];
        }
    }
}
