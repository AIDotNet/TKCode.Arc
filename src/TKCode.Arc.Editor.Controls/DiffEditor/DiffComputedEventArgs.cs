namespace TKCode.Arc;

/// <summary>What <see cref="DiffEditor.DiffComputed"/> reports: the size of the diff that was just computed.</summary>
public sealed class DiffComputedEventArgs : EventArgs
{
    /// <summary>Creates the event data.</summary>
    public DiffComputedEventArgs(int changeCount, int insertedLineCount, int removedLineCount, int movedBlockCount, bool isIdentical, bool hitTimeout)
    {
        ChangeCount = changeCount;
        InsertedLineCount = insertedLineCount;
        RemovedLineCount = removedLineCount;
        MovedBlockCount = movedBlockCount;
        IsIdentical = isIdentical;
        HitTimeout = hitTimeout;
    }

    /// <summary>The number of changes (runs of changed lines).</summary>
    public int ChangeCount { get; }

    /// <summary>The number of lines of the modified text that are new or changed.</summary>
    public int InsertedLineCount { get; }

    /// <summary>The number of lines of the original text that were removed or changed.</summary>
    public int RemovedLineCount { get; }

    /// <summary>The number of blocks of lines that moved.</summary>
    public int MovedBlockCount { get; }

    /// <summary>Whether the two texts are the same, character for character.</summary>
    public bool IsIdentical { get; }

    /// <summary>Whether the computation ran out of time; the diff is then an approximation.</summary>
    public bool HitTimeout { get; }
}
