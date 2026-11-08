using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The feature list of the <see cref="UsagePricing"/> card (notes 2b.5, 6.2, 6.3): lines are matched by key when the
/// recommendation changes. A line that stays keeps its place in the visual tree, slides from where it was to its new
/// slot (FLIP on the Smooth spring) and morphs its value; a new line fades in from 8px above over 240ms (enter curve); a
/// line that goes leaves the layout at once but stays painted where it was while it fades out over 120ms. The list's
/// own height follows on a soft Smooth spring, so the card's button glides up or down. Avalonia has no layout animation:
/// slots are computed here and the motion is a render offset, so only the height spring runs layout.
/// </summary>
internal sealed class UsagePricingFeatureList : Panel
{
    /// <summary>Arc's list frame was fitted at ω 10.6, ζ .90 (a frame late): the Smooth family, a little softer.</summary>
    internal static readonly SpringSpec HeightSpring = SpringSpec.FromVisual(0.45, 0);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<UsagePricingFeatureList, double>(nameof(RowHeight), 36);

    public static readonly StyledProperty<double> TopPaddingProperty =
        AvaloniaProperty.Register<UsagePricingFeatureList, double>(nameof(TopPadding), 10);

    public static readonly StyledProperty<IBrush?> StrongBrushProperty =
        AvaloniaProperty.Register<UsagePricingFeatureList, IBrush?>(nameof(StrongBrush));

    public static readonly StyledProperty<IBrush?> SoftBrushProperty =
        AvaloniaProperty.Register<UsagePricingFeatureList, IBrush?>(nameof(SoftBrush));

    public static readonly StyledProperty<IBrush?> ValueBrushProperty =
        AvaloniaProperty.Register<UsagePricingFeatureList, IBrush?>(nameof(ValueBrush));

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }
    public double TopPadding { get => GetValue(TopPaddingProperty); set => SetValue(TopPaddingProperty, value); }
    public IBrush? StrongBrush { get => GetValue(StrongBrushProperty); set => SetValue(StrongBrushProperty, value); }
    public IBrush? SoftBrush { get => GetValue(SoftBrushProperty); set => SetValue(SoftBrushProperty, value); }
    public IBrush? ValueBrush { get => GetValue(ValueBrushProperty); set => SetValue(ValueBrushProperty, value); }

    private readonly List<UsagePricingFeatureRow> _rows = new();
    private MotionValue<double>? _height;
    private double _targetHeight;

    static UsagePricingFeatureList()
    {
        AffectsMeasure<UsagePricingFeatureList>(RowHeightProperty, TopPaddingProperty);
        ClipToBoundsProperty.OverrideDefaultValue<UsagePricingFeatureList>(true);
    }

    /// <summary>The lines in the layout, top to bottom (leaving lines excluded; tests).</summary>
    internal IReadOnlyList<UsagePricingFeatureRow> Rows => _rows;

    /// <summary>Every row still painted, leaving ones included (tests).</summary>
    internal IEnumerable<UsagePricingFeatureRow> PaintedRows => Children.OfType<UsagePricingFeatureRow>();

    /// <summary>The height the list is heading for.</summary>
    internal double TargetHeight => _targetHeight;

    /// <summary>The height the list has right now.</summary>
    internal double CurrentHeight => _height?.Value ?? _targetHeight;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StrongBrushProperty || change.Property == SoftBrushProperty || change.Property == ValueBrushProperty)
        {
            foreach (var row in Children.OfType<UsagePricingFeatureRow>())
            {
                row.ApplyBrushes(StrongBrush, SoftBrush, ValueBrush);
            }
        }
    }

    /// <summary>Shows <paramref name="lines"/>; with <paramref name="animate"/> the change moves as described above.</summary>
    public void Update(IReadOnlyList<UsagePricingFeatureLine> lines, bool animate)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        animate = animate && IsLoaded && level != ArcMotionLevel.Off;
        var full = animate && level == ArcMotionLevel.Full;
        var rowHeight = RowHeight;
        var top = TopPadding;

        // Where every current line is drawn right now: the start of its slide.
        var drawnAt = new Dictionary<UsagePricingFeatureRow, double>();
        foreach (var row in _rows)
        {
            drawnAt[row] = row.SlotY + row.Shift.Y;
        }
        var byKey = new Dictionary<string, UsagePricingFeatureRow>(StringComparer.Ordinal);
        foreach (var row in _rows)
        {
            byKey.TryAdd(row.Key, row);
        }

        var next = new List<UsagePricingFeatureRow>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var slot = top + i * rowHeight;
            if (byKey.Remove(line.Key, out var row))
            {
                row.SetLine(line, animate);
                row.SlotY = slot;
                var offset = drawnAt[row] - slot;
                if (full && Math.Abs(offset) > 0.5)
                {
                    var motion = ShiftOf(row);
                    motion.Jump(offset);
                    _ = motion.AnimateTo(0, MotionSpec.Spring(ArcMotionPresets.Smooth));
                }
                else
                {
                    row.ShiftMotion?.Jump(0);
                    row.Shift.Y = 0;
                }
            }
            else
            {
                row = new UsagePricingFeatureRow(line.Key) { SlotY = slot };
                row.ApplyBrushes(StrongBrush, SoftBrush, ValueBrush);
                row.SetLine(line, animate: false);
                Children.Add(row);
                if (animate)
                {
                    var fade = FadeOf(row);
                    fade.Jump(0);
                    _ = fade.AnimateTo(1, full
                        ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
                        : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
                    if (full)
                    {
                        var shift = ShiftOf(row);
                        shift.Jump(-8);
                        _ = shift.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
                    }
                }
            }
            next.Add(row);
        }

        // Lines that go: out of the layout now, painted where they were while they fade.
        foreach (var gone in byKey.Values)
        {
            gone.IsLeaving = true;
            gone.SlotY = drawnAt[gone];
            gone.ShiftMotion?.Stop();
            gone.Shift.Y = 0;
            if (animate)
            {
                var leaving = gone;
                FadeOf(gone).AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard)).ContinueWith(_ =>
                    Dispatcher.UIThread.Post(() => Children.Remove(leaving)), TaskScheduler.Default);
            }
            else
            {
                Children.Remove(gone);
            }
        }

        _rows.Clear();
        _rows.AddRange(next);
        _targetHeight = lines.Count == 0 ? 0 : top + lines.Count * rowHeight;
        if (full && _height is not null)
        {
            _ = _height.AnimateTo(_targetHeight, MotionSpec.Spring(HeightSpring));
        }
        else
        {
            _height ??= CreateHeight();
            _height.Jump(_targetHeight);
        }
        InvalidateMeasure();
        InvalidateArrange();
    }

    private MotionValue<double> CreateHeight()
    {
        var value = new MotionValue<double>(this, _targetHeight, MotionVectors.Double);
        value.Changed += _ => InvalidateMeasure();
        return value;
    }

    private MotionValue<double> ShiftOf(UsagePricingFeatureRow row)
    {
        if (row.ShiftMotion is null)
        {
            var value = new MotionValue<double>(this, row.Shift.Y, MotionVectors.Double);
            value.Changed += v => row.Shift.Y = v;
            row.ShiftMotion = value;
        }
        return row.ShiftMotion;
    }

    private MotionValue<double> FadeOf(UsagePricingFeatureRow row)
    {
        if (row.FadeMotion is null)
        {
            var value = new MotionValue<double>(this, row.Opacity, MotionVectors.Double);
            value.Changed += v => row.Opacity = v;
            row.FadeMotion = value;
        }
        return row.FadeMotion;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 0.0 : availableSize.Width;
        var rowSize = new Size(availableSize.Width, RowHeight);
        foreach (var child in Children)
        {
            child.Measure(rowSize);
            if (double.IsInfinity(availableSize.Width))
            {
                width = Math.Max(width, child.DesiredSize.Width);
            }
        }
        return new Size(width, Math.Max(0, CurrentHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var child in Children)
        {
            if (child is UsagePricingFeatureRow row)
            {
                row.Arrange(new Rect(0, row.SlotY, finalSize.Width, RowHeight));
            }
        }
        return finalSize;
    }
}

/// <summary>One feature line: a check, the label and the value (a morph, so a changed value reshapes in place).</summary>
internal sealed class UsagePricingFeatureRow : Grid
{
    private readonly ArcGlyph _check;
    private readonly TextBlock _label;
    private readonly TextMorph _value;
    private IBrush? _strong;
    private IBrush? _soft;
    private bool _isNew;

    public UsagePricingFeatureRow(string key)
    {
        Key = key;
        Shift = new TranslateTransform();
        RenderTransform = Shift;
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        ColumnSpacing = 10;
        _check = new ArcGlyph
        {
            Data = ArcIcons.Check,
            Size = 16,
            StrokeThickness = 1.75,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAccessibilityView(_check, AccessibilityView.Raw);
        _label = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _value = new TextMorph
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        SetColumn(_label, 1);
        SetColumn(_value, 2);
        Children.Add(_check);
        Children.Add(_label);
        Children.Add(_value);
    }

    /// <summary>The feature key the row stands for.</summary>
    public string Key { get; }

    /// <summary>The row's slot (or, while it leaves, where it was last drawn).</summary>
    public double SlotY { get; set; }

    /// <summary>The render offset of the slide (changed in place).</summary>
    public TranslateTransform Shift { get; }

    public MotionValue<double>? ShiftMotion { get; set; }

    public MotionValue<double>? FadeMotion { get; set; }

    /// <summary>True once the line left the list (it is fading out).</summary>
    public bool IsLeaving { get; set; }

    /// <summary>Whether the line is new in the plan (drawn strong).</summary>
    public bool IsNew => _isNew;

    /// <summary>The label text (tests).</summary>
    public string? Label => _label.Text;

    /// <summary>The value text (tests).</summary>
    public string? Value => _value.Text;

    public void ApplyBrushes(IBrush? strong, IBrush? soft, IBrush? value)
    {
        _strong = strong;
        _soft = soft;
        _value.Foreground = value;
        ApplyNew();
    }

    public void SetLine(UsagePricingFeatureLine line, bool animate)
    {
        _label.Text = line.Label;
        _value.IsVisible = !string.IsNullOrEmpty(line.Value);
        _value.Text = line.Value ?? string.Empty;
        _isNew = line.IsNew;
        ApplyNew();
        AutomationProperties.SetName(this, string.IsNullOrEmpty(line.Value) ? line.Label : $"{line.Label}, {line.Value}");
    }

    private void ApplyNew()
    {
        var brush = _isNew ? _strong : _soft;
        _check.Foreground = brush;
        _label.Foreground = brush;
    }
}
