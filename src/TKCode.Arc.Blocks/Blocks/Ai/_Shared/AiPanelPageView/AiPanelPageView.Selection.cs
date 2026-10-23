using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The page selection: Avalonia selects inside one text only, while Arc's page selects across blocks and table cells.
/// The page therefore owns the selection: the texts take no input; a press sets the anchor at the nearest character, a
/// drag moves the focus end, and every text shows its part of the range (<see cref="AiPanelSelectionText.RangeIn"/>).
/// A double press selects a word and a triple press a whole text (mouse and pen; a touch drag scrolls the page). Release
/// with a non-empty range shows the ask button under the range's last line (right edge − 24, bottom + 8, kept 14px inside
/// the article's right edge); a new selection moves it by fading the old button out (100ms) while a second one fades in at
/// the new place.
/// </summary>
internal sealed partial class AiPanelPageView
{
    /// <summary>How far left of the selection's last line end the button starts.</summary>
    public const double AskInsetX = 24;

    /// <summary>How far under the selection's last line the button sits.</summary>
    public const double AskGapY = 8;

    /// <summary>The space the button keeps from the article's right edge.</summary>
    public const double AskEdge = 14;

    /// <summary>How far the button rises while it enters (Arc: translateY 3 → 0).</summary>
    public const double AskRise = 3;

    private readonly AiPanelAskButton[] _asks = new AiPanelAskButton[2];
    private readonly AiFadePresence[] _askPresence = new AiFadePresence[2];
    private readonly List<AiPanelTextPiece> _pieces = [];
    private readonly List<(AiPanelText Text, AiPanelBlockView View)> _pieceTexts = [];
    private bool _piecesDirty = true;
    private int _activeAsk;
    private AiPanelTextPoint? _anchor;
    private AiPanelTextPoint? _focus;
    private bool _dragging;
    private bool _byWords;
    private AiPanelSelection? _selection;

    /// <summary>The selection on the page, or null (it is offered by the ask button once the pointer is released).</summary>
    internal AiPanelSelection? Selection => _selection;

    /// <summary>The ask button on show (or last shown).</summary>
    internal AiPanelAskButton AskButton => _asks[_activeAsk];

    /// <summary>Both ask buttons (one enters while the other leaves).</summary>
    internal IReadOnlyList<AiPanelAskButton> AskButtons => _asks;

    /// <summary>Whether the ask button is shown (or entering).</summary>
    internal bool IsAskShown => _askPresence[_activeAsk].IsShown;

    private void CreateAskButtons()
    {
        for (var i = 0; i < _asks.Length; i++)
        {
            var ask = new AiPanelAskButton();
            ask.Click += OnAskClick;
            _asks[i] = ask;
            _askPresence[i] = new AiFadePresence(ask, false, AskRise);
        }
        RefreshAskButtons();
    }

    private void RefreshAskButtons()
    {
        foreach (var ask in _asks)
        {
            ask.Text = AskButtonText;
            ask.Mark = AssistantMark;
        }
    }

    /// <summary>Clears the page selection and hides the ask button.</summary>
    public void ClearSelection()
    {
        _anchor = null;
        _focus = null;
        _selection = null;
        ApplySelection();
        HideAsk();
    }

    /// <summary>Selects from <paramref name="anchor"/> to <paramref name="focus"/> as a drag would, then releases (tests and hosts).</summary>
    internal void Select(AiPanelTextPoint anchor, AiPanelTextPoint focus)
    {
        EnsurePieces();
        _anchor = anchor;
        _focus = focus;
        ApplySelection();
        FinishSelection();
    }

    /// <summary>The selectable texts in reading order (rebuilt when blocks change).</summary>
    internal IReadOnlyList<AiPanelTextPiece> Pieces
    {
        get
        {
            EnsurePieces();
            return _pieces;
        }
    }

    /// <summary>The text control of piece <paramref name="index"/>.</summary>
    internal AiPanelText TextOf(int index)
    {
        EnsurePieces();
        return _pieceTexts[index].Text;
    }

    private void EnsurePieces()
    {
        if (!_piecesDirty)
        {
            return;
        }
        _piecesDirty = false;
        _pieces.Clear();
        _pieceTexts.Clear();
        foreach (var view in _views)
        {
            foreach (var (text, row, column) in view.Texts)
            {
                _pieces.Add(new AiPanelTextPiece(view.Block, text.Text ?? string.Empty, row, column));
                _pieceTexts.Add((text, view));
            }
        }
        // Offsets into texts that are gone mean nothing.
        _anchor = null;
        _focus = null;
    }

    /// <summary>
    /// The texts are about to change (a block's content, blocks added, removed or moved): offsets into them would point at
    /// other characters, so a selection ends here and the reading order is rebuilt on next use.
    /// </summary>
    private void InvalidatePieces()
    {
        if (_anchor is not null || _selection is not null || _dragging)
        {
            _dragging = false;
            ClearSelection();
        }
        _piecesDirty = true;
    }

    private void ApplySelection()
    {
        if (_piecesDirty)
        {
            // Texts from before the change: clear whatever they show.
            foreach (var view in _views)
            {
                foreach (var (text, _, _) in view.Texts)
                {
                    text.Select(0, 0);
                }
            }
            return;
        }
        var has = _anchor is not null && _focus is not null;
        var (start, end) = has ? AiPanelSelectionText.Order(_anchor!.Value, _focus!.Value) : default;
        for (var i = 0; i < _pieceTexts.Count; i++)
        {
            var text = _pieceTexts[i].Text;
            var (from, to) = has ? AiPanelSelectionText.RangeIn(i, text.Length, start, end) : (0, 0);
            text.Select(from, to);
        }
    }

    private void FinishSelection()
    {
        _selection = _anchor is { } a && _focus is { } f && Page is { } page
            ? AiPanelSelectionText.Describe(_pieces, a, f, page.Blocks)
            : null;
        if (_selection is null)
        {
            HideAsk();
            return;
        }
        ShowAsk();
    }

    #region Ask button

    private void ShowAsk()
    {
        if (_article is null || LastLineRect() is not { } line)
        {
            HideAsk();
            return;
        }
        // A new place: the button on show (or still fading out after the press) leaves while the other one enters at the
        // new place (Arc keeps both for a moment).
        if (_askPresence[_activeAsk].IsShown || _asks[_activeAsk].IsVisible)
        {
            _askPresence[_activeAsk].SetShown(false);
            _activeAsk = 1 - _activeAsk;
        }
        var ask = _asks[_activeAsk];
        _askPresence[_activeAsk].SetShown(true);
        ask.Measure(Size.Infinity);
        var width = ask.DesiredSize.Width;
        var right = _article.Bounds.Width - width - AskEdge;
        var left = Math.Max(0, Math.Min(line.Right - AskInsetX, right));
        // Whole pixels: the article lays out without rounding, the button's frame should stay crisp.
        ask.Position = new Point(Math.Round(left), Math.Round(line.Bottom + AskGapY));
        _article.InvalidateArrange();
    }

    private void HideAsk()
    {
        foreach (var presence in _askPresence)
        {
            presence.SetShown(false);
        }
    }

    /// <summary>The last line rectangle of the selection, in article coordinates.</summary>
    private Rect? LastLineRect()
    {
        if (_anchor is not { } a || _focus is not { } f || _article is null)
        {
            return null;
        }
        var (start, end) = AiPanelSelectionText.Order(a, f);
        for (var i = Math.Min(end.Index, _pieceTexts.Count - 1); i >= start.Index && i >= 0; i--)
        {
            var text = _pieceTexts[i].Text;
            var (from, to) = AiPanelSelectionText.RangeIn(i, text.Length, start, end);
            if (to <= from)
            {
                continue;
            }
            Rect? last = null;
            foreach (var rect in text.RangeRects(from, to))
            {
                if (last is not { } l || rect.Bottom > l.Bottom + 0.5 || (Math.Abs(rect.Bottom - l.Bottom) <= 0.5 && rect.Right > l.Right))
                {
                    last = rect;
                }
            }
            if (last is { } found && text.TranslatePoint(found.TopLeft, _article) is { } topLeft)
            {
                return new Rect(topLeft, found.Size);
            }
        }
        return null;
    }

    private void OnAskClick(object? sender, RoutedEventArgs e)
    {
        if (_selection is not { } selection)
        {
            return;
        }
        ClearSelection();
        RaiseEvent(new AiPanelSelectionEventArgs(SelectionRequestedEvent, this, selection));
    }

    #endregion

    #region Pointer

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || _article is null)
        {
            return;
        }
        var point = e.GetCurrentPoint(this);
        // A touch drag scrolls the page; selecting is for the mouse and the pen.
        if (!point.Properties.IsLeftButtonPressed || e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        EnsurePieces();
        var at = PositionAt(e.GetPosition(_article), press: true);
        HideAsk();
        _selection = null;
        if (at is not { } hit)
        {
            _anchor = null;
            _focus = null;
            ApplySelection();
            return;
        }
        var text = _pieces[hit.Index].Text;
        // A double or triple press selects a word or a text; the drag that follows does not move it.
        _byWords = e.ClickCount >= 2;
        if (e.ClickCount == 2)
        {
            var (from, to) = AiPanelSelectionText.WordAt(text, hit.Offset);
            _anchor = new AiPanelTextPoint(hit.Index, from);
            _focus = new AiPanelTextPoint(hit.Index, to);
        }
        else if (e.ClickCount >= 3)
        {
            _anchor = new AiPanelTextPoint(hit.Index, 0);
            _focus = new AiPanelTextPoint(hit.Index, text.Length);
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _anchor is not null)
        {
            _focus = hit;
        }
        else
        {
            _anchor = hit;
            _focus = hit;
        }
        ApplySelection();
        _dragging = true;
        e.Pointer.Capture(this);
        Focus(NavigationMethod.Pointer);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || !ReferenceEquals(e.Pointer.Captured, this))
        {
            return;
        }
        MoveFocus(e.GetPosition(_article));
    }

    private void MoveFocus(Point point)
    {
        if (_byWords || _anchor is null || _article is null)
        {
            return;
        }
        if (PositionAt(point, press: false) is { } at && at != _focus)
        {
            _focus = at;
            ApplySelection();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }
        // The release point ends the range even when no move was reported on the way.
        MoveFocus(e.GetPosition(_article));
        _dragging = false;
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            e.Pointer.Capture(null);
        }
        FinishSelection();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            _dragging = false;
            FinishSelection();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _selection is not { } selection)
        {
            return;
        }
        var copy = Application.Current?.PlatformSettings?.HotkeyConfiguration.Copy;
        if (copy is not null && copy.Any(g => g.Matches(e)))
        {
            _ = ArcClipboard.CopyTextAsync(this, selection.Text);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The caret position nearest to <paramref name="point"/> (article coordinates). A press above the first block (the title)
    /// or with no texts gives null; a drag there clamps to the first text's start. Between blocks and beside them the nearest
    /// block counts; inside a block the nearest of its texts (a table's cells).
    /// </summary>
    internal AiPanelTextPoint? PositionAt(Point point, bool press)
    {
        EnsurePieces();
        if (_pieceTexts.Count == 0 || _article is null)
        {
            return null;
        }
        AiPanelBlockView? nearest = null;
        var best = double.PositiveInfinity;
        foreach (var view in _views)
        {
            if (view.Texts.Count == 0)
            {
                continue;
            }
            var bounds = view.Bounds;
            if (nearest is null && point.Y < bounds.Top - view.Margin.Top)
            {
                if (press)
                {
                    return null;
                }
                return First(view);
            }
            var distance = point.Y < bounds.Top ? bounds.Top - point.Y : point.Y > bounds.Bottom ? point.Y - bounds.Bottom : 0;
            if (distance < best)
            {
                best = distance;
                nearest = view;
            }
        }
        if (nearest is null)
        {
            return null;
        }
        var index = -1;
        var bestText = double.PositiveInfinity;
        for (var i = 0; i < _pieceTexts.Count; i++)
        {
            var (text, view) = _pieceTexts[i];
            if (!ReferenceEquals(view, nearest) || text.TranslatePoint(default, _article) is not { } origin)
            {
                continue;
            }
            var rect = new Rect(origin, text.Bounds.Size);
            var dx = point.X < rect.Left ? rect.Left - point.X : point.X > rect.Right ? point.X - rect.Right : 0;
            var dy = point.Y < rect.Top ? rect.Top - point.Y : point.Y > rect.Bottom ? point.Y - rect.Bottom : 0;
            // Rows first: a point level with a row picks a cell of that row.
            var score = dy * 1000 + dx;
            if (score < bestText)
            {
                bestText = score;
                index = i;
            }
        }
        if (index < 0)
        {
            return null;
        }
        var target = _pieceTexts[index].Text;
        var local = _article.TranslatePoint(point, target) ?? default;
        return new AiPanelTextPoint(index, target.OffsetAt(local));
    }

    private AiPanelTextPoint? First(AiPanelBlockView view)
    {
        for (var i = 0; i < _pieceTexts.Count; i++)
        {
            if (ReferenceEquals(_pieceTexts[i].View, view))
            {
                return new AiPanelTextPoint(i, 0);
            }
        }
        return null;
    }

    #endregion
}
