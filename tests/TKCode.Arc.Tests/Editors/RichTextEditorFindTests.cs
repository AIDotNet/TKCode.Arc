using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Find and replace: the engine's matching and replacing, and the find bar.</summary>
public class RichTextEditorFindTests
{
    private static RichTextEditorEngine Engine(string html)
    {
        var engine = new RichTextEditorEngine { Docs = true };
        engine.Load(RichTextEditorHtml.ToDocument(html, docs: true), record: false, caretAtEnd: true);
        return engine;
    }

    [Fact]
    public void Matches_Stay_Inside_Blocks_And_Follow_The_Case_Setting()
    {
        var engine = Engine("<p>Cat cat CAT</p><pre>cat()</pre><table><tr><th>a cat</th></tr></table><p>ca</p><p>t</p>");
        engine.FindAll("cat", matchCase: false).Select(m => (m.Block, m.Start)).ShouldBe(new[] { (0, 0), (0, 4), (0, 8), (1, 0), (2, 2) });
        engine.FindAll("cat", matchCase: true).Count.ShouldBe(3);
        engine.FindAll("", matchCase: false).ShouldBeEmpty();
        // "ca" and "t" in two paragraphs never match across the boundary.
        engine.FindAll("cat", false).ShouldAllBe(m => m.Block != 3);
        // Occurrences do not overlap: the search goes on after each match.
        Engine("<p>aaaa</p>").FindAll("aa", false).Count.ShouldBe(2);
    }

    [Fact]
    public void The_First_Match_Is_The_One_At_Or_After_The_Selection()
    {
        var engine = Engine("<p>one two one</p><p>one</p>");
        var matches = engine.FindAll("one", false);
        engine.MatchFrom(matches, new RichTextEditorPosition(0, 1)).ShouldBe(1);
        engine.MatchFrom(matches, new RichTextEditorPosition(0, 8)).ShouldBe(1);
        engine.MatchFrom(matches, new RichTextEditorPosition(1, 2)).ShouldBe(0);
        engine.MatchFrom(new List<RichTextEditorMatch>(), default).ShouldBe(-1);
    }

    [Fact]
    public void Replacing_Keeps_The_Style_And_Selects_The_New_Text()
    {
        var engine = Engine("<p>Say <strong>hello</strong> to <a href=\"https://a.example\">hello</a></p>");
        var matches = engine.FindAll("hello", false);
        engine.ReplaceMatch(matches[0], "hello", "goodbye", false).ShouldBeTrue();
        engine.Selection.ShouldBe(RichTextEditorSelection.Range(0, 4, 11));
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<p>Say <strong>goodbye</strong> to <a href=\"https://a.example\">hello</a></p>");
        // A match whose text changed since it was found is not replaced.
        engine.ReplaceMatch(matches[0], "hello", "x", false).ShouldBeFalse();
        engine.ReplaceMatch(engine.FindAll("hello", false)[0], "hello", "hi", false).ShouldBeTrue();
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe("<p>Say <strong>goodbye</strong> to <a href=\"https://a.example\">hi</a></p>");
        engine.Undo();
        engine.Undo();
        RichTextEditorHtml.Write(engine.Blocks).ShouldContain("<strong>hello</strong>");
    }

    [Fact]
    public void Replace_All_Is_One_Step()
    {
        var engine = Engine("<p>a-b-c</p><ul><li>x-y</li></ul><pre>1-2</pre>");
        engine.ReplaceAll("-", " – ", false).ShouldBe(4);
        engine.Blocks[0].Text.ShouldBe("a – b – c");
        engine.Blocks[1].Text.ShouldBe("x – y");
        engine.Blocks[2].Text.ShouldBe("1 – 2");
        engine.Selection.Focus.ShouldBe(new RichTextEditorPosition(2, 4));
        engine.Undo();
        engine.Blocks[0].Text.ShouldBe("a-b-c");
        engine.Blocks[2].Text.ShouldBe("1-2");
        // An empty replacement deletes; nothing to replace changes nothing.
        engine.ReplaceAll("-", "", false).ShouldBe(4);
        engine.Blocks[0].Text.ShouldBe("abc");
        engine.ReplaceAll("-", "+", false).ShouldBe(0);
    }

    private static void Press(TextBox box, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        box.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = box });

    [AvaloniaFact]
    public void The_Find_Bar_Steps_Through_Matches_Replaces_And_Closes()
    {
        var editor = new RichTextEditor { Width = 520, Profile = RichTextEditorProfile.Docs, Html = "<p>red green red</p><p>blue red</p>" };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = editor });
        Dispatcher.UIThread.RunJobs();
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            // The selected word seeds the search.
            editor.Engine.SetSelection(RichTextEditorSelection.Range(0, 4, 9));
            editor.OpenFind();
            Dispatcher.UIThread.RunJobs();
            var bar = editor.FindBar!;
            editor.IsFindOpen.ShouldBeTrue();
            bar.FindBox.Text.ShouldBe("green");
            bar.FindBox.IsFocused.ShouldBeTrue();
            bar.ShowsReplace.ShouldBeFalse();

            bar.FindBox.Text = "red";
            Dispatcher.UIThread.RunJobs();
            surface.MatchCountForTests.ShouldBe(3);
            // Typing selects the first match at or after the selection (the selection was "green").
            editor.Engine.Selection.ShouldBe(RichTextEditorSelection.Range(0, 10, 13));
            bar.Count.Text.ShouldBe("2 of 3");
            Press(bar.FindBox, Key.Enter);
            editor.Engine.Selection.ShouldBe(RichTextEditorSelection.Range(1, 5, 8));
            Press(bar.FindBox, Key.Enter);
            editor.Engine.Selection.ShouldBe(RichTextEditorSelection.Range(0, 0, 3));
            Press(bar.FindBox, Key.Enter, KeyModifiers.Shift);
            bar.Count.Text.ShouldBe("3 of 3");

            // The replace row: Replace changes the current match and moves to the next; Replace all does the rest.
            editor.OpenFind(replace: true);
            bar.ShowsReplace.ShouldBeTrue();
            bar.ReplaceBox.Text = "orange";
            Press(bar.ReplaceBox, Key.Enter);
            editor.Engine.Blocks[1].Text.ShouldBe("blue orange");
            editor.Engine.Selection.ShouldBe(RichTextEditorSelection.Range(0, 0, 3));
            bar.Count.Text.ShouldBe("1 of 2");
            bar.ReplaceEvery.Invoke();
            editor.Engine.Blocks[0].Text.ShouldBe("orange green orange");
            surface.MatchCountForTests.ShouldBe(0);
            bar.Count.Text.ShouldBe("No results");

            Press(bar.FindBox, Key.Escape);
            editor.IsFindOpen.ShouldBeFalse();
            surface.MatchCountForTests.ShouldBe(0);
            editor.Undo();
            editor.Engine.Blocks[0].Text.ShouldBe("red green red");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Read_Only_Editor_Finds_But_Does_Not_Replace()
    {
        var editor = new RichTextEditor { Width = 520, IsReadOnly = true, Html = "<p>one one</p>" };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = editor });
        Dispatcher.UIThread.RunJobs();
        try
        {
            editor.OpenFind(replace: true);
            editor.FindBar!.ShowsReplace.ShouldBeFalse();
            editor.FindBar.FindBox.Text = "one";
            editor.CountMatches("ONE").ShouldBe(2);
            editor.CountMatches("ONE", matchCase: true).ShouldBe(0);
            editor.ReplaceAll("one", "two").ShouldBe(0);
            editor.CloseFind();
            editor.IsFindOpen.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
