using Shouldly;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Core.Tests.Primitives;

public class WordDiffTests
{
    private static readonly WordDiffOptions Template = new() { Atoms = [("{{", "}}")] };

    private static string Old(WordDiffResult result)
        => string.Concat(result.Segments.Where(s => s.Kind != WordDiffKind.Insert).Select(s => s.Text));

    private static string New(WordDiffResult result)
        => string.Concat(result.Segments.Where(s => s.Kind != WordDiffKind.Delete).Select(s => s.Text));

    private static string Show(WordDiffResult result)
        => string.Join("|", result.Segments.Select(s => (s.Kind switch
        {
            WordDiffKind.Equal => "=",
            WordDiffKind.Insert => "+",
            _ => "-"
        }) + s.Text));

    [Fact]
    public void Identical_Texts_Are_One_Equal_Segment()
    {
        var result = WordDiff.Compute("Keep this as it is.", "Keep this as it is.");
        Show(result).ShouldBe("=Keep this as it is.");
        result.HasChanges.ShouldBeFalse();
        result.InsertedWords.ShouldBe(0);
        result.DeletedWords.ShouldBe(0);
    }

    [Fact]
    public void Both_Empty_Has_No_Segments()
    {
        var result = WordDiff.Compute("", "");
        result.Segments.ShouldBeEmpty();
        result.HasChanges.ShouldBeFalse();
    }

    [Fact]
    public void Text_From_Nothing_Is_One_Insert_And_Text_To_Nothing_Is_One_Delete()
    {
        var added = WordDiff.Compute("", "Three new words");
        Show(added).ShouldBe("+Three new words");
        added.InsertedWords.ShouldBe(3);
        added.DeletedWords.ShouldBe(0);

        var removed = WordDiff.Compute("Two words", "");
        Show(removed).ShouldBe("-Two words");
        removed.DeletedWords.ShouldBe(2);
        removed.InsertedWords.ShouldBe(0);
    }

    [Fact]
    public void A_Word_Replaced_In_The_Middle_Is_One_Delete_Then_One_Insert()
    {
        var result = WordDiff.Compute("Send it on Monday morning", "Send it on Friday morning");
        Show(result).ShouldBe("=Send it on |-Monday|+Friday|= morning");
        result.DeletedWords.ShouldBe(1);
        result.InsertedWords.ShouldBe(1);
    }

    [Fact]
    public void An_Insertion_Keeps_The_Surrounding_Text_Equal()
    {
        var result = WordDiff.Compute("The plan is ready", "The plan is almost ready");
        Show(result).ShouldBe("=The plan is |+almost |=ready");
        result.InsertedWords.ShouldBe(1);
    }

    [Fact]
    public void Punctuation_Stays_With_Its_Word()
    {
        // "ready." and "ready" are different words, so the change shows the full-stop with the word.
        var result = WordDiff.Compute("It is ready.", "It is ready");
        Show(result).ShouldBe("=It is |-ready.|+ready");
    }

    [Fact]
    public void A_Whitespace_Only_Change_Is_A_Change()
    {
        var result = WordDiff.Compute("one two", "one  two");
        result.HasChanges.ShouldBeTrue();
        result.InsertedWords.ShouldBe(0);
        result.DeletedWords.ShouldBe(0);
        Old(result).ShouldBe("one two");
        New(result).ShouldBe("one  two");
    }

    [Fact]
    public void Two_Changes_Separated_By_One_Space_Become_One_Region()
    {
        var result = WordDiff.Compute("alpha beta gamma", "one beta two");
        // "beta" is equal, so these are two regions with an equal word between them...
        Show(result).ShouldBe("-alpha|+one|= beta |-gamma|+two");

        // ...but when only a space separates two replaced words, the space joins the region.
        var joined = WordDiff.Compute("a b", "x y");
        Show(joined).ShouldBe("-a b|+x y");
    }

    [Fact]
    public void Segments_Of_The_Same_Kind_Are_Never_Adjacent_And_Never_Empty()
    {
        var result = WordDiff.Compute("a b c d e f g", "a x c y z f q");
        for (var i = 0; i < result.Segments.Count; i++)
        {
            result.Segments[i].Text.ShouldNotBeEmpty();
            if (i > 0)
            {
                result.Segments[i].Kind.ShouldNotBe(result.Segments[i - 1].Kind);
            }
        }
        // Inside a region the Delete always comes before the Insert.
        for (var i = 1; i < result.Segments.Count; i++)
        {
            if (result.Segments[i].Kind == WordDiffKind.Delete)
            {
                result.Segments[i - 1].Kind.ShouldBe(WordDiffKind.Equal);
            }
        }
    }

    [Fact]
    public void An_Edited_Atom_Is_Replaced_Whole_Never_Split()
    {
        var result = WordDiff.Compute("Dear {{ buyer name }}, welcome", "Dear {{ buyer_name }}, welcome", Template);
        Show(result).ShouldBe("=Dear |-{{ buyer name }}|+{{ buyer_name }}|=, welcome");
        result.DeletedWords.ShouldBe(1);
        result.InsertedWords.ShouldBe(1);
    }

    [Fact]
    public void An_Unchanged_Atom_With_Spaces_Stays_Equal_Around_A_Change()
    {
        var result = WordDiff.Compute("Hi {{ first name }} and welcome", "Hi {{ first name }} and thanks", Template);
        Show(result).ShouldBe("=Hi {{ first name }} and |-welcome|+thanks");
    }

    [Fact]
    public void An_Open_Marker_Without_A_Close_Is_Ordinary_Text()
    {
        var tokens = WordDiff.Tokenize("use {{ here and then", Template);
        tokens.ShouldBe(["use", " ", "{{", " ", "here", " ", "and", " ", "then"]);
    }

    [Fact]
    public void Tokenize_Joins_Back_To_The_Text_And_Keeps_Atoms_And_Cjk_Apart()
    {
        const string text = "Hi {{ name }}!  你好，world\n";
        var tokens = WordDiff.Tokenize(text, Template);
        string.Concat(tokens).ShouldBe(text);
        tokens.ShouldContain("{{ name }}");
        tokens.ShouldContain("你");
        tokens.ShouldContain("好");
        tokens.ShouldContain("!");
        tokens.ShouldContain("，world");
    }

    [Fact]
    public void Cjk_Elements_Are_Words_Of_Their_Own()
    {
        var result = WordDiff.Compute("请把报告发给我", "请把总结发给我");
        Old(result).ShouldBe("请把报告发给我");
        New(result).ShouldBe("请把总结发给我");
        Show(result).ShouldBe("=请把|-报告|+总结|=发给我");
        result.DeletedWords.ShouldBe(2);
        result.InsertedWords.ShouldBe(2);
    }

    [Fact]
    public void Above_The_Cell_Budget_The_Middle_Is_One_Delete_And_One_Insert()
    {
        var before = "start " + string.Join(" ", Enumerable.Range(0, 60).Select(i => $"a{i}")) + " end";
        var after = "start " + string.Join(" ", Enumerable.Range(0, 60).Select(i => $"b{i}")) + " end";
        var result = WordDiff.Compute(before, after, new WordDiffOptions { MaxCells = 100 });
        result.Segments.Count.ShouldBe(4);
        result.Segments[0].ShouldBe(new WordDiffSegment(WordDiffKind.Equal, "start "));
        result.Segments[1].Kind.ShouldBe(WordDiffKind.Delete);
        result.Segments[2].Kind.ShouldBe(WordDiffKind.Insert);
        result.Segments[3].ShouldBe(new WordDiffSegment(WordDiffKind.Equal, " end"));
        Old(result).ShouldBe(before);
        New(result).ShouldBe(after);
    }

    [Fact]
    public void Null_Texts_And_Empty_Atom_Markers_Are_Rejected()
    {
        Should.Throw<ArgumentNullException>(() => WordDiff.Compute(null!, "x"));
        Should.Throw<ArgumentNullException>(() => WordDiff.Compute("x", null!));
        Should.Throw<ArgumentException>(() => WordDiff.Tokenize("x", new WordDiffOptions { Atoms = [("", "}}")] }));
    }

    [Fact]
    public void Random_Edits_Reproduce_Both_Texts_Exactly()
    {
        var random = new Random(20261022);
        string[] vocabulary = ["the", "plan", "is", "ready", "{{ name }}", "{{id}}", "你", "好", "ok,", "yes.", "a-b", "x", "é", "😀"];
        string[] gaps = [" ", " ", " ", "  ", "\n", "\t"];
        for (var round = 0; round < 300; round++)
        {
            var before = Random(random, vocabulary, gaps, random.Next(0, 25));
            var after = Mutate(random, before, vocabulary, gaps);
            var result = WordDiff.Compute(before, after, Template);
            Old(result).ShouldBe(before, $"round {round}");
            New(result).ShouldBe(after, $"round {round}");
            result.HasChanges.ShouldBe(before != after, $"round {round}");
            var tokensAfter = WordDiff.Tokenize(after, Template).Count(t => !string.IsNullOrWhiteSpace(t));
            result.Segments.Where(s => s.Kind != WordDiffKind.Delete).Sum(s => WordDiff.Tokenize(s.Text, Template).Count(t => !string.IsNullOrWhiteSpace(t)))
                  .ShouldBeGreaterThanOrEqualTo(0);
            tokensAfter.ShouldBeGreaterThanOrEqualTo(result.InsertedWords, $"round {round}");
            for (var i = 1; i < result.Segments.Count; i++)
            {
                result.Segments[i].Kind.ShouldNotBe(result.Segments[i - 1].Kind, $"round {round}");
            }
        }
    }

    private static string Random(Random random, string[] vocabulary, string[] gaps, int words)
    {
        var parts = new List<string>();
        for (var i = 0; i < words; i++)
        {
            parts.Add(vocabulary[random.Next(vocabulary.Length)]);
            parts.Add(gaps[random.Next(gaps.Length)]);
        }
        return string.Concat(parts);
    }

    private static string Mutate(Random random, string text, string[] vocabulary, string[] gaps)
    {
        var tokens = WordDiff.Tokenize(text, Template).ToList();
        var edits = random.Next(0, 5);
        for (var i = 0; i < edits && tokens.Count > 0; i++)
        {
            var at = random.Next(tokens.Count);
            switch (random.Next(3))
            {
                case 0:
                    tokens.RemoveAt(at);
                    break;
                case 1:
                    tokens[at] = vocabulary[random.Next(vocabulary.Length)];
                    break;
                default:
                    tokens.Insert(at, vocabulary[random.Next(vocabulary.Length)] + gaps[random.Next(gaps.Length)]);
                    break;
            }
        }
        return string.Concat(tokens);
    }
}
