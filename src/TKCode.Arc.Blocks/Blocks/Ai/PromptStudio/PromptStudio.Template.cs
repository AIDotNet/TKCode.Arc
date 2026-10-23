using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

[TemplatePart(CardPart, typeof(Border))]
[TemplatePart(HeaderPart, typeof(Border))]
[TemplatePart(HeaderRowPart, typeof(PromptStudioHeaderPanel))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(VersionPillPart, typeof(PromptVersionPill))]
[TemplatePart(ModelPillPart, typeof(PromptModelPill))]
[TemplatePart(EditorSectionPart, typeof(Border))]
[TemplatePart(TabsPart, typeof(PromptPartTabs))]
[TemplatePart(TokensPart, typeof(PromptTokenCount))]
[TemplatePart(CompareCornerPart, typeof(Control))]
[TemplatePart(DiffStatsPart, typeof(Control))]
[TemplatePart(AddedPart, typeof(TextBlock))]
[TemplatePart(RemovedPart, typeof(TextBlock))]
[TemplatePart(BackPart, typeof(Button))]
[TemplatePart(SystemEditorPart, typeof(PromptTemplateEditor))]
[TemplatePart(UserEditorPart, typeof(PromptTemplateEditor))]
[TemplatePart(DiffPart, typeof(AiDiffText))]
[TemplatePart(HintPart, typeof(ArcContentSwap))]
[TemplatePart(VarsPart, typeof(Border))]
[TemplatePart(FieldsPart, typeof(PromptVariableFields))]
[TemplatePart(RunRowPart, typeof(Grid))]
[TemplatePart(CasesPart, typeof(PromptTestCases))]
[TemplatePart(RunButtonPart, typeof(PromptRunButton))]
[TemplatePart(OutputPart, typeof(PromptRunPanel))]
[TemplatePart(VersionMenuPart, typeof(PromptVersionMenu))]
[TemplatePart(ModelMenuPart, typeof(PromptModelMenu))]
[TemplatePart(StatusPart, typeof(AiAnnouncer))]
[PseudoClasses(CompactPseudoClass, NarrowPseudoClass, TwoColumnPseudoClass, OneColumnPseudoClass, ComparePseudoClass, BusyPseudoClass,
    ThinkingPseudoClass, StreamingPseudoClass, DonePseudoClass, StoppedPseudoClass, FailedPseudoClass)]
public partial class PromptStudio
{
    public const string CardPart = "PART_Card";
    public const string HeaderPart = "PART_Header";
    public const string HeaderRowPart = "PART_HeaderRow";
    public const string TitlePart = "PART_Title";
    public const string VersionPillPart = "PART_VersionPill";
    public const string ModelPillPart = "PART_ModelPill";
    public const string EditorSectionPart = "PART_EditorSection";
    public const string TabsPart = "PART_Tabs";
    public const string TokensPart = "PART_Tokens";
    public const string CompareCornerPart = "PART_CompareCorner";
    public const string DiffStatsPart = "PART_DiffStats";
    public const string AddedPart = "PART_Added";
    public const string RemovedPart = "PART_Removed";
    public const string BackPart = "PART_Back";
    public const string SystemEditorPart = "PART_SystemEditor";
    public const string UserEditorPart = "PART_UserEditor";
    public const string DiffPart = "PART_Diff";
    public const string HintPart = "PART_Hint";
    public const string VarsPart = "PART_Vars";
    public const string FieldsPart = "PART_Fields";
    public const string RunRowPart = "PART_RunRow";
    public const string CasesPart = "PART_Cases";
    public const string RunButtonPart = "PART_RunButton";
    public const string OutputPart = "PART_Output";
    public const string VersionMenuPart = "PART_VersionMenu";
    public const string ModelMenuPart = "PART_ModelMenu";
    public const string StatusPart = "PART_Status";

    /// <summary>Set while the card is 520 wide or less: tighter sides.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while the card is 480 wide or less: the test cases and Run stack.</summary>
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>Set while the card is 560 wide or less: the variable fields take two columns.</summary>
    public const string TwoColumnPseudoClass = ":two-col";

    /// <summary>Set while the card is 340 wide or less: the variable fields take one column.</summary>
    public const string OneColumnPseudoClass = ":one-col";

    /// <summary>Set while the editor shows the compare view.</summary>
    public const string ComparePseudoClass = ":compare";

    /// <summary>Set while a run is thinking or writing.</summary>
    public const string BusyPseudoClass = ":busy";

    /// <summary>Set while <see cref="RunState"/> is <see cref="PromptRunState.Thinking"/>.</summary>
    public const string ThinkingPseudoClass = ":thinking";

    /// <summary>Set while <see cref="RunState"/> is <see cref="PromptRunState.Streaming"/>.</summary>
    public const string StreamingPseudoClass = ":streaming";

    /// <summary>Set while <see cref="RunState"/> is <see cref="PromptRunState.Done"/>.</summary>
    public const string DonePseudoClass = ":done";

    /// <summary>Set while <see cref="RunState"/> is <see cref="PromptRunState.Stopped"/>.</summary>
    public const string StoppedPseudoClass = ":stopped";

    /// <summary>Set while <see cref="RunState"/> is <see cref="PromptRunState.Failed"/>.</summary>
    public const string FailedPseudoClass = ":failed";

    /// <summary>The compare view diffs whole <c>{{name}}</c> markers.</summary>
    private static readonly IReadOnlyList<string> s_atoms = ["{{", "}}"];

    #region Theme-driven internals

    /// <summary>Success (theme): the added-word count and the "+ added" key.</summary>
    internal static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<PromptStudio, IBrush?>(nameof(SuccessBrush));

    /// <summary>Danger (theme): the removed-word count and the "− removed" key.</summary>
    internal static readonly StyledProperty<IBrush?> DangerBrushProperty =
        AvaloniaProperty.Register<PromptStudio, IBrush?>(nameof(DangerBrush));

    /// <summary>TextSecondary (theme): "v3 to v4" in the compare hint.</summary>
    internal static readonly StyledProperty<IBrush?> SecondaryBrushProperty =
        AvaloniaProperty.Register<PromptStudio, IBrush?>(nameof(SecondaryBrush));

    /// <summary>TextMuted (theme): the hint line.</summary>
    internal static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<PromptStudio, IBrush?>(nameof(MutedBrush));

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<PromptStudio, double>(nameof(CompactWidth), 520);

    internal static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<PromptStudio, double>(nameof(NarrowWidth), 480);

    internal static readonly StyledProperty<double> TwoColumnWidthProperty =
        AvaloniaProperty.Register<PromptStudio, double>(nameof(TwoColumnWidth), 560);

    internal static readonly StyledProperty<double> OneColumnWidthProperty =
        AvaloniaProperty.Register<PromptStudio, double>(nameof(OneColumnWidth), 340);

    internal static readonly StyledProperty<double> HintGapProperty =
        AvaloniaProperty.Register<PromptStudio, double>(nameof(HintGap), 6);

    internal IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }
    internal IBrush? DangerBrush { get => GetValue(DangerBrushProperty); set => SetValue(DangerBrushProperty, value); }
    internal IBrush? SecondaryBrush { get => GetValue(SecondaryBrushProperty); set => SetValue(SecondaryBrushProperty, value); }
    internal IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }
    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }
    internal double NarrowWidth { get => GetValue(NarrowWidthProperty); set => SetValue(NarrowWidthProperty, value); }
    internal double TwoColumnWidth { get => GetValue(TwoColumnWidthProperty); set => SetValue(TwoColumnWidthProperty, value); }
    internal double OneColumnWidth { get => GetValue(OneColumnWidthProperty); set => SetValue(OneColumnWidthProperty, value); }
    internal double HintGap { get => GetValue(HintGapProperty); set => SetValue(HintGapProperty, value); }

    #endregion

    private Border? _header;
    private PromptStudioHeaderPanel? _headerRow;
    private PromptVersionPill? _versionPill;
    private PromptModelPill? _modelPill;
    private PromptPartTabs? _tabs;
    private PromptTokenCount? _tokens;
    private Control? _compareCorner;
    private Control? _diffStats;
    private TextBlock? _added;
    private TextBlock? _removed;
    private Button? _back;
    private PromptTemplateEditor? _systemEditor;
    private PromptTemplateEditor? _userEditor;
    private AiDiffText? _diff;
    private ArcContentSwap? _hint;
    private PromptVariableFields? _fields;
    private PromptTestCases? _cases;
    private PromptRunButton? _runButton;
    private PromptRunPanel? _output;
    private PromptVersionMenu? _versionMenu;
    private PromptModelMenu? _modelMenu;
    private AiAnnouncer? _status;
    private PromptPaneFade? _systemFade;
    private PromptPaneFade? _userFade;
    private AiFadePresence? _diffFade;
    private AiFadePresence? _tokensFade;
    private AiFadePresence? _compareFade;
    private bool _keyboardMode;
    private double _measuredWidth = double.NaN;

    static PromptStudio()
    {
        FocusableProperty.OverrideDefaultValue<PromptStudio>(false);
    }

    /// <summary>Creates an empty prompt studio.</summary>
    public PromptStudio()
    {
        // The command runs only while idle (a toolbar's Run does not restart a run); Run() itself replaces one.
        _runCommand.Execute = () =>
        {
            if (!IsBusy)
            {
                Run();
            }
        };
        _stopCommand.Execute = Stop;
        _stopCommand.CanExecuteNow = false;
        // Where the last input came from decides whether a moved focus shows the keyboard ring.
        AddHandler(KeyDownEvent, (_, _) => _keyboardMode = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, (_, _) => _keyboardMode = false, RoutingStrategies.Tunnel, handledEventsToo: true);
        // Mod+Enter runs from anywhere in the card, even where a part already used the key (the editors swallow it).
        AddHandler(KeyDownEvent, OnCardKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        Versions.CollectionChanged += OnVersionsChanged;
        Variables.CollectionChanged += OnVariablesChanged;
        TestCases.CollectionChanged += OnTestCasesChanged;
        Models.CollectionChanged += OnModelsChanged;
        Providers.CollectionChanged += OnProvidersChanged;
        Values.CollectionChanged += OnValuesChanged;
        AddHandler(PromptTemplateEditor.ChipInvokedEvent, OnChipInvoked);
        AddHandler(PromptTemplateEditor.VariableAddedEvent, OnEditorVariableAdded);
        AddHandler(PromptVariableFields.ValueEditedEvent, OnValueEdited);
        AddHandler(PromptRunButton.RunRequestedEvent, OnRunRequested);
        AddHandler(PromptRunButton.StopRequestedEvent, OnStopRequested);
        AddHandler(PromptRunPanel.RetryRequestedEvent, OnRetryRequested);
        AddHandler(PromptRunPanel.CopyRequestedEvent, OnCopyRequested);
        AddHandler(PromptRunPanel.RevealCompletedEvent, OnRevealCompleted);
        AddHandler(PromptTestCases.SelectionChangedEvent, OnCaseChosen);
        AddHandler(PromptVersionMenu.CompareRequestedEvent, OnCompareRequested);
        AddHandler(PromptVersionMenu.PromoteRequestedEvent, OnPromoteRequested);
        AddHandler(PromptVersionMenu.UndoPromoteRequestedEvent, OnUndoPromoteRequested);
        AddHandler(PromptModelMenu.ModelChosenEvent, OnModelChosen);
        AddHandler(PromptModelMenu.EffortChosenEvent, OnEffortChosen);
        AddHandler(PromptHeaderPill.OpenRequestedEvent, OnPillOpenRequested);
        UpdateRunClasses();
    }

    #region Test surface

    /// <summary>The last thing the live region said (tests).</summary>
    internal string? LastAnnouncement { get; private set; }

    internal PromptVersionPill? VersionPill => _versionPill;
    internal PromptModelPill? ModelPill => _modelPill;
    internal PromptPartTabs? Tabs => _tabs;
    internal PromptTokenCount? TokenCount => _tokens;
    internal Control? CompareCorner => _compareCorner;
    internal TextBlock? AddedText => _added;
    internal TextBlock? RemovedText => _removed;
    internal Button? BackButton => _back;
    internal PromptTemplateEditor? SystemEditor => _systemEditor;
    internal PromptTemplateEditor? UserEditor => _userEditor;
    internal AiDiffText? Diff => _diff;
    internal ArcContentSwap? Hint => _hint;
    internal PromptVariableFields? Fields => _fields;
    internal PromptTestCases? Cases => _cases;
    internal PromptRunButton? RunButton => _runButton;
    internal PromptRunPanel? Output => _output;
    internal PromptVersionMenu? VersionMenu => _versionMenu;
    internal PromptModelMenu? ModelMenu => _modelMenu;
    internal PromptStudioHeaderPanel? HeaderRow => _headerRow;
    internal AiAnnouncer? StatusRegion => _status;

    /// <summary>The editor of <paramref name="part"/>.</summary>
    internal PromptTemplateEditor? EditorOf(PromptPart part) => part == PromptPart.System ? _systemEditor : _userEditor;

    #endregion

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        UnhookParts();
        base.OnApplyTemplate(e);
        var scope = e.NameScope;
        _header = scope.Find<Border>(HeaderPart);
        _headerRow = scope.Find<PromptStudioHeaderPanel>(HeaderRowPart);
        _versionPill = scope.Find<PromptVersionPill>(VersionPillPart);
        _modelPill = scope.Find<PromptModelPill>(ModelPillPart);
        _tabs = scope.Find<PromptPartTabs>(TabsPart);
        _tokens = scope.Find<PromptTokenCount>(TokensPart);
        _compareCorner = scope.Find<Control>(CompareCornerPart);
        _diffStats = scope.Find<Control>(DiffStatsPart);
        _added = scope.Find<TextBlock>(AddedPart);
        _removed = scope.Find<TextBlock>(RemovedPart);
        _back = scope.Find<Button>(BackPart);
        _systemEditor = scope.Find<PromptTemplateEditor>(SystemEditorPart);
        _userEditor = scope.Find<PromptTemplateEditor>(UserEditorPart);
        _diff = scope.Find<AiDiffText>(DiffPart);
        _hint = scope.Find<ArcContentSwap>(HintPart);
        _fields = scope.Find<PromptVariableFields>(FieldsPart);
        _cases = scope.Find<PromptTestCases>(CasesPart);
        _runButton = scope.Find<PromptRunButton>(RunButtonPart);
        _output = scope.Find<PromptRunPanel>(OutputPart);
        _versionMenu = scope.Find<PromptVersionMenu>(VersionMenuPart);
        _modelMenu = scope.Find<PromptModelMenu>(ModelMenuPart);
        _status = scope.Find<AiAnnouncer>(StatusPart);
        HookParts();

        _hintKey = null;
        _measuredWidth = double.NaN;
        EnsureDraftLoaded();
        EnsureSelections();
        ApplyLabels();
        ApplyColors();
        PushDraftToEditors();
        RefreshNames();
        RefreshMissing();
        UpdateTokenCount();
        UpdateHeader();
        UpdateModelParts();
        UpdateCaseParts();
        UpdateCompare(announce: false);
        UpdateRunParts();
    }

    private void HookParts()
    {
        if (_tabs is not null)
        {
            _tabs.ActivePart = ActivePart;
            _tabs.PropertyChanged += OnTabsPropertyChanged;
        }
        foreach (var editor in Editors())
        {
            editor.Variables = _offered;
            editor.MissingVariables = _missing;
            editor.ToneSelector = ToneOf;
            editor.PropertyChanged += OnEditorPropertyChanged;
        }
        if (_systemEditor is not null)
        {
            _systemFade = new PromptPaneFade(_systemEditor, shown: ActivePart == PromptPart.System && !IsCompareMode);
        }
        if (_userEditor is not null)
        {
            _userFade = new PromptPaneFade(_userEditor, shown: ActivePart == PromptPart.User && !IsCompareMode);
        }
        if (_diff is not null)
        {
            _diff.Atoms = s_atoms;
            _diff.ChipTone = ToneOf;
            _diff.PropertyChanged += OnDiffPropertyChanged;
            _diffFade = new AiFadePresence(_diff, shown: false);
        }
        if (_tokens is not null)
        {
            _tokensFade = new AiFadePresence(_tokens, shown: true);
        }
        if (_compareCorner is not null)
        {
            _compareFade = new AiFadePresence(_compareCorner, shown: false);
        }
        if (_back is not null)
        {
            _back.Click += OnBackClick;
        }
        if (_fields is not null)
        {
            _fields.Variables = _effective;
            _fields.Values = Values;
            _fields.PropertyChanged += OnFieldsPropertyChanged;
        }
        if (_cases is not null)
        {
            _cases.Cases = TestCases;
            _cases.PropertyChanged += OnCasesPropertyChanged;
        }
        if (_versionMenu is not null)
        {
            _versionMenu.PlacementTarget = _versionPill;
            _versionMenu.Versions = Versions;
            _versionMenu.PropertyChanged += OnVersionMenuPropertyChanged;
        }
        if (_modelMenu is not null)
        {
            _modelMenu.PlacementTarget = _modelPill;
            _modelMenu.Models = Models;
            _modelMenu.Providers = Providers;
            _modelMenu.PropertyChanged += OnModelMenuPropertyChanged;
        }
        if (_modelPill is not null)
        {
            _modelPill.Providers = Providers;
        }
    }

    private void UnhookParts()
    {
        if (_tabs is not null)
        {
            _tabs.PropertyChanged -= OnTabsPropertyChanged;
        }
        foreach (var editor in Editors())
        {
            editor.PropertyChanged -= OnEditorPropertyChanged;
        }
        _systemFade?.Dispose();
        _userFade?.Dispose();
        _diffFade?.Dispose();
        _tokensFade?.Dispose();
        _compareFade?.Dispose();
        _systemFade = null;
        _userFade = null;
        _diffFade = null;
        _tokensFade = null;
        _compareFade = null;
        if (_diff is not null)
        {
            _diff.PropertyChanged -= OnDiffPropertyChanged;
        }
        if (_back is not null)
        {
            _back.Click -= OnBackClick;
        }
        if (_fields is not null)
        {
            _fields.PropertyChanged -= OnFieldsPropertyChanged;
        }
        if (_cases is not null)
        {
            _cases.PropertyChanged -= OnCasesPropertyChanged;
        }
        if (_versionMenu is not null)
        {
            _versionMenu.PropertyChanged -= OnVersionMenuPropertyChanged;
        }
        if (_modelMenu is not null)
        {
            _modelMenu.PropertyChanged -= OnModelMenuPropertyChanged;
        }
    }

    private IEnumerable<PromptTemplateEditor> Editors()
    {
        if (_systemEditor is not null)
        {
            yield return _systemEditor;
        }
        if (_userEditor is not null)
        {
            yield return _userEditor;
        }
    }

    #endregion

    #region Words

    /// <summary>Every word on screen and every accessible name, from <see cref="Labels"/>.</summary>
    private void ApplyLabels()
    {
        var labels = Labels;
        AutomationProperties.SetName(this, labels.CardName(Title ?? string.Empty));
        if (_versionPill is not null)
        {
            _versionPill.ProductionText = labels.Production;
            _versionPill.LatestText = labels.Latest;
            _versionPill.NameFormat = labels.VersionButton;
        }
        if (_modelPill is not null)
        {
            _modelPill.EffortText = labels.EffortName;
            _modelPill.NameFormat = labels.ModelButton;
        }
        if (_tabs is not null)
        {
            _tabs.SystemText = labels.SystemTab;
            _tabs.UserText = labels.UserTab;
            _tabs.ChangedName = labels.ChangedPart;
            _tabs.AccessibleName = labels.PartsName;
        }
        if (_tokens is not null)
        {
            _tokens.Format = labels.Tokens;
        }
        if (_back is not null)
        {
            _back.Content = labels.BackToEditing;
        }
        var help = labels.TypeToAddBefore + " {{ " + labels.TypeToAddAfter;
        ApplyEditorLabels(_systemEditor, labels, labels.SystemEditorName, labels.SystemPlaceholder, help);
        ApplyEditorLabels(_userEditor, labels, labels.UserEditorName, labels.UserPlaceholder, help);
        if (_fields is not null)
        {
            _fields.EmptyText = labels.EmptyValue;
            _fields.AccessibleName = labels.VariablesName;
        }
        if (_cases is not null)
        {
            _cases.AccessibleName = labels.TestCasesName;
        }
        if (_runButton is not null)
        {
            _runButton.RunText = labels.Run;
            _runButton.StopText = labels.Stop;
            _runButton.RunAccessibleName = labels.RunPrompt;
            _runButton.StopAccessibleName = labels.StopRun;
            AutomationProperties.SetHelpText(_runButton, labels.RunShortcutName(PromptKbd.ModifierName));
        }
        if (_output is not null)
        {
            _output.RetryText = labels.Retry;
            _output.CopyText = labels.CopyReply;
            _output.CopiedText = labels.Copied;
            _output.AccessibleName = labels.OutputName;
        }
        if (_versionMenu is not null)
        {
            _versionMenu.DialogName = labels.VersionsName;
            _versionMenu.GroupName = labels.VersionsGroup;
            _versionMenu.ProductionText = labels.Production;
            _versionMenu.CompareText = labels.CompareWith;
            _versionMenu.PromoteText = labels.Promote;
            _versionMenu.ProductionServesText = labels.ProductionServes;
            _versionMenu.InProductionText = labels.InProduction;
            _versionMenu.UndoText = labels.Undo;
            _versionMenu.MetaFormat = labels.VersionMeta;
            _versionMenu.DaysAgoText = labels.DaysAgo;
            _versionMenu.LastWeekText = labels.LastWeek;
            _versionMenu.WeeksAgoText = labels.WeeksAgo;
        }
        if (_modelMenu is not null)
        {
            _modelMenu.DialogName = labels.ModelMenuName;
            _modelMenu.ModelsName = labels.ModelsGroup;
            _modelMenu.EffortTitle = labels.EffortTitle;
            _modelMenu.EffortCellText = labels.EffortShort;
            _modelMenu.EffortName = labels.EffortName;
            _modelMenu.PriceNote = labels.PriceNote;
            _modelMenu.PriceName = labels.PriceName;
        }
        _hintKey = null;
        UpdateHint();
        UpdateCompareTexts();
        UpdateRunTexts();
    }

    private void ApplyEditorLabels(PromptTemplateEditor? editor, PromptStudioLabels labels, string name, string placeholder, string help)
    {
        if (editor is null)
        {
            return;
        }
        editor.AccessibleName = name;
        editor.Placeholder = placeholder;
        editor.ListName = labels.VariablesName;
        editor.AddVariableText = labels.AddVariable;
        // The "{{" list's rows are counted aloud in the card's live region.
        editor.AnnounceCount = count =>
        {
            var text = Labels.VariableCount(count);
            Announce(text);
            return text;
        };
        // The hint line describes the editor (Arc's aria-describedby).
        AutomationProperties.SetHelpText(editor, help);
    }

    #endregion

    #region Colors

    private IBrush? _addedBrush;
    private IBrush? _removedBrush;
    private IBrush? _addedTint;
    private IBrush? _removedTint;

    /// <summary>The compare view's word counts and keys: Success / Danger mixed 72% into Foreground, on 14% / 12% tints.</summary>
    private void ApplyColors()
    {
        var foreground = ColorOf(Foreground) ?? Colors.Black;
        var success = ColorOf(SuccessBrush) ?? Colors.Green;
        var danger = ColorOf(DangerBrush) ?? Colors.Red;
        _addedBrush = new ImmutableSolidColorBrush(PromptTone.Mix(foreground, success, 0.72));
        _removedBrush = new ImmutableSolidColorBrush(PromptTone.Mix(foreground, danger, 0.72));
        _addedTint = new ImmutableSolidColorBrush(PromptTone.WithOpacity(success, 0.14));
        _removedTint = new ImmutableSolidColorBrush(PromptTone.WithOpacity(danger, 0.12));
        if (_added is not null)
        {
            _added.Foreground = _addedBrush;
        }
        if (_removed is not null)
        {
            _removed.Foreground = _removedBrush;
        }
        _hintKey = null;
        UpdateHint();
    }

    private static Color? ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : null;

    #endregion

    #region Hint line

    /// <summary>The line under the editor: "Type [{{] to add a variable", or comparing "v3 to v4 [+ added] [− removed]".</summary>
    private void UpdateHint()
    {
        if (_hint is null)
        {
            return;
        }
        var compare = EffectiveCompareVersion;
        var key = compare is null ? "edit" : "compare|" + compare.Label + "|" + EffectiveOpenVersion?.Label;
        if (_hint.Content is not null && string.Equals(key, _hintKey, StringComparison.Ordinal))
        {
            return;
        }
        _hintKey = key;
        _hint.Content = compare is null ? BuildEditHint() : BuildCompareHint();
    }

    private Control BuildEditHint()
    {
        var labels = Labels;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = HintGap };
        row.Children.Add(HintText(labels.TypeToAddBefore, MutedBrush));
        row.Children.Add(new PromptKbd { Text = "{{" });
        row.Children.Add(HintText(labels.TypeToAddAfter, MutedBrush));
        return row;
    }

    private Control BuildCompareHint()
    {
        var labels = Labels;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = HintGap };
        var from = EffectiveCompareVersion?.Label ?? string.Empty;
        var to = EffectiveOpenVersion?.Label ?? string.Empty;
        var versions = HintText(labels.CompareHint(from, to), SecondaryBrush);
        versions.Margin = new Thickness(0, 0, 4, 0);
        row.Children.Add(versions);
        row.Children.Add(HintKey("+ " + labels.Added, _addedBrush, _addedTint));
        row.Children.Add(HintKey("− " + labels.Removed, _removedBrush, _removedTint));
        return row;
    }

    private static TextBlock HintText(string text, IBrush? brush) => new()
    {
        Text = text,
        Foreground = brush,
        FontSize = 12,
        LineHeight = 16.8,
        VerticalAlignment = VerticalAlignment.Center
    };

    private static Border HintKey(string text, IBrush? foreground, IBrush? tint) => new()
    {
        Background = tint,
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(5, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Child = HintText(text, foreground)
    };

    #endregion

    #region Size

    protected override Size MeasureOverride(Size availableSize)
    {
        // Arc's container query: the breakpoints follow the card's own width, decided before its parts measure.
        if (!double.IsInfinity(availableSize.Width))
        {
            ApplyBreakpoints(Math.Min(availableSize.Width, MaxWidth));
        }
        return base.MeasureOverride(availableSize);
    }

    /// <summary>The card's width classes, and the variable fields' columns to match them.</summary>
    private void ApplyBreakpoints(double width)
    {
        if (width == _measuredWidth)
        {
            return;
        }
        _measuredWidth = width;
        var oneColumn = width <= OneColumnWidth;
        var twoColumns = width <= TwoColumnWidth;
        PseudoClasses.Set(CompactPseudoClass, width <= CompactWidth);
        PseudoClasses.Set(NarrowPseudoClass, width <= NarrowWidth);
        PseudoClasses.Set(TwoColumnPseudoClass, twoColumns && !oneColumn);
        PseudoClasses.Set(OneColumnPseudoClass, oneColumn);
        if (_fields is not null)
        {
            // The fields count columns from their own width; the card decides instead (its width less its paddings).
            _fields.OneColumnMaxWidth = oneColumn ? double.MaxValue : -1;
            _fields.TwoColumnMaxWidth = twoColumns ? double.MaxValue : -1;
        }
    }

    #endregion

    #region Keys

    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not Visual source || !ReferenceEquals(source, this) && !this.IsVisualAncestorOf(source))
        {
            // Keys from the menus (they live in popups) are theirs.
            return;
        }
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (e.Key == Key.Enter && command && (e.KeyModifiers & (KeyModifiers.Alt | KeyModifiers.Shift)) == 0)
        {
            if (_runButton is not null && (ReferenceEquals(source, _runButton) || _runButton.IsVisualAncestorOf(source)))
            {
                // The Run / Stop button acts on the key itself.
                return;
            }
            e.Handled = true;
            // Arc: the shortcut runs; while a run is going it does not start another.
            if (!IsBusy)
            {
                Run();
            }
            return;
        }
        if (e.Key == Key.Escape && !e.Handled && e.KeyModifiers == KeyModifiers.None && IsCompareMode)
        {
            e.Handled = true;
            LeaveCompare(focusTab: true);
        }
    }

    /// <summary>The way focus moves now: with the keyboard ring after keys, without after a pointer.</summary>
    private NavigationMethod FocusMethod => _keyboardMode ? NavigationMethod.Tab : NavigationMethod.Pointer;

    #endregion

    #region Announcements

    /// <summary>Says <paramref name="text"/> in the card's polite live region.</summary>
    private void Announce(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        _status?.Announce(text);
        LastAnnouncement = text;
    }

    #endregion

    #region Lifetime

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // A studio that leaves the window stops its run; what was shown stays.
        if (_run is { } run)
        {
            StopRun(run, announce: false);
        }
    }

    #endregion

    #region Collections

    private void OnVersionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        EnsureDraftLoaded();
        UpdateHeader();
        UpdateCompare(announce: false);
        UpdateRunTexts();
    }

    private void OnVariablesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshNames();
    }

    private void OnTestCasesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        EnsureSelections();
        UpdateCaseParts();
    }

    private void OnModelsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        EnsureSelections();
        UpdateModelParts();
    }

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateModelParts();

    #endregion
}

/// <summary>
/// Crossfades one editor pane of <see cref="PromptStudio"/> (Arc: the old pane fades out in 100ms on the standard curve,
/// the new one in over 160ms on the enter curve; Reduced 120ms linear both ways; Off at once). A hidden pane keeps its
/// place in the layout (the editor slot is as high as the higher pane) but takes no input and leaves the tab order at
/// once, like Arc's <c>inert</c>. Code-driven: one opacity cannot take two durations in a ControlTheme transition, and
/// the pane must stay measured while it is hidden.
/// </summary>
internal sealed class PromptPaneFade : IDisposable
{
    private static readonly Avalonia.Animation.Easings.Easing s_linear = new Avalonia.Animation.Easings.LinearEasing();
    private static readonly TimeSpan s_exit = TimeSpan.FromMilliseconds(100);

    private readonly InputElement _pane;
    private readonly MotionValue<double> _opacity;
    private bool _shown;

    public PromptPaneFade(InputElement pane, bool shown)
    {
        _pane = pane;
        _shown = shown;
        _opacity = new MotionValue<double>(pane, shown ? 1 : 0, MotionVectors.Double);
        _opacity.Changed += OnChanged;
        pane.Opacity = shown ? 1 : 0;
        ApplyInert();
    }

    /// <summary>Whether the pane is the shown one.</summary>
    public bool IsShown => _shown;

    /// <summary>Shows or hides the pane.</summary>
    public void SetShown(bool shown)
    {
        if (shown == _shown)
        {
            return;
        }
        _shown = shown;
        ApplyInert();
        var level = TopLevel.GetTopLevel(_pane) is null ? ArcMotionLevel.Off : ArcMotion.GetEffectiveLevel(_pane);
        var target = shown ? 1.0 : 0.0;
        switch (level)
        {
            case ArcMotionLevel.Full:
                _ = _opacity.AnimateTo(target, shown
                    ? MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter)
                    : MotionSpec.Tween(s_exit, ArcEasings.Standard));
                break;
            case ArcMotionLevel.Reduced:
                _ = _opacity.AnimateTo(target, MotionSpec.Tween(ArcMotionPresets.Instant, s_linear));
                break;
            default:
                _opacity.Jump(target);
                break;
        }
    }

    private void ApplyInert()
    {
        _pane.IsHitTestVisible = _shown;
        _pane.IsEnabled = _shown;
        AutomationProperties.SetAccessibilityView(_pane, _shown ? AccessibilityView.Default : AccessibilityView.Raw);
    }

    private void OnChanged(double value) => _pane.Opacity = Math.Clamp(value, 0, 1);

    public void Dispose()
    {
        _opacity.Changed -= OnChanged;
        _opacity.Dispose();
    }
}
