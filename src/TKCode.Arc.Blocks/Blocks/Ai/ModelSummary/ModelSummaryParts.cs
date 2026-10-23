using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The description paragraph of a <see cref="ModelSummary"/>: centered, wrapped, and balanced the way CSS
/// <c>text-wrap: balance</c> does it. A paragraph of two to six lines is measured at the smallest width that keeps
/// its line count (a binary search on whole pixels), so the lines come out about equally long instead of a full line
/// followed by a stub. The result is cached per text and available width, so a stable layout measures it once.
/// </summary>
internal sealed class ModelSummaryText : TextBlock
{
    /// <summary>Chromium balances up to six lines; longer text wraps normally.</summary>
    internal const int MaxBalancedLines = 6;

    private string? _cachedText;
    private double _cachedAvailable = double.NaN;
    private double _cachedWidth = double.NaN;

    protected override Type StyleKeyOverride => typeof(ModelSummaryText);

    /// <summary>The width the paragraph was last measured at (the balanced width, or the available width).</summary>
    internal double BalancedWidth { get; private set; } = double.NaN;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == TextProperty || property == FontSizeProperty || property == FontFamilyProperty ||
            property == FontWeightProperty || property == FontStyleProperty || property == FontStretchProperty ||
            property == FontFeaturesProperty || property == LetterSpacingProperty || property == LineHeightProperty ||
            property == LineSpacingProperty || property == PaddingProperty || property == TextWrappingProperty ||
            property == MaxLinesProperty)
        {
            _cachedText = null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = Text;
        if (TextWrapping == TextWrapping.NoWrap || double.IsInfinity(availableSize.Width) || string.IsNullOrEmpty(text))
        {
            BalancedWidth = availableSize.Width;
            return base.MeasureOverride(availableSize);
        }
        if (ReferenceEquals(text, _cachedText) && availableSize.Width.Equals(_cachedAvailable))
        {
            BalancedWidth = _cachedWidth;
            return base.MeasureOverride(availableSize.WithWidth(_cachedWidth));
        }
        var natural = base.MeasureOverride(availableSize);
        var lines = TextLayout.TextLines.Count;
        var width = availableSize.Width;
        if (lines is >= 2 and <= MaxBalancedLines)
        {
            var padding = Padding.Left + Padding.Right;
            // The text is at least as long as its widest line, so a measure narrower than that over the line count
            // cannot hold it in the same number of lines: a safe lower bound for the search.
            var low = padding + Math.Max(0, natural.Width - padding) / lines;
            var high = Math.Min(availableSize.Width, natural.Width);
            while (high - low > 0.5)
            {
                var middle = (low + high) / 2;
                base.MeasureOverride(availableSize.WithWidth(middle));
                if (TextLayout.TextLines.Count <= lines)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }
            width = Math.Ceiling(high);
        }
        _cachedText = text;
        _cachedAvailable = availableSize.Width;
        _cachedWidth = width;
        BalancedWidth = width;
        return width.Equals(availableSize.Width) ? natural : base.MeasureOverride(availableSize.WithWidth(width));
    }
}

/// <summary>
/// The facts table of a <see cref="ModelSummary"/>: four equal columns, or two rows of two when
/// <see cref="IsCompact"/>. Each row is as high as its tallest cell. The panel also gives every
/// <see cref="ModelSummaryFact"/> its dividers: a left edge after the first column, a top edge from the second row on.
/// </summary>
internal sealed class ModelSummaryFacts : Panel
{
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ModelSummaryFacts, bool>(nameof(IsCompact));

    static ModelSummaryFacts()
    {
        AffectsMeasure<ModelSummaryFacts>(IsCompactProperty);
    }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>Columns in the current layout.</summary>
    public int Columns => IsCompact ? 2 : 4;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            UpdateDividers();
        }
    }

    protected override void ChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.ChildrenChanged(sender, e);
        UpdateDividers();
    }

    private void UpdateDividers()
    {
        var columns = Columns;
        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is ModelSummaryFact fact)
            {
                fact.Dividers = new Thickness(i % columns == 0 ? 0 : 1, i >= columns ? 1 : 0, 0, 0);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var columns = Columns;
        var finite = !double.IsInfinity(availableSize.Width);
        var cell = new Size(finite ? availableSize.Width / columns : double.PositiveInfinity, double.PositiveInfinity);
        var widest = 0.0;
        var height = 0.0;
        var row = 0.0;
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(cell);
            widest = Math.Max(widest, child.DesiredSize.Width);
            row = Math.Max(row, child.DesiredSize.Height);
            if (i % columns == columns - 1 || i == Children.Count - 1)
            {
                height += row;
                row = 0;
            }
        }
        return new Size(finite ? availableSize.Width : widest * columns, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Columns;
        var width = finalSize.Width / columns;
        // Column edges snap to device pixels from the left edge, so neighbours share their edge exactly
        // (438 / 4 = 109.5 would otherwise round to overlapping cells).
        var scale = LayoutHelper.GetLayoutScale(this);
        var y = 0.0;
        for (var start = 0; start < Children.Count; start += columns)
        {
            var end = Math.Min(start + columns, Children.Count);
            var row = 0.0;
            for (var i = start; i < end; i++)
            {
                row = Math.Max(row, Children[i].DesiredSize.Height);
            }
            for (var i = start; i < end; i++)
            {
                var column = i - start;
                var left = Snap(column * width, scale);
                var right = column == columns - 1 ? finalSize.Width : Snap((column + 1) * width, scale);
                Children[i].Arrange(new Rect(left, y, right - left, row));
            }
            y += row;
        }
        return finalSize;
    }

    private static double Snap(double value, double scale) => Math.Round(value * scale, MidpointRounding.AwayFromZero) / scale;

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupPeer(this);

    /// <summary>The four facts read as one group (Arc's description list).</summary>
    private sealed class GroupPeer(ModelSummaryFacts owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;
    }
}

/// <summary>
/// One fact of a <see cref="ModelSummary"/>: a small label over a value that crossfades when its text changes (a
/// value that stays the same does not move). Screen readers get the pair as one element ("Context: 800K tokens"),
/// named by the owner; the label and value inside are not read separately.
/// </summary>
[TemplatePart(ValuePart, typeof(ArcContentSwap))]
internal sealed class ModelSummaryFact : TemplatedControl
{
    public const string ValuePart = "PART_Value";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ModelSummaryFact, string?>(nameof(Label));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ModelSummaryFact, string?>(nameof(Value));

    /// <summary>The 1px dividers this cell draws: left after the first column, top from the second row on.</summary>
    public static readonly StyledProperty<Thickness> DividersProperty =
        AvaloniaProperty.Register<ModelSummaryFact, Thickness>(nameof(Dividers));

    static ModelSummaryFact()
    {
        FocusableProperty.OverrideDefaultValue<ModelSummaryFact>(false);
    }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public Thickness Dividers { get => GetValue(DividersProperty); set => SetValue(DividersProperty, value); }

    /// <summary>The value's crossfade host (template part).</summary>
    public ArcContentSwap? ValueSwap { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ValueSwap = e.NameScope.Find<ArcContentSwap>(ValuePart);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FactPeer(this);

    /// <summary>A leaf text element: its name holds the label and the value.</summary>
    private sealed class FactPeer(ModelSummaryFact owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>The content of the change line: one per <see cref="ModelSummary.ShowNotice"/> call, so even the same text crossfades in again.</summary>
internal sealed class ModelSummaryNoticeContent(string text)
{
    public string Text { get; } = text;

    public override string ToString() => Text;
}

/// <summary>The change line itself: a green check and the text, centered in the fixed 20px slot.</summary>
internal sealed class ModelSummaryNoticeLine : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ModelSummaryNoticeLine, string?>(nameof(Text));

    static ModelSummaryNoticeLine()
    {
        FocusableProperty.OverrideDefaultValue<ModelSummaryNoticeLine>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override AutomationPeer OnCreateAutomationPeer() => new NoticePeer(this);

    /// <summary>Read as its text (the check is decoration).</summary>
    private sealed class NoticePeer(ModelSummaryNoticeLine owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => ((ModelSummaryNoticeLine)Owner).Text;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
