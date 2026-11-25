// Ported from vscode (src/vs/editor/common/model/textModel.ts: DidChangeDecorationsEmitter and DidChangeContentEmitter;
// src/vs/editor/common/model/decorationProvider.ts: LineHeightChangingDecoration and LineFontChangingDecoration;
// commit b57d5c9e), MIT license.

namespace TKCode.Arc.Editor.Model;

/// <summary>A decoration that changes the height of a line, recorded until the decorations event fires.</summary>
internal sealed record LineHeightChangingDecoration(int OwnerId, string DecorationId, int LineNumber, double? LineHeight)
{
    public static string ToKey(LineHeightChangingDecoration obj) => $"{obj.OwnerId};{obj.DecorationId};{obj.LineNumber}";
}

/// <summary>A decoration that changes the font of a line, recorded until the decorations event fires.</summary>
internal sealed record LineFontChangingDecoration(int OwnerId, string DecorationId, int LineNumber)
{
    public static string ToKey(LineFontChangingDecoration obj) => $"{obj.OwnerId};{obj.DecorationId};{obj.LineNumber}";
}

/// <summary>
/// Raises the decorations changed event once per batch: while a deferral is open it only records what was affected.
/// </summary>
internal sealed class DidChangeDecorationsEmitter
{
    /// <summary>Called with what the batch affected, before the event is raised.</summary>
    internal delegate void BeforeFireHandler(
        IReadOnlyCollection<int>? affectedInjectedTextLines,
        IReadOnlyCollection<LineHeightChangingDecoration>? affectedLineHeights,
        IReadOnlyCollection<LineFontChangingDecoration>? affectedFontLines);

    private readonly BeforeFireHandler _handleBeforeFire;
    private readonly Action<ModelDecorationsChangedEvent> _fire;
    private int _deferredCnt;
    private bool _shouldFireDeferred;
    private bool _affectsMinimap;
    private bool _affectsOverviewRuler;
    private HashSet<int>? _affectedInjectedTextLines;
    private Dictionary<string, LineHeightChangingDecoration>? _affectedLineHeights;
    private Dictionary<string, LineFontChangingDecoration>? _affectedFontLines;
    private bool _affectsGlyphMargin;
    private bool _affectsLineNumber;

    /// <param name="handleBeforeFire">Called with what the batch affected, before the event.</param>
    /// <param name="fire">Raises the event.</param>
    public DidChangeDecorationsEmitter(BeforeFireHandler handleBeforeFire, Action<ModelDecorationsChangedEvent> fire)
    {
        _handleBeforeFire = handleBeforeFire;
        _fire = fire;
    }

    public void BeginDeferredEmit() => _deferredCnt++;

    public void EndDeferredEmit()
    {
        _deferredCnt--;
        if (_deferredCnt == 0)
        {
            if (_shouldFireDeferred)
            {
                DoFire();
            }

            _affectedInjectedTextLines = null;
            _affectedLineHeights = null;
            _affectedFontLines = null;
        }
    }

    public void RecordLineAffectedByInjectedText(int lineNumber)
    {
        _affectedInjectedTextLines ??= new HashSet<int>();
        _affectedInjectedTextLines.Add(lineNumber);
    }

    public void RecordLineAffectedByLineHeightChange(int ownerId, string decorationId, int lineNumber, double? lineHeight)
    {
        _affectedLineHeights ??= new Dictionary<string, LineHeightChangingDecoration>();
        var item = new LineHeightChangingDecoration(ownerId, decorationId, lineNumber, lineHeight);
        // Upstream's SetWithKey keeps the first item of a key.
        _affectedLineHeights.TryAdd(LineHeightChangingDecoration.ToKey(item), item);
    }

    public void RecordLineAffectedByFontChange(int ownerId, string decorationId, int lineNumber)
    {
        _affectedFontLines ??= new Dictionary<string, LineFontChangingDecoration>();
        var item = new LineFontChangingDecoration(ownerId, decorationId, lineNumber);
        _affectedFontLines.TryAdd(LineFontChangingDecoration.ToKey(item), item);
    }

    public void CheckAffectedAndFire(ModelDecorationOptions options)
    {
        _affectsMinimap |= options.Minimap is not null;
        _affectsOverviewRuler |= options.OverviewRuler is { Color.IsEmpty: false };
        _affectsGlyphMargin |= options.GlyphMarginClassName is not null;
        _affectsLineNumber |= options.LineNumberClassName is not null;
        TryFire();
    }

    public void Fire()
    {
        _affectsMinimap = true;
        _affectsOverviewRuler = true;
        _affectsGlyphMargin = true;
        TryFire();
    }

    private void TryFire()
    {
        if (_deferredCnt == 0)
        {
            DoFire();
        }
        else
        {
            _shouldFireDeferred = true;
        }
    }

    private void DoFire()
    {
        _handleBeforeFire(
            _affectedInjectedTextLines,
            _affectedLineHeights?.Values,
            _affectedFontLines?.Values);

        var e = new ModelDecorationsChangedEvent(_affectsMinimap, _affectsOverviewRuler, _affectsGlyphMargin, _affectsLineNumber);
        _shouldFireDeferred = false;
        _affectsMinimap = false;
        _affectsOverviewRuler = false;
        _affectsGlyphMargin = false;
        // Upstream does not reset affectsLineNumber either.
        _fire(e);
    }
}

/// <summary>Raises the content changed event once per batch, with the batch's changes merged into one event.</summary>
internal sealed class DidChangeContentEmitter
{
    private readonly Action<InternalModelContentChangeEvent> _fire;
    private int _deferredCnt;
    private InternalModelContentChangeEvent? _deferredEvent;

    /// <param name="fire">Raises the event.</param>
    public DidChangeContentEmitter(Action<InternalModelContentChangeEvent> fire)
    {
        _fire = fire;
    }

    public void BeginDeferredEmit() => _deferredCnt++;

    public void EndDeferredEmit(IReadOnlyList<TextSelection>? resultingSelection = null)
    {
        _deferredCnt--;
        if (_deferredCnt == 0 && _deferredEvent is not null)
        {
            _deferredEvent.RawContentChangedEvent.ResultingSelection = resultingSelection;
            var e = _deferredEvent;
            _deferredEvent = null;
            _fire(e);
        }
    }

    public void Fire(InternalModelContentChangeEvent e)
    {
        if (_deferredCnt > 0)
        {
            _deferredEvent = _deferredEvent is not null ? _deferredEvent.Merge(e) : e;
            return;
        }

        _fire(e);
    }
}
