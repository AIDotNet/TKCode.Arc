using Avalonia;
using Avalonia.Media;
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
    private readonly List<(long FromEnd, int Length, TimeSpan Time)> _batches = [];
    private readonly List<SegmentLineVisual> _clipped = [];
    private CompositionCustomVisual? _visual;
    private bool _hasFrame;
    private IDisposable? _settle;

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

        var fadeStart = (int)Math.Clamp(textLength - _batches[0].FromEnd, 0, textLength);
        fadeStart = SkipUnfadeable(layout, fadeStart, textLength);
        if (fadeStart >= textLength)
        {
            Clear();
            return;
        }

        // 渐显层要画的字形：渐显范围内各行的文字段
        var firstLine = layout.LineIndexOf(fadeStart);
        var runs = new List<FadeRun>();
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
                        runs.Add(new FadeRun(reference, foreground.ToImmutable(), shaped.Properties.BackgroundBrush?.ToImmutable(), glyphBounds,
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
        var pieces = new List<FadePiece>();
        TimeSpan newest = default;
        foreach (var (fromEnd, length, time) in _batches)
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
                var line = layout.LineIndexOf(Math.Clamp(layout.HitTestPoint(rect.Center).TextPosition, 0, textLength));
                // 竖直方向放宽到整行上下（只绘制同一行的字形，不会画到相邻行）
                pieces.Add(new FadePiece(new Rect(rect.X, rect.Y - rect.Height, rect.Width, rect.Height * 3), time, line));
            }
        }
        if (pieces.Count == 0 || runs.Count == 0)
        {
            foreach (var run in runs)
            {
                run.Reference.Dispose();
            }
            Clear();
            return;
        }
        for (var i = 0; i < pieces.Count; i++)
        {
            if (i == pieces.Count - 1 || pieces[i + 1].Line != pieces[i].Line)
            {
                var clip = pieces[i].Clip;
                pieces[i] = pieces[i] with { Clip = clip.WithWidth(clip.Width + fontSize * 2) };
            }
        }

        // 行视觉：渐显起点所在行只画起点左侧，之后的行全部交给渐显层
        var clipped = new List<SegmentLineVisual>();
        for (var i = firstLine; i < layout.LineCount && i < lines.Count; i++)
        {
            if (layout.IsEmptyLine(i))
            {
                continue;
            }
            var line = layout.LineAt(i);
            lines[i].ClipRight = i == firstLine ? line.GetDistanceFromCharacterHit(new CharacterHit(fadeStart)) : line.Start;
            clipped.Add(lines[i]);
        }
        foreach (var line in _clipped)
        {
            if (!clipped.Contains(line))
            {
                line.ClipRight = null;
            }
        }
        _clipped.Clear();
        _clipped.AddRange(clipped);

        var area = pieces.Aggregate(pieces[0].Clip, (a, p) => a.Union(p.Clip));
        visual.SendHandlerMessage(new FadeFrame(runs.ToArray(), pieces.ToArray(), newest, state.FadeDuration, area));
        _hasFrame = true;
        ScheduleSettle(newest + state.FadeDuration - now);
    }

    /// <summary>结束渐显：行视觉恢复完整绘制，渐显层清空。</summary>
    public void Clear()
    {
        _settle?.Dispose();
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

    private void ScheduleSettle(TimeSpan delay)
    {
        _settle?.Dispose();
        _settle = DispatcherTimer.RunOnce(owner.RefreshFade, delay < TimeSpan.Zero ? TimeSpan.Zero : delay + TimeSpan.FromMilliseconds(16));
    }

    private sealed record FadeRun(IImmutableGlyphRunReference Reference, IImmutableBrush Foreground, IImmutableBrush? Background, Rect GlyphBounds,
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
                using (context.PushClip(piece.Clip))
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
        }
    }
}
