// Ported from vscode (src/vs/editor/contrib/codeAction/test/browser/codeActionModel.test.ts, commit b57d5c9e), MIT
// license. All 6 upstream tests (the last two are one theory with two cases). The test editor is FakeCodeActionEditor
// (model, selection, the lightbulb option and the events the model listens to); faked timers are CodeActionFakeTimers, advanced
// by the test; the includeNearbyQuickFixes configuration is the editor's IncludeNearbyQuickFixes. Below them, tests
// written for this port.

using Shouldly;
using TKCode.Arc.Editor.Contrib.CodeAction;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform.Markers;
using Xunit;
using CodeAction = TKCode.Arc.Editor.Languages.CodeAction;

namespace TKCode.Arc.Editor.Tests;

public sealed class CodeActionModelTests : IDisposable
{
    private const string LanguageId = "foo-lang";
    private static readonly Uri s_uri = new("untitled:path");
    private readonly MarkerService _markerService = new();
    private readonly TextModel _model;
    private readonly CodeActionFakeTimers _timers = new();
    private readonly FakeCodeActionEditor _editor;
    private readonly LanguageFeatureRegistry<ICodeActionProvider> _registry = new();
    private readonly List<IDisposable> _store = new();

    public CodeActionModelTests()
    {
        _model = TestTextModel.Create("foobar  foo bar\nfarboo far boo", LanguageId, uri: s_uri);
        _editor = new FakeCodeActionEditor(_model, _timers.Scheduler);
        _editor.SetPosition(1, 1);
    }

    public void Dispose()
    {
        for (var i = _store.Count - 1; i >= 0; i--)
        {
            _store[i].Dispose();
        }

        _model.Dispose();
        _markerService.Dispose();
    }

    private sealed class TestProvider : ICodeActionProvider
    {
        public Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token) =>
            Task.FromResult<CodeActionList?>(new CodeActionList([new CodeAction("test") { Command = new LanguageCommand("test-command", "test") { Arguments = [] } }]));
    }

    private sealed class DelegateProvider(Func<CodeActionContext, Task<CodeActionList?>> provide) : ICodeActionProvider
    {
        public Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token) => provide(context);
    }

    private static readonly MarkerData[] s_error =
    [
        new() { StartLineNumber = 1, StartColumn = 1, EndLineNumber = 1, EndColumn = 6, Message = "error", Severity = MarkerSeverity.Hint, Code = string.Empty, Source = string.Empty },
    ];

    private CodeActionModel CreateModel()
    {
        var model = new CodeActionModel(_editor, _registry, _markerService, null);
        _store.Add(model);
        return model;
    }

    [Fact]
    public async Task Oracle_marker_added()
    {
        _store.Add(_registry.Register(LanguageId, new TestProvider()));
        var model = CreateModel();
        CodeActionsState.Triggered? state = null;
        model.DidChangeState += e => state = e.ShouldBeOfType<CodeActionsState.Triggered>();

        // start here
        _markerService.ChangeOne("fake", s_uri, s_error);
        _timers.Advance(250);

        state.ShouldNotBeNull();
        state.Trigger.Type.ShouldBe(CodeActionTriggerType.Auto);
        var fixes = await state.Actions;
        model.Dispose();
        fixes.ValidActions.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Oracle_position_changed()
    {
        _store.Add(_registry.Register(LanguageId, new TestProvider()));
        _markerService.ChangeOne("fake", s_uri, s_error);
        _editor.SetPosition(2, 1);

        var model = CreateModel();
        CodeActionsState.Triggered? state = null;
        model.DidChangeState += e => state = e.ShouldBeOfType<CodeActionsState.Triggered>();

        // start here
        _editor.SetPosition(1, 1);
        _timers.Advance(250);

        state.ShouldNotBeNull();
        state.Trigger.Type.ShouldBe(CodeActionTriggerType.Auto);
        var fixes = await state.Actions;
        model.Dispose();
        fixes.ValidActions.Count.ShouldBe(1);
    }

    [Fact]
    public void Oracle_should_only_auto_trigger_once_for_cursor_and_marker_update_right_after_each_other()
    {
        _store.Add(_registry.Register(LanguageId, new TestProvider()));
        var triggerCount = 0;
        var model = CreateModel();
        model.DidChangeState += e =>
        {
            e.ShouldBeOfType<CodeActionsState.Triggered>().Trigger.Type.ShouldBe(CodeActionTriggerType.Auto);
            ++triggerCount;
        };

        _markerService.ChangeOne("fake", s_uri, s_error);
        _editor.SetSelection(new TextSelection(1, 1, 4, 1));

        // give time for a second trigger before completing the test
        _timers.Advance(1000);
        model.Dispose();
        triggerCount.ShouldBe(1);
    }

    [Fact]
    public async Task Disposes_manually_triggered_code_actions_when_the_editor_model_is_cleared()
    {
        var disposeCount = 0;
        _store.Add(_registry.Register(LanguageId, new DelegateProvider(context =>
            Task.FromResult(context.Trigger != CodeActionTriggerType.Invoke
                ? null
                : new CodeActionList([new CodeAction("test") { Command = new LanguageCommand("test-command", "test") }]) { Dispose = () => disposeCount++ }))));

        var codeActionModel = CreateModel();
        CodeActionsState.Triggered? manual = null;
        codeActionModel.DidChangeState += state =>
        {
            if (state is CodeActionsState.Triggered { Trigger.Type: CodeActionTriggerType.Invoke } triggered)
            {
                manual = triggered;
            }
        };

        codeActionModel.Trigger(new CodeActionTrigger(CodeActionTriggerType.Invoke, CodeActionTriggerSource.Default));
        manual.ShouldNotBeNull();
        await manual.Actions;
        _editor.SetModel(null);
        disposeCount.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Manual_actions_survive_a_canceled_automatic_request(bool claimBeforeAutomaticResultCompletes)
    {
        // upstream: "does not dispose claimed manual code actions when a canceled automatic request completes" (true)
        // and "does not replace manual code actions when a canceled automatic request completes before claim" (false)
        var automaticResult = new TaskCompletionSource<CodeActionList?>();
        var provideCount = 0;
        var manualDisposeCount = 0;
        _store.Add(_registry.Register(LanguageId, new DelegateProvider(context =>
        {
            if (provideCount++ == 0)
            {
                return automaticResult.Task;
            }

            context.Trigger.ShouldBe(CodeActionTriggerType.Invoke);
            return Task.FromResult<CodeActionList?>(new CodeActionList(
            [
                new CodeAction("test") { Kind = "quickfix", Command = new LanguageCommand("test-command", "test") { Arguments = [true] } },
            ])
            {
                Dispose = () => manualDisposeCount++,
            });
        })));

        _editor.IncludeNearbyQuickFixes = true;
        var codeActionModel = CreateModel();
        CodeActionsState.Triggered? manual = null;
        codeActionModel.DidChangeState += state =>
        {
            if (state is CodeActionsState.Triggered { Trigger.Type: CodeActionTriggerType.Invoke } triggered)
            {
                manual = triggered;
            }
        };

        codeActionModel.Trigger(new CodeActionTrigger(CodeActionTriggerType.Invoke, CodeActionTriggerSource.QuickFix));
        var manualActionSet = await manual.ShouldNotBeNull().Actions;
        var manualActions = claimBeforeAutomaticResultCompletes ? codeActionModel.TakeCodeActions(manualActionSet) : null;

        automaticResult.SetResult(new CodeActionList([]));
        await Task.Yield();
        manualActions ??= codeActionModel.TakeCodeActions(manualActionSet);
        var wasClaimed = manualActions is not null;
        var disposeCountBeforeRelease = manualDisposeCount;
        manualActions?.Dispose();

        (wasClaimed, disposeCountBeforeRelease, manualDisposeCount).ShouldBe((true, 0, 1));
    }

    // ---- written for this port

    [Fact]
    public void No_provider_or_read_only_means_no_request_and_the_supported_kinds_follow_the_providers()
    {
        var keys = new TestContextKey();
        var model = new CodeActionModel(_editor, _registry, _markerService, keys);
        _store.Add(model);
        model.State.ShouldBeSameAs(CodeActionsState.Empty);

        var registration = _registry.Register(LanguageId, new KindsProvider(["quickfix", "refactor.extract"]));
        model.State.ShouldBeOfType<CodeActionsState.Triggered>();
        keys.Value.ShouldBe("quickfix refactor.extract");

        _editor.IsReadOnly = true;
        _editor.RaiseConfigurationChanged();
        model.State.ShouldBeSameAs(CodeActionsState.Empty);
        keys.WasReset.ShouldBeTrue();
        registration.Dispose();
    }

    [Theory]
    [InlineData(1, 1, true)] // 'f' right of the cursor
    [InlineData(1, 8, false)] // between two spaces
    [InlineData(1, 7, true)] // 'r' left of the cursor
    [InlineData(1, 16, true)] // end of line after 'r'
    public void Auto_trigger_on_code_ignores_a_cursor_enclosed_by_whitespace(int line, int column, bool triggers)
    {
        _store.Add(_registry.Register(LanguageId, new TestProvider()));
        _editor.SetPosition(line, column);
        var model = CreateModel();
        if (triggers)
        {
            model.State.ShouldBeOfType<CodeActionsState.Triggered>();
        }
        else
        {
            model.State.ShouldBeSameAs(CodeActionsState.Empty);
        }
    }

    [Fact]
    public void A_manual_request_is_replaced_by_an_automatic_one_at_another_position_only_after_500_ms()
    {
        _store.Add(_registry.Register(LanguageId, new TestProvider()));
        var model = CreateModel();
        model.Trigger(new CodeActionTrigger(CodeActionTriggerType.Invoke, CodeActionTriggerSource.QuickFix));
        var manual = model.State.ShouldBeOfType<CodeActionsState.Triggered>();
        manual.Trigger.Type.ShouldBe(CodeActionTriggerType.Invoke);

        _editor.SetPosition(2, 2);
        _timers.Advance(250);
        model.State.ShouldBeSameAs(manual);
        _timers.Advance(500);
        model.State.ShouldBeOfType<CodeActionsState.Triggered>().Trigger.Type.ShouldBe(CodeActionTriggerType.Auto);
    }

    private sealed class KindsProvider(IReadOnlyList<string> kinds) : ICodeActionProvider
    {
        public IReadOnlyList<string>? ProvidedCodeActionKinds => kinds;

        public Task<CodeActionList?> ProvideCodeActions(TextModel model, TextRange range, CodeActionContext context, CancellationToken token) =>
            Task.FromResult<CodeActionList?>(null);
    }

    private sealed class TestContextKey : Platform.IContextKey<string>
    {
        public string? Value { get; private set; }

        public bool WasReset { get; private set; }

        public void Set(string value) => Value = value;

        public void Reset()
        {
            Value = null;
            WasReset = true;
        }

        public string? Get() => Value;
    }
}
