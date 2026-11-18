using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special;

/// <summary>Code magic move: words and their matching, the move as a function of time, and the control.</summary>
public class CodeMagicMoveTests
{
    private const string V1 = """
        fn greet() {
          print("hi");
        }
        """;

    private const string V2 = """
        use std::io;

        fn greet() {
          let name = read();
          print("hi");
        }
        """;

    private const string V3 = """
        use std::io;

        fn greet(name: &str) {
          print("hi");
        }
        """;

    private static CodeMagicMoveSource Src(string code) => CodeMagicMoveSource.Parse(code);

    // --- words -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Splits_words_strings_comments_and_punctuation()
    {
        var source = Src("let a = \"x y\"; // note\n\tb.c()");
        source.Words.Select(w => w.Text).ShouldBe(["let", "a", "=", "\"x y\"", ";", "// note", "b", ".", "c", "(", ")"]);
        source.Words[6].ShouldBe(new CodeMagicMoveWord("b", 1, 2));
        source.LineCount.ShouldBe(2);
    }

    [Fact]
    public void Normalises_line_endings_tabs_and_shared_indentation()
    {
        CodeMagicMoveSource.Normalize("\r\n    a\r\n      b  \n\n").ShouldBe("a\n  b");
        Src("x\ty").Text.ShouldBe("x  y");
        Src(string.Empty).LineCount.ShouldBe(1);
    }

    // --- matching ----------------------------------------------------------------------------------------------------

    [Fact]
    public void Kept_words_continue_and_new_words_are_typed()
    {
        var from = Src(V1);
        var to = Src(V2);
        var match = CodeMagicMoveDiff.Match(from, to);
        // "use std::io;" and "let name = read();" are new.
        var typed = to.Words.Where((_, j) => match[j] < 0).Select(w => w.Text);
        typed.ShouldBe(["use", "std", ":", ":", "io", ";", "let", "name", "=", "read", "(", ")", ";"]);
        CodeMagicMoveDiff.Removed(from, match).ShouldAllBe(r => !r);
    }

    [Fact]
    public void A_line_that_moved_as_a_whole_glides_instead_of_being_retyped()
    {
        var from = Src("a();\nb();");
        var to = Src("b();\na();");
        var match = CodeMagicMoveDiff.Match(from, to);
        match.ShouldAllBe(m => m >= 0);
        // b moved up a line and a down one.
        from.Words[match[0]].ShouldBe(new CodeMagicMoveWord("b", 1, 0));
        from.Words[match[4]].ShouldBe(new CodeMagicMoveWord("a", 0, 0));
    }

    [Fact]
    public void Bands_cover_runs_of_lines_with_new_words()
    {
        var from = Src(V1);
        var to = Src(V2);
        var bands = CodeMagicMoveDiff.Bands(to, CodeMagicMoveDiff.Match(from, to));
        // Line 0 (use) and line 3 (let); the blank line 1 breaks nothing because it has no words.
        bands.ShouldBe([new CodeMagicMoveBand(0, 1), new CodeMagicMoveBand(3, 1)]);
    }

    [Fact]
    public void Line_counts_skip_blank_lines_and_count_a_changed_line_both_ways()
    {
        CodeMagicMoveDiff.LineCounts(Src(V1), Src(V2)).ShouldBe((2, 0));
        CodeMagicMoveDiff.LineCounts(Src(V2), Src(V3)).ShouldBe((1, 2));
    }

    // --- the move ----------------------------------------------------------------------------------------------------

    private static CodeMagicMovePlan Plan(string from, string to)
    {
        var a = Src(from);
        var b = Src(to);
        return new CodeMagicMovePlan(new CodeMagicMoveScene(a, []), b, CodeMagicMoveDiff.Bands(b, CodeMagicMoveDiff.Match(a, b)));
    }

    [Fact]
    public void Without_removals_kept_words_glide_at_once_a_line_later_each()
    {
        var plan = Plan(V1, V2);
        plan.HasRemovals.ShouldBeFalse();
        plan.GlideStart.ShouldBe(0);
        plan.GlideMs.ShouldBe(CodeMagicMovePlan.MinGlideMs);
        // "fn" goes from line 0 to line 2: halfway through its glide 24ms (two lines) after the glide starts.
        var fn = Array.FindIndex(plan.Texts, t => t == "fn");
        plan.Evaluate(24 + plan.GlideMs / 2);
        plan.Y[fn].ShouldBe(1, 0.01);
        plan.Evaluate(24 + plan.GlideMs);
        plan.Y[fn].ShouldBe(2);
    }

    [Fact]
    public void Removed_words_fade_and_lift_first_and_the_glide_waits_for_them()
    {
        var plan = Plan(V2, V1);
        plan.HasRemovals.ShouldBeTrue();
        plan.GlideStart.ShouldBe(CodeMagicMovePlan.RemovalLeadMs);
        var use = Array.FindIndex(plan.Texts, t => t == "use");
        plan.Roles[use].ShouldBe(CodeMagicMoveRole.Removed);
        plan.Evaluate(CodeMagicMovePlan.RemoveMs);
        plan.Alpha[use].ShouldBe(0, 1e-6);
        plan.Y[use].ShouldBe(-CodeMagicMovePlan.LiftLines, 1e-6);
        // Lines shrink from 6 to 3 on the glide's curve.
        plan.Evaluate(plan.GlideStart + plan.GlideMs);
        plan.Lines.ShouldBe(3);
    }

    [Fact]
    public void Longer_travel_makes_a_longer_glide()
    {
        var far = string.Join('\n', Enumerable.Range(0, 12).Select(i => $"x{i}();")) + "\nend();";
        var plan = Plan("end();", far);
        // end() travels 12 lines: 600 + 35 * 12.
        plan.GlideMs.ShouldBe(1020);
    }

    [Fact]
    public void Typing_starts_at_sixty_percent_of_the_glide_and_the_caret_fades_after()
    {
        var plan = Plan(V1, V2);
        plan.TypeStart.ShouldBe(0.6 * plan.GlideMs, 1e-6);
        plan.TypedChars.ShouldBe("usestd::io;letname=read();".Length);
        plan.Evaluate(plan.TypeStart - 1);
        plan.Caret.Alpha.ShouldBe(0);
        plan.Evaluate(plan.TypeStart);
        plan.Caret.Alpha.ShouldBe(1);
        plan.Caret.Column.ShouldBe(1);
        plan.Caret.Line.ShouldBe(0);
        // 26 characters at 35.5ms.
        plan.LastCharTime.ShouldBe(plan.TypeStart + 26 * CodeMagicMovePlan.TypePerCharMs, 1e-6);
        plan.Evaluate(plan.LastCharTime + CodeMagicMovePlan.CaretHoldMs + CodeMagicMovePlan.CaretFadeMs / 2);
        plan.Caret.Alpha.ShouldBe(0.5, 1e-6);
        plan.Duration.ShouldBe(plan.LastCharTime + CodeMagicMovePlan.CaretHoldMs + CodeMagicMovePlan.CaretFadeMs, 1e-6);
    }

    [Fact]
    public void A_long_change_types_within_a_second_and_a_half()
    {
        var big = string.Join('\n', Enumerable.Range(0, 10).Select(i => $"const value{i} = compute{i}(input);"));
        var plan = Plan("x", big);
        (plan.LastCharTime - plan.TypeStart).ShouldBe(CodeMagicMovePlan.MaxTypeMs, 1e-6);
    }

    [Fact]
    public void Words_type_in_whole_characters_and_fade_in()
    {
        var plan = Plan(V1, V2);
        var use = Array.FindIndex(plan.Texts, t => t == "use");
        plan.Evaluate(plan.TypeStart);
        plan.Chars[use].ShouldBe(1);
        plan.Alpha[use].ShouldBe(0);
        plan.Evaluate(plan.TypeStart + CodeMagicMovePlan.WordFadeMs);
        plan.Alpha[use].ShouldBe(1);
        plan.Chars[use].ShouldBe(2);
    }

    [Fact]
    public void At_the_end_every_word_rests_where_the_new_version_puts_it()
    {
        var plan = Plan(V2, V3);
        plan.Evaluate(plan.Duration);
        for (var k = 0; k < plan.Roles.Length; k++)
        {
            if (plan.Roles[k] == CodeMagicMoveRole.Removed)
            {
                plan.Alpha[k].ShouldBe(0, 1e-6);
                continue;
            }
            plan.X[k].ShouldBe(plan.ToX[k]);
            plan.Y[k].ShouldBe(plan.ToY[k]);
            plan.Alpha[k].ShouldBe(1);
            plan.Chars[k].ShouldBe(plan.Texts[k].Length);
        }
        plan.BandAlpha.ShouldAllBe(a => a == 1);
    }

    [Fact]
    public void A_new_move_mid_move_starts_from_where_every_word_is()
    {
        var first = Plan(V1, V2);
        var fn = Array.FindIndex(first.Texts, t => t == "fn");
        first.Evaluate(24 + first.GlideMs / 2);
        var scene = first.Snapshot();
        var v3 = Src(V3);
        var next = new CodeMagicMovePlan(scene, v3, []);
        var fnNext = Array.FindIndex(next.Texts, t => t == "fn");
        next.Evaluate(0);
        next.Y[fnNext].ShouldBe(first.Y[fn], 1e-6);
        next.Evaluate(next.Duration);
        next.Y[fnNext].ShouldBe(2);
    }

    // --- control -----------------------------------------------------------------------------------------------------

    private static CodeMagicMove Walkthrough() => new()
    {
        FileName = "greet.rs",
        Language = "rust",
        Label = "Greeting",
        Steps =
        {
            new CodeMagicMoveStep("Start", V1, "A function."),
            new CodeMagicMoveStep("Read a name", V2) { FileName = "main.rs" },
            new CodeMagicMoveStep("Take a name", V3, "Pass it in.")
        }
    };

    [Fact]
    public void Defaults_match_the_design()
    {
        var control = new CodeMagicMove();
        control.Value.ShouldBe(0);
        control.AutoPlay.ShouldBeTrue();
        control.Interval.ShouldBe(TimeSpan.FromSeconds(3.2));
        control.Speed.ShouldBe(1);
        control.IsPaused.ShouldBeFalse();
        control.Language.ShouldBe("tsx");
        control.Label.ShouldBe("Code walkthrough");
    }

    [AvaloniaFact]
    public void Shows_the_step_header_counts_and_caption()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control);
        control.CurrentStep!.Title.ShouldBe("Start");
        control.CurrentFileName.ShouldBe("greet.rs");
        control.Classes.Contains(CodeMagicMove.FirstStepPseudoClass).ShouldBeTrue();
        control.Value = 1;
        control.CurrentFileName.ShouldBe("main.rs");
        control.AddedLines.ShouldBe(2);
        control.RemovedLines.ShouldBe(0);
        control.Classes.Contains(CodeMagicMove.FirstStepPseudoClass).ShouldBeFalse();
        TestHelpers.Descendant<TextBlock>(control, CodeMagicMove.AddedPart).Text.ShouldBe("+2");
        TestHelpers.Descendant<TextBlock>(control, CodeMagicMove.RemovedPart).Text.ShouldBe("−0");
        window.Close();
    }

    [AvaloniaFact]
    public void With_motion_off_steps_change_at_once_and_raise_value_changed()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control);
        var changes = new List<(int, int)>();
        control.ValueChanged += (_, e) => changes.Add((e.OldValue, e.NewValue));
        control.Next();
        control.Plan.ShouldBeNull();
        control.Value.ShouldBe(1);
        control.GoTo(99);
        control.Value.ShouldBe(2);
        changes.ShouldBe([(0, 1), (1, 2)]);
        window.Close();
    }

    [AvaloniaFact]
    public void Arrow_keys_home_and_end_step_and_stop_autoplay()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control);
        var surface = TestHelpers.Descendant<CodeMagicMoveSurface>(control);
        surface.Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        control.Value.ShouldBe(1);
        control.IsPaused.ShouldBeTrue();
        window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
        control.Value.ShouldBe(2);
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        control.Value.ShouldBe(1);
        window.KeyPressQwerty(PhysicalKey.Home, RawInputModifiers.None);
        control.Value.ShouldBe(0);
        window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.None);
        control.Value.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Chosen_steps_are_announced_with_their_line_counts()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control);
        var status = TestHelpers.Descendant<TextBlock>(control, CodeMagicMove.StatusPart);
        AutomationProperties.GetLiveSetting(status).ShouldBe(AutomationLiveSetting.Polite);
        control.UserGoTo(2);
        status.Text.ShouldBe("Step 3 of 3: Take a name. 1 line added, 2 removed.");
        control.UserGoTo(0);
        status.Text.ShouldBe("Step 1 of 3: Start.");
        // Moves from code are not announced.
        control.Value = 1;
        status.Text.ShouldBe("Step 1 of 3: Start.");
        window.Close();
    }

    [AvaloniaFact]
    public void Dots_are_named_buttons_and_the_ends_disable_previous_and_next()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control);
        var dots = control.GetVisualDescendants().OfType<CodeMagicMoveDot>().ToList();
        dots.Count.ShouldBe(3);
        AutomationProperties.GetName(dots[1]).ShouldBe("Step 2: Read a name");
        dots[0].IsCurrent.ShouldBeTrue();
        var previous = TestHelpers.Descendant<Avalonia.Controls.Button>(control, CodeMagicMove.PreviousPart);
        var next = TestHelpers.Descendant<Avalonia.Controls.Button>(control, CodeMagicMove.NextPart);
        previous.IsEnabled.ShouldBeFalse();
        next.IsEnabled.ShouldBeTrue();
        control.Value = 2;
        dots[2].IsCurrent.ShouldBeTrue();
        dots[0].IsCurrent.ShouldBeFalse();
        next.IsEnabled.ShouldBeFalse();
        AutomationProperties.GetName(TestHelpers.Descendant<CodeMagicMoveSurface>(control)).ShouldBe("Greeting, step 3 of 3");
        window.Close();
    }

    [AvaloniaFact]
    public void A_move_runs_on_its_clock_and_lands()
    {
        var control = Walkthrough();
        control.AutoPlay = false;
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        control.Next();
        var plan = control.Plan.ShouldNotBeNull();
        control.IsMoving.ShouldBeTrue();
        control.Step(0.3);
        control.MoveTime.ShouldBe(300, 1e-6);
        control.Step(plan.Duration / 1000);
        control.Plan.ShouldBeNull();
        control.IsMoving.ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void Speed_multiplies_the_pace_and_going_back_mid_move_reverses()
    {
        var control = Walkthrough();
        control.AutoPlay = false;
        control.Speed = 2;
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        control.Next();
        control.Step(0.1);
        control.MoveTime.ShouldBe(200, 1e-6);
        control.Previous();
        control.Value.ShouldBe(0);
        control.Step(0.05);
        control.MoveTime.ShouldBe(100, 1e-6);
        control.Step(0.1);
        control.Plan.ShouldBeNull();
        window.Close();
    }

    [AvaloniaFact]
    public void A_drag_past_a_third_commits_and_a_short_one_rewinds()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        window.Width = 700;
        Dispatcher.UIThread.RunJobs();
        var width = TestHelpers.Descendant<CodeMagicMoveSurface>(control).Bounds.Width;
        var span = width * CodeMagicMove.DragSpan;

        control.BeginDrag();
        control.IsPaused.ShouldBeTrue();
        control.DragTo(-span * 0.25);
        var plan = control.Plan.ShouldNotBeNull();
        control.MoveTime.ShouldBe(plan.Duration * 0.25, 1);
        control.EndDrag(-span * 0.25, 0);
        control.Value.ShouldBe(0);
        control.Step(5);
        control.Plan.ShouldBeNull();
        control.Value.ShouldBe(0);

        var changes = 0;
        control.ValueChanged += (_, _) => changes++;
        control.BeginDrag();
        control.DragTo(-span * 0.4);
        control.EndDrag(-span * 0.4, 0);
        control.Value.ShouldBe(1);
        changes.ShouldBe(1);
        control.Plan.ShouldNotBeNull();
        control.Step(5);
        control.Plan.ShouldBeNull();

        // A flick commits even a short drag.
        control.BeginDrag();
        control.DragTo(-span * 0.1);
        control.EndDrag(-span * 0.1, -900);
        control.Value.ShouldBe(2);
        window.Close();
    }

    [AvaloniaFact]
    public void Past_the_first_step_the_code_rubber_bands_and_settles_back()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        control.BeginDrag();
        control.DragTo(10);
        control.RubberOffset.ShouldBe(0);
        control.DragTo(100);
        control.RubberOffset.ShouldBe(40 * 80 / (80 + CodeMagicMove.RubberHalf), 1e-6);
        control.DragTo(400);
        control.RubberOffset.ShouldBeLessThan(40);
        control.Plan.ShouldBeNull();
        control.EndDrag(400, 0);
        control.Value.ShouldBe(0);
        control.Step(1);
        control.RubberOffset.ShouldBe(0);
        window.Close();
    }

    [AvaloniaFact]
    public void Autoplay_rests_then_moves_on_and_loops_holding_under_the_mouse()
    {
        var control = Walkthrough();
        control.Interval = TimeSpan.FromSeconds(1);
        var window = TestHelpers.Show(control, ArcMotionLevel.Full);
        control.IsPlaying.ShouldBeTrue();
        control.Classes.Contains(CodeMagicMove.PlayingPseudoClass).ShouldBeTrue();
        control.Step(0.5);
        var dot = control.GetVisualDescendants().OfType<CodeMagicMoveDot>().First();
        // The frame loop may add a frame of its own.
        dot.Fill.ShouldBeInRange(0.5, 0.6);
        control.Step(0.5);
        control.Value.ShouldBe(1);
        control.Step(10);
        control.Plan.ShouldBeNull();
        control.Step(1);
        control.Value.ShouldBe(2);
        control.Step(10);
        control.Step(1);
        control.Value.ShouldBe(0);

        control.IsPaused = true;
        control.IsPlaying.ShouldBeFalse();
        control.Step(10);
        control.Value.ShouldBe(0);
        dot.Fill.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void Reduced_motion_jumps_and_keeps_autoplay_off()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control, ArcMotionLevel.Reduced);
        control.IsPlaying.ShouldBeFalse();
        control.Next();
        control.Plan.ShouldBeNull();
        control.Value.ShouldBe(1);
        window.Close();
    }

    [AvaloniaFact]
    public void The_code_shrinks_to_fit_a_narrow_width_and_the_footer_goes_compact()
    {
        var control = Walkthrough();
        control.Steps.Add(new CodeMagicMoveStep("Long", "let a_rather_long_identifier_name = compute_something_slow(arguments);"));
        var window = TestHelpers.Show(control);
        window.Width = 360;
        Dispatcher.UIThread.RunJobs();
        var surface = TestHelpers.Descendant<CodeMagicMoveSurface>(control);
        surface.FittedFontSize.ShouldBeLessThan(13);
        surface.FittedFontSize.ShouldBeGreaterThanOrEqualTo(10.5);
        control.Classes.Contains(CodeMagicMove.CompactPseudoClass).ShouldBeTrue();
        window.Width = 2000;
        Dispatcher.UIThread.RunJobs();
        surface.FittedFontSize.ShouldBe(13);
        control.Classes.Contains(CodeMagicMove.CompactPseudoClass).ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void The_height_follows_the_line_count()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control);
        window.Width = 700;
        Dispatcher.UIThread.RunJobs();
        var surface = TestHelpers.Descendant<CodeMagicMoveSurface>(control);
        var padding = surface.ActivePadding.Top + surface.ActivePadding.Bottom;
        surface.Bounds.Height.ShouldBe(3 * surface.LineHeight + padding, 2);
        control.Value = 1;
        Dispatcher.UIThread.RunJobs();
        surface.Bounds.Height.ShouldBe(6 * surface.LineHeight + padding, 2);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Words_take_the_syntax_colors_of_the_highlighter()
    {
        var control = Walkthrough();
        var window = TestHelpers.Show(control);
        await control.WhenHighlighted;
        Dispatcher.UIThread.RunJobs();
        var colors = control.SourceOf(0).Colors.ShouldNotBeNull();
        // "fn" is a keyword: it has a color of its own.
        colors[0].ShouldNotBe(0u);
        window.Close();
    }
}
