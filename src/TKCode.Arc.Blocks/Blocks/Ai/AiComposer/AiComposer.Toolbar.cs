using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

public partial class AiComposer
{
    private readonly List<ToolView> _toolViews = new();
    private readonly ModelMenu _menu = new()
    {
        EffortLayout = ModelMenuEffortLayout.Inline,
        ShowMoreModels = false,
        Placement = PlacementMode.TopEdgeAlignedRight
    };
    // The menu is the one place that resolves the default model and snaps the effort. Our properties mirror it: a change
    // coming from the menu is not pushed back, and a push we start does not echo the property we pushed.
    private bool _fromMenu;
    private AvaloniaProperty? _pushing;
    private bool _syncingTool;

    /// <summary>A toolbar toggle and the tool it shows.</summary>
    private sealed record ToolView(AiComposerTool Tool, AiToolToggle Toggle);

    #region Tools

    /// <summary>The toggles of the toolbar (tests).</summary>
    internal IReadOnlyList<AiToolToggle> ToolToggles => _toolViews.Select(v => v.Toggle).ToList();

    private void OnToolsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildTools();

    /// <summary>One <see cref="AiToolToggle"/> per tool, after the plus button, in order.</summary>
    private void RebuildTools()
    {
        ClearToolViews();
        if (_toolsPanel is not { } panel)
        {
            return;
        }
        foreach (var tool in Tools)
        {
            if (tool is null)
            {
                continue;
            }
            var toggle = new AiToolToggle { IsCompact = _isCompact };
            var view = new ToolView(tool, toggle);
            SyncToggle(view);
            toggle.IsCheckedChanged += OnToggleCheckedChanged;
            if (this.IsAttachedToVisualTree())
            {
                tool.PropertyChanged += OnToolPropertyChanged;
            }
            _toolViews.Add(view);
            panel.Children.Add(toggle);
        }
    }

    private void ClearToolViews()
    {
        foreach (var view in _toolViews)
        {
            view.Toggle.IsCheckedChanged -= OnToggleCheckedChanged;
            view.Tool.PropertyChanged -= OnToolPropertyChanged;
            if (view.Toggle.Parent is Panel parent)
            {
                parent.Children.Remove(view.Toggle);
            }
        }
        _toolViews.Clear();
    }

    /// <summary>
    /// The tools may belong to a longer-lived view model: the composer listens to them only while it is on screen (so
    /// they never keep a dropped composer alive) and catches up when it comes back.
    /// </summary>
    private void WatchTools(bool watch)
    {
        foreach (var view in _toolViews)
        {
            view.Tool.PropertyChanged -= OnToolPropertyChanged;
            if (watch)
            {
                view.Tool.PropertyChanged += OnToolPropertyChanged;
                SyncToggle(view);
            }
        }
    }

    private void SyncToggle(ToolView view)
    {
        var (tool, toggle) = view;
        _syncingTool = true;
        try
        {
            toggle.Label = tool.Label;
            toggle.Icon = tool.Icon;
            toggle.Tip = tool.Tip;
            toggle.IsChecked = tool.IsChecked;
        }
        finally
        {
            _syncingTool = false;
        }
    }

    private void OnToolPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not AiComposerTool tool || _toolViews.FirstOrDefault(v => ReferenceEquals(v.Tool, tool)) is not { } view)
        {
            return;
        }
        if (e.Property == AiComposerTool.IsCheckedProperty || e.Property == AiComposerTool.LabelProperty ||
            e.Property == AiComposerTool.IconProperty || e.Property == AiComposerTool.TipProperty)
        {
            SyncToggle(view);
        }
    }

    /// <summary>The person flipped a toggle: the tool follows, <see cref="ToolToggled"/> is raised and the state is announced.</summary>
    private void OnToggleCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_syncingTool || sender is not AiToolToggle toggle || _toolViews.FirstOrDefault(v => ReferenceEquals(v.Toggle, toggle)) is not { } view)
        {
            return;
        }
        var on = toggle.IsChecked == true;
        if (view.Tool.IsChecked == on)
        {
            return;
        }
        _syncingTool = true;
        try
        {
            view.Tool.IsChecked = on;
        }
        finally
        {
            _syncingTool = false;
        }
        RaiseEvent(new AiComposerToolToggledEventArgs(ToolToggledEvent, this, view.Tool));
        Announce(Labels.AnnounceTool(view.Tool.Label, on));
    }

    #endregion

    #region Model pill and menu

    /// <summary>The model menu (tests).</summary>
    internal ModelMenu ModelMenu => _menu;

    private void InitializeModelMenu()
    {
        _menu.ModelsSource = Models;
        _menu.AddHandler(ModelMenu.ModelChosenEvent, OnMenuModelChosen);
        _menu.AddHandler(ModelMenu.EffortChosenEvent, OnMenuEffortChosen);
        _menu.PropertyChanged += OnMenuPropertyChanged;
    }

    private void HookModelPill()
    {
        if (_modelPill is not null)
        {
            _modelPill.OpenRequested += OnModelPillOpenRequested;
        }
    }

    private void UnhookModelPill()
    {
        if (_modelPill is not null)
        {
            _modelPill.OpenRequested -= OnModelPillOpenRequested;
        }
    }

    /// <summary>
    /// The menu joins the tree next to the surface (it takes no space) after it was given the catalog and the declared
    /// selection: joining applies its default selection (the first model), which must not overwrite a declared one.
    /// </summary>
    private void AttachModelMenu()
    {
        _menu.PlacementTarget = _modelPill;
        _menu.Labels = Labels.ModelMenu;
        Mirror(Providers, _menu.Providers);
        if (SelectedModel is { } model)
        {
            Push(ModelMenu.SelectedModelProperty, () => _menu.SelectedModel = model);
        }
        if (Effort is { } effort)
        {
            Push(ModelMenu.EffortProperty, () => _menu.Effort = effort);
        }
        if (_host is not null && !_host.Children.Contains(_menu))
        {
            _host.Children.Add(_menu);
        }
    }

    private void DetachModelMenu()
    {
        _menu.Close(returnFocus: false);
        if (_menu.Parent is Panel parent)
        {
            parent.Children.Remove(_menu);
        }
    }

    private void OnModelsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateModelParts();

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e) => Mirror(Providers, _menu.Providers);

    /// <summary>The pill shows the selection while there are models; without models it is hidden and the menu closed.</summary>
    private void UpdateModelParts()
    {
        if (_modelPill is not { } pill)
        {
            return;
        }
        var hasModels = Models.Count > 0;
        pill.IsVisible = hasModels;
        pill.Model = SelectedModel;
        pill.Effort = Effort;
        pill.Providers = Providers;
        pill.IsOpen = _menu.IsOpen;
        if (!hasModels)
        {
            _menu.Close(returnFocus: false);
        }
    }

    private void UpdateModelLabels()
    {
        var labels = Labels;
        _menu.Labels = labels.ModelMenu;
        if (_modelPill is not null)
        {
            _modelPill.Labels = new ModelPillLabels
            {
                EffortName = labels.EffortName,
                AutomationName = (model, effort) => labels.ModelName(model.Name, labels.EffortName(effort).ToLowerInvariant()),
                AutomationNameWithoutEffort = model => labels.ModelName(model.Name, null)
            };
        }
    }

    private void OnModelPillOpenRequested(object? sender, ModelPillOpenRequestedEventArgs e)
    {
        e.Handled = true;
        if (_modelPill is { } pill)
        {
            _menu.Toggle(pill, e.Focus);
        }
    }

    private void Push(AvaloniaProperty property, Action action)
    {
        var previous = _pushing;
        _pushing = property;
        try
        {
            action();
        }
        finally
        {
            _pushing = previous;
        }
    }

    private static void Mirror<T>(AvaloniaList<T> source, AvaloniaList<T> target)
    {
        target.Clear();
        target.AddRange(source);
    }

    /// <summary>Code set <see cref="SelectedModel"/>: the menu takes it (and snaps the effort), <see cref="ModelChanged"/> is raised.</summary>
    private void OnSelectedModelChanged(AiModel? oldModel, AiModel? newModel)
    {
        if (_modelPill is not null)
        {
            _modelPill.Model = newModel;
        }
        if (_fromMenu)
        {
            return;
        }
        Push(ModelMenu.SelectedModelProperty, () => _menu.SelectedModel = newModel);
        if (!ReferenceEquals(oldModel, newModel))
        {
            RaiseEvent(new ModelPickerModelChangedEventArgs(ModelChangedEvent, this, oldModel, newModel));
        }
    }

    /// <summary>
    /// Code set <see cref="Effort"/>: a level the selected model lacks moves to the nearest one here (null to the model's
    /// default), so the event and the sent message report the level really in use.
    /// </summary>
    private void OnEffortChanged(ModelEffort? oldEffort, ModelEffort? newEffort)
    {
        if (_modelPill is not null)
        {
            _modelPill.Effort = newEffort;
        }
        if (_fromMenu)
        {
            return;
        }
        if (SelectedModel is { } selected && selected.SnapEffort(newEffort) is var valid && valid != newEffort)
        {
            SetCurrentValue(EffortProperty, valid);
            return;
        }
        Push(ModelMenu.EffortProperty, () => _menu.Effort = newEffort);
        if (oldEffort != newEffort)
        {
            RaiseEvent(new ModelPickerEffortChangedEventArgs(EffortChangedEvent, this, oldEffort, newEffort, isSnap: false));
        }
    }

    /// <summary>Mirrors the menu's resolved selection, effort and open state into our properties without pushing them back.</summary>
    private void OnMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(e.Property, _pushing))
        {
            return;
        }
        var was = _fromMenu;
        _fromMenu = true;
        try
        {
            if (e.Property == ModelMenu.SelectedModelProperty)
            {
                SetCurrentValue(SelectedModelProperty, _menu.SelectedModel);
            }
            else if (e.Property == ModelMenu.EffortProperty)
            {
                SetCurrentValue(EffortProperty, _menu.Effort);
            }
            else if (e.Property == ModelMenu.IsOpenProperty && _modelPill is not null)
            {
                _modelPill.IsOpen = _menu.IsOpen;
            }
        }
        finally
        {
            _fromMenu = was;
        }
    }

    private void OnMenuModelChosen(object? sender, ModelMenuModelEventArgs e)
    {
        e.Handled = true;
        if (!e.IsChange)
        {
            return;
        }
        RaiseEvent(new ModelPickerModelChangedEventArgs(ModelChangedEvent, this, e.PreviousModel, e.Model));
        Announce(Labels.AnnounceModel(e.Model.Name));
    }

    private void OnMenuEffortChosen(object? sender, ModelMenuEffortEventArgs e)
    {
        e.Handled = true;
        if (e.Effort == e.PreviousEffort)
        {
            return;
        }
        RaiseEvent(new ModelPickerEffortChangedEventArgs(EffortChangedEvent, this, e.PreviousEffort, e.Effort, e.IsSnap));
        // A snap follows a model change (announced) or code (not a person's choice): only a picked level is announced.
        if (!e.IsSnap && _pushing is null && e.Effort is { } effort)
        {
            Announce(Labels.AnnounceEffort(Labels.EffortName(effort)));
        }
    }

    #endregion

    #region Counter

    /// <summary>Whether the text is longer than <see cref="MaxLength"/>.</summary>
    internal bool IsOverLimit => MaxLength is { } max && max > 0 && (Text?.Length ?? 0) > max;

    private void UpdateCounter()
    {
        if (_counter is { } counter)
        {
            counter.CounterThreshold = CounterThreshold;
            counter.WarningThreshold = WarningThreshold;
            counter.Max = MaxLength is { } max && max > 0 ? max : 0;
            counter.Length = Text?.Length ?? 0;
        }
        PseudoClasses.Set(OverLimitPseudoClass, IsOverLimit);
    }

    #endregion

    #region Send and stop

    /// <summary>
    /// The send state: <c>Generating</c> while a reply runs; <c>Blocked</c> while an upload runs or the text is over the
    /// limit; <c>Ready</c> with text or a ready attachment (failed ones do not count); else <c>Empty</c>.
    /// </summary>
    private void UpdateSendState()
    {
        AiSendState state;
        if (IsGenerating)
        {
            state = AiSendState.Generating;
        }
        else
        {
            var uploading = false;
            var ready = false;
            foreach (var attachment in Attachments)
            {
                uploading |= attachment.Status == AiAttachmentStatus.Uploading;
                ready |= attachment.Status is AiAttachmentStatus.Ready or AiAttachmentStatus.Done;
            }
            var hasText = !string.IsNullOrWhiteSpace(Text);
            state = uploading || IsOverLimit ? AiSendState.Blocked
                : hasText || ready ? AiSendState.Ready
                : AiSendState.Empty;
        }
        SendState = state;
        PseudoClasses.Set(ReadyPseudoClass, state == AiSendState.Ready);
        PseudoClasses.Set(BlockedPseudoClass, state == AiSendState.Blocked);
        PseudoClasses.Set(GeneratingPseudoClass, state == AiSendState.Generating);
        if (_send is not null)
        {
            _send.State = state;
            _send.Progress = GenerationProgress;
        }
        UpdateSendNames();
    }

    /// <summary>The send button's name says what a press does, or why it does nothing.</summary>
    private void UpdateSendNames()
    {
        if (_send is not { } send)
        {
            return;
        }
        var labels = Labels;
        send.SendName = labels.Send;
        send.StopName = labels.Stop;
        send.EmptyName = labels.SendEmpty;
        send.BlockedName = IsOverLimit && MaxLength is { } max ? labels.SendOverLimit((Text?.Length ?? 0) - max) : labels.SendUploading;
    }

    private void OnSendClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (SendState == AiSendState.Generating)
        {
            RequestStop();
        }
        else
        {
            TrySend();
        }
    }

    /// <summary>
    /// Sends the prompt when it is <see cref="AiSendState.Ready"/>: raises <see cref="Sent"/>, runs <see cref="SendCommand"/>
    /// and empties the text, the tokens and the attachments (the tools and the model stay). Returns whether it sent.
    /// </summary>
    internal bool TrySend()
    {
        UpdateSendState();
        if (SendState != AiSendState.Ready)
        {
            return false;
        }
        var value = _editor is { } editor ? new AiComposerValue(editor.Text, ToTokens(editor.Tokens)) : Value;
        var attachments = Attachments.Where(a => a.Status is AiAttachmentStatus.Ready or AiAttachmentStatus.Done).ToArray();
        var tools = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var tool in Tools)
        {
            if (tool is not null && !string.IsNullOrEmpty(tool.Id))
            {
                tools[tool.Id] = tool.IsChecked;
            }
        }
        var model = Models.Count > 0 ? SelectedModel : null;
        var effort = model is not null ? Effort : null;
        var message = new AiComposerMessage(value, attachments, model, effort, tools);
        CloseSuggestions(dismiss: false, immediate: true);
        RaiseEvent(new AiComposerSentEventArgs(SentEvent, this, message));
        Run(SendCommand, message);
        Announce(Labels.AnnounceSent);
        // The sent attachments leave quietly (they live on in the message); a failed one left behind is a removal.
        if (_editor is { } input)
        {
            input.Clear();
        }
        else
        {
            SetCurrentValue(ValueProperty, AiComposerValue.Empty);
        }
        RemoveAllAttachments(raiseEvents: true, keep: attachments);
        return true;
    }

    /// <summary>The person stops the reply: <see cref="StopRequested"/> and <see cref="StopCommand"/>; the host ends <see cref="IsGenerating"/>.</summary>
    internal void RequestStop()
    {
        if (!IsGenerating)
        {
            return;
        }
        RaiseEvent(new RoutedEventArgs(StopRequestedEvent, this));
        Run(StopCommand, null);
        Announce(Labels.AnnounceStopped);
    }

    private static void Run(ICommand? command, object? parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    #endregion
}
