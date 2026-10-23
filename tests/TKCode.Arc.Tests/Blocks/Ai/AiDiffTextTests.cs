using System.Text;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// The word diff of the side panel's change card and the prompt studio's compare view: what is shown for edits at the start,
/// middle and end, replacements, adjacent changes, atoms, empty sides and white space; the decoration ranges and their
/// geometry (headless text advances 1em a character, so wrapped ranges are exact); the two appearances; copy without signs;
/// the counts; and what screen readers get. Sample texts are invented.
/// </summary>
public class AiDiffTextTests
{
    private const char Pad = AiDiffDocument.PadChar;

    private static readonly string[] Braces = ["{{", "}}"];

    private static AiDiffDocument Doc(string before, string after, AiDiffAppearance appearance = AiDiffAppearance.Card,
                                      Func<string, int>? tone = null, bool atoms = false)
    {
        var pairs = atoms || tone is not null ? AiDiffText.AtomPairs(Braces) : [];
        return AiDiffDocument.Compare(before, after, appearance, pairs, tone);
    }

    /// <summary>
    /// The runs as one line: kept text as is, <c>-old</c>, <c>+new</c>, <c>·</c> a padding, <c>_</c> the "old new" space,
    /// <c>⊕</c> / <c>⊖</c> the signs, <c>@name</c> a chip (its markers left out); runs separated by <c>|</c>.
    /// </summary>
    private static string Describe(AiDiffDocument doc)
    {
        var parts = new List<string>();
        foreach (var span in doc.Spans)
        {
            var text = doc.Text.Substring(span.Start, span.Length);
            parts.Add(span.Kind switch
            {
                AiDiffSpanKind.Equal => text,
                AiDiffSpanKind.Insert => "+" + text,
                AiDiffSpanKind.Delete => "-" + text,
                AiDiffSpanKind.Space => "·",
                AiDiffSpanKind.Separator => "_",
                AiDiffSpanKind.InsertSign => "⊕",
                AiDiffSpanKind.DeleteSign => "⊖",
                AiDiffSpanKind.ChipName => "@" + text,
                _ => string.Empty
            });
        }
        return string.Join("|", parts.Where(p => p.Length > 0));
    }

    /// <summary>The marks as (kind, covered text with paddings shown as ·).</summary>
    private static (AiDiffMarkKind Kind, string Text)[] Marks(AiDiffDocument doc) =>
        doc.Marks.Select(m => (m.Kind, doc.Text.Substring(m.Start, m.Length).Replace(Pad, '·'))).ToArray();

    private static AiDiffText Create(string before, string after, AiDiffAppearance appearance = AiDiffAppearance.Card, double width = double.NaN) =>
        new() { Before = before, After = after, Appearance = appearance, Width = width };

    private static Window Show(Control content)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20), Children = { content } });
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static TextLayout LayoutOf(AiDiffText diff) => diff.TextView!.TextLayout;

    private static int ChipCount(AiDiffText diff) =>
        diff.TextView!.Decorations.Count(d => d.Tag is AiDiffMark { Kind: AiDiffMarkKind.Chip });

    #region What is shown

    [AvaloniaFact]
    public void An_Edit_At_The_Start_Reads_Old_Then_New()
    {
        var doc = Doc("Draft notes stay on the device.", "Saved notes stay on the device.");
        Describe(doc).ShouldBe("-Draft|_|·|+Saved|·| notes stay on the device.");
        Marks(doc).ShouldBe([(AiDiffMarkKind.InsertTint, "Saved"), (AiDiffMarkKind.Strike, "Draft")]);
        doc.InsertedWords.ShouldBe(1);
        doc.DeletedWords.ShouldBe(1);
        doc.HasChanges.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void An_Insert_In_The_Middle_Keeps_Its_Spaces_Outside_The_Tint()
    {
        var doc = Doc("Photos are cached on the device.", "Photos and notes are cached on the device.");
        // The common prefix ends with "Photos ", so the insert is "and notes " with a trailing space outside the tint.
        Describe(doc).ShouldBe("Photos |·|+and notes|·|+ |are cached on the device.");
        Marks(doc).ShouldBe([(AiDiffMarkKind.InsertTint, "and notes")]);
        doc.InsertedWords.ShouldBe(2);
        doc.DeletedWords.ShouldBe(0);
    }

    [AvaloniaFact]
    public void A_Delete_At_The_End_Is_Struck_And_Not_Tinted_On_The_Card()
    {
        var doc = Doc("Older items download on demand when needed.", "Older items download on demand.");
        Describe(doc).ShouldBe("Older items download on |-demand when needed.|_|·|+demand.|·");
        Marks(doc).ShouldBe([(AiDiffMarkKind.InsertTint, "demand."), (AiDiffMarkKind.Strike, "demand when needed.")]);
    }

    [AvaloniaFact]
    public void A_Replacement_Is_One_Deleted_Run_And_One_Inserted_Run_With_A_Space_Between()
    {
        var doc = Doc("Each device keeps thirty days of edits.", "Each device keeps a month of edits.");
        Describe(doc).ShouldBe("Each device keeps |-thirty days|_|·|+a month|·| of edits.");
        doc.CopyText(0, doc.Text.Length).ShouldBe("Each device keeps thirty days a month of edits.");
    }

    [AvaloniaFact]
    public void White_Space_Between_Adjacent_Changed_Words_Belongs_To_The_Change()
    {
        var doc = Doc("Keep both versions and ask the author.", "Keep the newest version and tell the author.");
        // "both versions and ask" -> "the newest version and tell": "and" is kept in the LCS but sits between changes only
        // through white space on neither side, so the runs stay separate; inside each run the spaces are covered.
        var tints = doc.Marks.Where(m => m.Kind == AiDiffMarkKind.InsertTint).Select(m => doc.Text.Substring(m.Start, m.Length)).ToArray();
        tints.ShouldContain("the newest version");
        tints.ShouldAllBe(t => t.Length > 0 && t.Trim() == t);
    }

    [AvaloniaFact]
    public void Changes_Separated_Only_By_A_Space_Form_One_Run()
    {
        var doc = Doc("one two three four", "one 2 3 four");
        Describe(doc).ShouldBe("one |-two three|_|·|+2 3|·| four");
        Marks(doc).ShouldBe([(AiDiffMarkKind.InsertTint, "2 3"), (AiDiffMarkKind.Strike, "two three")]);
    }

    [AvaloniaFact]
    public void An_Empty_Before_Inserts_Everything_And_An_Empty_After_Deletes_Everything()
    {
        var added = Doc("", "Conflicts keep both versions.");
        Describe(added).ShouldBe("·|+Conflicts keep both versions.|·");
        added.InsertedWords.ShouldBe(4);
        added.DeletedWords.ShouldBe(0);
        added.AfterText.ShouldBe("Conflicts keep both versions.");

        var removed = Doc("Conflicts keep both versions.", "");
        Describe(removed).ShouldBe("-Conflicts keep both versions.");
        removed.DeletedWords.ShouldBe(4);
        removed.InsertedWords.ShouldBe(0);
        removed.AfterText.ShouldBe(string.Empty);

        var none = Doc("", "");
        none.Text.ShouldBe(string.Empty);
        none.Spans.ShouldBeEmpty();
        none.HasChanges.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Equal_Texts_Show_Plain_With_No_Marks()
    {
        var doc = Doc("Notes are cached on the device.", "Notes are cached on the device.");
        Describe(doc).ShouldBe("Notes are cached on the device.");
        doc.Marks.ShouldBeEmpty();
        doc.HasChanges.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void A_White_Space_Only_Change_Shows_The_New_Spacing_Without_Marks()
    {
        var doc = Doc("Cached  on device", "Cached on device");
        Describe(doc).ShouldBe("Cached|+ |on device");
        doc.Marks.ShouldBeEmpty();
        doc.HasChanges.ShouldBeTrue();
        doc.InsertedWords.ShouldBe(0);
        doc.DeletedWords.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Cjk_Text_Changes_Character_By_Character()
    {
        var doc = Doc("离线同步保存笔记", "离线同步保留笔记");
        doc.InsertedWords.ShouldBe(1);
        doc.DeletedWords.ShouldBe(1);
        Marks(doc).ShouldBe([(AiDiffMarkKind.InsertTint, "留"), (AiDiffMarkKind.Strike, "存")]);
    }

    [AvaloniaFact]
    public void The_Shown_Text_Holds_Both_Versions_And_After_Text_Is_The_New_One()
    {
        const string before = "Offline sync would let the app keep working when the connection drops.";
        const string after = "Offline sync lets the app keep working without a connection.";
        var doc = Doc(before, after);
        doc.AfterText.ShouldBe(after);
        var kept = new StringBuilder();
        var old = new StringBuilder();
        foreach (var span in doc.Spans)
        {
            var text = doc.Text.Substring(span.Start, span.Length);
            if (span.Kind is AiDiffSpanKind.Equal or AiDiffSpanKind.Insert)
            {
                kept.Append(text);
            }
            if (span.Kind is AiDiffSpanKind.Equal or AiDiffSpanKind.Delete)
            {
                old.Append(text);
            }
        }
        kept.ToString().ShouldBe(after);
        old.ToString().ShouldBe(before);
    }

    [AvaloniaFact]
    public void Spans_Cover_The_Text_In_Order()
    {
        var doc = Doc("Notes, checklists and photos are cached.", "Notes and photos are cached on the device, then synced.",
            AiDiffAppearance.Studio);
        var at = 0;
        foreach (var span in doc.Spans)
        {
            span.Start.ShouldBe(at);
            span.Length.ShouldBeGreaterThan(0);
            doc.SpanAt(span.Start).ShouldBe(doc.Spans.ToList().IndexOf(span));
            at += span.Length;
        }
        at.ShouldBe(doc.Text.Length);
        doc.SpanAt(-1).ShouldBe(-1);
        doc.SpanAt(doc.Text.Length).ShouldBe(-1);
    }

    #endregion

    #region Atoms and chips

    [AvaloniaFact]
    public void Atoms_Compare_Whole_And_Count_As_One_Word()
    {
        var doc = Doc("Greet {{ buyer_name }} by first name.", "Greet {{ buyer }} by first name.", atoms: true);
        doc.InsertedWords.ShouldBe(1);
        doc.DeletedWords.ShouldBe(1);
        Marks(doc).ShouldBe([(AiDiffMarkKind.InsertTint, "{{ buyer }}"), (AiDiffMarkKind.Strike, "{{ buyer_name }}")]);
    }

    [AvaloniaFact]
    public void Atoms_Without_A_Tone_Selector_Are_Plain_Words()
    {
        var doc = Doc("Mention ticket {{ticket_id}}.", "Mention ticket {{ticket_id}} once.", atoms: true);
        doc.Spans.ShouldNotContain(s => s.Kind == AiDiffSpanKind.ChipName || s.Kind == AiDiffSpanKind.ChipBrace);
        doc.Marks.ShouldNotContain(m => m.Kind == AiDiffMarkKind.Chip);
    }

    [AvaloniaFact]
    public void With_A_Tone_Selector_Atoms_Become_Chips_In_Their_Tone()
    {
        var tones = new Dictionary<string, int> { ["buyer_name"] = 1, ["ticket_id"] = 2, ["refund_rule"] = 7 };
        var doc = Doc("Apply {{refund_rule}} to {{ticket_id}}.", "Apply {{refund_rule}} to {{ticket_id}} for {{buyer_name}}.",
            AiDiffAppearance.Studio, name => tones[name]);
        Describe(doc).ShouldBe("Apply |@refund_rule| to |@ticket_id|+ |·|⊕|·|+for |@buyer_name|·|.");
        var chips = doc.Marks.Where(m => m.Kind == AiDiffMarkKind.Chip).ToArray();
        chips.Select(m => doc.Text.Substring(m.Start, m.Length)).ShouldBe(["{{refund_rule}}", "{{ticket_id}}", "{{buyer_name}}"]);
        // 7 wraps to 3; tones are carried on the chip runs too.
        chips.Select(m => m.Tone).ShouldBe([3, 2, 1]);
        doc.Spans.Where(s => s.Kind == AiDiffSpanKind.ChipBrace).ShouldAllBe(s => s.Width == AiDiffDocument.ChipBraceWidth);
        // The chip of an inserted run is drawn over that run's tint.
        Marks(doc)[0].ShouldBe((AiDiffMarkKind.InsertTint, "+·for {{buyer_name}}"));
    }

    [AvaloniaFact]
    public void An_Atom_Without_A_Name_Stays_Plain_Text()
    {
        var doc = Doc("Sign as {{}}.", "Sign as {{}} today.", AiDiffAppearance.Studio, _ => 1);
        doc.Marks.ShouldNotContain(m => m.Kind == AiDiffMarkKind.Chip);
    }

    [AvaloniaFact]
    public void Tones_Wrap_Around_Four()
    {
        AiDiffDocument.WrapTone(1).ShouldBe(1);
        AiDiffDocument.WrapTone(4).ShouldBe(4);
        AiDiffDocument.WrapTone(5).ShouldBe(1);
        AiDiffDocument.WrapTone(0).ShouldBe(4);
        AiDiffDocument.WrapTone(-1).ShouldBe(3);
    }

    [AvaloniaFact]
    public void Atom_Pairs_Ignore_An_Odd_Entry_And_Empty_Markers()
    {
        AiDiffText.AtomPairs(null).ShouldBeEmpty();
        AiDiffText.AtomPairs(["{{"]).ShouldBeEmpty();
        AiDiffText.AtomPairs(["{{", "}}", "[["]).ShouldBe([("{{", "}}")]);
        AiDiffText.AtomPairs(["", "}}", "<", ">"]).ShouldBe([("<", ">")]);
    }

    #endregion

    #region Studio

    [AvaloniaFact]
    public void Studio_Runs_Start_With_A_Sign_Inside_Their_Tint_And_Only_Words_Are_Struck()
    {
        var doc = Doc("Keep it under 150 words.", "Keep it under 120 words.", AiDiffAppearance.Studio);
        Describe(doc).ShouldBe("Keep it under |·|⊖|·|-150|·|·|⊕|·|+120|·| words.");
        Marks(doc).ShouldBe([
            (AiDiffMarkKind.DeleteTint, "−·150"),
            (AiDiffMarkKind.InsertTint, "+·120"),
            (AiDiffMarkKind.Strike, "150")
        ]);
        doc.Spans.Where(s => s.Kind == AiDiffSpanKind.Space).ShouldAllBe(s => s.Width == 3);
    }

    [AvaloniaFact]
    public void Studio_Keeps_Newlines_And_A_Multi_Line_Insert_Has_One_Sign()
    {
        const string before = "Apply this rule.\n\n- Mention the ticket.";
        const string after = "Apply this rule.\n\n- Mention the ticket.\n- Never invent shipping dates.\n- Sign as the support team.";
        var doc = Doc(before, after, AiDiffAppearance.Studio);
        doc.Text.Count(c => c == '\n').ShouldBe(after.Count(c => c == '\n'));
        doc.Spans.Count(s => s.Kind == AiDiffSpanKind.InsertSign).ShouldBe(1);
        // The line break that starts the insert stays outside the tint, so the sign starts the new line; the tint is
        // marked line by line.
        Marks(doc).ShouldBe([
            (AiDiffMarkKind.InsertTint, "+·- Never invent shipping dates."),
            (AiDiffMarkKind.InsertTint, "- Sign as the support team.")
        ]);
        var first = doc.Marks[0];
        doc.Text.Substring(first.Start - 2, 2).ShouldBe("\n" + Pad);
        doc.InsertedWords.ShouldBe(11);
    }

    [AvaloniaFact]
    public void Line_Breaks_Between_Edited_Lines_Stay_Kept()
    {
        // WordDiff alone would join "\n\n" between the two edits into one region and move the new first line after the
        // deleted second paragraph; line breaks compare as words here, so each line keeps its own change.
        const string before = "Be polite to the buyer.\n\nRefund rule: {{refund_rule}}";
        const string after = "Keep it friendly and short.\n\nApply this rule: {{refund_rule}}";
        var doc = Doc(before, after, AiDiffAppearance.Studio, _ => 1);
        var lines = doc.CopyText(0, doc.Text.Length).Split('\n');
        lines.Length.ShouldBe(3);
        lines[0].ShouldBe("Be polite to the buyer. Keep it friendly and short.");
        lines[1].ShouldBe(string.Empty);
        lines[2].ShouldEndWith("rule: {{refund_rule}}");
        doc.AfterText.ShouldBe(after);
        // Line breaks are not words.
        doc.InsertedWords.ShouldBe(7);
        doc.DeletedWords.ShouldBe(6);
    }

    [AvaloniaFact]
    public void An_Empty_Line_Inside_A_Run_Gets_No_Tint()
    {
        var doc = Doc("Intro.", "Intro.\nFirst new paragraph.\n\nSecond new paragraph.", AiDiffAppearance.Studio);
        Marks(doc).ShouldBe([
            (AiDiffMarkKind.InsertTint, "+·First new paragraph."),
            (AiDiffMarkKind.InsertTint, "Second new paragraph.")
        ]);
        doc.Spans.Count(s => s.Kind == AiDiffSpanKind.InsertSign).ShouldBe(1);
    }

    [AvaloniaFact]
    public void Studio_Card_Differences_In_The_Shown_Text()
    {
        var card = Doc("Ask the author which one wins.", "Ask the author which version wins.");
        var studio = Doc("Ask the author which one wins.", "Ask the author which version wins.", AiDiffAppearance.Studio);
        card.Spans.ShouldNotContain(s => s.Kind == AiDiffSpanKind.InsertSign || s.Kind == AiDiffSpanKind.DeleteSign);
        card.Marks.ShouldNotContain(m => m.Kind == AiDiffMarkKind.DeleteTint);
        card.Spans.Count(s => s.Kind == AiDiffSpanKind.Separator).ShouldBe(1);
        studio.Spans.ShouldNotContain(s => s.Kind == AiDiffSpanKind.Separator);
        studio.Marks.Count(m => m.Kind == AiDiffMarkKind.DeleteTint).ShouldBe(1);
        AiDiffDocument.OutsetOf(AiDiffMarkKind.InsertTint, AiDiffAppearance.Card).ShouldBe(new Thickness(2, 0, 2, 0));
        AiDiffDocument.OutsetOf(AiDiffMarkKind.InsertTint, AiDiffAppearance.Studio).ShouldBe(new Thickness(3, 1, 3, 1));
        AiDiffDocument.RadiusOf(AiDiffMarkKind.InsertTint).ShouldBe(4);
        AiDiffDocument.RadiusOf(AiDiffMarkKind.Chip).ShouldBe(6);
    }

    #endregion

    #region Copy

    [AvaloniaFact]
    public void Copy_Leaves_Out_Signs_And_Paddings()
    {
        var doc = Doc("Keep it under 150 words.", "Keep it under 120 words.", AiDiffAppearance.Studio);
        // The deleted run touches the inserted one: its padding copies as one space.
        doc.CopyText(0, doc.Text.Length).ShouldBe("Keep it under 150 120 words.");
        var inserted = doc.Marks.Single(m => m.Kind == AiDiffMarkKind.InsertTint);
        doc.CopyText(inserted.Start, inserted.Start + inserted.Length).ShouldBe("120");
        doc.CopyText(inserted.Start + inserted.Length, inserted.Start).ShouldBe("120");
        doc.CopyText(-5, 4).ShouldBe("Keep");
        doc.CopyText(3, 3).ShouldBe(string.Empty);
    }

    [AvaloniaFact]
    public void Copy_Keeps_Chips_As_Their_Template_Text()
    {
        var doc = Doc("Hi {{buyer_name}},", "Hello {{buyer_name}},", AiDiffAppearance.Studio, _ => 1);
        doc.CopyText(0, doc.Text.Length).ShouldBe("Hi Hello {{buyer_name}},");
    }

    [AvaloniaFact]
    public async Task Copying_A_Selection_Puts_The_Text_Without_Signs_On_The_Clipboard()
    {
        var diff = Create("Keep it under 150 words.", "Keep it under 120 words.", AiDiffAppearance.Studio);
        var window = Show(diff);
        try
        {
            var block = diff.TextView!;
            block.SelectAll();
            block.SelectedCopyText.ShouldBe("Keep it under 150 120 words.");
            block.SelectedText.ShouldContain(AiDiffDocument.InsertSignText);
            block.Copy();
            Dispatcher.UIThread.RunJobs();
            var clipboard = TopLevel.GetTopLevel(window)!.Clipboard!;
            (await clipboard.TryGetTextAsync()).ShouldBe("Keep it under 150 120 words.");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion

    #region The control

    [AvaloniaFact]
    public void Counts_Follow_The_Texts_And_Raise_Changes()
    {
        var diff = Create("Each device keeps thirty days of edits.", "Each device keeps a month of edits.");
        diff.InsertedWords.ShouldBe(2);
        diff.DeletedWords.ShouldBe(2);
        diff.HasChanges.ShouldBeTrue();
        var changed = new List<string>();
        diff.PropertyChanged += (_, e) => changed.Add(e.Property.Name);
        diff.After = "Each device keeps thirty days of edits and photos.";
        diff.InsertedWords.ShouldBe(3);
        diff.DeletedWords.ShouldBe(1);
        changed.ShouldContain(nameof(AiDiffText.InsertedWords));
        changed.ShouldContain(nameof(AiDiffText.DeletedWords));
        diff.Before = diff.After;
        diff.HasChanges.ShouldBeFalse();
        diff.InsertedWords.ShouldBe(0);
        changed.ShouldContain(nameof(AiDiffText.HasChanges));
    }

    [AvaloniaFact]
    public void The_Block_Shows_The_Document_And_Its_Marks_As_Decorations()
    {
        var diff = Create("Each device keeps thirty days of edits.", "Each device keeps a month of edits.");
        var window = Show(diff);
        try
        {
            var block = diff.TextView!;
            block.Text.ShouldBe(diff.Document.Text);
            block.Decorations.Select(d => (d.Start, d.Length)).ShouldBe(diff.Document.Marks.Select(m => (m.Start, m.Length)));
            var tint = block.Decorations[0];
            tint.Extent.ShouldBe(TextRangeExtent.Text);
            tint.Outset.ShouldBe(new Thickness(2, 0, 2, 0));
            tint.CornerRadius.ShouldBe(4);
            tint.Background.ShouldBeSameAs(diff.Palette.InsertTint);
            block.Decorations[1].StrikeThrough.ShouldBeSameAs(diff.Palette.Strike);
            block.Decorations[1].Background.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Run_That_Wraps_Is_Tinted_On_Each_Line()
    {
        // 13 DIP a character on the card: 200 wide holds 15 characters a line.
        var diff = Create("Notes stay.", "Notes, checklists and photos stay.", width: 200);
        var window = Show(diff);
        try
        {
            var layout = LayoutOf(diff);
            layout.TextLines.Count.ShouldBeGreaterThan(1);
            var mark = diff.Document.Marks.Single(m => m.Kind == AiDiffMarkKind.InsertTint);
            var rects = TextRangeRenderer.GetRects(layout, mark.Start, mark.Length);
            rects.Count.ShouldBeGreaterThan(1);
            rects[1].Top.ShouldBeGreaterThan(rects[0].Top);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paddings_And_Chip_Markers_Are_Exactly_As_Wide_As_Asked()
    {
        var diff = Create("Greet {{buyer_name}}.", "Greet {{buyer_name}} warmly.", AiDiffAppearance.Studio);
        diff.Atoms = Braces;
        diff.ChipTone = _ => 2;
        var window = Show(diff);
        try
        {
            var layout = LayoutOf(diff);
            var doc = diff.Document;
            foreach (var span in doc.Spans.Where(s => s.Kind is AiDiffSpanKind.Space or AiDiffSpanKind.ChipBrace))
            {
                var width = layout.HitTestTextRange(span.Start, span.Length).Sum(r => r.Width);
                width.ShouldBe(span.Width, 0.05);
            }
            // Chip names are 0.93 of the font size.
            var name = doc.Spans.First(s => s.Kind == AiDiffSpanKind.ChipName);
            var nameWidth = layout.HitTestTextRange(name.Start, name.Length).Sum(r => r.Width);
            nameWidth.ShouldBe(name.Length * (14 * AiDiffText.ChipFontScale + diff.LetterSpacing), 0.1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Card_And_Studio_Look_Different()
    {
        var card = Create("Keep it under 150 words.", "Keep it under 120 words.");
        var studio = Create("Keep it under 150 words.", "Keep it under 120 words.", AiDiffAppearance.Studio);
        var window = Show(new StackPanel { Children = { card, studio } });
        try
        {
            card.FontSize.ShouldBe(13);
            card.LineHeight.ShouldBe(20.8);
            card.Classes.Contains(":card").ShouldBeTrue();
            studio.FontSize.ShouldBe(14);
            studio.LineHeight.ShouldBe(23.8);
            studio.Classes.Contains(":studio").ShouldBeTrue();

            Color Token(ArcDesignTokenKind kind) => ArcTokens.GetColor(card, kind);
            static Color Alpha(Color c, double a) => Color.FromArgb((byte)Math.Round(c.A * a), c.R, c.G, c.B);

            TestHelpers.ColorOf(card.Palette.Equal).ShouldBe(Token(ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(card.Palette.Delete).ShouldBe(Token(ArcDesignTokenKind.TextMuted));
            TestHelpers.ColorOf(card.Palette.Insert).ShouldBe(Token(ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(card.Palette.InsertTint).ShouldBe(Alpha(Token(ArcDesignTokenKind.Accent), 0.13));
            card.Palette.DeleteTint.ShouldBeNull();
            TestHelpers.ColorOf(card.Palette.Strike!.Brush).ShouldBe(Alpha(Token(ArcDesignTokenKind.TextMuted), 0.7));

            TestHelpers.ColorOf(studio.Palette.Equal).ShouldBe(Token(ArcDesignTokenKind.Foreground));
            TestHelpers.ColorOf(studio.Palette.Delete).ShouldBe(Token(ArcDesignTokenKind.TextSecondary));
            TestHelpers.ColorOf(studio.Palette.InsertTint).ShouldBe(Alpha(Token(ArcDesignTokenKind.Success), 0.14));
            TestHelpers.ColorOf(studio.Palette.DeleteTint).ShouldBe(Alpha(Token(ArcDesignTokenKind.Danger), 0.11));
            TestHelpers.ColorOf(studio.Palette.Strike!.Brush).ShouldBe(Alpha(Token(ArcDesignTokenKind.Danger), 0.55));
            TestHelpers.ColorOf(studio.Palette.InsertSign)
                .ShouldBe(PromptTone.Mix(Token(ArcDesignTokenKind.Foreground), Token(ArcDesignTokenKind.Success), 0.72));

            studio.Document.Spans.ShouldContain(s => s.Kind == AiDiffSpanKind.InsertSign);
            card.Document.Spans.ShouldNotContain(s => s.Kind == AiDiffSpanKind.InsertSign);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Chips_Take_Their_Series_Color()
    {
        var diff = Create("Greet {{buyer_name}}.", "Greet {{buyer_name}} warmly.", AiDiffAppearance.Studio);
        diff.Atoms = Braces;
        diff.ChipTone = _ => 3;
        var window = Show(diff);
        try
        {
            var series = ArcTokens.GetColor(diff, ArcDesignTokenKind.Series3);
            var chip = diff.TextView!.Decorations.Single(d => d.Tag is AiDiffMark { Kind: AiDiffMarkKind.Chip });
            TestHelpers.ColorOf(chip.Background).ShouldBe(PromptTone.WithOpacity(series, 0.12));
            chip.CornerRadius.ShouldBe(6);
            TestHelpers.ColorOf(diff.Palette.ChipRing[2]!.Brush).ShouldBe(PromptTone.WithOpacity(series, 0.22));
            TestHelpers.ColorOf(diff.Palette.ChipText[2])
                .ShouldBe(PromptTone.Mix(ArcTokens.GetColor(diff, ArcDesignTokenKind.Foreground), series, 0.68));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Appearance_Or_Tone_Redraws_The_Same_Diff()
    {
        var diff = Create("Greet {{buyer_name}}.", "Greet {{buyer_name}} warmly.");
        diff.Atoms = Braces;
        var window = Show(diff);
        try
        {
            ChipCount(diff).ShouldBe(0);
            diff.ChipTone = _ => 1;
            ChipCount(diff).ShouldBe(1);
            diff.Appearance = AiDiffAppearance.Studio;
            diff.TextView!.Text.ShouldBe(diff.Document.Text);
            diff.TextView.Text!.ShouldContain(AiDiffDocument.InsertSignText);
            window.UpdateLayout();
            diff.FontSize.ShouldBe(14);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_New_Tint_Color_Retints_The_Same_Decorations()
    {
        var diff = Create("Keep it under 150 words.", "Keep it under 120 words.", AiDiffAppearance.Studio);
        var window = Show(diff);
        try
        {
            var block = diff.TextView!;
            var decorations = block.Decorations.ToArray();
            diff.InsertTint = new SolidColorBrush(Color.FromRgb(20, 120, 220));
            block.Decorations.ShouldBe(decorations);
            var tint = block.Decorations.Single(d => d.Tag is AiDiffMark { Kind: AiDiffMarkKind.InsertTint });
            TestHelpers.ColorOf(tint.Background).ShouldBe(Color.FromArgb((byte)Math.Round(255 * 0.14), 20, 120, 220));
            TestHelpers.ColorOf(diff.Palette.InsertSign).ShouldBe(PromptTone.Mix(ArcTokens.GetColor(diff, ArcDesignTokenKind.Foreground),
                Color.FromRgb(20, 120, 220), 0.72));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Selecting_Lays_The_Text_Out_Again_But_Reuses_The_Run_Properties()
    {
        var diff = Create("Each device keeps thirty days of edits.", "Each device keeps a month of edits.");
        var window = Show(diff);
        try
        {
            var block = diff.TextView!;
            var built = block.RunPropertiesBuilt;
            var layout = block.TextLayout;
            block.SelectionStart = 2;
            block.SelectionEnd = 9;
            window.UpdateLayout();
            block.TextLayout.ShouldNotBeSameAs(layout);
            block.RunPropertiesBuilt.ShouldBe(built);
            diff.Appearance = AiDiffAppearance.Studio;
            window.UpdateLayout();
            block.TextLayout.ShouldNotBeNull();
            block.RunPropertiesBuilt.ShouldBeGreaterThan(built);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Get_The_Summary_And_The_New_Text()
    {
        var diff = Create("Each device keeps thirty days of edits.", "Each device keeps a month of edits.");
        var window = Show(diff);
        try
        {
            var peer = ControlAutomationPeer.CreatePeerForElement(diff);
            peer.GetName().ShouldBe("Each device keeps a month of edits.");
            diff.SummaryName = "2 words added, 2 words removed";
            peer.GetName().ShouldBe("2 words added, 2 words removed");
            peer.GetHelpText().ShouldBe("Each device keeps a month of edits.");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Text);
            AutomationProperties.GetAccessibilityView(diff.TextView!).ShouldBe(AccessibilityView.Raw);
            KeyboardNavigation.GetIsTabStop(diff.TextView!).ShouldBeFalse();
            AutomationProperties.SetName(diff, "Proposed summary");
            peer.GetName().ShouldBe("Proposed summary");
        }
        finally
        {
            window.Close();
        }
    }

    #endregion
}
