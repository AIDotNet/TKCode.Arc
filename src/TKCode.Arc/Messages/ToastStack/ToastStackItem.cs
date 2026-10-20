using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Where one toast of a <see cref="ToastStack"/> should be (relative to the bottom of the list).</summary>
internal readonly record struct ToastStackTarget(double Y, double Scale, double Height, double Opacity, double Content);

/// <summary>A queued toast of a <see cref="ToastStack"/>.</summary>
internal sealed class ToastStackRecord
{
    public required string Id { get; init; }
    public ToastKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ToastAction? Action { get; set; }
    public TimeSpan? Duration { get; set; }
    public long Seq { get; init; }
    public int Version { get; set; }
}

/// <summary>
/// One toast card of a <see cref="ToastStack"/>. Its geometry (lift, depth scale, card height, opacity, swipe) lives in
/// motion values that only touch its render transform, opacity and the card it draws itself, so following the stack,
/// a finger or a timer never relayouts the page. The card (fill, border, shadow) is drawn in <see cref="Render"/>
/// at its animated height; the content is clipped to it while it is shorter than the content.
/// </summary>
[TemplatePart(ContentPart, typeof(Control))]
[TemplatePart(ActionPart, typeof(AvaloniaButton))]
[TemplatePart(ClosePart, typeof(AvaloniaButton))]
[PseudoClasses(FrontPseudoClass, ExpandedPseudoClass, TuckedPseudoClass, DraggingPseudoClass, NarrowPseudoClass)]
internal sealed class ToastStackItem : TemplatedControl
{
    public const string ContentPart = "PART_Content";
    public const string ActionPart = "PART_Action";
    public const string ClosePart = "PART_Close";
    public const string FrontPseudoClass = ":front";
    public const string ExpandedPseudoClass = ":expanded";
    public const string TuckedPseudoClass = ":tucked";
    public const string DraggingPseudoClass = ":dragging";
    public const string NarrowPseudoClass = ":narrow";

    /// <summary>A swipe this far right has faded most of the way out.</summary>
    private const double SwipeFade = 280;

    /// <summary>Below this card width the action drops under the copy (21rem).</summary>
    private const double NarrowWidth = 336;

    public static readonly StyledProperty<ToastKind> KindProperty =
        AvaloniaProperty.Register<ToastStackItem, ToastKind>(nameof(Kind), ToastKind.Info);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ToastStackItem, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ToastStackItem, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ActionLabelProperty =
        AvaloniaProperty.Register<ToastStackItem, string?>(nameof(ActionLabel));

    public static readonly StyledProperty<bool> HasActionProperty =
        AvaloniaProperty.Register<ToastStackItem, bool>(nameof(HasAction));

    public static readonly DirectProperty<ToastStackItem, Geometry?> GlyphProperty =
        AvaloniaProperty.RegisterDirect<ToastStackItem, Geometry?>(nameof(Glyph), o => o.Glyph);

    public static readonly DirectProperty<ToastStackItem, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<ToastStackItem, bool>(nameof(IsLoading), o => o.IsLoading);

    public static readonly DirectProperty<ToastStackItem, Thickness> DescriptionMarginProperty =
        AvaloniaProperty.RegisterDirect<ToastStackItem, Thickness>(nameof(DescriptionMargin), o => o.DescriptionMargin);

    public static readonly StyledProperty<IBrush?> CardBackgroundProperty =
        AvaloniaProperty.Register<ToastStackItem, IBrush?>(nameof(CardBackground));

    public static readonly StyledProperty<IBrush?> CardBorderBrushProperty =
        AvaloniaProperty.Register<ToastStackItem, IBrush?>(nameof(CardBorderBrush));

    public static readonly StyledProperty<BoxShadows> CardShadowProperty =
        AvaloniaProperty.Register<ToastStackItem, BoxShadows>(nameof(CardShadow));

    public static readonly StyledProperty<CornerRadius> CardRadiusProperty =
        AvaloniaProperty.Register<ToastStackItem, CornerRadius>(nameof(CardRadius), new CornerRadius(26));

    public ToastKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }
    public string? ActionLabel { get => GetValue(ActionLabelProperty); set => SetValue(ActionLabelProperty, value); }
    public bool HasAction { get => GetValue(HasActionProperty); set => SetValue(HasActionProperty, value); }
    public IBrush? CardBackground { get => GetValue(CardBackgroundProperty); set => SetValue(CardBackgroundProperty, value); }
    public IBrush? CardBorderBrush { get => GetValue(CardBorderBrushProperty); set => SetValue(CardBorderBrushProperty, value); }
    public BoxShadows CardShadow { get => GetValue(CardShadowProperty); set => SetValue(CardShadowProperty, value); }
    public CornerRadius CardRadius { get => GetValue(CardRadiusProperty); set => SetValue(CardRadiusProperty, value); }

    public Geometry? Glyph
    {
        get => _glyph;
        private set => SetAndRaise(GlyphProperty, ref _glyph, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    public Thickness DescriptionMargin
    {
        get => _descriptionMargin;
        private set => SetAndRaise(DescriptionMarginProperty, ref _descriptionMargin, value);
    }

    private readonly ToastStack _owner;
    private readonly MatrixTransform _transform = new();
    private readonly TranslateTransform _contentShift = new();
    private readonly RectangleGeometry _contentClip = new();
    private readonly ToastSwipeTracker _swipe = new();
    private readonly MotionValue<double> _y;
    private readonly MotionValue<double> _scale;
    private readonly MotionValue<double> _height;
    private readonly MotionValue<double> _opacity;
    private readonly MotionValue<double> _content;
    private readonly MotionValue<double> _x;
    private Geometry? _glyph = ToastGlyphs.For(ToastKind.Info);
    private bool _isLoading;
    private Thickness _descriptionMargin;
    private Control? _contentRoot;
    private AvaloniaButton? _action;
    private AvaloniaButton? _close;
    private PausableTimer? _timer;
    private bool? _appliedExpanded;
    private ToastStackTarget _applied;
    private double _naturalHeight;
    private bool _suppressClick;
    private bool _swiped;
    private bool _inert;

    public ToastStackItem(ToastStack owner, ToastStackRecord record)
    {
        _owner = owner;
        Record = record;
        RenderTransform = _transform;
        RenderTransformOrigin = RelativePoint.TopLeft;
        _y = CreateValue(0);
        _scale = CreateValue(1);
        _height = CreateValue(0);
        _opacity = CreateValue(0);
        _content = CreateValue(1);
        _x = CreateValue(0);
        _content.Changed += v =>
        {
            if (_contentRoot is not null)
            {
                _contentRoot.Opacity = Math.Clamp(v, 0, 1);
            }
        };
        Opacity = 0;
        ApplyRecord();
    }

    public ToastStackRecord Record { get; }

    public string Id => Record.Id;

    /// <summary>False once dismissed: the toast is playing its exit.</summary>
    public bool IsPresent { get; private set; } = true;

    public bool IsDragging => _swipe.IsDragging;

    /// <summary>Content height plus the card border, measured at the list width.</summary>
    public double NaturalHeight => _naturalHeight;

    internal double OffsetX => _x.Value;

    internal double LiftY => _y.Value;

    internal double DepthScale => _scale.Value;

    internal double CardHeight => _height.Value;

    internal double ContentOpacity => _content.Value;

    internal bool IsInert => _inert;

    internal AvaloniaButton? ActionButton => _action;

    internal AvaloniaButton? CloseButton => _close;

    internal TimeSpan? TimerRemaining => _timer is { } timer ? timer.Remaining : null;

    internal bool IsTimerRunning => _timer?.IsRunning ?? false;

    static ToastStackItem()
    {
        AffectsRender<ToastStackItem>(CardBackgroundProperty, CardBorderBrushProperty, CardShadowProperty, CardRadiusProperty);
        FocusableProperty.OverrideDefaultValue<ToastStackItem>(false);
    }

    private MotionValue<double> CreateValue(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += _ => ApplyGeometry();
        return value;
    }

    /// <summary>Copies the record into the bound properties (an update morphs the card in place).</summary>
    public void ApplyRecord()
    {
        Kind = Record.Kind;
        Glyph = ToastGlyphs.For(Record.Kind);
        IsLoading = Record.Kind == ToastKind.Loading;
        Title = Record.Title;
        Description = Record.Description;
        DescriptionMargin = string.IsNullOrEmpty(Record.Description) ? default : new Thickness(0, 2, 0, 0);
        if (Record.Action is { } action)
        {
            ActionLabel = action.Label;
        }
        HasAction = Record.Action is not null;
        AutomationProperties.SetName(this, $"{ToastGlyphs.LabelFor(Record.Kind)}: {Record.Title}");
        AutomationProperties.SetHelpText(this, Record.Description ?? string.Empty);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnActionClick;
        }
        if (_close is not null)
        {
            _close.Click -= OnCloseClick;
        }
        _contentRoot = e.NameScope.Find<Control>(ContentPart);
        _action = e.NameScope.Find<AvaloniaButton>(ActionPart);
        _close = e.NameScope.Find<AvaloniaButton>(ClosePart);
        if (_action is not null)
        {
            _action.Click += OnActionClick;
        }
        if (_close is not null)
        {
            _close.Click += OnCloseClick;
            AutomationProperties.SetName(_close, "Dismiss notification");
        }
        if (_contentRoot is not null)
        {
            _contentRoot.RenderTransform = _contentShift;
            _contentRoot.Opacity = Math.Clamp(_content.Value, 0, 1);
        }
        SetInert(_inert);
        ApplyGeometry();
    }

    #region 布局与绘制

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize.WithHeight(double.PositiveInfinity));
        _naturalHeight = size.Height;
        var width = double.IsInfinity(availableSize.Width) ? size.Width : availableSize.Width;
        PseudoClasses.Set(NarrowPseudoClass, width < NarrowWidth);
        return new Size(width, size.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        _arranged = finalSize;
        ApplyGeometry();
        return result;
    }

    /// <summary>The last arranged size (Bounds lags behind during ArrangeOverride).</summary>
    private Size _arranged;

    /// <summary>Writes the motion state to the transform, opacity and content clip (no layout).</summary>
    private void ApplyGeometry()
    {
        var width = _arranged.Width;
        var natural = _arranged.Height;
        var h = Math.Max(0, _height.Value);
        var top = natural - h;
        var s = _scale.Value;
        var x = _x.Value;
        // Card x is inside the item; the item scales from the card's top edge, then lifts by y.
        var origin = new Point(width / 2, top);
        _transform.Matrix = Matrix.CreateTranslation(x, 0) *
                            Matrix.CreateTranslation(-origin.X, -origin.Y) *
                            Matrix.CreateScale(s, s) *
                            Matrix.CreateTranslation(origin.X, origin.Y) *
                            Matrix.CreateTranslation(0, _y.Value);
        var fade = x > 0 ? 1 - Math.Min(x / SwipeFade, 1) * 0.75 : 1;
        Opacity = Math.Clamp(_opacity.Value, 0, 1) * fade;
        if (_contentRoot is not null)
        {
            _contentShift.Y = top;
            if (h < natural - 0.5)
            {
                var margin = _contentRoot.Margin;
                _contentClip.Rect = new Rect(-margin.Left, 0, width, Math.Max(0, h - margin.Top - margin.Bottom));
                _contentRoot.Clip = _contentClip;
            }
            else
            {
                _contentRoot.Clip = null;
            }
        }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        var width = _arranged.Width;
        var h = Math.Max(0, _height.Value);
        if (width <= 0 || h <= 0)
        {
            return;
        }
        var rect = new Rect(0, _arranged.Height - h, width, h);
        var radius = Math.Min(CardRadius.TopLeft, h / 2);
        var rounded = new RoundedRect(rect, radius);
        var shadow = CardShadow;
        if (shadow.Count > 0)
        {
            context.DrawRectangle(CardBackground, null, rounded, shadow);
        }
        else
        {
            context.DrawRectangle(CardBackground, null, rounded);
        }
        if (CardBorderBrush is { } border)
        {
            _borderPen ??= new Pen(border, 1);
            if (!ReferenceEquals(_borderPen.Brush, border))
            {
                _borderPen = new Pen(border, 1);
            }
            context.DrawRectangle(null, _borderPen, new RoundedRect(rect.Deflate(0.5), Math.Max(0, radius - 0.5)));
        }
    }

    private Pen? _borderPen;

    #endregion

    #region 堆叠运动

    /// <summary>
    /// Retargets the running springs from wherever they are, so rapid toasts and hover changes never queue.
    /// A change of the expanded state rides the morph spring; anything else the smooth one.
    /// </summary>
    public void ApplyTarget(ToastStackTarget target, bool expanded, bool front, bool hidden)
    {
        PseudoClasses.Set(FrontPseudoClass, front);
        PseudoClasses.Set(ExpandedPseudoClass, expanded);
        PseudoClasses.Set(TuckedPseudoClass, !front && !expanded);
        SetInert(hidden);
        if (!IsPresent || target.Height <= 0)
        {
            return;
        }
        var entering = _appliedExpanded is null;
        if (!entering && _appliedExpanded == expanded && _applied == target)
        {
            return;
        }
        var morph = !entering && _appliedExpanded != expanded;
        _appliedExpanded = expanded;
        _applied = target;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || TopLevel.GetTopLevel(this) is null)
        {
            _y.Jump(target.Y);
            _scale.Jump(target.Scale);
            _height.Jump(target.Height);
            _opacity.Jump(target.Opacity);
            _content.Jump(target.Content);
            return;
        }
        var fade = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        if (level == ArcMotionLevel.Reduced)
        {
            _y.Jump(target.Y);
            _scale.Jump(target.Scale);
            _height.Jump(target.Height);
            Retarget(_opacity, target.Opacity, fade);
            Retarget(_content, target.Content, fade);
            return;
        }
        if (entering)
        {
            // A new toast rises from exactly its own height below the edge.
            _y.Jump(target.Y + target.Height);
            _scale.Jump(target.Scale);
            _height.Jump(target.Height);
            _content.Jump(target.Content);
            _opacity.Jump(0);
        }
        var spring = MotionSpec.Spring(morph ? ArcSpring.Morph : ArcSpring.Smooth);
        Retarget(_y, target.Y, spring);
        Retarget(_scale, target.Scale, spring);
        if (!entering)
        {
            Retarget(_height, target.Height, spring);
        }
        Retarget(_opacity, target.Opacity, entering ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter) : fade);
        Retarget(_content, target.Content, fade);
    }

    private static void Retarget(MotionValue<double> value, double target, MotionSpec spec)
    {
        if (value.IsAnimating ? Math.Abs(value.Target - target) < 1e-6 : Math.Abs(value.Value - target) < 1e-6)
        {
            return;
        }
        _ = value.AnimateTo(target, spec);
    }

    /// <summary>Plays the exit: a closed toast sinks toward its edge, a thrown one keeps flying. Completes when gone.</summary>
    public Task Leave()
    {
        IsPresent = false;
        StopTimer();
        SetInert(true);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (level == ArcMotionLevel.Off || TopLevel.GetTopLevel(this) is null)
        {
            _opacity.Jump(0);
            return Task.CompletedTask;
        }
        if (level == ArcMotionLevel.Reduced)
        {
            return _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        }
        if (!_swiped)
        {
            _ = _y.AnimateTo(_y.Value + 10, MotionSpec.Spring(ArcSpring.Smooth));
            _ = _scale.AnimateTo(_scale.Value * 0.96, MotionSpec.Spring(ArcSpring.Smooth));
        }
        var duration = _swiped ? TimeSpan.FromSeconds(0.22) : ArcMotionPresets.Exit;
        return _opacity.AnimateTo(0, MotionSpec.Tween(duration, ArcEasings.Standard));
    }

    /// <summary>Hidden and leaving toasts are inert: no hit testing, no tab stops.</summary>
    private void SetInert(bool inert)
    {
        _inert = inert;
        IsHitTestVisible = !inert;
        if (_action is not null)
        {
            _action.IsTabStop = !inert;
            _action.Focusable = !inert;
        }
        if (_close is not null)
        {
            _close.IsTabStop = !inert;
            _close.Focusable = !inert;
        }
    }

    #endregion

    #region 计时

    /// <summary>An update restarts the clock; pausing keeps whatever time is left.</summary>
    public void RestartTimer(TimeSpan? duration, bool paused)
    {
        StopTimer();
        if (duration is not { } d || d <= TimeSpan.Zero || d == Timeout.InfiniteTimeSpan || d == TimeSpan.MaxValue)
        {
            return;
        }
        _timer = new PausableTimer(d, () => _owner.OnItemTimedOut(this));
        _timer.Start();
        if (paused)
        {
            _timer.Pause();
        }
    }

    public void SetPaused(bool paused)
    {
        if (_timer is null || !IsPresent)
        {
            return;
        }
        if (paused)
        {
            _timer.Pause();
        }
        else
        {
            _timer.Resume();
        }
    }

    private void StopTimer()
    {
        _timer?.Dispose();
        _timer = null;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (!IsPresent)
        {
            StopTimer();
        }
    }

    #endregion

    #region 交互

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        var keyboard = _action?.Classes.Contains(":focus-visible") ?? false;
        _owner.RunAction(this, keyboard);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => _owner.Dismiss(Id);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && !e.Handled)
        {
            e.Handled = true;
            _owner.Dismiss(Id);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _suppressClick = false;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || !IsPresent || IsInsideButton(e.Source))
        {
            return;
        }
        // Catch a toast that is still springing back, from exactly where it is.
        _x.Stop();
        _swipe.Begin(e.GetPosition(Parent as Visual ?? this), _x.Value);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_swipe.IsTracking)
        {
            return;
        }
        // Positions come from the list, which does not move with the card.
        var started = _swipe.Move(e.GetPosition(Parent as Visual ?? this), e.Timestamp);
        if (started)
        {
            e.Pointer.Capture(this);
            _suppressClick = true;
            PseudoClasses.Set(DraggingPseudoClass, true);
            _owner.OnItemDragChanged(true);
        }
        if (!_swipe.IsDragging)
        {
            return;
        }
        var raw = _swipe.RawOffset;
        // Right follows the finger 1:1; left is the wrong way, so it resists like an overscroll.
        _x.Jump(raw >= 0 ? raw : -ToastSwipeTracker.RubberBand(-raw, Bounds.Width));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var wasDragging = _swipe.IsDragging;
        var tracking = _swipe.IsTracking;
        EndSwipe(e.Timestamp, cancelled: false, e.Pointer);
        if (tracking && !wasDragging && !_suppressClick && e.Pointer.Type != PointerType.Mouse && !IsInsideButton(e.Source))
        {
            // Touch has no hover, so a tap on the card opens or closes the stack instead.
            _owner.OnItemTapped();
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        EndSwipe(0, cancelled: true, null);
    }

    private void EndSwipe(ulong timestamp, bool cancelled, IPointer? pointer)
    {
        if (!_swipe.IsTracking)
        {
            return;
        }
        var dragging = _swipe.IsDragging;
        var velocity = _swipe.End(timestamp, cancelled);
        if (!dragging)
        {
            return;
        }
        PseudoClasses.Set(DraggingPseudoClass, false);
        pointer?.Capture(null);
        _owner.OnItemDragChanged(false);
        var width = Bounds.Width;
        var offset = _x.Value;
        var reduce = ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full;
        if (!cancelled && offset > 0 && velocity > -200 && offset + ToastSwipeTracker.Project(velocity) > width * 0.4)
        {
            _swiped = true;
            if (!reduce)
            {
                _ = _x.AnimateTo(width + 40, MotionSpec.Spring(SpringSpec.FromVisual(0.32, 0)), Math.Max(velocity, 0));
            }
            _owner.Dismiss(Id);
            return;
        }
        if (reduce)
        {
            _x.Jump(0);
        }
        else
        {
            _ = _x.AnimateTo(0, MotionSpec.Spring(ArcSpring.Snappy), offset < 0 ? velocity * 0.3 : velocity);
        }
    }

    private bool IsInsideButton(object? source)
    {
        for (var visual = source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
        {
            if (visual is AvaloniaButton)
            {
                return true;
            }
        }
        return false;
    }

    #endregion
}
