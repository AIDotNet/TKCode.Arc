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

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The top bar of uiarc.dev: brand logo (back to the home page), the menu of sections, a GitHub button with the star
/// count, a search shortcut, the Appearance popover and the light / dark toggle.
/// </summary>
public sealed class GalleryTopBar : Border
{
    private const double NavMinWidth = 880;
    private const string ChangelogClassName = "DocsChangelog";

    private readonly GalleryTheme _theme;
    private readonly Button _themeToggle;
    private readonly Button _appearance;
    private readonly GalleryTopNav _nav;
    private readonly GalleryShell _shell;
    private ArcPopup? _popup;
    private DateTime _closedAt;

    public GalleryTopBar(GalleryTheme theme, GalleryShell shell)
    {
        _theme = theme;
        _shell = shell;
        Control owner = shell;

        var brand = new Button
        {
            Content = BuildBrand(),
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Padding = new Thickness(6, 4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            IsPressScaleEnabled = false
        };
        GalleryText.Bind(brand, Avalonia.Automation.AutomationProperties.NameProperty, GalleryShellLangResourceKind.GoHome);
        brand.Click += (_, _) => shell.Navigate(GalleryRoute.Home);

        // Components, Blocks, Docs and Changelog; the last two are pages of the Docs section.
        _nav = new GalleryTopNav(GalleryShellLangResourceKind.Components, GalleryShellLangResourceKind.Category_Blocks,
                                 GalleryShellLangResourceKind.Category_Docs, GalleryShellLangResourceKind.NavChangelog);
        _nav.Chosen += index => shell.Navigate(index switch
        {
            0 => shell.RouteFor(GalleryRouteKind.Components),
            1 => shell.RouteFor(GalleryRouteKind.Blocks),
            2 => shell.RouteFor(GalleryRouteKind.Docs),
            _ => GalleryRoute.To(GalleryCatalog.Docs.Entries.First(e => e.ClassName == ChangelogClassName))
        });

        _appearance = new Button
        {
            Content = Labeled(new ArcGlyph { Data = ArcIcons.SlidersHorizontal, Size = 16 }, GalleryShellLangResourceKind.Appearance),
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            IsPressScaleEnabled = false
        };
        _appearance.Click += (_, _) => ToggleAppearance(owner);

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

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        right.Children.Add(new GalleryGitHubButton());
        right.Children.Add(BuildSearchButton(shell.FocusSearch));
        right.Children.Add(_appearance);
        right.Children.Add(_themeToggle);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), Margin = new Thickness(24, 0) };
        Grid.SetColumn(_nav, 1);
        Grid.SetColumn(right, 2);
        grid.Children.Add(brand);
        grid.Children.Add(_nav);
        grid.Children.Add(right);
        // The menu needs the room of the right-hand buttons on a narrow page; the brand still leads home.
        grid.SizeChanged += (_, e) => _nav.IsVisible = e.NewSize.Width >= NavMinWidth;

        Height = 64;
        BorderThickness = new Thickness(0, 0, 0, 1);
        Child = grid;
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
    }

    private static Control BuildBrand()
    {
        // The logo carries its own light rounded tile, so it reads the same on the light and the dark bar.
        var mark = new Image { Source = GalleryBrand.CreateLogo(), Width = 30, Height = 30 };
        RenderOptions.SetBitmapInterpolationMode(mark, BitmapInterpolationMode.HighQuality);
        var name = GalleryUi.Text("Arc", ArcDesignTokenKind.Foreground, 20, FontWeight.SemiBold);
        var tag = GalleryUi.Chip(GalleryUi.Text("Gallery", ArcDesignTokenKind.TextSecondary, 11, FontWeight.Medium), padding: new Thickness(8, 2));
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(mark);
        brand.Children.Add(name);
        brand.Children.Add(tag);
        return brand;
    }

    private static Control Labeled(Control glyph, GalleryShellLangResourceKind text)
    {
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        GalleryText.Bind(label, TextBlock.TextProperty, text);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(glyph);
        row.Children.Add(label);
        return row;
    }

    private static Control BuildSearchButton(Action focusSearch)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), MinWidth = 148 };
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
        button.Click += (_, _) => focusSearch();
        return button;
    }

    /// <summary>Marks the menu item of the open page (none on the home page).</summary>
    public void SyncRoute(GalleryRoute route)
    {
        _nav.Mark(route.Kind switch
        {
            GalleryRouteKind.Components => 0,
            GalleryRouteKind.Blocks => 1,
            GalleryRouteKind.Docs => route.Entry?.ClassName == ChangelogClassName ? 3 : 2,
            _ => -1
        });
    }

    /// <summary>The item of the menu that is marked, or -1 (see <see cref="GalleryTopNav"/>).</summary>
    public int MarkedNavIndex => _nav.MarkedIndex;

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
