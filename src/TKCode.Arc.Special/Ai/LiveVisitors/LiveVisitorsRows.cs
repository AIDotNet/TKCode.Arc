using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A fixed-height list of keyed rows for a <see cref="LiveVisitors"/> card (the top pages and the conversions ticker).
/// Rows are absolutely placed and moved with render transforms, so nothing reflows the card: when the order changes each
/// row slides to its new slot on a bounce-free 0.28s spring, a new row fades in over 160ms, and a row that drops out
/// slides one row down and fades out below the clip. Rows are kept per key, so a row whose key stays is updated in place.
/// Reduced motion keeps the fades and drops the slides; Off changes at once. While connecting it draws placeholder bars.
/// </summary>
internal sealed class LiveVisitorsRowList : Control
{
    /// <summary>The slide: a bounce-free spring that settles in about 0.28s, keeping its speed when interrupted.</summary>
    internal static readonly SpringSpec SlideSpring = SpringSpec.FromVisual(0.28, 0);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<LiveVisitorsRowList, double>(nameof(RowHeight), 32);

    public static readonly StyledProperty<int> CapacityProperty =
        AvaloniaProperty.Register<LiveVisitorsRowList, int>(nameof(Capacity), 4);

    public static readonly StyledProperty<bool> IsPlaceholderProperty =
        AvaloniaProperty.Register<LiveVisitorsRowList, bool>(nameof(IsPlaceholder));

    public static readonly StyledProperty<IBrush?> PlaceholderBrushProperty =
        AvaloniaProperty.Register<LiveVisitorsRowList, IBrush?>(nameof(PlaceholderBrush));

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<LiveVisitorsRowList, double>(nameof(BarHeight), 28);

    public static readonly StyledProperty<CornerRadius> BarCornerRadiusProperty =
        AvaloniaProperty.Register<LiveVisitorsRowList, CornerRadius>(nameof(BarCornerRadius), new CornerRadius(6));

    public static readonly StyledProperty<string?> EmptyTextProperty =
        AvaloniaProperty.Register<LiveVisitorsRowList, string?>(nameof(EmptyText));

    public static readonly StyledProperty<IBrush?> EmptyForegroundProperty =
        AvaloniaProperty.Register<LiveVisitorsRowList, IBrush?>(nameof(EmptyForeground));

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<LiveVisitorsRowList>();

    private static readonly double[] s_placeholderWidths = [0.92, 0.74, 0.58, 0.46, 0.38, 0.3];

    private readonly List<Slot> _slots = [];
    private readonly TextBlock _empty;

    static LiveVisitorsRowList()
    {
        AffectsMeasure<LiveVisitorsRowList>(RowHeightProperty, CapacityProperty);
        AffectsRender<LiveVisitorsRowList>(IsPlaceholderProperty, PlaceholderBrushProperty, BarHeightProperty, BarCornerRadiusProperty);
    }

    public LiveVisitorsRowList()
    {
        ClipToBounds = true;
        _empty = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsVisible = false,
        };
        VisualChildren.Add(_empty);
        LogicalChildren.Add(_empty);
    }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public int Capacity { get => GetValue(CapacityProperty); set => SetValue(CapacityProperty, value); }

    public bool IsPlaceholder { get => GetValue(IsPlaceholderProperty); set => SetValue(IsPlaceholderProperty, value); }

    public IBrush? PlaceholderBrush { get => GetValue(PlaceholderBrushProperty); set => SetValue(PlaceholderBrushProperty, value); }

    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    public CornerRadius BarCornerRadius { get => GetValue(BarCornerRadiusProperty); set => SetValue(BarCornerRadiusProperty, value); }

    public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

    public IBrush? EmptyForeground { get => GetValue(EmptyForegroundProperty); set => SetValue(EmptyForegroundProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>The rows in their slots, top first (leaving rows excluded).</summary>
    internal IReadOnlyList<Control> Rows => _slots.Where(s => !s.Leaving).OrderBy(s => s.Index).Select(s => s.View).ToArray();

    /// <summary>The keys of the rows in their slots, top first.</summary>
    internal IReadOnlyList<string> Keys => _slots.Where(s => !s.Leaving).OrderBy(s => s.Index).Select(s => s.Key).ToArray();

    /// <summary>The keys of rows that are sliding out.</summary>
    internal IReadOnlyList<string> LeavingKeys => _slots.Where(s => s.Leaving).Select(s => s.Key).ToArray();

    /// <summary>The row of <paramref name="key"/>, leaving or not.</summary>
    internal Control? RowOf(string key) => _slots.FirstOrDefault(s => s.Key == key)?.View;

    /// <summary>The vertical offset a row is drawn at (tests: the slide).</summary>
    internal double OffsetOf(string key) => _slots.FirstOrDefault(s => s.Key == key)?.Transform.Y ?? double.NaN;

    /// <summary>
    /// Brings the rows in line with <paramref name="keys"/> (top first, at most <see cref="Capacity"/>). A missing row is
    /// made by <paramref name="create"/>; every row on screen is passed to <paramref name="update"/> with its slot.
    /// <paramref name="instant"/> places everything at rest (the first frame, or the end of a pause).
    /// </summary>
    public void Sync(IReadOnlyList<string> keys, Func<string, Control> create, Action<Control, int> update, bool instant)
    {
        var rowHeight = RowHeight;
        var animate = !instant && IsLoaded;
        var wanted = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < keys.Count && wanted.Count < Math.Max(0, Capacity); i++)
        {
            wanted.TryAdd(keys[i], wanted.Count);
        }
        // Rows that drop out slide one row down and fade out below the clip.
        foreach (var slot in _slots.ToArray())
        {
            if (wanted.ContainsKey(slot.Key) || slot.Leaving)
            {
                continue;
            }
            if (!animate)
            {
                Remove(slot);
                continue;
            }
            slot.Leaving = true;
            slot.View.IsHitTestVisible = false;
            _ = slot.Y.AnimateTo((slot.Index + 1) * rowHeight, MotionSpec.Spring(SlideSpring));
            _ = FadeOutAsync(slot);
        }
        foreach (var (key, index) in wanted)
        {
            var slot = _slots.FirstOrDefault(s => s.Key == key);
            if (slot is not null && slot.Leaving)
            {
                // A row that comes back while it leaves turns round from where it is.
                slot.Leaving = false;
                slot.View.IsHitTestVisible = true;
            }
            if (slot is null)
            {
                var view = create(key);
                slot = new Slot(key, view, new MotionValue<double>(this, index * rowHeight, MotionVectors.Double),
                    new MotionValue<double>(this, animate ? 0 : 1, MotionVectors.Double));
                var s = slot;
                slot.Y.Changed += y => s.Transform.Y = y;
                slot.Opacity.Changed += o => s.View.Opacity = o;
                view.RenderTransform = slot.Transform;
                view.Opacity = slot.Opacity.Value;
                slot.Transform.Y = index * rowHeight;
                _slots.Add(slot);
                VisualChildren.Add(view);
                LogicalChildren.Add(view);
            }
            slot.Index = index;
            update(slot.View, index);
            var target = index * rowHeight;
            if (animate)
            {
                if (Math.Abs(slot.Y.Value - target) > 0.01 || slot.Y.IsAnimating)
                {
                    _ = slot.Y.AnimateTo(target, MotionSpec.Spring(SlideSpring));
                }
                if (slot.Opacity.Value < 1 || slot.Opacity.IsAnimating)
                {
                    _ = slot.Opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
                }
            }
            else
            {
                slot.Y.Jump(target);
                slot.Transform.Y = target;
                slot.Opacity.Jump(1);
                slot.View.Opacity = 1;
            }
        }
        UpdateEmpty();
        InvalidateMeasure();
        InvalidateArrange();
    }

    private async Task FadeOutAsync(Slot slot)
    {
        var done = await slot.Opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        if (done && slot.Leaving)
        {
            Remove(slot);
        }
    }

    private void Remove(Slot slot)
    {
        slot.Y.Stop();
        slot.Opacity.Stop();
        _slots.Remove(slot);
        VisualChildren.Remove(slot.View);
        LogicalChildren.Remove(slot.View);
        UpdateEmpty();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPlaceholderProperty || change.Property == EmptyTextProperty)
        {
            UpdateEmpty();
        }
        else if (change.Property == EmptyForegroundProperty)
        {
            _empty.Foreground = EmptyForeground;
        }
        else if (change.Property == FontSizeProperty)
        {
            _empty.FontSize = FontSize;
        }
    }

    private void UpdateEmpty()
    {
        var placeholder = IsPlaceholder;
        foreach (var slot in _slots)
        {
            slot.View.IsVisible = !placeholder;
        }
        _empty.Text = EmptyText;
        _empty.IsVisible = !placeholder && !_slots.Any(s => !s.Leaving) && !string.IsNullOrEmpty(EmptyText);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 280;
        var rowHeight = RowHeight;
        foreach (var slot in _slots)
        {
            slot.View.Measure(new Size(width, rowHeight));
        }
        _empty.Measure(new Size(width, rowHeight));
        return new Size(width, Math.Max(1, Capacity) * rowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rowHeight = RowHeight;
        foreach (var slot in _slots)
        {
            slot.View.Arrange(new Rect(0, 0, finalSize.Width, rowHeight));
        }
        _empty.Arrange(new Rect(0, 0, finalSize.Width, rowHeight));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (!IsPlaceholder || PlaceholderBrush is not { } fill)
        {
            return;
        }
        var rowHeight = RowHeight;
        var bar = Math.Min(BarHeight, rowHeight);
        for (var i = 0; i < Math.Max(1, Capacity); i++)
        {
            var width = Bounds.Width * s_placeholderWidths[Math.Min(i, s_placeholderWidths.Length - 1)];
            var rect = new Rect(0, i * rowHeight + (rowHeight - bar) / 2, width, bar);
            context.DrawRectangle(fill, null, new RoundedRect(rect, BarCornerRadius));
        }
    }

    private sealed class Slot(string key, Control view, MotionValue<double> y, MotionValue<double> opacity)
    {
        public string Key { get; } = key;

        public Control View { get; } = view;

        public MotionValue<double> Y { get; } = y;

        public MotionValue<double> Opacity { get; } = opacity;

        public TranslateTransform Transform { get; } = new();

        public int Index { get; set; }

        public bool Leaving { get; set; }
    }
}

/// <summary>
/// A page row of <see cref="LiveVisitors"/>: a bar from the left scaled to the busiest page (it eases to its new width over
/// 280ms), the path over it (ellipsis when long) and the people on it in a fixed right-aligned column of tabular digits.
/// The bar is drawn here, so its width changes never reflow the row.
/// </summary>
internal sealed class LiveVisitorsPageRow : Control
{
    /// <summary>The bar's ease to a new width.</summary>
    internal static readonly TimeSpan BarDuration = TimeSpan.FromMilliseconds(280);

    internal static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<LiveVisitorsPageRow, IBrush?>(nameof(BarBrush));

    public static readonly StyledProperty<double> BarHeightProperty =
        AvaloniaProperty.Register<LiveVisitorsPageRow, double>(nameof(BarHeight), 28);

    public static readonly StyledProperty<CornerRadius> BarCornerRadiusProperty =
        AvaloniaProperty.Register<LiveVisitorsPageRow, CornerRadius>(nameof(BarCornerRadius), new CornerRadius(6));

    public static readonly StyledProperty<double> InsetProperty =
        AvaloniaProperty.Register<LiveVisitorsPageRow, double>(nameof(Inset), 10);

    public static readonly StyledProperty<double> CountColumnWidthProperty =
        AvaloniaProperty.Register<LiveVisitorsPageRow, double>(nameof(CountColumnWidth), 44);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LiveVisitorsPageRow>();

    public static readonly StyledProperty<IBrush?> CountForegroundProperty =
        AvaloniaProperty.Register<LiveVisitorsPageRow, IBrush?>(nameof(CountForeground));

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<LiveVisitorsPageRow>();

    private readonly TextBlock _path;
    private readonly TextBlock _count;
    private MotionValue<double>? _fraction;
    private double _shown;

    static LiveVisitorsPageRow()
    {
        AffectsRender<LiveVisitorsPageRow>(BarBrushProperty, BarHeightProperty, BarCornerRadiusProperty);
        AffectsMeasure<LiveVisitorsPageRow>(InsetProperty, CountColumnWidthProperty);
    }

    public LiveVisitorsPageRow()
    {
        _path = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        _count = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            FontFeatures = TabularNumbers,
        };
        VisualChildren.Add(_path);
        VisualChildren.Add(_count);
        LogicalChildren.Add(_path);
        LogicalChildren.Add(_count);
    }

    public IBrush? BarBrush { get => GetValue(BarBrushProperty); set => SetValue(BarBrushProperty, value); }

    public double BarHeight { get => GetValue(BarHeightProperty); set => SetValue(BarHeightProperty, value); }

    public CornerRadius BarCornerRadius { get => GetValue(BarCornerRadiusProperty); set => SetValue(BarCornerRadiusProperty, value); }

    public double Inset { get => GetValue(InsetProperty); set => SetValue(InsetProperty, value); }

    public double CountColumnWidth { get => GetValue(CountColumnWidthProperty); set => SetValue(CountColumnWidthProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? CountForeground { get => GetValue(CountForegroundProperty); set => SetValue(CountForegroundProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public string? Path => _path.Text;

    public string? CountText => _count.Text;

    /// <summary>The bar's share of the row as drawn now (0–1).</summary>
    public double BarFraction => _shown;

    /// <summary>Shows a page: its path, its count and the bar's share; <paramref name="animate"/> eases the bar.</summary>
    public void Show(string path, int visitors, double fraction, bool animate)
    {
        if (_path.Text != path)
        {
            _path.Text = path;
        }
        var count = visitors.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        if (_count.Text != count)
        {
            _count.Text = count;
        }
        fraction = Math.Clamp(double.IsFinite(fraction) ? fraction : 0, 0, 1);
        _fraction ??= CreateFraction();
        if (animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            if (Math.Abs(_fraction.Value - fraction) > 0.0005)
            {
                _ = _fraction.AnimateTo(fraction, MotionSpec.Tween(BarDuration, ArcEasings.Standard));
            }
        }
        else
        {
            _fraction.Jump(fraction);
            _shown = fraction;
            InvalidateVisual();
        }
    }

    private MotionValue<double> CreateFraction()
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            _shown = v;
            InvalidateVisual();
        };
        return value;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _path.Foreground = Foreground;
        }
        else if (change.Property == CountForegroundProperty)
        {
            _count.Foreground = CountForeground;
        }
        else if (change.Property == FontSizeProperty)
        {
            _path.FontSize = FontSize;
            _count.FontSize = FontSize;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 280;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 32;
        _path.Measure(new Size(Math.Max(0, width - Inset * 2 - CountColumnWidth), height));
        _count.Measure(new Size(CountColumnWidth, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var pathWidth = Math.Max(0, finalSize.Width - Inset * 2 - CountColumnWidth);
        _path.Arrange(new Rect(Inset, 0, pathWidth, finalSize.Height));
        _count.Arrange(new Rect(Math.Max(0, finalSize.Width - Inset - CountColumnWidth), 0, CountColumnWidth, finalSize.Height));
        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        if (BarBrush is not { } brush || _shown <= 0)
        {
            return;
        }
        var height = Math.Min(BarHeight, Bounds.Height);
        var width = Math.Max(height / 2, Bounds.Width * _shown);
        context.DrawRectangle(brush, null, new RoundedRect(new Rect(0, (Bounds.Height - height) / 2, width, height), BarCornerRadius));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LiveVisitorsRowPeer(this);
}

/// <summary>
/// An event row of <see cref="LiveVisitors"/>: the country code in a small chip (or the app's own image), what happened
/// and the city (ellipsis when long), and how long ago in a fixed right-aligned column. Screen readers read it as one
/// sentence with the country spelled out; the chip is decorative.
/// </summary>
internal sealed class LiveVisitorsEventRow : Control
{
    public static readonly StyledProperty<IBrush?> ChipBrushProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, IBrush?>(nameof(ChipBrush));

    public static readonly StyledProperty<IBrush?> ChipForegroundProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, IBrush?>(nameof(ChipForeground));

    public static readonly StyledProperty<double> ChipWidthProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, double>(nameof(ChipWidth), 24);

    public static readonly StyledProperty<double> ChipHeightProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, double>(nameof(ChipHeight), 16);

    public static readonly StyledProperty<CornerRadius> ChipCornerRadiusProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, CornerRadius>(nameof(ChipCornerRadius), new CornerRadius(4));

    public static readonly StyledProperty<double> ChipFontSizeProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, double>(nameof(ChipFontSize), 10);

    public static readonly StyledProperty<FontFamily> ChipFontFamilyProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, FontFamily>(nameof(ChipFontFamily), FontFamily.Default);

    public static readonly StyledProperty<double> TimeColumnWidthProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, double>(nameof(TimeColumnWidth), 36);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, double>(nameof(Gap), 10);

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<LiveVisitorsEventRow>();

    public static readonly StyledProperty<IBrush?> SecondaryForegroundProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, IBrush?>(nameof(SecondaryForeground));

    public static readonly StyledProperty<IBrush?> TimeForegroundProperty =
        AvaloniaProperty.Register<LiveVisitorsEventRow, IBrush?>(nameof(TimeForeground));

    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<LiveVisitorsEventRow>();

    private readonly TextBlock _text;
    private readonly Run _label = new() { FontWeight = FontWeight.Medium };
    private readonly Run _separator = new(" · ");
    private readonly Run _city = new();
    private readonly TextBlock _time;
    private readonly Image _flag;
    private TextLayout? _code;
    private string _codeText = string.Empty;

    static LiveVisitorsEventRow()
    {
        AffectsRender<LiveVisitorsEventRow>(ChipBrushProperty, ChipCornerRadiusProperty);
        AffectsMeasure<LiveVisitorsEventRow>(ChipWidthProperty, ChipHeightProperty, TimeColumnWidthProperty, GapProperty);
    }

    public LiveVisitorsEventRow()
    {
        _text = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        _text.Inlines!.Add(_label);
        _text.Inlines.Add(_separator);
        _text.Inlines.Add(_city);
        _time = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Right,
            FontFeatures = LiveVisitorsPageRow.TabularNumbers,
        };
        _flag = new Image { Stretch = Stretch.UniformToFill, IsVisible = false };
        VisualChildren.Add(_flag);
        VisualChildren.Add(_text);
        VisualChildren.Add(_time);
        LogicalChildren.Add(_flag);
        LogicalChildren.Add(_text);
        LogicalChildren.Add(_time);
    }

    public IBrush? ChipBrush { get => GetValue(ChipBrushProperty); set => SetValue(ChipBrushProperty, value); }

    public IBrush? ChipForeground { get => GetValue(ChipForegroundProperty); set => SetValue(ChipForegroundProperty, value); }

    public double ChipWidth { get => GetValue(ChipWidthProperty); set => SetValue(ChipWidthProperty, value); }

    public double ChipHeight { get => GetValue(ChipHeightProperty); set => SetValue(ChipHeightProperty, value); }

    public CornerRadius ChipCornerRadius { get => GetValue(ChipCornerRadiusProperty); set => SetValue(ChipCornerRadiusProperty, value); }

    public double ChipFontSize { get => GetValue(ChipFontSizeProperty); set => SetValue(ChipFontSizeProperty, value); }

    public FontFamily ChipFontFamily { get => GetValue(ChipFontFamilyProperty); set => SetValue(ChipFontFamilyProperty, value); }

    public double TimeColumnWidth { get => GetValue(TimeColumnWidthProperty); set => SetValue(TimeColumnWidthProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public IBrush? SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }

    public IBrush? TimeForeground { get => GetValue(TimeForegroundProperty); set => SetValue(TimeForegroundProperty, value); }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    public string Label => _label.Text ?? string.Empty;

    public string City => _city.Text ?? string.Empty;

    public string Code => _codeText;

    public string? TimeText => _time.Text;

    /// <summary>Shows an event: its label, city, country code (or image) and the short time.</summary>
    public void Show(string label, string city, string code, IImage? flag, string time)
    {
        if (_label.Text != label)
        {
            _label.Text = label;
        }
        if (_city.Text != city)
        {
            _city.Text = city;
        }
        var hasCity = !string.IsNullOrEmpty(city);
        _separator.Text = hasCity ? " · " : string.Empty;
        _flag.Source = flag;
        _flag.IsVisible = flag is not null;
        code = (code ?? string.Empty).ToUpperInvariant();
        if (code != _codeText)
        {
            _codeText = code;
            RebuildCode();
        }
        SetTime(time);
    }

    /// <summary>Updates only the time column (the ticker's clock).</summary>
    public void SetTime(string time)
    {
        if (_time.Text != time)
        {
            _time.Text = time;
        }
    }

    private void RebuildCode()
    {
        _code?.Dispose();
        _code = _codeText.Length == 0
            ? null
            : new TextLayout(_codeText, new Typeface(ChipFontFamily, FontStyle.Normal, FontWeight.SemiBold), ChipFontSize, ChipForeground);
        InvalidateVisual();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty)
        {
            _label.Foreground = Foreground;
        }
        else if (change.Property == SecondaryForegroundProperty)
        {
            _separator.Foreground = SecondaryForeground;
            _city.Foreground = SecondaryForeground;
        }
        else if (change.Property == TimeForegroundProperty)
        {
            _time.Foreground = TimeForeground;
        }
        else if (change.Property == FontSizeProperty)
        {
            _text.FontSize = FontSize;
            _time.FontSize = FontSize;
        }
        else if (change.Property == ChipForegroundProperty || change.Property == ChipFontSizeProperty || change.Property == ChipFontFamilyProperty)
        {
            RebuildCode();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 280;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : 32;
        _flag.Measure(new Size(ChipWidth, ChipHeight));
        _text.Measure(new Size(Math.Max(0, width - ChipWidth - Gap * 2 - TimeColumnWidth), height));
        _time.Measure(new Size(TimeColumnWidth, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _flag.Arrange(ChipRect(finalSize));
        var textX = ChipWidth + Gap;
        _text.Arrange(new Rect(textX, 0, Math.Max(0, finalSize.Width - textX - Gap - TimeColumnWidth), finalSize.Height));
        _time.Arrange(new Rect(Math.Max(0, finalSize.Width - TimeColumnWidth), 0, TimeColumnWidth, finalSize.Height));
        return finalSize;
    }

    private Rect ChipRect(Size size) => new(0, (size.Height - ChipHeight) / 2, ChipWidth, ChipHeight);

    public override void Render(DrawingContext context)
    {
        if (_flag.IsVisible)
        {
            return;
        }
        var chip = ChipRect(Bounds.Size);
        if (ChipBrush is { } brush)
        {
            context.DrawRectangle(brush, null, new RoundedRect(chip, ChipCornerRadius));
        }
        if (_code is { } code)
        {
            code.Draw(context, new Point(chip.X + (chip.Width - code.Width) / 2, chip.Y + (chip.Height - code.Height) / 2));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new LiveVisitorsRowPeer(this);
}

/// <summary>A list row read as one item by its name; its parts are not read on their own.</summary>
internal sealed class LiveVisitorsRowPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
