using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>One line of the breakdown as shown: key, label, optional note, formatted amount, and how to color it.</summary>
/// <param name="Key">Stable key; lines with a new key open, lines whose key went away close.</param>
/// <param name="Label">Left text.</param>
/// <param name="Note">Small text under the label.</param>
/// <param name="Amount">Formatted amount (or a pending note such as "Calculated at checkout").</param>
/// <param name="IsDiscount">True colors the amount green.</param>
/// <param name="IsPending">True shows the amount as muted text (no amount yet).</param>
internal sealed record CheckoutSummaryLineView(string Key, string Label, string? Note, string Amount, bool IsDiscount = false, bool IsPending = false);

/// <summary>A breakdown line: label (with an optional note) on the left, the amount on the right.</summary>
[PseudoClasses(DiscountPseudoClass, PendingPseudoClass)]
internal sealed class CheckoutSummaryLineRow : TemplatedControl
{
    public const string DiscountPseudoClass = ":discount";
    public const string PendingPseudoClass = ":pending";

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<CheckoutSummaryLineRow, string?>(nameof(Label));

    public static readonly StyledProperty<string?> NoteProperty =
        AvaloniaProperty.Register<CheckoutSummaryLineRow, string?>(nameof(Note));

    public static readonly StyledProperty<string?> AmountProperty =
        AvaloniaProperty.Register<CheckoutSummaryLineRow, string?>(nameof(Amount));

    public static readonly StyledProperty<bool> HasNoteProperty =
        AvaloniaProperty.Register<CheckoutSummaryLineRow, bool>(nameof(HasNote));

    public CheckoutSummaryLineRow(string key) => Key = key;

    public string Key { get; }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? Note { get => GetValue(NoteProperty); set => SetValue(NoteProperty, value); }

    public string? Amount { get => GetValue(AmountProperty); set => SetValue(AmountProperty, value); }

    public bool HasNote { get => GetValue(HasNoteProperty); set => SetValue(HasNoteProperty, value); }

    /// <summary>Copies a view's text and coloring.</summary>
    public void Apply(CheckoutSummaryLineView view)
    {
        Label = view.Label;
        Note = view.Note;
        HasNote = !string.IsNullOrEmpty(view.Note);
        Amount = view.Amount;
        PseudoClasses.Set(DiscountPseudoClass, view.IsDiscount);
        PseudoClasses.Set(PendingPseudoClass, view.IsPending);
        AutomationProperties.SetName(this, string.IsNullOrEmpty(view.Note) ? $"{view.Label}: {view.Amount}" : $"{view.Label}, {view.Note}: {view.Amount}");
    }
}

/// <summary>
/// The breakdown and total of <see cref="CheckoutSummary"/> (a candidate for a shared Pricing helper: cart drawer, live
/// checkout, seat manager): lines keyed by id that open and close with a height spring and a 240 ms fade (the rest of the
/// card follows the height), negative amounts in green, and a total row whose digits roll on the smooth spring.
/// </summary>
[TemplatePart(RowsPart, typeof(Panel))]
[TemplatePart(TotalLabelPart, typeof(TextBlock))]
[TemplatePart(TotalPart, typeof(RollingDigits))]
internal sealed class CheckoutSummaryLines : TemplatedControl
{
    public const string RowsPart = "PART_Rows";
    public const string TotalLabelPart = "PART_TotalLabel";
    public const string TotalPart = "PART_Total";

    private readonly List<(CheckoutSummaryLineRow Row, CheckoutSummaryCollapse Host)> _rows = new();
    private IReadOnlyList<CheckoutSummaryLineView> _views = Array.Empty<CheckoutSummaryLineView>();
    private Panel? _panel;
    private TextBlock? _totalLabel;
    private RollingDigits? _total;
    private string _totalText = string.Empty;
    private double _totalValue;
    private string? _prefix;
    private string? _suffix;
    private int _decimals = 2;
    private string _cultureName = "en-US";
    private string? _totalAnnouncement;

    /// <summary>Label of the total row.</summary>
    public string TotalLabel
    {
        get => _totalText;
        set
        {
            _totalText = value;
            if (_totalLabel is not null)
            {
                _totalLabel.Text = value;
            }
        }
    }

    /// <summary>The rolling total control (tests).</summary>
    internal RollingDigits? TotalDigits => _total;

    /// <summary>Rows currently in the panel, closing ones included (tests).</summary>
    internal IReadOnlyList<CheckoutSummaryLineRow> Rows => _rows.ConvertAll(r => r.Row);

    /// <summary>Rows that are open or opening (tests).</summary>
    internal IEnumerable<CheckoutSummaryLineRow> OpenRows => _rows.Where(r => r.Host.IsOpen).Select(r => r.Row);

    /// <summary>Sets the total: value, currency shape, culture and what a screen reader hears.</summary>
    public void SetTotal(decimal value, string prefix, string suffix, int decimals, string cultureName, string announcement)
    {
        _totalValue = (double)value;
        _prefix = prefix;
        _suffix = suffix;
        _decimals = decimals;
        _cultureName = cultureName;
        _totalAnnouncement = announcement;
        ApplyTotal();
    }

    /// <summary>Shows <paramref name="views"/>: matching keys update in place, new keys open, missing keys close.</summary>
    public void SetLines(IReadOnlyList<CheckoutSummaryLineView> views, bool animate)
    {
        _views = views;
        if (_panel is null)
        {
            return;
        }
        animate = animate && IsLoaded;
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        foreach (var view in views)
        {
            wanted.Add(view.Key);
        }
        // Close lines that went away; they stay in place until their spring ends.
        foreach (var (row, host) in _rows.ToArray())
        {
            if (!wanted.Contains(row.Key) && host.IsOpen)
            {
                host.SetOpen(false, animate);
            }
        }
        // Lay out the wanted lines in order, keeping closing lines where they were.
        var order = new List<(CheckoutSummaryLineRow Row, CheckoutSummaryCollapse Host)>(views.Count + _rows.Count);
        foreach (var view in views)
        {
            var index = _rows.FindIndex(r => r.Row.Key == view.Key);
            (CheckoutSummaryLineRow Row, CheckoutSummaryCollapse Host) entry;
            if (index >= 0)
            {
                entry = _rows[index];
                entry.Row.Apply(view);
                if (!entry.Host.IsOpen)
                {
                    entry.Host.SetOpen(true, animate);
                }
            }
            else
            {
                var row = new CheckoutSummaryLineRow(view.Key);
                row.Apply(view);
                var host = new CheckoutSummaryCollapse { Child = row };
                host.SetOpen(!animate, animate: false);
                host.Closed += OnRowClosed;
                entry = (row, host);
                if (animate)
                {
                    _pendingOpen.Add(host);
                }
            }
            order.Add(entry);
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            var entry = _rows[i];
            if (!wanted.Contains(entry.Row.Key))
            {
                order.Insert(Math.Min(i, order.Count), entry);
            }
        }
        _rows.Clear();
        _rows.AddRange(order);
        Sync();
        if (_pendingOpen.Count > 0)
        {
            foreach (var host in _pendingOpen)
            {
                host.SetOpen(true, animate: true);
            }
            _pendingOpen.Clear();
        }
    }

    private readonly List<CheckoutSummaryCollapse> _pendingOpen = new();

    private void OnRowClosed(object? sender, EventArgs e)
    {
        var index = _rows.FindIndex(r => ReferenceEquals(r.Host, sender));
        if (index >= 0 && !_rows[index].Host.IsOpen)
        {
            _rows.RemoveAt(index);
            Sync();
        }
    }

    private void Sync()
    {
        if (_panel is null)
        {
            return;
        }
        var children = _panel.Children;
        for (var i = 0; i < _rows.Count; i++)
        {
            var host = _rows[i].Host;
            var at = children.IndexOf(host);
            if (at == i)
            {
                continue;
            }
            if (at >= 0)
            {
                children.Move(at, i);
            }
            else
            {
                children.Insert(i, host);
            }
        }
        while (children.Count > _rows.Count)
        {
            children.RemoveAt(children.Count - 1);
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _panel = e.NameScope.Find<Panel>(RowsPart);
        _totalLabel = e.NameScope.Find<TextBlock>(TotalLabelPart);
        _total = e.NameScope.Find<RollingDigits>(TotalPart);
        if (_totalLabel is not null)
        {
            _totalLabel.Text = _totalText;
        }
        _rows.Clear();
        SetLines(_views, animate: false);
        ApplyTotal();
    }

    private void ApplyTotal()
    {
        if (_total is null)
        {
            return;
        }
        _total.Prefix = _prefix;
        _total.Suffix = _suffix;
        _total.Decimals = _decimals;
        _total.CultureName = _cultureName;
        _total.Value = _totalValue;
        AutomationProperties.SetName(_total, _totalAnnouncement ?? string.Empty);
    }
}
