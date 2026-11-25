// Tests of the port of vscode's src/vs/base/common/filters.ts (commit b57d5c9e, MIT license). The reference checkout
// has no src/vs/base/test, so upstream's filters.test.ts could not be ported file by file; these cases follow the
// suites of that file as they are published upstream (PrefixFilter, CamelCaseFilter, matchesSubString, matchesWords,
// fuzzyScore with its decorated-word assertions, topScore), with the cases that need the Korean alternate characters
// left out (not ported).

using Shouldly;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class FiltersTests
{
    private static void FilterOk(FilterFn filter, string word, string wordToMatchAgainst, params (int Start, int End)[] highlights)
    {
        var r = filter(word, wordToMatchAgainst);
        r.ShouldNotBeNull($"{word} didn't match {wordToMatchAgainst}");
        r.Select(m => (m.Start, m.End)).ToArray().ShouldBe(highlights);
    }

    private static void FilterNotOk(FilterFn filter, string word, string wordToMatchAgainst) =>
        filter(word, wordToMatchAgainst).ShouldBeNull($"{word} matched {wordToMatchAgainst}");

    [Fact]
    public void PrefixFilter_case_sensitive()
    {
        FilterNotOk(Filters.MatchesStrictPrefix, "", "");
        FilterOk(Filters.MatchesStrictPrefix, "", "anything");
        FilterOk(Filters.MatchesStrictPrefix, "alpha", "alpha", (0, 5));
        FilterOk(Filters.MatchesStrictPrefix, "alpha", "alphasomething", (0, 5));
        FilterNotOk(Filters.MatchesStrictPrefix, "alpha", "alp");
        FilterOk(Filters.MatchesStrictPrefix, "a", "alpha", (0, 1));
        FilterNotOk(Filters.MatchesStrictPrefix, "x", "alpha");
        FilterNotOk(Filters.MatchesStrictPrefix, "A", "alpha");
        FilterNotOk(Filters.MatchesStrictPrefix, "AlPh", "alPHA");
    }

    [Fact]
    public void PrefixFilter_ignore_case()
    {
        FilterOk(Filters.MatchesPrefix, "alpha", "alpha", (0, 5));
        FilterOk(Filters.MatchesPrefix, "alpha", "alphasomething", (0, 5));
        FilterNotOk(Filters.MatchesPrefix, "alpha", "alp");
        FilterOk(Filters.MatchesPrefix, "a", "alpha", (0, 1));
        FilterOk(Filters.MatchesPrefix, "\u00e4", "\u00c4lpha", (0, 1));
        FilterNotOk(Filters.MatchesPrefix, "x", "alpha");
        FilterOk(Filters.MatchesPrefix, "A", "alpha", (0, 1));
        FilterOk(Filters.MatchesPrefix, "AlPh", "alPHA", (0, 4));
        FilterNotOk(Filters.MatchesPrefix, "T", "4");
    }

    [Fact]
    public void CamelCaseFilter()
    {
        FilterNotOk(Filters.MatchesCamelCase, "", "");
        FilterOk(Filters.MatchesCamelCase, "", "anything");
        FilterOk(Filters.MatchesCamelCase, "alpha", "alpha", (0, 5));
        FilterOk(Filters.MatchesCamelCase, "AlPhA", "alpha", (0, 5));
        FilterOk(Filters.MatchesCamelCase, "alpha", "alphasomething", (0, 5));
        FilterNotOk(Filters.MatchesCamelCase, "alpha", "alp");

        FilterOk(Filters.MatchesCamelCase, "c", "CamelCaseRocks", (0, 1));
        FilterOk(Filters.MatchesCamelCase, "cc", "CamelCaseRocks", (0, 1), (5, 6));
        FilterOk(Filters.MatchesCamelCase, "ccr", "CamelCaseRocks", (0, 1), (5, 6), (9, 10));
        FilterOk(Filters.MatchesCamelCase, "cacr", "CamelCaseRocks", (0, 2), (5, 6), (9, 10));
        FilterOk(Filters.MatchesCamelCase, "cacar", "CamelCaseRocks", (0, 2), (5, 7), (9, 10));
        FilterOk(Filters.MatchesCamelCase, "ccarocks", "CamelCaseRocks", (0, 1), (5, 7), (9, 14));
        FilterOk(Filters.MatchesCamelCase, "cr", "CamelCaseRocks", (0, 1), (9, 10));
        FilterOk(Filters.MatchesCamelCase, "fba", "FooBarAbe", (0, 1), (3, 5));
        FilterOk(Filters.MatchesCamelCase, "fbar", "FooBarAbe", (0, 1), (3, 6));
        FilterOk(Filters.MatchesCamelCase, "fbara", "FooBarAbe", (0, 1), (3, 7));
        FilterOk(Filters.MatchesCamelCase, "fbaa", "FooBarAbe", (0, 1), (3, 5), (6, 7));
        FilterOk(Filters.MatchesCamelCase, "fbaab", "FooBarAbe", (0, 1), (3, 5), (6, 8));
        FilterOk(Filters.MatchesCamelCase, "c2d", "canvasCreation2D", (0, 1), (14, 16));
        FilterOk(Filters.MatchesCamelCase, "cce", "_canvasCreationEvent", (1, 2), (7, 8), (15, 16));
    }

    [Fact]
    public void CamelCaseFilter_issue_19256_and_23206()
    {
        Filters.MatchesCamelCase("Debug Console", "Open: Debug Console").ShouldNotBeNull();
        Filters.MatchesCamelCase("Debug console", "Open: Debug Console").ShouldNotBeNull();
        Filters.MatchesCamelCase("debug console", "Open: Debug Console").ShouldNotBeNull();
        Filters.MatchesCamelCase("long", "VeryLongName").ShouldNotBeNull();
    }

    [Fact]
    public void MatchesSubString()
    {
        FilterOk(Filters.MatchesSubString, "cmm", "cancelAnimationFrame()", (0, 1), (9, 10), (18, 19));
        FilterOk(Filters.MatchesSubString, "abc", "abcabc", (0, 3));
        FilterOk(Filters.MatchesSubString, "abc", "aaabbbccc", (0, 1), (3, 4), (6, 7));
    }

    [Fact]
    public void MatchesSubString_performance_issue_35346()
    {
        Filters.MatchesSubString("aaaaaaaaaaaaaaaaaaaax", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa").ShouldBeNull();
    }

    [Fact]
    public void MatchesWords()
    {
        FilterOk((w, t) => Filters.MatchesWords(w, t), "alpha", "alpha", (0, 5));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "alpha", "alphasomething", (0, 5));
        FilterNotOk((w, t) => Filters.MatchesWords(w, t), "alpha", "alp");
        FilterOk((w, t) => Filters.MatchesWords(w, t), "a", "alpha", (0, 1));
        FilterNotOk((w, t) => Filters.MatchesWords(w, t), "x", "alpha");
        FilterOk((w, t) => Filters.MatchesWords(w, t), "A", "alpha", (0, 1));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "AlPh", "alPHA", (0, 4));
        Filters.MatchesWords("Debug Console", "Open: Debug Console").ShouldNotBeNull();
        FilterOk((w, t) => Filters.MatchesWords(w, t), "gp", "Git: Pull", (0, 1), (5, 6));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "g p", "Git: Pull", (0, 1), (5, 6));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "gipu", "Git: Pull", (0, 2), (5, 7));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "gp", "Category: Git: Pull", (10, 11), (15, 16));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "g p", "Category: Git: Pull", (10, 11), (15, 16));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "gipu", "Category: Git: Pull", (10, 12), (15, 17));
        FilterNotOk((w, t) => Filters.MatchesWords(w, t), "it", "Git: Pull");
        FilterNotOk((w, t) => Filters.MatchesWords(w, t), "ll", "Git: Pull");
        FilterOk((w, t) => Filters.MatchesWords(w, t), "git: \u30d7\u30eb", "git: \u30d7\u30eb", (0, 7));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "git \u30d7\u30eb", "git: \u30d7\u30eb", (0, 3), (5, 7));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "\u00f6\u00e4k", "\u00d6hm: \u00c4lles Klar", (0, 1), (5, 6), (11, 12));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "C++", "C/C++: command", (2, 5));
        FilterOk((w, t) => Filters.MatchesWords(w, t), ".", ".", (0, 1));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "bar", "foo-bar", (4, 7));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "bar test", "foo-bar test", (4, 12));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "fbt", "foo-bar test", (0, 1), (4, 5), (8, 9));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "bar test", "foo-bar (test)", (4, 8), (9, 13));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "foo bar", "foo (bar)", (0, 4), (5, 8));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "foo bar", "foo-bar", (0, 3), (4, 7));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "foo bar", "foo:bar", (0, 3), (4, 7));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "foo bar", "foo-bar", (0, 3), (4, 7));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "foo-bar", "foo bar", (0, 3), (4, 7));
        FilterOk((w, t) => Filters.MatchesWords(w, t), "foo:bar", "foo:bar", (0, 7));
    }

    [Fact]
    public void MatchesWords_does_not_explode_on_many_separators()
    {
        var target = string.Concat(Enumerable.Repeat("a.b.c.d.e.f.g.h.", 20));
        Filters.MatchesWords("editor.action", target).ShouldBeNull();
    }

    // upstream's assertMatches: '^' before every matched character of the decorated word, or no match
    private static void AssertMatches(string pattern, string word, string? decoratedWord, FuzzyScorer filter, int patternPos = 0, int wordPos = 0, bool firstMatchCanBeWeak = false)
    {
        var r = filter(pattern, pattern.ToLowerInvariant(), patternPos, word, word.ToLowerInvariant(), wordPos, new FuzzyScoreOptions(firstMatchCanBeWeak, BoostFullMatch: true));
        if (decoratedWord is null)
        {
            r.ShouldBeNull($"{pattern} matched {word}");
            return;
        }

        r.ShouldNotBeNull($"{pattern} didn't match {word}");
        var matches = r.Matches.Select(m => m + r.WordStart).ToHashSet();
        var actual = string.Concat(word.Select((c, i) => matches.Contains(i) ? "^" + c : c.ToString()));
        actual.ShouldBe(decoratedWord, $"{pattern} -> {word}");
    }

    private static readonly FuzzyScorer s_fuzzyScore = (p, lp, pp, w, lw, wp, o) => Filters.FuzzyScore(p, lp, pp, w, lw, wp, o);

    private static readonly FuzzyScorer s_graceful = (p, lp, pp, w, lw, wp, o) => Filters.FuzzyScoreGraceful(p, lp, pp, w, lw, wp, o);

    [Fact]
    public void FuzzyScore_issue_23215()
    {
        AssertMatches("tit", "win.tit", "win.^t^i^t", s_fuzzyScore);
        AssertMatches("title", "win.title", "win.^t^i^t^l^e", s_fuzzyScore);
        AssertMatches("WordCla", "WordCharacterClassifier", "^W^o^r^dCharacter^C^l^assifier", s_fuzzyScore);
        AssertMatches("WordCCla", "WordCharacterClassifier", "^W^o^r^d^Character^C^l^assifier", s_fuzzyScore);
    }

    [Fact]
    public void FuzzyScore_issue_23332() => AssertMatches("dete", "\"editor.quickSuggestionsDelay\"", null, s_fuzzyScore);

    [Fact]
    public void FuzzyScore_issue_23190()
    {
        AssertMatches("c:\\do", "& 'C:\\Documents and Settings'", "& '^C^:^\\^D^ocuments and Settings'", s_fuzzyScore);
        AssertMatches("c:\\do", "& 'c:\\Documents and Settings'", "& '^c^:^\\^D^ocuments and Settings'", s_fuzzyScore);
    }

    [Fact]
    public void FuzzyScore_issue_23581()
    {
        AssertMatches("close", "css.lint.importStatement", "^css.^lint.imp^ort^Stat^ement", s_fuzzyScore);
        AssertMatches("close", "css.colorDecorators.enable", "^css.co^l^orDecorator^s.^enable", s_fuzzyScore);
        AssertMatches("close", "workbench.quickOpen.closeOnFocusOut", "workbench.quickOpen.^c^l^o^s^eOnFocusOut", s_fuzzyScore);
        AssertTopScore(s_fuzzyScore, "close", 2, "css.lint.importStatement", "css.colorDecorators.enable", "workbench.quickOpen.closeOnFocusOut");
    }

    [Fact]
    public void FuzzyScore_issue_23458()
    {
        AssertMatches("highlight", "editorHoverHighlight", "editorHover^H^i^g^h^l^i^g^h^t", s_fuzzyScore);
        AssertMatches("hhighlight", "editorHoverHighlight", "editor^Hover^H^i^g^h^l^i^g^h^t", s_fuzzyScore);
        AssertMatches("dhhighlight", "editorHoverHighlight", null, s_fuzzyScore);
    }

    [Fact]
    public void FuzzyScore_issue_23746() => AssertMatches("-moz", "-moz-foo", "^-^m^o^z-foo", s_fuzzyScore);

    [Fact]
    public void FuzzyScore()
    {
        AssertMatches("ab", "abA", "^a^bA", s_fuzzyScore);
        AssertMatches("ccm", "cacmelCase", "^ca^c^melCase", s_fuzzyScore);
        AssertMatches("bti", "the_black_knight", null, s_fuzzyScore);
        AssertMatches("ccm", "camelCase", null, s_fuzzyScore);
        AssertMatches("cmcm", "camelCase", null, s_fuzzyScore);
        AssertMatches("BK", "the_black_knight", "the_^black_^knight", s_fuzzyScore);
        AssertMatches("KeyboardLayout=", "KeyboardLayout", null, s_fuzzyScore);
        AssertMatches("LLL", "SVisualLoggerLogsList", "SVisual^Logger^Logs^List", s_fuzzyScore);
        AssertMatches("LLLL", "SVilLoLosLi", null, s_fuzzyScore);
        AssertMatches("LLLL", "SVisualLoggerLogsList", null, s_fuzzyScore);
        AssertMatches("TEdit", "TextEdit", "^Text^E^d^i^t", s_fuzzyScore);
        AssertMatches("TEdit", "TextEditor", "^Text^E^d^i^tor", s_fuzzyScore);
        AssertMatches("TEdit", "Textedit", "^Text^e^d^i^t", s_fuzzyScore);
        AssertMatches("TEdit", "text_edit", "^text_^e^d^i^t", s_fuzzyScore);
        AssertMatches("TEditDit", "TextEditorDecorationType", "^Text^E^d^i^tor^Decorat^ion^Type", s_fuzzyScore);
        AssertMatches("TEdit", "TextEditorDecorationType", "^Text^E^d^i^torDecorationType", s_fuzzyScore);
        AssertMatches("Tedit", "TextEdit", "^Text^E^d^i^t", s_fuzzyScore);
        AssertMatches("ba", "?AB?", null, s_fuzzyScore);
        AssertMatches("bkn", "the_black_knight", "the_^black_^k^night", s_fuzzyScore);
        AssertMatches("bt", "the_black_knight", "the_^black_knigh^t", s_fuzzyScore);
        AssertMatches("ccm", "camelCasecm", "^camel^Casec^m", s_fuzzyScore);
        AssertMatches("fdm", "findModel", "^fin^d^Model", s_fuzzyScore);
        AssertMatches("fob", "foobar", "^f^oo^bar", s_fuzzyScore);
        AssertMatches("fobz", "foobar", null, s_fuzzyScore);
        AssertMatches("foobar", "foobar", "^f^o^o^b^a^r", s_fuzzyScore);
        AssertMatches("form", "editor.formatOnSave", "editor.^f^o^r^matOnSave", s_fuzzyScore);
        AssertMatches("g p", "Git: Pull", "^Git:^ ^Pull", s_fuzzyScore);
        AssertMatches("gip", "Git: Pull", "^G^it: ^Pull", s_fuzzyScore);
        AssertMatches("gp", "Git: Pull", "^Git: ^Pull", s_fuzzyScore);
        AssertMatches("gp", "Git_Git_Pull", "^Git_Git_^Pull", s_fuzzyScore);
        AssertMatches("is", "ImportStatement", "^Import^Statement", s_fuzzyScore);
        AssertMatches("is", "isValid", "^i^sValid", s_fuzzyScore);
        AssertMatches("lowrd", "lowWord", "^l^o^wWo^r^d", s_fuzzyScore);
        AssertMatches("myvable", "myvariable", "^m^y^v^aria^b^l^e", s_fuzzyScore);
        AssertMatches("no", "", null, s_fuzzyScore);
        AssertMatches("no", "match", null, s_fuzzyScore);
        AssertMatches("ob", "foobar", null, s_fuzzyScore);
        AssertMatches("sl", "SVisualLoggerLogsList", "^SVisual^LoggerLogsList", s_fuzzyScore);
        AssertMatches("sllll", "SVisualLoggerLogsList", "^SVisua^l^Logger^Logs^List", s_fuzzyScore);
        AssertMatches("Three", "HTMLHRElement", null, s_fuzzyScore);
        AssertMatches("Three", "Three", "^T^h^r^e^e", s_fuzzyScore);
        AssertMatches("fo", "barfoo", null, s_fuzzyScore);
        AssertMatches("fo", "bar_foo", "bar_^f^oo", s_fuzzyScore);
        AssertMatches("fo", "bar_Foo", "bar_^F^oo", s_fuzzyScore);
        AssertMatches("fo", "bar foo", "bar ^f^oo", s_fuzzyScore);
        AssertMatches("fo", "bar.foo", "bar.^f^oo", s_fuzzyScore);
        AssertMatches("fo", "bar/foo", "bar/^f^oo", s_fuzzyScore);
        AssertMatches("fo", "bar\\foo", "bar\\^f^oo", s_fuzzyScore);
    }

    [Fact]
    public void FuzzyScore_issue_28791() => AssertMatches("history", "history", "^h^i^s^t^o^r^y", s_fuzzyScore);

    [Fact]
    public void FuzzyScore_with_offset()
    {
        var matches = Filters.FuzzyScore("bc", "bc", 0, "abc", "abc", 1, new FuzzyScoreOptions(FirstMatchCanBeWeak: true, BoostFullMatch: true));
        matches.ShouldNotBeNull();
        Filters.CreateMatches(matches).ShouldBe([new FilterMatch(1, 3)]);
    }

    [Fact]
    public void FuzzyScoreGraceful()
    {
        AssertMatches("rlut", "result", null, s_fuzzyScore);
        AssertMatches("rlut", "result", "^res^u^l^t", s_graceful);

        AssertMatches("cno", "console", "^co^ns^ole", s_fuzzyScore);
        AssertMatches("cno", "console", "^co^ns^ole", s_graceful);
        AssertMatches("cno", "console", "^c^o^nsole", (p, lp, pp, w, lw, wp, o) => Filters.FuzzyScoreGracefulAggressive(p, lp, pp, w, lw, wp, o));
        AssertMatches("cno", "co_new", "^c^o_^new", s_graceful);
        AssertMatches("cno", "co_new", "^c^o_^new", (p, lp, pp, w, lw, wp, o) => Filters.FuzzyScoreGracefulAggressive(p, lp, pp, w, lw, wp, o));
    }

    [Fact]
    public void List_highlights_are_wrong_issue_29453()
    {
        AssertMatches("ff", "ffffffffffffffffffffffffff", "^f^f" + new string('f', 24), s_fuzzyScore);
    }

    [Fact]
    public void Patterns_use_case_sensitive_scores()
    {
        AssertMatches("FDM", "findModel", "^fin^d^Model", s_fuzzyScore);
    }

    [Fact]
    public void First_match_can_be_weak()
    {
        AssertMatches("Three", "HTMLHRElement", null, s_fuzzyScore);
        AssertMatches("ob", "foobar", "fo^o^bar", s_fuzzyScore, firstMatchCanBeWeak: true);
    }

    private static void AssertTopScore(FuzzyScorer filter, string pattern, int expected, params string[] words)
    {
        var topScore = -(100 * 10);
        var topIdx = 0;
        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            var m = filter(pattern, pattern.ToLowerInvariant(), 0, word, word.ToLowerInvariant(), 0, null);
            if (m is not null && m.Score > topScore)
            {
                topScore = m.Score;
                topIdx = i;
            }
        }

        topIdx.ShouldBe(expected, $"{pattern} -> actual={words[topIdx]} <> expected={words[expected]}");
    }

    [Fact]
    public void TopScore_fuzzyScore()
    {
        AssertTopScore(s_fuzzyScore, "cons", 2, "ArrayBufferConstructor", "Console", "console");
        AssertTopScore(s_fuzzyScore, "Foo", 1, "foo", "Foo", "foo");

        // #24904
        AssertTopScore(s_fuzzyScore, "onMess", 1, "onmessage", "onMessage", "onThisMegaEscape");

        AssertTopScore(s_fuzzyScore, "CC", 1, "camelCase", "CamelCase");
        AssertTopScore(s_fuzzyScore, "cC", 0, "camelCase", "CamelCase");

        AssertTopScore(s_fuzzyScore, "p", 4, "parse", "posix", "pafdsa", "path", "p");
        AssertTopScore(s_fuzzyScore, "pa", 0, "parse", "pafdsa", "path");

        // issue #14583
        AssertTopScore(s_fuzzyScore, "log", 3, "HTMLOptGroupElement", "ScrollLogicalPosition", "SVGFEMorphologyElement", "log", "logger");
        AssertTopScore(s_fuzzyScore, "e", 2, "AbstractWorker", "ActiveXObject", "else");

        // issue #14446
        AssertTopScore(s_fuzzyScore, "workbench.sideb", 1, "workbench.editor.defaultSideBySideLayout", "workbench.sideBar.location");

        // issue #11423
        AssertTopScore(s_fuzzyScore, "editor.r", 2, "diffEditor.renderSideBySide", "editor.overviewRulerlanes", "editor.renderControlCharacter", "editor.renderWhitespace");

        AssertTopScore(s_fuzzyScore, "-mo", 1, "-ms-ime-mode", "-moz-columns");

        // dupe, issue #14861
        AssertTopScore(s_fuzzyScore, "convertModelPosition", 0, "convertModelPositionToViewPosition", "convertViewToModelPosition");

        // dupe, issue #14942
        AssertTopScore(s_fuzzyScore, "is", 0, "isValidViewletId", "import statement");

        AssertTopScore(s_fuzzyScore, "title", 1, "files.trimTrailingWhitespace", "window.title");

        AssertTopScore(s_fuzzyScore, "const", 1, "constructor", "const", "cuOnstrul");
    }

    [Fact]
    public void Unfortunate_suggest_scoring_issue_26423()
    {
        AssertTopScore(s_fuzzyScore, "as", 1, "ace", "assert", "async");
        AssertTopScore(s_fuzzyScore, "Qui", 0, "Quick", "Quickly");
        AssertTopScore(s_fuzzyScore, "fo", 1, "baz", "foo");
    }

    [Fact]
    public void AnyScore_returns_zero_when_nothing_matches()
    {
        var score = Filters.AnyScore("xyz", "xyz", 0, "abc", "abc", 0);
        score.Score.ShouldBe(0);
        score.Matches.ShouldBeEmpty();
        Filters.AnyScore("b", "b", 0, "abc", "abc", 0).Matches.Length.ShouldBe(1);
    }

    [Fact]
    public void MatchesFuzzy_and_MatchesFuzzy2()
    {
        Filters.MatchesFuzzy("ab*c", "xabyyc").ShouldBe([new FilterMatch(1, 6)]);
        Filters.MatchesFuzzy("", "anything").ShouldBe([new FilterMatch(0, 0)]);
        Filters.MatchesFuzzy("cr", "CamelCaseRocks").ShouldBe([new FilterMatch(0, 1), new FilterMatch(9, 10)]);
        Filters.MatchesFuzzy2("fdm", "findModel").ShouldBe([new FilterMatch(0, 1), new FilterMatch(3, 5)]);
        Filters.MatchesFuzzy2("xyz", "findModel").ShouldBeNull();
    }

    [Fact]
    public void TryNormalizeToBase_keeps_the_length()
    {
        Filters.TryNormalizeToBase("\u00c4lles").ShouldBe("alles");
        Filters.TryNormalizeToBase("Hello").ShouldBe("hello");
    }
}
