using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The schedule rows shared by the day panel, the upcoming list and the phone list.</summary>
internal static class MonthRows
{
    /// <summary>
    /// An event row (Arc Month calendar, design reference): padding 8 10, radius 10, columns 62 | 16 | the rest with a gap of
    /// 10: the time (12.5/17.5) over the length (11.5 muted) or "All day", the calendar icon in its tone (55% for an
    /// invitation waiting for a reply), then the title (13/500) over a 12px muted line with the video service or the place.
    /// </summary>
    public static Control EventRow(MonthCalendar o, MonthCalendarEvent e, bool inset)
    {
        var paint = o.Paint;
        var l = o.Labels;
        var source = o.SourceOf(e);
        var tone = o.ToneOf(source);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("62,16,*"), ColumnSpacing = 10 };
        var time = new StackPanel();
        if (e.IsAllDay || e.IsMultiDay)
        {
            time.Children.Add(ScreenText.Line(l.AllDay, 12.5, paint.ForegroundBrush, lineHeight: 17.5));
        }
        else
        {
            var start = ScreenText.Line(o.RowTime(e.StartTime!.Value), 12.5, paint.ForegroundBrush, lineHeight: 17.5);
            start.FontFeatures = FontFeatureCollection.Parse("tnum");
            time.Children.Add(start);
            time.Children.Add(ScreenText.Line(MonthCalendarModel.Duration(e.EffectiveEndTime - e.StartTime!.Value), 11.5, paint.MutedBrush,
                lineHeight: 16.1));
        }
        grid.Children.Add(time);
        var glyph = new ArcGlyph
        {
            Data = MonthCalendar.IconOf(source),
            Size = 16,
            StrokeThickness = 1.75,
            Foreground = tone.Tone,
            Opacity = e.Rsvp == MonthCalendarRsvp.Pending ? 0.55 : 1,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 0, 0),
        };
        Grid.SetColumn(glyph, 1);
        grid.Children.Add(glyph);
        var body = new StackPanel { Spacing = 1 };
        var title = ScreenText.Line(e.Title, 13, e.Rsvp == MonthCalendarRsvp.No ? paint.MutedBrush : paint.ForegroundBrush, FontWeight.Medium);
        if (e.Rsvp == MonthCalendarRsvp.No)
        {
            title.TextDecorations = TextDecorations.Strikethrough;
        }
        body.Children.Add(title);
        var meta = new ArcShrinkRow { Spacing = 6 };
        if (!string.IsNullOrEmpty(e.VideoProvider))
        {
            var video = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            video.Children.Add(new MonthVideoMark { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center });
            video.Children.Add(ScreenText.Line(e.VideoProvider, 12, paint.MutedBrush));
            meta.Children.Add(video);
        }
        else if (!string.IsNullOrEmpty(e.Location))
        {
            var place = ScreenText.Line(e.Location, 12, paint.MutedBrush);
            ArcShrinkRow.SetIsShrinkable(place, true);
            meta.Children.Add(place);
        }
        if (e.Rsvp == MonthCalendarRsvp.Pending)
        {
            if (meta.Children.Count > 0)
            {
                meta.Children.Add(ScreenText.Line("·", 12, paint.MutedBrush));
            }
            var awaiting = ScreenText.Line(l.AwaitingYourReply, 12, paint.MutedBrush);
            ArcShrinkRow.SetIsShrinkable(awaiting, true);
            meta.Children.Add(awaiting);
        }
        if (meta.Children.Count > 0)
        {
            body.Children.Add(meta);
        }
        Grid.SetColumn(body, 2);
        grid.Children.Add(body);
        var button = new ScreenPressable
        {
            Padding = new Thickness(10, 8),
            Margin = inset ? new Thickness(-10, 0) : new Thickness(0),
            Radius = 10,
            HoverFill = paint.HoverFill,
            ActiveFill = paint.ActiveFill,
            FocusPen = paint.FocusPen,
            IsActive = ReferenceEquals(o.OpenedEvent, e) && o.Pane == MonthPane.Event,
            Child = grid,
            Opacity = o.Matches(e) ? 1 : 0.28,
        };
        AutomationProperties.SetName(button, e.Title);
        button.Clicked += _ => o.OpenEvent(e);
        return button;
    }

    /// <summary>The heading of a day in a list: "Today  Nov 12" (Geist 13/500, the date 400 muted), a divider below.</summary>
    public static Control DayHeading(MonthCalendar o, DateOnly day)
    {
        var paint = o.Paint;
        var l = o.Labels;
        var today = o.EffectiveToday;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        string first;
        string? second;
        if (day == today)
        {
            first = l.TodayHeading;
            second = o.MonthDay(day);
        }
        else if (day == today.AddDays(1))
        {
            first = l.TomorrowHeading;
            second = o.MonthDay(day);
        }
        else
        {
            first = o.ShortDate(day);
            second = null;
        }
        row.Children.Add(ScreenText.Title(o, first, 13, 14.3, -0.39, paint.ForegroundBrush));
        if (second is not null)
        {
            var date = ScreenText.Line(second, 13, paint.MutedBrush, lineHeight: 14.3);
            date.FontFamily = ScreenText.Display(o);
            row.Children.Add(date);
        }
        return new Border
        {
            BorderBrush = paint.SubtleBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 0, 0, 8),
            Margin = new Thickness(0, 0, 0, 4),
            Child = row,
        };
    }

    /// <summary>The events of one day for lists: all-day ones first, then by time.</summary>
    public static List<MonthCalendarEvent> DayEvents(MonthCalendar o, DateOnly day)
    {
        var summary = o.SummaryOf(day);
        var list = new List<MonthCalendarEvent>(summary.AllDay);
        list.AddRange(summary.Timed);
        return list;
    }
}

/// <summary>
/// The Upcoming view: the days from today for four weeks that have events, centred at most 780 wide (padding 20 28 48), each
/// day a heading and its rows, 22 apart.
/// </summary>
internal sealed class MonthUpcomingView : UserControl
{
    private readonly MonthCalendar _owner;
    private readonly StackPanel _stack = new() { MaxWidth = 780, Margin = new Thickness(27, 0), HorizontalAlignment = HorizontalAlignment.Stretch };

    public MonthUpcomingView(MonthCalendar owner)
    {
        _owner = owner;
        var inner = new Border { Padding = new Thickness(28, 20, 28, 48), Child = _stack };
        Content = new ScrollViewer { Content = inner, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Rebuild();
    }

    public void Rebuild()
    {
        _stack.Children.Clear();
        var o = _owner;
        var today = o.EffectiveToday;
        var any = false;
        for (var i = 0; i < 28; i++)
        {
            var day = today.AddDays(i);
            var events = MonthRows.DayEvents(o, day);
            if (events.Count == 0)
            {
                continue;
            }
            var section = new StackPanel { Margin = new Thickness(0, any ? 22 : 0, 0, 0) };
            AutomationProperties.SetName(section, o.LongDate(day));
            section.Children.Add(MonthRows.DayHeading(o, day));
            var rows = new StackPanel { Spacing = 2 };
            foreach (var e in events)
            {
                rows.Children.Add(MonthRows.EventRow(o, e, inset: true));
            }
            section.Children.Add(rows);
            _stack.Children.Add(section);
            any = true;
        }
        if (!any)
        {
            _stack.Children.Add(ScreenText.Line(o.Labels.NothingUpcoming, 13, o.Paint.MutedBrush));
        }
    }
}

/// <summary>
/// The Invitations view: a card per invitation waiting for a reply (radius 14, 1px Border, padding 16 18, 12 apart): who sent it
/// and its calendar, the title (Geist 16/500), when, the video service and guests, the notes, then Accept, Maybe, Decline
/// and View in calendar. Answering folds the card away (opacity out in 135 ms, its height on the smooth spring).
/// </summary>
internal sealed class MonthInvitationsView : UserControl
{
    private readonly MonthCalendar _owner;
    private readonly StackPanel _stack = new() { MaxWidth = 780, Margin = new Thickness(27, 0) };
    private readonly Dictionary<MonthCalendarEvent, Control> _cards = new();

    public MonthInvitationsView(MonthCalendar owner)
    {
        _owner = owner;
        var inner = new Border { Padding = new Thickness(28, 20, 28, 48), Child = _stack };
        Content = new ScrollViewer { Content = inner, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Rebuild();
    }

    public void Rebuild()
    {
        var pending = _owner.PendingInvitations().ToList();
        // Cards of answered invitations fold away; the others are rebuilt in place.
        foreach (var (e, card) in _cards.ToList())
        {
            if (!pending.Contains(e))
            {
                _cards.Remove(e);
                Fold(card);
            }
        }
        foreach (var child in _stack.Children.ToList())
        {
            if (child is MonthFoldHost { IsFolding: true })
            {
                continue;
            }
            _stack.Children.Remove(child);
        }
        foreach (var e in pending)
        {
            var host = new MonthFoldHost(Card(e));
            _cards[e] = host;
            _stack.Children.Add(host);
        }
        if (pending.Count == 0 && _stack.Children.OfType<MonthFoldHost>().All(h => h.IsFolding))
        {
            _stack.Children.Add(ScreenText.Line(_owner.Labels.NoInvitations, 13, _owner.Paint.MutedBrush));
        }
    }

    private void Fold(Control card)
    {
        if (card is MonthFoldHost host)
        {
            host.Fold(() => _stack.Children.Remove(host));
        }
    }

    private Control Card(MonthCalendarEvent e)
    {
        var o = _owner;
        var paint = o.Paint;
        var l = o.Labels;
        var source = o.SourceOf(e);
        var tone = o.ToneOf(source);
        var stack = new StackPanel();
        // From.
        var organizer = o.PersonOf(e.OrganizerId);
        var from = new DockPanel { Height = 28 };
        var cal = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        cal.Children.Add(new ArcGlyph { Data = MonthCalendar.IconOf(source), Size = 16, StrokeThickness = 1.75, Foreground = tone.Tone });
        cal.Children.Add(ScreenText.Line(source?.Name, 12, paint.MutedBrush));
        DockPanel.SetDock(cal, Dock.Right);
        from.Children.Add(cal);
        var avatar = new Avatar { DisplayName = organizer?.Name ?? string.Empty, Source = organizer?.Avatar, Size = AvatarSize.Small, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(avatar, Dock.Left);
        from.Children.Add(avatar);
        var who = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 13, Foreground = paint.SecondaryBrush, TextTrimming = TextTrimming.CharacterEllipsis };
        var parts = string.Format(o.EffectiveCulture, l.InvitedYouCard, "\u0000").Split('\u0000');
        who.Inlines = new Avalonia.Controls.Documents.InlineCollection
        {
            new Avalonia.Controls.Documents.Run(parts[0]),
            new Avalonia.Controls.Documents.Run(organizer?.Name ?? string.Empty) { FontWeight = FontWeight.Medium, Foreground = paint.ForegroundBrush },
            new Avalonia.Controls.Documents.Run(parts.Length > 1 ? parts[1] : string.Empty),
        };
        from.Children.Add(who);
        stack.Children.Add(from);
        // Body.
        var title = ScreenText.Title(o, e.Title, 16, 17.6, -0.48, paint.ForegroundBrush);
        title.Margin = new Thickness(0, 12, 0, 0);
        stack.Children.Add(title);
        var whenText = e.IsAllDay
            ? string.Format(o.EffectiveCulture, l.AllDayWhen, o.ShortDate(e.StartDate))
            : string.Format(o.EffectiveCulture, l.TimedWhen, o.ShortDate(e.StartDate), o.RowTime(e.StartTime!.Value), o.RowTime(e.EffectiveEndTime));
        var when = ScreenText.Line(whenText, 13, paint.SecondaryBrush);
        when.Margin = new Thickness(0, 3, 0, 0);
        stack.Children.Add(when);
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(0, 8, 0, 0), Height = 28 };
        if (!string.IsNullOrEmpty(e.VideoProvider))
        {
            var video = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            video.Children.Add(new MonthVideoMark { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center });
            video.Children.Add(ScreenText.Line(e.VideoProvider, 12.5, paint.MutedBrush));
            meta.Children.Add(video);
        }
        if (e.Attendees.Count > 0)
        {
            var guests = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
            var faces = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 0, 0, 0) };
            foreach (var a in e.Attendees.Take(4))
            {
                var p = o.PersonOf(a.PersonId);
                faces.Children.Add(new Border
                {
                    Margin = new Thickness(-6, 0, 0, 0),
                    BorderBrush = paint.SurfaceBrush,
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(16),
                    Child = new Avatar { DisplayName = p?.Name ?? a.PersonId, Source = p?.Avatar, Size = AvatarSize.Small, Width = 24, Height = 24 },
                });
            }
            guests.Children.Add(faces);
            guests.Children.Add(ScreenText.Line(string.Format(o.EffectiveCulture, l.GuestCount, e.Attendees.Count), 12.5, paint.MutedBrush));
            meta.Children.Add(guests);
        }
        if (meta.Children.Count > 0)
        {
            stack.Children.Add(meta);
        }
        if (!string.IsNullOrWhiteSpace(e.Notes))
        {
            var notes = ScreenText.Para(e.Notes, 13, paint.SecondaryBrush, 19.5);
            notes.Margin = new Thickness(0, 10, 0, 0);
            stack.Children.Add(notes);
        }
        var actions = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var view = MonthDayPanelButtons.Make(l.ViewInCalendar, ButtonVariant.Ghost, () => o.OpenEventInMonth(e));
        DockPanel.SetDock(view, Dock.Right);
        actions.Children.Add(view);
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        left.Children.Add(MonthDayPanelButtons.Make(l.Accept, ButtonVariant.Primary, () => o.Reply(e, MonthCalendarRsvp.Yes)));
        left.Children.Add(MonthDayPanelButtons.Make(l.Maybe, ButtonVariant.Secondary, () => o.Reply(e, MonthCalendarRsvp.Maybe)));
        left.Children.Add(MonthDayPanelButtons.Make(l.Decline, ButtonVariant.Ghost, () => o.Reply(e, MonthCalendarRsvp.No)));
        actions.Children.Add(left);
        stack.Children.Add(actions);
        var card = new Border
        {
            Background = paint.SurfaceBrush,
            BorderBrush = paint.BorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18, 16),
            Margin = new Thickness(0, 0, 0, 12),
            Child = stack,
        }.BindCornerRadius(DesignTokens.ArcDesignTokenKind.RadiusStep14);
        AutomationProperties.SetName(card, e.Title);
        return card;
    }
}

internal static class MonthDayPanelButtons
{
    public static Control Make(string text, ButtonVariant variant, Action onClick)
    {
        return MonthDayPanel.ArcButton(text, variant, onClick);
    }
}

/// <summary>Folds its child away: opacity out in 135 ms while the height closes on the smooth spring (stiffness 300, damping 35).</summary>
internal sealed class MonthFoldHost : Decorator
{
    private MotionValue<double>? _fold;
    private double _progress = 1;
    private double _fullHeight;

    public MonthFoldHost(Control child)
    {
        Child = child;
        ClipToBounds = true;
    }

    public bool IsFolding { get; private set; }

    public async void Fold(Action done)
    {
        if (IsFolding)
        {
            return;
        }
        IsFolding = true;
        IsHitTestVisible = false;
        _fullHeight = Bounds.Height;
        if (!ArcMotion.IsFull(this) || _fullHeight <= 0)
        {
            done();
            return;
        }
        var fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        fade.Changed += v => { if (Child is { } c) { c.Opacity = v; } };
        _ = fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(135), ArcEasings.Standard));
        _fold = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fold.Changed += v =>
        {
            _progress = Math.Max(0, v);
            InvalidateMeasure();
        };
        await _fold.AnimateTo(0, MotionSpec.Spring(SpringSpec.FromPhysical(300, 35)));
        done();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return IsFolding ? new Size(size.Width, _fullHeight * _progress) : size;
    }
}

/// <summary>
/// The compact month's list under the grid (narrower than 560): the selected day's heading ("Today" or the long date, Geist
/// 14/500) with an Add button, then its rows (no free gaps).
/// </summary>
internal sealed class MonthPhoneDayList : Border
{
    private readonly MonthCalendar _owner;

    public MonthPhoneDayList(MonthCalendar owner)
    {
        _owner = owner;
        Padding = new Thickness(16, 14, 16, 28);
        BorderThickness = new Thickness(0, 1, 0, 0);
        Rebuild();
    }

    public void Rebuild()
    {
        var o = _owner;
        var paint = o.Paint;
        BorderBrush = paint.SubtleBrush;
        var day = o.SelectedDate;
        var stack = new StackPanel();
        var head = new DockPanel { Height = 32, Margin = new Thickness(0, 0, 0, 6) };
        var add = MonthDayPanel.ArcButton(o.Labels.Add, ButtonVariant.Secondary, () => o.OpenNewEvent(day), ArcIcons.Plus);
        DockPanel.SetDock(add, Dock.Right);
        head.Children.Add(add);
        head.Children.Add(ScreenText.Title(o, day == o.EffectiveToday ? o.Labels.TodayHeading : o.LongDate(day), 14, 15.4, -0.42,
            paint.ForegroundBrush));
        stack.Children.Add(head);
        var rows = new StackPanel { Spacing = 2 };
        foreach (var e in MonthRows.DayEvents(o, day))
        {
            rows.Children.Add(MonthRows.EventRow(o, e, inset: true));
        }
        if (rows.Children.Count == 0)
        {
            rows.Children.Add(ScreenText.Line(o.Labels.NothingPlanned, 13, paint.MutedBrush));
        }
        stack.Children.Add(rows);
        AutomationProperties.SetName(stack, o.LongDate(day));
        Child = stack;
    }
}
