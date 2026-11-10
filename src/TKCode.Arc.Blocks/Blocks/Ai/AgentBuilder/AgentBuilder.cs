using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A page for setting up an AI agent with a test chat beside it. The settings card has the agent's name and
/// description, its instructions with a character and token count, the model, the tools it may use (each with a
/// switch) and the knowledge sources it searches (add a link or upload files; each source shows its indexing). A save
/// bar keeps the draft apart from what is saved, and Publish moves the saved settings to what people use, with a
/// status badge (Draft, Published, Unpublished changes). The test chat answers with the settings it started with;
/// when they change it says so and Restart starts over with the new ones.
/// <para>
/// Without your own data the builder shows an invented support agent (<see cref="ShowsSampleData"/>). Connect
/// <see cref="Responder"/> to your agent runtime for real test replies, <see cref="SaveHandler"/> and
/// <see cref="PublishHandler"/> to your backend, and <see cref="IndexHandler"/> to your indexing; each is simulated
/// when unset.
/// </para>
/// </summary>
/// <example><code>
/// &lt;arc:AgentBuilder AgentName="{Binding Name}" Instructions="{Binding Prompt}" SelectedModelId="{Binding ModelId}" /&gt;
/// </code></example>
[TemplatePart(LayoutPart, typeof(Grid))]
[TemplatePart(SettingsCardPart, typeof(Border))]
[TemplatePart(ChatCardPart, typeof(Border))]
[TemplatePart(NameInputPart, typeof(Input))]
[TemplatePart(DescriptionInputPart, typeof(Input))]
[TemplatePart(InstructionsPart, typeof(Textarea))]
[TemplatePart(ModelPart, typeof(ModelPicker))]
[TemplatePart(ToolListPart, typeof(StackPanel))]
[TemplatePart(SourceListPart, typeof(StackPanel))]
[TemplatePart(LinkInputPart, typeof(Input))]
[TemplatePart(AddLinkPart, typeof(Button))]
[TemplatePart(UploadPart, typeof(Button))]
[TemplatePart(SavePart, typeof(Button))]
[TemplatePart(DiscardPart, typeof(Button))]
[TemplatePart(PublishPart, typeof(Button))]
[TemplatePart(ChatPart, typeof(AiChat))]
[TemplatePart(RestartPart, typeof(Button))]
[PseudoClasses(StackedPseudoClass, CompactPseudoClass, DirtyPseudoClass, SavingPseudoClass, StalePseudoClass,
    Tone1PseudoClass, Tone2PseudoClass, Tone3PseudoClass, Tone4PseudoClass)]
public partial class AgentBuilder : TemplatedControl
{
    public const string LayoutPart = "PART_Layout";
    public const string SettingsCardPart = "PART_SettingsCard";
    public const string ChatCardPart = "PART_ChatCard";
    public const string AvatarPart = "PART_Avatar";
    public const string InitialPart = "PART_Initial";
    public const string TitlePart = "PART_Title";
    public const string SubtitlePart = "PART_Subtitle";
    public const string StatusPart = "PART_Status";
    public const string PublishPart = "PART_Publish";
    public const string IdentityTitlePart = "PART_IdentityTitle";
    public const string NameInputPart = "PART_NameInput";
    public const string DescriptionInputPart = "PART_DescriptionInput";
    public const string InstructionsTitlePart = "PART_InstructionsTitle";
    public const string InstructionsHintPart = "PART_InstructionsHint";
    public const string InstructionsPart = "PART_Instructions";
    public const string InstructionsCountPart = "PART_InstructionsCount";
    public const string ModelTitlePart = "PART_ModelTitle";
    public const string ModelHintPart = "PART_ModelHint";
    public const string ModelPart = "PART_Model";
    public const string ToolsTitlePart = "PART_ToolsTitle";
    public const string ToolsCountPart = "PART_ToolsCount";
    public const string ToolsHintPart = "PART_ToolsHint";
    public const string ToolListPart = "PART_ToolList";
    public const string KnowledgeTitlePart = "PART_KnowledgeTitle";
    public const string KnowledgeCountPart = "PART_KnowledgeCount";
    public const string KnowledgeHintPart = "PART_KnowledgeHint";
    public const string SourceListPart = "PART_SourceList";
    public const string NoSourcesPart = "PART_NoSources";
    public const string LinkInputPart = "PART_LinkInput";
    public const string AddLinkPart = "PART_AddLink";
    public const string UploadPart = "PART_Upload";
    public const string SaveStatusPart = "PART_SaveStatus";
    public const string SavePart = "PART_Save";
    public const string DiscardPart = "PART_Discard";
    public const string ChatPart = "PART_Chat";
    public const string StaleHostPart = "PART_StaleHost";
    public const string StaleTextPart = "PART_StaleText";
    public const string RestartPart = "PART_Restart";

    /// <summary>Set while the test chat sits under the settings (the builder is narrower than its stack width, 760).</summary>
    public const string StackedPseudoClass = ":stacked";

    /// <summary>Set while the builder is narrower than its compact width (520): the section paddings tighten.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the draft differs from the saved settings.</summary>
    public const string DirtyPseudoClass = ":dirty";

    /// <summary>Set while a save or a publish runs.</summary>
    public const string SavingPseudoClass = ":saving";

    /// <summary>Set while the test chat runs on older settings than the draft.</summary>
    public const string StalePseudoClass = ":stale";

    /// <summary>The avatar's color, picked from the name: the first series color.</summary>
    public const string Tone1PseudoClass = ":tone-1";

    /// <summary>The avatar's color, picked from the name: the second series color.</summary>
    public const string Tone2PseudoClass = ":tone-2";

    /// <summary>The avatar's color, picked from the name: the third series color.</summary>
    public const string Tone3PseudoClass = ":tone-3";

    /// <summary>The avatar's color, picked from the name: the fourth series color.</summary>
    public const string Tone4PseudoClass = ":tone-4";

    #region Properties

    /// <summary>The agent's name (two-way: the name field edits it).</summary>
    public static readonly StyledProperty<string> AgentNameProperty =
        AvaloniaProperty.Register<AgentBuilder, string>(nameof(AgentName), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>One line on what the agent helps with (two-way: the description field edits it).</summary>
    public static readonly StyledProperty<string> AgentDescriptionProperty =
        AvaloniaProperty.Register<AgentBuilder, string>(nameof(AgentDescription), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The instructions, the agent's system prompt (two-way: the instructions field edits it).</summary>
    public static readonly StyledProperty<string> InstructionsProperty =
        AvaloniaProperty.Register<AgentBuilder, string>(nameof(Instructions), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the chosen model in <see cref="Models"/> (two-way: the model pill and the test chat's model button edit it).</summary>
    public static readonly StyledProperty<string?> SelectedModelIdProperty =
        AvaloniaProperty.Register<AgentBuilder, string?>(nameof(SelectedModelId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The longest the instructions may be (8,000 characters); longer instructions cannot be saved.</summary>
    public static readonly StyledProperty<int> InstructionsMaxLengthProperty =
        AvaloniaProperty.Register<AgentBuilder, int>(nameof(InstructionsMaxLength), 8_000);

    /// <summary>
    /// What is saved (two-way). Null when the builder first shows takes the settings on screen as saved. Discard returns to
    /// it; the save bar and <see cref="IsDirty"/> compare the draft with it.
    /// </summary>
    public static readonly StyledProperty<AgentBuilderConfig?> SavedConfigProperty =
        AvaloniaProperty.Register<AgentBuilder, AgentBuilderConfig?>(nameof(SavedConfig), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>What people use (two-way); null means the agent was never published (the badge says Draft).</summary>
    public static readonly StyledProperty<AgentBuilderConfig?> PublishedConfigProperty =
        AvaloniaProperty.Register<AgentBuilder, AgentBuilderConfig?>(nameof(PublishedConfig), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Answers the test chat; null uses <see cref="AgentBuilderSimulatedResponder"/>, which calls no model.</summary>
    public static readonly StyledProperty<IAgentBuilderResponder?> ResponderProperty =
        AvaloniaProperty.Register<AgentBuilder, IAgentBuilderResponder?>(nameof(Responder));

    /// <summary>Fills an empty builder with the invented support agent of <see cref="AgentBuilderSamples"/> when it first shows (default true).</summary>
    public static readonly StyledProperty<bool> ShowsSampleDataProperty =
        AvaloniaProperty.Register<AgentBuilder, bool>(nameof(ShowsSampleData), true);

    /// <summary>Culture of the counts and sizes (default "en-US").</summary>
    public static readonly StyledProperty<string> CultureNameProperty =
        AvaloniaProperty.Register<AgentBuilder, string>(nameof(CultureName), "en-US");

    /// <summary>Every word the builder shows or reads out (see <see cref="AgentBuilderLabels"/>).</summary>
    public static readonly StyledProperty<AgentBuilderLabels> LabelsProperty =
        AvaloniaProperty.Register<AgentBuilder, AgentBuilderLabels>(nameof(Labels), AgentBuilderLabels.Default);

    /// <summary>Whether the draft differs from <see cref="SavedConfig"/>.</summary>
    public static readonly DirectProperty<AgentBuilder, bool> IsDirtyProperty =
        AvaloniaProperty.RegisterDirect<AgentBuilder, bool>(nameof(IsDirty), o => o.IsDirty);

    /// <summary>Whether a save or a publish is running.</summary>
    public static readonly DirectProperty<AgentBuilder, bool> IsSavingProperty =
        AvaloniaProperty.RegisterDirect<AgentBuilder, bool>(nameof(IsSaving), o => o.IsSaving);

    /// <summary>Draft, Published or Unpublished changes, from <see cref="SavedConfig"/>, <see cref="PublishedConfig"/> and the draft.</summary>
    public static readonly DirectProperty<AgentBuilder, AgentBuilderPublishState> PublishStateProperty =
        AvaloniaProperty.RegisterDirect<AgentBuilder, AgentBuilderPublishState>(nameof(PublishState), o => o.PublishState);

    /// <summary>The draft: the settings as they are on screen.</summary>
    public static readonly DirectProperty<AgentBuilder, AgentBuilderConfig> ConfigProperty =
        AvaloniaProperty.RegisterDirect<AgentBuilder, AgentBuilderConfig>(nameof(Config), o => o.Config);

    /// <summary>The settings the open test chat started with; null before its first message.</summary>
    public static readonly DirectProperty<AgentBuilder, AgentBuilderConfig?> TestConfigProperty =
        AvaloniaProperty.RegisterDirect<AgentBuilder, AgentBuilderConfig?>(nameof(TestConfig), o => o.TestConfig);

    /// <summary>Whether the draft changed after the open test chat started (the chat shows a Restart note).</summary>
    public static readonly DirectProperty<AgentBuilder, bool> IsTestStaleProperty =
        AvaloniaProperty.RegisterDirect<AgentBuilder, bool>(nameof(IsTestStale), o => o.IsTestStale);

    /// <summary>The message of the last failed save or publish; null after a success.</summary>
    public static readonly DirectProperty<AgentBuilder, string?> SaveErrorProperty =
        AvaloniaProperty.RegisterDirect<AgentBuilder, string?>(nameof(SaveError), o => o.SaveError);

    internal static readonly StyledProperty<double> StackWidthProperty =
        AvaloniaProperty.Register<AgentBuilder, double>(nameof(StackWidth), 760);

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<AgentBuilder, double>(nameof(CompactWidth), 520);

    internal static readonly StyledProperty<double> ColumnGapProperty =
        AvaloniaProperty.Register<AgentBuilder, double>(nameof(ColumnGap), 16);

    internal static readonly StyledProperty<double> ChatShareProperty =
        AvaloniaProperty.Register<AgentBuilder, double>(nameof(ChatShare), 0.46);

    internal static readonly StyledProperty<double> ChatMinWidthProperty =
        AvaloniaProperty.Register<AgentBuilder, double>(nameof(ChatMinWidth), 340);

    internal static readonly StyledProperty<double> ChatMinHeightProperty =
        AvaloniaProperty.Register<AgentBuilder, double>(nameof(ChatMinHeight), 560);

    internal static readonly StyledProperty<double> StackedChatHeightProperty =
        AvaloniaProperty.Register<AgentBuilder, double>(nameof(StackedChatHeight), 540);

    internal static readonly StyledProperty<TimeSpan> SimulatedSaveProperty =
        AvaloniaProperty.Register<AgentBuilder, TimeSpan>(nameof(SimulatedSave), TimeSpan.FromMilliseconds(600));

    internal static readonly StyledProperty<TimeSpan> SimulatedIndexProperty =
        AvaloniaProperty.Register<AgentBuilder, TimeSpan>(nameof(SimulatedIndex), TimeSpan.FromMilliseconds(2400));

    /// <summary>Raised when the draft changes: a field, the model, a tool, a source, or Discard.</summary>
    public static readonly RoutedEvent<AgentBuilderConfigChangedEventArgs> ConfigChangedEvent =
        RoutedEvent.Register<AgentBuilder, AgentBuilderConfigChangedEventArgs>(nameof(ConfigChanged), RoutingStrategies.Bubble);

    /// <summary>Raised after a save went through, with what was saved.</summary>
    public static readonly RoutedEvent<AgentBuilderSavedEventArgs> SavedEvent =
        RoutedEvent.Register<AgentBuilder, AgentBuilderSavedEventArgs>(nameof(Saved), RoutingStrategies.Bubble);

    /// <summary>Raised after a publish went through, with what was published.</summary>
    public static readonly RoutedEvent<AgentBuilderSavedEventArgs> PublishedEvent =
        RoutedEvent.Register<AgentBuilder, AgentBuilderSavedEventArgs>(nameof(Published), RoutingStrategies.Bubble);

    /// <summary>Raised when a knowledge source finished indexing or failed.</summary>
    public static readonly RoutedEvent<AgentBuilderSourceEventArgs> SourceIndexedEvent =
        RoutedEvent.Register<AgentBuilder, AgentBuilderSourceEventArgs>(nameof(SourceIndexed), RoutingStrategies.Bubble);

    /// <summary>The models to choose from.</summary>
    public AvaloniaList<AiModel> Models { get; } = new();

    /// <summary>The providers of the models: their names and the marks of models without one.</summary>
    public AvaloniaList<AiModelProvider> Providers { get; } = new();

    /// <summary>The tools the agent may be given; each tool's <see cref="AgentBuilderTool.IsOn"/> says whether it has it.</summary>
    public AvaloniaList<AgentBuilderTool> Tools { get; } = new();

    /// <summary>The knowledge sources; adding a link or a file appends to it, Remove takes from it.</summary>
    public AvaloniaList<AgentBuilderSource> Sources { get; } = new();

    /// <summary>Prompts offered in the empty test chat.</summary>
    public AvaloniaList<AiChatSuggestion> Suggestions { get; } = new();

    /// <inheritdoc cref="AgentNameProperty"/>
    public string AgentName { get => GetValue(AgentNameProperty); set => SetValue(AgentNameProperty, value); }

    /// <inheritdoc cref="AgentDescriptionProperty"/>
    public string AgentDescription { get => GetValue(AgentDescriptionProperty); set => SetValue(AgentDescriptionProperty, value); }

    /// <inheritdoc cref="InstructionsProperty"/>
    public string Instructions { get => GetValue(InstructionsProperty); set => SetValue(InstructionsProperty, value); }

    /// <inheritdoc cref="SelectedModelIdProperty"/>
    public string? SelectedModelId { get => GetValue(SelectedModelIdProperty); set => SetValue(SelectedModelIdProperty, value); }

    /// <inheritdoc cref="InstructionsMaxLengthProperty"/>
    public int InstructionsMaxLength { get => GetValue(InstructionsMaxLengthProperty); set => SetValue(InstructionsMaxLengthProperty, value); }

    /// <inheritdoc cref="SavedConfigProperty"/>
    public AgentBuilderConfig? SavedConfig { get => GetValue(SavedConfigProperty); set => SetValue(SavedConfigProperty, value); }

    /// <inheritdoc cref="PublishedConfigProperty"/>
    public AgentBuilderConfig? PublishedConfig { get => GetValue(PublishedConfigProperty); set => SetValue(PublishedConfigProperty, value); }

    /// <inheritdoc cref="ResponderProperty"/>
    public IAgentBuilderResponder? Responder { get => GetValue(ResponderProperty); set => SetValue(ResponderProperty, value); }

    /// <inheritdoc cref="ShowsSampleDataProperty"/>
    public bool ShowsSampleData { get => GetValue(ShowsSampleDataProperty); set => SetValue(ShowsSampleDataProperty, value); }

    /// <inheritdoc cref="CultureNameProperty"/>
    public string CultureName { get => GetValue(CultureNameProperty); set => SetValue(CultureNameProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public AgentBuilderLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsDirtyProperty"/>
    public bool IsDirty { get => _isDirty; private set => SetAndRaise(IsDirtyProperty, ref _isDirty, value); }

    /// <inheritdoc cref="IsSavingProperty"/>
    public bool IsSaving { get => _isSaving; private set => SetAndRaise(IsSavingProperty, ref _isSaving, value); }

    /// <inheritdoc cref="PublishStateProperty"/>
    public AgentBuilderPublishState PublishState { get => _publishState; private set => SetAndRaise(PublishStateProperty, ref _publishState, value); }

    /// <inheritdoc cref="ConfigProperty"/>
    public AgentBuilderConfig Config { get => _config; private set => SetAndRaise(ConfigProperty, ref _config, value); }

    /// <inheritdoc cref="TestConfigProperty"/>
    public AgentBuilderConfig? TestConfig { get => _testConfig; private set => SetAndRaise(TestConfigProperty, ref _testConfig, value); }

    /// <inheritdoc cref="IsTestStaleProperty"/>
    public bool IsTestStale { get => _isTestStale; private set => SetAndRaise(IsTestStaleProperty, ref _isTestStale, value); }

    /// <inheritdoc cref="SaveErrorProperty"/>
    public string? SaveError { get => _saveError; private set => SetAndRaise(SaveErrorProperty, ref _saveError, value); }

    /// <summary>
    /// Saves the draft to your backend: complete once it is stored; throw to keep the draft unsaved and show the message in
    /// the save bar. The token is cancelled when the builder leaves the visual tree. Null simulates a short save.
    /// </summary>
    public Func<AgentBuilderConfig, CancellationToken, Task>? SaveHandler { get; set; }

    /// <summary>
    /// Publishes the saved settings: complete once people use them; throw to show the message. Null simulates a short
    /// publish.
    /// </summary>
    public Func<AgentBuilderConfig, CancellationToken, Task>? PublishHandler { get; set; }

    /// <summary>
    /// Indexes a new or retried source: report progress from 0 to 1 and return the passage count (or null); throw to mark it
    /// failed with the message. Null simulates indexing that takes about 2.4 seconds.
    /// </summary>
    public Func<AgentBuilderSource, IProgress<double>, CancellationToken, Task<int?>>? IndexHandler { get; set; }

    /// <summary>
    /// Lets the person choose files to add and returns them as sources (their status is set to Indexing). Null uses the
    /// window's file picker; the Upload button is hidden when neither is available.
    /// </summary>
    public Func<CancellationToken, Task<IReadOnlyList<AgentBuilderSource>>>? PickFilesHandler { get; set; }

    internal double StackWidth { get => GetValue(StackWidthProperty); set => SetValue(StackWidthProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal double ColumnGap { get => GetValue(ColumnGapProperty); set => SetValue(ColumnGapProperty, value); }
    internal double ChatShare { get => GetValue(ChatShareProperty); set => SetValue(ChatShareProperty, value); }
    internal double ChatMinWidth { get => GetValue(ChatMinWidthProperty); set => SetValue(ChatMinWidthProperty, value); }
    internal double ChatMinHeight { get => GetValue(ChatMinHeightProperty); set => SetValue(ChatMinHeightProperty, value); }
    internal double StackedChatHeight { get => GetValue(StackedChatHeightProperty); set => SetValue(StackedChatHeightProperty, value); }
    internal TimeSpan SimulatedSave { get => GetValue(SimulatedSaveProperty); set => SetValue(SimulatedSaveProperty, value); }
    internal TimeSpan SimulatedIndex { get => GetValue(SimulatedIndexProperty); set => SetValue(SimulatedIndexProperty, value); }

    /// <summary>The waits of the simulations (save, publish, indexing, test replies); tests replace it to step time.</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (time, token) => Task.Delay(time, token);

    /// <inheritdoc cref="ConfigChangedEvent"/>
    public event EventHandler<AgentBuilderConfigChangedEventArgs>? ConfigChanged
    {
        add => AddHandler(ConfigChangedEvent, value);
        remove => RemoveHandler(ConfigChangedEvent, value);
    }

    /// <inheritdoc cref="SavedEvent"/>
    public event EventHandler<AgentBuilderSavedEventArgs>? Saved
    {
        add => AddHandler(SavedEvent, value);
        remove => RemoveHandler(SavedEvent, value);
    }

    /// <inheritdoc cref="PublishedEvent"/>
    public event EventHandler<AgentBuilderSavedEventArgs>? Published
    {
        add => AddHandler(PublishedEvent, value);
        remove => RemoveHandler(PublishedEvent, value);
    }

    /// <inheritdoc cref="SourceIndexedEvent"/>
    public event EventHandler<AgentBuilderSourceEventArgs>? SourceIndexed
    {
        add => AddHandler(SourceIndexedEvent, value);
        remove => RemoveHandler(SourceIndexedEvent, value);
    }

    #endregion

    private bool _isDirty;
    private bool _isSaving;
    private AgentBuilderPublishState _publishState;
    private AgentBuilderConfig _config = AgentBuilderConfig.Empty;
    private AgentBuilderConfig? _testConfig;
    private bool _isTestStale;
    private string? _saveError;

    private CultureInfo Culture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(CultureName) ? "en-US" : CultureName);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.GetCultureInfo("en-US");
            }
        }
    }

    /// <summary>Creates an empty builder; it shows the sample agent when it first appears unless you give it data first.</summary>
    public AgentBuilder()
    {
        Tools.CollectionChanged += OnToolsChanged;
        Sources.CollectionChanged += OnSourcesChanged;
        Models.CollectionChanged += OnCatalogChanged;
        Providers.CollectionChanged += OnCatalogChanged;
        Suggestions.CollectionChanged += OnCatalogChanged;
    }
}
