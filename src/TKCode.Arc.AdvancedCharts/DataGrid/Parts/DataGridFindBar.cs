using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Localization;

namespace TKCode.Arc;

/// <summary>
/// The find bar: a field with the match count, previous and next, match case and whole cell toggles, and (with Ctrl+H or the
/// chevron) a replace row. It floats at the top right of the body while <see cref="DataGrid.IsFindOpen"/> is true.
/// </summary>
internal sealed class DataGridFindBar : Border
{
    private readonly DataGrid _grid;
    private readonly DataGridSearchBox _find;
    private readonly DataGridSearchBox _replace;
    private readonly TextBlock _count;
    private readonly DataGridToolButton _toggle;
    private readonly DataGridToolButton _previous;
    private readonly DataGridToolButton _next;
    private readonly DataGridToolButton _matchCase;
    private readonly DataGridToolButton _wholeCell;
    private readonly DataGridToolButton _close;
    private readonly DataGridToolButton _replaceOne;
    private readonly DataGridToolButton _replaceAll;
    private readonly StackPanel _replaceRow;
    private bool _syncing;

    public DataGridFindBar(DataGrid grid)
    {
        _grid = grid;
        IsVisible = false;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep12);
        BorderThickness = new Thickness(1);
        Padding = new Thickness(6);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(this, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);

        _find = new DataGridSearchBox { Width = 190, VerticalAlignment = VerticalAlignment.Center };
        _find.TextChanged += OnFindTextChanged;
        _find.KeyDown += OnFindKeyDown;
        _count = new TextBlock { Width = 104, VerticalAlignment = VerticalAlignment.Center, TextAlignment = Avalonia.Media.TextAlignment.Center };
        _count.FontFeatures = DataGridPalette.TabularNumbers;
        ArcTokens.Bind(_count, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        _toggle = new DataGridToolButton(ArcIcons.ChevronRight, iconOnly: true);
        _toggle.Click += (_, _) => SetReplaceVisible(!_replaceRow!.IsVisible);
        _previous = new DataGridToolButton(ArcIcons.ChevronUp, iconOnly: true);
        _previous.Click += (_, _) => _grid.FindPrevious();
        _next = new DataGridToolButton(ArcIcons.ChevronDown, iconOnly: true);
        _next.Click += (_, _) => _grid.FindNext();
        _matchCase = new DataGridToolButton(ArcIcons.Type, iconOnly: true);
        _matchCase.Click += (_, _) => _grid.SetCurrentValue(DataGrid.FindMatchCaseProperty, !_grid.FindMatchCase);
        _wholeCell = new DataGridToolButton(ArcIcons.Square, iconOnly: true);
        _wholeCell.Click += (_, _) => _grid.SetCurrentValue(DataGrid.FindMatchWholeCellProperty, !_grid.FindMatchWholeCell);
        _close = new DataGridToolButton(ArcIcons.X, iconOnly: true);
        _close.Click += (_, _) => _grid.CloseFind();
        var top = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { _toggle, _find, _count, _previous, _next, _matchCase, _wholeCell, _close }
        };

        _replace = new DataGridSearchBox { Width = 190, VerticalAlignment = VerticalAlignment.Center };
        _replace.KeyDown += OnReplaceKeyDown;
        _replaceOne = new DataGridToolButton(ArcIcons.RefreshCw);
        _replaceOne.Click += (_, _) => _grid.ReplaceCurrent(_replace.Text ?? string.Empty);
        _replaceAll = new DataGridToolButton(ArcIcons.CheckCheck);
        _replaceAll.Click += (_, _) => _grid.ReplaceAll(_replace.Text ?? string.Empty);
        _replaceRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(34, 4, 0, 0),
            IsVisible = false,
            Children = { _replace, _replaceOne, _replaceAll }
        };
        Child = new StackPanel { Children = { top, _replaceRow } };
        UpdateTexts();
    }

    /// <summary>Whether the replace row shows.</summary>
    public bool IsReplaceVisible => _replaceRow.IsVisible;

    /// <summary>Applies the built-in texts of the current language.</summary>
    public void UpdateTexts()
    {
        _find.PlaceholderText = DataGridText.Get(DataGridLangResourceKind.FindPlaceholder);
        _replace.PlaceholderText = DataGridText.Get(DataGridLangResourceKind.ReplacePlaceholder);
        Describe(_find, DataGridLangResourceKind.FindPlaceholder);
        Describe(_replace, DataGridLangResourceKind.ReplacePlaceholder);
        _previous.Label = DataGridText.Get(DataGridLangResourceKind.FindPrevious);
        _next.Label = DataGridText.Get(DataGridLangResourceKind.FindNext);
        _matchCase.Label = DataGridText.Get(DataGridLangResourceKind.FindMatchCase);
        _wholeCell.Label = DataGridText.Get(DataGridLangResourceKind.FindWholeCell);
        _close.Label = DataGridText.Get(DataGridLangResourceKind.CloseFind);
        _toggle.Label = DataGridText.Get(DataGridLangResourceKind.ToggleReplace);
        _replaceOne.Label = DataGridText.Get(DataGridLangResourceKind.ReplaceOne);
        _replaceAll.Label = DataGridText.Get(DataGridLangResourceKind.ReplaceAll);
        foreach (var button in new[] { _previous, _next, _matchCase, _wholeCell, _close, _toggle })
        {
            AutomationProperties.SetName(button, button.Label);
        }
        AutomationProperties.SetName(_replaceOne, _replaceOne.Label);
        AutomationProperties.SetName(_replaceAll, _replaceAll.Label);
    }

    private static void Describe(Control control, DataGridLangResourceKind kind) => AutomationProperties.SetName(control, DataGridText.Get(kind));

    /// <summary>Shows the bar, optionally with the replace row, and focuses the find field.</summary>
    public void Open(bool replace)
    {
        IsVisible = true;
        if (replace)
        {
            SetReplaceVisible(true);
        }
        Sync();
        Dispatcher.UIThread.Post(() =>
        {
            _find.Focus(NavigationMethod.Tab);
            _find.SelectAll();
        }, DispatcherPriority.Input);
    }

    private void SetReplaceVisible(bool visible)
    {
        _replaceRow.IsVisible = visible;
        _toggle.Icon = visible ? ArcIcons.ChevronDown : ArcIcons.ChevronRight;
        InvalidateMeasure();
        _grid.Viewport.InvalidateArrange();
    }

    /// <summary>Brings the bar in line with the grid: visibility, the field text, the toggles and the match count.</summary>
    public void Sync()
    {
        if (IsVisible != _grid.IsFindOpen)
        {
            IsVisible = _grid.IsFindOpen;
            _grid.Viewport.InvalidateArrange();
        }
        if (!IsVisible)
        {
            return;
        }
        _syncing = true;
        try
        {
            if ((_find.Text ?? string.Empty) != _grid.FindText)
            {
                _find.Text = _grid.FindText;
            }
        }
        finally
        {
            _syncing = false;
        }
        _matchCase.Classes.Set("pressed-state", _grid.FindMatchCase);
        _wholeCell.Classes.Set("pressed-state", _grid.FindMatchWholeCell);
        var (position, count) = _grid.FindPosition();
        _count.Text = _grid.FindText.Length == 0
            ? string.Empty
            : count == 0
                ? DataGridText.Get(DataGridLangResourceKind.FindNoMatches)
                : position > 0
                    ? DataGridText.Format(DataGridLangResourceKind.FindCount, position, count)
                    : count.ToString("N0", _grid.EffectiveCulture);
        var any = count > 0;
        _previous.IsEnabled = any;
        _next.IsEnabled = any;
        _replaceOne.IsEnabled = any;
        _replaceAll.IsEnabled = any;
        AutomationProperties.SetLiveSetting(_count, AutomationLiveSetting.Polite);
    }

    private void OnFindTextChanged(object? sender, TextChangedEventArgs e)
    {
        // TextChanged can arrive after a programmatic change; only text the grid does not know yet came from typing.
        if (_syncing || (_find.Text ?? string.Empty) == _grid.FindText)
        {
            return;
        }
        _grid.SetCurrentValue(DataGrid.FindTextProperty, _find.Text ?? string.Empty);
        _grid.FindStep(forward: true, inclusive: true);
    }

    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
                {
                    _grid.FindPrevious();
                }
                else
                {
                    _grid.FindNext();
                }
                break;
            case Key.Escape:
                e.Handled = true;
                _grid.CloseFind();
                break;
        }
    }

    private void OnReplaceKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                _grid.ReplaceCurrent(_replace.Text ?? string.Empty);
                break;
            case Key.Escape:
                e.Handled = true;
                _grid.CloseFind();
                break;
        }
    }
}
