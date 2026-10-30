using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A "get started in 30 seconds" card for SDK onboarding (Arc Install snippet, design reference): framework tabs, three
/// numbered steps (Install, Add your key, Ship) and highlighted snippets that take a developer from install to the first
/// event. Step 1 offers npm, pnpm, yarn and bun when the framework installs packages; every snippet has a copy button;
/// Generate key wipes a fresh key into the placeholder in place. Snippets, file names and step lines crossfade in space
/// that is reserved for the longest of them, so switching frameworks never moves anything.
/// <para>
/// Give it your <see cref="Frameworks"/> (write <c>{{key}}</c> where the key goes) and a <see cref="KeyGenerator"/> that
/// creates a real key, or pass <see cref="ApiKey"/> for a reader who already has one. With no frameworks it shows the SDK
/// of its <see cref="Simulation"/>, if any, and otherwise the three steps with empty snippets; with no generator it
/// invents keys (nothing is sent, and step 2 says so).
/// </para>
/// </summary>
[TemplatePart(VersionPart, typeof(Control))]
[TemplatePart(FrameworksPart, typeof(InstallSnippetTabs))]
[TemplatePart(StepsPart, typeof(Panel))]
[TemplatePart(InstallStepPart, typeof(InstallSnippetStepView))]
[TemplatePart(KeyStepPart, typeof(InstallSnippetStepView))]
[TemplatePart(CodeStepPart, typeof(InstallSnippetStepView))]
[TemplatePart(PackageManagersPart, typeof(InstallSnippetTabs))]
[TemplatePart(GeneratePart, typeof(AvaloniaButton))]
[TemplatePart(KeyStatusPart, typeof(InstallSnippetKeyStatus))]
[TemplatePart(LivePart, typeof(ExplainerAnnouncer))]
[PseudoClasses(NarrowPseudoClass, CompactPseudoClass, GeneratingPseudoClass, FailedPseudoClass, ReadyPseudoClass)]
public partial class InstallSnippet : TemplatedControl
{
    internal const string VersionPart = "PART_Version";
    internal const string FrameworksPart = "PART_Frameworks";
    internal const string StepsPart = "PART_Steps";
    internal const string InstallStepPart = "PART_InstallStep";
    internal const string KeyStepPart = "PART_KeyStep";
    internal const string CodeStepPart = "PART_CodeStep";
    internal const string PackageManagersPart = "PART_PackageManagers";
    internal const string GeneratePart = "PART_Generate";
    internal const string KeyStatusPart = "PART_KeyStatus";
    internal const string LivePart = "PART_Live";

    internal const string NarrowPseudoClass = ":narrow";
    internal const string CompactPseudoClass = ":compact";
    internal const string GeneratingPseudoClass = ":generating";
    internal const string FailedPseudoClass = ":failed";
    internal const string ReadyPseudoClass = ":ready";

    /// <summary>The card's title.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<InstallSnippet, string?>(nameof(Title), "Get started in 30 seconds");

    /// <summary>One line under the title.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<InstallSnippet, string?>(nameof(Description), "Three steps to your first event.");

    /// <summary>A muted label at the top right, such as the SDK version; null (the default) or empty hides it.</summary>
    public static readonly StyledProperty<string?> VersionProperty =
        AvaloniaProperty.Register<InstallSnippet, string?>(nameof(Version));

    /// <summary>The selected framework's id (two-way); null selects the first.</summary>
    public static readonly StyledProperty<string?> SelectedFrameworkProperty =
        AvaloniaProperty.Register<InstallSnippet, string?>(nameof(SelectedFramework), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The package manager of step 1 (two-way). Persist it to remember the reader's choice across pages.</summary>
    public static readonly StyledProperty<InstallSnippetPackageManager> PackageManagerProperty =
        AvaloniaProperty.Register<InstallSnippet, InstallSnippetPackageManager>(nameof(PackageManager),
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// The reader's own key. When set, the snippets show it and the Generate key button is hidden, for signed-in readers
    /// who already have a key.
    /// </summary>
    public static readonly StyledProperty<string?> ApiKeyProperty =
        AvaloniaProperty.Register<InstallSnippet, string?>(nameof(ApiKey));

    /// <summary>Shown, tinted, wherever <c>{{key}}</c> appears until a key exists; copies as written. "YOUR_API_KEY" by default.</summary>
    public static readonly StyledProperty<string> KeyPlaceholderProperty =
        AvaloniaProperty.Register<InstallSnippet, string>(nameof(KeyPlaceholder), "YOUR_API_KEY");

    /// <summary>
    /// Creates a real key. Return the key and it wipes into the snippets; throw to show the error with Try again. The token
    /// is cancelled when the card leaves the window or a new key is requested. Null invents a demo key (nothing is sent).
    /// </summary>
    public static readonly StyledProperty<Func<CancellationToken, Task<string>>?> KeyGeneratorProperty =
        AvaloniaProperty.Register<InstallSnippet, Func<CancellationToken, Task<string>>?>(nameof(KeyGenerator));

    /// <summary>
    /// A simulated SDK to show while <see cref="Frameworks"/> is empty: its framework tabs and the prefix of the keys the
    /// simulated Generate key invents. Null (the default) shows the three steps with empty snippets. Give each card its own
    /// simulation.
    /// </summary>
    public static readonly StyledProperty<InstallSnippetSimulation?> SimulationProperty =
        AvaloniaProperty.Register<InstallSnippet, InstallSnippetSimulation?>(nameof(Simulation));

    /// <summary>Below this width the padding tightens and the package manager tabs show marks only.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<InstallSnippet, double>(nameof(NarrowWidth), 460);

    /// <summary>Below this width the framework tabs drop their marks and the version label hides.</summary>
    public static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<InstallSnippet, double>(nameof(CompactWidth), 380);

    /// <summary>Every word the card shows or says.</summary>
    public static readonly StyledProperty<InstallSnippetLabels> LabelsProperty =
        AvaloniaProperty.Register<InstallSnippet, InstallSnippetLabels>(nameof(Labels), InstallSnippetLabels.Default);

    /// <summary>The key in the snippets: <see cref="ApiKey"/>, else the last generated key, else null (the placeholder shows).</summary>
    public static readonly DirectProperty<InstallSnippet, string?> CurrentKeyProperty =
        AvaloniaProperty.RegisterDirect<InstallSnippet, string?>(nameof(CurrentKey), o => o.CurrentKey);

    /// <summary>Where the Generate key button is: idle, generating, ready or failed.</summary>
    public static readonly DirectProperty<InstallSnippet, InstallSnippetKeyState> KeyStateProperty =
        AvaloniaProperty.RegisterDirect<InstallSnippet, InstallSnippetKeyState>(nameof(KeyState), o => o.KeyState);

    /// <summary>Raised when the reader picks another framework tab. Bubbles.</summary>
    public static readonly RoutedEvent<InstallSnippetFrameworkChangedEventArgs> FrameworkChangedEvent =
        RoutedEvent.Register<InstallSnippet, InstallSnippetFrameworkChangedEventArgs>(nameof(FrameworkChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the reader picks another package manager. Bubbles.</summary>
    public static readonly RoutedEvent<InstallSnippetPackageManagerChangedEventArgs> PackageManagerChangedEvent =
        RoutedEvent.Register<InstallSnippet, InstallSnippetPackageManagerChangedEventArgs>(nameof(PackageManagerChanged),
            RoutingStrategies.Bubble);

    /// <summary>Raised with each new key once it is in the snippets. Bubbles.</summary>
    public static readonly RoutedEvent<InstallSnippetKeyGeneratedEventArgs> KeyGeneratedEvent =
        RoutedEvent.Register<InstallSnippet, InstallSnippetKeyGeneratedEventArgs>(nameof(KeyGenerated), RoutingStrategies.Bubble);

    /// <summary>Raised after a copy with the step and the exact clipboard text (onboarding analytics). Bubbles.</summary>
    public static readonly RoutedEvent<InstallSnippetCopiedEventArgs> CopiedEvent =
        RoutedEvent.Register<InstallSnippet, InstallSnippetCopiedEventArgs>(nameof(Copied), RoutingStrategies.Bubble);

    private static readonly InstallSnippetStep[] s_steps = [InstallSnippetStep.Install, InstallSnippetStep.Key, InstallSnippetStep.Code];

    private readonly AvaloniaList<InstallSnippetFramework> _frameworks = [];
    private readonly bool[] _done = new bool[3];
    private readonly InstallSnippetStepView?[] _stepViews = new InstallSnippetStepView?[3];
    private Control? _version;
    private InstallSnippetTabs? _frameworkTabs;
    private Panel? _stepsPanel;
    private InstallSnippetTabs? _packageManagers;
    private AvaloniaButton? _generate;
    private InstallSnippetKeyStatus? _keyStatus;
    private ExplainerAnnouncer? _live;
    private string? _currentKey;
    private InstallSnippetKeyState _keyState;
    private bool _narrow;
    private bool _compact;

    /// <summary>Creates the card.</summary>
    public InstallSnippet()
    {
        _frameworks.CollectionChanged += OnFrameworksChanged;
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="VersionProperty"/>
    public string? Version { get => GetValue(VersionProperty); set => SetValue(VersionProperty, value); }

    /// <inheritdoc cref="SelectedFrameworkProperty"/>
    public string? SelectedFramework { get => GetValue(SelectedFrameworkProperty); set => SetValue(SelectedFrameworkProperty, value); }

    /// <inheritdoc cref="PackageManagerProperty"/>
    public InstallSnippetPackageManager PackageManager
    {
        get => GetValue(PackageManagerProperty);
        set => SetValue(PackageManagerProperty, value);
    }

    /// <inheritdoc cref="ApiKeyProperty"/>
    public string? ApiKey { get => GetValue(ApiKeyProperty); set => SetValue(ApiKeyProperty, value); }

    /// <inheritdoc cref="KeyPlaceholderProperty"/>
    public string KeyPlaceholder { get => GetValue(KeyPlaceholderProperty); set => SetValue(KeyPlaceholderProperty, value); }

    /// <inheritdoc cref="KeyGeneratorProperty"/>
    public Func<CancellationToken, Task<string>>? KeyGenerator
    {
        get => GetValue(KeyGeneratorProperty);
        set => SetValue(KeyGeneratorProperty, value);
    }

    /// <inheritdoc cref="NarrowWidthProperty"/>
    public double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }

    /// <inheritdoc cref="CompactWidthProperty"/>
    public double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public InstallSnippetSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public InstallSnippetLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="CurrentKeyProperty"/>
    public string? CurrentKey { get => _currentKey; private set => SetAndRaise(CurrentKeyProperty, ref _currentKey, value); }

    /// <inheritdoc cref="KeyStateProperty"/>
    public InstallSnippetKeyState KeyState { get => _keyState; private set => SetAndRaise(KeyStateProperty, ref _keyState, value); }

    /// <summary>The framework tabs, in order. Empty shows the <see cref="Simulation"/>'s, if any.</summary>
    [Content]
    public AvaloniaList<InstallSnippetFramework> Frameworks => _frameworks;

    /// <inheritdoc cref="FrameworkChangedEvent"/>
    public event EventHandler<InstallSnippetFrameworkChangedEventArgs>? FrameworkChanged
    {
        add => AddHandler(FrameworkChangedEvent, value);
        remove => RemoveHandler(FrameworkChangedEvent, value);
    }

    /// <inheritdoc cref="PackageManagerChangedEvent"/>
    public event EventHandler<InstallSnippetPackageManagerChangedEventArgs>? PackageManagerChanged
    {
        add => AddHandler(PackageManagerChangedEvent, value);
        remove => RemoveHandler(PackageManagerChangedEvent, value);
    }

    /// <inheritdoc cref="KeyGeneratedEvent"/>
    public event EventHandler<InstallSnippetKeyGeneratedEventArgs>? KeyGenerated
    {
        add => AddHandler(KeyGeneratedEvent, value);
        remove => RemoveHandler(KeyGeneratedEvent, value);
    }

    /// <inheritdoc cref="CopiedEvent"/>
    public event EventHandler<InstallSnippetCopiedEventArgs>? Copied
    {
        add => AddHandler(CopiedEvent, value);
        remove => RemoveHandler(CopiedEvent, value);
    }

    /// <summary>Whether a step is done: its snippet was copied (for step 2, while a key was in it).</summary>
    public bool IsStepDone(InstallSnippetStep step) => _done[(int)step];

    /// <summary>Clears the done marks of every step.</summary>
    public void ResetProgress()
    {
        Array.Clear(_done);
        Refresh(animateKey: false);
    }

    /// <summary>The frameworks on screen: the app's, the simulation's, or none.</summary>
    internal IReadOnlyList<InstallSnippetFramework> DisplayedFrameworks => _frameworks.Count > 0 ? _frameworks : Simulation?.Frameworks ?? [];

    /// <summary>The simulation in use: none once the app gives its own frameworks.</summary>
    internal InstallSnippetSimulation? ActiveSimulation => _frameworks.Count > 0 ? null : Simulation;

    /// <summary>The framework on screen.</summary>
    internal InstallSnippetFramework? CurrentFramework
    {
        get
        {
            var all = DisplayedFrameworks;
            if (all.Count == 0)
            {
                return null;
            }
            var id = SelectedFramework;
            return all.FirstOrDefault(f => f.TabId == id) ?? all[0];
        }
    }

    /// <summary>A step's view.</summary>
    internal InstallSnippetStepView? StepView(InstallSnippetStep step) => _stepViews[(int)step];

    internal InstallSnippetTabs? FrameworkTabs => _frameworkTabs;

    internal InstallSnippetTabs? PackageManagerTabs => _packageManagers;

    internal AvaloniaButton? GenerateButton => _generate;

    internal InstallSnippetKeyStatus? KeyStatus => _keyStatus;

    /// <summary>The key status line's text.</summary>
    internal string? KeyStatusText => _keyStatus?.Text;

    /// <summary>The last announcement of the live region.</summary>
    internal string? Announcement => _live?.Announcement;

    internal bool IsNarrow => _narrow;

    internal bool IsCompact => _compact;

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        _version = e.NameScope.Find<Control>(VersionPart);
        _frameworkTabs = e.NameScope.Find<InstallSnippetTabs>(FrameworksPart);
        _stepsPanel = e.NameScope.Find<Panel>(StepsPart);
        _stepViews[0] = e.NameScope.Find<InstallSnippetStepView>(InstallStepPart);
        _stepViews[1] = e.NameScope.Find<InstallSnippetStepView>(KeyStepPart);
        _stepViews[2] = e.NameScope.Find<InstallSnippetStepView>(CodeStepPart);
        _packageManagers = e.NameScope.Find<InstallSnippetTabs>(PackageManagersPart);
        _generate = e.NameScope.Find<AvaloniaButton>(GeneratePart);
        _keyStatus = e.NameScope.Find<InstallSnippetKeyStatus>(KeyStatusPart);
        _live = e.NameScope.Find<ExplainerAnnouncer>(LivePart);
        if (_frameworkTabs is not null)
        {
            _frameworkTabs.Picked += OnFrameworkPicked;
        }
        if (_packageManagers is not null)
        {
            _packageManagers.Picked += OnPackageManagerPicked;
        }
        if (_generate is not null)
        {
            _generate.Click += OnGenerateClick;
        }
        for (var i = 0; i < _stepViews.Length; i++)
        {
            if (_stepViews[i] is { } view)
            {
                view.Copied += OnStepCopied;
            }
        }
        UpdateKey(animate: false);
        UpdateBreakpoints(Bounds.Width);
        Refresh(animateKey: false);
    }

    private void Unhook()
    {
        if (_frameworkTabs is not null)
        {
            _frameworkTabs.Picked -= OnFrameworkPicked;
        }
        if (_packageManagers is not null)
        {
            _packageManagers.Picked -= OnPackageManagerPicked;
        }
        if (_generate is not null)
        {
            _generate.Click -= OnGenerateClick;
        }
        foreach (var view in _stepViews)
        {
            if (view is not null)
            {
                view.Copied -= OnStepCopied;
            }
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            UpdateBreakpoints(change.GetNewValue<Rect>().Width);
        }
        else if (change.Property == NarrowWidthProperty || change.Property == CompactWidthProperty)
        {
            UpdateBreakpoints(Bounds.Width);
        }
        else if (change.Property == ApiKeyProperty)
        {
            if (!string.IsNullOrEmpty(ApiKey))
            {
                CancelGeneration();
            }
            UpdateKey(animate: false);
            Refresh(animateKey: false);
        }
        else if (change.Property == KeyGeneratorProperty || change.Property == SimulationProperty)
        {
            Refresh(animateKey: false);
        }
        else if (change.Property == SelectedFrameworkProperty || change.Property == PackageManagerProperty
                 || change.Property == LabelsProperty || change.Property == KeyPlaceholderProperty
                 || change.Property == VersionProperty)
        {
            Refresh(animateKey: false);
        }
    }

    private void OnFrameworksChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (InstallSnippetFramework framework in e.OldItems)
            {
                framework.Changed -= OnFrameworkChanged;
            }
        }
        if (e.NewItems is not null)
        {
            foreach (InstallSnippetFramework framework in e.NewItems)
            {
                framework.Changed += OnFrameworkChanged;
            }
        }
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var framework in _frameworks)
            {
                framework.Changed -= OnFrameworkChanged;
                framework.Changed += OnFrameworkChanged;
            }
        }
        Refresh(animateKey: false);
    }

    private void OnFrameworkChanged(object? sender, EventArgs e) => Refresh(animateKey: false);

    private void OnFrameworkPicked(object? sender, string id)
    {
        SetCurrentValue(SelectedFrameworkProperty, id);
        var framework = DisplayedFrameworks.FirstOrDefault(f => f.TabId == id);
        Announce(Labels.FrameworkShown(framework?.Label ?? id));
        RaiseEvent(new InstallSnippetFrameworkChangedEventArgs(FrameworkChangedEvent, id));
    }

    private void OnPackageManagerPicked(object? sender, string id)
    {
        if (!Enum.TryParse<InstallSnippetPackageManager>(id, out var manager))
        {
            return;
        }
        SetCurrentValue(PackageManagerProperty, manager);
        Announce(Labels.PackageManagerShown(Labels.NameOf(manager)));
        RaiseEvent(new InstallSnippetPackageManagerChangedEventArgs(PackageManagerChangedEvent, manager));
    }

    private void OnStepCopied(object? sender, string value)
    {
        var index = Array.IndexOf(_stepViews, sender as InstallSnippetStepView);
        if (index < 0)
        {
            return;
        }
        var step = s_steps[index];
        var template = SnippetOf(CurrentFramework, step).Code;
        if (step != InstallSnippetStep.Key || CurrentKey is not null || !InstallSnippetText.HasKey(template))
        {
            _done[index] = true;
            Refresh(animateKey: false);
        }
        RaiseEvent(new InstallSnippetCopiedEventArgs(CopiedEvent, step, value));
    }

    private void UpdateBreakpoints(double width)
    {
        if (width <= 0)
        {
            return;
        }
        var narrow = width < NarrowWidth;
        var compact = width < CompactWidth;
        if (narrow == _narrow && compact == _compact)
        {
            return;
        }
        _narrow = narrow;
        _compact = compact;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        PseudoClasses.Set(CompactPseudoClass, compact);
        ApplyBreakpoints();
    }

    private void ApplyBreakpoints()
    {
        if (_frameworkTabs is not null)
        {
            _frameworkTabs.ShowsMarks = !_compact;
        }
        if (_packageManagers is not null)
        {
            _packageManagers.ShowsLabels = !_narrow;
        }
        if (_version is not null)
        {
            _version.IsVisible = !_compact && !string.IsNullOrEmpty(Version);
        }
    }

    /// <summary>A step's snippet for a framework: file name, code, language, line and whether it is a terminal command.</summary>
    internal (string FileName, string Code, string Language, string Description, bool IsCommand) SnippetOf(
        InstallSnippetFramework? framework, InstallSnippetStep step)
    {
        var labels = Labels;
        if (framework is null)
        {
            return (string.Empty, string.Empty, "bash", labels.DescriptionOf(step), false);
        }
        if (step == InstallSnippetStep.Install && framework.UsesPackages)
        {
            return (labels.Terminal, InstallSnippetText.InstallCommand(PackageManager, framework.Packages), "bash",
                framework.PackagesDescription ?? labels.InstallDescription, true);
        }
        var file = step switch
        {
            InstallSnippetStep.Install => framework.Install,
            InstallSnippetStep.Key => framework.Key,
            _ => framework.Code,
        };
        var name = file?.FileName ?? (step == InstallSnippetStep.Install ? labels.Terminal : string.Empty);
        var language = InstallSnippetText.LanguageOf(name, file?.Language);
        var command = name.Equals(labels.Terminal, StringComparison.OrdinalIgnoreCase)
                      || name.Equals("Terminal", StringComparison.OrdinalIgnoreCase);
        return (name, InstallSnippetText.Normalize(file?.Code), language, file?.Description ?? labels.DescriptionOf(step), command);
    }

    /// <summary>Brings every part in line with the state; a part that already shows the value is left alone.</summary>
    private void Refresh(bool animateKey)
    {
        if (_frameworkTabs is null)
        {
            return;
        }
        var labels = Labels;
        var all = DisplayedFrameworks;
        var framework = CurrentFramework;
        var key = CurrentKey;
        var placeholder = KeyPlaceholder;

        _frameworkTabs.SetItems(
            all.Select(f => new InstallSnippetTabItem(f.TabId, f.Label ?? f.TabId, f.Mark ?? InstallSnippetBuiltIns.MarkOf(f.Id))).ToList(),
            framework?.TabId);
        AutomationProperties.SetName(_frameworkTabs, labels.Frameworks);
        if (_stepsPanel is not null)
        {
            AutomationProperties.SetName(_stepsPanel, labels.Steps);
        }
        ApplyBreakpoints();

        if (_packageManagers is not null)
        {
            _packageManagers.IsVisible = framework?.UsesPackages == true;
            _packageManagers.SetItems(
                Enum.GetValues<InstallSnippetPackageManager>()
                    .Select(m => new InstallSnippetTabItem(m.ToString(), labels.NameOf(m), null, labels.NameOf(m)[..1]))
                    .ToList(),
                PackageManager.ToString());
            AutomationProperties.SetName(_packageManagers, labels.PackageManagers);
        }

        for (var i = 0; i < s_steps.Length; i++)
        {
            var view = _stepViews[i];
            if (view is null)
            {
                continue;
            }
            var step = s_steps[i];
            var snippet = SnippetOf(framework, step);
            var title = labels.TitleOf(step);
            view.Number = i + 1;
            view.Title = title;
            view.IsDone = _done[i];
            view.IsLast = i == s_steps.Length - 1;
            view.SetSpokenTitle(_done[i] ? labels.DoneTitle(title) : title);
            AutomationProperties.SetName(view, labels.StepName(i + 1, s_steps.Length));
            view.SetDescription(snippet.Description, all.Select(f => SnippetOf(f, step).Description).Distinct().ToList());
            view.SetFile(snippet.FileName, snippet.IsCommand);
            var minLines = all.Count == 0 ? 1 : all.Max(f => InstallSnippetText.LineCount(SnippetOf(f, step).Code));
            var code = view.CodeView;
            if (code is null || code.Template != snippet.Code || code.Language != snippet.Language || code.Placeholder != placeholder)
            {
                code = CreateCodeView(snippet.Code, snippet.Language, placeholder, key);
                code.MinLines = minLines;
                view.SetCode(code);
            }
            else
            {
                code.MinLines = minLines;
                code.SetKey(key, animateKey);
            }
            view.SetCopy(InstallSnippetText.Fill(snippet.Code, key ?? placeholder), labels.CopyOf(step));
        }

        RefreshKeyControls();
    }

    private InstallSnippetCodeView CreateCodeView(string code, string language, string placeholder, string? key)
    {
        var view = new InstallSnippetCodeView(code, language, placeholder, key);
        ArcTokens.Bind(view, InstallSnippetCodeView.SlotForegroundProperty, ArcDesignTokenKind.AccentStrong);
        ArcTokens.Bind(view, InstallSnippetCodeView.SlotBackgroundProperty, ArcDesignTokenKind.AccentSubtle);
        ArcTokens.Bind(view, InstallSnippetCodeView.PageBrushProperty, ArcDesignTokenKind.SurfaceMuted);
        return view;
    }

    private void Announce(string text)
    {
        if (_live is not null && text != _live.Announcement)
        {
            _live.Announce(text);
        }
    }
}
