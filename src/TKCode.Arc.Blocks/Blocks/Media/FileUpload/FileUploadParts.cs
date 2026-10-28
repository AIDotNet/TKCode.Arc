// Ported from Arc's free file-upload block (MIT, Copyright (c) 2026 Elia Kuratli): file-upload.tsx, file-upload.module.css.
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The drop target: a focusable surface with a 1px dashed edge (Avalonia's <see cref="Border"/> draws solid edges only),
/// exposed to assistive technology as a button (Arc's <c>role="button"</c>) named by the label and described by the help
/// text. The owner handles the keys, the press and the drag events.
/// </summary>
internal sealed class FileUploadDropzone : Decorator
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<FileUploadDropzone>();

    public static readonly StyledProperty<IBrush?> BorderBrushProperty = Border.BorderBrushProperty.AddOwner<FileUploadDropzone>();

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty = Border.CornerRadiusProperty.AddOwner<FileUploadDropzone>();

    /// <summary>The dash and the gap of the edge, in px.</summary>
    public static readonly StyledProperty<double> DashLengthProperty =
        AvaloniaProperty.Register<FileUploadDropzone, double>(nameof(DashLength), 3);

    /// <summary>The keyboard focus ring's brush (drawn 2px outside, 2px wide, while focus is visible).</summary>
    public static readonly StyledProperty<IBrush?> FocusRingBrushProperty =
        AvaloniaProperty.Register<FileUploadDropzone, IBrush?>(nameof(FocusRingBrush));

    private Pen? _pen;
    private IBrush? _penBrush;
    private Pen? _ringPen;
    private IBrush? _ringBrush;

    static FileUploadDropzone()
    {
        AffectsRender<FileUploadDropzone>(BackgroundProperty, BorderBrushProperty, CornerRadiusProperty, DashLengthProperty,
            FocusRingBrushProperty);
        FocusableProperty.OverrideDefaultValue<FileUploadDropzone>(true);
        ClipToBoundsProperty.OverrideDefaultValue<FileUploadDropzone>(false);
    }

    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <inheritdoc cref="DashLengthProperty"/>
    public double DashLength { get => GetValue(DashLengthProperty); set => SetValue(DashLengthProperty, value); }

    /// <inheritdoc cref="FocusRingBrushProperty"/>
    public IBrush? FocusRingBrush { get => GetValue(FocusRingBrushProperty); set => SetValue(FocusRingBrushProperty, value); }

    /// <summary>Runs when assistive technology invokes the button.</summary>
    public Action? Invoked { get; set; }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        var radius = CornerRadius.TopLeft;
        var edge = bounds.Deflate(0.5);
        context.DrawRectangle(Background ?? Brushes.Transparent, null, new RoundedRect(bounds, radius));
        if (BorderBrush is { } brush)
        {
            if (_pen is null || !ReferenceEquals(_penBrush, brush) || _pen.DashStyle?.Dashes?[0] != DashLength)
            {
                _pen = new Pen(brush, 1, new ImmutableDashStyle([DashLength, DashLength], 0));
                _penBrush = brush;
            }
            context.DrawRectangle(null, _pen, new RoundedRect(edge, Math.Max(0, radius - 0.5)));
        }
        if (Classes.Contains(":focus-visible") && FocusRingBrush is { } ring)
        {
            if (_ringPen is null || !ReferenceEquals(_ringBrush, ring))
            {
                _ringPen = new Pen(ring, 2);
                _ringBrush = ring;
            }
            context.DrawRectangle(null, _ringPen, new RoundedRect(bounds.Inflate(3), radius + 3));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DropzoneAutomationPeer(this);

    private sealed class DropzoneAutomationPeer(FileUploadDropzone owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public void Invoke()
        {
            if (Owner.IsEffectivelyEnabled)
            {
                ((FileUploadDropzone)Owner).Invoked?.Invoke();
            }
        }
    }
}

/// <summary>
/// Opens and closes its child's height the way Arc's <c>height: 0 → "auto"</c> rows do: the height on a smooth spring and
/// the opacity on a tween (240ms enter curve in; 160ms standard out, both after <see cref="ExitDelay"/>). Reduced motion
/// appears at once and fades out in 120ms; Off is immediate. The first state, and every state before the host is loaded,
/// applies without motion (<c>initial={false}</c>).
/// </summary>
/// <remarks>Code-driven: the exit must keep the child laid out while it closes and report when it is gone.</remarks>
internal sealed class FileUploadCollapse : Decorator
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<FileUploadCollapse, bool>(nameof(IsOpen), true);

    /// <summary>False keeps the opacity at 1 (the status frame only animates its height).</summary>
    public static readonly StyledProperty<bool> AnimatesOpacityProperty =
        AvaloniaProperty.Register<FileUploadCollapse, bool>(nameof(AnimatesOpacity), true);

    /// <summary>Wait before closing (the progress bar holds 240ms at 100%).</summary>
    public static readonly StyledProperty<TimeSpan> ExitDelayProperty =
        AvaloniaProperty.Register<FileUploadCollapse, TimeSpan>(nameof(ExitDelay));

    private MotionValue<double>? _height;
    private MotionValue<double>? _opacity;
    private double _shown = 1;
    private int _run;
    private bool _pendingEnter;

    static FileUploadCollapse()
    {
        ClipToBoundsProperty.OverrideDefaultValue<FileUploadCollapse>(false);
    }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="AnimatesOpacityProperty"/>
    public bool AnimatesOpacity { get => GetValue(AnimatesOpacityProperty); set => SetValue(AnimatesOpacityProperty, value); }

    /// <inheritdoc cref="ExitDelayProperty"/>
    public TimeSpan ExitDelay { get => GetValue(ExitDelayProperty); set => SetValue(ExitDelayProperty, value); }

    /// <summary>The share of the child's height in use, 0 to 1 (tests read it).</summary>
    public double Shown => _shown;

    /// <summary>True while opening or closing.</summary>
    public bool IsMoving => _height?.IsAnimating == true || _opacity?.IsAnimating == true;

    /// <summary>Raised when a close finished.</summary>
    public event EventHandler? Closed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            Run(change.GetNewValue<bool>());
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!IsLoaded && !_pendingEnter)
        {
            SetShown(IsOpen ? 1 : 0);
            Opacity = IsOpen || !AnimatesOpacity ? 1 : 0;
        }
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_pendingEnter)
        {
            _pendingEnter = false;
            Enter();
        }
    }

    /// <summary>
    /// Opens from zero height even if already open (a row that was just added). A host that is not loaded yet starts
    /// closed and opens once it is.
    /// </summary>
    public void Enter(bool deferUntilLoaded = false)
    {
        if (deferUntilLoaded && !IsLoaded)
        {
            _pendingEnter = true;
            SetCurrentValue(IsOpenProperty, true);
            SetShown(0);
            Opacity = AnimatesOpacity ? 0 : 1;
            return;
        }
        if (!IsLoaded || ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full)
        {
            SetCurrentValue(IsOpenProperty, true);
            Stop();
            SetShown(1);
            Opacity = 1;
            return;
        }
        Stop();
        SetShown(0);
        Opacity = AnimatesOpacity ? 0 : 1;
        SetCurrentValue(IsOpenProperty, true);
        Run(true, force: true);
    }

    private void Stop()
    {
        _run++;
        _height?.Stop();
        _opacity?.Stop();
    }

    private void SetShown(double value)
    {
        _shown = Math.Max(0, value);
        ClipToBounds = _shown < 1 || IsMoving;
        InvalidateMeasure();
    }

    private void Run(bool open, bool force = false)
    {
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            Stop();
            SetShown(open ? 1 : 0);
            Opacity = open || !AnimatesOpacity ? 1 : 0;
            if (!open && IsLoaded)
            {
                Closed?.Invoke(this, EventArgs.Empty);
            }
            return;
        }
        _height ??= CreateHeight();
        _opacity ??= CreateOpacity();
        var run = ++_run;
        var full = level == ArcMotionLevel.Full;
        if (open)
        {
            if (!full)
            {
                // Reduced: no height motion and an instant fade in.
                _height.Jump(1);
                _opacity.Jump(1);
                SetShown(1);
                Opacity = 1;
                return;
            }
            if (!force && _shown >= 1 && Opacity >= 1)
            {
                return;
            }
            _ = _height.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
            if (AnimatesOpacity)
            {
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
            }
            return;
        }
        if (!full)
        {
            _height.Jump(_shown);
            var fade = AnimatesOpacity
                ? _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard))
                : Task.FromResult(true);
            _ = Finish(fade, run);
            return;
        }
        var delay = ExitDelay;
        var height = _height.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth, delay));
        if (AnimatesOpacity)
        {
            _ = _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard, delay));
        }
        _ = Finish(height, run);
    }

    private async Task Finish(Task<bool> motion, int run)
    {
        var reached = await motion;
        if (!reached || run != _run || IsOpen)
        {
            return;
        }
        _height?.Jump(0);
        SetShown(0);
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private MotionValue<double> CreateHeight()
    {
        var value = new MotionValue<double>(this, _shown, MotionVectors.Double);
        value.Changed += SetShown;
        return value;
    }

    private MotionValue<double> CreateOpacity()
    {
        var value = new MotionValue<double>(this, Opacity, MotionVectors.Double);
        value.Changed += v => Opacity = Math.Clamp(v, 0, 1);
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize);
        var size = child.DesiredSize;
        return new Size(size.Width, size.Height * Math.Min(_shown, 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // The child keeps its full height and is clipped, so it reveals from the top like Arc's overflow: hidden rows.
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, Child.DesiredSize.Height)));
        return finalSize;
    }
}

/// <summary>
/// One item of a row's status slot (the check or the Retry button), entering and leaving in place like Arc's
/// <c>AnimatePresence mode="popLayout"</c>: in from <see cref="EnterScale"/> (and <see cref="EnterBlur"/>) with opacity on a
/// snappy spring; out to <see cref="ExitScale"/> over <see cref="ExitDuration"/>. Reduced motion appears at once and
/// fades out in 120ms.
/// </summary>
internal sealed class FileUploadSlotItem : Decorator
{
    private readonly ScaleTransform _scale = new();
    private MotionValue<double>? _progress;
    private int _run;

    public FileUploadSlotItem()
    {
        RenderTransform = _scale;
        RenderTransformOrigin = RelativePoint.Center;
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
    }

    /// <summary>The scale it enters from (.8 for the check, .6 for Retry).</summary>
    public double EnterScale { get; init; } = 0.8;

    /// <summary>The blur it enters from (0 for the check, 2 for Retry).</summary>
    public double EnterBlur { get; init; }

    /// <summary>The scale it leaves to (1 for the check, which only fades; .6 for Retry).</summary>
    public double ExitScale { get; init; } = 1;

    /// <summary>The blur it leaves to.</summary>
    public double ExitBlur { get; init; }

    /// <summary>How long it takes to leave (120ms for the check, 160ms for Retry).</summary>
    public TimeSpan ExitDuration { get; init; } = ArcMotionPresets.Instant;

    /// <summary>True between <see cref="Leave"/> and its end.</summary>
    public bool IsLeaving { get; private set; }

    /// <summary>Shows it entering, or at rest when <paramref name="animate"/> is false or motion is off.</summary>
    public void Enter(bool animate)
    {
        _run++;
        IsLeaving = false;
        _progress?.Stop();
        if (animate && !IsLoaded)
        {
            // Just added to the slot: start hidden and pop in once laid out.
            _pendingEnter = true;
            Apply(0, EnterScale, EnterBlur);
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level != ArcMotionLevel.Full)
        {
            Apply(1, EnterScale, EnterBlur);
            return;
        }
        _progress ??= new MotionValue<double>(this, 0, MotionVectors.Double);
        _progress.Jump(0);
        Apply(0, EnterScale, EnterBlur);
        _progress.Changed -= OnEnterChanged;
        _progress.Changed -= OnExitChanged;
        _progress.Changed += OnEnterChanged;
        _ = _progress.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
    }

    /// <summary>Plays the exit and then runs <paramref name="removed"/>; at once when motion is off.</summary>
    public async void Leave(Action removed)
    {
        var run = ++_run;
        IsLeaving = true;
        IsHitTestVisible = false;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            removed();
            return;
        }
        var full = level == ArcMotionLevel.Full;
        _progress ??= new MotionValue<double>(this, 1, MotionVectors.Double);
        _progress.Stop();
        _progress.Changed -= OnEnterChanged;
        _progress.Changed -= OnExitChanged;
        _exitFromOpacity = Opacity;
        _exitFull = full;
        _progress.Jump(0);
        _progress.Changed += OnExitChanged;
        var duration = full ? ExitDuration : ArcMotionPresets.Instant;
        await _progress.AnimateTo(1, MotionSpec.Tween(duration, ArcEasings.Standard));
        if (run == _run)
        {
            removed();
        }
    }

    private double _exitFromOpacity = 1;
    private bool _exitFull;
    private bool _pendingEnter;

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_pendingEnter && !IsLeaving)
        {
            _pendingEnter = false;
            Enter(animate: true);
        }
    }

    private void OnEnterChanged(double p) => Apply(p, EnterScale + (1 - EnterScale) * p, EnterBlur * (1 - p));

    private void OnExitChanged(double p)
    {
        var scale = _exitFull ? 1 + (ExitScale - 1) * p : 1;
        var blur = _exitFull ? ExitBlur * p : 0;
        Opacity = Math.Clamp(_exitFromOpacity * (1 - p), 0, 1);
        _scale.ScaleX = scale;
        _scale.ScaleY = scale;
        Effect = ArcBlur.Get(blur);
    }

    private void Apply(double opacity, double scale, double blur)
    {
        Opacity = Math.Clamp(opacity, 0, 1);
        var s = opacity >= 1 ? 1 : scale;
        _scale.ScaleX = s;
        _scale.ScaleY = s;
        Effect = opacity >= 1 ? null : ArcBlur.Get(blur);
    }
}

/// <summary>
/// The 26px round Retry and Remove buttons: transparent with a muted glyph; a mouse or pen over it fills it with the surface
/// and darkens the glyph. Pressing scales it to .96 quickly and releases on Arc's CSS spring.
/// </summary>
internal sealed class FileUploadIconButton : AvaloniaButton
{
    /// <summary>The glyph's geometry (24×24 Lucide).</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<FileUploadIconButton, Geometry?>(nameof(Glyph));

    /// <summary>The glyph's size in px.</summary>
    public static readonly StyledProperty<double> GlyphSizeProperty =
        AvaloniaProperty.Register<FileUploadIconButton, double>(nameof(GlyphSize), 15);

    /// <summary>The glyph's stroke width in the 24px grid.</summary>
    public static readonly StyledProperty<double> GlyphStrokeProperty =
        AvaloniaProperty.Register<FileUploadIconButton, double>(nameof(GlyphStroke), 2);

    protected override Type StyleKeyOverride => typeof(FileUploadIconButton);

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    public double GlyphSize { get => GetValue(GlyphSizeProperty); set => SetValue(GlyphSizeProperty, value); }

    public double GlyphStroke { get => GetValue(GlyphStrokeProperty); set => SetValue(GlyphStrokeProperty, value); }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover lives under (hover: hover) and (pointer: fine): touch never hovers.
        PseudoClasses.Set(":finehover", e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":finehover", false);
    }
}
