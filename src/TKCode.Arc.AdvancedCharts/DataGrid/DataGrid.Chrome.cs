using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

public partial class DataGrid
{
    private MenuSurface? _menu;
    private ArcPopup? _selectFilterPopup;

    /// <summary>The menu surface last opened by the grid (column, columns, density or export menu).</summary>
    internal MenuSurface? CurrentMenu => _menu;

    internal void UpdateToolbarState()
    {
        _toolbar?.UpdateState();
        Viewport.FilterRow.Refresh();
        Viewport.UpdateHeaders();
        Viewport.UpdateTotals();
    }

    private void UpdateStatus() => _statusBar?.UpdateStats();

    /// <summary>"120 rows", or "12 of 120 rows" while filtering.</summary>
    internal string RowCountText()
    {
        var culture = EffectiveCulture;
        var shown = VisibleRowCount;
        if (IsFiltering && !HasDataSource)
        {
            return DataGridText.Format(DataGridLangResourceKind.RowCountFiltered, shown.ToString("N0", culture), _store.Count.ToString("N0", culture));
        }
        return DataGridText.Count(DataGridLangResourceKind.RowCountOne, DataGridLangResourceKind.RowCountOther, shown, culture);
    }

    internal void InvokeBulkAction(DataGridBulkAction action)
    {
        var rows = CheckedItems();
        RaiseEvent(new DataGridBulkActionEventArgs(BulkActionInvokedEvent, action, rows));
        if (action.Command is { } command && command.CanExecute(rows))
        {
            command.Execute(rows);
        }
    }

    #region Menus

    /// <summary>A shared menu surface, refilled for each menu.</summary>
    private MenuSurface Menu(IEnumerable<Control> entries, Action<MenuEntry> clicked)
    {
        if (_menu is null)
        {
            _menu = new MenuSurface { Variant = MenuSurfaceVariant.Dropdown, Alignment = MenuAlignment.End };
            _menu.ItemClicked += (_, e) => (_menu.Tag as Action<MenuEntry>)?.Invoke(e.Entry);
        }
        _menu.IsOpen = false;
        _menu.Items.Clear();
        foreach (var entry in entries)
        {
            _menu.Items.Add(entry);
        }
        _menu.Tag = clicked;
        return _menu;
    }

    private static MenuEntry Entry(string header, Geometry? icon, string tag, bool enabled = true) =>
        new() { Header = header, Icon = icon, Tag = tag, IsEnabled = enabled };

    private static MenuOpenFocus FocusFor(Control anchor) =>
        anchor.IsKeyboardFocusWithin && anchor.Classes.Contains(":focus-visible") ? MenuOpenFocus.First : MenuOpenFocus.Surface;

    /// <summary>The column menu: sort, filter, group, pin and hide.</summary>
    internal void OpenColumnMenu(Control anchor, int accessor)
    {
        var column = ColumnOf(accessor);
        var entries = new List<Control>();
        var sort = SortDescriptions.FirstOrDefault(s => s.Key == column.Key);
        var sorted = sort.Key is not null;
        if (column.IsSortable)
        {
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.SortAscending), ArcIcons.ArrowUpNarrowWide, "asc"));
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.SortDescending), ArcIcons.ArrowDownWideNarrow, "desc"));
            if (sorted)
            {
                entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.ClearSort), ArcIcons.X, "unsort"));
            }
            entries.Add(new MenuEntrySeparator());
        }
        if (column.IsFilterable)
        {
            entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.Filter), ArcIcons.Filter, "filter"));
        }
        if (!HasDataSource && column.EffectiveIsGroupable)
        {
            var grouped = GroupBy.Contains(column.Key);
            entries.Add(Entry(grouped
                ? DataGridText.Get(DataGridLangResourceKind.StopGrouping)
                : DataGridText.Format(DataGridLangResourceKind.GroupByColumn, column.Header), grouped ? DataGridIcons.Ungroup : DataGridIcons.Group, "group"));
        }
        entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.FitColumnWidth), ArcIcons.Maximize2, "fit"));
        var pinned = IsPinnedColumn(column.Key);
        entries.Add(Entry(DataGridText.Get(pinned ? DataGridLangResourceKind.UnpinColumn : DataGridLangResourceKind.PinColumn),
            pinned ? DataGridIcons.PinOff : DataGridIcons.Pin, "pin"));
        entries.Add(Entry(DataGridText.Get(DataGridLangResourceKind.HideColumn), ArcIcons.EyeOff, "hide", VisibleAccessors.Length > 1));
        var menu = Menu(entries, entry =>
        {
            switch (entry.Tag as string)
            {
                case "asc":
                    ApplySort([DataGridSort.Ascending(column.Key)]);
                    break;
                case "desc":
                    ApplySort([DataGridSort.Descending(column.Key)]);
                    break;
                case "unsort":
                    ApplySort(SortDescriptions.Where(s => s.Key != column.Key).ToArray());
                    break;
                case "filter":
                    SetCurrentValue(IsFilterRowOpenProperty, true);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => Viewport.FilterRow.FocusColumn(accessor),
                        Avalonia.Threading.DispatcherPriority.Loaded);
                    break;
                case "group":
                    ToggleGroupBy(column.Key);
                    break;
                case "fit":
                    AutoFitColumn(column.Key);
                    break;
                case "pin":
                    TogglePinned(column.Key);
                    break;
                case "hide":
                    ToggleHidden(column.Key);
                    break;
            }
        });
        menu.Alignment = MenuAlignment.End;
        menu.Open(anchor, FocusFor(anchor));
    }

    /// <summary>The Columns menu: a checklist of every column (pinned ones marked) and Show all columns.</summary>
    internal void OpenColumnsMenu(Control anchor)
    {
        var entries = new List<Control>();
        var visibleCount = VisibleAccessors.Length;
        foreach (var column in Columns)
        {
            var shown = !IsHidden(column.Key);
            entries.Add(new MenuEntry
            {
                Header = column.Header,
                Tag = column.Key,
                ToggleType = MenuEntryToggleType.CheckBox,
                IsChecked = shown,
                StaysOpenOnClick = true,
                IsEnabled = !(shown && visibleCount <= 1),
                InputGestureText = IsPinnedColumn(column.Key) ? DataGridText.Get(DataGridLangResourceKind.Pinned) : null
            });
        }
        entries.Add(new MenuEntrySeparator());
        entries.Add(new MenuEntry
        {
            Header = DataGridText.Get(DataGridLangResourceKind.ShowAllColumns),
            Tag = "\u0000all",
            IsEnabled = Columns.Any(c => IsHidden(c.Key))
        });
        var menu = Menu(entries, entry =>
        {
            if (entry.Tag as string == "\u0000all")
            {
                ShowAllColumns();
            }
            else if (entry.Tag is string key)
            {
                ToggleHidden(key);
            }
            RefreshColumnsMenu();
        });
        menu.Open(anchor, FocusFor(anchor));
    }

    private void RefreshColumnsMenu()
    {
        if (_menu is null)
        {
            return;
        }
        var visibleCount = VisibleAccessors.Length;
        foreach (var entry in _menu.Items.OfType<MenuEntry>())
        {
            if (entry.Tag as string == "\u0000all")
            {
                entry.IsEnabled = Columns.Any(c => IsHidden(c.Key));
            }
            else if (entry.Tag is string key)
            {
                var shown = !IsHidden(key);
                entry.IsChecked = shown;
                entry.IsEnabled = !(shown && visibleCount <= 1);
            }
        }
    }

    /// <summary>The density menu: compact, standard and comfortable rows.</summary>
    internal void OpenDensityMenu(Control anchor)
    {
        var entries = new List<Control>
        {
            DensityEntry(DataGridDensity.Compact, DataGridLangResourceKind.DensityCompact, DataGridIcons.Rows4),
            DensityEntry(DataGridDensity.Standard, DataGridLangResourceKind.DensityStandard, DataGridIcons.Rows3),
            DensityEntry(DataGridDensity.Comfortable, DataGridLangResourceKind.DensityComfortable, ArcIcons.Rows2)
        };
        var menu = Menu(entries, entry =>
        {
            if (entry.Tag is DataGridDensity density)
            {
                SetCurrentValue(DensityProperty, density);
            }
        });
        menu.Open(anchor, FocusFor(anchor));
    }

    private MenuEntry DensityEntry(DataGridDensity density, DataGridLangResourceKind label, Geometry icon) => new()
    {
        Header = DataGridText.Get(label),
        Icon = icon,
        Tag = density,
        ToggleType = MenuEntryToggleType.Radio,
        GroupName = "density",
        IsChecked = Density == density
    };

    /// <summary>Export: one format exports at once; several open a menu.</summary>
    internal void OpenExport(Control anchor, bool checkedOnly)
    {
        var formats = new List<DataGridExportFormat>();
        if (ExportFormats.HasFlag(DataGridExportFormats.Csv))
        {
            formats.Add(DataGridExportFormat.Csv);
        }
        if (ExportFormats.HasFlag(DataGridExportFormats.Xlsx))
        {
            formats.Add(DataGridExportFormat.Xlsx);
        }
        if (formats.Count == 0)
        {
            return;
        }
        if (formats.Count == 1)
        {
            _ = ExportAsync(formats[0], checkedOnly);
            return;
        }
        var entries = formats.Select(f => (Control)new MenuEntry
        {
            Header = DataGridText.Get(f == DataGridExportFormat.Csv ? DataGridLangResourceKind.ExportCsv : DataGridLangResourceKind.ExportXlsx),
            Icon = f == DataGridExportFormat.Csv ? ArcIcons.FileText : DataGridIcons.FileSpreadsheet,
            InputGestureText = f == DataGridExportFormat.Csv ? ".csv" : ".xlsx",
            Tag = f
        }).ToList();
        var menu = Menu(entries, entry =>
        {
            if (entry.Tag is DataGridExportFormat format)
            {
                _ = ExportAsync(format, checkedOnly);
            }
        });
        menu.Open(anchor, FocusFor(anchor));
    }

    #endregion

    #region Select filter checklist

    /// <summary>The checklist of a select column's filter: search (more than 7 values), the values, Select all and Clear.</summary>
    internal void OpenSelectFilter(Control anchor, int accessor)
    {
        var column = ColumnOf(accessor);
        var options = FilterOptionsOf(accessor);
        // The filter row's input (DataGridTheme gives it its template; a plain TextBox has none of its own).
        var search = new DataGridFilterBox
        {
            PlaceholderText = DataGridText.Get(DataGridLangResourceKind.SearchValues),
            IsVisible = options.Count > 7,
            Height = 30,
            Margin = new Thickness(4, 4, 4, 6)
        };
        AutomationProperties.SetName(search, DataGridText.Get(DataGridLangResourceKind.SearchValues));
        var list = new StackPanel();
        AutomationProperties.SetName(list, DataGridText.Format(DataGridLangResourceKind.ColumnValues, column.Header));
        var scroll = new ScrollViewer { Content = list, MaxHeight = 260, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var selectAll = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = DataGridText.Get(DataGridLangResourceKind.SelectAll) };
        var clear = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = DataGridText.Get(DataGridLangResourceKind.ClearValues) };
        var footer = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 6, 0, 0) };
        DockPanel.SetDock(selectAll, Dock.Left);
        DockPanel.SetDock(clear, Dock.Right);
        footer.Children.Add(selectAll);
        footer.Children.Add(clear);
        var surface = new Border
        {
            Width = 240,
            Padding = new Thickness(6),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { search, scroll, footer } }
        };
        surface.BindCornerRadius(ArcDesignTokenKind.RadiusStep16);
        ArcTokens.Bind(surface, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(surface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(surface, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(surface, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
        AutomationProperties.SetName(surface, DataGridText.Format(DataGridLangResourceKind.FilterLabel, column.Header));

        void Render()
        {
            list.Children.Clear();
            var keep = FilterOf(column.Key)?.Values;
            var set = keep is null ? null : new HashSet<string>(keep, StringComparer.Ordinal);
            var query = (search.Text ?? string.Empty).Trim();
            var shown = 0;
            foreach (var option in options)
            {
                var label = option.Length == 0 ? DataGridText.Get(DataGridLangResourceKind.EmptyValue) : FilterLabelOf(column, option);
                if (query.Length > 0 && !label.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                {
                    continue;
                }
                shown++;
                var box = new Checkbox { Content = label, IsChecked = set is null || set.Contains(option), Margin = new Thickness(6, 4) };
                var value = option;
                box.IsCheckedChanged += (_, _) =>
                {
                    var current = FilterOf(column.Key)?.Values;
                    var next = new HashSet<string>(current ?? options, StringComparer.Ordinal);
                    if (box.IsChecked == true)
                    {
                        next.Add(value);
                    }
                    else
                    {
                        next.Remove(value);
                    }
                    SetFilter(column.Key, next.Count == options.Count ? null : DataGridFilter.OneOf(options.Where(next.Contains).ToArray()));
                    selectAll.IsEnabled = FilterOf(column.Key) is not null;
                    clear.IsEnabled = FilterOf(column.Key)?.Values is not { Count: 0 };
                };
                list.Children.Add(box);
            }
            if (shown == 0)
            {
                var none = new TextBlock { Text = DataGridText.Get(DataGridLangResourceKind.NoMatchingValues), Margin = new Thickness(8) };
                ArcTokens.Bind(none, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
                list.Children.Add(none);
            }
            selectAll.IsEnabled = keep is not null;
            clear.IsEnabled = keep is not { Count: 0 };
        }

        search.TextChanged += (_, _) => Render();
        selectAll.Click += (_, _) =>
        {
            SetFilter(column.Key, null);
            Render();
        };
        clear.Click += (_, _) =>
        {
            SetFilter(column.Key, DataGridFilter.OneOf([]));
            Render();
        };
        Render();
        if (_selectFilterPopup is { IsOpen: true } open)
        {
            open.IsOpen = false;
        }
        var popup = new ArcPopup
        {
            Child = surface,
            PlacementTarget = anchor,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            EnterOffset = 5,
            EnterScale = 0.97,
            MarginToAnchor = 4,
            IsLightDismissEnabled = true
        };
        popup.Closed += (_, _) =>
        {
            ((ISetLogicalParent)popup).SetParent(null);
            if (ReferenceEquals(_selectFilterPopup, popup))
            {
                _selectFilterPopup = null;
            }
        };
        surface.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                // Before the popup closes: closing it with focus inside puts focus back by itself, without the keyboard
                // ring, and focusing the already focused anchor then changes nothing.
                anchor.Focus(NavigationMethod.Tab);
                popup.IsOpen = false;
            }
        }, RoutingStrategies.Bubble);
        ((ISetLogicalParent)popup).SetParent(this);
        _selectFilterPopup = popup;
        popup.IsOpen = true;
        if (search.IsVisible)
        {
            search.Focus(NavigationMethod.Tab);
        }
        else
        {
            // Opened with a click: the first value takes focus without the keyboard ring.
            list.Children.OfType<Checkbox>().FirstOrDefault()?.Focus(DataGridFocusModality.For(anchor));
        }
    }

    #endregion
}
