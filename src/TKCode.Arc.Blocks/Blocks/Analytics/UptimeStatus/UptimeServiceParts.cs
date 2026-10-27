using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The name line of a service row (notes 3.2): the name (14, 500), the description (12, TextMuted), a status badge when the
/// service is not operational, and the 90-day uptime rolling digit by digit (14, 500, tabular) with "uptime" after it. It is
/// a toggle button that only ever turns on (<c>aria-pressed</c>): pressing it selects the service. Its theme is in
/// <c>Themes/UptimeStatusTheme.axaml</c>.
/// </summary>
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(BadgePart, typeof(UptimeBadge))]
[TemplatePart(UptimePart, typeof(AnalyticsRollingNumber))]
[TemplatePart(UptimeLabelPart, typeof(TextBlock))]
[PseudoClasses(CompactPseudoClass, NotOperationalPseudoClass)]
internal sealed class UptimeRowButton : ToggleButton
{
    internal const string NamePart = "PART_Name";
    internal const string DescriptionPart = "PART_Description";
    internal const string BadgePart = "PART_Badge";
    internal const string UptimePart = "PART_Uptime";
    internal const string UptimeLabelPart = "PART_UptimeLabel";
    internal const string CompactPseudoClass = ":compact";
    internal const string NotOperationalPseudoClass = ":not-operational";

    private TextBlock? _name;
    private TextBlock? _description;
    private UptimeBadge? _badge;
    private AnalyticsRollingNumber? _uptime;
    private TextBlock? _uptimeLabel;
    private UptimeService? _service;
    private bool _pending;
    private ServiceStatus _status;
    private string _badgeText = string.Empty;
    private string _uptimeText = "uptime";
    private bool _animate;

    protected override Type StyleKeyOverride => typeof(UptimeRowButton);

    /// <summary>Raised when the button is pressed (select the service).</summary>
    public event Action? Chosen;

    internal AnalyticsRollingNumber? UptimeNumber => _uptime;

    internal UptimeBadge? Badge => _badge;

    internal TextBlock? NameBlock => _name;

    /// <summary>Compact layout: the description and the word "uptime" are hidden.</summary>
    public bool IsCompact
    {
        get => PseudoClasses.Contains(CompactPseudoClass);
        set => PseudoClasses.Set(CompactPseudoClass, value);
    }

    protected override void Toggle() => Chosen?.Invoke();

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _name = e.NameScope.Find<TextBlock>(NamePart);
        _description = e.NameScope.Find<TextBlock>(DescriptionPart);
        _badge = e.NameScope.Find<UptimeBadge>(BadgePart);
        _uptime = e.NameScope.Find<AnalyticsRollingNumber>(UptimePart);
        _uptimeLabel = e.NameScope.Find<TextBlock>(UptimeLabelPart);
        if (_pending)
        {
            Apply(false);
        }
    }

    /// <summary>
    /// Shows a service: its name and description at once, the badge when it is not operational (at once) and the uptime
    /// rolling when <paramref name="animate"/>.
    /// </summary>
    public void Show(UptimeService service, string badgeText, string uptimeWord, bool animate)
    {
        _service = service;
        _status = service.CurrentStatus;
        _badgeText = badgeText;
        _uptimeText = uptimeWord;
        _animate = animate;
        _pending = true;
        Apply(animate);
    }

    private void Apply(bool animate)
    {
        if (_service is null)
        {
            return;
        }
        PseudoClasses.Set(NotOperationalPseudoClass, _status != ServiceStatus.Operational);
        if (_name is null)
        {
            return;
        }
        _name.Text = _service.Name;
        _description!.Text = _service.Description;
        _uptimeLabel!.Text = _uptimeText;
        if (_badge is not null)
        {
            _badge.IsVisible = _status != ServiceStatus.Operational;
            _badge.Label = _badgeText;
            _badge.Status = _status == ServiceStatus.Operational ? ServiceStatus.Degraded : _status;
        }
        if (_uptime is not null)
        {
            _uptime.IsInstant = !animate;
            _uptime.Format = AnalyticsNumberFormat.Percent(2);
            _uptime.Amount = UptimeEngine.Percent(_service.Uptime);
            _uptime.IsInstant = false;
        }
        _pending = false;
    }
}

/// <summary>
/// One service: the name line over the day bars (notes 3.2): padding (10, 8, 10, 10), 6 between the lines, radius 10. A row
/// that is not selected darkens to SurfaceMuted at 55% under the pointer (no transition); the selected row's backdrop is the
/// list's gliding highlight.
/// </summary>
internal sealed class UptimeServiceRow : Grid
{
    public UptimeServiceRow(UptimeService service)
    {
        Service = service;
        Background = Brushes.Transparent;
        RowDefinitions = new RowDefinitions("Auto,Auto");
        RowSpacing = 6;
        Button = new UptimeRowButton { Margin = new Thickness(10, 8, 10, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
        Strip = new UptimeStrip { Margin = new Thickness(10, 0, 10, 10) };
        SetRow(Strip, 1);
        Backdrop = new UptimeRowBackdrop(this);
        SetRowSpan(Backdrop, 2);
        Children.Add(Backdrop);
        Children.Add(Button);
        Children.Add(Strip);
    }

    /// <summary>The hover backdrop.</summary>
    internal UptimeRowBackdrop Backdrop { get; }

    /// <summary>The service shown.</summary>
    public UptimeService Service { get; }

    /// <summary>The name line.</summary>
    public UptimeRowButton Button { get; }

    /// <summary>The bars.</summary>
    public UptimeStrip Strip { get; }

    /// <summary>True for the charted service (its backdrop is the glide).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                Backdrop.InvalidateVisual();
            }
        }
    }

    private bool _isSelected;

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Backdrop.InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Backdrop.InvalidateVisual();
    }
}

/// <summary>The hover backdrop of a service row: SurfaceMuted at 55% under the pointer, radius 10 (no transition).</summary>
internal sealed class UptimeRowBackdrop(UptimeServiceRow row) : UptimeVisual
{
    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        if (!row.IsSelected && row.IsPointerOver)
        {
            context.DrawRectangle(Palette.Brush(Palette.SurfaceMuted, 0.55), null, new RoundedRect(new Rect(Bounds.Size), 10));
        }
    }
}

/// <summary>The selection highlight behind the service rows: SurfaceMuted with a 1px border, radius 10.</summary>
internal sealed class UptimeHighlight(UptimeServiceList list) : UptimeVisual
{
    public override void Render(DrawingContext context)
    {
        EnsurePalette();
        if (!list.HighlightVisible)
        {
            return;
        }
        var (y, h) = list.Highlight;
        var rect = new Rect(0, y, Bounds.Width, Math.Max(0, h));
        context.DrawRectangle(Palette.Brush(Palette.SurfaceMuted), Palette.Pen(Palette.Border, 1), new RoundedRect(rect.Deflate(0.5), 10));
    }
}

/// <summary>
/// The service rows stacked with a 1px gap, and the selection highlight behind them (notes 3.2 and 6.4): SurfaceMuted with a
/// 1px border, radius 10, which glides from row to row on a spring (k 150, c 24 ≈ Smooth) and follows a row whose height
/// changes (a badge appears).
/// </summary>
/// <remarks>Why code-driven: one shared shape travels between rows (a shared-layout glide), which a ControlTheme cannot express.</remarks>
internal sealed class UptimeServiceList : Panel
{
    private static readonly SpringSpec s_glide = SpringSpec.FromPhysical(150, 24);

    private readonly UptimeHighlight _highlight;
    private readonly LiveLoop _loop;
    private AnalyticsSpringChannel _y = new(0, 0.01, 0.05);
    private AnalyticsSpringChannel _h = new(0, 0.01, 0.05);
    private UptimeServiceRow? _selected;
    private bool _placed;

    public UptimeServiceList()
    {
        _highlight = new UptimeHighlight(this);
        Children.Add(_highlight);
        _loop = new LiveLoop(this, dt =>
        {
            var moving = _y.Step(dt) | _h.Step(dt);
            _highlight.InvalidateVisual();
            return moving;
        });
    }

    /// <summary>The row that carries the highlight.</summary>
    public UptimeServiceRow? SelectedRow => _selected;

    /// <summary>The rows in order.</summary>
    public IEnumerable<UptimeServiceRow> Rows => Children.OfType<UptimeServiceRow>();

    /// <summary>True once the highlight has been placed.</summary>
    internal bool HighlightVisible => _selected is not null && _placed;

    /// <summary>The highlight's top and height now.</summary>
    internal (double Y, double Height) Highlight => (_y.Value, _h.Value);

    /// <summary>The highlight's top and height it is heading to (tests).</summary>
    internal (double Y, double Height) HighlightTarget => (_y.Target, _h.Target);

    /// <summary>Moves the highlight to a row (the first placement jumps).</summary>
    public void Select(UptimeServiceRow? row)
    {
        if (ReferenceEquals(row, _selected))
        {
            return;
        }
        if (_selected is not null)
        {
            _selected.IsSelected = false;
        }
        _selected = row;
        if (row is not null)
        {
            row.IsSelected = true;
        }
        InvalidateArrange();
        Place();
    }

    /// <summary>Removes every row.</summary>
    public void Reset()
    {
        Children.Clear();
        Children.Add(_highlight);
        _selected = null;
        _placed = false;
    }

    private void Place()
    {
        if (_selected is null || _selected.Bounds.Height <= 0)
        {
            return;
        }
        var y = _selected.Bounds.Y;
        var h = _selected.Bounds.Height;
        if (!_placed || !ArcMotion.IsFull(this) || !this.IsAttachedToVisualTree())
        {
            _y.Jump(y);
            _h.Jump(h);
            _placed = true;
        }
        else
        {
            _y.SpringTo(y, s_glide);
            _h.SpringTo(h, s_glide);
        }
        _highlight.InvalidateVisual();
        _loop.Start();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        var count = 0;
        foreach (var row in Rows)
        {
            row.Measure(availableSize.WithHeight(double.PositiveInfinity));
            width = Math.Max(width, row.DesiredSize.Width);
            height += row.DesiredSize.Height + 1;
            count++;
        }
        _highlight.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, Math.Max(0, height - (count > 0 ? 1 : 0)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var y = 0.0;
        foreach (var row in Rows)
        {
            var h = row.DesiredSize.Height;
            row.Arrange(new Rect(0, y, finalSize.Width, h));
            y += h + 1;
        }
        _highlight.Arrange(new Rect(finalSize));
        Place();
        return finalSize;
    }
}
