// Ported from vscode's src/vs/editor/common/model/pieceTreeTextBuffer/pieceTreeTextBufferBuilder.ts
// (commit b57d5c9e1b27c4ede570c76940b7aa5207595a54), MIT licensed.
//
// vscode's factory creates a PieceTreeTextBuffer, which is not ported yet. It returns the piece tree together with the
// facts the text buffer will need (BOM, line ending, counts and flags) instead.

namespace TKCode.Arc.Editor.Model;

/// <summary>What the builder detected while reading the chunks, plus the tree built from them.</summary>
internal sealed class PieceTreeBufferResult
{
    internal PieceTreeBufferResult(
        PieceTreeBase pieceTree,
        string bom,
        string eol,
        int cr,
        int lf,
        int crlf,
        bool containsRtl,
        bool containsUnusualLineTerminators,
        bool isBasicAscii,
        bool normalizeEol)
    {
        PieceTree = pieceTree;
        Bom = bom;
        Eol = eol;
        Cr = cr;
        Lf = lf;
        Crlf = crlf;
        ContainsRtl = containsRtl;
        ContainsUnusualLineTerminators = containsUnusualLineTerminators;
        IsBasicAscii = isBasicAscii;
        NormalizeEol = normalizeEol;
    }

    internal PieceTreeBase PieceTree { get; }

    internal string Bom { get; }

    internal string Eol { get; }

    internal int Cr { get; }

    internal int Lf { get; }

    internal int Crlf { get; }

    internal bool ContainsRtl { get; }

    internal bool ContainsUnusualLineTerminators { get; }

    internal bool IsBasicAscii { get; }

    internal bool NormalizeEol { get; }
}

internal sealed class PieceTreeTextBufferFactory
{
    private readonly List<StringBuffer> _chunks;
    private readonly string _bom;
    private readonly int _cr;
    private readonly int _lf;
    private readonly int _crlf;
    private readonly bool _containsRtl;
    private readonly bool _containsUnusualLineTerminators;
    private readonly bool _isBasicAscii;
    private readonly bool _normalizeEol;

    internal PieceTreeTextBufferFactory(
        List<StringBuffer> chunks,
        string bom,
        int cr,
        int lf,
        int crlf,
        bool containsRtl,
        bool containsUnusualLineTerminators,
        bool isBasicAscii,
        bool normalizeEol)
    {
        _chunks = chunks;
        _bom = bom;
        _cr = cr;
        _lf = lf;
        _crlf = crlf;
        _containsRtl = containsRtl;
        _containsUnusualLineTerminators = containsUnusualLineTerminators;
        _isBasicAscii = isBasicAscii;
        _normalizeEol = normalizeEol;
    }

    private string GetEol(DefaultEndOfLine defaultEol)
    {
        var totalEolCount = _cr + _lf + _crlf;
        var totalCrCount = _cr + _crlf;
        if (totalEolCount == 0)
        {
            // This is an empty file or a file with precisely one line
            return defaultEol == DefaultEndOfLine.LF ? "\n" : "\r\n";
        }

        if (totalCrCount > totalEolCount / 2)
        {
            // More than half of the file contains \r\n ending lines
            return "\r\n";
        }

        // At least one line more ends in \n
        return "\n";
    }

    internal PieceTreeBufferResult Create(DefaultEndOfLine defaultEol)
    {
        var eol = GetEol(defaultEol);
        var chunks = _chunks;

        if (_normalizeEol
            && ((eol == "\r\n" && (_cr > 0 || _lf > 0))
                || (eol == "\n" && (_cr > 0 || _crlf > 0))))
        {
            // Normalize pieces
            for (int i = 0, len = chunks.Count; i < len; i++)
            {
                var str = PieceTreeStrings.ReplaceLineBreaks(chunks[i].Text, eol);
                var newLineStarts = PieceTreeBase.CreateLineStartsFast(str);
                chunks[i] = new StringBuffer(str, newLineStarts);
            }
        }

        var pieceTree = new PieceTreeBase(chunks, eol, _normalizeEol);
        return new PieceTreeBufferResult(
            pieceTree,
            _bom,
            eol,
            _cr,
            _lf,
            _crlf,
            _containsRtl,
            _containsUnusualLineTerminators,
            _isBasicAscii,
            _normalizeEol);
    }

    internal string GetFirstLineText(int lengthLimit)
    {
        var text = _chunks[0].Text;
        var limited = text.Substring(0, Math.Min(lengthLimit, text.Length));
        return PieceTreeStrings.SplitLines(limited)[0];
    }
}

internal sealed class PieceTreeTextBufferBuilder
{
    private readonly List<StringBuffer> _chunks;
    private string _bom;

    private bool _hasPreviousChar;
    private char _previousChar;

    private int _cr;
    private int _lf;
    private int _crlf;
    private bool _containsRtl;
    private bool _containsUnusualLineTerminators;
    private bool _isBasicAscii;

    internal PieceTreeTextBufferBuilder()
    {
        _chunks = new List<StringBuffer>();
        _bom = string.Empty;

        _hasPreviousChar = false;
        _previousChar = '\0';

        _cr = 0;
        _lf = 0;
        _crlf = 0;
        _containsRtl = false;
        _containsUnusualLineTerminators = false;
        _isBasicAscii = true;
    }

    internal void AcceptChunk(string chunk)
    {
        if (chunk.Length == 0)
        {
            return;
        }

        if (_chunks.Count == 0)
        {
            if (PieceTreeStrings.StartsWithUtf8Bom(chunk))
            {
                _bom = PieceTreeStrings.Utf8BomCharacter.ToString();
                chunk = chunk.Substring(1);
                if (chunk.Length == 0)
                {
                    return;
                }
            }
        }

        var lastChar = chunk[chunk.Length - 1];
        if (lastChar == PieceTreeStrings.CarriageReturn || (lastChar >= 0xD800 && lastChar <= 0xDBFF))
        {
            // last character is \r or a high surrogate => keep it back
            AcceptChunk1(chunk.Substring(0, chunk.Length - 1), false);
            _hasPreviousChar = true;
            _previousChar = lastChar;
        }
        else
        {
            AcceptChunk1(chunk, false);
            _hasPreviousChar = false;
            _previousChar = lastChar;
        }
    }

    private void AcceptChunk1(string chunk, bool allowEmptyStrings)
    {
        if (!allowEmptyStrings && chunk.Length == 0)
        {
            // Nothing to do
            return;
        }

        if (_hasPreviousChar)
        {
            AcceptChunk2(_previousChar + chunk);
        }
        else
        {
            AcceptChunk2(chunk);
        }
    }

    private void AcceptChunk2(string chunk)
    {
        var lineStarts = PieceTreeBase.CreateLineStarts(chunk);

        _chunks.Add(new StringBuffer(chunk, lineStarts.LineStartsArray));
        _cr += lineStarts.Cr;
        _lf += lineStarts.Lf;
        _crlf += lineStarts.Crlf;

        if (!lineStarts.IsBasicAscii)
        {
            // this chunk contains non basic ASCII characters
            _isBasicAscii = false;
            if (!_containsRtl)
            {
                _containsRtl = PieceTreeStrings.ContainsRtl(chunk);
            }

            if (!_containsUnusualLineTerminators)
            {
                _containsUnusualLineTerminators = PieceTreeStrings.ContainsUnusualLineTerminators(chunk);
            }
        }
    }

    internal PieceTreeTextBufferFactory Finish(bool normalizeEol = true)
    {
        FinishChunks();
        return new PieceTreeTextBufferFactory(
            _chunks,
            _bom,
            _cr,
            _lf,
            _crlf,
            _containsRtl,
            _containsUnusualLineTerminators,
            _isBasicAscii,
            normalizeEol);
    }

    private void FinishChunks()
    {
        if (_chunks.Count == 0)
        {
            AcceptChunk1(string.Empty, true);
            // Differs from upstream on purpose: AcceptChunk1 has just written the held-back character, so it must not be
            // appended again below. Upstream leaves the flag set, and a text that is only "\r" (or only a high
            // surrogate) comes out doubled.
            _hasPreviousChar = false;
        }

        if (_hasPreviousChar)
        {
            _hasPreviousChar = false;
            // recreate last chunk
            var lastChunk = _chunks[_chunks.Count - 1];
            lastChunk.Text += _previousChar;
            var newLineStarts = PieceTreeBase.CreateLineStartsFast(lastChunk.Text);
            lastChunk.LineStarts = newLineStarts;
            if (_previousChar == PieceTreeStrings.CarriageReturn)
            {
                _cr++;
            }
        }
    }
}
