using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A delete transition around a list or grid. <see cref="SwallowAsync"/> pulls the chosen items into a tiny black hole
/// along a logarithmic spiral while the rest of the content bends around it (gravitational lensing), then the hole
/// collapses with one soft shockwave; <see cref="ReleaseAsync"/> plays the same path backwards to put items back.
/// Calls queue, so a release asked for during a swallow waits its turn. The effect paints a snapshot of the content
/// while it runs; the real content is back the moment it ends. With reduced motion the items fade in 200 ms instead.
/// </summary>
[TemplatePart(ContentPresenterPart, typeof(ContentPresenter))]
[TemplatePart(StagePart, typeof(BlackHoleStage))]
public class BlackHole : ContentControl
{
    internal const string ContentPresenterPart = "PART_ContentPresenter";
    internal const string StagePart = "PART_Stage";

    /// <summary>The fade used instead of the effect under reduced motion.</summary>
    internal static readonly TimeSpan ReducedFade = TimeSpan.FromMilliseconds(200);

    /// <summary>How long a release waits at most for the restored items to stop moving (the list making room).</summary>
    internal static readonly TimeSpan SettleTimeout = TimeSpan.FromMilliseconds(1500);

    /// <summary>Snapshots are taken at the screen's scaling, capped at 2.</summary>
    internal const double MaxScale = 2;

    /// <summary>Length of a swallow (default 1.3 s, as measured); a release runs <see cref="ReleaseSpeed"/> times as long.</summary>
    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<BlackHole, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(1.3));

    /// <summary>Length of a release as a fraction of <see cref="Duration"/> (default 0.92: the reverse runs a little faster).</summary>
    public static readonly StyledProperty<double> ReleaseSpeedProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(ReleaseSpeed), 0.92);

    /// <summary>Resting radius of the black core in DIP (default 5.5; it grows 40 % while it eats). The lens scales with it.</summary>
    public static readonly StyledProperty<double> HoleRadiusProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(HoleRadius), 5.5);

    /// <summary>How far the shockwave ring travels after the hole collapses, in DIP (default 90).</summary>
    public static readonly StyledProperty<double> ShockwaveRadiusProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(ShockwaveRadius), 90);

    /// <summary>True while a transition runs (or a reduced-motion fade) or another one is queued. Ignore your delete and undo triggers while it is set.</summary>
    public static readonly DirectProperty<BlackHole, bool> IsRunningProperty =
        AvaloniaProperty.RegisterDirect<BlackHole, bool>(nameof(IsRunning), o => o.IsRunning);

    internal static readonly StyledProperty<double> CoreGrowthProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(CoreGrowth), 0.4);

    internal static readonly StyledProperty<double> EinsteinRatioProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(EinsteinRatio), 4);

    internal static readonly StyledProperty<double> HaloDecayProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(HaloDecay), 10.5);

    internal static readonly StyledProperty<double> HaloOpacityProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(HaloOpacity), 0.16);

    internal static readonly StyledProperty<double> PhotonRingWidthProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(PhotonRingWidth), 2);

    internal static readonly StyledProperty<IBrush?> PhotonRingBrushProperty =
        AvaloniaProperty.Register<BlackHole, IBrush?>(nameof(PhotonRingBrush));

    internal static readonly StyledProperty<double> ShockWidthProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(ShockWidth), 6);

    internal static readonly StyledProperty<double> ShockOpacityProperty =
        AvaloniaProperty.Register<BlackHole, double>(nameof(ShockOpacity), 0.6);

    internal static readonly StyledProperty<IBrush?> ShockBrushProperty =
        AvaloniaProperty.Register<BlackHole, IBrush?>(nameof(ShockBrush));

    private readonly BlackHoleQueue _queue = new();
    private readonly ConditionalWeakTable<Control, StrongBox<double>> _hidden = new();
    private ContentPresenter? _presenter;
    private BlackHoleStage? _stage;
    private bool _isRunning;
    private Action? _abort;

    /// <summary>Length of a swallow (default 1.3 s, as measured); a release runs <see cref="ReleaseSpeed"/> times as long.</summary>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>Length of a release as a fraction of <see cref="Duration"/> (default 0.92: the reverse runs a little faster).</summary>
    public double ReleaseSpeed
    {
        get => GetValue(ReleaseSpeedProperty);
        set => SetValue(ReleaseSpeedProperty, value);
    }

    /// <summary>Resting radius of the black core in DIP (default 5.5; it grows 40 % while it eats). The lens scales with it.</summary>
    public double HoleRadius
    {
        get => GetValue(HoleRadiusProperty);
        set => SetValue(HoleRadiusProperty, value);
    }

    /// <summary>How far the shockwave ring travels after the hole collapses, in DIP (default 90).</summary>
    public double ShockwaveRadius
    {
        get => GetValue(ShockwaveRadiusProperty);
        set => SetValue(ShockwaveRadiusProperty, value);
    }

    /// <summary>True while a transition runs (or a reduced-motion fade) or another one is queued. Ignore your delete and undo triggers while it is set.</summary>
    public bool IsRunning
    {
        get => _isRunning;
        private set => SetAndRaise(IsRunningProperty, ref _isRunning, value);
    }

    internal double CoreGrowth { get => GetValue(CoreGrowthProperty); set => SetValue(CoreGrowthProperty, value); }

    internal double EinsteinRatio { get => GetValue(EinsteinRatioProperty); set => SetValue(EinsteinRatioProperty, value); }

    internal double HaloDecay { get => GetValue(HaloDecayProperty); set => SetValue(HaloDecayProperty, value); }

    internal double HaloOpacity { get => GetValue(HaloOpacityProperty); set => SetValue(HaloOpacityProperty, value); }

    internal double PhotonRingWidth { get => GetValue(PhotonRingWidthProperty); set => SetValue(PhotonRingWidthProperty, value); }

    /// <summary>The photon ring's color (a solid brush from the token).</summary>
    internal IBrush? PhotonRingBrush { get => GetValue(PhotonRingBrushProperty); set => SetValue(PhotonRingBrushProperty, value); }

    internal double ShockWidth { get => GetValue(ShockWidthProperty); set => SetValue(ShockWidthProperty, value); }

    internal double ShockOpacity { get => GetValue(ShockOpacityProperty); set => SetValue(ShockOpacityProperty, value); }

    /// <summary>The shockwave's color (a solid brush from the token).</summary>
    internal IBrush? ShockBrush { get => GetValue(ShockBrushProperty); set => SetValue(ShockBrushProperty, value); }

    private static Color ColorOf(IBrush? brush, Color fallback) => brush is ISolidColorBrush solid
        ? Color.FromArgb((byte)Math.Round(solid.Color.A * Math.Clamp(solid.Opacity, 0, 1)), solid.Color.R, solid.Color.G, solid.Color.B)
        : fallback;

    /// <summary>Raised when a queued transition starts.</summary>
    public event EventHandler<BlackHoleTransitionEventArgs>? TransitionStarted;

    /// <summary>Raised when a transition has finished and the content is back in place.</summary>
    public event EventHandler<BlackHoleTransitionEventArgs>? TransitionCompleted;

    /// <summary>The renderer of the run in progress (tests and diagnostics).</summary>
    internal BlackHoleRenderer? Renderer { get; private set; }

    /// <summary>The frame worker of the run in progress (tests).</summary>
    internal BlackHoleFrameWorker? Worker { get; private set; }

    /// <summary>Elapsed time of the run in progress (ms).</summary>
    internal double Elapsed { get; private set; }

    /// <summary>Transitions queued or running.</summary>
    internal int PendingTransitions => _queue.Pending;

    internal BlackHoleStage? Stage => _stage;

    internal ContentPresenter? ContentHost => _presenter;

    /// <summary>
    /// Pulls <paramref name="targets"/> (controls inside the content) into the hole. They stay hidden afterwards: remove
    /// them from your data when the task completes, so the list closes the gap.
    /// </summary>
    /// <param name="targets">The items to swallow.</param>
    /// <param name="options">Origin and duration of this call.</param>
    public Task SwallowAsync(IReadOnlyList<Control> targets, BlackHoleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var list = targets.ToArray();
        return _queue.Enqueue(() => RunAsync(BlackHoleDirection.Swallow, list, options));
    }

    /// <summary>
    /// The reverse of <see cref="SwallowAsync"/>. Call it right after the items are back in the content: they are hidden
    /// at once, the black hole waits until they stop moving (the list making room), then they spiral out into place.
    /// </summary>
    /// <param name="targets">The restored items.</param>
    /// <param name="options">Origin and duration of this call.</param>
    public Task ReleaseAsync(IReadOnlyList<Control> targets, BlackHoleOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var list = targets.ToArray();
        // Hidden at once, before the first frame shows them (Arc asks for a layout effect for the same reason).
        foreach (var target in list)
        {
            Hide(target);
        }
        return _queue.Enqueue(() => RunAsync(BlackHoleDirection.Release, list, options));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _abort?.Invoke();
        _presenter = e.NameScope.Find<ContentPresenter>(ContentPresenterPart);
        _stage = e.NameScope.Find<BlackHoleStage>(StagePart);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Nothing to paint into any more: the run in progress jumps to its end.
        _abort?.Invoke();
    }

    private async Task RunAsync(BlackHoleDirection direction, Control[] targets, BlackHoleOptions? options)
    {
        var presenter = _presenter;
        var live = targets.Where(t => presenter is not null && t.IsAttachedToVisualTree() && presenter.IsVisualAncestorOf(t)).ToArray();
        if (live.Length == 0)
        {
            if (direction == BlackHoleDirection.Release)
            {
                foreach (var target in targets)
                {
                    Show(target);
                }
            }
            return;
        }
        var args = new BlackHoleTransitionEventArgs(direction, live.Length);
        IsRunning = true;
        TransitionStarted?.Invoke(this, args);
        try
        {
            var duration = options?.Duration ?? Duration;
            if (duration <= TimeSpan.Zero)
            {
                duration = TimeSpan.FromMilliseconds(1);
            }
            var level = ArcMotion.GetEffectiveLevel(this);
            if (direction == BlackHoleDirection.Swallow)
            {
                if (level == ArcMotionLevel.Full && TryPrepare(direction, live, options) is { } run)
                {
                    await PlayAsync(run, duration.TotalMilliseconds);
                }
                else if (level == ArcMotionLevel.Reduced)
                {
                    await FadeAsync(live, 1, 0);
                }
                foreach (var target in live)
                {
                    Hide(target);
                }
            }
            else
            {
                foreach (var target in live)
                {
                    Hide(target);
                }
                if (level != ArcMotionLevel.Off)
                {
                    await SettleAsync(live);
                }
                if (level == ArcMotionLevel.Full && TryPrepare(direction, live, options) is { } run)
                {
                    await PlayAsync(run, duration.TotalMilliseconds * Math.Max(0.05, ReleaseSpeed));
                }
                else if (level == ArcMotionLevel.Reduced)
                {
                    await FadeAsync(live, 0, 1);
                }
                foreach (var target in live)
                {
                    Show(target);
                }
            }
        }
        finally
        {
            IsRunning = _queue.Pending > 1;
            TransitionCompleted?.Invoke(this, args);
        }
    }

    // ------------------------------------------------------------------ hiding

    private void Hide(Control target)
    {
        if (!_hidden.TryGetValue(target, out _))
        {
            _hidden.Add(target, new StrongBox<double>(target.Opacity > 0 ? target.Opacity : 1));
        }
        target.Opacity = 0;
    }

    private void Show(Control target)
    {
        if (_hidden.TryGetValue(target, out var original))
        {
            target.Opacity = original.Value;
            _hidden.Remove(target);
        }
        else
        {
            target.Opacity = 1;
        }
    }

    private double OriginalOpacity(Control target) => _hidden.TryGetValue(target, out var original) ? original.Value : target.Opacity;

    // ------------------------------------------------------------------ the effect

    /// <summary>A prepared run: the renderer with its snapshots, and which way it plays.</summary>
    private sealed record Prepared(BlackHoleRenderer Renderer, BlackHoleDirection Direction, Rect Target);

    /// <summary>Snapshots the content (rest and items), builds the mesh and shows the stage; null when that is not possible.</summary>
    private Prepared? TryPrepare(BlackHoleDirection direction, Control[] targets, BlackHoleOptions? options)
    {
        if (_presenter is not { } presenter || _stage is not { } stage || presenter.Bounds.Width < 1 || presenter.Bounds.Height < 1)
        {
            return null;
        }
        var top = TopLevel.GetTopLevel(this);
        var scale = Math.Min(MaxScale, Math.Max(1, top?.RenderScaling ?? 1));
        var size = presenter.Bounds.Size;
        var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scale)), Math.Max(1, (int)Math.Ceiling(size.Height * scale)));
        var rects = new List<Rect>(targets.Length);
        foreach (var target in targets)
        {
            if (target.TransformToVisual(presenter) is { } m)
            {
                rects.Add(new Rect(target.Bounds.Size).TransformToAABB(m));
            }
        }
        if (rects.Count == 0)
        {
            return null;
        }
        BlackHoleRenderer renderer;
        try
        {
            // The rest of the content, without the items (hidden for the snapshot, as they will be afterwards).
            var opacities = new double[targets.Length];
            for (var i = 0; i < targets.Length; i++)
            {
                opacities[i] = targets[i].Opacity;
                targets[i].Opacity = 0;
            }
            bool redFirst;
            try
            {
                using var rest = new RenderTargetBitmap(pixels, new Vector(96 * scale, 96 * scale));
                rest.Render(presenter);
                redFirst = rest.Format == PixelFormat.Rgba8888;
                renderer = new BlackHoleRenderer(pixels.Width, pixels.Height, scale, redFirst);
                CopyPixels(rest, renderer.Rest, pixels.Width, new PixelRect(pixels), 0, 0);
            }
            finally
            {
                for (var i = 0; i < targets.Length; i++)
                {
                    targets[i].Opacity = opacities[i];
                }
            }
            // The items alone, each at its place in the content (shown for its snapshot even while hidden).
            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target.TransformToVisual(presenter) is not { } m)
                {
                    continue;
                }
                var origin = new Point(0, 0).Transform(m);
                var px = (int)Math.Round(origin.X * scale);
                var py = (int)Math.Round(origin.Y * scale);
                var tw = (int)Math.Ceiling(target.Bounds.Width * scale);
                var th = (int)Math.Ceiling(target.Bounds.Height * scale);
                if (tw < 1 || th < 1)
                {
                    continue;
                }
                var before = target.Opacity;
                target.Opacity = OriginalOpacity(target);
                try
                {
                    using var item = new RenderTargetBitmap(new PixelSize(tw, th), new Vector(96 * scale, 96 * scale));
                    item.Render(target);
                    CopyPixels(item, renderer.Items, pixels.Width, new PixelRect(0, 0, tw, th), px, py, pixels.Height);
                }
                finally
                {
                    target.Opacity = before;
                }
            }
        }
        catch (Exception)
        {
            // No offscreen rendering on this platform: the items simply disappear (or appear), as with motion off.
            return null;
        }
        var center = ResolveOrigin(options, presenter, rects);
        renderer.BuildMesh(rects, center, new BlackHoleLook(
            Math.Max(0, HoleRadius), CoreGrowth, EinsteinRatio, HaloDecay, HaloOpacity, PhotonRingWidth,
            ColorOf(PhotonRingBrush, Color.FromArgb(145, 0x2A, 0x2A, 0x2A)),
            Math.Max(0, ShockwaveRadius), ShockWidth, ShockOpacity, ColorOf(ShockBrush, Color.FromRgb(0x34, 0x34, 0x34))));
        var target0 = new Rect(presenter.Bounds.Position, new Size(pixels.Width / scale, pixels.Height / scale));
        return new Prepared(renderer, direction, target0);
    }

    private Point ResolveOrigin(BlackHoleOptions? options, ContentPresenter presenter, List<Rect> rects)
    {
        if (options?.Origin is { } origin && this.TranslatePoint(origin, presenter) is { } mapped)
        {
            return mapped;
        }
        if (options?.OriginElement is { } element && element.TranslatePoint(new Point(element.Bounds.Width / 2, element.Bounds.Height / 2), presenter) is { } centre)
        {
            return centre;
        }
        var union = rects[0];
        for (var i = 1; i < rects.Count; i++)
        {
            union = union.Union(rects[i]);
        }
        return union.Center;
    }

    /// <summary>Copies <paramref name="from"/> of a bitmap into a buffer of row length <paramref name="stride"/> at (x, y), clipped.</summary>
    private static void CopyPixels(RenderTargetBitmap bitmap, uint[] buffer, int stride, PixelRect from, int x, int y, int height = int.MaxValue)
    {
        var width = from.Width;
        var temp = new uint[width * from.Height];
        var handle = GCHandle.Alloc(temp, GCHandleType.Pinned);
        try
        {
            bitmap.CopyPixels(from, handle.AddrOfPinnedObject(), temp.Length * 4, width * 4);
        }
        finally
        {
            handle.Free();
        }
        var rows = Math.Min(from.Height, Math.Min(height, buffer.Length / stride) - y);
        for (var j = Math.Max(0, -y); j < rows; j++)
        {
            var left = Math.Max(0, -x);
            var right = Math.Min(width, stride - x);
            if (right <= left)
            {
                return;
            }
            var src = temp.AsSpan(j * width + left, right - left);
            var dst = buffer.AsSpan((y + j) * stride + x + left, right - left);
            // Items may overlap: later ones paint over earlier ones.
            for (var i = 0; i < src.Length; i++)
            {
                dst[i] = src[i] == 0 ? dst[i] : BlackHoleRenderer.Over(src[i], dst[i]);
            }
        }
    }

    /// <summary>
    /// Plays a prepared run: ticks on a <see cref="LiveLoop"/>, frames painted on a <see cref="BlackHoleFrameWorker"/> one
    /// tick ahead (the UI tick only uploads the finished buffer). It runs at every motion level once started, jumps to its
    /// end if the control leaves the tree, and completes only after the worker has stopped and the buffers are released.
    /// </summary>
    private Task PlayAsync(Prepared run, double lengthMs)
    {
        var presenter = _presenter!;
        var stage = _stage!;
        var renderer = run.Renderer;
        var swallow = run.Direction == BlackHoleDirection.Swallow;
        var reference = BlackHoleMath.ReferenceDuration;
        var swallowLength = swallow ? lengthMs : lengthMs / Math.Max(0.05, ReleaseSpeed);
        var done = new TaskCompletionSource();
        LiveLoop? loop = null;
        BlackHoleFrameWorker? worker = null;
        var presenterOpacity = presenter.Opacity;
        var lastStep = 1000.0 / 60;
        Renderer = renderer;
        Elapsed = 0;

        // Swallow time on the 1.3 s reference scale for elapsed run time e (ms).
        double TimeAt(double e) => swallow
            ? Math.Min(reference, e / swallowLength * reference)
            : BlackHoleMath.SwallowTimeForRelease(e, swallowLength, ReleaseSpeed) / swallowLength * reference;

        void Finish()
        {
            if (done.Task.IsCompleted)
            {
                return;
            }
            _abort = null;
            loop?.Dispose();
            // The frame in flight finishes before the buffers go; then the real content is back and the task completes.
            worker?.Dispose();
            Worker = null;
            stage.End();
            presenter.Opacity = presenterOpacity;
            Renderer = null;
            done.TrySetResult();
        }

        // The first frame is painted here, so the stage never shows an empty bitmap; the rest on the worker.
        stage.Begin(new PixelSize(renderer.Width, renderer.Height), run.Target, renderer.RedFirst);
        renderer.Render(TimeAt(0));
        stage.Present(renderer.Output, renderer.Width, renderer.Height);
        presenter.Opacity = 0;
        _abort = Finish;
        try
        {
            worker = new BlackHoleFrameWorker(renderer);
            Worker = worker;
            worker.Start(TimeAt(lastStep));
        }
        catch (Exception)
        {
            // No thread to spare: frames are painted on the UI tick instead.
            worker = null;
        }
        // Code-driven: every frame is a CPU paint of the snapshot, which no ControlTheme transition can express.
        loop = new LiveLoop(this, dt =>
        {
            var step = dt * 1000;
            Elapsed += step;
            if (Elapsed >= lengthMs)
            {
                Finish();
                return false;
            }
            if (worker is null)
            {
                renderer.Render(TimeAt(Elapsed));
                stage.Present(renderer.Output, renderer.Width, renderer.Height);
                return true;
            }
            // The frame asked for one tick ago (painted for about now): wait for it, ask for the next one, and upload this
            // one while the next is painted into the other buffer.
            if (!worker.Wait())
            {
                Finish();
                return false;
            }
            lastStep = step > 0 ? step : lastStep;
            var frame = renderer.Output;
            worker.Start(TimeAt(Elapsed + lastStep));
            stage.Present(frame, renderer.Width, renderer.Height);
            return true;
        })
        {
            MinimumLevel = ArcMotionLevel.Off,
            TracksViewport = false
        };
        loop.Start();
        if (!loop.IsRunning)
        {
            Finish();
        }
        return done.Task;
    }

    /// <summary>Reduced motion: the items fade (200 ms, as Arc documents) instead of falling.</summary>
    private Task FadeAsync(Control[] targets, double from, double to)
    {
        var done = new TaskCompletionSource();
        var originals = targets.Select(OriginalOpacity).ToArray();
        var elapsed = 0.0;
        LiveLoop? loop = null;

        void Set(double p)
        {
            var k = from + (to - from) * p;
            for (var i = 0; i < targets.Length; i++)
            {
                targets[i].Opacity = originals[i] * k;
            }
        }

        void Finish()
        {
            if (done.Task.IsCompleted)
            {
                return;
            }
            _abort = null;
            loop?.Dispose();
            Set(1);
            done.TrySetResult();
        }

        Set(0);
        _abort = Finish;
        loop = new LiveLoop(this, dt =>
        {
            elapsed += dt * 1000;
            if (elapsed >= ReducedFade.TotalMilliseconds)
            {
                Finish();
                return false;
            }
            Set(elapsed / ReducedFade.TotalMilliseconds);
            return true;
        })
        {
            MinimumLevel = ArcMotionLevel.Off,
            TracksViewport = false
        };
        loop.Start();
        if (!loop.IsRunning)
        {
            Finish();
        }
        return done.Task;
    }

    /// <summary>
    /// Waits until the restored items stop moving (the host's height animation opening their rows), so the release
    /// starts from their final places; at most <see cref="SettleTimeout"/>.
    /// </summary>
    private Task SettleAsync(Control[] targets)
    {
        if (_presenter is not { } presenter)
        {
            return Task.CompletedTask;
        }
        var done = new TaskCompletionSource();
        var last = new Rect[targets.Length];
        var elapsed = 0.0;
        var still = 0;
        var frames = 0;
        LiveLoop? loop = null;

        bool Measure()
        {
            var moved = false;
            for (var i = 0; i < targets.Length; i++)
            {
                var rect = targets[i].TransformToVisual(presenter) is { } m ? new Rect(targets[i].Bounds.Size).TransformToAABB(m) : default;
                var prev = last[i];
                if (Math.Abs(rect.X - prev.X) > 0.05 || Math.Abs(rect.Y - prev.Y) > 0.05 ||
                    Math.Abs(rect.Width - prev.Width) > 0.05 || Math.Abs(rect.Height - prev.Height) > 0.05)
                {
                    moved = true;
                }
                last[i] = rect;
            }
            return moved;
        }

        void Finish()
        {
            if (done.Task.IsCompleted)
            {
                return;
            }
            _abort = null;
            loop?.Dispose();
            done.TrySetResult();
        }

        Measure();
        _abort = Finish;
        loop = new LiveLoop(this, dt =>
        {
            elapsed += dt * 1000;
            frames++;
            still = Measure() ? 0 : still + 1;
            if ((still >= 2 && frames >= 2) || elapsed >= SettleTimeout.TotalMilliseconds)
            {
                Finish();
                return false;
            }
            return true;
        })
        {
            MinimumLevel = ArcMotionLevel.Off,
            TracksViewport = false
        };
        loop.Start();
        if (!loop.IsRunning)
        {
            Finish();
        }
        return done.Task;
    }
}
