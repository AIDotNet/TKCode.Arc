using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Surfaces;

/// <summary>
/// The Link unfurl composer: links from paste, typing and samples, the simulated fetch, the unfurl and fold, removal,
/// failures and retries, autoplay, the keyboard model, sending (pending, failure, success), drag to fold, motion levels,
/// breakpoints and accessible names.
/// </summary>
public class LinkUnfurlTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (LinkUnfurl Composer, Window Window) Open(ArcMotionLevel level = ArcMotionLevel.Full, Action<LinkUnfurl>? setup = null,
                                                              double width = 640, double height = 900)
    {
        var composer = new LinkUnfurl { AutoPlay = false, Width = width };
        setup?.Invoke(composer);
        var window = TestHelpers.Show(composer, level);
        window.Width = width + 40;
        window.Height = height;
        Pump();
        return (composer, window);
    }

    private static void Pump(int rounds = 3)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Runs <paramref name="seconds"/> of frames, letting continuations (fetches, waits) run between them.</summary>
    private static void Run(ManualFrameClock clock, double seconds)
    {
        var frames = (int)Math.Round(seconds * 60);
        for (var i = 0; i < frames; i++)
        {
            clock.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
        }
        Pump(1);
    }

    private static string? Announced(LinkUnfurl composer) => composer.Announcer?.Announcement;

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump(1);
    }

    private static void Type(Window window, string text)
    {
        foreach (var c in text)
        {
            window.KeyTextInput(c.ToString());
        }
        Pump(1);
    }

    [AvaloniaFact]
    public void A_Pasted_Address_Loads_Then_Unfurls_Its_Plain_Card()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Text = "see ");
        try
        {
            var added = new List<LinkUnfurlLink>();
            var resolved = new List<LinkUnfurlLink>();
            composer.LinkAdded += (_, e) => added.Add(e.Link);
            composer.LinkResolved += (_, e) => resolved.Add(e.Link);
            var link = composer.Paste("https://example.org/plan").ShouldNotBeNull();
            Pump();
            added.ShouldHaveSingleItem().ShouldBeSameAs(link);
            link.Status.ShouldBe(LinkUnfurlStatus.Loading);
            composer.Text.ShouldBe("see https://example.org/plan ");
            var token = composer.Editor!.Tokens.ShouldHaveSingleItem();
            token.Kind.ShouldBe(LinkUnfurlChipRenderer.Kind);
            token.Label.ShouldBe("https://example.org/plan");
            Announced(composer).ShouldBe("example.org link added, preview on its way.");
            composer.ChipRenderer.IsSweeping.ShouldBeTrue();
            AutomationProperties.GetItemStatus(composer.Editor).ShouldBe("example.org/plan, preview on its way");

            // The simulated fetch takes 1.15s.
            Run(clock, 1.1);
            link.Status.ShouldBe(LinkUnfurlStatus.Loading);
            Run(clock, 0.1);
            link.Status.ShouldBe(LinkUnfurlStatus.Ready);
            resolved.ShouldHaveSingleItem();
            link.Preview!.Title.ShouldBe("Plan");
            link.IsExpanded.ShouldBeTrue();
            Announced(composer).ShouldBe("Plan from example.org, card open.");
            composer.ChipRenderer.IsSweeping.ShouldBeFalse();
            AutomationProperties.GetItemStatus(composer.Editor).ShouldBe("Link to example.org: Plan");

            // Mid-flight the card is clipped to a rectangle between the link and its slot; it settles in about a second.
            var entry = composer.Entries.ShouldHaveSingleItem();
            var card = entry.Card.ShouldNotBeNull();
            Run(clock, 0.12);
            entry.Progress.Value.ShouldBeInRange(0.1, 0.8);
            card.ClipRect.ShouldNotBeNull();
            card.Lift.ShouldBeGreaterThan(0.2);
            composer.Lane!.Bounds.Height.ShouldBeLessThan(card.Bounds.Height);
            Run(clock, 1.2);
            entry.Progress.Value.ShouldBe(1);
            card.ClipRect.ShouldBeNull();
            card.IsVisible.ShouldBeTrue();
            // A plain card: 8 + 30 + 2 + 22.4 + 2 + 19.6 + 8 = 92 in Arc (the headless font is not Inter: allow some room).
            card.Media.IsVisible.ShouldBeFalse();
            composer.Lane.Bounds.Height.ShouldBe(card.Bounds.Height + LinkUnfurlLane.Gap, 0.5);
            card.Bounds.Width.ShouldBe(440);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Sample_Resolves_By_Address_After_Its_Image_Decodes()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Samples = LinkUnfurlSamples.Previews);
        try
        {
            var link = composer.Paste(LinkUnfurlSamples.Trail.Url).ShouldNotBeNull();
            Run(clock, 1.2);
            link.Status.ShouldBe(LinkUnfurlStatus.Loading);
            Run(clock, 0.1);
            link.Status.ShouldBe(LinkUnfurlStatus.Ready);
            link.Preview.ShouldBeSameAs(LinkUnfurlSamples.Trail);
            Run(clock, 1.2);
            var card = composer.Entries[0].Card!;
            card.Media.IsVisible.ShouldBeTrue();
            // 1.91 : 1 at the header's width (440 − 16).
            card.Media.Bounds.Width.ShouldBe(424);
            card.Media.Bounds.Height.ShouldBe(424 / 1.91, 0.1);
            AutomationProperties.GetName(card.CollapseButton).ShouldBe("Fold card: Seven lakes loop above the timber line");
            AutomationProperties.GetName(card.RemoveButton).ShouldBe("Remove link: Seven lakes loop above the timber line");
            AutomationProperties.GetName(card.Card).ShouldBe("Seven lakes loop above the timber line on Ridgeway");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Speed_Scales_The_Simulated_Fetch()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Speed = 2);
        try
        {
            var link = composer.Paste("https://example.org/plan").ShouldNotBeNull();
            Run(clock, 0.55);
            link.Status.ShouldBe(LinkUnfurlStatus.Loading);
            Run(clock, 0.05);
            link.Status.ShouldBe(LinkUnfurlStatus.Ready);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_The_Link_Or_Alt_Enter_Folds_And_Shows_Its_Card()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Text = "see ");
        try
        {
            var link = composer.Paste("https://example.org/plan")!;
            Run(clock, 2.6);
            var entry = composer.Entries[0];
            link.IsExpanded.ShouldBeTrue();
            composer.Editor!.Focus();
            Pump();
            composer.Editor.Select(composer.Text.Length, composer.Text.Length);
            Pump();
            Press(window, PhysicalKey.Enter, RawInputModifiers.Alt);
            link.IsExpanded.ShouldBeFalse();
            Announced(composer).ShouldBe("Plan, card folded.");
            composer.Text.ShouldBe("see https://example.org/plan ");
            Run(clock, 1.0);
            entry.Progress.Value.ShouldBe(0);
            entry.Card!.IsVisible.ShouldBeFalse();
            composer.Lane!.Bounds.Height.ShouldBe(0);

            Press(window, PhysicalKey.Enter, RawInputModifiers.Alt);
            link.IsExpanded.ShouldBeTrue();
            entry.Card.IsVisible.ShouldBeTrue();
            Run(clock, 1.2);

            // A click on the chip toggles too.
            composer.Editor.TryGetTokenBounds(4, window, out var chip).ShouldBeTrue();
            chip.Width.ShouldBeGreaterThan(30);
            var point = chip.Center;
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Pump();
            link.IsExpanded.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Collapse_Button_Folds_And_The_Remove_Button_Takes_The_Link_Out()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Text = "one ");
        try
        {
            var removed = new List<LinkUnfurlLink>();
            composer.LinkRemoved += (_, e) => removed.Add(e.Link);
            var link = composer.Paste("https://example.org/plan")!;
            Run(clock, 2.6);
            var entry = composer.Entries[0];
            composer.CollapsePreview(link.Id);
            link.IsExpanded.ShouldBeFalse();
            Run(clock, 1);
            composer.ExpandPreview(link.Id);
            Run(clock, 1.5);
            composer.RemoveLink(link.Id);
            Pump();
            // The card folds into the link first, then the link leaves the text.
            composer.Text.ShouldBe("one https://example.org/plan ");
            Run(clock, 1.5);
            composer.Text.ShouldBe("one ");
            composer.Links.ShouldBeEmpty();
            composer.Editor!.Tokens.ShouldBeEmpty();
            removed.ShouldHaveSingleItem().ShouldBeSameAs(link);
            Announced(composer).ShouldBe("Link removed. Plan.");
            composer.Lane!.Children.ShouldBeEmpty();
            entry.Card.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Backspace_Deletes_The_Whole_Link_And_Its_Card_Folds_In_Place()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Text = "go ");
        try
        {
            var cancelled = false;
            composer.Resolver = async (url, token) =>
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                    throw;
                }
                return null;
            };
            var link = composer.Paste("https://example.org/plan")!;
            Pump();
            composer.Editor!.Focus();
            var end = composer.Text.Length;
            composer.Editor.Select(end, end);
            Pump();
            // The first Backspace takes the space, the second the whole link.
            Press(window, PhysicalKey.Backspace);
            composer.Text.ShouldBe("go https://example.org/plan");
            Press(window, PhysicalKey.Backspace);
            composer.Text.ShouldBe("go ");
            composer.Links.ShouldBeEmpty();
            TestHelpers.PumpUntil(() => cancelled, 2000).ShouldBeTrue();
            Announced(composer).ShouldBe("Link removed.");

        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Card_Whose_Link_Was_Deleted_Shrinks_Where_It_Is_And_Goes()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Text = "go ");
        try
        {
            composer.Paste("https://example.org/plan");
            Run(clock, 2.6);
            var entry = composer.Entries[0];
            var card = entry.Card!;
            composer.Editor!.Focus();
            var end = composer.Text.Length;
            composer.Editor.Select(end, end);
            Pump();
            Press(window, PhysicalKey.Backspace);
            Press(window, PhysicalKey.Backspace);
            entry.FoldsInPlace.ShouldBeTrue();
            Run(clock, 0.1);
            // Anchored at its own top left: the visible box keeps the slot's corner.
            card.Plate.X.ShouldBe(0, 0.5);
            card.Plate.Y.ShouldBe(0, 0.5);
            card.Plate.Width.ShouldBeLessThan(440);
            Run(clock, 1);
            composer.Lane!.Children.ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Typing_An_Address_And_A_Space_Makes_A_Link()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open();
        try
        {
            composer.Editor!.Focus();
            Pump();
            Type(window, "see www.sample.io/path");
            composer.Links.ShouldBeEmpty();
            Type(window, " ");
            Pump();
            var link = composer.Links.ShouldHaveSingleItem();
            link.Address.ShouldBe("www.sample.io/path");
            link.Url.ShouldBe("https://www.sample.io/path");
            composer.Text.ShouldBe("see www.sample.io/path ");
            composer.Editor.CaretIndex.ShouldBe(composer.Text.Length);
            Type(window, "ok");
            composer.Text.ShouldBe("see www.sample.io/path ok");
            // A host without a dot stays text.
            Type(window, " https://x ");
            composer.Links.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failed_Preview_Leaves_A_Link_That_Fetches_Again_When_Clicked()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open();
        try
        {
            var calls = 0;
            composer.Resolver = (_, _) =>
            {
                calls++;
                return Task.FromResult<LinkUnfurlPreview?>(calls == 1 ? null : new LinkUnfurlPreview("https://example.org/a", "Example", "A page"));
            };
            var link = composer.Paste("https://example.org/a")!;
            Pump();
            link.Status.ShouldBe(LinkUnfurlStatus.Failed);
            link.Preview.ShouldBeNull();
            composer.Entries[0].Card.ShouldBeNull();
            Announced(composer).ShouldBe("No preview for example.org. The link stays as text.");
            AutomationProperties.GetItemStatus(composer.Editor!).ShouldBe("example.org/a, no preview, select to try again");
            composer.Retry(link.Id);
            Pump();
            calls.ShouldBe(2);
            link.Status.ShouldBe(LinkUnfurlStatus.Ready);
            link.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_AutoExpand_A_Resolved_Link_Waits_For_A_Click()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.AutoExpand = false);
        try
        {
            var link = composer.Paste("https://example.org/plan")!;
            Run(clock, 1.3);
            link.Status.ShouldBe(LinkUnfurlStatus.Ready);
            link.IsExpanded.ShouldBeFalse();
            composer.Entries[0].Card!.IsVisible.ShouldBeFalse();
            Announced(composer).ShouldBe("Plan from example.org, card ready.");
            composer.TogglePreview(link.Id);
            link.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_And_Pause_Open_And_Fold_At_Once()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(ArcMotionLevel.Reduced);
        try
        {
            var link = composer.Paste("https://example.org/plan")!;
            composer.ChipRenderer.IsSweeping.ShouldBeFalse();
            Run(clock, 1.2);
            link.IsExpanded.ShouldBeTrue();
            composer.Entries[0].Progress.Value.ShouldBe(1);
            composer.CollapsePreview(link.Id);
            composer.Entries[0].Progress.Value.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
        var (paused, pausedWindow) = Open(setup: c => c.IsPaused = true);
        try
        {
            var link = paused.Paste("https://example.org/plan")!;
            paused.ChipRenderer.IsSweeping.ShouldBeFalse();
            Run(clock, 1.2);
            paused.Entries[0].Progress.Value.ShouldBe(1);
            link.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            pausedWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Pastes_The_First_Sample_Unless_Someone_Interacted()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c =>
        {
            c.AutoPlay = true;
            c.Samples = LinkUnfurlSamples.Previews;
            c.Text = LinkUnfurlSamples.Message;
        });
        try
        {
            Run(clock, 1.0);
            composer.Links.ShouldBeEmpty();
            Run(clock, 0.2);
            var link = composer.Links.ShouldHaveSingleItem();
            link.Url.ShouldBe(LinkUnfurlSamples.Trail.Url);
            composer.Text.ShouldBe(LinkUnfurlSamples.Message + LinkUnfurlSamples.Trail.Url + " ");
            Run(clock, 1.4);
            link.IsExpanded.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
        var (other, otherWindow) = Open(setup: c =>
        {
            c.AutoPlay = true;
            c.Samples = LinkUnfurlSamples.Previews;
        });
        try
        {
            var inside = other.Editor!.TranslatePoint(new Point(40, 30), otherWindow)!.Value;
            otherWindow.MouseDown(inside, MouseButton.Left);
            otherWindow.MouseUp(inside, MouseButton.Left);
            Pump();
            other.HasInteracted.ShouldBeTrue();
            Run(clock, 1.5);
            other.Links.ShouldBeEmpty();
        }
        finally
        {
            otherWindow.Close();
        }
    }

    [AvaloniaFact]
    public void Sample_Buttons_Paste_Then_Toggle_Their_Card_And_Say_So()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Samples = LinkUnfurlSamples.Previews);
        try
        {
            var buttons = composer.SamplesPanel!.Children.OfType<LinkUnfurlSampleButton>().ToList();
            buttons.Count.ShouldBe(3);
            AutomationProperties.GetName(buttons[1]).ShouldBe("Add the Panfold sample");
            AutomationProperties.GetName(composer.SamplesPanel).ShouldBe("Sample links");
            buttons[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Pump();
            var link = composer.Links.ShouldHaveSingleItem();
            link.Url.ShouldBe(LinkUnfurlSamples.Recipe.Url);
            buttons[1].IsActive.ShouldBeTrue();
            AutomationProperties.GetName(buttons[1]).ShouldBe("Panfold sample still loading");
            Run(clock, 1.4);
            AutomationProperties.GetName(buttons[1]).ShouldBe("Fold the Panfold card");
            buttons[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            link.IsExpanded.ShouldBeFalse();
            AutomationProperties.GetName(buttons[1]).ShouldBe("Open the Panfold card");
            composer.Links.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Clicked_Sample_Shows_No_Focus_Ring_And_A_Keyboard_Ring_Is_Not_Clipped()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Samples = LinkUnfurlSamples.Previews);
        try
        {
            var first = composer.SamplesPanel!.Children.OfType<LinkUnfurlSampleButton>().First();
            var center = first.TranslatePoint(new Point(first.Bounds.Width / 2, first.Bounds.Height / 2), window)!.Value;
            window.MouseDown(center, MouseButton.Left);
            window.MouseUp(center, MouseButton.Left);
            Pump();
            composer.Links.ShouldHaveSingleItem();
            first.Classes.Contains(":focus-visible").ShouldBeFalse("a mouse click must not show the keyboard focus ring");

            composer.FocusMessage();
            Pump();
            first.Focus(NavigationMethod.Tab);
            Pump();
            first.Classes.Contains(":focus-visible").ShouldBeTrue();
            var ring = first.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_FocusRing" && b.TemplatedParent == first);
            ring.IsVisible.ShouldBeTrue();
            // The nearest clipping ancestor (the samples' scroll viewer) holds the whole ring.
            var viewer = first.GetVisualAncestors().OfType<ScrollViewer>().First();
            var ringBox = new Rect(ring.TranslatePoint(default, viewer)!.Value, ring.Bounds.Size);
            new Rect(viewer.Bounds.Size).Inflate(0.5).Contains(ringBox).ShouldBeTrue($"ring {ringBox} is clipped by the viewer {viewer.Bounds.Size}");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_Sends_The_Text_And_Previews_And_Shift_Enter_Adds_A_Line()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Text = "look ");
        try
        {
            var sent = new List<LinkUnfurlMessage>();
            composer.SendRequested += (_, e) => sent.Add(e.Message);
            composer.Paste("https://example.org/plan");
            Run(clock, 1.3);
            composer.Editor!.Focus();
            Pump();
            var end = composer.Text.Length;
            composer.Editor.Select(end, end);
            Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
            sent.ShouldBeEmpty();
            composer.Text.ShouldBe("look https://example.org/plan \n");
            Type(window, "ok");
            Press(window, PhysicalKey.Enter);
            var message = sent.ShouldHaveSingleItem();
            message.Text.ShouldBe("look https://example.org/plan \nok");
            message.Previews.ShouldHaveSingleItem().Title.ShouldBe("Plan");
            composer.SendButton!.IsSent.ShouldBeTrue();
            Announced(composer).ShouldBe("Message sent.");
            Run(clock, 1.0);
            composer.Text.ShouldBe(string.Empty);
            composer.Links.ShouldBeEmpty();
            composer.Lane!.Children.ShouldBeEmpty();
            Run(clock, 0.8);
            composer.SendButton.IsSent.ShouldBeFalse();
            // An empty message is not sent.
            Press(window, PhysicalKey.Enter);
            sent.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Pending_Send_Holds_The_Composer_And_A_Failure_Shows_An_Alert()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Text = "hello");
        try
        {
            var gate = new TaskCompletionSource();
            composer.SendHandler = _ => gate.Task;
            var send = composer.Send();
            Pump();
            composer.IsSending.ShouldBeTrue();
            composer.Editor!.IsReadOnly.ShouldBeTrue();
            composer.SendButton!.IsSending.ShouldBeTrue();
            AutomationProperties.GetName(composer.SendButton).ShouldBe("Sending message");
            gate.SetException(new InvalidOperationException("The channel is archived."));
            Pump();
            composer.IsSending.ShouldBeFalse();
            composer.SendError.ShouldBe("The channel is archived.");
            composer.ErrorText!.IsVisible.ShouldBeTrue();
            AutomationProperties.GetLiveSetting(composer.ErrorText).ShouldBe(AutomationLiveSetting.Assertive);
            composer.Text.ShouldBe("hello");
            send.IsCompleted.ShouldBeTrue();

            composer.SendHandler = _ => Task.CompletedTask;
            _ = composer.Send();
            Run(clock, 1.0);
            composer.SendError.ShouldBeNull();
            composer.Text.ShouldBe(string.Empty);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sending_Cancels_Pending_Fetches()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Text = "x ");
        try
        {
            CancellationToken seen = default;
            composer.Resolver = (_, token) =>
            {
                seen = token;
                return new TaskCompletionSource<LinkUnfurlPreview?>().Task;
            };
            composer.Paste("https://example.org/plan");
            Pump();
            seen.IsCancellationRequested.ShouldBeFalse();
            _ = composer.Send();
            Run(clock, 0.5);
            seen.IsCancellationRequested.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_A_Card_Up_Follows_The_Pointer_Then_Springs_Back_Or_Folds()
    {
        var clock = new ManualFrameClock();
        using var installed = clock.Install();
        var (composer, window) = Open(setup: c => c.Samples = LinkUnfurlSamples.Previews);
        try
        {
            var link = composer.Paste(LinkUnfurlSamples.Trail.Url)!;
            Run(clock, 2.8);
            var entry = composer.Entries[0];
            var card = entry.Card!;
            var height = card.Bounds.Height;
            var start = card.Media.TranslatePoint(new Point(card.Media.Bounds.Width / 2, card.Media.Bounds.Height / 2), window)!.Value;
            window.MouseDown(start, MouseButton.Left);
            for (var dy = 10; dy <= 80; dy += 10)
            {
                window.MouseMove(start - new Point(0, dy), RawInputModifiers.LeftMouseButton);
            }
            Pump();
            entry.IsDragging.ShouldBeTrue();
            entry.Progress.Value.ShouldBe(LinkUnfurlDragFold.Progress(1, -80, height), 0.02);
            composer.Classes.Contains(LinkUnfurl.DraggingPseudoClass).ShouldBeTrue();
            Run(clock, 0.3);
            // Hold still before letting go: no flick.
            Thread.Sleep(120);
            window.MouseUp(start - new Point(0, 80), MouseButton.Left);
            Pump();
            link.IsExpanded.ShouldBeTrue();
            Run(clock, 1.2);
            entry.Progress.Value.ShouldBe(1);

            window.MouseDown(start, MouseButton.Left);
            for (var dy = 20; dy <= 200; dy += 20)
            {
                window.MouseMove(start - new Point(0, dy), RawInputModifiers.LeftMouseButton);
            }
            Run(clock, 0.3);
            Thread.Sleep(120);
            window.MouseUp(start - new Point(0, 200), MouseButton.Left);
            Pump();
            link.IsExpanded.ShouldBeFalse();
            Run(clock, 1.2);
            entry.Progress.Value.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Composers_Step_Their_Padding_Down_And_Hide_Try()
    {
        var (composer, window) = Open(ArcMotionLevel.Off, c => c.Samples = LinkUnfurlSamples.Previews, width: 340);
        try
        {
            composer.Classes.Contains(LinkUnfurl.CompactPseudoClass).ShouldBeTrue();
            composer.Classes.Contains(LinkUnfurl.TightPseudoClass).ShouldBeTrue();
            composer.Editor!.Padding.ShouldBe(new Thickness(18, 16, 18, 12));
            composer.TryText!.IsVisible.ShouldBeFalse();
            composer.Width = 600;
            Pump();
            composer.Editor.Padding.ShouldBe(new Thickness(22, 18, 22, 14));
            composer.TryText.IsVisible.ShouldBeTrue();
            composer.TryText.Text.ShouldBe("Try");
            // The message field is at least 84 high (two 26px lines and the padding).
            composer.Editor.Bounds.Height.ShouldBe(84, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Message_Field_Is_Named_And_Describes_Its_Keys()
    {
        var (composer, window) = Open(ArcMotionLevel.Off, c =>
        {
            c.Label = LinkUnfurlSamples.Channel;
            c.Placeholder = LinkUnfurlSamples.Channel;
        });
        try
        {
            AutomationProperties.GetName(composer.Editor!).ShouldBe("Message #trail-club");
            AutomationProperties.GetHelpText(composer.Editor!).ShouldStartWith("Paste a link to preview it.");
            composer.Editor!.Placeholder.ShouldBe("Message #trail-club");
            AutomationProperties.GetName(composer.SendButton!).ShouldBe("Send message");
            composer.Labels = new LinkUnfurlLabels { Send = "Post" };
            AutomationProperties.GetName(composer.SendButton!).ShouldBe("Post");
        }
        finally
        {
            window.Close();
        }
    }
}
