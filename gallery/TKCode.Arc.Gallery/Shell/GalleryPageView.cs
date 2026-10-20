using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// One component page, laid out like a uiarc.dev docs page: breadcrumb, title, summary, usage and status, the
/// Preview tab, the examples, and a sticky "On this page" list that follows the scroll position.
/// </summary>
public sealed class GalleryPageView : Grid
{
    private const double MainWidth = 780;
    private const double TocWidth = 196;
    private const double TocMinimumPageWidth = 960;

    private readonly GalleryEntry _entry;
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Border _padding = new();
    private readonly List<GallerySection> _sections;
    private readonly GalleryToc? _toc;
    private MotionValue<double>? _scrollY;

    public GalleryPageView(GalleryEntry entry, Control body, bool implemented)
    {
        _entry = entry;
        _sections = body.GetLogicalDescendants().OfType<GallerySection>().ToList();

        var column = new StackPanel();
        column.Children.Add(BuildBreadcrumb(entry));
        column.Children.Add(Spaced(GalleryUi.Text(entry.Title, ArcDesignTokenKind.Foreground, 44), top: 14));
        var summary = GalleryUi.Text(entry.Description, ArcDesignTokenKind.TextSecondary, 16);
        summary.MaxWidth = 640;
        summary.HorizontalAlignment = HorizontalAlignment.Left;
        column.Children.Add(Spaced(summary, top: 12));
        column.Children.Add(Spaced(BuildUsage(entry, implemented), top: 24));
        column.Children.Add(Spaced(BuildTabs(), top: 40));
        column.Children.Add(Spaced(body, top: 32));

        // The main column fills the page up to MainWidth and stays left; a hugging panel would resize with its content.
        var fixedWidth = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        fixedWidth.ColumnDefinitions[0].MaxWidth = MainWidth;
        fixedWidth.Children.Add(column);

        _padding.Padding = new Thickness(56, 36, 56, 96);
        _padding.Child = fixedWidth;
        _scroll.Content = _padding;
        Children.Add(_scroll);

        if (_sections.Count >= 2)
        {
            _toc = new GalleryToc(_sections.Select(s => s.Title).ToList(), ScrollToSection, () => ScrollTo(0), OpenOriginal)
            {
                Width = TocWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 36, 40, 0),
                IsVisible = false
            };
            Children.Add(_toc);
            _scroll.ScrollChanged += (_, _) => UpdateActiveSection();
            _scroll.AddHandler(PointerWheelChangedEvent, (_, _) => StopScrollAnimation(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateToc();
        }
    }

    private void UpdateToc()
    {
        var visible = _toc is not null && Bounds.Width >= TocMinimumPageWidth;
        if (_toc is not null && _toc.IsVisible != visible)
        {
            _toc.IsVisible = visible;
        }
        // The page keeps clear of the list instead of sliding under it.
        var right = visible ? 56 + TocWidth + 24 : 56;
        if (Math.Abs(_padding.Padding.Right - right) > 0.5)
        {
            _padding.Padding = new Thickness(56, 36, right, 96);
        }
    }

    #region Header

    private static Control BuildBreadcrumb(GalleryEntry entry)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(GalleryUi.Text("Components", ArcDesignTokenKind.TextMuted, 13));
        row.Children.Add(GalleryUi.Glyph(ArcIcons.ChevronRight, 12, ArcDesignTokenKind.TextMuted));
        row.Children.Add(GalleryUi.Text(entry.Title, ArcDesignTokenKind.Foreground, 13));
        return row;
    }

    private Control BuildUsage(GalleryEntry entry, bool implemented)
    {
        var left = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 12, LineSpacing = 12 };
        if (implemented)
        {
            left.Children.Add(BuildUsagePill($"<arc:{entry.ClassName} />"));
        }
        left.Children.Add(BuildStatus(entry, implemented));

        var original = new Button
        {
            Content = Labeled("View on uiarc.dev", ArcIcons.ExternalLink),
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            IsPressScaleEnabled = false
        };
        original.Click += (_, _) => OpenOriginal();

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(original, 1);
        original.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(left);
        grid.Children.Add(original);
        return grid;
    }

    /// <summary>The Arc "$ pnpm dlx …" install pill; here it is the tag to put in AXAML, with a copy button.</summary>
    private static Control BuildUsagePill(string markup)
    {
        var copy = new CopyButton { Value = markup, IsIconOnly = true, Variant = CopyButtonVariant.Plain };
        Avalonia.Automation.AutomationProperties.SetName(copy, "Copy markup");
        var text = GalleryUi.Mono(markup, ArcDesignTokenKind.TextSecondary, 13);
        text.Margin = new Thickness(0, 0, 4, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(GalleryUi.Mono("$", ArcDesignTokenKind.TextMuted, 13));
        row.Children.Add(text);
        row.Children.Add(copy);

        var pill = new Border
        {
            MinHeight = 36,
            Padding = new Thickness(14, 0, 3, 0),
            CornerRadius = new CornerRadius(999),
            BorderThickness = new Thickness(1),
            Child = row
        };
        ArcTokens.Bind(pill, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(pill, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return pill;
    }

    private static Control BuildStatus(GalleryEntry entry, bool implemented)
    {
        var dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(dot, Shape.FillProperty, implemented ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Warning);
        var label = GalleryUi.Text(implemented ? "Ready" : $"Planned · P{entry.Phase}", ArcDesignTokenKind.TextSecondary, 13, FontWeight.Medium);
        label.Margin = new Thickness(8, 0, 0, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(dot);
        row.Children.Add(label);
        var chip = GalleryUi.Chip(row, padding: new Thickness(12, 0));
        chip.MinHeight = 36;
        return chip;
    }

    private static Control Labeled(string text, Geometry glyph)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(new ArcGlyph { Data = glyph, Size = 14 });
        return row;
    }

    /// <summary>Preview is the live page; Code arrives with CodeBlock (P2) and stays visibly unavailable until then.</summary>
    private static Control BuildTabs()
    {
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        tabs.Children.Add(Tab("Preview", ArcIcons.Eye, active: true));
        var code = Tab("Code", GalleryIcons.Code, active: false);
        Tooltip.SetTip(code, "Source view arrives with CodeBlock (phase P2)");
        tabs.Children.Add(code);

        var live = GalleryUi.Text("Live preview", ArcDesignTokenKind.TextMuted, 13);
        live.VerticalAlignment = VerticalAlignment.Center;
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(live, 2);
        grid.Children.Add(tabs);
        grid.Children.Add(live);

        var line = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
        ArcTokens.Bind(line, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return line;
    }

    private static Border Tab(string label, Geometry glyph, bool active)
    {
        var color = active ? ArcDesignTokenKind.Foreground : ArcDesignTokenKind.TextMuted;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(GalleryUi.Glyph(glyph, 16, color));
        row.Children.Add(GalleryUi.Text(label, color, 14, FontWeight.Medium));
        var tab = new Border
        {
            Padding = new Thickness(0, 12),
            Margin = new Thickness(0, 0, 0, -1),
            BorderThickness = new Thickness(0, 0, 0, 2),
            Child = row
        };
        if (active)
        {
            ArcTokens.Bind(tab, Border.BorderBrushProperty, ArcDesignTokenKind.Foreground);
        }
        return tab;
    }

    private static Control Spaced(Control control, double top)
    {
        control.Margin = new Thickness(0, top, 0, 0);
        return control;
    }

    private void OpenOriginal()
    {
        _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri(_entry.ArcUrl));
    }

    #endregion

    #region Scrolling

    private double SectionTop(GallerySection section)
        => section.TranslatePoint(default, _padding) is { } point ? point.Y : 0;

    /// <summary>Scrolls to the <paramref name="index"/>th example (what a click in "On this page" does).</summary>
    public void ScrollToSection(int index)
    {
        if (index >= 0 && index < _sections.Count)
        {
            ScrollTo(Math.Max(0, SectionTop(_sections[index]) - 24));
        }
    }

    private void ScrollTo(double offset)
    {
        var max = Math.Max(0, _scroll.Extent.Height - _scroll.Viewport.Height);
        offset = Math.Clamp(offset, 0, max);
        // ScrollViewer.Offset is a direct property, which a ControlTheme transition cannot target,
        // so the glide to a section is driven by a MotionValue.
        if (_scrollY is null)
        {
            _scrollY = new MotionValue<double>(_scroll, _scroll.Offset.Y, MotionVectors.Double);
            _scrollY.Changed += y => _scroll.Offset = new Vector(_scroll.Offset.X, y);
        }
        _scrollY.Jump(_scroll.Offset.Y);
        _ = _scrollY.AnimateTo(offset, MotionSpec.Spring(ArcSpring.Smooth));
    }

    /// <summary>A wheel or touch scroll during the glide takes over from it.</summary>
    private void StopScrollAnimation()
    {
        if (_scrollY is { IsAnimating: true } animation)
        {
            animation.Jump(_scroll.Offset.Y);
        }
    }

    /// <summary>The section being read: the last one whose top has passed a line a little below the viewport top.</summary>
    private void UpdateActiveSection()
    {
        if (_toc is null || _sections.Count == 0)
        {
            return;
        }
        var offset = _scroll.Offset.Y;
        var active = 0;
        for (var i = 0; i < _sections.Count; i++)
        {
            if (SectionTop(_sections[i]) <= offset + 120)
            {
                active = i;
            }
        }
        if (offset + _scroll.Viewport.Height >= _scroll.Extent.Height - 2)
        {
            active = _sections.Count - 1;
        }
        _toc.SetActive(active);
    }

    #endregion
}
