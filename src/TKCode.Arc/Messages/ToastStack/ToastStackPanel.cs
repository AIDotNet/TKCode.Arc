using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering;

namespace TKCode.Arc;

/// <summary>
/// The viewport and list of a <see cref="ToastStack"/>. It fills the stack's area but only the list (the toasts and
/// the gaps between them) takes pointer input, so clicks around the toasts reach the page. Toasts hang from the
/// bottom of the list; tucked toasts take the front toast's height so their edges line up, open ones their own.
/// </summary>
internal sealed class ToastStackPanel : Panel, ICustomHitTest
{
    /// <summary>Each tucked toast shows this many pixels above the one in front of it.</summary>
    internal const double Peek = 14;

    /// <summary>Space between toasts when the stack is open.</summary>
    internal const double Gap = 12;

    /// <summary>Tucked toasts shrink by this much per step back.</summary>
    internal const double Step = 0.05;

    private readonly List<ToastStackItem> _present = new();
    private readonly List<double> _tops = new();
    private Rect _listRect;

    public ToastStack? Owner { get; set; }

    /// <summary>Present toasts, newest first (leaving toasts are children too but not in this list).</summary>
    public IReadOnlyList<ToastStackItem> Present => _present;

    public Rect ListRect => _listRect;

    public double ListHeight { get; private set; }

    public double ListWidth { get; private set; }

    public void AddFront(ToastStackItem item)
    {
        _present.Insert(0, item);
        Children.Add(item);
        InvalidateMeasure();
    }

    public void MarkLeaving(ToastStackItem item)
    {
        _present.Remove(item);
        InvalidateMeasure();
    }

    public void RemoveItem(ToastStackItem item)
    {
        _present.Remove(item);
        Children.Remove(item);
        InvalidateMeasure();
    }

    bool ICustomHitTest.HitTest(Point point) => _listRect.Contains(point);

    private (double Width, double Bottom, double Inset, bool Center) Geometry(Size size)
    {
        var owner = Owner;
        var contained = owner?.IsContained ?? false;
        var position = owner?.Position ?? ToastStackPosition.BottomRight;
        var maxWidth = owner?.MaxListWidth ?? 360;
        var width = double.IsInfinity(size.Width) ? maxWidth : Math.Max(0, Math.Min(maxWidth, size.Width - 32));
        // Small windows always center the stack at the bottom (max-width: 30rem).
        var narrow = !contained && !double.IsInfinity(size.Width) && size.Width < 480;
        var bottom = contained || narrow ? 16 : 24;
        var inset = contained ? 16 : 24;
        var center = narrow || position == ToastStackPosition.BottomCenter;
        return (width, bottom, inset, center);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var (width, bottom, inset, _) = Geometry(availableSize);
        ListWidth = width;
        foreach (var child in Children)
        {
            child.Measure(new Size(width, double.PositiveInfinity));
        }
        ListHeight = ComputeListHeight();
        var desiredWidth = double.IsInfinity(availableSize.Width) ? width + 2 * inset : availableSize.Width;
        var desiredHeight = ListHeight + bottom;
        return new Size(desiredWidth, double.IsInfinity(availableSize.Height) ? desiredHeight : Math.Max(desiredHeight, 0));
    }

    private bool Expanded => Owner?.IsExpanded ?? false;

    private int VisibleCount => Math.Max(1, Owner?.VisibleToasts ?? 3);

    private double HeightOf(ToastStackItem item) => item.NaturalHeight;

    private double ComputeListHeight()
    {
        if (_present.Count == 0)
        {
            return 0;
        }
        var visible = VisibleCount;
        _tops.Clear();
        _tops.Add(0);
        for (var i = 0; i < _present.Count; i++)
        {
            _tops.Add(_tops[i] + (i < visible ? HeightOf(_present[i]) + Gap : 0));
        }
        var front = HeightOf(_present[0]);
        return Expanded ? _tops[_present.Count] - Gap : front + Peek * (Math.Min(_present.Count, visible) - 1);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var (width, bottom, inset, center) = Geometry(finalSize);
        var position = Owner?.Position ?? ToastStackPosition.BottomRight;
        var x = center
            ? (finalSize.Width - width) / 2
            : position == ToastStackPosition.BottomLeft ? inset : finalSize.Width - inset - width;
        var listBottom = finalSize.Height - bottom;
        _listRect = new Rect(x, listBottom - ListHeight, width, ListHeight);
        foreach (var child in Children)
        {
            var height = child.DesiredSize.Height;
            child.Arrange(new Rect(x, listBottom - height, width, height));
        }
        ApplyTargets();
        return finalSize;
    }

    /// <summary>Computes every present toast's place and retargets its springs.</summary>
    public void ApplyTargets()
    {
        if (_present.Count == 0)
        {
            return;
        }
        var expanded = Expanded;
        var visible = VisibleCount;
        if (_tops.Count != _present.Count + 1)
        {
            ComputeListHeight();
        }
        var front = HeightOf(_present[0]);
        for (var i = 0; i < _present.Count; i++)
        {
            var item = _present[i];
            var shown = i < visible;
            var depth = Math.Min(i, visible);
            var own = HeightOf(item);
            var target = expanded
                ? new ToastStackTarget(-_tops[i], 1, own, shown ? 1 : 0, 1)
                : new ToastStackTarget(-Peek * depth, 1 - Step * depth, i == 0 ? own : front, shown ? 1 : 0, i == 0 ? 1 : 0);
            item.ZIndex = (int)Math.Min(int.MaxValue, item.Record.Seq);
            item.ApplyTarget(target, expanded, i == 0, !shown);
        }
    }
}
