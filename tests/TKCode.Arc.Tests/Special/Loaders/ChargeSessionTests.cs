using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;
using TKCode.Arc.Motion;
using TKCode.Arc.Tests.Special.Ai;
using Xunit;

namespace TKCode.Arc.Tests.Special.Loaders;

/// <summary>The charge session card in a window: template, loop, buttons, keys, drags, breakpoints and reduced motion.</summary>
public class ChargeSessionTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (Window Window, ChargeSession Card) Show(ChargeSession card, ArcMotionLevel level = ArcMotionLevel.Full,
        double width = 928)
    {
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (window, card);
    }

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static TextBlock Part(ChargeSession card, string name) => TestHelpers.Descendant<TextBlock>(card, name);

    [AvaloniaFact]
    public void Defaults_show_the_invented_rapid_point_charging_from_28_percent()
    {
        var (window, card) = Show(new ChargeSession(), ArcMotionLevel.Off);
        try
        {
            card.State.ShouldBe(ChargeSessionState.Charging);
            Part(card, "PART_Title").Text.ShouldBe("Bay 6 rapid point");
            Part(card, "PART_ChargeText").Text.ShouldBe("28%");
            Part(card, "PART_StatusText").Text.ShouldBe("Charging");
            Part(card, "PART_CurveTitle").Text.ShouldBe("Charge curve");
            Part(card, "PART_PowerAxis").Text.ShouldBe("180 kW");
            Part(card, "PART_TimeName").Text.ShouldBe("Time to 80%");
            Part(card, "PART_CostValue").Text.ShouldBe("$0.00");
            Part(card, "PART_EnergyValue").Text.ShouldBe("0.0 kWh");
            var handle = TestHelpers.Descendant<ChargeSessionLimitHandle>(card);
            handle.Value.ShouldBe(80);
            handle.Minimum.ShouldBe(50);
            handle.Text.ShouldBe("80%");
            AutomationProperties.GetName(handle).ShouldBe("Charge limit");
            AutomationProperties.GetHelpText(handle).ShouldStartWith("Charge limit 80%, about ");
            ControlAutomationPeer.CreatePeerForElement(handle).GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
            var action = TestHelpers.Descendant<ChargeSessionActionButton>(card);
            AutomationProperties.GetName(action).ShouldBe("Stop charging");
            AutomationProperties.GetName(card).ShouldBe("Bay 6 rapid point");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_loop_ramps_up_and_fills_the_pack()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, card) = Show(new ChargeSession());
        try
        {
            card.Loop.IsActive.ShouldBeTrue();
            clock.Advance(TimeSpan.FromSeconds(3), Frame);
            card.Model.Availability.ShouldBeGreaterThan(0.99);
            card.Charge.ShouldBeGreaterThan(33);
            card.Power.ShouldBe(180);
            card.Flow.Count.ShouldBeGreaterThan(4);
            Part(card, "PART_PowerValue").Text.ShouldBe("180 kW");
            Part(card, "PART_CurveReading").Text.ShouldBe($"180 kW at {Math.Floor(card.Model.Charge)}%");
            card.MinutesToLimit.ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_drains_the_flow_and_the_loop_idles()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, card) = Show(new ChargeSession());
        var events = new List<ChargeSessionStateChangedEventArgs>();
        card.StateChanged += (_, e) => events.Add(e);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            card.Stop();
            card.State.ShouldBe(ChargeSessionState.Stopped);
            events.Count.ShouldBe(1);
            events[0].PreviousState.ShouldBe(ChargeSessionState.Charging);
            events[0].Detail.Limit.ShouldBe(80);
            Part(card, "PART_StatusText").Text.ShouldBe("Stopped");
            Part(card, "PART_TimeValue").Text.ShouldBe("Paused");
            card.Announcer!.Announcement.ShouldStartWith("Stopped at ");
            AutomationProperties.GetName(TestHelpers.Descendant<ChargeSessionActionButton>(card)).ShouldBe("Resume charging");
            clock.Advance(TimeSpan.FromSeconds(8), Frame);
            card.Power.ShouldBe(0);
            card.Flow.IsIdle.ShouldBeTrue();
            card.Loop.IsActive.ShouldBeFalse();
            card.Resume();
            card.State.ShouldBe(ChargeSessionState.Charging);
            card.Loop.IsActive.ShouldBeTrue();
            card.Announcer!.Announcement.ShouldBe("Charging again, up to 80%.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_button_steps_through_stop_resume_and_new_session()
    {
        var (window, card) = Show(new ChargeSession { StartCharge = 60 }, ArcMotionLevel.Off);
        try
        {
            var action = TestHelpers.Descendant<ChargeSessionActionButton>(card);
            action.Focus();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.State.ShouldBe(ChargeSessionState.Stopped);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            card.State.ShouldBe(ChargeSessionState.Charging);
            card.Limit = 50;
            card.State.ShouldBe(ChargeSessionState.Complete);
            Part(card, "PART_StatusText").Text.ShouldBe("Limit reached");
            Part(card, "PART_TimeValue").Text.ShouldBe("Done");
            AutomationProperties.GetName(action).ShouldBe("Start new session");
            card.Limit = 70;
            card.State.ShouldBe(ChargeSessionState.Charging);
            card.Limit = 50;
            var events = new List<ChargeSessionStateChangedEventArgs>();
            card.StateChanged += (_, e) => events.Add(e);
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Pump();
            events.Single().IsNewSession.ShouldBeTrue();
            card.Charge.ShouldBe(60);
            card.EnergyAdded.ShouldBe(0);
            // The limit is kept, so a new session that starts at or above it completes at once.
            card.State.ShouldBe(ChargeSessionState.Complete);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keys_move_the_limit_and_the_change_settles_after_350_ms()
    {
        var time = new AgentStatusTestClock();
        var (window, card) = Show(new ChargeSession { TimeProvider = time }, ArcMotionLevel.Off);
        var settled = new List<double>();
        card.LimitChanged += (_, e) => settled.Add(e.Limit);
        try
        {
            var handle = TestHelpers.Descendant<ChargeSessionLimitHandle>(card);
            handle.Focus(NavigationMethod.Tab);
            Pump();
            TestHelpers.IsFocusRingVisible(handle).ShouldBeTrue();
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            card.Limit.ShouldBe(81);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.Shift);
            card.Limit.ShouldBe(91);
            window.KeyPressQwerty(PhysicalKey.PageDown, RawInputModifiers.None);
            card.Limit.ShouldBe(81);
            window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
            card.Limit.ShouldBe(80);
            window.KeyPressQwerty(PhysicalKey.PageUp, RawInputModifiers.None);
            card.Limit.ShouldBe(90);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            card.Limit.ShouldBe(100);
            window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
            card.Limit.ShouldBe(50);
            handle.Text.ShouldBe("50%");
            settled.ShouldBeEmpty();
            time.Advance(349);
            Pump();
            settled.ShouldBeEmpty();
            time.Advance(2);
            Pump();
            settled.ShouldBe(new[] { 50.0 });
            card.Announcer!.Announcement.ShouldStartWith("Limit set to 50%.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_on_the_pack_sets_the_limit_one_to_one()
    {
        var (window, card) = Show(new ChargeSession(), ArcMotionLevel.Off);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var hero = TestHelpers.Descendant<ChargeSessionHero>(card);
            var handle = TestHelpers.Descendant<ChargeSessionLimitHandle>(card);
            var origin = hero.TranslatePoint(default, window)!.Value;
            var k = hero.Bounds.Width / 1000;
            Point At(double limit) => new(origin.X + k * ChargeSessionScene.LimitX(limit), origin.Y + hero.SceneRect.Y + 100 * k);
            window.MouseMove(At(90), RawInputModifiers.None);
            window.MouseDown(At(90), MouseButton.Left, RawInputModifiers.None);
            card.Limit.ShouldBe(90);
            handle.IsHeld.ShouldBeTrue();
            handle.Classes.Contains(":held").ShouldBeTrue();
            window.MouseMove(At(66.4), RawInputModifiers.LeftMouseButton);
            card.Limit.ShouldBe(66);
            // Past the ends it clamps.
            window.MouseMove(new Point(origin.X - 50, At(0).Y), RawInputModifiers.LeftMouseButton);
            card.Limit.ShouldBe(50);
            window.MouseUp(new Point(origin.X - 50, At(0).Y), MouseButton.Left, RawInputModifiers.None);
            handle.IsHeld.ShouldBeFalse();
            // Pressing leaves focus where it was.
            handle.IsFocused.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_on_the_curve_maps_the_plot_width_to_0_100()
    {
        var (window, card) = Show(new ChargeSession(), ArcMotionLevel.Off);
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            var plot = TestHelpers.Descendant<ChargeSessionPlot>(card);
            var origin = plot.TranslatePoint(default, window)!.Value;
            var p = new Point(origin.X + plot.Bounds.Width * 0.6, origin.Y + 20);
            window.MouseDown(p, MouseButton.Left, RawInputModifiers.None);
            card.Limit.ShouldBe(60);
            window.MouseMove(p + new Vector(plot.Bounds.Width * 0.15, 0), RawInputModifiers.LeftMouseButton);
            card.Limit.ShouldBe(75);
            window.MouseUp(p, MouseButton.Left, RawInputModifiers.None);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Breakpoints_follow_the_card_width()
    {
        var (window, card) = Show(new ChargeSession(), ArcMotionLevel.Off, width: 928);
        try
        {
            card.Classes.Contains(":stacked").ShouldBeFalse();
            TestHelpers.Descendant<ChargeSessionLower>(card).IsStacked.ShouldBeFalse();
            TestHelpers.Descendant<ChargeSessionPlot>(card).Aspect.ShouldBe(0.40);
            window.Width = 560;
            Pump();
            card.Classes.Contains(":stacked").ShouldBeTrue();
            card.Classes.Contains(":compact").ShouldBeFalse();
            TestHelpers.Descendant<ChargeSessionLower>(card).IsStacked.ShouldBeTrue();
            TestHelpers.Descendant<ChargeSessionPlot>(card).Aspect.ShouldBe(0.50);
            Part(card, "PART_ChargeText").FontSize.ShouldBe(52);
            window.Width = 400;
            Pump();
            card.Classes.Contains(":compact").ShouldBeTrue();
            Part(card, "PART_ChargeText").FontSize.ShouldBe(36);
            TestHelpers.Descendant<ChargeSessionFooter>(card).IsButtonFull.ShouldBeTrue();
            TestHelpers.Descendant<ChargeSessionHero>(card).TopSpace.ShouldBe(32);
            // The scene keeps its 0.24 aspect at every width.
            var scene = TestHelpers.Descendant<ChargeSessionScene>(card);
            (scene.Bounds.Height / scene.Bounds.Width).ShouldBe(0.24, 0.001);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_motion_holds_the_session_still_with_even_packets()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, card) = Show(new ChargeSession(), ArcMotionLevel.Reduced);
        try
        {
            var distances = Enumerable.Range(0, ChargeSessionFlow.PoolSize).Select(card.Flow.DistanceOf).ToArray();
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            card.Charge.ShouldBe(28);
            card.Power.ShouldBe(180);
            card.Flow.IsStatic.ShouldBeTrue();
            card.Flow.Count.ShouldBeGreaterThan(3);
            // The packets do not travel, but they breathe together so the card still reads as charging.
            Enumerable.Range(0, ChargeSessionFlow.PoolSize).Select(card.Flow.DistanceOf).ToArray().ShouldBe(distances);
            card.Loop.IsRunning.ShouldBeTrue();
            var opacities = new List<double>();
            for (var i = 0; i < 6; i++)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300), Frame);
                opacities.Add(card.Flow.StaticOpacity);
            }
            opacities.Min().ShouldBeLessThan(0.6);
            opacities.Max().ShouldBeGreaterThan(0.9);
            card.Charge.ShouldBe(28);
            card.Stop();
            card.Power.ShouldBe(0);
            card.Flow.Count.ShouldBe(0);
            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            card.Loop.IsRunning.ShouldBeFalse();
            card.Resume();
            card.Power.ShouldBe(180);
            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            card.Loop.IsRunning.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Motion_off_holds_the_session_and_its_packets_still()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, card) = Show(new ChargeSession(), ArcMotionLevel.Off);
        try
        {
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            card.Charge.ShouldBe(28);
            card.Flow.IsStatic.ShouldBeTrue();
            card.Loop.IsRunning.ShouldBeFalse();
            card.Flow.StaticOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paused_freezes_the_session_and_speed_zero_keeps_the_flow()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (window, card) = Show(new ChargeSession());
        try
        {
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            card.IsPaused = true;
            var charge = card.Model.Charge;
            var landed = card.Flow.Landed;
            clock.Advance(TimeSpan.FromSeconds(1), Frame);
            card.Model.Charge.ShouldBe(charge);
            card.Loop.IsActive.ShouldBeFalse();
            card.IsPaused = false;
            card.Speed = 0;
            clock.Advance(TimeSpan.FromSeconds(2), Frame);
            card.Model.Charge.ShouldBe(charge);
            card.Flow.Landed.ShouldBeGreaterThan(landed);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_scene_draws_in_six_paint_order_layers()
    {
        var (window, card) = Show(new ChargeSession(), ArcMotionLevel.Off);
        try
        {
            var scene = TestHelpers.Descendant<ChargeSessionScene>(card);
            var layers = scene.GetVisualChildren().OfType<ChargeSessionSceneLayer>().ToList();
            layers.Select(l => l.Kind).ShouldBe(Enum.GetValues<ChargeSessionSceneLayerKind>());
            layers.ShouldAllBe(l => !l.IsHitTestVisible && l.Bounds.Size == scene.Bounds.Size);
            // Updating with nothing changed is a no-op; a step that moves the charge keeps the layers in sync.
            scene.Update();
            card.Advance(0.5);
            Pump();
            card.Model.Charge.ShouldBeGreaterThan(28);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Samples_apply_with_their_currency_and_locale()
    {
        var card = new ChargeSession();
        ChargeSessionSamples.FleetVan.ApplyTo(card);
        var (window, _) = Show(card, ArcMotionLevel.Off);
        try
        {
            Part(card, "PART_Title").Text.ShouldBe("Fleet van 14");
            Part(card, "PART_ChargeText").Text.ShouldBe("15%");
            Part(card, "PART_CostValue").Text.ShouldBe("0,00 €");
            Part(card, "PART_EnergyValue").Text.ShouldBe("0,0 kWh");
            card.Limit.ShouldBe(90);
            card.Advance(5);
            Pump();
            card.Cost.ShouldBeGreaterThan(0m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_labels_and_accent_reach_the_parts()
    {
        var accent = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Colors.SeaGreen);
        var card = new ChargeSession
        {
            AccentBrush = accent,
            Labels = new ChargeSessionLabels { Stop = "Halt", Charging = "Filling", Hint = "Move the cap." },
        };
        var (window, _) = Show(card, ArcMotionLevel.Off);
        try
        {
            Part(card, "PART_StatusText").Text.ShouldBe("Filling");
            Part(card, "PART_Hint").Text.ShouldBe("Move the cap.");
            AutomationProperties.GetName(TestHelpers.Descendant<ChargeSessionActionButton>(card)).ShouldBe("Halt");
            TestHelpers.Descendant<ChargeSessionScene>(card).AccentInk.ShouldBe(accent);
            TestHelpers.Descendant<ChargeSessionPlot>(card).AccentInk.ShouldBe(accent);
            TestHelpers.Descendant<ArcGlyph>(card).ShouldNotBeNull();
        }
        finally
        {
            window.Close();
        }
    }
}
