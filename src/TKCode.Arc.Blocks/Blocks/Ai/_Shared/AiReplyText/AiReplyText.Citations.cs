using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Model;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Citations of <see cref="AiReplyText"/>: painting the pills, hover, keyboard stops and the preview card.</summary>
internal partial class AiReplyText
{
    private const double LabelFontSize = 11;

    /// <summary>Hover (or keyboard focus) time before a preview opens.</summary>
    internal static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(350);

    /// <summary>Moving to another citation while a preview is open switches almost at once.</summary>
    internal static readonly TimeSpan SwitchDelay = TimeSpan.FromMilliseconds(80);

    /// <summary>Grace after the pointer leaves, so it can travel into the card.</summary>
    internal static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(200);

    private static readonly Easing s_linear = new LinearEasing();

    private readonly Dictionary<(string Label, bool Active), TextLayout> _labels = new();
    private readonly List<(int Number, int Key)> _shown = [];
    private readonly Dictionary<int, MotionValue<double>> _reveals = new();
    private readonly Dictionary<int, MotionValue<double>> _inversions = new();
    private readonly Dictionary<int, TextSegmentView> _segments = new();
    private readonly List<AiCitationTarget> _targetList = [];
    private Canvas? _targets;
    private Pen? _ringPen;
    private int _hoverKey = -1;
    private int _focusKey = -1;
    private bool _focusVisible;
    private int _cardKey = -1;
    private int _cardNumber;
    private int _pendingKey = -1;
    private int _pendingNumber;
    private int _dismissedKey = -1;
    private bool _pointerInCard;
    private int _pressedKey = -1;
    private TextSegmentView? _cursorSegment;
    private static readonly Cursor s_hand = new(StandardCursorType.Hand);
    private ArcPopup? _popup;
    private AiCitationCard? _card;
    private DispatcherTimer? _openTimer;
    private DispatcherTimer? _closeTimer;

    /// <summary>The citations shown so far, in reading order: their number and key (tests).</summary>
    internal IReadOnlyList<(int Number, int Key)> ShownCitations => _shown;

    /// <summary>The keyboard stops of the citations (tests).</summary>
    internal IReadOnlyList<AiCitationTarget> CitationTargets => _targetList;

    /// <summary>The key of the citation under the pointer, or -1.</summary>
    internal int HoveredKey => _hoverKey;

    /// <summary>The key of the citation whose preview is open, or -1.</summary>
    internal int CardKey => _cardKey;

    /// <summary>Whether a preview is open.</summary>
    internal bool IsCardOpen => _cardKey >= 0 && _popup?.IsOpen == true;

    /// <summary>The preview card (created on first open).</summary>
    internal AiCitationCard? Card => _card;

    /// <summary>The popup of the preview card (created on first open).</summary>
    internal ArcPopup? CardPopup => _popup;

    /// <summary>How inverted the pill with <paramref name="key"/> is (0 at rest, 1 hovered or previewed; tests).</summary>
    internal double InversionOf(int key) => _inversions.TryGetValue(key, out var value) ? value.Value : 0;

    /// <summary>How far the pill with <paramref name="key"/> has faded in (tests).</summary>
    internal double RevealOf(int key) => _reveals.TryGetValue(key, out var value) ? value.Value : 1;

    /// <summary>The source cited as <paramref name="number"/> (by <see cref="AiSource.Index"/>), or null.</summary>
    internal AiSource? FindSource(int number)
    {
        if (number < 1 || Sources is not { } sources)
        {
            return null;
        }
        for (var i = 0; i < sources.Count; i++)
        {
            if (sources[i] is { } source && source.Index == number)
            {
                return source;
            }
        }
        return null;
    }

    #region Painting

    /// <summary>Width of the pill for <paramref name="label"/>: the number plus 5 on each side, at least 18 (a circle).</summary>
    internal double PillWidth(string label)
        => Math.Max(AiCitationRun.PillHeight, Math.Ceiling(Label(label, active: false).WidthIncludingTrailingWhitespace) + 10);

    /// <summary>
    /// Paints a citation run: a pill on the muted surface when the reply has its source, else the bare number; inverted
    /// (foreground fill, background number) as far as its hover / preview state has turned; the keyboard ring; all at the
    /// opacity of its fade-in.
    /// </summary>
    internal void DrawCitation(DrawingContext context, Point origin, AiCitationRun run)
    {
        var opacity = run.Key >= 0 && _reveals.TryGetValue(run.Key, out var reveal) ? Math.Clamp(reveal.Value, 0, 1) : 1;
        if (opacity <= 0.004)
        {
            return;
        }
        var hasSource = FindSource(run.Number) is not null;
        var invert = hasSource && run.Key >= 0 && _inversions.TryGetValue(run.Key, out var inversion) ? Math.Clamp(inversion.Value, 0, 1) : 0;
        var pill = new Rect(origin.X + AiCitationRun.Margin, origin.Y, Math.Max(0, run.Size.Width - 2 * AiCitationRun.Margin), AiCitationRun.PillHeight);
        var radius = pill.Height / 2;
        using (context.PushOpacity(opacity))
        {
            if (hasSource && CitationBackground is { } rest)
            {
                context.DrawRectangle(rest, null, pill, radius, radius);
            }
            if (invert > 0 && CitationActiveBackground is { } active)
            {
                using (context.PushOpacity(invert))
                {
                    context.DrawRectangle(active, null, pill, radius, radius);
                }
            }
            DrawLabel(context, pill, Label(run.Label, active: false), 1 - invert);
            DrawLabel(context, pill, Label(run.Label, active: true), invert);
            if (run.Key >= 0 && run.Key == _focusKey && _focusVisible && CitationFocusBrush is { } ring)
            {
                // Arc's focus ring: 2px, 2px outside the pill.
                _ringPen ??= new Pen(ring, 2);
                context.DrawRectangle(null, _ringPen, pill.Inflate(3), radius + 3, radius + 3);
            }
        }
    }

    private static void DrawLabel(DrawingContext context, Rect pill, TextLayout layout, double opacity)
    {
        if (opacity <= 0.004)
        {
            return;
        }
        // Digits are cap-height glyphs: put the middle of the cap height (about 0.35em above the baseline) on the pill's middle.
        var x = pill.X + (pill.Width - layout.WidthIncludingTrailingWhitespace) / 2;
        var y = pill.Y + pill.Height / 2 + LabelFontSize * 0.35 - layout.Baseline;
        using (context.PushOpacity(opacity))
        {
            layout.Draw(context, new Point(x, y));
        }
    }

    private TextLayout Label(string label, bool active)
    {
        if (!_labels.TryGetValue((label, active), out var layout))
        {
            layout = new TextLayout(label, new Typeface(CitationFontFamily ?? FontFamily.Default), LabelFontSize,
                active ? CitationActiveForeground : CitationForeground);
            _labels[(label, active)] = layout;
        }
        return layout;
    }

    private void ClearLabels()
    {
        foreach (var layout in _labels.Values)
        {
            layout.Dispose();
        }
        _labels.Clear();
        _ringPen = null;
    }

    /// <summary>Repaints the lines of the pill with <paramref name="key"/> (its text layout stays as it is).</summary>
    private void InvalidateCitation(int key)
    {
        if (FindSegment(key, out _) is not { } segment)
        {
            return;
        }
        foreach (var line in segment.GetVisualChildren())
        {
            line.InvalidateVisual();
        }
    }

    private void InvalidateAllCitations()
    {
        if (_markdown is null)
        {
            return;
        }
        foreach (var segment in _markdown.GetVisualDescendants().OfType<TextSegmentView>())
        {
            if (TryFindOffset(segment, -1, out _))
            {
                foreach (var line in segment.GetVisualChildren())
                {
                    line.InvalidateVisual();
                }
            }
        }
    }

    #endregion

    #region Finding pills

    private static bool IsCitation(InlineObject item)
        => item.Kind == InlineObjectKind.Component && string.Equals(item.Value, "citation", StringComparison.OrdinalIgnoreCase);

    /// <summary>The offset of the citation with <paramref name="key"/> in <paramref name="segment"/> (-1: any citation).</summary>
    private static bool TryFindOffset(TextSegmentView segment, int key, out int offset)
    {
        foreach (var item in segment.Content.Objects)
        {
            if (IsCitation(item) && (key < 0 || AiCitationRenderer.Parse(item.Attributes["key"]) == key))
            {
                offset = item.Offset;
                return true;
            }
        }
        offset = 0;
        return false;
    }

    /// <summary>The text segment that shows the citation with <paramref name="key"/>, once the view has laid it out.</summary>
    private TextSegmentView? FindSegment(int key, out int offset)
    {
        if (_segments.TryGetValue(key, out var cached) && cached.IsAttachedToVisualTree() && TryFindOffset(cached, key, out offset))
        {
            return cached;
        }
        if (_markdown is not null)
        {
            foreach (var segment in _markdown.GetVisualDescendants().OfType<TextSegmentView>())
            {
                if (TryFindOffset(segment, key, out offset))
                {
                    _segments[key] = segment;
                    return segment;
                }
            }
        }
        offset = 0;
        return null;
    }

    /// <summary>The pill of the citation with <paramref name="key"/> in this control's coordinates, or null before it is laid out.</summary>
    internal Rect? GetCitationRect(int key)
    {
        if (FindSegment(key, out var offset) is not { Layout: { } layout } segment)
        {
            return null;
        }
        var box = BoxOf(layout, offset);
        var baseline = AiCitationRun.BaselineOffset;
        foreach (var line in layout.TextLines)
        {
            if (offset >= line.FirstTextSourceIndex && offset < line.FirstTextSourceIndex + line.Length)
            {
                baseline = line.Baseline;
                break;
            }
        }
        var pill = new Rect(box.X + AiCitationRun.Margin, box.Y + baseline - AiCitationRun.BaselineOffset,
            Math.Max(0, box.Width - 2 * AiCitationRun.Margin), AiCitationRun.PillHeight);
        return segment.TranslatePoint(pill.TopLeft, this) is { } at ? new Rect(at, pill.Size) : null;
    }

    /// <summary>
    /// The line box of the embedded object at <paramref name="offset"/>. (A caret position has no width at the end of a
    /// segment, so this asks for the range of the one placeholder character.)
    /// </summary>
    private static Rect BoxOf(SegmentLayout layout, int offset)
    {
        foreach (var rect in layout.HitTestTextRange(offset, 1))
        {
            return rect;
        }
        return default;
    }

    private static bool CitationAt(TextSegmentView segment, Point point, out int key, out int number)
    {
        key = -1;
        number = 0;
        if (segment.Layout is not { } layout)
        {
            return false;
        }
        foreach (var item in segment.Content.Objects)
        {
            if (IsCitation(item) && BoxOf(layout, item.Offset).Contains(point))
            {
                key = AiCitationRenderer.Parse(item.Attributes["key"]);
                number = AiCitationRenderer.Parse(item.Attributes["id"]);
                return key >= 0;
            }
        }
        return false;
    }

    #endregion

    #region State

    /// <summary>A citation reached the Markdown: it gets a keyboard stop and, when batches fade, its own fade-in.</summary>
    private void OnCitationShown(int number, int key, bool fade)
    {
        _shown.Add((number, key));
        var duration = FadeDuration();
        if (fade && duration > TimeSpan.Zero)
        {
            var value = new MotionValue<double>(this, 0, MotionVectors.Double);
            value.Changed += _ => InvalidateCitation(key);
            _reveals[key] = value;
            _ = FadeIn(key, value, duration);
        }
        AddTarget(number, key);
    }

    private async Task FadeIn(int key, MotionValue<double> value, TimeSpan duration)
    {
        // Linear, like StreamMark's batch fade, so the pill keeps pace with the words around it.
        var done = await value.AnimateTo(1, MotionSpec.Tween(duration, s_linear));
        if (done && _reveals.TryGetValue(key, out var current) && ReferenceEquals(current, value))
        {
            _reveals.Remove(key);
            value.Dispose();
            InvalidateCitation(key);
        }
    }

    /// <summary>Turns the pill with <paramref name="key"/> toward inverted (hovered or previewed) or back: 160 ms, 120 ms linear at reduced motion.</summary>
    private void UpdateActive(int key)
    {
        if (key < 0)
        {
            return;
        }
        // Only a linked citation inverts; a plain number stays as it is.
        var active = (key == _hoverKey || key == _cardKey) && FindSource(NumberOf(key)) is not null;
        if (!_inversions.TryGetValue(key, out var value))
        {
            if (!active)
            {
                return;
            }
            value = new MotionValue<double>(this, 0, MotionVectors.Double);
            value.Changed += _ => InvalidateCitation(key);
            _inversions[key] = value;
        }
        var target = active ? 1.0 : 0.0;
        if (value.Target.Equals(target) && (value.IsAnimating || value.Value.Equals(target)))
        {
            return;
        }
        switch (ArcMotion.GetEffectiveLevel(this))
        {
            case ArcMotionLevel.Full:
                _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                break;
            case ArcMotionLevel.Reduced:
                _ = value.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
                break;
            default:
                value.Jump(target);
                break;
        }
    }

    /// <summary>The number cited by the citation with <paramref name="key"/> (keys count the citations in order).</summary>
    private int NumberOf(int key)
    {
        if (key >= 0 && key < _shown.Count && _shown[key].Key == key)
        {
            return _shown[key].Number;
        }
        foreach (var (number, shownKey) in _shown)
        {
            if (shownKey == key)
            {
                return number;
            }
        }
        return 0;
    }

    private void ClearCitations()
    {
        CloseCard();
        foreach (var value in _reveals.Values)
        {
            value.Dispose();
        }
        foreach (var value in _inversions.Values)
        {
            value.Dispose();
        }
        _reveals.Clear();
        _inversions.Clear();
        _shown.Clear();
        _segments.Clear();
        _hoverKey = _focusKey = _dismissedKey = -1;
        _focusVisible = false;
        _targets?.Children.Clear();
        _targetList.Clear();
    }

    private void OnSourcesChanged()
    {
        UpdateTargetNames();
        InvalidateAllCitations();
        if (_cardKey >= 0)
        {
            if (FindSource(_cardNumber) is { } source)
            {
                _card?.Show(source, _cardNumber);
            }
            else
            {
                CloseCard();
            }
        }
    }

    #endregion

    #region Keyboard stops

    private void AddTarget(int number, int key)
    {
        var target = new AiCitationTarget(this, number, key);
        UpdateTarget(target);
        _targetList.Add(target);
        _targets?.Children.Add(target);
    }

    private void RebuildTargets()
    {
        if (_targets is null)
        {
            return;
        }
        _targets.Children.Clear();
        foreach (var target in _targetList)
        {
            _targets.Children.Add(target);
        }
    }

    /// <summary>A citation without a source is a plain number: its stop is hidden (and so skipped by Tab).</summary>
    private void UpdateTarget(AiCitationTarget target)
    {
        var source = FindSource(target.Number);
        target.IsVisible = source is not null;
        AutomationProperties.SetName(target, source is null ? null : CitationName?.Invoke(target.Number, source.Title, source.Domain));
    }

    private void UpdateTargetNames()
    {
        foreach (var target in _targetList)
        {
            UpdateTarget(target);
        }
    }

    private AiCitationTarget? TargetFor(int key)
    {
        foreach (var target in _targetList)
        {
            if (target.Key == key)
            {
                return target;
            }
        }
        return null;
    }

    internal void OnTargetFocused(AiCitationTarget target, bool keyboard)
    {
        var previous = _focusKey;
        _focusKey = target.Key;
        _focusVisible = keyboard;
        if (previous >= 0 && previous != target.Key)
        {
            InvalidateCitation(previous);
        }
        if (GetCitationRect(target.Key) is { } rect)
        {
            // Sit on the pill so a scroll viewer can bring it into view.
            Canvas.SetLeft(target, rect.X);
            Canvas.SetTop(target, rect.Y);
            target.Width = rect.Width;
            target.Height = rect.Height;
            Dispatcher.UIThread.Post(() =>
            {
                if (target.IsFocused)
                {
                    target.BringIntoView();
                }
            }, DispatcherPriority.Background);
        }
        InvalidateCitation(target.Key);
        if (FindSource(target.Number) is not null)
        {
            ScheduleOpen(target.Key, target.Number);
        }
    }

    internal void OnTargetBlurred(AiCitationTarget target)
    {
        if (_focusKey != target.Key)
        {
            return;
        }
        _focusKey = -1;
        _focusVisible = false;
        if (_dismissedKey == target.Key)
        {
            _dismissedKey = -1;
        }
        InvalidateCitation(target.Key);
        ScheduleClose();
    }

    /// <summary>Raises <see cref="CitationInvoked"/> for the source cited as <paramref name="number"/>, if the reply has it.</summary>
    internal void InvokeCitation(int number, object origin)
    {
        if (FindSource(number) is { } source)
        {
            RaiseEvent(new AiReplyTextSourceEventArgs(CitationInvokedEvent, this, source, number));
        }
    }

    #endregion

    #region Pointer

    private void OnPointerMovedInside(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        var segment = HitCitation(e, out var key, out var number);
        SetHover(segment, key, number);
        if (segment is not null && FindSource(number) is not null)
        {
            // StreamMark shows the hand only over the left half of an embedded object.
            segment.Cursor = s_hand;
            _cursorSegment = segment;
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHover(null, -1, 0);
    }

    private void SetHover(TextSegmentView? segment, int key, int number)
    {
        if (key == _hoverKey)
        {
            return;
        }
        var previous = _hoverKey;
        _hoverKey = key;
        if (segment is not null && key >= 0)
        {
            _segments[key] = segment;
        }
        if (_cursorSegment is { } shaped && !ReferenceEquals(shaped, segment))
        {
            shaped.Cursor = Cursor.Default;
            _cursorSegment = null;
        }
        if (key != _dismissedKey && _focusKey != _dismissedKey)
        {
            _dismissedKey = -1;
        }
        UpdateActive(previous);
        UpdateActive(key);
        if (key >= 0 && FindSource(number) is not null)
        {
            ScheduleOpen(key, number);
        }
        else
        {
            ScheduleClose();
        }
    }

    private void OnComponentClicked(object? sender, ComponentClickedEventArgs e)
    {
        // The click itself is taken from the pointer release (OnPointerReleasedInside): StreamMark only reports the
        // left half of an embedded object. Handled so StreamMark does not try to open a URL of its own.
        if (string.Equals(e.Name, "citation", StringComparison.OrdinalIgnoreCase))
        {
            e.Handled = true;
        }
    }

    private void OnPointerPressedInside(object? sender, PointerPressedEventArgs e)
    {
        _pressedKey = e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && HitCitation(e, out var key, out _) is not null ? key : -1;
    }

    private void OnPointerReleasedInside(object? sender, PointerReleasedEventArgs e)
    {
        var pressed = _pressedKey;
        _pressedKey = -1;
        if (pressed < 0 || e.InitialPressMouseButton != MouseButton.Left || _markdown is { HasSelection: true }
            || HitCitation(e, out var key, out var number) is null || key != pressed)
        {
            return;
        }
        InvokeCitation(number, this);
    }

    /// <summary>The text segment and citation under the pointer of <paramref name="e"/>, if it is one of this reply's.</summary>
    private TextSegmentView? HitCitation(PointerEventArgs e, out int key, out int number)
    {
        key = -1;
        number = 0;
        if (e.Source is Visual source && (source as TextSegmentView ?? source.FindAncestorOfType<TextSegmentView>()) is { } hit
            && ReferenceEquals(hit.FindAncestorOfType<AiReplyText>(), this) && CitationAt(hit, e.GetPosition(hit), out key, out number))
        {
            return hit;
        }
        return null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CloseCard();
        _hoverKey = -1;
    }

    #endregion

    #region Card

    private void ScheduleOpen(int key, int number)
    {
        _closeTimer?.Stop();
        if (key == _cardKey || key == _dismissedKey)
        {
            return;
        }
        _pendingKey = key;
        _pendingNumber = number;
        _openTimer ??= CreateTimer(OnOpenTimer);
        _openTimer.Stop();
        _openTimer.Interval = IsCardOpen ? SwitchDelay : OpenDelay;
        _openTimer.Start();
    }

    private void ScheduleClose()
    {
        _openTimer?.Stop();
        _pendingKey = -1;
        if (!IsCardOpen || _pointerInCard || _cardKey == _hoverKey || _cardKey == _focusKey)
        {
            return;
        }
        _closeTimer ??= CreateTimer(OnCloseTimer);
        _closeTimer.Stop();
        _closeTimer.Interval = CloseDelay;
        _closeTimer.Start();
    }

    private void OnOpenTimer()
    {
        var key = _pendingKey;
        _pendingKey = -1;
        if (key >= 0 && (key == _hoverKey || key == _focusKey))
        {
            OpenCard(key, _pendingNumber);
        }
    }

    private void OnCloseTimer()
    {
        if (IsCardOpen && !_pointerInCard && _cardKey != _hoverKey && _cardKey != _focusKey)
        {
            CloseCard();
        }
    }

    private static DispatcherTimer CreateTimer(Action tick)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal);
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            tick();
        };
        return timer;
    }

    /// <summary>Runs pending open and close timers now (tests: headless dispatcher timers do not advance).</summary>
    internal void FlushCardTimers()
    {
        if (_openTimer?.IsEnabled == true)
        {
            _openTimer.Stop();
            OnOpenTimer();
        }
        if (_closeTimer?.IsEnabled == true)
        {
            _closeTimer.Stop();
            OnCloseTimer();
        }
    }

    /// <summary>Opens (or moves) the preview above the citation with <paramref name="key"/>, centered, 8px away.</summary>
    internal void OpenCard(int key, int number)
    {
        if (FindSource(number) is not { } source || GetCitationRect(key) is not { } rect)
        {
            return;
        }
        EnsurePopup();
        _card!.Show(source, number);
        var previous = _cardKey;
        _cardKey = key;
        _cardNumber = number;
        _popup!.PlacementRect = rect;
        if (_popup.IsOpen)
        {
            // Offset changes are the public way to make the popup run its placement again.
            var offset = _popup.VerticalOffset;
            _popup.VerticalOffset = offset + 1;
            _popup.VerticalOffset = offset;
        }
        else
        {
            _popup.IsOpen = true;
        }
        if (previous >= 0 && previous != key)
        {
            SetDescription(previous, null);
            UpdateActive(previous);
        }
        UpdateActive(key);
        // Avalonia has no aria-describedby: the preview rides on the citation's help text while it is open.
        SetDescription(key, string.IsNullOrWhiteSpace(source.Snippet) ? source.Title : source.Title + ". " + source.Snippet);
    }

    /// <summary>Closes the preview; <paramref name="dismissed"/> (Escape) keeps it closed while the citation stays hovered or focused.</summary>
    internal void CloseCard(bool dismissed = false)
    {
        _openTimer?.Stop();
        _closeTimer?.Stop();
        _pendingKey = -1;
        _pointerInCard = false;
        var key = _cardKey;
        _cardKey = -1;
        if (dismissed)
        {
            _dismissedKey = key;
        }
        if (_popup is { IsOpen: true })
        {
            _popup.IsOpen = false;
        }
        if (key >= 0)
        {
            SetDescription(key, null);
            UpdateActive(key);
        }
    }

    private void SetDescription(int key, string? text)
    {
        if (TargetFor(key) is { } target)
        {
            AutomationProperties.SetHelpText(target, text);
        }
    }

    private void EnsurePopup()
    {
        if (_popup is not null)
        {
            return;
        }
        _card = new AiCitationCard();
        _card.PointerEntered += OnCardEntered;
        _card.PointerExited += OnCardExited;
        _popup = new ArcPopup
        {
            Child = _card,
            Placement = PlacementMode.Top,
            PlacementTarget = this,
            IsLightDismissEnabled = false,
            TakesFocusFromNativeControl = false,
            EnterScale = 0.96,
            EnterOffset = 4
        };
        LogicalChildren.Add(_popup);
    }

    private void OnCardEntered(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type == PointerType.Touch)
        {
            return;
        }
        _pointerInCard = true;
        _closeTimer?.Stop();
    }

    private void OnCardExited(object? sender, PointerEventArgs e)
    {
        _pointerInCard = false;
        ScheduleClose();
    }

    #endregion
}
