using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc;

public partial class PromptStudio
{
    private readonly AvaloniaList<string> _effective = new();
    private readonly AvaloniaList<string> _missing = new();
    private readonly AvaloniaList<string> _offered = new();
    private bool _offerPosted;
    private PromptVersion? _draftSource;
    private bool _draftEdited;
    private bool _loadingDraft;
    private bool _pushingDraft;
    private bool _editing;
    private bool _loadingCase;
    private bool _syncingTabs;
    private bool _syncingCases;
    private bool _syncingModel;
    private bool _settingEffort;
    private bool _effortExplicit;

    /// <summary>
    /// The variables in the order of their colors and fields (read-only): <see cref="Variables"/>, then the names the
    /// template uses that are not listed, in order of appearance (system first, then user).
    /// </summary>
    public IReadOnlyList<string> EffectiveVariables => _effective;

    /// <summary>The variables whose value is empty (their chips are dashed).</summary>
    internal IReadOnlyList<string> MissingVariableNames => _missing;

    /// <summary>The version the editor shows: <see cref="OpenVersion"/>, or the first (newest) one.</summary>
    internal PromptVersion? EffectiveOpenVersion => OpenVersion ?? (Versions.Count > 0 ? Versions[0] : null);

    /// <summary>The version in production: <see cref="ProductionVersion"/>, or the open one.</summary>
    internal PromptVersion? EffectiveProductionVersion => ProductionVersion ?? EffectiveOpenVersion;

    /// <summary>The system message of the draft (never null).</summary>
    internal string SystemText => Draft.System ?? string.Empty;

    /// <summary>The user message of the draft (never null).</summary>
    internal string UserText => Draft.User ?? string.Empty;

    private string TextOf(PromptPart part) => part == PromptPart.System ? SystemText : UserText;

    /// <summary>The tone (1 to 4) of a variable: its place in <see cref="EffectiveVariables"/>, the four series colors in turn.</summary>
    private int ToneOf(string name) => PromptTone.ToneOf(name, _effective);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == DraftProperty)
        {
            OnDraftChanged();
        }
        else if (property == OpenVersionProperty)
        {
            // Opening a version puts its text in the editor.
            LoadDraft(EffectiveOpenVersion);
            UpdateHeader();
            UpdateCompare(announce: false);
        }
        else if (property == ProductionVersionProperty)
        {
            UpdateHeader();
        }
        else if (property == ActivePartProperty)
        {
            OnActivePartChanged();
        }
        else if (property == SelectedTestCaseProperty)
        {
            OnSelectedCaseChanged();
        }
        else if (property == SelectedModelProperty)
        {
            OnSelectedModelChanged(change.GetOldValue<AiModel?>(), change.GetNewValue<AiModel?>());
        }
        else if (property == EffortProperty)
        {
            if (!_settingEffort)
            {
                _effortExplicit = true;
            }
            UpdateModelParts();
        }
        else if (property == RunHandlerProperty)
        {
            UpdateRunTexts();
        }
        else if (property == TokenEstimatorProperty)
        {
            UpdateTokenCount();
        }
        else if (property == NowProperty)
        {
            if (_versionMenu is not null)
            {
                _versionMenu.Now = Now;
            }
        }
        else if (property == IsCompareModeProperty || property == CompareVersionProperty)
        {
            UpdateCompare(announce: true);
        }
        else if (property == LabelsProperty)
        {
            ApplyLabels();
        }
        else if (property == TitleProperty)
        {
            AutomationProperties.SetName(this, Labels.CardName(Title ?? string.Empty));
        }
        else if (property == ForegroundProperty || property == SuccessBrushProperty || property == DangerBrushProperty ||
                 property == SecondaryBrushProperty || property == MutedBrushProperty)
        {
            ApplyColors();
        }
        else if (property == CompactWidthProperty || property == NarrowWidthProperty || property == TwoColumnWidthProperty ||
                 property == OneColumnWidthProperty || property == MaxWidthProperty)
        {
            _measuredWidth = double.NaN;
            InvalidateMeasure();
        }
        else if (property == HintGapProperty)
        {
            _hintKey = null;
            UpdateHint();
        }
    }

    #region Draft

    /// <summary>Puts <paramref name="version"/>'s text (or nothing) in the draft; the draft counts as unedited.</summary>
    private void LoadDraft(PromptVersion? version)
    {
        _loadingDraft = true;
        try
        {
            SetCurrentValue(DraftProperty, version?.Template ?? new PromptTemplate(string.Empty, string.Empty));
        }
        finally
        {
            _loadingDraft = false;
        }
        _draftSource = version;
        _draftEdited = false;
    }

    /// <summary>
    /// The draft starts as the open version's text: while nobody has edited or set it, it follows the open version as the
    /// versions arrive (a draft the host set, or the person edited, stays).
    /// </summary>
    private void EnsureDraftLoaded()
    {
        if (_draftEdited)
        {
            return;
        }
        var open = EffectiveOpenVersion;
        if (ReferenceEquals(open, _draftSource) && Draft.System is not null && Draft.User is not null)
        {
            return;
        }
        LoadDraft(open);
    }

    private void OnDraftChanged()
    {
        if (!_loadingDraft && !_pushingDraft)
        {
            _draftEdited = true;
        }
        PushDraftToEditors();
        RefreshNames();
        UpdateTokenCount();
        if (IsCompareMode)
        {
            UpdateCompareDiff();
            UpdateTabDots();
        }
    }

    /// <summary>The editors show the draft (an editor that already holds the text is left alone, with its undo steps).</summary>
    private void PushDraftToEditors()
    {
        _pushingDraft = true;
        try
        {
            if (_systemEditor is { } system && !string.Equals(system.Text, SystemText, StringComparison.Ordinal))
            {
                system.Text = SystemText;
            }
            if (_userEditor is { } user && !string.Equals(user.Text, UserText, StringComparison.Ordinal))
            {
                user.Text = UserText;
            }
        }
        finally
        {
            _pushingDraft = false;
        }
    }

    /// <summary>An editor's text changed: the draft follows; a change the person made is announced through <see cref="TemplateChanged"/>.</summary>
    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != PromptTemplateEditor.TextProperty)
        {
            return;
        }
        // Each editor holds its own part; the other part stays as the draft has it (it may not be pushed yet).
        var system = ReferenceEquals(sender, _systemEditor) ? _systemEditor!.Text : SystemText;
        var user = ReferenceEquals(sender, _userEditor) ? _userEditor!.Text : UserText;
        if (string.Equals(system, SystemText, StringComparison.Ordinal) && string.Equals(user, UserText, StringComparison.Ordinal))
        {
            return;
        }
        var template = new PromptTemplate(system, user);
        if (_pushingDraft)
        {
            // The editor tidied text the host set ({{ name }} → {{name}}): the draft takes the tidy form quietly.
            SetCurrentValue(DraftProperty, template);
            return;
        }
        _editing = true;
        try
        {
            SetCurrentValue(DraftProperty, template);
        }
        finally
        {
            _editing = false;
        }
        RaiseEvent(new PromptTemplateChangedEventArgs(TemplateChangedEvent, this, template));
    }

    #endregion

    #region Variables

    /// <summary>
    /// The effective variables: <see cref="Variables"/> then the template's other names in order of appearance. The list is
    /// changed in place (fields and chips keep their state) and only when it really changes.
    /// </summary>
    private void RefreshNames()
    {
        var names = new List<string>();
        foreach (var name in Variables)
        {
            if (!string.IsNullOrEmpty(name) && !names.Contains(name))
            {
                names.Add(name);
            }
        }
        foreach (var name in PromptTemplateText.Names(SystemText))
        {
            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }
        foreach (var name in PromptTemplateText.Names(UserText))
        {
            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }
        if (Patch(_effective, names))
        {
            RefreshMissing();
        }
        if (_editing)
        {
            // The editor reports a name the person just added (VariableAdded) only while its list does not hold it yet:
            // the list it offers catches up after the editor's own pass over the edit.
            PostOffer();
        }
        else
        {
            Patch(_offered, names);
        }
    }

    private void PostOffer()
    {
        if (_offerPosted)
        {
            return;
        }
        _offerPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _offerPosted = false;
            Patch(_offered, new List<string>(_effective));
        }, DispatcherPriority.Background);
    }

    /// <summary>The variables without a value (dashed chips).</summary>
    private void RefreshMissing()
    {
        var missing = new List<string>();
        foreach (var name in _effective)
        {
            if (!Values.TryGetValue(name, out var value) || string.IsNullOrEmpty(value))
            {
                missing.Add(name);
            }
        }
        Patch(_missing, missing);
    }

    /// <summary>Makes <paramref name="list"/> equal to <paramref name="target"/> with the fewest removals, moves and inserts; false when it already was.</summary>
    private static bool Patch(AvaloniaList<string> list, List<string> target)
    {
        if (list.Count == target.Count)
        {
            var same = true;
            for (var i = 0; i < target.Count; i++)
            {
                if (!string.Equals(list[i], target[i], StringComparison.Ordinal))
                {
                    same = false;
                    break;
                }
            }
            if (same)
            {
                return false;
            }
        }
        for (var i = list.Count - 1; i >= 0; i--)
        {
            if (!target.Contains(list[i]))
            {
                list.RemoveAt(i);
            }
        }
        for (var i = 0; i < target.Count; i++)
        {
            if (i < list.Count && string.Equals(list[i], target[i], StringComparison.Ordinal))
            {
                continue;
            }
            var at = list.IndexOf(target[i]);
            if (at >= 0)
            {
                list.Move(at, i);
            }
            else
            {
                list.Insert(i, target[i]);
            }
        }
        return true;
    }

    private void OnValuesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_loadingCase)
        {
            return;
        }
        RefreshMissing();
        UpdateTokenCount();
    }

    /// <summary>The estimate of the shown part's tokens, with the values filled in.</summary>
    private void UpdateTokenCount()
    {
        if (_tokens is null)
        {
            return;
        }
        _tokens.Count = Math.Max(0, Estimate(PromptStudioText.Render(TextOf(ActivePart), Values)));
    }

    /// <summary>The tokens of <paramref name="text"/> by <see cref="TokenEstimator"/> (about a quarter of the characters by default).</summary>
    private int Estimate(string text) => (TokenEstimator ?? PromptStudioText.EstimateTokens)(text);

    private void OnFieldsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != PromptVariableFields.ActiveVariableProperty)
        {
            return;
        }
        // A focused field highlights its chips.
        var active = _fields?.ActiveVariable;
        foreach (var editor in Editors())
        {
            editor.ActiveVariable = active;
        }
    }

    /// <summary>Each key in a field pulses its chips once.</summary>
    private void OnValueEdited(object? sender, PromptVariableEventArgs e)
    {
        foreach (var editor in Editors())
        {
            editor.FlashVariable(e.Name);
        }
    }

    /// <summary>A chip was clicked: its field takes the focus with the caret at the end.</summary>
    private void OnChipInvoked(object? sender, PromptChipEventArgs e)
    {
        e.Handled = true;
        _fields?.FocusField(e.Name);
    }

    /// <summary>The person added a variable in the editor ("+ Add", <c>name}}</c> or a paste): it already has its field.</summary>
    private void OnEditorVariableAdded(object? sender, PromptChipEventArgs e)
    {
        e.Handled = true;
        RefreshNames();
        Announce(Labels.AnnounceVariableAdded(e.Name));
        RaiseEvent(new PromptVariableAddedEventArgs(VariableAddedEvent, this, e.Name));
    }

    #endregion

    #region Tabs

    private void OnTabsPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PromptPartTabs.ActivePartProperty && !_syncingTabs && _tabs is not null)
        {
            SetCurrentValue(ActivePartProperty, _tabs.ActivePart);
        }
    }

    private void OnActivePartChanged()
    {
        if (_tabs is not null)
        {
            _syncingTabs = true;
            try
            {
                _tabs.ActivePart = ActivePart;
            }
            finally
            {
                _syncingTabs = false;
            }
        }
        UpdatePanes();
        UpdateTokenCount();
        if (IsCompareMode)
        {
            UpdateCompareDiff();
        }
    }

    /// <summary>The shown part's editor, or (comparing) the diff; the others fade out and go inert.</summary>
    private void UpdatePanes()
    {
        var comparing = IsCompareMode && EffectiveCompareVersion is not null;
        foreach (var editor in Editors())
        {
            var shown = !comparing && ReferenceEquals(editor, EditorOf(ActivePart));
            if (!shown && editor.IsKeyboardFocusWithin)
            {
                // The pane goes inert under the focus: the tab of the shown part takes it.
                _tabs?.FocusActive(FocusMethod);
            }
        }
        _systemFade?.SetShown(!comparing && ActivePart == PromptPart.System);
        _userFade?.SetShown(!comparing && ActivePart == PromptPart.User);
    }

    #endregion

    #region Test cases

    /// <summary>The first test case and the first model are selected while nothing is.</summary>
    private void EnsureSelections()
    {
        if (SelectedTestCase is null && TestCases.Count > 0)
        {
            SetCurrentValue(SelectedTestCaseProperty, TestCases[0]);
        }
        if (SelectedModel is null && Models.Count > 0)
        {
            SetCurrentValue(SelectedModelProperty, Models[0]);
        }
    }

    private void OnSelectedCaseChanged()
    {
        if (SelectedTestCase is { } testCase)
        {
            LoadCase(testCase);
        }
        UpdateCaseParts();
    }

    /// <summary>Choosing a test case resets every value to the case's own (edits are dropped).</summary>
    private void LoadCase(PromptTestCase testCase)
    {
        _loadingCase = true;
        try
        {
            Values.Clear();
            foreach (var pair in testCase.Values)
            {
                Values[pair.Key] = pair.Value;
            }
        }
        finally
        {
            _loadingCase = false;
        }
        RefreshMissing();
        UpdateTokenCount();
    }

    private void UpdateCaseParts()
    {
        if (_cases is not null)
        {
            _syncingCases = true;
            try
            {
                _cases.SelectedCase = SelectedTestCase;
            }
            finally
            {
                _syncingCases = false;
            }
        }
        UpdateRunTexts();
    }

    private void OnCasesPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PromptTestCases.SelectedCaseProperty && !_syncingCases && _cases is not null)
        {
            SetCurrentValue(SelectedTestCaseProperty, _cases.SelectedCase);
        }
    }

    /// <summary>The person picked a test case: say that its values are in.</summary>
    private void OnCaseChosen(object? sender, RoutedEventArgs e)
    {
        if (SelectedTestCase is { } testCase)
        {
            Announce(Labels.AnnounceCaseLoaded(testCase.Label));
        }
    }

    #endregion

    #region Models

    /// <summary>
    /// A new model brings its default effort. The first model set while an effort was chosen explicitly keeps that effort
    /// (moved to the nearest level the model offers).
    /// </summary>
    private void OnSelectedModelChanged(AiModel? oldModel, AiModel? newModel)
    {
        if (newModel is { } model)
        {
            var keep = oldModel is null && _effortExplicit;
            var effort = ModelEffortSnap.Snap(keep ? Effort : null, model.EffortLevels, model.DefaultEffort);
            if (effort is { } next)
            {
                SetEffort(next);
            }
        }
        UpdateModelParts();
    }

    private void SetEffort(ModelEffort effort)
    {
        _settingEffort = true;
        try
        {
            SetCurrentValue(EffortProperty, effort);
        }
        finally
        {
            _settingEffort = false;
        }
    }

    /// <summary>The effort the selected model runs at, or null for a model without effort levels.</summary>
    private ModelEffort? ShownEffort => SelectedModel is { SupportsEffort: true } ? Effort : null;

    private void UpdateModelParts()
    {
        var model = SelectedModel;
        if (_modelPill is not null)
        {
            _modelPill.IsVisible = model is not null;
            _modelPill.Model = model;
            _modelPill.Effort = ShownEffort;
        }
        if (_modelMenu is not null)
        {
            _syncingModel = true;
            try
            {
                _modelMenu.SelectedModel = model;
                _modelMenu.Effort = ShownEffort;
            }
            finally
            {
                _syncingModel = false;
            }
            if (model is null && _modelMenu.IsOpen)
            {
                _modelMenu.Close();
            }
        }
    }

    private void OnModelMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_modelMenu is null)
        {
            return;
        }
        if (e.Property == PromptPopoverMenu.IsOpenProperty)
        {
            if (_modelPill is not null)
            {
                _modelPill.IsOpen = _modelMenu.IsOpen;
            }
            return;
        }
        if (_syncingModel)
        {
            return;
        }
        if (e.Property == PromptModelMenu.SelectedModelProperty)
        {
            SetCurrentValue(SelectedModelProperty, _modelMenu.SelectedModel);
        }
        else if (e.Property == PromptModelMenu.EffortProperty && _modelMenu.Effort is { } effort)
        {
            SetCurrentValue(EffortProperty, effort);
        }
    }

    /// <summary>The person picked a model: say which, with the effort it brings.</summary>
    private void OnModelChosen(object? sender, ModelMenuModelEventArgs e)
    {
        if (!e.IsChange)
        {
            return;
        }
        var model = e.Model;
        var effort = ModelEffortSnap.Snap(null, model.EffortLevels, model.DefaultEffort);
        Announce(Labels.AnnounceModel(model.Name, effort is { } level ? Labels.EffortName(level) : null));
    }

    /// <summary>The person picked an effort.</summary>
    private void OnEffortChosen(object? sender, ModelMenuEffortEventArgs e)
    {
        if (!e.IsSnap && e.Effort is { } effort)
        {
            Announce(Labels.AnnounceEffort(Labels.EffortName(effort)));
        }
    }

    #endregion
}
