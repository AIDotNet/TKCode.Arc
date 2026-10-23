using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One row of a <see cref="RankedBarList"/>: mark, name, win-rate bar and the value as a rolling whole percentage. The list
/// owns it: it sets the row's item, slot and state, and moves it with the row's own motion values (offset from its slot,
/// bar fill, opacity), so a replay only touches render state. Screen readers get one list item named by the list; the
/// parts inside are not read on their own.
/// </summary>
[TemplatePart(MarkPart, typeof(ModelMark))]
[TemplatePart(NamePart, typeof(TextBlock))]
[TemplatePart(BarPart, typeof(RankedBarBar))]
[TemplatePart(ValuePart, typeof(RollingDigits))]
internal sealed class RankedBarRow : TemplatedControl
{
    public const string MarkPart = "PART_Mark";
    public const string NamePart = "PART_Name";
    public const string BarPart = "PART_Bar";
    public const string ValuePart = "PART_Value";

    public static readonly StyledProperty<string?> ItemNameProperty =
        AvaloniaProperty.Register<RankedBarRow, string?>(nameof(ItemName));

    public static readonly StyledProperty<object?> MarkProperty =
        AvaloniaProperty.Register<RankedBarRow, object?>(nameof(Mark));

    public static readonly StyledProperty<IBrush?> MarkColorProperty =
        AvaloniaProperty.Register<RankedBarRow, IBrush?>(nameof(MarkColor));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<RankedBarRow, IBrush?>(nameof(FillBrush));

    private readonly TranslateTransform _translate = new();
    private readonly MotionValue<double> _offset;
    private readonly MotionValue<double> _fill;
    private readonly MotionValue<double> _fade;
    private RankedBarBar? _bar;
    private RollingDigits? _digits;
    private RankedBarItem _item;
    private int _slot;
    private int _percent;

    static RankedBarRow()
    {
        FocusableProperty.OverrideDefaultValue<RankedBarRow>(false);
    }

    public RankedBarRow(RankedBarItem item)
    {
        _item = item;
        RenderTransform = _translate;
        // Code-driven: the offset starts from where the row was drawn before a reorder (its old rank), the fill from the
        // old value, and both are retargeted from the list's replay timeline; a ControlTheme transition has no such
        // starting point.
        _offset = new MotionValue<double>(this, 0, MotionVectors.Double);
        _offset.Changed += y => _translate.Y = y;
        _fill = new MotionValue<double>(this, item.Ratio, MotionVectors.Double);
        _fill.Changed += ratio =>
        {
            if (_bar is not null)
            {
                _bar.Ratio = ratio;
            }
        };
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += opacity => Opacity = opacity;
        _percent = item.Percent;
        ApplyItem();
    }

    /// <summary>The visible name.</summary>
    public string? ItemName { get => GetValue(ItemNameProperty); set => SetValue(ItemNameProperty, value); }

    /// <summary>The mark (see <see cref="ModelMark.Mark"/>).</summary>
    public object? Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <summary>The fill of a geometry mark; null uses the foreground.</summary>
    public IBrush? MarkColor { get => GetValue(MarkColorProperty); set => SetValue(MarkColorProperty, value); }

    /// <summary>The bar's fill (the item's series color).</summary>
    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    /// <summary>The item the row shows.</summary>
    public RankedBarItem Item
    {
        get => _item;
        set
        {
            _item = value;
            ApplyItem();
        }
    }

    /// <summary>The row's place from the top (its layout position; a replay draws it elsewhere through <see cref="Offset"/>).</summary>
    public int Slot
    {
        get => _slot;
        set
        {
            if (_slot == value)
            {
                return;
            }
            _slot = value;
            (Parent as Avalonia.Layout.Layoutable)?.InvalidateArrange();
        }
    }

    /// <summary>Shown, entering or leaving.</summary>
    public RankedBarRowState State { get; set; }

    /// <summary>True while the row is shown (laid out and read).</summary>
    public bool IsShown => State == RankedBarRowState.Shown;

    /// <summary>How far from its slot the row is drawn right now.</summary>
    public double Offset => _offset.Value;

    /// <summary>The share of the bar filled right now.</summary>
    public double FillRatio => _fill.Value;

    /// <summary>The whole percentage the number shows (or rolls to).</summary>
    public int Percent => _percent;

    /// <summary>True while the offset, the fill or the opacity is moving.</summary>
    public bool IsMoving => _offset.IsAnimating || _fill.IsAnimating || _fade.IsAnimating;

    /// <summary>The bar (template part).</summary>
    internal RankedBarBar? Bar => _bar;

    /// <summary>The number (template part).</summary>
    internal RollingDigits? Digits => _digits;

    /// <summary>Shows <paramref name="ratio"/> and <paramref name="percent"/> at its slot, opaque, at once.</summary>
    public void Rest(double ratio, int percent)
    {
        _offset.Jump(0);
        _fill.Jump(ratio);
        _fade.Jump(1);
        RollTo(percent, instant: true);
    }

    /// <summary>Draws the row <paramref name="from"/> away from its slot and moves it home with <paramref name="spec"/>.</summary>
    public Task<bool> MoveHome(double from, MotionSpec spec)
    {
        _offset.Jump(from);
        return _offset.AnimateTo(0, spec);
    }

    /// <summary>Moves the bar's fill to <paramref name="ratio"/>.</summary>
    public Task<bool> FillTo(double ratio, MotionSpec spec) => _fill.AnimateTo(ratio, spec);

    /// <summary>Fades the row to <paramref name="opacity"/>.</summary>
    public Task<bool> FadeTo(double opacity, MotionSpec spec) => _fade.AnimateTo(opacity, spec);

    /// <summary>Sets the opacity at once.</summary>
    public void JumpOpacity(double opacity) => _fade.Jump(opacity);

    /// <summary>Jumps every motion to its target (a replay that has to end now).</summary>
    public void Finish()
    {
        _offset.Jump(_offset.Target);
        _fill.Jump(_fill.Target);
        _fade.Jump(_fade.Target);
        RollTo(_percent, instant: true);
    }

    /// <summary>Rolls the number to <paramref name="percent"/>; instant (or a number not yet shown) jumps.</summary>
    public void RollTo(int percent, bool instant)
    {
        _percent = percent;
        if (_digits is null)
        {
            return;
        }
        if (instant)
        {
            _digits.IsInstant = true;
            _digits.Value = percent;
            _digits.IsInstant = false;
        }
        else
        {
            _digits.Value = percent;
        }
    }

    private void ApplyItem()
    {
        ItemName = _item.Name;
        Mark = _item.Mark;
        MarkColor = _item.MarkColor;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _bar = e.NameScope.Find<RankedBarBar>(BarPart);
        _digits = e.NameScope.Find<RollingDigits>(ValuePart);
        if (_bar is not null)
        {
            _bar.Ratio = _fill.Value;
        }
        RollTo(_percent, instant: true);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RowPeer(this);

    /// <summary>A list item read by its name alone (rank, name, win rate); mark, bar and number stay silent.</summary>
    private sealed class RowPeer(RankedBarRow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
