using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// ToneTuner: the text model (paragraphs, sentences, alignment, inserted ranges, instructions), the simulation (an
/// invented fixture and the plain rules), the empty card, the card's states (original, rewriting, rewritten, failed),
/// chips and sliders, the debounce and superseded requests, the change highlights and count, labels, the keyboard model,
/// breakpoints, the reserved height and the sentence crossfade.
/// </summary>
public class ToneTunerTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    /// <summary>The invented session, answering at once.</summary>
    private static ToneTunerSimulation Demo() => new()
    {
        Text = ToneTunerFixture.Draft,
        Versions = ToneTunerFixture.Versions,
        RecipientName = ToneTunerFixture.RecipientName,
        RecipientSubject = ToneTunerFixture.RecipientSubject,
        Model = ToneTunerFixture.Model,
        Latency = TimeSpan.Zero,
    };

    /// <summary>A card playing the fixture session (pass <paramref name="simulate"/> false for a card with no data).</summary>
    private static (ToneTuner Tuner, Window Window) Open(Action<ToneTuner>? setup = null, double width = 640,
        ArcMotionLevel level = ArcMotionLevel.Off, bool simulate = true)
    {
        var tuner = new ToneTuner { SimulatedLatency = TimeSpan.Zero, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (simulate)
        {
            tuner.Simulation = Demo();
        }
        setup?.Invoke(tuner);
        var window = TestHelpers.Show(tuner, level);
        window.Width = width;
        window.Height = 900;
        Pump();
        return (tuner, window);
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

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        window.KeyReleaseQwerty(key, RawInputModifiers.None);
        Pump();
    }

    private static ToneTunerChip Chip(ToneTuner tuner, ToneVoice voice) => tuner.ChipViews.First(c => c.Preset!.Voice == voice);

    private static void Click(ToneTunerChip chip)
    {
        chip.Focus();
        var top = TopLevel.GetTopLevel(chip)!;
        top.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        top.KeyReleaseQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Pump();
    }

    // ---- text model ----

    [Fact]
    public void Paragraphs_Split_On_Blank_Lines_And_Sentences_On_Stops_And_Line_Breaks()
    {
        var text = ToneTunerText.Normalize("Dear Ms. Ramirez,\r\n\r\nThanks! It costs $249.00 today. Really?  Yes.\n  \nBest,\nMaya\n");
        var paragraphs = ToneTunerText.Paragraphs(text);
        paragraphs.Select(p => p.Text).ShouldBe(["Dear Ms. Ramirez,", "Thanks! It costs $249.00 today. Really?  Yes.", "Best,\nMaya"]);
        paragraphs[0].Sentences.ShouldBe(["Dear Ms. Ramirez,"]);
        paragraphs[1].Sentences.ShouldBe(["Thanks!", "It costs $249.00 today.", "Really?", "Yes."]);
        paragraphs[2].Sentences.ShouldBe(["Best,", "Maya"]);
        foreach (var paragraph in paragraphs)
        {
            string.Concat(paragraph.Pieces.Select(p => paragraph.Text.Substring(p.Start, p.Length))).ShouldBe(paragraph.Text);
            text.Substring(paragraph.Offset, paragraph.Text.Length).ShouldBe(paragraph.Text);
        }
    }

    [Fact]
    public void Align_Counts_Equal_Sentences_At_Both_Ends()
    {
        ToneTunerText.Align(["a", "b", "c", "d"], ["a", "x", "d"]).ShouldBe((1, 1));
        ToneTunerText.Align(["a", "b"], ["a", "b"]).ShouldBe((2, 0));
        ToneTunerText.Align(["a"], ["b", "c"]).ShouldBe((0, 0));
        // Match keeps an unchanged sentence between two changed ones (it never animates).
        ToneTunerText.Match(["ack", "cause", "refund", "timing"], ["ack2", "cause", "refund2", "timing"]).ShouldBe([-1, 1, -1, 3]);
        ToneTunerText.Match(["a", "b"], ["b", "c", "a"]).Count(i => i >= 0).ShouldBe(1);
    }

    [Fact]
    public void Inserted_Ranges_Cover_New_Words_Only()
    {
        const string original = "Thank you for the report. We refunded it.";
        const string text = "Thanks so much for the report. We refunded it.";
        var (ranges, words) = ToneTunerText.Inserted(original, text);
        words.ShouldBe(3);
        ranges.Select(r => text.Substring(r.Start, r.Length)).ShouldBe(["Thanks so much"]);
        ToneTunerText.Inserted(original, original).Ranges.ShouldBeEmpty();
    }

    [Fact]
    public void Instructions_Name_The_Tone_And_Keep_The_Facts()
    {
        var text = ToneTunerText.Instructions(new ToneSettings(2, 0, ToneVoice.Empathetic));
        text.ShouldContain("formal");
        text.ShouldContain("brief");
        text.ShouldContain("empathetic");
        text.ShouldContain("Keep every fact, number");
        text.ShouldContain("plain text");
    }

    [Fact]
    public void Fixture_Starts_From_Its_Default_Version_And_Keeps_Unchanged_Sentences_Identical()
    {
        ToneTunerFixture.Draft.ShouldBe(ToneTunerFixture.Compose(ToneSettings.Default));
        ToneTunerFixture.Draft.ShouldStartWith("Hi Sofia,\n\n");
        ToneTunerFixture.Draft.ShouldContain("INV-20481");
        // Every tone but the starting one has its version, and the simulation answers with it.
        ToneTunerFixture.Versions.Count.ShouldBe(35);
        var simulation = Demo();
        foreach (var version in ToneTunerFixture.Versions)
        {
            simulation.Rewrite(version.Settings).ShouldBe(ToneTunerFixture.Compose(version.Settings));
        }
        simulation.Rewrite(ToneSettings.Default).ShouldBe(ToneTunerFixture.Draft);
        // A voice change at the same formality and detail keeps the cause and timing sentences word for word.
        var friendly = ToneTunerFixture.Compose(ToneSettings.Default.WithVoice(ToneVoice.Friendly));
        friendly.ShouldContain("A payment retry on our side went through twice after a timeout.");
        friendly.ShouldContain("It should appear on your statement within 5–7 business days.");
        // Brief drops the cause and the closing paragraph.
        var brief = ToneTunerFixture.Compose(ToneSettings.Default.WithDetail(0));
        brief.ShouldNotContain("payment retry");
        ToneTunerText.Paragraphs(brief).Count.ShouldBe(3);
    }

    [Fact]
    public void Simulation_Rewrites_Tones_Without_A_Version_With_Plain_Rules()
    {
        const string draft = "Hi Sam,\n\nI am glad you asked. We have fixed the bug in 2 places. It is live now.\n\nBest,\nAva";
        var simulation = new ToneTunerSimulation { Text = draft };
        simulation.Rewrite(ToneSettings.Default).ShouldBe(draft);
        simulation.Rewrite(new ToneSettings(0, 1, ToneVoice.Neutral)).ShouldBe(ToneTunerSimulation.Rules(draft, new ToneSettings(0, 1, ToneVoice.Neutral)));
        var casual = ToneTunerSimulation.Rules(draft, new ToneSettings(0, 1, ToneVoice.Neutral));
        casual.ShouldStartWith("Hey Sam,");
        casual.ShouldContain("I'm glad you asked.");
        casual.ShouldEndWith("Cheers,\nAva");
        var formal = ToneTunerSimulation.Rules(draft, new ToneSettings(2, 1, ToneVoice.Neutral));
        formal.ShouldStartWith("Dear Sam,");
        formal.ShouldEndWith("Kind regards,\nAva");
        var brief = ToneTunerSimulation.Rules(draft, new ToneSettings(1, 0, ToneVoice.Neutral));
        brief.ShouldContain("I am glad you asked. We have fixed the bug in 2 places.");
        brief.ShouldNotContain("It is live now.");
        ToneTunerSimulation.Rules(draft, new ToneSettings(1, 1, ToneVoice.Empathetic)).ShouldContain("I'm sorry for the trouble");
        // A version given for a tone wins over the rules.
        var given = new ToneTunerSimulation { Text = draft, Versions = [new ToneTunerVersion(new ToneSettings(2, 1, ToneVoice.Neutral), "Dear Sam,\n\nDone.")] };
        given.Rewrite(new ToneSettings(2, 1, ToneVoice.Neutral)).ShouldBe("Dear Sam,\n\nDone.");
    }

    // ---- the card ----

    [AvaloniaFact]
    public void Opens_On_The_Simulation_Original()
    {
        var (tuner, window) = Open();
        try
        {
            tuner.IsSimulated.ShouldBeTrue();
            tuner.Status.ShouldBe(ToneTunerStatus.Original);
            tuner.StatusLabel.ShouldBe("Original draft");
            tuner.DisplayedText.ShouldBe(ToneTunerFixture.Draft);
            tuner.ChangedWords.ShouldBe(0);
            tuner.CopyView!.Value.ShouldBe(ToneTunerFixture.Draft);
            tuner.ChipViews.Select(c => c.Label).ShouldBe(["Friendly", "Confident", "Empathetic"]);
            tuner.ChipViews.ShouldAllBe(c => c.IsChecked == false);
            TestHelpers.Descendant<TextBlock>(tuner, ToneTuner.NamePart).Text.ShouldBe("Sofia Ramirez");
            TestHelpers.Descendant<TextBlock>(tuner, ToneTuner.SubjectPart).Text.ShouldBe("Re: Duplicate charge on INV-20481");
            tuner.FormalitySlider!.Label.ShouldBe("Formality");
            tuner.FormalitySlider.FormatValue(2).ShouldBe("Formal");
            tuner.DetailSlider!.FormatValue(0).ShouldBe("Brief");
            tuner.CountView!.IsVisible.ShouldBeFalse();
            tuner.MessageView!.ParagraphTexts.Count.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Chip_Sets_Its_Voice_And_Starting_Point_And_Rewrites_After_The_Debounce()
    {
        var (tuner, window) = Open();
        try
        {
            var changes = new List<ToneTunerValueChangedEventArgs>();
            var landed = new List<ToneTunerRewrittenEventArgs>();
            tuner.ValueChanged += (_, e) => changes.Add(e);
            tuner.Rewritten += (_, e) => landed.Add(e);

            Click(Chip(tuner, ToneVoice.Friendly));

            var friendly = new ToneSettings(0, 1, ToneVoice.Friendly);
            tuner.Value.ShouldBe(friendly);
            changes.Count.ShouldBe(1);
            changes[0].OldValue.ShouldBe(ToneSettings.Default);
            changes[0].NewValue.ShouldBe(friendly);
            Chip(tuner, ToneVoice.Friendly).IsChecked.ShouldBe(true);
            tuner.FormalitySlider!.Value.ShouldBe(0);
            // The status flips on the same frame as the input; the request waits for the quiet time.
            tuner.Status.ShouldBe(ToneTunerStatus.Rewriting);
            tuner.StatusLabel.ShouldBe("Rewriting with " + ToneTunerFixture.Model);
            tuner.StatusTextView!.IsShimmering.ShouldBeTrue();
            tuner.IsWaitingForDebounce.ShouldBeTrue();
            landed.ShouldBeEmpty();

            tuner.FlushDebounce().ShouldBeTrue();
            Pump();

            tuner.Status.ShouldBe(ToneTunerStatus.Rewritten);
            tuner.DisplayedText.ShouldBe(ToneTunerFixture.Compose(friendly));
            landed.Count.ShouldBe(1);
            landed[0].Text.ShouldBe(tuner.DisplayedText);
            landed[0].Value.ShouldBe(friendly);
            landed[0].IsSimulated.ShouldBeTrue();
            tuner.ChangedWords.ShouldBeGreaterThan(0);
            tuner.CountView!.IsVisible.ShouldBeTrue();
            tuner.CountText!.Text.ShouldBe(tuner.ChangedWords + " words changed");
            tuner.SettingsText!.Text.ShouldBe("· Casual · Standard · Friendly");
            tuner.Announcement.ShouldBe("Rewritten: Casual · Standard · Friendly. Sample rewrite, nothing was sent.");
            tuner.MessageView!.Highlights.ShouldNotBeEmpty();
            tuner.MessageView.Highlights.ShouldAllBe(h => h.Opacity == 1);
            tuner.CopyView!.Value.ShouldBe(tuner.DisplayedText);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressing_The_Same_Chip_Again_Returns_To_The_Neutral_Voice()
    {
        var (tuner, window) = Open();
        try
        {
            Click(Chip(tuner, ToneVoice.Empathetic));
            tuner.Value.ShouldBe(new ToneSettings(1, 2, ToneVoice.Empathetic));
            Click(Chip(tuner, ToneVoice.Empathetic));
            tuner.Value.ShouldBe(new ToneSettings(1, 2, ToneVoice.Neutral));
            tuner.ChipViews.ShouldAllBe(c => c.IsChecked == false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Starting_Tone_Shows_The_Original_Without_A_Request()
    {
        var calls = 0;
        var (tuner, window) = Open(t =>
        {
            t.Text = "Hello there,\n\nIt works.\n\nBest,\nAva";
            t.Rewrite = (r, _) =>
            {
                calls++;
                return Task.FromResult("Hi there,\n\nIt works now.\n\nBest,\nAva");
            };
            t.Debounce = TimeSpan.Zero;
        });
        try
        {
            calls.ShouldBe(0);
            tuner.Value = new ToneSettings(2, 1, ToneVoice.Neutral);
            Pump();
            calls.ShouldBe(1);
            tuner.Status.ShouldBe(ToneTunerStatus.Rewritten);
            tuner.Value = ToneSettings.Default;
            Pump();
            calls.ShouldBe(1);
            tuner.Status.ShouldBe(ToneTunerStatus.Original);
            tuner.DisplayedText.ShouldBe("Hello there,\n\nIt works.\n\nBest,\nAva");
            tuner.ChangedWords.ShouldBe(0);
            tuner.Announcement.ShouldBe("Showing the original draft.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Rewrite_Function_Gets_The_Draft_And_Instructions_And_Superseded_Requests_Are_Cancelled()
    {
        var requests = new List<(ToneRewriteRequest Request, CancellationToken Token, TaskCompletionSource<string> Answer)>();
        var (tuner, window) = Open(t =>
        {
            t.Text = "Hi Sam,\n\nThe fix is live.\n\nBest,\nAva";
            t.Rewrite = (request, token) =>
            {
                var answer = new TaskCompletionSource<string>();
                requests.Add((request, token, answer));
                return answer.Task;
            };
        });
        try
        {
            tuner.Value = new ToneSettings(0, 1, ToneVoice.Neutral);
            tuner.FlushDebounce().ShouldBeTrue();
            requests.Count.ShouldBe(1);
            requests[0].Request.Text.ShouldBe("Hi Sam,\n\nThe fix is live.\n\nBest,\nAva");
            requests[0].Request.Formality.ShouldBe(0);
            requests[0].Request.Detail.ShouldBe(1);
            requests[0].Request.Voice.ShouldBe(ToneVoice.Neutral);
            requests[0].Request.Instructions.ShouldBe(ToneTunerText.Instructions(tuner.Value));

            // A newer change cancels the request in flight; its late answer is ignored.
            tuner.Value = new ToneSettings(2, 1, ToneVoice.Neutral);
            requests[0].Token.IsCancellationRequested.ShouldBeTrue();
            tuner.FlushDebounce().ShouldBeTrue();
            requests.Count.ShouldBe(2);
            requests[0].Answer.SetResult("Hey Sam,\n\nThe fix is live.\n\nCheers,\nAva");
            Pump();
            tuner.Status.ShouldBe(ToneTunerStatus.Rewriting);

            requests[1].Answer.SetResult("Dear Sam,\n\nThe fix is now live.\n\nKind regards,\nAva");
            Pump();
            tuner.Status.ShouldBe(ToneTunerStatus.Rewritten);
            tuner.DisplayedText.ShouldBe("Dear Sam,\n\nThe fix is now live.\n\nKind regards,\nAva");
            tuner.Announcement.ShouldBe("Rewritten: Formal · Standard.");
            tuner.MessageView!.Highlights.Select(h => h.Text).ShouldBe(["Dear", "now", "Kind regards,"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Failure_Keeps_The_Text_Shows_Retry_And_Retry_Runs_It_Again()
    {
        var calls = 0;
        var (tuner, window) = Open(t =>
        {
            t.Text = "Hi Sam,\n\nThe fix is live.";
            t.Debounce = TimeSpan.Zero;
            t.Rewrite = (_, _) => ++calls == 1
                ? Task.FromException<string>(new InvalidOperationException("The model did not respond."))
                : Task.FromResult("Dear Sam,\n\nThe fix is live.");
        });
        try
        {
            tuner.Value = new ToneSettings(2, 1, ToneVoice.Neutral);
            Pump();
            tuner.Status.ShouldBe(ToneTunerStatus.Failed);
            tuner.StatusLabel.ShouldBe("Couldn't rewrite");
            tuner.DisplayedText.ShouldBe("Hi Sam,\n\nThe fix is live.");
            tuner.RetryButton!.IsVisible.ShouldBeTrue();
            tuner.RetryButton.Content.ShouldBe("Retry");
            tuner.Announcement.ShouldBe("Couldn't rewrite: The model did not respond. Press Retry to try again.");

            tuner.RetryButton.Focus();
            Press(window, PhysicalKey.Enter);
            calls.ShouldBe(2);
            tuner.Status.ShouldBe(ToneTunerStatus.Rewritten);
            tuner.DisplayedText.ShouldBe("Dear Sam,\n\nThe fix is live.");
            tuner.RetryButton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Changes_Switch_Hides_The_Highlights_And_The_Count()
    {
        var (tuner, window) = Open(t => t.Value = new ToneSettings(2, 2, ToneVoice.Confident));
        try
        {
            tuner.Status.ShouldBe(ToneTunerStatus.Rewritten);
            tuner.CountView!.IsVisible.ShouldBeTrue();
            tuner.ChangesSwitch!.IsChecked.ShouldBe(true);
            tuner.ChangesSwitch.Content.ShouldBe("Changes");

            tuner.ChangesSwitch.IsChecked = false;
            Pump();
            tuner.ShowChanges.ShouldBeFalse();
            tuner.CountView.IsVisible.ShouldBeFalse();
            tuner.MessageView!.Highlights.ShouldAllBe(h => h.Opacity == 0);

            tuner.ShowChanges = true;
            Pump();
            tuner.ChangesSwitch.IsChecked.ShouldBe(true);
            tuner.MessageView.Highlights.ShouldAllBe(h => h.Opacity == 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sliders_Step_Between_Levels_With_The_Keyboard()
    {
        var (tuner, window) = Open();
        try
        {
            var thumb = tuner.FormalitySlider!.GetVisualDescendants().OfType<InputElement>().First(e => e.Focusable);
            thumb.Focus();
            Press(window, PhysicalKey.ArrowRight);
            tuner.Value.Formality.ShouldBe(2);
            Press(window, PhysicalKey.Home);
            tuner.Value.Formality.ShouldBe(0);
            Press(window, PhysicalKey.End);
            tuner.Value.Formality.ShouldBe(2);
            Press(window, PhysicalKey.ArrowLeft);
            tuner.Value.ShouldBe(ToneSettings.Default);
            tuner.Status.ShouldBe(ToneTunerStatus.Original);

            var detail = tuner.DetailSlider!.GetVisualDescendants().OfType<InputElement>().First(e => e.Focusable);
            detail.Focus();
            Press(window, PhysicalKey.ArrowLeft);
            tuner.Value.ShouldBe(new ToneSettings(1, 0, ToneVoice.Neutral));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chips_Are_Named_Toggles_In_A_Voice_Group_And_Space_Presses_Them()
    {
        var (tuner, window) = Open();
        try
        {
            var chip = Chip(tuner, ToneVoice.Confident);
            AutomationProperties.GetName(chip).ShouldBe("Confident");
            AutomationProperties.GetName(tuner.GetVisualDescendants().OfType<ToneTunerChipRow>().Single()).ShouldBe("Voice");
            chip.Focus();
            Press(window, PhysicalKey.Space);
            tuner.Value.ShouldBe(new ToneSettings(1, 0, ToneVoice.Confident));
            chip.IsChecked.ShouldBe(true);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Replace_Every_Word()
    {
        var (tuner, window) = Open(t => t.Labels = new ToneTunerLabels
        {
            Voice = "Stimme",
            Formality = "Ton",
            FormalityLevels = ["Locker", "Neutral", "Förmlich"],
            VoiceName = v => v == ToneVoice.Friendly ? "Freundlich" : v.ToString(),
            Changes = "Änderungen",
            OriginalDraft = "Entwurf",
        });
        try
        {
            tuner.StatusLabel.ShouldBe("Entwurf");
            tuner.FormalitySlider!.Label.ShouldBe("Ton");
            tuner.FormalitySlider.FormatValue(0).ShouldBe("Locker");
            tuner.ChangesSwitch!.Content.ShouldBe("Änderungen");
            Chip(tuner, ToneVoice.Friendly).Label.ShouldBe("Freundlich");
            AutomationProperties.GetName(tuner.GetVisualDescendants().OfType<ToneTunerChipRow>().Single()).ShouldBe("Stimme");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Custom_Presets_Replace_The_Chips()
    {
        var (tuner, window) = Open(t => t.Presets =
        [
            new TonePreset { Voice = ToneVoice.Confident, Label = "Direct", Formality = 2, Detail = 0 },
        ]);
        try
        {
            tuner.ChipViews.Count.ShouldBe(1);
            tuner.ChipViews[0].Label.ShouldBe("Direct");
            tuner.ChipViews[0].ShowsIcon.ShouldBeFalse();
            Click(tuner.ChipViews[0]);
            tuner.Value.ShouldBe(new ToneSettings(2, 0, ToneVoice.Confident));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Stack_The_Sliders_And_Drop_Icons_And_Labels()
    {
        var (tuner, window) = Open(width: 360);
        try
        {
            tuner.Bounds.Width.ShouldBe(360, 0.5);
            tuner.IsStacked.ShouldBeTrue();
            tuner.IsCompact.ShouldBeTrue();
            tuner.GetVisualDescendants().OfType<ToneTunerSliderRow>().Single().IsStacked.ShouldBeTrue();
            tuner.GetVisualDescendants().OfType<ToneTunerChipRow>().Single().IsUniform.ShouldBeTrue();
            tuner.ChipViews.ShouldAllBe(c => !c.ShowsIcon);
            tuner.CopyView!.IsIconOnly.ShouldBeTrue();
            // Equal columns that stay inside the card.
            var widths = tuner.ChipViews.Select(c => c.Bounds.Width).ToList();
            widths.Max().ShouldBe(widths.Min(), 0.5);
            tuner.FormalitySlider!.Bounds.Width.ShouldBe(tuner.DetailSlider!.Bounds.Width, 0.5);
            tuner.DetailSlider.Bounds.Y.ShouldBeGreaterThan(tuner.FormalitySlider.Bounds.Y);

            window.Width = 460;
            Pump();
            tuner.IsStacked.ShouldBeTrue();
            tuner.IsCompact.ShouldBeFalse();
            tuner.ChipViews.ShouldAllBe(c => c.ShowsIcon);

            window.Width = 640;
            Pump();
            tuner.IsStacked.ShouldBeFalse();
            tuner.FormalitySlider.Bounds.Y.ShouldBe(tuner.DetailSlider.Bounds.Y, 0.5);
            tuner.CopyView.IsIconOnly.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_Voice_Never_Changes_The_Message_Height()
    {
        var (tuner, window) = Open(t => t.Value = new ToneSettings(1, 2, ToneVoice.Neutral));
        try
        {
            var message = tuner.MessageView!;
            var heights = new List<double>();
            foreach (var voice in new[] { ToneVoice.Friendly, ToneVoice.Confident, ToneVoice.Empathetic, ToneVoice.Neutral })
            {
                tuner.Value = tuner.Value.WithVoice(voice);
                tuner.FlushDebounce();
                Pump();
                tuner.DisplayedText.ShouldBe(ToneTunerFixture.Compose(tuner.Value));
                heights.Add(message.Bounds.Height);
            }
            heights.Distinct().Count().ShouldBe(1);

            // A detail change reserves a different height.
            tuner.Value = tuner.Value.WithDetail(0);
            Pump();
            tuner.FlushDebounce();
            Pump();
            message.Bounds.Height.ShouldBeLessThan(heights[0]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Changed_Sentences_Fade_Out_Swap_Together_And_Fade_In_While_The_Rest_Stay_Still()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (tuner, window) = Open(t => t.AutoPlay = false, level: ArcMotionLevel.Full);
        try
        {
            var message = tuner.MessageView!;
            var before = message.Sentences.Select(s => s.Text).ToList();
            tuner.Value = ToneSettings.Default.WithVoice(ToneVoice.Confident);
            tuner.FlushDebounce();
            Dispatcher.UIThread.RunJobs();
            message.IsSwapping.ShouldBeTrue();
            var target = ToneTunerFixture.Compose(tuner.Value);

            clock.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
            var mid = message.Sentences;
            // Still the old text: changed sentences are fading, unchanged ones untouched.
            mid.Select(s => s.Text).ShouldBe(before);
            mid.Single(s => s.Text.StartsWith("Thank you for letting us know")).Alpha.ShouldBeInRange(0.01, 0.99);
            mid.Single(s => s.Text.StartsWith("A payment retry")).Alpha.ShouldBe(1);

            clock.Advance(TimeSpan.FromMilliseconds(100), Frame);
            Dispatcher.UIThread.RunJobs();
            // Swapped on one frame: the new text is laid out, the new sentences start invisible.
            string.Join("\n\n", message.ParagraphTexts).ShouldBe(target);
            message.Sentences.Single(s => s.Text.StartsWith("You're right")).Alpha.ShouldBeLessThan(1);
            message.Sentences.Single(s => s.Text.StartsWith("A payment retry")).Alpha.ShouldBe(1);

            clock.Advance(TimeSpan.FromMilliseconds(200), Frame);
            Dispatcher.UIThread.RunJobs();
            message.IsSwapping.ShouldBeFalse();
            message.Sentences.ShouldAllBe(s => s.Alpha == 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Swaps_At_Once_And_Never_Tours()
    {
        var (tuner, window) = Open(level: ArcMotionLevel.Reduced);
        try
        {
            tuner.EffectiveAutoPlay.ShouldBeTrue();
            tuner.IsTouring.ShouldBeFalse();
            tuner.Value = new ToneSettings(0, 1, ToneVoice.Friendly);
            tuner.FlushDebounce();
            Pump();
            tuner.MessageView!.IsSwapping.ShouldBeFalse();
            string.Join("\n\n", tuner.MessageView.ParagraphTexts).ShouldBe(ToneTunerFixture.Compose(tuner.Value));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tour_Walks_The_Voices_And_Stops_At_The_First_Press()
    {
        var (tuner, window) = Open();
        try
        {
            tuner.StepTour();
            tuner.Value.ShouldBe(new ToneSettings(0, 1, ToneVoice.Friendly));
            tuner.StepTour();
            tuner.Value.Voice.ShouldBe(ToneVoice.Confident);
            tuner.StepTour();
            tuner.Value.Voice.ShouldBe(ToneVoice.Empathetic);
            tuner.StepTour();
            tuner.Value.ShouldBe(ToneSettings.Default);

            tuner.TourStopped.ShouldBeFalse();
            Chip(tuner, ToneVoice.Friendly).Focus();
            Pump();
            tuner.TourStopped.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Draft_Of_Your_Own_Wins_Over_The_Simulation_And_Turns_The_Tour_Off()
    {
        var (tuner, window) = Open(t =>
        {
            t.Text = "Hi Sam,\n\nAll good.";
            t.RecipientName = "Sam Park";
        });
        try
        {
            tuner.PlayingSimulation.ShouldBeNull();
            tuner.EffectiveAutoPlay.ShouldBeFalse();
            tuner.DisplayedText.ShouldBe("Hi Sam,\n\nAll good.");
            TestHelpers.Descendant<TextBlock>(tuner, ToneTuner.NamePart).Text.ShouldBe("Sam Park");
            TestHelpers.Descendant<TextBlock>(tuner, ToneTuner.SubjectPart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Text_And_No_Simulation_Shows_The_Empty_Card_With_The_Dials_Off()
    {
        var (tuner, window) = Open(simulate: false);
        try
        {
            tuner.IsSimulated.ShouldBeFalse();
            tuner.HasDraft.ShouldBeFalse();
            tuner.EffectiveAutoPlay.ShouldBeFalse();
            tuner.IsTouring.ShouldBeFalse();
            tuner.DisplayedText.ShouldBe(string.Empty);
            tuner.Status.ShouldBe(ToneTunerStatus.Original);
            TestHelpers.Descendant<TextBlock>(tuner, ToneTuner.NamePart).IsVisible.ShouldBeFalse();
            TestHelpers.Descendant<Avatar>(tuner, ToneTuner.AvatarPart).IsVisible.ShouldBeFalse();
            tuner.GetVisualDescendants().OfType<ToneTunerChipRow>().Single().IsEnabled.ShouldBeFalse();
            tuner.GetVisualDescendants().OfType<ToneTunerSliderRow>().Single().IsEnabled.ShouldBeFalse();
            tuner.ChangesSwitch!.IsEnabled.ShouldBeFalse();
            tuner.CopyView!.IsEnabled.ShouldBeFalse();
            // A tone set from code asks nothing: there is nothing to rewrite.
            tuner.Value = new ToneSettings(2, 1, ToneVoice.Neutral);
            Pump();
            tuner.IsWaitingForDebounce.ShouldBeFalse();
            tuner.Status.ShouldBe(ToneTunerStatus.Original);
            tuner.StatusLabel.ShouldBe("Original draft");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Plays_Its_Draft_Recipient_Model_And_Versions_And_Tours()
    {
        var (tuner, window) = Open(simulate: false);
        try
        {
            tuner.Simulation = Demo();
            Pump();
            tuner.IsSimulated.ShouldBeTrue();
            tuner.EffectiveAutoPlay.ShouldBeTrue();
            tuner.DisplayedText.ShouldBe(ToneTunerFixture.Draft);
            TestHelpers.Descendant<TextBlock>(tuner, ToneTuner.NamePart).Text.ShouldBe(ToneTunerFixture.RecipientName);
            tuner.CopyView!.IsEnabled.ShouldBeTrue();

            tuner.StepTour();
            tuner.FlushDebounce();
            Pump();
            tuner.Status.ShouldBe(ToneTunerStatus.Rewritten);
            tuner.DisplayedText.ShouldBe(ToneTunerFixture.Compose(new ToneSettings(0, 1, ToneVoice.Friendly)));
            tuner.Announcement.ShouldEndWith("Sample rewrite, nothing was sent.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Your_Text_And_Rewrite_Win_Over_The_Simulation()
    {
        var requests = new List<ToneRewriteRequest>();
        var (tuner, window) = Open(t =>
        {
            t.Text = "Hi Sam,\n\nThe fix is live.";
            t.Debounce = TimeSpan.Zero;
            t.Rewrite = (request, _) =>
            {
                requests.Add(request);
                return Task.FromResult("Dear Sam,\n\nThe fix is live.");
            };
        });
        try
        {
            tuner.Value = new ToneSettings(2, 1, ToneVoice.Neutral);
            Pump();
            requests.Count.ShouldBe(1);
            requests[0].Text.ShouldBe("Hi Sam,\n\nThe fix is live.");
            tuner.DisplayedText.ShouldBe("Dear Sam,\n\nThe fix is live.");
            // The simulation's model and recipient do not leak into your card.
            tuner.EffectiveModel.ShouldBe(string.Empty);
            tuner.EffectiveAutoPlay.ShouldBeFalse();
            TestHelpers.Descendant<TextBlock>(tuner, ToneTuner.NamePart).IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Draft_Of_Your_Own_Without_Rewrite_Is_Simulated_With_Plain_Rules()
    {
        var (tuner, window) = Open(t => t.Text = "Hi Sam,\n\nThe fix is live.", simulate: false);
        try
        {
            tuner.IsSimulated.ShouldBeTrue();
            tuner.EffectiveAutoPlay.ShouldBeFalse();
            tuner.Value = new ToneSettings(2, 1, ToneVoice.Neutral);
            tuner.FlushDebounce().ShouldBeTrue();
            Pump();
            tuner.Status.ShouldBe(ToneTunerStatus.Rewritten);
            tuner.DisplayedText.ShouldBe(ToneTunerSimulation.Rules("Hi Sam,\n\nThe fix is live.", new ToneSettings(2, 1, ToneVoice.Neutral)));
            tuner.DisplayedText.ShouldStartWith("Dear Sam,");
            tuner.Announcement.ShouldEndWith("Sample rewrite, nothing was sent.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Simulation_To_Null_Stops_Playback_And_Shows_The_Empty_Card()
    {
        var (tuner, window) = Open(t => t.Simulation!.Latency = TimeSpan.FromSeconds(30));
        try
        {
            tuner.EffectiveAutoPlay.ShouldBeTrue();
            tuner.Value = new ToneSettings(0, 1, ToneVoice.Friendly);
            tuner.FlushDebounce();
            Pump();
            tuner.Status.ShouldBe(ToneTunerStatus.Rewriting);

            tuner.Simulation = null;
            Pump();
            tuner.IsSimulated.ShouldBeFalse();
            tuner.EffectiveAutoPlay.ShouldBeFalse();
            tuner.IsTouring.ShouldBeFalse();
            tuner.Status.ShouldBe(ToneTunerStatus.Original);
            tuner.DisplayedText.ShouldBe(string.Empty);
            TestHelpers.Descendant<TextBlock>(tuner, ToneTuner.NamePart).IsVisible.ShouldBeFalse();
            tuner.CopyView!.IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>Invented test data for the ToneTuner tests (people, companies and numbers are made up).</summary>
file static class ToneTunerFixture
{
    public const string RecipientName = "Sofia Ramirez";

    public const string RecipientSubject = "Re: Duplicate charge on INV-20481";

    /// <summary>The model the status names while a rewrite runs.</summary>
    public static string Model => "Claude Sonnet 5.5";

    /// <summary>The draft the session starts from: the reply at the starting tone.</summary>
    public static string Draft { get; } = Compose(ToneSettings.Default);

    /// <summary>The reply in every other tone (3 formalities × 3 details × 4 voices, the starting tone left out).</summary>
    public static IReadOnlyList<ToneTunerVersion> Versions { get; } =
    [
        .. from f in Enumerable.Range(0, 3)
           from d in Enumerable.Range(0, 3)
           from v in new[] { ToneVoice.Neutral, ToneVoice.Friendly, ToneVoice.Confident, ToneVoice.Empathetic }
           let settings = new ToneSettings(f, d, v)
           where settings != ToneSettings.Default
           select new ToneTunerVersion(settings, Compose(settings)),
    ];

    /// <summary>The reply in <paramref name="value"/>'s tone.</summary>
    public static string Compose(ToneSettings value)
    {
        var f = Math.Clamp(value.Formality, 0, 2);
        var d = Math.Clamp(value.Detail, 0, 2);
        var v = value.Voice;
        var paragraphs = new List<string> { Greeting(f, v) };
        var body = new List<string?> { Acknowledge(f, v), d >= 1 ? Cause(f, d) : null, Refund(f, v), Timing(f, d), d == 2 ? Prevention(f) : null };
        paragraphs.Add(string.Join(" ", body.Where(sentence => sentence is not null)));
        if (d >= 1)
        {
            var closer = Closer(f, v);
            if (v == ToneVoice.Empathetic)
            {
                closer += " " + Patience(f);
            }
            paragraphs.Add(closer);
        }
        paragraphs.Add(SignOff(f, v));
        return string.Join("\n\n", paragraphs);
    }

    private static string Greeting(int f, ToneVoice v) => (f, v) switch
    {
        (2, _) => "Dear Ms. Ramirez,",
        (_, ToneVoice.Friendly) => "Hi Sofia!",
        (0, _) => "Hey Sofia,",
        _ => "Hi Sofia,",
    };

    private static string Acknowledge(int f, ToneVoice v) => (v, f) switch
    {
        (ToneVoice.Friendly, 0) => "Thanks so much for flagging the double charge on INV-20481!",
        (ToneVoice.Friendly, 1) => "Thanks so much for letting us know about the duplicate charge on INV-20481.",
        (ToneVoice.Friendly, _) => "Thank you very much for bringing the duplicate charge on invoice INV-20481 to our attention.",
        (ToneVoice.Confident, 0) => "You're right, INV-20481 was charged twice.",
        (ToneVoice.Confident, 1) => "You're right: INV-20481 was charged twice.",
        (ToneVoice.Confident, _) => "You are correct that invoice INV-20481 was charged twice.",
        (ToneVoice.Empathetic, 0) => "I'm so sorry you got charged twice for INV-20481.",
        (ToneVoice.Empathetic, 1) => "I'm sorry you were charged twice for INV-20481, and I understand how frustrating that is.",
        (ToneVoice.Empathetic, _) => "I am sorry that you were charged twice for invoice INV-20481, and I understand how frustrating this must be.",
        (_, 0) => "Thanks for flagging the double charge on INV-20481.",
        (_, 1) => "Thank you for letting us know about the duplicate charge on INV-20481.",
        _ => "Thank you for bringing the duplicate charge on invoice INV-20481 to our attention.",
    };

    private static string Cause(int f, int d) => (f, d) switch
    {
        (0, 1) => "A payment retry on our side went through twice.",
        (0, _) => "A payment retry on our side went through twice after our processor timed out on March 3.",
        (1, 1) => "A payment retry on our side went through twice after a timeout.",
        (1, _) => "A payment retry on our side went through twice after our processor timed out on March 3.",
        (_, 1) => "A payment retry on our side was processed twice following a timeout.",
        _ => "A payment retry on our side was processed twice after our payment processor timed out on March 3.",
    };

    private static string Refund(int f, ToneVoice v) => (v == ToneVoice.Confident, f) switch
    {
        (true, 0) => "We've already refunded the extra $249.00 to your card ending in 4417.",
        (true, 1) => "We've already refunded the duplicate $249.00 to your card ending in 4417.",
        (true, _) => "The duplicate $249.00 has already been refunded to your card ending in 4417.",
        (_, 0) => "We've refunded the extra $249.00 to your card ending in 4417.",
        (_, 1) => "We have refunded the duplicate $249.00 to your card ending in 4417.",
        _ => "We have issued a full refund of the duplicate $249.00 to your card ending in 4417.",
    };

    private static string Timing(int f, int d) => (d, f) switch
    {
        (0, 0) => "It'll show up in 5–7 business days.",
        (0, 1) => "Expect it within 5–7 business days.",
        (0, _) => "Please allow 5–7 business days.",
        (1, 0) => "It should show up within 5–7 business days.",
        (1, _) => "It should appear on your statement within 5–7 business days.",
        (_, 0) => "It should show up within 5–7 business days, depending on your bank.",
        (_, 1) => "It should appear on your statement within 5–7 business days, depending on your bank.",
        _ => "It should appear on your statement within 5–7 business days, depending on your bank's processing time.",
    };

    private static string Prevention(int f) => f switch
    {
        0 => "We've also added a check so a retry can't charge you twice again.",
        1 => "We've also added a safeguard so a retried payment can't be charged twice again.",
        _ => "We have also added a safeguard to ensure that a retried payment cannot be charged twice in future.",
    };

    private static string Closer(int f, ToneVoice v) => (v, f) switch
    {
        (ToneVoice.Friendly, 0) => "If anything still looks off, just reply here and I'll sort it out!",
        (ToneVoice.Friendly, 1) => "If anything still looks off, just reply here and I'll happily sort it out.",
        (ToneVoice.Friendly, _) => "Should anything else look incorrect, please reply to this email and I will gladly look into it personally.",
        (ToneVoice.Confident, 0) => "If anything still looks off, reply here and I'll fix it the same day.",
        (ToneVoice.Confident, 1) => "If anything still looks off, reply to this email and I'll fix it the same day.",
        (ToneVoice.Confident, _) => "Should anything else look incorrect, reply to this email and I will resolve it the same day.",
        (ToneVoice.Empathetic, 0) => "If anything still looks off, just reply here, I'm happy to help.",
        (ToneVoice.Empathetic, 1) => "If anything still looks off, reply to this email and I'll make sure it's put right.",
        (ToneVoice.Empathetic, _) => "Should anything else look incorrect, please reply to this email and I will make sure it is put right.",
        (_, 0) => "If anything still looks off, just reply here.",
        (_, 1) => "If anything still looks off, reply to this email and I'll take care of it.",
        _ => "Should anything else look incorrect, please reply to this email and I will look into it personally.",
    };

    private static string Patience(int f) => f switch
    {
        0 => "Thanks for bearing with us.",
        1 => "Thank you for your patience.",
        _ => "Thank you for your patience while we resolved this.",
    };

    private static string SignOff(int f, ToneVoice v) => (f, v) switch
    {
        (2, _) => "Kind regards,\nMaya Chen\nBilling Support",
        (_, ToneVoice.Friendly) or (0, _) => "Cheers,\nMaya",
        (_, ToneVoice.Empathetic) => "Warm regards,\nMaya",
        _ => "Best,\nMaya",
    };
}
