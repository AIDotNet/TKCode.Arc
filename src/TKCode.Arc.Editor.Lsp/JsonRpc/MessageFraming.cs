// The base protocol of the Language Server Protocol (specification 3.17, "Base Protocol"): a message is a header part
// (ASCII lines "Name: value" ended by CRLF, the last one empty) and a UTF-8 JSON body whose length in bytes
// Content-Length gives. Written for this package; vscode's src/vs has no language client.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace TKCode.Arc.Editor.Lsp.JsonRpc;

/// <summary>Reads framed messages from a stream (the server's output).</summary>
internal sealed class MessageReader
{
    // Header lines longer than this are a protocol error (a stream that is not LSP, a binary file).
    private const int MaxHeaderLength = 8 * 1024;

    private readonly Stream _input;
    private byte[] _buffer = new byte[16 * 1024];
    private int _start;
    private int _end;

    public MessageReader(Stream input)
    {
        _input = input;
    }

    /// <summary>The body of the next message; null at the end of the stream.</summary>
    /// <exception cref="InvalidDataException">The header is malformed or has no Content-Length.</exception>
    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken)
    {
        var contentLength = -1;
        var headerBytes = 0;
        while (true)
        {
            var line = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                if (headerBytes == 0)
                {
                    return null;
                }
                throw new InvalidDataException("The stream ended inside a message header.");
            }
            headerBytes += line.Length + 2;
            if (headerBytes > MaxHeaderLength)
            {
                throw new InvalidDataException("The message header is too long.");
            }
            if (line.Length == 0)
            {
                if (headerBytes == 2)
                {
                    // Tolerate blank lines between messages.
                    headerBytes = 0;
                    continue;
                }
                break;
            }
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                throw new InvalidDataException($"Malformed header line '{line}'.");
            }
            var name = line.AsSpan(0, colon).Trim();
            var value = line.AsSpan(colon + 1).Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength) || contentLength < 0)
                {
                    throw new InvalidDataException($"Invalid Content-Length '{value.ToString()}'.");
                }
            }
            // Content-Type is allowed and ignored: the specification only knows utf-8 (utf8 is accepted for compatibility).
        }
        if (contentLength < 0)
        {
            throw new InvalidDataException("The message header has no Content-Length.");
        }

        var body = new byte[contentLength];
        var copied = 0;
        var buffered = Math.Min(_end - _start, contentLength);
        if (buffered > 0)
        {
            Buffer.BlockCopy(_buffer, _start, body, 0, buffered);
            _start += buffered;
            copied = buffered;
        }
        while (copied < contentLength)
        {
            var read = await _input.ReadAsync(body.AsMemory(copied, contentLength - copied), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("The stream ended inside a message body.");
            }
            copied += read;
        }
        return body;
    }

    // One ASCII header line without its CRLF; null at the end of the stream before any byte of the line.
    private async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        var scanFrom = _start;
        while (true)
        {
            for (var i = scanFrom; i + 1 < _end; i++)
            {
                if (_buffer[i] == (byte)'\r' && _buffer[i + 1] == (byte)'\n')
                {
                    var line = Encoding.ASCII.GetString(_buffer, _start, i - _start);
                    _start = i + 2;
                    return line;
                }
            }
            if (_end - _start > MaxHeaderLength)
            {
                throw new InvalidDataException("The message header is too long.");
            }
            if (!await FillAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_end > _start)
                {
                    throw new InvalidDataException("The stream ended inside a message header.");
                }
                return null;
            }
            // The buffer moved to its start: scan the line again (header lines are short).
            scanFrom = _start;
        }
    }

    private async Task<bool> FillAsync(CancellationToken cancellationToken)
    {
        if (_start > 0)
        {
            var length = _end - _start;
            Buffer.BlockCopy(_buffer, _start, _buffer, 0, length);
            _start = 0;
            _end = length;
        }
        if (_end == _buffer.Length)
        {
            Array.Resize(ref _buffer, _buffer.Length * 2);
        }
        var read = await _input.ReadAsync(_buffer.AsMemory(_end), cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return false;
        }
        _end += read;
        return true;
    }
}

/// <summary>Writes framed messages to a stream (the server's input).</summary>
internal static class MessageWriter
{
    /// <summary>The header and the body of one message, in one buffer.</summary>
    public static byte[] Frame(ReadOnlySpan<byte> body)
    {
        var header = "Content-Length: " + body.Length.ToString(CultureInfo.InvariantCulture) + "\r\n\r\n";
        var headerLength = Encoding.ASCII.GetByteCount(header);
        var message = new byte[headerLength + body.Length];
        Encoding.ASCII.GetBytes(header, message);
        body.CopyTo(message.AsSpan(headerLength));
        return message;
    }

    /// <summary>The body of a message as a writer produces it.</summary>
    public static byte[] Frame(ArrayBufferWriter<byte> body) => Frame(body.WrittenSpan);
}
