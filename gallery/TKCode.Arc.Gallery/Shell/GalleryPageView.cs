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

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// One component page, laid out like a uiarc.dev docs page: breadcrumb, title, summary, usage and status, the
/// Preview and Code tabs, the examples, and a sticky "On this page" list that follows the scroll position. Code shows
/// the component's Markdown page (<see cref="GalleryDocs"/>) in the current AtomUI language and follows language
/// changes.
/// </summary>
public sealed class GalleryPageView : Grid
{
    private const double MainWidth = 780;
    // Docs tables need more room than the examples; Arc's docs column is about 890 wide.
    private const double CodeWidth = 880;
    private const double TocWidth = 196;
    private const double TocMinimumPageWidth = 960;

    private readonly GalleryEntry _entry;
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    private readonly Border _padding = new();
    private readonly List<GallerySection> _sections;
    private readonly GalleryToc? _toc;
    private readonly Control _body;
    private readonly Panel _content = new();
    private readonly ColumnDefinition _mainColumn = new(GridLength.Star) { MaxWidth = MainWidth };
    private readonly PageTab _previewTab = new(GalleryShellLangResourceKind.Preview, ArcIcons.Eye);
    private readonly PageTab _codeTab = new(GalleryShellLangResourceKind.Code, GalleryIcons.Code);
    private readonly TextBlock _live = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextMuted, 13);
    private readonly CopyButton _copyMarkdown = new() { Variant = CopyButtonVariant.Plain, IsVisible = false };
    private readonly TextBlock _summary = GalleryUi.Text(string.Empty, ArcDesignTokenKind.TextSecondary, 16);
    private Markdown? _code;
    private MotionValue<double>? _scrollY;

    public GalleryPageView(GalleryEntry entry, Control body, bool implemented)
    {
        _entry = entry;
        _body = body;
        _sections = body.GetLogicalDescendants().OfType<GallerySection>().ToList();

        var column = new StackPanel();
        column.Children.Add(BuildBreadcrumb(entry));
        column.Children.Add(Spaced(GalleryUi.Text(entry.Title, ArcDesignTokenKind.Foreground, 44), top: 14));
        _summary.MaxWidth = 640;
        _summary.HorizontalAlignment = HorizontalAlignment.Left;
        column.Children.Add(Spaced(_summary, top: 12));
        column.Children.Add(Spaced(BuildUsage(entry, implemented), top: 24));
        column.Children.Add(Spaced(BuildTabs(), top: 40));
        _content.Children.Add(body);
        column.Children.Add(Spaced(_content, top: 32));

        // The main column fills the page up to MainWidth and stays left; a hugging panel would resize with its content.
        var fixedWidth = new Grid { ColumnDefinitions = { _mainColumn, new ColumnDefinition(GridLength.Auto) } };
        fixedWidth.Children.Add(column);

        _padding.Padding = new Thickness(56, 36, 56, 96);
        _padding.Child = fixedWidth;
        _scroll.Content = _padding;
        Children.Add(_scroll);

        if (_sections.Count >= 2)
        {
            _toc = new GalleryToc(_sections.Select(s => s.Title).ToList(), ScrollToSection, () => ScrollTo(0), entry.IsArc ? OpenOriginal : null)
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

        GalleryText.Bind(_live, TextBlock.TextProperty, GalleryShellLangResourceKind.LivePreview);
        GalleryText.Bind(_copyMarkdown, CopyButton.LabelProperty, GalleryShellLangResourceKind.CopyMarkdown);
        GalleryText.Bind(_copyMarkdown, CopyButton.CopiedLabelProperty, GalleryShellLangResourceKind.Copied);
        GalleryText.Bind(_copyMarkdown, CopyButton.FailedLabelProperty, GalleryShellLangResourceKind.Failed);
        // The summary and the Code tab are the page text that comes from the docs, in the current language.
        RefreshLanguage();
        GalleryText.Track(this, RefreshLanguage);
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
        // The list follows the examples, so it belongs to the Preview tab.
        var visible = _toc is not null && !IsShowingCode && Bounds.Width >= TocMinimumPageWidth;
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
            left.Children.Add(BuildUsagePill($"<arc:{entry.ClassName} />"));
        }
        left.Children.Add(BuildStatus(entry, implemented));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(left);
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
            Grid.SetColumn(original, 1);
            original.VerticalAlignment = VerticalAlignment.Top;
            grid.Children.Add(original);
        }
        return grid;
    }

    /// <summary>The Arc "$ pnpm dlx …" install pill; here it is the tag to put in AXAML, with a copy button.</summary>
    private static Control BuildUsagePill(string markup)
    {
        var copy = new CopyButton { Value = markup, IsIconOnly = true, Variant = CopyButtonVariant.Plain };
        GalleryText.Bind(copy, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.CopyMarkup);
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

    /// <summary>Preview is the live page; Code is the component's Markdown page, unavailable until one is written.</summary>
    private Control BuildTabs()
    {
        _previewTab.IsActive = true;
        _previewTab.Click += () => ShowCode(false);
        _codeTab.Click += () => ShowCode(true);
        if (!GalleryDocs.Has(_entry.ClassName))
        {
            _codeTab.IsAvailable = false;
            GalleryText.Bind(_codeTab, Tooltip.TipProperty, GalleryShellLangResourceKind.NoDocsYet);
        }
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        tabs.Children.Add(_previewTab);
        tabs.Children.Add(_codeTab);

        _live.VerticalAlignment = VerticalAlignment.Center;
        _copyMarkdown.VerticalAlignment = VerticalAlignment.Center;
        var actions = new Panel();
        actions.Children.Add(_live);
        actions.Children.Add(_copyMarkdown);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(actions, 2);
        grid.Children.Add(tabs);
        grid.Children.Add(actions);

        var line = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
        ArcTokens.Bind(line, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return line;
    }

    /// <summary>Whether the Code tab is showing.</summary>
    public bool IsShowingCode => _codeTab.IsActive;

    /// <summary>Switches between the examples and the docs page (Code is ignored when there is no page).</summary>
    public void ShowCode(bool code)
    {
        if (code == IsShowingCode || (code && !GalleryDocs.Has(_entry.ClassName)))
        {
            return;
        }
        if (code && _code is null)
        {
            _code = new Markdown();
            _content.Children.Add(_code);
            RefreshLanguage();
        }
        _previewTab.IsActive = !code;
        _codeTab.IsActive = code;
        _body.IsVisible = !code;
        if (_code is not null)
        {
            _code.IsVisible = code;
        }
        _live.IsVisible = !code;
        _copyMarkdown.IsVisible = code;
        _mainColumn.MaxWidth = code ? CodeWidth : MainWidth;
        UpdateToc();
    }

    /// <summary>
    /// Shows the summary and the Code tab in the current AtomUI language. A page that is not translated falls back to
    /// en-US by itself, and a component without a docs page keeps its catalog description.
    /// </summary>
    private void RefreshLanguage()
    {
        var language = GalleryText.CurrentLanguage;
        _summary.Text = GalleryDocs.Summary(_entry.ClassName, language) ?? _entry.Description;
        if (_code is not null && GalleryDocs.Get(_entry.ClassName, language) is { } doc && _code.Text != doc.Markdown)
        {
            _code.Text = doc.Markdown;
            _copyMarkdown.Value = doc.Markdown;
        }
    }

    /// <summary>A page tab: a glyph and a label over a 2px underline that marks the active tab.</summary>
    private sealed class PageTab : Border
    {
        private readonly GalleryNavRow _row;
        private IDisposable? _underline;

        public PageTab(GalleryShellLangResourceKind label, Geometry glyph)
        {
            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            GalleryText.Bind(text, TextBlock.TextProperty, label);
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(new ArcGlyph { Data = glyph, Size = 16 });
            content.Children.Add(text);
            _row = new GalleryNavRow(content, fontSize: 14, height: 0, padding: new Thickness(0, 12), radius: 8,
                weight: FontWeight.Medium, rest: ArcDesignTokenKind.TextMuted);
            GalleryText.Bind(_row, Avalonia.Automation.AutomationProperties.NameProperty, label);
            _row.Click += () => Click?.Invoke();
            Margin = new Thickness(0, 0, 0, -1);
            BorderThickness = new Thickness(0, 0, 0, 2);
            Child = _row;
        }

        public event Action? Click;

        public bool IsActive
        {
            get => _row.IsSelected;
            set
            {
                _row.IsSelected = value;
                _underline?.Dispose();
                _underline = value ? ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Foreground) : null;
            }
        }

        /// <summary>
        /// An unavailable tab cannot be focused or clicked. It is dimmed and shows the default cursor, and it stays
        /// hoverable (the tooltip sits on the tab itself, which remains enabled) so the tooltip can explain.
        /// </summary>
        public bool IsAvailable
        {
            get => _row.IsEnabled;
            set
            {
                _row.IsEnabled = value;
                _row.Cursor = value ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
                Opacity = value ? 1 : 0.45;
            }
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
