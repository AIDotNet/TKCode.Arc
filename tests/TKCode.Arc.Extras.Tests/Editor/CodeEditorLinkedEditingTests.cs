// Tests of linked editing, ported from vscode (src/vs/editor/contrib/linkedEditing/test/browser/linkedEditing.test.ts,
// commit b57d5c9e), MIT license: 39 of the 42 cases (the three that use deleteWordLeft and deleteAllLeft wait for the
// word and line operations; upstream's commented-out cases are left out as upstream does). The editor is a CodeEditor
// with only the linked editing contribution and its commands; the provider returns the occurrences of the word at
// the position, as upstream's; the language id is selected by language (upstream: by the 'test' scheme). Upstream
// waits for the contribution's update and sync promises; here the dispatcher is pumped until they complete.

using System.Text.RegularExpressions;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Tests.Editor;

public class CodeEditorLinkedEditingTests
{
    private const string LanguageId = "linkedEditingTestLangage";

    private sealed class TestEditor(CodeEditor editor, LinkedEditingContribution contribution)
    {
        public CodeEditor Editor => editor;

        public LinkedEditingContribution Contribution => contribution;

        public void SetPosition(int line, int column)
        {
            editor.SetPosition(new TextPosition(line, column));
            Pump(contribution.CurrentUpdateTriggerPromise);
        }

        public void SetSelection(int a, int b, int c, int d)
        {
            editor.SetSelection(new TextSelection(a, b, c, d));
            Pump(contribution.CurrentUpdateTriggerPromise);
        }

        public void ExecuteEdits(TextRange range, string text)
        {
            editor.ExecuteEdits("test", [EditOperation.Replace(range, text)]);
            Pump(contribution.CurrentUpdateTriggerPromise);
        }

        public void Type(string text)
        {
            editor.Trigger("keyboard", EditorHandler.Type, new TypePayload(text));
            Pump(contribution.CurrentSyncTriggerPromise);
        }

        public void Paste(string text)
        {
            editor.Trigger("keyboard", EditorHandler.Paste, new PastePayload(text));
            Pump(contribution.CurrentSyncTriggerPromise);
        }

        public void DeleteLeft()
        {
            editor.Trigger("keyboard", "deleteLeft", null);
            Pump(contribution.CurrentSyncTriggerPromise);
        }

        public void Undo()
        {
            editor.Trigger("keyboard", "undo", null);
            Dispatcher.UIThread.RunJobs();
        }

        public void Redo()
        {
            editor.Trigger("keyboard", "redo", null);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Pump(Task task)
    {
        TestHelpers.PumpUntil(() => task.IsCompleted, 5000);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class WordOccurrences(Regex? responseWordPattern) : ILinkedEditingRangeProvider
    {
        private static readonly Regex Word = new("[a-zA-Z]+");

        public Task<LinkedEditingRanges?> ProvideLinkedEditingRanges(TextModel model, TextPosition position, CancellationToken token)
        {
            // upstream: model.getWordAtPosition(pos), then findMatches(word, wholeWord)
            var line = model.GetLineContent(position.LineNumber);
            foreach (Match match in Word.Matches(line))
            {
                if (match.Index + 1 <= position.Column && position.Column <= match.Index + 1 + match.Length)
                {
                    var matches = model.FindMatches(match.Value, false, false, true, WordHelper.UsualWordSeparators, false);
                    return Task.FromResult<LinkedEditingRanges?>(new LinkedEditingRanges([.. matches.Select(m => m.Range)]) { WordPattern = responseWordPattern });
                }
            }

            return Task.FromResult<LinkedEditingRanges?>(new LinkedEditingRanges([]) { WordPattern = responseWordPattern });
        }
    }

    private static void TestCase(string[] text, Action<TestEditor> operations, string[] expectedEndText, Regex? responseWordPattern = null)
    {
        var editor = new CodeEditor([LinkedEditingContribution.Description], [LinkedEditingContribution.CancelLinkedEditingInputCommand])
        {
            Width = 400,
            Height = 200,
            LanguageId = LanguageId,
            LinkedEditing = true,
        };
        using var configuration = editor.LanguageConfigurationService.Register(LanguageId, new LanguageConfiguration { WordPattern = new Regex("[a-zA-Z]+") });
        using var registration = editor.LanguageFeatures.LinkedEditingRangeProvider.Register(LanguageId, new WordOccurrences(responseWordPattern));
        editor.Text = string.Join('\n', text);
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        var contribution = editor.GetContribution<LinkedEditingContribution>(LinkedEditingContribution.Id).ShouldNotBeNull();
        contribution.SetDebounceDuration(0);

        operations(new TestEditor(editor, contribution));

        Dispatcher.UIThread.RunJobs();
        editor.Model.GetValue().ShouldBe(string.Join('\n', expectedEndText));
        window.Close();
    }

    private static readonly string[] State = ["<ooo></ooo>"];

    private static readonly Regex State3Pattern = new("[a-yA-Y]+");

    private static readonly Regex State4Pattern = new("[a-eA-E]+");

    // ---- Simple insertion

    [AvaloniaFact]
    public void Simple_insert_initial() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("i"); }, ["<iooo></iooo>"]);

    [AvaloniaFact]
    public void Simple_insert_middle() => TestCase(State, e => { e.SetPosition(1, 3); e.Type("i"); }, ["<oioo></oioo>"]);

    [AvaloniaFact]
    public void Simple_insert_end() => TestCase(State, e => { e.SetPosition(1, 5); e.Type("i"); }, ["<oooi></oooi>"]);

    // ---- Simple insertion - end

    [AvaloniaFact]
    public void Simple_insert_end_initial() => TestCase(State, e => { e.SetPosition(1, 8); e.Type("i"); }, ["<iooo></iooo>"]);

    [AvaloniaFact]
    public void Simple_insert_end_middle() => TestCase(State, e => { e.SetPosition(1, 9); e.Type("i"); }, ["<oioo></oioo>"]);

    [AvaloniaFact]
    public void Simple_insert_end_end() => TestCase(State, e => { e.SetPosition(1, 11); e.Type("i"); }, ["<oooi></oooi>"]);

    // ---- Boundary insertion

    [AvaloniaFact]
    public void Simple_insert_out_of_boundary() => TestCase(State, e => { e.SetPosition(1, 1); e.Type("i"); }, ["i<ooo></ooo>"]);

    [AvaloniaFact]
    public void Simple_insert_out_of_boundary_2() => TestCase(State, e => { e.SetPosition(1, 6); e.Type("i"); }, ["<ooo>i</ooo>"]);

    [AvaloniaFact]
    public void Simple_insert_out_of_boundary_3() => TestCase(State, e => { e.SetPosition(1, 7); e.Type("i"); }, ["<ooo><i/ooo>"]);

    [AvaloniaFact]
    public void Simple_insert_out_of_boundary_4() => TestCase(State, e => { e.SetPosition(1, 12); e.Type("i"); }, ["<ooo></ooo>i"]);

    // ---- Insert + Move

    [AvaloniaFact]
    public void Continuous_insert() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("i"); e.Type("i"); }, ["<iiooo></iiooo>"]);

    [AvaloniaFact]
    public void Quick_inserts_with_a_range_update_before_synchronization() => TestCase(State, e =>
    {
        e.SetPosition(1, 2);
        e.Editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("i"));
        var firstSync = e.Contribution.CurrentSyncTriggerPromise;
        e.Editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("j"));
        var secondSync = e.Contribution.CurrentSyncTriggerPromise;
        Pump(e.Contribution.UpdateRanges());
        Pump(Task.WhenAll(firstSync, secondSync));
        e.Type("k");
    }, ["<ijkooo></ijkooo>"]);

    [AvaloniaFact]
    public void Quick_inserts_in_closing_tag_with_a_range_update_before_synchronization() => TestCase(State, e =>
    {
        e.SetPosition(1, 8);
        e.Editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("i"));
        var firstSync = e.Contribution.CurrentSyncTriggerPromise;
        e.Editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("j"));
        var secondSync = e.Contribution.CurrentSyncTriggerPromise;
        Pump(e.Contribution.UpdateRanges());
        Pump(Task.WhenAll(firstSync, secondSync));
        e.Type("k");
    }, ["<ijkooo></ijkooo>"]);

    [AvaloniaFact]
    public void Insert_move_insert() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("i"); e.SetPosition(1, 4); e.Type("i"); }, ["<ioioo></ioioo>"]);

    [AvaloniaFact]
    public void Insert_move_insert_outside_region() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("i"); e.SetPosition(1, 7); e.Type("i"); }, ["<iooo>i</iooo>"]);

    [AvaloniaFact]
    public void Edits_outside_the_reference_range_refresh_linked_editing() => TestCase(State, e =>
    {
        e.SetPosition(1, 2);
        e.ExecuteEdits(new TextRange(1, 8, 1, 11), "other");
        e.Type("i");
    }, ["<iooo></other>"]);

    // ---- Selection insert

    [AvaloniaFact]
    public void Selection_insert_simple() => TestCase(State, e => { e.SetPosition(1, 2); e.SetSelection(1, 2, 1, 3); e.Type("i"); }, ["<ioo></ioo>"]);

    [AvaloniaFact]
    public void Selection_insert_whole() => TestCase(State, e => { e.SetPosition(1, 2); e.SetSelection(1, 2, 1, 5); e.Type("i"); }, ["<i></i>"]);

    [AvaloniaFact]
    public void Selection_insert_across_boundary() => TestCase(State, e => { e.SetPosition(1, 2); e.SetSelection(1, 1, 1, 3); e.Type("i"); }, ["ioo></oo>"]);

    // ---- Break out behavior

    [AvaloniaFact]
    public void Breakout_type_space() => TestCase(State, e => { e.SetPosition(1, 5); e.Type(" "); }, ["<ooo ></ooo>"]);

    [AvaloniaFact]
    public void Breakout_type_space_then_undo() => TestCase(State, e => { e.SetPosition(1, 5); e.Type(" "); e.Undo(); }, ["<ooo></ooo>"]);

    [AvaloniaFact]
    public void Breakout_type_space_in_middle() => TestCase(State, e => { e.SetPosition(1, 4); e.Type(" "); }, ["<oo o></ooo>"]);

    [AvaloniaFact]
    public void Breakout_paste_content_starting_with_space() => TestCase(State, e => { e.SetPosition(1, 5); e.Paste(" i=\"i\""); }, ["<ooo i=\"i\"></ooo>"]);

    [AvaloniaFact]
    public void Breakout_paste_content_starting_with_space_then_undo() => TestCase(State, e => { e.SetPosition(1, 5); e.Paste(" i=\"i\""); e.Undo(); }, ["<ooo></ooo>"]);

    [AvaloniaFact]
    public void Breakout_paste_content_starting_with_space_in_middle() => TestCase(State, e => { e.SetPosition(1, 4); e.Paste(" i"); }, ["<oo io></ooo>"]);

    // ---- Break out with custom provider wordPattern

    [AvaloniaFact]
    public void Breakout_with_stop_pattern_insert() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("i"); }, ["<iooo></iooo>"], State3Pattern);

    [AvaloniaFact]
    public void Breakout_with_stop_pattern_insert_stop_char() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("z"); }, ["<zooo></ooo>"], State3Pattern);

    [AvaloniaFact]
    public void Breakout_with_stop_pattern_paste_char() => TestCase(State, e => { e.SetPosition(1, 2); e.Paste("z"); }, ["<zooo></ooo>"], State3Pattern);

    [AvaloniaFact]
    public void Breakout_with_stop_pattern_paste_string() => TestCase(State, e => { e.SetPosition(1, 2); e.Paste("zo"); }, ["<zoooo></ooo>"], State3Pattern);

    [AvaloniaFact]
    public void Breakout_with_stop_pattern_insert_at_end() => TestCase(State, e => { e.SetPosition(1, 5); e.Type("z"); }, ["<oooz></ooo>"], State3Pattern);

    [AvaloniaFact]
    public void Breakout_with_stop_pattern_insert_stop_char_respos() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("i"); }, ["<iooo></ooo>"], State4Pattern);

    // ---- Delete

    [AvaloniaFact]
    public void Delete_left_char() => TestCase(State, e => { e.SetPosition(1, 5); e.DeleteLeft(); }, ["<oo></oo>"]);

    [AvaloniaFact]
    public void Quick_deletes_with_a_range_update_before_synchronization() => TestCase(State, e =>
    {
        e.SetPosition(1, 5);
        e.Editor.Trigger("keyboard", "deleteLeft", null);
        var firstSync = e.Contribution.CurrentSyncTriggerPromise;
        e.Editor.Trigger("keyboard", "deleteLeft", null);
        var secondSync = e.Contribution.CurrentSyncTriggerPromise;
        Pump(e.Contribution.UpdateRanges());
        Pump(Task.WhenAll(firstSync, secondSync));
        e.DeleteLeft();
    }, ["<></>"]);

    [AvaloniaFact]
    public void Delete_left_char_then_undo() => TestCase(State, e => { e.SetPosition(1, 5); e.DeleteLeft(); e.Undo(); }, ["<ooo></ooo>"]);

    [AvaloniaFact]
    public void Delete_selection() => TestCase(State, e => { e.SetPosition(1, 5); e.SetSelection(1, 2, 1, 3); e.DeleteLeft(); }, ["<oo></oo>"]);

    [AvaloniaFact]
    public void Delete_selection_across_boundary() => TestCase(State, e => { e.SetPosition(1, 3); e.SetSelection(1, 1, 1, 3); e.DeleteLeft(); }, ["oo></oo>"]);

    // ---- Undo / redo

    [AvaloniaFact]
    public void Undo_redo_simple_undo() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("i"); e.Undo(); e.Undo(); }, ["<ooo></ooo>"]);

    [AvaloniaFact]
    public void Undo_redo_simple_undo_redo() => TestCase(State, e => { e.SetPosition(1, 2); e.Type("i"); e.Undo(); e.Redo(); }, ["<iooo></iooo>"]);

    // ---- Multi line

    [AvaloniaFact]
    public void Multiline_insert() => TestCase(["<ooo>", "</ooo>"], e => { e.SetPosition(1, 2); e.Type("i"); }, ["<iooo>", "</iooo>"]);

    // ---- own tests

    [AvaloniaFact]
    public void Linked_ranges_are_decorated_and_escape_clears_them()
    {
        TestCase(State, e =>
        {
            e.SetPosition(1, 3);
            e.Contribution.CurrentRanges.ShouldBe([new TextRange(1, 2, 1, 5), new TextRange(1, 8, 1, 11)]);
            var rects = e.Editor.View!.GetDecorationRects(1, 1).Where(r => r.ClassName == LinkedEditingContribution.DecorationClassName).ToList();
            rects.Count.ShouldBe(2);
            rects[0].Fill.ShouldNotBeNull();
            e.Editor.ContextKeyService.GetContextKeyValue("LinkedEditingInputVisible").ShouldBe(true);

            e.Editor.Trigger("keyboard", "cancelLinkedEditingInput", null);
            e.Contribution.CurrentRanges.ShouldBeEmpty();
            e.Editor.ContextKeyService.GetContextKeyValue("LinkedEditingInputVisible").ShouldBe(false);
        }, ["<ooo></ooo>"]);
    }

    [AvaloniaFact]
    public void Turning_the_option_off_stops_linked_editing()
    {
        TestCase(State, e =>
        {
            e.Editor.LinkedEditing = false;
            e.SetPosition(1, 2);
            e.Type("i");
        }, ["<iooo></ooo>"]);
    }
}
