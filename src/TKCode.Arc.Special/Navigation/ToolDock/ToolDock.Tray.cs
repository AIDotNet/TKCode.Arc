using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

public partial class ToolDock
{
    /// <summary>The tray sinking back: its height on a quick spring, its width kept (fitted ω0 17.05, ζ 0.98).</summary>
    private static readonly SpringSpec s_trayClose = SpringSpec.FromVisual(0.3, 0);

    /// <summary>Members enter 80 ms after the tray starts to grow, 35 ms apart.</summary>
    private static readonly TimeSpan s_memberDelay = TimeSpan.FromMilliseconds(80);

    /// <summary>The tray's contents are hidden until the outline is 45% grown and fully shown at 85%.</summary>
    private const double ContentFadeStart = 0.45;
    private const double ContentFadeSpan = 0.4;

    private ToolDockTray? _tray;
    private ToolDockItem? _openGroup;
    private MotionValue<Rect>? _tab;
    private TopLevel? _outsideRoot;

    /// <summary>The tray part once a group was opened.</summary>
    internal ToolDockTray? Tray => _tray;

    /// <summary>The group whose tray is open.</summary>
    internal ToolDockItem? OpenGroup => _openGroup;

    /// <summary>Closes the open tray, if any.</summary>
    public void CloseTray()
    {
        var group = _openGroup;
        if (group is null)
        {
            return;
        }
        _openGroup = null;
        group.SetTrayOpen(false);
        PseudoClasses.Set(OpenPseudoClass, false);
        UnsubscribeOutside();
        if (_tray is { } tray)
        {
            tray.IsHitTestVisible = false;
            var animate = IsAttached && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
            foreach (var button in tray.Buttons)
            {
                button.IsTabStop = false;
                button.Exit(animate);
            }
            SinkTray(tray);
        }
        if (_labelTarget is ToolDockTrayButton)
        {
            HideLabel();
        }
    }

    private async void SinkTray(ToolDockTray tray)
    {
        if (_tab is null)
        {
            tray.IsVisible = false;
            return;
        }
        var current = _tab.Value;
        var done = await _tab.AnimateTo(new Rect(current.X, 0, current.Width, 0), MotionSpec.Spring(s_trayClose));
        if (done && _openGroup is null)
        {
            tray.IsVisible = false;
        }
    }

    /// <summary>Opens <paramref name="group"/>'s tray (closing another one), optionally moving the focus to its member.</summary>
    internal void OpenTray(ToolDockItem group, bool focusMember)
    {
        if (!group.IsGroup || _stage is null || _surface is null)
        {
            return;
        }
        if (ReferenceEquals(group, _openGroup))
        {
            if (focusMember)
            {
                FocusTrayMember();
            }
            return;
        }
        _openGroup?.SetTrayOpen(false);
        var wasOpen = _openGroup is not null;
        _openGroup = group;
        group.SetTrayOpen(true);
        PseudoClasses.Set(OpenPseudoClass, true);
        var tray = EnsureTray();
        var animate = IsAttached && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        BuildTrayButtons(tray, group, animate);
        tray.Group = group;
        tray.IsVisible = true;
        tray.IsHitTestVisible = true;
        var target = PlaceTray(group, tray);
        var tab = EnsureTab();
        if (!wasOpen && tab.Value.Height < 0.5)
        {
            // The tray grows out of the group's own slot.
            var slot = SlotRect(group);
            tab.Jump(new Rect(slot.X, 0, slot.Width, 0));
        }
        tab.AnimateTo(target, MotionSpec.Spring(ArcMotionPresets.Smooth));
        if (ReferenceEquals(_labelTarget, group))
        {
            HideLabel();
        }
        SubscribeOutside();
        if (focusMember)
        {
            FocusTrayMember();
        }
    }

    private ToolDockTray EnsureTray()
    {
        if (_tray is not null)
        {
            return _tray;
        }
        _tray = new ToolDockTray { IsVisible = false };
        var index = _stage!.Children.Count;
        for (var i = 0; i < _stage.Children.Count; i++)
        {
            if (_stage.Children[i] is ToolDockLabel)
            {
                index = i;
                break;
            }
        }
        _stage.Children.Insert(index, _tray);
        return _tray;
    }

    private MotionValue<Rect> EnsureTab()
    {
        if (_tab is not null)
        {
            return _tab;
        }
        _tab = new MotionValue<Rect>(this, default, MotionVectors.Rect);
        _tab.Changed += OnTabChanged;
        return _tab;
    }

    private void OnTabChanged(Rect tab)
    {
        if (_surface is not null)
        {
            _surface.Tab = new Rect(tab.X, -tab.Height, tab.Width, tab.Height);
        }
        if (_tray is { } tray)
        {
            var full = Math.Max(1, tray.Placement.Height);
            tray.Opacity = Math.Clamp((tab.Height / full - ContentFadeStart) / ContentFadeSpan, 0, 1);
        }
    }

    internal void BuildTrayButtons(ToolDockTray tray, ToolDockItem group, bool animate)
    {
        var same = ReferenceEquals(tray.Group, group) && tray.Buttons.Select(b => b.Member).SequenceEqual(group.Items);
        if (!same)
        {
            tray.Children.Clear();
            foreach (var member in group.Items)
            {
                tray.Children.Add(new ToolDockTrayButton(this, member) { Glyph = member.Glyph, IsEnabled = member.IsEnabled });
            }
        }
        var focusTarget = group.FindMember(SelectedValue) ?? group.ShownItem;
        var index = 0;
        foreach (var button in tray.Buttons)
        {
            button.SetSelected(SelectedValue is not null && Equals(button.Member.Value, SelectedValue));
            button.IsTabStop = ReferenceEquals(button.Member, focusTarget);
            button.Enter(s_memberDelay + TimeSpan.FromTicks(ArcMotionPresets.StaggerItem.Ticks * index), animate && ArcMotion.IsFull(this));
            index++;
        }
    }

    /// <summary>
    /// Places the tray centred over its group, kept the tray inset away from the bar's ends (centred on the bar when it
    /// does not fit), and returns its rectangle in the outline's terms (x, 0, width, height).
    /// </summary>
    private Rect PlaceTray(ToolDockItem group, ToolDockTray tray)
    {
        tray.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = tray.DesiredSize;
        var bar = _stage!.Bounds.Width > 0 ? _stage.Bounds.Width : _stage.DesiredSize.Width;
        var slot = SlotRect(group);
        var inset = tray.TrayInset;
        var x = slot.Center.X - size.Width / 2;
        if (bar - 2 * inset < size.Width)
        {
            x = (bar - size.Width) / 2;
        }
        else
        {
            x = Math.Clamp(x, inset, bar - inset - size.Width);
        }
        tray.Placement = new Rect(x, -size.Height, size.Width, size.Height);
        _stage.InvalidateArrange();
        return new Rect(x, 0, size.Width, size.Height);
    }

    /// <summary>A slot's layout rectangle in the stage's coordinates (without its drag or reflow offset).</summary>
    internal Rect SlotRect(ToolDockItem slot)
    {
        var x = slot.Bounds.X;
        var y = slot.Bounds.Y;
        for (var parent = slot.GetVisualParent(); parent is not null && !ReferenceEquals(parent, _stage); parent = parent.GetVisualParent())
        {
            x += parent.Bounds.X;
            y += parent.Bounds.Y;
        }
        return new Rect(x, y, slot.Bounds.Width, slot.Bounds.Height);
    }

    private void FocusTrayMember()
    {
        if (_tray is not { } tray || _openGroup is not { } group)
        {
            return;
        }
        var target = group.FindMember(SelectedValue) ?? group.ShownItem;
        var button = tray.Buttons.FirstOrDefault(b => ReferenceEquals(b.Member, target)) ?? tray.Buttons.FirstOrDefault();
        button?.Focus(NavigationMethod.Directional);
    }

    private void SubscribeOutside()
    {
        if (_outsideRoot is not null)
        {
            return;
        }
        _outsideRoot = TopLevel.GetTopLevel(this);
        _outsideRoot?.AddHandler(PointerPressedEvent, OnPointerPressedAnywhere, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void UnsubscribeOutside()
    {
        _outsideRoot?.RemoveHandler(PointerPressedEvent, OnPointerPressedAnywhere);
        _outsideRoot = null;
    }

    /// <summary>A press anywhere outside the dock and its tray closes the tray.</summary>
    private void OnPointerPressedAnywhere(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (ReferenceEquals(source, this) || this.IsVisualAncestorOf(source)))
        {
            return;
        }
        CloseTray();
    }

    private bool IsAttached => TopLevel.GetTopLevel(this) is not null;
}
