using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The header of a screen's main card (Arc screen shell, design reference): 52 high (48 on phones) with a 1px divider,
/// padding 0 12 0 20 (0 8 0 12 on phones). From the left: the menu button (only while the sidebar is a sheet), the
/// breadcrumbs (muted crumbs with 13px chevrons at 70%, the current title 13/500, a 12px muted meta), then the actions
/// (gap 8). The search control sits first among the actions while the card is wider than <see cref="SearchInlineWidth"/>,
/// otherwise in its own row under the header (padding 0 20 10).
/// </summary>
internal sealed class ScreenHeader : Border
{
    /// <summary>Arc moves the filter under the header when the main card is 760 wide or less.</summary>
    public const double SearchInlineWidth = 760;

    private readonly Grid _row;
    private readonly Border _menuSlot = new() { Margin = new Thickness(-6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ArcShrinkRow _crumbs = new() { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, ClipToBounds = true };
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _searchInline = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _searchBelow = new() { Padding = new Thickness(20, 0, 20, 10) };
    private Control? _search;
    private bool _below;
    private bool _phone;

    public ScreenHeader()
    {
        _row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8,
            Height = 52,
            Margin = new Thickness(20, 0, 12, 0),
        };
        Grid.SetColumn(_menuSlot, 0);
        Grid.SetColumn(_crumbs, 1);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(_searchInline);
        actions.Children.Add(_actions);
        Grid.SetColumn(actions, 2);
        _row.Children.Add(_menuSlot);
        _row.Children.Add(_crumbs);
        _row.Children.Add(actions);
        var stack = new StackPanel();
        stack.Children.Add(_row);
        stack.Children.Add(_searchBelow);
        Child = stack;
        BorderThickness = new Thickness(0, 0, 0, 1);
        _menuSlot.IsVisible = false;
        _searchBelow.IsVisible = false;
    }

    /// <summary>The menu button shown while the sidebar is a sheet.</summary>
    public Control? MenuButton
    {
        get => _menuSlot.Child;
        set => _menuSlot.Child = value;
    }

    /// <summary>Shows the menu button.</summary>
    public bool IsMenuVisible
    {
        get => _menuSlot.IsVisible;
        set => _menuSlot.IsVisible = value;
    }

    /// <summary>The breadcrumb row (crumbs, title, meta); the screen fills it.</summary>
    public ArcShrinkRow Crumbs => _crumbs;

    /// <summary>The action controls after the search.</summary>
    public Controls Actions => _actions.Children;

    /// <summary>The search control (inline or below by width).</summary>
    public Control? Search
    {
        get => _search;
        set
        {
            _search = value;
            Place();
        }
    }

    /// <summary>True while the search sits in its own row.</summary>
    public bool IsSearchBelow => _below;

    /// <summary>Lays the header out for a main card of <paramref name="width"/>; true on phones (≤ 560 shell width).</summary>
    public void Fit(double width, bool phone)
    {
        var below = width <= SearchInlineWidth;
        if (below != _below || phone != _phone)
        {
            _below = below;
            _phone = phone;
            _row.Height = phone ? 48 : 52;
            _row.Margin = phone ? new Thickness(12, 0, 8, 0) : new Thickness(20, 0, 12, 0);
            Place();
        }
    }

    private void Place()
    {
        _searchInline.Child = null;
        _searchBelow.Child = null;
        if (_search is null)
        {
            _searchBelow.IsVisible = false;
            return;
        }
        if (_below)
        {
            _searchBelow.Child = _search;
            _searchBelow.IsVisible = true;
            _search.Width = double.NaN;
        }
        else
        {
            _searchInline.Child = _search;
            _searchBelow.IsVisible = false;
            _search.Width = 196;
        }
    }

    /// <summary>Builds a muted crumb ("Calendar") followed by a chevron.</summary>
    public static Control Crumb(ScreenPaint paint, string text, Action? onClick = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        Control label;
        if (onClick is null)
        {
            label = ScreenText.Line(text, 13, paint.MutedBrush);
        }
        else
        {
            var button = new ScreenPressable
            {
                Radius = 6,
                Padding = new Thickness(6, 4),
                Margin = new Thickness(-6, 0),
                HoverFill = paint.HoverFill,
                FocusPen = paint.FocusPen,
                Child = ScreenText.Line(text, 13, paint.MutedBrush),
            };
            Avalonia.Automation.AutomationProperties.SetName(button, text);
            button.Clicked += _ => onClick();
            label = button;
        }
        ArcShrinkRow.SetIsShrinkable(row, true);
        row.Children.Add(label);
        row.Children.Add(new ArcGlyph
        {
            Data = ArcIcons.ChevronRight,
            Size = 13,
            StrokeThickness = 1.75,
            Foreground = paint.MutedBrush,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    public void ApplyPaint(ScreenPaint paint) => BorderBrush = paint.SubtleBrush;
}
