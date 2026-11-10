using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>One card of a <see cref="FeatureBento"/>: the live drawing on top, then the title and the sentence.</summary>
internal sealed class FeatureBentoCard : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<FeatureBentoCard, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<FeatureBentoCard, string?>(nameof(Description));

    public static readonly StyledProperty<FeatureBentoArt> ArtProperty =
        AvaloniaProperty.Register<FeatureBentoCard, FeatureBentoArt>(nameof(Art));

    private LazyFocusRing? _ring;

    static FeatureBentoCard()
    {
        FocusableProperty.OverrideDefaultValue<FeatureBentoCard>(true);
        ClipToBoundsProperty.OverrideDefaultValue<FeatureBentoCard>(false);
    }

    public FeatureBentoCard(FeatureBentoItem item)
    {
        Item = item;
        Title = item.Title;
        Description = item.Description;
        Art = item.Art;
        AutomationProperties.SetName(this, item.Title);
        AutomationProperties.SetHelpText(this, item.Description);
    }

    public FeatureBentoItem Item { get; }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public FeatureBentoArt Art { get => GetValue(ArtProperty); set => SetValue(ArtProperty, value); }

    /// <summary>The cell the panel placed the card in: column, row, column span, row span.</summary>
    public (int Column, int Row, int ColumnSpan, int RowSpan) Cell { get; set; }

    /// <summary>Raised on a click, a tap, Enter or Space.</summary>
    public event EventHandler? Invoked;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _ring?.Attach(null, null);
        if (e.NameScope.Find<Panel>("PART_RingHost") is { } host)
        {
            _ring = new LazyFocusRing(this, ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness),
                ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep28));
            _ring.Attach(ring =>
            {
                ring.Margin = new Thickness(-4);
                host.Children.Add(ring);
            }, ring => host.Children.Remove(ring));
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _ring?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _ring?.Update();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Invoked?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Enter or Key.Space)
        {
            Invoked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}

/// <summary>
/// The bento grid: six equal columns (two below the compact width, one below the narrow width) and rows of one
/// <see cref="RowHeight"/>. Cards are packed in order into the first free place their size fits, row by row, so a tall
/// card leaves room beside it for the next ones. <see cref="FeatureBentoSize"/> maps to spans per column count.
/// </summary>
internal sealed class FeatureBentoPanel : Panel
{
    public static readonly StyledProperty<int> ColumnCountProperty =
        AvaloniaProperty.Register<FeatureBentoPanel, int>(nameof(ColumnCount), 6);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<FeatureBentoPanel, double>(nameof(RowHeight), 220);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<FeatureBentoPanel, double>(nameof(Gap), 16);

    static FeatureBentoPanel()
    {
        AffectsMeasure<FeatureBentoPanel>(ColumnCountProperty, RowHeightProperty, GapProperty);
    }

    public int ColumnCount { get => GetValue(ColumnCountProperty); set => SetValue(ColumnCountProperty, value); }

    public double RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <summary>The rows of the last layout.</summary>
    public int RowCount { get; private set; }

    /// <summary>The spans of <paramref name="size"/> in a grid of <paramref name="columns"/> columns.</summary>
    internal static (int Columns, int Rows) Span(FeatureBentoSize size, int columns)
    {
        if (columns <= 1)
        {
            return (1, 1);
        }
        if (columns < 6)
        {
            return size switch
            {
                FeatureBentoSize.Wide => (2, 1),
                FeatureBentoSize.Large => (2, 2),
                FeatureBentoSize.Tall => (1, 2),
                _ => (1, 1)
            };
        }
        return size switch
        {
            FeatureBentoSize.Medium => (3, 1),
            FeatureBentoSize.Wide => (4, 1),
            FeatureBentoSize.Tall => (2, 2),
            FeatureBentoSize.Large => (4, 2),
            _ => (2, 1)
        };
    }

    /// <summary>Packs <paramref name="sizes"/> into <paramref name="columns"/> columns; returns each one's cell and the row count.</summary>
    internal static (List<(int Column, int Row, int ColumnSpan, int RowSpan)> Cells, int Rows) Pack(IReadOnlyList<FeatureBentoSize> sizes, int columns)
    {
        columns = Math.Max(1, columns);
        var taken = new List<bool[]>();
        var cells = new List<(int, int, int, int)>(sizes.Count);
        var rows = 0;
        foreach (var size in sizes)
        {
            var (cs, rs) = Span(size, columns);
            cs = Math.Min(cs, columns);
            for (var row = 0; ; row++)
            {
                var placed = false;
                for (var col = 0; col + cs <= columns; col++)
                {
                    if (!Fits(taken, row, col, cs, rs))
                    {
                        continue;
                    }
                    for (var r = row; r < row + rs; r++)
                    {
                        while (taken.Count <= r)
                        {
                            taken.Add(new bool[columns]);
                        }
                        for (var c = col; c < col + cs; c++)
                        {
                            taken[r][c] = true;
                        }
                    }
                    cells.Add((col, row, cs, rs));
                    rows = Math.Max(rows, row + rs);
                    placed = true;
                    break;
                }
                if (placed)
                {
                    break;
                }
            }
        }
        return (cells, rows);
    }

    private static bool Fits(List<bool[]> taken, int row, int col, int cs, int rs)
    {
        for (var r = row; r < row + rs; r++)
        {
            if (r >= taken.Count)
            {
                continue;
            }
            for (var c = col; c < col + cs; c++)
            {
                if (taken[r][c])
                {
                    return false;
                }
            }
        }
        return true;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 960 : availableSize.Width;
        var columns = Math.Max(1, ColumnCount);
        var cards = Children.OfType<FeatureBentoCard>().ToList();
        var (cells, rows) = Pack(cards.Select(c => c.Item.Size).ToList(), columns);
        var cell = (width - Gap * (columns - 1)) / columns;
        for (var i = 0; i < cards.Count; i++)
        {
            var (_, _, cs, rs) = cells[i];
            cards[i].Cell = cells[i];
            cards[i].Measure(new Size(cell * cs + Gap * (cs - 1), RowHeight * rs + Gap * (rs - 1)));
        }
        RowCount = rows;
        return new Size(width, rows == 0 ? 0 : rows * RowHeight + (rows - 1) * Gap);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Math.Max(1, ColumnCount);
        var cell = (finalSize.Width - Gap * (columns - 1)) / columns;
        foreach (var card in Children.OfType<FeatureBentoCard>())
        {
            var (c, r, cs, rs) = card.Cell;
            card.Arrange(new Rect(c * (cell + Gap), r * (RowHeight + Gap), cell * cs + Gap * (cs - 1), RowHeight * rs + Gap * (rs - 1)));
        }
        return finalSize;
    }
}
