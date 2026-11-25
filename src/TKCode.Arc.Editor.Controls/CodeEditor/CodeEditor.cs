using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Commands;
using TKCode.Arc.Editor.Config;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Standalone;
using TKCode.Arc.Editor.Languages.BuiltIn;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.Platform;
using TKCode.Arc.Editor.Platform.Markers;
using TKCode.Arc.Editor.Services;
using TKCode.Arc.Editor.ViewModel;
using EditorViewModel = TKCode.Arc.Editor.ViewModel.ViewModel;

namespace TKCode.Arc;

/// <summary>
/// A code editor modelled on the Monaco editor: a piece tree text buffer with undo, multiple cursors, auto-closing
/// pairs, auto-indent, word wrap and Monaco's keybindings.
/// </summary>
[TemplatePart(PartView, typeof(CodeEditorView))]
[TemplatePart(PartVerticalScrollBar, typeof(ScrollBar))]
[TemplatePart(PartHorizontalScrollBar, typeof(ScrollBar))]
[TemplatePart(PartWidgetHost, typeof(CodeEditorWidgetHost))]
public sealed partial class CodeEditor : TemplatedControl
{
    internal const string PartView = "PART_View";
    internal const string PartWidgetHost = "PART_WidgetHost";
    internal const string PartVerticalScrollBar = "PART_VerticalScrollBar";
    internal const string PartHorizontalScrollBar = "PART_HorizontalScrollBar";

    /// <summary>The text of the document. Setting it replaces the document and clears the undo history; typing updates it (two-way by default).</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<CodeEditor, string>(nameof(Text), string.Empty, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The language of the document, by id (<c>csharp</c>, <c>typescript</c>) or name (<c>C#</c>, <c>TypeScript</c>): its syntax highlighting and its configuration (brackets, auto-closing pairs, indentation). The 89 built-in languages of the Monaco editor are known. Default <c>plaintext</c>.</summary>
    public static readonly StyledProperty<string> LanguageIdProperty =
        AvaloniaProperty.Register<CodeEditor, string>(nameof(LanguageId), LanguageIds.PlaintextLanguageId);

    /// <summary>Whether the user cannot change the text. The cursor still moves, and text can still be selected and copied.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(IsReadOnly));

    /// <summary>The width of a tab, in spaces (at least 1; default 4); the indent size follows it.</summary>
    public static readonly StyledProperty<int> TabSizeProperty =
        AvaloniaProperty.Register<CodeEditor, int>(nameof(TabSize), 4, coerce: static (_, value) => Math.Max(1, value));

    /// <summary>Whether Tab and auto-indent insert spaces instead of tab characters (default true).</summary>
    public static readonly StyledProperty<bool> InsertSpacesProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(InsertSpaces), true);

    /// <summary>Whether long lines wrap at the width of the editor (default false). Wrapped parts have no line number.</summary>
    public static readonly StyledProperty<bool> WordWrapProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(WordWrap));

    /// <summary>Whether line numbers show left of the text (default true).</summary>
    public static readonly StyledProperty<bool> ShowLineNumbersProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(ShowLineNumbers), true);

    /// <summary>Whether a zoomed out picture of the document shows at the right edge, with a slider to scroll it, as
    /// Monaco's <c>minimap.enabled</c> option (default true).</summary>
    public static readonly StyledProperty<bool> ShowMinimapProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(ShowMinimap), true);

    /// <summary>Whether vertical lines mark the indentation levels, with the one of the block the cursor is in highlighted,
    /// as Monaco's <c>guides.indentation</c> option (default true).</summary>
    public static readonly StyledProperty<bool> ShowIndentGuidesProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(ShowIndentGuides), true);

    /// <summary>Whether scrolling animates (the wheel, Page Up / Page Down and revealing the cursor), as Monaco's
    /// <c>smoothScrolling</c> option (default false).</summary>
    public static readonly StyledProperty<bool> SmoothScrollingProperty =
        AvaloniaProperty.Register<CodeEditor, bool>(nameof(SmoothScrolling));

    private static int s_nextEditorId;

    private readonly int _editorId = Interlocked.Increment(ref s_nextEditorId);
    private CodeEditorView? _view;
    private bool _syncingText;
    private IAttachedView? _attachedView;
    private readonly IDisposable _markerDecorationsRegistration;
    private readonly CodeEditorModeContext _modeContext;
    private bool _textFocused;

    static CodeEditor()
    {
        FocusableProperty.OverrideDefaultValue<CodeEditor>(true);
        RegisterInputHandlers();
    }

    /// <summary>Creates an empty plain text editor.</summary>
    public CodeEditor()
        : this(CodeEditorContributions.Contributions, CodeEditorContributions.EditorCommands)
    {
    }

    /// <summary>
    /// Creates an editor with the given contributions and editor commands instead of the built-in ones
    /// (<see cref="CodeEditorContributions"/>); tests use it.
    /// </summary>
    internal CodeEditor(IReadOnlyList<EditorContributionDescription> contributions, IReadOnlyList<EditorCommand> editorCommands)
    {
        _contributionDescriptions = contributions;
        LanguageServices = new LanguageServices();
        foreach (var language in BuiltInLanguages.All)
        {
            language.Register(LanguageServices);
        }

        UpdateTokenTheme();
        ActualThemeVariantChanged += (_, _) => UpdateTokenTheme();
        CommandsRegistry = new CommandsRegistry();
        KeybindingsRegistry = new KeybindingsRegistry(CommandsRegistry);
        CoreCommandsRegistration.Register(CommandsRegistry, KeybindingsRegistry);
        ContextKeyService = new ContextKeyService();
        Model = CreateModel(string.Empty, LanguageIds.PlaintextLanguageId);
        AttachModel(Model);
        MarkerService = new MarkerService();
        MarkerDecorationsService = new MarkerDecorationsService(MarkerService, LanguageConfigurationService);
        _markerDecorationsRegistration = MarkerDecorationsService.AddModel(Model);
        _modeContext = new CodeEditorModeContext(Model, ContextKeyService, LanguageFeatures);
        RegisterEditorCommands(editorCommands);
    }

    /// <summary>The text of the document. Setting it replaces the document and clears the undo history.</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The language of the document, by id or name: its syntax highlighting and its configuration.</summary>
    public string LanguageId
    {
        get => GetValue(LanguageIdProperty);
        set => SetValue(LanguageIdProperty, value);
    }

    /// <summary>Whether the user cannot change the text.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>The width of a tab, in spaces; the indent size follows it.</summary>
    public int TabSize
    {
        get => GetValue(TabSizeProperty);
        set => SetValue(TabSizeProperty, value);
    }

    /// <summary>Whether Tab and auto-indent insert spaces instead of tab characters.</summary>
    public bool InsertSpaces
    {
        get => GetValue(InsertSpacesProperty);
        set => SetValue(InsertSpacesProperty, value);
    }

    /// <summary>Whether long lines wrap at the width of the editor.</summary>
    public bool WordWrap
    {
        get => GetValue(WordWrapProperty);
        set => SetValue(WordWrapProperty, value);
    }

    /// <summary>Whether line numbers show left of the text.</summary>
    public bool ShowLineNumbers
    {
        get => GetValue(ShowLineNumbersProperty);
        set => SetValue(ShowLineNumbersProperty, value);
    }

    /// <summary>Whether vertical lines mark the indentation levels, with the one of the block the cursor is in highlighted,
    /// as Monaco's <c>guides.indentation</c> option (default true).</summary>
    public bool ShowIndentGuides
    {
        get => GetValue(ShowIndentGuidesProperty);
        set => SetValue(ShowIndentGuidesProperty, value);
    }

    /// <summary>Whether a zoomed out picture of the document shows at the right edge, with a slider to scroll it.</summary>
    public bool ShowMinimap
    {
        get => GetValue(ShowMinimapProperty);
        set => SetValue(ShowMinimapProperty, value);
    }

    /// <summary>Whether scrolling animates (the wheel, Page Up / Page Down and revealing the cursor).</summary>
    public bool SmoothScrolling
    {
        get => GetValue(SmoothScrollingProperty);
        set => SetValue(SmoothScrollingProperty, value);
    }

    /// <summary>Raised when the user or the program changed the text of the document.</summary>
    public event EventHandler? TextChanged;

    internal TextModel Model { get; private set; }

    internal EditorViewModel? ViewModel { get; private set; }

    internal ViewModelConfiguration? Configuration { get; private set; }

    /// <summary>The languages, their configurations and tokenizers, and the token theme (one set per editor).</summary>
    internal LanguageServices LanguageServices { get; }

    internal LanguageConfigurationService LanguageConfigurationService => LanguageServices.LanguageConfigurationService;

    internal LanguageIdCodec LanguageIdCodec => LanguageServices.LanguageService.Codec;

    /// <summary>The tokenizers the editor's models use; the built-in languages register here.</summary>
    internal TokenizationRegistry<ITokenizationSupport> TokenizationRegistry => LanguageServices.TokenizationRegistry;

    /// <summary>
    /// Runs the model's background tokenization on the UI thread when it is idle (upstream's runWhenGlobalIdle):
    /// Background priority, after input and rendering.
    /// </summary>
    internal static IdleScheduler IdleScheduler { get; } = new(
        static action => PostIdle(action),
        runAfter: static (delay, action) => PostAfter(delay, action));

    internal CommandsRegistry CommandsRegistry { get; }

    internal KeybindingsRegistry KeybindingsRegistry { get; }

    internal ContextKeyService ContextKeyService { get; }

    /// <summary>The language feature providers of the editor's languages (hover, completion, ...; upstream's ILanguageFeaturesService).</summary>
    internal LanguageFeatures LanguageFeatures => LanguageServices.LanguageFeatures;

    /// <summary>The markers (errors, warnings) of documents; the editor shows those of its model's URI as squiggles.</summary>
    internal MarkerService MarkerService { get; }

    /// <summary>Turns the markers of the editor's model into decorations, and decorations back into markers.</summary>
    internal MarkerDecorationsService MarkerDecorationsService { get; }

    /// <summary>How the view draws decoration class names (upstream's decoration CSS rules); features register theirs.</summary>
    internal CodeEditorDecorationStyles DecorationStyles { get; } = new();

    /// <summary>The gutter's columns besides the line numbers (glyph margin, lines decorations, folding).</summary>
    internal CodeEditorGutter Gutter { get; } = new();

    /// <summary>The registration that shows the markers of the editor's model (dispose to stop).</summary>
    internal IDisposable MarkerDecorationsRegistration => _markerDecorationsRegistration;

    /// <summary>The context keys of the model's language and its providers.</summary>
    internal CodeEditorModeContext ModeContext => _modeContext;

    /// <summary>Whether the editor's text has keyboard focus (not a widget in it).</summary>
    internal bool IsTextFocused => _textFocused;

    /// <summary>The editor's view (null before the template is applied).</summary>
    internal CodeEditorView? View => _view;

    /// <summary>Binds <paramref name="key"/> to the editor's context key service, for a feature to set as its state changes.</summary>
    internal IContextKey<T> CreateContextKey<T>(RawContextKey<T> key) => key.BindTo(ContextKeyService);

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        DisposeContributions();
        _view?.Detach();
        AttachWidgetHost(e.NameScope.Find<CodeEditorWidgetHost>(PartWidgetHost));
        _view = e.NameScope.Find<CodeEditorView>(PartView);
        _view?.Attach(this);
        _view?.AttachScrollBars(e.NameScope.Find<ScrollBar>(PartVerticalScrollBar), e.NameScope.Find<ScrollBar>(PartHorizontalScrollBar));
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InitializeContributions();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        DisposeContributions();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ForegroundProperty || change.Property == BackgroundProperty)
        {
            UpdateTokenTheme();
        }

        if (change.Property == TextProperty && !_syncingText)
        {
            Model.SetValue(change.GetNewValue<string>() ?? string.Empty);
        }
        else if (change.Property == LanguageIdProperty)
        {
            var languageId = ResolveLanguageId(change.GetNewValue<string>());
            LanguageIdCodec.Register(languageId);
            Model.SetLanguage(languageId);
        }
        else if (change.Property == TabSizeProperty || change.Property == InsertSpacesProperty)
        {
            Model.UpdateOptions(new TextModelUpdateOptions { TabSize = TabSize, IndentSizeIsTabSize = true, InsertSpaces = InsertSpaces });
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            _view?.UpdateConfiguration();
            OnReadOnlyChanged();
        }
        else if (change.Property == WordWrapProperty || change.Property == SmoothScrollingProperty || change.Property == ShowMinimapProperty)
        {
            _view?.UpdateConfiguration();
        }
        else if (change.Property == ShowLineNumbersProperty)
        {
            _view?.InvalidateVisual();
        }
        else if (change.Property == ShowIndentGuidesProperty)
        {
            _view?.InvalidateLayers(CodeEditorLayers.Background);
        }
    }

    // A language id, or a language's name or alias (C#, JavaScript, ...); an unknown name is taken as an id.
    private string ResolveLanguageId(string? languageIdOrName)
    {
        if (string.IsNullOrEmpty(languageIdOrName))
        {
            return LanguageIds.PlaintextLanguageId;
        }

        var languages = LanguageServices.LanguageService;
        return languages.IsRegisteredLanguageId(languageIdOrName)
            ? languageIdOrName
            : languages.GetLanguageIdByLanguageName(languageIdOrName) ?? languageIdOrName;
    }

    /// <summary>
    /// The token colors: Monaco's light or dark theme (vs, vs-dark) after the editor's appearance, with the editor's own
    /// foreground and background as the default token colors, so that untokenized text matches the rest of Arc.
    /// </summary>
    private void UpdateTokenTheme()
    {
        var isDark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Foreground is ISolidColorBrush { Color: var foreground })
        {
            colors["editor.foreground"] = ToHex(foreground);
        }

        if (Background is ISolidColorBrush { Color: var background } && background.A == 255)
        {
            colors["editor.background"] = ToHex(background);
        }

        var themes = LanguageServices.ThemeService;
        themes.DefineTheme(TokenThemeName, new StandaloneThemeData(
            isDark ? StandaloneTheme.VsDarkThemeName : StandaloneTheme.VsLightThemeName,
            Inherit: true,
            Rules: [],
            Colors: colors));
        themes.SetTheme(TokenThemeName);
    }

    private const string TokenThemeName = "arc";

    private static string ToHex(Color color) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"#{color.R:x2}{color.G:x2}{color.B:x2}");

    private TextModel CreateModel(string text, string languageId)
    {
        LanguageIdCodec.Register(languageId);
        // the indent size follows the tab size (Monaco's editor option indentSize: 'tabSize')
        var options = TextModel.DefaultCreationOptions with { TabSize = TabSize, IndentSize = null, InsertSpaces = InsertSpaces };
        return new TextModel(
            text, languageId, options, new UndoRedoService(new NoDialogs(), new NoNotifications()), null, LanguageIdCodec,
            TokenizationRegistry, IdleScheduler);
    }

    private void AttachModel(TextModel model)
    {
        model.DidChangeContent += OnModelContentChanged;
        model.DidChangeLanguage += OnModelLanguageChanged;
    }

    private void OnModelContentChanged(ModelContentChangedEvent e)
    {
        _syncingText = true;
        try
        {
            SetCurrentValue(TextProperty, Model.GetValue());
        }
        finally
        {
            _syncingText = false;
        }

        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Creates the view model for the view's layout (called by the view when it knows its size and font).</summary>
    internal EditorViewModel CreateViewModel(ViewModelOptions options, Func<Action, IDisposable> scheduleAtNextAnimationFrame)
    {
        DisposeViewModel();
        Configuration = new ViewModelConfiguration(options);
        var lineBreaks = new MonospaceLineBreaksComputerFactory(
            WrappingOptions.WordWrapBreakBeforeCharacters,
            WrappingOptions.WordWrapBreakAfterCharacters);
        // CodeEditorWidget._attachModel: the model learns the view's visible lines through the attached view
        _attachedView = Model.OnBeforeAttached();
        // the bracket pairs part brings the colorized bracket decorations (upstream: created with the model)
        Model.GetBracketPairs(LanguageConfigurationService);
        ViewModel = new EditorViewModel(
            _editorId,
            Configuration,
            Model,
            lineBreaks,
            lineBreaks,
            scheduleAtNextAnimationFrame,
            LanguageConfigurationService,
            _attachedView);
        return ViewModel;
    }

    /// <summary>
    /// Called by the view once a new view model is set up: relays its events, puts the view zones into it and creates
    /// the contributions (the first time).
    /// </summary>
    internal void OnViewModelReady()
    {
        if (ViewModel is not { } viewModel)
        {
            return;
        }

        RelayViewModelEvents(viewModel);
        OnViewModelCreatedForWidgets(viewModel);
        if (VisualRoot is not null)
        {
            InitializeContributions();
        }
    }

    internal void DisposeViewModel()
    {
        UnrelayViewModelEvents();
        ViewModel?.Dispose();
        ViewModel = null;
        Configuration = null;
        if (_attachedView is { } attachedView)
        {
            _attachedView = null;
            Model.OnBeforeDetached(attachedView);
        }
    }

    private static IDisposable PostIdle(Action action)
    {
        var operation = Dispatcher.UIThread.InvokeAsync(action, DispatcherPriority.Background);
        return new CancelOperation(operation);
    }

    private static IDisposable PostAfter(TimeSpan delay, Action action)
    {
        var timer = new DispatcherTimer(delay, DispatcherPriority.Background, (sender, _) =>
        {
            ((DispatcherTimer)sender!).Stop();
            action();
        });
        timer.Start();
        return new StopTimer(timer);
    }

    private sealed class CancelOperation(DispatcherOperation operation) : IDisposable
    {
        public void Dispose() => operation.Abort();
    }

    private sealed class StopTimer(DispatcherTimer timer) : IDisposable
    {
        public void Dispose() => timer.Stop();
    }

    private sealed class NoDialogs : IUndoRedoDialogService
    {
        public ValueTask<UndoChoice> PromptAsync(UndoRedoPromptOptions options) => ValueTask.FromResult(UndoChoice.All);

        public ValueTask<bool> ConfirmAsync(UndoRedoConfirmOptions options) => ValueTask.FromResult(true);
    }

    private sealed class NoNotifications : IUndoRedoNotificationService
    {
        public void Warn(string message)
        {
        }

        public void Error(Exception error)
        {
        }
    }
}
