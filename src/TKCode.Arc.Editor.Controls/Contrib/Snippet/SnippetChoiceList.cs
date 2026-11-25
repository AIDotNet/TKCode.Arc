// The list of a choice placeholder's options (behavior reference: upstream shows them in the suggest widget,
// src/vs/editor/contrib/suggest/browser/suggestWidget.ts, through showSimpleSuggestions; commit b57d5c9e, MIT license).
// The snippet controller shows this list when no suggest feature takes the choices (ISnippetInserter
// .ShowSimpleSuggestions). It never takes focus: the editor keeps the keyboard and the controller moves the selection
// (Up / Down / Page Up / Page Down / Home / End), accepts (Enter, Tab) and hides it (Escape).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>A small list of a snippet choice's options, drawn under the cursor.</summary>
[TemplatePart(PartRows, typeof(SnippetChoiceRows))]
internal sealed class SnippetChoiceList : TemplatedControl
{
    internal const string PartRows = "PART_Rows";

    /// <summary>The background of the selected option.</summary>
    public static readonly StyledProperty<IBrush?> SelectedBackgroundProperty =
        AvaloniaProperty.Register<SnippetChoiceList, IBrush?>(nameof(SelectedBackground));

    /// <summary>The text color of the selected option.</summary>
    public static readonly StyledProperty<IBrush?> SelectedForegroundProperty =
        AvaloniaProperty.Register<SnippetChoiceList, IBrush?>(nameof(SelectedForeground));

    /// <summary>The space around an option's text.</summary>
    public static readonly StyledProperty<Thickness> ItemPaddingProperty =
        AvaloniaProperty.Register<SnippetChoiceList, Thickness>(nameof(ItemPadding));

    /// <summary>The corner radius of the selected option's background.</summary>
    public static readonly StyledProperty<CornerRadius> ItemCornerRadiusProperty =
        AvaloniaProperty.Register<SnippetChoiceList, CornerRadius>(nameof(ItemCornerRadius));

    /// <summary>The height of one option.</summary>
    public static readonly StyledProperty<double> ItemHeightProperty =
        AvaloniaProperty.Register<SnippetChoiceList, double>(nameof(ItemHeight), 22);

    /// <summary>How many options show at once; more scroll.</summary>
    public static readonly StyledProperty<int> MaxVisibleItemsProperty =
        AvaloniaProperty.Register<SnippetChoiceList, int>(nameof(MaxVisibleItems), 12);

    private IReadOnlyList<string> _items = [];
    private int _selectedIndex;
    private int _firstVisible;
    private double _availableHeight = double.PositiveInfinity;
    private SnippetChoiceRows? _rows;

    /// <summary>The background of the selected option.</summary>
    public IBrush? SelectedBackground
    {
        get => GetValue(SelectedBackgroundProperty);
        set => SetValue(SelectedBackgroundProperty, value);
    }

    /// <summary>The text color of the selected option.</summary>
    public IBrush? SelectedForeground
    {
        get => GetValue(SelectedForegroundProperty);
        set => SetValue(SelectedForegroundProperty, value);
    }

    /// <summary>The space around an option's text.</summary>
    public Thickness ItemPadding
    {
        get => GetValue(ItemPaddingProperty);
        set => SetValue(ItemPaddingProperty, value);
    }

    /// <summary>The corner radius of the selected option's background.</summary>
    public CornerRadius ItemCornerRadius
    {
        get => GetValue(ItemCornerRadiusProperty);
        set => SetValue(ItemCornerRadiusProperty, value);
    }

    /// <summary>The height of one option.</summary>
    public double ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>How many options show at once; more scroll.</summary>
    public int MaxVisibleItems
    {
        get => GetValue(MaxVisibleItemsProperty);
        set => SetValue(MaxVisibleItemsProperty, value);
    }

    /// <summary>The options, in order.</summary>
    public IReadOnlyList<string> Items
    {
        get => _items;
        set
        {
            _items = value;
            _selectedIndex = 0;
            _firstVisible = 0;
            InvalidateRows(measure: true);
        }
    }

    /// <summary>The selected option's index.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            var index = _items.Count == 0 ? 0 : Math.Clamp(value, 0, _items.Count - 1);
            if (index == _selectedIndex)
            {
                return;
            }

            _selectedIndex = index;
            var visible = VisibleCount;
            if (_selectedIndex < _firstVisible)
            {
                _firstVisible = _selectedIndex;
            }
            else if (_selectedIndex >= _firstVisible + visible)
            {
                _firstVisible = _selectedIndex - visible + 1;
            }

            InvalidateRows(measure: false);
        }
    }

    /// <summary>The first option shown (the list scrolls when there are more than <see cref="MaxVisibleItems"/>).</summary>
    public int FirstVisibleIndex => _firstVisible;

    /// <summary>How many options show: at most <see cref="MaxVisibleItems"/>, and as many as fit <see cref="AvailableHeight"/>.</summary>
    public int VisibleCount
    {
        get
        {
            var count = Math.Min(_items.Count, Math.Max(1, MaxVisibleItems));
            if (double.IsFinite(AvailableHeight) && ItemHeight > 0)
            {
                var fit = (int)Math.Floor((AvailableHeight - ChromeHeight) / ItemHeight);
                count = Math.Min(count, Math.Max(1, fit));
            }

            return count;
        }
    }

    /// <summary>The height the list may take in the editor (the space above or below the choice); unlimited by default.</summary>
    internal double AvailableHeight
    {
        get => _availableHeight;
        set
        {
            _availableHeight = value;
            _firstVisible = Math.Clamp(_firstVisible, 0, Math.Max(0, _selectedIndex));
            if (_selectedIndex >= _firstVisible + VisibleCount)
            {
                _firstVisible = _selectedIndex - VisibleCount + 1;
            }

            InvalidateRows(measure: true);
        }
    }

    /// <summary>The height of the list with all its options up to <see cref="MaxVisibleItems"/> showing.</summary>
    internal double FullHeight => (Math.Min(_items.Count, Math.Max(1, MaxVisibleItems)) * ItemHeight) + ChromeHeight;

    // the border and padding around the rows
    private double ChromeHeight => Padding.Top + Padding.Bottom + BorderThickness.Top + BorderThickness.Bottom;

    /// <summary>Raised when the user clicks an option (its index).</summary>
    public event Action<int>? ItemInvoked;

    /// <summary>Moves the selection by <paramref name="delta"/>, wrapping at the ends like the suggest widget.</summary>
    public void MoveSelection(int delta)
    {
        if (_items.Count == 0)
        {
            return;
        }

        var index = _selectedIndex + delta;
        if (Math.Abs(delta) == 1)
        {
            index = ((index % _items.Count) + _items.Count) % _items.Count;
        }

        SelectedIndex = index;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _rows = e.NameScope.Find<SnippetChoiceRows>(PartRows);
        if (_rows is not null)
        {
            _rows.Owner = this;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MaxVisibleItemsProperty || change.Property == ItemPaddingProperty || change.Property == ItemHeightProperty)
        {
            InvalidateRows(measure: true);
        }
        else if (change.Property == SelectedBackgroundProperty || change.Property == SelectedForegroundProperty || change.Property == ItemCornerRadiusProperty)
        {
            InvalidateRows(measure: false);
        }
    }

    internal void RaiseItemInvoked(int index)
    {
        SelectedIndex = index;
        ItemInvoked?.Invoke(index);
    }

    private void InvalidateRows(bool measure)
    {
        if (_rows is null)
        {
            return;
        }

        if (measure)
        {
            _rows.InvalidateMeasure();
        }

        _rows.InvalidateVisual();
    }
}

/// <summary>Draws the options of a <see cref="SnippetChoiceList"/> with its brushes and font.</summary>
internal sealed class SnippetChoiceRows : Control
{
    private SnippetChoiceList? _owner;

    internal SnippetChoiceList? Owner
    {
        get => _owner;
        set
        {
            _owner = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>The height of one option.</summary>
    internal double RowHeight => _owner?.ItemHeight ?? 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_owner is null || _owner.Items.Count == 0)
        {
            return default;
        }

        var width = 0.0;
        foreach (var item in _owner.Items)
        {
            width = Math.Max(width, Format(item, _owner.Foreground).WidthIncludingTrailingWhitespace);
        }

        width += _owner.ItemPadding.Left + _owner.ItemPadding.Right;
        return new Size(Math.Ceiling(width), RowHeight * _owner.VisibleCount);
    }

    public override void Render(DrawingContext context)
    {
        if (_owner is not { } owner || owner.Items.Count == 0)
        {
            return;
        }

        var rowHeight = RowHeight;
        var padding = owner.ItemPadding;
        var count = owner.VisibleCount;
        for (var i = 0; i < count; i++)
        {
            var index = owner.FirstVisibleIndex + i;
            if (index >= owner.Items.Count)
            {
                break;
            }

            var row = new Rect(0, i * rowHeight, Bounds.Width, rowHeight);
            var selected = index == owner.SelectedIndex;
            if (selected && owner.SelectedBackground is { } background)
            {
                var radius = owner.ItemCornerRadius;
                context.DrawRectangle(background, null, new RoundedRect(row, radius.TopLeft, radius.TopRight, radius.BottomRight, radius.BottomLeft));
            }

            var text = Format(owner.Items[index], selected ? owner.SelectedForeground ?? owner.Foreground : owner.Foreground);
            var y = row.Y + ((rowHeight - text.Height) / 2);
            context.DrawText(text, new Point(row.X + padding.Left, y));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_owner is null || RowHeight <= 0)
        {
            return;
        }

        var index = _owner.FirstVisibleIndex + (int)(e.GetPosition(this).Y / RowHeight);
        if (index >= 0 && index < _owner.Items.Count)
        {
            _owner.RaiseItemInvoked(index);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_owner is null)
        {
            return;
        }

        _owner.MoveSelection(e.Delta.Y > 0 ? -1 : 1);
        e.Handled = true;
    }

    private FormattedText Format(string text, IBrush? brush)
    {
        var owner = _owner!;
        var typeface = new Typeface(owner.FontFamily, owner.FontStyle, owner.FontWeight);
        return new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, owner.FontSize, brush);
    }
}
