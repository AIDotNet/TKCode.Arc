// Ported from vscode (src/vs/editor/contrib/parameterHints/test/browser/parameterHintsModel.test.ts, commit b57d5c9e),
// MIT license. All 10 upstream tests. The editor is a small IParameterHintsEditor over a text model whose Type does
// what the editor's type handler does for the model (insert at the cursor, then the content, cursor and type events);
// runWithFakedTimers is FakeTimers. A provider's asserts record into a list that the test checks afterwards (upstream
// asserts inside the provider, where a failure only logs). Two own tests follow: cycling the signatures and cursor
// moves.

using Shouldly;
using TKCode.Arc.Editor.Contrib.ParameterHints;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ParameterHintsModelTests : IDisposable
{
    private static readonly Uri s_mockFile = new("test:somefile.ttt");
    private static readonly LanguageSelector s_mockFileSelector = new LanguageFilter { Scheme = "test" };

    private static readonly SignatureHelp s_emptySigHelp = new([new SignatureInformation("none", [])], 0, 0);

    private static readonly SignatureHelpResult s_emptySigHelpResult = new(s_emptySigHelp);

    private readonly List<IDisposable> _disposables = [];
    private readonly LanguageFeatureRegistry<ISignatureHelpProvider> _registry = new();

    public void Dispose()
    {
        for (var i = _disposables.Count - 1; i >= 0; i--)
        {
            _disposables[i].Dispose();
        }
    }

    private T Add<T>(T disposable)
        where T : IDisposable
    {
        _disposables.Add(disposable);
        return disposable;
    }

    private TestEditor CreateMockEditor(string fileContents)
    {
        var model = Add(TestTextModel.Create(fileContents, uri: s_mockFile));
        return new TestEditor(model, _registry);
    }

    private static Task<SignatureHelp?> GetNextHint(ParameterHintsModel model)
    {
        var completion = new TaskCompletionSource<SignatureHelp?>();
        Action<SignatureHelp?>? handler = null;
        handler = e =>
        {
            model.OnChangedHints -= handler;
            completion.TrySetResult(e);
        };
        model.OnChangedHints += handler;
        return completion.Task;
    }

    [Fact]
    public void Provider_Should_Get_Trigger_Character_On_Type() => FakeTimers.Run(async _ =>
    {
        var done = new TaskCompletionSource();
        const string triggerChar = "(";

        var editor = CreateMockEditor("");
        Add(new ParameterHintsModel(editor, FakeTimersScheduler()));

        SignatureHelpContext? seen = null;
        Add(_registry.Register(s_mockFileSelector, new Provider([triggerChar], [], (_, context) =>
        {
            seen = context;
            done.TrySetResult();
            return null;
        })));

        editor.Type(triggerChar);
        await done.Task;

        seen!.TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        seen.TriggerCharacter.ShouldBe(triggerChar);
    });

    [Fact]
    public void Provider_Should_Be_Retriggered_If_Already_Active() => FakeTimers.Run(async timers =>
    {
        var done = new TaskCompletionSource();
        const string triggerChar = "(";

        var editor = CreateMockEditor("");
        Add(new ParameterHintsModel(editor, timers.Scheduler));

        var contexts = new List<SignatureHelpContext>();
        Add(_registry.Register(s_mockFileSelector, new Provider([triggerChar], [], (_, context) =>
        {
            contexts.Add(context);
            if (contexts.Count == 1)
            {
                // Retrigger
                timers.SetTimeout(0, () => editor.Type(triggerChar));
            }
            else
            {
                done.TrySetResult();
            }

            return s_emptySigHelpResult;
        })));

        editor.Type(triggerChar);
        await done.Task;

        contexts.Count.ShouldBe(2);
        contexts[0].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[0].TriggerCharacter.ShouldBe(triggerChar);
        contexts[0].IsRetrigger.ShouldBeFalse();
        contexts[0].ActiveSignatureHelp.ShouldBeNull();
        contexts[1].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[1].IsRetrigger.ShouldBeTrue();
        contexts[1].TriggerCharacter.ShouldBe(triggerChar);
        contexts[1].ActiveSignatureHelp.ShouldBe(s_emptySigHelp);
    });

    [Fact]
    public void Provider_Should_Not_Be_Retriggered_If_Previous_Help_Is_Canceled_First() => FakeTimers.Run(async timers =>
    {
        var done = new TaskCompletionSource();
        const string triggerChar = "(";

        var editor = CreateMockEditor("");
        var hintModel = Add(new ParameterHintsModel(editor, timers.Scheduler));

        var contexts = new List<SignatureHelpContext>();
        Add(_registry.Register(s_mockFileSelector, new Provider([triggerChar], [], (_, context) =>
        {
            contexts.Add(context);
            if (contexts.Count == 1)
            {
                // Cancel and retrigger
                hintModel.Cancel();
                editor.Type(triggerChar);
            }
            else
            {
                done.TrySetResult();
            }

            return s_emptySigHelpResult;
        })));

        editor.Type(triggerChar);
        await done.Task;

        contexts.Count.ShouldBe(2);
        contexts[0].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[0].TriggerCharacter.ShouldBe(triggerChar);
        contexts[0].IsRetrigger.ShouldBeFalse();
        contexts[0].ActiveSignatureHelp.ShouldBeNull();
        contexts[1].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[1].TriggerCharacter.ShouldBe(triggerChar);
        contexts[1].IsRetrigger.ShouldBeTrue();
        contexts[1].ActiveSignatureHelp.ShouldBeNull();
    });

    [Fact]
    public void Provider_Should_Get_Last_Trigger_Character_When_Triggered_Multiple_Times_And_Only_Be_Invoked_Once() => FakeTimers.Run(async timers =>
    {
        var done = new TaskCompletionSource();

        var editor = CreateMockEditor("");
        Add(new ParameterHintsModel(editor, timers.Scheduler, 5));

        var contexts = new List<SignatureHelpContext>();
        var invokeCountAfterWait = -1;
        Add(_registry.Register(s_mockFileSelector, new Provider(["a", "b", "c"], [], (_, context) =>
        {
            contexts.Add(context);

            // Give some time to allow for later triggers
            timers.SetTimeout(50, () =>
            {
                invokeCountAfterWait = contexts.Count;
                done.TrySetResult();
            });
            return null;
        })));

        editor.Type("a");
        editor.Type("b");
        editor.Type("c");

        await done.Task;

        invokeCountAfterWait.ShouldBe(1);
        contexts[0].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[0].IsRetrigger.ShouldBeFalse();
        contexts[0].TriggerCharacter.ShouldBe("c");
    });

    [Fact]
    public void Provider_Should_Be_Retriggered_If_Already_Active_After_Delay() => FakeTimers.Run(async timers =>
    {
        var done = new TaskCompletionSource();

        var editor = CreateMockEditor("");
        Add(new ParameterHintsModel(editor, timers.Scheduler, 5));

        var contexts = new List<SignatureHelpContext>();
        Add(_registry.Register(s_mockFileSelector, new Provider(["a", "b"], [], (_, context) =>
        {
            contexts.Add(context);
            if (contexts.Count == 1)
            {
                // retrigger after delay for widget to show up
                timers.SetTimeout(50, () => editor.Type("b"));
            }
            else if (contexts.Count == 2)
            {
                done.TrySetResult();
            }

            return s_emptySigHelpResult;
        })));

        editor.Type("a");
        await done.Task;

        contexts.Count.ShouldBe(2);
        contexts[0].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[0].TriggerCharacter.ShouldBe("a");
        contexts[1].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[1].IsRetrigger.ShouldBeTrue();
        contexts[1].TriggerCharacter.ShouldBe("b");
    });

    [Fact]
    public void Should_Cancel_Existing_Request_When_New_Request_Comes_In() => FakeTimers.Run(async timers =>
    {
        var editor = CreateMockEditor("abc def");
        var hintsModel = Add(new ParameterHintsModel(editor, timers.Scheduler));

        var didRequestCancellationOf = -1;
        var invokeCount = 0;
        Add(_registry.Register(s_mockFileSelector, Provider.Async([], [], async (token, _) =>
        {
            var count = invokeCount++;
            token.Register(() => didRequestCancellationOf = count);

            // retrigger on first request
            if (count == 0)
            {
                hintsModel.Trigger(new ParameterHintsTriggerContext(SignatureHelpTriggerKind.Invoke), 0);
            }

            var completion = new TaskCompletionSource();
            timers.SetTimeout(100, completion.SetResult);
            await completion.Task;
            return new SignatureHelpResult(new SignatureHelp([new SignatureInformation(count.ToString(System.Globalization.CultureInfo.InvariantCulture), [])], 0, 0));
        })));

        hintsModel.Trigger(new ParameterHintsTriggerContext(SignatureHelpTriggerKind.Invoke), 0);
        didRequestCancellationOf.ShouldBe(-1);

        var newParameterHints = await GetNextHint(hintsModel);
        didRequestCancellationOf.ShouldBe(0);
        newParameterHints!.Signatures[0].Label.ShouldBe("1");
    });

    [Fact]
    public void Provider_Should_Be_Retriggered_By_Retrigger_Character() => FakeTimers.Run(async timers =>
    {
        var done = new TaskCompletionSource();
        const string triggerChar = "a";
        const string retriggerChar = "b";

        var editor = CreateMockEditor("");
        Add(new ParameterHintsModel(editor, timers.Scheduler, 5));

        var contexts = new List<SignatureHelpContext>();
        Add(_registry.Register(s_mockFileSelector, new Provider([triggerChar], [retriggerChar], (_, context) =>
        {
            contexts.Add(context);
            if (contexts.Count == 1)
            {
                // retrigger after delay for widget to show up
                timers.SetTimeout(50, () => editor.Type(retriggerChar));
            }
            else if (contexts.Count == 2)
            {
                done.TrySetResult();
            }

            return s_emptySigHelpResult;
        })));

        // This should not trigger anything
        editor.Type(retriggerChar);

        // But a trigger character should
        editor.Type(triggerChar);

        await done.Task;

        contexts.Count.ShouldBe(2);
        contexts[0].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[0].TriggerCharacter.ShouldBe(triggerChar);
        contexts[1].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[1].IsRetrigger.ShouldBeTrue();
        contexts[1].TriggerCharacter.ShouldBe(retriggerChar);
    });

    [Fact]
    public void Should_Use_First_Result_From_Multiple_Providers() => FakeTimers.Run(async timers =>
    {
        const string triggerChar = "a";
        const string firstProviderId = "firstProvider";
        const string secondProviderId = "secondProvider";
        const string parameterLabel = "parameter";

        var editor = CreateMockEditor("");
        var model = Add(new ParameterHintsModel(editor, timers.Scheduler, 5));

        Add(_registry.Register(s_mockFileSelector, new Provider([triggerChar], [], (_, context) =>
        {
            if (!context.IsRetrigger)
            {
                // retrigger after delay for widget to show up
                timers.SetTimeout(50, () => editor.Type(triggerChar));

                return new SignatureHelpResult(new SignatureHelp(
                    [new SignatureInformation(firstProviderId, [new ParameterInformation { Label = parameterLabel }])], 0, 0));
            }

            return null;
        })));

        Add(_registry.Register(s_mockFileSelector, new Provider([triggerChar], [], (_, context) =>
        {
            if (context.IsRetrigger)
            {
                return new SignatureHelpResult(new SignatureHelp(
                    [new SignatureInformation(secondProviderId, context.ActiveSignatureHelp is { } active ? active.Signatures[0].Parameters : [])],
                    context.ActiveSignatureHelp is { } previous ? previous.ActiveSignature + 1 : 0,
                    0));
            }

            return null;
        })));

        editor.Type(triggerChar);

        var firstHint = (await GetNextHint(model))!;
        firstHint.Signatures[0].Label.ShouldBe(firstProviderId);
        firstHint.ActiveSignature.ShouldBe(0);
        firstHint.Signatures[0].Parameters[0].Label.ShouldBe(parameterLabel);

        var secondHint = (await GetNextHint(model))!;
        secondHint.Signatures[0].Label.ShouldBe(secondProviderId);
        secondHint.ActiveSignature.ShouldBe(1);
        secondHint.Signatures[0].Parameters[0].Label.ShouldBe(parameterLabel);
    });

    [Fact]
    public void Quick_Typing_Should_Use_The_First_Trigger_Character() => FakeTimers.Run(async timers =>
    {
        var editor = CreateMockEditor("");
        var model = Add(new ParameterHintsModel(editor, timers.Scheduler, 50));

        const string triggerCharacter = "a";

        var contexts = new List<SignatureHelpContext>();
        Add(_registry.Register(s_mockFileSelector, new Provider([triggerCharacter], [], (_, context) =>
        {
            contexts.Add(context);
            return s_emptySigHelpResult;
        })));

        editor.Type(triggerCharacter);
        editor.Type("x");

        await GetNextHint(model);

        contexts.Count.ShouldBe(1);
        contexts[0].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[0].TriggerCharacter.ShouldBe(triggerCharacter);
    });

    [Fact]
    public void Retrigger_While_A_Pending_Resolve_Is_Still_Going_On_Should_Preserve_Last_Active_Signature_96702() => FakeTimers.Run(async timers =>
    {
        var done = new TaskCompletionSource();

        var editor = CreateMockEditor("");
        var model = Add(new ParameterHintsModel(editor, timers.Scheduler, 50));

        const string triggerCharacter = "a";
        const string retriggerCharacter = "b";

        var contexts = new List<SignatureHelpContext>();
        Add(_registry.Register(s_mockFileSelector, Provider.Async([triggerCharacter], [retriggerCharacter], async (_, context) =>
        {
            contexts.Add(context);
            if (contexts.Count == 1)
            {
                timers.SetTimeout(50, () => editor.Type(retriggerCharacter));
            }
            else if (contexts.Count == 2)
            {
                // Trigger again while we wait for resolve to take place
                timers.SetTimeout(50, () => editor.Type(retriggerCharacter));
                var wait = new TaskCompletionSource();
                timers.SetTimeout(1000, wait.SetResult);
                await wait.Task;
            }
            else if (contexts.Count == 3)
            {
                done.TrySetResult();
            }

            return s_emptySigHelpResult;
        })));

        editor.Type(triggerCharacter);

        await GetNextHint(model);
        await GetNextHint(model);

        await done.Task;

        contexts.Count.ShouldBe(3);
        contexts[0].TriggerKind.ShouldBe(SignatureHelpTriggerKind.TriggerCharacter);
        contexts[0].TriggerCharacter.ShouldBe(triggerCharacter);

        // Make sure that in a retrigger during a pending resolve, we still have the old active signature.
        contexts[2].ActiveSignatureHelp.ShouldBe(s_emptySigHelp);
    });

    // ---- own tests

    [Fact]
    public void Next_And_Previous_Cycle_The_Signatures() => FakeTimers.Run(async timers =>
    {
        var editor = CreateMockEditor("");
        var model = Add(new ParameterHintsModel(editor, timers.Scheduler, 5));
        Add(_registry.Register(s_mockFileSelector, new Provider(["("], [], (_, _) =>
            new SignatureHelpResult(new SignatureHelp(
                [new SignatureInformation("f()", []), new SignatureInformation("f(a)", []), new SignatureInformation("f(a, b)", [])], 0, 0)))));

        editor.Type("(");
        (await GetNextHint(model))!.ActiveSignature.ShouldBe(0);

        var seen = new List<int?>();
        model.OnChangedHints += hints => seen.Add(hints?.ActiveSignature);
        model.Next();
        model.Next();
        model.Next();
        model.Previous();
        seen.ShouldBe([1, 2, 0, 2]);

        editor.Options = new ParameterHintsOptions { Cycle = false };
        model.Previous();
        model.Previous();
        model.Previous();
        seen.ShouldBe([1, 2, 0, 2, 1, 0, null]);
        model.ActiveHints.ShouldBeNull();
    });

    [Fact]
    public void Mouse_Cursor_Move_Cancels_And_Keyboard_Move_Retriggers() => FakeTimers.Run(async timers =>
    {
        var editor = CreateMockEditor("f(a, b)");
        var model = Add(new ParameterHintsModel(editor, timers.Scheduler, 5));
        var contexts = new List<SignatureHelpContext>();
        Add(_registry.Register(s_mockFileSelector, new Provider(["("], [], (_, context) =>
        {
            contexts.Add(context);
            return s_emptySigHelpResult;
        })));

        model.Trigger(new ParameterHintsTriggerContext(SignatureHelpTriggerKind.Invoke), 0);
        (await GetNextHint(model)).ShouldNotBeNull();

        editor.MoveCursor(new TextPosition(1, 5), "keyboard");
        (await GetNextHint(model)).ShouldNotBeNull();
        contexts[^1].TriggerKind.ShouldBe(SignatureHelpTriggerKind.ContentChange);
        contexts[^1].IsRetrigger.ShouldBeTrue();

        var hidden = GetNextHint(model);
        editor.MoveCursor(new TextPosition(1, 1), "mouse");
        (await hidden).ShouldBeNull();
        model.IsTriggered.ShouldBeFalse();
    });

    private static IdleScheduler FakeTimersScheduler() => ((FakeTimers)SynchronizationContext.Current!).Scheduler;

    private sealed class Provider : ISignatureHelpProvider
    {
        private readonly IReadOnlyList<string> _triggerCharacters;
        private readonly IReadOnlyList<string> _retriggerCharacters;
        private readonly Func<CancellationToken, SignatureHelpContext, Task<SignatureHelpResult?>> _provide;

        public Provider(IReadOnlyList<string> triggerCharacters, IReadOnlyList<string> retriggerCharacters, Func<CancellationToken, SignatureHelpContext, SignatureHelpResult?> provide)
            : this(triggerCharacters, retriggerCharacters, (token, context) => Task.FromResult(provide(token, context)), true)
        {
        }

        private Provider(IReadOnlyList<string> triggerCharacters, IReadOnlyList<string> retriggerCharacters, Func<CancellationToken, SignatureHelpContext, Task<SignatureHelpResult?>> provide, bool _)
        {
            _triggerCharacters = triggerCharacters;
            _retriggerCharacters = retriggerCharacters;
            _provide = provide;
        }

        public static Provider Async(IReadOnlyList<string> triggerCharacters, IReadOnlyList<string> retriggerCharacters, Func<CancellationToken, SignatureHelpContext, Task<SignatureHelpResult?>> provide) =>
            new(triggerCharacters, retriggerCharacters, provide, true);

        public IReadOnlyList<string>? SignatureHelpTriggerCharacters => _triggerCharacters;

        public IReadOnlyList<string>? SignatureHelpRetriggerCharacters => _retriggerCharacters;

        public Task<SignatureHelpResult?> ProvideSignatureHelp(TextModel model, TextPosition position, CancellationToken token, SignatureHelpContext context) =>
            _provide(token, context);
    }

    // upstream's test code editor, as far as the parameter hints model sees it
    private sealed class TestEditor(TextModel model, LanguageFeatureRegistry<ISignatureHelpProvider> registry) : IParameterHintsEditor
    {
        private TextPosition _position = new(1, 1);

        public TextModel? Model { get; } = model;

        public ParameterHintsOptions Options { get; set; } = ParameterHintsOptions.Default;

        public ParameterHintsOptions ParameterHintsOptions => Options;

        public LanguageFeatureRegistry<ISignatureHelpProvider> SignatureHelpProviders { get; } = registry;

        public event Action? DidBlurEditorWidget;

        public event Action? DidChangeConfiguration;

        public event Action? DidChangeModel;

        public event Action<string>? DidChangeCursorSelection;

        public event Action? DidChangeModelContent;

        public event Action<string>? DidType;

        public TextPosition? GetPosition() => _position;

        // the editor's type handler: the edit, then the content and cursor events, then onDidType
        public void Type(string text)
        {
            Model!.ApplyEdits([new SingleEditOperation(new TextRange(_position.LineNumber, _position.Column, _position.LineNumber, _position.Column), text)]);
            _position = new TextPosition(_position.LineNumber, _position.Column + text.Length);
            DidChangeModelContent?.Invoke();
            DidChangeCursorSelection?.Invoke("keyboard");
            DidType?.Invoke(text);
        }

        public void MoveCursor(TextPosition position, string source)
        {
            _position = position;
            DidChangeCursorSelection?.Invoke(source);
        }

        public void Blur() => DidBlurEditorWidget?.Invoke();

        public void ChangeConfiguration() => DidChangeConfiguration?.Invoke();

        public void ChangeModel() => DidChangeModel?.Invoke();
    }
}
