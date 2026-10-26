using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using StreamMark.Avalonia.Rendering;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.CorePrimitives;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="ArenaAnswerView"/>: appended text, completion and the new-round reset; the edge fade and the keyboard ring above
/// it; following on a manual clock (22% a frame, released by the person's scroll, back only at the current bottom within 4px);
/// the keys; the error card (height on the Smooth spring, fades, alert, action, retry glyph); Reduced and Off; a long answer
/// with a list and a code block streamed in five-word batches.
/// </summary>
public class ArenaAnswerViewTests
{
    private const string AnswerName = "Model A answer";
    private const string ErrorTitle = "Connection dropped";
    private const string ErrorBody = "The stream ended before this answer was complete.";

    /// <summary>An invented answer: a lead paragraph, a small heading, a list, a code block and a closing line.</summary>
    internal const string LongAnswer =
        "Debounce waits for the input to go quiet; throttle lets one call through per window. Both stop a busy handler " +
        "from running on every event, but they suit different jobs.\n\n" +
        "#### Debounce\n\n" +
        "Use it when only the final value matters, like a search box that should query once the person stops typing.\n\n" +
        "#### Throttle\n\n" +
        "Use it when steady updates matter, like a scroll listener that moves a progress bar.\n\n" +
        "- **Debounce:** fires after 300ms of silence, so a fast typist triggers one request.\n" +
        "- **Throttle:** fires at most every 100ms, so a long scroll still updates ten times a second.\n" +
        "- **Leading edge:** both can run the first call at once and then hold the rest.\n\n" +
        "```ts\n" +
        "function debounce(run: () => void, wait = 300) {\n" +
        "  let timer: ReturnType<typeof setTimeout> | undefined;\n" +
        "  return () => {\n" +
        "    clearTimeout(timer);\n" +
        "    timer = setTimeout(run, wait); // only the last call within the window survives the wait\n" +
        "  };\n" +
        "}\n" +
        "```\n\n" +
        "Pick debounce for intent and throttle for progress.";

    private const string Lines =
        "First line of a long answer that keeps going.\n\nSecond paragraph with a few more words in it.\n\n" +
        "Third paragraph, still going, to fill the pane.\n\nFourth paragraph adds a little more height.\n\n" +
        "Fifth paragraph runs past the bottom edge.\n\nSixth paragraph, the last one for now.\n\n";

    private sealed class Rig : IDisposable
    {
        private readonly IDisposable _clockInstall;

        public ManualFrameClock Clock { get; } = new();
        public ArenaAnswerView View { get; }
        public Window Window { get; }
        public int Requests { get; private set; }

        public Rig(ArcMotionLevel level = ArcMotionLevel.Off, double width = 391, double height = 272)
        {
            _clockInstall = Clock.Install();
            View = new ArenaAnswerView
            {
                Width = width,
                Height = height,
                AnswerName = AnswerName,
                StreamingStatus = "Writing",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
            View.ActionRequested += (_, _) => Requests++;
            Window = TestHelpers.Show(View, level);
            Window.Width = 640;
            Window.Height = 600;
            Layout();
        }

        public ScrollArea Scroll => View.Scroll!;
        public double Offset => Scroll.Offset.Y;
        public double Bottom => Math.Max(0, Scroll.Extent.Height - Scroll.Viewport.Height);
        public ArenaAnswerErrorHost Host => View.ErrorHost!;
        public ArenaAnswerErrorCard Card => View.ErrorCard!;

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Runs <paramref name="count"/> frames of 1/60 s and the layout they cause.</summary>
        public void Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                CoreFrames.Run(Clock, 1);
                Layout();
            }
        }

        /// <summary>Waits until StreamMark has parsed everything appended and the view shows it.</summary>
        public async Task Parsed()
        {
            for (var i = 0; i < 600; i++)
            {
                Dispatcher.UIThread.RunJobs();
                var body = View.Body!;
                var document = body.Stream?.Document;
                if (document is null || document.Latest.Version >= document.Version)
                {
                    body.View?.ApplyLatest();
                    Layout();
                    if (document is null || ReferenceEquals(body.View?.Snapshot, document.Latest))
                    {
                        return;
                    }
                }
                await Task.Delay(5);
            }
            throw new TimeoutException("The Markdown was not parsed in time.");
        }

        public Point Center => View.TranslatePoint(new Point(View.Bounds.Width / 2, 100), Window) ?? default;

        /// <summary>The wheel over the answer: positive moves up, 50px a notch.</summary>
        public void Wheel(double notches)
        {
            Window.MouseWheel(Center, new Vector(0, notches));
            Layout();
        }

        /// <summary>A press and release on the answer (stands in for grabbing the scrollbar).</summary>
        public void Touch()
        {
            Window.MouseDown(Center, MouseButton.Left);
            Window.MouseUp(Center, MouseButton.Left);
            Layout();
        }

        public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
        {
            Window.KeyPressQwerty(key, modifiers);
            Window.KeyReleaseQwerty(key, modifiers);
            Layout();
        }

        public void SetOffset(double y)
        {
            Scroll.Offset = new Vector(0, y);
            Layout();
        }

        public void Dispose()
        {
            Window.Close();
            _clockInstall.Dispose();
        }
    }

    private static void ShowError(ArenaAnswerView view, bool retry = true) =>
        view.ShowError(ErrorTitle, ErrorBody, retry ? "Retry model A" : "Try a sample prompt", retry);

    #region Text

    [AvaloniaFact]
    public async Task Append_Shows_The_Text_And_Complete_Ends_It()
    {
        using var rig = new Rig();
        rig.View.Append("Hi Jonas, ");
        rig.View.Append("could we move our Friday 1:1 to Monday?");
        await rig.Parsed();
        rig.View.Text.ShouldBe("Hi Jonas, could we move our Friday 1:1 to Monday?");
        var body = rig.View.Body!;
        body.IsPaced.ShouldBeFalse();
        body.IsStreaming.ShouldBeTrue();
        body.Stream!.Document.GetText(0, 200).ShouldBe(rig.View.Text);
        rig.Scroll.Extent.Height.ShouldBeGreaterThan(8 + 18 + 20);

        rig.View.Complete();
        rig.View.IsCompleted.ShouldBeTrue();
        rig.View.Append(" Thanks!");
        rig.View.Text.ShouldNotContain("Thanks");
        await rig.Parsed();
        body.IsStreaming.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Reset_Clears_The_Text_Scrolls_To_The_Top_And_Follows_Without_Motion()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.View.Append(Lines + Lines);
        await rig.Parsed();
        rig.Frames(40);
        rig.Bottom.ShouldBeGreaterThan(100);
        rig.Touch();
        rig.SetOffset(40);
        rig.View.IsFollowing.ShouldBeFalse();
        ShowError(rig.View);
        rig.Frames(10);
        rig.Host.IsMoving.ShouldBeTrue();

        // A press in the answer just before (the Retry click) must not make the reset's clamp a scroll by the person.
        rig.Touch();
        rig.View.Reset();
        rig.View.IsFollowing.ShouldBeTrue();
        rig.Offset.ShouldBe(0);
        rig.View.Text.ShouldBeEmpty();
        rig.View.IsErrorShown.ShouldBeFalse();
        rig.Layout();
        rig.Offset.ShouldBe(0);
        rig.Bottom.ShouldBe(0);
        rig.View.Body!.Bounds.Height.ShouldBeLessThan(1);
        rig.Host.CurrentHeight.ShouldBe(0);
        rig.Host.IsMoving.ShouldBeFalse();
        rig.Card.IsVisible.ShouldBeFalse();
        rig.View.Follow!.IsChasing.ShouldBeFalse();
        rig.View.IsFollowing.ShouldBeTrue();

        // The next round streams again from the top.
        rig.View.Append("A new answer.");
        await rig.Parsed();
        rig.View.Text.ShouldBe("A new answer.");
        rig.View.Body!.IsStreaming.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task Text_Appended_Before_The_Template_Shows_Once_It_Exists()
    {
        var view = new ArenaAnswerView { Width = 391, Height = 272 };
        view.Append("Written before the answer was shown.");
        view.Complete();
        var window = TestHelpers.Show(view);
        try
        {
            for (var i = 0; i < 200 && view.Body?.Stream?.Document is not { IsComplete: true }; i++)
            {
                Dispatcher.UIThread.RunJobs();
                await Task.Delay(5);
            }
            view.Body!.Stream!.Document.GetText(0, 100).ShouldBe("Written before the answer was shown.");
            view.Body.Stream.Document.IsComplete.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Each_Piece_Fades_In_Over_160ms_At_Full_And_At_Once_Otherwise()
    {
        foreach (var (level, expected) in new[]
                 {
                     (ArcMotionLevel.Full, TimeSpan.FromMilliseconds(160)), (ArcMotionLevel.Reduced, TimeSpan.Zero),
                     (ArcMotionLevel.Off, TimeSpan.Zero)
                 })
        {
            using var rig = new Rig(level);
            rig.View.Append("One batch of words.");
            await rig.Parsed();
            rig.View.Body!.Stream!.State.FadeDuration.ShouldBe(expected);
        }
        using (var rig = new Rig(ArcMotionLevel.Full))
        {
            rig.View.RevealFadeDuration = TimeSpan.FromMilliseconds(150);
            rig.View.Append("Another batch.");
            await rig.Parsed();
            rig.View.Body!.Stream!.State.FadeDuration.ShouldBe(TimeSpan.FromMilliseconds(150));
        }
    }

    [AvaloniaFact]
    public void The_Body_Is_14_On_22_4_With_12_Between_Blocks_Inside_8_20_18()
    {
        using var rig = new Rig();
        var body = rig.View.Body!;
        body.FontSize.ShouldBe(14);
        body.LineHeight.ShouldBe(22.4);
        body.BlockSpacing.ShouldBe(12);
        body.RevealFadeInReducedMotion.ShouldBeFalse();
        var origin = body.TranslatePoint(default, rig.View)!.Value;
        origin.X.ShouldBe(20, 0.01);
        origin.Y.ShouldBe(8, 0.01);
        body.Bounds.Width.ShouldBe(391 - 40, 0.01);
        // The arena's code block: SurfaceMuted, 1px Border, radius 14 (the RadiusStep14 token, so a corner style reaches it).
        var radius = body.Resources[StreamMark.Avalonia.MarkdownResourceKeys.CornerRadius].ShouldBeOfType<StreamMark.Avalonia.MarkdownResourceAlias>();
        radius.Key.ShouldBe(TKCode.Arc.DesignTokens.ArcDesignTokenKind.RadiusStep14);
    }

    #endregion

    #region Edge fade and focus

    [AvaloniaFact]
    public void The_Edge_Fade_Hides_8px_At_The_Top_And_18px_At_The_Bottom_And_Follows_The_Height()
    {
        using var rig = new Rig();
        var follow = rig.View.Follow!;
        var mask = follow.OpacityMask.ShouldBeOfType<ImmutableLinearGradientBrush>();
        mask.GradientStops.Count.ShouldBe(4);
        mask.GradientStops[0].Color.A.ShouldBe((byte)0);
        mask.GradientStops[1].Color.A.ShouldBe((byte)255);
        mask.GradientStops[2].Color.A.ShouldBe((byte)255);
        mask.GradientStops[3].Color.A.ShouldBe((byte)0);
        var band = rig.View.MaskBand!.Value;
        band.Top.ShouldBe(8 / 272.0, 1e-4);
        band.Bottom.ShouldBe(1 - 18 / 272.0, 1e-4);
        // The scroll area draws no fades of its own.
        rig.Scroll.FadeLength.ShouldBe(0);

        rig.View.Height = 200;
        rig.Layout();
        band = rig.View.MaskBand!.Value;
        band.Top.ShouldBe(8 / 200.0, 1e-4);
        band.Bottom.ShouldBe(1 - 18 / 200.0, 1e-4);
        // Only a new height publishes a new brush.
        var brush = follow.OpacityMask;
        rig.View.InvalidateArrange();
        rig.Layout();
        follow.OpacityMask.ShouldBeSameAs(brush);
    }

    [AvaloniaFact]
    public void The_Answer_Is_The_Tab_Stop_With_An_Inset_Ring_Above_The_Fade()
    {
        using var rig = new Rig();
        var ring = rig.View.FocusRing!;
        // A sibling drawn after the masked scroller, not inside it.
        ring.GetVisualAncestors().ShouldNotContain(rig.View.Follow!);
        var panel = (Panel)ring.GetVisualParent()!;
        panel.Children.IndexOf(ring).ShouldBeGreaterThan(panel.Children.IndexOf(rig.View.Follow!));
        ring.OpacityMask.ShouldBeNull();
        ring.BorderThickness.ShouldBe(new Thickness(2));
        ring.Margin.ShouldBe(default);
        TestHelpers.ColorOf(ring.BorderBrush).ShouldBe(ArcTokens.GetColor(rig.View, ArcDesignTokenKind.FocusRing));
        ring.IsVisible.ShouldBeFalse();

        rig.View.Focusable.ShouldBeTrue();
        KeyboardNavigation.GetIsTabStop(rig.View).ShouldBeTrue();
        rig.Scroll.Focusable.ShouldBeFalse();
        rig.View.Focus(NavigationMethod.Tab);
        rig.Layout();
        rig.View.IsFocused.ShouldBeTrue();
        ring.IsVisible.ShouldBeTrue();
        // Inset: the 2px ring covers the answer's own edge (Arc: outline-offset −2).
        ring.Bounds.ShouldBe(new Rect(0, 0, 391, 272));

        // A pointer focus draws no ring.
        using var pointer = new Rig();
        pointer.View.Focus(NavigationMethod.Pointer);
        pointer.Layout();
        pointer.View.IsFocused.ShouldBeTrue();
        pointer.View.FocusRing!.IsVisible.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task The_Text_Is_Not_A_Tab_Stop()
    {
        using var rig = new Rig();
        rig.View.Append("Some text.");
        await rig.Parsed();
        var view = rig.View.Body!.View!;
        KeyboardNavigation.GetIsTabStop(view).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void It_Is_A_Named_Group_Busy_While_Streaming()
    {
        using var rig = new Rig();
        AutomationProperties.GetName(rig.View).ShouldBe(AnswerName);
        var peer = ControlAutomationPeer.CreatePeerForElement(rig.View);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        peer.GetName().ShouldBe(AnswerName);
        peer.IsKeyboardFocusable().ShouldBeTrue();
        AutomationProperties.GetItemStatus(rig.View).ShouldBeNull();

        rig.View.IsStreaming = true;
        AutomationProperties.GetItemStatus(rig.View).ShouldBe("Writing");
        rig.View.Classes.ShouldContain(ArenaAnswerView.StreamingPseudoClass);
        rig.View.IsStreaming = false;
        AutomationProperties.GetItemStatus(rig.View).ShouldBeNull();
        rig.View.Classes.ShouldNotContain(ArenaAnswerView.StreamingPseudoClass);

        rig.View.AnswerName = "Model A, Kite 5 answer";
        AutomationProperties.GetName(rig.View).ShouldBe("Model A, Kite 5 answer");
    }

    #endregion

    #region Following

    [AvaloniaFact]
    public async Task Growth_While_Following_Chases_22_Percent_Of_The_Rest_Per_Frame()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.View.Append(Lines);
        await rig.Parsed();
        rig.Frames(40);
        rig.Offset.ShouldBe(rig.Bottom, 0.5);
        rig.View.IsFollowing.ShouldBeTrue();
        rig.View.Follow!.ReattachDistance.ShouldBe(4);

        var from = rig.Offset;
        rig.View.Append(Lines);
        await rig.Parsed();
        var bottom = rig.Bottom;
        bottom.ShouldBeGreaterThan(from + 50);
        rig.View.Follow!.IsChasing.ShouldBeTrue();
        rig.Offset.ShouldBe(from, 0.5);
        for (var n = 1; n <= 4; n++)
        {
            rig.Frames(1);
            rig.Offset.ShouldBe(bottom - (bottom - from) * Math.Pow(0.78, n), 1);
        }
        rig.Frames(40);
        rig.Offset.ShouldBe(rig.Bottom, 0.01);
        rig.View.Follow.IsChasing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Reduced_And_Off_Jump_To_The_Bottom()
    {
        foreach (var level in new[] { ArcMotionLevel.Reduced, ArcMotionLevel.Off })
        {
            using var rig = new Rig(level);
            rig.View.Append(Lines + Lines);
            await rig.Parsed();
            rig.Bottom.ShouldBeGreaterThan(100);
            rig.Offset.ShouldBe(rig.Bottom, 0.5);
            rig.View.Follow!.IsChasing.ShouldBeFalse();
        }
    }

    [AvaloniaFact]
    public async Task The_Wheel_Up_Releases_And_New_Text_Then_Stays_Put()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.View.Append(Lines + Lines);
        await rig.Parsed();
        rig.Frames(40);
        rig.Wheel(1);
        rig.View.IsFollowing.ShouldBeFalse();
        var offset = rig.Offset;
        offset.ShouldBeLessThan(rig.Bottom - 4);
        rig.View.Append(Lines);
        await rig.Parsed();
        rig.Frames(20);
        rig.Offset.ShouldBe(offset, 0.5);
        rig.View.Follow!.IsChasing.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task Following_Resumes_Only_Within_4px_Of_The_Current_Bottom()
    {
        using var rig = new Rig();
        rig.View.Append(Lines + Lines);
        await rig.Parsed();
        rig.Wheel(2);
        rig.View.IsFollowing.ShouldBeFalse();
        var oldBottom = rig.Bottom;

        // More text arrives; going back to where the bottom was is not enough.
        rig.View.Append(Lines);
        await rig.Parsed();
        rig.Bottom.ShouldBeGreaterThan(oldBottom + 50);
        rig.Touch();
        rig.SetOffset(oldBottom);
        rig.View.IsFollowing.ShouldBeFalse();

        rig.Touch();
        rig.SetOffset(rig.Bottom - 6);
        rig.View.IsFollowing.ShouldBeFalse();
        rig.Touch();
        rig.SetOffset(rig.Bottom - 3);
        rig.View.IsFollowing.ShouldBeTrue();

        rig.View.Append(Lines);
        await rig.Parsed();
        rig.Offset.ShouldBe(rig.Bottom, 0.5);
    }

    [AvaloniaFact]
    public async Task Programmatic_Offsets_Never_Release()
    {
        using var rig = new Rig();
        rig.View.Append(Lines + Lines);
        await rig.Parsed();
        rig.SetOffset(30);
        rig.View.IsFollowing.ShouldBeTrue();
        rig.Offset.ShouldBe(rig.Bottom, 0.5);
    }

    #endregion

    #region Keys

    [AvaloniaFact]
    public async Task Up_PageUp_And_Home_Scroll_Up_And_Release()
    {
        foreach (var key in new[] { PhysicalKey.ArrowUp, PhysicalKey.PageUp, PhysicalKey.Home })
        {
            using var rig = new Rig();
            rig.View.Append(Lines + Lines + Lines);
            await rig.Parsed();
            var bottom = rig.Bottom;
            rig.View.Focus(NavigationMethod.Tab);
            rig.Key(key);
            rig.View.IsFollowing.ShouldBeFalse();
            var expected = key switch
            {
                PhysicalKey.ArrowUp => bottom - ArenaAnswerView.LineStep,
                PhysicalKey.PageUp => bottom - rig.Scroll.Viewport.Height * 0.9,
                _ => 0
            };
            rig.Offset.ShouldBe(expected, 0.5);
            rig.View.Append(Lines);
            await rig.Parsed();
            rig.Offset.ShouldBe(expected, 0.5);
        }
    }

    [AvaloniaFact]
    public async Task End_Goes_Back_To_The_Bottom_And_Follows_Again()
    {
        using var rig = new Rig();
        rig.View.Append(Lines + Lines + Lines);
        await rig.Parsed();
        rig.View.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Home);
        rig.View.IsFollowing.ShouldBeFalse();
        rig.Key(PhysicalKey.ArrowDown);
        rig.Offset.ShouldBe(ArenaAnswerView.LineStep, 0.5);
        rig.View.IsFollowing.ShouldBeFalse();
        rig.Key(PhysicalKey.End);
        rig.Offset.ShouldBe(rig.Bottom, 0.5);
        rig.View.IsFollowing.ShouldBeTrue();
        rig.View.Append(Lines);
        await rig.Parsed();
        rig.Offset.ShouldBe(rig.Bottom, 0.5);
    }

    [AvaloniaFact]
    public async Task A_Key_Up_At_Full_Motion_Releases_Against_A_Running_Chase()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.View.Append(Lines + Lines);
        await rig.Parsed();
        rig.Frames(40);
        rig.View.Append(Lines);
        await rig.Parsed();
        rig.Frames(2);
        rig.View.Follow!.IsChasing.ShouldBeTrue();
        rig.View.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.PageUp);
        rig.Frames(6);
        rig.View.IsFollowing.ShouldBeFalse();
        rig.Frames(60);
        var offset = rig.Offset;
        offset.ShouldBeLessThan(rig.Bottom - 100);
        rig.Frames(10);
        rig.Offset.ShouldBe(offset, 0.01);
    }

    [AvaloniaFact]
    public async Task Keys_With_Control_Or_Already_Handled_Are_Left_Alone()
    {
        using var rig = new Rig();
        rig.View.Append(Lines + Lines);
        await rig.Parsed();
        rig.View.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Home, RawInputModifiers.Control);
        rig.View.IsFollowing.ShouldBeTrue();
        rig.Offset.ShouldBe(rig.Bottom, 0.5);
    }

    #endregion

    #region Error card

    [AvaloniaFact]
    public void The_Card_Is_A_Bordered_Box_With_The_Alert_Title_Text_And_Action()
    {
        using var rig = new Rig();
        ShowError(rig.View);
        rig.Layout();
        var card = rig.Card;
        card.IsVisible.ShouldBeTrue();
        card.Opacity.ShouldBe(1);
        card.Margin.ShouldBe(new Thickness(0, 14, 0, 0));
        card.Padding.ShouldBe(new Thickness(14, 12, 14, 14));
        card.BorderThickness.ShouldBe(new Thickness(1));
        card.CornerRadius.ShouldBe(new CornerRadius(14));
        card.Background.ShouldBeNull();
        TestHelpers.ColorOf(card.BorderBrush).ShouldBe(ArcTokens.GetColor(rig.View, ArcDesignTokenKind.Border));

        var glyphs = card.GetVisualDescendants().OfType<ArcGlyph>().ToList();
        var alert = glyphs.First(g => g.Name != "PART_ErrorActionIcon");
        alert.Size.ShouldBe(14);
        TestHelpers.ColorOf(alert.Foreground).ShouldBe(ArcTokens.GetColor(rig.View, ArcDesignTokenKind.Danger));
        var title = TestHelpers.Descendant<TextBlock>(card, ArenaAnswerView.ErrorTitlePart);
        title.Text.ShouldBe(ErrorTitle);
        title.FontWeight.ShouldBe(Avalonia.Media.FontWeight.Medium);
        title.FontSize.ShouldBe(14);
        (title.TranslatePoint(default, card)!.Value.X - alert.TranslatePoint(default, card)!.Value.X).ShouldBe(14 + 8, 0.01);
        var body = TestHelpers.Descendant<TextBlock>(card, ArenaAnswerView.ErrorTextPart);
        body.Text.ShouldBe(ErrorBody);
        TestHelpers.ColorOf(body.Foreground).ShouldBe(ArcTokens.GetColor(rig.View, ArcDesignTokenKind.TextSecondary));

        var action = rig.View.ErrorAction!;
        action.Variant.ShouldBe(ButtonVariant.Secondary);
        action.Size.ShouldBe(ControlSize.Small);
        action.TranslatePoint(default, card)!.Value.X.ShouldBe(15, 0.01);
        TestHelpers.Descendant<ArcGlyph>(card, "PART_ErrorActionIcon").IsVisible.ShouldBeTrue();
        TestHelpers.Descendant<TextBlock>(card, ArenaAnswerView.ErrorActionTextPart).Text.ShouldBe("Retry model A");
        rig.View.Classes.ShouldContain(ArenaAnswerView.ErrorPseudoClass);
        rig.View.Classes.ShouldContain(ArenaAnswerView.RetryPseudoClass);

        // The slot is the card plus its 14 above; it ends the scrolled content.
        rig.Host.CurrentHeight.ShouldBe(card.Bounds.Height + 14, 0.5);
        var content = TestHelpers.Descendant<StackPanel>(rig.View, ArenaAnswerView.BodyPart);
        content.DesiredSize.Height.ShouldBe(8 + rig.View.Body!.Bounds.Height + rig.Host.Bounds.Height + 18, 0.5);
        rig.Host.TranslatePoint(default, content)!.Value.Y.ShouldBe(rig.View.Body.Bounds.Height, 0.5);
    }

    [AvaloniaFact]
    public void A_Card_Without_Retry_Has_No_Glyph()
    {
        using var rig = new Rig();
        ShowError(rig.View, retry: false);
        rig.Layout();
        rig.View.ErrorActionText.ShouldBe("Try a sample prompt");
        TestHelpers.Descendant<ArcGlyph>(rig.Card, "PART_ErrorActionIcon").IsVisible.ShouldBeFalse();
        rig.View.Classes.ShouldNotContain(ArenaAnswerView.RetryPseudoClass);
    }

    [AvaloniaFact]
    public void The_Card_Is_An_Assertive_Alert_Named_By_Its_Title_And_Text()
    {
        using var rig = new Rig();
        ShowError(rig.View);
        rig.Layout();
        AutomationProperties.GetLiveSetting(rig.Card).ShouldBe(AutomationLiveSetting.Assertive);
        AutomationProperties.GetName(rig.Card).ShouldBe(ErrorTitle + ". " + ErrorBody);
        var peer = ControlAutomationPeer.CreatePeerForElement(rig.Card);
        peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
        peer.IsControlElement().ShouldBeTrue();
        AutomationProperties.GetName(rig.View.ErrorAction!).ShouldBe("Retry model A");
        rig.View.HideError();
        AutomationProperties.GetName(rig.Card).ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_Action_Raises_ActionRequested_By_Pointer_And_Keyboard()
    {
        using var rig = new Rig();
        ShowError(rig.View);
        rig.Layout();
        var action = rig.View.ErrorAction!;
        var point = action.TranslatePoint(new Point(action.Bounds.Width / 2, action.Bounds.Height / 2), rig.Window)!.Value;
        rig.Window.MouseDown(point, MouseButton.Left);
        rig.Window.MouseUp(point, MouseButton.Left);
        rig.Layout();
        rig.Requests.ShouldBe(1);
        action.Focus(NavigationMethod.Tab);
        rig.Key(PhysicalKey.Enter);
        rig.Requests.ShouldBe(2);
    }

    [AvaloniaFact]
    public void At_Full_The_Height_Opens_On_The_Smooth_Spring_While_The_Card_Fades_In()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.View.Append("Hi Jonas,");
        rig.Layout();
        ShowError(rig.View);
        rig.View.IsErrorShown.ShouldBeTrue();
        rig.Layout();
        rig.Host.IsOpen.ShouldBeTrue();
        rig.Host.IsMoving.ShouldBeTrue();
        var target = rig.Host.TargetHeight;
        target.ShouldBe(rig.Card.DesiredSize.Height, 0.5);
        rig.Host.CurrentHeight.ShouldBeLessThan(1);
        rig.Card.Opacity.ShouldBeLessThan(0.05);

        // Smooth (ω ≈ 13.1, critically damped): about half way after 8 frames, 99% by about 490ms.
        rig.Frames(8);
        rig.Host.CurrentHeight.ShouldBeInRange(target * 0.35, target * 0.7);
        rig.Card.Opacity.ShouldBeInRange(0.5, 1);
        rig.Frames(3);
        rig.Card.Opacity.ShouldBe(1, 0.001);
        rig.Frames(19);
        rig.Host.CurrentHeight.ShouldBeGreaterThan(target * 0.97);
        rig.Frames(40);
        rig.Host.CurrentHeight.ShouldBe(target, 0.01);
        rig.Host.IsMoving.ShouldBeFalse();
        rig.Host.Bounds.Height.ShouldBe(target, 0.5);
    }

    [AvaloniaFact]
    public void At_Full_HideError_Fades_Out_In_100ms_While_The_Height_Closes()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        ShowError(rig.View);
        rig.Frames(60);
        var open = rig.Host.CurrentHeight;
        open.ShouldBeGreaterThan(50);

        rig.View.HideError();
        rig.View.IsErrorShown.ShouldBeFalse();
        rig.View.Classes.ShouldNotContain(ArenaAnswerView.ErrorPseudoClass);
        rig.Host.IsOpen.ShouldBeFalse();
        rig.Frames(3);
        rig.Card.Opacity.ShouldBeInRange(0.05, 0.9);
        rig.Card.IsHitTestVisible.ShouldBeFalse();
        rig.Host.CurrentHeight.ShouldBeInRange(open * 0.5, open * 0.999);
        rig.Frames(4);
        rig.Card.IsVisible.ShouldBeFalse();
        rig.Host.CurrentHeight.ShouldBeGreaterThan(5);
        rig.Frames(40);
        rig.Host.CurrentHeight.ShouldBe(0, 0.01);
        rig.Host.Bounds.Height.ShouldBe(0);
    }

    [AvaloniaFact]
    public void At_Reduced_The_Card_Fades_In_120ms_And_The_Height_Moves_At_Once()
    {
        using var rig = new Rig(ArcMotionLevel.Reduced);
        ShowError(rig.View);
        rig.Layout();
        var target = rig.Host.TargetHeight;
        rig.Host.CurrentHeight.ShouldBe(target, 0.01);
        rig.Host.IsMoving.ShouldBeFalse();
        rig.Frames(4);
        rig.Card.Opacity.ShouldBeInRange(0.2, 0.9);
        rig.Frames(5);
        rig.Card.Opacity.ShouldBe(1, 0.001);

        // Hiding keeps the room while the card fades, then closes it at once.
        rig.View.HideError();
        rig.Layout();
        rig.Frames(3);
        rig.Card.Opacity.ShouldBeInRange(0.2, 0.9);
        rig.Host.CurrentHeight.ShouldBe(target, 0.01);
        rig.Frames(6);
        rig.Card.IsVisible.ShouldBeFalse();
        rig.Host.CurrentHeight.ShouldBe(0, 0.01);
        rig.Host.IsMoving.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void At_Off_The_Card_Comes_And_Goes_At_Once()
    {
        using var rig = new Rig();
        ShowError(rig.View);
        rig.Layout();
        rig.Card.Opacity.ShouldBe(1);
        rig.Host.CurrentHeight.ShouldBe(rig.Host.TargetHeight, 0.01);
        rig.Host.CurrentHeight.ShouldBeGreaterThan(50);
        rig.View.HideError();
        rig.Layout();
        rig.Card.IsVisible.ShouldBeFalse();
        rig.Host.CurrentHeight.ShouldBe(0);
    }

    [AvaloniaFact]
    public void ShowError_Again_Changes_The_Texts_In_Place()
    {
        using var rig = new Rig();
        ShowError(rig.View);
        rig.Layout();
        rig.View.ShowError("No demo answer", "This demo only knows the three sample prompts.", "Try a sample prompt", false);
        rig.Layout();
        rig.View.ErrorTitle.ShouldBe("No demo answer");
        AutomationProperties.GetName(rig.Card).ShouldBe("No demo answer. This demo only knows the three sample prompts.");
        rig.Card.GetVisualDescendants().OfType<TextBlock>().ShouldContain(t => t.Text == "Try a sample prompt");
        rig.View.Classes.ShouldNotContain(ArenaAnswerView.RetryPseudoClass);
    }

    [AvaloniaFact]
    public void Hiding_The_Card_Under_The_Keyboard_Keeps_Focus_In_The_Answer()
    {
        using var rig = new Rig();
        ShowError(rig.View);
        rig.Layout();
        rig.View.ErrorAction!.Focus(NavigationMethod.Tab);
        rig.Layout();
        rig.View.HideError();
        rig.Layout();
        rig.View.IsFocused.ShouldBeTrue();
        rig.View.FocusRing!.IsVisible.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void The_Card_Follows_Into_View_While_Following()
    {
        using var rig = new Rig(ArcMotionLevel.Off);
        rig.View.Append(Lines + Lines);
        rig.Layout();
        for (var i = 0; i < 100 && rig.Bottom < 50; i++)
        {
            Dispatcher.UIThread.RunJobs();
            rig.View.Body!.View?.ApplyLatest();
            rig.Layout();
            Thread.Sleep(5);
        }
        var before = rig.Bottom;
        ShowError(rig.View);
        rig.Layout();
        rig.Bottom.ShouldBeGreaterThan(before + 50);
        rig.Offset.ShouldBe(rig.Bottom, 0.5);
    }

    #endregion

    #region A long answer

    /// <summary>Splits text into batches of <paramref name="words"/> whole words, keeping every space and line break.</summary>
    internal static List<string> Batches(string text, int words)
    {
        var batches = new List<string>();
        var start = 0;
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var atEnd = i == text.Length - 1;
            if (char.IsWhiteSpace(text[i]) && (atEnd || !char.IsWhiteSpace(text[i + 1])))
            {
                count++;
                if (count == words)
                {
                    batches.Add(text[start..(i + 1)]);
                    start = i + 1;
                    count = 0;
                }
            }
        }
        if (start < text.Length)
        {
            batches.Add(text[start..]);
        }
        return batches;
    }

    [AvaloniaFact]
    public async Task A_Long_Answer_With_A_List_And_Code_Streams_In_Five_Word_Batches_And_Ends_At_The_Bottom()
    {
        using var rig = new Rig(ArcMotionLevel.Full);
        rig.View.IsStreaming = true;
        var batches = Batches(LongAnswer, 5);
        string.Concat(batches).ShouldBe(LongAnswer);
        batches.Count.ShouldBeGreaterThan(20);
        foreach (var batch in batches)
        {
            rig.View.Append(batch);
            rig.Frames(3);
        }
        rig.View.Complete();
        rig.View.IsStreaming = false;
        await rig.Parsed();
        rig.Frames(60);
        rig.View.Text.ShouldBe(LongAnswer);
        rig.View.IsFollowing.ShouldBeTrue();
        rig.Bottom.ShouldBeGreaterThan(200);
        rig.Offset.ShouldBe(rig.Bottom, 0.5);

        // The code block stays inside the pane (its long line scrolls sideways in the block, not the pane).
        var code = rig.View.Body!.GetVisualDescendants().OfType<CodeBlockView>().ShouldHaveSingleItem();
        code.Bounds.Width.ShouldBeLessThanOrEqualTo(391 - 40 + 0.5);
        rig.Scroll.Extent.Width.ShouldBeLessThanOrEqualTo(391 + 0.5);
        rig.Scroll.Offset.X.ShouldBe(0);
        // A sideways wheel over the block scrolls the block: the answer neither moves nor stops following.
        rig.Scroll.ScrollToOffset(new Vector(0, rig.Bottom));
        rig.Frames(60);
        var offset = rig.Offset;
        var center = code.TranslatePoint(new Point(code.Bounds.Width / 2, code.Bounds.Height / 2), rig.Window)!.Value;
        center.Y.ShouldBeInRange(0, 272);
        rig.Window.MouseWheel(center, new Vector(-1, 0));
        rig.Layout();
        rig.Offset.ShouldBe(offset, 0.01);
        rig.View.IsFollowing.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Batches_Keep_Whole_Words_And_Every_Character()
    {
        var batches = Batches("One two three four five six\n\n- seven eight", 5);
        batches.ShouldBe(["One two three four five ", "six\n\n- seven eight"]);
    }

    #endregion
}
