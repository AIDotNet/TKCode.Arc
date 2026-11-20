using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace StreamMark.Avalonia.Rendering;

/// <summary>
/// 文档末尾片段的逐字渐显，在渲染线程上完成：界面线程只在排版变化时计算一次「哪些字形、各批何时出现」，
/// 交给合成层的自定义视觉；之后每帧的不透明度变化由渲染线程绘制，界面线程不做任何工作。
/// </summary>
/// <remarks>
/// 渐显中的文字总是文本末尾的一段。所在的行视觉只绘制渐显起点左侧（矩形裁剪），右侧由渐显层按批次各自的不透明度绘制同一批字形，
/// 两者在同一个合成批次中提交，因此没有闪烁。渐显层只能绘制字形与矩形：含装饰线（链接下划线）或内嵌对象（公式、图片、复选框）
/// 的部分不参与渐显，直接显示。全部渐显结束后再恢复行视觉的完整绘制。
/// </remarks>
internal sealed class TailFade(TextSegmentView owner)
{
    // 高 TPS（如 300 TPS / 120Hz）下把同一时间窗内的批次合并成少量桶，限制每帧 Skia PushOpacity 离屏层数量
    private const int MaxFadeBuckets = 8;

    private readonly List<(long FromEnd, int Length, TimeSpan Time)> _batches = [];
    private readonly List<(long FromEnd, int Length, TimeSpan Time)> _merged = [];
    private readonly List<FadeRun> _runs = [];
    private readonly List<FadePiece> _pieces = [];
    private readonly List<SegmentLineVisual> _clipped = [];
    private readonly List<SegmentLineVisual> _nextClipped = [];
    private CompositionCustomVisual? _visual;
    private bool _hasFrame;
    private DispatcherTimer? _settleTimer;
    private TimeSpan _settleDeadline;

    /// <summary>按当前排版与显示记录更新渐显（排版或末尾状态变化后调用）。</summary>
    public void Update(SegmentLayout? layout, IReadOnlyList<SegmentLineVisual> lines, StreamingState? state, int textLength, double fontSize, Size bounds)
    {
        if (layout is null || state is null || !owner.IsDocumentTail || textLength == 0)
        {
            Clear();
            return;
        }
        var now = StreamClock.Now;
        state.GetFadingBatches(now, _batches);
        if (_batches.Count == 0 || EnsureVisual(bounds, fontSize) is not { } visual)
        {
            Clear();
            return;
        }

        CoalesceBatches(_batches, state.FadeDuration, _merged);
        var fadeStart = (int)Math.Clamp(textLength - _merged[0].FromEnd, 0, textLength);
        fadeStart = SkipUnfadeable(layout, fadeStart, textLength);
        if (fadeStart >= textLength)
        {
            Clear();
            return;
        }

        // 渐显层要画的字形：渐显范围内各行的文字段
        var firstLine = layout.LineIndexOf(fadeStart);
        _runs.Clear();
        for (var i = firstLine; i < layout.LineCount; i++)
        {
            if (layout.IsEmptyLine(i))
            {
                continue;
            }
            var line = layout.LineAt(i);
            var position = line.FirstTextSourceIndex;
            var x = line.Start;
            foreach (var run in line.TextRuns)
            {
                var end = position + run.Length;
                if (run is ShapedTextRun shaped && end > fadeStart && shaped.Properties.ForegroundBrush is { } foreground)
                {
                    var origin = new Point(x, layout.LineTop(i) + BaselineOffset(line, shaped));
                    if (shaped.GlyphRun.TryCreateImmutableGlyphRunReference() is { } reference)
                    {
                        var glyphBounds = shaped.GlyphRun.Bounds;
                        var immFg = foreground.ToImmutable();
                        var immBg = shaped.Properties.BackgroundBrush?.ToImmutable();
                        var fgColor = TrySolidColor(immFg);
                        _runs.Add(new FadeRun(reference, immFg, fgColor, immBg, glyphBounds,
                            origin, i, glyphBounds.Translate(origin)));
                    }
                }
                if (run is DrawableTextRun drawable)
                {
                    x += drawable.Size.Width;
                }
                position = end;
            }
        }

        // 每批文字所在的矩形（按行），最后一段向右延伸，容纳字形的右侧溢出
        _pieces.Clear();
        TimeSpan newest = default;
        foreach (var (fromEnd, length, time) in _merged)
        {
            var start = (int)Math.Clamp(textLength - fromEnd, 0, textLength);
            var end = Math.Min(textLength, start + length);
            start = Math.Max(start, fadeStart);
            newest = time;
            if (end <= start)
            {
                continue;
            }
            foreach (var rect in layout.HitTestTextRange(start, end - start))
            {
                var line = layout.LineIndexAtY(rect.Center.Y);
                // 竖直方向放宽到整行上下（只绘制同一行的字形，不会画到相邻行）
                _pieces.Add(new FadePiece(new Rect(rect.X, rect.Y - rect.Height, rect.Width, rect.Height * 3), time, line));
            }
        }
        if (_pieces.Count == 0 || _runs.Count == 0)
        {
            foreach (var run in _runs)
            {
                run.Reference.Dispose();
            }
            _runs.Clear();
            Clear();
            return;
        }
        for (var i = 0; i < _pieces.Count; i++)
        {
            if (i == _pieces.Count - 1 || _pieces[i + 1].Line != _pieces[i].Line)
            {
                var clip = _pieces[i].Clip;
                _pieces[i] = _pieces[i] with { Clip = clip.WithWidth(clip.Width + fontSize * 2) };
            }
        }

        // 行视觉：渐显起点所在行只画起点左侧，之后的行全部交给渐显层
        _nextClipped.Clear();
        for (var i = firstLine; i < layout.LineCount && i < lines.Count; i++)
        {
            if (layout.IsEmptyLine(i))
            {
                continue;
            }
            var line = layout.LineAt(i);
            lines[i].ClipRight = i == firstLine ? line.GetDistanceFromCharacterHit(new CharacterHit(fadeStart)) : line.Start;
            _nextClipped.Add(lines[i]);
        }
        foreach (var line in _clipped)
        {
            if (!_nextClipped.Contains(line))
            {
                line.ClipRight = null;
            }
        }
        _clipped.Clear();
        _clipped.AddRange(_nextClipped);

        var area = _pieces[0].Clip;
        for (var i = 1; i < _pieces.Count; i++)
        {
            area = area.Union(_pieces[i].Clip);
        }
        visual.SendHandlerMessage(new FadeFrame(_runs.ToArray(), _pieces.ToArray(), newest, state.FadeDuration, area));
        _runs.Clear();
        _pieces.Clear();
        _hasFrame = true;
        ScheduleSettle(newest + state.FadeDuration, now);
    }

    private static void CoalesceBatches(List<(long FromEnd, int Length, TimeSpan Time)> source, TimeSpan fadeDuration,
        List<(long FromEnd, int Length, TimeSpan Time)> output)
    {
        output.Clear();
        if (source.Count <= MaxFadeBuckets)
        {
            output.AddRange(source);
            return;
        }
        var bucketTicks = Math.Max(TimeSpan.FromMilliseconds(24).Ticks, fadeDuration.Ticks / MaxFadeBuckets);
        var current = source[0];
        for (var i = 1; i < source.Count; i++)
        {
            var next = source[i];
            // 仅合并紧邻且时间差在同一个桶内的批次，保持各行裁剪区间连续且每帧 SaveLayer 数量受控
            if (current.FromEnd - current.Length == next.FromEnd && (next.Time - current.Time).Ticks < bucketTicks)
            {
                current = (current.FromEnd, current.Length + next.Length, next.Time);
            }
            else
            {
                output.Add(current);
                current = next;
            }
        }
        output.Add(current);
    }

    /// <summary>结束渐显：行视觉恢复完整绘制，渐显层清空。</summary>
    public void Clear()
    {
        _settleDeadline = default;
        _settleTimer?.Stop();
        foreach (var line in _clipped)
        {
            line.ClipRight = null;
        }
        _clipped.Clear();
        if (_hasFrame)
        {
            _visual?.SendHandlerMessage(FadeHandler.ClearMessage);
            _hasFrame = false;
        }
    }

    /// <summary>离开可视树：合成视觉随之失效，下次挂载时重建。</summary>
    public void Detach()
    {
        Clear();
        if (_visual is not null)
        {
            ElementComposition.SetElementChildVisual(owner, null);
            _visual = null;
        }
    }

    private CompositionCustomVisual? EnsureVisual(Size bounds, double fontSize)
    {
        if (_visual is null)
        {
            if (ElementComposition.GetElementVisual(owner)?.Compositor is not { } compositor)
            {
                return null;
            }
            _visual = compositor.CreateCustomVisual(new FadeHandler());
            ElementComposition.SetElementChildVisual(owner, _visual);
        }
        // 光标与字形可能略超出片段右侧
        _visual.Size = new Vector(bounds.Width + fontSize * 4, bounds.Height + fontSize);
        return _visual;
    }

    // 渐显起点之后若有不能由渐显层绘制的内容（内嵌对象、带装饰线的文字），起点移到其后
    private static int SkipUnfadeable(SegmentLayout layout, int fadeStart, int textLength)
    {
        for (var i = layout.LineIndexOf(fadeStart); i < layout.LineCount; i++)
        {
            if (layout.IsEmptyLine(i))
            {
                continue;
            }
            var line = layout.LineAt(i);
            var position = line.FirstTextSourceIndex;
            foreach (var run in line.TextRuns)
            {
                var end = position + run.Length;
                if (end > fadeStart && !Fadeable(run))
                {
                    fadeStart = Math.Min(textLength, end);
                }
                position = end;
            }
        }
        return fadeStart;
    }

    private static bool Fadeable(TextRun run) => run switch
    {
        ShapedTextRun shaped => shaped.Properties.TextDecorations is not { Count: > 0 } && (shaped.BidiLevel & 1) == 0,
        DrawableTextRun drawable => drawable.Size.Width <= 0,
        _ => true,
    };

    // 与 TextLine 绘制各段时的基线偏移一致
    private static double BaselineOffset(TextLine line, DrawableTextRun run)
    {
        var baseline = run.Baseline;
        var offset = -baseline;
        switch (run.Properties?.BaselineAlignment ?? BaselineAlignment.Baseline)
        {
            case BaselineAlignment.Top:
            case BaselineAlignment.TextTop:
                offset += line.Height - line.Extent + run.Size.Height / 2;
                break;
            case BaselineAlignment.Center:
                offset += line.Height / 2 + baseline - run.Size.Height / 2;
                break;
            case BaselineAlignment.Subscript:
            case BaselineAlignment.Bottom:
            case BaselineAlignment.TextBottom:
                offset += line.Height - run.Size.Height + baseline;
                break;
            case BaselineAlignment.Superscript:
                offset += baseline;
                break;
            default:
                offset += line.Baseline;
                break;
        }
        return offset;
    }

    private static Color? TrySolidColor(IImmutableBrush brush)
    {
        if (brush is ISolidColorBrush solid)
        {
            var alpha = (byte)Math.Clamp(Math.Round(solid.Color.A * solid.Opacity), 0, 255);
            return Color.FromArgb(alpha, solid.Color.R, solid.Color.G, solid.Color.B);
        }
        return null;
    }

    private void ScheduleSettle(TimeSpan deadline, TimeSpan now)
    {
        _settleDeadline = deadline;
        // 定时器已在跑时只推后截止时间，避免 300 TPS / 120Hz 流式期间每帧 Stop/Start 重注册系统定时器
        if (_settleTimer is { IsEnabled: true })
        {
            return;
        }
        var delay = deadline - now;
        var interval = delay < TimeSpan.Zero ? TimeSpan.FromMilliseconds(16) : delay + TimeSpan.FromMilliseconds(16);
        if (_settleTimer is null)
        {
            _settleTimer = new DispatcherTimer(interval, DispatcherPriority.Normal, (_, _) =>
            {
                var remaining = _settleDeadline - StreamClock.Now;
                if (remaining > TimeSpan.FromMilliseconds(4))
                {
                    _settleTimer!.Interval = remaining + TimeSpan.FromMilliseconds(16);
                    return;
                }
                _settleTimer?.Stop();
                owner.RefreshFade();
            });
            _settleTimer.Start();
            return;
        }
        _settleTimer.Interval = interval;
        _settleTimer.Start();
    }

    private sealed record FadeRun(IImmutableGlyphRunReference Reference, IImmutableBrush Foreground, Color? ForegroundColor, IImmutableBrush? Background, Rect GlyphBounds,
        Point Origin, int Line, Rect Bounds);

    private readonly record struct FadePiece(Rect Clip, TimeSpan Time, int Line);

    private sealed record FadeFrame(FadeRun[] Runs, FadePiece[] Pieces, TimeSpan Newest, TimeSpan Duration, Rect Area) : IDisposable
    {
        public void Dispose()
        {
            foreach (var run in Runs)
            {
                run.Reference.Dispose();
            }
        }
    }

    /// <summary>渲染线程：按各批出现的时间绘制渐显中的字形，渐显期间每帧只重绘这一小块区域。</summary>
    private sealed class FadeHandler : CompositionCustomVisualHandler
    {
        public static readonly object ClearMessage = new();

        private FadeFrame? _frame;
        private readonly Dictionary<uint, IImmutableSolidColorBrush> _alphaBrushes = [];

        public override void OnMessage(object message)
        {
            var previous = _frame;
            _frame = message as FadeFrame;
            Invalidate();
            previous?.Dispose();
            if (_frame is not null)
            {
                RegisterForNextAnimationFrameUpdate();
            }
        }

        public override void OnAnimationFrameUpdate()
        {
            if (_frame is not { } frame)
            {
                return;
            }
            Invalidate(frame.Area);
            if (StreamClock.Now - frame.Newest < frame.Duration)
            {
                RegisterForNextAnimationFrameUpdate();
            }
        }

        public override void OnRender(ImmediateDrawingContext context)
        {
            if (_frame is not { } frame)
            {
                return;
            }
            var now = StreamClock.Now;
            var duration = frame.Duration.TotalMilliseconds;
            foreach (var piece in frame.Pieces)
            {
                var opacity = duration <= 0 ? 1 : Math.Clamp((now - piece.Time).TotalMilliseconds / duration, 0, 1);
                if (opacity <= 0.004)
                {
                    continue;
                }
                // 纯文字（无底色、纯色画刷）直接把透明度乘进前景色 Alpha，跳过 PushOpacity 离屏 SaveLayer（300 TPS 下大幅降 GPU 开销）
                var needsLayer = false;
                foreach (var run in frame.Runs)
                {
                    if (run.Line != piece.Line || !run.Bounds.Intersects(piece.Clip))
                    {
                        continue;
                    }
                    if (run.Background is not null || run.ForegroundColor is null)
                    {
                        needsLayer = true;
                        break;
                    }
                }
                using (context.PushClip(piece.Clip))
                {
                    if (needsLayer)
                    {
                        using (context.PushOpacity(opacity, piece.Clip))
                        {
                            foreach (var run in frame.Runs)
                            {
                                if (run.Line != piece.Line || !run.Bounds.Intersects(piece.Clip))
                                {
                                    continue;
                                }
                                using (context.PushPreTransform(Matrix.CreateTranslation(run.Origin)))
                                {
                                    if (run.Background is { } background)
                                    {
                                        context.FillRectangle(background, run.GlyphBounds);
                                    }
                                    context.DrawGlyphRun(run.Foreground, run.Reference);
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (var run in frame.Runs)
                        {
                            if (run.Line != piece.Line || !run.Bounds.Intersects(piece.Clip))
                            {
                                continue;
                            }
                            var brush = BrushWithOpacity(run.ForegroundColor!.Value, opacity);
                            using (context.PushPreTransform(Matrix.CreateTranslation(run.Origin)))
                            {
                                context.DrawGlyphRun(brush, run.Reference);
                            }
                        }
                    }
                }
            }
        }

        private IImmutableSolidColorBrush BrushWithOpacity(Color baseColor, double opacity)
        {
            var alpha = (byte)Math.Clamp(Math.Round(baseColor.A * opacity), 0, 255);
            var key = ((uint)alpha << 24) | ((uint)baseColor.R << 16) | ((uint)baseColor.G << 8) | baseColor.B;
            if (!_alphaBrushes.TryGetValue(key, out var brush))
            {
                if (_alphaBrushes.Count >= 512)
                {
                    _alphaBrushes.Clear();
                }
                brush = new ImmutableSolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
                _alphaBrushes[key] = brush;
            }
            return brush;
        }
    }
}
