using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Editors;

/// <summary>The block handle: whole-block moves, copies and removals in the engine, and the hover handle, drag and menu on the control.</summary>
public class RichTextEditorBlockHandleTests
{
    private static RichTextEditorEngine Engine(string html)
    {
        var engine = new RichTextEditorEngine { Docs = true };
        engine.Load(RichTextEditorHtml.ToDocument(html, docs: true), record: false);
        return engine;
    }

    private static string Html(RichTextEditorEngine engine) => RichTextEditorHtml.Write(engine.Blocks);

    [Fact]
    public void Move_Takes_A_List_Item_With_Its_Children()
    {
        var engine = Engine("<p>a</p><ul><li>one<ul><li>child</li></ul></li><li>two</li></ul><p>z</p>");
        RichTextEditorEngine.UnitExtent(engine.Blocks, 1).ShouldBe((1, 3));
        engine.MoveUnit(1, engine.Blocks.Count).ShouldBeTrue();
        Html(engine).ShouldBe("<p>a</p><ul><li>two</li></ul><p>z</p><ul><li>one<ul><li>child</li></ul></li></ul>");
        engine.Undo().ShouldBeTrue();
        Html(engine).ShouldBe("<p>a</p><ul><li>one<ul><li>child</li></ul></li><li>two</li></ul><p>z</p>");
    }

    [Fact]
    public void Move_Rejects_Gaps_Inside_The_Unit_And_Inside_Tables()
    {
        var engine = Engine("<p>a</p><p>b</p><p>c</p>");
        engine.MoveUnit(1, 1).ShouldBeFalse();
        engine.MoveUnit(1, 2).ShouldBeFalse();
        engine.MoveUnit(0, 2).ShouldBeTrue();
        Html(engine).ShouldBe("<p>b</p><p>a</p><p>c</p>");
        engine.MoveUnit(0, 99).ShouldBeFalse();

        var table = new RichTextEditorEngine { Docs = true };
        var blocks = RichTextEditorHtml.ToDocument("<p>x</p>", docs: true).AddRange(RichTextEditorTables.Create(2, 2)).Add(RichTextEditorBlock.Plain(RichTextBlockType.Paragraph, "y"));
        table.Load(blocks, record: false);
        RichTextEditorEngine.IsDropGap(table.Blocks, 1).ShouldBeTrue();
        RichTextEditorEngine.IsDropGap(table.Blocks, 2).ShouldBeFalse();
        RichTextEditorEngine.UnitExtent(table.Blocks, 3).ShouldBe((1, 5));
        table.MoveUnit(2, 0).ShouldBeTrue();
        table.Blocks[0].StartsTable.ShouldBeTrue();
        table.Blocks[4].Text.ShouldBe("x");
    }

    [Fact]
    public void Moving_A_Nested_Item_Out_Keeps_The_List_Levels_Valid()
    {
        var engine = Engine("<p>a</p><ul><li>one<ul><li>child</li></ul></li></ul>");
        engine.MoveUnit(2, 0).ShouldBeTrue();
        engine.Blocks[0].Text.ShouldBe("child");
        engine.Blocks[0].Level.ShouldBe(0);
    }

    [Fact]
    public void Duplicate_Delete_And_Insert_After_Work_On_Units()
    {
        var engine = Engine("<h2>t</h2><ul><li>one<ul><li>c</li></ul></li></ul>");
        engine.DuplicateUnit(1).ShouldBeTrue();
        Html(engine).ShouldBe("<h2>t</h2><ul><li>one<ul><li>c</li></ul></li><li>one<ul><li>c</li></ul></li></ul>");
        engine.Selection.Focus.Block.ShouldBe(3);

        engine.DeleteUnit(1).ShouldBeTrue();
        engine.DeleteUnit(1).ShouldBeTrue();
        Html(engine).ShouldBe("<h2>t</h2>");
        engine.DeleteUnit(0).ShouldBeTrue();
        engine.Blocks.Count.ShouldBe(1);
        engine.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);

        var after = Engine("<p>a</p><p>b</p>");
        after.InsertParagraphAfterUnit(0).ShouldBe(1);
        after.Blocks.Count.ShouldBe(3);
        after.Selection.Focus.ShouldBe(new RichTextEditorPosition(1, 0));

        var typed = Engine("<p>a</p><p>b</p>");
        typed.SetBlockTypeAt(1, RichTextBlockType.Heading2);
        typed.Blocks[1].Type.ShouldBe(RichTextBlockType.Heading2);
    }

    private static (RichTextEditor Editor, Window Window, RichTextEditorSurface Surface) Show(string html, RichTextEditorProfile profile = RichTextEditorProfile.Docs)
    {
        var editor = new RichTextEditor { Width = 480, Profile = profile, Html = html };
        var window = TestHelpers.Show(new Border { Padding = new Thickness(20, 80, 20, 20), Child = editor });
        window.Width = 540;
        window.Height = 600;
        Dispatcher.UIThread.RunJobs();
        return (editor, window, TestHelpers.Descendant<RichTextEditorSurface>(editor));
    }

    private static Point Center(Control control, Window window) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static Point InWindow(RichTextEditorSurface surface, Window window, Point point) => surface.TranslatePoint(point, window)!.Value;

    private static void Hover(RichTextEditorSurface surface, Window window, int block)
    {
        var point = InWindow(surface, window, new Point(40, surface.HandleY(block)));
        window.MouseMove(point);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Hovering_A_Block_Shows_The_Handle_Beside_It_Only_In_The_Docs_Profile()
    {
        var (editor, window, surface) = Show("<p>one</p><p>two</p><p>three</p>");
        try
        {
            Hover(surface, window, 1);
            var handle = editor.BlockHandle!;
            handle.Block.ShouldBe(1);
            handle.Grip.IsVisible.ShouldBeTrue();
            var grip = Center(handle.Grip, window);
            var text = InWindow(surface, window, new Point(0, surface.HandleY(1)));
            Math.Abs(grip.Y - text.Y).ShouldBeLessThan(3);
            grip.X.ShouldBeLessThan(text.X);

            Hover(surface, window, 2);
            handle.Block.ShouldBe(2);

            editor.IsReadOnly = true;
            Dispatcher.UIThread.RunJobs();
            handle.Refresh();
            handle.Grip.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }

        var (arc, arcWindow, arcSurface) = Show("<p>one</p>", RichTextEditorProfile.Arc);
        try
        {
            Hover(arcSurface, arcWindow, 0);
            arc.BlockHandle.ShouldBeNull();
        }
        finally
        {
            arcWindow.Close();
        }
    }

    [AvaloniaFact]
    public void The_Handle_Holds_While_The_Pointer_Heads_For_It_And_Sits_Beside_Nested_Items()
    {
        var (editor, window, surface) = Show("<p>one</p><p>two</p><p>three</p><ul><li>a<ul><li>b</li></ul></li></ul>");
        try
        {
            var start = InWindow(surface, window, new Point(300, surface.HandleY(0)));
            window.MouseMove(start);
            window.MouseMove(start);
            Dispatcher.UIThread.RunJobs();
            var handle = editor.BlockHandle!;
            handle.Block.ShouldBe(0);

            // Far from the handle it follows at once, even on a move to the left.
            window.MouseMove(InWindow(surface, window, new Point(260, surface.BlockTop(1) + 2)));
            Dispatcher.UIThread.RunJobs();
            handle.Block.ShouldBe(1);
            window.MouseMove(start);
            window.MouseMove(start);
            Dispatcher.UIThread.RunJobs();
            handle.Block.ShouldBe(0);

            // Drifting into the next block on the way left to the grip keeps the handle where it is.
            window.MouseMove(InWindow(surface, window, new Point(20, surface.BlockTop(1) + 2)));
            Dispatcher.UIThread.RunJobs();
            handle.Block.ShouldBe(0);

            // A move that no longer points at the handle follows the pointer again.
            window.MouseMove(InWindow(surface, window, new Point(20, surface.HandleY(2))));
            Dispatcher.UIThread.RunJobs();
            handle.Block.ShouldBe(2);

            Hover(surface, window, 3);
            handle.Block.ShouldBe(3);
            Hover(surface, window, 4);
            handle.Block.ShouldBe(4);
            surface.HandleX(4).ShouldBeGreaterThan(surface.HandleX(3));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Dragging_The_Grip_Moves_The_Block_And_Undo_Restores_It()
    {
        var (editor, window, surface) = Show("<p>one</p><p>two</p><p>three</p>");
        try
        {
            Hover(surface, window, 0);
            var handle = editor.BlockHandle!;
            var grip = Center(handle.Grip, window);
            window.MouseMove(grip);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(grip, MouseButton.Left);
            var target = InWindow(surface, window, new Point(40, surface.GapY(3) + 2));
            window.MouseMove(new Point(grip.X, grip.Y + 10));
            window.MouseMove(new Point(grip.X, target.Y));
            Dispatcher.UIThread.RunJobs();
            handle.IsDragging.ShouldBeTrue();
            handle.Indicator.IsVisible.ShouldBeTrue();
            surface.ActiveUnit.ShouldBe((0, 1));
            window.MouseUp(new Point(grip.X, target.Y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            handle.IsDragging.ShouldBeFalse();
            handle.Indicator.IsVisible.ShouldBeFalse();
            surface.ActiveUnit.ShouldBeNull();
            editor.GetHtml().ShouldBe("<p>two</p><p>three</p><p>one</p>");
            editor.Undo();
            editor.GetHtml().ShouldBe("<p>one</p><p>two</p><p>three</p>");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_The_Grip_Opens_The_Block_Menu_With_Turn_Into_Duplicate_And_Delete()
    {
        var (editor, window, surface) = Show("<p>one</p><p>two</p>");
        try
        {
            Hover(surface, window, 1);
            var handle = editor.BlockHandle!;
            var grip = Center(handle.Grip, window);
            window.MouseMove(grip);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(grip, MouseButton.Left);
            window.MouseUp(grip, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            handle.Menu.ShouldNotBeNull();
            handle.Menu!.IsOpen.ShouldBeTrue();
            var headers = handle.Menu.Items.OfType<MenuEntry>().Select(e => e.Header as string).ToList();
            headers.ShouldContain("Heading 2");
            headers.ShouldContain("Duplicate");
            headers.ShouldContain("Delete");
            handle.Menu.Items.OfType<MenuEntry>().First(e => (e.Header as string) == "Text").IsChecked.ShouldBeTrue();
            surface.ActiveUnit.ShouldBe((1, 2));
            handle.Menu.Close(returnFocus: false);
            Dispatcher.UIThread.RunJobs();
            surface.ActiveUnit.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Empty_Line_Shows_Plus_Which_Opens_The_Slash_Menu_There()
    {
        var (editor, window, surface) = Show("<p>one</p><p></p>");
        try
        {
            Hover(surface, window, 0);
            var handle = editor.BlockHandle!;
            handle.Grip.IsVisible.ShouldBeTrue();
            handle.Add.IsVisible.ShouldBeFalse();
            Hover(surface, window, 1);
            handle.Block.ShouldBe(1);
            handle.Grip.IsVisible.ShouldBeFalse();
            handle.Add.IsVisible.ShouldBeTrue();
            var plus = Center(handle.Add, window);
            window.MouseMove(plus);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(plus, MouseButton.Left);
            window.MouseUp(plus, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            editor.GetHtml().ShouldBe("<p>one</p><p>/</p>");
            editor.IsSlashOpenForTests.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Shift_Command_Arrows_Move_The_Block_Under_The_Caret()
    {
        var (editor, window, _) = Show("<p>one</p><p>two</p>");
        try
        {
            editor.FocusEditor();
            Dispatcher.UIThread.RunJobs();
            editor.SelectAll();
            window.KeyPressQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Control | RawInputModifiers.Shift);
            window.KeyReleaseQwerty(PhysicalKey.ArrowUp, RawInputModifiers.Control | RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();
            // The caret block (the last, after Select all) moved above the first.
            editor.GetHtml().ShouldBe("<p>two</p><p>one</p>");
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Callouts_Round_Trip_Through_Html_And_Alerts_And_Behave_Like_Quotes_On_Enter()
    {
        var engine = Engine("<aside data-callout=\"green\" data-icon=\"✅\"><p>one</p><p>two</p></aside><p>after</p>");
        engine.Blocks.Select(b => b.Type).ShouldBe(new[] { RichTextBlockType.Callout, RichTextBlockType.Callout, RichTextBlockType.Paragraph });
        engine.Blocks[0].Tone.ShouldBe(RichTextColor.Green);
        engine.Blocks[0].Icon.ShouldBe("✅");
        var html = Html(engine);
        html.ShouldBe("<aside data-callout=\"green\" data-icon=\"✅\"><p>one</p><p>two</p></aside><p>after</p>");
        RichTextEditorMarkdown.Write(engine.Blocks).ShouldBe("> [!TIP]\n> one\n> two\n\nafter");
        RichTextEditorMarkdown.ToDocument("> [!WARNING]\n> careful", docs: true)[0].Tone.ShouldBe(RichTextColor.Yellow);
        RichTextEditorMarkdown.ToDocument("> [!WARNING]\n> careful", docs: false)[0].Type.ShouldBe(RichTextBlockType.Quote);

        // The box is one unit; a new paragraph continues it; an empty one leaves it.
        RichTextEditorEngine.UnitExtent(engine.Blocks, 1).ShouldBe((0, 2));
        engine.SetSelection(RichTextEditorSelection.Caret(1, 3));
        engine.InsertParagraph();
        engine.Blocks[2].Type.ShouldBe(RichTextBlockType.Callout);
        engine.Blocks[2].Tone.ShouldBe(RichTextColor.Green);
        engine.InsertParagraph();
        engine.Blocks[2].Type.ShouldBe(RichTextBlockType.Paragraph);

        // Conversion gives the default look; changing it applies to the whole box.
        var plain = Engine("<p>a</p><p>b</p>");
        plain.SetBlockTypeAt(0, RichTextBlockType.Callout);
        plain.Blocks[0].Tone.ShouldBe(RichTextColor.Blue);
        plain.Blocks[0].Icon.ShouldBe(RichTextEditorBlock.DefaultCalloutIcon);
        plain.SetCallout(0, RichTextColor.Red, "🔥").ShouldBeTrue();
        plain.Blocks[0].Tone.ShouldBe(RichTextColor.Red);

        var arc = new RichTextEditorEngine();
        arc.SetBlockType(RichTextBlockType.Callout);
        arc.Blocks[0].Type.ShouldBe(RichTextBlockType.Paragraph);
    }

    [AvaloniaFact]
    public void A_Callout_Draws_With_An_Icon_Indent_And_Offers_Color_And_Icon_In_The_Block_Menu()
    {
        var (editor, window, surface) = Show("<aside data-callout=\"blue\" data-icon=\"💡\"><p>tip one</p><p>tip two</p></aside><p>text</p>");
        try
        {
            var inside = surface.CaretRect(new RichTextEditorPosition(0, 0));
            var plain = surface.CaretRect(new RichTextEditorPosition(2, 0));
            inside.X.ShouldBeGreaterThan(plain.X + 30);
            // The two paragraphs of one box touch.
            surface.BlockTop(1).ShouldBe(surface.BlockTop(0) + surface.BlockHeightForTests(0), 0.5);

            Hover(surface, window, 0);
            var grip = Center(editor.BlockHandle!.Grip, window);
            window.MouseMove(grip);
            Dispatcher.UIThread.RunJobs();
            window.MouseDown(grip, MouseButton.Left);
            window.MouseUp(grip, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            var headers = editor.BlockHandle.Menu!.Items.OfType<MenuEntry>().Select(e => e.Header as string).ToList();
            headers.ShouldContain("Callout");
            headers.ShouldContain("Color: Green");
            headers.ShouldContain("Icon: ⚠️");
            surface.ActiveUnit.ShouldBe((0, 2));
            editor.BlockHandle.Menu.Close(returnFocus: false);
        }
        finally
        {
            window.Close();
        }
    }
}
