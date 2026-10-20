using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>The bare text box inside an expanded <see cref="ExpandingSearch"/> (no frame of its own).</summary>
internal sealed class ExpandingSearchInput : AvaloniaTextBox
{
    protected override Type StyleKeyOverride => typeof(ExpandingSearchInput);
}

/// <summary>The invisible button that covers the collapsed circle.</summary>
internal sealed class ExpandingSearchTrigger : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(ExpandingSearchTrigger);
}

/// <summary>One result row: an optional 18px icon, the title with its match in medium weight, and a meta line.</summary>
internal sealed class ExpandingSearchOption : Border
{
    public ExpandingSearchOption(ExpandingSearchItem item, string query, int index)
    {
        Item = item;
        Index = index;
        Background = Brushes.Transparent;
        MinHeight = 48;
        Padding = new Thickness(12, 6);
        CornerRadius = new CornerRadius(20);
        Cursor = new Cursor(StandardCursorType.Hand);
        var row = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        if (item.Icon is { } icon)
        {
            Control iconControl = icon is Geometry geometry
                ? new ArcGlyph { Data = geometry, Size = 18, StrokeThickness = 1.75 }
                : new ContentControl { Content = icon };
            Icon = iconControl;
            _iconBrush = ArcTokens.Bind(iconControl, TextElement.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
            var iconHost = new Border { Width = 18, Margin = new Thickness(0, 0, 12, 0), Child = iconControl, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(iconHost, Dock.Left);
            row.Children.Add(iconHost);
        }
        var text = new StackPanel();
        Title = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 14 * 1.4 };
        ArcTokens.Bind(Title, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(Title, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeSM);
        SetTitle(item.Title, query);
        text.Children.Add(Title);
        if (!string.IsNullOrEmpty(item.Meta))
        {
            var meta = new TextBlock { Text = item.Meta, TextTrimming = TextTrimming.CharacterEllipsis, LineHeight = 12 * 1.4 };
            ArcTokens.Bind(meta, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(meta, TextBlock.FontSizeProperty, DesignTokens.ArcDesignTokenKind.TypeXS);
            text.Children.Add(meta);
        }
        row.Children.Add(text);
        Child = row;
        Avalonia.Automation.AutomationProperties.SetName(this, item.Meta is null ? item.Title : $"{item.Title}, {item.Meta}");
    }

    public ExpandingSearchItem Item { get; }

    public int Index { get; }

    public TextBlock Title { get; }

    public Control? Icon { get; }

    private IDisposable? _iconBrush;
    private bool _active;

    private void SetTitle(string title, string query)
    {
        var at = query.Length > 0 ? title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) : -1;
        if (at < 0)
        {
            Title.Text = title;
            return;
        }
        Title.Inlines = new InlineCollection
        {
            new Run(title[..at]),
            new Run(title.Substring(at, query.Length)) { FontWeight = FontWeight.Medium },
            new Run(title[(at + query.Length)..])
        };
    }

    public void SetActive(bool active)
    {
        if (Icon is not null && active != _active)
        {
            _active = active;
            _iconBrush?.Dispose();
            _iconBrush = ArcTokens.Bind(Icon, TextElement.ForegroundProperty,
                active ? DesignTokens.ArcDesignTokenKind.Foreground : DesignTokens.ArcDesignTokenKind.TextMuted);
        }
    }
}
