using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>A part of a row head that comes and goes with the status.</summary>
internal enum ToolCallHeadSlot
{
    /// <summary>The reason line under the title.</summary>
    Note,

    /// <summary>The quiet meta ("4 results · 1.2s", "Denied").</summary>
    Meta,

    /// <summary>The buttons (Retry, or Deny and Allow).</summary>
    Actions
}

/// <summary>
/// Lays out a call row's head as measured: 12px in from the sides and 6px from the top and bottom; columns mark 16 |
/// title | meta or buttons | status 16 | chevron 16, 10px apart. The first line is 24px (28px with buttons) and a 4px
/// strip follows, so a plain row is 40px; a reason line takes 18px under it (58px), spanning from the title to the end.
/// <see cref="IsCompact"/> (a row under 440px): the reason wraps and the buttons move under it, 6px below, in line with
/// the title (a failed row is 84px); the meta keeps only the duration (the owner decides its text). <see cref="IsTight"/>
/// hides the meta.
/// <para>
/// The reason, the meta and the buttons fade in over 160ms when they appear. When one goes it leaves the layout at once
/// (so the row's height spring starts right away) but stays drawn where it was while it fades out over 160ms, and it no
/// longer takes the pointer or Tab (Arc's pop-layout presence). The fades are code-driven (<see cref="MotionValue{T}"/>)
/// because a leaving part keeps its old place outside the layout, which a ControlTheme transition cannot express.
/// </para>
/// </summary>
internal sealed class ToolCallHeadPanel : Panel
{
    internal const double PaddingX = 12;
    internal const double PaddingY = 6;
    internal const double Gap = 10;
    internal const double Cell = 16;
    internal const double LineHeight = 24;
    internal const double ActionLineHeight = 28;
    internal const double CompactLineHeight = 20;
    internal const double Strip = 4;
    internal const double NoteLineHeight = 18;
    internal const double ActionsGap = 6;

    /// <summary>The left edge of the title, the reason and the compact buttons.</summary>
    internal const double TitleLeft = PaddingX + Cell + Gap;

    private sealed class Slot
    {
        public Control? Control;
        public bool Shown;
        public bool Leaving;
        public Rect Last;
        public MotionValue<double>? Fade;
        public int Run;
    }

    private readonly Slot[] _slots = [new(), new(), new()];
    private bool _compact;
    private bool _tight;

    /// <summary>The tool mark (16px).</summary>
    public Control? MarkPart { get; set; }

    /// <summary>The title.</summary>
    public Control? TitlePart { get; set; }

    /// <summary>The status icon (16px).</summary>
    public Control? StatusPart { get; set; }

    /// <summary>The chevron (16px).</summary>
    public Control? ChevronPart { get; set; }

    /// <summary>The reason line (a <see cref="TextBlock"/>: one trimmed line, or wrapped when compact).</summary>
    public Control? NotePart { get => _slots[0].Control; set => Attach(0, value); }

    /// <summary>The meta.</summary>
    public Control? MetaPart { get => _slots[1].Control; set => Attach(1, value); }

    /// <summary>The buttons.</summary>
    public Control? ActionsPart { get => _slots[2].Control; set => Attach(2, value); }

    /// <summary>The compact layout (a row under 440px).</summary>
    public bool IsCompact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }
            _compact = value;
            if (NotePart is TextBlock note)
            {
                note.TextWrapping = value ? TextWrapping.Wrap : TextWrapping.NoWrap;
                note.TextTrimming = value ? TextTrimming.None : TextTrimming.CharacterEllipsis;
            }
            InvalidateMeasure();
        }
    }

    /// <summary>The tightest layout (a row under 370px): no meta.</summary>
    public bool IsTight
    {
        get => _tight;
        set
        {
            if (_tight == value)
            {
                return;
            }
            _tight = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Whether a slot is shown (and not leaving).</summary>
    internal bool IsShown(ToolCallHeadSlot slot) => _slots[(int)slot].Shown;

    /// <summary>Whether a slot is fading out.</summary>
    internal bool IsLeaving(ToolCallHeadSlot slot) => _slots[(int)slot].Leaving;

    private void Attach(int index, Control? control)
    {
        var slot = _slots[index];
        slot.Control = control;
        if (control is not null)
        {
            control.IsVisible = slot.Shown;
        }
        InvalidateMeasure();
    }

    /// <summary>Shows or hides a slot; <paramref name="animate"/> fades it (full motion only).</summary>
    internal void SetShown(ToolCallHeadSlot which, bool shown, bool animate)
    {
        var slot = _slots[(int)which];
        if (slot.Control is not { } control)
        {
            slot.Shown = shown;
            return;
        }
        if (slot.Shown == shown && !(shown && slot.Leaving))
        {
            return;
        }
        slot.Shown = shown;
        slot.Run++;
        var run = slot.Run;
        var fades = animate && IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        if (shown)
        {
            slot.Leaving = false;
            control.IsVisible = true;
            control.IsHitTestVisible = which == ToolCallHeadSlot.Actions;
            KeyboardNavigation.SetTabNavigation(control, KeyboardNavigationMode.Continue);
            slot.Fade ??= CreateFade(control);
            if (fades)
            {
                if (slot.Fade.Value >= 0.999)
                {
                    slot.Fade.Jump(0);
                }
                _ = slot.Fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                slot.Fade.Jump(1);
            }
            control.Opacity = slot.Fade.Value;
        }
        else
        {
            // Out of the layout now; drawn where it was until it has faded, and no longer reachable.
            control.IsHitTestVisible = false;
            KeyboardNavigation.SetTabNavigation(control, KeyboardNavigationMode.None);
            slot.Fade ??= CreateFade(control);
            if (fades && control.IsVisible)
            {
                slot.Leaving = true;
                _ = slot.Fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard)).ContinueWith(_ =>
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => FinishLeave(slot, run)), TaskScheduler.Default);
            }
            else
            {
                slot.Fade.Jump(0);
                slot.Leaving = false;
                control.IsVisible = false;
            }
        }
        InvalidateMeasure();
    }

    private void FinishLeave(Slot slot, int run)
    {
        if (slot.Run != run || slot.Shown)
        {
            return;
        }
        slot.Leaving = false;
        if (slot.Control is { } control)
        {
            control.IsVisible = false;
        }
        InvalidateMeasure();
    }

    private static MotionValue<double> CreateFade(Control control)
    {
        var value = new MotionValue<double>(control, control.IsVisible ? 1 : 0, MotionVectors.Double);
        value.Changed += v => control.Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    private bool Present(int index) => _slots[index].Shown && _slots[index].Control is not null;

    private bool MetaPresent => Present(1) && !_tight;

    private bool ActionsPresent => Present(2);

    private bool NotePresent => Present(0);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        MarkPart?.Measure(new Size(Cell, Cell));
        StatusPart?.Measure(new Size(Cell, Cell));
        ChevronPart?.Measure(new Size(Cell, Cell));
        foreach (var slot in _slots)
        {
            if (slot.Control is { IsVisible: true } control && slot.Control != NotePart)
            {
                control.Measure(infinite);
            }
        }
        var aside = AsideWidth();
        var titleWidth = Math.Max(0, width - 2 * PaddingX - 3 * Cell - 4 * Gap - aside);
        TitlePart?.Measure(new Size(titleWidth, double.PositiveInfinity));
        var noteWidth = Math.Max(0, width - TitleLeft - PaddingX);
        NotePart?.Measure(new Size(noteWidth, double.PositiveInfinity));
        return new Size(width, Layout(width, arrange: false));
    }

    /// <summary>The width of the column between the title and the status: the buttons (wide) or the meta.</summary>
    private double AsideWidth()
    {
        if (ActionsPresent && !_compact)
        {
            return ActionsPart!.DesiredSize.Width;
        }
        if (MetaPresent)
        {
            return MetaPart!.DesiredSize.Width;
        }
        return 0;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width, arrange: true);
        return finalSize;
    }

    /// <summary>Places the parts (when <paramref name="arrange"/>) and returns the head's height.</summary>
    private double Layout(double width, bool arrange)
    {
        var note = NotePresent;
        var actions = ActionsPresent;
        double line;
        if (_compact)
        {
            line = note || actions ? CompactLineHeight : LineHeight;
        }
        else
        {
            line = actions ? ActionLineHeight : LineHeight;
        }
        var top = PaddingY;
        var center = top + line / 2;
        var right = width - PaddingX;
        var chevronX = right - Cell;
        var statusX = chevronX - Gap - Cell;
        var aside = AsideWidth();
        var asideRight = statusX - Gap;
        var titleRight = aside > 0 ? asideRight - aside - Gap : asideRight - Gap;
        var y = top + line;
        double noteHeight = 0;
        if (note)
        {
            noteHeight = _compact ? Math.Max(NoteLineHeight, NotePart!.DesiredSize.Height) : NoteLineHeight;
        }
        var height = y + (note ? noteHeight : 0);
        double actionsTop = 0;
        if (_compact && actions)
        {
            actionsTop = height + ActionsGap;
            height = actionsTop + ActionLineHeight;
        }
        else if (!note)
        {
            height += Strip;
        }
        height += PaddingY;
        if (!arrange)
        {
            return height;
        }
        Place(MarkPart, new Rect(PaddingX, center - Cell / 2, Cell, Cell));
        Place(StatusPart, new Rect(statusX, center - Cell / 2, Cell, Cell));
        Place(ChevronPart, new Rect(chevronX, center - Cell / 2, Cell, Cell));
        if (TitlePart is { } title)
        {
            var h = title.DesiredSize.Height;
            title.Arrange(new Rect(TitleLeft, center - h / 2, Math.Max(0, titleRight - TitleLeft), h));
        }
        // The slots: present ones are laid out; leaving ones stay where they were.
        PlaceSlot(0, note, new Rect(TitleLeft, y, Math.Max(0, right - TitleLeft), noteHeight));
        if (MetaPart is { } meta)
        {
            var size = meta.DesiredSize;
            PlaceSlot(1, MetaPresent, new Rect(asideRight - size.Width, center - size.Height / 2, size.Width, size.Height));
        }
        if (ActionsPart is { } buttons)
        {
            var size = buttons.DesiredSize;
            var rect = _compact
                ? new Rect(TitleLeft, actionsTop, size.Width, size.Height)
                : new Rect(asideRight - size.Width, center - size.Height / 2, size.Width, size.Height);
            PlaceSlot(2, actions, rect);
        }
        return height;
    }

    private void PlaceSlot(int index, bool present, Rect rect)
    {
        var slot = _slots[index];
        if (slot.Control is not { } control)
        {
            return;
        }
        if (present)
        {
            slot.Last = rect;
            control.Arrange(rect);
        }
        else if (slot.Leaving)
        {
            control.Arrange(slot.Last);
        }
        else
        {
            control.Arrange(default);
        }
    }

    private static void Place(Control? control, Rect rect) => control?.Arrange(rect);
}
