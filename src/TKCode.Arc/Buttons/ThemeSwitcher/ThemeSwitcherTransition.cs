using AtomUI;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using AtomUI.Theme;
using AtomUI.Theme.Algorithms;
using AtomUI.Theme.Configuration;
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
/// Applies light or dark through AtomUI's theme manager: the last applied configuration is kept (accent, tokens,
/// control overrides) and only <c>ThemeAlgorithm.Dark</c> is toggled. Before any request has been observed the
/// current Arc accent preset is carried over.
/// </summary>
internal static class ThemeSwitcherThemeApplier
{
    private static ThemeConfig? s_lastConfig;
    private static IThemeManager? s_tracked;

    public static void EnsureTracking()
    {
        var manager = Application.Current?.GetThemeManager();
        if (manager is null || ReferenceEquals(manager, s_tracked))
        {
            return;
        }
        if (s_tracked is not null)
        {
            s_tracked.ThemeChanged -= OnThemeChanged;
        }
        s_tracked = manager;
        s_lastConfig = null;
        manager.ThemeChanged += OnThemeChanged;
    }

    private static void OnThemeChanged(object? sender, ThemeChangedEventArgs e) => s_lastConfig = e.Request.Config;

    public static async Task ApplyAsync(Control context, bool dark)
    {
        EnsureTracking();
        var manager = Application.Current?.GetThemeManager();
        if (manager is null)
        {
            return;
        }
        var config = Build(s_lastConfig, manager.CurrentTheme?.Algorithms, dark, AccentOf(context));
        var themeId = manager.CurrentTheme?.ThemeId ?? IThemeManager.DEFAULT_THEME_ID;
        await manager.ApplyThemeAsync(new ThemeRequest(themeId, config, ThemeTransitionReason.UserRequest));
    }

    private static ArcAccentPreset? AccentOf(Control context)
    {
        if (context.TryFindResource(ArcDesignTokenKind.AccentPreset, out var value) && value is string name &&
            Enum.TryParse<ArcAccentPreset>(name, true, out var preset))
        {
            return preset;
        }
        return null;
    }

    internal static ThemeConfig Build(ThemeConfig? previous, IReadOnlyList<ThemeAlgorithm>? current, bool dark, ArcAccentPreset? accent)
    {
        var algorithms = (previous?.Algorithms ?? current ?? [ThemeAlgorithm.Default]).Where(a => a != ThemeAlgorithm.Dark).ToList();
        if (!algorithms.Contains(ThemeAlgorithm.Default))
        {
            algorithms.Insert(0, ThemeAlgorithm.Default);
        }
        if (dark)
        {
            algorithms.Add(ThemeAlgorithm.Dark);
        }
        var builder = new ThemeConfigBuilder().WithAlgorithms(algorithms.ToArray());
        if (previous is not null)
        {
            builder = builder.WithInherit(previous.Inherit);
            foreach (var (name, value) in previous.Tokens)
            {
                builder = builder.WithToken(name, value);
            }
            foreach (var (identity, control) in previous.Controls)
            {
                builder = builder.WithControl(identity, control);
            }
        }
        else if (accent is { } preset && preset != ArcAccentPreset.Neutral)
        {
            builder = builder.WithArc(arc => arc.Accent(preset));
        }
        return builder.Build();
    }
}
