using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A table card with an AI column (Arc Enrich column, design reference). One prompt runs down every row; each cell
/// shimmers while it works, then settles into a value with its source, or a failure with a retry. The top bar holds the
/// table's name, the row count and the column button (Run column, Stop, Re-run); the AI column's header shows its name
/// over its prompt with an edit control that opens an editor; the footer names the model and counts the results.
/// <para>
/// Give it <see cref="Rows"/> and an <see cref="Enrich"/> delegate that fills one cell: it gets the row and an
/// <see cref="EnrichColumnContext"/> (the prompt, a cancellation token, the attempt and <c>SetActivity</c> for the
/// shimmering label) and returns the value and its source, or throws to fail the cell with the exception's message. At
/// most <see cref="Concurrency"/> cells run at once and the workers start 140ms apart so the column fills top to bottom.
/// Persist values from <see cref="CellChanged"/> and feed them back through <see cref="InitialCells"/>. With no rows of
/// its own the column plays its <see cref="Simulation"/>, a table you supply, and nothing is sent; with neither it shows
/// the empty frame (no rows, Run column unavailable).
/// </para>
/// <para>
/// The rows are a virtualizing list: give the control a height (or a <c>MaxHeight</c>) for long tables and only the rows
/// on screen are built; without one every row shows. Below 560px wide the domain, the source text and the model detail
/// fold away; below 440px the gutters tighten, values wrap to two lines and failure reasons hide behind "Failed".
/// </para>
/// </summary>
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(MetaPart, typeof(TextBlock))]
[TemplatePart(RunPart, typeof(AvaloniaButton))]
[TemplatePart(RunGlyphPart, typeof(ArcGlyph))]
[TemplatePart(RunTextPart, typeof(TextBlock))]
[TemplatePart(RowHeaderPart, typeof(TextBlock))]
[TemplatePart(ColumnHeaderPart, typeof(Control))]
[TemplatePart(ColumnNamePart, typeof(TextBlock))]
[TemplatePart(ColumnPromptPart, typeof(TextBlock))]
[TemplatePart(EditPart, typeof(EnrichColumnButton))]
[TemplatePart(EditorPart, typeof(SpringSizeDecorator))]
[TemplatePart(EditorBodyPart, typeof(Control))]
[TemplatePart(NameInputPart, typeof(Input))]
[TemplatePart(PromptInputPart, typeof(Textarea))]
[TemplatePart(HintPart, typeof(TextBlock))]
[TemplatePart(CancelPart, typeof(AvaloniaButton))]
[TemplatePart(SavePart, typeof(AvaloniaButton))]
[TemplatePart(ListPart, typeof(ItemsControl))]
[TemplatePart(FooterPart, typeof(Control))]
[TemplatePart(ModelMarkPart, typeof(ArcGlyph))]
[TemplatePart(ModelNamePart, typeof(TextBlock))]
[TemplatePart(ModelDetailPart, typeof(TextBlock))]
[TemplatePart(ProgressPart, typeof(TextBlock))]
[TemplatePart(FailedPart, typeof(TextBlock))]
[TemplatePart(AnnouncerPart, typeof(ExplainerAnnouncer))]
[PseudoClasses(RunningPseudoClass, EditingPseudoClass, CompactPseudoClass, NarrowPseudoClass)]
public class EnrichColumn : TemplatedControl
{
    internal const string TitlePart = "PART_Title";
    internal const string MetaPart = "PART_Meta";
    internal const string RunPart = "PART_Run";
    internal const string RunGlyphPart = "PART_RunGlyph";
    internal const string RunTextPart = "PART_RunText";
    internal const string RowHeaderPart = "PART_RowHeader";
    internal const string ColumnHeaderPart = "PART_ColumnHeader";
    internal const string ColumnNamePart = "PART_ColumnName";
    internal const string ColumnPromptPart = "PART_ColumnPrompt";
    internal const string EditPart = "PART_Edit";
    internal const string EditorPart = "PART_Editor";
    internal const string EditorBodyPart = "PART_EditorBody";
    internal const string NameInputPart = "PART_NameInput";
    internal const string PromptInputPart = "PART_PromptInput";
    internal const string HintPart = "PART_Hint";
    internal const string CancelPart = "PART_Cancel";
    internal const string SavePart = "PART_Save";
    internal const string ListPart = "PART_List";
    internal const string FooterPart = "PART_Footer";
    internal const string ModelMarkPart = "PART_ModelMark";
    internal const string ModelNamePart = "PART_ModelName";
    internal const string ModelDetailPart = "PART_ModelDetail";
    internal const string ProgressPart = "PART_Progress";
    internal const string FailedPart = "PART_Failed";
    internal const string AnnouncerPart = "PART_Announcer";

    internal const string RunningPseudoClass = ":running";
    internal const string EditingPseudoClass = ":editing";
    internal const string CompactPseudoClass = ":compact";
    internal const string NarrowPseudoClass = ":narrow";

    /// <summary>Below this width the domain, the source text and the model detail fold away.</summary>
    internal const double CompactWidth = 560;

    /// <summary>Below this width the gutters tighten, values wrap and failure reasons hide.</summary>
    internal const double NarrowWidth = 440;

    /// <summary>The table's name in the top bar. Null shows the simulation's name while it plays.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EnrichColumn, string?>(nameof(Title));

    /// <summary>The rows to enrich. Null shows the <see cref="Simulation"/>'s rows, or no rows without one.</summary>
    public static readonly StyledProperty<IEnumerable<EnrichColumnRow>?> RowsProperty =
        AvaloniaProperty.Register<EnrichColumn, IEnumerable<EnrichColumnRow>?>(nameof(Rows));

    /// <summary>
    /// The AI column's name and prompt (two-way). The person can edit both from the column header; saving re-runs every
    /// row. Null shows the simulation's column while it plays, and an unnamed column otherwise.
    /// </summary>
    public static readonly StyledProperty<EnrichColumnDefinition?> ColumnProperty =
        AvaloniaProperty.Register<EnrichColumn, EnrichColumnDefinition?>(nameof(Column), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>
    /// Fills one cell: it gets the row and its context (prompt, column, cancellation token, attempt, <c>SetActivity</c>)
    /// and returns the value with its source. Throw to mark the cell failed with the exception's message. Null plays the
    /// <see cref="Simulation"/>'s answers while its rows are shown; with rows of your own and no delegate nothing runs.
    /// </summary>
    public static readonly StyledProperty<Func<EnrichColumnRow, EnrichColumnContext, Task<EnrichColumnResult>>?> EnrichProperty =
        AvaloniaProperty.Register<EnrichColumn, Func<EnrichColumnRow, EnrichColumnContext, Task<EnrichColumnResult>>?>(nameof(Enrich));

    /// <summary>Values you already have, keyed by row id, such as cells saved from an earlier run.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, EnrichColumnCell>?> InitialCellsProperty =
        AvaloniaProperty.Register<EnrichColumn, IReadOnlyDictionary<string, EnrichColumnCell>?>(nameof(InitialCells));

    /// <summary>How many cells run at once. Workers start 140ms apart so the column fills top to bottom.</summary>
    public static readonly StyledProperty<int> ConcurrencyProperty =
        AvaloniaProperty.Register<EnrichColumn, int>(nameof(Concurrency), 2, coerce: (_, v) => Math.Max(1, v));

    /// <summary>
    /// Runs the column once when it first scrolls into view, if no cell has a value yet. Null (the default) means on
    /// while the <see cref="Simulation"/> plays and off with your own <see cref="Enrich"/>.
    /// </summary>
    public static readonly StyledProperty<bool?> AutoRunProperty =
        AvaloniaProperty.Register<EnrichColumn, bool?>(nameof(AutoRun));

    /// <summary>The model shown in the footer. Null shows the simulation's model while it plays, and hides it otherwise.</summary>
    public static readonly StyledProperty<EnrichColumnModel?> ModelProperty =
        AvaloniaProperty.Register<EnrichColumn, EnrichColumnModel?>(nameof(Model));

    /// <summary>Shows the edit control on the AI column's header.</summary>
    public static readonly StyledProperty<bool> IsEditableProperty =
        AvaloniaProperty.Register<EnrichColumn, bool>(nameof(IsEditable), true);

    /// <summary>A short note after the row count. Null shows "sample data" while the simulation plays and nothing otherwise.</summary>
    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<EnrichColumn, string?>(nameof(Note));

    /// <summary>
    /// The table the column plays on its own while <see cref="Rows"/> is null: your name, column, model, rows and the
    /// answer each row settles into, run exactly like your own delegate would (queued, shimmering, settled, a failure to
    /// retry). Null shows the empty frame instead.
    /// </summary>
    public static readonly StyledProperty<EnrichColumnSimulation?> SimulationProperty =
        AvaloniaProperty.Register<EnrichColumn, EnrichColumnSimulation?>(nameof(Simulation));

    /// <summary>Every word the column shows or says.</summary>
    public static readonly StyledProperty<EnrichColumnLabels> LabelsProperty =
        AvaloniaProperty.Register<EnrichColumn, EnrichColumnLabels>(nameof(Labels), EnrichColumnLabels.Default);

    /// <summary>Whether any cell is queued or running.</summary>
    public static readonly DirectProperty<EnrichColumn, bool> IsRunningProperty =
        AvaloniaProperty.RegisterDirect<EnrichColumn, bool>(nameof(IsRunning), o => o.IsRunning);

    /// <summary>Raised whenever a cell changes state (queued, running, done, failed). Use it to persist values. Bubbles.</summary>
    public static readonly RoutedEvent<EnrichColumnCellChangedEventArgs> CellChangedEvent =
        RoutedEvent.Register<EnrichColumn, EnrichColumnCellChangedEventArgs>(nameof(CellChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when a run settles (finished or stopped), with every cell. Bubbles.</summary>
    public static readonly RoutedEvent<EnrichColumnRunCompletedEventArgs> RunCompletedEvent =
        RoutedEvent.Register<EnrichColumn, EnrichColumnRunCompletedEventArgs>(nameof(RunCompleted), RoutingStrategies.Bubble);

    /// <summary>Raised when the person saves an edited column (the column then re-runs). Bubbles.</summary>
    public static readonly RoutedEvent<EnrichColumnColumnChangedEventArgs> ColumnChangedEvent =
        RoutedEvent.Register<EnrichColumn, EnrichColumnColumnChangedEventArgs>(nameof(ColumnChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the person opens a value's source. Set Handled to open it yourself; otherwise the system opens the address. Bubbles.</summary>
    public static readonly RoutedEvent<EnrichColumnSourceRequestedEventArgs> SourceRequestedEvent =
        RoutedEvent.Register<EnrichColumn, EnrichColumnSourceRequestedEventArgs>(nameof(SourceRequested), RoutingStrategies.Bubble);

    /// <summary>How much room the column has; inherited by its rows.</summary>
    internal static readonly AttachedProperty<EnrichColumnDensity> DensityProperty =
        AvaloniaProperty.RegisterAttached<EnrichColumn, Control, EnrichColumnDensity>("Density", inherits: true);

    /// <summary>Whether the last pointer was a finger (row controls then stay visible); inherited by its rows.</summary>
    internal static readonly AttachedProperty<bool> IsTouchProperty =
        AvaloniaProperty.RegisterAttached<EnrichColumn, Control, bool>("IsTouch", inherits: true);

    private readonly AvaloniaList<EnrichColumnRowItem> _items = [];
    private readonly Dictionary<string, EnrichColumnRowItem> _byId = new(StringComparer.Ordinal);
    private readonly EnrichColumnRunner _runner;
    private IReadOnlyList<EnrichColumnRow> _rows = [];
    private INotifyCollectionChanged? _observed;
    private IEnrichColumnClock _clock = EnrichColumnSystemClock.Instance;
    private ViewportVisibility? _visibility;
    private bool _autoRunDone;
    private bool _isRunning;
    private bool _isEditing;
    private bool _simulatedRun;

    private TextBlock? _title;
    private TextBlock? _meta;
    private AvaloniaButton? _run;
    private ArcGlyph? _runGlyph;
    private TextBlock? _runText;
    private TextBlock? _rowHeader;
    private Control? _columnHeader;
    private TextBlock? _columnName;
    private TextBlock? _columnPrompt;
    private EnrichColumnButton? _edit;
    private SpringSizeDecorator? _editor;
    private Control? _editorBody;
    private Input? _nameInput;
    private Textarea? _promptInput;
    private TextBlock? _hint;
    private AvaloniaButton? _cancel;
    private AvaloniaButton? _save;
    private ItemsControl? _list;
    private Control? _footer;
    private ArcGlyph? _modelMark;
    private TextBlock? _modelName;
    private TextBlock? _modelDetail;
    private TextBlock? _progress;
    private TextBlock? _failed;
    private ExplainerAnnouncer? _announcer;

    /// <summary>Creates an empty column; give it <see cref="Rows"/> and <see cref="Enrich"/>, or a <see cref="Simulation"/>.</summary>
    public EnrichColumn()
    {
        SpecialFocusModality.Watch();
        _runner = new EnrichColumnRunner(() => _rows, () => EffectiveColumn, RunCell, () => Labels.UnknownError)
        {
            Concurrency = Concurrency,
        };
        _runner.CellChanged += OnCellChanged;
        _runner.Started += OnRunStarted;
        _runner.Settled += OnRunSettled;
        RebuildItems();
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="RowsProperty"/>
    public IEnumerable<EnrichColumnRow>? Rows { get => GetValue(RowsProperty); set => SetValue(RowsProperty, value); }

    /// <inheritdoc cref="ColumnProperty"/>
    public EnrichColumnDefinition? Column { get => GetValue(ColumnProperty); set => SetValue(ColumnProperty, value); }

    /// <inheritdoc cref="EnrichProperty"/>
    public Func<EnrichColumnRow, EnrichColumnContext, Task<EnrichColumnResult>>? Enrich
    {
        get => GetValue(EnrichProperty);
        set => SetValue(EnrichProperty, value);
    }

    /// <inheritdoc cref="InitialCellsProperty"/>
    public IReadOnlyDictionary<string, EnrichColumnCell>? InitialCells
    {
        get => GetValue(InitialCellsProperty);
        set => SetValue(InitialCellsProperty, value);
    }

    /// <inheritdoc cref="ConcurrencyProperty"/>
    public int Concurrency { get => GetValue(ConcurrencyProperty); set => SetValue(ConcurrencyProperty, value); }

    /// <inheritdoc cref="AutoRunProperty"/>
    public bool? AutoRun { get => GetValue(AutoRunProperty); set => SetValue(AutoRunProperty, value); }

    /// <inheritdoc cref="ModelProperty"/>
    public EnrichColumnModel? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <inheritdoc cref="IsEditableProperty"/>
    public bool IsEditable { get => GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <inheritdoc cref="NoteProperty"/>
    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    /// <inheritdoc cref="SimulationProperty"/>
    public EnrichColumnSimulation? Simulation { get => GetValue(SimulationProperty); set => SetValue(SimulationProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public EnrichColumnLabels Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning { get => _isRunning; private set => SetAndRaise(IsRunningProperty, ref _isRunning, value); }

    /// <summary>Every row's cell now, keyed by row id.</summary>
    public IReadOnlyDictionary<string, EnrichColumnCell> Cells => _runner.Snapshot();

    /// <inheritdoc cref="CellChangedEvent"/>
    public event EventHandler<EnrichColumnCellChangedEventArgs>? CellChanged
    {
        add => AddHandler(CellChangedEvent, value);
        remove => RemoveHandler(CellChangedEvent, value);
    }

    /// <inheritdoc cref="RunCompletedEvent"/>
    public event EventHandler<EnrichColumnRunCompletedEventArgs>? RunCompleted
    {
        add => AddHandler(RunCompletedEvent, value);
        remove => RemoveHandler(RunCompletedEvent, value);
    }

    /// <inheritdoc cref="ColumnChangedEvent"/>
    public event EventHandler<EnrichColumnColumnChangedEventArgs>? ColumnChanged
    {
        add => AddHandler(ColumnChangedEvent, value);
        remove => RemoveHandler(ColumnChangedEvent, value);
    }

    /// <inheritdoc cref="SourceRequestedEvent"/>
    public event EventHandler<EnrichColumnSourceRequestedEventArgs>? SourceRequested
    {
        add => AddHandler(SourceRequestedEvent, value);
        remove => RemoveHandler(SourceRequestedEvent, value);
    }

    /// <summary>Whether the simulation's table is shown (the app has set no rows).</summary>
    internal bool ShowsSimulation => Rows is null && Simulation is not null;

    /// <summary>Whether the values come from the simulation (its rows are shown and there is no <see cref="Enrich"/> delegate).</summary>
    internal bool IsSimulated => Enrich is null && ShowsSimulation;

    /// <summary>Whether a run has something to call: the app's delegate, or the simulation while its rows are shown.</summary>
    internal bool CanRun => Enrich is not null || ShowsSimulation;

    /// <summary>The name in the top bar: <see cref="Title"/>, else the simulation's while it plays.</summary>
    internal string DisplayedTitle => Title ?? (ShowsSimulation ? Simulation!.Title : null) ?? string.Empty;

    /// <summary>The AI column: <see cref="Column"/>, else the simulation's while it plays, else an unnamed one.</summary>
    internal EnrichColumnDefinition EffectiveColumn => Column ?? (ShowsSimulation ? Simulation!.Column : null) ?? s_unnamed;

    /// <summary>The footer's model: <see cref="Model"/>, else the simulation's while it plays.</summary>
    internal EnrichColumnModel? EffectiveModel => Model ?? (ShowsSimulation ? Simulation!.Model : null);

    private static readonly EnrichColumnDefinition s_unnamed = new();

    /// <summary>Whether the column runs itself when it first comes into view.</summary>
    internal bool EffectiveAutoRun => AutoRun ?? IsSimulated;

    /// <summary>Whether the column editor is open.</summary>
    internal bool IsEditing => _isEditing;

    /// <summary>The rows on screen now.</summary>
    internal IReadOnlyList<EnrichColumnRow> DisplayedRows => _rows;

    internal IReadOnlyList<EnrichColumnRowItem> Items => _items;

    internal EnrichColumnRunner Runner => _runner;

    internal ExplainerAnnouncer? Announcer => _announcer;

    internal AvaloniaButton? RunButton => _run;

    internal EnrichColumnButton? EditButton => _edit;

    internal Input? NameInput => _nameInput;

    internal Textarea? PromptInput => _promptInput;

    internal SpringSizeDecorator? Editor => _editor;

    internal ItemsControl? List => _list;

    internal TextBlock? ProgressText => _progress;

    internal TextBlock? MetaText => _meta;

    internal Control? Footer => _footer;

    /// <summary>The clock of the stagger and the simulation (tests swap in a manual one).</summary>
    internal IEnrichColumnClock Clock
    {
        get => _clock;
        set
        {
            _clock = value;
            _runner.Clock = value;
        }
    }

    internal static EnrichColumnDensity GetDensity(Control control) => control.GetValue(DensityProperty);

    internal static bool GetIsTouch(Control control) => control.GetValue(IsTouchProperty);

    /// <summary>The density for a width.</summary>
    internal static EnrichColumnDensity DensityFor(double width) =>
        width < NarrowWidth ? EnrichColumnDensity.Narrow : width < CompactWidth ? EnrichColumnDensity.Compact : EnrichColumnDensity.Regular;

    /// <summary>Runs every row from the top (a run in flight starts over). Does nothing when there is nothing to call.</summary>
    public void Run()
    {
        if (CanRun)
        {
            _runner.RunAll();
        }
    }

    /// <summary>Stops the run: requests are cancelled and queued or running cells go back to what they showed before.</summary>
    public void Stop() => _runner.Stop();

    /// <summary>Runs one row again, or retries a failed one. Does nothing when there is nothing to call.</summary>
    public void RunRow(string rowId)
    {
        if (CanRun)
        {
            _runner.RunRow(rowId);
        }
    }

    /// <summary>Fills one cell with the app's delegate, or plays the simulation's answer.</summary>
    private Task<EnrichColumnResult> RunCell(EnrichColumnRow row, EnrichColumnContext context)
    {
        if (Enrich is { } enrich)
        {
            return enrich(row, context);
        }
        if (ShowsSimulation)
        {
            return Simulation!.RunAsync(row, context, _clock);
        }
        return Task.FromException<EnrichColumnResult>(new InvalidOperationException(string.Empty));
    }

    /// <summary>The cell of a row (idle when it has not run).</summary>
    public EnrichColumnCell GetCell(string rowId) => _runner.Get(rowId);

    /// <summary>Opens the column editor and moves focus to the name field.</summary>
    public void BeginEdit()
    {
        if (_isEditing || !IsEditable)
        {
            return;
        }
        _isEditing = true;
        if (_nameInput is not null)
        {
            _nameInput.Text = EffectiveColumn.Name;
        }
        if (_promptInput is not null)
        {
            _promptInput.Text = EffectiveColumn.Prompt;
        }
        UpdateEditor();
        _nameInput?.Focus(NavigationMethod.Tab);
    }

    /// <summary>Closes the column editor without saving and returns focus to the edit control.</summary>
    public void CancelEdit()
    {
        if (!_isEditing)
        {
            return;
        }
        _isEditing = false;
        UpdateEditor();
        // Cancel clicked with the mouse hands focus back without the keyboard ring.
        _edit?.Focus(SpecialFocusModality.For(this));
    }

    /// <summary>Saves the edited column, raises <see cref="ColumnChanged"/> and re-runs every row.</summary>
    public void SaveEdit()
    {
        if (!_isEditing)
        {
            return;
        }
        var old = EffectiveColumn;
        var name = _nameInput?.Text?.Trim();
        var prompt = _promptInput?.Text?.Trim();
        var column = new EnrichColumnDefinition(
            string.IsNullOrEmpty(name) ? old.Name : name,
            string.IsNullOrEmpty(prompt) ? old.Prompt : prompt);
        _isEditing = false;
        UpdateEditor();
        SetCurrentValue(ColumnProperty, column);
        RaiseEvent(new EnrichColumnColumnChangedEventArgs(ColumnChangedEvent, column));
        _edit?.Focus(SpecialFocusModality.For(this));
        Run();
    }

    /// <summary>Opens a source: the app gets <see cref="SourceRequested"/> first, then the system opens the address.</summary>
    internal void OpenSource(EnrichColumnRow row, EnrichColumnSource source)
    {
        var args = new EnrichColumnSourceRequestedEventArgs(SourceRequestedEvent, row, source);
        RaiseEvent(args);
        if (args.Handled || !Uri.TryCreate(source.Href, UriKind.Absolute, out var uri))
        {
            return;
        }
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Unhook();
        var scope = e.NameScope;
        _title = scope.Find<TextBlock>(TitlePart);
        _meta = scope.Find<TextBlock>(MetaPart);
        _run = scope.Find<AvaloniaButton>(RunPart);
        _runGlyph = scope.Find<ArcGlyph>(RunGlyphPart);
        _runText = scope.Find<TextBlock>(RunTextPart);
        _rowHeader = scope.Find<TextBlock>(RowHeaderPart);
        _columnHeader = scope.Find<Control>(ColumnHeaderPart);
        _columnName = scope.Find<TextBlock>(ColumnNamePart);
        _columnPrompt = scope.Find<TextBlock>(ColumnPromptPart);
        _edit = scope.Find<EnrichColumnButton>(EditPart);
        _editor = scope.Find<SpringSizeDecorator>(EditorPart);
        _editorBody = scope.Find<Control>(EditorBodyPart);
        _nameInput = scope.Find<Input>(NameInputPart);
        _promptInput = scope.Find<Textarea>(PromptInputPart);
        _hint = scope.Find<TextBlock>(HintPart);
        _cancel = scope.Find<AvaloniaButton>(CancelPart);
        _save = scope.Find<AvaloniaButton>(SavePart);
        _list = scope.Find<ItemsControl>(ListPart);
        _footer = scope.Find<Control>(FooterPart);
        _modelMark = scope.Find<ArcGlyph>(ModelMarkPart);
        _modelName = scope.Find<TextBlock>(ModelNamePart);
        _modelDetail = scope.Find<TextBlock>(ModelDetailPart);
        _progress = scope.Find<TextBlock>(ProgressPart);
        _failed = scope.Find<TextBlock>(FailedPart);
        _announcer = scope.Find<ExplainerAnnouncer>(AnnouncerPart);

        if (_run is not null)
        {
            _run.Click += OnRunClick;
        }
        if (_edit is not null)
        {
            _edit.Click += OnEditClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click += OnCancelClick;
        }
        if (_save is not null)
        {
            _save.Click += OnSaveClick;
        }
        _editorBody?.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        if (_list is not null)
        {
            _list.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
            _list.ItemTemplate = new FuncDataTemplate<EnrichColumnRowItem>((_, _) => new EnrichColumnRowView(), supportsRecycling: true);
            _list.ItemsSource = _items;
            AutomationProperties.SetName(_list, DisplayedTitle);
        }
        UpdateEditor();
        RefreshChrome();
    }

    private void Unhook()
    {
        if (_run is not null)
        {
            _run.Click -= OnRunClick;
        }
        if (_edit is not null)
        {
            _edit.Click -= OnEditClick;
        }
        if (_cancel is not null)
        {
            _cancel.Click -= OnCancelClick;
        }
        if (_save is not null)
        {
            _save.Click -= OnSaveClick;
        }
        _editorBody?.RemoveHandler(KeyDownEvent, OnEditorKeyDown);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RowsProperty)
        {
            Observe(change.GetNewValue<IEnumerable<EnrichColumnRow>?>());
            RebuildItems();
            RefreshChrome();
        }
        else if (change.Property == SimulationProperty)
        {
            OnSimulationChanged();
        }
        else if (change.Property == InitialCellsProperty)
        {
            _runner.Seed(InitialCells);
            foreach (var item in _items)
            {
                item.Update(_runner.Get(item.Row.Id));
            }
            RefreshChrome();
        }
        else if (change.Property == ConcurrencyProperty)
        {
            _runner.Concurrency = Concurrency;
        }
        else if (change.Property == IsEditableProperty)
        {
            if (!IsEditable && _isEditing)
            {
                _isEditing = false;
                UpdateEditor();
            }
            RefreshChrome();
        }
        else if (change.Property == TitleProperty || change.Property == ColumnProperty || change.Property == ModelProperty ||
                 change.Property == NoteProperty || change.Property == EnrichProperty || change.Property == LabelsProperty)
        {
            RefreshChrome();
            if (change.Property == LabelsProperty)
            {
                foreach (var item in _items)
                {
                    item.Refresh();
                }
            }
        }
        else if (change.Property == DensityProperty)
        {
            var density = GetDensity(this);
            PseudoClasses.Set(CompactPseudoClass, density != EnrichColumnDensity.Regular);
            PseudoClasses.Set(NarrowPseudoClass, density == EnrichColumnDensity.Narrow);
            RefreshChrome();
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var density = DensityFor(e.NewSize.Width);
        if (density != GetDensity(this))
        {
            SetValue(DensityProperty, density);
        }
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        NotePointer(e.Pointer.Type);
    }

    /// <inheritdoc />
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        NotePointer(e.Pointer.Type);
    }

    private void NotePointer(PointerType type)
    {
        // There is no hover on a touch screen, so the row controls stay visible after a finger touches the table.
        var touch = type == PointerType.Touch;
        if (touch != GetIsTouch(this))
        {
            SetValue(IsTouchProperty, touch);
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        WatchForFirstSight();
    }

    /// <summary>Runs the column once it is first seen (when <see cref="EffectiveAutoRun"/>).</summary>
    private void WatchForFirstSight()
    {
        if (!_autoRunDone)
        {
            _visibility?.Dispose();
            _visibility = new ViewportVisibility(this, 0.2, once: true);
            _visibility.VisibilityChanged += OnFirstSeen;
        }
    }

    /// <summary>
    /// Another simulation (or none): while it supplies the rows, the run in flight stops, its cells are forgotten and the
    /// new table runs on its own once seen, as on first load.
    /// </summary>
    private void OnSimulationChanged()
    {
        if (Rows is null)
        {
            _runner.Reset();
            _runner.Seed(InitialCells);
            RebuildItems();
            foreach (var item in _items)
            {
                item.Update(_runner.Get(item.Row.Id));
            }
            IsRunning = _runner.IsRunning;
            _autoRunDone = false;
            if (TopLevel.GetTopLevel(this) is not null)
            {
                WatchForFirstSight();
            }
        }
        RefreshChrome();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _visibility?.Dispose();
        _visibility = null;
        // Away from a window nothing should keep working: requests are cancelled and their cells put back.
        _runner.Stop();
    }

    private void OnFirstSeen(object? sender, bool visible)
    {
        if (!visible || _autoRunDone)
        {
            return;
        }
        _autoRunDone = true;
        if (EffectiveAutoRun && !_runner.IsRunning && !_rows.Any(r => _runner.Get(r.Id).HasValue))
        {
            Run();
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new EnrichColumnPeer(this);

    private void Observe(IEnumerable<EnrichColumnRow>? rows)
    {
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnRowsCollectionChanged;
        }
        _observed = rows as INotifyCollectionChanged;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnRowsCollectionChanged;
        }
    }

    private void OnRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildItems();
        RefreshChrome();
    }

    /// <summary>The list mirrors the rows: an item is kept per id (so its row view is not rebuilt), new ids get new items.</summary>
    private void RebuildItems()
    {
        _rows = (Rows ?? Simulation?.Rows) is { } rows ? rows.Where(r => r is not null).ToArray() : [];
        var next = new List<EnrichColumnRowItem>(_rows.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in _rows)
        {
            if (!seen.Add(row.Id))
            {
                continue;
            }
            if (_byId.TryGetValue(row.Id, out var item))
            {
                var changed = !ReferenceEquals(item.Row, row);
                item.Row = row;
                if (changed)
                {
                    item.Refresh();
                }
            }
            else
            {
                item = new EnrichColumnRowItem(this, row, _runner.Get(row.Id));
                _byId[row.Id] = item;
            }
            next.Add(item);
        }
        foreach (var id in _byId.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _byId.Remove(id);
        }
        var same = next.Count == _items.Count;
        for (var i = 0; same && i < next.Count; i++)
        {
            same = ReferenceEquals(next[i], _items[i]);
        }
        if (!same)
        {
            _items.Clear();
            _items.AddRange(next);
        }
    }

    private void OnCellChanged(EnrichColumnRow row, EnrichColumnCell cell)
    {
        if (_byId.TryGetValue(row.Id, out var item))
        {
            item.Update(cell);
        }
        IsRunning = _runner.IsRunning;
        RefreshChrome();
        RaiseEvent(new EnrichColumnCellChangedEventArgs(CellChangedEvent, row, cell));
    }

    private void OnRunStarted(int count)
    {
        _simulatedRun = IsSimulated;
        IsRunning = true;
        var text = Labels.AnnounceRun(count);
        Announce(_simulatedRun ? text + " " + Labels.AnnounceSimulated : text);
        RefreshChrome();
    }

    private void OnRunSettled(EnrichColumnSettle settle)
    {
        IsRunning = _runner.IsRunning;
        var (done, failed, total) = Counts();
        Announce(settle == EnrichColumnSettle.Stopped
            ? Labels.AnnounceStopped(done, total)
            : Labels.AnnounceFinished(done, failed, total));
        RefreshChrome();
        RaiseEvent(new EnrichColumnRunCompletedEventArgs(RunCompletedEvent, _runner.Snapshot(), settle == EnrichColumnSettle.Stopped));
    }

    private void Announce(string text) => _announcer?.Announce(text);

    /// <summary>Done, failed and total over the rows shown.</summary>
    internal (int Done, int Failed, int Total) Counts()
    {
        var done = 0;
        var failed = 0;
        foreach (var row in _rows)
        {
            var status = _runner.Get(row.Id).Status;
            if (status == EnrichColumnCellStatus.Done)
            {
                done++;
            }
            else if (status == EnrichColumnCellStatus.Failed)
            {
                failed++;
            }
        }
        return (done, failed, _rows.Count);
    }

    private void OnRunClick(object? sender, RoutedEventArgs e)
    {
        if (_runner.IsRunning)
        {
            Stop();
        }
        else
        {
            Run();
        }
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (_isEditing)
        {
            CancelEdit();
        }
        else
        {
            BeginEdit();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => CancelEdit();

    private void OnSaveClick(object? sender, RoutedEventArgs e) => SaveEdit();

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelEdit();
        }
        else if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta)))
        {
            e.Handled = true;
            SaveEdit();
        }
    }

    /// <summary>The editor's height spring follows <see cref="IsEditing"/>; a closed editor keeps its fields out of the Tab order.</summary>
    private void UpdateEditor()
    {
        PseudoClasses.Set(EditingPseudoClass, _isEditing);
        if (_editor is not null)
        {
            _editor.IsExpanded = _isEditing;
            KeyboardNavigation.SetTabNavigation(_editor, _isEditing ? KeyboardNavigationMode.Continue : KeyboardNavigationMode.None);
        }
        if (_editorBody is not null)
        {
            _editorBody.IsHitTestVisible = _isEditing;
        }
        if (_edit is not null)
        {
            Avalonia.Automation.AutomationProperties.SetName(_edit, Labels.EditColumn);
        }
    }

    /// <summary>Brings the top bar, the header and the footer in line with the state.</summary>
    private void RefreshChrome()
    {
        var labels = Labels;
        var density = GetDensity(this);
        var (done, failed, total) = Counts();
        var running = _runner.IsRunning;
        PseudoClasses.Set(RunningPseudoClass, running);
        if (_title is not null)
        {
            _title.Text = DisplayedTitle;
        }
        if (_meta is not null)
        {
            var note = Note ?? (IsSimulated ? labels.SampleNote : null);
            _meta.Text = string.IsNullOrEmpty(note) ? labels.Rows(total) : labels.Rows(total) + " · " + note;
        }
        if (_run is not null)
        {
            _run.IsEnabled = running || CanRun;
            var hasValues = done > 0 || failed > 0;
            var text = running ? labels.Stop : hasValues ? labels.Rerun : labels.RunColumn;
            if (_runText is not null)
            {
                _runText.Text = text;
            }
            if (_runGlyph is not null)
            {
                _runGlyph.Data = running ? ArcIcons.Square : hasValues ? ArcIcons.RotateCcw : ArcIcons.Play;
            }
            AutomationProperties.SetName(_run, text);
        }
        if (_rowHeader is not null)
        {
            _rowHeader.Text = labels.RowHeader;
        }
        var column = EffectiveColumn;
        if (_columnName is not null)
        {
            _columnName.Text = column.Name;
        }
        if (_columnPrompt is not null)
        {
            _columnPrompt.Text = column.Prompt;
            Tooltip.SetTip(_columnPrompt, column.Prompt);
        }
        if (_columnHeader is not null)
        {
            AutomationProperties.SetName(_columnHeader, column.Name + ": " + column.Prompt);
        }
        if (_edit is not null)
        {
            _edit.IsVisible = IsEditable;
            AutomationProperties.SetName(_edit, labels.EditColumn);
            Tooltip.SetTip(_edit, labels.EditColumn);
        }
        if (_nameInput is not null)
        {
            _nameInput.Label = labels.ColumnName;
        }
        if (_promptInput is not null)
        {
            _promptInput.Label = labels.Prompt;
        }
        if (_hint is not null)
        {
            _hint.Text = labels.SaveHint;
        }
        if (_cancel is not null)
        {
            _cancel.Content = labels.Cancel;
        }
        if (_save is not null)
        {
            _save.Content = labels.SaveAndRun;
        }
        var model = EffectiveModel;
        if (_modelName is not null)
        {
            _modelName.Text = model?.Name;
            _modelName.IsVisible = model is not null;
        }
        if (_modelMark is not null)
        {
            _modelMark.IsVisible = model is not null;
            _modelMark.Data = model?.Mark ?? ArcIcons.Sparkles;
            if (model?.MarkBrush is { } markBrush)
            {
                _modelMark.Foreground = markBrush;
            }
            else
            {
                _modelMark.ClearValue(ArcGlyph.ForegroundProperty);
            }
        }
        if (_modelDetail is not null)
        {
            var detail = model?.Detail;
            _modelDetail.Text = string.IsNullOrEmpty(detail) ? null : "· " + detail;
            _modelDetail.IsVisible = !string.IsNullOrEmpty(detail) && density == EnrichColumnDensity.Regular;
        }
        if (_progress is not null)
        {
            _progress.Text = labels.Progress(done, total);
        }
        if (_failed is not null)
        {
            _failed.Text = labels.FailedCount(failed);
            _failed.IsVisible = failed > 0;
        }
        if (_list is not null)
        {
            AutomationProperties.SetName(_list, DisplayedTitle);
        }
    }

    private sealed class EnrichColumnPeer(EnrichColumn owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => ((EnrichColumn)Owner).DisplayedTitle;
    }
}
