// Ported from vscode (src/vs/editor/common/core/textChange.ts, commit b57d5c9e), MIT license.

using System.Buffers.Binary;

namespace TKCode.Arc.Editor;

/// <summary>
/// One replaced stretch of text, by offset: <see cref="OldText"/> at <see cref="OldPosition"/> in the text before the
/// edit became <see cref="NewText"/> at <see cref="NewPosition"/> in the text after it. Undo and redo are stored as these.
/// </summary>
internal sealed class TextChange : IEquatable<TextChange>
{
    public TextChange(int oldPosition, string oldText, int newPosition, string newText)
    {
        OldPosition = oldPosition;
        OldText = oldText;
        NewPosition = newPosition;
        NewText = newText;
    }

    public int OldPosition { get; }

    public string OldText { get; }

    public int NewPosition { get; }

    public string NewText { get; }

    public int OldLength => OldText.Length;

    public int OldEnd => OldPosition + OldText.Length;

    public int NewLength => NewText.Length;

    public int NewEnd => NewPosition + NewText.Length;

    public override string ToString()
    {
        if (OldText.Length == 0)
        {
            return $"(insert@{OldPosition} \"{EscapeNewLine(NewText)}\")";
        }
        if (NewText.Length == 0)
        {
            return $"(delete@{OldPosition} \"{EscapeNewLine(OldText)}\")";
        }
        return $"(replace@{OldPosition} \"{EscapeNewLine(OldText)}\" with \"{EscapeNewLine(NewText)}\")";
    }

    private static string EscapeNewLine(string str) => str.Replace("\n", "\\n").Replace("\r", "\\r");

    private static int WriteStringSize(string str) => 4 + 2 * str.Length;

    private static int WriteString(Span<byte> b, string str, int offset)
    {
        BinaryPrimitives.WriteUInt32BigEndian(b[offset..], (uint)str.Length);
        offset += 4;
        for (var i = 0; i < str.Length; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(b[offset..], str[i]);
            offset += 2;
        }
        return offset;
    }

    private static string ReadString(ReadOnlySpan<byte> b, int offset)
    {
        var len = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
        offset += 4;
        var chars = new char[len];
        for (var i = 0; i < len; i++)
        {
            chars[i] = (char)BinaryPrimitives.ReadUInt16LittleEndian(b[(offset + 2 * i)..]);
        }
        return new string(chars);
    }

    /// <summary>The number of bytes <see cref="Write"/> needs.</summary>
    public int WriteSize() => 4 + 4 + WriteStringSize(OldText) + WriteStringSize(NewText);

    /// <summary>Writes this change at <paramref name="offset"/> and returns the offset after it.</summary>
    public int Write(Span<byte> b, int offset)
    {
        BinaryPrimitives.WriteUInt32BigEndian(b[offset..], (uint)OldPosition);
        offset += 4;
        BinaryPrimitives.WriteUInt32BigEndian(b[offset..], (uint)NewPosition);
        offset += 4;
        offset = WriteString(b, OldText, offset);
        offset = WriteString(b, NewText, offset);
        return offset;
    }

    /// <summary>Reads one change at <paramref name="offset"/>, adds it to <paramref name="dest"/> and returns the offset after it.</summary>
    public static int Read(ReadOnlySpan<byte> b, int offset, List<TextChange> dest)
    {
        var oldPosition = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
        offset += 4;
        var newPosition = (int)BinaryPrimitives.ReadUInt32BigEndian(b[offset..]);
        offset += 4;
        var oldText = ReadString(b, offset);
        offset += WriteStringSize(oldText);
        var newText = ReadString(b, offset);
        offset += WriteStringSize(newText);
        dest.Add(new TextChange(oldPosition, oldText, newPosition, newText));
        return offset;
    }

    public bool Equals(TextChange? other) =>
        other is not null && OldPosition == other.OldPosition && NewPosition == other.NewPosition
        && OldText == other.OldText && NewText == other.NewText;

    public override bool Equals(object? obj) => Equals(obj as TextChange);

    public override int GetHashCode() => HashCode.Combine(OldPosition, OldText, NewPosition, NewText);

    /// <summary>
    /// Folds two consecutive sets of changes into one set that takes the text before <paramref name="prevEdits"/>
    /// straight to the text after <paramref name="currEdits"/>.
    /// </summary>
    public static List<TextChange> CompressConsecutiveTextChanges(List<TextChange>? prevEdits, List<TextChange> currEdits)
    {
        if (prevEdits is null || prevEdits.Count == 0)
        {
            return currEdits;
        }
        return new TextChangeCompressor(prevEdits, currEdits).Compress();
    }
}

internal sealed class TextChangeCompressor
{
    private readonly List<TextChange> _prevEdits;
    private readonly List<TextChange> _currEdits;
    private readonly List<TextChange> _result = new();
    private int _prevDeltaOffset;
    private int _currDeltaOffset;

    public TextChangeCompressor(List<TextChange> prevEdits, List<TextChange> currEdits)
    {
        _prevEdits = prevEdits;
        _currEdits = currEdits;
    }

    public List<TextChange> Compress()
    {
        var prevIndex = 0;
        var currIndex = 0;

        var prevEdit = GetPrev(prevIndex);
        var currEdit = GetCurr(currIndex);

        while (prevIndex < _prevEdits.Count || currIndex < _currEdits.Count)
        {
            if (prevEdit is null)
            {
                AcceptCurr(currEdit!);
                currEdit = GetCurr(++currIndex);
                continue;
            }

            if (currEdit is null)
            {
                AcceptPrev(prevEdit);
                prevEdit = GetPrev(++prevIndex);
                continue;
            }

            if (currEdit.OldEnd <= prevEdit.NewPosition)
            {
                AcceptCurr(currEdit);
                currEdit = GetCurr(++currIndex);
                continue;
            }

            if (prevEdit.NewEnd <= currEdit.OldPosition)
            {
                AcceptPrev(prevEdit);
                prevEdit = GetPrev(++prevIndex);
                continue;
            }

            if (currEdit.OldPosition < prevEdit.NewPosition)
            {
                var (e1, e2) = SplitCurr(currEdit, prevEdit.NewPosition - currEdit.OldPosition);
                AcceptCurr(e1);
                currEdit = e2;
                continue;
            }

            if (prevEdit.NewPosition < currEdit.OldPosition)
            {
                var (e1, e2) = SplitPrev(prevEdit, currEdit.OldPosition - prevEdit.NewPosition);
                AcceptPrev(e1);
                prevEdit = e2;
                continue;
            }

            // At this point, currEdit.OldPosition == prevEdit.NewPosition

            TextChange mergePrev;
            TextChange mergeCurr;

            if (currEdit.OldEnd == prevEdit.NewEnd)
            {
                mergePrev = prevEdit;
                mergeCurr = currEdit;
                prevEdit = GetPrev(++prevIndex);
                currEdit = GetCurr(++currIndex);
            }
            else if (currEdit.OldEnd < prevEdit.NewEnd)
            {
                var (e1, e2) = SplitPrev(prevEdit, currEdit.OldLength);
                mergePrev = e1;
                mergeCurr = currEdit;
                prevEdit = e2;
                currEdit = GetCurr(++currIndex);
            }
            else
            {
                var (e1, e2) = SplitCurr(currEdit, prevEdit.NewLength);
                mergePrev = prevEdit;
                mergeCurr = e1;
                prevEdit = GetPrev(++prevIndex);
                currEdit = e2;
            }

            _result.Add(new TextChange(mergePrev.OldPosition, mergePrev.OldText, mergeCurr.NewPosition, mergeCurr.NewText));
            _prevDeltaOffset += mergePrev.NewLength - mergePrev.OldLength;
            _currDeltaOffset += mergeCurr.NewLength - mergeCurr.OldLength;
        }

        return RemoveNoOps(Merge(_result));
    }

    private void AcceptCurr(TextChange currEdit)
    {
        _result.Add(RebaseCurr(_prevDeltaOffset, currEdit));
        _currDeltaOffset += currEdit.NewLength - currEdit.OldLength;
    }

    private TextChange? GetCurr(int currIndex) => currIndex < _currEdits.Count ? _currEdits[currIndex] : null;

    private void AcceptPrev(TextChange prevEdit)
    {
        _result.Add(RebasePrev(_currDeltaOffset, prevEdit));
        _prevDeltaOffset += prevEdit.NewLength - prevEdit.OldLength;
    }

    private TextChange? GetPrev(int prevIndex) => prevIndex < _prevEdits.Count ? _prevEdits[prevIndex] : null;

    private static TextChange RebaseCurr(int prevDeltaOffset, TextChange currEdit) =>
        new(currEdit.OldPosition - prevDeltaOffset, currEdit.OldText, currEdit.NewPosition, currEdit.NewText);

    private static TextChange RebasePrev(int currDeltaOffset, TextChange prevEdit) =>
        new(prevEdit.OldPosition, prevEdit.OldText, prevEdit.NewPosition + currDeltaOffset, prevEdit.NewText);

    private static (TextChange, TextChange) SplitPrev(TextChange edit, int offset)
    {
        var preText = edit.NewText[..offset];
        var postText = edit.NewText[offset..];
        return (
            new TextChange(edit.OldPosition, edit.OldText, edit.NewPosition, preText),
            new TextChange(edit.OldEnd, string.Empty, edit.NewPosition + offset, postText));
    }

    private static (TextChange, TextChange) SplitCurr(TextChange edit, int offset)
    {
        var preText = edit.OldText[..offset];
        var postText = edit.OldText[offset..];
        return (
            new TextChange(edit.OldPosition, preText, edit.NewPosition, edit.NewText),
            new TextChange(edit.OldPosition + offset, postText, edit.NewEnd, string.Empty));
    }

    private static List<TextChange> Merge(List<TextChange> edits)
    {
        if (edits.Count == 0)
        {
            return edits;
        }

        var result = new List<TextChange>(edits.Count);
        var prev = edits[0];
        for (var i = 1; i < edits.Count; i++)
        {
            var curr = edits[i];
            if (prev.OldEnd == curr.OldPosition)
            {
                // Merge into `prev`
                prev = new TextChange(prev.OldPosition, prev.OldText + curr.OldText, prev.NewPosition, prev.NewText + curr.NewText);
            }
            else
            {
                result.Add(prev);
                prev = curr;
            }
        }
        result.Add(prev);
        return result;
    }

    private static List<TextChange> RemoveNoOps(List<TextChange> edits)
    {
        if (edits.Count == 0)
        {
            return edits;
        }

        var result = new List<TextChange>(edits.Count);
        for (var i = 0; i < edits.Count; i++)
        {
            var edit = edits[i];
            if (edit.OldText == edit.NewText)
            {
                continue;
            }
            result.Add(edit);
        }
        return result;
    }
}
