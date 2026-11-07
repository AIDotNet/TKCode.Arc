using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One piece of a <see cref="SkeletonMorph"/> layout (Arc Skeleton morph, design reference). While its region loads it
/// is a skeleton of the given size and its content is not shown; when the region resolves, the content mounts in place,
/// the skeleton stretches from its own box to the content's measured box on a critically damped spring while it fades,
/// and the content sharpens in out of a 4 px blur. Give text skeletons the text's line height and line count
/// (<see cref="Lines"/>, <see cref="LineHeight"/>) and set <see cref="SkeletonWidth"/> near the typical text width.
/// A block outside a <see cref="SkeletonMorph"/> simply shows its content.
/// </summary>
[TemplatePart(ContentPresenterPart, typeof(ContentPresenter))]
[TemplatePart(PlaceholderPart, typeof(MorphBlockPlaceholder))]
[PseudoClasses(SkeletonPseudoClass)]
public class MorphBlock : ContentControl
{
    public const string ContentPresenterPart = "PART_ContentPresenter";
    public const string PlaceholderPart = "PART_Placeholder";
    public const string SkeletonPseudoClass = ":skeleton";

    /// <summary>
    /// Skeleton width in pixels. NaN (default) fills the available width (times <see cref="SkeletonWidthRatio"/>), and a
    /// filling block also takes the full width once loaded.
    /// </summary>
    public static readonly StyledProperty<double> SkeletonWidthProperty =
        AvaloniaProperty.Register<MorphBlock, double>(nameof(SkeletonWidth), double.NaN);

    /// <summary>The fraction of the available width a filling skeleton (<see cref="SkeletonWidth"/> NaN) covers; 1 by default.</summary>
    public static readonly StyledProperty<double> SkeletonWidthRatioProperty =
        AvaloniaProperty.Register<MorphBlock, double>(nameof(SkeletonWidthRatio), 1);

    /// <summary>
    /// Skeleton height in pixels. NaN (default) is <see cref="Lines"/> × <see cref="LineHeight"/> for a text skeleton,
    /// the width for a circle with a set width, and 12 otherwise.
    /// </summary>
    public static readonly StyledProperty<double> SkeletonHeightProperty =
        AvaloniaProperty.Register<MorphBlock, double>(nameof(SkeletonHeight), double.NaN);

    /// <summary>Corner radius of a shape skeleton in pixels (6 by default; clamped to half the short side).</summary>
    public static readonly StyledProperty<double> SkeletonRadiusProperty =
        AvaloniaProperty.Register<MorphBlock, double>(nameof(SkeletonRadius), 6);

    /// <summary>Makes the skeleton round (an avatar placeholder): its radius is half the short side.</summary>
    public static readonly StyledProperty<bool> IsCircleProperty =
        AvaloniaProperty.Register<MorphBlock, bool>(nameof(IsCircle));

    /// <summary>
    /// Shows a text skeleton with this many lines: one bar centred in each line box, the last of several shorter. 0
    /// (default) shows a filled shape instead. Use 1 for single-line text.
    /// </summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<MorphBlock, int>(nameof(Lines), 0, coerce: static (_, v) => Math.Max(0, v));

    /// <summary>Line height of the loaded text in pixels (20 by default), so the skeleton is exactly as tall as the text.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<MorphBlock, double>(nameof(LineHeight), 20);

    /// <summary>True once the content is shown (mounted); false while the block is a skeleton or waiting for its turn.</summary>
    public static readonly DirectProperty<MorphBlock, bool> IsResolvedProperty =
        AvaloniaProperty.RegisterDirect<MorphBlock, bool>(nameof(IsResolved), o => o.IsResolved);

    /// <inheritdoc cref="SkeletonWidthProperty"/>
    public double SkeletonWidth
    {
        get => GetValue(SkeletonWidthProperty);
        set => SetValue(SkeletonWidthProperty, value);
    }

    /// <inheritdoc cref="SkeletonWidthRatioProperty"/>
    public double SkeletonWidthRatio
    {
        get => GetValue(SkeletonWidthRatioProperty);
        set => SetValue(SkeletonWidthRatioProperty, value);
    }

    /// <inheritdoc cref="SkeletonHeightProperty"/>
    public double SkeletonHeight
    {
        get => GetValue(SkeletonHeightProperty);
        set => SetValue(SkeletonHeightProperty, value);
    }

    /// <inheritdoc cref="SkeletonRadiusProperty"/>
    public double SkeletonRadius
    {
        get => GetValue(SkeletonRadiusProperty);
        set => SetValue(SkeletonRadiusProperty, value);
    }

    /// <inheritdoc cref="IsCircleProperty"/>
    public bool IsCircle
    {
        get => GetValue(IsCircleProperty);
        set => SetValue(IsCircleProperty, value);
    }

    /// <inheritdoc cref="LinesProperty"/>
    public int Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    private bool _isResolved;

    /// <inheritdoc cref="IsResolvedProperty"/>
    public bool IsResolved
    {
        get => _isResolved;
        private set => SetAndRaise(IsResolvedProperty, ref _isResolved, value);
    }

    /// <summary>The blur the content sharpens out of (theme: MorphBlockToken.RevealBlur).</summary>
    internal static readonly StyledProperty<double> RevealBlurProperty =
        AvaloniaProperty.Register<MorphBlock, double>(nameof(RevealBlur), 4);

    internal double RevealBlur
    {
        get => GetValue(RevealBlurProperty);
        set => SetValue(RevealBlurProperty, value);
    }

    private ContentPresenter? _presenter;
    private MorphBlockPlaceholder? _placeholder;
    private SkeletonMorph? _owner;
    private bool _mounted;
    private bool _awaitMount;
    private bool _stretchPending;
    private bool _blurs;
    private int _generation;
    private double _pulse = 1;
    private double _fadeValue = 1;
    private Size _skeletonBox;
    // Built the first time a block resolves with motion; most blocks of a static page never need them.
    private MotionValue<double>? _reveal;
    private MotionValue<double>? _fade;
    private MotionValue<Size>? _box;

    static MorphBlock()
    {
        FocusableProperty.OverrideDefaultValue<MorphBlock>(false);
        ClipToBoundsProperty.OverrideDefaultValue<MorphBlock>(false);
        HorizontalAlignmentProperty.OverrideDefaultValue<MorphBlock>(HorizontalAlignment.Left);
        VerticalAlignmentProperty.OverrideDefaultValue<MorphBlock>(VerticalAlignment.Top);
        AffectsMeasure<MorphBlock>(SkeletonWidthProperty, SkeletonWidthRatioProperty, SkeletonHeightProperty, LinesProperty,
            LineHeightProperty, IsCircleProperty);
    }

    /// <summary>The region this block belongs to (the nearest <see cref="SkeletonMorph"/> above it), if any.</summary>
    internal SkeletonMorph? Owner => _owner;

    /// <summary>True while the block shows its skeleton and has not mounted its content (it then breathes with the pulse).</summary>
    internal bool WantsPulse => !_mounted;

    internal MorphBlockPlaceholder? Placeholder => _placeholder;

    internal ContentPresenter? ContentHost => _presenter;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _presenter = e.NameScope.Find<ContentPresenter>(ContentPresenterPart);
        _placeholder = e.NameScope.Find<MorphBlockPlaceholder>(PlaceholderPart);
        UpdateShape();
        ApplyMountedContent();
        ApplyPlaceholder();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _owner = this.FindAncestorOfType<SkeletonMorph>();
        _owner?.Register(this);
        if (_owner is null)
        {
            MountInstant();
        }
        else if (_owner.IsLoading)
        {
            ShowSkeleton(_owner.CurrentPulse);
        }
        else if (!_mounted)
        {
            // A block that appears while its region resolves (inside a block that just mounted) joins the resolve;
            // otherwise it simply shows its content.
            if (_owner.IsResolving)
            {
                Resolve(TimeSpan.Zero, ArcMotion.GetEffectiveLevel(this), -1);
            }
            else
            {
                MountInstant();
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _owner?.Unregister(this);
        _owner = null;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == ContentTemplateProperty)
        {
            if (_mounted)
            {
                ApplyMountedContent();
            }
        }
        else if (change.Property == LinesProperty || change.Property == LineHeightProperty
                 || change.Property == SkeletonRadiusProperty || change.Property == IsCircleProperty)
        {
            UpdateShape();
        }
    }

    // ---- State changes, driven by the owning SkeletonMorph ----

    /// <summary>Back to the skeleton, at once: content unmounted, skeleton box, full opacity (Arc: no transition).</summary>
    internal void ShowSkeleton(double pulse)
    {
        _generation++;
        _awaitMount = false;
        _stretchPending = false;
        _pulse = pulse;
        _fadeValue = 1;
        _reveal?.Stop();
        _fade?.Stop();
        _box?.Stop();
        if (_mounted)
        {
            _mounted = false;
            ApplyMountedContent();
            InvalidateMeasure();
        }
        IsResolved = false;
        PseudoClasses.Set(SkeletonPseudoClass, true);
        if (_presenter is { } presenter)
        {
            presenter.Opacity = 0;
            presenter.Effect = null;
            _blurStep = 0;
        }
        ApplyPlaceholder();
    }

    /// <summary>The breath of the region's pulse; only skeletons that have not mounted follow it.</summary>
    internal void SetPulse(double pulse)
    {
        if (_mounted || _pulse.Equals(pulse))
        {
            return;
        }
        _pulse = pulse;
        ApplyPlaceholder();
    }

    /// <summary>
    /// Resolves after <paramref name="delay"/>: the content mounts, sharpens in and the skeleton stretches to it and fades
    /// (Full); a 120 ms crossfade (Reduced); at once (Off). Reports to the owner when done.
    /// </summary>
    internal void Resolve(TimeSpan delay, ArcMotionLevel level, int ownerGeneration)
    {
        if (_mounted)
        {
            _owner?.OnBlockSettled(ownerGeneration);
            return;
        }
        if (level == ArcMotionLevel.Off || !this.IsAttachedToVisualTree())
        {
            MountInstant();
            _owner?.OnBlockSettled(ownerGeneration);
            return;
        }
        var generation = ++_generation;
        _blurs = level == ArcMotionLevel.Full;
        var full = level == ArcMotionLevel.Full;
        _reveal ??= CreateReveal();
        _fade ??= CreateFade();
        _reveal.Jump(0);
        _fade.Jump(1);
        // The first frame of the reveal (after the stagger delay) mounts the content: see CreateReveal.
        _awaitMount = true;
        var revealSpec = full
            ? MotionSpec.Tween(SkeletonMorphMath.RevealDuration, ArcEasings.Standard, delay)
            : MotionSpec.Tween(SkeletonMorphMath.ReducedCrossfade, ArcEasings.Standard, delay);
        var fadeSpec = full
            ? MotionSpec.Tween(SkeletonMorphMath.FadeDuration, ArcEasings.Standard, delay + SkeletonMorphMath.FadeDelay)
            : MotionSpec.Tween(SkeletonMorphMath.ReducedCrossfade, SkeletonMorphMath.ReducedFadeEasing, delay);
        var reveal = _reveal.AnimateTo(1, revealSpec);
        var fade = _fade.AnimateTo(0, fadeSpec);
        _ = ReportWhenSettled(reveal, fade, generation, ownerGeneration);
    }

    private async Task ReportWhenSettled(Task<bool> reveal, Task<bool> fade, int generation, int ownerGeneration)
    {
        var revealed = await reveal;
        var faded = await fade;
        if (revealed && faded && generation == _generation)
        {
            _owner?.OnBlockSettled(ownerGeneration);
        }
    }

    private MotionValue<double> CreateReveal()
    {
        // Code-driven: the content mounts on the first frame after the block's stagger delay, which a ControlTheme
        // transition cannot express, and the blur follows the opacity frame by frame (blur = 4 · (1 − opacity)).
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            if (_awaitMount)
            {
                Mount();
            }
            if (_mounted && _presenter is { } presenter)
            {
                presenter.Opacity = v;
                SetBlur(presenter, _blurs ? SkeletonMorphMath.RevealBlur(v, RevealBlur) : 0);
            }
        };
        return value;
    }

    // The blur is an offscreen layer per block: it changes only in 0.5 px steps (8 steps from 4 px instead of 16) and is
    // dropped below 0.3 px, where it is no longer visible, so a resolving card rebuilds few layers.
    private double _blurStep;

    private void SetBlur(ContentPresenter presenter, double radius)
    {
        var step = SkeletonMorphMath.BlurStep(radius);
        if (step.Equals(_blurStep) && (step > 0) == (presenter.Effect is not null))
        {
            return;
        }
        _blurStep = step;
        presenter.Effect = step > 0 ? ArcBlur.Get(step) : null;
    }

    private MotionValue<double> CreateFade()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += v =>
        {
            _fadeValue = v;
            ApplyPlaceholder();
        };
        return value;
    }

    private MotionValue<Size> CreateBox(Size initial)
    {
        // Code-driven: the stretch starts from the skeleton box to a box only known after the content is measured, and
        // keeps its velocity if the content box changes again while it runs.
        var value = new MotionValue<Size>(this, initial, MotionVectors.Size);
        value.Changed += v => _placeholder?.SetBox(v);
        return value;
    }

    private void Mount()
    {
        _awaitMount = false;
        _mounted = true;
        // The skeleton stops breathing the moment its content mounts (Arc removes the pulse: the fade starts from 1).
        _pulse = 1;
        _stretchPending = true;
        ApplyMountedContent();
        if (_presenter is { } presenter)
        {
            presenter.Opacity = 0;
            SetBlur(presenter, _blurs ? RevealBlur : 0);
        }
        PseudoClasses.Set(SkeletonPseudoClass, false);
        IsResolved = true;
        ApplyPlaceholder();
        InvalidateMeasure();
    }

    private void MountInstant()
    {
        _generation++;
        _awaitMount = false;
        _stretchPending = false;
        _reveal?.Stop();
        _fade?.Stop();
        _box?.Stop();
        _pulse = 1;
        _fadeValue = 0;
        if (!_mounted)
        {
            _mounted = true;
            ApplyMountedContent();
            InvalidateMeasure();
        }
        if (_presenter is { } presenter)
        {
            presenter.Opacity = 1;
            presenter.Effect = null;
            _blurStep = 0;
        }
        PseudoClasses.Set(SkeletonPseudoClass, false);
        IsResolved = true;
        ApplyPlaceholder();
    }

    private void ApplyMountedContent()
    {
        if (_presenter is not { } presenter)
        {
            return;
        }
        // Content first, then the template; clearing goes the other way (a mounted presenter rebuilds on every change).
        if (_mounted && Content is { } content)
        {
            presenter.Content = content;
            presenter.ContentTemplate = ContentTemplate;
            presenter.IsVisible = true;
        }
        else
        {
            presenter.ContentTemplate = null;
            presenter.Content = null;
            presenter.IsVisible = _mounted;
        }
    }

    private void ApplyPlaceholder()
    {
        if (_placeholder is not { } placeholder)
        {
            return;
        }
        var opacity = _mounted ? _fadeValue : _pulse * _fadeValue;
        placeholder.Opacity = opacity;
        placeholder.IsVisible = opacity > 0;
    }

    private void UpdateShape() => _placeholder?.SetShape(Lines, LineHeight, SkeletonRadius, IsCircle);

    // ---- Layout ----

    private bool Fills => double.IsNaN(SkeletonWidth);

    private Size Skeleton(double availableWidth)
        => SkeletonMorphMath.SkeletonSize(SkeletonWidth, 1, SkeletonHeight, Lines, LineHeight, IsCircle, availableWidth);

    protected override Size MeasureOverride(Size availableSize)
    {
        var root = VisualChildren.Count > 0 ? VisualChildren[0] as Layoutable : null;
        if (!_mounted)
        {
            // While loading the block is exactly its skeleton box; a filling one takes the available width.
            var skeleton = Skeleton(availableSize.Width);
            root?.Measure(skeleton);
            return skeleton;
        }
        if (root is null)
        {
            return default;
        }
        root.Measure(availableSize);
        var desired = root.DesiredSize;
        if (Fills && !double.IsInfinity(availableSize.Width))
        {
            desired = desired.WithWidth(Math.Max(desired.Width, availableSize.Width));
        }
        return desired;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var root = VisualChildren.Count > 0 ? VisualChildren[0] as Layoutable : null;
        root?.Arrange(new Rect(finalSize));
        if (!_mounted)
        {
            var skeleton = Skeleton(finalSize.Width);
            _skeletonBox = Fills ? new Size(finalSize.Width * Math.Clamp(SkeletonWidthRatio, 0, 1), skeleton.Height) : skeleton;
            _box?.Jump(_skeletonBox);
            _placeholder?.SetBox(_skeletonBox);
            return finalSize;
        }
        var target = finalSize;
        if (_stretchPending)
        {
            // The content's measured box is known now: stretch the skeleton from its own box to it (no frame at the
            // content size first, unlike Arc).
            _stretchPending = false;
            _box ??= CreateBox(_skeletonBox);
            _box.Jump(_skeletonBox);
            _ = _box.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
        }
        else if (_box is { } box && box.Target != target)
        {
            // The content box moved: keep the stretch's velocity while the skeleton is still visible, otherwise follow.
            if (_fadeValue > 0 && box.IsAnimating)
            {
                _ = box.AnimateTo(target, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                box.Jump(target);
            }
        }
        else if (_box is null)
        {
            _placeholder?.SetBox(target);
        }
        return finalSize;
    }
}
