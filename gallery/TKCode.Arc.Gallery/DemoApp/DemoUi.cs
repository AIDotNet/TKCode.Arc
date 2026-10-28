using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>What a page of the demo can ask of the app around it.</summary>
public interface IDemoHost
{
    /// <summary>The account that is signed in.</summary>
    DemoAccount Account { get; }

    /// <summary>Opens <see cref="DemoIntent.Page"/> and hands it the intent (a view, a section, a project).</summary>
    void Navigate(DemoIntent intent);

    /// <summary>Shows a toast at the bottom of the app and returns its id; an id in the options replaces the toast that has it.</summary>
    string Toast(ToastOptions options);

    /// <summary>True while the sidebar is the 64px icon rail.</summary>
    bool IsSidebarCollapsed { get; }

    /// <summary>Folds the sidebar into the icon rail or opens it again (the <c>[</c> key).</summary>
    void ToggleSidebar();

    /// <summary>Fades the app out, ends the session and opens the sign-in page.</summary>
    Task SignOutAsync();

    /// <summary>Changes a toast that is showing (a loading toast into its result, an Undo into what it undid).</summary>
    void UpdateToast(string id, ToastUpdate update);
}

public static class DemoHostExtensions
{
    public static string Toast(this IDemoHost host, ToastKind kind, string title, string? description = null, string? id = null)
        => host.Toast(new ToastOptions { Kind = kind, Title = title, Description = description, Id = id });
}

/// <summary>How a <see cref="DemoRow"/> lays out its cards once the page is wide enough (<see cref="DemoRow.ColumnsFrom"/>).</summary>
public enum DemoRowLayout
{
    /// <summary>One column.</summary>
    Stack,

    /// <summary>Two equal columns.</summary>
    Split,

    /// <summary>A main column and a side column, 1.7 : 1.</summary>
    MainSide
}

/// <summary>
/// A row of cards on a demo page: one column, or two side by side when the row is at least <see cref="ColumnsFrom"/>
/// wide. Cards in a row line up at the top and share the height of the tallest one.
/// </summary>
public sealed class DemoRow : Panel
{
    public const double ColumnsFrom = 1180;
    public const double Gap = 16;

    public DemoRow(DemoRowLayout layout, string? id, params Control[] cards)
    {
        Layout = layout;
        Id = id;
        Children.AddRange(cards);
    }

    public DemoRowLayout Layout { get; }

    /// <summary>The section a search result or <c>?view=</c> scrolls to (uiarc.dev's <c>#section-&lt;id&gt;</c>).</summary>
    public string? Id { get; }

    private bool IsColumns(double width) => Layout != DemoRowLayout.Stack && Children.Count > 1 && width >= ColumnsFrom;

    private (double First, double Second) Widths(double width)
    {
        var free = Math.Max(0, width - Gap);
        var share = Layout == DemoRowLayout.MainSide ? 1.7 / 2.7 : 0.5;
        return (free * share, free * (1 - share));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = availableSize.Width;
        if (IsColumns(width))
        {
            var (first, second) = Widths(width);
            Children[0].Measure(new Size(first, double.PositiveInfinity));
            Children[1].Measure(new Size(second, double.PositiveInfinity));
            return new Size(width, Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
        }
        double height = 0;
        double widest = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
            height += child.DesiredSize.Height;
            widest = Math.Max(widest, child.DesiredSize.Width);
        }
        height += Gap * Math.Max(0, Children.Count(child => child.IsVisible) - 1);
        return new Size(double.IsInfinity(width) ? widest : width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (IsColumns(finalSize.Width))
        {
            var (first, second) = Widths(finalSize.Width);
            Children[0].Arrange(new Rect(0, 0, first, finalSize.Height));
            Children[1].Arrange(new Rect(first + Gap, 0, second, finalSize.Height));
            return finalSize;
        }
        double y = 0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Arrange(new Rect(0, y, finalSize.Width, child.DesiredSize.Height));
            y += child.DesiredSize.Height + Gap;
        }
        return finalSize;
    }
}

/// <summary>The demo's text, cards and headings, in the sizes of uiarc.dev/demo (a 13px body).</summary>
public static class DemoUi
{
    public const double BodySize = 13;
    public const double CardRadius = 16;

    /// <summary>Text at the body size (13px) unless told otherwise; it wraps.</summary>
    public static TextBlock Text(string text, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground, double size = BodySize,
                                 FontWeight weight = FontWeight.Normal) => GalleryUi.Text(text, color, size, weight);

    /// <summary>Muted secondary text, 12px.</summary>
    public static TextBlock Caption(string text) => GalleryUi.Text(text, ArcDesignTokenKind.TextMuted, 12);

    /// <summary>Text on one line that ends in an ellipsis when it does not fit.</summary>
    public static TextBlock Line(string text, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground, double size = BodySize,
                                 FontWeight weight = FontWeight.Normal)
    {
        var block = GalleryUi.Text(text, color, size, weight);
        block.TextWrapping = TextWrapping.NoWrap;
        block.TextTrimming = TextTrimming.CharacterEllipsis;
        return block;
    }

    public static ArcGlyph Glyph(Geometry data, double size = 16, ArcDesignTokenKind color = ArcDesignTokenKind.TextMuted)
    {
        var glyph = GalleryUi.Glyph(data, size, color);
        glyph.StrokeThickness = 1.75;
        return glyph;
    }

    /// <summary>
    /// A card: 1px border, 16px corners, the surface fill, padding 16 20 20, with a head (title, a muted line and actions
    /// on the right) over its content.
    /// </summary>
    public static Border Panel(string? title, string? subtitle, Control content, Control? actions = null)
    {
        var body = new StackPanel { Spacing = 16 };
        if (title is not null || actions is not null)
        {
            body.Children.Add(Head(title, subtitle, actions, 14));
        }
        body.Children.Add(content);
        return Card(body, new Thickness(20, 16, 20, 20));
    }

    /// <summary>A bordered surface with 16px corners around <paramref name="child"/>.</summary>
    public static Border Card(Control child, Thickness padding)
    {
        var card = new Border
        {
            Padding = padding,
            CornerRadius = new CornerRadius(CardRadius),
            BorderThickness = new Thickness(1),
            Child = child
        };
        ArcTokens.Bind(card, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(card, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        return card;
    }

    /// <summary>A heading row: the title and an optional muted line on the left, actions on the right (min height 32).</summary>
    public static Control Head(string? title, string? subtitle, Control? actions = null, double titleSize = 15)
    {
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        if (title is not null)
        {
            text.Children.Add(Text(title, ArcDesignTokenKind.Foreground, titleSize, FontWeight.Medium));
        }
        if (subtitle is not null)
        {
            text.Children.Add(Text(subtitle, ArcDesignTokenKind.TextMuted, titleSize >= 15 ? 13 : 12));
        }
        var head = new DockPanel { MinHeight = 32 };
        if (actions is not null)
        {
            actions.VerticalAlignment = VerticalAlignment.Center;
            actions.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(actions, Dock.Right);
            head.Children.Add(actions);
        }
        head.Children.Add(text);
        return head;
    }

    /// <summary>A row of controls 8px apart.</summary>
    public static StackPanel Actions(params Control[] controls)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.AddRange(controls);
        return row;
    }

    /// <summary>A small button with an optional leading glyph.</summary>
    public static Button Button(string label, ButtonVariant variant = ButtonVariant.Secondary, Geometry? icon = null,
                                ControlSize size = ControlSize.Small)
    {
        object content = label;
        if (icon is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            row.Children.Add(new ArcGlyph { Data = icon, Size = 14, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
            content = row;
        }
        return new Button { Variant = variant, Size = size, Content = content };
    }

    /// <summary>
    /// A portrait address no avatar can load, so a timeline row shows the person's initials: Timeline draws an avatar only
    /// for an event with a portrait, and the gallery has no photos.
    /// </summary>
    public const string InitialsPortrait = "initials";

    /// <summary>Gives every event that has a person behind it an initials avatar (<see cref="InitialsPortrait"/>).</summary>
    public static IEnumerable<TimelineEvent> WithInitials(IEnumerable<TimelineEvent> events) => events.Select(WithInitials);

    /// <inheritdoc cref="WithInitials(IEnumerable{TimelineEvent})"/>
    public static TimelineEvent WithInitials(TimelineEvent item)
    {
        if (!string.IsNullOrEmpty(item.Actor) && string.IsNullOrEmpty(item.Avatar))
        {
            item.Avatar = InitialsPortrait;
        }
        return item;
    }

    /// <summary>
    /// The 1px border, 16px corners and surface fill that uiarc.dev's blocks draw around themselves; our ports of
    /// RevenueExplorer, CohortRetention, JourneyFlow and UptimeStatus leave the frame to the page.
    /// </summary>
    public static Border Frame(Control block)
    {
        var frame = Card(block, default);
        frame.ClipToBounds = true;
        return frame;
    }

    /// <summary>A 1px rule in the subtle border color.</summary>
    public static Border Rule()
    {
        var rule = new Border { Height = 1 };
        ArcTokens.Bind(rule, Border.BackgroundProperty, ArcDesignTokenKind.BorderSubtle);
        return rule;
    }
}
