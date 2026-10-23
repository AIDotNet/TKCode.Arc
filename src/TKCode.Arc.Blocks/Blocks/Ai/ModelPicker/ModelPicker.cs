using System.Collections.Specialized;
using System.Windows.Input;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// Chooses a model and its reasoning effort from one compact pill. The pill shows the current choice; its menu lists a
/// few featured models with a one-line note each, an effort setting, and every other model grouped by provider. Under
/// the pill a summary shows what the model can hold and costs, and confirms each change for a moment. A composition
/// of <see cref="ModelPill"/>, <see cref="ModelMenu"/> and <see cref="ModelSummary"/>: the catalog, the selection and
/// the effort are properties, so bind them or read them back from the events.
/// <para>
/// The pill opens on a pointer press or on Enter, Space and ArrowDown; the menu has a featured list, an Effort row and a More models row that open submenus (or
/// pages in a narrow window); choosing closes the menu and returns focus to the pill; the effort moves to the nearest
/// level a new model offers. Set <see cref="IsSummaryVisible"/> to false for the pill alone (a toolbar, a header).
/// </para>
/// <code language="xml">
/// &lt;arc:ModelPicker SelectedModelId="aurora-prime-3"&gt;
///     &lt;arc:ModelPicker.Providers&gt;...&lt;/arc:ModelPicker.Providers&gt;
///     &lt;arc:AiModel Id="aurora-prime-3" Name="Aurora Prime 3" ProviderId="aurora" /&gt;
/// &lt;/arc:ModelPicker&gt;
/// </code>
/// Screen readers hear a polite announcement after each change ("Now using Aurora Prime 3, medium effort"); the visible
/// confirmation under the summary stays for <see cref="NoticeDuration"/>.
/// </summary>
[TemplatePart(HostPart, typeof(Panel))]
[TemplatePart(PillPart, typeof(ModelPill))]
[TemplatePart(SummaryPart, typeof(ModelSummary))]
[PseudoClasses(OpenPseudoClass, SummaryHiddenPseudoClass)]
public class ModelPicker : TemplatedControl
{
    public const string HostPart = "PART_Host";
    public const string PillPart = "PART_Pill";
    public const string SummaryPart = "PART_Summary";

    public const string OpenPseudoClass = ":open";
    public const string SummaryHiddenPseudoClass = ":summary-hidden";

    #region Properties

    /// <summary>The catalog when you give it as a collection to follow; it wins over <see cref="Models"/> when set.</summary>
    public static readonly StyledProperty<IEnumerable<AiModel>?> ModelsSourceProperty =
        AvaloniaProperty.Register<ModelPicker, IEnumerable<AiModel>?>(nameof(ModelsSource));

    /// <summary>The selected model (two-way). Null at first selects the first featured model once and writes it back.</summary>
    public static readonly StyledProperty<AiModel?> SelectedModelProperty =
        AvaloniaProperty.Register<ModelPicker, AiModel?>(nameof(SelectedModel), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The id of the selected model (two-way); an id the catalog does not have yet waits for the catalog.</summary>
    public static readonly StyledProperty<string?> SelectedModelIdProperty =
        AvaloniaProperty.Register<ModelPicker, string?>(nameof(SelectedModelId), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The effort (two-way); a level the selected model does not offer moves to the nearest one, and a model without levels has none (null).</summary>
    public static readonly StyledProperty<ModelEffort?> EffortProperty =
        AvaloniaProperty.Register<ModelPicker, ModelEffort?>(nameof(Effort), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the menu is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModelPicker, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Shows the summary under the pill (default); false leaves the pill alone, for a toolbar or a header.</summary>
    public static readonly StyledProperty<bool> IsSummaryVisibleProperty =
        AvaloniaProperty.Register<ModelPicker, bool>(nameof(IsSummaryVisible), true);

    /// <summary>Small print under the summary's facts, such as "Prices in USD per 1M tokens · demo values"; null shows none.</summary>
    public static readonly StyledProperty<string?> SummaryNoteProperty =
        AvaloniaProperty.Register<ModelPicker, string?>(nameof(SummaryNote));

    /// <summary>The window width at or below which the menu's submenus become pages inside the menu (640).</summary>
    public static readonly StyledProperty<double> PagedMenuWidthProperty =
        AvaloniaProperty.Register<ModelPicker, double>(nameof(PagedMenuWidth), 640);

    /// <summary>How long the confirmation under the summary stays after a change (2.6 s); zero shows none.</summary>
    public static readonly StyledProperty<TimeSpan> NoticeDurationProperty =
        AvaloniaProperty.Register<ModelPicker, TimeSpan>(nameof(NoticeDuration), TimeSpan.FromSeconds(2.6));

    /// <summary>The currency symbol in front of the summary's prices ("$").</summary>
    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<ModelPicker, string>(nameof(CurrencySymbol), "$");

    /// <summary>Run with the new <see cref="AiModel"/> when the selection changes.</summary>
    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<ModelPicker, ICommand?>(nameof(SelectCommand));

    /// <summary>Run with the new <see cref="ModelEffort"/> (null when the model has none) when the effort changes.</summary>
    public static readonly StyledProperty<ICommand?> EffortCommandProperty =
        AvaloniaProperty.Register<ModelPicker, ICommand?>(nameof(EffortCommand));

    /// <summary>Every word the picker shows or reads out, with English defaults (see <see cref="ModelPickerLabels"/>).</summary>
    public static readonly StyledProperty<ModelPickerLabels> LabelsProperty =
        AvaloniaProperty.Register<ModelPicker, ModelPickerLabels>(nameof(Labels), new ModelPickerLabels());

    /// <summary>Raised when the selected model changes, by a choice in the menu or by code.</summary>
    public static readonly RoutedEvent<ModelPickerModelChangedEventArgs> ModelChangedEvent =
        RoutedEvent.Register<ModelPicker, ModelPickerModelChangedEventArgs>(nameof(ModelChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the effort changes: a choice in the menu, a snap to a level the new model offers, or code.</summary>
    public static readonly RoutedEvent<ModelPickerEffortChangedEventArgs> EffortChangedEvent =
        RoutedEvent.Register<ModelPicker, ModelPickerEffortChangedEventArgs>(nameof(EffortChanged), RoutingStrategies.Bubble);

    /// <summary>The models, declared inline or added in code. <see cref="ModelsSource"/> replaces them when set.</summary>
    [Content]
    public AvaloniaList<AiModel> Models { get; } = new();

    /// <summary>The providers: the order and names of the groups in "More models", and the marks of models that have none.</summary>
    public AvaloniaList<AiModelProvider> Providers { get; } = new();

    /// <summary>The ids of the models listed first in the menu, in order; empty lists the first four.</summary>
    public AvaloniaList<string> FeaturedIds { get; } = new();

    /// <inheritdoc cref="ModelsSourceProperty"/>
    public IEnumerable<AiModel>? ModelsSource { get => GetValue(ModelsSourceProperty); set => SetValue(ModelsSourceProperty, value); }

    /// <inheritdoc cref="SelectedModelProperty"/>
    public AiModel? SelectedModel { get => GetValue(SelectedModelProperty); set => SetValue(SelectedModelProperty, value); }

    /// <inheritdoc cref="SelectedModelIdProperty"/>
    public string? SelectedModelId { get => GetValue(SelectedModelIdProperty); set => SetValue(SelectedModelIdProperty, value); }

    /// <inheritdoc cref="EffortProperty"/>
    public ModelEffort? Effort { get => GetValue(EffortProperty); set => SetValue(EffortProperty, value); }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="IsSummaryVisibleProperty"/>
    public bool IsSummaryVisible { get => GetValue(IsSummaryVisibleProperty); set => SetValue(IsSummaryVisibleProperty, value); }

    /// <inheritdoc cref="SummaryNoteProperty"/>
    public string? SummaryNote { get => GetValue(SummaryNoteProperty); set => SetValue(SummaryNoteProperty, value); }

    /// <inheritdoc cref="PagedMenuWidthProperty"/>
    public double PagedMenuWidth { get => GetValue(PagedMenuWidthProperty); set => SetValue(PagedMenuWidthProperty, value); }

    /// <inheritdoc cref="NoticeDurationProperty"/>
    public TimeSpan NoticeDuration { get => GetValue(NoticeDurationProperty); set => SetValue(NoticeDurationProperty, value); }

    /// <inheritdoc cref="CurrencySymbolProperty"/>
    public string CurrencySymbol { get => GetValue(CurrencySymbolProperty); set => SetValue(CurrencySymbolProperty, value); }

    /// <inheritdoc cref="SelectCommandProperty"/>
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }

    /// <inheritdoc cref="EffortCommandProperty"/>
    public ICommand? EffortCommand { get => GetValue(EffortCommandProperty); set => SetValue(EffortCommandProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public ModelPickerLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="ModelChangedEvent"/>
    public event EventHandler<ModelPickerModelChangedEventArgs>? ModelChanged
    {
        add => AddHandler(ModelChangedEvent, value);
        remove => RemoveHandler(ModelChangedEvent, value);
    }

    /// <inheritdoc cref="EffortChangedEvent"/>
    public event EventHandler<ModelPickerEffortChangedEventArgs>? EffortChanged
    {
        add => AddHandler(EffortChangedEvent, value);
        remove => RemoveHandler(EffortChangedEvent, value);
    }

    #endregion

    private ModelPill? _pill;
    private ModelSummary? _summary;
    private Panel? _host;
    private ModelMenu? _menu;
    private readonly AiAnnouncer _announcer = new();
    // The menu is the single place that resolves ids, defaults and effort snapping. Our properties mirror it, so a
    // change coming from the menu must not be pushed back, and a push we start must not echo the property we pushed.
    private bool _fromMenu;
    private AvaloniaProperty? _pushing;
    // A choice in the menu raises ModelChosen, then (after the menu selected it) possibly a snapping EffortChosen.
    // The confirmation is built once both have arrived, in one dispatcher turn, so it never flickers twice.
    private PendingChange? _pending;
    private bool _flushQueued;

    static ModelPicker()
    {
        FocusableProperty.OverrideDefaultValue<ModelPicker>(false);
    }

    /// <summary>Creates a picker with empty lists.</summary>
    public ModelPicker()
    {
        Providers.CollectionChanged += OnProvidersChanged;
        FeaturedIds.CollectionChanged += OnFeaturedChanged;
    }

    /// <summary>The menu (null until the template is applied).</summary>
    internal ModelMenu? Menu => _menu;

    /// <summary>The pill (null until the template is applied).</summary>
    internal ModelPill? Pill => _pill;

    /// <summary>The summary (null until the template is applied).</summary>
    internal ModelSummary? Summary => _summary;

    /// <summary>The last polite announcement (tests and tools).</summary>
    internal string? LastAnnouncement => _announcer.Announcement;

    /// <summary>Opens the menu below the pill. With <see cref="ModelMenuOpenFocus.Selected"/> the keyboard position starts on the selected model.</summary>
    /// <param name="focus">Where the keyboard position starts.</param>
    public void Open(ModelMenuOpenFocus focus = ModelMenuOpenFocus.Surface)
    {
        if (_menu is { } menu && _pill is { } pill)
        {
            menu.Open(pill, focus);
        }
        else
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    /// <summary>Closes the menu; with <paramref name="returnFocus"/> the pill takes focus back.</summary>
    /// <param name="returnFocus">Whether focus returns to the pill.</param>
    public void Close(bool returnFocus = true)
    {
        if (_menu is { } menu)
        {
            menu.Close(returnFocus);
        }
        else
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    #region Template and wiring

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_pill is not null)
        {
            _pill.OpenRequested -= OnOpenRequested;
        }
        if (_host is not null)
        {
            _host.Children.Remove(_announcer);
            if (_menu is not null)
            {
                _host.Children.Remove(_menu);
            }
        }
        _host = e.NameScope.Find<Panel>(HostPart);
        _pill = e.NameScope.Find<ModelPill>(PillPart);
        _summary = e.NameScope.Find<ModelSummary>(SummaryPart);
        if (_menu is null)
        {
            _menu = new ModelMenu();
            _menu.AddHandler(ModelMenu.ModelChosenEvent, OnMenuModelChosen);
            _menu.AddHandler(ModelMenu.EffortChosenEvent, OnMenuEffortChosen);
            _menu.PropertyChanged += OnMenuPropertyChanged;
        }
        if (_pill is not null)
        {
            _pill.OpenRequested += OnOpenRequested;
        }
        _menu.PlacementTarget = _pill;
        // Hand the menu the catalog, the declared selection and the declared effort before it joins the tree: joining
        // applies its default selection, which must not overwrite what was declared.
        PushCatalogToMenu();
        PushScalarsToMenu();
        _host?.Children.Add(_menu);
        _host?.Children.Add(_announcer);
        SyncParts();
        UpdatePseudoClasses();
        if (IsOpen)
        {
            // Declared open (a screenshot, a first-run hint): open once the pill has a place on screen.
            Dispatcher.UIThread.Post(() =>
            {
                if (IsOpen && _menu is { IsOpen: false })
                {
                    Open();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _menu?.Close(returnFocus: false);
    }

    private void OnOpenRequested(object? sender, ModelPillOpenRequestedEventArgs e)
    {
        e.Handled = true;
        if (_menu is { } menu && _pill is { } pill)
        {
            menu.Toggle(pill, e.Focus);
        }
    }

    /// <summary>Copies the picker's state into the pill and the summary (the menu pushes are separate).</summary>
    private void SyncParts()
    {
        if (_pill is { } pill)
        {
            pill.Model = SelectedModel;
            pill.Effort = Effort;
            pill.Providers = Providers;
            pill.IsOpen = IsOpen;
            pill.Labels = Labels.Pill;
        }
        if (_summary is { } summary)
        {
            summary.Model = SelectedModel;
            summary.Effort = Effort;
            summary.Note = SummaryNote;
            summary.NoticeDuration = NoticeDuration;
            summary.CurrencySymbol = CurrencySymbol;
            summary.Labels = Labels.Summary;
        }
    }

    private void PushCatalogToMenu()
    {
        if (_menu is not { } menu)
        {
            return;
        }
        menu.ModelsSource = ModelsSource ?? Models;
        Mirror(Providers, menu.Providers);
        Mirror(FeaturedIds, menu.FeaturedIds);
    }

    private void PushScalarsToMenu()
    {
        if (_menu is not { } menu)
        {
            return;
        }
        menu.PagedBelowWidth = PagedMenuWidth;
        menu.Labels = Labels.Menu;
        // The menu resolves ids, the default selection and the effort snap; it echoes the results back (OnMenuPropertyChanged).
        if (SelectedModel is { } model)
        {
            Push(ModelMenu.SelectedModelProperty, () => menu.SelectedModel = model);
        }
        else if (SelectedModelId is { } id)
        {
            Push(ModelMenu.SelectedModelIdProperty, () => menu.SelectedModelId = id);
        }
        if (Effort is { } effort)
        {
            Push(ModelMenu.EffortProperty, () => menu.Effort = effort);
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

    private void OnProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_menu is { } menu)
        {
            Mirror(Providers, menu.Providers);
        }
        // The pill reads the same list, so it needs no copy; it follows the collection itself.
    }

    private void OnFeaturedChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_menu is { } menu)
        {
            Mirror(FeaturedIds, menu.FeaturedIds);
        }
    }

    #endregion

    #region Our properties

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == SelectedModelProperty)
        {
            OnSelectedModelChanged(change.GetOldValue<AiModel?>(), change.GetNewValue<AiModel?>());
        }
        else if (property == SelectedModelIdProperty)
        {
            if (!_fromMenu && _menu is { } menu)
            {
                var id = change.GetNewValue<string?>();
                Push(ModelMenu.SelectedModelIdProperty, () => menu.SelectedModelId = id);
            }
        }
        else if (property == EffortProperty)
        {
            OnEffortChanged(change.GetOldValue<ModelEffort?>(), change.GetNewValue<ModelEffort?>());
        }
        else if (property == IsOpenProperty)
        {
            var open = change.GetNewValue<bool>();
            _pill?.SetCurrentValue(ModelPill.IsOpenProperty, open);
            UpdatePseudoClasses();
            if (!_fromMenu)
            {
                if (open)
                {
                    Open();
                }
                else
                {
                    Close(returnFocus: false);
                }
            }
        }
        else if (property == ModelsSourceProperty)
        {
            PushCatalogToMenu();
        }
        else if (property == IsSummaryVisibleProperty)
        {
            UpdatePseudoClasses();
        }
        else if (property == SummaryNoteProperty)
        {
            _summary?.SetCurrentValue(ModelSummary.NoteProperty, change.GetNewValue<string?>());
        }
        else if (property == NoticeDurationProperty)
        {
            _summary?.SetCurrentValue(ModelSummary.NoticeDurationProperty, change.GetNewValue<TimeSpan>());
        }
        else if (property == CurrencySymbolProperty)
        {
            _summary?.SetCurrentValue(ModelSummary.CurrencySymbolProperty, change.GetNewValue<string>());
        }
        else if (property == PagedMenuWidthProperty)
        {
            _menu?.SetCurrentValue(ModelMenu.PagedBelowWidthProperty, change.GetNewValue<double>());
        }
        else if (property == LabelsProperty)
        {
            var labels = change.GetNewValue<ModelPickerLabels>();
            _pill?.SetCurrentValue(ModelPill.LabelsProperty, labels.Pill);
            _summary?.SetCurrentValue(ModelSummary.LabelsProperty, labels.Summary);
            _menu?.SetCurrentValue(ModelMenu.LabelsProperty, labels.Menu);
        }
    }

    private void OnSelectedModelChanged(AiModel? oldModel, AiModel? newModel)
    {
        _pill?.SetCurrentValue(ModelPill.ModelProperty, newModel);
        _summary?.SetCurrentValue(ModelSummary.ModelProperty, newModel);
        if (!_fromMenu)
        {
            if (_menu is { } menu)
            {
                Push(ModelMenu.SelectedModelProperty, () => menu.SelectedModel = newModel);
            }
            if (!ReferenceEquals(oldModel, newModel))
            {
                RaiseEvent(new ModelPickerModelChangedEventArgs(ModelChangedEvent, this, oldModel, newModel));
                RunCommand(SelectCommand, newModel);
            }
        }
        // The id follows the model (a null model keeps the id: it may be waiting for the catalog).
        if (newModel is not null && SelectedModelId != newModel.Id)
        {
            var wasFromMenu = _fromMenu;
            _fromMenu = true;
            try
            {
                SetCurrentValue(SelectedModelIdProperty, newModel.Id);
            }
            finally
            {
                _fromMenu = wasFromMenu;
            }
        }
    }

    private void OnEffortChanged(ModelEffort? oldEffort, ModelEffort? newEffort)
    {
        _pill?.SetCurrentValue(ModelPill.EffortProperty, newEffort);
        _summary?.SetCurrentValue(ModelSummary.EffortProperty, newEffort);
        if (_fromMenu)
        {
            return;
        }
        // A code assignment: a level the selected model lacks moves to the nearest one right here, so the event reports
        // the level that is really in use (and a model without levels has none).
        if (SelectedModel is { } selected && selected.SnapEffort(newEffort) is var valid && valid != newEffort &&
            (newEffort is not null || valid is not null) && newEffort is not null)
        {
            SetCurrentValue(EffortProperty, valid);
            return;
        }
        if (_menu is { } menu)
        {
            Push(ModelMenu.EffortProperty, () => menu.Effort = newEffort);
        }
        if (oldEffort != newEffort)
        {
            RaiseEvent(new ModelPickerEffortChangedEventArgs(EffortChangedEvent, this, oldEffort, newEffort, isSnap: false));
            RunCommand(EffortCommand, newEffort);
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(OpenPseudoClass, IsOpen);
        PseudoClasses.Set(SummaryHiddenPseudoClass, !IsSummaryVisible);
    }

    private static void RunCommand(ICommand? command, object? parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    #endregion

    #region From the menu

    /// <summary>Mirrors the menu's resolved selection, effort and open state into our properties without pushing them back.</summary>
    private void OnMenuPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (ReferenceEquals(e.Property, _pushing))
        {
            return;
        }
        var wasFromMenu = _fromMenu;
        _fromMenu = true;
        try
        {
            if (e.Property == ModelMenu.SelectedModelProperty)
            {
                SetCurrentValue(SelectedModelProperty, _menu!.SelectedModel);
            }
            else if (e.Property == ModelMenu.SelectedModelIdProperty)
            {
                SetCurrentValue(SelectedModelIdProperty, _menu!.SelectedModelId);
            }
            else if (e.Property == ModelMenu.EffortProperty)
            {
                SetCurrentValue(EffortProperty, _menu!.Effort);
            }
            else if (e.Property == ModelMenu.IsOpenProperty)
            {
                SetCurrentValue(IsOpenProperty, _menu!.IsOpen);
            }
        }
        finally
        {
            _fromMenu = wasFromMenu;
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
        RunCommand(SelectCommand, e.Model);
        Pending().ModelChange = (e.PreviousModel, e.Model);
        QueueFlush();
    }

    private void OnMenuEffortChosen(object? sender, ModelMenuEffortEventArgs e)
    {
        e.Handled = true;
        if (e.Effort == e.PreviousEffort)
        {
            return;
        }
        RaiseEvent(new ModelPickerEffortChangedEventArgs(EffortChangedEvent, this, e.PreviousEffort, e.Effort, e.IsSnap));
        RunCommand(EffortCommand, e.Effort);
        if (_menuPushInFlight)
        {
            return;
        }
        var pending = Pending();
        if (e.IsSnap)
        {
            pending.SnappedTo = e.Effort;
        }
        else
        {
            pending.EffortChange = (e.PreviousEffort, e.Effort);
        }
        QueueFlush();
    }

    // A snap caused by assigning SelectedModel in code is not a person's choice: no confirmation, no announcement.
    private bool _menuPushInFlight => _pushing is not null;

    #endregion

    #region Confirmation and announcement

    private sealed class PendingChange
    {
        public (AiModel? Previous, AiModel Next)? ModelChange;
        public (ModelEffort? Previous, ModelEffort? Next)? EffortChange;
        public ModelEffort? SnappedTo;
    }

    private PendingChange Pending() => _pending ??= new PendingChange();

    private void QueueFlush()
    {
        if (_flushQueued)
        {
            return;
        }
        _flushQueued = true;
        Dispatcher.UIThread.Post(Flush, DispatcherPriority.Normal);
    }

    /// <summary>Shows one confirmation and says one sentence for everything a choice changed.</summary>
    private void Flush()
    {
        _flushQueued = false;
        var pending = _pending;
        _pending = null;
        if (pending is null)
        {
            return;
        }
        var labels = Labels;
        var effortName = Effort is { } effort ? labels.EffortName(effort) : null;
        string? notice = null;
        string? announcement = null;
        if (pending.ModelChange is { } change)
        {
            var previous = change.Previous?.Name ?? string.Empty;
            notice = pending.SnappedTo is { } snapped
                ? labels.ModelAdjustedNotice(previous, change.Next.Name, labels.EffortName(snapped))
                : labels.ModelNotice(previous, change.Next.Name);
            announcement = labels.ModelAnnouncement(change.Next.Name, effortName);
        }
        else if (pending.EffortChange is { } effortChange && effortChange.Next is { } next)
        {
            var name = SelectedModel?.Name ?? string.Empty;
            var before = effortChange.Previous is { } p ? labels.EffortName(p) : string.Empty;
            notice = labels.EffortNotice(before, labels.EffortName(next));
            announcement = labels.EffortAnnouncement(name, labels.EffortName(next));
        }
        if (notice is not null && IsSummaryVisible)
        {
            _summary?.ShowNotice(notice);
        }
        _announcer.Announce(announcement);
    }

    #endregion
}
