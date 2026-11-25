// Ported from vscode (src/vs/editor/contrib/inlineCompletions/test/browser/inlineCompletions.test.ts and utils.ts:
// MockInlineCompletionsProvider, GhostTextContext; commit b57d5c9e), MIT license.
//
// Ported: 'Does not trigger automatically if disabled', 'Ghost text is shown after trigger', 'Ghost text is shown
// automatically when configured', 'Ghost text is updated automatically', 'Unindent whitespace', 'Unindent tab', 'No
// unindent after indentation', 'Next/previous', 'Calling the provider is debounced', 'No race conditions', 'Additional
// Text Edits' and the 6 'Multi Cursor Support' tests (17). Not ported: the telemetry test, 'Backspace is debounced' and
// 'Do not reuse cache' (they need the editor's cursor commands, not part of the reduced model), the Forward Stability
// suite (forward stability is not ported) and the change hint tests (no change hints).
//
// Differences: the editor is the engine's TestCodeEditor behind an IInlineCompletionsEditor; fake timers are
// InlineFakeTimers; the provider's delay is a timer of those; view states are recorded when the model's state changes
// (upstream: an autorun on primaryGhostText). Below them, tests written for this port.

using System.Text.RegularExpressions;
using Shouldly;
using TKCode.Arc.Editor.Contrib.InlineSuggestions;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class InlineCompletionsModelTests
{
    internal sealed class MockInlineCompletionsProvider(InlineFakeTimers timers) : IInlineCompletionsProvider
    {
        private List<InlineCompletion> _returnValue = new();
        private int _delayMs;
        private double? _lastTimeMs;

        public List<string> CallHistory { get; } = new();

        public bool CalledTwiceIn50Ms { get; private set; }

        public List<InlineCompletion> Rejected { get; } = new();

        public List<(int Accepted, PartialAcceptInfo Info)> PartialAccepts { get; } = new();

        public void SetReturnValue(InlineCompletion? value, int delayMs = 0)
        {
            _returnValue = value is null ? [] : [value];
            _delayMs = delayMs;
        }

        public void SetReturnValues(IEnumerable<InlineCompletion> values, int delayMs = 0)
        {
            _returnValue = values.ToList();
            _delayMs = delayMs;
        }

        public List<string> GetAndClearCallHistory()
        {
            var history = CallHistory.ToList();
            CallHistory.Clear();
            return history;
        }

        public Task<InlineCompletions?> ProvideInlineCompletions(TextModel model, TextPosition position, InlineCompletionContext context, CancellationToken token)
        {
            if (_lastTimeMs is { } last && timers.Now - last < 50)
            {
                CalledTwiceIn50Ms = true;
            }

            _lastTimeMs = timers.Now;
            CallHistory.Add($"{position} {(int)context.TriggerKind} {model.GetValue()}");
            var result = _returnValue.Select(v => v.Range is null ? v with { Range = model.GetFullModelRange() } : v).ToList();
            var list = new InlineCompletions(result);
            if (_delayMs <= 0)
            {
                return Task.FromResult<InlineCompletions?>(list);
            }

            var tcs = new TaskCompletionSource<InlineCompletions?>();
            timers.Scheduler.RunAfter(TimeSpan.FromMilliseconds(_delayMs), () => tcs.TrySetResult(list));
            return tcs.Task;
        }

        public void HandleRejection(InlineCompletions completions, InlineCompletion item) => Rejected.Add(item);

        public void HandlePartialAccept(InlineCompletions completions, InlineCompletion item, int acceptedCharacters, PartialAcceptInfo info) =>
            PartialAccepts.Add((acceptedCharacters, info));
    }

    private sealed class Host : IInlineCompletionsEditor
    {
        private readonly TestCodeEditor _editor;

        public Host(TestCodeEditor editor) => _editor = editor;

        public TextModel Model => _editor.Model;

        public List<LanguageCommand> Commands { get; } = new();

        public IReadOnlyList<TextSelection> GetSelections() => _editor.GetSelections();

        public void PushUndoStop() => _editor.PushUndoStop();

        public bool ExecuteEdits(string source, IReadOnlyList<SingleEditOperation> edits, IReadOnlyList<TextSelection>? endCursorState) =>
            _editor.ExecuteEdits(source, edits, endCursorState);

        public Regex GetWordDefinition(TextPosition position) =>
            _editor.LanguageConfigurationService.GetLanguageConfiguration(Model.GetLanguageIdAtPosition(position.LineNumber, position.Column)).GetWordDefinition();

        public void ExecuteCommand(LanguageCommand command) => Commands.Add(command);
    }

    private sealed class Context : IDisposable
    {
        private string? _current;

        public Context(string text, bool enabled = true)
        {
            Timers = new InlineFakeTimers();
            Provider = new MockInlineCompletionsProvider(Timers);
            Features = new LanguageFeatures();
            Registration = Features.InlineCompletionsProvider.Register("*", Provider);
            Editor = TestCodeEditor.Create(text);
            Host = new Host(Editor);
            Model = new InlineCompletionsModel(Host, Features.InlineCompletionsProvider, Timers.Scheduler) { Enabled = enabled };
            Model.DidChangeState += Record;
            Editor.ViewModel.OutgoingEvent += e =>
            {
                if (e is CursorStateChangedEvent)
                {
                    Model.HandleCursorChanged();
                }
            };
            Record();
        }

        public InlineFakeTimers Timers { get; }

        public MockInlineCompletionsProvider Provider { get; }

        public LanguageFeatures Features { get; }

        public IDisposable Registration { get; }

        public TestCodeEditor Editor { get; }

        public Host Host { get; }

        public InlineCompletionsModel Model { get; }

        public List<string> ViewStates { get; } = new();

        private void Record()
        {
            var value = Editor.GetValue();
            var view = Model.PrimaryGhostText is { } ghostText ? ghostText.Render(value, true) : value;
            if (_current != view)
            {
                ViewStates.Add(view);
            }

            _current = view;
        }

        public List<string> GetAndClearViewStates()
        {
            var states = ViewStates.ToList();
            ViewStates.Clear();
            return states;
        }

        public void KeyboardType(string text)
        {
            foreach (var ch in text)
            {
                Editor.Type(ch.ToString());
            }
        }

        public void Dispose()
        {
            Model.Dispose();
            Registration.Dispose();
            Editor.Dispose();
        }
    }

    private static InlineCompletion Item(string insertText, TextRange? range = null) => new() { InsertText = insertText, Range = range };

    [Fact]
    public void Does_not_trigger_automatically_if_disabled()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("foo");
        context.Timers.Advance(1000);

        // Provider is not called, no ghost text is shown.
        context.Provider.GetAndClearCallHistory().ShouldBeEmpty();
        context.GetAndClearViewStates().ShouldBe([""]);
    }

    [Fact]
    public void Ghost_text_is_shown_after_trigger()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("foo");
        context.Provider.SetReturnValue(Item("foobar", new TextRange(1, 1, 1, 4)));
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);

        context.Provider.GetAndClearCallHistory().ShouldBe(["(1,4) 1 foo"]);
        context.GetAndClearViewStates().ShouldBe(["", "foo[bar]"]);
    }

    [Fact]
    public void Ghost_text_is_shown_automatically_when_configured()
    {
        using var context = new Context("");
        context.KeyboardType("foo");

        context.Provider.SetReturnValue(Item("foobar", new TextRange(1, 1, 1, 4)));
        context.Timers.Advance(1000);

        context.Provider.GetAndClearCallHistory().ShouldBe(["(1,4) 0 foo"]);
        context.GetAndClearViewStates().ShouldBe(["", "foo[bar]"]);
    }

    [Fact]
    public void Ghost_text_is_updated_automatically()
    {
        using var context = new Context("");
        context.Provider.SetReturnValue(Item("foobar", new TextRange(1, 1, 1, 4)));
        context.KeyboardType("foo");
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);

        context.Provider.SetReturnValue(Item("foobizz", new TextRange(1, 1, 1, 6)));
        context.KeyboardType("b");
        context.KeyboardType("i");
        context.Timers.Advance(1000);

        context.Provider.GetAndClearCallHistory().ShouldBe(["(1,4) 1 foo", "(1,6) 0 foobi"]);
        context.GetAndClearViewStates().ShouldBe(["", "foo[bar]", "foob[ar]", "foobi", "foobi[zz]"]);
    }

    [Fact]
    public void Unindent_whitespace()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("  ");
        context.Provider.SetReturnValue(Item("foo", new TextRange(1, 2, 1, 3)));
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);

        context.GetAndClearViewStates().ShouldBe(["", "  [foo]"]);

        context.Model.Accept();

        context.Provider.GetAndClearCallHistory().ShouldBe(["(1,3) 1   "]);
        context.GetAndClearViewStates().ShouldBe([" foo"]);
    }

    [Fact]
    public void Unindent_tab()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("\t\t");
        context.Provider.SetReturnValue(Item("foo", new TextRange(1, 2, 1, 3)));
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);

        context.GetAndClearViewStates().ShouldBe(["", "\t\t[foo]"]);

        context.Model.Accept();

        context.Provider.GetAndClearCallHistory().ShouldBe(["(1,3) 1 \t\t"]);
        context.GetAndClearViewStates().ShouldBe(["\tfoo"]);
    }

    [Fact]
    public void No_unindent_after_indentation()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("buzz  ");
        context.Provider.SetReturnValue(Item("foo", new TextRange(1, 6, 1, 7)));
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);

        context.GetAndClearViewStates().ShouldBe([""]);

        context.Model.Accept();

        context.Provider.GetAndClearCallHistory().ShouldBe(["(1,7) 1 buzz  "]);
        context.GetAndClearViewStates().ShouldBeEmpty();
    }

    [Fact]
    public async Task Next_previous()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("foo");
        context.Provider.SetReturnValue(Item("foobar1", new TextRange(1, 1, 1, 4)));
        _ = context.Model.Trigger();
        context.Timers.Advance(1000);

        context.GetAndClearViewStates().ShouldBe(["", "foo[bar1]"]);

        context.Provider.SetReturnValues(
        [
            Item("foobar1", new TextRange(1, 1, 1, 4)),
            Item("foobizz2", new TextRange(1, 1, 1, 4)),
            Item("foobuzz3", new TextRange(1, 1, 1, 4)),
        ]);

        await context.Model.Next();
        context.Timers.Advance(1000);
        context.GetAndClearViewStates().ShouldBe(["foo[bizz2]"]);

        await context.Model.Next();
        context.Timers.Advance(1000);
        context.GetAndClearViewStates().ShouldBe(["foo[buzz3]"]);

        await context.Model.Next();
        context.Timers.Advance(1000);
        context.GetAndClearViewStates().ShouldBe(["foo[bar1]"]);

        await context.Model.Previous();
        context.Timers.Advance(1000);
        context.GetAndClearViewStates().ShouldBe(["foo[buzz3]"]);

        await context.Model.Previous();
        context.Timers.Advance(1000);
        context.GetAndClearViewStates().ShouldBe(["foo[bizz2]"]);

        await context.Model.Previous();
        context.Timers.Advance(1000);
        context.GetAndClearViewStates().ShouldBe(["foo[bar1]"]);

        context.Provider.GetAndClearCallHistory().ShouldBe(["(1,4) 0 foo", "(1,4) 1 foo"]);
    }

    [Fact]
    public void Calling_the_provider_is_debounced()
    {
        using var context = new Context("");
        _ = context.Model.Trigger();

        context.KeyboardType("f");
        context.Timers.Advance(40);
        context.KeyboardType("o");
        context.Timers.Advance(40);
        context.KeyboardType("o");
        context.Timers.Advance(40);

        // The provider is not called
        context.Provider.GetAndClearCallHistory().ShouldBeEmpty();

        context.Timers.Advance(400);
        context.Provider.GetAndClearCallHistory().ShouldBe(["(1,4) 0 foo"]);

        context.Provider.CalledTwiceIn50Ms.ShouldBeFalse();
    }

    [Fact]
    public void No_race_conditions()
    {
        using var context = new Context("", enabled: true);
        context.KeyboardType("h");
        context.Provider.SetReturnValue(Item("helloworld", new TextRange(1, 1, 1, 2)), 1000);

        _ = context.Model.TriggerExplicitly();

        context.Timers.Advance(1030);
        context.KeyboardType("ello");
        context.Provider.SetReturnValue(Item("helloworld", new TextRange(1, 1, 1, 6)), 1000);

        // after 20ms: Inline completion provider answers back; after 50ms: Debounce is triggered
        context.Timers.Advance(2000);

        context.GetAndClearViewStates().ShouldBe(["", "hello[world]"]);
    }

    [Fact]
    public void Additional_text_edits()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("buzz\nbaz");
        context.Provider.SetReturnValue(new InlineCompletion
        {
            InsertText = "bazz",
            Range = new TextRange(2, 1, 2, 4),
            AdditionalTextEdits = [new SingleEditOperation(new TextRange(1, 1, 1, 5), "bla")],
        });
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);

        context.Model.Accept();

        context.Provider.GetAndClearCallHistory().ShouldBe(["(2,4) 1 buzz\nbaz"]);
        context.GetAndClearViewStates().ShouldBe(["", "buzz\nbaz[z]", "bla\nbazz"]);
    }

    // ---- Multi Cursor Support

    private static Context MultiCursor(string typed, TextSelection[] selections, string insertText)
    {
        var context = new Context("", enabled: false);
        context.KeyboardType(typed);
        context.Editor.SetSelections(selections);
        context.Provider.SetReturnValue(Item(insertText, new TextRange(1, 1, 1, 1000)));
        return context;
    }

    [Fact]
    public void Multi_cursor_basic()
    {
        using var context = MultiCursor("console\nconsole\n", [new(1, 1000, 1, 1000), new(2, 1000, 2, 1000)], "console.log(\"hello\");");
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);
        context.Model.Accept();
        context.Editor.GetValue().ShouldBe(string.Join('\n', "console.log(\"hello\");", "console.log(\"hello\");", ""));
    }

    [Fact]
    public void Multi_cursor_multi_part()
    {
        using var context = MultiCursor("console.log()\nconsole.log\n", [new(1, 12, 1, 12), new(2, 1000, 2, 1000)], "console.log(\"hello\");");
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);
        context.Model.Accept();
        context.Editor.GetValue().ShouldBe(string.Join('\n', "console.log(\"hello\");", "console.log", ""));
    }

    [Fact]
    public void Multi_cursor_multi_part_and_different_cursor_columns()
    {
        using var context = MultiCursor("console.log()\nconsole.warn\n", [new(1, 12, 1, 12), new(2, 14, 2, 14)], "console.log(\"hello\");");
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);
        context.Model.Accept();
        context.Editor.GetValue().ShouldBe(string.Join('\n', "console.log(\"hello\");", "console.warn", ""));
    }

    private static void AcceptNextWord(Context context, int timesToAccept = 1)
    {
        for (var i = 0; i < timesToAccept; i++)
        {
            _ = context.Model.TriggerExplicitly();
            context.Timers.Advance(1000);
            context.Model.AcceptNextWord();
        }
    }

    [Fact]
    public void Basic_partial_completion()
    {
        using var context = MultiCursor("let\nlet\n", [new(1, 1000, 1, 1000), new(2, 1000, 2, 1000)], "let a = 'some word'; ");
        AcceptNextWord(context, 2);
        context.Editor.GetValue().ShouldBe(string.Join('\n', "let a", "let a", ""));
    }

    [Fact]
    public void Partial_multi_part_completion()
    {
        using var context = MultiCursor("for ()\nfor \n", [new(1, 5, 1, 5), new(2, 1000, 2, 1000)], "for (let i = 0; i < 10; i++) {");
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);
        AcceptNextWord(context, 3);
        context.Editor.GetValue().ShouldBe(string.Join('\n', "for (let i)", "for ", ""));
    }

    [Fact]
    public void Partial_multi_part_and_different_cursor_columns_completion()
    {
        using var context = MultiCursor("console.log()\nconsole.warnnnn\n", [new(1, 12, 1, 12), new(2, 16, 2, 16)], "console.log(\"hello\" + \" \" + \"world\");");
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(1000);
        AcceptNextWord(context, 4);
        context.Editor.GetValue().ShouldBe(string.Join('\n', "console.log(\"hello\" + )", "console.warnnnn", ""));
    }

    // ---- written for the port

    [Fact]
    public void Escape_rejects_and_hides_and_accepting_runs_the_command()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("foo");
        var item = new InlineCompletion { InsertText = "foobar", Range = new TextRange(1, 1, 1, 4), Command = new LanguageCommand("after", "After") };
        context.Provider.SetReturnValue(item);
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(100);
        context.Model.PrimaryGhostText.ShouldNotBeNull();

        context.Model.Stop(explicitCancel: true);
        context.Model.PrimaryGhostText.ShouldBeNull();
        context.Provider.Rejected.ShouldBe([item]);

        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(100);
        context.Model.Accept();
        context.Editor.GetValue().ShouldBe("foobar");
        context.Editor.GetPosition().ShouldBe(new TextPosition(1, 7));
        context.Host.Commands.Select(c => c.Id).ShouldBe(["after"]);
        context.Model.PrimaryGhostText.ShouldBeNull();
    }

    [Fact]
    public void Accept_next_line_and_partial_accept_reports()
    {
        using var context = new Context("", enabled: false);
        context.KeyboardType("if");
        context.Provider.SetReturnValue(Item("if (a) {\n\tb();\n}", new TextRange(1, 1, 1, 3)));
        _ = context.Model.TriggerExplicitly();
        context.Timers.Advance(100);
        context.Model.PrimaryGhostText!.LineCount.ShouldBe(3);

        context.Model.AcceptNextLine();
        context.Editor.GetValue().ShouldBe("if (a) {\n");
        context.Provider.PartialAccepts.Single().Info.Kind.ShouldBe(PartialAcceptTriggerKind.Line);

        // the rest is still offered on the new line
        context.Model.PrimaryGhostText.ShouldNotBeNull();
        context.Model.PrimaryGhostText!.Render(context.Editor.GetValue(), true).ShouldBe("if (a) {\n[\tb();\n}]");
    }
}
