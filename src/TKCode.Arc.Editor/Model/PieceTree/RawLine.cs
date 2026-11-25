// Not in vscode: how PieceTreeBase hands out one line without copying it (see PieceTreeBase.Lines.cs).

using System.Text;

namespace TKCode.Arc.Editor.Model;

/// <summary>The text of one line: a place in a buffer, or a string when the line had to be put together.</summary>
internal readonly struct RawLine
{
    private readonly StringBuffer? _buffer;
    private readonly int _start;
    private readonly int _end;
    private readonly string? _text;

    internal RawLine(StringBuffer buffer, int start, int end)
    {
        _buffer = buffer;
        _start = start;
        _end = end;
        _text = null;
    }

    internal RawLine(string text)
    {
        _buffer = null;
        _start = 0;
        _end = 0;
        _text = text;
    }

    internal ReadOnlySpan<char> AsSpan() => _text is not null ? _text.AsSpan() : _buffer!.Slice(_start, _end);

    public override string ToString() => _text ?? _buffer!.Substring(_start, _end);
}

/// <summary>
/// Collects the parts of one line. While all its text comes from one piece nothing is copied; a string is put together
/// only when a second piece adds text, which is a line that an edit split.
/// </summary>
internal struct RawLineBuilder
{
    private StringBuffer? _buffer;
    private int _start;
    private int _end;
    private StringBuilder? _text;

    internal void Append(StringBuffer buffer, int start, int end)
    {
        var part = buffer.Slice(start, end);
        if (part.IsEmpty)
        {
            return;
        }

        if (_text is not null)
        {
            _text.Append(part);
            return;
        }

        if (_buffer is null)
        {
            // Slice clamps and orders the two indices; so does RawLine, which gets them as they are.
            _buffer = buffer;
            _start = start;
            _end = end;
            return;
        }

        _text = new StringBuilder();
        _text.Append(_buffer.Slice(_start, _end));
        _text.Append(part);
    }

    internal readonly RawLine ToRawLine()
    {
        if (_text is not null)
        {
            return new RawLine(_text.ToString());
        }

        return _buffer is not null ? new RawLine(_buffer, _start, _end) : new RawLine(string.Empty);
    }
}
