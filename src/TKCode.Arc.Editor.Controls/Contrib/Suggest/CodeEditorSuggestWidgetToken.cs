using Avalonia;

namespace TKCode.Arc;

/// <summary>Geometry of the suggest widget of <see cref="CodeEditor"/> that follows neither the theme nor the accent.</summary>
[ControlDesignToken]
internal sealed class CodeEditorSuggestWidgetToken : AbstractControlDesignToken
{
    /// <summary>The width of the list (Monaco's default suggest widget width, 430).</summary>
    public double ListWidth { get; set; }

    /// <summary>The width of the details pane beside the list.</summary>
    public double DetailsWidth { get; set; }

    /// <summary>The height of one row of the list.</summary>
    public double RowHeight { get; set; }

    /// <summary>The most rows the list shows before it scrolls (Monaco's maxVisibleSuggestions, 12).</summary>
    public int MaxVisibleItems { get; set; }

    /// <summary>The size of the kind icons.</summary>
    public double IconSize { get; set; }

    /// <summary>The stroke width of the kind icons in their 24 × 24 box.</summary>
    public double IconStrokeWidth { get; set; }

    /// <summary>The space left and right inside a row.</summary>
    public double RowPadding { get; set; }

    /// <summary>The space between the icon, the label and the details of a row.</summary>
    public double RowSpacing { get; set; }

    /// <summary>The corner radius of the focused row.</summary>
    public double RowCornerRadius { get; set; }

    /// <summary>The space between the frame and the rows.</summary>
    public Thickness Padding { get; set; }

    /// <summary>The space inside the details pane.</summary>
    public Thickness DetailsPadding { get; set; }

    /// <summary>The largest height of the details pane.</summary>
    public double DetailsMaxHeight { get; set; }

    /// <inheritdoc />
    public override void CalculateTokenValues(bool isDarkMode)
    {
        base.CalculateTokenValues(isDarkMode);
        ListWidth = 430;
        DetailsWidth = 320;
        RowHeight = 24;
        MaxVisibleItems = 12;
        IconSize = 14;
        IconStrokeWidth = 2;
        RowPadding = 6;
        RowSpacing = 6;
        RowCornerRadius = 4;
        Padding = new Thickness(4);
        DetailsPadding = new Thickness(10, 8);
        DetailsMaxHeight = 300;
    }
}
