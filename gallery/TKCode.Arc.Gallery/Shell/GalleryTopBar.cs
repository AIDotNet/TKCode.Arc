using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Catalog;
using TKCode.Arc.Gallery.Localization;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The site's top bar: the brand (back to the home page), Components (a menu of the library's parts), Open source and Docs;
/// then a GitHub button with the star count and icon buttons for search and the light / dark toggle. The site also has
/// language and Appearance buttons and a download button; the desktop app keeps those two choices in its settings menu.
/// When everything does not fit, the bar gives up room step by step (<see cref="FitLevel"/>).
/// </summary>
public sealed class GalleryTopBar : Border
{
    private const double SideMargin = 24;

    /// <summary>The header's content is at most this wide (its <c>InnerMaxWidth</c>).</summary>
    private const double HeaderMaxWidth = 1200;
    private const double MenuButtonWidth = 44;
    private const double Gap = 16;
    private const double MenuInset = 32;
    private const double SearchMinWidth = 148;
    private const int MostCompactLevel = 4;
    private const string BrandName = "TKCode";

    private readonly GalleryTheme _theme;
    private readonly Button _themeToggle;
    private readonly Button _appearance;
    private readonly TextBlock _appearanceLabel;
    private readonly Button _search;
    private readonly Grid _searchRow;
    private readonly GalleryGitHubButton _github;
    private readonly SiteHeader _header;
    private readonly StackPanel _right;
    private readonly Button _language;
    private readonly TextBlock _languageName;
    private readonly MenuSurface _languageMenu;
    private readonly string[] _navValues = ["components", "projects", "docs"];
    private readonly GalleryShellLangResourceKind[] _navLabels =
    [
        GalleryShellLangResourceKind.Components, GalleryShellLangResourceKind.NavProjects, GalleryShellLangResourceKind.Category_Docs
    ];
    private readonly List<(SiteHeaderLink Link, GalleryShellLangResourceKind Label, GalleryShellLangResourceKind Text, Func<GalleryRoute> Route)> _componentLinks = new();
    private readonly Button? _download;
    private readonly TextBlock? _downloadLabel;
    private readonly Button? _gear;
    private double _navWidth = 420;
    private double _brandWidth = 150;
    // The header's menu and brand, from its template.
    private Control? _navList;
    private Control? _brandSlot;
    private readonly GalleryShell _shell;
    private readonly Dictionary<Button, IDisposable> _compactTips = new();
    private ArcPopup? _popup;
    private DateTime _closedAt;
    private int _fitLevel = -1;
    // The width and level of the last fit; a new language forgets them (its labels may be shorter).
    private double _fittedInner = double.PositiveInfinity;
    private int _fittedLevel = -1;
    // What the last fit counted on besides the width (see NeedsFit).
    private double _fittedNav;
    private double _fittedBrand;
    private double _fittedButtons;

    public GalleryTopBar(GalleryTheme theme, GalleryShell shell)
    {
        _theme = theme;
        _shell = shell;
        Control owner = shell;

        // The site header block draws the brand and the section menu; the buttons ride in its trailing slot. Components opens a
        // mega menu of the library's parts and the dashboard demo; Open source and Docs open their pages.
        _header = new SiteHeader
        {
            Variant = SiteHeaderVariant.Mega,
            BrandName = BrandName,
            // The logo carries its own light rounded tile, so it reads the same on the light and the dark bar.
            BrandMark = GalleryBrand.CreateLogo(),
            IsSticky = false,
            IsPrimaryActionVisible = false,
            IsSecondaryActionVisible = false,
            CollapseWidth = 0
        };
        for (var i = 0; i < _navValues.Length; i++)
        {
            _header.Items.Add(new SiteHeaderItem { Value = _navValues[i], Label = GalleryText.Get(_navLabels[i]) });
        }
        BuildComponentsMenu(shell);
        GalleryText.Bind(_header, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.GoHome);
        _header.TemplateApplied += (_, e) =>
        {
            _navList = e.NameScope.Find<Control>(SiteHeader.NavListPart);
            _brandSlot = e.NameScope.Find<Control>("PART_BrandSlot");
        };
        _header.Navigate += (_, e) =>
        {
            switch (e.Kind)
            {
                case SiteHeaderNavigateKind.Brand:
                    shell.Navigate(GalleryRoute.Home);
                    break;
                case SiteHeaderNavigateKind.Item:
                    shell.Navigate(e.Section switch
                    {
                        "docs" => shell.RouteFor(GalleryRouteKind.Docs),
                        "projects" => GalleryRoute.Projects,
                        _ => GalleryRoute.ComponentsIndex
                    });
                    break;
                case SiteHeaderNavigateKind.PanelLink:
                    foreach (var (link, _, _, route) in _componentLinks)
                    {
                        if (ReferenceEquals(link, e.Link))
                        {
                            shell.Navigate(route());
                        }
                    }
                    break;
            }
        };

        _appearanceLabel = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(_appearanceLabel, TextBlock.TextProperty, GalleryShellLangResourceKind.Appearance);
        var appearanceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        appearanceRow.Children.Add(new ArcGlyph { Data = ArcIcons.SlidersHorizontal, Size = 16 });
        appearanceRow.Children.Add(_appearanceLabel);
        _appearance = new Button
        {
            Content = appearanceRow,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            IsPressScaleEnabled = false
        };
        GalleryText.Bind(_appearance, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.Appearance);
        _appearance.Click += (_, _) => ToggleAppearance(owner);

        (_language, _languageName, _languageMenu) = BuildLanguageMenu();

        _themeToggle = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            Padding = new Thickness(0),
            MinWidth = 36
        };
        GalleryText.Bind(_themeToggle, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.ToggleAppearance);
        _themeToggle.Click += async (_, _) => await _theme.ToggleDarkAsync();
        _theme.Changed += SyncThemeToggle;
        SyncThemeToggle();

        (_search, _searchRow) = BuildSearchButton(shell.FocusSearch);
        _github = new GalleryGitHubButton();

        _right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        _right.Children.Add(_github);
        _right.Children.Add(_search);
        _right.Children.Add(_language);
        _right.Children.Add(_appearance);
        _right.Children.Add(_themeToggle);
        // Icons only: the bar stays quiet, and each button says what it is when pointed at.
        SetIconOnly(_search, null, true, GalleryShellLangResourceKind.Search);
        _searchRow.Children[1].IsVisible = false;
        _searchRow.Children[2].IsVisible = false;
        _searchRow.MinWidth = 0;
        SetIconOnly(_language, _languageName, true, null);
        SetIconOnly(_appearance, _appearanceLabel, true, GalleryShellLangResourceKind.Appearance);
        // The desktop app keeps these two in its settings menu.
        _language.IsVisible = GalleryHost.IsWeb;
        _appearance.IsVisible = GalleryHost.IsWeb;
        if (GalleryHost.IsWeb)
        {
            (_download, _downloadLabel) = BuildDownloadButton(shell);
            _right.Children.Add(_download);
        }
        else
        {
            _gear = BuildSettingsMenu(shell);
            _right.Children.Add(_gear);
        }

        _header.TrailingContent = _right;
        _header.Label = GalleryText.Get(GalleryShellLangResourceKind.GoHome);

        BorderThickness = new Thickness(0, 0, 0, 1);
        Child = _header;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        // The menu marks the current language, whoever changed it (the menu, a host, a test). The labels change width with
        // it, hidden ones too, so the bar refits once the new text is in.
        GalleryText.Track(this, () =>
        {
            for (var i = 0; i < _navValues.Length; i++)
            {
                _header.Items[i].Label = GalleryText.Get(_navLabels[i]);
            }
            SyncComponentsMenu();
            SyncLanguage();
            _fittedLevel = -1;
            Dispatcher.UIThread.Post(InvalidateMeasure);
        });
    }

    /// <summary>
    /// How much room the bar has given up: 0 shows everything; 1 shows only the icon of the site's download button, 2 also
    /// folds the section menu into the header's menu button and drops the download button (a phone has nothing to install,
    /// and the home page offers it too), 3 also hides the GitHub button, 4 also leaves only the brand's logo.
    /// </summary>
    public int FitLevel => _fitLevel;

    /// <summary>
    /// Fits the bar to its width whenever it is measured: a new width, a label or the star count that changes size (they
    /// re-measure the bar), or a new language (it may widen labels that are hidden).
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var inner = Math.Min(availableSize.Width, HeaderMaxWidth) - 2 * SideMargin;
        if (double.IsFinite(inner) && inner > 0)
        {
            RememberWidths();
            if (NeedsFit(inner))
            {
                Fit(inner);
            }
        }
        var size = base.MeasureOverride(availableSize);
        _fittedNav = _navWidth;
        _fittedBrand = _brandWidth;
        _fittedButtons = ButtonsWidth(measure: false);
        return size;
    }

    /// <summary>
    /// Whether anything <see cref="Fit"/> depends on moved since it last ran. A fit measures every button again, and this is asked
    /// on each frame of a width spring in the bar, so it reads only what the buttons last reported. A label that changed has
    /// already reported its new width, or has yet to be measured.
    /// </summary>
    private bool NeedsFit(double inner)
    {
        if (inner != _fittedInner || _fitLevel != _fittedLevel || _navWidth != _fittedNav || _brandWidth != _fittedBrand)
        {
            return true;
        }
        foreach (var child in _right.Children)
        {
            if (child is Button { IsVisible: true, Content: Control { IsMeasureValid: false } })
            {
                return true;
            }
        }
        return ButtonsWidth(measure: false) != _fittedButtons;
    }

    /// <summary>
    /// Picks the fullest <see cref="FitLevel"/> whose parts fit in <paramref name="inner"/>. The bar is measured whenever
    /// anything in it changes size (a button's width spring does so every frame), so the search starts at the current level
    /// and only tries its neighbours: a level that still fits is checked against the next fuller one only when the bar
    /// has grown or its labels may have become shorter (a new language); one that no longer fits gives way to more compact ones.
    /// </summary>
    private void Fit(double inner)
    {
        var level = Math.Clamp(_fitLevel, 0, MostCompactLevel);
        Apply(level);
        if (Fits(inner))
        {
            if (inner > _fittedInner || _fitLevel != _fittedLevel)
            {
                while (level > 0)
                {
                    Apply(level - 1);
                    if (!Fits(inner))
                    {
                        break;
                    }
                    level--;
                }
            }
        }
        else
        {
            while (level < MostCompactLevel)
            {
                level++;
                Apply(level);
                if (Fits(inner))
                {
                    break;
                }
            }
        }
        Apply(level);
        _fittedInner = inner;
        _fittedLevel = level;
        // An Arc button springs its width to a new label. Measuring the buttons once under a constraint other than the row's
        // makes the change passive (SpringSizeDecorator), so they jump to the widths Fits counted on instead of overlapping
        // the menu on their way there.
        _right.Measure(Size.Infinity);
    }

    private bool Fits(double inner)
    {
        var right = ButtonsWidth(measure: true);
        RememberWidths();
        if (_fitLevel >= 2)
        {
            // The menu is folded away: brand and buttons share the bar, with the header's menu button after the buttons.
            return _brandWidth + Gap + right + MenuButtonWidth <= inner;
        }
        // The menu follows the brand and the buttons end the bar; the mega menu's items keep a wider gap than Gap to both.
        return _brandWidth + MenuInset + _navWidth + MenuInset + right <= inner;
    }

    /// <summary>Takes the widths of the header's brand and menu while they are shown (the header hides the menu when it folds).</summary>
    private void RememberWidths()
    {
        if (_navList is { IsVisible: true } nav && nav.DesiredSize.Width > 0)
        {
            _navWidth = nav.DesiredSize.Width;
        }
        if (_brandSlot is { IsVisible: true } brand && brand.DesiredSize.Width > 0)
        {
            _brandWidth = brand.DesiredSize.Width;
        }
    }

    /// <summary>The width of the buttons that are shown, with the spacing between them.</summary>
    private double ButtonsWidth(bool measure)
    {
        var right = 0.0;
        var shown = 0;
        foreach (var child in _right.Children)
        {
            if (child.IsVisible)
            {
                right += SettledWidth((Button)child, measure);
                shown++;
            }
        }
        return right + Math.Max(0, shown - 1) * _right.Spacing;
    }

    /// <summary>
    /// The width a button settles at: its content's natural width plus padding and border. The button itself would report
    /// the width its spring has reached so far.
    /// </summary>
    private static double SettledWidth(Button button, bool measure)
    {
        var content = 0.0;
        if (button.Content is Control control)
        {
            if (measure)
            {
                control.Measure(Size.Infinity);
            }
            content = control.DesiredSize.Width;
        }
        var chrome = button.Padding.Left + button.Padding.Right + button.BorderThickness.Left + button.BorderThickness.Right;
        return Math.Max(button.MinWidth, content + chrome) + button.Margin.Left + button.Margin.Right;
    }

    private void Apply(int level)
    {
        if (level == _fitLevel)
        {
            return;
        }
        _fitLevel = level;
        _header.CollapseWidth = level >= 2 ? double.MaxValue : 0;
        _github.IsVisible = level < 3;
        _header.BrandName = level >= 4 ? string.Empty : BrandName;
        if (_download is not null)
        {
            _download.IsVisible = level < 2 && GalleryHost.ClientOs != GalleryClientOs.Mobile;
            SetIconOnly(_download, _downloadLabel, level >= 1, GalleryShellLangResourceKind.NavDownload);
            Remeasure(_download);
            Remeasure(_downloadLabel!);
        }

        // A control re-measures only itself; its parents learn of a new size in the next layout pass. Fits measures now, so
        // mark everything between each changed part and the bar.
        Remeasure(_github);
    }

    private void Remeasure(Visual from)
    {
        for (var visual = from; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            (visual as Layoutable)?.InvalidateMeasure();
        }
    }

    /// <summary>
    /// Shows a button as a 36px square with its icon only (like the light / dark toggle), with its label as the tooltip.
    /// </summary>
    private void SetIconOnly(Button button, Control? label, bool iconOnly, GalleryShellLangResourceKind? tip)
    {
        if (label is not null)
        {
            label.IsVisible = !iconOnly;
        }
        if (iconOnly)
        {
            button.Padding = new Thickness(0);
            button.MinWidth = 36;
            if (tip is { } text && !_compactTips.ContainsKey(button))
            {
                _compactTips[button] = GalleryText.Bind(button, Tooltip.TipProperty, text);
            }
        }
        else
        {
            button.ClearValue(Avalonia.Controls.Primitives.TemplatedControl.PaddingProperty);
            button.ClearValue(MinWidthProperty);
            if (_compactTips.Remove(button, out var binding))
            {
                binding.Dispose();
                button.ClearValue(Tooltip.TipProperty);
            }
        }
    }

    /// <summary>
    /// The language button: the current language's own name, opening a menu of all of them in their own names. A choice
    /// is kept for the next session (<see cref="GalleryLanguages.Choose"/>).
    /// </summary>
    private static (Button Button, TextBlock Name, MenuSurface Menu) BuildLanguageMenu()
    {
        var name = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new ArcGlyph { Data = ArcIcons.Languages, Size = 16 });
        row.Children.Add(name);
        var button = new Button
        {
            Content = row,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small
        };
        GalleryText.Bind(button, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.Language);
        GalleryText.Bind(button, Tooltip.TipProperty, GalleryShellLangResourceKind.Language);

        var menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.End, AnchorGap = 6 };
        GalleryText.Bind(menu, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.Language);
        foreach (var language in GalleryLanguages.All)
        {
            var entry = new MenuEntry { Header = language.Name, ToggleType = MenuEntryToggleType.Radio, GroupName = "language" };
            var tag = language.Tag;
            entry.Click += (_, _) => GalleryLanguages.Choose(tag);
            menu.Items.Add(entry);
        }
        // Clicks and the arrow keys open it; Escape returns focus to the button.
        DropdownMenu.SetMenu(button, menu);
        return (button, name, menu);
    }

    private void SyncLanguage()
    {
        var current = GalleryText.CurrentLanguage;
        var languages = GalleryLanguages.All;
        var index = Enumerable.Range(0, languages.Count)
                              .FirstOrDefault(i => string.Equals(languages[i].Tag, current, StringComparison.OrdinalIgnoreCase));
        _languageName.Text = languages[index].Name;
        for (var i = 0; i < _languageMenu.Items.Count; i++)
        {
            if (_languageMenu.Items[i] is MenuEntry entry)
            {
                entry.IsChecked = i == index;
            }
        }
    }

    /// <summary>The Components menu: the parts of the library, and the demo built from them.</summary>
    private void BuildComponentsMenu(GalleryShell shell)
    {
        void Add(GalleryShellLangResourceKind label, GalleryShellLangResourceKind text, Geometry icon, Func<GalleryRoute> route)
        {
            var link = new SiteHeaderLink { Icon = icon };
            _componentLinks.Add((link, label, text, route));
            _header.Items[0].Links.Add(link);
        }

        Add(GalleryShellLangResourceKind.ProductAllComponents, GalleryShellLangResourceKind.ProductArcNav, ArcIcons.Layers, () => GalleryRoute.ComponentsIndex);
        Add(GalleryShellLangResourceKind.Category_Blocks, GalleryShellLangResourceKind.ProductBlocksNav, ArcIcons.LayoutTemplate, () => shell.RouteFor(GalleryRouteKind.Blocks));
        Add(GalleryShellLangResourceKind.ProductSpecial, GalleryShellLangResourceKind.ProductSpecialNav, ArcIcons.Sparkles, () => GalleryRoute.To(GalleryCatalog.Special[0].AllEntries.First()));
        Add(GalleryShellLangResourceKind.ProductMobile, GalleryShellLangResourceKind.ProductMobileNav, GalleryIcons.Smartphone, () => GalleryRoute.To(GalleryCatalog.Mobile.Entries[0]));
        Add(GalleryShellLangResourceKind.NavDemo, GalleryShellLangResourceKind.ProductDemoNav, ArcIcons.Monitor, () => GalleryRoute.Demo());
        Add(GalleryShellLangResourceKind.ProductTemplates, GalleryShellLangResourceKind.ProductTemplatesNav, ArcIcons.Package, () => GallerySite.Docs("DocsTemplates"));
        SyncComponentsMenu();
    }

    private void SyncComponentsMenu()
    {
        foreach (var (link, label, text, _) in _componentLinks)
        {
            link.Label = GalleryText.Get(label);
            link.Description = GalleryText.Get(text);
        }
    }

    private static (Button Button, TextBlock Label) BuildDownloadButton(GalleryShell shell)
    {
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(label, TextBlock.TextProperty, GalleryShellLangResourceKind.NavDownload);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new ArcGlyph { Data = ArcIcons.Download, Size = 16, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(label);
        var button = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, Content = row, IsPressScaleEnabled = false };
        GalleryText.Bind(button, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.NavDownload);
        button.Click += (_, _) => shell.ShowDownload();
        return (button, label);
    }

    /// <summary>The desktop app's settings menu: Appearance, Language, Check for updates and About.</summary>
    private Button BuildSettingsMenu(GalleryShell shell)
    {
        var menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.End, AnchorGap = 6 };
        GalleryText.Bind(menu, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.Settings);

        void Add(GalleryShellLangResourceKind label, Geometry icon, Action click)
        {
            var entry = new MenuEntry { Icon = new ArcGlyph { Data = icon, Size = 15 } };
            GalleryText.Bind(entry, MenuEntry.HeaderProperty, label);
            entry.Click += (_, _) => click();
            menu.Items.Add(entry);
        }

        Add(GalleryShellLangResourceKind.Appearance, ArcIcons.Palette, () => shell.Navigate(GalleryRoute.Settings(GallerySettingsView.AppearancePage)));
        Add(GalleryShellLangResourceKind.Language, ArcIcons.Languages, () => shell.Navigate(GalleryRoute.Settings(GallerySettingsView.LanguagePage)));
        Add(GalleryShellLangResourceKind.SettingsCheckUpdates, ArcIcons.RefreshCw, shell.CheckForUpdates);
        Add(GalleryShellLangResourceKind.SettingsAboutApp, ArcIcons.Info, () => shell.Navigate(GalleryRoute.Settings(GallerySettingsView.AboutPage)));
        SettingsMenu = menu;

        var button = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            Padding = new Thickness(0),
            MinWidth = 36,
            Content = new ArcGlyph { Data = ArcIcons.Settings, Size = 16 }
        };
        GalleryText.Bind(button, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.Settings);
        GalleryText.Bind(button, Tooltip.TipProperty, GalleryShellLangResourceKind.Settings);
        DropdownMenu.SetMenu(button, menu);
        return button;
    }

    /// <summary>The desktop app's settings menu; null on the site.</summary>
    public MenuSurface? SettingsMenu { get; private set; }

    /// <summary>The desktop app's settings button; null on the site.</summary>
    public Button? SettingsButton => _gear;

    /// <summary>The site's download button; null in the desktop app.</summary>
    public Button? DownloadButton => _download;

    /// <summary>The language button of the bar; it opens the language menu.</summary>
    public Button LanguageButton => _language;

    /// <summary>The language menu (one radio row per language of <see cref="GalleryLanguages.All"/>).</summary>
    public MenuSurface LanguageMenu => _languageMenu;

    private static (Button Button, Grid Row) BuildSearchButton(Action focusSearch)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), MinWidth = SearchMinWidth };
        var icon = new ArcGlyph { Data = ArcIcons.Search, Size = 16 };
        var label = new TextBlock { Margin = new Thickness(8, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(label, TextBlock.TextProperty, GalleryShellLangResourceKind.Search);
        var hint = GalleryUi.Text(OperatingSystem.IsMacOS() ? "⌘K" : "Ctrl K", ArcDesignTokenKind.TextMuted, 12);
        hint.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(label, 1);
        Grid.SetColumn(hint, 2);
        grid.Children.Add(icon);
        grid.Children.Add(label);
        grid.Children.Add(hint);
        var button = new Button
        {
            Content = grid,
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsPressScaleEnabled = false
        };
        GalleryText.Bind(button, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.Search);
        button.Click += (_, _) => focusSearch();
        return (button, grid);
    }

    /// <summary>Marks the menu item of the open page (none on the home page).</summary>
    public void SyncRoute(GalleryRoute route)
    {
        _header.Current = route.Kind switch
        {
            GalleryRouteKind.Components or GalleryRouteKind.Blocks or GalleryRouteKind.ComponentsIndex => "components",
            GalleryRouteKind.Docs => "docs",
            GalleryRouteKind.Projects or GalleryRouteKind.StreamMark => "projects",
            _ => null
        };
    }

    /// <summary>The index of the menu item that is marked, or -1 (see <see cref="SiteHeader.Current"/>).</summary>
    public int MarkedNavIndex => _header.Current is { } current ? Array.IndexOf(_navValues, current) : -1;

    /// <summary>The header block that draws the brand and the menu.</summary>
    public SiteHeader Header => _header;

    /// <summary>The buttons at the end of the bar: GitHub, search, language, Appearance, the light / dark toggle and the host's own.</summary>
    public StackPanel Buttons => _right;

    private void SyncThemeToggle()
    {
        // Setting new content lets the button's own content swap play the icon change.
        _themeToggle.Content = new ArcGlyph { Data = _theme.IsDark ? ArcIcons.Moon : GalleryIcons.Sun, Size = 16 };
    }

    /// <summary>Opens or closes the Appearance popover.</summary>
    public void ToggleAppearance(Control owner)
    {
        if (_popup is { IsOpen: true } open)
        {
            open.IsOpen = false;
            return;
        }
        // Pressing the button while the popover is open light-dismisses it first; that same click must not reopen it.
        if (DateTime.UtcNow - _closedAt < TimeSpan.FromMilliseconds(250))
        {
            return;
        }
        if (_popup is null)
        {
            _popup = new ArcPopup
            {
                Child = new GalleryAppearancePanel(_theme, owner),
                PlacementTarget = _appearance,
                Placement = PlacementMode.BottomEdgeAlignedRight,
                MarginToAnchor = 8,
                EnterOffset = 5
            };
            _popup.Closed += (_, _) =>
            {
                _closedAt = DateTime.UtcNow;
                if (_popup is { IsOpen: false, Parent: not null } closed)
                {
                    ((ISetLogicalParent)closed).SetParent(null);
                }
            };
        }
        if (_popup.Parent is null)
        {
            ((ISetLogicalParent)_popup).SetParent(_appearance);
        }
        _popup.IsOpen = true;
    }
}
