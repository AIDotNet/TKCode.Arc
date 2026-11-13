using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The month calendar's sidebar (Arc screen shell, design reference: notes §2.1): the workspace row and the collapse button,
/// the search (Ctrl or Cmd + K; it filters the views and teammates), the views (Calendar, Upcoming, Invitations with its
/// count), "My calendars" (a toggle per calendar with its icon in its tone and this month's count, an Only pill on hover,
/// All when only one shows, an eye-off glyph for a hidden one), "Team calendars" (the first five teammates) and the account
/// row with its menu (week start, weekends, shortcuts, sign out). Rows are 32 high, radius 8, padding 0 10 0 12, gap 10;
/// hover fills the foreground at 5%, the current view at 8%. Collapsed to the 56 rail only the icons show.
/// </summary>
internal sealed class MonthSidebar : DockPanel
{
    private readonly MonthCalendar _owner;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroll;
    private readonly Border _top = new() { Margin = new Thickness(0, 0, 0, 4), Height = 40 };
    private readonly Border _searchRow = new() { Padding = new Thickness(0, 0, 0, 8) };
    private readonly Border _account = new() { Padding = new Thickness(0, 8, 0, 0) };
    private readonly List<Control> _labels = new();
    private ScreenSearchBox? _search;
    private double _collapse;

    public MonthSidebar(MonthCalendar owner)
    {
        _owner = owner;
        LastChildFill = true;
        DockPanel.SetDock(_top, Dock.Top);
        DockPanel.SetDock(_searchRow, Dock.Top);
        DockPanel.SetDock(_account, Dock.Bottom);
        _scroll = new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Children.Add(_top);
        Children.Add(_searchRow);
        Children.Add(_account);
        Children.Add(_scroll);
    }

    public void FocusSearch() => _search?.FocusInput();

    public void SetCollapse(double t)
    {
        _collapse = t;
        var opacity = Math.Clamp(1 - t * 2.5, 0, 1);
        foreach (var l in _labels)
        {
            l.Opacity = opacity;
        }
    }

    private T Label<T>(T control) where T : Control
    {
        _labels.Add(control);
        control.Opacity = Math.Clamp(1 - _collapse * 2.5, 0, 1);
        return control;
    }

    public void Rebuild()
    {
        var o = _owner;
        var paint = o.Paint;
        var l = o.Labels;
        _labels.Clear();
        var collapsed = o.IsSidebarCollapsed && (o.Shell?.IsSidebarDocked ?? true);
        var sheet = o.Shell is { IsSidebarDocked: false };

        // Top: workspace and the collapse / close button.
        var top = new Grid();
        var workspace = new ScreenPressable
        {
            Height = 36,
            Radius = 10,
            Padding = new Thickness(9, 0, 40, 0),
            HoverFill = paint.HoverFill,
            FocusPen = paint.FocusPen,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var wsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        wsRow.Children.Add(new MonthWorkspaceMark(paint) { Width = 24, Height = 24 });
        wsRow.Children.Add(Label(ScreenText.Line(o.WorkspaceName, 13, paint.ForegroundBrush, FontWeight.Medium, 17.55)));
        wsRow.Children.Add(Label(new ArcGlyph { Data = ArcIcons.ChevronDown, Size = 14, StrokeThickness = 1.75, Foreground = paint.MutedBrush, VerticalAlignment = VerticalAlignment.Center }));
        workspace.Child = wsRow;
        AutomationProperties.SetName(workspace, o.WorkspaceName);
        if (collapsed)
        {
            workspace.Clicked += _ => o.IsSidebarCollapsed = false;
            AutomationProperties.SetName(workspace, l.ExpandSidebar);
        }
        top.Children.Add(workspace);
        var toggle = sheet
            ? ScreenButtons.Icon(paint, ArcIcons.X, l.CloseNavigation, () =>
            {
                if (o.Shell is { } shell)
                {
                    shell.IsSidebarSheetOpen = false;
                }
            })
            : ScreenButtons.Icon(paint, ArcIcons.PanelLeft, l.CollapseSidebar, () => o.IsSidebarCollapsed = !o.IsSidebarCollapsed);
        toggle.HorizontalAlignment = HorizontalAlignment.Right;
        toggle.Margin = new Thickness(0, 0, 2, 0);
        if (!collapsed)
        {
            top.Children.Add(Label(toggle));
        }
        _top.Child = top;

        // Search.
        if (_search is null)
        {
            _search = new ScreenSearchBox(paint, l.Search, l.Dismiss, new ScreenKbd(OperatingSystem.IsMacOS() ? "⌘K" : "Ctrl K")
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 1, 0),
            });
            _search.TextChanged += text =>
            {
                o.SidebarSearch = text;
                RebuildList();
            };
        }
        _search.ApplyPaint(paint);
        if (collapsed)
        {
            var searchIcon = ScreenButtons.Icon(paint, ArcIcons.Search, l.Search, () =>
            {
                o.IsSidebarCollapsed = false;
                FocusSearch();
            });
            _searchRow.Child = searchIcon;
        }
        else
        {
            _searchRow.Child = _search;
        }

        RebuildList();
        RebuildAccount(collapsed);
    }

    private void RebuildList()
    {
        var o = _owner;
        var paint = o.Paint;
        var l = o.Labels;
        var collapsed = o.IsSidebarCollapsed && (o.Shell?.IsSidebarDocked ?? true);
        _list.Children.Clear();
        var query = o.SidebarSearch.Trim();
        bool Match(string text) => query.Length == 0 || text.Contains(query, StringComparison.CurrentCultureIgnoreCase);

        var nav = new StackPanel { Spacing = 1 };
        var invites = o.PendingInvitations().Count();
        AddNav(nav, MonthCalendarView.Calendar, ArcIcons.CalendarDays, l.Calendar, null, Match);
        AddNav(nav, MonthCalendarView.Upcoming, MonthGlyphs.CalendarClock, l.Upcoming, null, Match);
        AddNav(nav, MonthCalendarView.Invitations, ArcIcons.Inbox, l.Invitations, invites > 0 ? invites : null, Match);
        if (nav.Children.Count > 0)
        {
            _list.Children.Add(nav);
        }

        if (query.Length == 0)
        {
            var calendars = o.Calendars.Where(c => c.IsVisible).ToList();
            if (calendars.Count > 0)
            {
                _list.Children.Add(SectionHead(collapsed ? ArcIcons.Layers : ArcIcons.Layers, l.MyCalendars, null));
                if (!collapsed)
                {
                    var list = new StackPanel { Spacing = 1 };
                    var shown = calendars.Count(c => c.IsShown);
                    foreach (var c in calendars)
                    {
                        list.Children.Add(CalendarRow(c, onlyOne: shown == 1 && c.IsShown));
                    }
                    AutomationProperties.SetName(list, l.MyCalendars);
                    _list.Children.Add(list);
                }
            }
        }
        var mates = o.People.Where(p => p.IsVisible && p.IsTeammate && p.Id != o.CurrentUserId).Take(5)
                      .Where(p => Match(p.Name)).ToList();
        if (mates.Count > 0 && !collapsed)
        {
            if (query.Length == 0)
            {
                var add = ScreenButtons.Icon(paint, MonthGlyphs.UserPlus, l.AddTeammate, () => { }, glyphSize: 14, foreground: paint.MutedBrush, size: 22);
                add.Radius = 6;
                _list.Children.Add(SectionHead(ArcIcons.Users, l.TeamCalendars, add));
            }
            var list = new StackPanel { Spacing = 1 };
            foreach (var p in mates)
            {
                list.Children.Add(TeammateRow(p));
            }
            AutomationProperties.SetName(list, l.TeamCalendars);
            _list.Children.Add(list);
        }
        if (query.Length > 0 && _list.Children.Count == 0)
        {
            var none = ScreenText.Line(string.Format(o.EffectiveCulture, l.NoMatches, query), 13, paint.MutedBrush);
            none.Margin = new Thickness(12, 8, 0, 0);
            _list.Children.Add(none);
        }
    }

    private void AddNav(StackPanel nav, MonthCalendarView view, Geometry icon, string label, int? count, Func<string, bool> match)
    {
        if (!match(label))
        {
            return;
        }
        var o = _owner;
        var current = o.View == view;
        var row = Row(icon, label, count, current ? o.Paint.ForegroundBrush : o.Paint.SecondaryBrush,
            current ? new ImmutableSolidColorBrush(Oklch.ToColor(o.Paint.IsDark ? 0.8 : 0.35, 0, 0)) : o.Paint.MutedBrush, current);
        row.Clicked += _ =>
        {
            o.View = view;
            CloseSheet();
        };
        nav.Children.Add(row);
    }

    private void CloseSheet()
    {
        if (_owner.Shell is { IsSidebarDocked: false } shell)
        {
            shell.IsSidebarSheetOpen = false;
        }
    }

    private ScreenPressable Row(Geometry? icon, string label, int? count, IBrush labelBrush, IBrush iconBrush, bool active,
                                Control? marker = null)
    {
        var paint = _owner.Paint;
        var dock = new DockPanel { LastChildFill = true };
        Control lead = marker ?? new ArcGlyph { Data = icon, Size = 16, StrokeThickness = 1.75, Foreground = iconBrush, VerticalAlignment = VerticalAlignment.Center };
        lead.Margin = new Thickness(0, 0, 10, 0);
        DockPanel.SetDock(lead, Dock.Left);
        dock.Children.Add(lead);
        if (count is { } n)
        {
            var c = Label(ScreenText.Line(n.ToString(_owner.EffectiveCulture), 12, paint.MutedBrush));
            ((TextBlock)c).FontFeatures = FontFeatureCollection.Parse("tnum");
            c.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(c, Dock.Right);
            dock.Children.Add(c);
        }
        dock.Children.Add(Label(ScreenText.Line(label, 13, labelBrush)));
        var row = new ScreenPressable
        {
            Height = 32,
            Radius = 8,
            Padding = new Thickness(12, 0, 10, 0),
            HoverFill = paint.HoverFill,
            ActiveFill = paint.ActiveFill,
            IsActive = active,
            FocusPen = paint.FocusPen,
            Child = dock,
        };
        AutomationProperties.SetName(row, label);
        return row;
    }

    private Control SectionHead(Geometry icon, string label, Control? action)
    {
        var paint = _owner.Paint;
        var row = new DockPanel { Height = 32, Margin = new Thickness(0, 8, 0, 0) };
        if (action is not null)
        {
            DockPanel.SetDock(action, Dock.Right);
            action.Margin = new Thickness(0, 0, 4, 4);
            action.VerticalAlignment = VerticalAlignment.Bottom;
            row.Children.Add(Label(action));
        }
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(12, 0, 0, 4), VerticalAlignment = VerticalAlignment.Bottom };
        head.Children.Add(new ArcGlyph { Data = icon, Size = 16, StrokeThickness = 1.75, Foreground = paint.MutedBrush });
        head.Children.Add(Label(ScreenText.Line(label, 13, paint.MutedBrush)));
        row.Children.Add(head);
        return row;
    }

    private Control CalendarRow(MonthCalendarSource c, bool onlyOne)
    {
        var o = _owner;
        var paint = o.Paint;
        var l = o.Labels;
        var tone = o.ToneOf(c);
        var count = o.MonthCountOf(c);
        var shown = c.IsShown;
        var dock = new DockPanel { LastChildFill = true };
        var icon = new ArcGlyph
        {
            Data = MonthCalendar.IconOf(c),
            Size = 16,
            StrokeThickness = 1.75,
            Foreground = shown ? tone.Tone : paint.MutedBrush,
            Opacity = shown ? 1 : 0.6,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(icon, Dock.Left);
        dock.Children.Add(icon);
        Control trailing = shown
            ? ScreenText.Line(count.ToString(o.EffectiveCulture), 12, paint.MutedBrush)
            : new ArcGlyph { Data = ArcIcons.EyeOff, Size = 16, StrokeThickness = 1.75, Foreground = paint.MutedBrush, VerticalAlignment = VerticalAlignment.Center };
        trailing.MinWidth = 20;
        trailing.Margin = new Thickness(8, 0, 0, 0);
        if (trailing is TextBlock tb)
        {
            tb.TextAlignment = TextAlignment.Right;
            tb.FontFeatures = FontFeatureCollection.Parse("tnum");
        }
        DockPanel.SetDock(trailing, Dock.Right);
        dock.Children.Add(trailing);
        dock.Children.Add(ScreenText.Line(c.Name, 13, shown ? paint.SecondaryBrush : paint.MutedBrush));
        var row = new ScreenPressable
        {
            Height = 32,
            Radius = 8,
            Padding = new Thickness(12, 0, 10, 0),
            HoverFill = paint.HoverFill,
            FocusPen = paint.FocusPen,
            Child = dock,
            IsEnabled = c.IsEnabled,
        };
        AutomationProperties.SetName(row, string.Format(o.EffectiveCulture, l.CalendarToggleName, c.Name, count));
        AutomationProperties.SetHelpText(row, shown ? "pressed" : "not pressed");
        row.Clicked += _ => o.SetCalendarShown(c, !c.IsShown);

        // The Only (or All) pill shows over the count while the row is hovered or focused.
        var pill = new ScreenPressable
        {
            Height = 22,
            Radius = 6,
            Padding = new Thickness(6, 0),
            Fill = paint.IsDark ? paint.RaisedBrush : paint.SurfaceBrush,
            HoverFill = paint.ButtonHover,
            BorderPen = paint.BorderPen,
            FocusPen = paint.FocusPen,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Opacity = 0,
            Child = ScreenText.Line(onlyOne ? l.All : l.Only, 13, paint.SecondaryBrush),
            IsEnabled = c.IsEnabled,
        };
        AutomationProperties.SetName(pill, onlyOne ? l.ShowAll : string.Format(o.EffectiveCulture, l.ShowOnly, c.Name));
        pill.Clicked += _ => o.ShowOnly(c);
        var host = new Panel();
        host.Children.Add(row);
        host.Children.Add(pill);
        void Update()
        {
            var on = row.IsPointerOver || pill.IsPointerOver || pill.IsKeyboardFocusWithin;
            pill.Opacity = on ? 1 : 0;
            trailing.Opacity = on ? 0 : 1;
        }
        host.PointerEntered += (_, _) => Update();
        host.PointerExited += (_, _) => Update();
        host.PointerMoved += (_, _) => Update();
        pill.GotFocus += (_, _) => Update();
        pill.LostFocus += (_, _) => Update();
        return host;
    }

    private Control TeammateRow(MonthCalendarPerson p)
    {
        var o = _owner;
        var paint = o.Paint;
        var active = o.View == MonthCalendarView.Teammate && o.TeammateId == p.Id;
        var avatar = new Avatar { DisplayName = p.Name, Source = p.Avatar, Size = AvatarSize.Small, Width = 16, Height = 16, FontSize = 7, VerticalAlignment = VerticalAlignment.Center };
        var row = Row(null, p.Name, null, active ? paint.ForegroundBrush : paint.SecondaryBrush, paint.MutedBrush, active, avatar);
        row.IsEnabled = p.IsEnabled;
        row.Clicked += _ =>
        {
            o.TeammateId = p.Id;
            o.View = MonthCalendarView.Teammate;
            CloseSheet();
        };
        return row;
    }

    private void RebuildAccount(bool collapsed)
    {
        var o = _owner;
        var paint = o.Paint;
        var l = o.Labels;
        var me = o.CurrentUser;
        if (me is null)
        {
            _account.Child = null;
            return;
        }
        var dock = new DockPanel { LastChildFill = true };
        var avatar = new Avatar { DisplayName = me.Name, Source = me.Avatar, Size = AvatarSize.Small, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(avatar, Dock.Left);
        dock.Children.Add(avatar);
        var chevrons = Label(new ArcGlyph { Data = ArcIcons.ChevronsUpDown, Size = 14, StrokeThickness = 1.75, Foreground = paint.MutedBrush, VerticalAlignment = VerticalAlignment.Center });
        DockPanel.SetDock(chevrons, Dock.Right);
        dock.Children.Add(chevrons);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(ScreenText.Line(me.Name, 13, paint.ForegroundBrush, FontWeight.Medium, 17.55));
        if (!string.IsNullOrEmpty(me.Email))
        {
            text.Children.Add(ScreenText.Line(me.Email, 12, paint.MutedBrush, lineHeight: 16.8));
        }
        dock.Children.Add(Label(text));
        var button = new ScreenPressable
        {
            Height = 44,
            Radius = 10,
            Padding = new Thickness(6, 0),
            HoverFill = paint.HoverFill,
            FocusPen = paint.FocusPen,
            Child = dock,
        };
        AutomationProperties.SetName(button, me.Name);
        button.Clicked += _ => OpenAccountMenu(button);
        _account.Child = button;
    }

    private void OpenAccountMenu(Control anchor)
    {
        var o = _owner;
        var l = o.Labels;
        var menu = new MenuSurface { Side = MenuSide.Top, Alignment = MenuAlignment.Start, AnchorGap = 6 };
        var monday = new MenuEntry { Header = l.StartWeeksOnMonday, ToggleType = MenuEntryToggleType.CheckBox, IsChecked = o.FirstDayOfWeek == DayOfWeek.Monday };
        var weekends = new MenuEntry { Header = l.ShowWeekends, ToggleType = MenuEntryToggleType.CheckBox, IsChecked = o.ShowWeekends };
        var shortcuts = new MenuEntry { Header = l.KeyboardShortcuts, InputGestureText = "?", HasSeparatorBefore = true, Icon = ArcIcons.Keyboard };
        var signOut = new MenuEntry { Header = l.SignOut, IsDestructive = true, HasSeparatorBefore = true, Icon = ArcIcons.LogOut };
        menu.Items.Add(monday);
        menu.Items.Add(weekends);
        menu.Items.Add(shortcuts);
        menu.Items.Add(signOut);
        menu.ItemClicked += (_, e) =>
        {
            if (ReferenceEquals(e.Entry, monday))
            {
                o.FirstDayOfWeek = o.FirstDayOfWeek == DayOfWeek.Monday ? DayOfWeek.Sunday : DayOfWeek.Monday;
            }
            else if (ReferenceEquals(e.Entry, weekends))
            {
                o.ShowWeekends = !o.ShowWeekends;
            }
            else if (ReferenceEquals(e.Entry, shortcuts))
            {
                o.ShowShortcuts();
            }
        };
        menu.Open(anchor);
    }
}

/// <summary>The workspace mark: a 24 rounded square in the foreground with a small calendar cut out (our own glyph).</summary>
internal sealed class MonthWorkspaceMark : Control
{
    private readonly ScreenPaint _paint;

    public MonthWorkspaceMark(ScreenPaint paint)
    {
        _paint = paint;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var s = Math.Min(Bounds.Width, Bounds.Height);
        context.DrawRectangle(_paint.ForegroundBrush, null, new Rect(2, 2, s - 4, s - 4), 5, 5);
        var pen = new ImmutablePen(_paint.IsDark ? new ImmutableSolidColorBrush(_paint.Background) : _paint.SurfaceBrush, 1.8,
            lineCap: PenLineCap.Round);
        context.DrawLine(pen, new Point(7, 10), new Point(s - 7, 10));
        context.DrawLine(pen, new Point(9, 6.5), new Point(9, 8));
        context.DrawLine(pen, new Point(s - 9, 6.5), new Point(s - 9, 8));
        context.DrawEllipse(pen.Brush, null, new Point(s / 2, 15.5), 1.6, 1.6);
    }
}
