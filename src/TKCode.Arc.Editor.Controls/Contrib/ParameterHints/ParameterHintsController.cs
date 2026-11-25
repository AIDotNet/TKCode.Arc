// Ported from vscode (src/vs/editor/contrib/parameterHints/browser/parameterHints.ts: ParameterHintsController,
// TriggerParameterHintsAction, closeParameterHints, showPrevParameterHint, showNextParameterHint; the Context keys of
// provideSignatureHelp.ts; commit b57d5c9e), MIT license.
// Differences from upstream: the model's editor is CodeEditorParameterHintsEditor (the events of the CodeEditor); the
// options are the controller's Options; the timers run on the editor's IdleScheduler (a test can give its own).

using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.ParameterHints;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.ViewModel;
using EditorKeyCode = TKCode.Arc.Editor.KeyCode;

namespace TKCode.Arc;

/// <summary>Parameter hints: the signature help providers' signatures while typing a call (upstream's ParameterHintsController).</summary>
internal sealed class ParameterHintsController : ICodeEditorContribution
{
    public const string Id = "editor.controller.parameterHints";
    public const string TriggerParameterHintsActionId = "editor.action.triggerParameterHints";

    /// <summary>Upstream's Context.MultipleSignatures (only the parameter hints commands read it).</summary>
    public static readonly RawContextKey<bool> MultipleSignatures = new("parameterHintsMultipleSignatures", false);

    public static readonly EditorContributionDescription Description =
        new(Id, editor => new ParameterHintsController(editor), EditorContributionInstantiation.BeforeFirstInteraction);

    private const int Weight = KeybindingWeight.EditorContrib + 75;

    private readonly CodeEditor _editor;
    private readonly CodeEditorParameterHintsEditor _hintsEditor;
    private readonly IContextKey<bool> _keyMultipleSignatures;
    private ParameterHintsModel? _model;
    private ParameterHintsWidget? _widget;
    private IdleScheduler? _scheduler;
    private bool _disposed;

    private ParameterHintsController(CodeEditor editor)
    {
        _editor = editor;
        _hintsEditor = new CodeEditorParameterHintsEditor(editor);
        _keyMultipleSignatures = editor.CreateContextKey(MultipleSignatures);
        _ = Model;
    }

    /// <summary>The controller of <paramref name="editor"/>, or null.</summary>
    public static ParameterHintsController? Get(CodeEditor editor) => editor.GetContribution<ParameterHintsController>(Id);

    /// <summary>Upstream's editor.parameterHints options.</summary>
    public ParameterHintsOptions Options
    {
        get => _hintsEditor.ParameterHintsOptions;
        set => _hintsEditor.SetOptions(value);
    }

    /// <summary>The model (created on first use).</summary>
    internal ParameterHintsModel Model
    {
        get
        {
            if (_model is null)
            {
                _model = new ParameterHintsModel(_hintsEditor, _scheduler ?? CodeEditor.IdleScheduler);
                _model.OnChangedHints += OnChangedHints;
            }

            return _model;
        }
    }

    /// <summary>The widget, once shown.</summary>
    internal ParameterHintsWidget? Widget => _widget;

    /// <summary>Runs the timers on <paramref name="scheduler"/> (tests); before the first use.</summary>
    internal void SetScheduler(IdleScheduler scheduler)
    {
        _scheduler = scheduler;
        if (_model is not null)
        {
            _model.OnChangedHints -= OnChangedHints;
            _model.Dispose();
            _model = null;
        }

        _ = Model;
    }

    private void OnChangedHints(SignatureHelp? newParameterHints)
    {
        if (newParameterHints is not null)
        {
            _widget ??= new ParameterHintsWidget(_editor, Model, _keyMultipleSignatures);
            _widget.Show();
            _widget.Render(newParameterHints);
        }
        else
        {
            _widget?.Hide();
        }
    }

    /// <summary>Hides the hints (upstream's cancel).</summary>
    public void Cancel() => _model?.Cancel();

    /// <summary>Shows the previous signature.</summary>
    public void Previous() => _widget?.Previous();

    /// <summary>Shows the next signature.</summary>
    public void Next() => _widget?.Next();

    /// <summary>Asks the providers at once (upstream's trigger).</summary>
    public void Trigger(ParameterHintsTriggerContext context) => Model.Trigger(context, 0);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_model is not null)
        {
            _model.OnChangedHints -= OnChangedHints;
            _model.Dispose();
        }

        _widget?.Dispose();
        _hintsEditor.Dispose();
    }

    // ---- actions and commands

    /// <summary>Upstream's TriggerParameterHintsAction (Ctrl+Shift+Space).</summary>
    public static readonly EditorAction TriggerParameterHintsAction = EditorAction.Create(
        TriggerParameterHintsActionId, "Trigger Parameter Hints", EditorContextKeys.HasSignatureHelpProvider,
        (editor, _) => Get(editor)?.Trigger(new ParameterHintsTriggerContext(SignatureHelpTriggerKind.Invoke)),
        new EditorKeybinding(KeyMod.CtrlCmd | KeyMod.Shift | (int)EditorKeyCode.Space)
        {
            When = EditorContextKeys.EditorTextFocus,
            Weight = KeybindingWeight.EditorContrib,
        });

    public static readonly EditorCommand CloseParameterHintsCommand = EditorCommand.BindToContribution<ParameterHintsController>(
        Id, "closeParameterHints", CodeEditorContextKeys.ParameterHintsVisible, (controller, _) => controller.Cancel(),
        new EditorKeybinding((int)EditorKeyCode.Escape)
        {
            Weight = Weight,
            When = EditorContextKeys.Focus,
            Secondary = [KeyMod.Shift | (int)EditorKeyCode.Escape],
        });

    public static readonly EditorCommand ShowPrevParameterHintCommand = EditorCommand.BindToContribution<ParameterHintsController>(
        Id, "showPrevParameterHint", ContextKeyExpr.And(CodeEditorContextKeys.ParameterHintsVisible, MultipleSignatures), (controller, _) => controller.Previous(),
        new EditorKeybinding((int)EditorKeyCode.UpArrow)
        {
            Weight = Weight,
            When = EditorContextKeys.Focus,
            Secondary = [KeyMod.Alt | (int)EditorKeyCode.UpArrow],
            Mac = new PlatformKeybindings { Primary = (int)EditorKeyCode.UpArrow, Secondary = [KeyMod.Alt | (int)EditorKeyCode.UpArrow, KeyMod.WinCtrl | (int)EditorKeyCode.KeyP] },
        });

    public static readonly EditorCommand ShowNextParameterHintCommand = EditorCommand.BindToContribution<ParameterHintsController>(
        Id, "showNextParameterHint", ContextKeyExpr.And(CodeEditorContextKeys.ParameterHintsVisible, MultipleSignatures), (controller, _) => controller.Next(),
        new EditorKeybinding((int)EditorKeyCode.DownArrow)
        {
            Weight = Weight,
            When = EditorContextKeys.Focus,
            Secondary = [KeyMod.Alt | (int)EditorKeyCode.DownArrow],
            Mac = new PlatformKeybindings { Primary = (int)EditorKeyCode.DownArrow, Secondary = [KeyMod.Alt | (int)EditorKeyCode.DownArrow, KeyMod.WinCtrl | (int)EditorKeyCode.KeyN] },
        });
}

/// <summary>What the parameter hints model listens to on a <see cref="CodeEditor"/> (upstream: the ICodeEditor).</summary>
internal sealed class CodeEditorParameterHintsEditor : IParameterHintsEditor, IDisposable
{
    private readonly CodeEditor _editor;
    private ParameterHintsOptions _options = ParameterHintsOptions.Default;

    public CodeEditorParameterHintsEditor(CodeEditor editor)
    {
        _editor = editor;
        editor.DidChangeTextFocus += OnTextFocus;
        editor.DidChangeModelLanguage += OnLanguage;
        editor.DidChangeCursorSelection += OnCursor;
        editor.DidChangeModelContent += OnContent;
        editor.DidType += OnType;
    }

    public TextModel? Model => _editor.Model;

    public ParameterHintsOptions ParameterHintsOptions => _options;

    public LanguageFeatureRegistry<ISignatureHelpProvider> SignatureHelpProviders => _editor.LanguageFeatures.SignatureHelpProvider;

    public event Action? DidBlurEditorWidget;

    public event Action? DidChangeConfiguration;

    public event Action? DidChangeModel;

    public event Action<string>? DidChangeCursorSelection;

    public event Action? DidChangeModelContent;

    public event Action<string>? DidType;

    public TextPosition? GetPosition() => _editor.GetPosition();

    public void SetOptions(ParameterHintsOptions options)
    {
        _options = options;
        DidChangeConfiguration?.Invoke();
    }

    private void OnTextFocus(bool focused)
    {
        // upstream's onDidBlurEditorWidget: the focus left the editor and its widgets
        if (!focused && !_editor.IsKeyboardFocusWithin)
        {
            DidBlurEditorWidget?.Invoke();
        }
    }

    private void OnLanguage(ModelLanguageChangedEvent e) => DidChangeModel?.Invoke();

    private void OnCursor(CursorStateChangedEvent e) => DidChangeCursorSelection?.Invoke(e.Source);

    private void OnContent(ModelContentChangedEvent e) => DidChangeModelContent?.Invoke();

    private void OnType(string text) => DidType?.Invoke(text);

    public void Dispose()
    {
        _editor.DidChangeTextFocus -= OnTextFocus;
        _editor.DidChangeModelLanguage -= OnLanguage;
        _editor.DidChangeCursorSelection -= OnCursor;
        _editor.DidChangeModelContent -= OnContent;
        _editor.DidType -= OnType;
    }
}
