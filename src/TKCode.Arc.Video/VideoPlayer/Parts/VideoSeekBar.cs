using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The player's seek bar (spec sections 7 and 9): the whole-width track (white 0.24) with the buffered ranges (white 0.4)
/// and the played part (accent) up to the thumb, cut into chapter segments with a 2px gap. Values are seconds. While the
/// pointer is over it, <see cref="HoverChanged"/> reports the time under the pointer so the player can show its time tip.
/// With chapters, Page Up jumps to the previous chapter start (the current one when more than 1 s into it) and Page Down
/// to the next (spec section 9); without chapters they move a tenth of the length (APG seek slider, Page Up forward).
/// </summary>
internal sealed class VideoSeekBar : VideoRange
{
    /// <summary>The buffered ranges' brush.</summary>
    public static readonly StyledProperty<IBrush?> BufferedBrushProperty =
        AvaloniaProperty.Register<VideoSeekBar, IBrush?>(nameof(BufferedBrush));

    /// <summary>The gap cut between chapter segments (2).</summary>
    public static readonly StyledProperty<double> ChapterGapProperty =
        AvaloniaProperty.Register<VideoSeekBar, double>(nameof(ChapterGap), 2);

    private IReadOnlyList<VideoTimeRange> _buffered = [];
    private IReadOnlyList<TimeSpan> _chapters = [];

    static VideoSeekBar()
    {
        AffectsRender<VideoSeekBar>(BufferedBrushProperty, ChapterGapProperty);
    }

    /// <inheritdoc cref="BufferedBrushProperty"/>
    public IBrush? BufferedBrush { get => GetValue(BufferedBrushProperty); set => SetValue(BufferedBrushProperty, value); }

    /// <inheritdoc cref="ChapterGapProperty"/>
    public double ChapterGap { get => GetValue(ChapterGapProperty); set => SetValue(ChapterGapProperty, value); }

    /// <summary>The buffered ranges.</summary>
    public IReadOnlyList<VideoTimeRange> Buffered
    {
        get => _buffered;
        set
        {
            _buffered = value ?? [];
            InvalidateVisual();
        }
    }

    /// <summary>The chapter start times in order (the first chapter usually starts at zero).</summary>
    public IReadOnlyList<TimeSpan> Chapters
    {
        get => _chapters;
        set
        {
            _chapters = value ?? [];
            InvalidateVisual();
        }
    }

    /// <summary>Raised with the time under the pointer while it is over the bar, and null when it leaves.</summary>
    public event Action<double?>? HoverChanged;

    /// <summary>Where the thumb is now, in the bar's coordinates.</summary>
    public double ThumbOffset => ThumbX(Bounds.Width);

    /// <summary>The x position of <paramref name="seconds"/>.</summary>
    public double OffsetOf(double seconds) => Maximum > Minimum ? Math.Clamp((seconds - Minimum) / (Maximum - Minimum), 0, 1) * Bounds.Width : 0;

    protected override bool SnapsToStep => false;

    protected override double PageTarget(int direction)
    {
        if (_chapters.Count == 0)
        {
            return Value + direction * PageChange;
        }
        // Spec section 9: with chapters Page Up goes to the previous chapter and Page Down to the next.
        // Same rules as VideoChapters.NextStart / PreviousStart, on the chapter start times this bar already holds.
        var now = Value;
        if (direction < 0)
        {
            foreach (var start in _chapters)
            {
                if (start.TotalSeconds > now + 0.5)
                {
                    return start.TotalSeconds;
                }
            }
            return Maximum;
        }
        // Back: to the start of the current chapter, or the previous one when already near its start (as media keys do).
        var target = Minimum;
        foreach (var start in _chapters)
        {
            if (start.TotalSeconds < now - 1)
            {
                target = start.TotalSeconds;
            }
        }
        return target;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        HoverChanged?.Invoke(ValueAt(e.GetPosition(this).X));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsDragging)
        {
            HoverChanged?.Invoke(null);
        }
    }

    protected override void RenderTrack(DrawingContext context, Rect track, double thumbX)
    {
        var width = track.Width;
        var range = Maximum - Minimum;
        if (width <= 0)
        {
            return;
        }
        var corner = track.Height / 2;
        var gap = _chapters.Count > 0 ? ChapterGap : 0;
        // Segment edges in x: 0, each chapter start after 0, width.
        var start = 0.0;
        var index = 0;
        while (true)
        {
            var end = width;
            while (index < _chapters.Count)
            {
                var x = range > 0 ? (_chapters[index].TotalSeconds - Minimum) / range * width : 0;
                index++;
                if (x > start + gap && x < width - gap)
                {
                    end = x;
                    break;
                }
            }
            var left = start == 0 ? 0 : start + gap / 2;
            var right = end >= width ? width : end - gap / 2;
            if (right > left)
            {
                DrawSegment(context, new Rect(track.X + left, track.Y, right - left, track.Height), corner, thumbX, width, range);
            }
            if (end >= width)
            {
                break;
            }
            start = end;
        }
    }

    private void DrawSegment(DrawingContext context, Rect segment, double corner, double thumbX, double width, double range)
    {
        if (TrackBrush is { } trackBrush)
        {
            context.DrawRectangle(trackBrush, null, new RoundedRect(segment, corner));
        }
        if (BufferedBrush is { } bufferedBrush && range > 0)
        {
            foreach (var buffered in _buffered)
            {
                var from = Math.Max(segment.Left, (buffered.Start.TotalSeconds - Minimum) / range * width);
                var to = Math.Min(segment.Right, (buffered.End.TotalSeconds - Minimum) / range * width);
                if (to > from)
                {
                    context.DrawRectangle(bufferedBrush, null, new RoundedRect(new Rect(from, segment.Y, to - from, segment.Height), corner));
                }
            }
        }
        if (FillBrush is { } fillBrush && thumbX > segment.Left)
        {
            var right = Math.Min(segment.Right, thumbX);
            context.DrawRectangle(fillBrush, null, new RoundedRect(new Rect(segment.Left, segment.Y, right - segment.Left, segment.Height), corner));
        }
    }
}
