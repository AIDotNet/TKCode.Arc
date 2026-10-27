using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// One row of the moments column of a <see cref="LineReplay"/>: icon, title, time, the optional detail and the effect.
/// Its states (before the playhead, passed, the latest passed, an incident, effect played) are pseudo-classes the theme
/// colors; Enter or Space moves the playhead to it.
/// </summary>
[PseudoClasses(ReachedPseudoClass, CurrentPseudoClass, IncidentPseudoClass, EffectShownPseudoClass)]
internal sealed class LineReplayMomentButton : AvaloniaButton
{
    public const string ReachedPseudoClass = ":reached";
    public const string CurrentPseudoClass = ":current";
    public const string IncidentPseudoClass = ":incident";
    public const string EffectShownPseudoClass = ":effect-shown";

    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<LineReplayMomentButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LineReplayMomentButton, string?>(nameof(Title));

    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<LineReplayMomentButton, string?>(nameof(TimeText));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<LineReplayMomentButton, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> EffectTextProperty =
        AvaloniaProperty.Register<LineReplayMomentButton, string?>(nameof(EffectText));

    private bool _reached;
    private bool _current;
    private bool _effectShown;

    public LineReplayMomentButton(LineReplayMomentInfo info)
    {
        Info = info;
        PseudoClasses.Set(IncidentPseudoClass, info.IsIncident);
    }

    public LineReplayMomentInfo Info { get; }

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }

    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public string? EffectText { get => GetValue(EffectTextProperty); set => SetValue(EffectTextProperty, value); }

    public bool IsReached => _reached;

    public bool IsCurrent => _current;

    public bool IsEffectShown => _effectShown;

    protected override Type StyleKeyOverride => typeof(LineReplayMomentButton);

    /// <summary>Applies the playhead's position to the row's states.</summary>
    public void SetState(bool reached, bool current, bool effectShown)
    {
        if (reached != _reached)
        {
            _reached = reached;
            PseudoClasses.Set(ReachedPseudoClass, reached);
        }
        if (current != _current)
        {
            _current = current;
            PseudoClasses.Set(CurrentPseudoClass, current);
            AutomationProperties.SetItemStatus(this, current ? "Current" : null);
        }
        if (effectShown != _effectShown)
        {
            _effectShown = effectShown;
            PseudoClasses.Set(EffectShownPseudoClass, effectShown);
            UpdateAutomationName();
        }
    }

    internal void UpdateAutomationName()
    {
        // The effect is read only once the replay has played through it.
        var parts = new List<string?> { Title, TimeText, Detail };
        if (_effectShown)
        {
            parts.Add(EffectText);
        }
        AutomationProperties.SetName(this, string.Join(", ", parts.Where(p => !string.IsNullOrEmpty(p))));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty || change.Property == TimeTextProperty || change.Property == DetailProperty ||
            change.Property == EffectTextProperty)
        {
            UpdateAutomationName();
        }
    }
}

/// <summary>
/// Lays the moment rows out in one or two columns (rows as tall as their tallest cell, 2 apart; columns 8 apart).
/// </summary>
internal sealed class LineReplayMomentsPanel : Panel
{
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<LineReplayMomentsPanel, int>(nameof(Columns), 1);

    private const double ColumnGap = 8;
    private const double RowGap = 2;

    static LineReplayMomentsPanel()
    {
        AffectsMeasure<LineReplayMomentsPanel>(ColumnsProperty);
    }

    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    private double CellWidth(double width)
    {
        var columns = Math.Max(1, Columns);
        return Math.Max(0, (width - ColumnGap * (columns - 1)) / columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Math.Max(1, Columns);
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 300 * columns;
        var cell = CellWidth(width);
        var height = 0.0;
        var rowHeight = 0.0;
        var visible = Children.Where(c => c.IsVisible).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            visible[i].Measure(new Size(cell, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, visible[i].DesiredSize.Height);
            if (i % columns == columns - 1 || i == visible.Count - 1)
            {
                height += rowHeight + (i >= columns ? RowGap : 0);
                rowHeight = 0;
            }
        }
        return new Size(double.IsFinite(availableSize.Width) ? width : cell * columns + ColumnGap * (columns - 1), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var cell = CellWidth(finalSize.Width);
        var visible = Children.Where(c => c.IsVisible).ToList();
        var y = 0.0;
        for (var start = 0; start < visible.Count; start += columns)
        {
            var count = Math.Min(columns, visible.Count - start);
            var rowHeight = 0.0;
            for (var j = 0; j < count; j++)
            {
                rowHeight = Math.Max(rowHeight, visible[start + j].DesiredSize.Height);
            }
            for (var j = 0; j < count; j++)
            {
                visible[start + j].Arrange(new Rect(j * (cell + ColumnGap), y, cell, rowHeight));
            }
            y += rowHeight + RowGap;
        }
        return finalSize;
    }
}

/// <summary>
/// The readout's four items (time, value, ratio, peak): one row 32 apart, or below 560 three equal columns with the
/// peak on a second row.
/// </summary>
internal sealed class LineReplayReadoutPanel : Panel
{
    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<LineReplayReadoutPanel, bool>(nameof(IsNarrow));

    private const double Gap = 32;
    private const double NarrowColumnGap = 16;
    private const double RowGap = 12;

    static LineReplayReadoutPanel()
    {
        AffectsMeasure<LineReplayReadoutPanel>(IsNarrowProperty);
    }

    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    private List<Control> Visible => Children.Where(c => c.IsVisible).ToList();

    protected override Size MeasureOverride(Size availableSize)
    {
        var items = Visible;
        if (!IsNarrow)
        {
            var width = 0.0;
            var height = 0.0;
            foreach (var item in items)
            {
                item.Measure(Size.Infinity);
                width += item.DesiredSize.Width;
                height = Math.Max(height, item.DesiredSize.Height);
            }
            return new Size(width + Gap * Math.Max(0, items.Count - 1), height);
        }
        var total = double.IsFinite(availableSize.Width) ? availableSize.Width : 360;
        var cell = (total - NarrowColumnGap * 2) / 3;
        var first = 0.0;
        var second = 0.0;
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Measure(new Size(i < 3 ? cell : total, double.PositiveInfinity));
            if (i < 3)
            {
                first = Math.Max(first, items[i].DesiredSize.Height);
            }
            else
            {
                second = Math.Max(second, items[i].DesiredSize.Height);
            }
        }
        return new Size(total, first + (items.Count > 3 ? RowGap + second : 0));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var items = Visible;
        if (!IsNarrow)
        {
            var x = 0.0;
            foreach (var item in items)
            {
                item.Arrange(new Rect(x, 0, item.DesiredSize.Width, finalSize.Height));
                x += item.DesiredSize.Width + Gap;
            }
            return finalSize;
        }
        var cell = (finalSize.Width - NarrowColumnGap * 2) / 3;
        var first = items.Take(3).Select(i => i.DesiredSize.Height).DefaultIfEmpty(0).Max();
        for (var i = 0; i < items.Count; i++)
        {
            var rect = i < 3
                ? new Rect(i * (cell + NarrowColumnGap), first - items[i].DesiredSize.Height, cell, items[i].DesiredSize.Height)
                : new Rect(0, first + RowGap, finalSize.Width, items[i].DesiredSize.Height);
            items[i].Arrange(rect);
        }
        return finalSize;
    }
}

/// <summary>
/// A readout value redrawn every frame of playback without layout: it measures as wide as its widest
/// <see cref="Sample"/> (or the widest text it has shown), and draws its text left-aligned.
/// </summary>
internal sealed class LineReplayReadoutValue : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<LineReplayReadoutValue>();

    private const double Size16 = 16;
    private const double LineHeight = 22;

    private readonly LineChartsTextCache _text = new(256);
    private string _value = "";
    private string _sample = "";
    private double _widest;

    static LineReplayReadoutValue()
    {
        AffectsRender<LineReplayReadoutValue>(ForegroundProperty);
    }

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public string Value => _value;

    /// <summary>The text the value is sized for (its widest expected form).</summary>
    public string Sample
    {
        get => _sample;
        set
        {
            _sample = value;
            _widest = 0;
            InvalidateMeasure();
        }
    }

    public void SetValue(string value)
    {
        if (value == _value)
        {
            return;
        }
        _value = value;
        AutomationProperties.SetName(this, value);
        if (TopLevel.GetTopLevel(this) is not null && Measure(value) > _widest + 0.5)
        {
            InvalidateMeasure();
        }
        InvalidateVisual();
    }

    private double Measure(string text)
    {
        _text.Family = TextElement.GetFontFamily(this);
        return _text.Get(text, Size16, FontWeight.Medium).Width;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _widest = Math.Max(_widest, Math.Max(Measure(_sample), Measure(_value)));
        var width = Math.Ceiling(_widest);
        return new Size(double.IsFinite(availableSize.Width) ? Math.Min(width, availableSize.Width) : width, LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        if (Foreground is not { } brush || _value.Length == 0)
        {
            return;
        }
        _text.Family = TextElement.GetFontFamily(this);
        _text.Get(_value, Size16, FontWeight.Medium).DrawCentered(context, brush, 0, Bounds.Height / 2);
    }
}
