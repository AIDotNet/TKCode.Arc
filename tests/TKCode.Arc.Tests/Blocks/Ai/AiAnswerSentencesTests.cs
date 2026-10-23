namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary>
/// <see cref="AiAnswerSentences"/>: where sentences end, which citations belong to which sentence, and what is lit (the
/// sentence's text without the citations after it).
/// </summary>
public class AiAnswerSentencesTests
{
    /// <summary>Splits a text written with <c>{n}</c> for citations, returning each sentence's lit text and its citation numbers.</summary>
    private static List<(string Text, int[] Numbers)> Split(string written)
    {
        var inlines = AiAnswerMarkdown.ParseInlines(written.Replace("{", "[").Replace("}", "]"));
        var indices = new List<int>();
        var display = AiAnswerText.BuildDisplay(inlines, indices);
        var numbers = inlines.Where(i => i.Kind == AiAnswerInlineKind.Citation).Select(i => i.Number).ToArray();
        return AiAnswerSentences.Split(display, indices)
            .Select(s => (display.Substring(s.Start, s.Length).Replace(AiAnswerText.ObjectChar, '#'),
                          s.Citations.Select(c => numbers[indices.IndexOf(c)]).ToArray()))
            .ToList();
    }

    [Fact]
    public void A_Citation_Lights_The_Sentence_Right_Before_It()
    {
        var sentences = Split("Keep both pipelines running for a week. {1} Keep the old system as the fallback. {3}");
        sentences.Count.ShouldBe(2);
        sentences[0].Text.ShouldBe("Keep both pipelines running for a week.");
        sentences[0].Numbers.ShouldBe([1]);
        sentences[1].Text.ShouldBe("Keep the old system as the fallback.");
        sentences[1].Numbers.ShouldBe([3]);
    }

    [Fact]
    public void Two_Citations_After_One_Sentence_Both_Belong_To_It()
    {
        var sentences = Split("Track pass rates; investigate any job that disagrees. {4} {6} Then switch.");
        sentences[0].Numbers.ShouldBe([4, 6]);
        sentences[1].Text.ShouldBe("Then switch.");
        sentences[1].Numbers.ShouldBeEmpty();
        Split("Adjacent pills. {1}{2}")[0].Numbers.ShouldBe([1, 2]);
    }

    [Fact]
    public void A_Bold_Lead_Is_A_Sentence_Of_Its_Own()
    {
        var sentences = Split("**Mirror.** Copy each job into the new system. {1}");
        sentences.Select(s => s.Text).ShouldBe(["Mirror.", "Copy each job into the new system."]);
        sentences[0].Numbers.ShouldBeEmpty();
        sentences[1].Numbers.ShouldBe([1]);
    }

    [Fact]
    public void Questions_Exclamations_And_Closing_Quotes_End_Sentences()
    {
        Split("Is it ready? Yes! He said \"go.\" Done.").Select(s => s.Text).ShouldBe(["Is it ready?", "Yes!", "He said \"go.\"", "Done."]);
    }

    [Fact]
    public void Decimals_Versions_And_Ellipses_Do_Not_End_Sentences()
    {
        Split("Builds took 4.5 minutes on v2.1 runners... then less. Next.").Select(s => s.Text)
            .ShouldBe(["Builds took 4.5 minutes on v2.1 runners... then less.", "Next."]);
        Split("Use a shared cache, e.g. one keyed by the lockfile. Done.").Select(s => s.Text)
            .ShouldBe(["Use a shared cache, e.g. one keyed by the lockfile.", "Done."]);
    }

    [Fact]
    public void A_Citation_Mid_Sentence_Closes_The_Part_Before_It()
    {
        var sentences = Split("Use a cache {2} keyed by the lockfile. {7}");
        sentences.Select(s => s.Text).ShouldBe(["Use a cache", "keyed by the lockfile."]);
        sentences[0].Numbers.ShouldBe([2]);
        sentences[1].Numbers.ShouldBe([7]);
    }

    [Fact]
    public void A_Citation_Before_Any_Text_Belongs_To_No_Sentence()
    {
        var sentences = Split("{1} Then text.");
        sentences.Single().Text.ShouldBe("Then text.");
        sentences.Single().Numbers.ShouldBeEmpty();
    }

    [Fact]
    public void Text_Without_An_End_Mark_Is_One_Sentence_And_Line_Breaks_End_Sentences()
    {
        Split("Shared by lockfile").Single().Text.ShouldBe("Shared by lockfile");
        Split("first line  \nsecond line {2}").Select(s => s.Text).ShouldBe(["first line", "second line"]);
        Split("first line  \nsecond line {2}")[1].Numbers.ShouldBe([2]);
    }

    [Fact]
    public void Inline_Code_Is_Part_Of_Its_Sentence()
    {
        var sentences = Split("Request credentials with `mediation: \"conditional\"` so saved passkeys appear. {1} {2}");
        sentences.Single().Text.ShouldBe("Request credentials with # so saved passkeys appear.");
        sentences.Single().Numbers.ShouldBe([1, 2]);
    }

    [Fact]
    public void Empty_And_Space_Only_Texts_Have_No_Sentences()
    {
        AiAnswerSentences.Split(string.Empty, []).ShouldBeEmpty();
        AiAnswerSentences.Split("     ", []).ShouldBeEmpty();
    }
}
