using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Token bindings that change with state (a chosen mark turns accent): one live binding per element and property, the
/// previous one disposed when the token changes, so state changes never stack bindings.
/// </summary>
internal sealed class SupportKitBindings
{
    private readonly Dictionary<(StyledElement, AvaloniaProperty), IDisposable> _live = new();

    public void Rebind(StyledElement target, AvaloniaProperty property, ArcDesignTokenKind kind)
    {
        if (_live.Remove((target, property), out var old))
        {
            old.Dispose();
        }
        _live[(target, property)] = ArcTokens.Bind(target, property, kind);
    }
}

/// <summary>
/// Builders shared by the Support blocks (and <see cref="ActivityFeed"/>): text, glyphs, panels and pills whose colors,
/// type sizes and radii are bound to ArcDesign tokens, plus the relative-time wording the blocks share.
/// </summary>
internal static class SupportKit
{
    /// <summary>Lucide <c>life-buoy</c>.</summary>
    public static readonly Geometry LifeBuoy = ArcIcons.Parse(
        "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20 M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8 M4.93 4.93l4.24 4.24 M14.83 9.17l4.24-4.24 M14.83 14.83l4.24 4.24 M9.17 14.83l-4.24 4.24");

    /// <summary>Lucide <c>user-plus</c>.</summary>
    public static readonly Geometry UserPlus = ArcIcons.Parse(
        "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M5 7a4 4 0 1 0 8 0a4 4 0 1 0-8 0 M19 8v6 M22 11h-6");

    /// <summary>Lucide <c>sticky-note</c> (internal notes).</summary>
    public static readonly Geometry StickyNote = ArcIcons.Parse(
        "M16 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V8Z M15 3v4a2 2 0 0 0 2 2h4");

    /// <summary>Lucide <c>rocket</c> (deploys).</summary>
    public static readonly Geometry Rocket = ArcIcons.Parse(
        "M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09z M12 15l-3-3a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.35 22.35 0 0 1-4 2z M9 12H4s.55-3.03 2-4c1.62-1.08 5 0 5 0 M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5");

    public static TextBlock Text(string? text, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground,
        ArcDesignTokenKind size = ArcDesignTokenKind.TypeSM, FontWeight weight = FontWeight.Normal, bool wrap = false, bool trim = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontWeight = weight,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = trim ? TextTrimming.CharacterEllipsis : TextTrimming.None,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ArcTokens.Bind(block, TextBlock.ForegroundProperty, color);
        ArcTokens.Bind(block, TextBlock.FontSizeProperty, size);
        return block;
    }

    /// <summary>A display-font heading at <paramref name="size"/>, marked as a heading for screen readers.</summary>
    public static TextBlock Heading(string? text, ArcDesignTokenKind size = ArcDesignTokenKind.TypeBase, int level = 2)
    {
        var block = Text(text, ArcDesignTokenKind.Foreground, size, FontWeight.SemiBold, wrap: true);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        AutomationProperties.SetHeadingLevel(block, level);
        return block;
    }

    public static ArcGlyph Glyph(Geometry data, double size = 16, ArcDesignTokenKind? color = null)
    {
        var glyph = new ArcGlyph { Data = data, Size = size, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        if (color is { } kind)
        {
            ArcTokens.Bind(glyph, ArcGlyph.ForegroundProperty, kind);
        }
        return glyph;
    }

    /// <summary>A bordered panel on <paramref name="fill"/> with a token radius.</summary>
    public static Border Panel(Control? child, ArcDesignTokenKind radius = ArcDesignTokenKind.RadiusStep16,
        ArcDesignTokenKind fill = ArcDesignTokenKind.Surface, Thickness? padding = null, bool edge = true)
    {
        var border = new Border { Child = child, Padding = padding ?? new Thickness(16) }.BindCornerRadius(radius);
        ArcTokens.Bind(border, Border.BackgroundProperty, fill);
        if (edge)
        {
            ArcTokens.Bind(border, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(border, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        }
        return border;
    }

    /// <summary>A one-pixel line in <see cref="ArcDesignTokenKind.BorderSubtle"/>.</summary>
    public static Border Rule(bool vertical = false)
    {
        var rule = new Border { IsHitTestVisible = false };
        if (vertical)
        {
            rule.Width = 1;
        }
        else
        {
            rule.Height = 1;
        }
        ArcTokens.Bind(rule, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
        return rule;
    }

    /// <summary>A small pill label (a status, a tag) on <paramref name="fill"/> with <paramref name="color"/> text.</summary>
    public static Border Pill(string text, ArcDesignTokenKind fill = ArcDesignTokenKind.SurfaceMuted,
        ArcDesignTokenKind color = ArcDesignTokenKind.TextSecondary, Geometry? icon = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (icon is not null)
        {
            row.Children.Add(Glyph(icon, 12, color));
        }
        var label = Text(text, color, ArcDesignTokenKind.TypeXS, FontWeight.Medium);
        row.Children.Add(label);
        var pill = new Border
        {
            Child = row,
            Padding = new Thickness(8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(pill, Border.BackgroundProperty, fill);
        return pill;
    }

    public static Ellipse Dot(ArcDesignTokenKind color, double size = 8)
    {
        var dot = new Ellipse { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        ArcTokens.Bind(dot, Shape.FillProperty, color);
        return dot;
    }

    public static Button Button(string label, ButtonVariant variant = ButtonVariant.Secondary, Geometry? icon = null,
        ControlSize size = ControlSize.Small, string? name = null)
    {
        object content = label;
        if (icon is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new ArcGlyph { Data = icon, Size = 15, VerticalAlignment = VerticalAlignment.Center });
            if (label.Length > 0)
            {
                row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            }
            content = row;
        }
        var button = new Button { Variant = variant, Size = size, Content = content };
        AutomationProperties.SetName(button, name ?? label);
        return button;
    }

    /// <summary>A square ghost button that shows only <paramref name="icon"/>; <paramref name="name"/> is its accessible name and tooltip.</summary>
    public static Button IconButton(Geometry icon, string name, ControlSize size = ControlSize.Small)
    {
        var button = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = size,
            Content = new ArcGlyph { Data = icon, Size = 16, VerticalAlignment = VerticalAlignment.Center },
        };
        AutomationProperties.SetName(button, name);
        Tooltip.SetTip(button, name);
        return button;
    }

    /// <summary>A grid with the given column and row definitions (strings such as "Auto,*").</summary>
    public static Grid Grid(string columns, string? rows = null, double columnSpacing = 0, double rowSpacing = 0)
    {
        var grid = new Grid { ColumnDefinitions = ColumnDefinitions.Parse(columns), ColumnSpacing = columnSpacing, RowSpacing = rowSpacing };
        if (rows is not null)
        {
            grid.RowDefinitions = RowDefinitions.Parse(rows);
        }
        return grid;
    }

    public static T At<T>(this T control, int column, int row = 0, int columnSpan = 1) where T : Control
    {
        Avalonia.Controls.Grid.SetColumn(control, column);
        Avalonia.Controls.Grid.SetRow(control, row);
        if (columnSpan > 1)
        {
            Avalonia.Controls.Grid.SetColumnSpan(control, columnSpan);
        }
        return control;
    }

    /// <summary>"now", "5m", "3h", "Yesterday", a weekday within a week, else "Mar 4" (in <paramref name="culture"/>).</summary>
    public static string Ago(DateTimeOffset at, DateTimeOffset now, CultureInfo culture)
    {
        var span = now - at;
        if (span < TimeSpan.FromMinutes(1))
        {
            return "now";
        }
        if (span < TimeSpan.FromHours(1))
        {
            return $"{(int)span.TotalMinutes}m";
        }
        if (at.Date == now.Date)
        {
            return $"{(int)span.TotalHours}h";
        }
        if (at.Date == now.Date.AddDays(-1))
        {
            return "Yesterday";
        }
        if (span < TimeSpan.FromDays(7))
        {
            return at.ToString("ddd", culture);
        }
        return at.ToString("MMM d", culture);
    }

    /// <summary>The words of <paramref name="query"/> all occur in <paramref name="text"/> (case-insensitive).</summary>
    public static bool Matches(string? text, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        foreach (var word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (text.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>A plural-aware count phrase in English ("1 article", "3 articles").</summary>
    public static string Count(int count, string one, string many) => $"{count.ToString("N0", CultureInfo.InvariantCulture)} {(count == 1 ? one : many)}";

    /// <summary>Puts <paramref name="child"/> into <paramref name="host"/> (taking it out of a previous host first).</summary>
    public static void Mount(Decorator? host, Control child)
    {
        if (host is null || ReferenceEquals(host.Child, child))
        {
            return;
        }
        if (child.Parent is Decorator old)
        {
            old.Child = null;
        }
        host.Child = child;
    }
}
