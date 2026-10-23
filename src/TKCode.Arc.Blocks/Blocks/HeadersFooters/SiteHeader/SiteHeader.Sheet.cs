// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli).
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class SiteHeader
{
    private Border? _sheet;
    private SiteHeightBox? _sheetBox;
    private Border? _scrim;
    private ScrollViewer? _sheetScroll;
    private StackPanel? _sheetNav;
    private MotionValue<double>? _scrimOpacity;
    private string? _expandedGroup;
    private bool _sheetExiting;
    private int _sheetGeneration;
    private readonly Dictionary<string, (SiteSheetRow Row, SiteHeightBox Box)> _groups = new(StringComparer.Ordinal);
    private readonly List<SiteSheetRow> _sheetRows = new();

    /// <summary>The menu sheet while it is on screen (tests).</summary>
    internal Border? Sheet => _sheet;

    internal SiteHeightBox? SheetBox => _sheetBox;

    internal Border? Scrim => _scrim;

    internal ScrollViewer? SheetScroll => _sheetScroll;

    /// <summary>The value of the group that is expanded in the sheet, or null.</summary>
    internal string? ExpandedGroup => _expandedGroup;

    internal IReadOnlyList<SiteSheetRow> SheetRows => _sheetRows;

    internal IReadOnlyDictionary<string, (SiteSheetRow Row, SiteHeightBox Box)> SheetGroups => _groups;

    private void OnMenuOpenChanged(bool open)
    {
        UpdateAutomation();
        UpdatePseudoClasses();
        UpdateOutside();
        if (!open)
        {
            _expandedGroup = null;
        }
        if (_sheetLayer is null)
        {
            return;
        }
        if (open)
        {
            OpenSheet();
        }
        else
        {
            CloseSheet();
        }
    }

    private void CloseMenuCore(bool restoreFocus)
    {
        if (!IsMenuOpen)
        {
            return;
        }
        IsMenuOpen = false;
        if (restoreFocus)
        {
            _menuButton?.Focus(NavigationMethod.Directional);
        }
    }

    // Metrics of SiteHeaderToken: SheetPadding 8 16 20, SheetChrome 65, SheetActionsTop 20, SheetActionsGap 8, SheetGroupSpace 12.
    private void OpenSheet()
    {
        if (_sheetLayer is null)
        {
            return;
        }
        if (_sheet is not null)
        {
            // Opened again while it was still leaving: it comes back from where it is.
            _sheetExiting = false;
            _sheetGeneration++;
            _sheet.IsHitTestVisible = true;
            if (_scrim is not null)
            {
                _scrim.IsHitTestVisible = true;
            }
            if (_sheetBox is not null)
            {
                _sheetBox.IsOpen = true;
            }
            FadeScrim(1);
            return;
        }
        _sheetLayer.ScrimHeight = ViewportHeight();

        var scrim = new Border { Background = new SolidColorBrush(Color.FromArgb(0x29, 0, 0, 0)) };
        SiteHeaderFloatLayer.SetPlacement(scrim, SiteHeaderPlacement.Scrim);
        scrim.PointerPressed += (_, e) =>
        {
            CloseMenuCore(false);
            e.Handled = true;
        };
        AutomationProperties.SetAccessibilityView(scrim, AccessibilityView.Raw);
        _scrim = scrim;
        _scrimOpacity = null;

        var nav = new StackPanel { Margin = new Thickness(16, 8, 16, 20) };
        AutomationProperties.SetName(nav, Label);
        AutomationProperties.SetLandmarkType(nav, AutomationLandmarkType.Navigation);
        _sheetNav = nav;
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            MaxHeight = Math.Max(0, ViewportHeight() - 65),
            Content = nav
        };
        // overscroll-behavior: contain: the wheel never leaks to the page behind.
        scroll.PointerWheelChanged += (_, e) => e.Handled = true;
        _sheetScroll = scroll;
        var box = new SiteHeightBox { StartCollapsed = true, Child = scroll };
        box.Arrived += OnSheetArrived;
        _sheetBox = box;
        var chrome = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = box, ClipToBounds = true };
        ArcTokens.Bind(chrome, Border.BackgroundProperty, ArcDesignTokenKind.Background);
        ArcTokens.Bind(chrome, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        SiteHeaderFloatLayer.SetPlacement(chrome, SiteHeaderPlacement.Sheet);
        _sheet = chrome;
        _sheetExiting = false;

        BuildSheetRows();
        _sheetLayer.Children.Add(scrim);
        _sheetLayer.Children.Add(chrome);
        if (ArcMotion.IsFull(this))
        {
            ScrimValue().Jump(0);
        }
        FadeScrim(1);
    }

    private MotionValue<double> ScrimValue()
    {
        if (_scrimOpacity is null && _scrim is not null)
        {
            var scrim = _scrim;
            _scrimOpacity = new MotionValue<double>(scrim, scrim.Opacity, MotionVectors.Double);
            _scrimOpacity.Changed += v => scrim.Opacity = v;
        }
        return _scrimOpacity!;
    }

    /// <summary>Arc: the scrim fades over 240 ms (standard); reduced motion and Off show it at once.</summary>
    private void FadeScrim(double to)
    {
        if (_scrim is null)
        {
            return;
        }
        if (ArcMotion.IsFull(this))
        {
            _ = ScrimValue().AnimateTo(to, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
        }
        else
        {
            ScrimValue().Jump(to);
        }
    }

    private void CloseSheet()
    {
        if (_sheet is null || _sheetBox is null)
        {
            return;
        }
        _sheetExiting = true;
        _sheetGeneration++;
        _sheet.IsHitTestVisible = false;
        if (_scrim is not null)
        {
            _scrim.IsHitTestVisible = false;
        }
        FadeScrim(0);
        if (_sheetBox.CurrentHeight <= 0.5 && !_sheetBox.IsMoving)
        {
            RemoveSheet();
            return;
        }
        // Arc: the sheet leaves on SHRINK, then it is gone.
        _sheetBox.IsOpen = false;
    }

    private void OnSheetArrived(object? sender, EventArgs e)
    {
        if (_sheetExiting && _sheetBox is { IsOpen: false })
        {
            RemoveSheet();
        }
    }

    private void RemoveSheet()
    {
        if (_sheetLayer is not null)
        {
            if (_sheet is not null)
            {
                _sheetLayer.Children.Remove(_sheet);
            }
            if (_scrim is not null)
            {
                _sheetLayer.Children.Remove(_scrim);
            }
        }
        if (_sheetBox is not null)
        {
            _sheetBox.Arrived -= OnSheetArrived;
        }
        _sheet = null;
        _sheetBox = null;
        _scrim = null;
        _scrimOpacity = null;
        _sheetScroll = null;
        _sheetNav = null;
        _sheetExiting = false;
        _groups.Clear();
        _sheetRows.Clear();
    }

    /// <summary>Viewport size changed: the sheet's list may use the viewport minus the bar, the scrim covers it.</summary>
    private void UpdateSheetLimits()
    {
        if (_sheet is null)
        {
            return;
        }
        var height = ViewportHeight();
        if (_sheetLayer is not null)
        {
            _sheetLayer.ScrimHeight = height;
        }
        if (_sheetScroll is not null)
        {
            _sheetScroll.MaxHeight = Math.Max(0, height - 65);
        }
    }

    private void RebuildSheet()
    {
        if (_sheet is null)
        {
            return;
        }
        BuildSheetRows();
    }

    private void RebuildSheetActions() => RebuildSheet();

    private void BuildSheetRows()
    {
        if (_sheetNav is null)
        {
            return;
        }
        _sheetNav.Children.Clear();
        _groups.Clear();
        _sheetRows.Clear();
        var index = 0;
        foreach (var item in _resolved)
        {
            var captured = item;
            var group = HasPanel(item);
            var row = new SiteSheetRow
            {
                Label = item.Label,
                IsGroup = group,
                IsExpandable = group,
                IsCurrent = !group && string.Equals(item.Value, Current, StringComparison.Ordinal),
                Tag = item
            };
            AutomationProperties.SetName(row, item.Label);
            _sheetRows.Add(row);
            var holder = new StackPanel();
            holder.Children.Add(row);
            if (group)
            {
                var links = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                foreach (var link in item.Links)
                {
                    var l = link;
                    var button = new SiteSheetLink { Icon = link.Icon, Label = link.Label, Tag = link };
                    AutomationProperties.SetName(button, link.Label);
                    button.Click += (_, _) => Choose(SiteHeaderNavigateKind.PanelLink, l.Label, l.Href, captured, l, null);
                    links.Children.Add(button);
                }
                var expanded = string.Equals(_expandedGroup, item.Value, StringComparison.Ordinal);
                var smooth = SpringSpec.FromVisual(0.4, 0);
                var box = new SiteHeightBox
                {
                    Child = links,
                    UseFade = true,
                    GrowSpring = smooth,
                    ShrinkSpring = smooth,
                    IsOpen = expanded,
                    StartCollapsed = !expanded,
                    IsVisible = expanded
                };
                box.Arrived += (_, _) =>
                {
                    if (!box.IsOpen)
                    {
                        box.IsVisible = false;
                    }
                };
                row.IsExpanded = expanded;
                row.Click += (_, _) => ToggleGroup(captured.Value);
                holder.Children.Add(box);
                _groups[item.Value] = (row, box);
            }
            else
            {
                row.Click += (_, _) => Choose(SiteHeaderNavigateKind.Item, captured.Label, captured.Href, captured, null, null);
            }
            var rule = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = holder };
            ArcTokens.Bind(rule, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
            _sheetNav.Children.Add(new SiteRise { Delay = TimeSpan.FromMilliseconds(40 + index * 35), Child = rule });
            index++;
        }
        if (IsSecondaryActionVisible || IsPrimaryActionVisible)
        {
            var actions = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8, Margin = new Thickness(0, 20, 0, 0) };
            if (IsSecondaryActionVisible)
            {
                var secondary = new SiteActionButton { Label = SecondaryActionLabel, IsInSheet = true };
                AutomationProperties.SetName(secondary, SecondaryActionLabel);
                secondary.Click += OnSecondaryClick;
                actions.Children.Add(secondary);
            }
            if (IsPrimaryActionVisible)
            {
                var primary = new SiteActionButton { Label = PrimaryActionLabel, IsPrimary = true, IsInSheet = true };
                AutomationProperties.SetName(primary, PrimaryActionLabel);
                primary.Click += OnPrimaryClick;
                Grid.SetColumn(primary, 1);
                actions.Children.Add(primary);
            }
            _sheetNav.Children.Add(new SiteRise { Delay = TimeSpan.FromMilliseconds(40 + index * 35), Child = actions });
        }
    }

    /// <summary>Arc: opening a group closes the open one; the same row toggles.</summary>
    private void ToggleGroup(string value)
    {
        var next = string.Equals(_expandedGroup, value, StringComparison.Ordinal) ? null : value;
        _expandedGroup = next;
        foreach (var (key, (row, box)) in _groups)
        {
            var open = string.Equals(key, next, StringComparison.Ordinal);
            row.IsExpanded = open;
            if (open)
            {
                box.IsVisible = true;
            }
            box.IsOpen = open;
        }
    }

    private void UpdateSheetCurrent()
    {
        foreach (var row in _sheetRows)
        {
            if (row.Tag is SiteHeaderItem item && !row.IsGroup)
            {
                row.IsCurrent = string.Equals(item.Value, Current, StringComparison.Ordinal);
            }
        }
    }
}
