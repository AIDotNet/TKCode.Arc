using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The surface of a collapsed group's drop-down: the group's commands at their own size, with the group's name under them.
/// Built in code (it lives in a pop-up); colors, radius and spacing come from tokens.
/// </summary>
internal sealed class RibbonGroupDropDown : Border
{
    private readonly TextBlock _header;

    public RibbonGroupDropDown()
    {
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        Bind(PaddingProperty, this.GetResourceObservable(RibbonGroupTokens.Key(RibbonGroupTokenKind.DropDownPadding)));
        _header = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        ArcTokens.Bind(_header, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_header, TextBlock.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        _header.Bind(TextBlock.FontSizeProperty, _header.GetResourceObservable(RibbonGroupTokens.Key(RibbonGroupTokenKind.HeaderFontSize)));
        _header.Bind(TextBlock.MarginProperty, _header.GetResourceObservable(RibbonGroupTokens.Key(RibbonGroupTokenKind.HeaderMargin)));
        DockPanel.SetDock(_header, Dock.Bottom);
        var dock = new DockPanel();
        dock.Children.Add(_header);
        dock.Children.Add(Slot);
        Child = dock;
    }

    public RibbonVisualSlot Slot { get; } = new();

    public void SetHeader(string? header) => _header.Text = header;
}
