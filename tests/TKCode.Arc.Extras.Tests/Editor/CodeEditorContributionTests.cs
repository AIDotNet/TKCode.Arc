using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Shouldly;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc.Tests.Editor;

/// <summary>
/// The contribution host (upstream's editorExtensions.ts and codeEditorContributions.ts): contributions by
/// instantiation kind, their disposal, editor actions with keybindings and preconditions, trigger(), context keys and
/// the language feature context keys.
/// </summary>
public class CodeEditorContributionTests
{
    private sealed class Probe : ICodeEditorContribution
    {
        public Probe(CodeEditor editor, List<string> log, string id)
        {
            Editor = editor;
            Log = log;
            Id = id;
            log.Add("create " + id);
        }

        public CodeEditor Editor { get; }

        public List<string> Log { get; }

        public string Id { get; }

        public int Calls { get; set; }

        public void Dispose() => Log.Add("dispose " + Id);
    }

    private static (CodeEditor Editor, Window Window) Show(
        IReadOnlyList<EditorContributionDescription> contributions,
        IReadOnlyList<EditorCommand> commands,
        string text = "hello")
    {
        var editor = new CodeEditor(contributions, commands) { Text = text, Width = 400, Height = 200 };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Dispose();
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    [AvaloniaFact]
    public void Contributions_are_created_by_kind_and_disposed_with_the_editor()
    {
        var log = new List<string>();
        EditorContributionDescription[] contributions =
        [
            new("eager", e => new Probe(e, log, "eager")),
            new("idle", e => new Probe(e, log, "idle"), EditorContributionInstantiation.Eventually),
            new("lazy", e => new Probe(e, log, "lazy"), EditorContributionInstantiation.Lazy),
            new("render", e => new Probe(e, log, "render"), EditorContributionInstantiation.AfterFirstRender),
        ];
        var (editor, window) = Show(contributions, []);

        // the eager one at once, the idle ones when the dispatcher was idle; the lazy one only when asked for
        log.ShouldBe(["create eager", "create render", "create idle"]);
        editor.GetContributionIds().ShouldBe(["eager", "render", "idle"]);
        editor.GetContribution<Probe>("lazy").ShouldNotBeNull().Editor.ShouldBe(editor);
        log.ShouldContain("create lazy");
        editor.GetContribution<Probe>("missing").ShouldBeNull();

        // removed from the visual tree: disposed newest first; back: created again
        window.Content = null;
        log.Skip(4).ShouldBe(["dispose lazy", "dispose idle", "dispose render", "dispose eager"]);
        editor.HasContributions.ShouldBeFalse();
        window.Content = editor;
        Dispatcher.UIThread.RunJobs();
        editor.GetContribution<Probe>("eager").ShouldNotBeNull();
    }

    [AvaloniaFact]
    public void A_contribution_before_first_interaction_is_created_by_the_first_key()
    {
        var log = new List<string>();
        var editor = new CodeEditor([new("interaction", e => new Probe(e, log, "interaction"), EditorContributionInstantiation.BeforeFirstInteraction)], [])
        {
            Width = 400,
            Height = 200,
        };
        var window = TestHelpers.Show(editor);
        editor.Focus();
        editor.OnBeforeInteractionEvent();
        log.ShouldBe(["create interaction"]);
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        log.ShouldBe(["create interaction"]);
    }

    [AvaloniaFact]
    public void An_action_runs_through_its_keybinding_when_its_precondition_holds()
    {
        var runs = new List<object?>();
        var action = EditorAction.Create(
            "test.action",
            "Test Action",
            EditorContextKeys.ReadOnly.ToNegated(),
            (editor, args) => runs.Add(args),
            new EditorKeybinding(KeyMod.CtrlCmd | (int)EditorKeyCode.KeyJ) { Args = "from-key" });
        var (editor, window) = Show([], [action]);

        var modifier = OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.J, modifier);
        Dispatcher.UIThread.RunJobs();
        runs.ShouldBe(["from-key"]);

        // read-only: the precondition (editorReadonly is false) fails, the key does nothing
        editor.IsReadOnly = true;
        window.KeyPressQwerty(PhysicalKey.J, modifier);
        Dispatcher.UIThread.RunJobs();
        runs.Count.ShouldBe(1);
        editor.GetAction("test.action").ShouldNotBeNull().IsSupported().ShouldBeFalse();
        editor.GetSupportedActions().ShouldBeEmpty();

        editor.IsReadOnly = false;
        var bound = editor.GetAction("test.action")!;
        bound.Label.ShouldBe("Test Action");
        bound.IsSupported().ShouldBeTrue();
        bound.Run("direct");
        editor.Trigger("api", "test.action", "trigger");
        runs.ShouldBe(["from-key", "direct", "trigger"]);
    }

    [AvaloniaFact]
    public void A_command_bound_to_a_contribution_calls_it()
    {
        var log = new List<string>();
        var command = EditorCommand.BindToContribution<Probe>("probe", "test.probeCommand", null, (probe, _) => probe.Calls++);
        var (editor, _) = Show([new("probe", e => new Probe(e, log, "probe"))], [command]);

        editor.Trigger("api", "test.probeCommand", null);
        editor.ExecuteCommand("test.probeCommand").Wait();
        editor.GetContribution<Probe>("probe")!.Calls.ShouldBe(2);

        // a command is not an action
        editor.GetAction("test.probeCommand").ShouldBeNull();
    }

    [AvaloniaFact]
    public void Trigger_types_pastes_and_runs_core_commands()
    {
        var typed = new List<string>();
        var (editor, _) = Show([], [], "");
        editor.DidType += typed.Add;

        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload("ab"));
        editor.Trigger("keyboard", EditorHandler.Paste, new PastePayload("cd"));
        editor.Text.ShouldBe("abcd");
        typed.ShouldBe(["ab"]);

        editor.Trigger("keyboard", "cursorHome", null);
        editor.Trigger("keyboard", EditorHandler.Type, new TypePayload(">"));
        editor.Text.ShouldBe(">abcd");
    }

    [AvaloniaFact]
    public void Context_keys_bind_to_the_editor_and_gate_keybindings()
    {
        var runs = 0;
        var key = new RawContextKey<bool>("testWidgetVisible", false);
        var action = EditorAction.Create(
            "test.close",
            "Close",
            null,
            (_, _) => runs++,
            new EditorKeybinding((int)EditorKeyCode.Escape) { When = key });
        var (editor, window) = Show([], [action]);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        runs.ShouldBe(0);

        var bound = editor.CreateContextKey(key);
        bound.Set(true);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        runs.ShouldBe(1);
        editor.ContextKeyService.ContextMatchesRules(CodeEditorContextKeys.FindWidgetVisible.ToNegated()).ShouldBeTrue();
    }

    [AvaloniaFact]
    public void Language_feature_context_keys_follow_the_registries_and_the_language()
    {
        var (editor, _) = Show([], [], "x");
        var service = editor.ContextKeyService;
        service.ContextMatchesRules(EditorContextKeys.HasHoverProvider).ShouldBeFalse();
        service.ContextMatchesRules(ContextKeyExpr.Equals("editorLangId", "plaintext")).ShouldBeTrue();

        var registration = editor.LanguageFeatures.HoverProvider.Register("csharp", new NullHover());
        service.ContextMatchesRules(EditorContextKeys.HasHoverProvider).ShouldBeFalse();
        editor.LanguageId = "csharp";
        service.ContextMatchesRules(EditorContextKeys.HasHoverProvider).ShouldBeTrue();
        service.ContextMatchesRules(ContextKeyExpr.Equals("editorLangId", "csharp")).ShouldBeTrue();

        registration.Dispose();
        service.ContextMatchesRules(EditorContextKeys.HasHoverProvider).ShouldBeFalse();
    }

    [AvaloniaFact]
    public void Keys_from_a_widget_run_keybindings_but_never_type()
    {
        var runs = 0;
        var action = EditorAction.Create(
            "test.escape",
            "Escape",
            null,
            (_, _) => runs++,
            new EditorKeybinding((int)EditorKeyCode.Escape));
        var (editor, window) = Show([], [action], "abc");
        var widget = new Border { Width = 50, Height = 20, Focusable = true };
        editor.AddOverlayWidget(new Overlay("w", widget));
        Dispatcher.UIThread.RunJobs();
        widget.Focus();
        Dispatcher.UIThread.RunJobs();
        editor.IsTextFocused.ShouldBeFalse();
        editor.ContextKeyService.ContextMatchesRules(EditorContextKeys.EditorTextFocus).ShouldBeFalse();
        editor.ContextKeyService.ContextMatchesRules(EditorContextKeys.Focus).ShouldBeTrue();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyTextInput("x");
        Dispatcher.UIThread.RunJobs();
        runs.ShouldBe(1);
        editor.Text.ShouldBe("abc");

        editor.Focus();
        Dispatcher.UIThread.RunJobs();
        editor.IsTextFocused.ShouldBeTrue();
    }

    private sealed class NullHover : IHoverProvider
    {
        public Task<Hover?> ProvideHover(TextModel model, TextPosition position, CancellationToken token, HoverContext? context = null) =>
            Task.FromResult<Hover?>(null);
    }

    private sealed class Overlay(string id, Control control) : ICodeEditorOverlayWidget
    {
        public string Id => id;

        public Control Control => control;

        public OverlayWidgetPosition? GetPosition() => new(OverlayWidgetPositionPreference.TopRightCorner);
    }
}
