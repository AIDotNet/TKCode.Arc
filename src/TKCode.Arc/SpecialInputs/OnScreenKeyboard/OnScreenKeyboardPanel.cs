using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>
/// Lays the keys of an <see cref="OnScreenKeyboard"/> out in rows. One key unit is the width divided by the unit count of
/// the widest row; every row is centred, so a shorter row is indented on both sides (the QWERTY home row sits half a key
/// in). A key's slot is its units wide and holds the key with half a gap on each side; the outermost keys touch the
/// edges. Rows share the height the panel is given, or are <see cref="KeyHeight"/> tall when it is not limited.
/// </summary>
internal sealed class OnScreenKeyboardPanel : Panel
{
    public static readonly StyledProperty<double> KeyHeightProperty =
        AvaloniaProperty.Register<OnScreenKeyboardPanel, double>(nameof(KeyHeight), 46);

    public static readonly StyledProperty<double> KeyUnitWidthProperty =
        AvaloniaProperty.Register<OnScreenKeyboardPanel, double>(nameof(KeyUnitWidth), 52);

    public static readonly StyledProperty<double> KeyGapProperty =
        AvaloniaProperty.Register<OnScreenKeyboardPanel, double>(nameof(KeyGap), 6);

    public static readonly StyledProperty<double> RowGapProperty =
        AvaloniaProperty.Register<OnScreenKeyboardPanel, double>(nameof(RowGap), 8);

    private readonly List<Row> _rows = new();
    private double _maxUnits;

    static OnScreenKeyboardPanel()
    {
        AffectsMeasure<OnScreenKeyboardPanel>(KeyHeightProperty, KeyUnitWidthProperty, KeyGapProperty, RowGapProperty);
    }

    public double KeyHeight { get => GetValue(KeyHeightProperty); set => SetValue(KeyHeightProperty, value); }

    public double KeyUnitWidth { get => GetValue(KeyUnitWidthProperty); set => SetValue(KeyUnitWidthProperty, value); }

    public double KeyGap { get => GetValue(KeyGapProperty); set => SetValue(KeyGapProperty, value); }

    public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }

    /// <summary>The number of rows laid out last.</summary>
    public int RowCount => _rows.Count;

    /// <summary>The unit count of the widest row.</summary>
    public double MaxUnits => _maxUnits;

    /// <summary>The key whose slot (its key plus half the gaps around it) contains <paramref name="point"/>, so a press between two keys still lands on one.</summary>
    public OnScreenKeyboardKeyPresenter? KeyAt(Point point)
    {
        OnScreenKeyboardKeyPresenter? best = null;
        var bestDistance = double.MaxValue;
        var halfRowGap = RowGap / 2;
        foreach (var child in Children)
        {
            if (child is not OnScreenKeyboardKeyPresenter { IsVisible: true, IsSpacer: false } key || !key.IsEffectivelyEnabled)
            {
                continue;
            }
            var slot = key.Bounds.Inflate(new Thickness(KeyGap / 2, halfRowGap));
            if (!slot.Contains(point))
            {
                continue;
            }
            var center = key.Bounds.Center;
            var distance = Math.Abs(center.X - point.X) + Math.Abs(center.Y - point.Y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = key;
            }
        }
        return best;
    }

    private void CollectRows()
    {
        _rows.Clear();
        _maxUnits = 0;
        Row? current = null;
        var currentIndex = -1;
        foreach (var child in Children)
        {
            if (child is not OnScreenKeyboardKeyPresenter key)
            {
                continue;
            }
            if (current is null || key.Row != currentIndex)
            {
                current = new Row();
                currentIndex = key.Row;
                _rows.Add(current);
            }
            if (!key.IsVisible)
            {
                continue;
            }
            current.Keys.Add(key);
            current.Units += key.Units;
        }
        // A row whose keys are all hidden is hidden with them.
        _rows.RemoveAll(static row => row.Keys.Count == 0);
        foreach (var row in _rows)
        {
            _maxUnits = Math.Max(_maxUnits, row.Units);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        CollectRows();
        if (_rows.Count == 0 || _maxUnits <= 0)
        {
            return default;
        }
        var gap = KeyGap;
        var naturalWidth = Math.Max(0, _maxUnits * KeyUnitWidth - gap);
        var width = double.IsFinite(availableSize.Width) ? Math.Min(availableSize.Width, naturalWidth) : naturalWidth;
        var naturalHeight = _rows.Count * KeyHeight + (_rows.Count - 1) * RowGap;
        var unit = (width + gap) / _maxUnits;
        foreach (var row in _rows)
        {
            foreach (var key in row.Keys)
            {
                key.Measure(new Size(Math.Max(0, key.Units * unit - gap), KeyHeight));
            }
        }
        return new Size(width, naturalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_rows.Count == 0 || _maxUnits <= 0)
        {
            return finalSize;
        }
        var gap = KeyGap;
        var unit = (finalSize.Width + gap) / _maxUnits;
        var rowHeight = Math.Max(0, (finalSize.Height - (_rows.Count - 1) * RowGap) / _rows.Count);
        var y = 0d;
        foreach (var row in _rows)
        {
            // The row's slots span (units × unit), which is the width plus one gap for the widest row; half a gap hangs
            // over each edge, so the outermost keys sit flush with the panel.
            var x = (finalSize.Width + gap - row.Units * unit) / 2 - gap / 2;
            foreach (var key in row.Keys)
            {
                var slotWidth = key.Units * unit;
                key.Arrange(new Rect(x + gap / 2, y, Math.Max(0, slotWidth - gap), rowHeight));
                x += slotWidth;
            }
            y += rowHeight + RowGap;
        }
        return finalSize;
    }

    private sealed class Row
    {
        public List<OnScreenKeyboardKeyPresenter> Keys { get; } = new();

        public double Units { get; set; }
    }
}
