using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Shell;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// The Northstar sidebar of uiarc.dev/demo: the studio's name, the eleven pages (the last five under "Workspace") with
/// one highlight that glides to the open page, and a footer with Invite people, the collapse toggle and the way back.
/// Collapsed it is a 64px rail of icons whose labels fade out and become tooltips. The phone-width drawer shows the
/// same list without the brand and the toggle.
/// </summary>
public sealed class DemoNav : DockPanel
{
    public const double ExpandedWidth = 232;
    public const double CollapsedWidth = 64;

    private readonly Dictionary<string, GalleryNavRow> _rows = new();
    private readonly List<Control> _labels = new();
    private readonly Panel _content = new();
    private readonly GlidingIndicator _highlight = new() { IsHitTestVisible = false, Spring = ArcSpring.Morph, Opacity = 0.07 };
    private readonly ArcGlyph? _collapseGlyph;
    private readonly GalleryNavRow? _collapseRow;
    private string? _selected;
    private Rect? _highlightTarget;
    private bool _isCollapsed;

    /// <param name="isRail">The desktop sidebar: with the brand and the collapse toggle (the drawer has neither).</param>
    public DemoNav(bool isRail)
    {
        Margin = new Thickness(12);
        LastChildFill = true;

        if (isRail)
        {
            var brand = Brand();
            SetDock(brand, Dock.Top);
            Children.Add(brand);
        }

        var footer = new StackPanel { Spacing = 1, Margin = new Thickness(0, 20, 0, 0) };
        footer.Children.Add(Row(null, "Invite people", DemoIcons.UserPlus, () => InviteRequested?.Invoke()));
        if (isRail)
        {
            _collapseGlyph = DemoUi.Glyph(DemoIcons.PanelLeftClose);
            _collapseRow = Row(null, "Collapse sidebar", _collapseGlyph, () => CollapseToggled?.Invoke());
            footer.Children.Add(_collapseRow);
        }
        footer.Children.Add(Row(null, "Arc library", ArcIcons.ArrowLeft, () => BackRequested?.Invoke(), ArcDesignTokenKind.TextMuted));
        SetDock(footer, Dock.Bottom);
        Children.Add(footer);

        var groups = new StackPanel { Spacing = 20 };
        groups.Children.Add(Group(null, DemoPages.App.Where(page => !page.IsWorkspace)));
        groups.Children.Add(Group("Workspace", DemoPages.App.Where(page => page.IsWorkspace)));
        ArcTokens.Bind(_highlight, GlidingIndicator.FillProperty, ArcDesignTokenKind.Foreground);
        _highlight.CornerRadius = new CornerRadius(8);
        _content.Children.Add(_highlight);
        _content.Children.Add(groups);
        Children.Add(new ScrollViewer
        {
            Content = _content,
            Margin = new Thickness(0, isRail ? 20 : 0, 0, 0),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
        });
        _content.LayoutUpdated += (_, _) => SyncHighlight(animate: false);
    }

    /// <summary>A page row was chosen.</summary>
    public event Action<DemoPage>? PageChosen;

    public event Action? InviteRequested;

    public event Action? CollapseToggled;

    /// <summary>"Arc library": back to the gallery.</summary>
    public event Action? BackRequested;

    /// <summary>The open page's slug; the highlight glides to its row.</summary>
    public string? Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }
            var animate = _selected is not null;
            if (_selected is not null && _rows.TryGetValue(_selected, out var old))
            {
                old.IsSelected = false;
            }
            _selected = value;
            if (value is not null && _rows.TryGetValue(value, out var row))
            {
                row.IsSelected = true;
            }
            // Glide now: a layout pass before a posted glide would see the new row and jump the highlight there.
            if (!SyncHighlight(animate))
            {
                Dispatcher.UIThread.Post(() => SyncHighlight(animate), DispatcherPriority.Render);
            }
        }
    }

    /// <summary>The rail: labels fade out (they stay in the layout and are clipped by the sidebar) and become tooltips.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            _isCollapsed = value;
            foreach (var label in _labels)
            {
                label.Opacity = value ? 0 : 1;
                label.IsHitTestVisible = !value;
            }
            foreach (var (slug, row) in _rows)
            {
                ToolTip.SetTip(row, value ? DemoPages.Find(slug)?.Title : null);
            }
            if (_collapseGlyph is not null && _collapseRow is not null)
            {
                _collapseGlyph.Data = value ? DemoIcons.PanelLeftOpen : DemoIcons.PanelLeftClose;
                ToolTip.SetTip(_collapseRow, value ? "Expand sidebar  [" : "Collapse sidebar  [");
                Avalonia.Automation.AutomationProperties.SetName(_collapseRow, value ? "Expand sidebar" : "Collapse sidebar");
            }
        }
    }

    private Control Brand()
    {
        var mark = new Avalonia.Controls.Shapes.Path
        {
            Data = DemoArt.NorthstarMark,
            Width = 20,
            Height = 20,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.75,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            VerticalAlignment = VerticalAlignment.Center
        };
        ArcTokens.Bind(mark, Shape.StrokeProperty, ArcDesignTokenKind.Foreground);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(DemoUi.Line(DemoData.StudioName, ArcDesignTokenKind.Foreground, 13, FontWeight.Medium));
        text.Children.Add(DemoUi.Line(DemoData.StudioTagline, ArcDesignTokenKind.TextMuted, 12));
        Fades(text);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Height = 40, Margin = new Thickness(10, 0) };
        row.Children.Add(mark);
        row.Children.Add(text);
        return row;
    }

    private Control Group(string? heading, IEnumerable<DemoPage> pages)
    {
        var group = new StackPanel { Spacing = 1 };
        if (heading is not null)
        {
            var title = DemoUi.Line(heading, ArcDesignTokenKind.TextMuted, 12);
            title.Margin = new Thickness(12, 0, 12, 4);
            Fades(title);
            group.Children.Add(title);
        }
        foreach (var page in pages)
        {
            var row = Row(page.Slug, page.Title, page.Icon, () => PageChosen?.Invoke(page));
            _rows[page.Slug] = row;
            group.Children.Add(row);
        }
        return group;
    }

    private GalleryNavRow Row(string? slug, string label, Geometry icon, Action click, ArcDesignTokenKind rest = ArcDesignTokenKind.TextSecondary)
        => Row(slug, label, DemoUi.Glyph(icon), click, rest);

    private GalleryNavRow Row(string? slug, string label, ArcGlyph glyph, Action click, ArcDesignTokenKind rest = ArcDesignTokenKind.TextSecondary)
    {
        // The label takes the row's color (resting, hover, selected); the glyph keeps its muted color like uiarc.dev's icons.
        var text = new TextBlock { Text = label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        Fades(text);
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        content.Children.Add(glyph);
        content.Children.Add(text);
        var row = new GalleryNavRow(content, label, fontSize: 13, height: 32, padding: new Thickness(12, 0), radius: 8,
            rest: rest, hoverFill: ArcDesignTokenKind.SurfaceMuted);
        row.Click += click;
        return row;
    }

    private void Fades(Control label)
    {
        label.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(160) }];
        _labels.Add(label);
    }

    /// <summary>
    /// Moves the highlight onto the open page's row. Layout passes (every pass in the window, a page switch makes many)
    /// call it without animation and only jump the highlight when the row itself moved, so they don't cut a glide short.
    /// </summary>
    private bool SyncHighlight(bool animate)
    {
        if (_selected is null || !_rows.TryGetValue(_selected, out var row) || row.Bounds.Height <= 0
            || row.TranslatePoint(default, _content) is not { } origin)
        {
            return false;
        }
        var rect = new Rect(origin, row.Bounds.Size);
        if (!animate && rect == _highlightTarget)
        {
            return true;
        }
        _highlightTarget = rect;
        _highlight.MoveTo(rect, animate);
        return true;
    }
}
