using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Tests.Special.Navigation;

/// <summary>
/// The share sheet: the button morphing into the panel and back, picks and chips, the access list, channels, copy, send
/// with its confirmation, Undo, failures, the bottom sheet on narrow viewports, the keyboard and the motion.
/// </summary>
public class ShareSheetTests
{
    private static ShareSheet Build()
    {
        var sheet = new ShareSheet { Title = "Trail notes", Link = "https://example.com/doc/trail-notes" };
        sheet.AccessLevels.Add(new ShareSheetAccess { Value = "private", Label = "Private", Description = "Only the people you add", Glyph = ArcIcons.Lock });
        sheet.AccessLevels.Add(new ShareSheetAccess { Value = "team", Label = "Team", Description = "Everyone on the team can comment", Glyph = ArcIcons.Users });
        sheet.AccessLevels.Add(new ShareSheetAccess { Value = "public", Label = "Public link", Description = "Anyone with the link can view", Glyph = ArcIcons.Globe });
        sheet.Channels.Add(new ShareSheetChannel { Id = "email", Label = "Email", DoneLabel = "Sent", Glyph = ArcIcons.Mail });
        sheet.Channels.Add(new ShareSheetChannel { Id = "chat", Label = "Chat", DoneLabel = "Posted", Glyph = ArcIcons.MessageCircle });
        sheet.People.Add(new ShareSheetPerson { Id = "ada", Name = "Ada Moreno" });
        sheet.People.Add(new ShareSheetPerson { Id = "ravi", Name = "Ravi Kapoor" });
        sheet.People.Add(new ShareSheetPerson { Id = "june", Name = "June Park" });
        return sheet;
    }

    /// <summary>Shows the sheet in a window wide enough for the floating panel.</summary>
    private static Window ShowWide(ShareSheet sheet, Motion.ArcMotionLevel level = Motion.ArcMotionLevel.Off)
    {
        var host = new Panel { Children = { sheet } };
        sheet.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        sheet.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        var window = TestHelpers.Show(host, level);
        window.Width = 1000;
        window.Height = 800;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Click(Avalonia.Controls.Button button)
    {
        // The sheet's parts act on their Click event, so raising it is a press.
        button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Frames(Motion.ManualFrameClock clock, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(16));
            Dispatcher.UIThread.RunJobs();
        }
    }

    [Fact]
    public void Defaults_match_the_design()
    {
        var sheet = new ShareSheet();
        sheet.Label.ShouldBe("Share");
        sheet.Glyph.ShouldBeSameAs(ArcIcons.Share);
        sheet.Align.ShouldBe(ShareSheetAlign.End);
        sheet.SheetOnPhones.ShouldBeTrue();
        sheet.IsOpen.ShouldBeFalse();
        new ShareSheetChannel().DoneLabel.ShouldBe("Done");
    }

    [Fact]
    public void The_chosen_access_is_the_matching_value_or_the_first()
    {
        var sheet = Build();
        sheet.ChosenAccess!.Value.ShouldBe("private");
        sheet.Access = "public";
        sheet.ChosenAccess!.Value.ShouldBe("public");
        sheet.Access = "nobody-has-this";
        sheet.ChosenAccess!.Value.ShouldBe("private");
    }

    [Fact]
    public void Names_words_and_formats()
    {
        new ShareSheetPerson { Name = "Ada Moreno" }.ShortName.ShouldBe("Ada");
        new ShareSheetPerson { Id = "x" }.DisplayName.ShouldBe("x");
        ShareSheetPanelFace.Display("https://example.com/a").ShouldBe("example.com/a");
        ShareSheetSendButton.SplitFormat("Send to {0} people", out var before, out var after);
        before.ShouldBe("Send to ");
        after.ShouldBe(" people");
        var labels = new ShareSheetLabels();
        labels.Title("Plan").ShouldBe("Share “Plan”");
        labels.SentDetail(new ShareSheetAccess { Label = "Team", Description = "Can comment" }).ShouldBe("Team · can comment");
        labels.SentTitle([new ShareSheetPerson { Name = "Ada Moreno" }]).ShouldBe("Shared with Ada Moreno");
        ShareSheetAvatar.Seed("ada").ShouldBe(ShareSheetAvatar.Seed("ada"));
        ShareSheetAvatar.Seed("ada").ShouldNotBe(ShareSheetAvatar.Seed("ravi"));
    }

    [AvaloniaFact]
    public void The_button_opens_the_floating_panel_and_folds_back()
    {
        var sheet = Build();
        var window = ShowWide(sheet);
        try
        {
            sheet.UsesSheet.ShouldBeFalse();
            Click(sheet.Trigger!);
            sheet.IsOpen.ShouldBeTrue();
            sheet.IsFloating.ShouldBeTrue();
            var stage = sheet.FloatingStage!;
            stage.Current.ShouldBeSameAs(sheet.PanelFace);
            stage.CurrentKind.ShouldBe(ShareSheetFace.Panel);
            TopLevel.GetTopLevel(sheet.Surface!).ShouldNotBeNull();
            sheet.Trigger!.Opacity.ShouldBe(0);
            sheet.Trigger.IsTabStop.ShouldBeFalse();
            AutomationProperties.GetItemStatus(sheet.Trigger).ShouldBe("Expanded");
            Dispatcher.UIThread.RunJobs();
            stage.ShapeSize.Width.ShouldBe(384, 0.5);
            stage.ShapeRadius.ShouldBe(28, 0.01);

            sheet.Close();
            Dispatcher.UIThread.RunJobs();
            sheet.IsFloating.ShouldBeFalse();
            TopLevel.GetTopLevel(sheet.Surface!).ShouldBeNull();
            sheet.Trigger.Opacity.ShouldBe(1);
            sheet.Trigger.IsTabStop.ShouldBeTrue();
            AutomationProperties.GetItemStatus(sheet.Trigger).ShouldBe("Collapsed");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Align_keeps_the_chosen_edge_of_the_button()
    {
        foreach (var align in new[] { ShareSheetAlign.Start, ShareSheetAlign.End, ShareSheetAlign.Center })
        {
            var sheet = Build();
            sheet.Align = align;
            var window = ShowWide(sheet);
            // Near the top, so the panel has room below and is not pushed in from the window's edge.
            sheet.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            sheet.Margin = new Thickness(0, 40, 0, 0);
            Dispatcher.UIThread.RunJobs();
            try
            {
                sheet.Open();
                Dispatcher.UIThread.RunJobs();
                var layer = sheet.OverlayRoot!.GetVisualParent<Visual>()!;
                var trigger = sheet.Trigger!;
                var origin = trigger.TranslatePoint(default, layer)!.Value;
                var button = new Rect(origin, trigger.Bounds.Size);
                var shape = new Rect(sheet.Surface!.TranslatePoint(default, layer)!.Value, sheet.FloatingStage!.ShapeSize);
                switch (align)
                {
                    case ShareSheetAlign.Start:
                        shape.Left.ShouldBe(button.Left, 0.5);
                        shape.Top.ShouldBe(button.Top, 0.5);
                        break;
                    case ShareSheetAlign.End:
                        shape.Right.ShouldBe(button.Right, 0.5);
                        shape.Top.ShouldBe(button.Top, 0.5);
                        break;
                    default:
                        shape.Center.X.ShouldBe(button.Center.X, 0.5);
                        break;
                }
                // Never past the window's edges.
                shape.Top.ShouldBeGreaterThanOrEqualTo(8 - 0.5);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void Picking_adds_a_chip_and_the_count_and_announces_it()
    {
        var sheet = Build();
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var panel = sheet.PanelFace!;
            Click(panel.PersonButtons[1]);
            sheet.SelectedPeople.ShouldBe(new[] { sheet.People[1] });
            panel.PersonButtons[1].IsChecked.ShouldBe(true);
            panel.Chips.Count.ShouldBe(1);
            panel.Send!.Count.ShouldBe(1);
            AutomationProperties.GetName(panel.Send).ShouldBe("Send to 1 person");
            sheet.LastAnnouncement.ShouldBe("Added Ravi Kapoor");

            Click(panel.PersonButtons[0]);
            AutomationProperties.GetName(panel.Send).ShouldBe("Send to 2 people");

            // The chip's Remove takes the person out again.
            var chip = panel.Chips.Single(c => c.Person == sheet.People[1]);
            Click(chip.Remove!);
            sheet.SelectedPeople.ShouldBe(new[] { sheet.People[0] });
            panel.PersonButtons[1].IsChecked.ShouldBe(false);
            sheet.LastAnnouncement.ShouldBe("Removed Ravi Kapoor");
            AutomationProperties.GetName(chip.Remove!).ShouldBe("Remove Ravi Kapoor");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Send_with_nobody_picked_only_announces()
    {
        var sheet = Build();
        var sent = 0;
        sheet.Sent += (_, _) => sent++;
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            Click(sheet.PanelFace!.Send!);
            sent.ShouldBe(0);
            sheet.LastAnnouncement.ShouldBe("Add at least one person first");
            sheet.FloatingStage!.CurrentKind.ShouldBe(ShareSheetFace.Panel);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_successful_send_shows_the_confirmation_and_done_folds_back()
    {
        var sheet = Build();
        sheet.Access = "team";
        var gate = new TaskCompletionSource();
        ShareSheetSubmission? received = null;
        sheet.SendHandler = async (submission, _) =>
        {
            received = submission;
            await gate.Task;
        };
        ShareSheetSentEventArgs? sent = null;
        sheet.Sent += (_, e) => sent = e;
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var panel = sheet.PanelFace!;
            Click(panel.PersonButtons[0]);
            Click(panel.PersonButtons[2]);
            Click(panel.Send!);
            sheet.IsSending.ShouldBeTrue();
            panel.Send!.IsSending.ShouldBeTrue();
            received!.People.Select(p => p.Id).ShouldBe(new[] { "ada", "june" });
            received.Access!.Value.ShouldBe("team");
            sent.ShouldBeNull();

            gate.SetResult();
            TestHelpers.PumpUntil(() => sent is not null).ShouldBeTrue();
            sheet.IsSending.ShouldBeFalse();
            sheet.FloatingStage!.CurrentKind.ShouldBe(ShareSheetFace.Sent);
            sheet.SentFace!.TitleText.ShouldBe("Shared with Ada and June");
            sheet.LastAnnouncement.ShouldBe("Shared with Ada and June");
            sheet.FloatingStage.ShapeSize.Width.ShouldBe(304, 0.5);
            sheet.FloatingStage.ShapeRadius.ShouldBe(26, 0.01);
            TestHelpers.PumpUntil(() => window.FocusManager!.GetFocusedElement() == sheet.SentFace.Done).ShouldBeTrue();

            Click(sheet.SentFace.Done!);
            sheet.IsOpen.ShouldBeFalse();
            sheet.IsFloating.ShouldBeFalse();
            sheet.SelectedPeople.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_failed_send_keeps_the_panel_and_the_people()
    {
        var sheet = Build();
        sheet.SendHandler = (_, _) => throw new InvalidOperationException("offline");
        var sent = 0;
        sheet.Sent += (_, _) => sent++;
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var panel = sheet.PanelFace!;
            Click(panel.PersonButtons[1]);
            Click(panel.Send!);
            TestHelpers.PumpUntil(() => panel.ErrorMessage is not null).ShouldBeTrue();
            sent.ShouldBe(0);
            sheet.IsOpen.ShouldBeTrue();
            sheet.IsSending.ShouldBeFalse();
            sheet.FloatingStage!.CurrentKind.ShouldBe(ShareSheetFace.Panel);
            sheet.SelectedPeople.Count.ShouldBe(1);
            sheet.LastAnnouncement.ShouldBe(sheet.Labels.SendFailed);

            // A change of people clears the error.
            Click(panel.PersonButtons[0]);
            panel.ErrorMessage.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Undo_goes_back_to_the_panel_and_tells_the_app()
    {
        var sheet = Build();
        ShareSheetSentEventArgs? undone = null;
        sheet.SendUndone += (_, e) => undone = e;
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var panel = sheet.PanelFace!;
            Click(panel.PersonButtons[1]);
            Click(panel.Send!);
            TestHelpers.PumpUntil(() => sheet.FloatingStage!.CurrentKind == ShareSheetFace.Sent).ShouldBeTrue();
            Click(sheet.SentFace!.Undo!);
            undone.ShouldNotBeNull();
            undone.Submission.People.Single().Id.ShouldBe("ravi");
            sheet.FloatingStage!.CurrentKind.ShouldBe(ShareSheetFace.Panel);
            sheet.SelectedPeople.Count.ShouldBe(1);
            sheet.IsOpen.ShouldBeTrue();
            sheet.LastAnnouncement.ShouldBe(sheet.Labels.UndoneAnnouncement);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Channels_spin_then_show_their_result()
    {
        var sheet = Build();
        var gate = new TaskCompletionSource();
        sheet.ChannelHandler = async (channel, _) =>
        {
            await gate.Task;
            if (channel.Id == "chat")
            {
                throw new InvalidOperationException("down");
            }
        };
        var shared = new List<string?>();
        sheet.ChannelShared += (_, e) => shared.Add(e.Channel.Id);
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var email = sheet.PanelFace!.ChannelButtons[0];
            var chat = sheet.PanelFace.ChannelButtons[1];
            Click(email);
            Click(chat);
            email.State.ShouldBe(ShareSheetChannelState.Pending);
            chat.State.ShouldBe(ShareSheetChannelState.Pending);
            gate.SetResult();
            TestHelpers.PumpUntil(() => chat.State == ShareSheetChannelState.Failed).ShouldBeTrue();
            email.State.ShouldBe(ShareSheetChannelState.Done);
            shared.ShouldBe(new string?[] { "email" });
            sheet.LastAnnouncement.ShouldBe("Chat: Failed");
            TestHelpers.PumpUntil(() => email.State == ShareSheetChannelState.Idle, 5000).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Copy_puts_the_link_on_the_clipboard_and_raises_the_event()
    {
        var sheet = Build();
        sheet.Access = "public";
        ShareSheetLinkCopiedEventArgs? copied = null;
        sheet.LinkCopied += (_, e) => copied = e;
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var copy = sheet.PanelFace!.Copy!;
            AutomationProperties.GetName(copy).ShouldBe("Copy link");
            Click(copy);
            TestHelpers.PumpUntil(() => copy.State != ShareSheetCopyState.Idle).ShouldBeTrue();
            copy.State.ShouldBe(ShareSheetCopyState.Copied);
            copied!.Link.ShouldBe("https://example.com/doc/trail-notes");
            copied.Access!.Value.ShouldBe("public");
            sheet.LastAnnouncement.ShouldBe("Link copied to the clipboard");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_access_list_opens_moves_with_the_arrows_and_chooses()
    {
        var sheet = Build();
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var panel = sheet.PanelFace!;
            panel.AccessRow!.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            panel.IsAccessListOpen.ShouldBeTrue();
            panel.Options.Count.ShouldBe(3);
            panel.Options.Count(o => o.IsTabStop).ShouldBe(1);
            TestHelpers.PumpUntil(() => window.FocusManager!.GetFocusedElement() == panel.Options[0]).ShouldBeTrue();

            // Up from the first wraps to the last.
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(panel.Options[2]);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            window.FocusManager.GetFocusedElement().ShouldBeSameAs(panel.Options[0]);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            window.FocusManager.GetFocusedElement().ShouldBeSameAs(panel.Options[1]);

            Click(panel.Options[1]);
            sheet.Access.ShouldBe("team");
            panel.IsAccessListOpen.ShouldBeFalse();
            panel.AccessRow.Label.ShouldBe("Team");
            panel.Options[1].IsSelected.ShouldBeTrue();
            panel.Options[0].IsSelected.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Escape_closes_the_list_first_then_the_panel()
    {
        var sheet = Build();
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var panel = sheet.PanelFace!;
            Click(panel.AccessRow!);
            panel.IsAccessListOpen.ShouldBeTrue();
            panel.Options[0].Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            panel.IsAccessListOpen.ShouldBeFalse();
            sheet.IsOpen.ShouldBeTrue();
            window.FocusManager!.GetFocusedElement().ShouldBeSameAs(panel.AccessRow);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            sheet.IsOpen.ShouldBeFalse();
            window.FocusManager.GetFocusedElement().ShouldBeSameAs(sheet.Trigger);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Opening_focuses_copy()
    {
        var sheet = Build();
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            TestHelpers.PumpUntil(() => window.FocusManager!.GetFocusedElement() == sheet.PanelFace!.Copy).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_narrow_viewport_opens_a_bottom_sheet()
    {
        var sheet = Build();
        // The test window is 400 px wide: at or under the 520 px breakpoint.
        var window = TestHelpers.Show(sheet);
        try
        {
            sheet.UsesSheet.ShouldBeTrue();
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            sheet.IsSheetOpen.ShouldBeTrue();
            sheet.Presence!.IsOpen.ShouldBeTrue();
            sheet.PanelFace!.Classes.Contains(ShareSheetPanelFace.SheetPseudoClass).ShouldBeTrue();
            sheet.SheetFrame!.Grabber.ShouldNotBeNull();
            sheet.Trigger!.Opacity.ShouldBe(1);
            sheet.Presence.ScrimOpacity.ShouldBe(1, 0.01);
            sheet.Presence.Host.Scrim.Opacity.ShouldBe(1, 0.01);

            sheet.Presence.Dismiss(global::TKCode.Arc.Primitives.DismissReason.Escape);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => !sheet.IsOpen).ShouldBeTrue();
            sheet.IsSheetOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_bottom_sheet_slides_up_over_a_dimmed_page_and_springs_back_from_a_short_drag()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var sheet = Build();
        var window = TestHelpers.Show(sheet, Motion.ArcMotionLevel.Full);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var frame = sheet.SheetFrame!;
            frame.Offset.ShouldBeGreaterThan(frame.Bounds.Height);
            Frames(clock, 3);
            sheet.Presence!.ScrimOpacity.ShouldBeGreaterThan(0);
            Frames(clock, 90);
            frame.Offset.ShouldBe(0, 0.5);
            sheet.Presence.ScrimOpacity.ShouldBe(1, 0.01);
            sheet.Presence.Host.Scrim.Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void In_a_phone_viewport_the_sheet_and_its_dimmed_page_stay_inside_it()
    {
        var sheet = Build();
        var phone = new ArcViewportHost { Width = 390, Height = 700, Children = { sheet } };
        var window = TestHelpers.Show(new Panel { Children = { phone } });
        window.Width = 1200;
        window.Height = 900;
        Dispatcher.UIThread.RunJobs();
        try
        {
            sheet.UsesSheet.ShouldBeTrue();
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var host = sheet.Presence!.Host;
            host.GetVisualParent().ShouldBeSameAs(phone.Layer);
            host.Scrim.Opacity.ShouldBe(1, 0.01);
            host.Scrim.Background.ShouldNotBeNull();
            host.Scrim.IsEffectivelyVisible.ShouldBeTrue();
            host.Scrim.Bounds.Width.ShouldBe(390, 0.5);
            host.Scrim.Bounds.Height.ShouldBe(700, 0.5);
            sheet.SheetFrame!.Bounds.Width.ShouldBe(390 - 16, 0.5);

            // Moved to another host while open (the gallery's phone switch): it opens again there, dimmed.
            phone.Children.Remove(sheet);
            Dispatcher.UIThread.RunJobs();
            var other = new ArcViewportHost { Width = 360, Height = 640, Children = { sheet } };
            ((Panel)window.Content!).Children.Add(other);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => sheet.IsSheetOpen).ShouldBeTrue();
            sheet.Presence!.Host.GetVisualParent().ShouldBeSameAs(other.Layer);
            sheet.Presence.Host.Scrim.Opacity.ShouldBe(1, 0.01);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_floating_panel_in_a_phone_viewport_keeps_its_theme()
    {
        var sheet = Build();
        sheet.SheetOnPhones = false;
        var phone = new ArcViewportHost { Width = 390, Height = 700, Children = { sheet } };
        var window = TestHelpers.Show(new Panel { Children = { phone } });
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            sheet.IsFloating.ShouldBeTrue();
            sheet.OverlayRoot!.GetVisualParent().ShouldBeSameAs(phone.Layer);
            // The shape's logical parent is the sheet, so its parts find their templates and tokens.
            sheet.PanelFace!.Copy.ShouldNotBeNull();
            sheet.Surface!.Background.ShouldNotBeNull();
            sheet.FloatingStage!.ShapeSize.Width.ShouldBe(390 - 32, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SheetOnPhones_false_floats_even_when_narrow()
    {
        var sheet = Build();
        sheet.SheetOnPhones = false;
        var window = TestHelpers.Show(sheet);
        try
        {
            sheet.UsesSheet.ShouldBeFalse();
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            sheet.IsFloating.ShouldBeTrue();
            // Never wider than the viewport less 32 px.
            sheet.FloatingStage!.ShapeSize.Width.ShouldBeLessThanOrEqualTo(400 - 32 + 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_change_the_words()
    {
        var sheet = Build();
        sheet.Labels = new ShareSheetLabels
        {
            Title = name => $"分享“{name}”",
            Send = "发送",
            SendTo = n => "发送给 {0} 人",
            AddedAnnouncement = name => $"已添加 {name}"
        };
        var window = ShowWide(sheet);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var panel = sheet.PanelFace!;
            AutomationProperties.GetName(panel).ShouldBe("分享“Trail notes”");
            AutomationProperties.GetName(panel.Send!).ShouldBe("发送");
            Click(panel.PersonButtons[0]);
            AutomationProperties.GetName(panel.Send!).ShouldBe("发送给 1 人");
            sheet.LastAnnouncement.ShouldBe("已添加 Ada Moreno");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_shape_springs_from_the_button_to_the_panel_and_back()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var sheet = Build();
        var window = ShowWide(sheet, Motion.ArcMotionLevel.Full);
        try
        {
            Frames(clock, 2);
            var buttonWidth = sheet.Trigger!.Bounds.Width;
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            var stage = sheet.FloatingStage!;
            stage.ShapeSize.Width.ShouldBe(buttonWidth, 0.5);
            Frames(clock, 6);
            stage.ShapeSize.Width.ShouldBeGreaterThan(buttonWidth + 1);
            stage.ShapeSize.Width.ShouldBeLessThan(384);
            stage.ShapeRadius.ShouldBeInRange(18, 28);
            Frames(clock, 90);
            stage.ShapeSize.Width.ShouldBe(384, 0.5);
            stage.LeavingCount.ShouldBe(0);
            stage.Children.Count.ShouldBe(1);
            sheet.PanelFace!.Opacity.ShouldBe(1, 0.01);

            sheet.Close();
            Dispatcher.UIThread.RunJobs();
            Frames(clock, 4);
            sheet.IsFloating.ShouldBeTrue();
            stage.ShapeSize.Width.ShouldBeLessThan(384);
            Frames(clock, 90);
            TestHelpers.PumpUntil(() => !sheet.IsFloating).ShouldBeTrue();
            sheet.Trigger.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_pick_flies_its_avatar_to_the_chip_and_an_empty_send_nudges_the_row()
    {
        var clock = new Motion.ManualFrameClock();
        using var installed = clock.Install();
        var sheet = Build();
        var window = ShowWide(sheet, Motion.ArcMotionLevel.Full);
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            Frames(clock, 90);
            var panel = sheet.PanelFace!;

            Click(panel.Send!);
            Frames(clock, 2);
            panel.ShakeOffset.ShouldBeLessThan(-0.5);
            Frames(clock, 60);
            panel.ShakeOffset.ShouldBe(0, 0.05);

            Click(panel.PersonButtons[2]);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.PumpUntil(() => panel.FlightCount == 1, 2000).ShouldBeTrue();
            var chip = panel.Chips.Single();
            chip.Avatar!.Opacity.ShouldBe(0);
            Frames(clock, 90);
            TestHelpers.PumpUntil(() => panel.FlightCount == 0, 2000).ShouldBeTrue();
            chip.Avatar.Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Detaching_while_open_unmounts_the_shape()
    {
        var sheet = Build();
        var host = new Panel { Children = { sheet } };
        var window = TestHelpers.Show(host);
        window.Width = 1000;
        Dispatcher.UIThread.RunJobs();
        try
        {
            sheet.Open();
            Dispatcher.UIThread.RunJobs();
            TopLevel.GetTopLevel(sheet.Surface!).ShouldNotBeNull();
            host.Children.Clear();
            Dispatcher.UIThread.RunJobs();
            TopLevel.GetTopLevel(sheet.Surface!).ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
