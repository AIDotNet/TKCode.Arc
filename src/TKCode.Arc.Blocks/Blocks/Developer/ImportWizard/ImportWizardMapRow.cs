using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>What a column's confidence cell shows.</summary>
internal enum ImportMapResult
{
    /// <summary>"Matching", pulsing, before the column resolved.</summary>
    Matching,

    /// <summary>The matcher's percent with its meter.</summary>
    Score,

    /// <summary>A field the person chose that the matcher did not propose (full meter, success color).</summary>
    Chosen,

    /// <summary>"Don't import".</summary>
    Ignored,

    /// <summary>The field is chosen for another column too.</summary>
    Duplicate
}

/// <summary>
/// The 28×4 confidence meter: a pill track in the muted surface color with a fill in the level color that grows from the
/// left. The fill follows its value on the smooth spring (a spring-shaped tween with reduced motion, where a
/// <see cref="MotionValue{T}"/> spring would jump), so a resolving column fills 0 → value.
/// </summary>
internal sealed class ImportWizardMeter : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ImportWizardMeter, IBrush?>(nameof(Foreground));

    private static readonly ArcSpringEasing s_smooth = new(ImportWizardMotion.Smooth);
    private MotionValue<double>? _fill;
    private double _value;
    private IBrush? _track;
    private Color _trackColor;

    static ImportWizardMeter()
    {
        AffectsRender<ImportWizardMeter>(ForegroundProperty);
    }

    public ImportWizardMeter()
    {
        Width = 28;
        Height = 4;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    /// <summary>The fill color.</summary>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>The drawn fill, 0–1.</summary>
    public double Fill => _value;

    /// <summary>The fill the meter is heading to, 0–1.</summary>
    public double Target { get; private set; }

    /// <summary>Fills to <paramref name="target"/> from <paramref name="from"/> (null: from where it is).</summary>
    public void SetFill(double target, bool animate, double? from = null)
    {
        Target = target;
        if (from is { } start)
        {
            _value = start;
            _fill?.Jump(start);
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off)
        {
            _value = target;
            _fill?.Jump(target);
            InvalidateVisual();
            return;
        }
        if (_fill is null)
        {
            _fill = new MotionValue<double>(this, _value, MotionVectors.Double);
            _fill.Changed += v =>
            {
                _value = v;
                InvalidateVisual();
            };
        }
        var spec = level == ArcMotionLevel.Full
            ? MotionSpec.Spring(ImportWizardMotion.Smooth)
            : MotionSpec.Tween(s_smooth.SettleDuration, s_smooth);
        _ = _fill.AnimateTo(target, spec);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = bounds.Height / 2;
        var track = ArcTokens.GetColor(this, ArcDesignTokenKind.SurfaceMuted);
        if (_track is null || track != _trackColor)
        {
            _trackColor = track;
            _track = new ImmutableSolidColorBrush(track);
        }
        context.DrawRectangle(_track, null, bounds, radius, radius);
        if (_value > 0.0005 && Foreground is { } fill)
        {
            using (context.PushClip(new RoundedRect(bounds, radius)))
            {
                context.DrawRectangle(fill, null, new Rect(0, 0, bounds.Width * Math.Clamp(_value, 0, 1), bounds.Height), radius, radius);
            }
        }
    }
}

/// <summary>
/// One shown result of a confidence cell: <c>[meter] 7 [percent] 7 [%]</c>, or <c>[meter] 7 Chosen</c>, "Ignored" or
/// "Duplicate", right aligned, 12 / 16.8. It enters with opacity 0 → 1 and a 3px blur clearing on the quick spring
/// ("Ignored" fades in over 300ms instead) and leaves with a 300ms fade.
/// </summary>
internal sealed class ImportWizardScoreLayer : StackPanel
{
    private static readonly ArcSpringEasing s_quick = new(ImportWizardMotion.Quick);
    private MotionValue<double>? _presence;
    private bool _leaving;
    private bool _blur;

    public ImportWizardScoreLayer(ImportMapResult result, int percent, ImportWizardLabels labels)
    {
        Result = result;
        Percent = percent;
        Orientation = Orientation.Horizontal;
        Spacing = 7;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        switch (result)
        {
            case ImportMapResult.Score:
            {
                var level = ImportWizardMatcher.LevelOf(percent);
                Meter = new ImportWizardMeter();
                Children.Add(Meter);
                var number = Label(percent.ToString(System.Globalization.CultureInfo.CurrentCulture), FontWeight.Medium);
                number.FontFeatures = ImportWizardUi.TabularNumbers;
                Children.Add(number);
                Children.Add(Label("%", FontWeight.Medium));
                Paint(level);
                break;
            }
            case ImportMapResult.Chosen:
                Meter = new ImportWizardMeter();
                Children.Add(Meter);
                Children.Add(Label(labels.Chosen, FontWeight.Medium));
                Paint(ImportConfidenceLevel.Exact);
                break;
            case ImportMapResult.Ignored:
                Children.Add(Label(labels.Ignored, FontWeight.Normal));
                PaintText(ArcDesignTokenKind.TextMuted);
                break;
            case ImportMapResult.Duplicate:
                Children.Add(Label(labels.Duplicate, FontWeight.Medium));
                PaintText(ArcDesignTokenKind.Danger);
                break;
        }
    }

    /// <summary>What the layer shows.</summary>
    public ImportMapResult Result { get; }

    /// <summary>The percent of a score.</summary>
    public int Percent { get; }

    /// <summary>The meter of a score or a chosen field; null otherwise.</summary>
    public ImportWizardMeter? Meter { get; }

    /// <summary>The meter's fill when it is full.</summary>
    public double MeterTarget => Result == ImportMapResult.Chosen ? 1 : Percent / 100.0;

    private static TextBlock Label(string text, FontWeight weight)
    {
        var block = ImportWizardUi.Text(text, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, weight, 16.8);
        block.VerticalAlignment = VerticalAlignment.Center;
        return block;
    }

    private void Paint(ImportConfidenceLevel level)
    {
        foreach (var child in Children)
        {
            switch (level)
            {
                case ImportConfidenceLevel.Likely:
                    ImportWizardUi.Bind(child, child is ImportWizardMeter ? ImportWizardMeter.ForegroundProperty : TextBlock.ForegroundProperty,
                        ImportWizardTokenKind.LikelyColor);
                    break;
                default:
                    ArcTokens.Bind(child, child is ImportWizardMeter ? ImportWizardMeter.ForegroundProperty : TextBlock.ForegroundProperty,
                        level == ImportConfidenceLevel.Exact ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Warning);
                    break;
            }
        }
    }

    private void PaintText(ArcDesignTokenKind color)
    {
        foreach (var child in Children)
        {
            ArcTokens.Bind(child, TextBlock.ForegroundProperty, color);
        }
    }

    /// <summary>Shows the layer; the meter fills from <paramref name="meterFrom"/>.</summary>
    public void Enter(bool animate, double meterFrom)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        animate = animate && level != ArcMotionLevel.Off;
        Meter?.SetFill(MeterTarget, animate, meterFrom);
        if (!animate)
        {
            Opacity = 1;
            Effect = null;
            return;
        }
        // "Ignored" fades in like "Matching" fades out; the others clear a 3px blur on the quick spring (Arc keeps the
        // blur with reduced motion too, running the spring's curve as a tween there).
        _blur = Result != ImportMapResult.Ignored;
        var spec = Result == ImportMapResult.Ignored
            ? MotionSpec.Tween(ImportWizardMotion.FadeOutDuration, ImportWizardMotion.FadeOut)
            : level == ArcMotionLevel.Full
                ? MotionSpec.Spring(ImportWizardMotion.Quick)
                : MotionSpec.Tween(s_quick.SettleDuration, s_quick);
        Presence(0).Jump(0);
        _ = Presence(0).AnimateTo(1, spec);
    }

    /// <summary>True once the layer started leaving.</summary>
    public bool IsLeaving => _leaving;

    /// <summary>Fades the layer out over 300ms and then calls <paramref name="done"/>.</summary>
    public async Task LeaveAsync(bool animate, Action<ImportWizardScoreLayer> done)
    {
        _leaving = true;
        Effect = null;
        if (animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off)
        {
            await Presence(Opacity).AnimateTo(0, MotionSpec.Tween(ImportWizardMotion.FadeOutDuration, ImportWizardMotion.FadeOut));
        }
        done(this);
    }

    private MotionValue<double> Presence(double initial)
    {
        if (_presence is null)
        {
            _presence = new MotionValue<double>(this, initial, MotionVectors.Double);
            _presence.Changed += v =>
            {
                Opacity = Math.Clamp(v, 0, 1);
                Effect = _blur && !_leaving ? ArcBlur.Get(3 * (1 - Math.Clamp(v, 0, 1))) : null;
            };
        }
        return _presence;
    }
}

/// <summary>
/// The confidence cell of a mapping row (92 wide, 84 compact; content right aligned): "Matching" pulsing until the column
/// resolves, then the result. On resolve "Matching" fades out over 300ms while the result blurs in; a later change of
/// the result (another field chosen) cross-fades the same way and the meter moves on from where it was.
/// </summary>
internal sealed class ImportWizardConfidence : Panel
{
    private readonly TextBlock _matching;
    private MotionValue<double>? _matchingFade;
    private double _fade = 1;
    private double _pulse = 1;
    private ImportWizardScoreLayer? _shown;
    private ImportWizardLabels _labels;

    public ImportWizardConfidence(ImportWizardLabels labels)
    {
        _labels = labels;
        VerticalAlignment = VerticalAlignment.Center;
        _matching = ImportWizardUi.Text(labels.Matching, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, FontWeight.Medium, 16.8);
        _matching.HorizontalAlignment = HorizontalAlignment.Right;
        _matching.VerticalAlignment = VerticalAlignment.Center;
        _matching.IsHitTestVisible = false;
        Children.Add(_matching);
        AutomationProperties.SetName(this, labels.Matching);
    }

    /// <summary>What the cell shows (or is heading to).</summary>
    public ImportMapResult Result { get; private set; } = ImportMapResult.Matching;

    /// <summary>The shown percent of a score (0 otherwise).</summary>
    public int Percent { get; private set; }

    /// <summary>The shown result layer (tests); null while matching.</summary>
    public ImportWizardScoreLayer? Shown => _shown;

    /// <summary>True while "Matching" can be seen (pulsing or fading out).</summary>
    public bool IsMatchingVisible => _matching.IsVisible;

    /// <summary>The "Matching" label (tests).</summary>
    public TextBlock MatchingLabel => _matching;

    /// <summary>Sets the pulse of "Matching" (1 → 0.4 → 1 every second, from the view's loop).</summary>
    public void SetPulse(double opacity)
    {
        _pulse = opacity;
        ApplyMatching();
    }

    private void ApplyMatching() => _matching.Opacity = _pulse * _fade;

    /// <summary>Shows <paramref name="result"/>; the first result after "Matching" fades it out.</summary>
    public void Show(ImportMapResult result, int percent, bool animate)
    {
        percent = result == ImportMapResult.Score ? percent : 0;
        if (result == Result && percent == Percent && (_shown is not null || result == ImportMapResult.Matching))
        {
            return;
        }
        var previous = _shown;
        Result = result;
        Percent = percent;
        UpdateName();
        if (result == ImportMapResult.Matching)
        {
            return;
        }
        // The meter carries on from what was drawn (Score ↔ Chosen) or fills from empty.
        var meterFrom = previous?.Meter?.Fill ?? 0;
        if (previous is not null)
        {
            _ = previous.LeaveAsync(animate, RemoveLayer);
        }
        else if (_matching.IsVisible)
        {
            HideMatching(animate);
        }
        var layer = new ImportWizardScoreLayer(result, Percent, _labels);
        _shown = layer;
        Children.Add(layer);
        layer.Enter(animate, meterFrom);
    }

    private void RemoveLayer(ImportWizardScoreLayer layer)
    {
        if (layer != _shown)
        {
            Children.Remove(layer);
        }
    }

    private void HideMatching(bool animate)
    {
        if (!animate || ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Off)
        {
            _fade = 0;
            _matchingFade?.Jump(0);
            _matching.IsVisible = false;
            return;
        }
        if (_matchingFade is null)
        {
            _matchingFade = new MotionValue<double>(this, _fade, MotionVectors.Double);
            _matchingFade.Changed += v =>
            {
                _fade = v;
                ApplyMatching();
                if (v <= 0)
                {
                    _matching.IsVisible = false;
                }
            };
        }
        _ = _matchingFade.AnimateTo(0, MotionSpec.Tween(ImportWizardMotion.FadeOutDuration, ImportWizardMotion.FadeOut));
    }

    /// <summary>Re-reads the words (a new layer is built in place, without motion).</summary>
    public void SetLabels(ImportWizardLabels labels)
    {
        _labels = labels;
        _matching.Text = labels.Matching;
        if (_shown is { } shown)
        {
            var fill = shown.Meter?.Fill ?? 0;
            Children.Remove(shown);
            _shown = new ImportWizardScoreLayer(Result, Percent, labels);
            Children.Add(_shown);
            _shown.Enter(false, fill);
        }
        UpdateName();
    }

    private void UpdateName()
    {
        var name = Result switch
        {
            ImportMapResult.Score => _labels.ConfidenceName(Percent),
            ImportMapResult.Chosen => _labels.Chosen,
            ImportMapResult.Ignored => _labels.Ignored,
            ImportMapResult.Duplicate => _labels.Duplicate,
            _ => _labels.Matching,
        };
        AutomationProperties.SetName(this, name);
    }
}

/// <summary>
/// The rows of the mapping list, stacked like a StackPanel but on their unrounded heights: every boundary is rounded on
/// its own, so rows are 57 or 58 tall in turn and land where Arc's 57.4 rows do (instead of drifting by 0.6 a row).
/// </summary>
internal sealed class ImportWizardMapList : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var child in Children)
        {
            child.Measure(availableSize.WithHeight(double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += Exact(child);
        }
        return new Size(width, Math.Round(height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var child in Children)
        {
            var top = Math.Round(y);
            y += Exact(child);
            child.Arrange(new Rect(0, top, finalSize.Width, Math.Round(y) - top));
        }
        return finalSize;
    }

    private static double Exact(Control child) =>
        child.IsVisible ? child.DesiredSize.Height - (child is ImportWizardMapRow row ? row.RoundingSlack : 0) : 0;
}

/// <summary>
/// One source column in the mapping list: padding 10, 12, 10, 16 and a divider above every row but the first. Wide:
/// three columns 4* / 16 / 5* 12 apart (the column's name over two sample values, a 15px arrow, the field select with the
/// confidence cell 12 to its right). Compact: the source above the select and an 84 wide cell, 6 apart, no arrow.
/// </summary>
internal sealed class ImportWizardMapRow : Border
{
    private readonly Grid _grid;
    private readonly StackPanel _source;
    private readonly TextBlock _name;
    private readonly TextBlock _samples;
    private readonly ArcGlyph _arrow;
    private readonly Grid _target;
    private Border? _trigger;
    private bool _compact;
    private bool _ignored;

    public ImportWizardMapRow(ImportColumnState column, IReadOnlyList<SelectOption> options, ImportWizardLabels labels, bool compact)
    {
        Column = column;
        Padding = new Thickness(16, 10, 12, 10);
        _name = ImportWizardUi.Text(column.Header, ArcDesignTokenKind.TypeSM, ArcDesignTokenKind.Foreground, FontWeight.Medium, 19.6);
        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _samples = ImportWizardUi.Text(column.Samples, ArcDesignTokenKind.TypeXS, ArcDesignTokenKind.TextMuted, lineHeight: 16.8);
        _samples.TextTrimming = TextTrimming.CharacterEllipsis;
        _source = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        _source.Children.Add(_name);
        _source.Children.Add(_samples);

        _arrow = ImportWizardUi.Glyph(ArcIcons.ArrowRight, 15, ArcDesignTokenKind.TextMuted);
        _arrow.HorizontalAlignment = HorizontalAlignment.Left;

        Select = new Select
        {
            Size = ControlSize.Small,
            MinWidth = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            ItemsSource = options,
            SelectedIndex = column.Target + 1,
        };
        AutomationProperties.SetName(Select, labels.FieldFor(column.Header));
        Select.TemplateApplied += OnSelectTemplateApplied;
        Select.GotFocus += OnSelectGotFocus;

        Confidence = new ImportWizardConfidence(labels);
        _target = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        _target.Children.Add(Select);
        Grid.SetColumn(Confidence, 1);
        _target.Children.Add(Confidence);

        _grid = new Grid();
        _grid.Children.Add(_source);
        _grid.Children.Add(_arrow);
        _grid.Children.Add(_target);
        Child = _grid;
        _compact = !compact;
        SetCompact(compact);
        SetIgnored(column.IsIgnored);
    }

    /// <summary>The column.</summary>
    public ImportColumnState Column { get; }

    /// <summary>The field select.</summary>
    public Select Select { get; }

    /// <summary>The confidence cell.</summary>
    public ImportWizardConfidence Confidence { get; }

    /// <summary>The column name (tests).</summary>
    public TextBlock NameText => _name;

    /// <summary>The arrow (hidden when compact).</summary>
    public ArcGlyph Arrow => _arrow;

    /// <summary>
    /// How much taller layout rounding made the row than its unrounded height (the 19.6 and 16.8 line boxes of the source
    /// round up to whole pixels): 0.6 at scale 1. The list takes it off so the rows fall on Arc's 57.4 cadence.
    /// </summary>
    public double RoundingSlack
    {
        get
        {
            if (!_compact && _source.DesiredSize.Height < _target.DesiredSize.Height)
            {
                return 0;
            }
            var scale = LayoutHelper.GetLayoutScale(this);
            return Slack(_name.LineHeight, scale) + Slack(_samples.LineHeight, scale);
        }
    }

    private static double Slack(double value, double scale) =>
        double.IsNaN(value) ? 0 : Math.Max(0, LayoutHelper.RoundLayoutValue(Math.Ceiling(value * scale - 1e-6) / scale, scale) - value);

    /// <summary>Draws the divider above the row (every row but the first).</summary>
    public void SetDivider(bool divider)
    {
        if (divider)
        {
            ImportWizardUi.TopLine(this);
        }
        else
        {
            ClearValue(BorderThicknessProperty);
            ClearValue(BorderBrushProperty);
        }
    }

    /// <summary>Mutes the column name when the column is not imported.</summary>
    public void SetIgnored(bool ignored)
    {
        _ignored = ignored;
        ArcTokens.Bind(_name, TextBlock.ForegroundProperty, ignored ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.Foreground);
    }

    /// <summary>True when the name is muted (tests).</summary>
    public bool IsIgnoredShown => _ignored;

    /// <summary>Stacks the row (compact) or lays it out in three columns.</summary>
    public void SetCompact(bool compact)
    {
        if (_compact == compact)
        {
            return;
        }
        _compact = compact;
        ImportWizardUi.Bind(Confidence, WidthProperty, compact ? ImportWizardTokenKind.CompactConfidenceWidth : ImportWizardTokenKind.ConfidenceWidth);
        if (compact)
        {
            _grid.ColumnDefinitions = new ColumnDefinitions("*");
            _grid.RowDefinitions = new RowDefinitions("Auto,Auto");
            _grid.ColumnSpacing = 0;
            _grid.RowSpacing = 6;
            Grid.SetColumn(_target, 0);
            Grid.SetRow(_target, 1);
            _arrow.IsVisible = false;
            return;
        }
        _grid.ColumnDefinitions = new ColumnDefinitions("4*,16,5*");
        _grid.RowDefinitions = new RowDefinitions();
        _grid.ColumnSpacing = 12;
        _grid.RowSpacing = 0;
        Grid.SetColumn(_arrow, 1);
        Grid.SetRow(_target, 0);
        Grid.SetColumn(_target, 2);
        _arrow.IsVisible = true;
    }

    /// <summary>Re-reads the words.</summary>
    public void SetLabels(ImportWizardLabels labels, IReadOnlyList<SelectOption> options)
    {
        AutomationProperties.SetName(Select, labels.FieldFor(Column.Header));
        Select.ItemsSource = options;
        Select.SelectedIndex = Column.Target + 1;
        Confidence.SetLabels(labels);
    }

    // The Select of the mapping list is smaller than the library's field: a 34px trigger with radius 14, padding
    // 0 10 0 12, a 16px chevron (stroke 1.75) 8 from the value; its focus ring keeps 4 more radius.
    private void OnSelectTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _trigger = e.NameScope.Find<Border>(Select.TriggerPart);
        if (_trigger is { } trigger)
        {
            trigger.MinHeight = 34;
            trigger.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
            trigger.Padding = new Thickness(12, 0, 10, 0);
        }
        if (e.NameScope.Find<ArcGlyph>("PART_Chevron") is { } chevron)
        {
            chevron.Margin = new Thickness(8, 0, 0, 0);
            chevron.StrokeThickness = 1.75;
        }
    }

    private void OnSelectGotFocus(object? sender, Avalonia.Input.FocusChangedEventArgs e)
    {
        // The select builds its focus ring on the first keyboard focus (before this handler runs).
        if (_trigger?.Parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Border { Name: LazyFocusRing.PartName } ring && ring.Tag is null)
                {
                    ring.Tag = this;
                    ring.BindCornerRadius(ArcDesignTokenKind.RadiusStep18);
                }
            }
        }
    }
}
