using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A rolling number fed with formatted text: the text's leading and trailing non-digits become the static prefix and
/// suffix ("$", "%", " users"), the digits in between roll (Journey flow's odometers: each digit a clipped 0–9 strip on
/// the Smooth spring, no fade at the edges). Any <c>Func&lt;double, string&gt;</c> formatter works this way.
/// </summary>
internal sealed class JourneyFlowRollingText : RollingDigits
{
    /// <summary>The formatted text.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<JourneyFlowRollingText, string?>(nameof(Text));

    public JourneyFlowRollingText()
    {
        Falloff = RollingDigitsFalloff.None;
        WheelSpring = ArcSpring.Smooth;
        CultureName = CultureInfo.CurrentCulture.Name;
    }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override Type StyleKeyOverride => typeof(RollingDigits);

    /// <summary>Splits formatted text into prefix, number, decimals and suffix (false when it has no digits).</summary>
    internal static bool TrySplit(string text, CultureInfo culture, out string prefix, out double value, out int decimals, out string suffix)
    {
        prefix = text;
        suffix = string.Empty;
        value = 0;
        decimals = 0;
        var first = -1;
        var last = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsAsciiDigit(text[i]))
            {
                if (first < 0)
                {
                    first = i;
                }
                last = i;
            }
        }
        if (first < 0)
        {
            return false;
        }
        var core = text.Substring(first, last - first + 1);
        var format = culture.NumberFormat;
        var decimalAt = core.LastIndexOf(format.NumberDecimalSeparator, StringComparison.Ordinal);
        var plain = core.Replace(format.NumberGroupSeparator, string.Empty, StringComparison.Ordinal);
        if (!double.TryParse(plain, NumberStyles.AllowDecimalPoint, culture, out value))
        {
            return false;
        }
        decimals = decimalAt >= 0 ? core.Length - decimalAt - format.NumberDecimalSeparator.Length : 0;
        prefix = text[..first];
        suffix = text[(last + 1)..];
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Apply();
        }
    }

    private void Apply()
    {
        var text = Text ?? string.Empty;
        var culture = CultureInfo.GetCultureInfo(string.IsNullOrEmpty(CultureName) ? "en-US" : CultureName);
        if (TrySplit(text, culture, out var prefix, out var value, out var decimals, out var suffix))
        {
            SetCurrentValue(PrefixProperty, prefix);
            SetCurrentValue(SuffixProperty, suffix);
            SetCurrentValue(DecimalsProperty, decimals);
            SetCurrentValue(ValueProperty, value);
        }
        AutomationProperties.SetName(this, text);
    }
}

/// <summary>What a side panel row shows.</summary>
internal enum JourneyFlowRowState
{
    /// <summary>Name, count, share and a share bar.</summary>
    Data,

    /// <summary>One muted line ("The journey begins here"), no numbers, the bar empty.</summary>
    Placeholder,

    /// <summary>Nothing (a reserved slot that keeps the group's height).</summary>
    Empty
}

/// <summary>
/// One row of the Journey flow side panel: a name cut at the column (no ellipsis, as measured), a rolling count, a rolling
/// share and a 3px share bar under them. Groups keep a fixed number of rows so the panel does not change height.
/// </summary>
[PseudoClasses(PlaceholderPseudoClass, EmptyPseudoClass)]
internal sealed class JourneyFlowRow : TemplatedControl
{
    public const string PlaceholderPseudoClass = ":placeholder";
    public const string EmptyPseudoClass = ":empty";

    /// <summary>The name.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<JourneyFlowRow, string?>(nameof(Label));

    /// <summary>The count as text.</summary>
    public static readonly StyledProperty<string?> CountTextProperty =
        AvaloniaProperty.Register<JourneyFlowRow, string?>(nameof(CountText));

    /// <summary>The share as text.</summary>
    public static readonly StyledProperty<string?> ShareTextProperty =
        AvaloniaProperty.Register<JourneyFlowRow, string?>(nameof(ShareText));

    /// <summary>The bar's fill (0–1).</summary>
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<JourneyFlowRow, double>(nameof(Fraction));

    /// <summary>Data, placeholder or empty.</summary>
    public static readonly StyledProperty<JourneyFlowRowState> StateProperty =
        AvaloniaProperty.Register<JourneyFlowRow, JourneyFlowRowState>(nameof(State));

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="CountTextProperty"/>
    public string? CountText { get => GetValue(CountTextProperty); set => SetValue(CountTextProperty, value); }

    /// <inheritdoc cref="ShareTextProperty"/>
    public string? ShareText { get => GetValue(ShareTextProperty); set => SetValue(ShareTextProperty, value); }

    /// <inheritdoc cref="FractionProperty"/>
    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }

    /// <inheritdoc cref="StateProperty"/>
    public JourneyFlowRowState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    /// <summary>Shows a row of data.</summary>
    public void ShowData(string label, string count, string share, double fraction)
    {
        Label = label;
        CountText = count;
        ShareText = share;
        Fraction = fraction;
        State = JourneyFlowRowState.Data;
        AutomationProperties.SetName(this, $"{label}, {count}, {share}");
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    /// <summary>Shows a placeholder line.</summary>
    public void ShowPlaceholder(string text)
    {
        Label = text;
        Fraction = 0;
        State = JourneyFlowRowState.Placeholder;
        AutomationProperties.SetName(this, text);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    /// <summary>Keeps the slot empty.</summary>
    public void ShowEmpty()
    {
        Fraction = 0;
        State = JourneyFlowRowState.Empty;
        AutomationProperties.SetName(this, string.Empty);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            PseudoClasses.Set(PlaceholderPseudoClass, State == JourneyFlowRowState.Placeholder);
            PseudoClasses.Set(EmptyPseudoClass, State == JourneyFlowRowState.Empty);
        }
    }
}

/// <summary>The "Flow table" disclosure button: label and a chevron that turns over when expanded.</summary>
[PseudoClasses(ExpandedPseudoClass)]
internal sealed class JourneyFlowTableButton : AvaloniaButton
{
    public const string ExpandedPseudoClass = ":expanded";

    /// <summary>Whether the table is open.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<JourneyFlowTableButton, bool>(nameof(IsExpanded));

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    protected override Type StyleKeyOverride => typeof(JourneyFlowTableButton);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, IsExpanded);
            if (ControlAutomationPeer.FromElement(this) is TablePeer peer)
            {
                peer.RaiseStateChanged(change.GetOldValue<bool>(), IsExpanded);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TablePeer(this);

    internal void Activate() => OnClick();

    /// <summary>A button that also reports expanded or collapsed (Arc's aria-expanded).</summary>
    internal sealed class TablePeer(JourneyFlowTableButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        public ExpandCollapseState ExpandCollapseState => owner.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!owner.IsExpanded)
            {
                owner.Activate();
            }
        }

        public void Collapse()
        {
            if (owner.IsExpanded)
            {
                owner.Activate();
            }
        }

        internal void RaiseStateChanged(bool oldValue, bool newValue) => RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
    }
}

/// <summary>The side panel's "Clear" button (an x and a short label).</summary>
internal sealed class JourneyFlowClearButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(JourneyFlowClearButton);
}

/// <summary>
/// The header row: the title block at the start and the segment switch at the end on one line when they fit (24 apart),
/// otherwise the switch on a second line 12 below, at the start; stretched over the whole width when compact.
/// </summary>
internal sealed class JourneyFlowHeaderPanel : Panel
{
    /// <summary>The switch takes the whole width on its own line (compact blocks).</summary>
    public static readonly StyledProperty<bool> IsStretchedProperty =
        AvaloniaProperty.Register<JourneyFlowHeaderPanel, bool>(nameof(IsStretched));

    static JourneyFlowHeaderPanel()
    {
        AffectsMeasure<JourneyFlowHeaderPanel>(IsStretchedProperty);
    }

    /// <inheritdoc cref="IsStretchedProperty"/>
    public bool IsStretched { get => GetValue(IsStretchedProperty); set => SetValue(IsStretchedProperty, value); }

    /// <summary>True when the switch sits on its own line.</summary>
    internal bool IsWrapped { get; private set; }

    private const double ColumnGap = 24;
    private const double RowGap = 12;

    protected override Size MeasureOverride(Size availableSize)
    {
        var title = Children.Count > 0 ? Children[0] : null;
        var tail = Children.Count > 1 ? Children[1] : null;
        var width = availableSize.Width;
        title?.Measure(new Size(width, double.PositiveInfinity));
        tail?.Measure(new Size(IsStretched ? width : double.PositiveInfinity, double.PositiveInfinity));
        var titleSize = title is { IsVisible: true } ? title.DesiredSize : default;
        var tailSize = tail is { IsVisible: true } ? tail.DesiredSize : default;
        IsWrapped = tailSize.Width > 0 && (IsStretched || titleSize.Width + ColumnGap + tailSize.Width > width);
        if (IsWrapped)
        {
            if (IsStretched && double.IsFinite(width))
            {
                tail!.Measure(new Size(width, double.PositiveInfinity));
                tailSize = tail.DesiredSize;
            }
            var gap = titleSize.Height > 0 ? RowGap : 0;
            return new Size(Math.Max(titleSize.Width, tailSize.Width), titleSize.Height + gap + tailSize.Height);
        }
        return new Size(titleSize.Width + (tailSize.Width > 0 ? ColumnGap + tailSize.Width : 0), Math.Max(titleSize.Height, tailSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var title = Children.Count > 0 ? Children[0] : null;
        var tail = Children.Count > 1 ? Children[1] : null;
        var titleSize = title is { IsVisible: true } ? title.DesiredSize : default;
        var tailSize = tail is { IsVisible: true } ? tail.DesiredSize : default;
        if (IsWrapped)
        {
            title?.Arrange(new Rect(0, 0, finalSize.Width, titleSize.Height));
            var gap = titleSize.Height > 0 ? RowGap : 0;
            var w = IsStretched ? finalSize.Width : tailSize.Width;
            tail?.Arrange(new Rect(0, titleSize.Height + gap, w, tailSize.Height));
        }
        else
        {
            var height = finalSize.Height;
            title?.Arrange(new Rect(0, (height - titleSize.Height) / 2, Math.Max(0, finalSize.Width - tailSize.Width - ColumnGap), titleSize.Height));
            tail?.Arrange(new Rect(finalSize.Width - tailSize.Width, (height - tailSize.Height) / 2, tailSize.Width, tailSize.Height));
        }
        return finalSize;
    }
}

/// <summary>
/// The body: the chart area and the side panel side by side (panel <see cref="PanelWidth"/> wide) when
/// <see cref="IsWide"/>, otherwise the panel under the chart.
/// </summary>
internal sealed class JourneyFlowBodyPanel : Panel
{
    /// <summary>Side by side.</summary>
    public static readonly StyledProperty<bool> IsWideProperty =
        AvaloniaProperty.Register<JourneyFlowBodyPanel, bool>(nameof(IsWide));

    /// <summary>Width of the side panel when side by side.</summary>
    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<JourneyFlowBodyPanel, double>(nameof(PanelWidth), 280);

    static JourneyFlowBodyPanel()
    {
        AffectsMeasure<JourneyFlowBodyPanel>(IsWideProperty, PanelWidthProperty);
    }

    /// <inheritdoc cref="IsWideProperty"/>
    public bool IsWide { get => GetValue(IsWideProperty); set => SetValue(IsWideProperty, value); }

    /// <inheritdoc cref="PanelWidthProperty"/>
    public double PanelWidth { get => GetValue(PanelWidthProperty); set => SetValue(PanelWidthProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var chart = Children.Count > 0 ? Children[0] : null;
        var panel = Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 900;
        if (IsWide && panel is not null)
        {
            chart?.Measure(new Size(Math.Max(0, width - PanelWidth), double.PositiveInfinity));
            panel.Measure(new Size(PanelWidth, double.PositiveInfinity));
            return new Size(width, Math.Max(chart?.DesiredSize.Height ?? 0, panel.DesiredSize.Height));
        }
        chart?.Measure(new Size(width, double.PositiveInfinity));
        panel?.Measure(new Size(width, double.PositiveInfinity));
        return new Size(width, (chart?.DesiredSize.Height ?? 0) + (panel?.DesiredSize.Height ?? 0));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var chart = Children.Count > 0 ? Children[0] : null;
        var panel = Children.Count > 1 && Children[1].IsVisible ? Children[1] : null;
        if (IsWide && panel is not null)
        {
            var chartWidth = Math.Max(0, finalSize.Width - PanelWidth);
            chart?.Arrange(new Rect(0, 0, chartWidth, finalSize.Height));
            panel.Arrange(new Rect(chartWidth, 0, PanelWidth, finalSize.Height));
            return finalSize;
        }
        var chartHeight = chart?.DesiredSize.Height ?? 0;
        chart?.Arrange(new Rect(0, 0, finalSize.Width, chartHeight));
        panel?.Arrange(new Rect(0, chartHeight, finalSize.Width, panel.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>The side panel's two groups: stacked, or side by side in equal columns (<see cref="Columns"/> 2).</summary>
internal sealed class JourneyFlowGroupsPanel : Panel
{
    /// <summary>1 or 2 columns.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<JourneyFlowGroupsPanel, int>(nameof(Columns), 1);

    /// <summary>Gap between columns.</summary>
    public static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<JourneyFlowGroupsPanel, double>(nameof(ColumnGap), 28);

    /// <summary>Gap between rows.</summary>
    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<JourneyFlowGroupsPanel, double>(nameof(RowGap), 20);

    static JourneyFlowGroupsPanel()
    {
        AffectsMeasure<JourneyFlowGroupsPanel>(ColumnsProperty, ColumnGapProperty, RowGapProperty);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns { get => GetValue(ColumnsProperty); set => SetValue(ColumnsProperty, value); }

    /// <inheritdoc cref="ColumnGapProperty"/>
    public double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }

    /// <inheritdoc cref="RowGapProperty"/>
    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    private double ColumnWidth(double width)
    {
        var columns = Math.Max(1, Columns);
        return columns == 1 ? width : Math.Max(0, (width - ColumnGap * (columns - 1)) / columns);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : 280;
        var columns = Math.Max(1, Columns);
        var cell = ColumnWidth(width);
        double height = 0, row = 0;
        var visible = Children.Where(c => c.IsVisible).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            visible[i].Measure(new Size(cell, double.PositiveInfinity));
            row = Math.Max(row, visible[i].DesiredSize.Height);
            if (i % columns == columns - 1 || i == visible.Count - 1)
            {
                height += row + (height > 0 ? RowGap : 0);
                row = 0;
            }
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, Columns);
        var cell = ColumnWidth(finalSize.Width);
        var visible = Children.Where(c => c.IsVisible).ToList();
        double y = 0;
        for (var start = 0; start < visible.Count; start += columns)
        {
            double row = 0;
            for (var i = start; i < Math.Min(visible.Count, start + columns); i++)
            {
                row = Math.Max(row, visible[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(visible.Count, start + columns); i++)
            {
                var c = i - start;
                visible[i].Arrange(new Rect(c * (cell + ColumnGap), y, cell, row));
            }
            y += row + RowGap;
        }
        return finalSize;
    }
}

/// <summary>
/// The flow table: a caption, a header row (From, To, count, share of step) and one row per link, 1px BorderSubtle rules
/// under every row; numbers right-aligned and tabular. Built from text blocks so screen readers read it as text.
/// </summary>
internal sealed class JourneyFlowTable : Grid
{
    private readonly TextBlock _caption = new();
    private readonly List<Control> _rows = new();

    public JourneyFlowTable()
    {
        ColumnDefinitions = new ColumnDefinitions("*,*,*,*");
        _caption.Margin = new Thickness(0, 4, 0, 10);
        _caption.FontSize = 12;
        _caption.LineHeight = 16.8;
        ArcTokens.Bind(_caption, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        SetColumnSpan(_caption, 4);
        Children.Add(_caption);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Content);
    }

    /// <summary>The caption text (tests).</summary>
    internal string? Caption => _caption.Text;

    /// <summary>Rows of data (tests).</summary>
    internal int RowCount { get; private set; }

    /// <summary>The text of a cell (tests): row −1 is the header.</summary>
    internal string? CellText(int row, int column) => _rows.OfType<TextBlock>()
        .FirstOrDefault(t => GetRow(t) == row + 2 && GetColumn(t) == column)?.Text;

    /// <summary>Rebuilds the table.</summary>
    public void Update(string caption, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        _caption.Text = caption;
        foreach (var control in _rows)
        {
            Children.Remove(control);
        }
        _rows.Clear();
        RowDefinitions.Clear();
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var r = -1; r < rows.Count; r++)
        {
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var gridRow = r + 2;
            var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), IsHitTestVisible = false };
            ArcTokens.Bind(rule, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            SetRow(rule, gridRow);
            SetColumnSpan(rule, 4);
            AutomationProperties.SetAccessibilityView(rule, AccessibilityView.Raw);
            _rows.Add(rule);
            for (var c = 0; c < 4; c++)
            {
                var text = r < 0 ? (c < headers.Count ? headers[c] : string.Empty) : (c < rows[r].Length ? rows[r][c] : string.Empty);
                var cell = new TextBlock
                {
                    Text = text,
                    FontSize = r < 0 ? 12 : 13,
                    LineHeight = r < 0 ? 16.8 : 18.2,
                    Margin = new Thickness(c == 1 ? 12 : 0, 8, 0, 8),
                    TextAlignment = c >= 2 ? TextAlignment.Right : TextAlignment.Left,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                if (c >= 2)
                {
                    cell.FontFeatures = AnalyticsTextCache.TabularFigures;
                }
                ArcTokens.Bind(cell, TextBlock.ForegroundProperty, r < 0 ? ArcDesignTokenKind.TextMuted : ArcDesignTokenKind.Foreground);
                SetRow(cell, gridRow);
                SetColumn(cell, c);
                _rows.Add(cell);
            }
        }
        RowCount = rows.Count;
        Children.AddRange(_rows);
    }
}
