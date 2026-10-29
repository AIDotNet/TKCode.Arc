using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Geometry and the moving average colors of <see cref="MarketTerminal"/> (sizes from Arc Market terminal, design reference).</summary>
[ControlDesignToken]
internal sealed class MarketTerminalToken : AbstractControlDesignToken
{
    /// <summary>Gap between the cards.</summary>
    public double CardGap { get; set; }

    /// <summary>Space inside the chart card.</summary>
    public Thickness ChartCardPadding { get; set; }

    /// <summary>Width of the watchlist and ticket column beside the chart.</summary>
    public double SideColumnWidth { get; set; }

    /// <summary>Block widths from which the side column sits beside the chart, the two cards sit side by side, and the toolbar turns compact.</summary>
    public double WideThreshold { get; set; }

    public double MediumThreshold { get; set; }

    public double CompactThreshold { get; set; }

    /// <summary>Chart height when it does not stretch beside the side column, and in the compact layout.</summary>
    public double ChartHeight { get; set; }

    public double CompactChartHeight { get; set; }

    /// <summary>Width of the price axis.</summary>
    public double ChartGutter { get; set; }

    /// <summary>Space around a card's title line (watchlist, ticket).</summary>
    public Thickness CardHeaderPadding { get; set; }

    /// <summary>Space around the watchlist rows.</summary>
    public Thickness WatchListPadding { get; set; }

    /// <summary>Space inside a watchlist row, its corner radius and its columns.</summary>
    public Thickness WatchRowPadding { get; set; }

    public CornerRadius WatchRowRadius { get; set; }

    public double SparklineWidth { get; set; }

    public double SparklineHeight { get; set; }

    public double WatchPriceColumnWidth { get; set; }

    /// <summary>Space inside the ticket under its title, and between its parts.</summary>
    public Thickness TicketPadding { get; set; }

    public double TicketGap { get; set; }

    /// <summary>Width of the shares and limit fields.</summary>
    public double TicketFieldWidth { get; set; }

    /// <summary>Space around the order list.</summary>
    public Thickness OrdersPadding { get; set; }

    /// <summary>Height of an order row.</summary>
    public double OrderRowHeight { get; set; }

    /// <summary>Height of a toolbar button (grows on touch layouts).</summary>
    public double ToolHeight { get; set; }

    public double CompactToolHeight { get; set; }

    /// <summary>The 20 and 50 candle moving averages: a blue and an amber that read on light and dark surfaces (accent independent).</summary>
    public Color Ma20Color { get; set; }

    public Color Ma50Color { get; set; }

    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        CardGap = 16;
        ChartCardPadding = new Thickness(20);
        SideColumnWidth = 340;
        WideThreshold = 1080;
        MediumThreshold = 720;
        CompactThreshold = 640;
        ChartHeight = 420;
        CompactChartHeight = 340;
        ChartGutter = 70;
        CardHeaderPadding = new Thickness(20, 16, 20, 8);
        WatchListPadding = new Thickness(8, 0, 8, 8);
        WatchRowPadding = new Thickness(12, 8);
        WatchRowRadius = new CornerRadius(18);
        SparklineWidth = 64;
        SparklineHeight = 28;
        WatchPriceColumnWidth = 84;
        TicketPadding = new Thickness(20, 8, 20, 20);
        TicketGap = 16;
        TicketFieldWidth = 196;
        OrdersPadding = new Thickness(20, 12, 20, 16);
        OrderRowHeight = 48;
        ToolHeight = 36;
        CompactToolHeight = 44;
        Ma20Color = isDarkMode ? Oklch.ToColor(0.70, 0.14, 250) : Oklch.ToColor(0.58, 0.15, 250);
        Ma50Color = isDarkMode ? Oklch.ToColor(0.76, 0.16, 70) : Oklch.ToColor(0.72, 0.18, 74);
    }
}
