using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.OpenGL;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// Drawing moving Showpiece parts from bitmap caches: whether the compositor renders on a GPU (the caches pay off only
/// there), how a part's cache is switched, the scale it is rendered at, and how long it is kept after the motion rests
/// (<see cref="ShowpieceCacheHold"/>).
/// </summary>
/// <remarks>
/// <para>
/// A GPU draws a cached bitmap through a perspective matrix as one textured quad, whereas vectors under the same matrix lose
/// the backend's fast paths (text, rounded clips, strokes, dashes and blurred shadows fall back to software masks every
/// frame). The software rasterizer samples every pixel through the perspective and is slower with bitmaps than with the
/// vectors (measured at 2x: about 28 ms against 20 ms a frame), so the caches are for GPU compositors only.
/// </para>
/// <para>
/// Avalonia's Skia backend publishes its interop features (external objects, OpenGL texture sharing) only for GPU contexts,
/// so a compositor that has one of them renders on a GPU. A GPU without them is treated as software: the parts then draw
/// vectors. The answer is asked once per compositor (it may need a trip to the render thread) and kept.
/// </para>
/// <para>
/// Avalonia 12.1 behaviours the callers rely on: setting <see cref="Visual.CacheMode"/> does not invalidate the visual (the
/// compositor reads it when the visual is next recorded), so <see cref="SetCache"/> invalidates it; a cache is re-rendered
/// when a descendant's drawing changes but not when the cached visual's own drawing does, so a part that changes while
/// cached draws in a child of the cached visual; and a cache set before the visual was first rendered was ignored in a
/// minimal repro, so parts are cached once they move (they have been drawn by then).
/// </para>
/// </remarks>
internal static class ShowpieceRenderer
{
    /// <summary>
    /// How long caches stay on after the motion rests: transitions that run past the motion (dimming, outlines) stay
    /// cheap, and a motion that resumes soon keeps its bitmaps.
    /// </summary>
    public static readonly TimeSpan CacheHold = TimeSpan.FromMilliseconds(500);

    private static readonly ConditionalWeakTable<Compositor, Task<bool>> s_gpu = new();

    /// <summary>The cached answer for <paramref name="compositor"/>, or null while it is being asked.</summary>
    public static bool? TryGetIsGpu(Compositor compositor) =>
        s_gpu.TryGetValue(compositor, out var task) && task.IsCompletedSuccessfully ? task.Result : null;

    /// <summary>Whether <paramref name="compositor"/> renders on a GPU (false when it cannot tell).</summary>
    public static Task<bool> IsGpuAsync(Compositor compositor) => s_gpu.GetValue(compositor, Ask);

    /// <summary>
    /// Reports whether <paramref name="visual"/>'s compositor (its window's, else the default one) renders on a GPU: at once
    /// when the answer is known or there is no compositor (false), otherwise later on the UI thread.
    /// </summary>
    public static void Detect(Visual visual, Action<bool> apply)
    {
        var compositor = TopLevel.GetTopLevel(visual) is { } top ? ElementComposition.GetElementVisual(top)?.Compositor : null;
        compositor ??= Compositor.TryGetDefaultCompositor();
        if (compositor is null)
        {
            apply(false);
            return;
        }
        if (TryGetIsGpu(compositor) is { } known)
        {
            apply(known);
            return;
        }
        _ = DetectAsync(compositor, apply);
    }

    /// <summary>
    /// Draws <paramref name="visual"/> and its children from <paramref name="mode"/> (null: as vectors) and invalidates it so
    /// the compositor picks the change up. True when the mode changed.
    /// </summary>
    public static bool SetCache(Visual? visual, CacheMode? mode)
    {
        if (visual is null || ReferenceEquals(visual.CacheMode, mode))
        {
            return false;
        }
        visual.CacheMode = mode;
        visual.InvalidateVisual();
        return true;
    }

    /// <summary>
    /// The <see cref="BitmapCache.RenderAtScale"/> for a part shown at most <paramref name="largest"/> times its size: rounded
    /// up to a quarter (at least 0.25), so small changes keep the bitmaps.
    /// </summary>
    public static double CacheScale(double largest) =>
        double.IsFinite(largest) ? Math.Max(0.25, Math.Ceiling(largest * 4) / 4) : 1;

    /// <summary>
    /// The largest magnification <paramref name="matrix"/> (projective) gives <paramref name="rect"/>: the longer screen
    /// length of the rectangle's two unit axes, taken at its corners (where a perspective magnifies most). Corners behind the
    /// eye do not count; 0 when none is in front.
    /// </summary>
    public static double Magnification(in Matrix matrix, Rect rect)
    {
        var largest = 0.0;
        CornerScale(matrix, rect.Left, rect.Top, ref largest);
        CornerScale(matrix, rect.Right, rect.Top, ref largest);
        CornerScale(matrix, rect.Right, rect.Bottom, ref largest);
        CornerScale(matrix, rect.Left, rect.Bottom, ref largest);
        return largest;
    }

    private static void CornerScale(in Matrix m, double x, double y, ref double largest)
    {
        var w = x * m.M13 + y * m.M23 + m.M33;
        if (!(w > 1e-9))
        {
            return;
        }
        var px = (x * m.M11 + y * m.M21 + m.M31) / w;
        var py = (x * m.M12 + y * m.M22 + m.M32) / w;
        // The Jacobian's columns: how far the screen point moves per unit along x and along y.
        var ax = (m.M11 - px * m.M13) / w;
        var ay = (m.M12 - py * m.M13) / w;
        var bx = (m.M21 - px * m.M23) / w;
        var by = (m.M22 - py * m.M23) / w;
        largest = Math.Max(largest, Math.Sqrt(Math.Max(ax * ax + ay * ay, bx * bx + by * by)));
    }

    private static Task<bool> Ask(Compositor compositor) => AskAsync(compositor);

    private static async Task<bool> AskAsync(Compositor compositor)
    {
        try
        {
            if (await compositor.TryGetRenderInterfaceFeature(typeof(IExternalObjectsRenderInterfaceContextFeature)) is not null)
            {
                return true;
            }
            return await compositor.TryGetRenderInterfaceFeature(typeof(IOpenGlTextureSharingRenderInterfaceContextFeature)) is not null;
        }
        catch (Exception)
        {
            // A compositor that cannot answer (shutting down, no render interface) gets the vectors.
            return false;
        }
    }

    private static async Task DetectAsync(Compositor compositor, Action<bool> apply)
    {
        var gpu = await IsGpuAsync(compositor);
        if (Dispatcher.UIThread.CheckAccess())
        {
            apply(gpu);
        }
        else
        {
            Dispatcher.UIThread.Post(() => apply(gpu));
        }
    }
}

/// <summary>
/// The wait between a motion resting and its caches going back to vectors (<see cref="ShowpieceRenderer.CacheHold"/>). It
/// runs on the frame clock (a linear tween of a <see cref="MotionValue{T}"/>), so it follows the stage's frames and the
/// test clock; code-driven because it starts from the state of springs, which no ControlTheme transition can observe.
/// Not attached or with motion off, it ends at once.
/// </summary>
internal sealed class ShowpieceCacheHold
{
    private static readonly LinearEasing s_linear = new();

    private readonly Visual _owner;
    private readonly Action _elapsed;
    private MotionValue<double>? _value;

    /// <param name="owner">The visual whose frame clock and motion level the hold follows.</param>
    /// <param name="elapsed">Called when the hold runs out (not when it is cancelled).</param>
    public ShowpieceCacheHold(Visual owner, Action elapsed)
    {
        _owner = owner;
        _elapsed = elapsed;
    }

    /// <summary>The hold is counting down.</summary>
    public bool IsRunning => _value?.IsAnimating == true;

    /// <summary>Starts the hold, or starts it again from the beginning.</summary>
    public void Start()
    {
        if (_value is null)
        {
            _value = new MotionValue<double>(_owner, 0, MotionVectors.Double);
            _value.Changed += OnChanged;
        }
        _value.Jump(0);
        _ = _value.AnimateTo(1, MotionSpec.Tween(ShowpieceRenderer.CacheHold, s_linear));
    }

    /// <summary>Stops the hold without calling back.</summary>
    public void Cancel() => _value?.Stop();

    private void OnChanged(double value)
    {
        if (value >= 1)
        {
            _elapsed();
        }
    }
}
