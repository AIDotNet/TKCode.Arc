using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>The orbit logos: the rings' geometry and motion (pure), then selection, categories, keys, connecting and motion levels.</summary>
public class OrbitLogosTests
{
    private const double Tau = Math.PI * 2;

    private static OrbitLogosSimulation Rings(int count = 14, double detail = 0)
    {
        var sim = new OrbitLogosSimulation { DetailAngle = detail };
        sim.Build(count);
        return sim;
    }

    private static void Run(OrbitLogosSimulation sim, double seconds)
    {
        for (var t = 0.0; t < seconds; t += 1.0 / 60)
        {
            sim.Step(1.0 / 60);
        }
    }

    private static double Gap(double a, double b) => Math.Abs(OrbitLogosSimulation.Wrap(a - b));

    #region Simulation

    [Fact]
    public void Logos_are_split_two_fifths_inner_and_spread_evenly()
    {
        var sim = Rings();
        sim.RingCount(0).ShouldBe(6);
        sim.RingCount(1).ShouldBe(8);
        Enumerable.Range(0, 14).Count(i => sim.RingOf(i) == 0).ShouldBe(6);
        // Inner ring: six logos 60° apart.
        var inner = Enumerable.Range(0, 14).Where(i => sim.RingOf(i) == 0).Select(sim.Slot).ToArray();
        for (var i = 1; i < inner.Length; i++)
        {
            (inner[i] - inner[i - 1]).ShouldBe(Tau / 6, 1e-9);
        }
        // Positions sit on their ring's radius.
        for (var i = 0; i < 14; i++)
        {
            var (x, y) = sim.Offset(i, 100, 200);
            Math.Sqrt(x * x + y * y).ShouldBe(sim.RingOf(i) == 0 ? 100 : 200, 1e-9);
        }
    }

    [Fact]
    public void Few_logos_go_on_the_outer_ring_only()
    {
        var sim = Rings(2);
        sim.RingCount(0).ShouldBe(0);
        sim.RingCount(1).ShouldBe(2);
        Rings(0).Count.ShouldBe(0);
    }

    [Fact]
    public void Rings_turn_at_their_periods_in_opposite_directions()
    {
        var sim = Rings();
        sim.Step(1.0 / 20);
        for (var i = 0; i < 59 * 3; i++)
        {
            sim.Step(1.0 / 60);
        }
        var seconds = 1.0 / 20 + 59 * 3 / 60.0;
        sim.RingAngle(0).ShouldBe(Tau / OrbitLogosSimulation.InnerPeriod * seconds, 1e-6);
        sim.RingAngle(1).ShouldBe(-Tau / OrbitLogosSimulation.OuterPeriod * seconds, 1e-6);
    }

    [Fact]
    public void Speed_scales_the_turn_and_zero_holds_it()
    {
        var fast = Rings();
        fast.Speed = 2;
        Run(fast, 2);
        var normal = Rings();
        Run(normal, 2);
        fast.RingAngle(0).ShouldBe(normal.RingAngle(0) * 2, 1e-6);

        var still = Rings();
        still.Speed = 0;
        Run(still, 3);
        still.RingAngle(0).ShouldBeLessThan(0.01);
        still.Step(1.0 / 60).ShouldBeFalse();
    }

    [Fact]
    public void Holding_eases_the_turn_to_rest_and_releasing_brings_it_back()
    {
        var sim = Rings();
        Run(sim, 1);
        sim.Held = true;
        Run(sim, 0.2);
        sim.Spin.ShouldBeInRange(0.01, 0.95);
        Run(sim, 3);
        sim.Spin.ShouldBe(0);
        var rest = sim.RingAngle(0);
        sim.Step(1.0 / 60).ShouldBeFalse();
        sim.RingAngle(0).ShouldBe(rest, 1e-12);

        sim.Held = false;
        Run(sim, 4);
        sim.Spin.ShouldBe(1, 0.001);
        sim.RingAngle(0).ShouldBeGreaterThan(rest);
    }

    [Fact]
    public void Paused_orbit_does_not_turn()
    {
        var sim = Rings();
        sim.Paused = true;
        // It eases to rest from its turning speed, then stays.
        Run(sim, 3);
        var rest = sim.RingAngle(0);
        Run(sim, 2);
        sim.RingAngle(0).ShouldBe(rest, 1e-9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(Math.PI / 2)]
    public void A_chosen_logo_parks_at_the_detail_angle_the_short_way(double detail)
    {
        var sim = Rings(14, detail);
        Run(sim, 5);
        for (var index = 0; index < 14; index++)
        {
            var from = sim.RingAngle(sim.RingOf(index));
            sim.Retarget(index, null);
            var ring = sim.RingOf(index);
            Math.Abs(sim.TargetAngle(ring) - from).ShouldBeLessThanOrEqualTo(Math.PI + 1e-9);
            Run(sim, 4);
            Gap(sim.Angle(index), detail).ShouldBeLessThan(0.001);
            sim.Step(1.0 / 60).ShouldBeFalse();
            sim.Retarget(-1, null);
        }
    }

    [Fact]
    public void A_chosen_logo_settles_with_a_touch_of_overshoot()
    {
        var sim = Rings();
        var index = Enumerable.Range(0, 14).First(i => sim.RingOf(i) == 1 && i > 3);
        sim.Retarget(index, null);
        var ring = sim.RingOf(index);
        var start = sim.RingAngle(ring);
        var target = sim.TargetAngle(ring);
        var sign = Math.Sign(target - start);
        var worst = 0.0;
        for (var i = 0; i < 300; i++)
        {
            sim.Step(1.0 / 60);
            worst = Math.Max(worst, sign * (sim.RingAngle(ring) - target));
        }
        worst.ShouldBeGreaterThan(0);
        worst.ShouldBeLessThan(0.08 * Math.Abs(target - start));
    }

    [Fact]
    public void A_category_turns_its_logos_toward_the_detail_side_without_overshoot()
    {
        var sim = Rings();
        var matches = Enumerable.Range(0, 14).Select(i => i % 5 is 1 or 2).ToArray();
        sim.Retarget(-1, matches);
        var worst = new double[2];
        var start = new[] { sim.RingAngle(0), sim.RingAngle(1) };
        for (var i = 0; i < 400; i++)
        {
            sim.Step(1.0 / 60);
            for (var ring = 0; ring < 2; ring++)
            {
                if (sim.HasTarget(ring))
                {
                    var sign = Math.Sign(sim.TargetAngle(ring) - start[ring]);
                    worst[ring] = Math.Max(worst[ring], sign * (sim.RingAngle(ring) - sim.TargetAngle(ring)));
                }
            }
        }
        worst[0].ShouldBeLessThan(1e-3);
        worst[1].ShouldBeLessThan(1e-3);
        // The mean direction of the matching logos on each turned ring is at the detail angle.
        (sim.HasTarget(0) || sim.HasTarget(1)).ShouldBeTrue();
        for (var ring = 0; ring < 2; ring++)
        {
            if (!sim.HasTarget(ring))
            {
                continue;
            }
            double sx = 0, sy = 0;
            for (var i = 0; i < 14; i++)
            {
                if (sim.RingOf(i) == ring && matches[i])
                {
                    sx += Math.Cos(sim.Angle(i));
                    sy += Math.Sin(sim.Angle(i));
                }
            }
            Gap(Math.Atan2(sy, sx), 0).ShouldBeLessThan(0.01);
        }
    }

    [Fact]
    public void The_inner_ring_steps_aside_when_an_outer_logo_parks()
    {
        var sim = Rings(14, Math.PI / 2);
        var outer = Enumerable.Range(0, 14).First(i => sim.RingOf(i) == 1);
        sim.Retarget(outer, null);
        Run(sim, 5);
        // No inner logo is on the connector: the nearest is half a gap (30°) away.
        var nearest = Enumerable.Range(0, 14).Where(i => sim.RingOf(i) == 0).Min(i => Gap(sim.Angle(i), Math.PI / 2));
        nearest.ShouldBe(Math.PI / 6, 0.01);
        // An inner logo parked instead leaves the outer ring alone (free to hold still).
        sim.Retarget(-1, null);
        var inner = Enumerable.Range(0, 14).First(i => sim.RingOf(i) == 0);
        sim.Retarget(inner, null);
        sim.HasTarget(1).ShouldBeFalse();
        sim.HasTarget(0).ShouldBeTrue();
    }

    [Fact]
    public void Without_motion_targets_are_reached_at_once_and_nothing_turns()
    {
        var sim = Rings();
        sim.CanMove = false;
        var index = 5;
        sim.Retarget(index, null);
        Gap(sim.Angle(index), 0).ShouldBeLessThan(1e-9);
        sim.Step(1.0 / 60).ShouldBeFalse();
        sim.Spin.ShouldBe(0);
    }

    [Fact]
    public void Wrap_keeps_angles_in_half_a_turn()
    {
        OrbitLogosSimulation.Wrap(Tau + 0.5).ShouldBe(0.5, 1e-9);
        OrbitLogosSimulation.Wrap(-Tau - 0.5).ShouldBe(-0.5, 1e-9);
        OrbitLogosSimulation.Wrap(3 * Math.PI / 2).ShouldBe(-Math.PI / 2, 1e-9);
    }

    #endregion

    #region Control

    private static OrbitLogos Orbit(out OrbitLogo[] logos)
    {
        logos =
        [
            new OrbitLogo { Id = "a", Name = "Alpha", Category = "Pay", Description = "First one.", Icon = "M12 2l10 18H2z", IsConnected = true, Href = "https://example.com" },
            new OrbitLogo { Name = "Bravo", Category = "Pay", Icon = "M3 3h18v18H3z" },
            new OrbitLogo { Name = "Charlie", Category = "Dev", Icon = "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18z" },
            new OrbitLogo { Name = "Delta", Category = "Dev", Icon = "M5 4l15 8-15 8z" },
            new OrbitLogo { Name = "Echo", Category = "Chat", Icon = "M4 4h16v12H4z" }
        ];
        var orbit = new OrbitLogos();
        orbit.Logos.AddRange(logos);
        return orbit;
    }

    private static Window ShowOrbit(OrbitLogos orbit, ArcMotionLevel level = ArcMotionLevel.Off, double width = 900)
    {
        var window = TestHelpers.Show(orbit, level);
        window.Width = width;
        window.Height = 640;
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void Click(OrbitLogosChip chip) => chip.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    [AvaloniaFact]
    public void Builds_a_chip_per_logo_with_a_name_that_includes_category_and_state()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit);
        orbit.Chips.Count.ShouldBe(5);
        Avalonia.Automation.AutomationProperties.GetName(orbit.Chips[0]).ShouldBe("Alpha, Pay, connected");
        Avalonia.Automation.AutomationProperties.GetName(orbit.Chips[1]).ShouldBe("Bravo, Pay, not connected");
        orbit.CategoryChips.Select(c => c.Content).ShouldBe(["All", "Pay", "Dev", "Chat"]);
        orbit.CategoryChips[0].IsChecked.ShouldBe(true);
    }

    [AvaloniaFact]
    public void The_orbit_is_a_single_tab_stop()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit);
        orbit.Chips.Count(c => c.IsTabStop).ShouldBe(1);
        orbit.Chips[0].IsTabStop.ShouldBeTrue();
        orbit.Chips[3].Focus();
        Dispatcher.UIThread.RunJobs();
        orbit.Chips.Count(c => c.IsTabStop).ShouldBe(1);
        orbit.Chips[3].IsTabStop.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Clicking_a_logo_opens_its_card_and_clicking_again_closes_it()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit);
        var events = new List<(string? Name, int Index)>();
        orbit.SelectionChanged += (_, e) => events.Add((e.Logo?.Name, e.Index));

        Click(orbit.Chips[2]);
        Dispatcher.UIThread.RunJobs();
        orbit.SelectedIndex.ShouldBe(2);
        orbit.SelectedId.ShouldBe("Charlie");
        orbit.Chips[2].IsChecked.ShouldBe(true);
        orbit.Card!.IsVisible.ShouldBeTrue();
        orbit.Card.Clip.ShouldBeNull();
        orbit.Stage!.Simulation.HasTarget(orbit.Stage.Simulation.RingOf(2)).ShouldBeTrue();

        Click(orbit.Chips[2]);
        Dispatcher.UIThread.RunJobs();
        orbit.SelectedIndex.ShouldBe(-1);
        orbit.SelectedId.ShouldBeNull();
        orbit.Card.IsVisible.ShouldBeFalse();
        events.ShouldBe([("Charlie", 2), (null, -1)]);
    }

    [AvaloniaFact]
    public void The_card_shows_the_description_status_and_actions()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit);
        Click(orbit.Chips[0]);
        Dispatcher.UIThread.RunJobs();
        var texts = orbit.Card!.GetVisualDescendantsOfType<TextBlock>().Select(t => t.Text).ToArray();
        texts.ShouldContain("Alpha");
        texts.ShouldContain("First one.");
        texts.ShouldContain("Connected");
        var buttons = orbit.Card!.GetVisualDescendantsOfType<Avalonia.Controls.Button>()
            .Select(b => Avalonia.Automation.AutomationProperties.GetName(b)).ToArray();
        buttons.ShouldContain("Alpha docs");
        buttons.ShouldContain("Previous integration");
        buttons.ShouldContain("Next integration");
    }

    [AvaloniaFact]
    public void Selected_id_opens_a_logo_by_id_or_name_and_null_closes_it()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit);
        orbit.SelectedId = "a";
        orbit.SelectedIndex.ShouldBe(0);
        orbit.SelectedId = "Delta";
        orbit.SelectedIndex.ShouldBe(3);
        orbit.SelectedId = null;
        orbit.SelectedIndex.ShouldBe(-1);
        orbit.Card!.IsVisible.ShouldBeFalse();
        orbit.SelectedId = "nope";
        orbit.SelectedIndex.ShouldBe(-1);
    }

    [AvaloniaFact]
    public void Default_selected_opens_a_logo_on_first_display()
    {
        var orbit = Orbit(out _);
        orbit.DefaultSelected = "Echo";
        ShowOrbit(orbit);
        orbit.SelectedIndex.ShouldBe(4);
        orbit.Card!.IsVisible.ShouldBeTrue();
        orbit.SelectedId.ShouldBe("Echo");
    }

    [AvaloniaFact]
    public void A_category_dims_the_others_and_closes_a_card_outside_it()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit);
        Click(orbit.Chips[4]);
        orbit.SelectedIndex.ShouldBe(4);

        orbit.CategoryChips[2].RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        orbit.CategoryIndex.ShouldBe(2);
        orbit.CategoryChips[2].IsChecked.ShouldBe(true);
        orbit.CategoryChips[0].IsChecked.ShouldBe(false);
        orbit.SelectedIndex.ShouldBe(-1);
        orbit.Chips.Select(c => c.Classes.Contains("dim")).ShouldBe([true, true, false, false, true]);
        orbit.Stage!.Simulation.HasTarget(0).ShouldBeTrue("a category turns its logos toward the detail side");

        orbit.CategoryChips[0].RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        orbit.Chips.Any(c => c.Classes.Contains("dim")).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Arrow_keys_step_through_the_current_category_and_the_card_follows()
    {
        var orbit = Orbit(out _);
        var window = ShowOrbit(orbit);
        orbit.CategoryChips[2].RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        orbit.Chips[2].Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        orbit.Chips[3].IsFocused.ShouldBeTrue();
        orbit.SelectedIndex.ShouldBe(-1, "arrows only move focus while no card is open");

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        orbit.Chips[2].IsFocused.ShouldBeTrue("wraps inside the category");

        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        orbit.Chips[2].IsFocused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        orbit.Chips[3].IsFocused.ShouldBeTrue();

        Click(orbit.Chips[3]);
        orbit.SelectedIndex.ShouldBe(3);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        orbit.SelectedIndex.ShouldBe(2);
        orbit.Chips[2].IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Escape_closes_the_card_and_returns_focus_to_its_logo()
    {
        var orbit = Orbit(out _);
        var window = ShowOrbit(orbit);
        Click(orbit.Chips[1]);
        orbit.Chips[1].Focus();
        orbit.Card!.IsVisible.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        orbit.SelectedIndex.ShouldBe(-1);
        orbit.Chips[1].IsFocused.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Previous_and_next_in_the_card_step_and_wrap()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit);
        Click(orbit.Chips[0]);
        Dispatcher.UIThread.RunJobs();
        var previous = orbit.Card!.GetVisualDescendantsOfType<Avalonia.Controls.Button>()
            .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Previous integration");
        previous.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        orbit.SelectedIndex.ShouldBe(4);
        Dispatcher.UIThread.RunJobs();
        var next = orbit.Card!.GetVisualDescendantsOfType<Avalonia.Controls.Button>()
            .First(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Next integration");
        next.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        orbit.SelectedIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public void The_idle_panel_counts_integrations_and_connections()
    {
        var orbit = Orbit(out var logos);
        ShowOrbit(orbit);
        var texts = () => TestHelpers.Descendant<TextBlock>(orbit, OrbitLogos.IdleSummaryPart).Text;
        texts().ShouldBe("5 integrations, 1 connected");
        TestHelpers.Descendant<TextBlock>(orbit, OrbitLogos.IdleTotalPart).Text.ShouldBe("1 of 5 connected in total");
        TestHelpers.Descendant<TextBlock>(orbit, OrbitLogos.IdleTitlePart).Text.ShouldBe("Integrations");

        orbit.CategoryChips[1].RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        texts().ShouldBe("2 Pay integrations, 1 connected");
        logos[1].IsConnected = true;
        texts().ShouldBe("2 Pay integrations, 2 connected");
        TestHelpers.Descendant<TextBlock>(orbit, OrbitLogos.IdleTotalPart).Text.ShouldBe("2 of 5 connected in total");
    }

    [AvaloniaFact]
    public void Connect_holds_the_connecting_state_until_the_task_ends()
    {
        var orbit = Orbit(out var logos);
        ShowOrbit(orbit);
        var gate = new TaskCompletionSource();
        OrbitLogo? asked = null;
        orbit.ConnectHandler = logo =>
        {
            asked = logo;
            return gate.Task;
        };
        Click(orbit.Chips[1]);
        Dispatcher.UIThread.RunJobs();
        var connect = orbit.Card!.GetVisualDescendantsOfType<TKCode.Arc.Button>().First();
        connect.Content.ShouldBe("Connect");
        connect.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        asked.ShouldBe(logos[1]);
        connect.Content.ShouldBe("Connecting…");
        connect.IsLoading.ShouldBeTrue();
        logos[1].IsConnected.ShouldBeFalse();

        gate.SetResult();
        TestHelpers.PumpUntil(() => logos[1].IsConnected).ShouldBeTrue();
        orbit.Card!.GetVisualDescendantsOfType<TextBlock>().Select(t => t.Text).ShouldContain("Connected");
        orbit.Chips[1].Classes.Contains("connected").ShouldBeTrue();
    }

    [AvaloniaFact]
    public void A_failed_connection_shows_try_again_and_a_second_try_can_succeed()
    {
        var orbit = Orbit(out var logos);
        ShowOrbit(orbit);
        var attempts = 0;
        orbit.ConnectHandler = _ => ++attempts == 1 ? Task.FromException(new InvalidOperationException("no")) : Task.CompletedTask;
        Click(orbit.Chips[2]);
        Dispatcher.UIThread.RunJobs();
        var connect = orbit.Card!.GetVisualDescendantsOfType<TKCode.Arc.Button>().First();
        connect.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        TestHelpers.PumpUntil(() => (string?)connect.Content == "Try again").ShouldBeTrue();
        logos[2].IsConnected.ShouldBeFalse();

        connect.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        TestHelpers.PumpUntil(() => logos[2].IsConnected).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Connect_requested_can_supply_the_task()
    {
        var orbit = Orbit(out var logos);
        ShowOrbit(orbit);
        orbit.ConnectRequested += (_, e) => e.Completion = Task.CompletedTask;
        Click(orbit.Chips[3]);
        Dispatcher.UIThread.RunJobs();
        var connect = orbit.Card!.GetVisualDescendantsOfType<TKCode.Arc.Button>().First();
        connect.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        TestHelpers.PumpUntil(() => logos[3].IsConnected).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Wide_layouts_park_the_logo_on_the_right_and_narrow_ones_below()
    {
        var wide = Orbit(out _);
        ShowOrbit(wide, width: 900);
        wide.Stage!.IsSideBySide.ShouldBeTrue();
        Click(wide.Chips[1]);
        var simulation = wide.Stage.Simulation;
        Gap(simulation.Angle(1), 0).ShouldBeLessThan(0.01);

        var narrow = Orbit(out _);
        ShowOrbit(narrow, width: 500);
        narrow.Stage!.IsSideBySide.ShouldBeFalse();
        Click(narrow.Chips[1]);
        Gap(narrow.Stage.Simulation.Angle(1), Math.PI / 2).ShouldBeLessThan(0.01);
    }

    [AvaloniaFact]
    public void Chips_are_sized_for_the_width()
    {
        var phone = Orbit(out _);
        ShowOrbit(phone, width: 400);
        phone.Stage!.ChipSize.ShouldBe(44);

        var desktop = Orbit(out _);
        ShowOrbit(desktop, width: 900);
        desktop.Stage!.ChipSize.ShouldBeInRange(48, 56);
        desktop.Stage.Bounds.Width.ShouldBeLessThanOrEqualTo(OrbitLogosStage.MaxSize);
    }

    [AvaloniaFact]
    public void Chips_sit_on_their_rings_around_the_centre()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit, width: 900);
        var stage = orbit.Stage!;
        var sim = stage.Simulation;
        for (var i = 0; i < orbit.Chips.Count; i++)
        {
            var radius = Math.Sqrt(orbit.Chips[i].X * orbit.Chips[i].X + orbit.Chips[i].Y * orbit.Chips[i].Y);
            radius.ShouldBe(sim.RingOf(i) == 0 ? stage.InnerRadius : stage.OuterRadius, 0.01);
        }
    }

    [AvaloniaFact]
    public void Under_reduced_motion_the_orbit_does_not_turn_and_targets_are_instant()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit, ArcMotionLevel.Reduced);
        orbit.Stage!.Simulation.CanMove.ShouldBeFalse();
        Click(orbit.Chips[1]);
        Gap(orbit.Stage.Simulation.Angle(1), 0).ShouldBeLessThan(1e-6);
        orbit.Card!.IsVisible.ShouldBeTrue();
        orbit.Card.Clip.ShouldBeNull("the card appears without the morph");
    }

    [AvaloniaFact]
    public void Speed_and_pause_reach_the_rings()
    {
        var orbit = Orbit(out _);
        orbit.Speed = 0.5;
        orbit.IsPaused = true;
        ShowOrbit(orbit);
        orbit.Stage!.Simulation.Speed.ShouldBe(0.5);
        orbit.Stage.Simulation.Paused.ShouldBeTrue();
        orbit.IsPaused = false;
        orbit.Stage.Simulation.Paused.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void The_list_is_labelled_and_the_default_center_is_a_mark()
    {
        var orbit = Orbit(out _);
        orbit.Label = "Our tools";
        ShowOrbit(orbit);
        Avalonia.Automation.AutomationProperties.GetName(orbit.Stage!).ShouldBe("Our tools");
        TestHelpers.Descendant<TextBlock>(orbit, OrbitLogos.IdleTitlePart).Text.ShouldBe("Our tools");
        TestHelpers.Descendant<Avalonia.Controls.Presenters.ContentPresenter>(orbit, OrbitLogos.CenterPart).Content.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Adding_a_logo_rebuilds_the_chips()
    {
        var orbit = Orbit(out _);
        ShowOrbit(orbit);
        orbit.Logos.Add(new OrbitLogo { Name = "Foxtrot", Category = "Chat" });
        Dispatcher.UIThread.RunJobs();
        orbit.Chips.Count.ShouldBe(6);
        orbit.Stage!.Simulation.Count.ShouldBe(6);
    }

    #endregion
}
