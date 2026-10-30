using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Special.Ai;

/// <summary>
/// AiTranslate: the text helpers (sentences, detection, chunks), the simulation it plays (an invented fixture), the
/// empty card, streaming over the dimmed text
/// with one old sentence retiring per new sentence, the cache and Show original, failures and Retry, an app translator
/// with cancellation, the byline, the breakpoints, the menu's keyboard model and the fades at full motion.
/// </summary>
public class AiTranslateTests
{
    private const string English =
        "Version 4.2 is out. PDF exports are now twice as fast, and tables keep their formatting. Thanks to everyone who reported bugs this week.";

    /// <summary>The invented message: the French release note and its translations.</summary>
    private static AiTranslateSimulation Demo() => new()
    {
        Text = AiTranslateFixture.Text,
        Translations = AiTranslateFixture.Translations,
        Author = AiTranslateFixture.Author,
        Timestamp = AiTranslateFixture.Timestamp,
        ModelName = AiTranslateFixture.ModelName,
        Tour = AiTranslateFixture.Tour,
    };

    /// <summary>A card playing the fixture message (pass <paramref name="simulate"/> false for a card with no data).</summary>
    private static (AiTranslate Card, Window Window) Open(Action<AiTranslate>? setup = null, double width = 700,
        ArcMotionLevel level = ArcMotionLevel.Off, bool simulate = true)
    {
        var card = new AiTranslate { AutoPlay = false, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
        if (simulate)
        {
            card.Simulation = Demo();
        }
        setup?.Invoke(card);
        var window = TestHelpers.Show(card, level);
        window.Width = width;
        window.Height = 700;
        Pump();
        return (card, window);
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

    // ---- Pure text helpers ----

    [Fact]
    public void Sentences_Keep_Decimals_And_Join_Back()
    {
        var text = "La version 4.2 est disponible. Les exports sont rapides! Merci?  Fin";
        var sentences = AiTranslateText.Sentences(text);
        sentences.Count.ShouldBe(4);
        sentences[0].ShouldBe("La version 4.2 est disponible. ");
        string.Concat(sentences).ShouldBe(text);
        AiTranslateText.StartedSentences("One. Two").ShouldBe(2);
        AiTranslateText.StartedSentences("One. ").ShouldBe(1);
        AiTranslateText.StartedSentences(string.Empty).ShouldBe(0);
    }

    [Fact]
    public void Japanese_Sentences_End_Without_A_Space()
    {
        var sentences = AiTranslateText.Sentences("バージョン4.2を公開しました。表の書式も保たれます！ありがとう？");
        sentences.Count.ShouldBe(3);
        sentences[0].ShouldBe("バージョン4.2を公開しました。");
    }

    [Theory]
    [InlineData(AiTranslateFixture.Text, AiTranslateLanguage.French)]
    [InlineData("Hallo zusammen, der Export hängt seit dem Update bei 80 Prozent.", AiTranslateLanguage.German)]
    [InlineData("Hemos actualizado los precios del plan. Los clientes mantienen su tarifa hasta enero.", AiTranslateLanguage.Spanish)]
    [InlineData("Atualizamos os preços do plano. Os clientes atuais mantêm sua tarifa até janeiro.", AiTranslateLanguage.Portuguese)]
    [InlineData("明日の定例ミーティングは10時開始に変更になりました。", AiTranslateLanguage.Japanese)]
    [InlineData("Thanks to everyone who reported bugs this week.", AiTranslateLanguage.English)]
    [InlineData("", AiTranslateLanguage.English)]
    public void Detects_The_Language(string text, AiTranslateLanguage expected) =>
        AiTranslateText.Detect(text).ShouldBe(expected);

    [Fact]
    public void Chunks_Are_Whole_Words_In_Turn()
    {
        var pending = new StringBuilder("one two three four five six seven eight nine ten eleven");
        AiTranslateText.TakeChunk(pending, final: false, cjk: false, 0, out var first).ShouldBeTrue();
        first.ShouldBe("one two three four ");
        AiTranslateText.TakeChunk(pending, final: false, cjk: false, 1, out var second).ShouldBeTrue();
        second.ShouldBe("five six seven ");
        // "eleven" may still be arriving: without a space after it, it waits.
        AiTranslateText.TakeChunk(pending, final: false, cjk: false, 3, out var third).ShouldBeTrue();
        third.ShouldBe("eight nine ten ");
        AiTranslateText.TakeChunk(pending, final: false, cjk: false, 4, out _).ShouldBeFalse();
        AiTranslateText.TakeChunk(pending, final: true, cjk: false, 4, out var last).ShouldBeTrue();
        last.ShouldBe("eleven");
        pending.Length.ShouldBe(0);
    }

    [Fact]
    public void Japanese_Chunks_Are_About_Ten_Characters()
    {
        var pending = new StringBuilder("バージョン4.2を公開しました。PDFの書き出しが2倍速くなり、表の書式も保たれるようになりました。");
        AiTranslateText.TakeChunk(pending, final: false, cjk: true, 0, out var chunk).ShouldBeTrue();
        chunk.Length.ShouldBeInRange(10, 11);
        var rest = new StringBuilder("短い");
        AiTranslateText.TakeChunk(rest, final: false, cjk: true, 0, out _).ShouldBeFalse();
        AiTranslateText.TakeChunk(rest, final: true, cjk: true, 0, out var tail).ShouldBeTrue();
        tail.ShouldBe("短い");
    }

    [Fact]
    public void Separator_Adds_A_Space_Only_Between_Words()
    {
        AiTranslateText.Separator("Version 4.2 is out.", "Les exports").ShouldBe(" ");
        AiTranslateText.Separator("Version 4.2 is out. ", "Les exports").ShouldBe(string.Empty);
        AiTranslateText.Separator("公開しました。", "表の書式").ShouldBe(string.Empty);
        AiTranslateText.Separator(string.Empty, "Les exports").ShouldBe(string.Empty);
    }

    // ---- The card ----

    [AvaloniaFact]
    public void Opens_On_The_Simulated_French_Message()
    {
        var (card, window) = Open();
        try
        {
            card.State.ShouldBe(AiTranslateState.Original);
            card.EffectiveSourceLanguage.ShouldBe(AiTranslateLanguage.French);
            card.Paragraph!.DisplayedText.ShouldBe(AiTranslateFixture.Text);
            card.Chip!.Content.ShouldBe("Detected · French");
            card.StatusLabel!.Text.ShouldBe(AiTranslateFixture.ModelName);
            card.PickerLabel!.Text.ShouldBe("Translate");
            card.ToggleLabel!.Text.ShouldBe("Show original");
            card.ToggleButton!.IsEnabled.ShouldBeFalse();
            card.RetryButton!.IsVisible.ShouldBeFalse();
            card.StatusLabel!.Glyph.ShouldBe(ArcIcons.Sparkles);
            AutomationProperties.GetName(card).ShouldBe("Message from Camille Laurent");
            card.MenuEntries.Count.ShouldBe(6);
            card.MenuEntries.Single(e => e.IsChecked).Tag.ShouldBe(AiTranslateLanguage.French);
            card.MenuEntries.Single(e => Equals(e.Tag, AiTranslateLanguage.French)).InputGestureText.ShouldBe("Original");
            card.MenuEntries.Select(e => e.Header as string).ToArray().ShouldBe(new[] { "English", "Deutsch", "Français", "Español", "日本語", "Português" });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streams_In_Place_Retiring_One_Old_Sentence_Per_New_One()
    {
        var (card, window) = Open();
        try
        {
            card.Pick(AiTranslateLanguage.English);
            card.State.ShouldBe(AiTranslateState.Translating);
            card.StatusLabel!.Text.ShouldBe("Translating to English");
            card.PickerLabel!.Text.ShouldBe("English");
            var paragraph = card.Paragraph!;
            paragraph.IsStreaming.ShouldBeTrue();
            paragraph.RemainingOldSentences.ShouldBe(3);
            paragraph.CurrentDim.ShouldBe(0.45, 0.001);

            // The simulated model answers after its latency, then a chunk lands every 34ms.
            card.Advance(AiTranslate.SimulatedLatency + 0.06);
            paragraph.StreamedText.ShouldBe("Version 4.2 is out. ");
            paragraph.RemainingOldSentences.ShouldBe(2);
            paragraph.DisplayedText.ShouldStartWith("Version 4.2 is out. Les exports PDF");

            card.Advance(0.07);
            paragraph.StreamedText.ShouldBe("Version 4.2 is out. PDF exports are now twice as fast, and ");
            paragraph.RemainingOldSentences.ShouldBe(1);
            paragraph.DisplayedText.ShouldEndWith("Merci à toutes celles et ceux qui ont signalé des bugs cette semaine.");

            card.Advance(1);
            card.State.ShouldBe(AiTranslateState.Translated);
            paragraph.IsStreaming.ShouldBeFalse();
            paragraph.DisplayedText.ShouldBe(English);
            card.StatusLabel.Text.ShouldBe("Translated from French");
            card.ToggleButton!.IsEnabled.ShouldBeTrue();
            card.IsCached(AiTranslateLanguage.English).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Raises_Events_With_Their_Arguments()
    {
        var picks = new List<AiTranslateValueChangedEventArgs>();
        var done = new List<AiTranslateCompletedEventArgs>();
        var (card, window) = Open(c =>
        {
            c.ValueChanged += (_, e) => picks.Add(e);
            c.Translated += (_, e) => done.Add(e);
        });
        try
        {
            card.Pick(AiTranslateLanguage.German);
            card.Advance(3);
            picks.Count.ShouldBe(1);
            picks[0].Language.ShouldBe(AiTranslateLanguage.German);
            picks[0].IsOriginal.ShouldBeFalse();
            done.Count.ShouldBe(1);
            done[0].From.ShouldBe(AiTranslateLanguage.French);
            done[0].To.ShouldBe(AiTranslateLanguage.German);
            done[0].Text.ShouldStartWith("Version 4.2 ist da.");
            card.Value.ShouldBe(AiTranslateLanguage.German);

            // Setting Value from code is not a reader's pick.
            card.Value = AiTranslateLanguage.French;
            picks.Count.ShouldBe(1);
            card.State.ShouldBe(AiTranslateState.Original);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Show_Original_Toggles_Through_The_Cache()
    {
        var picks = new List<AiTranslateValueChangedEventArgs>();
        var (card, window) = Open(c => c.ValueChanged += (_, e) => picks.Add(e));
        try
        {
            card.Pick(AiTranslateLanguage.English);
            card.Advance(3);
            card.ToggleButton!.Focus();
            Press(window, PhysicalKey.Enter);
            card.State.ShouldBe(AiTranslateState.Original);
            card.Paragraph!.DisplayedText.ShouldBe(AiTranslateFixture.Text);
            card.StatusLabel!.Text.ShouldBe("Showing original");
            card.ToggleLabel!.Text.ShouldBe("Show translation");
            AutomationProperties.GetItemStatus(card.ToggleButton).ShouldBe("pressed");
            picks[^1].IsOriginal.ShouldBeTrue();
            picks[^1].Language.ShouldBe(AiTranslateLanguage.French);

            Press(window, PhysicalKey.Enter);
            // Cached: no stream, straight back.
            card.State.ShouldBe(AiTranslateState.Translated);
            card.Paragraph.IsStreaming.ShouldBeFalse();
            card.Paragraph.DisplayedText.ShouldBe(English);
            card.ToggleLabel.Text.ShouldBe("Show original");
            picks[^1].Language.ShouldBe(AiTranslateLanguage.English);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Missing_Translation_Fails_And_Retries()
    {
        var failures = new List<AiTranslateFailedEventArgs>();
        var (card, window) = Open(c =>
        {
            c.Text = "Hemos actualizado los precios del plan Equipo. Los clientes actuales mantienen su tarifa hasta enero.";
            c.SourceLanguage = AiTranslateLanguage.Spanish;
            c.Author = null;
            c.Translations = new Dictionary<AiTranslateLanguage, string> { [AiTranslateLanguage.English] = "We updated the prices." };
            c.TranslationFailed += (_, e) => failures.Add(e);
        });
        try
        {
            card.Chip!.Content.ShouldBe("Written in Spanish");
            card.Classes.Contains(AiTranslate.NoAuthorPseudoClass).ShouldBeTrue();
            AutomationProperties.GetName(card).ShouldBe("Message");

            card.Pick(AiTranslateLanguage.German);
            card.Advance(1);
            card.State.ShouldBe(AiTranslateState.Failed);
            card.Paragraph!.DisplayedText.ShouldStartWith("Hemos actualizado");
            card.Paragraph.IsStreaming.ShouldBeFalse();
            card.StatusLabel!.Text.ShouldBe("Couldn't translate");
            card.StatusLabel.Glyph.ShouldBe(ArcIcons.TriangleAlert);
            card.StatusLabel.GlyphColor.ShouldBe(TKCode.Arc.DesignTokens.ArcDesignTokenKind.Warning);
            card.RetryButton!.IsVisible.ShouldBeTrue();
            card.FailureMessage.ShouldBe("No German translation is available.");
            Tooltip.GetTip(card.StatusPanel!).ShouldBe("No German translation is available.");
            failures.Count.ShouldBe(1);
            failures[0].To.ShouldBe(AiTranslateLanguage.German);
            failures[0].Exception.ShouldBeNull();

            // Picking the language that just failed retries it.
            card.Pick(AiTranslateLanguage.German);
            card.State.ShouldBe(AiTranslateState.Translating);
            card.Advance(1);
            failures.Count.ShouldBe(2);

            // Another language clears the failure.
            card.Pick(AiTranslateLanguage.English);
            card.Advance(2);
            card.State.ShouldBe(AiTranslateState.Translated);
            card.Paragraph.DisplayedText.ShouldBe("We updated the prices.");
            card.RetryButton.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_App_Translator_Streams_And_Is_Cancelled_On_Switch()
    {
        var requests = new List<AiTranslateRequest>();
        var failures = 0;
        var gate = new TaskCompletionSource();
        async IAsyncEnumerable<string> Translate(AiTranslateRequest request, [EnumeratorCancellation] CancellationToken token = default)
        {
            requests.Add(request);
            yield return "Hello ";
            if (request.To == AiTranslateLanguage.German)
            {
                await gate.Task.WaitAsync(request.CancellationToken);
            }
            await Task.Yield();
            yield return "everyone.";
        }

        var (card, window) = Open(c =>
        {
            c.Text = "Bonjour à tous, merci pour vos retours.";
            c.Translator = r => Translate(r);
            c.TranslationFailed += (_, _) => failures++;
        });
        try
        {
            card.EffectiveSimulated.ShouldBeFalse();
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.Pick(AiTranslateLanguage.German);
            Pump();
            requests.Count.ShouldBe(1);
            requests[0].Text.ShouldBe("Bonjour à tous, merci pour vos retours.");
            requests[0].From.ShouldBe(AiTranslateLanguage.French);
            card.Advance(0.1);
            card.Paragraph!.StreamedText.ShouldBe("Hello ");

            // Switching cancels the first request; it never counts as a failure.
            card.Pick(AiTranslateLanguage.English);
            Pump();
            requests[0].CancellationToken.IsCancellationRequested.ShouldBeTrue();
            TestHelpers.PumpUntil(() =>
            {
                card.Advance(0.05);
                return card.State == AiTranslateState.Translated;
            }).ShouldBeTrue();
            card.Paragraph.DisplayedText.ShouldBe("Hello everyone.");
            card.ShownLanguage.ShouldBe(AiTranslateLanguage.English);
            failures.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Translator_Exception_Is_The_Failure_Message()
    {
        AiTranslateFailedEventArgs? failure = null;
        static async IAsyncEnumerable<string> Broken(AiTranslateRequest request)
        {
            await Task.Yield();
            yield return "Half ";
            throw new InvalidOperationException("The translation service is unavailable.");
        }

        var (card, window) = Open(c =>
        {
            c.Text = AiTranslateFixture.Text;
            c.Translator = Broken;
            c.TranslationFailed += (_, e) => failure = e;
        });
        try
        {
            card.Pick(AiTranslateLanguage.English);
            TestHelpers.PumpUntil(() => card.State == AiTranslateState.Failed).ShouldBeTrue();
            failure.ShouldNotBeNull();
            failure!.Message.ShouldBe("The translation service is unavailable.");
            failure.Exception.ShouldBeOfType<InvalidOperationException>();
            card.Paragraph!.DisplayedText.ShouldBe(AiTranslateFixture.Text);
            AutomationProperties.GetName(card.StatusPanel!).ShouldBe("Couldn't translate. The translation service is unavailable.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Simulated_Announcements_Say_Nothing_Was_Sent()
    {
        var (card, window) = Open();
        try
        {
            card.EffectiveSimulated.ShouldBeTrue();
            card.Pick(AiTranslateLanguage.Japanese);
            AutomationProperties.GetName(card.StatusPanel!).ShouldBe("Translating to Japanese. Simulated, nothing was sent");
            AutomationProperties.GetLiveSetting(card.StatusPanel!).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetItemStatus(card.Paragraph!).ShouldBe("busy");
            card.Advance(3);
            card.Paragraph!.DisplayedText.ShouldBe(AiTranslateFixture.Translations[AiTranslateLanguage.Japanese]);
            AutomationProperties.GetName(card.StatusPanel!).ShouldBe("Translated from French. Simulated, nothing was sent");
            AutomationProperties.GetHelpText(card.Paragraph).ShouldBe("ja");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Value_Set_Before_Showing_Translates_On_Load()
    {
        var (card, window) = Open(c => c.Value = AiTranslateLanguage.Portuguese);
        try
        {
            card.State.ShouldBe(AiTranslateState.Translating);
            card.Advance(3);
            card.Paragraph!.DisplayedText.ShouldStartWith("A versão 4.2");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Text_Clears_The_Cache_And_Shows_The_Original()
    {
        var (card, window) = Open();
        try
        {
            card.Pick(AiTranslateLanguage.English);
            card.Advance(3);
            card.IsCached(AiTranslateLanguage.English).ShouldBeTrue();
            card.Text = "Danke an alle für das Feedback.";
            card.IsCached(AiTranslateLanguage.English).ShouldBeFalse();
            card.Value.ShouldBeNull();
            card.State.ShouldBe(AiTranslateState.Original);
            card.EffectiveSourceLanguage.ShouldBe(AiTranslateLanguage.German);
            card.Paragraph!.DisplayedText.ShouldBe("Danke an alle für das Feedback.");
            card.Chip!.Content.ShouldBe("Detected · German");
            card.MenuEntries.Single(e => e.IsChecked).Tag.ShouldBe(AiTranslateLanguage.German);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Languages_Keep_The_Source_In_The_Menu()
    {
        var (card, window) = Open(c => c.Languages = [AiTranslateLanguage.English, AiTranslateLanguage.Japanese]);
        try
        {
            card.MenuEntries.Select(e => e.Tag).ShouldBe(new object[] { AiTranslateLanguage.French, AiTranslateLanguage.English, AiTranslateLanguage.Japanese });
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Narrow_Cards_Drop_The_Status_And_Shorten_The_Chip()
    {
        var (card, window) = Open(width: 360);
        try
        {
            card.Bounds.Width.ShouldBeLessThan(AiTranslate.CompactWidth);
            card.Classes.Contains(AiTranslate.NarrowPseudoClass).ShouldBeTrue();
            card.Classes.Contains(AiTranslate.CompactPseudoClass).ShouldBeTrue();
            card.Chip!.Content.ShouldBe("French");
            Grid.GetRow(card.StatusPanel!).ShouldBe(1);
            card.Padding.Left.ShouldBe(16);

            window.Width = 440;
            Pump();
            card.Classes.Contains(AiTranslate.CompactPseudoClass).ShouldBeFalse();
            card.Classes.Contains(AiTranslate.NarrowPseudoClass).ShouldBeTrue();
            card.Chip.Content.ShouldBe("Detected · French");

            window.Width = 700;
            Pump();
            card.Bounds.Width.ShouldBe(620);
            card.Classes.Contains(AiTranslate.NarrowPseudoClass).ShouldBeFalse();
            Grid.GetRow(card.StatusPanel!).ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Width_Never_Follows_The_Streaming_Text()
    {
        var (card, window) = Open();
        try
        {
            var width = card.Bounds.Width;
            var picker = card.PickerButton!.Bounds.Width;
            card.Pick(AiTranslateLanguage.English);
            for (var i = 0; i < 20; i++)
            {
                card.Advance(0.05);
                Pump();
                card.Bounds.Width.ShouldBe(width);
                card.PickerButton.Bounds.Width.ShouldBe(picker);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Opens_The_Menu_On_The_Current_Language_And_Picks()
    {
        var (card, window) = Open();
        try
        {
            card.Pick(AiTranslateLanguage.German);
            card.Advance(3);
            card.PickerButton!.Focus(NavigationMethod.Tab);
            Press(window, PhysicalKey.ArrowDown);
            TestHelpers.PumpUntil(() => card.Menu!.HighlightedEntry?.Tag is AiTranslateLanguage.German).ShouldBeTrue();
            card.Menu!.IsOpen.ShouldBeTrue();

            Press(window, PhysicalKey.ArrowDown);
            card.Menu.HighlightedEntry!.Tag.ShouldBe(AiTranslateLanguage.French);
            Press(window, PhysicalKey.Enter);
            TestHelpers.PumpUntil(() => !card.Menu.IsOpen).ShouldBeTrue();
            // French is the source: the original comes back.
            card.State.ShouldBe(AiTranslateState.Original);

            // The list loops: Up from the first row lands on the last.
            Press(window, PhysicalKey.ArrowDown);
            TestHelpers.PumpUntil(() => card.Menu.HighlightedEntry?.Tag is AiTranslateLanguage.French).ShouldBeTrue();
            Press(window, PhysicalKey.Home);
            Press(window, PhysicalKey.ArrowUp);
            card.Menu.HighlightedEntry!.Tag.ShouldBe(AiTranslateLanguage.Portuguese);
            Press(window, PhysicalKey.Escape);
            TestHelpers.PumpUntil(() => !card.Menu.IsOpen).ShouldBeTrue();

            card.PickerButton.IsFocused.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Labels_Reword_Everything()
    {
        var (card, window) = Open(c => c.Labels = new AiTranslateLabels
        {
            Translate = "Traduire",
            ShowOriginal = "Voir l'original",
            TranslatingTo = language => "Traduction : " + language,
            Detected = language => "Détecté : " + language,
            LanguageName = language => language == AiTranslateLanguage.French ? "français" : "anglais",
        });
        try
        {
            card.PickerLabel!.Text.ShouldBe("Traduire");
            card.ToggleLabel!.Text.ShouldBe("Voir l'original");
            card.Chip!.Content.ShouldBe("Détecté : français");
            card.Pick(AiTranslateLanguage.English);
            card.StatusLabel!.Text.ShouldBe("Traduction : anglais");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Autoplay_Tours_The_Simulation_And_Stops_On_A_Press()
    {
        var (card, window) = Open(c => c.AutoPlay = null, level: ArcMotionLevel.Full);
        try
        {
            card.EffectiveAutoPlay.ShouldBeTrue();
            card.Advance(AiTranslate.AutoPlayStart + 0.05);
            card.Value.ShouldBe(AiTranslateLanguage.English);
            // The dwell starts once the translation has arrived.
            card.Advance(1);
            card.ShownLanguage.ShouldBe(AiTranslateLanguage.English);
            card.Advance(AiTranslate.AutoPlayDwell);
            card.Value.ShouldBe(AiTranslateLanguage.German);
            card.Advance(1);

            card.PickerButton!.Focus();
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.Advance(AiTranslate.AutoPlayDwell * 3);
            card.ShownLanguage.ShouldBe(AiTranslateLanguage.German);
            card.TranslatingTo.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Autoplay_Translates_Once()
    {
        var (card, window) = Open(c => c.AutoPlay = true, level: ArcMotionLevel.Reduced);
        try
        {
            card.Advance(AiTranslate.AutoPlayStart + 0.05);
            card.TranslatingTo.ShouldBe(AiTranslateLanguage.English);
            card.Advance(3 + AiTranslate.AutoPlayDwell * 3);
            card.ShownLanguage.ShouldBe(AiTranslateLanguage.English);
            card.TranslatingTo.ShouldBeNull();
            card.EffectiveAutoPlay.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Dims_Fades_Chunks_And_Springs_The_Height()
    {
        var (card, window) = Open(level: ArcMotionLevel.Full);
        try
        {
            var paragraph = card.Paragraph!;
            var before = paragraph.NaturalHeight;
            card.Pick(AiTranslateLanguage.English);
            paragraph.CurrentDim.ShouldBe(1, 0.01);
            paragraph.Step(0.1);
            paragraph.CurrentDim.ShouldBeInRange(0.46, 0.99);
            paragraph.Step(0.15);
            paragraph.CurrentDim.ShouldBe(0.45, 0.001);
            paragraph.IsFading.ShouldBeTrue();

            card.Advance(3);
            Pump();
            card.State.ShouldBe(AiTranslateState.Translated);
            // The English text is shorter: the height follows the text.
            paragraph.NaturalHeight.ShouldBeLessThanOrEqualTo(before);
            TestHelpers.PumpUntil(() =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return Math.Abs(paragraph.DisplayedHeight - paragraph.NaturalHeight) < 0.5;
            }).ShouldBeTrue();

            // Show original swaps back with a crossfade.
            card.ToggleButton!.Focus();
            Press(window, PhysicalKey.Enter);
            paragraph.DisplayedText.ShouldBe(AiTranslateFixture.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leaving_The_Window_Cancels_The_Stream()
    {
        var token = default(CancellationToken);
        async IAsyncEnumerable<string> Hang(AiTranslateRequest request)
        {
            token = request.CancellationToken;
            await Task.Delay(Timeout.Infinite, request.CancellationToken);
            yield return "never";
        }

        var (card, window) = Open(c =>
        {
            c.Text = AiTranslateFixture.Text;
            c.Translator = Hang;
        });
        try
        {
            card.Pick(AiTranslateLanguage.English);
            Pump();
            card.State.ShouldBe(AiTranslateState.Translating);
            window.Content = null;
            Pump();
            token.IsCancellationRequested.ShouldBeTrue();
            card.State.ShouldBe(AiTranslateState.Original);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void No_Text_And_No_Simulation_Shows_The_Empty_Card()
    {
        var (card, window) = Open(c => c.AutoPlay = null, simulate: false, level: ArcMotionLevel.Full);
        try
        {
            card.HasText.ShouldBeFalse();
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.State.ShouldBe(AiTranslateState.Original);
            card.Paragraph!.DisplayedText.ShouldBe(string.Empty);
            card.Classes.Contains(AiTranslate.NoAuthorPseudoClass).ShouldBeTrue();
            card.Chip!.IsVisible.ShouldBeFalse();
            card.PickerButton!.IsEnabled.ShouldBeFalse();
            card.ToggleButton!.IsEnabled.ShouldBeFalse();
            card.StatusLabel!.Text.ShouldBe(string.Empty);
            AutomationProperties.GetName(card).ShouldBe("Message");
            // A language set from code has nothing to translate.
            card.Value = AiTranslateLanguage.German;
            card.Advance(3);
            card.TranslatingTo.ShouldBeNull();
            card.State.ShouldBe(AiTranslateState.Original);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Simulation_Plays_Its_Message_Byline_Model_And_Translations()
    {
        var (card, window) = Open(simulate: false);
        try
        {
            card.Simulation = Demo();
            Pump();
            card.Paragraph!.DisplayedText.ShouldBe(AiTranslateFixture.Text);
            card.Chip!.IsVisible.ShouldBeTrue();
            card.PickerButton!.IsEnabled.ShouldBeTrue();
            TestHelpers.Descendant<TextBlock>(card, AiTranslate.NamePart).Text.ShouldBe(AiTranslateFixture.Author.Name);
            TestHelpers.Descendant<TextBlock>(card, AiTranslate.MetaPart).Text.ShouldBe("Product lead · " + AiTranslateFixture.Timestamp);
            card.StatusLabel!.Text.ShouldBe(AiTranslateFixture.ModelName);
            card.EffectiveSimulated.ShouldBeTrue();

            card.Pick(AiTranslateLanguage.Spanish);
            card.Advance(3);
            card.State.ShouldBe(AiTranslateState.Translated);
            card.Paragraph.DisplayedText.ShouldBe(AiTranslateFixture.Translations[AiTranslateLanguage.Spanish]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Your_Text_Wins_Over_The_Simulation()
    {
        var requests = new List<AiTranslateRequest>();
        async IAsyncEnumerable<string> Translate(AiTranslateRequest request)
        {
            requests.Add(request);
            await Task.Yield();
            yield return "Thanks, everyone.";
        }

        var (card, window) = Open(c =>
        {
            c.AutoPlay = null;
            c.Text = "Merci à tous.";
            c.Translator = r => Translate(r);
        });
        try
        {
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.EffectiveSimulated.ShouldBeFalse();
            card.Paragraph!.DisplayedText.ShouldBe("Merci à tous.");
            card.Classes.Contains(AiTranslate.NoAuthorPseudoClass).ShouldBeTrue();
            card.StatusLabel!.Text.ShouldBe(string.Empty);
            card.Pick(AiTranslateLanguage.English);
            TestHelpers.PumpUntil(() =>
            {
                card.Advance(0.1);
                return card.State == AiTranslateState.Translated;
            }).ShouldBeTrue();
            requests.Count.ShouldBe(1);
            requests[0].Text.ShouldBe("Merci à tous.");
            card.Paragraph.DisplayedText.ShouldBe("Thanks, everyone.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Setting_Simulation_To_Null_Stops_Playback_And_Shows_The_Empty_Card()
    {
        var (card, window) = Open(c => c.AutoPlay = null, level: ArcMotionLevel.Full);
        try
        {
            card.EffectiveAutoPlay.ShouldBeTrue();
            card.Advance(AiTranslate.AutoPlayStart + 0.05);
            card.TranslatingTo.ShouldBe(AiTranslateLanguage.English);

            card.Simulation = null;
            card.TranslatingTo.ShouldBeNull();
            card.EffectiveAutoPlay.ShouldBeFalse();
            card.Advance(AiTranslate.AutoPlayDwell * 3);
            card.TranslatingTo.ShouldBeNull();
            card.State.ShouldBe(AiTranslateState.Original);
            card.Paragraph!.DisplayedText.ShouldBe(string.Empty);
            card.Chip!.IsVisible.ShouldBeFalse();
            card.Classes.Contains(AiTranslate.NoAuthorPseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }
}

/// <summary>Invented test data for the AiTranslate tests (people, companies and numbers are made up).</summary>
file static class AiTranslateFixture
{
    public const string Text =
        "La version 4.2 est disponible. Les exports PDF sont désormais deux fois plus rapides et les tableaux gardent leur mise en forme. " +
        "Merci à toutes celles et ceux qui ont signalé des bugs cette semaine.";

    public const string Timestamp = "Oct 27, 09:14";

    /// <summary>The model credited in the status line.</summary>
    public static string ModelName => "Gemini 3.8 Flash";

    public static AiTranslateAuthor Author { get; } = new() { Name = "Camille Laurent", Role = "Product lead" };

    /// <summary>The self playing tour: English, Deutsch, 日本語 and Português, then back to the original.</summary>
    public static IReadOnlyList<AiTranslateLanguage> Tour { get; } =
    [
        AiTranslateLanguage.English, AiTranslateLanguage.German, AiTranslateLanguage.Japanese, AiTranslateLanguage.Portuguese,
    ];

    /// <summary>The release note in the other five languages.</summary>
    public static IReadOnlyDictionary<AiTranslateLanguage, string> Translations { get; } = new Dictionary<AiTranslateLanguage, string>
    {
        [AiTranslateLanguage.English] =
            "Version 4.2 is out. PDF exports are now twice as fast, and tables keep their formatting. " +
            "Thanks to everyone who reported bugs this week.",
        [AiTranslateLanguage.German] =
            "Version 4.2 ist da. PDF-Exporte sind jetzt doppelt so schnell, und Tabellen behalten ihre Formatierung. " +
            "Danke an alle, die diese Woche Fehler gemeldet haben.",
        [AiTranslateLanguage.Spanish] =
            "Ya está disponible la versión 4.2. Las exportaciones a PDF ahora son el doble de rápidas y las tablas conservan su formato. " +
            "Gracias a todas las personas que informaron de errores esta semana.",
        [AiTranslateLanguage.Japanese] =
            "バージョン4.2を公開しました。PDFの書き出しが2倍速くなり、表の書式も保たれるようになりました。" +
            "今週バグを報告してくださった皆さん、ありがとうございます。",
        [AiTranslateLanguage.Portuguese] =
            "A versão 4.2 já está disponível. As exportações em PDF agora são duas vezes mais rápidas e as tabelas mantêm a formatação. " +
            "Obrigado a todos que relataram bugs esta semana.",
    };
}
