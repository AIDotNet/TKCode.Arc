// The base protocol's framing and JSON-RPC over it. Written for this package (vscode's src/vs has no language client).

using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Shouldly;
using TKCode.Arc.Editor.Lsp.JsonRpc;
using Xunit;

namespace TKCode.Arc.Editor.Lsp.Tests;

public sealed class MessageFramingTests
{
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public async Task Reads_A_Message_Delivered_One_Byte_At_A_Time()
    {
        var body = "{\"jsonrpc\":\"2.0\",\"method\":\"x\"}";
        var reader = new MessageReader(new TrickleStream(Bytes($"Content-Length: {body.Length}\r\n\r\n{body}")));

        var message = await reader.ReadAsync(CancellationToken.None);

        Encoding.UTF8.GetString(message!).ShouldBe(body);
        (await reader.ReadAsync(CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Reads_Several_Messages_From_One_Buffer_With_Other_Headers()
    {
        var a = "{\"a\":1}";
        var b = "{\"b\":\"é中\"}";
        var data = Bytes($"Content-Length: {a.Length}\r\nContent-Type: application/vscode-jsonrpc; charset=utf-8\r\n\r\n{a}")
            .Concat(Bytes($"content-length: {Encoding.UTF8.GetByteCount(b)}\r\n\r\n{b}")).ToArray();
        var reader = new MessageReader(new MemoryStream(data));

        Encoding.UTF8.GetString((await reader.ReadAsync(CancellationToken.None))!).ShouldBe(a);
        Encoding.UTF8.GetString((await reader.ReadAsync(CancellationToken.None))!).ShouldBe(b);
        (await reader.ReadAsync(CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task A_Header_Without_Content_Length_Is_An_Error()
    {
        var reader = new MessageReader(new MemoryStream(Bytes("Content-Type: x\r\n\r\n{}")));

        await Should.ThrowAsync<InvalidDataException>(() => reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task A_Truncated_Body_Is_An_Error()
    {
        var reader = new MessageReader(new MemoryStream(Bytes("Content-Length: 10\r\n\r\n{}")));

        await Should.ThrowAsync<InvalidDataException>(() => reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public void Frame_Counts_Utf8_Bytes()
    {
        var body = Bytes("{\"t\":\"é\"}");

        var framed = MessageWriter.Frame(body);

        Encoding.ASCII.GetString(framed, 0, 20).ShouldBe("Content-Length: 10\r\n");
        framed.Length.ShouldBe(22 + 10);
    }

    // A stream that returns one byte per read, as a slow pipe may.
    private sealed class TrickleStream(byte[] data) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => data.Length;

        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= data.Length || count == 0)
            {
                return 0;
            }
            buffer[offset] = data[_position++];
            return 1;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public sealed class JsonRpcConnectionTests : IAsyncLifetime
{
    private readonly AnonymousPipeServerStream _aToB = new(PipeDirection.Out);
    private readonly AnonymousPipeServerStream _bToA = new(PipeDirection.Out);
    private JsonRpcConnection _a = null!;
    private JsonRpcConnection _b = null!;

    public ValueTask InitializeAsync()
    {
        _a = new JsonRpcConnection(new AnonymousPipeClientStream(PipeDirection.In, _bToA.ClientSafePipeHandle), _aToB);
        _b = new JsonRpcConnection(new AnonymousPipeClientStream(PipeDirection.In, _aToB.ClientSafePipeHandle), _bToA);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _a.Dispose();
        _b.Dispose();
        _aToB.Dispose();
        _bToA.Dispose();
        return ValueTask.CompletedTask;
    }

    private static JsonValueWriter Raw(string json) => writer => writer.WriteRawValue(json);

    [Fact]
    public async Task A_Request_Gets_Its_Result()
    {
        _b.OnRequest("add", (parameters, _) =>
        {
            var sum = parameters[0].GetInt32() + parameters[1].GetInt32();
            return Task.FromResult<JsonValueWriter?>(writer => writer.WriteNumberValue(sum));
        });
        _a.Start();
        _b.Start();

        var result = await _a.SendRequest("add", Raw("[2,3]"));

        result.GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task A_Null_Result_Is_Json_Null()
    {
        _b.OnRequest("nothing", (_, _) => Task.FromResult<JsonValueWriter?>(null));
        _a.Start();
        _b.Start();

        var result = await _a.SendRequest("nothing", null);

        result.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task An_Error_Response_Throws_With_Its_Code()
    {
        _b.OnRequest("fail", (_, _) => throw new JsonRpcException(JsonRpcErrorCodes.ContentModified, "changed"));
        _a.Start();
        _b.Start();

        var error = await Should.ThrowAsync<JsonRpcException>(() => _a.SendRequest("fail", null));

        error.Code.ShouldBe(JsonRpcErrorCodes.ContentModified);
        error.Message.ShouldBe("changed");
        error.IsCancellation.ShouldBeTrue();
    }

    [Fact]
    public async Task An_Unknown_Method_Is_Method_Not_Found()
    {
        _a.Start();
        _b.Start();

        var error = await Should.ThrowAsync<JsonRpcException>(() => _a.SendRequest("nope", null));

        error.Code.ShouldBe(JsonRpcErrorCodes.MethodNotFound);
    }

    [Fact]
    public async Task Cancelling_A_Request_Tells_The_Other_Side()
    {
        var started = new TaskCompletionSource();
        var cancelled = new TaskCompletionSource();
        _b.OnRequest("slow", async (_, token) =>
        {
            started.SetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, token);
            }
            catch (OperationCanceledException)
            {
                cancelled.SetResult();
                throw;
            }
            return null;
        });
        _a.Start();
        _b.Start();
        using var cts = new CancellationTokenSource();

        var request = _a.SendRequest("slow", null, cts.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => request);
        await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Notifications_Arrive_In_Order_Before_A_Later_Request()
    {
        var seen = new List<int>();
        _b.OnNotification("n", parameters =>
        {
            lock (seen)
            {
                seen.Add(parameters.GetInt32());
            }
        });
        _b.OnRequest("count", (_, _) =>
        {
            int count;
            lock (seen)
            {
                count = seen.Count;
            }
            return Task.FromResult<JsonValueWriter?>(writer => writer.WriteNumberValue(count));
        });
        _a.Start();
        _b.Start();

        for (var i = 0; i < 50; i++)
        {
            _a.SendNotification("n", Raw(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        var count = await _a.SendRequest("count", null);

        count.GetInt32().ShouldBe(50);
        seen.ShouldBe(Enumerable.Range(0, 50));
    }

    [Fact]
    public async Task Closing_The_Other_Side_Fails_Pending_Requests()
    {
        _b.OnRequest("hang", async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return null;
        });
        var closed = new TaskCompletionSource();
        _a.Closed += _ => closed.TrySetResult();
        _a.Start();
        _b.Start();

        var request = _a.SendRequest("hang", null);
        await Task.Delay(50);
        _b.Dispose();
        _bToA.Dispose();

        await Should.ThrowAsync<IOException>(() => request.WaitAsync(TimeSpan.FromSeconds(5)));
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _a.IsClosed.ShouldBeTrue();
    }
}
