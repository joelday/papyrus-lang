using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Papyrus.LanguageServer;
using Xunit;

namespace Papyrus.LanguageServer.Tests;

public sealed class LanguageServerHostTests
{
    [Fact]
    public void Version_flag_prints_assembly_version()
    {
        using var stdout = new StringWriter();
        Assert.True(Program.TryHandleCli(["--version"], stdout));
        Assert.Equal(Program.Version, stdout.ToString().Trim());

        using var empty = new StringWriter();
        Assert.False(Program.TryHandleCli([], empty));
        Assert.Empty(empty.ToString());
    }

    [Fact]
    public async Task Initialize_returns_result()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        await using var session = Session.Start(cancellationToken);

        var response = await session.RequestAsync(
            1,
            "initialize",
            """{"processId":1,"capabilities":{}}""",
            cancellationToken);

        using var document = JsonDocument.Parse(response);
        Assert.False(document.RootElement.TryGetProperty("error", out _), response);
        Assert.True(document.RootElement.TryGetProperty("result", out var result), response);
        Assert.True(result.TryGetProperty("capabilities", out var capabilities), response);
        Assert.Equal(JsonValueKind.Object, capabilities.ValueKind);
        Assert.True(result.TryGetProperty("serverInfo", out var serverInfo), response);
        Assert.Equal("Papyrus Language Server", serverInfo.GetProperty("name").GetString());

        await session.ShutdownAndExitAsync(expectedCode: 0, cancellationToken);
    }

    [Fact]
    public async Task Shutdown_then_exit_returns_zero()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        await using var session = Session.Start(cancellationToken);

        await session.RequestAsync(
            1,
            "initialize",
            """{"capabilities":{}}""",
            cancellationToken);
        await session.NotifyAsync("initialized", "{}", cancellationToken);

        var shutdown = await session.RequestAsync(2, "shutdown", parameters: null, cancellationToken);
        using var document = JsonDocument.Parse(shutdown);
        Assert.False(document.RootElement.TryGetProperty("error", out _));
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("result").ValueKind);

        await session.ShutdownAndExitAsync(expectedCode: 0, cancellationToken, alreadyShutDown: true);
    }

    [Fact]
    public async Task Exit_without_shutdown_returns_nonzero()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        await using var session = Session.Start(cancellationToken);

        await session.RequestAsync(
            1,
            "initialize",
            """{"capabilities":{}}""",
            cancellationToken);
        await session.NotifyAsync("exit", cancellationToken: cancellationToken);

        Assert.Equal(1, await session.HostTask);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    public async Task Shutdown_then_exit_with_params_returns_zero(string parameters)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        await using var session = Session.Start(cancellationToken);

        await session.RequestAsync(
            1,
            "initialize",
            """{"capabilities":{}}""",
            cancellationToken);
        await session.NotifyAsync("initialized", parameters, cancellationToken);

        var shutdown = await session.RequestAsync(2, "shutdown", parameters, cancellationToken);
        using var document = JsonDocument.Parse(shutdown);
        Assert.False(document.RootElement.TryGetProperty("error", out _), shutdown);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("result").ValueKind);

        await session.NotifyAsync("exit", parameters, cancellationToken);
        Assert.Equal(0, await session.HostTask.WaitAsync(cancellationToken));
    }

    [Fact]
    public async Task Snapshot_is_non_mutating_and_didOpen_advances_generation()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        await using var session = Session.Start(cancellationToken);

        await session.RequestAsync(
            1,
            "initialize",
            """{"capabilities":{}}""",
            cancellationToken);

        var before = await session.RequestAsync(2, "papyrus/snapshot", "{}", cancellationToken);
        Assert.Equal(0, ReadGeneration(before));

        var didOpen = await session.RequestAsync(
            3,
            "textDocument/didOpen",
            """{"textDocument":{"uri":"file:///tmp/Foo.psc","languageId":"papyrus","version":1,"text":"ScriptName Foo\n"}}""",
            cancellationToken);
        AssertNoError(didOpen);

        var afterOpen = await session.RequestAsync(4, "papyrus/snapshot", "{}", cancellationToken);
        Assert.Equal(1, ReadGeneration(afterOpen));

        var didChange = await session.RequestAsync(
            5,
            "textDocument/didChange",
            """{"textDocument":{"uri":"file:///tmp/Foo.psc","version":2},"contentChanges":[{"text":"ScriptName Foo\nInt x = 1\n"}]}""",
            cancellationToken);
        AssertNoError(didChange);

        var didClose = await session.RequestAsync(
            6,
            "textDocument/didClose",
            """{"textDocument":{"uri":"file:///tmp/Foo.psc"}}""",
            cancellationToken);
        AssertNoError(didClose);

        var afterClose = await session.RequestAsync(7, "papyrus/snapshot", "{}", cancellationToken);
        Assert.Equal(3, ReadGeneration(afterClose));

        await session.ShutdownAndExitAsync(expectedCode: 0, cancellationToken);
    }

    [Fact]
    public async Task Initialized_loads_ppj_from_workspace_folders_inferring_game_from_flags()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        var root = CreateTempDirectory();
        try
        {
            // Mirrors the Fallout 4 test-scripts layout: no Game attribute, the
            // flags file is the only thing identifying the game.
            File.WriteAllText(
                Path.Combine(root, "Fallout4.ppj"),
                """
                <PapyrusProject Flags="Institute_Papyrus_Flags.flg">
                  <Folders>
                    <Folder>.</Folder>
                  </Folders>
                </PapyrusProject>
                """);
            var scriptPath = Path.Combine(root, "StructUser.psc");
            const string script = "ScriptName StructUser Native\nStruct Point\n  Int X\nEndStruct\nUnknownType Baz\n";
            File.WriteAllText(scriptPath, script);
            var scriptUri = JsonString(new Uri(scriptPath).AbsoluteUri);
            var scriptText = JsonString(script);

            await using var session = Session.Start(cancellationToken);
            await session.RequestAsync(1, "initialize", InitializeWithFolders(root), cancellationToken);
            await session.NotifyAsync("initialized", "{}", cancellationToken);

            AssertNoError(
                await session.RequestAsync(
                    2,
                    "textDocument/didOpen",
                    $$$"""{"textDocument":{"uri":{{{scriptUri}}},"languageId":"papyrus","version":1,"text":{{{scriptText}}}}}""",
                    cancellationToken));

            var infos = await session.RequestAsync(3, "papyrus/projectInfos", "{}", cancellationToken);
            using (var document = JsonDocument.Parse(infos))
            {
                var projects = document.RootElement.GetProperty("result").GetProperty("projects");
                Assert.Equal(1, projects.GetArrayLength());
                Assert.Equal("Fallout4", projects[0].GetProperty("name").GetString());
            }

            var diagnostics = await session.RequestAsync(
                4,
                "textDocument/diagnostic",
                $$$"""{"textDocument":{"uri":{{{scriptUri}}}}}""",
                cancellationToken);
            using (var document = JsonDocument.Parse(diagnostics))
            {
                // Only the intentional unresolved type: that proves the document was
                // analyzed as part of the project, and under the Skyrim fallback `Struct`
                // would be an identifier and report a second, unresolved 'Struct' type.
                var item = Assert.Single(
                    document.RootElement.GetProperty("result").GetProperty("items").EnumerateArray());
                Assert.Equal("PAP1003", item.GetProperty("code").GetString());
                Assert.Contains("UnknownType", item.GetProperty("message").GetString(), StringComparison.Ordinal);
            }

            await session.ShutdownAndExitAsync(expectedCode: 0, cancellationToken);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DidChangeWorkspaceFolders_loads_and_unloads_projects()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        var root = CreateTempDirectory();
        try
        {
            var nested = Directory.CreateDirectory(Path.Combine(root, "mods", "demo")).FullName;
            File.WriteAllText(Path.Combine(nested, "demo.ppj"), """<PapyrusProject Game="sse" />""");
            var ignored = Directory.CreateDirectory(Path.Combine(root, "node_modules", "pkg")).FullName;
            File.WriteAllText(Path.Combine(ignored, "ignored.ppj"), """<PapyrusProject Game="sse" />""");
            var folder = $$"""{"uri":{{JsonString(new Uri(root).AbsoluteUri)}},"name":"root"}""";

            await using var session = Session.Start(cancellationToken);
            await session.RequestAsync(1, "initialize", """{"capabilities":{},"workspaceFolders":[]}""", cancellationToken);
            await session.NotifyAsync("initialized", "{}", cancellationToken);
            Assert.Equal(0, await CountProjectsAsync(session, 2, cancellationToken));

            await session.NotifyAsync(
                "workspace/didChangeWorkspaceFolders",
                $$$"""{"event":{"added":[{{{folder}}}],"removed":[]}}""",
                cancellationToken);
            Assert.Equal(1, await CountProjectsAsync(session, 3, cancellationToken));

            await session.NotifyAsync(
                "workspace/didChangeWorkspaceFolders",
                $$$"""{"event":{"added":[],"removed":[{{{folder}}}]}}""",
                cancellationToken);
            Assert.Equal(0, await CountProjectsAsync(session, 4, cancellationToken));

            await session.ShutdownAndExitAsync(expectedCode: 0, cancellationToken);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Project_without_identifiable_game_logs_a_warning()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "mystery.ppj"), """<PapyrusProject Flags="Custom.flg" />""");

            await using var session = Session.Start(cancellationToken);
            await session.RequestAsync(1, "initialize", InitializeWithFolders(root), cancellationToken);
            await session.NotifyAsync("initialized", "{}", cancellationToken);
            Assert.Equal(1, await CountProjectsAsync(session, 2, cancellationToken));

            Assert.Contains(
                session.Notifications,
                message =>
                {
                    using var document = JsonDocument.Parse(message);
                    var element = document.RootElement;
                    return element.GetProperty("method").GetString() == "window/logMessage"
                        && element.GetProperty("params").GetProperty("type").GetInt32() == 2
                        && element.GetProperty("params").GetProperty("message").GetString()!.Contains(
                            "mystery.ppj",
                            StringComparison.Ordinal);
                });

            await session.ShutdownAndExitAsync(expectedCode: 0, cancellationToken);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string JsonString(string value) => "\"" + JsonEncodedText.Encode(value) + "\"";

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "papyrus-lsp-folders-" + Guid.NewGuid().ToString("N"));
        return Directory.CreateDirectory(root).FullName;
    }

    private static string InitializeWithFolders(string root)
    {
        var uri = JsonString(new Uri(root).AbsoluteUri);
        return $$"""{"capabilities":{},"workspaceFolders":[{"uri":{{uri}},"name":"root"}]}""";
    }

    private static async Task<int> CountProjectsAsync(Session session, int id, CancellationToken cancellationToken)
    {
        var response = await session.RequestAsync(id, "papyrus/projectInfos", "{}", cancellationToken);
        using var document = JsonDocument.Parse(response);
        return document.RootElement.GetProperty("result").GetProperty("projects").GetArrayLength();
    }

    [Fact]
    public async Task Initialize_advertises_full_text_document_sync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = timeout.Token;
        await using var session = Session.Start(cancellationToken);

        var response = await session.RequestAsync(
            1,
            "initialize",
            """{"capabilities":{}}""",
            cancellationToken);

        using var document = JsonDocument.Parse(response);
        Assert.False(document.RootElement.TryGetProperty("error", out _), response);
        var sync = document.RootElement.GetProperty("result").GetProperty("capabilities").GetProperty("textDocumentSync");
        Assert.True(sync.GetProperty("openClose").GetBoolean());
        Assert.Equal((int)TextDocumentSyncKind.Full, sync.GetProperty("change").GetInt32());

        await session.ShutdownAndExitAsync(expectedCode: 0, cancellationToken);
    }

    private static void AssertNoError(string response)
    {
        using var document = JsonDocument.Parse(response);
        Assert.False(document.RootElement.TryGetProperty("error", out _), response);
    }

    private static int ReadGeneration(string response)
    {
        using var document = JsonDocument.Parse(response);
        Assert.False(document.RootElement.TryGetProperty("error", out _), response);
        return document.RootElement.GetProperty("result").GetProperty("generation").GetInt32();
    }

    private sealed class Session : IAsyncDisposable
    {
        private readonly Pipe _clientToServer;
        private readonly Pipe _serverToClient;
        private int _nextId = 10;

        private Session(Pipe clientToServer, Pipe serverToClient, Task<int> hostTask)
        {
            _clientToServer = clientToServer;
            _serverToClient = serverToClient;
            HostTask = hostTask;
        }

        internal Task<int> HostTask { get; }

        internal static Session Start(CancellationToken cancellationToken)
        {
            var clientToServer = new Pipe();
            var serverToClient = new Pipe();
            var hostTask = LanguageServerHost.RunAsync(
                clientToServer.Reader,
                serverToClient.Writer,
                cancellationToken);
            return new Session(clientToServer, serverToClient, hostTask);
        }

        internal async Task<string> RequestAsync(
            int id,
            string method,
            string? parameters,
            CancellationToken cancellationToken)
        {
            var payload = parameters is null
                ? $$"""{"jsonrpc":"2.0","id":{{id.ToString(CultureInfo.InvariantCulture)}},"method":"{{method}}"}"""
                : $$"""{"jsonrpc":"2.0","id":{{id.ToString(CultureInfo.InvariantCulture)}},"method":"{{method}}","params":{{parameters}}}""";
            await WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            while (true)
            {
                var message = await ReadAsync(cancellationToken).ConfigureAwait(false);
                using var document = JsonDocument.Parse(message);
                if (document.RootElement.TryGetProperty("id", out _))
                {
                    return message;
                }

                // Server-initiated notifications (logMessage, publishDiagnostics) can
                // arrive ahead of the response; keep them for tests to inspect.
                Notifications.Add(message);
            }
        }

        internal List<string> Notifications { get; } = new();

        internal Task NotifyAsync(
            string method,
            string? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var payload = parameters is null
                ? $$"""{"jsonrpc":"2.0","method":"{{method}}"}"""
                : $$"""{"jsonrpc":"2.0","method":"{{method}}","params":{{parameters}}}""";
            return WriteAsync(payload, cancellationToken);
        }

        internal async Task ShutdownAndExitAsync(
            int expectedCode,
            CancellationToken cancellationToken,
            bool alreadyShutDown = false)
        {
            if (!alreadyShutDown)
            {
                await RequestAsync(++_nextId, "shutdown", parameters: null, cancellationToken).ConfigureAwait(false);
            }

            await NotifyAsync("exit", cancellationToken: cancellationToken).ConfigureAwait(false);
            Assert.Equal(expectedCode, await HostTask.WaitAsync(cancellationToken).ConfigureAwait(false));
        }

        public async ValueTask DisposeAsync()
        {
            await _clientToServer.Writer.CompleteAsync().ConfigureAwait(false);
            try
            {
                await HostTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        private Task WriteAsync(string json, CancellationToken cancellationToken)
        {
            return LspFrame.WriteAsync(_clientToServer.Writer, json, cancellationToken);
        }

        private Task<string> ReadAsync(CancellationToken cancellationToken)
        {
            return LspFrame.ReadAsync(_serverToClient.Reader, cancellationToken);
        }
    }

    private static class LspFrame
    {
        internal static async Task WriteAsync(PipeWriter writer, string json, CancellationToken cancellationToken)
        {
            var body = Encoding.UTF8.GetBytes(json);
            var header = Encoding.ASCII.GetBytes(
                "Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n");
            await writer.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await writer.WriteAsync(body, cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<string> ReadAsync(PipeReader reader, CancellationToken cancellationToken)
        {
            while (true)
            {
                var read = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                var buffer = read.Buffer;
                if (TryReadMessage(ref buffer, out var json))
                {
                    reader.AdvanceTo(buffer.Start);
                    return json;
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
                if (read.IsCompleted)
                {
                    throw new EndOfStreamException("LSP stream ended before a complete message arrived.");
                }
            }
        }

        private static bool TryReadMessage(ref ReadOnlySequence<byte> buffer, out string json)
        {
            json = "";
            var headerReader = new SequenceReader<byte>(buffer);
            if (!headerReader.TryReadTo(out ReadOnlySequence<byte> headerBytes, "\r\n\r\n"u8))
            {
                return false;
            }

            var length = ParseContentLength(GetString(headerBytes, Encoding.ASCII));
            if (headerReader.Remaining < length)
            {
                return false;
            }

            var body = headerReader.UnreadSequence.Slice(0, length);
            json = GetString(body, Encoding.UTF8);
            headerReader.Advance(length);
            buffer = headerReader.UnreadSequence;
            return true;
        }

        private static int ParseContentLength(string headers)
        {
            const string prefix = "Content-Length:";
            foreach (var line in headers.Split("\r\n", StringSplitOptions.None))
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return int.Parse(line.AsSpan(prefix.Length).Trim(), CultureInfo.InvariantCulture);
                }
            }

            throw new InvalidDataException("LSP message is missing Content-Length.");
        }

        private static string GetString(ReadOnlySequence<byte> bytes, Encoding encoding)
        {
            return bytes.IsSingleSegment
                ? encoding.GetString(bytes.FirstSpan)
                : encoding.GetString(bytes.ToArray());
        }
    }
}
