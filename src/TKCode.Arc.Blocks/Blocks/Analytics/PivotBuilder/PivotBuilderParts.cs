using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A field chip: a pill that can be dragged to another zone (the chip follows the pointer by its render transform and the
/// zone under it lights up), or pressed (Enter, Space or a click without moving) to open its menu. Delete takes it out
/// of the layout.
/// </summary>
internal sealed class PivotBuilderChip : AvaloniaButton
{
    private const double DragThreshold = 4;

    private readonly TranslateTransform _shift = new();
    private LazyFocusRing? _focusRing;
    private Point? _pressAt;
    private bool _dragging;

    public PivotBuilderChip(string fieldKey, PivotBuilderZone zone)
    {
        FieldKey = fieldKey;
        Zone = zone;
        RenderTransform = _shift;
    }

    protected override Type StyleKeyOverride => typeof(PivotBuilderChip);

    public string FieldKey { get; }

    public PivotBuilderZone Zone { get; }

    /// <summary>Called with the pointer in the owner's coordinates while dragging, and with null when the drag ends.</summary>
    public Action<PivotBuilderChip, Point?>? DragMoved { get; set; }

    /// <summary>Called when a drag ends over a point (in the owner's coordinates).</summary>
    public Action<PivotBuilderChip, Point>? Dropped { get; set; }

    /// <summary>Called on Delete.</summary>
    public Action<PivotBuilderChip>? RemoveRequested { get; set; }

    /// <summary>The control whose coordinates drag positions are reported in.</summary>
    public Visual? Owner { get; set; }

    public bool IsDragging => _dragging;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, ArcDesignTokens.Key(ArcDesignTokenKind.FocusEdgeThickness), ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(ring =>
        {
            ring.Margin = new Thickness(-3);
            e.NameScope.Find<Panel>("PART_RingHost")?.Children.Insert(0, ring);
        }, ring => (ring.Parent as Panel)?.Children.Remove(ring));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && Owner is not null)
        {
            _pressAt = e.GetPosition(Owner);
        }
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pressAt is not { } start || Owner is null)
        {
            return;
        }
        var now = e.GetPosition(Owner);
        if (!_dragging && (Math.Abs(now.X - start.X) > DragThreshold || Math.Abs(now.Y - start.Y) > DragThreshold))
        {
            _dragging = true;
            e.Pointer.Capture(this);
            PseudoClasses.Set(":dragging", true);
            ZIndex = 10;
        }
        if (_dragging)
        {
            _shift.X = now.X - start.X;
            _shift.Y = now.Y - start.Y;
            DragMoved?.Invoke(this, now);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_dragging && Owner is not null)
        {
            var at = e.GetPosition(Owner);
            EndDrag();
            e.Pointer.Capture(null);
            e.Handled = true;
            Dropped?.Invoke(this, at);
            return;
        }
        _pressAt = null;
        base.OnPointerReleased(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        _dragging = false;
        _pressAt = null;
        _shift.X = 0;
        _shift.Y = 0;
        ZIndex = 0;
        PseudoClasses.Set(":dragging", false);
        DragMoved?.Invoke(this, null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if ((e.Key == Key.Delete || e.Key == Key.Back) && Zone != PivotBuilderZone.None)
        {
            RemoveRequested?.Invoke(this);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>A drop zone: a titled box holding chips; it shows <c>:target</c> while a dragged chip is over it.</summary>
internal sealed class PivotBuilderZoneBox : ContentControl
{
    public PivotBuilderZoneBox(PivotBuilderZone zone)
    {
        Zone = zone;
    }

    protected override Type StyleKeyOverride => typeof(PivotBuilderZoneBox);

    public PivotBuilderZone Zone { get; }

    public WrapPanel Chips { get; } = new() { ItemSpacing = 6, LineSpacing = 6 };

    public bool IsTarget
    {
        get => Classes.Contains(":target");
        set => PseudoClasses.Set(":target", value);
    }
}

/// <summary>A row of the pivot table: its row labels and its cell values (null for no data).</summary>
internal sealed class PivotBuilderRow
{
    public PivotBuilderRow(string[] labels, double?[] cells, bool isTotal)
    {
        Labels = labels;
        Cells = cells;
        IsTotal = isTotal;
    }

    public string[] Labels { get; }

    public double?[] Cells { get; }

    public bool IsTotal { get; }
}
