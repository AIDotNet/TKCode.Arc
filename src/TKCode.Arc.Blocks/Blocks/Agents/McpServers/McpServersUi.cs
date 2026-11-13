using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>Builders for the code-built parts of <see cref="McpServers"/>: every color, size and radius bound to tokens.</summary>
internal static class McpServersUi
{
    public static TextBlock Text(string? text, ArcDesignTokenKind color, ArcDesignTokenKind size = ArcDesignTokenKind.TypeXS,
        bool mono = false, bool medium = false, double lineHeight = 16.8)
    {
        var block = new TextBlock { Text = text, LineHeight = lineHeight, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        if (medium)
        {
            ArcTokens.Bind(block, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        }
        if (mono)
        {
            ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        }
        return block;
    }

    public static ArcGlyph Glyph(Geometry data, double size, ArcDesignTokenKind color)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, color);
        return glyph;
    }

    public static Button Button(ButtonVariant variant, string text)
    {
        var button = new Button { Variant = variant, Size = ControlSize.Small, Content = text };
        AutomationProperties.SetName(button, text);
        return button;
    }

    public static void SetText(Button button, string text)
    {
        button.Content = text;
        AutomationProperties.SetName(button, text);
    }

    /// <summary>A small tag: a word in a 1px bordered pill, optionally after a glyph.</summary>
    public static Border Tag(string text, Geometry? icon = null)
    {
        var label = Text(text, ArcDesignTokenKind.TextSecondary, lineHeight: 16);
        label.FontSize = 11;
        object content = label;
        if (icon is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(Glyph(icon, 11, ArcDesignTokenKind.TextSecondary));
            row.Children.Add(label);
            content = row;
        }
        var tag = new Border
        {
            Padding = new Thickness(6, 1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = (Control)content,
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(tag, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(tag, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return tag;
    }

    public static Border Divider(ArcDesignTokenKind color = ArcDesignTokenKind.BorderSubtle)
    {
        var line = new Border { Height = 1, IsHitTestVisible = false };
        ArcTokens.Bind(line, Border.BackgroundProperty, color);
        return line;
    }
}

/// <summary>Lucide glyphs <see cref="McpServers"/> uses that Core does not ship.</summary>
internal static class McpServersGlyphs
{
    /// <summary>Lucide <c>pencil-line</c>: the "Changes data" tag.</summary>
    public static readonly Geometry PencilLine = ArcIcons.Parse(
        "M12 20h9 M16.376 3.622a1 1 0 0 1 3.002 3.002L7.368 18.635a2 2 0 0 1-.855.506l-2.872.838a.5.5 0 0 1-.62-.62l.838-2.872a2 2 0 0 1 .506-.854z M15 5l3 3");

    /// <summary>Lucide <c>plug</c>: the empty list.</summary>
    public static readonly Geometry Plug = ArcIcons.Parse(
        "M12 22v-5 M9 8V2 M15 8V2 M18 8v5a4 4 0 0 1-4 4h-4a4 4 0 0 1-4-4V8Z");
}
