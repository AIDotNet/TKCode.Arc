using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>One piece of a <see cref="PickerRollText"/> line; literal pieces (separators) never roll.</summary>
internal readonly record struct PickerRollSegment(string Text, bool Rolls = true);

/// <summary>How a <see cref="PickerRollText"/> pairs the pieces of the old and the new line.</summary>
internal enum PickerRollMatching
{
    /// <summary>
    /// Date parts (DatePicker, TimePicker, status lines): pieces pair by index while the line keeps its shape; a
    /// different shape (placeholder, other separators) swaps the whole line.
    /// </summary>
    Parts,

    /// <summary>
    /// Words (DateRangePicker): pieces pair by index, extra words appear and missing ones vanish at once (Arc's
    /// <c>initial={false}</c> word slots), and only words that changed roll.
    /// </summary>
    Words
}

/// <summary>
/// The value text of the pickers (P3 + P19): every changed piece rolls in time's direction (a later value rises from
/// below through a soft blur while the old copy lifts away), pieces that kept their text hold still or slide to their
/// new place when a neighbour changed width, and a leaving copy keeps its own place so the next one never waits for
/// room. Each copy is one small drawn layer moved by a render transform; nothing relayouts while it moves.
/// Reduced motion crossfades in place; Off swaps at once. Assistive technology reads the owner's name, not this layer.
/// </summary>
internal sealed class PickerRollText : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<PickerRollText>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<PickerRollText>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<PickerRollText>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<PickerRollText>();

    public static readonly StyledProperty<IBrush?> PlaceholderForegroundProperty =
        AvaloniaProperty.Register<PickerRollText, IBrush?>(nameof(PlaceholderForeground));

    public static readonly StyledProperty<FontWeight?> PlaceholderFontWeightProperty =
        AvaloniaProperty.Register<PickerRollText, FontWeight?>(nameof(PlaceholderFontWeight));

    /// <summary>Line height in px (NaN = the font's own line).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<PickerRollText, double>(nameof(LineHeight), double.NaN);

    /// <summary>Gap between pieces in em (DateRangePicker words: 0.28).</summary>
    public static readonly StyledProperty<double> SegmentGapProperty =
        AvaloniaProperty.Register<PickerRollText, double>(nameof(SegmentGap));

    public static readonly StyledProperty<PickerRollMatching> MatchingProperty =
        AvaloniaProperty.Register<PickerRollText, PickerRollMatching>(nameof(Matching));

    /// <summary>Travel of the incoming copy in em (Arc: 0.35; DateRangePicker 0.45; status 0.3).</summary>
    public static readonly StyledProperty<double> EnterTravelProperty =
        AvaloniaProperty.Register<PickerRollText, double>(nameof(EnterTravel), 0.35);

    public static readonly StyledProperty<double> ExitTravelProperty =
        AvaloniaProperty.Register<PickerRollText, double>(nameof(ExitTravel), 0.3);

    public static readonly StyledProperty<double> EnterBlurProperty =
        AvaloniaProperty.Register<PickerRollText, double>(nameof(EnterBlur), 4);

    public static readonly StyledProperty<double> ExitBlurProperty =
        AvaloniaProperty.Register<PickerRollText, double>(nameof(ExitBlur), 2);

    public static readonly StyledProperty<TimeSpan> EnterDurationProperty =
        AvaloniaProperty.Register<PickerRollText, TimeSpan>(nameof(EnterDuration), ArcMotionPresets.Standard);

    public static readonly StyledProperty<TimeSpan> ExitDurationProperty =
        AvaloniaProperty.Register<PickerRollText, TimeSpan>(nameof(ExitDuration), TimeSpan.FromMilliseconds(140));

    /// <summary>Crossfade length under reduced motion (Arc: duration.instant, 120ms).</summary>
    public static readonly StyledProperty<TimeSpan> ReducedDurationProperty =
        AvaloniaProperty.Register<PickerRollText, TimeSpan>(nameof(ReducedDuration), ArcMotionPresets.Instant);

    /// <summary>Pieces that kept their text slide to their new place (DatePicker's layout="position").</summary>
    public static readonly StyledProperty<bool> AnimatePositionsProperty =
        AvaloniaProperty.Register<PickerRollText, bool>(nameof(AnimatePositions));

    /// <summary>Pins the line to the trailing edge (status lines), so a shorter line never shifts.</summary>
    public static readonly StyledProperty<bool> AlignEndProperty =
        AvaloniaProperty.Register<PickerRollText, bool>(nameof(AlignEnd));

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? PlaceholderForeground { get => GetValue(PlaceholderForegroundProperty); set => SetValue(PlaceholderForegroundProperty, value); }
    public FontWeight? PlaceholderFontWeight { get => GetValue(PlaceholderFontWeightProperty); set => SetValue(PlaceholderFontWeightProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public double SegmentGap { get => GetValue(SegmentGapProperty); set => SetValue(SegmentGapProperty, value); }
    public PickerRollMatching Matching { get => GetValue(MatchingProperty); set => SetValue(MatchingProperty, value); }
    public double EnterTravel { get => GetValue(EnterTravelProperty); set => SetValue(EnterTravelProperty, value); }
    public double ExitTravel { get => GetValue(ExitTravelProperty); set => SetValue(ExitTravelProperty, value); }
    public double EnterBlur { get => GetValue(EnterBlurProperty); set => SetValue(EnterBlurProperty, value); }
    public double ExitBlur { get => GetValue(ExitBlurProperty); set => SetValue(ExitBlurProperty, value); }
    public TimeSpan EnterDuration { get => GetValue(EnterDurationProperty); set => SetValue(EnterDurationProperty, value); }
    public TimeSpan ExitDuration { get => GetValue(ExitDurationProperty); set => SetValue(ExitDurationProperty, value); }
    public TimeSpan ReducedDuration { get => GetValue(ReducedDurationProperty); set => SetValue(ReducedDurationProperty, value); }
    public bool AnimatePositions { get => GetValue(AnimatePositionsProperty); set => SetValue(AnimatePositionsProperty, value); }
    public bool AlignEnd { get => GetValue(AlignEndProperty); set => SetValue(AlignEndProperty, value); }

    private readonly List<Slot> _slots = new();
    private readonly List<Piece> _leaving = new();
    private readonly Stack<Piece> _pool = new();
    private bool _placeholder;
    private double _lineWidth;
    private double _lineHeight;
    private int _animating;
    private int _fontVersion;

    static PickerRollText()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<PickerRollText>(false);
        ClipToBoundsProperty.OverrideDefaultValue<PickerRollText>(false);
        AutomationProperties.AccessibilityViewProperty.OverrideDefaultValue<PickerRollText>(AccessibilityView.Raw);
    }

    /// <summary>The text of the current line (pieces joined).</summary>
    public string Text { get; private set; } = string.Empty;

    public bool IsPlaceholder => _placeholder;

    /// <summary>Width of the current line.</summary>
    public double LineWidth => _lineWidth;

    /// <summary>True while any copy is still moving.</summary>
    public bool IsRolling => _animating > 0;

    /// <summary>Raised when the last moving copy settles.</summary>
    public event EventHandler? Settled;

    /// <summary>Shows a single-piece line.</summary>
    public void Show(string text, bool placeholder, int direction, bool animate, int? exitDirection = null)
    {
        _single[0] = new PickerRollSegment(text);
        Show(_single, placeholder, direction, animate, exitDirection);
    }

    private readonly PickerRollSegment[] _single = new PickerRollSegment[1];
    private readonly List<PickerRollSegment> _words = new();

    /// <summary>Shows <paramref name="text"/> split into words (whitespace runs separate them).</summary>
    public void ShowWords(string text, bool placeholder, int direction, bool animate)
    {
        _words.Clear();
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            _words.Add(new PickerRollSegment(word));
        }
        Show(_words, placeholder, direction, animate);
    }

    /// <summary>
    /// Shows a new line. <paramref name="direction"/> is 1 when the value moved later (the new copy rises from below)
    /// and -1 when earlier; <paramref name="exitDirection"/> overrides the direction the old copy leaves in when the
    /// whole line swaps (DatePicker: the placeholder drops in from above while the date still lifts away).
    /// </summary>
    public void Show(IReadOnlyList<PickerRollSegment> segments, bool placeholder, int direction, bool animate, int? exitDirection = null)
    {
        if (placeholder == _placeholder && SameSegments(segments))
        {
            return;
        }
        Text = Join(segments);
        var level = ArcMotion.GetEffectiveLevel(this);
        var motion = animate && IsLoaded && IsEffectivelyVisible && level != ArcMotionLevel.Off && _slots.Count > 0;
        var full = level == ArcMotionLevel.Full;
        var dir = direction >= 0 ? 1 : -1;
        var whole = Matching == PickerRollMatching.Parts && (placeholder != _placeholder || !SameShape(segments));
        if (!motion)
        {
            ClearAll();
            _placeholder = placeholder;
            Build(segments, enter: false, full, dir);
            return;
        }
        if (whole)
        {
            var exitDir = exitDirection is { } e ? (e >= 0 ? 1 : -1) : dir;
            foreach (var slot in _slots)
            {
                if (slot.Piece is { } piece)
                {
                    Exit(piece, full, exitDir);
                }
            }
            _slots.Clear();
            _placeholder = placeholder;
            Build(segments, enter: true, full, dir);
            return;
        }
        _placeholder = placeholder;
        var positions = Layout(segments, placeholder, out var width);
        var count = segments.Count;
        for (var i = 0; i < count; i++)
        {
            var segment = segments[i];
            if (i >= _slots.Count)
            {
                // Words mode: a new word slot appears without animation (Arc's initial={false}).
                var slot = new Slot { Text = segment.Text, Rolls = segment.Rolls };
                slot.Piece = Rent(segment.Text, placeholder);
                Place(slot.Piece, positions[i], width, animate: false);
                _slots.Add(slot);
                continue;
            }
            var existing = _slots[i];
            if (existing.Text == segment.Text && existing.Piece is { } restyled && restyled.IsPlaceholder != placeholder)
            {
                // Same word in the other style (placeholder <-> value): restyle in place.
                Return(restyled);
                existing.Piece = Rent(segment.Text, placeholder);
            }
            if (existing.Text == segment.Text)
            {
                existing.Rolls = segment.Rolls;
                if (existing.Piece is { } still)
                {
                    still.LineWidth = width;
                    Place(still, positions[i], width, animate: AnimatePositions && full);
                }
                continue;
            }
            if (existing.Piece is { } old)
            {
                if (segment.Rolls || existing.Rolls)
                {
                    Exit(old, full, dir);
                }
                else
                {
                    Return(old);
                }
            }
            existing.Text = segment.Text;
            existing.Rolls = segment.Rolls;
            var incoming = Rent(segment.Text, placeholder);
            existing.Piece = incoming;
            Place(incoming, positions[i], width, animate: false);
            if (segment.Rolls)
            {
                Enter(incoming, full, dir);
            }
        }
        for (var i = _slots.Count - 1; i >= count; i--)
        {
            // Words mode: a word slot that is gone vanishes with its slot.
            if (_slots[i].Piece is { } gone)
            {
                Return(gone);
            }
            _slots.RemoveAt(i);
        }
        SetLine(width);
    }

    /// <summary>Drops every copy at once (no animation).</summary>
    public void Clear()
    {
        ClearAll();
        Text = string.Empty;
        SetLine(0);
    }

    private static string Join(IReadOnlyList<PickerRollSegment> segments)
    {
        if (segments.Count == 1)
        {
            return segments[0].Text;
        }
        return string.Concat(segments.Select(s => s.Text));
    }

    private bool SameSegments(IReadOnlyList<PickerRollSegment> segments)
    {
        if (segments.Count != _slots.Count)
        {
            return false;
        }
        for (var i = 0; i < segments.Count; i++)
        {
            if (_slots[i].Text != segments[i].Text || _slots[i].Rolls != segments[i].Rolls)
            {
                return false;
            }
        }
        return true;
    }

    private bool SameShape(IReadOnlyList<PickerRollSegment> segments)
    {
        if (segments.Count != _slots.Count)
        {
            return false;
        }
        for (var i = 0; i < segments.Count; i++)
        {
            var slot = _slots[i];
            if (slot.Rolls != segments[i].Rolls || (!slot.Rolls && slot.Text != segments[i].Text))
            {
                return false;
            }
        }
        return true;
    }

    private void Build(IReadOnlyList<PickerRollSegment> segments, bool enter, bool full, int dir)
    {
        var positions = Layout(segments, _placeholder, out var width);
        for (var i = 0; i < segments.Count; i++)
        {
            var slot = new Slot { Text = segments[i].Text, Rolls = segments[i].Rolls };
            var piece = Rent(slot.Text, _placeholder);
            slot.Piece = piece;
            Place(piece, positions[i], width, animate: false);
            if (enter)
            {
                Enter(piece, full, dir);
            }
            _slots.Add(slot);
        }
        SetLine(width);
    }

    private readonly List<double> _positions = new();

    /// <summary>Left edge of each piece on the new line (pieces measured one by one, plus the word gap).</summary>
    private List<double> Layout(IReadOnlyList<PickerRollSegment> segments, bool placeholder, out double width)
    {
        _positions.Clear();
        var gap = Matching == PickerRollMatching.Words ? SegmentGap * FontSize : 0;
        var x = 0.0;
        for (var i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                x += gap;
            }
            _positions.Add(x);
            x += Measure(segments[i].Text, placeholder);
        }
        width = Math.Ceiling(x);
        return _positions;
    }

    private readonly Dictionary<(string, bool), double> _widths = new();

    private double Measure(string text, bool placeholder)
    {
        if (_widths.TryGetValue((text, placeholder), out var width))
        {
            return width;
        }
        using var layout = CreateLayout(text, placeholder);
        width = layout.WidthIncludingTrailingWhitespace;
        if (_widths.Count > 256)
        {
            _widths.Clear();
        }
        _widths[(text, placeholder)] = width;
        return width;
    }

    internal TextLayout CreateLayout(string text, bool placeholder)
    {
        var weight = placeholder ? PlaceholderFontWeight ?? FontWeight : FontWeight;
        var brush = placeholder ? PlaceholderForeground ?? Foreground : Foreground;
        return new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, weight), FontSize, brush,
            TextAlignment.Left, TextWrapping.NoWrap, TextTrimming.None, null, FlowDirection.LeftToRight,
            double.PositiveInfinity, double.PositiveInfinity, LineHeight, 0, 1);
    }

    private void SetLine(double width)
    {
        _lineWidth = width;
        foreach (var slot in _slots)
        {
            if (slot.Piece is { } piece)
            {
                piece.LineWidth = width;
            }
        }
        InvalidateMeasure();
        InvalidateArrange();
    }

    private Piece Rent(string text, bool placeholder)
    {
        var piece = _pool.Count > 0 ? _pool.Pop() : new Piece(this);
        piece.Reset(text, placeholder, _fontVersion);
        if (!VisualChildren.Contains(piece))
        {
            VisualChildren.Add(piece);
        }
        piece.IsVisible = true;
        return piece;
    }

    private void Return(Piece piece)
    {
        if (piece.IsMoving)
        {
            piece.StopAll();
        }
        _leaving.Remove(piece);
        piece.IsVisible = false;
        piece.Effect = null;
        _pool.Push(piece);
    }

    private void ClearAll()
    {
        foreach (var slot in _slots)
        {
            if (slot.Piece is { } piece)
            {
                Return(piece);
            }
        }
        _slots.Clear();
        foreach (var piece in _leaving.ToArray())
        {
            Return(piece);
        }
        _leaving.Clear();
        _animating = 0;
    }

    private void Place(Piece piece, double x, double lineWidth, bool animate)
    {
        piece.LineWidth = lineWidth;
        if (animate && piece.SlotX != x)
        {
            BeginMotion();
            _ = piece.SlideTo(x, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)).ContinueWith(EndMotionTask, TaskScheduler.Default);
        }
        else
        {
            piece.JumpX(x);
        }
    }

    private void Enter(Piece piece, bool full, int dir)
    {
        var em = FontSize;
        BeginMotion();
        Task<bool> task;
        if (!full)
        {
            task = piece.Fade(enter: true, 0, 0, 0, 0, MotionSpec.Tween(ReducedDuration, ArcEasings.Standard));
        }
        else
        {
            task = piece.Fade(enter: true, dir * EnterTravel * em, 0, EnterBlur, 0,
                MotionSpec.Tween(EnterDuration, ArcEasings.Enter));
        }
        _ = task.ContinueWith(EndMotionTask, TaskScheduler.Default);
    }

    private void Exit(Piece piece, bool full, int dir)
    {
        _leaving.Add(piece);
        var em = FontSize;
        BeginMotion();
        var startY = piece.OffsetY;
        Task<bool> task = !full
            ? piece.Fade(enter: false, startY, startY, 0, 0, MotionSpec.Tween(ReducedDuration, ArcEasings.Standard))
            : piece.Fade(enter: false, startY, -dir * ExitTravel * em, 0, ExitBlur, MotionSpec.Tween(ExitDuration, ArcEasings.Standard));
        _ = task.ContinueWith(t =>
        {
            Dispatcher.Post(() =>
            {
                if (_leaving.Contains(piece))
                {
                    Return(piece);
                }
                EndMotion();
            });
        }, TaskScheduler.Default);
    }

    private void BeginMotion() => _animating++;

    private void EndMotionTask(Task<bool> task) => Dispatcher.Post(EndMotion);

    private void EndMotion()
    {
        if (_animating <= 0)
        {
            return;
        }
        _animating--;
        if (_animating == 0)
        {
            Settled?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty || change.Property == FontWeightProperty ||
            change.Property == ForegroundProperty || change.Property == PlaceholderForegroundProperty ||
            change.Property == PlaceholderFontWeightProperty || change.Property == LineHeightProperty ||
            change.Property == SegmentGapProperty)
        {
            _widths.Clear();
            _fontVersion++;
            if (_slots.Count == 0)
            {
                return;
            }
            // Metrics changed: rebuild the line in place (no motion).
            var segments = _slots.Select(s => new PickerRollSegment(s.Text, s.Rolls)).ToArray();
            var placeholder = _placeholder;
            ClearAll();
            _placeholder = placeholder;
            Build(segments, enter: false, full: false, dir: 1);
        }
        else if (change.Property == AlignEndProperty)
        {
            InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var height = double.IsNaN(LineHeight) ? 0 : LineHeight;
        foreach (var visual in VisualChildren)
        {
            if (visual is Piece { IsVisible: true } piece)
            {
                piece.Measure(Size.Infinity);
                height = Math.Max(height, piece.DesiredSize.Height);
            }
        }
        if (height <= 0)
        {
            height = Math.Ceiling(FontSize * 1.4);
        }
        _lineHeight = height;
        return new Size(_lineWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var visual in VisualChildren)
        {
            if (visual is Piece piece)
            {
                piece.Arrange(new Rect(0, 0, Math.Max(piece.DesiredSize.Width, 0), finalSize.Height));
                piece.Base = AlignEnd ? finalSize.Width - piece.LineWidth : 0;
                piece.Apply();
            }
        }
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Settle in place: leaving copies go, the current ones rest.
        foreach (var piece in _leaving.ToArray())
        {
            Return(piece);
        }
        foreach (var slot in _slots)
        {
            slot.Piece?.Settle();
        }
        _animating = 0;
    }

    private sealed class Slot
    {
        public string Text = string.Empty;
        public bool Rolls;
        public Piece? Piece;
    }

    /// <summary>One drawn copy of a piece.</summary>
    private sealed class Piece : Control
    {
        private readonly PickerRollText _owner;
        private readonly TranslateTransform _translate = new();
        private TextLayout? _layout;
        private MotionValue<double>? _fade;
        private MotionValue<double>? _slide;
        private bool _entering;
        private double _fromY;
        private double _toY;
        private double _fromBlur;
        private double _toBlur;
        private double _fromOpacity;

        public Piece(PickerRollText owner)
        {
            _owner = owner;
            RenderTransform = _translate;
            IsHitTestVisible = false;
        }

        public string Text { get; private set; } = string.Empty;
        public bool IsPlaceholder { get; private set; }
        public double LineWidth { get; set; }
        public double Base { get; set; }
        public double SlotX { get; private set; }
        public double OffsetY { get; private set; }
        public bool IsMoving => _fade?.IsAnimating == true || _slide?.IsAnimating == true;

        private int _version = -1;

        public void Reset(string text, bool placeholder, int version)
        {
            StopAll();
            if (Text != text || IsPlaceholder != placeholder || _layout is null || _version != version)
            {
                Text = text;
                IsPlaceholder = placeholder;
                _version = version;
                _layout?.Dispose();
                _layout = null;
                InvalidateMeasure();
            }
            Opacity = 1;
            OffsetY = 0;
            Effect = null;
            InvalidateVisual();
        }

        public void StopAll()
        {
            _fade?.Stop();
            _slide?.Stop();
        }

        public void Settle()
        {
            StopAll();
            Opacity = 1;
            OffsetY = 0;
            Effect = null;
            if (_slide is not null)
            {
                SlotX = _slide.Target;
            }
            Apply();
        }

        public void JumpX(double x)
        {
            _slide?.Stop();
            SlotX = x;
            Apply();
        }

        public Task<bool> SlideTo(double x, MotionSpec spec)
        {
            if (_slide is null)
            {
                _slide = new MotionValue<double>(this, SlotX, MotionVectors.Double);
                _slide.Changed += v =>
                {
                    SlotX = v;
                    Apply();
                };
            }
            else
            {
                _slide.Jump(SlotX);
            }
            return _slide.AnimateTo(x, spec);
        }

        public Task<bool> Fade(bool enter, double fromY, double toY, double fromBlur, double toBlur, MotionSpec spec)
        {
            if (_fade is null)
            {
                _fade = new MotionValue<double>(this, 0, MotionVectors.Double);
                _fade.Changed += Step;
            }
            _entering = enter;
            _fromY = fromY;
            _toY = toY;
            _fromBlur = fromBlur;
            _toBlur = toBlur;
            _fromOpacity = enter ? 0 : Opacity;
            _fade.Jump(0);
            return _fade.AnimateTo(1, spec);
        }

        private void Step(double p)
        {
            Opacity = _entering ? p : _fromOpacity * (1 - p);
            OffsetY = _fromY + (_toY - _fromY) * p;
            Effect = PickerDrawing.Blur(_fromBlur + (_toBlur - _fromBlur) * p);
            Apply();
        }

        public void Apply()
        {
            _translate.X = Base + SlotX;
            _translate.Y = OffsetY;
        }

        private TextLayout Layout => _layout ??= _owner.CreateLayout(Text, IsPlaceholder);

        protected override Size MeasureOverride(Size availableSize)
        {
            var layout = Layout;
            return new Size(Math.Ceiling(layout.WidthIncludingTrailingWhitespace), layout.Height);
        }

        public override void Render(DrawingContext context)
        {
            var layout = Layout;
            layout.Draw(context, new Point(0, (Bounds.Height - layout.Height) / 2));
        }
    }
}
