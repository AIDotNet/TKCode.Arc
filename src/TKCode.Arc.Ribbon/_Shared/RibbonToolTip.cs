using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The tooltip of a ribbon command: the label as a title, the shortcut beside it, and the description under them.</summary>
internal static class RibbonToolTip
{
    /// <summary>
    /// The tooltip content, or null when there is nothing the button does not already show (a labelled command without a
    /// shortcut or description).
    /// </summary>
    public static object? Create(string? label, string? shortcut, string? description, bool labelShown)
    {
        var hasShortcut = !string.IsNullOrEmpty(shortcut);
        var hasDescription = !string.IsNullOrEmpty(description);
        if (!hasShortcut && !hasDescription)
        {
            return labelShown || string.IsNullOrEmpty(label) ? null : label;
        }
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, MaxWidth = 280 };
        var title = new ArcShrinkRow { Spacing = 8 };
        if (!string.IsNullOrEmpty(label))
        {
            var text = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap };
            ArcTokens.Bind(text, TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
            ArcShrinkRow.SetIsShrinkable(text, true);
            title.Children.Add(text);
        }
        if (hasShortcut)
        {
            title.Children.Add(new TextBlock { Text = shortcut, Opacity = 0.7 });
        }
        if (title.Children.Count > 0)
        {
            panel.Children.Add(title);
        }
        if (hasDescription)
        {
            panel.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Opacity = 0.8 });
        }
        return panel;
    }
}
