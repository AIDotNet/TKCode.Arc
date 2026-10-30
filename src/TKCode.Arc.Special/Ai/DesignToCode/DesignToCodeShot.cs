using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The screenshot on the design side: the source's image fitted into the frame, or, for a source without an image, its
/// <see cref="DesignToCodeSource.Sketch"/> drawn in the theme's colors. It is
/// arranged by <see cref="DesignToCodeCanvas"/> at the design's rectangle and only redraws when its size, source or
/// colors change (the scan line and the boxes are drawn above it by <see cref="DesignToCodeOverlay"/>).
/// </summary>
internal sealed class DesignToCodeShot : Control
{
    public static readonly StyledProperty<IBrush?> PaperBrushProperty =
        AvaloniaProperty.Register<DesignToCodeShot, IBrush?>(nameof(PaperBrush));

    public static readonly StyledProperty<IBrush?> WellBrushProperty =
        AvaloniaProperty.Register<DesignToCodeShot, IBrush?>(nameof(WellBrush));

    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<DesignToCodeShot, IBrush?>(nameof(InkBrush));

    public static readonly StyledProperty<IBrush?> MutedInkBrushProperty =
        AvaloniaProperty.Register<DesignToCodeShot, IBrush?>(nameof(MutedInkBrush));

    public static readonly StyledProperty<IBrush?> LineBrushProperty =
        AvaloniaProperty.Register<DesignToCodeShot, IBrush?>(nameof(LineBrush));

    public static readonly StyledProperty<IBrush?> AccentBrushProperty =
        AvaloniaProperty.Register<DesignToCodeShot, IBrush?>(nameof(AccentBrush));

    public static readonly StyledProperty<IBrush?> AccentSubtleBrushProperty =
        AvaloniaProperty.Register<DesignToCodeShot, IBrush?>(nameof(AccentSubtleBrush));

    public static readonly StyledProperty<IBrush?> AccentForegroundBrushProperty =
        AvaloniaProperty.Register<DesignToCodeShot, IBrush?>(nameof(AccentForegroundBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        AvaloniaProperty.Register<DesignToCodeShot, CornerRadius>(nameof(CornerRadius));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        Avalonia.Controls.Documents.TextElement.FontFamilyProperty.AddOwner<DesignToCodeShot>();

    private DesignToCodeSource? _source;

    static DesignToCodeShot()
    {
        AffectsRender<DesignToCodeShot>(PaperBrushProperty, WellBrushProperty, InkBrushProperty, MutedInkBrushProperty,
            LineBrushProperty, AccentBrushProperty, AccentSubtleBrushProperty, AccentForegroundBrushProperty, CornerRadiusProperty);
    }

    public IBrush? PaperBrush { get => GetValue(PaperBrushProperty); set => SetValue(PaperBrushProperty, value); }

    public IBrush? WellBrush { get => GetValue(WellBrushProperty); set => SetValue(WellBrushProperty, value); }

    public IBrush? InkBrush { get => GetValue(InkBrushProperty); set => SetValue(InkBrushProperty, value); }

    public IBrush? MutedInkBrush { get => GetValue(MutedInkBrushProperty); set => SetValue(MutedInkBrushProperty, value); }

    public IBrush? LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    public IBrush? AccentSubtleBrush { get => GetValue(AccentSubtleBrushProperty); set => SetValue(AccentSubtleBrushProperty, value); }

    public IBrush? AccentForegroundBrush { get => GetValue(AccentForegroundBrushProperty); set => SetValue(AccentForegroundBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>The design drawn.</summary>
    public DesignToCodeSource? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
            {
                return;
            }
            _source = value;
            InvalidateVisual();
        }
    }

    /// <summary>The design's aspect ratio (width over height): the image's, or the sketch's.</summary>
    public double AspectRatio =>
        _source?.Image is { } image && image.Size.Height > 0
            ? image.Size.Width / image.Size.Height
            : _source is { SketchSize: { Width: > 0, Height: > 0 } size } ? size.Width / size.Height
            : DesignToCodeCanvas.DefaultDesignSize.Width / DesignToCodeCanvas.DefaultDesignSize.Height;

    protected override AutomationPeer OnCreateAutomationPeer() => new DesignToCodeShotPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0 || _source is null)
        {
            return;
        }
        var radius = CornerRadius;
        var frame = new RoundedRect(bounds, radius);
        if (_source.Image is { } image)
        {
            using (context.PushClip(frame))
            {
                context.DrawImage(image, new Rect(image.Size), bounds);
            }
            if (LineBrush is { } line)
            {
                context.DrawRectangle(null, new Pen(line), frame.Deflate(0.5, 0.5));
            }
            return;
        }
        if (_source.Sketch is { } sketch)
        {
            var palette = new DesignToCodeSketchPalette(PaperBrush, WellBrush, InkBrush, MutedInkBrush, LineBrush, AccentBrush,
                AccentSubtleBrush, AccentForegroundBrush, FontFamily, radius);
            using (context.PushClip(frame))
            {
                sketch(context, bounds, palette);
            }
        }
    }

    /// <summary>Screen readers read the screenshot as an image named after the design.</summary>
    private sealed class DesignToCodeShotPeer(DesignToCodeShot owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Image;

        protected override bool IsContentElementCore() => owner._source?.IsSketch == false;

        protected override bool IsControlElementCore() => owner._source?.IsSketch == false;

        protected override string? GetNameCore() => AutomationProperties.GetName(owner);
    }
}
