using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.CodeAction;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using CodeAction = TKCode.Arc.Editor.Languages.CodeAction;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// Code actions in the editor (upstream's codeActionController.ts, codeActionCommands.ts, lightBulbWidget.ts and the
/// action widget): the menu on Ctrl+., moving through it and applying an action, the lightbulb in the glyph margin,
/// auto-apply, the not-available message and edits of other documents.
/// </summary>
public class CodeEditorCodeActionTests
{
    internal static RawInputModifiers CtrlCmd => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    internal static (CodeEditor Editor, Window Window) Show(string text, Action<CodeEditor>? setup = null)
    {
        var editor = new CodeEditor { Text = text, Width = 400, Height = 300 };
        setup?.Invoke(editor);
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Render(window);
        return (editor, window);
    }

    internal static void Render(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame()?.Dispose();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Provider(Func<TextModel, TextRange, CodeActionContext, IReadOnlyList<CodeAction>> provide, IReadOnlyList<string>? kinds = null) : ICodeActionProvider
    {
        public int Calls { get; private set; }

        public IReadOnlyList<string>? ProvidedCodeActionKinds => kinds;

        public Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token)
        {
            Calls++;
            return Task.FromResult<CodeActionList?>(new CodeActionList(provide(model, range, context)));
        }
    }

    private static CodeAction Replace(TextModel model, string title, TextRange range, string text, string kind = "quickfix") =>
        new(title)
        {
            Kind = kind,
            Edit = new WorkspaceEdit([new WorkspaceTextEdit(model.Uri, new TextEdit(range, text))]),
        };

    [AvaloniaFact]
    public void Ctrl_period_shows_the_menu_and_Enter_applies_the_focused_action()
    {
        var (editor, window) = Show("let x = 1;\nlet y = 2;", e => e.LanguageFeatures.CodeActionProvider.Register("plaintext", new Provider((model, _, _) =>
        [
            Replace(model, "Rename to a", new TextRange(1, 5, 1, 6), "a"),
            Replace(model, "Extract constant", new TextRange(1, 9, 1, 10), "ONE", "refactor.extract"),
        ])));

        window.KeyPressQwerty(PhysicalKey.Period, CtrlCmd);
        Render(window);

        var controller = CodeActionController.Get(editor).ShouldNotBeNull();
        var menu = controller.Menu.ShouldNotBeNull();
        menu.Items.Select(i => i.IsHeader ? "# " + i.Group.Title : i.Label).ShouldBe(["# Quick Fix", "Rename to a", "# Extract", "Extract constant"]);
        menu.FocusedItem!.Action.Title.ShouldBe("Rename to a");
        editor.ContextKeyService.ContextMatchesRules(CodeEditorContextKeys.CodeActionMenuVisible).ShouldBeTrue();
        editor.GetContentWidgetPlacement(menu.Id).ShouldNotBeNull();

        // Down skips the header; Enter applies
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        menu.FocusedItem!.Action.Title.ShouldBe("Extract constant");
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        menu.FocusedItem!.Action.Title.ShouldBe("Rename to a");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);

        editor.Text.ShouldBe("let a = 1;\nlet y = 2;");
        controller.Menu.ShouldBeNull();
        editor.ContextKeyService.ContextMatchesRules(CodeEditorContextKeys.CodeActionMenuVisible).ShouldBeFalse();

        // one undo step
        editor.Trigger("keyboard", "undo", null);
        editor.Text.ShouldBe("let x = 1;\nlet y = 2;");
    }

    [AvaloniaFact]
    public void Escape_hides_the_menu_and_no_actions_shows_a_message()
    {
        var actions = new List<CodeAction>();
        var (editor, window) = Show("abc", e => e.LanguageFeatures.CodeActionProvider.Register("plaintext", new Provider((_, _, _) => actions)));

        window.KeyPressQwerty(PhysicalKey.Period, CtrlCmd);
        Render(window);
        CodeActionController.Get(editor)!.Menu.ShouldBeNull();
        MessageController.Get(editor)!.Text.ShouldBe("No code actions available");

        actions.Add(new CodeAction("Do it") { Kind = "quickfix", Command = new LanguageCommand("test.doIt", "Do it") });
        window.KeyPressQwerty(PhysicalKey.Period, CtrlCmd);
        Render(window);
        CodeActionController.Get(editor)!.Menu.ShouldNotBeNull();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        CodeActionController.Get(editor)!.Menu.ShouldBeNull();
    }

    [AvaloniaFact]
    public void The_lightbulb_shows_in_the_glyph_margin_and_a_click_opens_the_menu()
    {
        var (editor, window) = Show("foo bar", e => e.LanguageFeatures.CodeActionProvider.Register("plaintext", new Provider((model, _, _) =>
            [Replace(model, "Fix it", new TextRange(1, 1, 1, 4), "FOO")])));
        editor.SetPosition(new TextPosition(1, 2));
        var controller = CodeActionController.Get(editor).ShouldNotBeNull();
        controller.Model.Trigger(new CodeActionTrigger(CodeActionTriggerType.Auto, CodeActionTriggerSource.Default));
        Render(window);

        editor.Gutter.GlyphMargin.IsVisible.ShouldBeTrue();
        controller.LightBulbLine.ShouldBe(1);
        editor.Model.GetLineDecorations(1).ShouldContain(d => d.Options.GlyphMarginClassName == CodeActionController.LightBulbClassName);

        // a press on the glyph margin of that line
        var view = editor.View!;
        var layout = view.GetGutterLayout();
        var glyph = layout.Columns.Single(c => c.Column.Id == CodeEditorGutter.GlyphMarginId);
        var point = view.TranslatePoint(new Avalonia.Point(glyph.X + (glyph.Width / 2), view.GetY(1) + (view.LineHeight / 2)), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Render(window);
        controller.Menu.ShouldNotBeNull().Items.Count(i => !i.IsHeader).ShouldBe(1);
        editor.Text.ShouldBe("foo bar");
    }

    [AvaloniaFact]
    public void A_kind_with_a_single_action_is_applied_at_once_and_other_documents_go_to_the_host()
    {
        var other = new Uri("file:///other.txt");
        WorkspaceEdit? hostEdit = null;
        var (editor, window) = Show("import b;\nimport a;", e =>
        {
            e.LanguageFeatures.CodeActionProvider.Register("plaintext", new Provider((model, _, context) =>
            [
                new CodeAction("Organize Imports")
                {
                    Kind = CodeActionKind.SourceOrganizeImports.Value,
                    Edit = new WorkspaceEdit(
                    [
                        new WorkspaceTextEdit(model.Uri, new TextEdit(new TextRange(1, 1, 2, 10), "import a;\nimport b;")),
                        new WorkspaceTextEdit(other, new TextEdit(new TextRange(1, 1, 1, 1), "x")),
                    ]),
                },
            ], [CodeActionKind.SourceOrganizeImports.Value]));
            e.ActionHost.ApplyWorkspaceEdit = edit =>
            {
                hostEdit = edit;
                return Task.FromResult(true);
            };
        });

        window.KeyPressQwerty(PhysicalKey.O, RawInputModifiers.Shift | RawInputModifiers.Alt);
        Render(window);

        editor.Text.ShouldBe("import a;\nimport b;");
        hostEdit.ShouldNotBeNull().Edits.Single().ShouldBeOfType<WorkspaceTextEdit>().Resource.ShouldBe(other);
        CodeActionController.Get(editor)!.Menu.ShouldBeNull();
    }

    [AvaloniaFact]
    public void A_command_of_an_action_runs_through_the_editor_or_the_host()
    {
        var ran = new List<string>();
        var (editor, window) = Show("abc", e =>
        {
            e.LanguageFeatures.CodeActionProvider.Register("plaintext", new Provider((_, _, _) =>
                [new CodeAction("Run") { Kind = "quickfix", Command = new LanguageCommand("host.run", "Run") { Arguments = [42] } }]));
            e.ActionHost.ExecuteCommand = command =>
            {
                ran.Add(command.Id + ":" + command.Arguments![0]);
                return Task.CompletedTask;
            };
        });

        window.KeyPressQwerty(PhysicalKey.Period, CtrlCmd);
        Render(window);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Render(window);
        ran.ShouldBe(["host.run:42"]);
    }
}
