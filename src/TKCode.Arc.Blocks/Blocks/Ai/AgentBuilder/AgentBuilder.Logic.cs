using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TKCode.Arc;

public partial class AgentBuilder
{
    private CancellationTokenSource? _lifetime;
    private readonly Dictionary<AgentBuilderSource, CancellationTokenSource> _indexing = new();
    private readonly HashSet<AgentBuilderTool> _watchedTools = new();
    private bool _applying;
    private bool _publishing;
    private bool _dataReady;

    #region Data

    /// <summary>Fills an empty builder with the sample agent (once) and takes the first saved snapshot.</summary>
    private void EnsureData()
    {
        if (_dataReady)
        {
            return;
        }
        _dataReady = true;
        var empty = Models.Count == 0 && Tools.Count == 0 && Sources.Count == 0 && string.IsNullOrEmpty(AgentName)
                    && string.IsNullOrEmpty(Instructions) && SavedConfig is null;
        if (empty && ShowsSampleData)
        {
            _applying = true;
            try
            {
                Providers.AddRange(AgentBuilderSamples.Providers());
                Models.AddRange(AgentBuilderSamples.Models());
                Tools.AddRange(AgentBuilderSamples.Tools());
                Sources.AddRange(AgentBuilderSamples.Sources());
                Suggestions.AddRange(AgentBuilderSamples.Suggestions());
                SetCurrentValue(AgentNameProperty, AgentBuilderSamples.Name);
                SetCurrentValue(AgentDescriptionProperty, AgentBuilderSamples.Description);
                SetCurrentValue(InstructionsProperty, AgentBuilderSamples.Instructions);
                SetCurrentValue(SelectedModelIdProperty, AgentBuilderSamples.ModelId);
            }
            finally
            {
                _applying = false;
            }
            var saved = BuildConfig();
            SetCurrentValue(SavedConfigProperty, saved);
            // The sample was published before the hand-off tool was switched on, so Publish has something to do.
            SetCurrentValue(PublishedConfigProperty, saved with { ToolIds = saved.ToolIds.Where(id => id != "handoff").ToList() });
        }
        if (SelectedModelId is null && Models.Count > 0)
        {
            _applying = true;
            try
            {
                SetCurrentValue(SelectedModelIdProperty, Models[0].Id);
            }
            finally
            {
                _applying = false;
            }
        }
        if (SavedConfig is null)
        {
            SetCurrentValue(SavedConfigProperty, BuildConfig());
        }
        SyncCatalog();
        SyncFields();
        UpdateState();
    }

    /// <summary>The settings as they are now.</summary>
    private AgentBuilderConfig BuildConfig() => new(
        AgentName ?? string.Empty,
        AgentDescription ?? string.Empty,
        Instructions ?? string.Empty,
        SelectedModelId,
        Tools.Where(t => t.IsOn).Select(t => t.Id).ToList(),
        Sources.ToList());

    /// <summary>
    /// Shows <paramref name="config"/> as the saved settings: the fields, model, tools (by id) and sources take its values,
    /// nothing is unsaved, and the test chat is marked stale if it ran on something else.
    /// </summary>
    /// <param name="config">The settings to load.</param>
    public void Load(AgentBuilderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _dataReady = true;
        Apply(config);
        SetCurrentValue(SavedConfigProperty, config);
        SaveError = null;
        UpdateState();
    }

    /// <summary>Puts <paramref name="config"/> on screen without raising change events for each part.</summary>
    private void Apply(AgentBuilderConfig config)
    {
        _applying = true;
        try
        {
            SetCurrentValue(AgentNameProperty, config.Name);
            SetCurrentValue(AgentDescriptionProperty, config.Description);
            SetCurrentValue(InstructionsProperty, config.Instructions);
            SetCurrentValue(SelectedModelIdProperty, config.ModelId);
            foreach (var tool in Tools)
            {
                tool.IsOn = config.HasTool(tool.Id);
            }
            if (!Sources.SequenceEqual(config.Sources))
            {
                foreach (var gone in Sources.Where(s => !config.Sources.Contains(s)).ToList())
                {
                    CancelIndexing(gone);
                }
                Sources.Clear();
                Sources.AddRange(config.Sources);
            }
        }
        finally
        {
            _applying = false;
        }
        SyncFields();
        SyncCatalog();
    }

    /// <summary>Recomputes the draft, dirty, publish and stale states, then the visuals.</summary>
    private void UpdateState()
    {
        var config = BuildConfig();
        if (!config.Equals(Config) || !ReferenceEquals(config.Sources, Config.Sources))
        {
            Config = config;
        }
        IsDirty = SavedConfig is { } saved && !config.Equals(saved);
        PublishState = PublishedConfig is not { } published
            ? AgentBuilderPublishState.Draft
            : !IsDirty && SavedConfig is { } current && published.Equals(current)
                ? AgentBuilderPublishState.Published
                : AgentBuilderPublishState.Changed;
        var stale = TestConfig is { } tested && !tested.Equals(config);
        if (stale && !IsTestStale)
        {
            Announce(Labels.StaleAnnouncement);
        }
        IsTestStale = stale;
        UpdateVisuals();
    }

    /// <summary>A part of the draft changed: refresh and tell the app (unless the change came from Load or Discard).</summary>
    private void OnDraftChanged(AgentBuilderField field)
    {
        if (SaveError is not null)
        {
            SaveError = null;
        }
        UpdateState();
        if (!_applying)
        {
            RaiseEvent(new AgentBuilderConfigChangedEventArgs(ConfigChangedEvent, this, field, Config, IsDirty));
        }
    }

    private bool CanSave(bool allowClean)
    {
        if (IsSaving || (!allowClean && !IsDirty))
        {
            return false;
        }
        return !string.IsNullOrWhiteSpace(AgentName) && Instructions.Length <= InstructionsMaxLength;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == AgentNameProperty)
        {
            SyncFieldsIfIdle();
            OnDraftChanged(AgentBuilderField.Name);
        }
        else if (property == AgentDescriptionProperty)
        {
            SyncFieldsIfIdle();
            OnDraftChanged(AgentBuilderField.Description);
        }
        else if (property == InstructionsProperty)
        {
            SyncFieldsIfIdle();
            OnDraftChanged(AgentBuilderField.Instructions);
        }
        else if (property == SelectedModelIdProperty)
        {
            var id = change.GetNewValue<string?>();
            if (_model is { } picker && picker.SelectedModelId != id && id is not null)
            {
                picker.SelectedModelId = id;
            }
            if (_chat is { } chat && !ReferenceEquals(chat.SelectedModel, ModelById(id)))
            {
                chat.SelectedModel = ModelById(id);
            }
            OnDraftChanged(AgentBuilderField.Model);
        }
        else if (property == SavedConfigProperty || property == PublishedConfigProperty || property == InstructionsMaxLengthProperty
                 || property == CultureNameProperty)
        {
            if (property == CultureNameProperty)
            {
                RebuildSourceRows();
            }
            UpdateState();
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
            UpdateVisuals();
        }
        else if (property == StackWidthProperty || property == CompactWidthProperty || property == ChatShareProperty
                 || property == ChatMinWidthProperty || property == ColumnGapProperty || property == StackedChatHeightProperty
                 || property == ChatMinHeightProperty)
        {
            _stacked = null;
            InvalidateMeasure();
        }
    }

    private void SyncFieldsIfIdle()
    {
        if (!_syncingFields)
        {
            SyncFields();
        }
    }

    private void OnFieldTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingFields)
        {
            return;
        }
        _syncingFields = true;
        try
        {
            if (ReferenceEquals(sender, _nameInput))
            {
                SetCurrentValue(AgentNameProperty, _nameInput!.Text ?? string.Empty);
            }
            else if (ReferenceEquals(sender, _descriptionInput))
            {
                SetCurrentValue(AgentDescriptionProperty, _descriptionInput!.Text ?? string.Empty);
            }
            else if (ReferenceEquals(sender, _instructions))
            {
                SetCurrentValue(InstructionsProperty, _instructions!.Text ?? string.Empty);
            }
        }
        finally
        {
            _syncingFields = false;
        }
    }

    private void OnModelPickerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ModelPicker.SelectedModelIdProperty && _model?.SelectedModelId is { } id && id != SelectedModelId)
        {
            SetCurrentValue(SelectedModelIdProperty, id);
        }
    }

    private void OnChatPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == AiChat.SelectedModelProperty && _chat?.SelectedModel is { } model && model.Id != SelectedModelId)
        {
            SetCurrentValue(SelectedModelIdProperty, model.Id);
        }
    }

    private void OnCatalogChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncCatalog();

    #endregion

    #region Tools

    private void OnToolsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var current = new HashSet<AgentBuilderTool>(Tools);
        foreach (var gone in _watchedTools.Where(t => !current.Contains(t)).ToList())
        {
            gone.PropertyChanged -= OnToolPropertyChanged;
            _watchedTools.Remove(gone);
        }
        foreach (var tool in Tools)
        {
            if (_watchedTools.Add(tool))
            {
                tool.PropertyChanged += OnToolPropertyChanged;
            }
        }
        RebuildToolRows();
        OnDraftChanged(AgentBuilderField.Tools);
    }

    private void OnToolPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not AgentBuilderTool tool)
        {
            return;
        }
        if (e.Property == AgentBuilderTool.IsOnProperty)
        {
            if (!_applying)
            {
                Announce(Labels.ToolAnnouncement(tool.Name, tool.IsOn));
            }
            OnDraftChanged(AgentBuilderField.Tools);
        }
        else if (e.Property == AgentBuilderTool.IdProperty)
        {
            UpdateState();
        }
    }

    #endregion

    #region Sources

    private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildSourceRows();
        OnDraftChanged(AgentBuilderField.Sources);
    }

    /// <summary>
    /// Adds a web page as a source and starts indexing it. Returns false (and shows why under the link field) when the text
    /// is not a full http or https link or the page is already a source.
    /// </summary>
    /// <param name="link">The page's address.</param>
    public bool AddLink(string link)
    {
        var labels = Labels;
        var text = (link ?? string.Empty).Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            ShowLinkError(labels.InvalidLink);
            return false;
        }
        if (Sources.Any(s => s.Url is { } u && Uri.Compare(u, uri, UriComponents.HttpRequestUrl, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0))
        {
            ShowLinkError(labels.DuplicateLink);
            return false;
        }
        var source = new AgentBuilderSource
        {
            Id = "link-" + Guid.NewGuid().ToString("N")[..8],
            Name = (uri.Host + uri.AbsolutePath).TrimEnd('/'),
            Kind = AgentBuilderSourceKind.Website,
            Url = uri
        };
        ShowLinkError(null);
        if (_linkInput is not null)
        {
            _linkInput.Text = string.Empty;
        }
        AddSources([source]);
        return true;
    }

    /// <summary>Adds sources (files your code received, for example) and starts indexing each.</summary>
    /// <param name="sources">The sources to add; their status becomes Indexing.</param>
    public void AddSources(IEnumerable<AgentBuilderSource> sources)
    {
        foreach (var source in sources.ToList())
        {
            if (Sources.Contains(source))
            {
                continue;
            }
            if (string.IsNullOrEmpty(source.Id))
            {
                source.Id = "source-" + Guid.NewGuid().ToString("N")[..8];
            }
            source.Status = AgentBuilderSourceStatus.Indexing;
            source.Progress = 0;
            source.ErrorMessage = null;
            Sources.Add(source);
            Announce(Labels.SourceAddedAnnouncement(source.Name));
            StartIndexing(source, announce: true);
        }
    }

    /// <summary>Removes a source and stops its indexing.</summary>
    /// <param name="source">The source to remove.</param>
    public void RemoveSource(AgentBuilderSource source)
    {
        var index = Sources.IndexOf(source);
        if (index < 0)
        {
            return;
        }
        CancelIndexing(source);
        Sources.RemoveAt(index);
        Announce(Labels.SourceRemovedAnnouncement(source.Name));
    }

    /// <summary>Indexes a failed (or any) source again.</summary>
    /// <param name="source">The source to index.</param>
    public void RetrySource(AgentBuilderSource source)
    {
        if (!Sources.Contains(source))
        {
            return;
        }
        source.ErrorMessage = null;
        source.Progress = 0;
        source.Status = AgentBuilderSourceStatus.Indexing;
        StartIndexing(source, announce: true);
    }

    private void OnSourceRemoveRequested(object? sender, EventArgs e)
    {
        if (sender is not AgentBuilderSourceRow row)
        {
            return;
        }
        var index = Sources.IndexOf(row.Source);
        var keyboard = row.RemoveButton?.IsKeyboardFocusWithin == true;
        RemoveSource(row.Source);
        if (keyboard)
        {
            // Focus stays in the list: the row that took its place, the one above, or the link field.
            var next = index < Sources.Count ? Sources[index] : index > 0 ? Sources[index - 1] : null;
            if (next is not null && _sourceRows.TryGetValue(next, out var nextRow) && nextRow.RemoveButton is { } button)
            {
                button.Focus(NavigationMethod.Tab);
            }
            else
            {
                _linkInput?.Focus(NavigationMethod.Tab);
            }
        }
    }

    private void OnSourceRetryRequested(object? sender, EventArgs e)
    {
        if (sender is AgentBuilderSourceRow row)
        {
            var keyboard = row.RetryButton?.IsKeyboardFocusWithin == true;
            RetrySource(row.Source);
            if (keyboard)
            {
                row.RemoveButton?.Focus(NavigationMethod.Tab);
            }
        }
    }

    private void CancelIndexing(AgentBuilderSource source)
    {
        if (_indexing.Remove(source, out var run))
        {
            run.Cancel();
            run.Dispose();
        }
    }

    private void StartIndexing(AgentBuilderSource source, bool announce)
    {
        CancelIndexing(source);
        if (_lifetime is null)
        {
            // Not shown: it starts when the builder is (OnAttachedToVisualTree).
            return;
        }
        var run = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _indexing[source] = run;
        _ = IndexAsync(source, run, announce);
    }

    private async Task IndexAsync(AgentBuilderSource source, CancellationTokenSource run, bool announce)
    {
        var token = run.Token;
        try
        {
            int? chunks;
            if (IndexHandler is { } handler)
            {
                chunks = await handler(source, new Progress<double>(p =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        source.Progress = Math.Clamp(p, 0, 1);
                    }
                }), token);
            }
            else
            {
                const int steps = 12;
                var step = TimeSpan.FromTicks(Math.Max(1, SimulatedIndex.Ticks / steps));
                for (var i = 1; i <= steps; i++)
                {
                    await Delay(step, token);
                    source.Progress = (double)i / steps;
                }
                chunks = SimulatedChunks(source);
            }
            token.ThrowIfCancellationRequested();
            source.ChunkCount = chunks;
            source.Progress = 1;
            source.Status = AgentBuilderSourceStatus.Ready;
            if (announce)
            {
                Announce(Labels.SourceReadyAnnouncement(source.Name));
            }
            RaiseEvent(new AgentBuilderSourceEventArgs(SourceIndexedEvent, this, source));
        }
        catch (OperationCanceledException)
        {
            // Removed, retried or the builder left: whoever cancelled decides what the source shows.
        }
        catch (Exception ex)
        {
            source.ErrorMessage = ex.Message;
            source.Status = AgentBuilderSourceStatus.Failed;
            Announce(Labels.SourceFailedAnnouncement(source.Name, ex.Message));
            RaiseEvent(new AgentBuilderSourceEventArgs(SourceIndexedEvent, this, source));
        }
        finally
        {
            if (_indexing.TryGetValue(source, out var current) && ReferenceEquals(current, run))
            {
                _indexing.Remove(source);
                run.Dispose();
            }
        }
        UpdateState();
    }

    /// <summary>An invented, stable passage count for a simulated index (one per ~5 KB of a file, a few dozen for a page).</summary>
    internal static int SimulatedChunks(AgentBuilderSource source) =>
        source.Size is { } size ? (int)Math.Clamp(size / 5_000, 3, 4_000) : 12 + Math.Abs(ToneOf(source.Name) * 7 + source.Name.Length) % 40;

    private void ShowLinkError(string? message)
    {
        if (_linkInput is not null)
        {
            _linkInput.ErrorMessage = message;
        }
    }

    private void OnLinkKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            AddLink(_linkInput?.Text ?? string.Empty);
        }
    }

    private void OnLinkTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_linkInput?.ErrorMessage is not null)
        {
            ShowLinkError(null);
        }
    }

    private void OnAddLinkClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (!AddLink(_linkInput?.Text ?? string.Empty))
        {
            _linkInput?.Focus(NavigationMethod.Tab);
        }
    }

    private async void OnUploadClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var token = _lifetime?.Token ?? CancellationToken.None;
        try
        {
            IReadOnlyList<AgentBuilderSource> picked;
            if (PickFilesHandler is { } handler)
            {
                picked = await handler(token);
            }
            else if (TopLevel.GetTopLevel(this)?.StorageProvider is { CanOpen: true } storage)
            {
                var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = true, Title = Labels.Upload });
                var list = new List<AgentBuilderSource>();
                foreach (var file in files)
                {
                    long? size = null;
                    try
                    {
                        var properties = await file.GetBasicPropertiesAsync();
                        size = properties.Size is { } s ? (long)s : null;
                    }
                    catch (Exception)
                    {
                        // A file whose size cannot be read is still a source; the row leaves the size out.
                    }
                    list.Add(new AgentBuilderSource { Name = file.Name, Kind = AgentBuilderSourceKind.File, Size = size });
                }
                picked = list;
            }
            else
            {
                return;
            }
            if (!token.IsCancellationRequested)
            {
                AddSources(picked);
            }
        }
        catch (OperationCanceledException)
        {
            // The builder left while the picker was open.
        }
    }

    #endregion

    #region Save, discard and publish

    /// <summary>Saves the draft (through <see cref="SaveHandler"/> or a short simulation). Returns whether it was saved.</summary>
    public async Task<bool> SaveAsync()
    {
        if (!IsDirty || !CanSave(allowClean: false))
        {
            return false;
        }
        var config = BuildConfig();
        IsSaving = true;
        SaveError = null;
        UpdateVisuals();
        try
        {
            await Run(SaveHandler, config);
        }
        catch (OperationCanceledException)
        {
            IsSaving = false;
            UpdateState();
            return false;
        }
        catch (Exception ex)
        {
            IsSaving = false;
            SaveError = ex.Message;
            UpdateState();
            return false;
        }
        IsSaving = false;
        SetCurrentValue(SavedConfigProperty, config);
        RaiseEvent(new AgentBuilderSavedEventArgs(SavedEvent, this, config));
        Announce(Labels.SavedAnnouncement);
        UpdateState();
        return true;
    }

    /// <summary>Returns to the saved settings: fields, model, tools and sources.</summary>
    public void Discard()
    {
        if (SavedConfig is not { } saved || IsSaving)
        {
            return;
        }
        var wasDirty = IsDirty;
        Apply(saved);
        SaveError = null;
        UpdateState();
        if (wasDirty)
        {
            RaiseEvent(new AgentBuilderConfigChangedEventArgs(ConfigChangedEvent, this, AgentBuilderField.All, Config, IsDirty));
            Announce(Labels.DiscardedAnnouncement);
        }
    }

    /// <summary>
    /// Saves the draft if needed, then publishes the saved settings (through <see cref="PublishHandler"/> or a short
    /// simulation). Returns whether it was published.
    /// </summary>
    public async Task<bool> PublishAsync()
    {
        if (!CanSave(allowClean: true) || PublishState == AgentBuilderPublishState.Published)
        {
            return false;
        }
        _publishing = true;
        try
        {
            if (IsDirty && !await SaveAsync())
            {
                return false;
            }
            if (SavedConfig is not { } saved)
            {
                return false;
            }
            IsSaving = true;
            SaveError = null;
            UpdateVisuals();
            try
            {
                await Run(PublishHandler, saved);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception ex)
            {
                SaveError = ex.Message;
                return false;
            }
            finally
            {
                IsSaving = false;
            }
            SetCurrentValue(PublishedConfigProperty, saved);
            RaiseEvent(new AgentBuilderSavedEventArgs(PublishedEvent, this, saved));
            Announce(Labels.PublishedAnnouncement(string.IsNullOrWhiteSpace(saved.Name) ? Labels.UntitledAgent : saved.Name.Trim()));
            return true;
        }
        finally
        {
            _publishing = false;
            UpdateState();
        }
    }

    private Task Run(Func<AgentBuilderConfig, CancellationToken, Task>? handler, AgentBuilderConfig config)
    {
        var token = _lifetime?.Token ?? CancellationToken.None;
        return handler is not null ? handler(config, token) : Delay(SimulatedSave, token);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = SaveAsync();
    }

    private void OnDiscardClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        var keyboard = _discard?.IsKeyboardFocusWithin == true;
        Discard();
        if (keyboard)
        {
            // Discard and Save are disabled now; keep the focus on something that still works.
            _nameInput?.Focus(NavigationMethod.Tab);
        }
    }

    private void OnPublishClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _ = PublishAsync();
    }

    #endregion

    #region Test chat

    /// <summary>Stops the test chat and starts an empty one; its next message uses the current draft.</summary>
    public void RestartTest()
    {
        var keyboard = _restart?.IsKeyboardFocusWithin == true;
        TestConfig = null;
        if (_chat is { } chat)
        {
            chat.StopReply();
            chat.Conversations.Clear();
            chat.NewChat();
        }
        UpdateState();
        Announce(Labels.RestartedAnnouncement);
        if (keyboard && _chat?.Composer is { } composer)
        {
            composer.FocusInput(NavigationMethod.Tab);
        }
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        RestartTest();
    }

    private void OnChatNewChatRequested(object? sender, RoutedEventArgs e)
    {
        // The chat's own New chat button is a restart here: there is no history to keep.
        e.Handled = true;
        RestartTest();
    }

    private void OnChatConversationCreated(object? sender, AiChatConversationEventArgs e)
    {
        TestConfig = BuildConfig();
        UpdateState();
    }

    /// <summary>The settings, model and tools the test chat answers with.</summary>
    internal AgentBuilderTestRequest TestRequest(AiChatRequest request)
    {
        var config = TestConfig ?? BuildConfig();
        var tools = Tools.Where(t => config.HasTool(t.Id)).ToList();
        return new AgentBuilderTestRequest(config, ModelById(config.ModelId), tools, request);
    }

    internal IAgentBuilderResponder EffectiveResponder => Responder ?? new AgentBuilderSimulatedResponder(Delay);

    #endregion

    private void Announce(string text) => _announcer.Announce(text);
}

/// <summary>Connects the test chat to the builder: each reply is answered with the settings the chat started with.</summary>
internal sealed class AgentBuilderChatResponder(AgentBuilder builder) : IAiChatResponder
{
    public async IAsyncEnumerable<AiChatStreamEvent> RespondAsync(AiChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var test = builder.TestRequest(request);
        await foreach (var item in builder.EffectiveResponder.RespondAsync(test, cancellationToken).WithCancellation(cancellationToken))
        {
            yield return item;
        }
    }
}
