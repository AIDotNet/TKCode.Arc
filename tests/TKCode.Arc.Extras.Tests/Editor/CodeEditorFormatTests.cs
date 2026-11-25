// Tests of the format contributions and actions of the code editor (the port of vscode's
// src/vs/editor/contrib/format/browser/formatActions.ts and formattingEdit.ts, commit b57d5c9e; upstream has no unit
// tests for them): format document and selection through their actions and keybindings, format on type, format on
// paste, minimal edits keeping the cursor, and cancellation by a cursor move.

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Tests.Editor;

public class CodeEditorFormatTests
{
    private const string Lang = "fmtlang";

    private static (CodeEditor Editor, Avalonia.Controls.Window Window) Show(string text)
    {
        var editor = new CodeEditor { Text = text, Width = 500, Height = 300, LanguageId = Lang };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    private static void Pump(Task task)
    {
        TestHelpers.PumpUntil(() => task.IsCompleted, 5000).ShouldBeTrue();
        Dispatcher.UIThread.RunJobs();
    }

    // indents every line after a line ending with '{' by four spaces, until '}' (a tiny formatter)
    private static string Indent(string text)
    {
        var lines = text.Split('\n');
        var depth = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('}'))
            {
                depth = Math.Max(0, depth - 1);
            }

            lines[i] = trimmed.Length == 0 ? string.Empty : new string(' ', depth * 4) + trimmed;
            if (trimmed.EndsWith('{'))
            {
                depth++;
            }
        }

        return string.Join('\n', lines);
    }

    private sealed class DocumentFormatter : IDocumentFormattingEditProvider
    {
        public int Calls { get; private set; }

        public TaskCompletionSource? Gate { get; set; }

        public async Task<IReadOnlyList<TextEdit>?> ProvideDocumentFormattingEdits(TextModel model, FormattingOptions options, CancellationToken token)
        {
            Calls++;
            if (Gate is { } gate)
            {
                await gate.Task;
            }

            return [new TextEdit(model.GetFullModelRange(), Indent(model.GetValue()))];
        }
    }

    private sealed class RangeFormatter : IDocumentRangeFormattingEditProvider
    {
        public List<TextRange> Ranges { get; } = [];

        public Task<IReadOnlyList<TextEdit>?> ProvideDocumentRangeFormattingEdits(TextModel model, TextRange range, FormattingOptions options, CancellationToken token)
        {
            Ranges.Add(range);
            var edits = new List<TextEdit>();
            for (var line = range.StartLineNumber; line <= range.EndLineNumber; line++)
            {
                var content = model.GetLineContent(line);
                var trimmed = content.TrimStart();
                if (trimmed.Length != content.Length)
                {
                    edits.Add(new TextEdit(new TextRange(line, 1, line, content.Length - trimmed.Length + 1), string.Empty));
                }
            }

            return Task.FromResult<IReadOnlyList<TextEdit>?>(edits);
        }
    }

    private sealed class SemicolonFormatter : IOnTypeFormattingEditProvider
    {
        public IReadOnlyList<string> AutoFormatTriggerCharacters { get; } = [";"];

        public Task<IReadOnlyList<TextEdit>?> ProvideOnTypeFormattingEdits(TextModel model, TextPosition position, string ch, FormattingOptions options, CancellationToken token)
        {
            // collapse "a  =  b;" into "a = b;" on the typed line
            var content = model.GetLineContent(position.LineNumber);
            var collapsed = System.Text.RegularExpressions.Regex.Replace(content, " {2,}", " ");
            return Task.FromResult<IReadOnlyList<TextEdit>?>([new TextEdit(new TextRange(position.LineNumber, 1, position.LineNumber, content.Length + 1), collapsed)]);
        }
    }

    [AvaloniaFact]
    public void Format_document_runs_the_formatter_with_minimal_edits_as_one_undo_step()
    {
        var (editor, window) = Show("if (a) {\nb();\nc();\n}");
        var formatter = new DocumentFormatter();
        using var registration = editor.LanguageFeatures.DocumentFormattingEditProvider.Register(Lang, formatter);
        editor.SetPosition(new TextPosition(3, 3));
        var action = editor.GetAction("editor.action.formatDocument").ShouldNotBeNull();
        action.IsSupported().ShouldBeTrue();
        Pump(action.Run());

        editor.Text.ShouldBe("if (a) {\n    b();\n    c();\n}");
        formatter.Calls.ShouldBe(1);
        // the minimal edits insert before the cursor's text: the cursor moves with it
        editor.GetPosition().ShouldBe(new TextPosition(3, 7));

        editor.Trigger("keyboard", "undo", null);
        Dispatcher.UIThread.RunJobs();
        editor.Text.ShouldBe("if (a) {\nb();\nc();\n}");
        window.Close();
    }

    [AvaloniaFact]
    public void Shift_alt_f_formats_and_the_action_needs_a_formatter()
    {
        var (editor, window) = Show("x {\ny\n}");
        editor.GetAction("editor.action.formatDocument")!.IsSupported().ShouldBeFalse();
        using var registration = editor.LanguageFeatures.DocumentFormattingEditProvider.Register(Lang, new DocumentFormatter());
        editor.GetAction("editor.action.formatDocument")!.IsSupported().ShouldBeTrue();

        var modifiers = RawInputModifiers.Shift | RawInputModifiers.Alt;
        if (OperatingSystem.IsLinux())
        {
            modifiers = RawInputModifiers.Control | RawInputModifiers.Shift;
        }

        window.KeyPressQwerty(OperatingSystem.IsLinux() ? PhysicalKey.I : PhysicalKey.F, modifiers);
        TestHelpers.PumpUntil(() => editor.Text == "x {\n    y\n}", 5000).ShouldBeTrue();

        // read-only: not supported
        editor.IsReadOnly = true;
        editor.GetAction("editor.action.formatDocument")!.IsSupported().ShouldBeFalse();
        window.Close();
    }

    [AvaloniaFact]
    public void A_range_formatter_formats_the_document_and_the_selection_lines()
    {
        var (editor, window) = Show("  a\n  b\n  c\n  d");
        var formatter = new RangeFormatter();
        using var registration = editor.LanguageFeatures.DocumentRangeFormattingEditProvider.Register(Lang, formatter);

        // an empty selection formats its line (upstream's FormatSelectionAction)
        editor.SetPosition(new TextPosition(2, 2));
        Pump(editor.GetAction("editor.action.formatSelection")!.Run());
        editor.Text.ShouldBe("  a\nb\n  c\n  d");
        formatter.Ranges.ShouldBe([new TextRange(2, 1, 2, 4)]);

        // two selections, formatted separately
        editor.SetSelections([new TextSelection(3, 1, 3, 2), new TextSelection(1, 1, 1, 4)]);
        Pump(editor.GetAction("editor.action.formatSelection")!.Run());
        editor.Text.ShouldBe("a\nb\nc\n  d");

        // the range formatter also formats the whole document (a synthetic document formatter)
        Pump(editor.GetAction("editor.action.formatDocument")!.Run());
        editor.Text.ShouldBe("a\nb\nc\nd");
        formatter.Ranges[^1].ShouldBe(new TextRange(1, 1, 4, 4));

        // editor.action.format: the selection when there is one
        editor.Text = "  x\n  y";
        editor.SetSelection(new TextSelection(2, 1, 2, 2));
        Pump(editor.ExecuteCommand("editor.action.format", null) ?? Task.CompletedTask);
        TestHelpers.PumpUntil(() => editor.Text == "  x\ny", 5000).ShouldBeTrue();
        window.Close();
    }

    [AvaloniaFact]
    public void A_cursor_move_while_the_formatter_runs_cancels_the_format()
    {
        var (editor, window) = Show("a {\nb\n}");
        var formatter = new DocumentFormatter { Gate = new TaskCompletionSource() };
        using var registration = editor.LanguageFeatures.DocumentFormattingEditProvider.Register(Lang, formatter);
        var run = editor.GetAction("editor.action.formatDocument")!.Run();
        editor.SetPosition(new TextPosition(3, 2));
        formatter.Gate.SetResult();
        Pump(run);
        editor.Text.ShouldBe("a {\nb\n}");
        window.Close();
    }

    [AvaloniaFact]
    public void Format_on_type_formats_after_a_trigger_character_only_when_enabled()
    {
        var (editor, window) = Show(string.Empty);
        using var registration = editor.LanguageFeatures.OnTypeFormattingEditProvider.Register(Lang, new SemicolonFormatter());
        editor.OnBeforeInteractionEvent();
        Dispatcher.UIThread.RunJobs();

        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("a  =  b;"));
        Dispatcher.UIThread.RunJobs();
        editor.Text.ShouldBe("a  =  b;");

        editor.FormatOnType = true;
        editor.Text = string.Empty;
        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("a  =  b"));
        Dispatcher.UIThread.RunJobs();
        editor.Text.ShouldBe("a  =  b");
        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload(";"));
        var contribution = FormatOnTypeContribution.Get(editor).ShouldNotBeNull();
        Pump(contribution.PendingFormat ?? Task.CompletedTask);
        editor.Text.ShouldBe("a = b;");
        window.Close();
    }

    [AvaloniaFact]
    public void Format_on_paste_formats_the_pasted_range()
    {
        var (editor, window) = Show("x\n");
        var formatter = new RangeFormatter();
        using var registration = editor.LanguageFeatures.DocumentRangeFormattingEditProvider.Register(Lang, formatter);
        editor.FormatOnPaste = true;
        editor.OnBeforeInteractionEvent();
        Dispatcher.UIThread.RunJobs();
        editor.SetPosition(new TextPosition(2, 1));
        editor.Trigger("keyboard", EditorHandler.Paste, new PastePayload("   p\n   q"));
        var contribution = FormatOnPasteContribution.Get(editor).ShouldNotBeNull();
        Pump(contribution.PendingFormat ?? Task.CompletedTask);
        editor.Text.ShouldBe("x\np\nq");
        formatter.Ranges.ShouldBe([new TextRange(2, 1, 3, 5)]);

        // one undo step for the paste formatting, then the paste
        editor.Trigger("keyboard", "undo", null);
        Dispatcher.UIThread.RunJobs();
        editor.Text.ShouldBe("x\n   p\n   q");
        window.Close();
    }
}
