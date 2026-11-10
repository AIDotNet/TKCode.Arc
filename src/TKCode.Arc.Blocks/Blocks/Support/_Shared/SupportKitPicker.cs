using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>
/// A short list in a <see cref="Popover"/> for the Support blocks (assign to a teammate, add a label, insert a canned
/// reply): a heading, one row per choice with an optional second line and a check on the chosen ones. ↑ / ↓ / Home / End
/// move between rows, Enter or Space picks, Escape closes and returns focus to the anchor.
/// </summary>
internal sealed class SupportKitPicker
{
    private readonly Popover _popover = new() { Placement = PopoverPlacement.Bottom, Alignment = PopoverAlignment.Start };
    private readonly List<SupportKitRow> _rows = new();
    private Action<string>? _picked;

    /// <summary>The rows shown now (tests).</summary>
    public IReadOnlyList<SupportKitRow> Rows => _rows;

    public bool IsOpen => _popover.IsOpen;

    public Popover Popover => _popover;

    /// <summary>Opens the list under <paramref name="anchor"/>; <paramref name="picked"/> runs with the chosen value.</summary>
    public void Open(Control anchor, string heading, IEnumerable<(string Value, string? Detail)> choices, ISet<string>? chosen,
        Action<string> picked)
    {
        _picked = picked;
        _rows.Clear();
        var list = new StackPanel { Spacing = 2, MinWidth = 220, MaxWidth = 320 };
        AutomationProperties.SetName(list, heading);
        var title = SupportKit.Text(heading, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, FontWeight.Medium);
        title.Margin = new Thickness(10, 2, 10, 6);
        list.Children.Add(title);
        foreach (var (value, detail) in choices)
        {
            var isChosen = chosen?.Contains(value) == true;
            var text = new StackPanel { Spacing = 2 };
            text.Children.Add(SupportKit.Text(value, ArcDesignTokenKind.Foreground, ArcDesignTokenKind.TypeSM, trim: true));
            if (!string.IsNullOrEmpty(detail))
            {
                var second = SupportKit.Text(detail, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS, wrap: true);
                second.MaxHeight = 34;
                text.Children.Add(second);
            }
            var grid = SupportKit.Grid("*,Auto", columnSpacing: 8);
            grid.Children.Add(text.At(0));
            var check = SupportKit.Glyph(ArcIcons.Check, 14, ArcDesignTokenKind.Accent);
            check.IsVisible = isChosen;
            grid.Children.Add(check.At(1));
            var row = new SupportKitRow { Content = grid, Item = value, Padding = new Thickness(10, 7), IsChecked = isChosen };
            AutomationProperties.SetName(row, isChosen ? $"{value}, selected" : value);
            row.Click += OnRowClick;
            _rows.Add(row);
            list.Children.Add(row);
        }
        list.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        _popover.Content = list;
        _popover.ShowAt(anchor);
        if (_rows.Count > 0)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => _rows[0].Focus(NavigationMethod.Tab), Avalonia.Threading.DispatcherPriority.Input);
        }
    }

    public void Close() => _popover.Close();

    /// <summary>Picks <paramref name="value"/> as if its row was pressed (tests and keyboard shortcuts).</summary>
    public void Pick(string value)
    {
        var picked = _picked;
        _popover.Close();
        picked?.Invoke(value);
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is SupportKitRow { Item: string value })
        {
            Pick(value);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var at = _rows.FindIndex(r => r.IsFocused);
        var next = e.Key switch
        {
            Key.Down => Math.Min(_rows.Count - 1, at + 1),
            Key.Up => Math.Max(0, at - 1),
            Key.Home => 0,
            Key.End => _rows.Count - 1,
            _ => -1,
        };
        if (next >= 0 && _rows.Count > 0)
        {
            _rows[next].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }
}
