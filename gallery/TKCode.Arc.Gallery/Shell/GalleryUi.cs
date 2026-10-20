using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// One example on a page: a title, a sentence of guidance and a stage with the live control. Sections are
/// what "On this page" lists, so every example on a page should be a <see cref="GalleryUi.Card"/>.
/// </summary>
public sealed class GallerySection : Border
{
    public GallerySection(string title)
    {
        Title = title;
    }

    public string Title { get; }
}

/// <summary>Small helpers that build gallery chrome and example pages with Arc tokens.</summary>
public static class GalleryUi
{
    public static TextBlock Text(string text, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground, double size = 14,
                                 FontWeight weight = FontWeight.Normal)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, size >= 28 ? ArcDesignTokenKind.DisplayFontFamily : ArcDesignTokenKind.BodyFontFamily);
        return block;
    }

    public static TextBlock Mono(string text, ArcDesignTokenKind color = ArcDesignTokenKind.TextSecondary, double size = 13)
    {
        var block = new TextBlock { Text = text, FontSize = size, TextWrapping = TextWrapping.NoWrap };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        return block;
    }

    /// <summary>A Lucide glyph colored by an ArcDesign token.</summary>
    public static ArcGlyph Glyph(Geometry data, double size = 16, ArcDesignTokenKind color = ArcDesignTokenKind.TextMuted)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    /// <summary>A rounded chip with a muted fill, for counts, phases and status.</summary>
    public static Border Chip(Control content, ArcDesignTokenKind fill = ArcDesignTokenKind.SurfaceMuted, Thickness? padding = null)
    {
        var chip = new Border
        {
            Padding = padding ?? new Thickness(8, 2),
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Center,
            Child = content
        };
        ArcTokens.Bind(chip, Border.BackgroundProperty, fill);
        return chip;
    }

    /// <summary>
    /// One example: title and guidance above a stage (the muted rounded panel Arc previews live on).
    /// Rows center themselves on the stage; anything else stretches to its width.
    /// </summary>
    public static GallerySection Card(string title, string? description, Control content)
    {
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(Text(title, ArcDesignTokenKind.Foreground, 18, FontWeight.Medium));
        if (!string.IsNullOrEmpty(description))
        {
            header.Children.Add(Text(description, ArcDesignTokenKind.TextSecondary, 14));
        }

        var stage = new Border
        {
            Padding = new Thickness(32, 40),
            CornerRadius = new CornerRadius(26),
            BorderThickness = new Thickness(1),
            Child = content
        };
        ArcTokens.Bind(stage, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(stage, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var stack = new StackPanel { Spacing = 16 };
        stack.Children.Add(header);
        stack.Children.Add(stage);
        return new GallerySection(title) { Child = stack };
    }

    public static WrapPanel Row(params Control[] children)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = 12,
            LineSpacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }
        return panel;
    }

    public static StackPanel Column(double spacing, params Control[] children)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var child in children)
        {
            panel.Children.Add(child);
        }
        return panel;
    }

    /// <summary>The 1px vertical rail that rail items hang from.</summary>
    public static Border Rail(Control child, double inset)
    {
        var rail = new Border { BorderThickness = new Thickness(1, 0, 0, 0), Margin = new Thickness(inset, 0, 0, 0), Child = child };
        ArcTokens.Bind(rail, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return rail;
    }
}
