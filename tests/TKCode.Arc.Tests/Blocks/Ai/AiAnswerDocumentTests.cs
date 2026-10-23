using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiAnswerDocument"/>: every block kind and its look, citations and their sources, sentence lighting in both
/// directions, the preview card, keyboard and focus order, the reveal cadence on the frame clock, stop and complete,
/// reduced and off motion, plain text.
/// </summary>
public class AiAnswerDocumentTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(1000.0 / 60);

    private static (AiAnswerDocument Doc, Window Window) Show(ArcMotionLevel level = ArcMotionLevel.Off, double width = 680, AvaloniaList<AiSource>? sources = null)
    {
        var doc = new AiAnswerDocument
        {
            Sources = sources ?? AiAnswerSamples.Sources(),
            CitationNameFormat = (n, title, domain) => $"Source {n}: {title}, {domain}",
            TableName = "Comparison table",
            CodeName = file => "Code: " + file,
            CopyCodeLabel = "Copy",
            CopiedCodeLabel = "Copied",
            CopyFailedCodeLabel = "Failed"
        };
        var window = TestHelpers.Show(new StackPanel { Width = width, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Children = { doc } }, level);
        window.Width = width + 40;
        window.Height = 900;
        Settle(window);
        return (doc, window);
    }

    private static void Settle(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Frames(ManualFrameClock clock, Window window, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(Frame, Frame);
            Dispatcher.UIThread.RunJobs();
        }
        window.UpdateLayout();
    }

    private static int Words(string text) => text.Split((char[])[' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static AiAnswerText FirstParagraph(AiAnswerDocument doc) => doc.BlockViews.OfType<AiAnswerText>().First();

    private static AiAnswerList List(AiAnswerDocument doc) => doc.BlockViews.OfType<AiAnswerList>().Single();

    #region Rendering

    [AvaloniaFact]
    public void SetMarkdown_Shows_Every_Block_Kind_At_Once()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            doc.IsRevealing.ShouldBeFalse();
            var views = doc.BlockViews.ToList();
            views.Count.ShouldBe(10);
            views[0].ShouldBeOfType<AiAnswerText>().Role.ShouldBe(AiAnswerTextRole.Paragraph);
            views[1].ShouldBeOfType<AiAnswerText>().Role.ShouldBe(AiAnswerTextRole.Heading);
            views[2].ShouldBeOfType<AiAnswerList>().Items.Count.ShouldBe(3);
            views[5].ShouldBeOfType<AiAnswerTable>().Cells.Count.ShouldBe(15);
            views[8].ShouldBeOfType<AiAnswerCodeBlock>().Code.ShouldStartWith("# Checks that must pass");
            doc.AllCitations().Count().ShouldBe(11);
            // A finished answer does not fade or show a cursor.
            doc.AllTexts().ShouldAllBe(t => !t.Fades.IsActive && !t.ShowsCursor);
            doc.CursorText.ShouldBeNull();
            doc.Body!.Children.Count.ShouldBe(10);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Body_Type_And_Spacing_Follow_The_Spec()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var views = doc.BlockViews.ToList();
            var paragraph = (AiAnswerText)views[0];
            paragraph.FontSize.ShouldBe(16);
            paragraph.LineHeight.ShouldBe(25.6);
            paragraph.Margin.ShouldBe(default);
            var heading = (AiAnswerText)views[1];
            heading.FontSize.ShouldBe(18);
            heading.LineHeight.ShouldBe(19.8);
            heading.FontWeight.ShouldBe(FontWeight.Medium);
            heading.Margin.ShouldBe(new Thickness(0, 16, 0, -6));
            views[5].Margin.ShouldBe(new Thickness(0, 8));
            views[8].Margin.ShouldBe(new Thickness(0, 8));
            ((StackPanel)doc.Body!).Spacing.ShouldBe(16);
            // The lead sentence of a list item is 500, not bold.
            var item = List(doc).Items[0];
            item.Text.Inlines!.OfType<Run>().First().FontWeight.ShouldBe(FontWeight.Medium);
            item.Marker.Text.ShouldBe("1.");
            item.Text.Margin.Left.ShouldBe(AiAnswerListItem.TextIndent);
            List(doc).Spacing.ShouldBe(10);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Inline_Code_Is_A_Chip_That_Never_Breaks_And_Sits_On_The_Baseline()
    {
        var (doc, window) = Show(width: 300);
        try
        {
            doc.SetMarkdown("Run `npm ci --prefer offline` before the build. [2]");
            Settle(window);
            var text = FirstParagraph(doc);
            var chip = text.Chips.Single();
            chip.Text.ShouldBe("npm ci --prefer offline");
            chip.FontSize.ShouldBe(13.6, 0.001);
            chip.BorderBrush.ShouldBe(doc.HairlineBrush);
            chip.Background.ShouldNotBeNull();
            text.DisplayText.ShouldBe("Run \uFFFC before the build.\u00A0\uFFFC");
            // One object: the chip is never split across lines, and its text baseline is the line's.
            var index = text.DisplayText.IndexOf('\uFFFC');
            text.TextLayout.HitTestTextRange(index, 1).Count().ShouldBe(1);
            var line = text.TextLayout.TextLines[text.TextLayout.GetLineIndexFromCharacterIndex(index, false)];
            var lineTop = text.TextLayout.TextLines.TakeWhile(l => l != line).Sum(l => l.Height);
            (chip.Bounds.Y + TextBlock.GetBaselineOffset(chip)).ShouldBe(lineTop + line.Baseline, 1);
            // Copying the selection reads as written.
            text.GetPlainText(0, text.DisplayText.Length).ShouldBe("Run npm ci --prefer offline before the build. [2]");
            text.GetPlainText(4, 5).ShouldBe("npm ci --prefer offline");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Bullet_Lists_Use_A_Muted_Bullet_And_Ordered_Lists_Keep_Their_Start()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown("- Pin the image. [7]\n- Cache by lockfile.\n\n4. Fourth\n5. Fifth");
            Settle(window);
            var lists = doc.BlockViews.OfType<AiAnswerList>().ToList();
            lists.Count.ShouldBe(2);
            lists[0].Ordered.ShouldBeFalse();
            lists[0].Items.Select(i => i.Marker.Text).ShouldBe(["•", "•"]);
            lists[0].Items[0].Text.Pills.Single().Number.ShouldBe(7);
            lists[1].Items.Select(i => i.Marker.Text).ShouldBe(["4.", "5."]);
            var marker = lists[1].Items[0].Marker;
            marker.Width.ShouldBe(18);
            marker.TextAlignment.ShouldBe(TextAlignment.Right);
            marker.LineHeight.ShouldBe(25.6);
            // Lighting reaches list items.
            doc.LitSourceNumber = 7;
            lists[0].Items[0].Text.IsSentenceLit(0).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Citations_Map_To_Sources_By_Index_Then_By_Position()
    {
        var sources = new AvaloniaList<AiSource>
        {
            new() { Title = "First" },
            new() { Title = "Second", Domain = "two.example" }
        };
        var (doc, window) = Show(sources: sources);
        try
        {
            doc.SetMarkdown("One. [1] Two. [2] Missing. [5]");
            Settle(window);
            var pills = doc.AllCitations().ToList();
            pills.Select(p => p.HasSource).ShouldBe([true, true, false]);
            AutomationProperties.GetName(pills[1]).ShouldBe("Source 2: Second, two.example");
            pills[2].Classes.Contains(AiAnswerCitation.PlainPseudoClass).ShouldBeTrue();
            pills[2].Focusable.ShouldBeFalse();
            doc.FindSource(2)!.Title.ShouldBe("Second");

            // Numbered sources win over positions; a source that arrives makes its citation a link.
            sources.Add(new AiSource { Index = 5, Title = "Fifth" });
            pills[2].HasSource.ShouldBeTrue();
            AutomationProperties.GetName(pills[2]).ShouldBe("Source 5: Fifth, ");
            doc.CitationNameFormat = null;
            doc.CitationName = "Source";
            AutomationProperties.GetName(pills[2]).ShouldBe("Source 5: Fifth");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Table_Is_A_Named_Focusable_Region_With_Header_And_Row_Headers()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var table = doc.BlockViews.OfType<AiAnswerTable>().Single();
            AutomationProperties.GetName(table).ShouldBe("Comparison table");
            table.Focusable.ShouldBeTrue();
            table.Cells[1].Role.ShouldBe(AiAnswerTextRole.HeaderCell);
            table.Cells[1].FontWeight.ShouldBe(FontWeight.Medium);
            table.Cells[3].Role.ShouldBe(AiAnswerTextRole.RowHeaderCell);
            table.Cells[4].Role.ShouldBe(AiAnswerTextRole.Cell);
            table.Cells[4].FontSize.ShouldBe(14);
            table.Cells[4].LineHeight.ShouldBe(20.3);
            var panel = table.Panel!;
            panel.ColumnWidths.Count.ShouldBe(3);
            panel.ColumnWidths.Sum().ShouldBe(panel.Bounds.Width, 0.5);
            // The table fills the region (680 minus the 1px border on each side).
            panel.Bounds.Width.ShouldBe(678, 0.5);
            panel.RowHeights.Count.ShouldBe(5);
            panel.HeaderBrush.ShouldBe(doc.MutedBrush);
            panel.LineBrush.ShouldBe(doc.HairlineBrush);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Narrow_Table_Keeps_Its_Minimum_Width_And_Scrolls()
    {
        var (doc, window) = Show(width: 288);
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var table = doc.BlockViews.OfType<AiAnswerTable>().Single();
            table.Panel!.Bounds.Width.ShouldBe(448, 0.5);
            table.Bounds.Width.ShouldBe(288, 0.5);
            var scroll = table.Scroll!;
            scroll.Extent.Width.ShouldBeGreaterThan(scroll.Viewport.Width);
            table.Focus(NavigationMethod.Tab);
            window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
            Settle(window);
            scroll.Offset.X.ShouldBe(40, 0.5);
            window.KeyPressQwerty(PhysicalKey.End, RawInputModifiers.None);
            Settle(window);
            scroll.Offset.X.ShouldBe(scroll.Extent.Width - scroll.Viewport.Width, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Code_Block_Has_Its_Header_Copy_And_A_Named_Code_Area()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var code = doc.BlockViews.OfType<AiAnswerCodeBlock>().Single();
            code.FileLabel.ShouldBe("ci/required-checks.yml");
            code.LanguageLabel.ShouldBe("YAML");
            code.CopyButton!.Label.ShouldBe("Copy");
            code.CopyButton.CopiedLabel.ShouldBe("Copied");
            code.CopyButton.Value.ShouldBe(code.Code);
            AutomationProperties.GetName(code.CodeArea!).ShouldBe("Code: ci/required-checks.yml");
            code.CodeArea!.Focusable.ShouldBeTrue();
            code.Background.ShouldBe(doc.CodeSurfaceBrush);
            var text = code.CodeText!;
            text.FontSize.ShouldBe(13);
            text.LineHeight.ShouldBe(21.45);
            await Task.WhenAny(text.WhenHighlighted, Task.Delay(TimeSpan.FromSeconds(20)));
            Settle(window);
            text.WhenHighlighted.IsCompleted.ShouldBeTrue();
            text.Tokens.ShouldNotBeNull();
            // Keys and comments get their own colors from the CodeBlock palette.
            text.Inlines!.OfType<Run>().Count(r => r.Foreground is not null).ShouldBeGreaterThan(2);
            string.Concat(text.Inlines!.OfType<Run>().Select(r => r.Text)).ShouldBe(code.Code);

            // Without a file name the language takes the file's place.
            doc.SetMarkdown("```ts\nconst a = 1;\n```");
            Settle(window);
            var bare = doc.BlockViews.OfType<AiAnswerCodeBlock>().Single();
            bare.FileLabel.ShouldBe("TypeScript");
            bare.LanguageLabel.ShouldBe(string.Empty);
            bare.Classes.Contains(AiAnswerCodeBlock.NoFilePseudoClass).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Plain_Text_Is_The_Answer_With_Citation_Numbers()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            doc.PlainText.ShouldStartWith("Keep both pipelines running for a while");
            doc.PlainText.ShouldContain("for a week. [1] Keep the old system");
            doc.PlainText.ShouldContain("1. Mirror. Copy each job");
            doc.PlainText.ShouldNotContain("**");
            doc.PlainText.ShouldNotContain(" ");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Lighting

    [AvaloniaFact]
    public void Pointing_At_A_Citation_Lights_The_Sentence_Before_It_And_Reports_Its_Number()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var numbers = new List<int>();
            doc.CitationActivated += (_, e) => numbers.Add(e.SourceNumber);
            var text = FirstParagraph(doc);
            var pill3 = text.Pills[1];
            pill3.Number.ShouldBe(3);

            var center = pill3.TranslatePoint(new Point(pill3.Bounds.Width / 2, pill3.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Settle(window);
            doc.HoveredCitation.ShouldBe(pill3);
            numbers.ShouldBe([3]);
            text.IsSentenceLit(1).ShouldBeTrue();
            text.IsSentenceLit(0).ShouldBeFalse();
            text.Lights[1].Opacity.ShouldBe(1);
            text.Lights[1].Start.ShouldBe(text.Sentences[1].Start);
            text.DisplayText.Substring(text.Sentences[1].Start, text.Sentences[1].Length)
                .ShouldBe("Keep the old system as the fallback until two releases have shipped on the new one.");
            pill3.IsLit.ShouldBeTrue();
            pill3.Classes.Contains(AiAnswerCitation.LitPseudoClass).ShouldBeTrue();
            text.Pills[0].IsLit.ShouldBeFalse();
            // Other sentences citing the same number elsewhere stay dark: only the one before the citation lights.
            doc.AllTexts().Where(t => t != text).ShouldAllBe(t => t.Sentences.Select((_, i) => i).All(i => !t.IsSentenceLit(i)));

            window.MouseMove(new Point(2, 880), RawInputModifiers.None);
            Settle(window);
            numbers.ShouldBe([3, 0]);
            text.IsSentenceLit(1).ShouldBeFalse();
            text.Lights[1].Opacity.ShouldBe(0);
            pill3.IsLit.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Lit_Source_Lights_Every_Sentence_Citing_It_And_Only_Its_Pills()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            doc.LitSourceNumber = 1;
            var paragraph = FirstParagraph(doc);
            paragraph.IsSentenceLit(0).ShouldBeTrue();
            paragraph.IsSentenceLit(1).ShouldBeFalse();
            var first = List(doc).Items[0].Text;
            // "Mirror." is a sentence of its own and cites nothing.
            first.IsSentenceLit(0).ShouldBeFalse();
            first.IsSentenceLit(1).ShouldBeTrue();
            doc.AllCitations().Where(p => p.IsLit).Select(p => p.Number).ShouldBe([1, 1]);

            // Two pills after one sentence: the card of one lights the sentence and that pill only.
            doc.LitSourceNumber = 6;
            var second = List(doc).Items[1].Text;
            second.IsSentenceLit(1).ShouldBeTrue();
            second.Pills.Select(p => p.IsLit).ShouldBe([false, true]);
            paragraph.IsSentenceLit(0).ShouldBeFalse();

            // No preview opens for a lit card; 0 or a missing source lights nothing.
            doc.IsCardOpen.ShouldBeFalse();
            doc.LitSourceNumber = 0;
            doc.AllCitations().ShouldAllBe(p => !p.IsLit);
            doc.LitSourceNumber = 42;
            doc.AllTexts().ShouldAllBe(t => t.Sentences.Select((_, i) => i).All(i => !t.IsSentenceLit(i)));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Lighting_Fades_Over_160ms_At_Full_Motion_And_Jumps_At_Reduced()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show(ArcMotionLevel.Full);
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            Frames(clock, window, 30);
            var text = FirstParagraph(doc);
            doc.LitSourceNumber = 1;
            text.IsSentenceLit(0).ShouldBeTrue();
            Frames(clock, window, 5);
            text.Lights[0].Opacity.ShouldBeInRange(0.05, 0.98);
            Frames(clock, window, 8);
            text.Lights[0].Opacity.ShouldBe(1);
            doc.LitSourceNumber = 0;
            Frames(clock, window, 12);
            text.Lights[0].Opacity.ShouldBe(0);

            ArcMotion.SetLevel(window, ArcMotionLevel.Reduced);
            Settle(window);
            doc.LitSourceNumber = 1;
            text.Lights[0].Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Keyboard and card

    [AvaloniaFact]
    public void Tab_Visits_Citations_Table_And_Code_In_Reading_Order()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var first = FirstParagraph(doc).Pills[0];
            first.Focus(NavigationMethod.Tab);
            Settle(window);
            var stops = new List<object?> { window.FocusManager!.GetFocusedElement() };
            for (var i = 0; i < 14; i++)
            {
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Settle(window);
                stops.Add(window.FocusManager.GetFocusedElement());
            }
            string Describe(object? stop) => stop switch
            {
                AiAnswerCitation pill => pill.Number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AiAnswerTable => "table",
                CopyButton => "copy",
                ScrollViewer => "code",
                _ => stop?.GetType().Name ?? "null"
            };
            string.Join(" ", stops.Select(Describe)).ShouldStartWith("1 3 1 4 6 5 2 7 table 5 copy code 2 8");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Opens_The_Card_At_Once_Escape_Closes_It_And_Focus_Stays()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var numbers = new List<int>();
            doc.CitationActivated += (_, e) => numbers.Add(e.SourceNumber);
            var pill = FirstParagraph(doc).Pills[1];
            pill.Focus(NavigationMethod.Tab);
            Settle(window);
            doc.FocusedCitation.ShouldBe(pill);
            pill.Classes.Contains(":focus-visible").ShouldBeTrue();
            doc.IsCardOpen.ShouldBeTrue();
            doc.CardCitation.ShouldBe(pill);
            doc.Card!.Title.ShouldBe("Release freeze checklist");
            doc.Card.Number.ShouldBe("3");
            doc.Card.Width.ShouldBe(AiAnswerDocument.CardWidth);
            AutomationProperties.GetHelpText(pill).ShouldStartWith("Release freeze checklist. Freeze pipeline changes");
            numbers.ShouldBe([3]);
            FirstParagraph(doc).IsSentenceLit(1).ShouldBeTrue();

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle(window);
            doc.IsCardOpen.ShouldBeFalse();
            window.FocusManager!.GetFocusedElement().ShouldBe(pill);
            // The lighting stays until focus leaves.
            FirstParagraph(doc).IsSentenceLit(1).ShouldBeTrue();
            AutomationProperties.GetHelpText(pill).ShouldBeNull();

            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Settle(window);
            FirstParagraph(doc).IsSentenceLit(1).ShouldBeFalse();
            numbers.ShouldBe([3, 1]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pointing_Opens_The_Card_After_A_Delay_And_Leaving_Closes_It()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var pill = FirstParagraph(doc).Pills[0];
            doc.OnCitationPointer(pill, entered: true);
            doc.IsCardOpen.ShouldBeFalse();
            doc.FlushCardTimers();
            Settle(window);
            doc.IsCardOpen.ShouldBeTrue();
            doc.CardPopup!.PlacementTarget.ShouldBe(pill);
            doc.Card!.Title.ShouldBe("Running two pipelines side by side");

            // Another citation while it is open: the card moves.
            var other = FirstParagraph(doc).Pills[1];
            doc.OnCitationPointer(pill, entered: false);
            doc.OnCitationPointer(other, entered: true);
            doc.FlushCardTimers();
            doc.CardCitation.ShouldBe(other);

            doc.OnCitationPointer(other, entered: false);
            doc.IsCardOpen.ShouldBeTrue();
            doc.FlushCardTimers();
            doc.IsCardOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Enter_And_Click_Invoke_The_Source_And_Space_Does_Not()
    {
        var (doc, window) = Show();
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var invoked = new List<AiAnswerSourceEventArgs>();
            doc.CitationInvoked += (_, e) => invoked.Add(e);
            var pill = FirstParagraph(doc).Pills[0];
            pill.Focus(NavigationMethod.Tab);
            Settle(window);
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Settle(window);
            invoked.ShouldBeEmpty();
            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            Settle(window);
            invoked.Count.ShouldBe(1);
            invoked[0].Item.Title.ShouldBe("Running two pipelines side by side");
            invoked[0].FromCitation.ShouldBeTrue();
            // The open preview would cover the next citation: close it first.
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Settle(window);

            var target = FirstParagraph(doc).Pills[1];
            var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
            window.MouseMove(center, RawInputModifiers.None);
            Settle(window);
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            Settle(window);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Settle(window);
            invoked.Count.ShouldBe(2);
            invoked[1].Item.Index.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pills_Sit_On_The_Text_Baseline_And_Never_Start_A_Line_Alone()
    {
        var (doc, window) = Show(width: 320);
        try
        {
            doc.SetMarkdown(AiAnswerSamples.Answer);
            Settle(window);
            var text = FirstParagraph(doc);
            foreach (var pill in text.Pills)
            {
                pill.Bounds.Height.ShouldBe(18);
                pill.Bounds.Width.ShouldBeGreaterThanOrEqualTo(18);
                // The pill's index is preceded by a no-break space glued to the sentence's last word.
                var index = text.CitationIndices[text.Pills.ToList().IndexOf(pill)];
                text.DisplayText[index - 1].ShouldBe(' ');
                var pillRect = text.TextLayout.HitTestTextRange(index, 1).First();
                var wordRect = text.TextLayout.HitTestTextRange(index - 2, 1).First();
                pillRect.Y.ShouldBe(wordRect.Y, 0.5);
            }
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region Streaming

    [AvaloniaFact]
    public void Push_Shows_Four_Whole_Words_Every_Third_Frame()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show();
        try
        {
            // Under a second of reading (about 80 words): no catching up.
            const string prose =
                "Keep both pipelines running for a while and switch the required checks only after the new one has matched " +
                "the old one for a week. Keep the old system as the fallback until two releases have shipped on the new one. " +
                "Watch merge-queue wait times during the first week, and if they double, pause the switch and look at caching first. ";
            Words(prose).ShouldBeLessThan(80);
            doc.Push(prose);
            doc.IsRevealing.ShouldBeTrue();
            doc.Classes.Contains(AiAnswerDocument.RevealingPseudoClass).ShouldBeTrue();
            Frames(clock, window, 1);
            doc.Releases.ShouldBe(1);
            Words(doc.RevealedMarkdown).ShouldBe(4);
            Frames(clock, window, 2);
            doc.Releases.ShouldBe(1);
            Frames(clock, window, 1);
            doc.Releases.ShouldBe(2);
            Words(doc.RevealedMarkdown).ShouldBe(8);
            Frames(clock, window, 27);
            doc.Releases.ShouldBe(11);
            Words(doc.RevealedMarkdown).ShouldBe(44);
            doc.RevealedMarkdown.ShouldBe(string.Join(" ", prose.Split(' ').Take(44)) + " ");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Streamed_Text_Grows_In_Place_And_Headings_Wait_For_Their_Line()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show();
        try
        {
            doc.Push("Keep both pipelines running for a while. [1]\n\n### Move it in three phases\n\n1. **Mirror.** Copy each job.");
            Frames(clock, window, 1);
            var paragraph = FirstParagraph(doc);
            Frames(clock, window, 6);
            FirstParagraph(doc).ShouldBeSameAs(paragraph);
            doc.RevealedMarkdown.ShouldContain("[1]");
            paragraph.Pills.Count.ShouldBe(1);
            var pill = paragraph.Pills[0];

            // "### Move it in" has arrived, its line has not: no heading yet.
            while (!doc.RevealedMarkdown.Contains("### Move"))
            {
                Frames(clock, window, 3);
            }
            doc.RevealedMarkdown.ShouldNotContain("phases\n");
            doc.BlockViews.Count().ShouldBe(1);
            while (!doc.RevealedMarkdown.Contains("phases\n"))
            {
                Frames(clock, window, 3);
            }
            // The batch that ends the heading's line may already start the list after it.
            doc.BlockViews.Count().ShouldBeGreaterThanOrEqualTo(2);
            ((AiAnswerText)doc.BlockViews.ElementAt(1)).Pieces.Single().Text.ShouldBe("Move it in three phases");
            paragraph.Pills[0].ShouldBeSameAs(pill);

            // A list item shows its words as they come, the bold lead without its markers.
            while (!doc.RevealedMarkdown.Contains("Copy"))
            {
                Frames(clock, window, 3);
            }
            var item = List(doc).Items[0].Text;
            item.DisplayText.ShouldStartWith("Mirror. Copy");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Complete_Drains_Then_Raises_RevealCompleted_And_Hides_The_Cursor()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show();
        try
        {
            var completed = 0;
            doc.RevealCompleted += (_, _) => completed++;
            doc.Push(AiAnswerSamples.Answer);
            Frames(clock, window, 6);
            doc.CursorText.ShouldNotBeNull();
            doc.CursorText!.ShowsCursor.ShouldBeTrue();
            doc.CursorText.ShouldBe(doc.AllTexts().Last());
            doc.Complete();
            doc.Push("ignored");
            Frames(clock, window, 50);
            completed.ShouldBe(1);
            doc.IsRevealing.ShouldBeFalse();
            doc.CursorText.ShouldBeNull();
            doc.AllTexts().ShouldAllBe(t => !t.ShowsCursor);
            doc.RevealedMarkdown.ShouldBe(AiAnswerSamples.Answer);
            doc.Blocks.Count.ShouldBe(10);
            doc.PlainText.ShouldEndWith("[2] [8]");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Stop_Keeps_What_Was_Released_Without_RevealCompleted()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show();
        try
        {
            var completed = 0;
            doc.RevealCompleted += (_, _) => completed++;
            doc.Push(AiAnswerSamples.Answer);
            Frames(clock, window, 10);
            var shown = doc.RevealedMarkdown;
            doc.Stop();
            Frames(clock, window, 30);
            doc.RevealedMarkdown.ShouldBe(shown);
            doc.IsRevealing.ShouldBeFalse();
            completed.ShouldBe(0);
            doc.CursorText.ShouldBeNull();
            doc.Push("more");
            Frames(clock, window, 10);
            doc.RevealedMarkdown.ShouldBe(shown);

            // Reset starts a new answer.
            doc.Reset();
            doc.BlockViews.ShouldBeEmpty();
            doc.Push("New answer. ");
            Frames(clock, window, 2);
            doc.RevealedMarkdown.ShouldBe("New answer. ");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Full_Motion_Fades_Each_Batch_In_Over_150ms()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show(ArcMotionLevel.Full);
        try
        {
            Frames(clock, window, 30);
            doc.Push("Keep both pipelines running for a while and switch the checks later. [1] More text follows here.");
            Frames(clock, window, 1);
            var text = FirstParagraph(doc);
            text.Fades.IsActive.ShouldBeTrue();
            var range = text.Fades.Ranges.Single();
            range.Start.ShouldBe(0);
            range.End.ShouldBe(text.DisplayText.Length);
            Frames(clock, window, 3);
            text.Fades.Ranges.Count().ShouldBe(2);
            Frames(clock, window, 50);
            // At the end of the stream nothing fades any more, and the pill came in with its batch.
            doc.Complete();
            Frames(clock, window, 60);
            text.Fades.IsActive.ShouldBeFalse();
            text.Pills.Single().Opacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ArcMotionLevel.Reduced)]
    [InlineData(ArcMotionLevel.Off)]
    public void Reduced_And_Off_Keep_The_Cadence_Without_Fades(ArcMotionLevel level)
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show(level);
        try
        {
            Frames(clock, window, 30);
            doc.Push(AiAnswerSamples.Answer);
            Frames(clock, window, 1);
            doc.Releases.ShouldBe(1);
            Frames(clock, window, 3);
            doc.Releases.ShouldBe(2);
            doc.AllTexts().ShouldAllBe(t => !t.Fades.IsActive);
            doc.AllCitations().ShouldAllBe(p => p.Opacity == 1);
            // The cursor is shown but still.
            doc.CursorText.ShouldNotBeNull();
            Frames(clock, window, 20);
            doc.CursorOpacity.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Cursor_Breathes_At_Full_Motion()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show(ArcMotionLevel.Full);
        try
        {
            Frames(clock, window, 30);
            doc.Push("A long answer that keeps arriving word by word for a while longer than a second. ");
            var seen = new List<double>();
            for (var i = 0; i < 40; i++)
            {
                Frames(clock, window, 1);
                seen.Add(doc.CursorOpacity);
            }
            seen.Min().ShouldBeLessThan(0.5);
            seen.Max().ShouldBeGreaterThan(0.9);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Table_Rows_And_Code_Lines_Appear_Whole()
    {
        var clock = new ManualFrameClock();
        using var _ = clock.Install();
        var (doc, window) = Show();
        try
        {
            doc.Push("| | Hosted runners | Self-hosted pool |\n|---|---|---|\n| Setup | Minutes | Days |\n\n```yaml ci/checks.yml\nrequired:\n  - build\n```\n");
            var rowCounts = new HashSet<int>();
            var codeLines = new List<string>();
            for (var i = 0; i < 60 && doc.IsRevealing; i++)
            {
                Frames(clock, window, 3);
                if (doc.BlockViews.OfType<AiAnswerTable>().SingleOrDefault() is { } table)
                {
                    rowCounts.Add(table.Rows.Count);
                    table.Cells.ShouldAllBe(c => !c.DisplayText.EndsWith("Min"));
                }
                if (doc.BlockViews.OfType<AiAnswerCodeBlock>().SingleOrDefault() is { } code)
                {
                    codeLines.Add(code.Code);
                }
                if (i == 20)
                {
                    doc.Complete();
                }
            }
            rowCounts.ShouldBe([1, 2], ignoreOrder: true);
            codeLines.Distinct().ShouldBe(["required:", "required:\n  - build"]);
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
