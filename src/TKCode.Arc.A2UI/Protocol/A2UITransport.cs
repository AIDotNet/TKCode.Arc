using System.Runtime.CompilerServices;
using System.Text;

namespace TKCode.Arc.A2UI;

/// <summary>Reads JSON Lines (one A2UI envelope per line) from a stream.</summary>
public static class A2UIJsonl
{
    /// <summary>
    /// The non-empty lines of the stream as they arrive. A line longer than <paramref name="maxLineBytes"/> is skipped.
    /// </summary>
    public static async IAsyncEnumerable<string> ReadLinesAsync(Stream stream, int maxLineBytes = 1 << 20,
                                                                [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024, leaveOpen: true);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                yield break;
            }

            line = line.Trim();
            if (line.Length == 0 || line.Length > maxLineBytes)
            {
                continue;
            }

            yield return line;
        }
    }
}

/// <summary>One Server-Sent Events message.</summary>
public readonly record struct A2UISseEvent(string? Event, string Data, string? Id);

/// <summary>Reads Server-Sent Events: <c>data:</c> lines are joined, a blank line ends an event, comments are skipped.</summary>
public static class A2UISse
{
    /// <summary>The events of the stream as they arrive.</summary>
    public static async IAsyncEnumerable<A2UISseEvent> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 16 * 1024, leaveOpen: true);
        var data = new StringBuilder();
        string? eventName = null;
        string? id = null;
        var hasData = false;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                if (hasData)
                {
                    yield return new A2UISseEvent(eventName, data.ToString(), id);
                }

                yield break;
            }

            if (line.Length == 0)
            {
                if (hasData)
                {
                    yield return new A2UISseEvent(eventName, data.ToString(), id);
                }

                data.Clear();
                eventName = null;
                hasData = false;
                continue;
            }

            if (line[0] == ':')
            {
                continue;
            }

            var colon = line.IndexOf(':');
            var field = colon < 0 ? line : line[..colon];
            var value = colon < 0 ? string.Empty : line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }

            switch (field)
            {
                case "data":
                    if (hasData)
                    {
                        data.Append('\n');
                    }

                    data.Append(value);
                    hasData = true;
                    break;
                case "event":
                    eventName = value;
                    break;
                case "id":
                    id = value;
                    break;
            }
        }
    }
}
