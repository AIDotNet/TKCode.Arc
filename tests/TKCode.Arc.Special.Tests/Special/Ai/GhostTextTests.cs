using System.Threading.Channels;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// GhostText: the ghost laid out after the text at the caret, Tab / Cmd+Right / Escape on a showing suggestion (and their
/// fall-through without one), typing through the ghost, caret moves, whole, async and streamed answers, null and failures
/// with Retry, the chip's states, single-line mode, the pause, labels, the sample and the pure helpers.
/// </summary>
public class GhostTextTests
{
    private static (GhostText Field, Window Window) Open(Action<GhostText>? setup = null, ArcMotionLevel level = ArcMotionLevel.Off,
        double width = 640, Control? after = null)
    {
        var field = new GhostText();
        setup?.Invoke(field);
        Control content = field;
        if (after is not null)
        {
            var panel = new StackPanel();
            panel.Children.Add(field);
            panel.Children.Add(after);
            content = panel;
        }
        var window = TestHelpers.Show(content, level);
        window.Width = width;
        window.Height = 600;
        Pump();
        return (field, window);
    }

    private static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        window.KeyReleaseQwerty(key, modifiers);
        Pump();
    }

    private static GhostTextSuggest Whole(string reply, List<string>? asked = null) => (text, _) =>
    {
        asked?.Add(text);
        return reply;
    };

    /// <summary>A field with text, focused, the caret at the end and a suggestion asked for.</summary>
    private static (GhostText Field, Window Window) Ready(string text, string reply, List<string>? asked = null,
        Control? after = null, Action<GhostText>? setup = null)
    {
        var (field, window) = Open(f =>
        {
            f.Suggest = Whole(reply, asked);
            f.Text = text;
            setup?.Invoke(f);
        }, after: after);
        field.Editor!.Focus();
        field.Editor.CaretToEnd();
        Pump();
        field.Request().ShouldBeTrue();
        Pump();
        return (field, window);
    }

    [AvaloniaFact]
    public void Suggestion_is_laid_out_right_after_the_text()
    {
        var (field, _) = Ready("Fixes the flaky", " checkout test.");
        field.Status.ShouldBe(GhostTextStatus.Ready);
        field.Suggestion.ShouldBe(" checkout test.");
        field.IsStreaming.ShouldBeFalse();
        field.Text.ShouldBe("Fixes the flaky");
        field.Editor!.Presenter!.LaidOutText.ShouldBe("Fixes the flaky checkout test.");
        field.Editor.CaretIndex.ShouldBe("Fixes the flaky".Length);
        field.Chip!.State.ShouldBe(GhostTextChipState.Ready);
        field.Announcer!.Announcement.ShouldBe("Suggestion: checkout test.. Press Tab to accept.");
    }

    [AvaloniaFact]
    public void Tab_accepts_the_whole_suggestion_and_keeps_the_caret_at_the_end()
    {
        var (field, window) = Ready("Fixes the flaky", " checkout test.");
        GhostTextAcceptedEventArgs? accepted = null;
        field.Accepted += (_, e) => accepted = e;
        Press(window, PhysicalKey.Tab);
        field.Text.ShouldBe("Fixes the flaky checkout test.");
        field.Editor!.CaretIndex.ShouldBe(field.Text!.Length);
        field.Status.ShouldBe(GhostTextStatus.Idle);
        field.Suggestion.ShouldBe(string.Empty);
        accepted.ShouldNotBeNull();
        accepted.AcceptedText.ShouldBe(" checkout test.");
        accepted.Mode.ShouldBe(GhostTextAcceptMode.All);
        field.Chip!.State.ShouldBe(GhostTextChipState.Accepted);
        field.Announcer!.Announcement.ShouldBe("Suggestion accepted");
        field.Editor.IsFocused.ShouldBeTrue();
        field.Editor.Presenter!.LaidOutText.ShouldBe("Fixes the flaky checkout test.");
        field.EndAcceptedHold();
        field.Chip.State.ShouldBe(GhostTextChipState.Idle);
    }

    [AvaloniaFact]
    public void Tab_moves_focus_when_no_suggestion_shows()
    {
        var next = new Avalonia.Controls.Button { Content = "Next" };
        var (field, window) = Open(f => f.Suggest = Whole(" x"), after: next);
        field.Editor!.Focus();
        Pump();
        Press(window, PhysicalKey.Tab);
        window.FocusManager!.GetFocusedElement().ShouldBe(next);
    }

    [AvaloniaFact]
    public void Ctrl_or_alt_right_accepts_one_word_at_a_time()
    {
        var (field, window) = Ready("Fixes the flaky", " checkout test for real.");
        var words = new List<string>();
        field.Accepted += (_, e) =>
        {
            e.Mode.ShouldBe(GhostTextAcceptMode.Word);
            words.Add(e.AcceptedText);
        };
        Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Control);
        field.Text.ShouldBe("Fixes the flaky checkout");
        field.Suggestion.ShouldBe(" test for real.");
        field.Status.ShouldBe(GhostTextStatus.Ready);
        field.Editor!.Presenter!.LaidOutText.ShouldBe("Fixes the flaky checkout test for real.");
        Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Alt);
        Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Meta);
        field.Text.ShouldBe("Fixes the flaky checkout test for");
        field.AcceptWord().ShouldBeTrue();
        words.ShouldBe([" checkout", " test", " for", " real."]);
        field.Status.ShouldBe(GhostTextStatus.Idle);
        field.Chip!.State.ShouldBe(GhostTextChipState.Accepted);
    }

    [AvaloniaFact]
    public void Word_key_moves_the_caret_as_usual_without_a_suggestion()
    {
        var (field, window) = Open(f => f.Text = "one two three");
        field.Editor!.Focus();
        field.Editor.CaretIndex = 0;
        Pump();
        Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Control);
        field.Editor.CaretIndex.ShouldBeGreaterThan(0);
        field.Text.ShouldBe("one two three");
    }

    [AvaloniaFact]
    public void Escape_dismisses_and_is_left_alone_without_a_suggestion()
    {
        var (field, window) = Ready("Fixes the flaky", " checkout test.");
        GhostTextDismissedEventArgs? dismissed = null;
        field.Dismissed += (_, e) => dismissed = e;
        var unhandledEscapes = 0;
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape && !e.Handled)
            {
                unhandledEscapes++;
            }
        }, RoutingStrategies.Bubble);
        Press(window, PhysicalKey.Escape);
        dismissed.ShouldNotBeNull();
        dismissed.Suggestion.ShouldBe(" checkout test.");
        field.Status.ShouldBe(GhostTextStatus.Idle);
        field.Text.ShouldBe("Fixes the flaky");
        field.Editor!.Ghost.ShouldBe(string.Empty);
        field.Announcer!.Announcement.ShouldBe("Suggestion dismissed");
        unhandledEscapes.ShouldBe(0);
        Press(window, PhysicalKey.Escape);
        unhandledEscapes.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Typing_the_same_characters_consumes_the_ghost_without_asking_again()
    {
        var asked = new List<string>();
        var (field, window) = Ready("Fixes the flaky", " checkout test.", asked);
        window.KeyTextInput(" che");
        Pump();
        field.Text.ShouldBe("Fixes the flaky che");
        field.Suggestion.ShouldBe("ckout test.");
        field.Status.ShouldBe(GhostTextStatus.Ready);
        field.Editor!.Presenter!.LaidOutText.ShouldBe("Fixes the flaky checkout test.");
        field.IsWaitingForPause.ShouldBeFalse();
        asked.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public void Typing_something_else_clears_the_ghost_at_once_and_waits_for_a_pause()
    {
        var asked = new List<string>();
        var (field, window) = Ready("Fixes the flaky", " checkout test.", asked);
        window.KeyTextInput("x");
        Pump();
        field.Text.ShouldBe("Fixes the flakyx");
        field.Suggestion.ShouldBe(string.Empty);
        field.Status.ShouldBe(GhostTextStatus.Idle);
        field.Editor!.Ghost.ShouldBe(string.Empty);
        field.IsWaitingForPause.ShouldBeTrue();
        field.FlushPause().ShouldBeTrue();
        Pump();
        asked.ShouldBe(["Fixes the flaky", "Fixes the flakyx"]);
        field.Status.ShouldBe(GhostTextStatus.Ready);
    }

    [AvaloniaFact]
    public void Moving_the_caret_away_from_the_end_clears_the_suggestion()
    {
        var (field, window) = Ready("Fixes the flaky", " checkout test.");
        Press(window, PhysicalKey.ArrowLeft);
        field.Status.ShouldBe(GhostTextStatus.Idle);
        field.Editor!.Ghost.ShouldBe(string.Empty);
        field.Editor.CaretIndex.ShouldBe("Fixes the flaky".Length - 1);
        field.Request().ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Keys_that_leave_the_caret_at_the_end_keep_the_ghost()
    {
        var (field, window) = Ready("Fixes the flaky", " checkout test.");
        Press(window, PhysicalKey.End);
        field.Status.ShouldBe(GhostTextStatus.Ready);
        field.Editor!.CaretIndex.ShouldBe("Fixes the flaky".Length);
        field.Editor.Presenter!.LaidOutText.ShouldBe("Fixes the flaky checkout test.");
    }

    [AvaloniaFact]
    public void Leading_space_is_stripped_when_the_text_ends_with_whitespace()
    {
        var (field, _) = Ready("Fixes the flaky ", " checkout test.");
        field.Suggestion.ShouldBe("checkout test.");
    }

    [AvaloniaFact]
    public void Null_skips_without_thinking_and_short_text_never_asks()
    {
        var statuses = new List<GhostTextStatus>();
        var (field, _) = Open(f =>
        {
            f.Suggest = (_, _) => null;
            f.Text = "Hello there";
        });
        field.StatusChanged += (_, e) => statuses.Add(e.NewStatus);
        field.Editor!.Focus();
        field.Editor.CaretToEnd();
        field.Request().ShouldBeFalse();
        statuses.ShouldBeEmpty();
        field.Status.ShouldBe(GhostTextStatus.Idle);

        var calls = 0;
        field.Suggest = (_, _) =>
        {
            calls++;
            return "x";
        };
        field.Text = "ab";
        field.Editor.CaretToEnd();
        field.Request().ShouldBeFalse();
        calls.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_failure_shows_no_suggestion_and_retry_asks_again()
    {
        var fail = true;
        var (field, _) = Open(f =>
        {
            f.Suggest = (_, _) => fail ? throw new InvalidOperationException("down") : (GhostTextResponse?)" works.";
            f.Text = "Autocomplete";
        });
        field.Editor!.Focus();
        field.Editor.CaretToEnd();
        field.Request().ShouldBeTrue();
        Pump();
        field.Status.ShouldBe(GhostTextStatus.Error);
        field.Chip!.State.ShouldBe(GhostTextChipState.Error);
        field.Chip.Focusable.ShouldBeTrue();
        AutomationProperties.GetName(field.Chip).ShouldBe("Retry suggestion");
        field.Announcer!.Announcement.ShouldBe("Couldn't get a suggestion");
        fail = false;
        field.Chip.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        Pump();
        field.Status.ShouldBe(GhostTextStatus.Ready);
        field.Suggestion.ShouldBe(" works.");
        field.Chip.Focusable.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_task_shows_thinking_until_it_completes()
    {
        var source = new TaskCompletionSource<string?>();
        var (field, _) = Open(f =>
        {
            f.Suggest = (_, _) => source.Task;
            f.Text = "Release notes";
        });
        field.Editor!.Focus();
        field.Editor.CaretToEnd();
        field.Request().ShouldBeTrue();
        field.Status.ShouldBe(GhostTextStatus.Loading);
        field.Chip!.State.ShouldBe(GhostTextChipState.Thinking);
        source.SetResult(" for the March release.");
        TestHelpers.PumpUntil(() => field.Status == GhostTextStatus.Ready).ShouldBeTrue();
        field.Suggestion.ShouldBe(" for the March release.");
    }

    [AvaloniaFact]
    public void A_stream_appends_chunks_and_late_chunks_are_ignored_after_typing()
    {
        var channel = Channel.CreateUnbounded<string>();
        CancellationToken seen = default;
        var (field, window) = Open(f =>
        {
            f.Suggest = (_, token) =>
            {
                seen = token;
                return GhostTextResponse.FromStream(channel.Reader.ReadAllAsync(token));
            };
            f.Text = "The retry helper now";
        });
        field.Editor!.Focus();
        field.Editor.CaretToEnd();
        field.Request().ShouldBeTrue();
        field.Status.ShouldBe(GhostTextStatus.Loading);
        channel.Writer.TryWrite(" backs off");
        TestHelpers.PumpUntil(() => field.Status == GhostTextStatus.Ready).ShouldBeTrue();
        field.IsStreaming.ShouldBeTrue();
        field.Suggestion.ShouldBe(" backs off");
        channel.Writer.TryWrite(" for two seconds.");
        TestHelpers.PumpUntil(() => field.Suggestion == " backs off for two seconds.").ShouldBeTrue();
        field.Editor.Chunks.Count.ShouldBe(2);

        window.KeyTextInput("!");
        Pump();
        seen.IsCancellationRequested.ShouldBeTrue();
        channel.Writer.TryWrite(" late");
        Pump();
        field.Suggestion.ShouldBe(string.Empty);
        field.Editor.Ghost.ShouldBe(string.Empty);
        field.IsStreaming.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_finished_stream_is_announced()
    {
        var channel = Channel.CreateUnbounded<string>();
        var (field, _) = Open(f =>
        {
            f.Suggest = (_, token) => GhostTextResponse.FromStream(channel.Reader.ReadAllAsync(token));
            f.Text = "Ship it";
        });
        field.Editor!.Focus();
        field.Editor.CaretToEnd();
        field.Request();
        channel.Writer.TryWrite(" after review.");
        channel.Writer.Complete();
        TestHelpers.PumpUntil(() => field.Status == GhostTextStatus.Ready && !field.IsStreaming).ShouldBeTrue();
        field.Announcer!.Announcement.ShouldBe("Suggestion: after review.. Press Tab to accept.");
    }

    [AvaloniaFact]
    public void Leaving_the_field_aborts_and_takes_the_ghost_away()
    {
        var next = new Avalonia.Controls.Button { Content = "Next" };
        var (field, _) = Ready("Fixes the flaky", " checkout test.", after: next);
        next.Focus();
        Pump();
        field.Status.ShouldBe(GhostTextStatus.Idle);
        field.Editor!.Ghost.ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void The_pause_asks_only_when_focused_with_the_caret_at_the_end()
    {
        var asked = new List<string>();
        var (field, window) = Open(f => f.Suggest = Whole(" more", asked));
        field.Editor!.Focus();
        Pump();
        window.KeyTextInput("Hel");
        Pump();
        field.IsWaitingForPause.ShouldBeTrue();
        field.Delay.ShouldBe(TimeSpan.FromMilliseconds(450));
        field.FlushPause().ShouldBeTrue();
        asked.ShouldBe(["Hel"]);
    }

    [AvaloniaFact]
    public void Disabled_fields_do_not_ask()
    {
        var asked = new List<string>();
        var (field, _) = Open(f =>
        {
            f.Suggest = Whole(" more", asked);
            f.Text = "Hello";
            f.IsEnabled = false;
        });
        field.Request().ShouldBeFalse();
        asked.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Single_line_turns_breaks_into_spaces_and_keeps_the_chip_inline()
    {
        var (field, window) = Open(f =>
        {
            f.IsMultiline = false;
            f.Suggest = Whole(" line one\nline two");
        });
        field.Classes.Contains(GhostText.SingleLinePseudoClass).ShouldBeTrue();
        field.Editor!.AcceptsReturn.ShouldBeFalse();
        field.Editor.TextWrapping.ShouldBe(Avalonia.Media.TextWrapping.NoWrap);
        Grid.GetRow((Control)field.Chip!.Parent!).ShouldBe(1);
        field.Editor.Focus();
        Pump();
        window.KeyTextInput("a\nb c");
        Pump();
        field.Text.ShouldBe("a b c");
        field.Request().ShouldBeTrue();
        field.Suggestion.ShouldBe(" line one line two");
    }

    [AvaloniaFact]
    public void Rows_set_the_minimum_and_maximum_height_and_the_ghost_grows_the_field()
    {
        var (field, window) = Open(f =>
        {
            f.Rows = 2;
            f.MaxRows = 4;
            f.Text = "Short";
            f.Suggest = Whole(string.Concat(Enumerable.Repeat(" a long continuation that wraps", 6)));
        }, width: 360);
        var editor = field.Editor!;
        var padding = editor.Padding.Top + editor.Padding.Bottom;
        editor.MinHeight.ShouldBe(2 * editor.LineHeight + padding);
        editor.MaxHeight.ShouldBe(4 * editor.LineHeight + padding);
        var before = editor.Bounds.Height;
        editor.Focus();
        editor.CaretToEnd();
        field.Request().ShouldBeTrue();
        Pump();
        editor.Bounds.Height.ShouldBeGreaterThan(before);
        editor.Bounds.Height.ShouldBeLessThanOrEqualTo(editor.MaxHeight);
        window.Width.ShouldBe(360);
    }

    [AvaloniaFact]
    public void Chip_shows_the_model_and_hints_show_on_wide_fields_only()
    {
        var (field, window) = Ready("Fixes the flaky", " checkout test.", setup: f => f.Model = new AgentModel("Mistral Small 4"));
        field.Hints!.IsVisible.ShouldBeTrue();
        field.WordKey!.Text.ShouldEndWith("→");
        field.EscapeKey!.Text.ShouldBe("Esc");
        window.Width = 400;
        Pump();
        field.Hints.IsVisible.ShouldBeFalse();
        field.Dismiss().ShouldBeTrue();
        field.Chip!.State.ShouldBe(GhostTextChipState.Idle);
        AutomationProperties.GetName(field.Chip).ShouldBe("Suggestions by Mistral Small 4");
    }

    [AvaloniaFact]
    public void Labels_reword_the_chip_and_the_announcements()
    {
        var (field, _) = Ready("Fixes the flaky", " checkout test.", setup: f => f.Labels = new GhostTextLabels
        {
            AcceptSuggestion = "Accepter",
            SuggestionAnnouncement = s => "Suggestion : " + s,
            KeysDescription = "Tab pour accepter",
        });
        AutomationProperties.GetName(field.Chip!).ShouldBe("Accepter");
        field.Announcer!.Announcement.ShouldBe("Suggestion : checkout test.");
        AutomationProperties.GetHelpText(field.Editor!).ShouldBe("Tab pour accepter");
    }

    [AvaloniaFact]
    public void Key_caps_press_when_their_key_fires()
    {
        var (field, window) = Ready("Fixes the flaky", " checkout test.");
        Press(window, PhysicalKey.ArrowRight, RawInputModifiers.Control);
        field.WordKey!.IsStruck.ShouldBeTrue();
        Press(window, PhysicalKey.Escape);
        field.EscapeKey!.IsStruck.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Accepted_text_merges_ink_with_full_motion()
    {
        var (field, window) = Open(f =>
        {
            f.Suggest = Whole(" checkout test.");
            f.Text = "Fixes the flaky";
        }, level: ArcMotionLevel.Full);
        field.Editor!.Focus();
        field.Editor.CaretToEnd();
        field.Request().ShouldBeTrue();
        field.Editor.Chunks[0].Opacity.ShouldBeLessThan(1);
        Press(window, PhysicalKey.Tab);
        field.Editor.Merges.Count.ShouldBe(1);
        field.Editor.Merges[0].Start.ShouldBe("Fixes the flaky".Length);
        TestHelpers.PumpUntil(() => field.Editor.Merges.Count == 0).ShouldBeTrue();
        field.Text.ShouldBe("Fixes the flaky checkout test.");
    }

    /// <summary>An invented pull request description in two lines, with the suggestions that continue them.</summary>
    private static readonly GhostTextSimulationLine[] s_script =
    [
        new("Fixes the flaky checkout test", " by waiting for the payment webhook before it asserts."),
        new("The retry helper now", " backs off for up to two seconds instead of sleeping."),
    ];

    private static void Simulated(GhostText field)
    {
        field.Label = "Description";
        field.Placeholder = "Describe what changed and why";
        field.Simulation = new GhostTextSimulation { Script = s_script, Suggest = Whole(" and more") };
    }

    [AvaloniaFact]
    public void Field_without_a_suggest_function_or_a_simulation_is_a_plain_field()
    {
        var (field, window) = Open();
        field.SampleState.ShouldBe(GhostTextSampleState.None);
        field.PlayingSimulation.ShouldBeNull();
        field.EffectiveSuggest.ShouldBeNull();
        field.Editor!.PlaceholderText.ShouldBeNull();
        field.Model.ShouldBeNull();
        field.Text.ShouldBeNullOrEmpty();
        window.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Removing_the_simulation_stops_it_and_empties_the_field()
    {
        var (field, window) = Open(Simulated);
        var simulation = field.Simulation!;
        simulation.IsRunning.ShouldBeTrue();
        simulation.Tick(0.7);
        simulation.Tick(0.5);
        field.Text!.Length.ShouldBeGreaterThan(0);
        field.Simulation = null;
        Pump();
        simulation.IsRunning.ShouldBeFalse();
        field.SampleState.ShouldBe(GhostTextSampleState.None);
        field.PlayingSimulation.ShouldBeNull();
        field.EffectiveSuggest.ShouldBeNull();
        field.Text.ShouldBeNullOrEmpty();
        window.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Simulation_plays_with_no_suggest_function_and_clicking_takes_it_over()
    {
        var (field, window) = Open(Simulated);
        field.SampleState.ShouldBe(GhostTextSampleState.Playing);
        field.Simulation!.IsRunning.ShouldBeTrue();
        var editor = field.Editor!;
        AutomationProperties.GetName(editor).ShouldBe("Description");
        editor.PlaceholderText.ShouldBe("Describe what changed and why");
        field.Simulation.Tick(0.7);
        field.Simulation.Tick(0.5);
        field.Text!.Length.ShouldBeGreaterThan(0);
        "Fixes the flaky checkout test".ShouldStartWith(field.Text);

        editor.Focus();
        Pump();
        field.SampleState.ShouldBe(GhostTextSampleState.TakenOver);
        field.Simulation.IsRunning.ShouldBeFalse();
        field.EffectiveSuggest.ShouldNotBeNull();
        window.ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void Text_or_a_suggest_function_wins_over_the_simulation()
    {
        var (withText, _) = Open(f =>
        {
            Simulated(f);
            f.Text = "Hello";
        });
        withText.SampleState.ShouldBe(GhostTextSampleState.Declined);
        withText.Simulation!.IsRunning.ShouldBeFalse();
        var (withSuggest, _) = Open(f =>
        {
            Simulated(f);
            f.Suggest = Whole(" x");
        });
        withSuggest.SampleState.ShouldNotBe(GhostTextSampleState.Playing);
        withSuggest.Simulation!.IsRunning.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Plain_variant_and_keyboard_focus_set_pseudo_classes()
    {
        var (field, window) = Open(f =>
        {
            f.Variant = GhostTextVariant.Plain;
            f.Suggest = Whole(" x");
        });
        field.Classes.Contains(GhostText.PlainPseudoClass).ShouldBeTrue();
        field.Editor!.Focus(NavigationMethod.Tab);
        Pump();
        field.Classes.Contains(GhostText.KeyboardFocusPseudoClass).ShouldBeTrue();
        field.Classes.Contains(GhostText.FocusedPseudoClass).ShouldBeTrue();
        window.ShouldNotBeNull();
    }

    [Fact]
    public void Next_word_takes_leading_spaces_and_one_word()
    {
        GhostText.NextWord(" checkout test.").ShouldBe(" checkout");
        GhostText.NextWord("ckout test.").ShouldBe("ckout");
        GhostText.NextWord("  ").ShouldBe("  ");
        GhostText.NextWord(string.Empty).ShouldBe(string.Empty);
        GhostText.SingleLine("a\r\nb\nc").ShouldBe("a b c");
    }

    [Fact]
    public void Simulation_answers_its_script_and_hands_the_rest_to_its_suggest_function()
    {
        var scriptedOnly = new GhostTextSimulation { Script = s_script };
        scriptedOnly.Answer("Fixes the flaky checkout test", CancellationToken.None).ShouldNotBeNull();
        scriptedOnly.Answer("Something unscripted", CancellationToken.None).ShouldBeNull();
        var withModel = new GhostTextSimulation { Script = s_script, Suggest = Whole(" more") };
        withModel.Answer("Something unscripted", CancellationToken.None).ShouldNotBeNull();
        foreach (var chunk in GhostTextSimulation.Chunks(" one two three four five six seven eight nine ten eleven"))
        {
            chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBeInRange(1, 6);
        }
        string.Concat(GhostTextSimulation.Chunks(" a b c d e f g")).ShouldBe(" a b c d e f g");
    }
}
