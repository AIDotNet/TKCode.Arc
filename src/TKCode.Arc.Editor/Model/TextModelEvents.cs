// Ported from vscode (src/vs/editor/common/textModelEvents.ts and model/mirrorTextModel.ts, commit b57d5c9e),
// MIT license. Injected text, line height and font events come with the decoration options.

namespace TKCode.Arc.Editor.Model;

/// <summary>One change to the text, in the coordinates before that change (upstream's IModelContentChange).</summary>
internal readonly record struct ModelContentChange(TextRange Range, int RangeOffset, int RangeLength, string Text);

/// <summary>The content of a model changed (upstream's IModelContentChangedEvent).</summary>
internal sealed class ModelContentChangedEvent
{
    public ModelContentChangedEvent(
        IReadOnlyList<ModelContentChange> changes,
        string eol,
        int versionId,
        bool isUndoing,
        bool isRedoing,
        bool isFlush,
        bool isEolChange,
        IReadOnlyList<TextModelEditSource> detailedReasons,
        IReadOnlyList<int> detailedReasonsChangeLengths)
    {
        Changes = changes;
        Eol = eol;
        VersionId = versionId;
        IsUndoing = isUndoing;
        IsRedoing = isRedoing;
        IsFlush = isFlush;
        IsEolChange = isEolChange;
        DetailedReasons = detailedReasons;
        DetailedReasonsChangeLengths = detailedReasonsChangeLengths;
    }

    /// <summary>The changes, ordered from the end of the document to the beginning.</summary>
    public IReadOnlyList<ModelContentChange> Changes { get; }

    /// <summary>The end of line sequence after the change.</summary>
    public string Eol { get; }

    /// <summary>The version of the model after the change.</summary>
    public int VersionId { get; }

    public bool IsUndoing { get; }

    public bool IsRedoing { get; }

    /// <summary>The whole content was replaced (<c>SetValue</c>); every decoration is gone.</summary>
    public bool IsFlush { get; }

    /// <summary>Only the end of line sequence changed.</summary>
    public bool IsEolChange { get; }

    /// <summary>Why the content changed, one entry per merged event.</summary>
    public IReadOnlyList<TextModelEditSource> DetailedReasons { get; }

    /// <summary>How many of <see cref="Changes"/> belong to each entry of <see cref="DetailedReasons"/>.</summary>
    public IReadOnlyList<int> DetailedReasonsChangeLengths { get; }
}

/// <summary>The language of a model changed.</summary>
internal readonly record struct ModelLanguageChangedEvent(string OldLanguage, string NewLanguage, string Source);

/// <summary>The decorations of a model changed.</summary>
internal readonly record struct ModelDecorationsChangedEvent(
    bool AffectsMinimap,
    bool AffectsOverviewRuler,
    bool AffectsGlyphMargin,
    bool AffectsLineNumber);

/// <summary>The options of a model changed; each flag says whether that option did.</summary>
internal readonly record struct ModelOptionsChangedEvent(bool TabSize, bool IndentSize, bool InsertSpaces, bool TrimAutoWhitespace);

internal enum RawContentChangedType
{
    Flush = 1,
    LineChanged = 2,
    LinesDeleted = 3,
    LinesInserted = 4,
    EOLChanged = 5,
}

/// <summary>A line-based change, as the view model consumes it.</summary>
internal abstract class ModelRawChange
{
    public abstract RawContentChangedType ChangeType { get; }
}

/// <summary>The whole content was replaced.</summary>
internal sealed class ModelRawFlush : ModelRawChange
{
    public override RawContentChangedType ChangeType => RawContentChangedType.Flush;
}

/// <summary>A line changed.</summary>
internal sealed class ModelRawLineChanged : ModelRawChange
{
    public ModelRawLineChanged(int lineNumber, int lineNumberPostEdit)
    {
        LineNumber = lineNumber;
        LineNumberPostEdit = lineNumberPostEdit;
    }

    public override RawContentChangedType ChangeType => RawContentChangedType.LineChanged;

    /// <summary>The line number before the whole edit.</summary>
    public int LineNumber { get; }

    /// <summary>The line number after the whole edit.</summary>
    public int LineNumberPostEdit { get; }
}

/// <summary>Lines were deleted.</summary>
internal sealed class ModelRawLinesDeleted : ModelRawChange
{
    public ModelRawLinesDeleted(int fromLineNumber, int toLineNumber, int lastUntouchedLinePostEdit)
    {
        FromLineNumber = fromLineNumber;
        ToLineNumber = toLineNumber;
        LastUntouchedLinePostEdit = lastUntouchedLinePostEdit;
    }

    public override RawContentChangedType ChangeType => RawContentChangedType.LinesDeleted;

    /// <summary>The first deleted line, in pre-edit numbers.</summary>
    public int FromLineNumber { get; }

    /// <summary>The last deleted line, in pre-edit numbers.</summary>
    public int ToLineNumber { get; }

    /// <summary>The last line the deletion left alone, in post-edit numbers.</summary>
    public int LastUntouchedLinePostEdit { get; }
}

/// <summary>Lines were inserted.</summary>
internal sealed class ModelRawLinesInserted : ModelRawChange
{
    public ModelRawLinesInserted(int fromLineNumber, int fromLineNumberPostEdit, int count)
    {
        FromLineNumber = fromLineNumber;
        FromLineNumberPostEdit = fromLineNumberPostEdit;
        Count = count;
    }

    public override RawContentChangedType ChangeType => RawContentChangedType.LinesInserted;

    /// <summary>The first inserted line, in pre-edit numbers.</summary>
    public int FromLineNumber { get; }

    /// <summary>The first inserted line, in post-edit numbers.</summary>
    public int FromLineNumberPostEdit { get; }

    public int Count { get; }

    public int ToLineNumber => FromLineNumber + Count - 1;

    public int ToLineNumberPostEdit => FromLineNumberPostEdit + Count - 1;
}

/// <summary>The end of line sequence changed.</summary>
internal sealed class ModelRawEOLChanged : ModelRawChange
{
    public override RawContentChangedType ChangeType => RawContentChangedType.EOLChanged;
}

/// <summary>The line-based form of a content change.</summary>
internal sealed class ModelRawContentChangedEvent
{
    public ModelRawContentChangedEvent(IReadOnlyList<ModelRawChange> changes, int versionId, bool isUndoing, bool isRedoing)
    {
        Changes = changes;
        VersionId = versionId;
        IsUndoing = isUndoing;
        IsRedoing = isRedoing;
    }

    public IReadOnlyList<ModelRawChange> Changes { get; }

    public int VersionId { get; }

    public bool IsUndoing { get; }

    public bool IsRedoing { get; }

    /// <summary>The selections the edit asked for; set when the event is emitted.</summary>
    public IReadOnlyList<TextSelection>? ResultingSelection { get; set; }

    public bool ContainsEvent(RawContentChangedType type)
    {
        for (var i = 0; i < Changes.Count; i++)
        {
            if (Changes[i].ChangeType == type)
            {
                return true;
            }
        }

        return false;
    }

    public static ModelRawContentChangedEvent Merge(ModelRawContentChangedEvent a, ModelRawContentChangedEvent b)
    {
        var changes = new List<ModelRawChange>(a.Changes.Count + b.Changes.Count);
        changes.AddRange(a.Changes);
        changes.AddRange(b.Changes);
        return new ModelRawContentChangedEvent(changes, b.VersionId, a.IsUndoing || b.IsUndoing, a.IsRedoing || b.IsRedoing);
    }
}

/// <summary>
/// What a model tells its view models before anything else (upstream's
/// <c>InternalModelContentChangeEvent | ModelInjectedTextChangedEvent</c>).
/// </summary>
internal interface IModelContentOrInjectedTextChangedEvent
{
}

/// <summary>Both forms of one content change, kept together so that deferred events merge consistently.</summary>
internal sealed class InternalModelContentChangeEvent : IModelContentOrInjectedTextChangedEvent
{
    public InternalModelContentChangeEvent(ModelRawContentChangedEvent rawContentChangedEvent, ModelContentChangedEvent contentChangedEvent)
    {
        RawContentChangedEvent = rawContentChangedEvent;
        ContentChangedEvent = contentChangedEvent;
    }

    public ModelRawContentChangedEvent RawContentChangedEvent { get; }

    public ModelContentChangedEvent ContentChangedEvent { get; }

    public InternalModelContentChangeEvent Merge(InternalModelContentChangeEvent other) =>
        new(
            ModelRawContentChangedEvent.Merge(RawContentChangedEvent, other.RawContentChangedEvent),
            MergeChangeEvents(ContentChangedEvent, other.ContentChangedEvent));

    private static ModelContentChangedEvent MergeChangeEvents(ModelContentChangedEvent a, ModelContentChangedEvent b)
    {
        var changes = new List<ModelContentChange>(a.Changes.Count + b.Changes.Count);
        changes.AddRange(a.Changes);
        changes.AddRange(b.Changes);
        var reasons = new List<TextModelEditSource>(a.DetailedReasons.Count + b.DetailedReasons.Count);
        reasons.AddRange(a.DetailedReasons);
        reasons.AddRange(b.DetailedReasons);
        var lengths = new List<int>(a.DetailedReasonsChangeLengths.Count + b.DetailedReasonsChangeLengths.Count);
        lengths.AddRange(a.DetailedReasonsChangeLengths);
        lengths.AddRange(b.DetailedReasonsChangeLengths);
        return new ModelContentChangedEvent(
            changes,
            b.Eol,
            b.VersionId,
            a.IsUndoing || b.IsUndoing,
            a.IsRedoing || b.IsRedoing,
            a.IsFlush || b.IsFlush,
            // Both must be true, so that listeners who skip such edits are not confused.
            a.IsEolChange && b.IsEolChange,
            reasons,
            lengths);
    }
}
