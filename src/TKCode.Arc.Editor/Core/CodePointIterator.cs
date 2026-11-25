// Ported from vscode (src/vs/base/common/strings.ts: CodePointIterator, commit b57d5c9e), MIT license.
// Differences from upstream: reading outside the string gives -1 where JavaScript's charCodeAt gives NaN (see
// Strings.CharCodeAt), so stepping past either end moves one code unit as upstream does instead of throwing.

namespace TKCode.Arc.Editor;

/// <summary>Walks a string one code point (one or two UTF-16 code units) at a time, in either direction.</summary>
internal sealed class CodePointIterator
{
    private const int UnicodeSupplementaryPlaneBegin = 0x010000;

    private readonly string _str;
    private readonly int _len;
    private int _offset;

    public CodePointIterator(string str, int offset = 0)
    {
        _str = str;
        _len = str.Length;
        _offset = offset;
    }

    /// <summary>The current offset in UTF-16 code units.</summary>
    public int Offset => _offset;

    public void SetOffset(int offset)
    {
        _offset = offset;
    }

    /// <summary>The code point that ends at the current offset; moves the offset before it.</summary>
    public int PrevCodePoint()
    {
        var codePoint = Strings.GetPrevCodePoint(_str, _offset);
        _offset -= codePoint >= UnicodeSupplementaryPlaneBegin ? 2 : 1;
        return codePoint;
    }

    /// <summary>The code point that starts at the current offset; moves the offset after it.</summary>
    public int NextCodePoint()
    {
        var codePoint = Strings.GetNextCodePoint(_str, _len, _offset);
        _offset += codePoint >= UnicodeSupplementaryPlaneBegin ? 2 : 1;
        return codePoint;
    }

    /// <summary>Whether the offset is at or past the end of the string.</summary>
    public bool Eol() => _offset >= _len;
}
