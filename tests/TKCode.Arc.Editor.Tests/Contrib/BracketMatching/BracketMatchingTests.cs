// Ported from vscode (src/vs/editor/contrib/bracketMatching/test/browser/bracketMatching.test.ts, commit b57d5c9e), MIT
// license. Differences from upstream: the controller's commands are the engine's BracketMatching methods on the test
// editor; the highlight tests at the end are not upstream's.

using TKCode.Arc.Editor.Contrib;
using TKCode.Arc.Editor.Contrib.BracketMatching;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Shouldly;
using Xunit;
using static TKCode.Arc.Editor.Tests.Contrib.EditingAssert;
using BracketMatchingImpl = TKCode.Arc.Editor.Contrib.BracketMatching.BracketMatching;

namespace TKCode.Arc.Editor.Tests.Contrib;

public sealed class BracketMatchingTests
{
    private static EditingTestEditor CreateCodeEditorWithBrackets(string text)
    {
        const string languageId = "bracketMode";
        var service = new LanguageConfigurationService();
        service.Register(languageId, new LanguageConfiguration { Brackets = [("{", "}"), ("[", "]"), ("(", ")")] });
        return new EditingTestEditor(TestCodeEditor.Create(text, new TestCodeEditorOptions { LanguageId = languageId, LanguageConfigurationService = service }), service);
    }

    [Fact]
    public void Issue183JumpToMatchingBracketPosition()
    {
        using var editor = CreateCodeEditorWithBrackets("var x = (3 + (5-7)) + ((5+3)+5);");

        // start on closing bracket
        editor.SetPosition(new TextPosition(1, 20));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 9));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 19));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 9));

        // start on opening bracket
        editor.SetPosition(new TextPosition(1, 23));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 31));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 23));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 31));
    }


    [Fact]
    public void JumpToNextBracket()
    {
        using var editor = CreateCodeEditorWithBrackets("var x = (3 + (5-7)); y();");

        // start position between brackets
        editor.SetPosition(new TextPosition(1, 16));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 18));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 14));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 18));

        // skip brackets in comments
        editor.SetPosition(new TextPosition(1, 21));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 23));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 24));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 23));

        // do not break if no brackets are available
        editor.SetPosition(new TextPosition(1, 26));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 26));
    }


    [Fact]
    public void SelectToNextBracket()
    {
        using var editor = CreateCodeEditorWithBrackets("var x = (3 + (5-7)); y();");

        // start position in open brackets
        editor.SetPosition(new TextPosition(1, 9));
        BracketMatchingImpl.SelectToBracket(editor, true);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 20));
        AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 9, 1, 20));

        // start position in close brackets (should select backwards)
        editor.SetPosition(new TextPosition(1, 20));
        BracketMatchingImpl.SelectToBracket(editor, true);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 9));
        AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 20, 1, 9));

        // start position between brackets
        editor.SetPosition(new TextPosition(1, 16));
        BracketMatchingImpl.SelectToBracket(editor, true);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 19));
        AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 14, 1, 19));

        // start position outside brackets
        editor.SetPosition(new TextPosition(1, 21));
        BracketMatchingImpl.SelectToBracket(editor, true);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 25));
        AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 23, 1, 25));

        // do not break if no brackets are available
        editor.SetPosition(new TextPosition(1, 26));
        BracketMatchingImpl.SelectToBracket(editor, true);
        AssertDeepStrictEqual(editor.GetPosition(), new TextPosition(1, 26));
        AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 26, 1, 26));
    }


    [Fact]
    public void Issue1772JumpToEnclosingBrackets()
    {
        var text = string.Join("\n", new[] {
            "const x = {",
            "    something: [0, 1, 2],",
            "    another: true,",
            "    somethingmore: [0, 2, 4]",
            "};"
        });
        using var editor = CreateCodeEditorWithBrackets(text);

        editor.SetPosition(new TextPosition(3, 5));
        BracketMatchingImpl.JumpToBracket(editor);
        AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(5, 1, 5, 1));
    }


    [Fact]
    public void Issue43371ArgumentToNotSelectBrackets()
    {
        var text = string.Join("\n", new[] {
            "const x = {",
            "    something: [0, 1, 2],",
            "    another: true,",
            "    somethingmore: [0, 2, 4]",
            "};"
        });
        using var editor = CreateCodeEditorWithBrackets(text);

        editor.SetPosition(new TextPosition(3, 5));
        BracketMatchingImpl.SelectToBracket(editor, false);
        AssertDeepStrictEqual(editor.GetSelection(), new TextSelection(1, 12, 5, 1));
    }


    [Fact]
    public void Issue45369SelectToBracketWithMulticursor()
    {
        using var editor = CreateCodeEditorWithBrackets("{  }   {   }   { }");

        // cursors inside brackets become selections of the entire bracket contents
        editor.SetSelections([
            new TextSelection(1, 3, 1, 3),
            new TextSelection(1, 10, 1, 10),
            new TextSelection(1, 17, 1, 17)
        ]);
        BracketMatchingImpl.SelectToBracket(editor, true);
        AssertDeepStrictEqual(editor.GetSelections(), [
            new TextSelection(1, 1, 1, 5),
            new TextSelection(1, 8, 1, 13),
            new TextSelection(1, 16, 1, 19)
        ]);

        // cursors to the left of bracket pairs become selections of the entire pair
        editor.SetSelections([
            new TextSelection(1, 1, 1, 1),
            new TextSelection(1, 6, 1, 6),
            new TextSelection(1, 14, 1, 14)
        ]);
        BracketMatchingImpl.SelectToBracket(editor, true);
        AssertDeepStrictEqual(editor.GetSelections(), [
            new TextSelection(1, 1, 1, 5),
            new TextSelection(1, 8, 1, 13),
            new TextSelection(1, 16, 1, 19)
        ]);

        // cursors just right of a bracket pair become selections of the entire pair
        editor.SetSelections([
            new TextSelection(1, 5, 1, 5),
            new TextSelection(1, 13, 1, 13),
            new TextSelection(1, 19, 1, 19)
        ]);
        BracketMatchingImpl.SelectToBracket(editor, true);
        AssertDeepStrictEqual(editor.GetSelections(), [
            new TextSelection(1, 5, 1, 1),
            new TextSelection(1, 13, 1, 8),
            new TextSelection(1, 19, 1, 16)
        ]);
    }


    [Fact]
    public void RemovesBrackets()
    {
        using var editor = CreateCodeEditorWithBrackets("var x = (3 + (5-7)); y();");
        void removeBrackets() => BracketMatchingImpl.RemoveBrackets(editor, null);

        // position before the bracket
        editor.SetPosition(new TextPosition(1, 9));
        removeBrackets();
        AssertDeepStrictEqual(editor.GetModel().GetValue(), "var x = 3 + (5-7); y();");
        editor.GetModel().SetValue("var x = (3 + (5-7)); y();");

        // position between brackets
        editor.SetPosition(new TextPosition(1, 16));
        removeBrackets();
        AssertDeepStrictEqual(editor.GetModel().GetValue(), "var x = (3 + 5-7); y();");
        removeBrackets();
        AssertDeepStrictEqual(editor.GetModel().GetValue(), "var x = 3 + 5-7; y();");
        removeBrackets();
        AssertDeepStrictEqual(editor.GetModel().GetValue(), "var x = 3 + 5-7; y();");
    }

    // Not upstream: the highlight (_updateBrackets) of the bracket at a cursor, of the enclosing brackets with
    // matchBrackets 'always', and nothing without focus.
    [Fact]
    public void HighlightsTheMatchingBracketAtACursor()
    {
        using var editor = CreateCodeEditorWithBrackets("var x = (3 + (5-7)); y();");
        var state = new BracketMatchingState();

        editor.SetPosition(new TextPosition(1, 9));
        var decorations = state.Update(editor, true, MatchBracketsMode.Near)!;
        decorations.Select(d => d.Range).ShouldBe([new TextRange(1, 9, 1, 10), new TextRange(1, 19, 1, 20)]);
        decorations.ShouldAllBe(d => d.Options == BracketMatchingImpl.DecorationOptionsWithOverviewRuler);
        decorations[0].Options.OverviewRuler.ShouldNotBeNull();

        // between brackets: nothing near, the enclosing pair with 'always'
        editor.SetPosition(new TextPosition(1, 16));
        state.Update(editor, true, MatchBracketsMode.Near)!.ShouldBeEmpty();

        // the option changed (upstream's onDidChangeConfiguration resets the cache)
        state.Reset();
        decorations = state.Update(editor, true, MatchBracketsMode.Always)!;
        decorations.Select(d => d.Range).ShouldBe([new TextRange(1, 14, 1, 15), new TextRange(1, 18, 1, 19)]);
        decorations.ShouldAllBe(d => d.Options == BracketMatchingImpl.DecorationOptionsWithoutOverviewRuler);

        // never: the decorations stay as they are; no focus: none
        state.Update(editor, true, MatchBracketsMode.Never).ShouldBeNull();
        state.Update(editor, false, MatchBracketsMode.Always)!.ShouldBeEmpty();
    }

    [Fact]
    public void HighlightsBracketsAtEveryCollapsedCursor()
    {
        using var editor = CreateCodeEditorWithBrackets("{  }   {   }   { }");
        var state = new BracketMatchingState();
        editor.SetSelections([new TextSelection(1, 16, 1, 16), new TextSelection(1, 1, 1, 1), new TextSelection(1, 8, 1, 10)]);

        var decorations = state.Update(editor, true, MatchBracketsMode.Near)!;
        decorations.Select(d => d.Range).ShouldBe(
        [
            new TextRange(1, 1, 1, 2), new TextRange(1, 4, 1, 5),
            new TextRange(1, 16, 1, 17), new TextRange(1, 18, 1, 19),
        ]);
    }
}
