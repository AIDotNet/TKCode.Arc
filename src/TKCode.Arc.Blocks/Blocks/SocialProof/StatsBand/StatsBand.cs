// Ported from Arc's free stats-band block (MIT, Copyright (c) 2026 Elia Kuratli): stats-band.tsx and stats-band.module.css. The
// sample numbers are this library's own.

using System.Globalization;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A band of three or four headline numbers, each with an optional tiny picture that proves it: a trend, an uptime strip, a
/// latency histogram or a dotted world. The numbers count up in sequence the first time the band is in view and the pictures
/// then draw in; hovering or focusing a stat swaps its detail line for a line of context and gives its picture the accent.
/// With no <see cref="Stats"/> it shows sample numbers.
/// </summary>
/// <remarks>Ported from Arc's free stats-band block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
[TemplatePart(BodyPart, typeof(Border))]
[TemplatePart(GridPart, typeof(UniformGrid))]
[TemplatePart(RulesPart, typeof(StatsBandRules))]
[PseudoClasses(StackedPseudoClass, PhonePseudoClass)]
public class StatsBand : TemplatedControl
{
    public const string BodyPart = "PART_Body";
    public const string GridPart = "PART_Grid";
    public const string RulesPart = "PART_Rules";

    /// <summary>Set while the control is at most 800 wide: two columns, or one for a band of three.</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the control is at most 520 wide: one stat per row, with the picture beside the number.</summary>
    public const string PhonePseudoClass = ":phone";

    /// <summary>The stats; three or four read best. While the list is empty the band shows sample numbers.</summary>
    public static readonly DirectProperty<StatsBand, AvaloniaList<StatsBandStat>> StatsProperty =
        AvaloniaProperty.RegisterDirect<StatsBand, AvaloniaList<StatsBandStat>>(nameof(Stats), o => o.Stats);

    /// <summary>Plain floats the stats on whitespace; Divided sets them in a hairline grid ruled above and below.</summary>
    public static readonly StyledProperty<StatsBandLayout> LayoutProperty =
        AvaloniaProperty.Register<StatsBand, StatsBandLayout>(nameof(Layout));

    /// <summary>An optional heading above the band.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<StatsBand, string?>(nameof(Title));

    /// <summary>An optional line beside the heading.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<StatsBand, string?>(nameof(Description));

    /// <summary>The accessible name of the band when it has no title.</summary>
    public static readonly StyledProperty<string> AccessibleLabelProperty =
        AvaloniaProperty.Register<StatsBand, string>(nameof(AccessibleLabel), "Key numbers");

    /// <summary>How long each number takes to count up.</summary>
    public static readonly StyledProperty<TimeSpan> CountDurationProperty =
        AvaloniaProperty.Register<StatsBand, TimeSpan>(nameof(CountDuration), TimeSpan.FromSeconds(1.6));

    /// <summary>The culture name used to format numbers.</summary>
    public static readonly StyledProperty<string> LocaleProperty =
        AvaloniaProperty.Register<StatsBand, string>(nameof(Locale), "en-US");

    private readonly AvaloniaList<StatsBandStat> _stats = new();
    private readonly List<StatsBandItem> _items = new();
    private readonly LinearEasing _linear = new();
    private Border? _body;
    private UniformGrid? _grid;
    private StatsBandRules? _rules;
    private Grid? _header;
    private CtaBalancedText? _titleBlock;
    private CtaBalancedText? _descriptionBlock;
    private ViewportVisibility? _watch;
    private MotionValue<double>? _clock;
    private double _time = double.PositiveInfinity;
    private bool _revealed;

    public StatsBand()
    {
        _stats.CollectionChanged += (_, _) => Rebuild();
    }

    /// <inheritdoc cref="StatsProperty"/>
    public AvaloniaList<StatsBandStat> Stats => _stats;

    /// <inheritdoc cref="LayoutProperty"/>
    public StatsBandLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="AccessibleLabelProperty"/>
    public string AccessibleLabel { get => GetValue(AccessibleLabelProperty); set => SetValue(AccessibleLabelProperty, value); }

    /// <inheritdoc cref="CountDurationProperty"/>
    public TimeSpan CountDuration { get => GetValue(CountDurationProperty); set => SetValue(CountDurationProperty, value); }

    /// <inheritdoc cref="LocaleProperty"/>
    public string Locale { get => GetValue(LocaleProperty); set => SetValue(LocaleProperty, value); }

    internal IReadOnlyList<StatsBandItem> Items => _items;

    /// <summary>The entrance clock in ms; infinity once the entrance has finished.</summary>
    internal double Time => _time;

    internal int ColumnCount => _grid?.Columns ?? 0;

    /// <summary>Plays the entrance again, the way switching layouts does in Arc's preview.</summary>
    public void Replay()
    {
        _revealed = false;
        Arm();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _body = e.NameScope.Find<Border>(BodyPart);
        _grid = e.NameScope.Find<UniformGrid>(GridPart);
        _rules = e.NameScope.Find<StatsBandRules>(RulesPart);
        _header = e.NameScope.Find<Grid>("PART_Header");
        _titleBlock = e.NameScope.Find<CtaBalancedText>("PART_Title");
        _descriptionBlock = e.NameScope.Find<CtaBalancedText>("PART_Description");
        Rebuild();
        UpdateHeader();
        if (VisualRoot is not null)
        {
            Arm();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Arm();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _watch?.Dispose();
        _watch = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateWidthState();
        }
        else if (change.Property == TitleProperty || change.Property == DescriptionProperty || change.Property == AccessibleLabelProperty)
        {
            UpdateHeader();
        }
        else if (change.Property == LayoutProperty)
        {
            UpdateWidthState();
        }
        else if (change.Property == LocaleProperty)
        {
            Rebuild();
        }
    }

    private CultureInfo ResolveCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(Locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private void Rebuild()
    {
        if (_grid is null)
        {
            return;
        }
        _grid.Children.Clear();
        _items.Clear();
        var source = _stats.Count > 0 ? (IReadOnlyList<StatsBandStat>)_stats : StatsBandSamples.Create();
        var culture = ResolveCulture();
        var hasVisuals = source.Any(s => s.Visual is not null);
        for (var i = 0; i < source.Count; i++)
        {
            var item = new StatsBandItem(source[i], i, culture);
            item.TemplateApplied += (s, _) => ((StatsBandItem)s!).SetHasVisualRow(hasVisuals);
            _items.Add(item);
            _grid.Children.Add(item);
        }
        UpdateWidthState();
        ApplyTime();
    }

    private void UpdateHeader()
    {
        if (_titleBlock is null || _descriptionBlock is null || _header is null)
        {
            return;
        }
        var hasTitle = !string.IsNullOrEmpty(Title);
        var hasDescription = !string.IsNullOrEmpty(Description);
        _titleBlock.Text = Title;
        _titleBlock.IsVisible = hasTitle;
        _descriptionBlock.Text = Description;
        _descriptionBlock.IsVisible = hasDescription;
        _header.IsVisible = hasTitle || hasDescription;
        AutomationProperties.SetName(this, hasTitle ? Title : AccessibleLabel);
        UpdateWidthState();
    }

    private void UpdateWidthState()
    {
        var width = Bounds.Width;
        if (_grid is null || _body is null)
        {
            return;
        }
        var phone = width > 0 && width <= 520;
        var stacked = width > 0 && width <= 800;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        PseudoClasses.Set(PhonePseudoClass, phone);

        var count = Math.Clamp(_items.Count, 1, 4);
        var columns = phone ? 1 : stacked ? (count == 3 ? 1 : 2) : count;
        var divided = Layout == StatsBandLayout.Divided;
        _grid.Columns = columns;
        _grid.ColumnSpacing = divided ? 0 : stacked ? 32 : 40;
        _grid.RowSpacing = divided ? 0 : phone ? 40 : stacked ? 48 : 64;
        _body.Padding = phone ? new Thickness(16, 64) : stacked ? new Thickness(32, 80) : new Thickness(32, 96);
        if (_titleBlock is not null)
        {
            HeroText.SetTitleSize(_titleBlock, Math.Clamp(16 + 0.026 * width, 30, ArcTokens.Get<double>(this, ArcDesignTokenKind.Type4XL)));
        }
        var numberSize = Math.Clamp(22.4 + 0.03 * width, 40, ArcTokens.Get<double>(this, ArcDesignTokenKind.Type4XL));

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            item.NumberSize = numberSize;
            item.SetPhone(phone);
            if (!divided)
            {
                item.Padding = default;
                continue;
            }
            var column = i % columns;
            var left = column == 0 || columns == 1 ? 0 : 32;
            var right = column == columns - 1 || columns == 1 ? 0 : 32;
            item.Padding = stacked ? new Thickness(left, 32, right, 32) : new Thickness(left, 40, right, 32);
        }
        _rules?.Configure(_items, columns, divided);

        if (_header is not null)
        {
            _header.ColumnDefinitions = stacked ? new ColumnDefinitions("*") : new ColumnDefinitions("1.2*,*");
            _header.RowDefinitions = stacked ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
            _header.Margin = new Thickness(0, 0, 0, phone ? 40 : stacked ? 48 : 64);
            if (_descriptionBlock is not null)
            {
                var hasTitle = !string.IsNullOrEmpty(Title);
                Grid.SetColumn(_descriptionBlock, stacked || !hasTitle ? 0 : 1);
                Grid.SetRow(_descriptionBlock, stacked && hasTitle ? 1 : 0);
                Grid.SetColumnSpan(_descriptionBlock, !stacked && !hasTitle ? 2 : 1);
                _descriptionBlock.HorizontalAlignment = stacked || !hasTitle ? Avalonia.Layout.HorizontalAlignment.Left : Avalonia.Layout.HorizontalAlignment.Right;
            }
        }
    }

    private double DrawDelay => Math.Round(CountDuration.TotalSeconds * 420);

    /// <summary>
    /// Puts the band in its waiting state and watches for the first view; below full motion the band simply rests.
    /// The entrance is one clock in code because it is a staggered sequence that also writes digits and draws pictures,
    /// which a ControlTheme's transitions cannot express.
    /// </summary>
    private void Arm()
    {
        _watch?.Dispose();
        _watch = null;
        if (_revealed || !ArcMotion.IsFull(this))
        {
            _time = double.PositiveInfinity;
            ApplyTime();
            return;
        }
        _time = 0;
        ApplyTime();
        if (_grid is null)
        {
            return;
        }
        _watch = new ViewportVisibility(_grid, 0.4);
        _watch.VisibilityChanged += OnVisibilityChanged;
    }

    private void OnVisibilityChanged(object? sender, bool visible)
    {
        if (visible && !_revealed)
        {
            _revealed = true;
            Play();
        }
    }

    private async void Play()
    {
        if (!ArcMotion.IsFull(this))
        {
            _time = double.PositiveInfinity;
            ApplyTime();
            return;
        }
        var count = Math.Max(1, _items.Count);
        var lastStart = (count - 1) * StatsBandTiming.Stagger + DrawDelay;
        var total = Math.Max(
            lastStart + Math.Max(StatsBandTiming.Wipe, Math.Max(StatsBandTiming.BarSweep + StatsBandTiming.BarGrow, StatsBandTiming.DotDelay + StatsBandTiming.DotSpring)),
            (count - 1) * StatsBandTiming.Stagger + CountDuration.TotalMilliseconds);
        if (_clock is null)
        {
            _clock = new MotionValue<double>(this, 0, MotionVectors.Double);
            _clock.Changed += value =>
            {
                _time = value;
                ApplyTime();
            };
        }
        _clock.Jump(0);
        var finished = await _clock.AnimateTo(total, MotionSpec.Tween(TimeSpan.FromMilliseconds(total), _linear));
        if (finished)
        {
            _time = double.PositiveInfinity;
            ApplyTime();
        }
    }

    private void ApplyTime()
    {
        var count = CountDuration.TotalMilliseconds;
        var draw = DrawDelay;
        foreach (var item in _items)
        {
            item.SetTime(_time, count, draw);
        }
        if (_rules is not null)
        {
            _rules.Time = _time;
        }
    }
}
