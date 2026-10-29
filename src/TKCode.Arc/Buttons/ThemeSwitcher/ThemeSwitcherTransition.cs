using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The Avalonia stand-in for Arc's View Transition: a snapshot of the old window sits in the overlay layer while the
/// new theme is applied underneath, then its clip opens by variant (circle from the trigger, an eclipsing disc, a
/// centre seam, a rising edge) and the snapshot is removed. Skipped under reduced motion and Off.
/// </summary>
internal static class ThemeSwitcherTransition
{
    /// <summary>Duration and easing of the reveal (Arc's transition CSS is not part of the free source).</summary>
    private static readonly TimeSpan Duration = ArcMotionPresets.Considered;

    public static async Task RunAsync(Visual trigger, ThemeSwitcherVariant variant, Func<Task> apply)
    {
        var topLevel = TopLevel.GetTopLevel(trigger);
        var layer = OverlayLayer.GetOverlayLayer(trigger);
        if (topLevel is not null && layer is not null && layer.Bounds.Width <= 0)
        {
            // A layer manager creates its overlay layer the first time it is asked for (here, when nothing has opened a
            // popup yet); one layout pass sizes it.
            topLevel.UpdateLayout();
        }
        if (topLevel is null || layer is null || !ArcMotion.IsFull(trigger) || layer.Bounds.Width <= 0 || layer.Bounds.Height <= 0)
        {
            await apply();
            return;
        }
        var offset = layer.TranslatePoint(default, topLevel) ?? default;
        var origin = trigger.TranslatePoint(new Point(trigger.Bounds.Width / 2, trigger.Bounds.Height / 2), layer) ?? default;
        RenderTargetBitmap? snapshot = null;
        try
        {
            var scaling = topLevel.RenderScaling;
            var size = topLevel.Bounds.Size;
            var pixels = new PixelSize(Math.Max(1, (int)Math.Ceiling(size.Width * scaling)), Math.Max(1, (int)Math.Ceiling(size.Height * scaling)));
            snapshot = new RenderTargetBitmap(pixels, new Vector(96 * scaling, 96 * scaling));
            snapshot.Render(topLevel);
        }
        catch (Exception)
        {
            // No snapshot (for example a platform without render targets): switch without the transition.
            snapshot?.Dispose();
            await apply();
            return;
        }
        var overlay = new ThemeSwitcherSnapshot(snapshot, variant, origin, offset, topLevel.Bounds.Size)
        {
            Width = layer.Bounds.Width,
            Height = layer.Bounds.Height
        };
        layer.Children.Add(overlay);
        try
        {
            await apply();
            // Let the new theme lay out and paint once under the snapshot before revealing it.
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await overlay.PlayAsync(Duration);
        }
        finally
        {
            layer.Children.Remove(overlay);
            snapshot.Dispose();
        }
    }
}

/// <summary>The old window, drawn over the new one and clipped to the part not yet revealed.</summary>
internal sealed class ThemeSwitcherSnapshot : Control
{
    private readonly Bitmap _image;
    private readonly ThemeSwitcherVariant _variant;
    private readonly Point _origin;
    private readonly Point _offset;
    private readonly Size _imageSize;
    private readonly RectangleGeometry _bounds = new();
    private readonly EllipseGeometry _disc = new();
    private readonly RectangleGeometry _seam = new();
    private readonly CombinedGeometry _clip;
    private MotionValue<double>? _progress;

    public ThemeSwitcherSnapshot(Bitmap image, ThemeSwitcherVariant variant, Point origin, Point offset, Size imageSize)
    {
        _image = image;
        _variant = variant;
        _origin = origin;
        _offset = offset;
        _imageSize = imageSize;
        IsHitTestVisible = true;
        _clip = new CombinedGeometry(GeometryCombineMode.Exclude, _bounds, variant == ThemeSwitcherVariant.Split ? _seam : _disc);
    }

    public Task PlayAsync(TimeSpan duration)
    {
        _progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        _progress.Changed += Update;
        Update(0);
        return _progress.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.InOut));
    }

    /// <summary>Moves the reveal shape (outside Render) and re-attaches it to the combined clip.</summary>
    private void Update(double p)
    {
        var w = Bounds.Width > 0 ? Bounds.Width : Width;
        var h = Bounds.Height > 0 ? Bounds.Height : Height;
        _bounds.Rect = new Rect(0, 0, w, h);
        switch (_variant)
        {
            case ThemeSwitcherVariant.Reveal:
            {
                // A circle from the trigger to the farthest corner.
                var reach = Math.Max(Math.Max(Distance(_origin, new Point(0, 0)), Distance(_origin, new Point(w, 0))),
                    Math.Max(Distance(_origin, new Point(0, h)), Distance(_origin, new Point(w, h))));
                SetDisc(_origin, reach * p);
                break;
            }
            case ThemeSwitcherVariant.Eclipse:
            {
                // A disc as wide as the diagonal crosses from the right and settles over the page.
                var radius = Math.Sqrt(w * w + h * h);
                SetDisc(new Point(w / 2 + (w / 2 + radius) * (1 - p), h / 2), radius);
                break;
            }
            case ThemeSwitcherVariant.Split:
                _seam.Rect = new Rect(w / 2 * (1 - p), 0, w * p, h);
                break;
        }
        _clip.Geometry2 = null;
        _clip.Geometry2 = _variant == ThemeSwitcherVariant.Split ? _seam : _disc;
        _riseTop = h * (1 - p);
        InvalidateVisual();
    }

    private double _riseTop;

    private void SetDisc(Point center, double radius)
    {
        _disc.Center = center;
        _disc.RadiusX = radius;
        _disc.RadiusY = radius;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>
    /// The whole bitmap as a draw source. <c>DrawImage</c> reads the source rectangle in pixels, while
    /// <see cref="Bitmap.Size"/> is in DIPs: on a scaled display (the snapshot is taken at the top level's render
    /// scaling) <c>new Rect(image.Size)</c> would cover only the top-left <c>1/scaling²</c> of the window and stretch it.
    /// </summary>
    internal static Rect SourceRect(Bitmap image) => new(0, 0, image.PixelSize.Width, image.PixelSize.Height);

    public override void Render(DrawingContext context)
    {
        var dest = new Rect(-_offset.X, -_offset.Y, _imageSize.Width, _imageSize.Height);
        var source = SourceRect(_image);
        if (_variant == ThemeSwitcherVariant.Rise)
        {
            if (_riseTop <= 0.5)
            {
                return;
            }
            using (context.PushClip(new Rect(0, 0, Bounds.Width, _riseTop)))
            {
                context.DrawImage(_image, source, dest);
            }
            return;
        }
        using (context.PushGeometryClip(_clip))
        {
            context.DrawImage(_image, source, dest);
        }
    }
}

/// <summary>
/// Applies light or dark through the application's <see cref="ArcThemeManager"/>: only the appearance changes, the rest of
/// the theme state (accent, token overrides, motion level) is kept by the manager. The switcher's own appearance follows
/// <c>ActualThemeVariant</c>, which the manager sets when it publishes.
/// </summary>
internal static class ThemeSwitcherThemeApplier
{
    public static Task ApplyAsync(Control context, bool dark)
    {
        var manager = ArcThemeManager.Current;
        if (manager is null)
        {
            return Task.CompletedTask;
        }
        return manager.ApplyAsync(t => t.Appearance(dark ? ArcAppearance.Dark : ArcAppearance.Light));
    }
}
