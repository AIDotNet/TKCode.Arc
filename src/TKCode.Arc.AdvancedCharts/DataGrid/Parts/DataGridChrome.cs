using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The search field of the toolbar (its look comes from DataGridTheme).</summary>
[TemplatePart(ClearPart, typeof(Avalonia.Controls.Button))]
internal sealed class DataGridSearchBox : TextBox
{
    public const string ClearPart = "PART_Clear";

    private Avalonia.Controls.Button? _clear;

    public DataGridSearchBox()
    {
        FocusAdorner = null;
    }

    protected override Type StyleKeyOverride => typeof(DataGridSearchBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_clear is not null)
        {
            _clear.Click -= OnClearClick;
        }
        _clear = e.NameScope.Find<Avalonia.Controls.Button>(ClearPart);
        if (_clear is not null)
        {
            _clear.Click += OnClearClick;
            AutomationProperties.SetName(_clear, DataGridText.Get(DataGridLangResourceKind.ClearSearch));
        }
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        Text = string.Empty;
        Focus(NavigationMethod.Pointer);
    }
}

/// <summary>The input of a text, number or date filter cell (its look comes from DataGridTheme).</summary>
internal sealed class DataGridFilterBox : TextBox
{
    public DataGridFilterBox()
    {
        FocusAdorner = null;
    }

    protected override Type StyleKeyOverride => typeof(DataGridFilterBox);
}

/// <summary>A ghost toolbar button with a glyph, a label (hidden below 640px) and an optional count badge.</summary>
internal sealed class DataGridToolButton : Button
{
    private readonly ArcGlyph _glyph;
    private readonly TextBlock _label;
    private readonly Border _badge;
    private readonly TextBlock _badgeText;
    private bool _compact;

    public DataGridToolButton(Geometry icon, bool iconOnly = false)
    {
        Variant = ButtonVariant.Ghost;
        Size = ControlSize.Small;
        Height = 32;
        MinHeight = 32;
        IsIconOnly = iconOnly;
        _glyph = new ArcGlyph { Data = icon, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
        _label = new TextBlock { VerticalAlignment = VerticalAlignment.Center, IsVisible = !iconOnly };
        _badgeText = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        _badge = new Border
        {
            MinWidth = 18,
            Height = 18,
            Padding = new Thickness(5, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsVisible = false,
            Child = _badgeText
        };
        _badge.BindCornerRadius(ArcDesignTokenKind.RadiusStep9);
        ArcTokens.Bind(_badge, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children = { _glyph, _label, _badge }
        };
        UpdatePadding();
    }

    public bool IsIconOnly { get; }

    public Geometry Icon
    {
        get => _glyph.Data!;
        set => _glyph.Data = value;
    }

    public string Label
    {
        get => _label.Text ?? string.Empty;
        set
        {
            _label.Text = value;
            AutomationProperties.SetName(this, value);
        }
    }

    public string? Badge
    {
        get => _badgeText.Text;
        set
        {
            _badgeText.Text = value;
            _badge.IsVisible = !string.IsNullOrEmpty(value);
        }
    }

    public bool IsDanger
    {
        set
        {
            if (value)
            {
                ArcTokens.Bind(this, ForegroundProperty, ArcDesignTokenKind.Danger, Avalonia.Data.BindingPriority.LocalValue);
            }
        }
    }

    /// <summary>Below 640px of grid width labels hide and buttons become 32px squares.</summary>
    public void SetCompact(bool compact)
    {
        _compact = compact;
        _label.IsVisible = !compact && !IsIconOnly;
        UpdatePadding();
    }

    private void UpdatePadding()
    {
        var square = _compact || IsIconOnly;
        Padding = square ? new Thickness(0) : new Thickness(10, 0);
        Width = square ? 32 : double.NaN;
        MinWidth = 32;
    }
}

/// <summary>
/// The grid's toolbar: search, row count, undo and redo, filters, columns, density, group folding and export; while rows
/// are checked it swaps (rise and fade) to bulk actions: clear, a rolling count, copy, export, custom actions and delete.
/// </summary>
internal sealed class DataGridToolbar : Border
{
    private DataGrid? _grid;
    private readonly PresenceHost _normalHost;
    private readonly PresenceHost _bulkHost;
    private readonly DataGridSearchBox _search;
    private readonly TextBlock _rowCount;
    private readonly DataGridToolButton _addRow;
    private readonly DataGridToolButton _errors;
    private readonly DataGridToolButton _undo;
    private readonly DataGridToolButton _redo;
    private readonly StackPanel _undoGroup;
    private readonly DataGridToolButton _filter;
    private readonly DataGridToolButton _columns;
    private readonly DataGridToolButton _density;
    private readonly DataGridToolButton _groups;
    private readonly DataGridToolButton _export;
    private readonly DataGridToolButton _clearChecked;
    private readonly AnimatedCounter _checkedCount;
    private readonly TextBlock _checkedPrefix;
    private readonly TextBlock _checkedSuffix;
    private readonly DataGridToolButton _copy;
    private readonly DataGridToolButton _bulkExport;
    private readonly DataGridToolButton _delete;
    private readonly StackPanel _bulkActions;
    private bool _syncingSearch;
    private bool _compact;

    public DataGridToolbar()
    {
        _search = new DataGridSearchBox { VerticalAlignment = VerticalAlignment.Center };
        _search.TextChanged += OnSearchChanged;
        _search.KeyDown += OnSearchKeyDown;
        _rowCount = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        ArcTokens.Bind(_rowCount, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_rowCount, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        _rowCount.FontFeatures = DataGridPalette.TabularNumbers;
        AutomationProperties.SetLiveSetting(_rowCount, AutomationLiveSetting.Polite);
        _addRow = new DataGridToolButton(ArcIcons.Plus) { IsVisible = false };
        _addRow.Click += (_, _) => Run(g => g.AddRow());
        _errors = new DataGridToolButton(ArcIcons.TriangleAlert) { IsDanger = true, IsVisible = false };
        _errors.Click += (_, _) => Run(g => g.NextError());
        _undo = new DataGridToolButton(ArcIcons.Undo2, iconOnly: true);
        _redo = new DataGridToolButton(ArcIcons.Redo2, iconOnly: true);
        _undo.Click += (_, _) => Run(g =>
        {
            g.Undo();
            g.Viewport.FocusGrid();
        });
        _redo.Click += (_, _) => Run(g =>
        {
            g.Redo();
            g.Viewport.FocusGrid();
        });
        _undoGroup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { _undo, _redo } };
        _filter = new DataGridToolButton(ArcIcons.Filter);
        _filter.Click += (_, _) => Run(g => g.SetCurrentValue(DataGrid.IsFilterRowOpenProperty, !g.IsFilterRowOpen));
        _columns = new DataGridToolButton(ArcIcons.Columns2);
        _columns.Click += (_, _) => Run(g => g.OpenColumnsMenu(_columns));
        _density = new DataGridToolButton(DataGridIcons.Rows3, iconOnly: true);
        _density.Click += (_, _) => Run(g => g.OpenDensityMenu(_density));
        _groups = new DataGridToolButton(ArcIcons.ChevronsDownUp, iconOnly: true);
        _groups.Click += (_, _) => Run(g => g.SetAllGroupsCollapsed(!g.AnyCollapsed));
        _export = new DataGridToolButton(ArcIcons.Download);
        _export.Click += (_, _) => Run(g => g.OpenExport(_export, checkedOnly: false));
        var normal = new DockPanel { LastChildFill = false };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Children = { _search, _rowCount, _errors } };
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { _addRow, _undoGroup, _filter, _columns, _density, _groups, _export }
        };
        DockPanel.SetDock(left, Dock.Left);
        DockPanel.SetDock(right, Dock.Right);
        normal.Children.Add(left);
        normal.Children.Add(right);

        _clearChecked = new DataGridToolButton(ArcIcons.X, iconOnly: true);
        _clearChecked.Click += (_, _) => Run(g => g.ClearChecked());
        _checkedPrefix = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _checkedSuffix = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        _checkedCount = new AnimatedCounter { VerticalAlignment = VerticalAlignment.Center };
        DataGridStatusBar.Small(_checkedCount, ArcDesignTokenKind.TypeSM);
        _copy = new DataGridToolButton(ArcIcons.Copy);
        _copy.Click += (_, _) => Run(g => _ = g.CopyCheckedRowsAsync());
        _bulkExport = new DataGridToolButton(ArcIcons.Download);
        _bulkExport.Click += (_, _) => Run(g => g.OpenExport(_bulkExport, checkedOnly: true));
        _bulkActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        _delete = new DataGridToolButton(ArcIcons.Trash2) { IsDanger = true };
        _delete.Click += (_, _) => Run(g => g.DeleteChecked());
        var bulk = new DockPanel { LastChildFill = false };
        var bulkLeft = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                _clearChecked,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children = { _checkedPrefix, _checkedCount, _checkedSuffix }
                }
            }
        };
        var bulkRight = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { _copy, _bulkExport, _bulkActions, _delete }
        };
        DockPanel.SetDock(bulkLeft, Dock.Left);
        DockPanel.SetDock(bulkRight, Dock.Right);
        bulk.Children.Add(bulkLeft);
        bulk.Children.Add(bulkRight);

        _normalHost = new PresenceHost { Mode = PresenceMode.Drop, Offset = 6, IsPresent = true, Child = normal };
        _bulkHost = new PresenceHost { Mode = PresenceMode.Rise, Offset = 6, IsPresent = false, Child = bulk };
        Child = new Panel { Children = { _normalHost, _bulkHost } };
        Padding = new Thickness(10, 0);
        BorderThickness = new Thickness(0, 0, 0, 1);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(this, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
    }

    internal DataGridSearchBox SearchBox => _search;

    internal bool IsBulk => _bulkHost.IsPresent;

    private void Run(Action<DataGrid> action)
    {
        if (_grid is { } grid)
        {
            action(grid);
        }
    }

    public void Attach(DataGrid grid)
    {
        _grid = grid;
        RebuildBulkActions();
        UpdateTexts();
        UpdateState();
    }

    public void Detach() => _grid = null;

    public void SetResponsive(bool compact, bool minimal)
    {
        _compact = compact;
        foreach (var button in new[] { _addRow, _errors, _filter, _columns, _export, _copy, _bulkExport, _delete })
        {
            button.SetCompact(compact);
        }
        foreach (var button in _bulkActions.Children.OfType<DataGridToolButton>())
        {
            button.SetCompact(compact);
        }
        _rowCount.IsVisible = !minimal;
        _undoGroup.IsVisible = !minimal;
        _search.Width = compact ? 150 : double.NaN;
    }

    public void RebuildBulkActions()
    {
        _bulkActions.Children.Clear();
        if (_grid is not { } grid)
        {
            return;
        }
        foreach (var action in grid.BulkActions)
        {
            var button = new DataGridToolButton(action.Icon ?? ArcIcons.Zap) { Label = action.Label, IsDanger = action.IsDanger };
            button.SetCompact(_compact);
            var captured = action;
            button.Click += (_, _) => Run(g => g.InvokeBulkAction(captured));
            _bulkActions.Children.Add(button);
        }
    }

    public void UpdateTexts()
    {
        if (_grid is not { } grid)
        {
            return;
        }
        _search.PlaceholderText = DataGridText.Get(DataGridLangResourceKind.SearchPlaceholder);
        AutomationProperties.SetName(_search, DataGridText.Format(DataGridLangResourceKind.SearchLabel, grid.AccessibleName));
        _addRow.Label = DataGridText.Get(DataGridLangResourceKind.AddRow);
        _undo.Label = DataGridText.Get(DataGridLangResourceKind.Undo);
        _redo.Label = DataGridText.Get(DataGridLangResourceKind.Redo);
        _filter.Label = DataGridText.Get(DataGridLangResourceKind.Filter);
        _columns.Label = DataGridText.Get(DataGridLangResourceKind.Columns);
        _export.Label = DataGridText.Get(DataGridLangResourceKind.Export);
        _bulkExport.Label = DataGridText.Get(DataGridLangResourceKind.Export);
        _copy.Label = DataGridText.Get(DataGridLangResourceKind.Copy);
        _delete.Label = DataGridText.Get(DataGridLangResourceKind.Delete);
        _clearChecked.Label = DataGridText.Get(DataGridLangResourceKind.ClearRowSelection);
        var (prefix, suffix) = DataGridText.Around(DataGridLangResourceKind.CheckedCount);
        _checkedPrefix.Text = prefix;
        _checkedSuffix.Text = suffix;
        UpdateState();
    }

    public void UpdateUndo()
    {
        if (_grid is not { } grid)
        {
            return;
        }
        _undo.IsEnabled = grid.CanUndo;
        _redo.IsEnabled = grid.CanRedo;
    }

    public void UpdateState()
    {
        if (_grid is not { } grid)
        {
            return;
        }
        IsVisible = grid.ShowToolbar;
        var checkedCount = grid.CheckedCount;
        var bulk = grid.IsRowCheckEnabled && checkedCount > 0;
        _normalHost.IsPresent = !bulk;
        _bulkHost.IsPresent = bulk;
        _normalHost.IsHitTestVisible = !bulk;
        _bulkHost.IsHitTestVisible = bulk;
        _checkedCount.CultureName = CultureName(grid);
        _checkedCount.Value = checkedCount;
        _rowCount.Text = grid.RowCountText();
        UpdateUndo();
        var active = grid.ActiveFilterCount;
        _filter.Badge = active > 0 ? active.ToString(grid.EffectiveCulture) : null;
        AutomationProperties.SetName(_filter, DataGridText.Get(DataGridLangResourceKind.Filter));
        _filter.Classes.Set("pressed-state", grid.IsFilterRowOpen);
        var hidden = grid.Columns.Count - grid.VisibleAccessors.Length;
        _columns.Badge = hidden > 0 ? $"{grid.VisibleAccessors.Length}/{grid.Columns.Count}" : null;
        var densityLabel = grid.Density switch
        {
            DataGridDensity.Compact => DataGridText.Get(DataGridLangResourceKind.DensityCompact),
            DataGridDensity.Comfortable => DataGridText.Get(DataGridLangResourceKind.DensityComfortable),
            _ => DataGridText.Get(DataGridLangResourceKind.DensityStandard)
        };
        _density.Icon = grid.Density switch
        {
            DataGridDensity.Compact => DataGridIcons.Rows4,
            DataGridDensity.Comfortable => ArcIcons.Rows2,
            _ => DataGridIcons.Rows3
        };
        _density.Label = DataGridText.Format(DataGridLangResourceKind.DensityLabel, densityLabel);
        _groups.IsVisible = grid.View.IsGrouped;
        _groups.Icon = grid.AnyCollapsed ? ArcIcons.ChevronsUpDown : ArcIcons.ChevronsDownUp;
        _groups.Label = DataGridText.Get(grid.AnyCollapsed ? DataGridLangResourceKind.ExpandAllGroups : DataGridLangResourceKind.CollapseAllGroups);
        var formats = grid.ExportFormats;
        var single = formats switch
        {
            DataGridExportFormats.Csv => DataGridText.Get(DataGridLangResourceKind.ExportCsv),
            DataGridExportFormats.Xlsx => DataGridText.Get(DataGridLangResourceKind.ExportXlsx),
            _ => null
        };
        var exportName = single is null
            ? DataGridText.Get(DataGridLangResourceKind.Export)
            : DataGridText.Format(DataGridLangResourceKind.ExportFormatLabel, single);
        AutomationProperties.SetName(_export, exportName);
        AutomationProperties.SetName(_bulkExport, exportName);
        _export.IsVisible = !grid.HasDataSource && formats != DataGridExportFormats.None;
        _export.IsEnabled = grid.VisibleRowCount > 0 && grid.CanSaveFiles;
        _bulkExport.IsVisible = formats != DataGridExportFormats.None;
        _bulkExport.IsEnabled = grid.CanSaveFiles;
        _delete.IsVisible = grid.CanDeleteRows && !grid.HasDataSource;
        _addRow.IsVisible = grid.CanCreateRows;
        _errors.IsVisible = grid.ErrorCount > 0;
        if (grid.ErrorCount > 0)
        {
            _errors.Label = DataGridText.Count(DataGridLangResourceKind.ErrorsOne, DataGridLangResourceKind.ErrorsOther, grid.ErrorCount, grid.EffectiveCulture);
        }
        SyncSearch(grid);
    }

    private static string CultureName(DataGrid grid) => string.IsNullOrEmpty(grid.EffectiveCulture.Name) ? "en-US" : grid.EffectiveCulture.Name;

    private void SyncSearch(DataGrid grid)
    {
        var text = grid.SearchText ?? string.Empty;
        if (_search.Text != text)
        {
            _syncingSearch = true;
            _search.Text = text;
            _syncingSearch = false;
        }
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncingSearch || _grid is not { } grid)
        {
            return;
        }
        // Every change of SearchText filters, sorts and regroups the whole grid (and starts a server query in a data
        // source), so typing waits for a pause; clearing the box and leaving it with Enter apply at once.
        if (string.IsNullOrEmpty(_search.Text))
        {
            _searchDebounce?.Stop();
            grid.SetCurrentValue(DataGrid.SearchTextProperty, string.Empty);
            return;
        }
        if (_searchDebounce is null)
        {
            _searchDebounce = new DispatcherTimer { Interval = SearchDebounce };
            _searchDebounce.Tick += (_, _) => FlushSearch();
        }
        _searchDebounce.Stop();
        _searchDebounce.Start();
    }

    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(180);
    private DispatcherTimer? _searchDebounce;

    private void FlushSearch()
    {
        _searchDebounce?.Stop();
        if (_grid is { } grid && grid.SearchText != (_search.Text ?? string.Empty))
        {
            grid.SetCurrentValue(DataGrid.SearchTextProperty, _search.Text ?? string.Empty);
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (_grid is not { } grid)
        {
            return;
        }
        switch (e.Key)
        {
            case Key.Escape:
                e.Handled = true;
                if (!string.IsNullOrEmpty(_search.Text))
                {
                    _search.Text = string.Empty;
                }
                else
                {
                    grid.Viewport.FocusGrid();
                }
                break;
            case Key.Enter:
            case Key.Down:
                e.Handled = true;
                FlushSearch();
                grid.Viewport.FocusGrid();
                break;
        }
    }
}

/// <summary>
/// The status bar: the selection address, the Count, Sum and Average of the selected cells (rolling numbers), and status
/// messages that rise in from a soft blur, optionally with an action such as Undo.
/// </summary>
internal sealed class DataGridStatusBar : Border
{
    private DataGrid? _grid;
    private readonly Grid _layout;
    private readonly TextBlock _address;
    private readonly StackPanel _stats;
    private readonly (TextBlock Label, AnimatedCounter Value, StackPanel Host) _sum;
    private readonly (TextBlock Label, AnimatedCounter Value, StackPanel Host) _average;
    private readonly (TextBlock Label, AnimatedCounter Value, StackPanel Host) _count;
    private readonly StackPanel _messageHost;
    private readonly ArcContentSwap _message;
    private readonly Button _action;
    private Action? _actionRun;
    private DispatcherTimer? _timer;
    private bool _wrap;

    public DataGridStatusBar()
    {
        _address = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MinWidth = 44 };
        _address.FontFeatures = DataGridPalette.TabularNumbers;
        ArcTokens.Bind(_address, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _sum = Stat();
        _average = Stat();
        _count = Stat();
        _stats = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 14,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _sum.Host, _average.Host, _count.Host }
        };
        _message = new ArcContentSwap
        {
            Mode = ContentSwapMode.BlurSwap,
            HorizontalContentAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _action = new Button
        {
            Variant = ButtonVariant.Ghost,
            Size = ControlSize.Small,
            Height = 24,
            MinHeight = 24,
            Padding = new Thickness(8, 0),
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center
        };
        _action.Click += (_, _) =>
        {
            var run = _actionRun;
            ClearMessage();
            run?.Invoke();
        };
        _messageHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _message, _action }
        };
        _layout = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"), RowDefinitions = new RowDefinitions("Auto,Auto") };
        _layout.Children.Add(_address);
        Grid.SetColumn(_stats, 1);
        _layout.Children.Add(_stats);
        _layout.Children.Add(_messageHost);
        Place(false);
        Child = _layout;
        MinHeight = 34;
        Padding = new Thickness(14, 6);
        BorderThickness = new Thickness(0, 1, 0, 0);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(this, TextElement.ForegroundProperty, ArcDesignTokenKind.TextMuted);
    }

    private static (TextBlock, AnimatedCounter, StackPanel) Stat()
    {
        var label = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var value = new AnimatedCounter { VerticalAlignment = VerticalAlignment.Center };
        Small(value, ArcDesignTokenKind.TypeXS);
        var host = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, Children = { label, value } };
        return (label, value, host);
    }

    /// <summary>Gives a rolling counter the body font at a small size (AnimatedCounter defaults to a display size).</summary>
    internal static void Small(AnimatedCounter counter, ArcDesignTokenKind size)
    {
        ArcTokens.Bind(counter, AnimatedCounter.FontSizeProperty, size);
        ArcTokens.Bind(counter, AnimatedCounter.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(counter, AnimatedCounter.FontWeightProperty, ArcDesignTokenKind.WeightMedium);
        ArcTokens.Bind(counter, AnimatedCounter.ForegroundProperty, ArcDesignTokenKind.Foreground);
    }

    internal string? MessageText => _message.Content as string;

    internal bool HasAction => _action.IsVisible;

    internal void InvokeAction() => _action.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

    public void Attach(DataGrid grid)
    {
        _grid = grid;
        UpdateTexts();
        UpdateStats();
    }

    public void SetWrap(bool wrap)
    {
        _wrap = wrap;
        Place(wrap);
    }

    private void Place(bool wrap)
    {
        Grid.SetRow(_messageHost, wrap ? 1 : 0);
        Grid.SetColumn(_messageHost, wrap ? 0 : 2);
        Grid.SetColumnSpan(_messageHost, wrap ? 3 : 1);
        _messageHost.HorizontalAlignment = wrap ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        _messageHost.Margin = wrap ? new Thickness(0, 4, 0, 0) : new Thickness(12, 0, 0, 0);
    }

    public void UpdateTexts()
    {
        _sum.Label.Text = DataGridText.Get(DataGridLangResourceKind.StatSum);
        _average.Label.Text = DataGridText.Get(DataGridLangResourceKind.StatAverage);
        _count.Label.Text = DataGridText.Get(DataGridLangResourceKind.StatCount);
        UpdateStats();
    }

    public void UpdateStats()
    {
        if (_grid is not { } grid)
        {
            return;
        }
        var show = grid.ShowSelectionStats;
        _address.IsVisible = show;
        _stats.IsVisible = show && grid.HasCells;
        if (!show)
        {
            return;
        }
        _address.Text = grid.SelectionAddress();
        AutomationProperties.SetName(_address, DataGridText.Format(DataGridLangResourceKind.SelectionAddress, _address.Text));
        if (!grid.HasCells)
        {
            return;
        }
        var (filled, numbers, sum, prefix, suffix) = grid.SelectionStats();
        var culture = string.IsNullOrEmpty(grid.EffectiveCulture.Name) ? "en-US" : grid.EffectiveCulture.Name;
        var many = numbers > 1;
        _sum.Host.IsVisible = many;
        _average.Host.IsVisible = many;
        if (many)
        {
            var roundedSum = Math.Round(sum, 2);
            Set(_sum.Value, (double)roundedSum, roundedSum == decimal.Truncate(roundedSum) ? 0 : 2, prefix, suffix, culture);
            var average = Math.Round(sum / numbers, 1);
            Set(_average.Value, (double)average, average == decimal.Truncate(average) ? 0 : 1, prefix, suffix, culture);
        }
        Set(_count.Value, filled, 0, null, null, culture);
    }

    private static void Set(AnimatedCounter counter, double value, int decimals, string? prefix, string? suffix, string culture)
    {
        counter.CultureName = culture;
        counter.Decimals = decimals;
        counter.Prefix = string.IsNullOrEmpty(prefix) ? null : prefix;
        counter.Suffix = string.IsNullOrEmpty(suffix) ? null : suffix;
        counter.Value = value;
    }

    /// <summary>Shows a message for <paramref name="duration"/>, optionally with an action button.</summary>
    public void ShowMessage(string text, string? actionLabel, Action? action, TimeSpan duration)
    {
        _message.Content = text;
        _action.IsVisible = actionLabel is not null;
        _action.Content = actionLabel;
        _actionRun = action;
        _timer?.Stop();
        _timer = new DispatcherTimer { Interval = duration };
        _timer.Tick += (_, _) =>
        {
            _timer?.Stop();
            ClearMessage();
        };
        _timer.Start();
    }

    private void ClearMessage()
    {
        _timer?.Stop();
        _message.Content = null;
        _action.IsVisible = false;
        _actionRun = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
    }
}
