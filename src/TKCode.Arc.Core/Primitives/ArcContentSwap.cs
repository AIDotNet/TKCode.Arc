using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace TKCode.Arc.Primitives;

/// <summary>How <see cref="ArcContentSwap"/> moves between contents.</summary>
public enum ContentSwapMode
{
    /// <summary>Replace immediately.</summary>
    None,

    /// <summary>Opacity crossfade.</summary>
    Fade,

    /// <summary>P2: the new content rises 4px out of a soft blur; the old one lifts away and fades.</summary>
    BlurSwap,

    /// <summary>P3: like BlurSwap, but travels 8px in the direction of change (<see cref="ArcContentSwap.Direction"/>).</summary>
    Roll,

    /// <summary>Icon swap: the new glyph grows from 0.6 out of a soft blur on a slow, almost critically damped spring.</summary>
    Pop,

    /// <summary>P25 title slide: the new content arrives 12px from the side of travel, the old one leaves the other way (no blur).</summary>
    Slide,

    /// <summary>
    /// Label swap (Arc AI blocks: status words, pill text, counts): the old label fades out in 100ms while the new one
    /// fades in over 160ms (enter curve) rising 2px, no blur. Pair it with a width spring (<see cref="ArcSpring.Quick"/>).
    /// </summary>
    Label,

    /// <summary>Crossfade on Arc's standard curve: out 100ms, in 160ms, no movement (values and summaries replaced in place).</summary>
    Crossfade,

    /// <summary>
    /// Sequential replacement of a whole view (a conversation, a panel): the old content fades out in 100ms, then the new
    /// one fades in over 160ms rising 4px.
    /// </summary>
    Lift
}

/// <summary>
/// Content presenter that animates content changes (Arc primitives P2 blur swap and P3 directional roll).
/// The outgoing content is popped out of layout: only the incoming content determines the size, and the outgoing copy
/// keeps its own size, placed by <see cref="HorizontalContentAlignment"/> / <see cref="VerticalContentAlignment"/>
/// like the incoming one (Left / Top and Right / Bottom pin it to that edge, so left-aligned text never jumps; Center
/// and Stretch center it on the slot). The first content never animates.
/// </summary>
public class ArcContentSwap : Control
{
    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<ArcContentSwap>();

    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        ContentControl.ContentTemplateProperty.AddOwner<ArcContentSwap>();

    public static readonly StyledProperty<ContentSwapMode> ModeProperty =
        AvaloniaProperty.Register<ArcContentSwap, ContentSwapMode>(nameof(Mode), ContentSwapMode.BlurSwap);

    /// <summary>1 when the new value is later or higher (rolls up), -1 when earlier or lower (rolls down).</summary>
    public static readonly StyledProperty<int> DirectionProperty =
        AvaloniaProperty.Register<ArcContentSwap, int>(nameof(Direction), 1);

    public static readonly StyledProperty<Avalonia.Layout.HorizontalAlignment> HorizontalContentAlignmentProperty =
        ContentControl.HorizontalContentAlignmentProperty.AddOwner<ArcContentSwap>();

    /// <summary>Enter duration for <see cref="ContentSwapMode.Label"/>, <see cref="ContentSwapMode.Crossfade"/> and <see cref="ContentSwapMode.Lift"/>; null uses the mode's own (160ms).</summary>
    public static readonly StyledProperty<TimeSpan?> EnterDurationProperty =
        AvaloniaProperty.Register<ArcContentSwap, TimeSpan?>(nameof(EnterDuration));

    /// <summary>Exit duration for the Label, Crossfade and Lift modes; null uses the mode's own (100ms).</summary>
    public static readonly StyledProperty<TimeSpan?> ExitDurationProperty =
        AvaloniaProperty.Register<ArcContentSwap, TimeSpan?>(nameof(ExitDuration));

    /// <summary>Delay before the new content starts to appear in the Label, Crossfade and Lift modes; null uses the mode's own (Lift waits for the exit).</summary>
    public static readonly StyledProperty<TimeSpan?> EnterDelayProperty =
        AvaloniaProperty.Register<ArcContentSwap, TimeSpan?>(nameof(EnterDelay));

    /// <summary>How far the new content rises (px) in the Label, Crossfade and Lift modes; null uses the mode's own (2, 0, 4).</summary>
    public static readonly StyledProperty<double?> EnterOffsetProperty =
        AvaloniaProperty.Register<ArcContentSwap, double?>(nameof(EnterOffset));

    /// <summary>
    /// When true, outgoing content keeps counting toward the desired size until it is gone, so a surrounding
    /// <see cref="SpringSizeDecorator"/> grows at once but shrinks only after the old content has faded out.
    /// </summary>
    public static readonly StyledProperty<bool> MeasuresExitingProperty =
        AvaloniaProperty.Register<ArcContentSwap, bool>(nameof(MeasuresExiting));

    public static readonly StyledProperty<Avalonia.Layout.VerticalAlignment> VerticalContentAlignmentProperty =
        ContentControl.VerticalContentAlignmentProperty.AddOwner<ArcContentSwap>();

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public IDataTemplate? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    public ContentSwapMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public int Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public Avalonia.Layout.HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    public Avalonia.Layout.VerticalAlignment VerticalContentAlignment
    {
        get => GetValue(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    /// <inheritdoc cref="EnterDurationProperty"/>
    public TimeSpan? EnterDuration
    {
        get => GetValue(EnterDurationProperty);
        set => SetValue(EnterDurationProperty, value);
    }

    /// <inheritdoc cref="ExitDurationProperty"/>
    public TimeSpan? ExitDuration
    {
        get => GetValue(ExitDurationProperty);
        set => SetValue(ExitDurationProperty, value);
    }

    /// <inheritdoc cref="EnterDelayProperty"/>
    public TimeSpan? EnterDelay
    {
        get => GetValue(EnterDelayProperty);
        set => SetValue(EnterDelayProperty, value);
    }

    /// <inheritdoc cref="EnterOffsetProperty"/>
    public double? EnterOffset
    {
        get => GetValue(EnterOffsetProperty);
        set => SetValue(EnterOffsetProperty, value);
    }

    /// <inheritdoc cref="MeasuresExitingProperty"/>
    public bool MeasuresExiting
    {
        get => GetValue(MeasuresExitingProperty);
        set => SetValue(MeasuresExitingProperty, value);
    }

    private Layer? _current;
    private readonly List<Layer> _exiting = new();

    static ArcContentSwap()
    {
        AffectsArrange<ArcContentSwap>(HorizontalContentAlignmentProperty, VerticalContentAlignmentProperty);
        AffectsMeasure<ArcContentSwap>(MeasuresExitingProperty);
    }

    private sealed class Layer
    {
        public required ContentPresenter Presenter { get; init; }
        public required TranslateTransform Translate { get; init; }
        /// <summary>The layer blurs while it moves; the effect itself is a cached <see cref="ArcBlur"/> step set per frame.</summary>
        public bool Blurs { get; set; }
        public MotionValue<double>? Progress { get; set; }
        public Rect LastRect { get; set; }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
        {
            Swap(change.NewValue);
        }
        else if (change.Property == ContentTemplateProperty && _current is not null)
        {
            // Only with content: a presenter builds a template it is given while its content is null (with null).
            _current.Presenter.ContentTemplate = _current.Presenter.Content is null ? null : ContentTemplate;
        }
        else if (change.Property == HorizontalContentAlignmentProperty || change.Property == VerticalContentAlignmentProperty)
        {
            if (_current is not null)
            {
                _current.Presenter.HorizontalContentAlignment = HorizontalContentAlignment;
                _current.Presenter.VerticalContentAlignment = VerticalContentAlignment;
            }
            foreach (var layer in _exiting)
            {
                layer.Presenter.HorizontalContentAlignment = HorizontalContentAlignment;
                layer.Presenter.VerticalContentAlignment = VerticalContentAlignment;
            }
        }
    }

    private Layer CreateLayer(object? content)
    {
        var translate = new TranslateTransform();
        var presenter = new ContentPresenter
        {
            Content = content,
            ContentTemplate = content is null ? null : ContentTemplate,
            RenderTransform = translate,
            HorizontalContentAlignment = HorizontalContentAlignment,
            VerticalContentAlignment = VerticalContentAlignment
        };
        LogicalChildren.Add(presenter);
        VisualChildren.Add(presenter);
        return new Layer { Presenter = presenter, Translate = translate };
    }

    /// <summary>
    /// Changes a layer's content without ever building the content template for null. A presenter builds the template it has
    /// whenever its content changes, and one that is attached while the content is null is built with null: a user's template that
    /// reads its data would throw. So the template is dropped before the content is cleared, and put back after content arrives.
    /// </summary>
    private void SetContent(ContentPresenter presenter, object? content)
    {
        if (content is null)
        {
            presenter.ContentTemplate = null;
            presenter.Content = null;
        }
        else if (ReferenceEquals(presenter.ContentTemplate, ContentTemplate))
        {
            presenter.Content = content;
        }
        else
        {
            presenter.Content = content;
            presenter.ContentTemplate = ContentTemplate;
        }
    }

    private void RemoveLayer(Layer layer)
    {
        layer.Progress?.Dispose();
        // A removed presenter keeps its child until its content changes, and a control has one parent: without this the
        // content could never be shown again (a kept view that the swap shows, hides and shows again).
        SetContent(layer.Presenter, null);
        LogicalChildren.Remove(layer.Presenter);
        VisualChildren.Remove(layer.Presenter);
        _exiting.Remove(layer);
        if (MeasuresExiting)
        {
            InvalidateMeasure();
        }
    }

    private void Swap(object? content)
    {
        // A control that comes back while it is still leaving (A → B → A inside one swap) is still the child of its exiting
        // layer; a control has one parent, so that layer goes first.
        if (content is Control control)
        {
            foreach (var layer in _exiting.Where(layer => ReferenceEquals(layer.Presenter.Content, control)).ToList())
            {
                RemoveLayer(layer);
            }
        }
        if (_current is null)
        {
            _current = CreateLayer(content);
            InvalidateMeasure();
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || Mode == ContentSwapMode.None || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            SetContent(_current.Presenter, content);
            return;
        }
        var outgoing = _current;
        _exiting.Add(outgoing);
        _current = CreateLayer(content);
        var full = level == ArcMotionLevel.Full && Mode != ContentSwapMode.Fade;
        var dir = Direction >= 0 ? 1 : -1;
        if (Mode == ContentSwapMode.Pop)
        {
            StartPopEnter(_current, full);
            StartPopExit(outgoing, full);
            InvalidateMeasure();
            return;
        }
        if (Mode is ContentSwapMode.Label or ContentSwapMode.Crossfade or ContentSwapMode.Lift)
        {
            StartQuietSwap(outgoing, _current, level == ArcMotionLevel.Full);
            InvalidateMeasure();
            return;
        }
        if (Mode == ContentSwapMode.Slide)
        {
            StartEnter(_current, full, 12.0 * dir, horizontal: true, blur: false);
            StartExit(outgoing, full, -12.0 * dir, horizontal: true, blur: false, duration: TimeSpan.FromMilliseconds(140));
            InvalidateMeasure();
            return;
        }
        var travel = Mode == ContentSwapMode.Roll ? 8.0 : 4.0;
        StartEnter(_current, full, travel * dir);
        StartExit(outgoing, full, (Mode == ContentSwapMode.Roll ? -8.0 : -3.0) * dir);
        InvalidateMeasure();
    }

    private void StartEnter(Layer layer, bool full, double fromOffset, bool horizontal = false, bool blur = true,
        TimeSpan? duration = null, Avalonia.Animation.Easings.Easing? easing = null, TimeSpan delay = default)
    {
        layer.Presenter.Opacity = 0;
        var blurRadius = full && blur ? 4.0 : 0;
        if (full)
        {
            SetOffset(layer, horizontal, fromOffset);
            if (blurRadius > 0)
            {
                layer.Blurs = true;
                layer.Presenter.Effect = ArcBlur.Get(blurRadius);
            }
        }
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Presenter.Opacity = p;
            if (full)
            {
                SetOffset(layer, horizontal, fromOffset * (1 - p));
                var radius = blurRadius * (1 - p);
                if (radius < 0.05)
                {
                    layer.Presenter.Effect = null;
                }
                else if (layer.Blurs)
                {
                    layer.Presenter.Effect = ArcBlur.Get(radius);
                }
            }
        };
        var spec = full
            ? MotionSpec.Tween(duration ?? ArcMotionPresets.Standard, easing ?? ArcEasings.Enter, delay)
            : MotionSpec.Tween(ArcMotionPresets.Instant, new Avalonia.Animation.Easings.LinearEasing());
        _ = progress.AnimateTo(1, spec);
    }

    private static readonly TimeSpan QuietExit = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Label, Crossfade and Lift: opacity only on the way out, opacity plus a small rise on the way in, no blur. Reduced
    /// motion crossfades both at once (<see cref="ArcMotionPresets.Instant"/>, no rise, no delay).
    /// </summary>
    private void StartQuietSwap(Layer outgoing, Layer incoming, bool full)
    {
        var mode = Mode;
        var exit = ExitDuration ?? QuietExit;
        var enter = EnterDuration ?? ArcMotionPresets.Fast;
        var delay = EnterDelay ?? (mode == ContentSwapMode.Lift ? exit : TimeSpan.Zero);
        var rise = EnterOffset ?? mode switch
        {
            ContentSwapMode.Label => 2.0,
            ContentSwapMode.Lift => 4.0,
            _ => 0.0
        };
        var easing = mode == ContentSwapMode.Label ? ArcEasings.Enter : ArcEasings.Standard;
        StartEnter(incoming, full, rise, blur: false, duration: enter, easing: easing, delay: delay);
        StartExit(outgoing, full, outgoing.Translate.Y, blur: false, duration: exit);
    }

    private static void SetOffset(Layer layer, bool horizontal, double value)
    {
        if (horizontal)
        {
            layer.Translate.X = value;
        }
        else
        {
            layer.Translate.Y = value;
        }
    }

    private static readonly SpringSpec PopSpring = SpringSpec.FromVisual(0.5, 0.06);

    private void StartPopEnter(Layer layer, bool full)
    {
        var scale = new ScaleTransform(full ? 0.6 : 1, full ? 0.6 : 1);
        layer.Presenter.RenderTransformOrigin = RelativePoint.Center;
        layer.Presenter.RenderTransform = scale;
        layer.Presenter.Opacity = 0;
        if (full)
        {
            layer.Blurs = true;
            layer.Presenter.Effect = ArcBlur.Get(4);
        }
        var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = fade;
        fade.Changed += p =>
        {
            layer.Presenter.Opacity = p;
            if (layer.Blurs)
            {
                var radius = 4 * (1 - p);
                if (radius < 0.05)
                {
                    layer.Presenter.Effect = null;
                }
                else
                {
                    layer.Presenter.Effect = ArcBlur.Get(radius);
                }
            }
        };
        _ = fade.AnimateTo(1, full
            ? MotionSpec.Tween(TimeSpan.FromSeconds(0.36), ArcEasings.Enter, TimeSpan.FromSeconds(0.03))
            : MotionSpec.Tween(ArcMotionPresets.Instant, new Avalonia.Animation.Easings.LinearEasing()));
        if (full)
        {
            var grow = new MotionValue<double>(this, 0.6, MotionVectors.Double);
            grow.Changed += v =>
            {
                scale.ScaleX = v;
                scale.ScaleY = v;
            };
            _ = grow.AnimateTo(1, MotionSpec.Spring(PopSpring));
        }
    }

    private void StartPopExit(Layer layer, bool full)
    {
        layer.Progress?.Stop();
        var scale = new ScaleTransform(1, 1);
        layer.Presenter.RenderTransformOrigin = RelativePoint.Center;
        layer.Presenter.RenderTransform = scale;
        if (full)
        {
            layer.Blurs = true;
        }
        var startOpacity = layer.Presenter.Opacity;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Presenter.Opacity = startOpacity * (1 - p);
            if (full)
            {
                var s = 1 - 0.4 * p;
                scale.ScaleX = s;
                scale.ScaleY = s;
                if (layer.Blurs)
                {
                    layer.Presenter.Effect = ArcBlur.Get(4 * p);
                }
            }
        };
        progress.AnimateTo(1, full
                ? MotionSpec.Tween(TimeSpan.FromSeconds(0.24), ArcEasings.Standard)
                : MotionSpec.Tween(ArcMotionPresets.Instant, new Avalonia.Animation.Easings.LinearEasing()))
            .ContinueWith(_ => Dispatcher.Post(() => RemoveLayer(layer)), TaskScheduler.Default);
    }

    private void StartExit(Layer layer, bool full, double toOffset, bool horizontal = false, bool blur = true, TimeSpan? duration = null)
    {
        layer.Progress?.Stop();
        var startOpacity = layer.Presenter.Opacity;
        if (full && blur)
        {
            layer.Blurs = true;
        }
        var start = horizontal ? layer.Translate.X : layer.Translate.Y;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Presenter.Opacity = startOpacity * (1 - p);
            if (full)
            {
                SetOffset(layer, horizontal, start + (toOffset - start) * p);
                if (blur && layer.Blurs)
                {
                    layer.Presenter.Effect = ArcBlur.Get(2 * p);
                }
            }
        };
        var spec = full
            ? MotionSpec.Tween(duration ?? ArcMotionPresets.Fast, ArcEasings.Standard)
            : MotionSpec.Tween(ArcMotionPresets.Instant, new Avalonia.Animation.Easings.LinearEasing());
        progress.AnimateTo(1, spec).ContinueWith(_ => Dispatcher.Post(() => RemoveLayer(layer)),
            TaskScheduler.Default);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var layer in _exiting)
        {
            layer.Presenter.Measure(availableSize);
        }
        if (_current is null)
        {
            return default;
        }
        _current.Presenter.Measure(availableSize);
        var size = _current.Presenter.DesiredSize;
        if (MeasuresExiting)
        {
            foreach (var layer in _exiting)
            {
                size = new Size(Math.Max(size.Width, layer.Presenter.DesiredSize.Width), Math.Max(size.Height, layer.Presenter.DesiredSize.Height));
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _exiting)
        {
            // Outgoing content is out of layout and keeps its own size, aligned on the slot like the incoming content.
            var size = layer.Presenter.DesiredSize;
            var x = HorizontalContentAlignment switch
            {
                Avalonia.Layout.HorizontalAlignment.Left => 0,
                Avalonia.Layout.HorizontalAlignment.Right => finalSize.Width - size.Width,
                _ => (finalSize.Width - size.Width) / 2
            };
            var y = VerticalContentAlignment switch
            {
                Avalonia.Layout.VerticalAlignment.Top => 0,
                Avalonia.Layout.VerticalAlignment.Bottom => finalSize.Height - size.Height,
                _ => (finalSize.Height - size.Height) / 2
            };
            layer.Presenter.Arrange(new Rect(x, y, size.Width, size.Height));
        }
        if (_current is not null)
        {
            var rect = new Rect(finalSize);
            _current.Presenter.Arrange(rect);
            _current.LastRect = rect;
        }
        return finalSize;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var layer in _exiting.ToArray())
        {
            RemoveLayer(layer);
        }
        if (_current is not null)
        {
            _current.Progress?.Stop();
            _current.Presenter.Opacity = 1;
            _current.Presenter.Effect = null;
            _current.Translate.Y = 0;
        }
    }
}
