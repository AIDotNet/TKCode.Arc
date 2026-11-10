using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The picture of a <see cref="ScrollStory"/>: a small app window drawing one of the <see cref="ScrollStoryScene"/>s, or a
/// step's own image. Changing <see cref="Scene"/> (or <see cref="Image"/>) cross-fades from the old picture to the new
/// one; at full motion the new one also rises 10 px. Brushes and pens are cached per color; a frame allocates nothing.
/// </summary>
internal sealed class ScrollStorySceneView : Control
{
    public static readonly StyledProperty<ScrollStoryScene> SceneProperty =
        AvaloniaProperty.Register<ScrollStorySceneView, ScrollStoryScene>(nameof(Scene));

    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<ScrollStorySceneView, IImage?>(nameof(Image));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<ScrollStorySceneView>();

    public static readonly StyledProperty<IBrush?> WindowBrushProperty =
        AvaloniaProperty.Register<ScrollStorySceneView, IBrush?>(nameof(WindowBrush));

    public static readonly StyledProperty<IBrush?> PanelBrushProperty =
        AvaloniaProperty.Register<ScrollStorySceneView, IBrush?>(nameof(PanelBrush));

    public static readonly StyledProperty<IBrush?> EdgeBrushProperty =
        AvaloniaProperty.Register<ScrollStorySceneView, IBrush?>(nameof(EdgeBrush));

    public static readonly StyledProperty<IBrush?> MutedBrushProperty =
        AvaloniaProperty.Register<ScrollStorySceneView, IBrush?>(nameof(MutedBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<ScrollStorySceneView, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> SuccessBrushProperty =
        AvaloniaProperty.Register<ScrollStorySceneView, IBrush?>(nameof(SuccessBrush));

    private static readonly int[] s_columnCards = [3, 2, 1];
    private MotionValue<double>? _mix;
    private ScrollStoryScene _previousScene;
    private IImage? _previousImage;
    private bool _hasPrevious;
    private IPen? _edgePen;
    private IPen? _accentPen;
    private IBrush? _accentSoft;

    static ScrollStorySceneView()
    {
        AffectsRender<ScrollStorySceneView>(CornerRadiusProperty, WindowBrushProperty, PanelBrushProperty, MutedBrushProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<ScrollStorySceneView>(false);
    }

    public ScrollStorySceneView()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    public ScrollStoryScene Scene { get => GetValue(SceneProperty); set => SetValue(SceneProperty, value); }

    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public IBrush? WindowBrush { get => GetValue(WindowBrushProperty); set => SetValue(WindowBrushProperty, value); }

    public IBrush? PanelBrush { get => GetValue(PanelBrushProperty); set => SetValue(PanelBrushProperty, value); }

    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }

    public IBrush? MutedBrush { get => GetValue(MutedBrushProperty); set => SetValue(MutedBrushProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? SuccessBrush { get => GetValue(SuccessBrushProperty); set => SetValue(SuccessBrushProperty, value); }

    /// <summary>How far the change to the current picture is, 0 to 1.</summary>
    internal double Mix => _mix?.Value ?? 1;

    /// <summary>Shows a step's picture; the first call (or one before the view is shown) does not animate.</summary>
    public void Show(ScrollStoryScene scene, IImage? image)
    {
        if (scene == Scene && ReferenceEquals(image, Image))
        {
            return;
        }
        _previousScene = Scene;
        _previousImage = Image;
        _hasPrevious = true;
        SetCurrentValue(SceneProperty, scene);
        SetCurrentValue(ImageProperty, image);
        _mix ??= CreateMix();
        _mix.Jump(0);
        _ = _mix.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Standard));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == EdgeBrushProperty || change.Property == AccentBrushProperty)
        {
            _edgePen = null;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        EnsurePens();
        var shape = new RoundedRect(bounds.Deflate(0.5), CornerRadius);
        context.DrawRectangle(WindowBrush, _edgePen, shape);
        using var clip = context.PushClip(new RoundedRect(bounds, CornerRadius));
        var mix = Mix;
        var full = ArcMotion.IsFull(this);
        if (_hasPrevious && mix < 1)
        {
            using (context.PushOpacity(1 - mix))
            {
                DrawPicture(context, bounds, _previousScene, _previousImage);
            }
        }
        using (context.PushOpacity(_hasPrevious ? mix : 1))
        using (context.PushTransform(Matrix.CreateTranslation(0, full && _hasPrevious ? (1 - mix) * 10 : 0)))
        {
            DrawPicture(context, bounds, Scene, Image);
        }
    }

    private void DrawPicture(DrawingContext context, Rect bounds, ScrollStoryScene scene, IImage? image)
    {
        if (image is not null)
        {
            var size = image.Size;
            if (size.Width > 0 && size.Height > 0)
            {
                var scale = Math.Max(bounds.Width / size.Width, bounds.Height / size.Height);
                var w = bounds.Width / scale;
                var h = bounds.Height / scale;
                context.DrawImage(image, new Rect((size.Width - w) / 2, (size.Height - h) / 2, w, h), bounds);
            }
            return;
        }
        // The window chrome: three dots and a title bar line.
        var pad = Math.Max(12, bounds.Width * 0.06);
        for (var i = 0; i < 3; i++)
        {
            context.DrawEllipse(MutedBrush, null, new Point(pad + i * 12, pad), 3.5, 3.5);
        }
        context.DrawLine(_edgePen!, new Point(0, pad * 2), new Point(bounds.Width, pad * 2));
        var area = new Rect(pad, pad * 2 + pad * 0.8, bounds.Width - pad * 2, bounds.Height - pad * 3.8);
        if (area.Width <= 0 || area.Height <= 0)
        {
            return;
        }
        switch (scene)
        {
            case ScrollStoryScene.Capture:
                DrawCapture(context, area);
                break;
            case ScrollStoryScene.Arrange:
                DrawArrange(context, area);
                break;
            case ScrollStoryScene.Review:
                DrawReview(context, area);
                break;
            default:
                DrawLaunch(context, area);
                break;
        }
    }

    private void DrawCapture(DrawingContext context, Rect area)
    {
        var rowH = Math.Min(44, area.Height / 5.5);
        for (var i = 0; i < 5; i++)
        {
            var row = new Rect(area.X, area.Y + i * (rowH + 8), area.Width, rowH);
            if (row.Bottom > area.Bottom)
            {
                break;
            }
            var first = i == 0;
            context.DrawRectangle(first ? _accentSoft : PanelBrush, first ? _accentPen : _edgePen, new RoundedRect(row, 10));
            context.DrawEllipse(first ? AccentBrush : MutedBrush, null, new Point(row.X + rowH / 2, row.Center.Y), rowH * 0.2, rowH * 0.2);
            Bar(context, row.X + rowH, row.Center.Y - 5, row.Width * (first ? 0.42 : 0.3 + 0.08 * (i % 3)), 4, first ? AccentBrush : MutedBrush);
            Bar(context, row.X + rowH, row.Center.Y + 3, row.Width * 0.5, 3, MutedBrush);
        }
    }

    private void DrawArrange(DrawingContext context, Rect area)
    {
        var gap = 10.0;
        var colW = (area.Width - gap * 2) / 3;
        for (var c = 0; c < 3; c++)
        {
            var col = new Rect(area.X + c * (colW + gap), area.Y, colW, area.Height);
            context.DrawRectangle(PanelBrush, null, new RoundedRect(col, 10));
            Bar(context, col.X + 10, col.Y + 12, colW * 0.45, 4, MutedBrush);
            for (var k = 0; k < s_columnCards[c]; k++)
            {
                var card = new Rect(col.X + 8, col.Y + 28 + k * 52, colW - 16, 44);
                if (card.Bottom > col.Bottom)
                {
                    break;
                }
                context.DrawRectangle(WindowBrush, _edgePen, new RoundedRect(card, 8));
                Bar(context, card.X + 8, card.Y + 12, card.Width * 0.6, 4, MutedBrush);
                Bar(context, card.X + 8, card.Y + 24, card.Width * 0.4, 3, MutedBrush);
            }
        }
        // The card being moved: tilted, lifted, in accent.
        var lifted = new Rect(area.X + colW * 0.7, area.Y + 96, colW - 10, 44);
        var center = lifted.Center;
        using (context.PushTransform(Matrix.CreateTranslation(-center.X, -center.Y) * Matrix.CreateRotation(-0.06) * Matrix.CreateTranslation(center.X, center.Y)))
        {
            context.DrawRectangle(_accentSoft, _accentPen, new RoundedRect(lifted, 8));
            Bar(context, lifted.X + 8, lifted.Y + 12, lifted.Width * 0.6, 4, AccentBrush);
        }
    }

    private void DrawReview(DrawingContext context, Rect area)
    {
        var lineGap = Math.Max(14, area.Height / 9);
        for (var i = 0; i < 7; i++)
        {
            var y = area.Y + i * lineGap;
            if (y > area.Bottom - 8)
            {
                break;
            }
            var width = area.Width * (i == 0 ? 0.5 : 0.62 + 0.12 * Math.Sin(i * 1.7));
            Bar(context, area.X, y, width * 0.75, i == 0 ? 6 : 4, i == 3 ? AccentBrush : MutedBrush);
        }
        var bubble = new Rect(area.X + area.Width * 0.58, area.Y + lineGap * 2.6, area.Width * 0.42, lineGap * 2.4);
        context.DrawRectangle(WindowBrush, _accentPen, new RoundedRect(bubble, 10));
        Bar(context, bubble.X + 10, bubble.Y + 12, bubble.Width * 0.6, 4, MutedBrush);
        Bar(context, bubble.X + 10, bubble.Y + 22, bubble.Width * 0.4, 3, MutedBrush);
        var check = new Point(bubble.Right - 16, bubble.Bottom - 14);
        context.DrawEllipse(SuccessBrush, null, check, 8, 8);
    }

    private void DrawLaunch(DrawingContext context, Rect area)
    {
        var baseY = area.Bottom - 6;
        context.DrawLine(_edgePen!, new Point(area.X, baseY), new Point(area.Right, baseY));
        var previous = default(Point);
        for (var i = 0; i <= 12; i++)
        {
            var t = i / 12.0;
            var value = 0.12 + 0.75 * t * t + 0.05 * Math.Sin(i * 1.9);
            var point = new Point(area.X + t * area.Width, baseY - value * (area.Height - 30));
            if (i > 0)
            {
                context.DrawLine(_accentPen!, previous, point);
            }
            previous = point;
        }
        context.DrawEllipse(AccentBrush, null, previous, 5, 5);
        var badge = new Rect(area.X, area.Y, Math.Min(86, area.Width * 0.4), 24);
        context.DrawRectangle(_accentSoft, null, new RoundedRect(badge, 12));
        context.DrawEllipse(SuccessBrush, null, new Point(badge.X + 14, badge.Center.Y), 4, 4);
        Bar(context, badge.X + 24, badge.Center.Y - 2, badge.Width - 36, 4, AccentBrush);
    }

    private static void Bar(DrawingContext context, double x, double y, double width, double height, IBrush? brush)
    {
        if (width > 0 && brush is not null)
        {
            context.DrawRectangle(brush, null, new RoundedRect(new Rect(x, y, width, height), height / 2));
        }
    }

    private void EnsurePens()
    {
        if (_edgePen is not null)
        {
            return;
        }
        var edge = (EdgeBrush as ISolidColorBrush)?.Color ?? Colors.LightGray;
        var accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.SteelBlue;
        _edgePen = new ImmutablePen(new ImmutableSolidColorBrush(edge), 1);
        _accentPen = new ImmutablePen(new ImmutableSolidColorBrush(accent), 1.5, lineCap: PenLineCap.Round);
        _accentSoft = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, 0.14));
    }

    private MotionValue<double> CreateMix()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += _ => InvalidateVisual();
        return value;
    }
}
