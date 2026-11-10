using TKCode.Arc.DesignTokens;
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

[TemplatePart(BodyPart, typeof(Decorator))]
[TemplatePart(AnnouncerPart, typeof(AnalyticsAnnouncer))]
[PseudoClasses(FramedPseudoClass, CompactPseudoClass, DraggingPseudoClass)]
public partial class PivotBuilder
{
    public const string BodyPart = "PART_Body";
    public const string AnnouncerPart = "PART_Announcer";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the block is at most <c>CompactWidth</c> (760) wide: the field list sits above the zones.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while a chip is being dragged.</summary>
    public const string DraggingPseudoClass = ":dragging";

    internal static readonly StyledProperty<double> CompactWidthProperty =
        AvaloniaProperty.Register<PivotBuilder, double>(nameof(CompactWidth), 760);

    internal double CompactWidth { get => GetValue(CompactWidthProperty); set => SetValue(CompactWidthProperty, value); }

    private PivotBuilderField[]? _sampleFields;
    private PivotBuilderSampleSale[]? _sampleRecords;
    private bool _compact;
    private bool _layoutSeeded;
    private bool _updating;

    private Decorator? _body;
    private AnalyticsAnnouncer? _announcer;
    private Grid? _columns;
    private PivotBuilderZoneBox? _fieldBox;
    private readonly Dictionary<PivotBuilderZone, PivotBuilderZoneBox> _zones = new();
    private Control? _side;
    private Control? _main;
    private Table? _table;
    private TextBlock? _size;
    private TextBlock? _noValues;
    private MenuSurface? _menu;
    private PivotBuilderChip? _menuChip;

    static PivotBuilder()
    {
        FocusableProperty.OverrideDefaultValue<PivotBuilder>(false);
    }

    public PivotBuilder()
    {
        PseudoClasses.Set(FramedPseudoClass, true);
        Fields.CollectionChanged += OnInputsChanged;
        RowFields.CollectionChanged += OnLayoutCollectionChanged;
        ColumnFields.CollectionChanged += OnLayoutCollectionChanged;
        ValueFields.CollectionChanged += OnLayoutCollectionChanged;
    }

    /// <summary>True while the block pivots its built-in invented sales (no <see cref="ItemsSource"/> and no <see cref="Fields"/>).</summary>
    public bool IsShowingSamples => ItemsSource is null && Fields.Count == 0;

    /// <summary>The fields in use (yours, or the samples').</summary>
    public IReadOnlyList<PivotBuilderField> EffectiveFields => IsShowingSamples ? _sampleFields ??= PivotBuilderSamples.Fields() : Fields;

    private IEnumerable EffectiveRecords => IsShowingSamples ? _sampleRecords ??= PivotBuilderSamples.Sales() : ItemsSource ?? Array.Empty<object>();

    internal Table? Table => _table;

    internal AnalyticsAnnouncer? Announcer => _announcer;

    internal IReadOnlyDictionary<PivotBuilderZone, PivotBuilderZoneBox> Zones => _zones;

    internal PivotBuilderZoneBox? FieldBox => _fieldBox;

    internal MenuSurface? Menu => _menu;

    internal bool IsCompact => _compact;

    /// <summary>The zone a field is in (None while it waits in the field list).</summary>
    public PivotBuilderZone ZoneOf(string fieldKey)
        => RowFields.Contains(fieldKey) ? PivotBuilderZone.Rows
            : ColumnFields.Contains(fieldKey) ? PivotBuilderZone.Columns
            : ValueFields.Any(v => v.FieldKey == fieldKey) ? PivotBuilderZone.Values
            : PivotBuilderZone.None;

    /// <summary>
    /// Moves a field to <paramref name="zone"/> at <paramref name="index"/> (the end when null), taking it out of the zone it
    /// was in; None takes it out of the layout. A non-numeric field placed in Values is counted.
    /// </summary>
    public void MoveField(string fieldKey, PivotBuilderZone zone, int? index = null)
    {
        if (EffectiveFields.FirstOrDefault(f => f.Key == fieldKey) is not { } field)
        {
            return;
        }
        _updating = true;
        try
        {
            var aggregation = ValueFields.FirstOrDefault(v => v.FieldKey == fieldKey)?.Aggregation
                              ?? (field.IsNumeric ? PivotBuilderAggregation.Sum : PivotBuilderAggregation.Count);
            RowFields.Remove(fieldKey);
            ColumnFields.Remove(fieldKey);
            foreach (var v in ValueFields.Where(v => v.FieldKey == fieldKey).ToArray())
            {
                ValueFields.Remove(v);
            }
            switch (zone)
            {
                case PivotBuilderZone.Rows:
                    RowFields.Insert(Math.Clamp(index ?? RowFields.Count, 0, RowFields.Count), fieldKey);
                    break;
                case PivotBuilderZone.Columns:
                    ColumnFields.Insert(Math.Clamp(index ?? ColumnFields.Count, 0, ColumnFields.Count), fieldKey);
                    break;
                case PivotBuilderZone.Values:
                    if (!field.IsNumeric)
                    {
                        aggregation = PivotBuilderAggregation.Count;
                    }
                    ValueFields.Insert(Math.Clamp(index ?? ValueFields.Count, 0, ValueFields.Count), new PivotBuilderValue(fieldKey, aggregation));
                    break;
            }
        }
        finally
        {
            _updating = false;
        }
        var labels = Labels;
        _announcer?.Announce(string.Format(CultureInfo.CurrentCulture, labels.MovedFormat, field.Title, ZoneName(zone)));
        Rebuild();
        RaiseEvent(new PivotBuilderLayoutChangedEventArgs(LayoutChangedEvent, fieldKey, zone));
    }

    /// <summary>Sets the aggregation of a field in Values (Count only, for a non-numeric field).</summary>
    public void SetAggregation(string fieldKey, PivotBuilderAggregation aggregation)
    {
        var value = ValueFields.FirstOrDefault(v => v.FieldKey == fieldKey);
        var field = EffectiveFields.FirstOrDefault(f => f.Key == fieldKey);
        if (value is null || field is null || (!field.IsNumeric && aggregation != PivotBuilderAggregation.Count))
        {
            return;
        }
        value.Aggregation = aggregation;
        Rebuild();
        RaiseEvent(new PivotBuilderLayoutChangedEventArgs(LayoutChangedEvent, fieldKey, PivotBuilderZone.Values));
    }

    /// <summary>Re-reads the records and recomputes the table (records and fields are not watched item by item).</summary>
    public void Refresh() => Rebuild();

    private void OnInputsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void OnLayoutCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_updating)
        {
            Rebuild();
        }
    }

    private void SeedLayout()
    {
        if (_layoutSeeded)
        {
            return;
        }
        _layoutSeeded = true;
        if (IsShowingSamples && RowFields.Count == 0 && ColumnFields.Count == 0 && ValueFields.Count == 0)
        {
            _updating = true;
            RowFields.Add("region");
            ColumnFields.Add("quarter");
            ValueFields.Add(new PivotBuilderValue("revenue"));
            _updating = false;
        }
    }

    private string ZoneName(PivotBuilderZone zone) => (int)zone < Labels.Zones.Count ? Labels.Zones[(int)zone] : zone.ToString();

    #region Template

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        SeedLayout();
        if (_body is not null)
        {
            _body.Child = null;
        }
        _body = e.NameScope.Find<Decorator>(BodyPart);
        _announcer = e.NameScope.Find<AnalyticsAnnouncer>(AnnouncerPart);
        if (_body is not null)
        {
            _body.Child = BuildBody();
            Rebuild();
        }
    }

    private Control BuildBody()
    {
        var labels = Labels;
        _zones.Clear();
        _fieldBox = Zone(PivotBuilderZone.None, labels.Fields);
        _side = _fieldBox;

        var zoneRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 10 };
        var index = 0;
        foreach (var zone in new[] { PivotBuilderZone.Rows, PivotBuilderZone.Columns, PivotBuilderZone.Values })
        {
            var box = Zone(zone, ZoneName(zone));
            _zones[zone] = box;
            Grid.SetColumn(box, index++);
            zoneRow.Children.Add(box);
        }

        _size = new TextBlock { LineHeight = 16.8 };
        ArcTokens.Bind(_size, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_size, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        _noValues = new TextBlock { Text = labels.NoValues, TextWrapping = TextWrapping.Wrap, LineHeight = 19.6 };
        ArcTokens.Bind(_noValues, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_noValues, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        _table = new Table { Caption = labels.TableCaption, MaxHeight = 460, MinWidth = 280 };
        var main = new StackPanel { Spacing = 12 };
        main.Children.Add(zoneRow);
        main.Children.Add(_size);
        main.Children.Add(_noValues);
        main.Children.Add(_table);
        _main = main;

        _columns = new Grid();
        _columns.Children.Add(_side);
        _columns.Children.Add(_main);
        ApplyColumns();
        return _columns;
    }

    private PivotBuilderZoneBox Zone(PivotBuilderZone zone, string title)
    {
        var box = new PivotBuilderZoneBox(zone);
        var heading = new TextBlock { Text = title, FontWeight = FontWeight.Medium, LineHeight = 16.8 };
        ArcTokens.Bind(heading, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(heading, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        AutomationProperties.SetHeadingLevel(heading, 3);
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(heading);
        stack.Children.Add(box.Chips);
        box.Content = stack;
        AutomationProperties.SetName(box, title);
        return box;
    }

    private void ApplyColumns()
    {
        if (_columns is null || _side is null || _main is null)
        {
            return;
        }
        _columns.ColumnDefinitions.Clear();
        _columns.RowDefinitions.Clear();
        if (_compact)
        {
            _columns.RowDefinitions = new RowDefinitions("Auto,Auto");
            _columns.RowSpacing = 12;
            _columns.ColumnSpacing = 0;
            Grid.SetColumn(_main, 0);
            Grid.SetRow(_main, 1);
        }
        else
        {
            _columns.ColumnDefinitions = new ColumnDefinitions("200,*");
            _columns.ColumnSpacing = 16;
            _columns.RowSpacing = 0;
            Grid.SetColumn(_main, 1);
            Grid.SetRow(_main, 0);
        }
    }

    #endregion

    #region Chips, drag and the menu

    private void BuildChips()
    {
        if (_fieldBox is null)
        {
            return;
        }
        var fields = EffectiveFields.ToDictionary(f => f.Key);
        _fieldBox.Chips.Children.Clear();
        foreach (var field in EffectiveFields.Where(f => ZoneOf(f.Key) == PivotBuilderZone.None))
        {
            _fieldBox.Chips.Children.Add(Chip(field, PivotBuilderZone.None, null));
        }
        Fill(PivotBuilderZone.Rows, RowFields.Where(fields.ContainsKey).Select(k => (fields[k], (PivotBuilderValue?)null)));
        Fill(PivotBuilderZone.Columns, ColumnFields.Where(fields.ContainsKey).Select(k => (fields[k], (PivotBuilderValue?)null)));
        Fill(PivotBuilderZone.Values, ValueFields.Where(v => fields.ContainsKey(v.FieldKey)).Select(v => (fields[v.FieldKey], (PivotBuilderValue?)v)));
    }

    private void Fill(PivotBuilderZone zone, IEnumerable<(PivotBuilderField Field, PivotBuilderValue? Value)> items)
    {
        if (!_zones.TryGetValue(zone, out var box))
        {
            return;
        }
        box.Chips.Children.Clear();
        foreach (var (field, value) in items)
        {
            box.Chips.Children.Add(Chip(field, zone, value));
        }
        if (box.Chips.Children.Count == 0)
        {
            var hint = new TextBlock { Text = Labels.DropHere, LineHeight = 16.8 };
            ArcTokens.Bind(hint, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            ArcTokens.Bind(hint, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
            box.Chips.Children.Add(hint);
        }
    }

    private PivotBuilderChip Chip(PivotBuilderField field, PivotBuilderZone zone, PivotBuilderValue? value)
    {
        var labels = Labels;
        var text = value is null ? field.Title : string.Format(CultureInfo.CurrentCulture, labels.ValueHeaderFormat,
            labels.Aggregations[(int)value.Aggregation], field.Title);
        var chip = new PivotBuilderChip(field.Key, zone)
        {
            Content = text,
            Owner = this,
            DragMoved = OnChipDragMoved,
            Dropped = OnChipDropped,
            RemoveRequested = c => MoveField(c.FieldKey, PivotBuilderZone.None),
        };
        AutomationProperties.SetName(chip, string.Format(CultureInfo.CurrentCulture, labels.ChipFormat, text, ZoneName(zone)));
        chip.Click += (_, _) =>
        {
            if (!chip.IsDragging)
            {
                OpenMenu(chip);
            }
        };
        return chip;
    }

    private PivotBuilderZoneBox? BoxAt(Point point)
    {
        foreach (var box in _zones.Values.Append(_fieldBox!))
        {
            if (box.TranslatePoint(default, this) is { } origin && new Rect(origin, box.Bounds.Size).Contains(point))
            {
                return box;
            }
        }
        return null;
    }

    private void OnChipDragMoved(PivotBuilderChip chip, Point? point)
    {
        PseudoClasses.Set(DraggingPseudoClass, point is not null);
        var target = point is { } p ? BoxAt(p) : null;
        foreach (var box in _zones.Values.Append(_fieldBox!))
        {
            box.IsTarget = ReferenceEquals(box, target) && box.Zone != chip.Zone;
        }
    }

    private void OnChipDropped(PivotBuilderChip chip, Point point)
    {
        foreach (var b in _zones.Values.Append(_fieldBox!))
        {
            b.IsTarget = false;
        }
        if (BoxAt(point) is not { } box)
        {
            return;
        }
        // The drop index: the chips of the zone whose middle lies before the pointer (on its line, or on lines above).
        var index = 0;
        foreach (var other in box.Chips.Children.OfType<PivotBuilderChip>())
        {
            if (ReferenceEquals(other, chip) || other.TranslatePoint(default, this) is not { } at)
            {
                continue;
            }
            var mid = new Point(at.X + other.Bounds.Width / 2, at.Y + other.Bounds.Height / 2);
            if (mid.Y < point.Y - other.Bounds.Height / 2 || (Math.Abs(mid.Y - point.Y) <= other.Bounds.Height / 2 && mid.X < point.X))
            {
                index++;
            }
        }
        if (box.Zone == chip.Zone && box.Zone == PivotBuilderZone.None)
        {
            return;
        }
        MoveField(chip.FieldKey, box.Zone, index);
        FocusChip(chip.FieldKey, NavigationMethod.Pointer);
    }

    private void OpenMenu(PivotBuilderChip chip)
    {
        var labels = Labels;
        if (_menu is null)
        {
            _menu = new MenuSurface { Side = MenuSide.Bottom, Alignment = MenuAlignment.Start };
            _menu.ItemClicked += OnMenuItemClicked;
        }
        _menuChip = chip;
        _menu.Items.Clear();
        foreach (var zone in new[] { PivotBuilderZone.Rows, PivotBuilderZone.Columns, PivotBuilderZone.Values })
        {
            if (zone != chip.Zone)
            {
                _menu.Items.Add(new MenuEntry { Header = string.Format(CultureInfo.CurrentCulture, labels.MoveToFormat, ZoneName(zone)), Tag = zone });
            }
        }
        if (chip.Zone != PivotBuilderZone.None)
        {
            var siblings = (chip.Parent as Panel)?.Children.OfType<PivotBuilderChip>().ToList() ?? [];
            var at = siblings.IndexOf(chip);
            _menu.Items.Add(new MenuEntrySeparator());
            _menu.Items.Add(new MenuEntry { Header = labels.MoveEarlier, Tag = -1, IsEnabled = at > 0 });
            _menu.Items.Add(new MenuEntry { Header = labels.MoveLater, Tag = +1, IsEnabled = at >= 0 && at < siblings.Count - 1 });
            if (chip.Zone == PivotBuilderZone.Values && EffectiveFields.FirstOrDefault(f => f.Key == chip.FieldKey) is { IsNumeric: true })
            {
                var current = ValueFields.FirstOrDefault(v => v.FieldKey == chip.FieldKey)?.Aggregation;
                _menu.Items.Add(new MenuEntrySeparator());
                foreach (var aggregation in Enum.GetValues<PivotBuilderAggregation>())
                {
                    _menu.Items.Add(new MenuEntry
                    {
                        Header = labels.Aggregations[(int)aggregation],
                        ToggleType = MenuEntryToggleType.Radio,
                        GroupName = "pivot-aggregation",
                        IsChecked = aggregation == current,
                        Tag = aggregation,
                    });
                }
            }
            _menu.Items.Add(new MenuEntrySeparator());
            _menu.Items.Add(new MenuEntry { Header = labels.Remove, Tag = PivotBuilderZone.None, IsDestructive = true });
        }
        _menu.Open(chip, MenuOpenFocus.First);
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (_menuChip is not { } chip)
        {
            return;
        }
        var key = chip.FieldKey;
        switch (e.Entry.Tag)
        {
            case PivotBuilderZone zone:
                MoveField(key, zone);
                break;
            case PivotBuilderAggregation aggregation:
                SetAggregation(key, aggregation);
                break;
            case int step:
                var siblings = (chip.Parent as Panel)?.Children.OfType<PivotBuilderChip>().ToList() ?? [];
                var at = siblings.IndexOf(chip);
                if (at >= 0)
                {
                    MoveField(key, chip.Zone, Math.Clamp(at + step, 0, siblings.Count - 1));
                }
                break;
        }
        FocusChip(key, NavigationMethod.Tab);
    }

    private void FocusChip(string fieldKey, NavigationMethod method)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var box in _zones.Values.Append(_fieldBox!))
            {
                if (box.Chips.Children.OfType<PivotBuilderChip>().FirstOrDefault(c => c.FieldKey == fieldKey) is { } chip)
                {
                    chip.Focus(method);
                    return;
                }
            }
        }, DispatcherPriority.Background);
    }

    #endregion

    #region The table

    private void Rebuild()
    {
        var fields = EffectiveFields.GroupBy(f => f.Key).ToDictionary(g => g.Key, g => g.First());
        var records = EffectiveRecords;
        Result = PivotBuilderEngine.Compute(records, fields, RowFields, ColumnFields, ValueFields);
        if (_table is null)
        {
            return;
        }
        BuildChips();
        BuildTable(Result, fields, records.Cast<object>().Count());
    }

    private void BuildTable(PivotBuilderResult result, IReadOnlyDictionary<string, PivotBuilderField> fields, int recordCount)
    {
        var labels = Labels;
        var rows = RowFields.Where(fields.ContainsKey).Select(k => fields[k]).ToArray();
        var columns = ColumnFields.Where(fields.ContainsKey).ToArray();
        var values = result.Values;
        _noValues!.IsVisible = values.Count == 0;
        _table!.IsVisible = values.Count > 0;
        _size!.Text = string.Format(CultureInfo.CurrentCulture, labels.SizeFormat, result.RowKeys.Count, result.ColumnKeys.Count, recordCount);
        if (values.Count == 0)
        {
            _table.ItemsSource = null;
            _table.Columns.Clear();
            return;
        }

        string Format(int v, double? x)
        {
            if (x is not { } n)
            {
                return "—";
            }
            var field = fields[values[v].FieldKey];
            var format = values[v].Aggregation == PivotBuilderAggregation.Count ? "N0"
                : field.Format ?? (values[v].Aggregation == PivotBuilderAggregation.Average ? "N1" : "N0");
            return n.ToString(format, CultureInfo.CurrentCulture);
        }

        string ValueTitle(int v) => values.Count == 1 && columns.Length > 0
            ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, labels.ValueHeaderFormat, labels.Aggregations[(int)values[v].Aggregation], fields[values[v].FieldKey].Title);

        string Header(string columnKey, int v)
        {
            var title = ValueTitle(v);
            return columnKey.Length == 0 ? title : title.Length == 0 ? columnKey : columnKey + " · " + title;
        }

        var tableColumns = new List<DataTableColumn>();
        for (var i = 0; i < Math.Max(1, rows.Length); i++)
        {
            var at = i;
            tableColumns.Add(new DataTableColumn(rows.Length == 0 ? labels.Total : rows[i].Title, o => ((PivotBuilderRow)o).Labels.ElementAtOrDefault(at))
            {
                Key = "row" + i,
                IsSortable = false,
                Width = new GridLength(1.2, GridUnitType.Star),
                CellTemplate = BoldWhenTotal(o => ((PivotBuilderRow)o).Labels.ElementAtOrDefault(at) ?? string.Empty, numeric: false),
            });
        }
        var cellCount = result.ColumnKeys.Count * values.Count + (ShowTotals && columns.Length > 0 ? values.Count : 0);
        var cell = 0;
        for (var c = 0; c < result.ColumnKeys.Count; c++)
        {
            for (var v = 0; v < values.Count; v++)
            {
                var at = cell++;
                var value = v;
                tableColumns.Add(NumberColumn(Header(string.Join(" · ", result.ColumnKeys[c]), v), at, value, Format));
            }
        }
        if (ShowTotals && columns.Length > 0)
        {
            for (var v = 0; v < values.Count; v++)
            {
                var at = cell++;
                var value = v;
                tableColumns.Add(NumberColumn(Header(labels.Total, v), at, value, Format));
            }
        }

        var tableRows = new List<PivotBuilderRow>();
        for (var r = 0; r < result.RowKeys.Count; r++)
        {
            var cells = new double?[cellCount];
            var i = 0;
            for (var c = 0; c < result.ColumnKeys.Count; c++)
            {
                for (var v = 0; v < values.Count; v++)
                {
                    cells[i++] = result.Cell(r, c, v);
                }
            }
            if (ShowTotals && columns.Length > 0)
            {
                for (var v = 0; v < values.Count; v++)
                {
                    cells[i++] = result.RowTotal(r, v);
                }
            }
            tableRows.Add(new PivotBuilderRow(rows.Length == 0 ? [labels.Total] : result.RowKeys[r], cells, isTotal: false));
        }
        if (ShowTotals && rows.Length > 0)
        {
            var cells = new double?[cellCount];
            var i = 0;
            for (var c = 0; c < result.ColumnKeys.Count; c++)
            {
                for (var v = 0; v < values.Count; v++)
                {
                    cells[i++] = result.ColumnTotal(c, v);
                }
            }
            if (columns.Length > 0)
            {
                for (var v = 0; v < values.Count; v++)
                {
                    cells[i++] = result.GrandTotal(v);
                }
            }
            var totalLabels = new string[rows.Length];
            totalLabels[0] = labels.Total;
            tableRows.Add(new PivotBuilderRow(totalLabels, cells, isTotal: true));
        }

        _table.ItemsSource = null;
        _table.Columns.Clear();
        _table.Columns.AddRange(tableColumns);
        _table.ItemsSource = tableRows;
    }

    private DataTableColumn NumberColumn(string header, int at, int value, Func<int, double?, string> format)
        => new(header, o => ((PivotBuilderRow)o).Cells[at])
        {
            Key = "c" + at.ToString(CultureInfo.InvariantCulture),
            IsSortable = false,
            IsNumeric = true,
            Width = new GridLength(1, GridUnitType.Star),
            Formatter = x => format(value, x as double?),
            CellTemplate = BoldWhenTotal(o => format(value, ((PivotBuilderRow)o).Cells[at]), numeric: true),
        };

    private static IDataTemplate BoldWhenTotal(Func<object, string> text, bool numeric)
        => new FuncDataTemplate<PivotBuilderRow>((row, _) =>
        {
            var block = new TextBlock
            {
                Text = row is null ? string.Empty : text(row),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = numeric ? TextAlignment.Right : TextAlignment.Left,
                HorizontalAlignment = numeric ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontWeight = row?.IsTotal == true ? FontWeight.SemiBold : FontWeight.Normal,
            };
            if (numeric)
            {
                block.FontFeatures = new FontFeatureCollection { FontFeature.Parse("tnum") };
            }
            ArcTokens.Bind(block, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
            return block;
        });

    #endregion

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == ShowTotalsProperty)
        {
            Rebuild();
        }
        else if (change.Property == LabelsProperty)
        {
            if (_body is not null)
            {
                _body.Child = BuildBody();
                Rebuild();
            }
        }
        else if (change.Property == IsFramedProperty)
        {
            PseudoClasses.Set(FramedPseudoClass, IsFramed);
        }
        else if (change.Property == CompactWidthProperty)
        {
            UpdateCompact(Bounds.Width);
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateCompact(e.NewSize.Width);
    }

    private void UpdateCompact(double width)
    {
        var compact = width > 0 && width <= CompactWidth;
        if (compact == _compact)
        {
            return;
        }
        _compact = compact;
        PseudoClasses.Set(CompactPseudoClass, compact);
        ApplyColumns();
    }
}
