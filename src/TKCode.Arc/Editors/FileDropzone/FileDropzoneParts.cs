using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The drop target's surface: background, a drawn dashed edge whose dash length is fitted to the perimeter (so the
/// seam never shows), and, while a file is over it, an accent wash and edge light that follow the pointer on a
/// spring. <see cref="Solid"/> (0–1) closes the dashes into a solid edge and brings the light in.
/// </summary>
internal sealed class FileDropzoneZone : Control
{
    private const double Period = 7;

    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<FileDropzoneZone, IBrush?>(nameof(Background));
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty = AvaloniaProperty.Register<FileDropzoneZone, CornerRadius>(nameof(CornerRadius));
    public static readonly StyledProperty<IBrush?> EdgeBrushProperty = AvaloniaProperty.Register<FileDropzoneZone, IBrush?>(nameof(EdgeBrush));
    public static readonly StyledProperty<IBrush?> AccentBrushProperty = AvaloniaProperty.Register<FileDropzoneZone, IBrush?>(nameof(AccentBrush));
    public static readonly StyledProperty<IBrush?> RestingBorderBrushProperty = AvaloniaProperty.Register<FileDropzoneZone, IBrush?>(nameof(RestingBorderBrush));
    public static readonly StyledProperty<double> SolidProperty = AvaloniaProperty.Register<FileDropzoneZone, double>(nameof(Solid));

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>The dashed edge's color at rest (BorderStrong; TextMuted on hover).</summary>
    public IBrush? EdgeBrush { get => GetValue(EdgeBrushProperty); set => SetValue(EdgeBrushProperty, value); }

    public IBrush? AccentBrush { get => GetValue(AccentBrushProperty); set => SetValue(AccentBrushProperty, value); }

    /// <summary>The theme border; the drag edge is the accent mixed 40% into it.</summary>
    public IBrush? RestingBorderBrush { get => GetValue(RestingBorderBrushProperty); set => SetValue(RestingBorderBrushProperty, value); }

    /// <summary>0 at rest, 1 while a file is over the target (the theme transitions it on the standard tween).</summary>
    public double Solid { get => GetValue(SolidProperty); set => SetValue(SolidProperty, value); }

    private readonly MotionValue<Point> _glow;
    private readonly RadialGradientBrush _wash = new() { RadiusX = new RelativeScalar(260, RelativeUnit.Absolute), RadiusY = new RelativeScalar(260, RelativeUnit.Absolute) };
    private readonly RadialGradientBrush _light = new() { RadiusX = new RelativeScalar(180, RelativeUnit.Absolute), RadiusY = new RelativeScalar(180, RelativeUnit.Absolute) };
    private readonly Pen _edgePen = new() { Thickness = 1 };
    private readonly DashStyle _dash = new();
    private readonly Pen _lightPen = new() { Thickness = 1.5 };
    private Color _accent;
    private Color _dragEdge;
    private Color _edgeColor;
    private IBrush? _edgeBrush;
    private IBrush? _accentTint;

    static FileDropzoneZone()
    {
        AffectsRender<FileDropzoneZone>(BackgroundProperty, CornerRadiusProperty, EdgeBrushProperty, SolidProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<FileDropzoneZone>(false);
    }

    public FileDropzoneZone()
    {
        // Arc: useSpring(stiffness 260, damping 32, mass .8) on the pointer position.
        _glow = new MotionValue<Point>(this, new Point(-1000, -1000), MotionVectors.Point);
        _glow.Changed += _ => InvalidateVisual();
        _dash.Dashes = new Avalonia.Collections.AvaloniaList<double> { 3.5, 3.5 };
        _lightPen.Brush = _light;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AccentBrushProperty || change.Property == RestingBorderBrushProperty)
        {
            _accent = (AccentBrush as ISolidColorBrush)?.Color ?? Colors.SteelBlue;
            var border = (RestingBorderBrush as ISolidColorBrush)?.Color ?? Colors.Gray;
            _dragEdge = Oklch.MixOklab(border, _accent, 0.4);
            _wash.GradientStops = new GradientStops
            {
                new GradientStop(Oklch.WithAlpha(_accent, 0.06), 0),
                new GradientStop(Oklch.WithAlpha(_accent, 0), 0.7)
            };
            _light.GradientStops = new GradientStops
            {
                new GradientStop(_accent, 0),
                new GradientStop(Oklch.WithAlpha(_accent, 0), 0.7)
            };
            _accentTint = new ImmutableSolidColorBrush(Oklch.WithAlpha(_accent, 0.025));
            InvalidateVisual();
        }
    }

    /// <summary>Moves the light toward the pointer; <paramref name="jump"/> places it (first frame of a drag, reduced motion).</summary>
    public void MoveGlow(Point point, bool jump)
    {
        if (jump)
        {
            _glow.Jump(point);
        }
        else
        {
            _ = _glow.AnimateTo(point, MotionSpec.Spring(SpringSpec.FromPhysical(260, 32, 0.8)));
        }
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var radius = CornerRadius.TopLeft;
        var rect = new Rect(size);
        var shape = new RoundedRect(rect, radius);
        var solid = Math.Clamp(Solid, 0, 1);
        if (Background is { } background)
        {
            context.DrawRectangle(background, null, shape);
        }
        var glow = _glow.Value;
        if (solid > 0.001)
        {
            using (context.PushOpacity(solid))
            {
                context.DrawRectangle(_accentTint, null, shape);
                _wash.Center = _wash.GradientOrigin = new RelativePoint(glow, RelativeUnit.Absolute);
                context.DrawRectangle(_wash, null, shape);
            }
        }

        // The edge: dashes fitted to the measured perimeter so every dash is the same size and the seam never shows.
        var width = size.Width - 1;
        var height = size.Height - 1;
        var r = Math.Max(0, Math.Min(Math.Min(radius - 0.5, width / 2), height / 2));
        var perimeter = 2 * (width + height) - 8 * r + 2 * Math.PI * r;
        var count = Math.Max(8, Math.Round(perimeter / Period));
        var k = perimeter / (count * Period);
        var edgeColor = Lerp((EdgeBrush as ISolidColorBrush)?.Color ?? Colors.Gray, _dragEdge, solid);
        if (_edgeBrush is null || edgeColor != _edgeColor)
        {
            _edgeColor = edgeColor;
            _edgeBrush = new ImmutableSolidColorBrush(edgeColor);
            _edgePen.Brush = _edgeBrush;
        }
        if (solid >= 0.999)
        {
            _edgePen.DashStyle = null;
        }
        else
        {
            // Arc: stroke-dasharray 3.5 3.5 → 7 0 and dashoffset 0 → 1.75, in path-length units.
            _dash.Dashes![0] = (3.5 + 3.5 * solid) * k;
            _dash.Dashes[1] = Math.Max(0.0001, 3.5 * (1 - solid) * k);
            _dash.Offset = 1.75 * solid * k;
            _edgePen.DashStyle = _dash;
        }
        context.DrawRectangle(null, _edgePen, new RoundedRect(new Rect(0.5, 0.5, width, height), r));

        if (solid > 0.001)
        {
            _light.Center = _light.GradientOrigin = new RelativePoint(glow, RelativeUnit.Absolute);
            using (context.PushOpacity(solid))
            {
                context.DrawRectangle(null, _lightPen, new RoundedRect(rect.Deflate(0.75), Math.Max(0, radius - 0.75)));
            }
        }
    }

    private static Color Lerp(Color a, Color b, double t) => t <= 0 ? a : t >= 1 ? b : Oklch.MixOklab(a, b, t);
}

/// <summary>
/// A row's presence: new rows open their height on the smooth spring while the row drops in from above (y −22, scale
/// 0.94, blur) and lands on a lively spring; leaving rows fold their height away. Only the list relayouts.
/// </summary>
internal sealed class FileDropzoneRowHost : Decorator
{
    private static readonly SpringSpec s_land = SpringSpec.FromVisual(0.42, 0.14);
    private static readonly SpringSpec s_collapse = SpringSpec.FromVisual(0.3, 0);
    private readonly MotionValue<double> _open;
    private readonly MotionValue<double> _y;
    private readonly MotionValue<double> _scale;
    private readonly MotionValue<double> _fade;
    private readonly MatrixTransform _transform = new();
    private readonly BlurEffect _blur = new() { Radius = 0 };
    private bool _blurs;

    public FileDropzoneRowHost(FileDropzoneRow row)
    {
        Row = row;
        Child = row;
        ClipToBounds = true;
        row.RenderTransform = _transform;
        row.RenderTransformOrigin = RelativePoint.Center;
        _open = new MotionValue<double>(this, 1, MotionVectors.Double);
        _y = new MotionValue<double>(this, 0, MotionVectors.Double);
        _scale = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _open.Changed += _ => InvalidateMeasure();
        _y.Changed += _ => Apply();
        _scale.Changed += _ => Apply();
        _fade.Changed += _ => Apply();
    }

    public FileDropzoneRow Row { get; }

    public bool IsLeaving { get; private set; }

    private void Apply()
    {
        _transform.Matrix = Matrix.CreateScale(_scale.Value, _scale.Value) * Matrix.CreateTranslation(0, _y.Value);
        Row.Opacity = _fade.Value;
        var blur = _blurs ? (1 - _fade.Value) * 4 : 0;
        _blur.Radius = blur;
        Row.Effect = blur > 0.05 ? _blur : null;
    }

    /// <summary>Opens a row added in this session, after <paramref name="delay"/> (Arc staggers a batch).</summary>
    public void Enter(TimeSpan delay)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            _fade.Jump(0);
            _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            return;
        }
        _blurs = true;
        _open.Jump(0);
        _y.Jump(-22);
        _scale.Jump(0.94);
        _fade.Jump(0);
        _ = _open.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth, delay));
        _ = _y.AnimateTo(0, MotionSpec.Spring(s_land, delay));
        _ = _scale.AnimateTo(1, MotionSpec.Spring(s_land, delay));
        _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, delay));
    }

    /// <summary>Folds the row away (shorter than the entry), then reports it gone.</summary>
    public async Task Leave()
    {
        IsLeaving = true;
        IsHitTestVisible = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off)
        {
            return;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            await _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            return;
        }
        _blurs = true;
        _ = _scale.AnimateTo(0.97, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        _ = _fade.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, TimeSpan.FromMilliseconds(40)));
        await _open.AnimateTo(0, MotionSpec.Spring(s_collapse));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        return new Size(size.Width, size.Height * Math.Clamp(_open.Value, 0, 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Child.DesiredSize.Height));
        return finalSize;
    }
}

/// <summary>The thin upload bar: the fill slides in from the left (so its round end keeps its shape) and exposes the value as a progress bar.</summary>
internal sealed class FileDropzoneProgressBar : RangeBase
{
    public static readonly StyledProperty<double> FillProperty = AvaloniaProperty.Register<FileDropzoneProgressBar, double>(nameof(Fill));
    public static readonly StyledProperty<IBrush?> TrackBrushProperty = AvaloniaProperty.Register<FileDropzoneProgressBar, IBrush?>(nameof(TrackBrush));
    public static readonly StyledProperty<IBrush?> FillBrushProperty = AvaloniaProperty.Register<FileDropzoneProgressBar, IBrush?>(nameof(FillBrush));

    /// <summary>The drawn fill from 0 to 100 (follows the value on a spring).</summary>
    public double Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    public IBrush? TrackBrush { get => GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }

    public IBrush? FillBrush { get => GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }

    static FileDropzoneProgressBar()
    {
        AffectsRender<FileDropzoneProgressBar>(FillProperty, TrackBrushProperty, FillBrushProperty);
        MaximumProperty.OverrideDefaultValue<FileDropzoneProgressBar>(100);
        FocusableProperty.OverrideDefaultValue<FileDropzoneProgressBar>(false);
    }

    protected override Type StyleKeyOverride => typeof(FileDropzoneProgressBar);

    protected override Size MeasureOverride(Size availableSize) => new(0, 4);

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        var shape = new RoundedRect(rect, rect.Height / 2);
        context.DrawRectangle(TrackBrush, null, shape);
        var fill = Math.Clamp(Fill, 0, 100) / 100;
        if (fill <= 0 || FillBrush is null)
        {
            return;
        }
        using (context.PushClip(shape))
        {
            context.DrawRectangle(FillBrush, null, new RoundedRect(rect.Translate(new Vector((fill - 1) * rect.Width, 0)), rect.Height / 2));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new FileDropzoneProgressBarAutomationPeer(this);
}

internal sealed class FileDropzoneProgressBarAutomationPeer(FileDropzoneProgressBar owner) : RangeBaseAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>The drop target's button: the whole target is one hit area for clicks, Enter and Space.</summary>
internal sealed class FileDropzoneTrigger : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(FileDropzoneTrigger);
}

/// <summary>The Retry and Remove buttons of a row (Retry is a pill with a label; Remove a round icon).</summary>
internal sealed class FileDropzoneActionButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(FileDropzoneActionButton);
}

/// <summary>
/// One file row: thumbnail or type icon, name, size and status (Uploading with a counted percentage and a sliding bar,
/// a drawn check when uploaded, the reason when failed), Retry and Remove. A failure shakes the row once.
/// </summary>
[TemplatePart(RetryPart, typeof(FileDropzoneActionButton))]
[TemplatePart(RemovePart, typeof(FileDropzoneActionButton))]
[TemplatePart(BarPart, typeof(FileDropzoneProgressBar))]
[PseudoClasses(FailedPseudoClass, NarrowPseudoClass, TinyPseudoClass, ThumbPseudoClass)]
internal sealed class FileDropzoneRow : TemplatedControl
{
    public const string RetryPart = "PART_Retry";
    public const string RemovePart = "PART_Remove";
    public const string BarPart = "PART_Bar";
    public const string FailedPseudoClass = ":failed";
    public const string NarrowPseudoClass = ":narrow";
    public const string TinyPseudoClass = ":tiny";
    public const string ThumbPseudoClass = ":thumb";

    public static readonly StyledProperty<string> NameTextProperty = AvaloniaProperty.Register<FileDropzoneRow, string>(nameof(NameText), string.Empty);
    public static readonly StyledProperty<string> SizeTextProperty = AvaloniaProperty.Register<FileDropzoneRow, string>(nameof(SizeText), string.Empty);
    public static readonly StyledProperty<Geometry?> TypeIconProperty = AvaloniaProperty.Register<FileDropzoneRow, Geometry?>(nameof(TypeIcon));
    public static readonly StyledProperty<IImage?> PreviewProperty = AvaloniaProperty.Register<FileDropzoneRow, IImage?>(nameof(Preview));
    public static readonly StyledProperty<object?> PhaseContentProperty = AvaloniaProperty.Register<FileDropzoneRow, object?>(nameof(PhaseContent));
    public static readonly StyledProperty<bool> HasPhaseProperty = AvaloniaProperty.Register<FileDropzoneRow, bool>(nameof(HasPhase));
    public static readonly StyledProperty<bool> IsBarOpenProperty = AvaloniaProperty.Register<FileDropzoneRow, bool>(nameof(IsBarOpen));
    public static readonly StyledProperty<bool> CanRetryProperty = AvaloniaProperty.Register<FileDropzoneRow, bool>(nameof(CanRetry));

    public string NameText { get => GetValue(NameTextProperty); set => SetValue(NameTextProperty, value); }
    public string SizeText { get => GetValue(SizeTextProperty); set => SetValue(SizeTextProperty, value); }
    public Geometry? TypeIcon { get => GetValue(TypeIconProperty); set => SetValue(TypeIconProperty, value); }
    public IImage? Preview { get => GetValue(PreviewProperty); set => SetValue(PreviewProperty, value); }
    public object? PhaseContent { get => GetValue(PhaseContentProperty); set => SetValue(PhaseContentProperty, value); }
    public bool HasPhase { get => GetValue(HasPhaseProperty); set => SetValue(HasPhaseProperty, value); }
    public bool IsBarOpen { get => GetValue(IsBarOpenProperty); set => SetValue(IsBarOpenProperty, value); }
    public bool CanRetry { get => GetValue(CanRetryProperty); set => SetValue(CanRetryProperty, value); }

    private static readonly double[] s_shake = { 0, -7, 6, -4, 3, -1.5, 0 };
    private static readonly Geometry s_checkCircle = ArcIcons.Parse("M12 2.5a9.5 9.5 0 1 1 0 19a9.5 9.5 0 1 1 0-19");
    private static readonly Geometry s_checkTick = ArcIcons.Parse("m8.2 12.4 2.6 2.6 5-5.2");

    private readonly MotionValue<double> _progress;
    private readonly MotionValue<double> _shakeClock;
    private readonly TranslateTransform _shakeTransform = new();
    private FileDropzoneProgressBar? _bar;
    private FileDropzoneActionButton? _retry;
    private FileDropzoneActionButton? _remove;
    private TextBlock? _percent;
    private string? _phaseKey;
    private bool _filled;
    private FileDropzoneStatus? _shakenFor;
    private TimeSpan _arrivalDelay;

    public FileDropzoneRow(FileDropzoneItem item, bool fresh, bool canRetry, TimeSpan arrivalDelay)
    {
        Item = item;
        CanRetryUploads = canRetry;
        _arrivalDelay = arrivalDelay;
        NameText = item.Name;
        SizeText = FileDropzoneItem.FormatSize(item.Size);
        TypeIcon = TypeIconFor(item.Name);
        Preview = item.Preview;
        PseudoClasses.Set(ThumbPseudoClass, item.Preview is not null);
        _filled = item.Status != FileDropzoneStatus.Uploading;
        _shakenFor = fresh ? null : item.Status;
        _progress = new MotionValue<double>(this, item.Status == FileDropzoneStatus.Uploaded ? 100 : item.Progress, MotionVectors.Double);
        _progress.Changed += OnProgress;
        _shakeClock = new MotionValue<double>(this, 1, MotionVectors.Double);
        _shakeClock.Changed += t => _shakeTransform.X = ShakeAt(t);
        item.PropertyChanged += OnItemChanged;
        AutomationProperties.SetName(this, item.Name);
        Update(initial: true);
    }

    public FileDropzoneItem Item { get; }

    /// <summary>True when the dropzone has an upload handler (Arc only offers Retry then).</summary>
    public bool CanRetryUploads { get; set; }

    public FileDropzoneActionButton? RetryButton => _retry;

    public FileDropzoneActionButton? RemoveButton => _remove;

    public event EventHandler? RemoveRequested;

    public event EventHandler? RetryRequested;

    public void Detach() => Item.PropertyChanged -= OnItemChanged;

    internal static Geometry TypeIconFor(string name)
    {
        var extension = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
        return extension switch
        {
            "png" or "jpg" or "jpeg" or "gif" or "webp" or "svg" or "avif" or "heic" => ArcIcons.FileImage,
            "mov" or "mp4" or "webm" or "m4v" or "avi" => ArcIcons.FilePlay,
            "zip" or "gz" or "tar" or "rar" or "7z" => ArcIcons.FileArchive,
            "pdf" or "md" or "txt" or "doc" or "docx" or "rtf" => ArcIcons.FileText,
            _ => ArcIcons.File
        };
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _bar = e.NameScope.Find<FileDropzoneProgressBar>(BarPart);
        _retry = e.NameScope.Find<FileDropzoneActionButton>(RetryPart);
        _remove = e.NameScope.Find<FileDropzoneActionButton>(RemovePart);
        if (e.NameScope.Find<Control>("PART_Frame") is { } frame)
        {
            frame.RenderTransform = _shakeTransform;
        }
        if (_retry is not null)
        {
            _retry.Click += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);
            AutomationProperties.SetName(_retry, $"Retry {Item.Name}");
            Tooltip.SetTip(_retry, "Retry");
        }
        if (_remove is not null)
        {
            _remove.Click += (_, _) => RemoveRequested?.Invoke(this, EventArgs.Empty);
            AutomationProperties.SetName(_remove, $"Remove {Item.Name}");
            Tooltip.SetTip(_remove, "Remove");
        }
        if (_bar is not null)
        {
            AutomationProperties.SetName(_bar, $"Uploading {Item.Name}");
            _bar.Fill = _progress.Value;
            _bar.Value = Math.Round(Target());
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        // Arc's container queries: under 440px Retry keeps only its icon, under 300px the type icon steps aside.
        PseudoClasses.Set(NarrowPseudoClass, e.NewSize.Width <= 440);
        PseudoClasses.Set(TinyPseudoClass, e.NewSize.Width <= 300);
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileDropzoneItem.Preview))
        {
            Preview = Item.Preview;
            PseudoClasses.Set(ThumbPseudoClass, Item.Preview is not null);
            return;
        }
        Update(initial: false);
    }

    private double Target() => Item.Status == FileDropzoneStatus.Uploaded ? 100 : Item.Progress;

    private bool Reduced => !ArcMotion.IsFull(this);

    private void Update(bool initial)
    {
        var status = Item.Status;
        if (status == FileDropzoneStatus.Uploading && _phaseKey is not "uploading" && !initial)
        {
            _filled = false;
        }
        PseudoClasses.Set(FailedPseudoClass, status == FileDropzoneStatus.Failed);
        CanRetry = status == FileDropzoneStatus.Failed && CanRetryUploads && Item.IsRetryable;
        var target = Target();
        if (_bar is not null)
        {
            _bar.Value = Math.Round(target);
        }
        if (status is FileDropzoneStatus.Uploading or FileDropzoneStatus.Uploaded)
        {
            // Every upload starts from an empty bar, even a retry that failed halfway.
            if (initial || Reduced || (status == FileDropzoneStatus.Uploading && target == 0))
            {
                _progress.Jump(target);
                if (status == FileDropzoneStatus.Uploaded)
                {
                    _filled = true;
                }
            }
            else
            {
                _ = SpringTo(target, status);
            }
        }
        RefreshPhase(initial);
        Shake(status);
    }

    private async Task SpringTo(double target, FileDropzoneStatus status)
    {
        var done = await _progress.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        if (done && status == FileDropzoneStatus.Uploaded && Item.Status == FileDropzoneStatus.Uploaded && !_filled)
        {
            _filled = true;
            RefreshPhase(initial: false);
        }
    }

    private void OnProgress(double value)
    {
        if (_bar is not null)
        {
            _bar.Fill = value;
        }
        if (_percent is not null)
        {
            var text = $"{Math.Round(Math.Clamp(value, 0, 100)).ToString(System.Globalization.CultureInfo.InvariantCulture)}%";
            if (_percent.Text != text)
            {
                _percent.Text = text;
            }
        }
        // The label turns as soon as the count reads 100%, without waiting out the spring's last fraction of a pixel.
        if (Item.Status == FileDropzoneStatus.Uploaded && value >= 99.5 && !_filled)
        {
            _filled = true;
            RefreshPhase(initial: false);
        }
    }

    /// <summary>A finished upload keeps its bar until the count reaches 100%, so 100% is seen before the label turns to Uploaded.</summary>
    private void RefreshPhase(bool initial)
    {
        var status = Item.Status;
        var phase = status == FileDropzoneStatus.Uploaded && !_filled && !Reduced ? FileDropzoneStatus.Uploading : status;
        IsBarOpen = phase == FileDropzoneStatus.Uploading;
        HasPhase = phase != FileDropzoneStatus.None;
        var key = phase switch
        {
            FileDropzoneStatus.Uploading => "uploading",
            FileDropzoneStatus.Uploaded => "uploaded",
            FileDropzoneStatus.Failed => "failed:" + Item.Error,
            _ => "none"
        };
        if (key == _phaseKey)
        {
            return;
        }
        _phaseKey = key;
        _percent = null;
        PhaseContent = phase switch
        {
            FileDropzoneStatus.Uploading => UploadingContent(),
            FileDropzoneStatus.Uploaded => UploadedContent(initial),
            FileDropzoneStatus.Failed => FailedContent(),
            _ => null
        };
    }

    private Control UploadingContent()
    {
        _percent = new TextBlock { Classes = { "percent" }, FontFeatures = new FontFeatureCollection { new FontFeature { Tag = "tnum", Value = 1 } } };
        OnProgress(_progress.Value);
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { new TextBlock { Text = "Uploading " }, _percent } };
    }

    private Control UploadedContent(bool initial)
    {
        var circle = new StrokeDrawGlyph { Data = s_checkCircle, Size = 14, StrokeThickness = 2.25 };
        var tick = new StrokeDrawGlyph { Data = s_checkTick, Size = 14, StrokeThickness = 2.25 };
        ArcTokens.Bind(circle, StrokeDrawGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Success);
        ArcTokens.Bind(tick, StrokeDrawGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Success);
        if (initial || Reduced)
        {
            circle.Progress = 1;
            tick.Progress = 1;
        }
        else
        {
            // The circle closes, then the tick draws through it.
            circle.Progress = 0;
            tick.Progress = 0;
            _ = circle.Play(TimeSpan.FromMilliseconds(336), ArcEasings.Enter);
            _ = tick.Play(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromMilliseconds(160));
        }
        var label = new TextBlock { Text = "Uploaded" };
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Children = { new Panel { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center, Children = { circle, tick } }, label }
        };
    }

    private Control FailedContent()
    {
        var reason = string.IsNullOrEmpty(Item.Error) ? "Upload failed" : Item.Error;
        var text = new TextBlock { Text = reason, TextTrimming = TextTrimming.CharacterEllipsis };
        Tooltip.SetTip(text, reason);
        var icon = new ArcGlyph { Data = ArcIcons.CircleAlert, Size = 14, StrokeThickness = 2.25, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(icon, ArcGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Danger);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Danger);
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            Children = { icon, text }
        };
        AutomationProperties.SetName(content, "Failed: " + reason);
        return content;
    }

    /// <summary>A failure shakes the row once: a file refused on arrival after it lands, an upload when it fails.</summary>
    private void Shake(FileDropzoneStatus status)
    {
        var previous = _shakenFor;
        if (previous == status)
        {
            return;
        }
        _shakenFor = status;
        if (status != FileDropzoneStatus.Failed || Reduced)
        {
            return;
        }
        var delay = previous is null ? _arrivalDelay + TimeSpan.FromMilliseconds(300) : TimeSpan.Zero;
        _shakeClock.Jump(0);
        _ = _shakeClock.AnimateTo(1, MotionSpec.Tween(TimeSpan.FromMilliseconds(420), new Avalonia.Animation.Easings.SplineEasing(0, 0, 0.58, 1), delay));
    }

    private static double ShakeAt(double t)
    {
        if (t <= 0 || t >= 1)
        {
            return 0;
        }
        var position = t * (s_shake.Length - 1);
        var index = (int)position;
        var fraction = position - index;
        return s_shake[index] + (s_shake[Math.Min(index + 1, s_shake.Length - 1)] - s_shake[index]) * fraction;
    }
}
