// Ported from vscode (src/vs/editor/contrib/smartSelect/test/browser/smartSelect.test.ts, commit b57d5c9e), MIT license.
// Differences from upstream: the providers take the suite's language configuration service; the bracket provider's
// time budget is a constructor argument (5000 ms here, as upstream's suiteSetup sets); the model service is
// TestTextModel; the tests are synchronous (they wait for the provider tasks).

using Shouldly;
using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.SmartSelect;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using Xunit;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class SmartSelectTests : IDisposable
{
    private const string languageId = "mockJSMode";
    private readonly LanguageConfigurationService _service = new();
    private readonly LanguageFeatureRegistry<ISelectionRangeProvider> _providers = new();

    public SmartSelectTests()
    {
        _service.Register(languageId, new LanguageConfiguration
        {
            Brackets = [("(", ")"), ("{", "}"), ("[", "]")],
            OnEnterRules = OnEnterRules.JavascriptOnEnterRules,
            WordPattern = JsRegExp.Of(@"(-?\d*\.\d\w*)|([^\`\~\!\@\#\$\%\^\&\*\(\)\=\+\[\{\]\}\\;\:\'\""\,\.\<\>\/\?\s]+)", "g"),
        });
    }

    public void Dispose() => _service.Dispose();

    private sealed class StaticProvider(IReadOnlyList<SelectionRange> ranges) : ISelectionRangeProvider
    {
        public Task<IReadOnlyList<IReadOnlyList<SelectionRange>>?> ProvideSelectionRanges(TextModel model, IReadOnlyList<TextPosition> positions, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<IReadOnlyList<SelectionRange>>?>([ranges]);
    }

    private void AssertGetRangesToPosition(string[] text, int lineNumber, int column, TextRange[] ranges, bool selectLeadingAndTrailingWhitespace = true)
    {
        using var model = TestTextModel.Create(string.Join("\n", text), languageId, null, new Uri("file:///test.js"));
        var all = SmartSelect.ProvideSelectionRanges(_providers, _service, model, [new TextPosition(lineNumber, column)], new SmartSelectOptions(selectLeadingAndTrailingWhitespace, true), CancellationToken.None, 5000).GetAwaiter().GetResult();
        var actualStr = all[0].Select(r => r.ToString()).ToArray();
        var desiredStr = ranges.Reverse().Select(r => r.ToString()).ToArray();

        actualStr.ShouldBe(desiredStr, $"\nA: {string.Join(",", actualStr)} VS \nE: {string.Join(",", desiredStr)}");
    }

    private void AssertRanges(ISelectionRangeProvider provider, string value, params TextRange[] expected)
    {
        var index = value.IndexOf('|', StringComparison.Ordinal);
        value = value.Remove(index, 1);

        using var model = TestTextModel.Create(value, languageId, null, new Uri("fake:lang"));
        var pos = model.GetPositionAt(index);
        var all = provider.ProvideSelectionRanges(model, [pos], CancellationToken.None).GetAwaiter().GetResult();
        var ranges = all![0];

        ranges.Count.ShouldBe(expected.Length);
        for (var i = 0; i < ranges.Count; i++)
        {
            ranges[i].Range.ShouldBe(expected[i], $"A={ranges[i].Range} <> E={expected[i]}");
        }
    }

    [Fact]
    public void GetRangesToPosition1()
    {

        AssertGetRangesToPosition([
            "function a(bar, foo){",
            "\tif (bar) {",
            "\t\treturn (bar + (2 * foo))",
            "\t}",
            "}"
        ], 3, 20, [
            new TextRange(1, 1, 5, 2), // all
            new TextRange(1, 21, 5, 2), // {} outside
            new TextRange(1, 22, 5, 1), // {} inside
            new TextRange(2, 1, 4, 3), // block
            new TextRange(2, 1, 4, 3),
            new TextRange(2, 2, 4, 3),
            new TextRange(2, 11, 4, 3),
            new TextRange(2, 12, 4, 2),
            new TextRange(3, 1, 3, 27), // line w/ triva
            new TextRange(3, 3, 3, 27), // line w/o triva
            new TextRange(3, 10, 3, 27), // () outside
            new TextRange(3, 11, 3, 26), // () inside
            new TextRange(3, 17, 3, 26), // () outside
            new TextRange(3, 18, 3, 25), // () inside
        ]);
    }


    [Fact]
    public void ConfigSelectLeadingAndTrailingWhitespace()
    {

        AssertGetRangesToPosition([
            "aaa",
            "\tbbb",
            ""
        ], 2, 3, [
            new TextRange(1, 1, 3, 1), // all
            new TextRange(2, 1, 2, 5), // line w/ triva
            new TextRange(2, 2, 2, 5), // bbb
        ], true);

        AssertGetRangesToPosition([
            "aaa",
            "\tbbb",
            ""
        ], 2, 3, [
            new TextRange(1, 1, 3, 1), // all
            new TextRange(2, 2, 2, 5), // () inside
        ], false);
    }


    [Fact]
    public void GetRangesToPosition56886SkipEmptyLinesCorrectly()
    {

        AssertGetRangesToPosition([
            "function a(bar, foo){",
            "\tif (bar) {",
            "",
            "\t}",
            "}"
        ], 3, 1, [
            new TextRange(1, 1, 5, 2),
            new TextRange(1, 21, 5, 2),
            new TextRange(1, 22, 5, 1),
            new TextRange(2, 1, 4, 3),
            new TextRange(2, 1, 4, 3),
            new TextRange(2, 2, 4, 3),
            new TextRange(2, 11, 4, 3),
            new TextRange(2, 12, 4, 2),
        ]);
    }


    [Fact]
    public void GetRangesToPosition56886DoNotSkipLinesWithOnlyWhitespaces()
    {

        AssertGetRangesToPosition([
            "function a(bar, foo){",
            "\tif (bar) {",
            " ",
            "\t}",
            "}"
        ], 3, 1, [
            new TextRange(1, 1, 5, 2), // all
            new TextRange(1, 21, 5, 2), // {} outside
            new TextRange(1, 22, 5, 1), // {} inside
            new TextRange(2, 1, 4, 3),
            new TextRange(2, 1, 4, 3),
            new TextRange(2, 2, 4, 3),
            new TextRange(2, 11, 4, 3),
            new TextRange(2, 12, 4, 2),
            new TextRange(3, 1, 3, 2), // block
            new TextRange(3, 1, 3, 2) // empty line
        ]);
    }


    [Fact]
    public void GetRangesToPosition40658CursorAtFirstPositionInsideBracketsShouldSelectLineInside()
    {

        AssertGetRangesToPosition([
            " [ ]",
            " { } ",
            "( ) "
        ], 2, 3, [
            new TextRange(1, 1, 3, 5),
            new TextRange(2, 1, 2, 6), // line w/ triava
            new TextRange(2, 2, 2, 5), // {} inside, line w/o triva
            new TextRange(2, 3, 2, 4) // {} inside
        ]);
    }


    [Fact]
    public void GetRangesToPosition40658CursorInEmptyBracketsShouldRevealBracketsFirst()
    {

        AssertGetRangesToPosition([
            " [] ",
            " { } ",
            "  ( ) "
        ], 1, 3, [
            new TextRange(1, 1, 3, 7), // all
            new TextRange(1, 1, 1, 5), // line w/ trival
            new TextRange(1, 2, 1, 4), // [] outside, line w/o trival
            new TextRange(1, 3, 1, 3), // [] inside
        ]);
    }


    [Fact]
    public void GetRangesToPosition40658TokensBeforeBracketWillBeRevealedFirst()
    {

        AssertGetRangesToPosition([
            "  [] ",
            " { } ",
            "selectthis( ) "
        ], 3, 11, [
            new TextRange(1, 1, 3, 15), // all
            new TextRange(3, 1, 3, 15), // line w/ trivia
            new TextRange(3, 1, 3, 14), // line w/o trivia
            new TextRange(3, 1, 3, 11) // word
        ]);
    }


    // -- bracket selections

    [Fact]
    public void BracketSelection()
    {
        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "(|)",
            new TextRange(1, 2, 1, 2), new TextRange(1, 1, 1, 3)
        );

        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "[[[](|)]]",
            new TextRange(1, 6, 1, 6), new TextRange(1, 5, 1, 7), // ()
            new TextRange(1, 3, 1, 7), new TextRange(1, 2, 1, 8), // [[]()]
            new TextRange(1, 2, 1, 8), new TextRange(1, 1, 1, 9) // [[[]()]]
        );

        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "[a[](|)a]",
            new TextRange(1, 6, 1, 6), new TextRange(1, 5, 1, 7),
            new TextRange(1, 2, 1, 8), new TextRange(1, 1, 1, 9)
        );

        // no bracket
        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "fofof|fofo");

        // empty
        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "[[[]()]]|");
        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "|[[[]()]]");

        // edge
        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "[|[[]()]]", new TextRange(1, 2, 1, 8), new TextRange(1, 1, 1, 9));
        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "[[[]()]|]", new TextRange(1, 2, 1, 8), new TextRange(1, 1, 1, 9));

        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "aaa(aaa)bbb(b|b)ccc(ccc)", new TextRange(1, 13, 1, 15), new TextRange(1, 12, 1, 16));
        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "(aaa(aaa)bbb(b|b)ccc(ccc))", new TextRange(1, 14, 1, 16), new TextRange(1, 13, 1, 17), new TextRange(1, 2, 1, 25), new TextRange(1, 1, 1, 26));
    }


    [Fact]
    public void BracketWithLeadingTrailing()
    {

        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "for(a of b){\n  foo(|);\n}",
            new TextRange(2, 7, 2, 7), new TextRange(2, 6, 2, 8),
            new TextRange(1, 13, 3, 1), new TextRange(1, 12, 3, 2),
            new TextRange(1, 1, 3, 2), new TextRange(1, 1, 3, 2)
        );

        AssertRanges(new BracketSelectionRangeProvider(_service, 5000), "for(a of b)\n{\n  foo(|);\n}",
            new TextRange(3, 7, 3, 7), new TextRange(3, 6, 3, 8),
            new TextRange(2, 2, 4, 1), new TextRange(2, 1, 4, 2),
            new TextRange(1, 1, 4, 2), new TextRange(1, 1, 4, 2)
        );
    }


    [Fact]
    public void InWordRanges()
    {

        AssertRanges(new WordSelectionRangeProvider(_service), "f|ooBar",
            new TextRange(1, 1, 1, 4), // foo
            new TextRange(1, 1, 1, 7), // fooBar
            new TextRange(1, 1, 1, 7) // doc
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "f|oo_Ba",
            new TextRange(1, 1, 1, 4),
            new TextRange(1, 1, 1, 7),
            new TextRange(1, 1, 1, 7)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "f|oo-Ba",
            new TextRange(1, 1, 1, 4),
            new TextRange(1, 1, 1, 7),
            new TextRange(1, 1, 1, 7)
        );
    }


    [Fact]
    public void InWordRangesWithSelectSubwordsFalse()
    {

        AssertRanges(new WordSelectionRangeProvider(_service, false), "f|ooBar",
            new TextRange(1, 1, 1, 7),
            new TextRange(1, 1, 1, 7)
        );

        AssertRanges(new WordSelectionRangeProvider(_service, false), "f|oo_Ba",
            new TextRange(1, 1, 1, 7),
            new TextRange(1, 1, 1, 7)
        );

        AssertRanges(new WordSelectionRangeProvider(_service, false), "f|oo-Ba",
            new TextRange(1, 1, 1, 7),
            new TextRange(1, 1, 1, 7)
        );
    }


    [Fact]
    public void DefaultSelectionShouldSelectCurrentWordHumpFirstInCamelCase67493()
    {

        AssertRanges(new WordSelectionRangeProvider(_service), "Abs|tractSmartSelect",
            new TextRange(1, 1, 1, 9),
            new TextRange(1, 1, 1, 20),
            new TextRange(1, 1, 1, 20)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "AbstractSma|rtSelect",
            new TextRange(1, 9, 1, 14),
            new TextRange(1, 1, 1, 20),
            new TextRange(1, 1, 1, 20)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Abstrac-Sma|rt-elect",
            new TextRange(1, 9, 1, 14),
            new TextRange(1, 1, 1, 20),
            new TextRange(1, 1, 1, 20)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Abstrac_Sma|rt_elect",
            new TextRange(1, 9, 1, 14),
            new TextRange(1, 1, 1, 20),
            new TextRange(1, 1, 1, 20)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Abstrac_Sma|rt-elect",
            new TextRange(1, 9, 1, 14),
            new TextRange(1, 1, 1, 20),
            new TextRange(1, 1, 1, 20)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Abstrac_Sma|rtSelect",
            new TextRange(1, 9, 1, 14),
            new TextRange(1, 1, 1, 20),
            new TextRange(1, 1, 1, 20)
        );
    }


    [Fact]
    public void SmartSelectOnlyAddLineRangesIfTheyReContainedByTheNextRange73850()
    {

        var reg = _providers.Register("*", new StaticProvider(
        [
            new SelectionRange(new TextRange(1, 10, 1, 11)),
            new SelectionRange(new TextRange(1, 10, 3, 2)),
            new SelectionRange(new TextRange(1, 1, 3, 2)),
        ]));

        AssertGetRangesToPosition(["type T = {", "\tx: number", "}"], 1, 10, [
            new TextRange(1, 1, 3, 2), // all
            new TextRange(1, 10, 3, 2), // { ... }
            new TextRange(1, 10, 1, 11), // {
        ]);

        reg.Dispose();
    }


    [Fact]
    public void ExpandSelectionInWordsWithUnderscoresIsInconsistent90589()
    {

        AssertRanges(new WordSelectionRangeProvider(_service), "Hel|lo_World",
            new TextRange(1, 1, 1, 6),
            new TextRange(1, 1, 1, 12),
            new TextRange(1, 1, 1, 12)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Hello_Wo|rld",
            new TextRange(1, 7, 1, 12),
            new TextRange(1, 1, 1, 12),
            new TextRange(1, 1, 1, 12)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Hello|_World",
            new TextRange(1, 1, 1, 6),
            new TextRange(1, 1, 1, 12),
            new TextRange(1, 1, 1, 12)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Hello_|World",
            new TextRange(1, 7, 1, 12),
            new TextRange(1, 1, 1, 12),
            new TextRange(1, 1, 1, 12)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Hello|-World",
            new TextRange(1, 1, 1, 6),
            new TextRange(1, 1, 1, 12),
            new TextRange(1, 1, 1, 12)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Hello-|World",
            new TextRange(1, 7, 1, 12),
            new TextRange(1, 1, 1, 12),
            new TextRange(1, 1, 1, 12)
        );

        AssertRanges(new WordSelectionRangeProvider(_service), "Hello|World",
            new TextRange(1, 6, 1, 11),
            new TextRange(1, 1, 1, 11),
            new TextRange(1, 1, 1, 11)
        );
    }
}
