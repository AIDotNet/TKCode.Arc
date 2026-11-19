using Avalonia.Controls;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// The surface the selected tab shows on while a collapsed ribbon is open: the ribbon's command area in a pop-up under the
/// tab row. Built in code (it lives in a pop-up); colors and radius come from tokens.
/// </summary>
internal sealed class RibbonPopupSurface : Border
{
    public RibbonPopupSurface()
    {
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        Bind(PaddingProperty, this.GetResourceObservable(RibbonTokens.Key(RibbonTokenKind.ContentPadding)));
        Child = Slot;
    }

    public RibbonVisualSlot Slot { get; } = new();
}
