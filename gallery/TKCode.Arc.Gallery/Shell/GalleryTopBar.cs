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
/// The top bar of uiarc.dev: brand logo (back to the home page), the menu of sections, a GitHub button with the star
/// count, a search shortcut, the language menu, the Appearance popover and the light / dark toggle. When the buttons do
/// not fit beside the centred menu, the bar gives up room step by step (<see cref="FitLevel"/>).
/// </summary>
public sealed class GalleryTopBar : Border
{
    private const string ChangelogClassName = "DocsChangelog";
    private const double SideMargin = 24;

    /// <summary>The header's content is at most this wide (its <c>InnerMaxWidth</c>).</summary>
    private const double HeaderMaxWidth = 1200;
    private const double MenuButtonWidth = 44;
    private const double Gap = 16;
    private const double SearchMinWidth = 148;
    private const int MostCompactLevel = 7;

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
    private readonly string[] _navValues = ["components", "blocks", "docs", "changelog", "friends"];
    private readonly GalleryShellLangResourceKind[] _navLabels =
    [
        GalleryShellLangResourceKind.Components, GalleryShellLangResourceKind.Category_Blocks, GalleryShellLangResourceKind.Category_Docs,
        GalleryShellLangResourceKind.NavChangelog, GalleryShellLangResourceKind.NavFriends
    ];
    private double _navWidth = 420;
    private double _brandWidth = 150;
    private readonly GalleryShell _shell;
    private readonly Dictionary<Button, IDisposable> _compactTips = new();
    private ArcPopup? _popup;
    private DateTime _closedAt;
    private int _fitLevel = -1;

    public GalleryTopBar(GalleryTheme theme, GalleryShell shell)
    {
        _theme = theme;
        _shell = shell;
        Control owner = shell;

        // The site header block draws the brand and the section menu (a capsule, as on uiarc.dev); the buttons ride in its
        // trailing slot. Components, Blocks, Docs, Changelog and Friends; the last two are pages of the Docs section.
        _header = new SiteHeader
        {
            Variant = SiteHeaderVariant.Simple,
            BrandName = "Arc Gallery",
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
        GalleryText.Bind(_header, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.GoHome);
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
                        "components" => shell.RouteFor(GalleryRouteKind.Components),
                        "blocks" => shell.RouteFor(GalleryRouteKind.Blocks),
                        "docs" => shell.RouteFor(GalleryRouteKind.Docs),
                        "changelog" => GalleryRoute.To(GalleryCatalog.Docs.Entries.First(entry => entry.ClassName == ChangelogClassName)),
                        _ => GalleryRoute.Friends
                    });
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
            SyncLanguage();
            Dispatcher.UIThread.Post(InvalidateMeasure);
        });
    }

    /// <summary>
    /// How much room the bar has given up: 0 shows everything; 1 shows only the icon of Appearance, 2 also of the language
    /// button, 3 also drops the shortcut hint of Search, 4 also folds the section menu into the header's menu button, 5 also
    /// shortens Search to its icon, 6 also hides the GitHub button, 7 also shortens the brand to "Arc" (a phone).
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
            Fit(inner);
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Picks the fullest <see cref="FitLevel"/> whose parts fit in <paramref name="inner"/>, measuring each level in turn.</summary>
    private void Fit(double inner)
    {
        var level = 0;
        for (; level < MostCompactLevel; level++)
        {
            Apply(level);
            if (Fits(inner))
            {
                break;
            }
        }
        Apply(level);
        // An Arc button springs its width to a new label. Measuring the buttons once under a constraint other than the row's
        // makes the change passive (SpringSizeDecorator), so they jump to the widths Fits counted on instead of overlapping
        // the menu on their way there.
        _right.Measure(Size.Infinity);
    }

    private bool Fits(double inner)
    {
        var right = 0.0;
        var shown = 0;
        foreach (var child in _right.Children)
        {
            if (child.IsVisible)
            {
                right += SettledWidth((Button)child);
                shown++;
            }
        }
        right += Math.Max(0, shown - 1) * _right.Spacing;
        RememberWidths();
        if (_fitLevel >= 4)
        {
            // The menu is folded away: brand and buttons share the bar, with the header's menu button after the buttons.
            return _brandWidth + Gap + right + MenuButtonWidth <= inner;
        }
        // The menu follows the brand (16 further in) and the buttons end the bar.
        return _brandWidth + Gap + 16 + _navWidth + Gap + right <= inner;
    }

    /// <summary>Takes the widths of the header's brand and menu while they are shown (the header hides the menu when it folds).</summary>
    private void RememberWidths()
    {
        foreach (var part in _header.GetVisualDescendants().OfType<Control>())
        {
            if (!part.IsVisible)
            {
                continue;
            }
            if (part.Name == "PART_NavList" && part.DesiredSize.Width > 0)
            {
                _navWidth = part.DesiredSize.Width;
            }
            else if (part.Name == "PART_BrandSlot" && part.DesiredSize.Width > 0)
            {
                _brandWidth = part.DesiredSize.Width;
            }
        }
    }

    /// <summary>
    /// The width a button settles at: its content's natural width plus padding and border. The button itself would report
    /// the width its spring has reached so far.
    /// </summary>
    private static double SettledWidth(Button button)
    {
        var content = 0.0;
        if (button.Content is Control control)
        {
            control.Measure(Size.Infinity);
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
        SetIconOnly(_appearance, _appearanceLabel, level >= 1, GalleryShellLangResourceKind.Appearance);
        SetIconOnly(_language, _languageName, level >= 2, null);
        // The search row is the icon, the label and the shortcut hint.
        var searchShort = level >= 3;
        var searchIconOnly = level >= 5;
        _searchRow.Children[1].IsVisible = !searchIconOnly;
        _searchRow.Children[1].Margin = new Thickness(8, 0, searchShort ? 0 : 12, 0);
        _searchRow.Children[2].IsVisible = !searchShort;
        _searchRow.MinWidth = searchShort ? 0 : SearchMinWidth;
        SetIconOnly(_search, null, searchIconOnly, GalleryShellLangResourceKind.Search);
        _header.CollapseWidth = level >= 4 ? double.MaxValue : 0;
        _github.IsVisible = level < 6;
        _header.BrandName = level >= 7 ? "Arc" : "Arc Gallery";

        // A control re-measures only itself; its parents learn of a new size in the next layout pass. Fits measures now, so
        // mark everything between each changed part and the bar.
        Remeasure(_appearanceLabel);
        Remeasure(_languageName);
        foreach (var child in _searchRow.Children)
        {
            Remeasure(child);
        }
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
            GalleryRouteKind.Components => "components",
            GalleryRouteKind.Blocks => "blocks",
            GalleryRouteKind.Docs => route.Entry?.ClassName == ChangelogClassName ? "changelog" : "docs",
            GalleryRouteKind.Friends => "friends",
            _ => null
        };
    }

    /// <summary>The index of the menu item that is marked, or -1 (see <see cref="SiteHeader.Current"/>).</summary>
    public int MarkedNavIndex => _header.Current is { } current ? Array.IndexOf(_navValues, current) : -1;

    /// <summary>The header block that draws the brand and the menu.</summary>
    public SiteHeader Header => _header;

    /// <summary>The buttons at the end of the bar: GitHub, search, language, Appearance and the light / dark toggle.</summary>
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
