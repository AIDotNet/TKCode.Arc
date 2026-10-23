using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// A table change in the side panel's change card (Arc F12): the whole NEW table redrawn small (12 / 16.8, tabular
/// figures, headers in TextMuted, cells in TextSecondary, cells padded 3 8 3 0, no row lines) where every cell the old
/// table did not have (a new column, a new row, a changed text) sits on the inserted tint of <see cref="AiDiffText"/>'s
/// card in Foreground. Removed rows and columns are not shown. <see cref="Summary"/> is the card's header note: one added
/// column reads through <see cref="AddsColumnFormat"/> ("Adds Sync column"), any other change is
/// <see cref="ChangesText"/>; both come from the owner. Nothing animates.
/// </summary>
/// <remarks>
/// The tables are read when <see cref="Before"/> or <see cref="After"/> is set (they are snapshots: the side panel keeps a
/// clone of the block); call <see cref="Refresh"/> after changing one in place. Columns are matched by header, rows by the
/// row-header text (see <see cref="AiDiffTableModel"/>). Screen readers get <see cref="Summary"/> as the table's name and
/// the cells' texts.
/// </remarks>
[TemplatePart(GridPart, typeof(AiDiffTableGrid))]
internal class AiDiffTable : TemplatedControl
{
    public const string GridPart = "PART_Grid";

    #region Properties

    /// <summary>The table before the change.</summary>
    public static readonly StyledProperty<AiPanelTable?> BeforeProperty =
        AvaloniaProperty.Register<AiDiffTable, AiPanelTable?>(nameof(Before));

    /// <summary>The table after the change (the one shown).</summary>
    public static readonly StyledProperty<AiPanelTable?> AfterProperty =
        AvaloniaProperty.Register<AiDiffTable, AiPanelTable?>(nameof(After));

    /// <summary>The summary of a change that only adds one column; the argument is its header ("Adds {0} column").</summary>
    public static readonly StyledProperty<Func<string, string>?> AddsColumnFormatProperty =
        AvaloniaProperty.Register<AiDiffTable, Func<string, string>?>(nameof(AddsColumnFormat));

    /// <summary>The summary of any other change ("Changes the table").</summary>
    public static readonly StyledProperty<string?> ChangesTextProperty =
        AvaloniaProperty.Register<AiDiffTable, string?>(nameof(ChangesText));

    /// <summary>Header text (the theme gives TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> HeaderForegroundProperty =
        AvaloniaProperty.Register<AiDiffTable, IBrush?>(nameof(HeaderForeground));

    /// <summary>Text of inserted cells (the theme gives Foreground).</summary>
    public static readonly StyledProperty<IBrush?> InsertForegroundProperty =
        AvaloniaProperty.Register<AiDiffTable, IBrush?>(nameof(InsertForeground));

    /// <summary>The color of the inserted tint (the theme gives Accent), used at <see cref="InsertTintOpacity"/>.</summary>
    public static readonly StyledProperty<IBrush?> InsertTintProperty =
        AvaloniaProperty.Register<AiDiffTable, IBrush?>(nameof(InsertTint));

    /// <summary>Strength of the inserted tint (0.13).</summary>
    public static readonly StyledProperty<double> InsertTintOpacityProperty =
        AvaloniaProperty.Register<AiDiffTable, double>(nameof(InsertTintOpacity), 0.13);

    /// <summary>Line height of the cells (16.8).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        Avalonia.Controls.TextBlock.LineHeightProperty.AddOwner<AiDiffTable>();

    /// <summary>Body cells shown as inserted (new rows, new columns, changed texts; header cells not counted).</summary>
    public static readonly DirectProperty<AiDiffTable, int> InsertedCellsProperty =
        AvaloniaProperty.RegisterDirect<AiDiffTable, int>(nameof(InsertedCells), o => o.InsertedCells);

    /// <summary>The card's note on the change: <see cref="AddsColumnFormat"/> for one added column, else <see cref="ChangesText"/>; empty without changes.</summary>
    public static readonly DirectProperty<AiDiffTable, string> SummaryProperty =
        AvaloniaProperty.RegisterDirect<AiDiffTable, string>(nameof(Summary), o => o.Summary);

    /// <summary>Whether the tables differ.</summary>
    public static readonly DirectProperty<AiDiffTable, bool> HasChangesProperty =
        AvaloniaProperty.RegisterDirect<AiDiffTable, bool>(nameof(HasChanges), o => o.HasChanges);

    /// <inheritdoc cref="BeforeProperty"/>
    public AiPanelTable? Before { get => GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }

    /// <inheritdoc cref="AfterProperty"/>
    public AiPanelTable? After { get => GetValue(AfterProperty); set => SetValue(AfterProperty, value); }

    /// <inheritdoc cref="AddsColumnFormatProperty"/>
    public Func<string, string>? AddsColumnFormat { get => GetValue(AddsColumnFormatProperty); set => SetValue(AddsColumnFormatProperty, value); }

    /// <inheritdoc cref="ChangesTextProperty"/>
    public string? ChangesText { get => GetValue(ChangesTextProperty); set => SetValue(ChangesTextProperty, value); }

    /// <inheritdoc cref="HeaderForegroundProperty"/>
    public IBrush? HeaderForeground { get => GetValue(HeaderForegroundProperty); set => SetValue(HeaderForegroundProperty, value); }

    /// <inheritdoc cref="InsertForegroundProperty"/>
    public IBrush? InsertForeground { get => GetValue(InsertForegroundProperty); set => SetValue(InsertForegroundProperty, value); }

    /// <inheritdoc cref="InsertTintProperty"/>
    public IBrush? InsertTint { get => GetValue(InsertTintProperty); set => SetValue(InsertTintProperty, value); }

    /// <inheritdoc cref="InsertTintOpacityProperty"/>
    public double InsertTintOpacity { get => GetValue(InsertTintOpacityProperty); set => SetValue(InsertTintOpacityProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <inheritdoc cref="InsertedCellsProperty"/>
    public int InsertedCells
    {
        get => _insertedCells;
        private set => SetAndRaise(InsertedCellsProperty, ref _insertedCells, value);
    }

    /// <inheritdoc cref="SummaryProperty"/>
    public string Summary
    {
        get => _summary;
        private set => SetAndRaise(SummaryProperty, ref _summary, value);
    }

    /// <inheritdoc cref="HasChangesProperty"/>
    public bool HasChanges
    {
        get => _hasChanges;
        private set => SetAndRaise(HasChangesProperty, ref _hasChanges, value);
    }

    #endregion

    private static readonly FontFeatureCollection TabularFigures = [FontFeature.Parse("tnum")];

    private AiDiffTableModel _model = AiDiffTableModel.Empty;
    private AiDiffTableGrid? _grid;
    private int _insertedCells;
    private string _summary = string.Empty;
    private bool _hasChanges;

    /// <summary>The comparison shown.</summary>
    internal AiDiffTableModel Model => _model;

    /// <summary>The grid of the template.</summary>
    internal AiDiffTableGrid? Grid => _grid;

    /// <summary>Compares the tables again (one was changed in place).</summary>
    public void Refresh()
    {
        _model = AiDiffTableModel.Compare(Before, After);
        InsertedCells = _model.InsertedCells;
        HasChanges = _model.HasChanges;
        UpdateSummary();
        BuildCells();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _grid = e.NameScope.Find<AiDiffTableGrid>(GridPart);
        BuildCells();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        var property = change.Property;
        if (property == BeforeProperty || property == AfterProperty)
        {
            Refresh();
        }
        else if (property == AddsColumnFormatProperty || property == ChangesTextProperty)
        {
            UpdateSummary();
        }
        else if (property == ForegroundProperty || property == HeaderForegroundProperty || property == InsertForegroundProperty ||
                 property == InsertTintProperty || property == InsertTintOpacityProperty || property == FontSizeProperty ||
                 property == FontFamilyProperty || property == LineHeightProperty)
        {
            BuildCells();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new AiDiffTableAutomationPeer(this);

    private void UpdateSummary()
    {
        Summary = !_model.HasChanges ? string.Empty
            : _model.IsSingleAddedColumn && AddsColumnFormat is { } format ? format(_model.AddedColumns[0])
            : ChangesText ?? string.Empty;
    }

    /// <summary>Fills the grid: the header row, then the rows, each cell plain or tinted.</summary>
    private void BuildCells()
    {
        if (_grid is null)
        {
            return;
        }
        var children = _grid.Children;
        children.Clear();
        var columns = _model.Columns;
        _grid.ColumnCount = columns.Count;
        if (columns.Count == 0)
        {
            return;
        }
        var tint = InsertTint is ISolidColorBrush solid
            ? new ImmutableSolidColorBrush(PromptTone.WithOpacity(PromptTone.WithOpacity(solid.Color, solid.Opacity), InsertTintOpacity))
            : null;
        foreach (var column in columns)
        {
            children.Add(Cell(column.Header, column.IsInserted, true, tint));
        }
        foreach (var row in _model.Rows)
        {
            foreach (var cell in row)
            {
                children.Add(Cell(cell.Text, cell.IsInserted, false, tint));
            }
        }
    }

    private AiDiffTableCell Cell(string text, bool inserted, bool header, IBrush? tint)
    {
        var cell = new AiDiffTableCell
        {
            Text = text,
            IsHeader = header,
            FontFamily = FontFamily,
            FontSize = FontSize,
            LineHeight = LineHeight,
            FontFeatures = TabularFigures,
            TextWrapping = TextWrapping.Wrap,
            Foreground = inserted ? InsertForeground ?? Foreground : header ? HeaderForeground ?? Foreground : Foreground
        };
        if (inserted)
        {
            cell.Tint = tint;
        }
        return cell;
    }

    /// <summary>A table named by the summary (unless the owner names it).</summary>
    private sealed class AiDiffTableAutomationPeer : ControlAutomationPeer
    {
        public AiDiffTableAutomationPeer(AiDiffTable owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Table;

        protected override string? GetNameCore()
        {
            var name = base.GetNameCore();
            return string.IsNullOrEmpty(name) ? ((AiDiffTable)Owner).Summary : name;
        }
    }
}
