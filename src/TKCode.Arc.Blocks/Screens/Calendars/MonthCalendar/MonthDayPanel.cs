using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The day panel (Arc Month calendar, design reference: notes §2.3): one pane at a time, the day, an event's details or the
/// new event form. Switching panes cross-fades in place (the old pane out in 150 ms, the new one in over 200 ms); another
/// day updates the day pane in place. Reduced and Off swap at once.
/// </summary>
internal sealed class MonthDayPanel : Panel
{
    private readonly MonthCalendar _owner;
    private Control? _current;
    private MonthPane _pane;
    private Input? _titleInput;

    public MonthDayPanel(MonthCalendar owner)
    {
        _owner = owner;
        ClipToBounds = true;
    }

    public void Show(MonthPane pane, DateOnly day, MonthCalendarEvent? e, MonthDraft? draft, bool crossfade)
    {
        var fresh = pane switch
        {
            MonthPane.Event when e is not null => BuildEvent(e, day),
            MonthPane.Form when draft is not null => BuildForm(draft),
            _ => BuildDay(day),
        };
        var old = _current;
        _current = fresh;
        _pane = pane;
        Children.Add(fresh);
        if (old is null)
        {
            return;
        }
        if (!crossfade || !ArcMotion.IsFull(this) || !IsEffectivelyVisible)
        {
            Children.Remove(old);
            return;
        }
        old.IsHitTestVisible = false;
        var fadeOut = new MotionValue<double>(old, 1, MotionVectors.Double);
        fadeOut.Changed += v => old.Opacity = v;
        _ = Remove(fadeOut, old);
        fresh.Opacity = 0;
        var fadeIn = new MotionValue<double>(fresh, 0, MotionVectors.Double);
        fadeIn.Changed += v => fresh.Opacity = v;
        _ = fadeIn.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(200), ArcEasings.InOut));
    }

    private async Task Remove(MotionValue<double> fade, Control old)
    {
        await fade.AnimateTo(0, MotionSpec.Tween(TimeSpan.FromMilliseconds(150), ArcEasings.Standard));
        Children.Remove(old);
    }

    public void FocusTitle() => _titleInput?.Focus(NavigationMethod.Tab);

    private ScreenPaint Paint => _owner.Paint;

    private MonthCalendarLabels L => _owner.Labels;

    // Shared pieces ----------------------------------------------------------------------------------------------------

    private static Control Pane(Control head, Control? body, Control? foot, string name)
    {
        var dock = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(head, Dock.Top);
        dock.Children.Add(head);
        if (foot is not null)
        {
            DockPanel.SetDock(foot, Dock.Bottom);
            dock.Children.Add(foot);
        }
        if (body is not null)
        {
            dock.Children.Add(new ScrollViewer
            {
                Content = body,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            });
        }
        AutomationProperties.SetName(dock, name);
        return dock;
    }

    private Border Head(Control content) => new()
    {
        Padding = new Thickness(18, 16, 18, 14),
        BorderBrush = Paint.SubtleBrush,
        BorderThickness = new Thickness(0, 0, 0, 1),
        Child = content,
    };

    private Border Foot(Control content) => new()
    {
        Padding = new Thickness(18, 12),
        BorderBrush = Paint.SubtleBrush,
        BorderThickness = new Thickness(0, 1, 0, 0),
        Child = content,
    };

    private TextBlock GroupLabel(string text) => ScreenText.Line(text, 12, Paint.MutedBrush, lineHeight: 16.8);

    private static Control Group(Control label, Control content, double top = 18)
    {
        var stack = new StackPanel { Spacing = 6, Margin = new Thickness(0, top, 0, 0) };
        stack.Children.Add(label);
        stack.Children.Add(content);
        return stack;
    }

    internal static AvaloniaButton ArcButton(string text, ButtonVariant variant, Action onClick, Geometry? glyph = null, bool iconOnly = false,
                                         string? name = null)
    {
        object content = text;
        if (glyph is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(new ArcGlyph { Data = glyph, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center });
            if (!iconOnly)
            {
                row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            }
            content = row;
        }
        // Arc's screen buttons are 32 high with 13px regular labels (12 in the free gaps).
        var button = new Button
        {
            Content = content,
            Variant = variant,
            Size = ControlSize.Small,
            Height = 32,
            MinHeight = 32,
            FontSize = 13,
            FontWeight = variant == ButtonVariant.Primary ? FontWeight.Medium : FontWeight.Normal,
        };
        if (glyph is not null)
        {
            // The glyph follows the button's text color.
            if (content is StackPanel sp && sp.Children[0] is ArcGlyph g)
            {
                g.Bind(ArcGlyph.ForegroundProperty, button.GetObservable(TemplatedControl.ForegroundProperty));
            }
        }
        if (iconOnly)
        {
            button.Width = 32;
            button.Padding = new Thickness(0);
        }
        AutomationProperties.SetName(button, name ?? text);
        button.Click += (_, _) => onClick();
        return button;
    }

    // Day pane ---------------------------------------------------------------------------------------------------------

    private Control BuildDay(DateOnly day)
    {
        var o = _owner;
        var paint = Paint;
        var summary = o.SummaryOf(day);
        var titleRow = new ArcShrinkRow { Spacing = 8 };
        var title = ScreenText.Title(o, o.LongDate(day), 15, 16.5, -0.45, paint.ForegroundBrush);
        ArcShrinkRow.SetIsShrinkable(title, true);
        titleRow.Children.Add(title);
        if (day == o.EffectiveToday)
        {
            titleRow.Children.Add(new Badge { Content = L.TodayBadge, Tone = BadgeTone.Info, Size = BadgeSize.Small, VerticalAlignment = VerticalAlignment.Center });
        }
        var events = summary.EventCount == 0 ? L.NoEvents
            : summary.EventCount == 1 ? L.OneEvent
            : string.Format(o.EffectiveCulture, L.ManyEvents, summary.EventCount);
        if (events.Length > 0)
        {
            events = char.ToUpper(events[0], o.EffectiveCulture) + events[1..];
        }
        var meta = ScreenText.Line(string.Format(o.EffectiveCulture, L.DayMeta, events, MonthCalendarModel.Duration(summary.Booked),
            MonthCalendarModel.Duration(summary.Free)), 12.5, paint.MutedBrush, lineHeight: 17.5);
        meta.Margin = new Thickness(0, 3, 0, 0);
        var headStack = new StackPanel();
        headStack.Children.Add(titleRow);
        headStack.Children.Add(meta);
        headStack.Children.Add(new MonthBusyStrip(o, day, summary) { Margin = new Thickness(0, 14, 0, 0) });
        var head = Head(headStack);

        var body = new StackPanel { Margin = new Thickness(18, 4, 18, 20) };
        if (summary.AllDay.Count > 0)
        {
            var list = new StackPanel { Spacing = 4 };
            foreach (var e in summary.AllDay)
            {
                list.Children.Add(AllDayRow(e));
            }
            body.Children.Add(Group(GroupLabel(L.AllDay), list));
        }
        var rows = new StackPanel { Spacing = 2 };
        foreach (var entry in summary.Schedule)
        {
            rows.Children.Add(entry.IsGap ? GapRow(day, entry) : MonthRows.EventRow(o, entry.Event!, inset: true));
        }
        if (summary.Schedule.Count == 0 && summary.AllDay.Count == 0)
        {
            var empty = ScreenText.Line(L.NothingPlanned, 13, paint.MutedBrush);
            empty.Margin = new Thickness(0, 6, 0, 0);
            rows.Children.Add(empty);
        }
        if (summary.Schedule.Count > 0 || summary.AllDay.Count == 0)
        {
            body.Children.Add(Group(GroupLabel(L.Schedule), rows));
        }
        var newButton = ArcButton(string.Format(o.EffectiveCulture, L.NewEventOn, o.MonthDay(day)), ButtonVariant.Secondary,
            () => o.OpenNewEvent(day), ArcIcons.Plus);
        newButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        newButton.HorizontalContentAlignment = HorizontalAlignment.Center;
        var pane = Pane(head, body, Foot(newButton), string.Format(o.EffectiveCulture, "{0} {1}", L.Schedule, o.LongDate(day)));
        if (!o.Shell?.IsPanelDocked ?? false)
        {
            AddSheetClose(head);
        }
        return pane;
    }

    private void AddSheetClose(Border head)
    {
        if (head.Child is not Control content)
        {
            return;
        }
        var close = ScreenButtons.Icon(Paint, ArcIcons.X, L.CloseDay, () =>
        {
            if (_owner.Shell is { } shell)
            {
                shell.IsPanelSheetOpen = false;
                _owner.RefreshHeader();
            }
        });
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        close.Margin = new Thickness(0, -6, -6, 0);
        var grid = new Grid();
        head.Child = null;
        grid.Children.Add(content);
        grid.Children.Add(close);
        if (content is StackPanel sp)
        {
            sp.Margin = new Thickness(0, 0, 34, 0);
        }
        head.Child = grid;
    }

    private Control AllDayRow(MonthCalendarEvent e)
    {
        var o = _owner;
        var tone = o.ToneOf(e);
        var row = new DockPanel { LastChildFill = true };
        var glyph = new ArcGlyph { Data = MonthCalendar.IconOf(o.SourceOf(e)), Size = 16, StrokeThickness = 1.75, Foreground = tone.Tone, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(glyph, Dock.Left);
        row.Children.Add(glyph);
        if (e.IsMultiDay)
        {
            var range = ScreenText.Line(string.Format(o.EffectiveCulture, L.DateRange, o.MonthDay(e.StartDate), o.MonthDay(e.LastDate)), 12,
                Paint.MutedBrush);
            range.Margin = new Thickness(10, 0, 0, 0);
            DockPanel.SetDock(range, Dock.Right);
            row.Children.Add(range);
        }
        row.Children.Add(ScreenText.Line(e.Title, 13, tone.BarText, FontWeight.Medium));
        var button = new ScreenPressable
        {
            Padding = new Thickness(10, 7),
            Radius = 8,
            Fill = tone.BarFill,
            HoverFill = tone.BarHover,
            FocusPen = Paint.FocusPen,
            Child = row,
        };
        AutomationProperties.SetName(button, e.Title);
        button.Clicked += _ => o.OpenEvent(e);
        return button;
    }

    private Control GapRow(DateOnly day, MonthScheduleEntry gap)
    {
        var o = _owner;
        var paint = Paint;
        var text = ScreenText.Line(string.Format(o.EffectiveCulture, L.FreeGap, o.RowTime(gap.Start), o.RowTime(gap.End)), 12, paint.MutedBrush);
        text.FontFeatures = FontFeatureCollection.Parse("tnum");
        var block = ArcButton(L.BlockFocusTime, ButtonVariant.Ghost, () => o.BlockFocusTime(day, gap.Start, gap.End));
        block.FontSize = 12;
        var dock = new DockPanel();
        DockPanel.SetDock(block, Dock.Right);
        dock.Children.Add(block);
        dock.Children.Add(text);
        return new MonthDashedFrame(paint.Border, 12)
        {
            Height = 44,
            Margin = new Thickness(-10, 2),
            Padding = new Thickness(10, 0, 3, 0),
            Child = dock,
        };
    }

    // Event pane -------------------------------------------------------------------------------------------------------

    private Control BuildEvent(MonthCalendarEvent e, DateOnly day)
    {
        var o = _owner;
        var paint = Paint;
        var source = o.SourceOf(e);
        var tone = o.ToneOf(source);
        var back = ArcButton(o.ShortDate(day).Split(',')[0] + ", " + o.MonthDay(day), ButtonVariant.Ghost, o.BackToDay, ArcIcons.ArrowLeft);
        back.Margin = new Thickness(-10, 0, 0, 0);
        var top = new DockPanel { Height = 36 };
        if (e.IsEnabled)
        {
            var del = ArcButton(string.Format(o.EffectiveCulture, L.DeleteEvent, e.Title), ButtonVariant.Ghost, () => o.DeleteEvent(e),
                ArcIcons.Trash2, iconOnly: true);
            DockPanel.SetDock(del, Dock.Right);
            top.Children.Add(del);
        }
        if (!o.Shell?.IsPanelDocked ?? false)
        {
            var close = ScreenButtons.Icon(paint, ArcIcons.X, L.CloseDay, () =>
            {
                if (o.Shell is { } shell)
                {
                    shell.IsPanelSheetOpen = false;
                    o.RefreshHeader();
                }
            });
            DockPanel.SetDock(close, Dock.Right);
            top.Children.Add(close);
        }
        back.HorizontalAlignment = HorizontalAlignment.Left;
        top.Children.Add(back);
        var head = new Border
        {
            Padding = new Thickness(18, 16, 18, 14),
            BorderBrush = paint.SubtleBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = top,
        };

        var body = new StackPanel { Margin = new Thickness(18, 4, 18, 20) };
        // Title and when.
        var titleRow = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        var glyph = new ArcGlyph { Data = MonthCalendar.IconOf(source), Size = 16, StrokeThickness = 1.75, Foreground = tone.Tone, Margin = new Thickness(0, 2, 10, 0), VerticalAlignment = VerticalAlignment.Top };
        DockPanel.SetDock(glyph, Dock.Left);
        titleRow.Children.Add(glyph);
        var title = ScreenText.Title(o, e.Title, 18, 19.8, -0.27, paint.ForegroundBrush);
        title.TextWrapping = TextWrapping.Wrap;
        title.TextTrimming = TextTrimming.None;
        if (e.Rsvp == MonthCalendarRsvp.No)
        {
            title.TextDecorations = TextDecorations.Strikethrough;
        }
        titleRow.Children.Add(title);
        body.Children.Add(titleRow);
        var when = ScreenText.Line(WhenText(e), 13, paint.SecondaryBrush);
        when.Margin = new Thickness(26, 4, 0, 0);
        when.TextWrapping = TextWrapping.Wrap;
        body.Children.Add(when);

        // Facts.
        var facts = new StackPanel { Spacing = 10, Margin = new Thickness(0, 16, 0, 0) };
        if (!e.IsAllDay && !e.IsMultiDay)
        {
            facts.Children.Add(Fact(new ArcGlyph { Data = ArcIcons.Clock, Size = 16, StrokeThickness = 1.75, Foreground = paint.MutedBrush },
                ScreenText.Line(string.Format(o.EffectiveCulture, L.DurationFact,
                    MonthCalendarModel.Duration(e.EffectiveEndTime - e.StartTime!.Value), o.RowTime(e.EffectiveEndTime)), 13, paint.ForegroundBrush)));
        }
        if (!string.IsNullOrEmpty(e.Location))
        {
            facts.Children.Add(Fact(new ArcGlyph { Data = MonthGlyphs.MapPin, Size = 16, StrokeThickness = 1.75, Foreground = paint.MutedBrush },
                ScreenText.Line(e.Location, 13, paint.ForegroundBrush)));
        }
        if (!string.IsNullOrEmpty(e.VideoProvider))
        {
            var name = new ArcShrinkRow { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
            name.Children.Add(ScreenText.Line(e.VideoProvider, 13, paint.ForegroundBrush));
            if (!string.IsNullOrEmpty(e.VideoUrl))
            {
                var url = ScreenText.Line(e.VideoUrl, 12, paint.MutedBrush);
                ArcShrinkRow.SetIsShrinkable(url, true);
                name.Children.Add(url);
            }
            var video = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(e.VideoUrl))
            {
                var copy = new CopyButton
                {
                    Value = e.VideoUrl,
                    Label = L.CopyLink,
                    CopiedLabel = L.Copied,
                    Margin = new Thickness(8, 0, 0, 0),
                };
                DockPanel.SetDock(copy, Dock.Right);
                video.Children.Add(copy);
            }
            video.Children.Add(name);
            var mark = new MonthVideoMark { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            AutomationProperties.SetName(mark, L.VideoCall);
            facts.Children.Add(Fact(mark, video));
        }
        if (facts.Children.Count > 0)
        {
            body.Children.Add(facts);
        }

        // Reply.
        if (e.Rsvp != MonthCalendarRsvp.None)
        {
            var organizer = o.PersonOf(e.OrganizerId)?.Name;
            var label = e.Rsvp == MonthCalendarRsvp.Pending && organizer is not null
                ? string.Format(o.EffectiveCulture, L.InvitedYou, organizer)
                : L.YourReply;
            var segmented = new SegmentedControl
            {
                IsEnabled = e.IsEnabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                // Arc's reply control splits the width into three equal segments.
                ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(() => new Avalonia.Controls.Primitives.UniformGrid { Rows = 1 }),
            };
            segmented.Items.Add(new SegmentedOption { Value = MonthCalendarRsvp.Yes, Label = L.Going });
            segmented.Items.Add(new SegmentedOption { Value = MonthCalendarRsvp.Maybe, Label = L.Maybe });
            segmented.Items.Add(new SegmentedOption { Value = MonthCalendarRsvp.No, Label = L.No });
            segmented.SelectedIndex = e.Rsvp switch
            {
                MonthCalendarRsvp.Yes => 0,
                MonthCalendarRsvp.Maybe => 1,
                MonthCalendarRsvp.No => 2,
                _ => -1,
            };
            AutomationProperties.SetName(segmented, L.YourReply);
            segmented.SelectionChanged += (_, _) =>
            {
                if (segmented.SelectedItem is SegmentedOption { Value: MonthCalendarRsvp reply })
                {
                    Dispatcher.UIThread.Post(() => o.Reply(e, reply));
                }
            };
            body.Children.Add(Group(GroupLabel(label), segmented));
        }

        // Guests.
        if (e.Attendees.Count > 0)
        {
            var going = e.Attendees.Count(a => a.Status == MonthCalendarRsvp.Yes);
            var labelRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            labelRow.Children.Add(new ArcGlyph { Data = ArcIcons.Users, Size = 16, StrokeThickness = 1.75, Foreground = paint.MutedBrush });
            labelRow.Children.Add(GroupLabel(string.Format(o.EffectiveCulture, L.GuestsSummary, e.Attendees.Count, going)));
            var guests = new StackPanel { Spacing = 8 };
            var ordered = e.Attendees.OrderBy(a => a.PersonId == e.OrganizerId ? 0 : 1).ToList();
            foreach (var a in ordered)
            {
                guests.Children.Add(GuestRow(e, a));
            }
            body.Children.Add(Group(labelRow, guests));
        }

        // Notes.
        if (!string.IsNullOrWhiteSpace(e.Notes))
        {
            body.Children.Add(Group(GroupLabel(L.Notes), ScreenText.Para(e.Notes, 13, paint.SecondaryBrush, 19.5)));
        }

        // Calendar.
        var visible = o.Calendars.Where(c => c.IsVisible).ToList();
        if (visible.Count > 0)
        {
            var select = new Select { IsEnabled = e.IsEnabled, HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var c in visible)
            {
                select.Items.Add(new SelectOption(c.Id, c.Name));
            }
            select.SelectedIndex = visible.FindIndex(c => c.Id == e.CalendarId);
            AutomationProperties.SetName(select, L.CalendarField);
            select.SelectionChanged += (_, _) =>
            {
                if (select.SelectedItem is SelectOption { Value: string id })
                {
                    Dispatcher.UIThread.Post(() => o.Recalendar(e, id));
                }
            };
            body.Children.Add(Group(GroupLabel(L.CalendarField), select));
        }
        return Pane(head, body, null, e.Title);
    }

    private string WhenText(MonthCalendarEvent e)
    {
        var o = _owner;
        if (e.IsMultiDay)
        {
            var range = string.Format(o.EffectiveCulture, L.DateRange, o.MonthDay(e.StartDate), o.MonthDay(e.LastDate));
            return e.IsAllDay ? string.Format(o.EffectiveCulture, L.AllDayWhen, range) : range;
        }
        if (e.IsAllDay)
        {
            return string.Format(o.EffectiveCulture, L.AllDayWhen, o.ShortDate(e.StartDate));
        }
        return string.Format(o.EffectiveCulture, L.TimedWhen, o.ShortDate(e.StartDate), o.RowTime(e.StartTime!.Value), o.RowTime(e.EffectiveEndTime));
    }

    private static Control Fact(Control icon, Control content)
    {
        var dock = new DockPanel { LastChildFill = true, MinHeight = 19 };
        var slot = new Border { Width = 16, Margin = new Thickness(0, 0, 10, 0), Child = icon, VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(slot, Dock.Left);
        dock.Children.Add(slot);
        dock.Children.Add(content);
        return dock;
    }

    private Control GuestRow(MonthCalendarEvent e, MonthCalendarAttendee a)
    {
        var o = _owner;
        var paint = Paint;
        var person = o.PersonOf(a.PersonId);
        var name = person?.Name ?? a.PersonId;
        var me = a.PersonId == o.CurrentUserId;
        var row = new DockPanel { LastChildFill = true };
        var avatar = new Avatar { DisplayName = name, Source = person?.Avatar, Size = AvatarSize.Small, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(avatar, Dock.Left);
        row.Children.Add(avatar);
        var (statusText, statusBrush) = a.Status switch
        {
            MonthCalendarRsvp.Yes => (L.Going, (IBrush)new ImmutableSolidColorBrush(MonthCalendarPalette.Going(paint.IsDark))),
            MonthCalendarRsvp.Maybe => (L.Maybe, o.ToneOf(new MonthCalendarSource { Tone = MonthCalendarTone.Amber }).Tone),
            MonthCalendarRsvp.No => (L.Declined, o.ToneOf(new MonthCalendarSource { Tone = MonthCalendarTone.Rose }).Tone),
            _ => (L.AwaitingReply, (IBrush)paint.MutedBrush),
        };
        var status = ScreenText.Line(statusText, 12, statusBrush);
        status.Margin = new Thickness(10, 0, 0, 0);
        DockPanel.SetDock(status, Dock.Right);
        row.Children.Add(status);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(ScreenText.Line(name + (me ? L.YouSuffix : string.Empty), 13, paint.ForegroundBrush));
        if (a.PersonId == e.OrganizerId)
        {
            text.Children.Add(ScreenText.Line(L.Organizer, 11.5, paint.MutedBrush, lineHeight: 16.1));
        }
        row.Children.Add(text);
        return row;
    }

    // Form -------------------------------------------------------------------------------------------------------------

    private Control BuildForm(MonthDraft d)
    {
        var o = _owner;
        var paint = Paint;
        var top = new DockPanel { Height = 36 };
        var cancelX = ArcButton(L.Cancel, ButtonVariant.Ghost, o.CancelDraft, ArcIcons.X, iconOnly: true);
        DockPanel.SetDock(cancelX, Dock.Right);
        top.Children.Add(cancelX);
        top.Children.Add(ScreenText.Title(o, L.NewEvent, 15, 16.5, -0.45, paint.ForegroundBrush));
        var head = new Border
        {
            Padding = new Thickness(18, 16, 18, 14),
            BorderBrush = paint.SubtleBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = top,
        };
        var body = new StackPanel { Margin = new Thickness(18, 18, 18, 20), Spacing = 14 };

        var titleInput = new Input
        {
            Label = L.Title,
            Placeholder = L.AddTitle,
            Text = d.Title,
            ErrorMessage = d.TitleError,
            IsInvalid = d.TitleError is not null,
        };
        titleInput.TextChanged += (_, _) =>
        {
            d.Title = titleInput.Text ?? string.Empty;
            if (d.TitleError is not null && d.Title.Length > 0)
            {
                d.TitleError = null;
                titleInput.ErrorMessage = null;
                titleInput.IsInvalid = false;
            }
        };
        titleInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                o.SaveDraft();
            }
        };
        _titleInput = titleInput;
        body.Children.Add(titleInput);

        var whenText = d.End > d.Start
            ? string.Format(o.EffectiveCulture, L.DateRange, o.ShortDate(d.Start), o.ShortDate(d.End))
            : o.LongDate(d.Start);
        var whenRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        whenRow.Children.Add(new ArcGlyph { Data = ArcIcons.Clock, Size = 16, StrokeThickness = 1.75, Foreground = paint.MutedBrush, VerticalAlignment = VerticalAlignment.Center });
        whenRow.Children.Add(ScreenText.Line(whenText, 13, paint.ForegroundBrush));
        // Arc's checkbox sits flush with the fields (its 40 square hit area reaches out to the left).
        var allDay = new Checkbox { Content = L.AllDayCheckbox, IsChecked = d.AllDay, Margin = new Thickness(-14, -6, 0, -6) };
        var times = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10 };
        var start = new TimePicker { Label = L.Starts, SelectedTime = d.StartTime, MinuteStep = 15, IsEnabled = !d.AllDay, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, Culture = o.EffectiveCulture };
        var end = new TimePicker { Label = L.Ends, SelectedTime = d.EndTime, MinuteStep = 15, IsEnabled = !d.AllDay, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, Culture = o.EffectiveCulture };
        Grid.SetColumn(end, 1);
        times.Children.Add(start);
        times.Children.Add(end);
        start.SelectedTimeChanged += (_, _) =>
        {
            if (start.SelectedTime is { } t)
            {
                var length = d.EndTime - d.StartTime;
                d.StartTime = t;
                var shifted = t.Add(length > TimeSpan.Zero ? length : TimeSpan.FromHours(1));
                if (shifted > t)
                {
                    d.EndTime = shifted;
                    end.SelectedTime = shifted;
                }
            }
        };
        end.SelectedTimeChanged += (_, _) =>
        {
            if (end.SelectedTime is { } t)
            {
                d.EndTime = t;
            }
        };
        allDay.IsCheckedChanged += (_, _) =>
        {
            d.AllDay = allDay.IsChecked == true;
            start.IsEnabled = !d.AllDay;
            end.IsEnabled = !d.AllDay;
        };
        var when = new StackPanel { Spacing = 6 };
        when.Children.Add(whenRow);
        when.Children.Add(allDay);
        when.Children.Add(times);
        body.Children.Add(when);

        var location = new Input { Label = L.Location, Placeholder = L.LocationPlaceholder, Text = d.Location };
        location.TextChanged += (_, _) => d.Location = location.Text ?? string.Empty;
        body.Children.Add(location);

        var video = new Checkbox { Content = L.AddVideoLink, Description = L.AddVideoLinkHint, IsChecked = d.Video, Margin = new Thickness(-14, -6, 0, -6) };
        video.IsCheckedChanged += (_, _) => d.Video = video.IsChecked == true;
        body.Children.Add(video);

        // Guests: the teammates as avatar toggles (off at 45%, on with a ring).
        var people = o.People.Where(p => p.IsVisible && p.Id != o.CurrentUserId).ToList();
        if (people.Count > 0)
        {
            var hint = ScreenText.Line(GuestHint(d), 12, paint.MutedBrush);
            var faces = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
            foreach (var p in people)
            {
                var toggle = new MonthGuestToggle(o, p, d.Guests.Contains(p.Id));
                toggle.Toggled += on =>
                {
                    if (on)
                    {
                        d.Guests.Add(p.Id);
                    }
                    else
                    {
                        d.Guests.Remove(p.Id);
                    }
                    hint.Text = GuestHint(d);
                };
                faces.Children.Add(toggle);
            }
            var guests = new StackPanel { Spacing = 6 };
            guests.Children.Add(ScreenText.Line(L.Guests, 13, paint.ForegroundBrush, FontWeight.Medium));
            guests.Children.Add(faces);
            guests.Children.Add(hint);
            body.Children.Add(guests);
        }

        var visible = o.Calendars.Where(c => c.IsVisible).ToList();
        if (visible.Count > 0)
        {
            var select = new Select { Label = L.CalendarField, HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var c in visible)
            {
                select.Items.Add(new SelectOption(c.Id, c.Name));
            }
            select.SelectedIndex = Math.Max(0, visible.FindIndex(c => c.Id == d.CalendarId));
            select.SelectionChanged += (_, _) =>
            {
                if (select.SelectedItem is SelectOption { Value: string id })
                {
                    d.CalendarId = id;
                }
            };
            body.Children.Add(select);
        }
        if (!string.IsNullOrEmpty(d.Error))
        {
            var error = ScreenText.Para(d.Error, 12, paint.DangerBrush, 16.8);
            AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Assertive);
            body.Children.Add(error);
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(ArcButton(L.Cancel, ButtonVariant.Ghost, o.CancelDraft));
        actions.Children.Add(ArcButton(L.SaveEvent, ButtonVariant.Primary, o.SaveDraft));
        return Pane(head, body, Foot(actions), L.NewEvent);
    }

    private string GuestHint(MonthDraft d)
    {
        if (d.Guests.Count == 0)
        {
            return L.GuestsHint;
        }
        var names = _owner.People.Where(p => d.Guests.Contains(p.Id)).Select(p => p.FirstName);
        return string.Format(_owner.EffectiveCulture, L.GuestsInvite, string.Join(", ", names));
    }
}

/// <summary>A rounded frame with a dashed 1px border (the free gaps: radius 12, Border color).</summary>
internal sealed class MonthDashedFrame : Decorator
{
    private readonly IPen _pen;
    private readonly double _radius;

    public MonthDashedFrame(Color color, double radius)
    {
        _pen = new ImmutablePen(new ImmutableSolidColorBrush(color), 1, new ImmutableDashStyle(new double[] { 3, 3 }, 0));
        _radius = radius;
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(null, _pen, new Rect(Bounds.Size).Deflate(0.5), _radius, _radius);
    }
}

/// <summary>
/// The busy strip of a day (Arc Month calendar, design reference): a 304 × 8 track (foreground 6%, radius 4) spanning 7:00 to
/// 22:00, a block per timed event in its calendar's tone (radius 3, at least 3 wide; invitations waiting for a reply at 35%),
/// hour labels at 8a, 12p, 4p and 8p (11px muted) and, today, a 2 × 16 now marker ringed with the card color.
/// </summary>
internal sealed class MonthBusyStrip : Control
{
    private const int FirstHour = 7;
    private const int LastHour = 22;
    private readonly MonthCalendar _owner;
    private readonly DateOnly _day;
    private readonly MonthDaySummary _summary;
    private readonly WeekCalendarText[] _labels = [new(), new(), new(), new()];

    public MonthBusyStrip(MonthCalendar owner, DateOnly day, MonthDaySummary summary)
    {
        _owner = owner;
        _day = day;
        _summary = summary;
        Height = 28;
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var paint = _owner.Paint;
        var w = Bounds.Width;
        var perHour = w / (LastHour - FirstHour);
        context.DrawRectangle(new ImmutableSolidColorBrush(Oklch.WithAlpha(paint.Foreground, 0.06)), null, new Rect(0, 0, w, 8), 4, 4);
        foreach (var e in _summary.Timed)
        {
            if (e.Rsvp == MonthCalendarRsvp.No)
            {
                continue;
            }
            var s = e.StartTime!.Value.ToTimeSpan().TotalHours;
            var end = e.EffectiveEndTime.ToTimeSpan().TotalHours;
            var x0 = Math.Clamp((s - FirstHour) * perHour, 0, w);
            var x1 = Math.Clamp((end - FirstHour) * perHour, 0, w);
            if (x1 <= 0 || x0 >= w)
            {
                continue;
            }
            var tone = _owner.ToneOf(e);
            context.DrawRectangle(e.Rsvp == MonthCalendarRsvp.Pending ? tone.Pending : tone.Tone, null,
                new Rect(x0, 0, Math.Max(3, x1 - x0), 8), 3, 3);
        }
        var face = new Typeface(ScreenText.Body(this));
        int[] hours = [8, 12, 16, 20];
        for (var i = 0; i < hours.Length; i++)
        {
            var text = MonthCalendarModel.ChipTime(new TimeOnly(hours[i], 0), _owner.EffectiveCulture);
            var layout = _labels[i].Get(text, face, 11, paint.MutedBrush, double.PositiveInfinity, 1, 15.4, -0.11);
            if (layout is null)
            {
                continue;
            }
            var x = (hours[i] - FirstHour) * perHour - layout.WidthIncludingTrailingWhitespace / 2;
            layout.Draw(context, new Point(x, 13));
        }
        if (_day == _owner.EffectiveToday)
        {
            var now = DateTime.Now.TimeOfDay.TotalHours;
            if (now > FirstHour && now < LastHour)
            {
                var x = (now - FirstHour) * perHour;
                var ring = new ImmutableSolidColorBrush(paint.Surface);
                context.DrawRectangle(ring, null, new Rect(x - 3, -6, 6, 20), 3, 3);
                context.DrawRectangle(paint.ForegroundBrush, null, new Rect(x - 1, -4, 2, 16), 1, 1);
            }
        }
    }
}

/// <summary>A neutral video call mark (our own glyph): a blue rounded square with a white camera.</summary>
internal sealed class MonthVideoMark : Control
{
    public MonthVideoMark()
    {
        IsHitTestVisible = false;
    }

    public override void Render(DrawingContext context)
    {
        var s = Math.Min(Bounds.Width, Bounds.Height);
        var fill = new ImmutableSolidColorBrush(Oklch.ToColor(0.58, 0.17, 255));
        context.DrawRectangle(fill, null, new Rect(0, 0, s, s), s * 0.25, s * 0.25);
        var pen = new ImmutablePen(Brushes.White, 2.4, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        var scale = s * 0.62 / 24;
        var offset = (s - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offset, offset)))
        {
            context.DrawGeometry(null, pen, MonthGlyphs.Video);
        }
    }
}

/// <summary>A guest toggle of the new event form: a 28 avatar at 45% when off, full with a 2 + 1.5 px ring when on.</summary>
internal sealed class MonthGuestToggle : ScreenPressable
{
    private readonly Avatar _avatar;
    private bool _on;

    public MonthGuestToggle(MonthCalendar owner, MonthCalendarPerson person, bool on)
    {
        _avatar = new Avatar { DisplayName = person.Name, Source = person.Avatar, Size = AvatarSize.Small };
        Child = _avatar;
        Radius = 14;
        Width = 28;
        Height = 28;
        FocusPen = owner.Paint.FocusPen;
        AutomationProperties.SetName(this, person.Name);
        _on = on;
        Apply(owner.Paint);
        Clicked += _ =>
        {
            _on = !_on;
            Apply(owner.Paint);
            Toggled?.Invoke(_on);
        };
    }

    public event Action<bool>? Toggled;

    public bool IsOn => _on;

    private void Apply(ScreenPaint paint)
    {
        _avatar.Opacity = _on ? 1 : 0.45;
        BorderPen = _on ? new ImmutablePen(paint.ForegroundBrush, 1.5) : null;
        AutomationProperties.SetHelpText(this, _on ? "on" : "off");
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_on)
        {
            context.DrawEllipse(null, BorderPen, new Point(14, 14), 16.75, 16.75);
        }
    }
}

/// <summary>Lucide glyphs the month calendar needs that Core's subset does not carry (Lucide, ISC license).</summary>
internal static class MonthGlyphs
{
    public static Geometry CalendarClock => ArcIcons.Parse("M16 14v2.2l1.6 1 M16 2v3 M21 7.338V5a2 2 0 00-2-2H5a2 2 0 00-2 2v14a2 2 0 002 2h2.338 M3 9h5.859 M8 2v3 M10 16a6 6 0 1 0 12 0a6 6 0 1 0 -12 0z");

    public static Geometry UserPlus => ArcIcons.Parse("M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M5 7a4 4 0 1 0 8 0a4 4 0 1 0 -8 0z M19 8L19 14 M22 11L16 11");

    public static Geometry Briefcase => ArcIcons.Parse("M16 20V4a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16 M4 6h16a2 2 0 0 1 2 2v10a2 2 0 0 1 -2 2h-16a2 2 0 0 1 -2 -2v-10a2 2 0 0 1 2 -2z");

    public static Geometry Rocket => ArcIcons.Parse("M12 15v5s3.03-.55 4-2c1.08-1.62 0-5 0-5 M4.5 16.5c-1.5 1.26-2 5-2 5s3.74-.5 5-2c.71-.84.7-2.13-.09-2.91a2.18 2.18 0 0 0-2.91-.09 M9 12a22 22 0 0 1 2-3.95A12.88 12.88 0 0 1 22 2c0 2.72-.78 7.5-6 11a22.4 22.4 0 0 1-4 2z M9 12H4s.55-3.03 2-4c1.62-1.08 5 .05 5 .05");

    public static Geometry Sprout => ArcIcons.Parse("M14 9.536V7a4 4 0 0 1 4-4h1.5a.5.5 0 0 1 .5.5V5a4 4 0 0 1-4 4 4 4 0 0 0-4 4c0 2 1 3 1 5a5 5 0 0 1-1 3 M4 9a5 5 0 0 1 8 4 5 5 0 0 1-8-4 M5 21h14");

    public static Geometry Plane => ArcIcons.Parse("M17.8 19.2 16 11l3.5-3.5C21 6 21.5 4 21 3c-1-.5-3 0-4.5 1.5L13 8 4.8 6.2c-.5-.1-.9.1-1.1.5l-.3.5c-.2.5-.1 1 .3 1.3L9 12l-2 3H4l-1 1 3 2 2 3 1-1v-3l3-2 3.5 5.3c.3.4.8.5 1.3.3l.5-.2c.4-.3.6-.7.5-1.2z");

    public static Geometry Video => ArcIcons.Parse("M16 13l5.223 3.482a.5.5 0 0 0 .777-.416V7.87a.5.5 0 0 0-.752-.432L16 10.5 M4 6h10a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2h-10a2 2 0 0 1 -2 -2v-8a2 2 0 0 1 2 -2z");

    public static Geometry MapPin => ArcIcons.Parse("M20 10c0 4.993-5.539 10.193-7.399 11.799a1 1 0 0 1-1.202 0C9.539 20.193 4 14.993 4 10a8 8 0 0 1 16 0 M9 10a3 3 0 1 0 6 0a3 3 0 1 0 -6 0z");
}
