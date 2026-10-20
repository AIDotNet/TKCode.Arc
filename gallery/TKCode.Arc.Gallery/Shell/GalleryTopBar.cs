using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The top bar of uiarc.dev: brand mark, the section tab, a search shortcut, the Appearance popover and the
/// light / dark toggle.
/// </summary>
public sealed class GalleryTopBar : Border
{
    // Three nested arches: the Arc mark, drawn as 24x24 stroke geometry like every other glyph.
    private static readonly Geometry s_mark = ArcIcons.Parse("M3 21V12a9 9 0 0 1 18 0v9M7.5 21v-9a4.5 4.5 0 0 1 9 0v9M12 21v-9");

    private readonly GalleryTheme _theme;
    private readonly Button _themeToggle;
    private readonly Button _appearance;
    private ArcPopup? _popup;
    private DateTime _closedAt;

    public GalleryTopBar(GalleryTheme theme, Window owner, Action focusSearch)
    {
        _theme = theme;

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 28, VerticalAlignment = VerticalAlignment.Stretch };
        left.Children.Add(BuildBrand());
        left.Children.Add(BuildTab("Components"));

        _appearance = new Button
        {
            Content = Labeled(new ArcGlyph { Data = ArcIcons.SlidersHorizontal, Size = 16 }, "Appearance"),
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
        Avalonia.Automation.AutomationProperties.SetName(_themeToggle, "Toggle light and dark appearance");
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
        var mark = new ArcGlyph { Data = s_mark, Size = 28, StrokeThickness = 2 };
        ArcTokens.Bind(mark, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var name = GalleryUi.Text("Arc", ArcDesignTokenKind.Foreground, 20, FontWeight.SemiBold);
        var tag = GalleryUi.Chip(GalleryUi.Text("Gallery", ArcDesignTokenKind.TextSecondary, 11, FontWeight.Medium), padding: new Thickness(8, 2));
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(mark);
        brand.Children.Add(name);
        brand.Children.Add(tag);
        return brand;
    }

    /// <summary>The current section: label with the 2px underline sitting on the bar's bottom border.</summary>
    private static Control BuildTab(string label)
    {
        var tab = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 2),
            Margin = new Thickness(0, 0, 0, -1),
            VerticalAlignment = VerticalAlignment.Stretch,
            Child = GalleryUi.Text(label, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium)
        };
        ((TextBlock)tab.Child).VerticalAlignment = VerticalAlignment.Center;
        ArcTokens.Bind(tab, BorderBrushProperty, ArcDesignTokenKind.Foreground);
        return tab;
    }

    private static Control Labeled(Control glyph, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(glyph);
        row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return row;
    }

    private static Control BuildSearchButton(Action focusSearch)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), MinWidth = 148 };
        var icon = new ArcGlyph { Data = ArcIcons.Search, Size = 16 };
        var label = new TextBlock { Text = "Search", Margin = new Thickness(8, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
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
    public void ToggleAppearance(Window owner)
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
