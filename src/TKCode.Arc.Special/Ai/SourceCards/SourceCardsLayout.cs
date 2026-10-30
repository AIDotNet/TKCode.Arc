using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>How a view arrives in a <see cref="SourceCardsCellHost"/>.</summary>
internal enum SourceCardsEnter
{
    /// <summary>A card: fades in rising 4px over 180ms on the enter curve.</summary>
    Rise,

    /// <summary>The stack or a reserved slot: fades in over 180ms, no movement.</summary>
    Fade
}

/// <summary>
/// The cells of the source grid: every cell is placed by its index (row-major), whatever it shows, so a leaving view fades
/// in place even after the rows it sits in no longer count. Wide containers get <see cref="Max"/> + 1 equal columns, those
/// narrower than <see cref="NarrowWidth"/> two (Arc's container query: the width is this panel's own). Every row is
/// <see cref="CardHeight"/> high, so a slot, a card and the stack take the same room and the grid never shifts as it
/// fills. Its width is the width it is given, never its content's.
/// </summary>
internal sealed class SourceCardsGrid : Panel
{
    public static readonly StyledProperty<int> MaxProperty =
        AvaloniaProperty.Register<SourceCardsGrid, int>(nameof(Max), 3);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<SourceCardsGrid, double>(nameof(Gap), 8);

    public static readonly StyledProperty<double> CardHeightProperty =
        AvaloniaProperty.Register<SourceCardsGrid, double>(nameof(CardHeight), 80);

    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<SourceCardsGrid, double>(nameof(NarrowWidth), SourceCardsFormat.NarrowWidth);

    /// <summary>The cells that count for the height (the plan); children past them may still be fading out.</summary>
    public static readonly StyledProperty<int> CellCountProperty =
        AvaloniaProperty.Register<SourceCardsGrid, int>(nameof(CellCount));

    static SourceCardsGrid()
    {
        AffectsMeasure<SourceCardsGrid>(MaxProperty, GapProperty, CardHeightProperty, NarrowWidthProperty, CellCountProperty);
    }

    public int Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public double CardHeight { get => GetValue(CardHeightProperty); set => SetValue(CardHeightProperty, value); }

    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    public int CellCount { get => GetValue(CellCountProperty); set => SetValue(CellCountProperty, value); }

    /// <summary>The columns of the last layout.</summary>
    internal int Columns { get; private set; }

    private double WidthFor(double available) =>
        double.IsFinite(available) ? available : (Math.Max(1, Max) + 1) * 180;

    private double CellWidth(double width, int columns) => Math.Max(0, (width - Gap * (columns - 1)) / columns);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = WidthFor(availableSize.Width);
        var columns = SourceCardsFormat.Columns(width, Max, NarrowWidth);
        Columns = columns;
        var cell = new Size(CellWidth(width, columns), CardHeight);
        foreach (var child in Children)
        {
            child.Measure(cell);
        }
        var rows = SourceCardsFormat.Rows(CellCount, columns);
        var height = rows == 0 ? 0 : rows * CardHeight + (rows - 1) * Gap;
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = SourceCardsFormat.Columns(finalSize.Width, Max, NarrowWidth);
        Columns = columns;
        var width = CellWidth(finalSize.Width, columns);
        for (var i = 0; i < Children.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            Children[i].Arrange(new Rect(column * (width + Gap), row * (CardHeight + Gap), width, CardHeight));
        }
        return finalSize;
    }
}

/// <summary>
/// One cell of the grid. It trades views in place: the arriving one fades in (a card also rises 4px, 180ms on the enter
/// curve) while the leaving one fades out where it is over 120ms and then leaves the tree. Reduced motion keeps the fades
/// (120ms) and drops the rise; off, and before the first layout, everything changes at once. Code-driven
/// (<see cref="MotionValue{T}"/>) because two views overlap in one cell on their own clocks and the leaving one is removed
/// when its fade ends, which ControlTheme transitions cannot express.
/// </summary>
internal sealed class SourceCardsCellHost : Control
{
    /// <summary>How far a card rises as it arrives.</summary>
    internal const double Rise = 4;

    internal static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(180);

    internal static readonly TimeSpan ExitDuration = TimeSpan.FromMilliseconds(120);

    private static readonly Easing s_linear = new LinearEasing();

    private sealed class Layer
    {
        public required Control View { get; init; }
        public required TranslateTransform Shift { get; init; }
        public MotionValue<double>? Progress { get; set; }
    }

    private readonly List<Layer> _leaving = new(2);
    private Layer? _current;

    /// <summary>The view shown now (null for an empty cell).</summary>
    internal Control? View => _current?.View;

    /// <summary>The views still fading out.</summary>
    internal int LeavingCount => _leaving.Count;

    /// <summary>Shows <paramref name="view"/> (null empties the cell).</summary>
    internal void Show(Control? view, SourceCardsEnter enter)
    {
        if (ReferenceEquals(_current?.View, view))
        {
            return;
        }
        if (view is not null)
        {
            foreach (var layer in _leaving.Where(l => ReferenceEquals(l.View, view)).ToList())
            {
                Remove(layer);
            }
            if (view.GetVisualParent() is SourceCardsCellHost other && !ReferenceEquals(other, this))
            {
                other.Release(view);
            }
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        var animate = IsLoaded && IsEffectivelyVisible && level != ArcMotionLevel.Off;
        if (_current is { } outgoing)
        {
            _current = null;
            if (animate)
            {
                StartExit(outgoing, level == ArcMotionLevel.Full);
            }
            else
            {
                Remove(outgoing);
            }
        }
        if (view is not null)
        {
            var shift = new TranslateTransform();
            view.RenderTransform = shift;
            _current = new Layer { View = view, Shift = shift };
            LogicalChildren.Add(view);
            VisualChildren.Add(view);
            if (animate)
            {
                StartEnter(_current, enter, level == ArcMotionLevel.Full);
            }
            else
            {
                view.Opacity = 1;
            }
        }
        InvalidateMeasure();
    }

    /// <summary>Lets another cell take <paramref name="view"/>.</summary>
    private void Release(Control view)
    {
        if (ReferenceEquals(_current?.View, view))
        {
            Remove(_current!);
            _current = null;
            return;
        }
        foreach (var layer in _leaving.Where(l => ReferenceEquals(l.View, view)).ToList())
        {
            Remove(layer);
        }
    }

    private void StartEnter(Layer layer, SourceCardsEnter enter, bool full)
    {
        var rises = full && enter == SourceCardsEnter.Rise;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        layer.View.Opacity = 0;
        layer.Shift.Y = rises ? Rise : 0;
        progress.Changed += p =>
        {
            layer.View.Opacity = Math.Clamp(p, 0, 1);
            layer.Shift.Y = rises ? Rise * (1 - p) : 0;
        };
        var spec = full
            ? MotionSpec.Tween(EnterDuration, enter == SourceCardsEnter.Rise ? ArcEasings.Enter : ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, s_linear);
        _ = progress.AnimateTo(1, spec);
    }

    private void StartExit(Layer layer, bool full)
    {
        layer.Progress?.Stop();
        _leaving.Add(layer);
        layer.View.IsHitTestVisible = false;
        var from = layer.View.Opacity;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p => layer.View.Opacity = Math.Clamp(from * (1 - p), 0, 1);
        progress.AnimateTo(1, MotionSpec.Tween(ExitDuration, full ? ArcEasings.Standard : s_linear))
            .ContinueWith(_ => Dispatcher.UIThread.Post(() =>
            {
                if (_leaving.Contains(layer))
                {
                    Remove(layer);
                }
            }), TaskScheduler.Default);
    }

    private void Remove(Layer layer)
    {
        layer.Progress?.Stop();
        _leaving.Remove(layer);
        VisualChildren.Remove(layer.View);
        LogicalChildren.Remove(layer.View);
        layer.View.Opacity = 1;
        layer.View.IsHitTestVisible = true;
        layer.Shift.Y = 0;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _leaving)
        {
            layer.View.Measure(availableSize);
        }
        _current?.View.Measure(availableSize);
        return new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
            double.IsFinite(availableSize.Height) ? availableSize.Height : 0);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rect = new Rect(finalSize);
        foreach (var layer in _leaving)
        {
            layer.View.Arrange(rect);
        }
        _current?.View.Arrange(rect);
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _leaving.ToList())
        {
            Remove(layer);
        }
        if (_current is { } current)
        {
            current.Progress?.Stop();
            current.View.Opacity = 1;
            current.Shift.Y = 0;
        }
    }
}

/// <summary>
/// A count that rolls in a reserved width ("3 of 7", "7 sources", "+4 more"): digits are tabular (each in a cell as wide as
/// the widest digit), so the width only changes with the number of digits. A new value at full motion rolls 5px: the old
/// text rises and fades within the first 100ms while the new one rises in from 5px below over 160ms (a smaller number
/// rolls the other way). Reduced motion and off change it in place. Code-driven: two layers of drawn text on one clock,
/// which ControlTheme transitions cannot express. A frame allocates nothing (text layouts are cached per run).
/// </summary>
internal sealed class SourceCardsCount : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SourceCardsCount, string?>(nameof(Text));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SourceCardsCount>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<SourceCardsCount>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<SourceCardsCount>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<SourceCardsCount>();

    /// <summary>The line height; NaN uses 1.4 × the font size.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<SourceCardsCount, double>(nameof(LineHeight), double.NaN);

    /// <summary>How far the digits roll.</summary>
    internal const double Roll = 5;

    internal static readonly TimeSpan RollDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>The share of the roll in which the old text is gone (100ms of 160ms).</summary>
    private const double ExitShare = 100.0 / 160.0;

    private readonly Dictionary<string, TextLayout> _runs = new(StringComparer.Ordinal);
    private double _digit = -1;
    private string _current = string.Empty;
    private string? _previous;
    private int _direction = 1;
    private MotionValue<double>? _roll;

    static SourceCardsCount()
    {
        AffectsMeasure<SourceCardsCount>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, LineHeightProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<SourceCardsCount>(false);
        FocusableProperty.OverrideDefaultValue<SourceCardsCount>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>The text shown now.</summary>
    internal string DisplayedText => _current;

    /// <summary>Whether a roll is under way.</summary>
    internal bool IsRolling => _roll?.IsAnimating == true;

    /// <summary>+1 when the number grew (it rolls up), −1 when it shrank.</summary>
    internal static int DirectionOf(string? from, string? to)
    {
        var a = LeadingNumber(from);
        var b = LeadingNumber(to);
        return a is { } x && b is { } y && y < x ? -1 : 1;
    }

    private static long? LeadingNumber(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        long value = 0;
        var any = false;
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c))
            {
                value = value * 10 + (c - '0');
                any = true;
            }
            else if (any)
            {
                break;
            }
        }
        return any ? value : null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            OnTextChanged(change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == ForegroundProperty || change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == LineHeightProperty)
        {
            ClearRuns();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void OnTextChanged(string text)
    {
        if (text == _current)
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level != ArcMotionLevel.Full || _current.Length == 0 || text.Length == 0)
        {
            _roll?.Jump(1);
            _previous = null;
            _current = text;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }
        _direction = DirectionOf(_current, text);
        _previous = _current;
        _current = text;
        _roll ??= CreateRoll();
        _roll.Jump(0);
        _roll.AnimateTo(1, MotionSpec.Tween(RollDuration, ArcEasings.Standard)).ContinueWith(_ => Dispatcher.UIThread.Post(() =>
        {
            if (_roll?.IsAnimating != true)
            {
                _previous = null;
                InvalidateMeasure();
                InvalidateVisual();
            }
        }), TaskScheduler.Default);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private MotionValue<double> CreateRoll()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }

    private double LineBox => double.IsNaN(LineHeight) ? FontSize * 1.4 : LineHeight;

    private void ClearRuns()
    {
        foreach (var layout in _runs.Values)
        {
            layout.Dispose();
        }
        _runs.Clear();
        _digit = -1;
    }

    private TextLayout Run(string text)
    {
        if (!_runs.TryGetValue(text, out var layout))
        {
            layout = new TextLayout(text, new Typeface(FontFamily, FontStyle.Normal, FontWeight), FontSize,
                Foreground ?? Brushes.Black, lineHeight: LineBox);
            _runs[text] = layout;
        }
        return layout;
    }

    private double DigitWidth
    {
        get
        {
            if (_digit < 0)
            {
                _digit = 0;
                for (var d = 0; d <= 9; d++)
                {
                    _digit = Math.Max(_digit, Run(((char)('0' + d)).ToString()).WidthIncludingTrailingWhitespace);
                }
            }
            return _digit;
        }
    }

    /// <summary>The width of <paramref name="text"/> with tabular digits.</summary>
    private double WidthOf(string text) => Draw(null, text, 0);

    /// <summary>Draws <paramref name="text"/> at <paramref name="y"/> (or only measures it when <paramref name="context"/> is null).</summary>
    private double Draw(DrawingContext? context, string text, double y)
    {
        var x = 0.0;
        var start = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            var isDigit = i < text.Length && char.IsAsciiDigit(text[i]);
            if (i < text.Length && !isDigit)
            {
                continue;
            }
            if (i > start)
            {
                var run = Run(text[start..i]);
                if (context is not null)
                {
                    run.Draw(context, new Point(x, y));
                }
                x += run.WidthIncludingTrailingWhitespace;
            }
            if (isDigit)
            {
                var glyph = Run(text[i].ToString());
                if (context is not null)
                {
                    glyph.Draw(context, new Point(x + (DigitWidth - glyph.WidthIncludingTrailingWhitespace) / 2, y));
                }
                x += DigitWidth;
            }
            start = i + 1;
        }
        return x;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = WidthOf(_current);
        if (_previous is { } previous)
        {
            width = Math.Max(width, WidthOf(previous));
        }
        return new Size(Math.Ceiling(width), _current.Length == 0 && _previous is null ? 0 : LineBox);
    }

    public override void Render(DrawingContext context)
    {
        var p = Math.Clamp(_roll?.Value ?? 1, 0, 1);
        if (_previous is { } previous && p < ExitShare)
        {
            using (context.PushOpacity(1 - p / ExitShare))
            {
                Draw(context, previous, -_direction * Roll * p);
            }
        }
        if (_current.Length == 0)
        {
            return;
        }
        if (_previous is null || p >= 1)
        {
            Draw(context, _current, 0);
            return;
        }
        using (context.PushOpacity(p))
        {
            Draw(context, _current, _direction * Roll * (1 - p));
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _roll?.Jump(1);
        _previous = null;
    }
}
