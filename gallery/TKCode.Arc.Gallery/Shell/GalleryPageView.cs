using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Docs;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using StreamMark.Avalonia;
using StreamMark.Avalonia.Rendering;
using StreamMark.Core.Streaming;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// One component page, laid out like a uiarc.dev docs page: breadcrumb, title, summary, install command and status, the
/// page content, and a sticky "On this page" list that follows the scroll position. The content is the component's docs
/// page (<see cref="GalleryDocs"/>) with its live demos (<see cref="GalleryDemoView"/>): "On this page" lists the page's
/// sections and "Copy page" copies it, in the current AtomUI language. A component without demos (a planned one) shows a
/// plain body instead, such as its placeholder.
/// </summary>
public sealed class GalleryPageView : Grid
{
    private const double MainWidth = 780;
    // Docs tables need more room than a plain body; Arc's docs column is about 890 wide.
    private const double DocsWidth = 880;
    private const double TocWidth = 196;
    private const double TocMinimumPageWidth = 960;
    // StreamMark shrinks every column of a table that is too wide, so short types and defaults break mid-word: a docs
    // page keeps its API tables at least this wide and drops "On this page" first.
    private const double DocsMinimumWidth = 840;

    private readonly GalleryEntry _entry;
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Border _padding = new();
    private readonly Panel _content = new();
    private readonly ColumnDefinition _mainColumn = new(GridLength.Star) { MaxWidth = MainWidth };
    private readonly TextBlock _summary = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 16);
    private readonly List<Control> _anchors = new();
    private GalleryToc? _toc;
    private MotionValue<double>? _scrollY;
    // The page opens at the top and stays there until the visitor scrolls (see KeepAtTopUntilTouched).
    private bool _touched;
    // Shown in place of the docs while their text is fetched and laid out, so the page is never an empty frame.
    private Control? _loading;

    // The docs page.
    private readonly Markdown? _docs;
    private readonly CopyButton? _copyPage;
    private MarkdownView? _docsView;
    private DocumentSnapshot? _outlineSnapshot;
    // The language whose docs page is shown or on its way: the constructor, attaching and attaching again all refresh, and
    // only a new language fetches the text again.
    private string? _docsLanguage;

    /// <summary>
    /// A page without demos: <paramref name="body"/> (a planned component's placeholder, or examples registered with
    /// <c>GalleryPages.Register</c>) under the page header; its <see cref="GallerySection"/>s are "On this page".
    /// </summary>
    public GalleryPageView(GalleryEntry entry, Control body, bool implemented)
    {
        _entry = entry;
        _content.Children.Add(body);
        Build(implemented);

        var sections = body.GetLogicalDescendants().OfType<GallerySection>().ToList();
        SetSections(sections.Select(s => s.Title).ToList(), sections.Cast<Control>().ToList());
        // The summary comes from the docs page when there is one, in the current language.
        RefreshLanguage();
        GalleryText.Track(this, RefreshLanguage);
    }

    /// <summary>The docs page: the component's Markdown page with its live demos is the whole page.</summary>
    public GalleryPageView(GalleryEntry entry)
    {
        _entry = entry;
        _docs = new Markdown { Services = GalleryDemoPlugin.Services };
        _copyPage = new CopyButton { Variant = CopyButtonVariant.Outline };
        GalleryText.Bind(_copyPage, CopyButton.LabelProperty, GalleryShellLangResourceKind.CopyPage);
        GalleryText.Bind(_copyPage, CopyButton.CopiedLabelProperty, GalleryShellLangResourceKind.Copied);
        GalleryText.Bind(_copyPage, CopyButton.FailedLabelProperty, GalleryShellLangResourceKind.Failed);
        _loading = BuildLoading();
        _content.Children.Add(_docs);
        _content.Children.Add(_loading);
        _mainColumn.MaxWidth = DocsWidth;
        Build(implemented: true);

        // StreamMark lays the document out a frame after the text is set, and again after every language change: the
        // section list is rebuilt whenever a new snapshot has been laid out.
        _docs.LayoutUpdated += (_, _) => SyncOutline();
        RefreshLanguage();
        GalleryText.Track(this, RefreshLanguage);
    }

    /// <summary>Whether this is a docs page with live demos (otherwise it shows a plain body).</summary>
    public bool IsDocsPage => _docs is not null;

    /// <summary>The docs page's Markdown (null on a page with a plain body).</summary>
    public Markdown? Docs => _docs;

    /// <summary>The titles "On this page" lists.</summary>
    public IReadOnlyList<string> SectionTitles { get; private set; } = [];

    private void Build(bool implemented)
    {
        var column = new StackPanel();
        column.Children.Add(BuildBreadcrumb(_entry));
        column.Children.Add(Spaced(GalleryUi.Text(_entry.Title, ArcDesignTokenKind.Foreground, 44), top: 14));
        _summary.MaxWidth = 640;
        _summary.HorizontalAlignment = HorizontalAlignment.Left;
        column.Children.Add(Spaced(_summary, top: 12));
        column.Children.Add(Spaced(BuildUsage(_entry, implemented), top: 24));
        column.Children.Add(Spaced(_content, top: 28));

        // The main column fills the page up to its maximum width and stays left; a hugging panel would resize with its content.
        var fixedWidth = new Grid { ColumnDefinitions = { _mainColumn, new ColumnDefinition(GridLength.Auto) } };
        fixedWidth.Children.Add(column);

        _padding.Padding = new Thickness(56, 36, 56, 96);
        _padding.Child = fixedWidth;
        _scroll.Content = _padding;
        Children.Add(_scroll);

        KeepAtTopUntilTouched();
        _scroll.ScrollChanged += (_, _) => UpdateActiveSection();
        _scroll.AddHandler(PointerWheelChangedEvent, (_, _) => StopScrollAnimation(), Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>
    /// A new page opens at the top. Demos that are built as the page lays out (or take the focus, or ask to be brought into
    /// view) would otherwise carry it away; the first wheel, key or press, or a jump from "On this page", hands the scroll
    /// over.
    /// </summary>
    private void KeepAtTopUntilTouched()
    {
        void Touch(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => _touched = true;
        _scroll.AddHandler(PointerWheelChangedEvent, Touch, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _scroll.AddHandler(PointerPressedEvent, Touch, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _scroll.AddHandler(KeyDownEvent, Touch, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        _scroll.BringIntoViewOnFocusChange = false;
        _scroll.ScrollChanged += (_, _) =>
        {
            if (!_touched && _scroll.Offset.Y > 0)
            {
                _scroll.Offset = default;
            }
        };
    }

    /// <summary>Grey blocks in the shape of a preview and a few paragraphs, until the document is laid out.</summary>
    private static Control BuildLoading()
    {
        var panel = new StackPanel { Spacing = 24, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
        panel.Children.Add(new Skeleton { Lines = 1, Height = 260, HorizontalAlignment = HorizontalAlignment.Stretch });
        panel.Children.Add(new Skeleton { Lines = 5 });
        panel.Children.Add(new Skeleton { Lines = 4 });
        return panel;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateToc();
        }
    }

    /// <summary>Replaces the "On this page" list: one row per title, each scrolling to the top of its anchor.</summary>
    private void SetSections(IReadOnlyList<string> titles, IReadOnlyList<Control> anchors)
    {
        if (_toc is not null)
        {
            Children.Remove(_toc);
            _toc = null;
        }
        SectionTitles = titles;
        _anchors.Clear();
        _anchors.AddRange(anchors);
        if (titles.Count >= 2)
        {
            _toc = new GalleryToc(titles, ScrollToSection, () => ScrollTo(0), _entry.IsArc ? OpenOriginal : null)
            {
                Width = TocWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 36, 40, 0),
                IsVisible = false
            };
            Children.Add(_toc);
        }
        UpdateToc();
        UpdateActiveSection();
    }

    private void UpdateToc()
    {
        var minimum = IsDocsPage ? 56 + DocsMinimumWidth + 24 + TocWidth + 56 : TocMinimumPageWidth;
        var visible = _toc is not null && Bounds.Width >= minimum;
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
        var components = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 13);
        GalleryText.Bind(components, TextBlock.TextProperty, GalleryShellLangResourceKind.Components);
        row.Children.Add(components);
        row.Children.Add(GalleryUi.Glyph(ArcIcons.ChevronRight, 12, ArcDesignTokenKind.TextMuted));
        row.Children.Add(GalleryUi.Text(entry.Title, ArcDesignTokenKind.Foreground, 13));
        return row;
    }

    private Control BuildUsage(GalleryEntry entry, bool implemented)
    {
        var left = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 12, LineSpacing = 12 };
        if (implemented)
        {
            left.Children.Add(BuildInstallCommand($"dotnet add package {GalleryDocs.PackageOf(entry.ClassName) ?? "TKCode.Arc"} --prerelease"));
        }
        left.Children.Add(BuildStatus(entry, implemented));

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Top };
        if (_copyPage is not null)
        {
            right.Children.Add(_copyPage);
        }
        // Extras have no Arc original to link to.
        if (entry.IsArc)
        {
            var original = new Button
            {
                Content = Labeled(GalleryShellLangResourceKind.ViewOnArc, ArcIcons.ExternalLink),
                Variant = ButtonVariant.Secondary,
                Size = ControlSize.Small,
                IsPressScaleEnabled = false
            };
            original.Click += (_, _) => OpenOriginal();
            right.Children.Add(original);
        }

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>The install command box, like Arc's "$ npx shadcn add …": the NuGet package the component ships in.</summary>
    private static Control BuildInstallCommand(string command)
    {
        var copy = new CopyButton { Value = command, IsIconOnly = true, Variant = CopyButtonVariant.Plain, VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(copy, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.CopyCommand);
        var prompt = GalleryUi.Mono("$", ArcDesignTokenKind.TextMuted, 13);
        var text = GalleryUi.Mono(command, ArcDesignTokenKind.TextSecondary, 13);
        prompt.VerticalAlignment = VerticalAlignment.Center;
        text.VerticalAlignment = VerticalAlignment.Center;
        text.Margin = new Thickness(0, 0, 2, 0);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(prompt);
        row.Children.Add(text);
        row.Children.Add(copy);

        var box = new Border
        {
            MinHeight = 36,
            Padding = new Thickness(12, 0, 2, 0),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            Child = row
        };
        ArcTokens.Bind(box, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        ArcTokens.Bind(box, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return box;
    }

    private static Control BuildStatus(GalleryEntry entry, bool implemented)
    {
        var dot = new Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(dot, Shape.FillProperty, implemented ? ArcDesignTokenKind.Success : ArcDesignTokenKind.Warning);
        var label = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 13, FontWeight.Medium);
        if (implemented)
        {
            GalleryText.Bind(label, TextBlock.TextProperty, GalleryShellLangResourceKind.Ready);
        }
        else
        {
            GalleryText.BindFormat(label, TextBlock.TextProperty, GalleryShellLangResourceKind.PlannedPhase, entry.Phase);
        }
        label.Margin = new Thickness(8, 0, 0, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(dot);
        row.Children.Add(label);
        var chip = GalleryUi.Chip(row, padding: new Thickness(12, 0));
        chip.MinHeight = 36;
        return chip;
    }

    private static Control Labeled(GalleryShellLangResourceKind text, Geometry glyph)
    {
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(label, TextBlock.TextProperty, text);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(label);
        row.Children.Add(new ArcGlyph { Data = glyph, Size = 14 });
        return row;
    }

    /// <summary>
    /// Shows the summary and the docs page in the current AtomUI language. A page that is not translated falls back to
    /// en-US by itself, and a component without a docs page keeps its catalog description.
    /// </summary>
    private void RefreshLanguage()
    {
        var language = GalleryText.CurrentLanguage;
        _summary.Text = GalleryDocs.Summary(_entry.ClassName, language) ?? _entry.Description;
        if (_docs is not null && _docsLanguage != language)
        {
            _docsLanguage = language;
            _ = ShowDocsAsync(_docs, _copyPage!, language);
        }
    }

    /// <summary>
    /// Shows the docs page in <paramref name="language"/>. The gallery's resources hand its text over at once, so the page
    /// is laid out with its first frame; the WebAssembly host fetches it, and the header shows until it arrives. A page
    /// that arrives after the language changed again is dropped, and one that cannot be read says so and is asked for
    /// again on the next refresh.
    /// </summary>
    private async Task ShowDocsAsync(Markdown docs, CopyButton copyPage, string language)
    {
        var doc = await GalleryDocs.GetAsync(_entry.ClassName, language);
        if (language != _docsLanguage)
        {
            return;
        }
        if (doc is null)
        {
            docs.Text = "> " + GalleryText.Get(GalleryShellLangResourceKind.PageLoadFailed);
            _docsLanguage = null;
        }
        else if (docs.Text != doc.Markdown)
        {
            docs.Text = doc.Markdown;
            copyPage.Value = doc.CopyMarkdown;
        }
    }

    private static Control Spaced(Control control, double top)
    {
        control.Margin = new Thickness(0, top, 0, 0);
        return control;
    }

    private void OpenOriginal()
    {
        if (_entry.ArcUrl is { } url)
        {
            _ = TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri(url));
        }
    }

    #endregion

    #region Docs outline

    /// <summary>
    /// Lists the page's preview (the demo above the first section) and its sections ("## " headings) in "On this page",
    /// once StreamMark has laid out a new snapshot of the document.
    /// </summary>
    private void SyncOutline()
    {
        _docsView ??= _docs?.GetVisualDescendants().OfType<MarkdownView>().FirstOrDefault();
        if (_docsView?.Snapshot is not { } snapshot || ReferenceEquals(snapshot, _outlineSnapshot))
        {
            return;
        }
        var blocks = _docsView.GetVisualChildren().OfType<BlockView>().ToList();
        if (blocks.Count == 0)
        {
            return;
        }
        _outlineSnapshot = snapshot;
        if (_loading is not null)
        {
            _loading.IsVisible = false;
        }

        var titles = new List<string>();
        var anchors = new List<Control>();
        if (_docsView.GetVisualDescendants().OfType<GalleryDemoView>().FirstOrDefault(d => d.IsHero) is { } hero)
        {
            titles.Add(GalleryText.Get(GalleryShellLangResourceKind.Preview));
            anchors.Add(hero);
        }
        foreach (var item in _docsView.GetOutline().Where(i => i.Level == 2))
        {
            if (blocks.FirstOrDefault(b => b.TopKey == item.Target.BlockKey) is { } heading)
            {
                titles.Add(item.Text);
                anchors.Add(heading);
            }
        }
        SetSections(titles, anchors);
    }

    #endregion

    #region Scrolling

    private double SectionTop(Control anchor)
        => anchor.TranslatePoint(default, _padding) is { } point ? point.Y : 0;

    /// <summary>Scrolls to the <paramref name="index"/>th entry of "On this page" (what a click on it does).</summary>
    public void ScrollToSection(int index)
    {
        if (index >= 0 && index < _anchors.Count)
        {
            ScrollTo(Math.Max(0, SectionTop(_anchors[index]) - 24));
        }
    }

    private void ScrollTo(double offset)
    {
        _touched = true;
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
        if (_toc is null || _anchors.Count == 0)
        {
            return;
        }
        var offset = _scroll.Offset.Y;
        var active = 0;
        for (var i = 0; i < _anchors.Count; i++)
        {
            if (SectionTop(_anchors[i]) <= offset + 120)
            {
                active = i;
            }
        }
        if (offset + _scroll.Viewport.Height >= _scroll.Extent.Height - 2)
        {
            active = _anchors.Count - 1;
        }
        _toc.SetActive(active);
    }

    #endregion
}
