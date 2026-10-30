using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays out the three parts of <see cref="OrbitLogos"/>: the category chips (first child), the orbit (second) and the
/// detail panel (third). From <see cref="SideBySideWidth"/> up the orbit sits on the left and the chips and the detail
/// panel stack on the right; below it the chips, the orbit and the detail panel stack in one column.
/// </summary>
internal sealed class OrbitLogosLayout : Panel
{
    /// <summary>From this width the orbit and the detail panel sit side by side.</summary>
    public const double SideBySideWidth = 760;

    private const double ColumnGap = 32;
    private const double RowGap = 16;

    private bool _sideBySide;
    private double _stageSide;

    /// <summary>Whether the last measure chose the side-by-side arrangement.</summary>
    public bool IsSideBySide => _sideBySide;

    private Control Chips => (Control)Children[0];

    private Control Stage => (Control)Children[1];

    private Control Detail => (Control)Children[2];

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 3)
        {
            return default;
        }
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : OrbitLogosStage.MaxSize;
        _sideBySide = width >= SideBySideWidth;
        var chips = Chips;
        var stage = Stage;
        var detail = Detail;
        if (_sideBySide)
        {
            var stageWidth = Math.Min(OrbitLogosStage.MaxSize, width * 0.58);
            var columnWidth = Math.Max(0, width - stageWidth - ColumnGap);
            stage.Measure(new Size(stageWidth, double.PositiveInfinity));
            chips.Measure(new Size(columnWidth, double.PositiveInfinity));
            detail.Measure(new Size(columnWidth, double.PositiveInfinity));
            _stageSide = stage.DesiredSize.Width;
            var column = (chips.IsVisible ? chips.DesiredSize.Height + RowGap : 0) + detail.DesiredSize.Height;
            return new Size(width, Math.Max(stage.DesiredSize.Height, column));
        }

        chips.Measure(new Size(width, double.PositiveInfinity));
        stage.Measure(new Size(Math.Min(width, OrbitLogosStage.MaxSize), double.PositiveInfinity));
        detail.Measure(new Size(width, double.PositiveInfinity));
        _stageSide = stage.DesiredSize.Width;
        var height = (chips.IsVisible ? chips.DesiredSize.Height + RowGap : 0)
            + stage.DesiredSize.Height + RowGap + detail.DesiredSize.Height;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 3)
        {
            return finalSize;
        }
        var chips = Chips;
        var stage = Stage;
        var detail = Detail;
        if (_sideBySide)
        {
            var stageSize = stage.DesiredSize;
            stage.Arrange(new Rect(0, Math.Max(0, (finalSize.Height - stageSize.Height) / 2), stageSize.Width, stageSize.Height));
            var left = stageSize.Width + ColumnGap;
            var columnWidth = Math.Max(0, finalSize.Width - left);
            var chipsHeight = chips.IsVisible ? chips.DesiredSize.Height : 0;
            var columnHeight = (chips.IsVisible ? chipsHeight + RowGap : 0) + detail.DesiredSize.Height;
            var top = Math.Max(0, (finalSize.Height - columnHeight) / 2);
            if (chips.IsVisible)
            {
                chips.Arrange(new Rect(left, top, columnWidth, chipsHeight));
                top += chipsHeight + RowGap;
            }
            detail.Arrange(new Rect(left, top, columnWidth, detail.DesiredSize.Height));
            return finalSize;
        }

        var y = 0.0;
        if (chips.IsVisible)
        {
            chips.Arrange(new Rect(0, 0, finalSize.Width, chips.DesiredSize.Height));
            y += chips.DesiredSize.Height + RowGap;
        }
        var side = stage.DesiredSize;
        stage.Arrange(new Rect((finalSize.Width - side.Width) / 2, y, side.Width, side.Height));
        y += side.Height + RowGap;
        detail.Arrange(new Rect(0, y, finalSize.Width, detail.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>
/// The detail card: a plain surface whose box the orbit clips while it morphs out of a chip. It reads as a labelled
/// region to assistive technology.
/// </summary>
internal sealed class OrbitLogosCard : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new CardAutomationPeer(this);

    private sealed class CardAutomationPeer(OrbitLogosCard owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => true;

        protected override string? GetNameCore() => AutomationProperties.GetName(Owner);
    }
}
