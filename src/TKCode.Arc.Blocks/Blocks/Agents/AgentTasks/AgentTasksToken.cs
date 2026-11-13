using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of <see cref="AgentTasks"/>. Colors, radii and fonts come from ArcDesign tokens in the theme.</summary>
[ControlDesignToken]
internal sealed class AgentTasksToken : AbstractControlDesignToken
{
    /// <summary>Padding of the board (20).</summary>
    public Thickness BoardPadding { get; set; }

    /// <summary>At or below this width the columns stack (720).</summary>
    public double CompactWidth { get; set; }

    /// <summary>Padding inside a column (8) and between its cards (8).</summary>
    public Thickness ColumnPadding { get; set; }

    /// <inheritdoc cref="ColumnPadding"/>
    public double CardGap { get; set; }

    /// <summary>Space between columns (12).</summary>
    public Thickness ColumnMargin { get; set; }

    /// <summary>Padding of a card (12).</summary>
    public Thickness CardPadding { get; set; }

    /// <summary>The height of a card's progress line (4).</summary>
    public double ProgressHeight { get; set; }

    /// <summary>Padding of a filter chip (10, 3).</summary>
    public Thickness ChipPadding { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        BoardPadding = new Thickness(20);
        CompactWidth = 720;
        ColumnPadding = new Thickness(8);
        CardGap = 8;
        ColumnMargin = new Thickness(6);
        CardPadding = new Thickness(12);
        ProgressHeight = 4;
        ChipPadding = new Thickness(10, 3);
    }
}
