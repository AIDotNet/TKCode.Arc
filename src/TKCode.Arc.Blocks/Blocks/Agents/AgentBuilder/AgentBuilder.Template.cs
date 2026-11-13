using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class AgentBuilder
{
    private Grid? _layout;
    private Border? _settingsCard;
    private Border? _chatCard;
    private TextBlock? _initial;
    private TextBlock? _title;
    private TextBlock? _subtitle;
    private Badge? _status;
    private Button? _publish;
    private Input? _nameInput;
    private Input? _descriptionInput;
    private Textarea? _instructions;
    private TextBlock? _instructionsCount;
    private ModelPicker? _model;
    private StackPanel? _toolList;
    private TextBlock? _toolsCount;
    private StackPanel? _sourceList;
    private TextBlock? _knowledgeCount;
    private TextBlock? _noSources;
    private Input? _linkInput;
    private Button? _addLink;
    private Button? _upload;
    private ArcContentSwap? _saveStatus;
    private Button? _save;
    private Button? _discard;
    private AiChat? _chat;
    private PresenceHost? _staleHost;
    private TextBlock? _staleText;
    private Button? _restart;
    private INameScope? _scope;
    private readonly AiAnnouncer _announcer = new();
    private readonly Dictionary<AgentBuilderTool, AgentBuilderToolRow> _toolRows = new();
    private readonly Dictionary<AgentBuilderSource, AgentBuilderSourceRow> _sourceRows = new();
    private bool _syncingFields;
    private bool? _stacked;
    private object? _saveStatusKey;

    #region Parts (tests and tools)

    internal AiChat? Chat => _chat;
    internal ModelPicker? ModelField => _model;
    internal Input? NameField => _nameInput;
    internal Input? DescriptionField => _descriptionInput;
    internal Textarea? InstructionsField => _instructions;
    internal TextBlock? InstructionsCountText => _instructionsCount;
    internal TextBlock? TitleText => _title;
    internal TextBlock? InitialText => _initial;
    internal Badge? StatusBadge => _status;
    internal Button? PublishButton => _publish;
    internal Button? SaveButton => _save;
    internal Button? DiscardButton => _discard;
    internal Input? LinkField => _linkInput;
    internal Button? AddLinkButton => _addLink;
    internal Button? UploadButton => _upload;
    internal Button? RestartButton => _restart;
    internal PresenceHost? StaleHost => _staleHost;
    internal TextBlock? ToolsCountText => _toolsCount;
    internal TextBlock? KnowledgeCountText => _knowledgeCount;
    internal TextBlock? NoSourcesText => _noSources;
    internal string? SaveStatusText => _saveStatus?.Content as string;
    internal IReadOnlyList<AgentBuilderToolRow> ToolRows => Tools.Where(_toolRows.ContainsKey).Select(t => _toolRows[t]).ToList();
    internal IReadOnlyList<AgentBuilderSourceRow> SourceRows => Sources.Where(_sourceRows.ContainsKey).Select(s => _sourceRows[s]).ToList();
    internal string? LastAnnouncement => _announcer.Announcement;
    internal bool IsStacked => _stacked == true;

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unwire();
        _layout?.Children.Remove(_announcer);
        _scope = e.NameScope;
        T? Find<T>(string name) where T : class => e.NameScope.Find<T>(name);
        _layout = Find<Grid>(LayoutPart);
        _settingsCard = Find<Border>(SettingsCardPart);
        _chatCard = Find<Border>(ChatCardPart);
        _initial = Find<TextBlock>(InitialPart);
        _title = Find<TextBlock>(TitlePart);
        _subtitle = Find<TextBlock>(SubtitlePart);
        _status = Find<Badge>(StatusPart);
        _publish = Find<Button>(PublishPart);
        _nameInput = Find<Input>(NameInputPart);
        _descriptionInput = Find<Input>(DescriptionInputPart);
        _instructions = Find<Textarea>(InstructionsPart);
        _instructionsCount = Find<TextBlock>(InstructionsCountPart);
        _model = Find<ModelPicker>(ModelPart);
        _toolList = Find<StackPanel>(ToolListPart);
        _toolsCount = Find<TextBlock>(ToolsCountPart);
        _sourceList = Find<StackPanel>(SourceListPart);
        _knowledgeCount = Find<TextBlock>(KnowledgeCountPart);
        _noSources = Find<TextBlock>(NoSourcesPart);
        _linkInput = Find<Input>(LinkInputPart);
        _addLink = Find<Button>(AddLinkPart);
        _upload = Find<Button>(UploadPart);
        _saveStatus = Find<ArcContentSwap>(SaveStatusPart);
        _save = Find<Button>(SavePart);
        _discard = Find<Button>(DiscardPart);
        _chat = Find<AiChat>(ChatPart);
        _staleHost = Find<PresenceHost>(StaleHostPart);
        _staleText = Find<TextBlock>(StaleTextPart);
        _restart = Find<Button>(RestartPart);
        _layout?.Children.Add(_announcer);
        Wire();
        _stacked = null;
        _saveStatusKey = null;
        RebuildToolRows();
        RebuildSourceRows();
        SyncCatalog();
        SyncFields();
        ApplyLabels();
        UpdateState();
        UpdateUploadVisibility();
    }

    private void Wire()
    {
        if (_nameInput is not null)
        {
            _nameInput.TextChanged += OnFieldTextChanged;
        }
        if (_descriptionInput is not null)
        {
            _descriptionInput.TextChanged += OnFieldTextChanged;
        }
        if (_instructions is not null)
        {
            _instructions.TextChanged += OnFieldTextChanged;
        }
        if (_model is not null)
        {
            _model.PropertyChanged += OnModelPickerPropertyChanged;
        }
        if (_linkInput is not null)
        {
            _linkInput.AddHandler(KeyDownEvent, OnLinkKeyDown, RoutingStrategies.Tunnel);
            _linkInput.TextChanged += OnLinkTextChanged;
        }
        Click(_addLink, OnAddLinkClick, true);
        Click(_upload, OnUploadClick, true);
        Click(_save, OnSaveClick, true);
        Click(_discard, OnDiscardClick, true);
        Click(_publish, OnPublishClick, true);
        Click(_restart, OnRestartClick, true);
        if (_chat is not null)
        {
            _chat.Responder = new AgentBuilderChatResponder(this);
            _chat.PropertyChanged += OnChatPropertyChanged;
            _chat.ConversationCreated += OnChatConversationCreated;
            _chat.NewChatRequested += OnChatNewChatRequested;
        }
    }

    private void Unwire()
    {
        if (_nameInput is not null)
        {
            _nameInput.TextChanged -= OnFieldTextChanged;
        }
        if (_descriptionInput is not null)
        {
            _descriptionInput.TextChanged -= OnFieldTextChanged;
        }
        if (_instructions is not null)
        {
            _instructions.TextChanged -= OnFieldTextChanged;
        }
        if (_model is not null)
        {
            _model.PropertyChanged -= OnModelPickerPropertyChanged;
        }
        if (_linkInput is not null)
        {
            _linkInput.RemoveHandler(KeyDownEvent, OnLinkKeyDown);
            _linkInput.TextChanged -= OnLinkTextChanged;
        }
        Click(_addLink, OnAddLinkClick, false);
        Click(_upload, OnUploadClick, false);
        Click(_save, OnSaveClick, false);
        Click(_discard, OnDiscardClick, false);
        Click(_publish, OnPublishClick, false);
        Click(_restart, OnRestartClick, false);
        if (_chat is not null)
        {
            _chat.Responder = null;
            _chat.PropertyChanged -= OnChatPropertyChanged;
            _chat.ConversationCreated -= OnChatConversationCreated;
            _chat.NewChatRequested -= OnChatNewChatRequested;
        }
        foreach (var row in _sourceRows.Values)
        {
            row.RemoveRequested -= OnSourceRemoveRequested;
            row.RetryRequested -= OnSourceRetryRequested;
        }
        _toolRows.Clear();
        _sourceRows.Clear();
        _toolList?.Children.Clear();
        _sourceList?.Children.Clear();
    }

    private static void Click(Button? button, EventHandler<RoutedEventArgs> handler, bool add)
    {
        if (button is null)
        {
            return;
        }
        if (add)
        {
            button.Click += handler;
        }
        else
        {
            button.Click -= handler;
        }
    }

    #region Layout

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!double.IsInfinity(availableSize.Width))
        {
            ApplyWidth(availableSize.Width);
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>Puts the chat beside or under the settings and tightens the paddings, by the builder's own width.</summary>
    private void ApplyWidth(double width)
    {
        PseudoClasses.Set(CompactPseudoClass, width < CompactWidth - 0.01);
        var stacked = width < StackWidth - 0.01;
        if (_layout is not { } layout || _settingsCard is null || _chatCard is null)
        {
            return;
        }
        var chatWidth = Math.Max(ChatMinWidth, Math.Round(width * ChatShare));
        if (_stacked == stacked && (stacked || layout.ColumnDefinitions.Count == 3 && Math.Abs(layout.ColumnDefinitions[2].Width.Value - chatWidth) < 0.5))
        {
            return;
        }
        _stacked = stacked;
        PseudoClasses.Set(StackedPseudoClass, stacked);
        layout.ColumnDefinitions.Clear();
        layout.RowDefinitions.Clear();
        if (stacked)
        {
            layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            layout.RowDefinitions.Add(new RowDefinition(new GridLength(ColumnGap)));
            layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(_chatCard, 0);
            Grid.SetRow(_chatCard, 2);
            _chatCard.Height = StackedChatHeight;
            _chatCard.MinHeight = 0;
        }
        else
        {
            layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            layout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(ColumnGap)));
            layout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(chatWidth)));
            layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetColumn(_chatCard, 2);
            Grid.SetRow(_chatCard, 0);
            _chatCard.Height = double.NaN;
            _chatCard.MinHeight = ChatMinHeight;
        }
        Grid.SetColumn(_settingsCard, 0);
        Grid.SetRow(_settingsCard, 0);
    }

    #endregion

    #region Lists

    private void RebuildToolRows()
    {
        if (_toolList is not { } list)
        {
            return;
        }
        var keep = new HashSet<AgentBuilderTool>(Tools);
        foreach (var gone in _toolRows.Keys.Where(t => !keep.Contains(t)).ToList())
        {
            _toolRows.Remove(gone);
        }
        list.Children.Clear();
        foreach (var tool in Tools)
        {
            if (!_toolRows.TryGetValue(tool, out var row))
            {
                row = new AgentBuilderToolRow(tool);
                _toolRows[tool] = row;
            }
            list.Children.Add(row);
        }
    }

    private void RebuildSourceRows()
    {
        if (_sourceList is not { } list)
        {
            return;
        }
        var keep = new HashSet<AgentBuilderSource>(Sources);
        foreach (var gone in _sourceRows.Keys.Where(s => !keep.Contains(s)).ToList())
        {
            var row = _sourceRows[gone];
            row.RemoveRequested -= OnSourceRemoveRequested;
            row.RetryRequested -= OnSourceRetryRequested;
            _sourceRows.Remove(gone);
        }
        list.Children.Clear();
        var labels = Labels;
        var culture = Culture;
        foreach (var source in Sources)
        {
            if (!_sourceRows.TryGetValue(source, out var row))
            {
                row = new AgentBuilderSourceRow(source);
                row.RemoveRequested += OnSourceRemoveRequested;
                row.RetryRequested += OnSourceRetryRequested;
                _sourceRows[source] = row;
            }
            row.SetLabels(labels, culture);
            list.Children.Add(row);
        }
    }

    #endregion

    #region Showing the state

    /// <summary>Copies the catalog (models, providers, suggestions) into the model pill and the test chat.</summary>
    private void SyncCatalog()
    {
        if (_model is { } picker)
        {
            Mirror(Providers, picker.Providers);
            Mirror(Models, picker.Models);
            picker.SelectedModelId = SelectedModelId;
        }
        if (_chat is { } chat)
        {
            Mirror(Providers, chat.Providers);
            Mirror(Models, chat.Models);
            Mirror(Suggestions, chat.Suggestions);
            chat.SelectedModel = ModelById(SelectedModelId);
        }
    }

    private static void Mirror<T>(IList<T> source, Avalonia.Collections.AvaloniaList<T> target)
    {
        if (target.SequenceEqual(source))
        {
            return;
        }
        target.Clear();
        target.AddRange(source);
    }

    internal AiModel? ModelById(string? id) => id is null ? null : Models.FirstOrDefault(m => m.Id == id);

    /// <summary>Copies the fields' properties into the inputs (without echoing back).</summary>
    private void SyncFields()
    {
        _syncingFields = true;
        try
        {
            SetText(_nameInput, AgentName);
            SetText(_descriptionInput, AgentDescription);
            SetText(_instructions, Instructions);
        }
        finally
        {
            _syncingFields = false;
        }
    }

    private static void SetText(TextBox? box, string text)
    {
        if (box is not null && box.Text != text)
        {
            box.Text = text;
        }
    }

    private void ApplyLabels()
    {
        var labels = Labels;
        SetText(IdentityTitlePart, labels.IdentityTitle);
        SetText(InstructionsTitlePart, labels.InstructionsTitle);
        SetText(InstructionsHintPart, labels.InstructionsHint);
        SetText(ModelTitlePart, labels.ModelTitle);
        SetText(ModelHintPart, labels.ModelHint);
        SetText(ToolsTitlePart, labels.ToolsTitle);
        SetText(ToolsHintPart, labels.ToolsHint);
        SetText(KnowledgeTitlePart, labels.KnowledgeTitle);
        SetText(KnowledgeHintPart, labels.KnowledgeHint);
        if (_noSources is not null)
        {
            _noSources.Text = labels.NoSources;
        }
        if (_staleText is not null)
        {
            _staleText.Text = labels.StaleNotice;
        }
        if (_nameInput is not null)
        {
            _nameInput.Label = labels.NameLabel;
            _nameInput.Placeholder = labels.NamePlaceholder;
        }
        if (_descriptionInput is not null)
        {
            _descriptionInput.Label = labels.DescriptionLabel;
            _descriptionInput.Placeholder = labels.DescriptionPlaceholder;
        }
        if (_instructions is not null)
        {
            _instructions.Placeholder = labels.InstructionsPlaceholder;
            AutomationProperties.SetName(_instructions, labels.InstructionsTitle);
            AutomationProperties.SetHelpText(_instructions, labels.InstructionsHint);
        }
        if (_model is not null)
        {
            _model.Labels = labels.Model;
            AutomationProperties.SetHelpText(_model, labels.ModelHint);
        }
        if (_linkInput is not null)
        {
            _linkInput.Placeholder = labels.LinkPlaceholder;
            AutomationProperties.SetName(_linkInput, labels.LinkName);
        }
        if (_addLink is not null)
        {
            _addLink.Content = labels.AddLink;
        }
        if (_upload is not null)
        {
            _upload.Content = labels.Upload;
        }
        if (_save is not null)
        {
            _save.Content = labels.Save;
        }
        if (_discard is not null)
        {
            _discard.Content = labels.Discard;
        }
        if (_restart is not null)
        {
            _restart.Content = labels.Restart;
            AutomationProperties.SetName(_restart, labels.RestartName);
        }
        if (_settingsCard is not null)
        {
            AutomationProperties.SetName(_settingsCard, labels.SettingsName);
        }
        if (_chatCard is not null)
        {
            AutomationProperties.SetName(_chatCard, labels.TestTitle);
            AutomationProperties.SetHelpText(_chatCard, labels.TestHint);
        }
        if (_chat is not null)
        {
            _chat.Labels = labels.Chat;
        }
        foreach (var row in _sourceRows.Values)
        {
            row.SetLabels(labels, Culture);
        }
    }

    private void SetText(string part, string text)
    {
        if (_scope?.Find<TextBlock>(part) is { } block)
        {
            block.Text = text;
        }
    }

    /// <summary>Brings every derived text, badge and button in line with the state.</summary>
    private void UpdateVisuals()
    {
        var labels = Labels;
        var culture = Culture;
        var name = string.IsNullOrWhiteSpace(AgentName) ? labels.UntitledAgent : AgentName.Trim();
        if (_title is not null)
        {
            _title.Text = name;
        }
        if (_initial is not null)
        {
            _initial.Text = string.IsNullOrWhiteSpace(AgentName) ? "?" : AgentName.Trim()[..1].ToUpper(culture);
        }
        if (_subtitle is not null)
        {
            _subtitle.Text = AgentDescription;
            _subtitle.IsVisible = !string.IsNullOrWhiteSpace(AgentDescription);
        }
        var tone = ToneOf(AgentName);
        PseudoClasses.Set(Tone1PseudoClass, tone == 0);
        PseudoClasses.Set(Tone2PseudoClass, tone == 1);
        PseudoClasses.Set(Tone3PseudoClass, tone == 2);
        PseudoClasses.Set(Tone4PseudoClass, tone == 3);

        if (_status is not null)
        {
            _status.Content = PublishState switch
            {
                AgentBuilderPublishState.Published => labels.Published,
                AgentBuilderPublishState.Changed => labels.Changed,
                _ => labels.Draft
            };
            _status.Tone = PublishState switch
            {
                AgentBuilderPublishState.Published => BadgeTone.Success,
                AgentBuilderPublishState.Changed => BadgeTone.Warning,
                _ => BadgeTone.Neutral
            };
        }
        var upToDate = PublishState == AgentBuilderPublishState.Published;
        if (_publish is not null)
        {
            _publish.Content = upToDate ? labels.UpToDate : labels.Publish;
            _publish.IsEnabled = !upToDate && CanSave(allowClean: true);
            _publish.IsLoading = _publishing;
        }

        var nameMissing = string.IsNullOrWhiteSpace(AgentName);
        if (_nameInput is not null)
        {
            _nameInput.ErrorMessage = nameMissing ? labels.NameRequired : null;
        }
        var characters = Instructions.Length;
        if (_instructionsCount is not null)
        {
            _instructionsCount.Text = labels.InstructionsCount(characters, EstimateTokens(Instructions), culture);
        }
        if (_instructions is not null)
        {
            _instructions.ErrorMessage = characters > InstructionsMaxLength ? labels.InstructionsTooLong(InstructionsMaxLength, culture) : null;
        }

        if (_toolsCount is not null)
        {
            _toolsCount.Text = labels.ToolsCount(Tools.Count(t => t.IsOn), Tools.Count);
        }
        if (_knowledgeCount is not null)
        {
            _knowledgeCount.Text = labels.KnowledgeCount(Sources.Count);
        }
        if (_noSources is not null)
        {
            _noSources.IsVisible = Sources.Count == 0;
        }

        string status;
        if (IsSaving)
        {
            status = labels.Saving;
        }
        else if (SaveError is { } error)
        {
            status = labels.SaveFailed(error);
        }
        else
        {
            status = IsDirty ? labels.Unsaved : labels.AllSaved;
        }
        if (_saveStatus is not null && !Equals(_saveStatusKey, status))
        {
            _saveStatusKey = status;
            _saveStatus.Content = status;
        }
        if (_save is not null)
        {
            _save.IsEnabled = IsDirty && CanSave(allowClean: false);
            _save.IsLoading = IsSaving && !_publishing;
        }
        if (_discard is not null)
        {
            _discard.IsEnabled = IsDirty && !IsSaving;
        }

        if (_staleHost is not null)
        {
            _staleHost.IsPresent = IsTestStale;
        }
        if (_chat is { } chat)
        {
            chat.GreetingText = labels.TestGreeting(name);
            chat.PlaceholderText = labels.TestPlaceholder(name);
        }
        PseudoClasses.Set(DirtyPseudoClass, IsDirty);
        PseudoClasses.Set(SavingPseudoClass, IsSaving);
        PseudoClasses.Set(StalePseudoClass, IsTestStale);
    }

    /// <summary>A rough token count: about four characters a token, as most tokenizers average on English text.</summary>
    internal static int EstimateTokens(string text) => text.Length == 0 ? 0 : (int)Math.Ceiling(text.Length / 4.0);

    /// <summary>A stable color slot from the name, so the avatar keeps its color between sessions.</summary>
    internal static int ToneOf(string name)
    {
        var sum = 0;
        foreach (var c in name.Trim().ToUpperInvariant())
        {
            sum = unchecked(sum * 31 + c);
        }
        return (int)((uint)sum % 4);
    }

    private void UpdateUploadVisibility()
    {
        if (_upload is not null)
        {
            _upload.IsVisible = PickFilesHandler is not null || TopLevel.GetTopLevel(this)?.StorageProvider.CanOpen == true;
        }
    }

    #endregion

    #region Lifetime and keyboard

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _lifetime = new CancellationTokenSource();
        EnsureData();
        UpdateUploadVisibility();
        // Sources that were indexing when the builder left (or were declared as indexing) carry on.
        foreach (var source in Sources.Where(s => s.Status == AgentBuilderSourceStatus.Indexing && !_indexing.ContainsKey(s)).ToList())
        {
            StartIndexing(source, announce: false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _lifetime = null;
        foreach (var run in _indexing.Values)
        {
            run.Cancel();
        }
        _indexing.Clear();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }
        if (e.Key == Key.S && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && (e.KeyModifiers & KeyModifiers.Alt) == 0)
        {
            e.Handled = true;
            if (IsDirty && CanSave(allowClean: false))
            {
                _ = SaveAsync();
            }
        }
    }

    #endregion

    /// <summary>Posts <paramref name="action"/> at background priority (after the layout that follows a change).</summary>
    private static void Later(Action action) => Dispatcher.UIThread.Post(action, DispatcherPriority.Background);
}
