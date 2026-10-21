using TKCode.Arc.DesignTokens;
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
/// The top bar of uiarc.dev: brand logo, the section tab, a search shortcut, the Appearance popover and the
/// light / dark toggle.
/// </summary>
public sealed class GalleryTopBar : Border
{
    private readonly GalleryTheme _theme;
    private readonly Button _themeToggle;
    private readonly Button _appearance;
    private ArcPopup? _popup;
    private DateTime _closedAt;

    public GalleryTopBar(GalleryTheme theme, Control owner, Action focusSearch)
    {
        _theme = theme;

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 28, VerticalAlignment = VerticalAlignment.Stretch };
        left.Children.Add(BuildBrand());
        left.Children.Add(BuildTab(GalleryShellLangResourceKind.Components));

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

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        right.Children.Add(BuildSearchButton(focusSearch));
        right.Children.Add(_appearance);
        right.Children.Add(_themeToggle);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(24, 0) };
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);

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

    /// <summary>The current section: label with the 2px underline sitting on the bar's bottom border.</summary>
    private static Control BuildTab(GalleryShellLangResourceKind label)
    {
        var text = GalleryUi.Text(string.Empty, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
        GalleryText.Bind(text, TextBlock.TextProperty, label);
        var tab = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 2),
            Margin = new Thickness(0, 0, 0, -1),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = text
        };
        text.VerticalAlignment = VerticalAlignment.Center;
        ArcTokens.Bind(tab, BorderBrushProperty, ArcDesignTokenKind.Foreground);
        return tab;
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
