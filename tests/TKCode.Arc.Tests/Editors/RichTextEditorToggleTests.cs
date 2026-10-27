using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>Toggle lists of the Docs profile: folding nested items away, editing around folds, Html and Markdown, and the layout.</summary>
public class RichTextEditorToggleTests
{
    private const string Folded =
        "<ul data-toggle=\"true\"><li data-collapsed=\"true\">Title<ul><li>one</li><li>two</li></ul></li></ul><p>after</p>";

    private static RichTextEditorEngine Engine(string html, bool docs = true)
    {
        var engine = new RichTextEditorEngine { Docs = docs };
        engine.Load(RichTextEditorHtml.ToDocument(html, docs), record: false, caretAtEnd: true);
        return engine;
    }

    [Fact]
    public void A_Collapsed_Toggle_Hides_Its_Nested_Items_And_Round_Trips()
    {
        var engine = Engine(Folded);
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Toggle);
        engine.Blocks[0].IsCollapsed.ShouldBeTrue();
        engine.Blocks[1].Level.ShouldBe(1);
        Enumerable.Range(0, 4).Select(engine.IsHidden).ShouldBe(new[] { false, true, true, false });
        RichTextEditorHtml.Write(engine.Blocks).ShouldBe(Folded);
        // Markdown has no toggles: the item is a plain one with its nested items.
        RichTextEditorMarkdown.Write(engine.Blocks).ShouldBe("- Title\n  - one\n  - two\n\nafter");
        // The Arc profile reads it as a bulleted list.
        Engine(Folded, docs: false).Blocks[0].Type.ShouldBe(RichTextBlockType.BulletedList);
        Engine(Folded, docs: false).IsHidden(1).ShouldBeFalse();
    }

    [Fact]
    public void Folding_Moves_The_Caret_Out_And_Navigation_Skips_What_Is_Folded()
    {
        var engine = Engine(Folded.Replace(" data-collapsed=\"true\"", string.Empty));
        engine.IsHidden(1).ShouldBeFalse();
        engine.SetSelection(RichTextEditorSelection.Caret(2, 1));
        engine.SetCollapsed(0, true).ShouldBeTrue();
        engine.Selection.ShouldBe(RichTextEditorSelection.Caret(0, 5));
        engine.SetCollapsed(0, true).ShouldBeFalse();
        // A selection set inside the fold stops at the toggle.
        engine.SetSelection(RichTextEditorSelection.Caret(1, 0));
        engine.Selection.ShouldBe(RichTextEditorSelection.Caret(0, 5));
        engine.Undo();
        engine.Blocks[0].IsCollapsed.ShouldBeFalse();
        // The Arc profile has no toggles to fold.
        Engine("<ul><li>x</li></ul>", docs: false).SetCollapsed(0, true).ShouldBeFalse();
    }

    [Fact]
    public void Enter_Starts_A_Nested_Item_Under_An_Open_Toggle_And_A_Sibling_After_A_Folded_One()
    {
        var open = Engine("<ul data-toggle=\"true\"><li>Title</li></ul>");
        open.SetSelection(RichTextEditorSelection.Caret(0, 5));
        open.InsertParagraph();
        (open.Blocks[1].Type, open.Blocks[1].Level).ShouldBe((RichTextBlockType.BulletedList, 1));
        open.Selection.ShouldBe(RichTextEditorSelection.Caret(1, 0));

        var folded = Engine(Folded);
        folded.SetSelection(RichTextEditorSelection.Caret(0, 5));
        folded.InsertParagraph();
        (folded.Blocks[3].Type, folded.Blocks[3].Level, folded.Blocks[3].IsCollapsed).ShouldBe((RichTextBlockType.Toggle, 0, false));
        folded.Selection.ShouldBe(RichTextEditorSelection.Caret(3, 0));
        folded.Blocks[0].IsCollapsed.ShouldBeTrue();
    }

    [Fact]
    public void Edits_That_Reach_Into_A_Fold_Unfold_It()
    {
        // Tab nests any item under a toggle; under a folded one, the toggle opens to show it.
        var engine = Engine(Folded.Replace("<p>after</p>", "<ul><li>three</li></ul>"));
        engine.SetSelection(RichTextEditorSelection.Caret(3, 0));
        engine.IndentListItem().ShouldBeTrue();
        engine.Blocks[3].Level.ShouldBe(1);
        engine.Blocks[0].IsCollapsed.ShouldBeFalse();

        // Delete at the end of a folded toggle opens it instead of pulling a hidden item into the title.
        var folded = Engine(Folded);
        folded.SetSelection(RichTextEditorSelection.Caret(0, 5));
        folded.DeleteForward();
        folded.Blocks[0].Text.ShouldBe("Title");
        folded.Blocks[0].IsCollapsed.ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Folded_Items_Take_No_Room_And_The_Arrow_Folds()
    {
        var editor = new RichTextEditor { Width = 480, Profile = RichTextEditorProfile.Docs, Html = Folded.Replace(" data-collapsed=\"true\"", string.Empty) };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20), Child = editor });
        Dispatcher.UIThread.RunJobs();
        try
        {
            var surface = TestHelpers.Descendant<RichTextEditorSurface>(editor);
            var afterOpen = surface.CaretRect(new RichTextEditorPosition(3, 0)).Y;
            var arrow = surface.ToggleArrowBounds(0);
            window.MouseDown(surface.TranslatePoint(arrow.Center, window)!.Value, MouseButton.Left);
            window.MouseUp(surface.TranslatePoint(arrow.Center, window)!.Value, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.Engine.Blocks[0].IsCollapsed.ShouldBeTrue();
            var afterFolded = surface.CaretRect(new RichTextEditorPosition(3, 0)).Y;
            afterFolded.ShouldBeLessThan(afterOpen - 20);
            surface.BlockHeightForTests(1).ShouldBeGreaterThan(0);
            // Down from the toggle goes straight to the paragraph after the fold, and the fold's area belongs to the toggle.
            surface.MoveVertical(new RichTextEditorPosition(0, 2), surface.CaretRect(new RichTextEditorPosition(0, 2)).X, 1).Block.ShouldBe(3);
            surface.MoveVertical(new RichTextEditorPosition(3, 0), surface.CaretRect(new RichTextEditorPosition(3, 0)).X, -1).Block.ShouldBe(0);
            surface.BlockIndexAtY(afterFolded - 4).ShouldNotBe(1);
            surface.BlockIndexAtY(afterFolded - 4).ShouldNotBe(2);

            editor.Engine.SetSelection(RichTextEditorSelection.Caret(0, 1));
            editor.ToggleFold().ShouldBeTrue();
            editor.Engine.Blocks[0].IsCollapsed.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }
}
