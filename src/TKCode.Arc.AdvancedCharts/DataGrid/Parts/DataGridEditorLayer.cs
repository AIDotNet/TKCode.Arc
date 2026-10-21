using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>The text box that edits a cell (its look comes from DataGridTheme).</summary>
[PseudoClasses(InvalidPseudoClass, NumericPseudoClass)]
internal sealed class DataGridCellEditor : TextBox
{
    public const string InvalidPseudoClass = ":invalid";
    public const string NumericPseudoClass = ":numeric";

    private readonly TranslateTransform _shake = new();
    private MotionValue<double>? _shakeProgress;

    public DataGridCellEditor()
    {
        RenderTransform = _shake;
        FocusAdorner = null;
        AcceptsReturn = false;
        AcceptsTab = false;
    }

    protected override Type StyleKeyOverride => typeof(DataGridCellEditor);

    public void SetInvalid(bool invalid) => PseudoClasses.Set(InvalidPseudoClass, invalid);

    public void SetNumeric(bool numeric) => PseudoClasses.Set(NumericPseudoClass, numeric);

    /// <summary>Shakes once (an unreadable value): −3, +3, −2, +1px over 0.32s.</summary>
    public void Shake()
    {
        if (!ArcMotion.IsFull(this))
        {
            return;
        }
        _shakeProgress ??= new MotionValue<double>(this, 1, MotionVectors.Double);
        _shakeProgress.Changed -= OnShake;
        _shakeProgress.Changed += OnShake;
        _shakeProgress.Jump(0);
        _ = _shakeProgress.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(320), ArcEasings.Standard));
    }

    private void OnShake(double t)
    {
        ReadOnlySpan<double> times = [0, 0.2, 0.45, 0.7, 0.9, 1];
        ReadOnlySpan<double> offsets = [0, -3, 3, -2, 1, 0];
        var x = 0.0;
        for (var i = 1; i < times.Length; i++)
        {
            if (t <= times[i])
            {
                var local = (t - times[i - 1]) / (times[i] - times[i - 1]);
                x = offsets[i - 1] + (offsets[i] - offsets[i - 1]) * local;
                break;
            }
        }
        _shake.X = x;
    }
}

/// <summary>
/// Hosts the single cell editor over the active cell (it follows scrolling and stays open when the cell scrolls away), the
/// option list of select columns and the calendar of date columns. Keys while editing: Enter / Shift+Enter commit and move
/// down / up, Tab / Shift+Tab commit and move right / left, arrows commit and move in enter mode, Escape closes the list or
/// cancels, Alt+Down opens the option list or moves focus into the calendar.
/// </summary>
internal sealed class DataGridEditorLayer : Control
{
    private readonly DataGrid _grid;
    private readonly DataGridViewport _viewport;
    private DataGridCellEditor? _editor;
    private ContentPresenter? _host;
    private DataGridEditState? _edit;
    private DataGridColumn? _column;
    private IReadOnlyList<string> _options = [];
    private List<string> _shown = new();
    private int _highlight;
    private bool _listOpen;
    private ArcPopup? _optionsPopup;
    private StackPanel? _optionRows;
    private ScrollViewer? _optionScroll;
    private ArcPopup? _datePopup;
    private Calendar? _calendar;
    private bool _syncing;

    public DataGridEditorLayer(DataGrid grid, DataGridViewport viewport)
    {
        _grid = grid;
        _viewport = viewport;
        ClipToBounds = true;
    }

    public string Text => _editor?.Text ?? string.Empty;

    public bool IsEditorFocused => _editor is { IsFocused: true } || _calendar?.IsKeyboardFocusWithin == true || IsInsideHost(FocusedElement());

    private IInputElement? FocusedElement() => TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();

    /// <summary>Whether an element is in the template host or in something it opened (a drop-down's popup is a logical child of its control).</summary>
    private bool IsInsideHost(IInputElement? element)
    {
        if (_host is not { IsVisible: true } host)
        {
            return false;
        }
        for (var logical = element as Avalonia.LogicalTree.ILogical; logical is not null; logical = logical.LogicalParent)
        {
            if (ReferenceEquals(logical, host))
            {
                return true;
            }
        }
        return false;
    }

    private ContentPresenter EnsureHost()
    {
        if (_host is not null)
        {
            return _host;
        }
        _host = new ContentPresenter { IsVisible = false };
        _host.AddHandler(KeyDownEvent, OnHostKeyDown, RoutingStrategies.Bubble);
        _host.AddHandler(LostFocusEvent, OnHostLostFocus, RoutingStrategies.Bubble);
        LogicalChildren.Add(_host);
        VisualChildren.Add(_host);
        return _host;
    }

    /// <summary>Opens the column's <see cref="DataGridColumn.EditTemplate"/> over the cell and focuses its first focusable control.</summary>
    public void OpenTemplate(DataGridEditState edit, Avalonia.Controls.Templates.IDataTemplate template, string label)
    {
        var host = EnsureHost();
        _edit = edit;
        _column = _grid.ColumnOf(edit.Accessor);
        host.Background = _grid.Palette.CellBrush;
        host.ContentTemplate = template;
        host.Content = edit.Context;
        AutomationProperties.SetName(host, label);
        host.IsVisible = true;
        InvalidateArrange();
        UpdateLayout();
        var first = host.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(c => c.Focusable && c.IsEffectivelyVisible && c.IsEffectivelyEnabled);
        (first ?? host as InputElement)?.Focus(NavigationMethod.Tab);
    }

    private void OnHostKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _edit is not { } edit)
        {
            return;
        }
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                _grid.CommitEdit((shift ? -1 : 1, 0), null);
                break;
            case Key.Tab:
                e.Handled = true;
                _grid.CommitEdit((0, shift ? -1 : 1), null);
                break;
            case Key.Escape:
                e.Handled = true;
                _grid.CancelEditFromEditor();
                break;
        }
    }

    private void OnHostLostFocus(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_edit is not { Done: false } || IsEditorFocused)
            {
                return;
            }
            _grid.CommitEdit(null, null);
        }, DispatcherPriority.Input);
    }

    private DataGridCellEditor EnsureEditor()
    {
        if (_editor is not null)
        {
            return _editor;
        }
        _editor = new DataGridCellEditor { IsVisible = false };
        _editor.AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        _editor.TextChanged += OnEditorTextChanged;
        _editor.LostFocus += OnEditorLostFocus;
        LogicalChildren.Add(_editor);
        VisualChildren.Add(_editor);
        return _editor;
    }

    public void Open(DataGridEditState edit, DataGridColumn column, string draft, bool openList, IReadOnlyList<string> options, string label)
    {
        var editor = EnsureEditor();
        _edit = edit;
        _column = column;
        _options = options;
        _highlight = Math.Max(0, IndexOf(options, draft));
        _syncing = true;
        editor.Text = draft;
        _syncing = false;
        editor.SetInvalid(false);
        editor.SetNumeric(column.IsNumeric);
        editor.PlaceholderText = column.Type == DataGridColumnType.Date ? DataGridText.Get(DataGridLangResourceKind.DatePlaceholder) : null;
        AutomationProperties.SetName(editor, label);
        editor.IsVisible = true;
        InvalidateArrange();
        UpdateLayout();
        editor.Focus(NavigationMethod.Tab);
        editor.CaretIndex = draft.Length;
        editor.ClearSelection();
        _listOpen = false;
        if (openList && column.Type == DataGridColumnType.Select)
        {
            ShowOptions(true);
        }
        else if (openList && column.Type == DataGridColumnType.Date)
        {
            ShowCalendar(focus: false);
        }
    }

    public void Close()
    {
        _edit = null;
        _column = null;
        HideOptions();
        HideCalendar();
        if (_editor is { } editor)
        {
            editor.IsVisible = false;
            editor.SetInvalid(false);
        }
        if (_host is { } host)
        {
            host.IsVisible = false;
            host.Content = null;
        }
    }

    public void MarkInvalid()
    {
        if (_editor is null)
        {
            return;
        }
        _editor.SetInvalid(true);
        _editor.Shake();
        if (_column?.Type == DataGridColumnType.Select)
        {
            ShowOptions(true);
        }
    }

    public void UpdateTexts()
    {
        if (_column is { } column && _editor is not null)
        {
            _editor.PlaceholderText = column.Type == DataGridColumnType.Date ? DataGridText.Get(DataGridLangResourceKind.DatePlaceholder) : null;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _editor?.Measure(availableSize);
        _host?.Measure(availableSize);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_editor is { IsVisible: true } editor && _edit is { } edit)
        {
            var rect = _viewport.CellBodyRect(new DataGridCellAddress(edit.Row, edit.Column));
            editor.Width = Math.Max(1, rect.Width);
            editor.Height = Math.Max(1, rect.Height);
            editor.Measure(rect.Size);
            editor.Arrange(rect);
        }
        if (_host is { IsVisible: true } host && _edit is { } templated)
        {
            var rect = _viewport.CellBodyRect(new DataGridCellAddress(templated.Row, templated.Column));
            host.Width = Math.Max(1, rect.Width);
            host.Height = Math.Max(1, rect.Height);
            host.Measure(rect.Size);
            host.Arrange(rect);
        }
        return finalSize;
    }

    #region Keys

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_edit is not { } edit || _column is not { } column)
        {
            return;
        }
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var alt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var select = column.Type == DataGridColumnType.Select;
        var date = column.Type == DataGridColumnType.Date;
        if (select && alt && e.Key == Key.Down)
        {
            e.Handled = true;
            _highlight = Math.Max(0, IndexOf(_shown.Count > 0 ? _shown : _options, Text));
            ShowOptions(true);
            return;
        }
        if (date && (alt || _datePopup is { IsOpen: true }) && e.Key == Key.Down)
        {
            e.Handled = true;
            ShowCalendar(focus: true);
            return;
        }
        if (select && _listOpen && e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            if (_shown.Count > 0)
            {
                _highlight = Math.Clamp(_highlight + (e.Key == Key.Down ? 1 : -1), 0, _shown.Count - 1);
                RenderOptions();
            }
            return;
        }
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                _grid.CommitEdit((shift ? -1 : 1, 0), Picked());
                return;
            case Key.Tab:
                e.Handled = true;
                _grid.CommitEdit((0, shift ? -1 : 1), Picked());
                return;
            case Key.Escape:
                e.Handled = true;
                if (_listOpen || _datePopup is { IsOpen: true })
                {
                    HideOptions();
                    HideCalendar();
                }
                else
                {
                    _grid.CancelEditFromEditor();
                }
                return;
        }
        if (edit.EnterMode && e.Key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            e.Handled = true;
            var move = e.Key switch
            {
                Key.Up => (-1, 0),
                Key.Down => (1, 0),
                Key.Left => (0, -1),
                _ => (0, 1)
            };
            _grid.CommitEdit(move, null);
        }
    }

    private string? Picked() => _listOpen && _highlight >= 0 && _highlight < _shown.Count ? _shown[_highlight] : null;

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _edit is null || _column is null)
        {
            return;
        }
        _edit.Invalid = false;
        _editor?.SetInvalid(false);
        if (_column.Type == DataGridColumnType.Select)
        {
            _highlight = Math.Max(0, IndexOf(Filtered(), Text));
            ShowOptions(true);
        }
        else if (_column.Type == DataGridColumnType.Date && _calendar is not null && DataGridValueParser.TryParseDate(Text, _grid.EffectiveCulture, out var day))
        {
            _calendar.SelectedDate = day;
            _calendar.DisplayMonth = day;
        }
    }

    private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_edit is not { Done: false } || IsEditorFocused)
            {
                return;
            }
            _grid.CommitEdit(null, null);
        }, DispatcherPriority.Input);
    }

    #endregion

    #region Option list

    private static int IndexOf(IReadOnlyList<string> options, string text)
    {
        var trimmed = text.Trim();
        for (var i = 0; i < options.Count; i++)
        {
            if (string.Equals(options[i], trimmed, StringComparison.CurrentCultureIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Options matching the draft: those starting with it first, then those containing it (all of them for an exact match).</summary>
    private List<string> Filtered()
    {
        var draft = Text.Trim();
        if (draft.Length == 0 || IndexOf(_options, draft) >= 0)
        {
            return _options.ToList();
        }
        var starts = _options.Where(o => o.StartsWith(draft, StringComparison.CurrentCultureIgnoreCase));
        var contains = _options.Where(o => !o.StartsWith(draft, StringComparison.CurrentCultureIgnoreCase) &&
                                           o.Contains(draft, StringComparison.CurrentCultureIgnoreCase));
        return starts.Concat(contains).ToList();
    }

    private void ShowOptions(bool open)
    {
        if (!open || _editor is null || _column is null)
        {
            HideOptions();
            return;
        }
        _shown = Filtered();
        _highlight = Math.Clamp(_highlight, 0, Math.Max(0, _shown.Count - 1));
        if (_optionsPopup is null)
        {
            _optionRows = new StackPanel();
            _optionScroll = new ScrollViewer
            {
                Content = _optionRows,
                MaxHeight = 248,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var surface = new Border
            {
                Padding = new Thickness(4),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                MinWidth = 160,
                Child = _optionScroll
            };
            ArcTokens.Bind(surface, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
            ArcTokens.Bind(surface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(surface, TextElement.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
            ArcTokens.Bind(surface, TextElement.FontSizeProperty, ArcDesignTokenKind.TypeSM);
            ArcTokens.Bind(surface, TextElement.ForegroundProperty, ArcDesignTokenKind.Foreground);
            _optionsPopup = new ArcPopup
            {
                Child = surface,
                EnterOffset = 4,
                EnterScale = 0.98,
                MarginToAnchor = 4,
                IsLightDismissEnabled = false,
                TakesFocusFromNativeControl = false
            };
        }
        var popup = _optionsPopup;
        popup.PlacementTarget = _editor;
        popup.Placement = OpenUp() ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        if (popup.Child is Border border)
        {
            border.MinWidth = Math.Max(160, _editor.Bounds.Width);
            AutomationProperties.SetName(border, DataGridText.Format(DataGridLangResourceKind.OptionsLabel, _column.Header));
        }
        RenderOptions();
        if (!popup.IsOpen)
        {
            if (popup.Parent is null)
            {
                ((ISetLogicalParent)popup).SetParent(this);
            }
            popup.IsOpen = true;
        }
        _listOpen = true;
    }

    private bool OpenUp()
    {
        if (_edit is null)
        {
            return false;
        }
        var cell = _viewport.CellBodyRect(new DataGridCellAddress(_edit.Row, _edit.Column));
        return cell.Bottom + 220 > _viewport.BodyHeight && cell.Top > 220;
    }

    private void RenderOptions()
    {
        if (_optionRows is null)
        {
            return;
        }
        _optionRows.Children.Clear();
        if (_shown.Count == 0)
        {
            var empty = new TextBlock
            {
                Text = DataGridText.Get(DataGridLangResourceKind.NoMatch),
                Margin = new Thickness(10, 8)
            };
            ArcTokens.Bind(empty, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            _optionRows.Children.Add(empty);
            return;
        }
        for (var i = 0; i < _shown.Count; i++)
        {
            var option = _shown[i];
            var row = new Border
            {
                Padding = new Thickness(10, 7),
                CornerRadius = new CornerRadius(8),
                Child = new TextBlock { Text = option, TextTrimming = TextTrimming.CharacterEllipsis },
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            if (i == _highlight)
            {
                ArcTokens.Bind(row, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            }
            var index = i;
            row.PointerEntered += (_, _) =>
            {
                if (_highlight != index)
                {
                    _highlight = index;
                    RenderOptions();
                }
            };
            row.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                _grid.CommitEdit((0, 0), option);
            };
            AutomationProperties.SetName(row, option);
            _optionRows.Children.Add(row);
        }
        if (_highlight >= 0 && _highlight < _optionRows.Children.Count)
        {
            _optionRows.Children[_highlight].BringIntoView();
        }
    }

    private void HideOptions()
    {
        _listOpen = false;
        if (_optionsPopup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    #endregion

    #region Calendar

    private void ShowCalendar(bool focus)
    {
        if (_editor is null || _column is null)
        {
            return;
        }
        if (_datePopup is null)
        {
            _calendar = new Calendar { ShowToday = true };
            _calendar.DateSelected += OnDateSelected;
            _calendar.AddHandler(KeyDownEvent, OnCalendarKeyDown, RoutingStrategies.Tunnel);
            var surface = new Border
            {
                Padding = new Thickness(12, 12, 14, 14),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Child = _calendar
            };
            ArcTokens.Bind(surface, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
            ArcTokens.Bind(surface, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            _datePopup = new ArcPopup
            {
                Child = surface,
                EnterOffset = 5,
                EnterScale = 0.97,
                MarginToAnchor = 6,
                IsLightDismissEnabled = true,
                TakesFocusFromNativeControl = false
            };
            _datePopup.Closed += (_, _) =>
            {
                if (_calendar?.IsKeyboardFocusWithin == true)
                {
                    _editor?.Focus(NavigationMethod.Tab);
                }
            };
        }
        var popup = _datePopup;
        popup.PlacementTarget = _editor;
        popup.Placement = OpenUp() ? PlacementMode.TopEdgeAlignedLeft : PlacementMode.BottomEdgeAlignedLeft;
        AutomationProperties.SetName(popup.Child!, DataGridText.Format(DataGridLangResourceKind.CalendarLabel, _column.Header));
        if (DataGridValueParser.TryParseDate(Text, _grid.EffectiveCulture, out var day))
        {
            _calendar!.SelectedDate = day;
            _calendar.DisplayMonth = day;
        }
        _calendar!.Culture = _grid.EffectiveCulture;
        if (!popup.IsOpen)
        {
            if (popup.Parent is null)
            {
                ((ISetLogicalParent)popup).SetParent(this);
            }
            popup.IsOpen = true;
        }
        if (focus)
        {
            Dispatcher.UIThread.Post(() => _calendar?.Focus(NavigationMethod.Tab), DispatcherPriority.Input);
        }
    }

    private void OnDateSelected(object? sender, CalendarDateSelectedEventArgs e)
    {
        if (_edit is null)
        {
            return;
        }
        _grid.CommitEdit((0, 0), e.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    private void OnCalendarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            HideCalendar();
            _editor?.Focus(NavigationMethod.Tab);
        }
    }

    private void HideCalendar()
    {
        if (_datePopup is { IsOpen: true } popup)
        {
            popup.IsOpen = false;
        }
    }

    #endregion
}

/// <summary>The body's empty state: "No rows yet", or "No rows match" with Clear filters.</summary>
internal sealed class DataGridEmptyState : Border
{
    private readonly DataGrid _grid;
    private readonly TextBlock _title;
    private readonly Button _clear;

    public DataGridEmptyState(DataGrid grid)
    {
        _grid = grid;
        _title = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
        ArcTokens.Bind(_title, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        _clear = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _clear.Click += (_, _) => _grid.ClearAllFilters();
        Child = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _title, _clear }
        };
        IsVisible = false;
        Padding = new Thickness(24);
    }

    public void Update()
    {
        var empty = !_grid.IsShowingSkeleton && _grid.LineCount == 0;
        IsVisible = empty;
        if (!empty)
        {
            return;
        }
        var filtering = _grid.IsFiltering;
        _title.Text = filtering
            ? DataGridText.Get(DataGridLangResourceKind.EmptyNoMatch)
            : _grid.EmptyMessage ?? DataGridText.Get(DataGridLangResourceKind.EmptyNoRows);
        _clear.IsVisible = filtering;
        _clear.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                new ArcGlyph { Data = ArcIcons.FilterX, Size = 16, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = DataGridText.Get(DataGridLangResourceKind.ClearFilters), VerticalAlignment = VerticalAlignment.Center }
            }
        };
    }
}
