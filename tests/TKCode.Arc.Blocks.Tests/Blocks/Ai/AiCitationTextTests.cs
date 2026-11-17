namespace TKCode.Arc.Tests.Blocks.Ai;

/// <summary><see cref="AiCitationText"/>: which brackets become citations, code is left alone, and streaming is split-proof.</summary>
public class AiCitationTextTests
{
    private static string C(int n) => AiCitationText.Tag(n);

    /// <summary>Texts that exercise every rule; each must convert the same way however it is split.</summary>
    private static readonly string[] Samples =
    [
        "Check the cache key [1].",
        "Pin the image.[2] Then split the job [3][1].",
        "See [12] and [123] but not [1234] or [0] or [01].",
        "Plain [a] and [] and [ 1] and [1 ] and an unmatched [1",
        "A link [1](https://example.org) and an image ![1](x.png) and ![2] alone.",
        "[1]: https://example.org/definition\n\nText [1].",
        "A reference [docs][1] and [docs] [2].",
        "Nested [[1]] and [see [2]](https://x.dev).",
        "Escaped \\[1] and a backslash \\\\[2].",
        "Inline `code [1]` then [2] and ``a ` [3] b`` then [4].",
        "```yaml\nkey: [1]\npath: \"[2]\"\n```\nAfter the fence [3].",
        "~~~\n[1]\n~~~~\nStill code? [2]\n~~~~~\n[3]",
        "````md\n```\n[1]\n```\n````\nOut [2].",
        "- item [1]\n  ```sh\n  echo [2]\n  ```\n- next [3]",
        "An open `span [1]\n\nNew paragraph [2].",
        "Ends with a citation [7]",
        "Ends with an open bracket [",
        "Ends with digits [4",
        "Ends inside ``",
        "1. **Bold** step.[1]\n2. Step with `x[2]` code.[3]",
        "中文句子[1]，再来一个[2]。",
        "> quote [1]\n> ```\n> [2]\n> ```\n> after [3]",
        "``` not a fence``` [1]",
    ];

    [Theory]
    [InlineData("[1]", "<Citation id=\"1\" />")]
    [InlineData("Check the key [1].", "Check the key <Citation id=\"1\" />.")]
    [InlineData("[12] and [123]", "<Citation id=\"12\" /> and <Citation id=\"123\" />")]
    [InlineData("[1][2]", "<Citation id=\"1\" /><Citation id=\"2\" />")]
    [InlineData("中文[1]。", "中文<Citation id=\"1\" />。")]
    public void Converts_Numbered_Brackets(string text, string expected)
        => AiCitationText.Convert(text).ShouldBe(expected);

    [Theory]
    [InlineData("[a]")]
    [InlineData("[]")]
    [InlineData("[0]")]
    [InlineData("[01]")]
    [InlineData("[1234]")]
    [InlineData("[ 1]")]
    [InlineData("[1 ]")]
    [InlineData("an unmatched [1")]
    [InlineData("an unmatched [")]
    [InlineData("[1](https://example.org)")]
    [InlineData("![1](x.png)")]
    [InlineData("[1]: https://example.org")]
    [InlineData("   [2]: https://example.org")]
    [InlineData("[docs][1]")]
    [InlineData("\\[1]")]
    [InlineData("`[1]`")]
    [InlineData("``a ` [1] b``")]
    [InlineData("```\n[1]\n```")]
    [InlineData("~~~text\n[1]\n~~~")]
    public void Leaves_Other_Brackets_And_Code_As_They_Are(string text)
        => AiCitationText.Convert(text).ShouldBe(text);

    [Fact]
    public void Nested_Brackets_Convert_Only_The_Number()
    {
        AiCitationText.Convert("[[1]]").ShouldBe("[" + C(1) + "]");
        AiCitationText.Convert("[see [2]](https://x.dev)").ShouldBe("[see " + C(2) + "](https://x.dev)");
    }

    [Fact]
    public void An_Image_Without_A_Destination_Is_A_Citation_After_An_Exclamation_Mark()
        => AiCitationText.Convert("Done![3]").ShouldBe("Done!" + C(3));

    [Fact]
    public void An_Escaped_Backslash_Does_Not_Escape_The_Bracket()
        => AiCitationText.Convert("a \\\\[2]").ShouldBe("a \\\\" + C(2));

    [Fact]
    public void A_Definition_Needs_The_Start_Of_A_Line()
    {
        AiCitationText.Convert("Ratio [1]: high").ShouldBe("Ratio " + C(1) + ": high");
        AiCitationText.Convert("Text\n[1]: https://a.b").ShouldBe("Text\n[1]: https://a.b");
    }

    [Fact]
    public void Code_Spans_And_Fences_Are_Skipped_And_Text_After_Them_Converts()
    {
        AiCitationText.Convert("`a[1]` then [2]").ShouldBe("`a[1]` then " + C(2));
        AiCitationText.Convert("```yaml\nkey: [1]\n```\nAfter [3].").ShouldBe("```yaml\nkey: [1]\n```\nAfter " + C(3) + ".");
        // A longer opener needs a closer at least as long; a shorter run inside is code.
        AiCitationText.Convert("````\n```\n[1]\n```\n````\n[2]").ShouldBe("````\n```\n[1]\n```\n````\n" + C(2));
        // The closer may not carry an info string.
        AiCitationText.Convert("```\n```js [1]\n[2]\n```\n[3]").ShouldBe("```\n```js [1]\n[2]\n```\n" + C(3));
        // A fence indented in a list item.
        AiCitationText.Convert("- a\n  ```sh\n  echo [1]\n  ```\n- b [2]").ShouldBe("- a\n  ```sh\n  echo [1]\n  ```\n- b " + C(2));
        // Tildes only fence at the start of a line; elsewhere they are text.
        AiCitationText.Convert("~~gone~~ [1]").ShouldBe("~~gone~~ " + C(1));
    }

    [Fact]
    public void A_Backtick_In_The_Info_String_Makes_The_Line_Inline_Code()
        => AiCitationText.Convert("``` not a fence``` [1]\n[2]").ShouldBe("``` not a fence``` " + C(1) + "\n" + C(2));

    [Fact]
    public void An_Unclosed_Code_Span_Ends_With_Its_Paragraph()
        => AiCitationText.Convert("open `span [1]\n\nnext [2]").ShouldBe("open `span [1]\n\nnext " + C(2));

    [Fact]
    public void Text_Is_Held_Only_Until_The_Next_Characters_Decide()
    {
        var scanner = new AiCitationText.Scanner();
        scanner.Push("see [").ShouldBe("see ");
        scanner.HasPending.ShouldBeTrue();
        scanner.Push("1").ShouldBe(string.Empty);
        scanner.Push("]").ShouldBe(string.Empty);
        scanner.Push(" more").ShouldBe(C(1) + " more");
        scanner.HasPending.ShouldBeFalse();

        scanner.Push("[x").ShouldBe("[x");
        scanner.Push("\n`").ShouldBe("\n");
        scanner.Push("`").ShouldBe(string.Empty);
        scanner.Push("`yaml").ShouldBe(string.Empty);
        scanner.Push("\n").ShouldBe("```yaml\n");
        scanner.IsInFence.ShouldBeTrue();
        scanner.Push("[2]").ShouldBe("[2]");
        scanner.Flush().ShouldBe(string.Empty);
    }

    [Fact]
    public void Flush_Converts_A_Closed_Citation_And_Releases_Anything_Else()
    {
        var scanner = new AiCitationText.Scanner();
        scanner.Push("end [7]").ShouldBe("end ");
        scanner.Flush().ShouldBe(C(7));

        scanner.Reset();
        scanner.Push("end [4").ShouldBe("end ");
        scanner.Flush().ShouldBe("[4");

        scanner.Reset();
        scanner.Push("tick ``").ShouldBe("tick ");
        scanner.Flush().ShouldBe("``");
    }

    [Fact]
    public void Every_Split_Gives_The_Same_Text_As_Converting_Whole()
    {
        foreach (var sample in Samples)
        {
            var whole = AiCitationText.Convert(sample);
            for (var cut = 0; cut <= sample.Length; cut++)
            {
                var scanner = new AiCitationText.Scanner();
                var result = scanner.Push(sample[..cut]) + scanner.Push(sample[cut..]) + scanner.Flush();
                result.ShouldBe(whole, $"split at {cut} of \"{sample}\"");
            }
            var single = new AiCitationText.Scanner();
            var chars = string.Concat(sample.Select(c => single.Push(c.ToString()))) + single.Flush();
            chars.ShouldBe(whole, $"char by char: \"{sample}\"");
        }
    }

    [Fact]
    public void Random_Chunkings_Give_The_Same_Text()
    {
        var random = new Random(4242);
        var text = string.Join("\n\n", Samples);
        var whole = AiCitationText.Convert(text);
        for (var round = 0; round < 200; round++)
        {
            var scanner = new AiCitationText.Scanner();
            var output = new System.Text.StringBuilder();
            var at = 0;
            while (at < text.Length)
            {
                var size = Math.Min(text.Length - at, random.Next(1, 12));
                output.Append(scanner.Push(text.Substring(at, size)));
                at += size;
            }
            output.Append(scanner.Flush());
            output.ToString().ShouldBe(whole, $"round {round}");
        }
    }

    [Fact]
    public void Keys_Number_The_Citations_In_Order_And_Can_Be_Found_Again()
    {
        var scanner = new AiCitationText.Scanner { EmitsKeys = true };
        var text = scanner.Push("A [2] b [1][2] `[9]` c [3]") + scanner.Flush();
        scanner.Citations.ShouldBe([(2, 0), (1, 1), (2, 2), (3, 3)]);
        text.ShouldBe("A " + AiCitationText.Tag(2, 0) + " b " + AiCitationText.Tag(1, 1) + AiCitationText.Tag(2, 2) + " `[9]` c " + AiCitationText.Tag(3, 3));

        var found = new List<(int Number, int Key)>();
        var from = 0;
        while (AiCitationText.FindKeyedTag(text, from, out var start, out var length, out var number, out var key))
        {
            text.Substring(start, length).ShouldBe(AiCitationText.Tag(number, key));
            found.Add((number, key));
            from = start + length;
        }
        found.ShouldBe(scanner.Citations);

        // A tag the model wrote itself has no key and is not reported.
        AiCitationText.FindKeyedTag("<Citation id=\"1\" />", 0, out _, out _, out _, out _).ShouldBeFalse();

        scanner.Reset();
        (scanner.Push("[5]") + scanner.Flush()).ShouldBe(AiCitationText.Tag(5, 0));
    }
}
