using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Helpers for the select menus' popup content, which the fields build on the first open instead of with every template
/// (a form full of selects that are never opened paid for all of it). The values are those the templates declared.
/// </summary>
internal static class SelectMenuParts
{
    /// <summary>Binds a control token resource, at template priority like a value declared in a template.</summary>
    public static T Token<T>(this T target, AvaloniaProperty property, object key) where T : Control
    {
        target.Bind(property, target.GetResourceObservable(key), BindingPriority.Template);
        return target;
    }

    /// <summary>Binds a theme token.</summary>
    public static T Token<T>(this T target, AvaloniaProperty property, ArcDesignTokenKind kind) where T : Control
    {
        ArcTokens.Bind(target, property, kind);
        return target;
    }

    /// <summary>The raised panel around a menu; the caller binds its padding and corner radius.</summary>
    public static Border Surface(Control child) =>
        new Border { Name = "PART_Surface", Child = child }
            .Token(Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness)
            .Token(Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised)
            .Token(Border.BorderBrushProperty, ArcDesignTokenKind.Border);

    /// <summary>The options list with what every field's menu shares; the caller binds the row geometry.</summary>
    public static SelectOptionView Options(SelectCheckMode mode) =>
        new SelectOptionView { Name = "PART_Options", CheckMode = mode }
            .Token(SelectOptionView.HighlightBrushProperty, ArcDesignTokenKind.SurfaceMuted)
            .Token(SelectOptionView.ForegroundProperty, ArcDesignTokenKind.Foreground)
            .Token(SelectOptionView.CheckForegroundProperty, ArcDesignTokenKind.Foreground);

    /// <summary>A centered 16px muted glyph.</summary>
    public static ArcGlyph Glyph(Geometry data) =>
        new ArcGlyph
        {
            Data = data,
            Size = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }.Token(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextMuted);
}
