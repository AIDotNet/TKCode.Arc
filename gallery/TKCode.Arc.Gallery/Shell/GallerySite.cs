using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The pieces the site's pages share (home, StreamMark, settings): section headings, panels, install commands, check lists,
/// and the page width. Radii come from the design tokens, so the corner styles of the theme panel reach them too.
/// </summary>
internal static class GallerySite
{
    /// <summary>The widest a page's column gets.</summary>
    public const double PageMaxWidth = 1180;

    /// <summary>Below this width a page uses the phone's spacing.</summary>
    public const double PhoneWidth = 640;

    /// <summary>The package version of TKCode.StreamMark.* (<c>StreamMarkVersion</c> in Directory.Packages.props).</summary>
    public const string StreamMarkVersion = "0.1.2";

    /// <summary>A label that follows the language.</summary>
    public static TextBlock Text(GalleryShellLangResourceKind kind, ArcDesignTokenKind color = ArcDesignTokenKind.Foreground, double size = 14,
                                 FontWeight weight = FontWeight.Normal)
    {
        var block = GalleryUi.Text(string.Empty, color, size, weight);
        GalleryText.Bind(block, TextBlock.TextProperty, kind);
        return block;
    }

    /// <summary>A formatted label that follows the language.</summary>
    public static TextBlock Format(GalleryShellLangResourceKind kind, ArcDesignTokenKind color, double size, params object[] args)
    {
        var block = GalleryUi.Text(string.Empty, color, size);
        GalleryText.BindFormat(block, TextBlock.TextProperty, kind, args);
        return block;
    }

    /// <summary>A small label above a heading.</summary>
    public static TextBlock Eyebrow(GalleryShellLangResourceKind kind)
    {
        var block = Text(kind, ArcDesignTokenKind.TextMuted, 12.5, FontWeight.Medium);
        ArcTokens.Bind(block, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
        return block;
    }

    /// <summary>A section's heading: an optional eyebrow, the title and an optional sentence.</summary>
    public static StackPanel Heading(GalleryShellLangResourceKind? eyebrow, Control title, GalleryShellLangResourceKind? text = null)
    {
        var stack = new StackPanel { Spacing = 8, MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
        if (eyebrow is { } label)
        {
            stack.Children.Add(Eyebrow(label));
        }
        stack.Children.Add(title);
        if (text is { } sentence)
        {
            stack.Children.Add(Text(sentence, ArcDesignTokenKind.TextSecondary, 15));
        }
        return stack;
    }

    /// <summary>A heading that reveals word by word when it scrolls into view; its text follows the language.</summary>
    public static TextReveal Reveal(GalleryShellLangResourceKind kind, TextLevel level = TextLevel.H2)
    {
        var reveal = new TextReveal { Level = level };
        GalleryText.Bind(reveal, TextReveal.TextProperty, kind);
        return reveal;
    }

    /// <summary>A bordered panel with the theme's panel corners.</summary>
    public static Border Panel(Control child, ArcDesignTokenKind fill = ArcDesignTokenKind.Surface, double padding = 20)
    {
        var panel = new Border { BorderThickness = new Thickness(1), Padding = new Thickness(padding), Child = child };
        ArcTokens.Bind(panel, Border.BackgroundProperty, fill);
        ArcTokens.Bind(panel, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(panel, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusPanel);
        return panel;
    }

    /// <summary>A command to copy. The command wraps at its spaces on a phone instead of pushing the page wider.</summary>
    public static Control Install(string command)
    {
        var text = GalleryUi.Mono($"$ {command}", ArcDesignTokenKind.TextSecondary, 13);
        text.TextWrapping = TextWrapping.Wrap;
        text.VerticalAlignment = VerticalAlignment.Center;
        var copy = new CopyButton { Value = command, IsIconOnly = true, VerticalAlignment = VerticalAlignment.Center };
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(copy, 1);
        row.Children.Add(text);
        row.Children.Add(copy);
        var install = new Border { Padding = new Thickness(14, 4, 6, 4), BorderThickness = new Thickness(1), Child = row };
        ArcTokens.Bind(install, Border.BackgroundProperty, ArcDesignTokenKind.Surface);
        ArcTokens.Bind(install, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(install, Border.CornerRadiusProperty, ArcDesignTokenKind.RadiusControl);
        return install;
    }

    /// <summary>A list of short points, each with a check mark.</summary>
    public static StackPanel Checks(params Control[] points)
    {
        var stack = new StackPanel { Spacing = 8 };
        foreach (var point in points)
        {
            var glyph = GalleryUi.Glyph(ArcIcons.Check, 15, ArcDesignTokenKind.Foreground);
            glyph.VerticalAlignment = VerticalAlignment.Top;
            glyph.Margin = new Thickness(0, 2, 0, 0);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 9 };
            Grid.SetColumn(point, 1);
            row.Children.Add(glyph);
            row.Children.Add(point);
            stack.Children.Add(row);
        }
        return stack;
    }

    /// <summary>A button with a label that follows the language and an optional glyph after it.</summary>
    public static Button Action(GalleryShellLangResourceKind label, ButtonVariant variant, Action click, Geometry? glyph = null, ControlSize size = ControlSize.Medium)
    {
        var button = new Button { Variant = variant, Size = size, Content = Labelled(label, glyph) };
        button.Click += (_, _) => click();
        return button;
    }

    /// <summary>The content of a button: a label and an optional glyph after it.</summary>
    public static Control Labelled(GalleryShellLangResourceKind label, Geometry? glyph)
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(text, TextBlock.TextProperty, label);
        if (glyph is null)
        {
            return text;
        }
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(text);
        row.Children.Add(new ArcGlyph { Data = glyph, Size = 14, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    /// <summary>Buttons in a row that wraps when they do not fit.</summary>
    public static WrapPanel Actions(params Control[] buttons)
    {
        var row = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 10, LineSpacing = 10 };
        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }
        return row;
    }

    /// <summary>Opens an address in the system's browser.</summary>
    public static void Open(Control from, Uri url) => _ = TopLevel.GetTopLevel(from)?.Launcher.LaunchUriAsync(url);

    /// <summary>The page of the docs entry <paramref name="className"/>.</summary>
    public static GalleryRoute Docs(string className) => GalleryRoute.To(GalleryCatalog.Docs.Entries.First(entry => entry.ClassName == className));

    /// <summary>A file size as people read it.</summary>
    public static string FormatSize(long bytes) => $"{bytes / (1024.0 * 1024.0):0} MB";

    /// <summary>
    /// A page's scrolling column: full width on a phone with the phone's margins, centered under <see cref="PageMaxWidth"/>
    /// otherwise.
    /// </summary>
    public static ScrollViewer Page(StackPanel column, out Border margins)
    {
        column.MaxWidth = PageMaxWidth;
        column.HorizontalAlignment = HorizontalAlignment.Stretch;
        var border = new Border { Padding = new Thickness(24, 24, 24, 0), Child = column };
        margins = border;
        void Fit(double width)
        {
            var phone = width < PhoneWidth;
            border.Padding = phone ? new Thickness(16, 16, 16, 0) : new Thickness(24, 24, 24, 0);
            column.Spacing = phone ? 56 : 80;
        }
        border.SizeChanged += (_, e) => Fit(e.NewSize.Width);
        Fit(double.PositiveInfinity);
        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            BringIntoViewOnFocusChange = false,
            Content = border
        };
    }
}
