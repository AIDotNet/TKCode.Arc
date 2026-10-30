using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The content area of a <see cref="SettingsPage"/>. Two pooled presenters take turns: the outgoing section stays where it
/// was and fades out (fast); once it is gone the incoming one rises 6px and fades in (standard, enter easing). Coming back to
/// the section that is still leaving swaps the two presenters' roles, so a section control is never in two places.
/// Reduced motion crossfades in place; Off switches at once. Only the incoming section takes part in layout.
/// Code-driven because the section controls are reused across swaps (a control has one visual parent), which a
/// ContentControl transition or ArcContentSwap's fresh layer per change cannot guarantee.
/// </summary>
internal sealed class SettingsPageContentHost : Control
{
    private const double Rise = 6;

    private sealed class Slot
    {
        public required ContentPresenter Presenter { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? Progress { get; set; }
        public int Generation { get; set; }
    }

    private readonly Slot _a;
    private readonly Slot _b;
    private Slot _current;
    private Slot? _exiting;

    public SettingsPageContentHost()
    {
        _a = CreateSlot();
        _b = CreateSlot();
        _current = _a;
    }

    /// <summary>The section being shown.</summary>
    public object? Current => _current.Presenter.Content;

    /// <summary>The section that is still fading out, if any (tests).</summary>
    internal object? Exiting => _exiting?.Presenter.Content;

    private Slot CreateSlot()
    {
        var translate = new TranslateTransform();
        var presenter = new ContentPresenter { RenderTransform = translate, IsVisible = false };
        LogicalChildren.Add(presenter);
        VisualChildren.Add(presenter);
        return new Slot { Presenter = presenter, Translate = translate };
    }

    /// <summary>Shows <paramref name="content"/>, crossfading from the current section when <paramref name="animate"/>.</summary>
    public void Show(object? content, bool animate)
    {
        if (ReferenceEquals(_current.Presenter.Content, content))
        {
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level == ArcMotionLevel.Off || !IsEffectivelyVisible || _current.Presenter.Content is null || content is null)
        {
            FinishExit();
            Place(_current, content);
            Settle(_current);
            InvalidateMeasure();
            return;
        }

        if (_exiting is not null && ReferenceEquals(_exiting.Presenter.Content, content))
        {
            (_current, _exiting) = (_exiting, _current);
        }
        else
        {
            FinishExit();
            var next = ReferenceEquals(_current, _a) ? _b : _a;
            _exiting = _current;
            _current = next;
            Place(_current, content);
        }

        var full = level == ArcMotionLevel.Full;
        Enter(_current, full);
        Exit(_exiting!, full);
        InvalidateMeasure();
    }

    private static void Place(Slot slot, object? content)
    {
        slot.Generation++;
        slot.Progress?.Stop();
        // Detach first: the section may only have one visual parent.
        slot.Presenter.Content = null;
        slot.Presenter.Content = content;
        slot.Presenter.IsVisible = content is not null;
    }

    private static void Settle(Slot slot)
    {
        slot.Progress?.Stop();
        slot.Presenter.Opacity = 1;
        slot.Translate.Y = 0;
    }

    private async void Enter(Slot slot, bool full)
    {
        var generation = ++slot.Generation;
        slot.Presenter.IsVisible = true;
        slot.Presenter.IsHitTestVisible = true;
        var from = slot.Presenter.Opacity < 1 ? slot.Presenter.Opacity : 0;
        slot.Presenter.Opacity = from;
        slot.Translate.Y = full ? Rise * (1 - from) : 0;
        slot.Progress ??= CreateProgress(slot);
        slot.Progress.Jump(from);
        // The two sections' text must never be on screen together (it reads as overlapping glyphs): a section that
        // starts from nothing waits for the one leaving to finish fading out.
        if (from == 0 && _exiting is not null)
        {
            await Task.Delay(full ? ArcMotionPresets.Fast : TimeSpan.FromMilliseconds(100));
            if (slot.Generation != generation)
            {
                return;
            }
        }
        var spec = full
            ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
            : MotionSpec.Tween(TimeSpan.FromMilliseconds(120), ArcEasings.Standard);
        await slot.Progress.AnimateTo(1, spec);
        if (slot.Generation == generation)
        {
            Settle(slot);
        }
    }

    private async void Exit(Slot slot, bool full)
    {
        var generation = ++slot.Generation;
        slot.Presenter.IsHitTestVisible = false;
        slot.Progress ??= CreateProgress(slot);
        slot.Progress.Jump(slot.Presenter.Opacity);
        slot.Translate.Y = 0;
        var spec = MotionSpec.Tween(full ? ArcMotionPresets.Fast : TimeSpan.FromMilliseconds(100), ArcEasings.Exit);
        await slot.Progress.AnimateTo(0, spec);
        if (slot.Generation == generation && ReferenceEquals(slot, _exiting))
        {
            FinishExit();
        }
    }

    private MotionValue<double> CreateProgress(Slot slot)
    {
        var progress = new MotionValue<double>(this, 1, MotionVectors.Double);
        progress.Changed += value =>
        {
            slot.Presenter.Opacity = value;
            // Only the entering section rises; the leaving one stays pinned.
            if (!ReferenceEquals(slot, _exiting) && ArcMotion.IsFull(this))
            {
                slot.Translate.Y = Rise * (1 - value);
            }
        };
        return progress;
    }

    private void FinishExit()
    {
        if (_exiting is null)
        {
            return;
        }
        var slot = _exiting;
        _exiting = null;
        slot.Generation++;
        slot.Progress?.Stop();
        slot.Presenter.Content = null;
        slot.Presenter.IsVisible = false;
        slot.Presenter.IsHitTestVisible = true;
        Settle(slot);
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _a.Presenter.Measure(availableSize);
        _b.Presenter.Measure(availableSize);
        return _current.Presenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _current.Presenter.Arrange(new Rect(finalSize));
        if (_exiting is not null)
        {
            // The leaving section keeps its own height and is drawn over the top of the area.
            _exiting.Presenter.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, _exiting.Presenter.DesiredSize.Height)));
        }
        return finalSize;
    }
}
