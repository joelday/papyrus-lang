using System;
using System.Buffers;
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
            return await ReadAsync(cancellationToken).ConfigureAwait(false);
        }

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
