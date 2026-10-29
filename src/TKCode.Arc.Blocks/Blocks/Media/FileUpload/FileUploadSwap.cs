// Adapted from Blocks/HeadersFooters/_Shared/FileUploadSwap.cs so the Media group stands alone (Arc's AnimatePresence mode="popLayout" swap).
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// Swaps one line of content in place, the way Arc's file-upload block does with
/// <c>AnimatePresence mode="popLayout"</c>: the old content leaves layout at once and lifts away
/// (<see cref="ExitOffset"/>, <see cref="ExitBlur"/>, <see cref="ExitDuration"/>) while the new one rises out of a soft
/// blur (<see cref="EnterOffset"/>, <see cref="EnterBlur"/>, <see cref="EnterDuration"/>, <see cref="EnterDelay"/>). Only
/// the new content sizes the control; the old copy keeps its own size, aligned like the new one. The theme sets File
/// upload's values: in 0.3em up from a 4px blur over 240 ms (enter curve); out 0.3em up into a 2px blur over 160 ms
/// (standard curve); reduced motion shows the new content at once and fades the old one out in 120 ms. Reduced motion fades only
/// (<see cref="ReducedEnterDuration"/> / <see cref="ReducedExitDuration"/>); Off and the first content swap at once.
/// </summary>
/// <remarks>
/// Code-driven because ControlTheme transitions cannot keep the outgoing content alive outside layout while the incoming
/// one takes its place. <see cref="ArcContentSwap"/> has fixed travel and blur per mode; File upload needs Arc's exact
/// per-swap values.
/// </remarks>
internal sealed class FileUploadSwap : Control
{
    public static readonly StyledProperty<object?> ContentProperty =
        ContentControl.ContentProperty.AddOwner<FileUploadSwap>();

    public static readonly StyledProperty<IDataTemplate?> ContentTemplateProperty =
        ContentControl.ContentTemplateProperty.AddOwner<FileUploadSwap>();

    public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
        ContentControl.HorizontalContentAlignmentProperty.AddOwner<FileUploadSwap>();

    public static readonly StyledProperty<VerticalAlignment> VerticalContentAlignmentProperty =
        ContentControl.VerticalContentAlignmentProperty.AddOwner<FileUploadSwap>();

    /// <summary>Where the new content starts, in px below its place (negative: above).</summary>
    public static readonly StyledProperty<double> EnterOffsetProperty =
        AvaloniaProperty.Register<FileUploadSwap, double>(nameof(EnterOffset), 6);

    /// <summary>Blur radius the new content starts from.</summary>
    public static readonly StyledProperty<double> EnterBlurProperty =
        AvaloniaProperty.Register<FileUploadSwap, double>(nameof(EnterBlur), 2);

    /// <summary>Duration of the enter tween (opacity, offset and blur together).</summary>
    public static readonly StyledProperty<TimeSpan> EnterDurationProperty =
        AvaloniaProperty.Register<FileUploadSwap, TimeSpan>(nameof(EnterDuration), ArcMotionPresets.Standard);

    /// <summary>Delay before the new content starts to appear.</summary>
    public static readonly StyledProperty<TimeSpan> EnterDelayProperty =
        AvaloniaProperty.Register<FileUploadSwap, TimeSpan>(nameof(EnterDelay));

    /// <summary>Where the old content goes, in px from its place (negative: up).</summary>
    public static readonly StyledProperty<double> ExitOffsetProperty =
        AvaloniaProperty.Register<FileUploadSwap, double>(nameof(ExitOffset), -6);

    /// <summary>Blur radius the old content ends at.</summary>
    public static readonly StyledProperty<double> ExitBlurProperty =
        AvaloniaProperty.Register<FileUploadSwap, double>(nameof(ExitBlur), 2);

    /// <summary>Duration of the exit tween (standard curve).</summary>
    public static readonly StyledProperty<TimeSpan> ExitDurationProperty =
        AvaloniaProperty.Register<FileUploadSwap, TimeSpan>(nameof(ExitDuration), ArcMotionPresets.Fast);

    /// <summary>Fade-in duration under reduced motion.</summary>
    public static readonly StyledProperty<TimeSpan> ReducedEnterDurationProperty =
        AvaloniaProperty.Register<FileUploadSwap, TimeSpan>(nameof(ReducedEnterDuration), ArcMotionPresets.Instant);

    /// <summary>Fade-out duration under reduced motion (zero removes the old content at once).</summary>
    public static readonly StyledProperty<TimeSpan> ReducedExitDurationProperty =
        AvaloniaProperty.Register<FileUploadSwap, TimeSpan>(nameof(ReducedExitDuration), ArcMotionPresets.Instant);

    private Layer? _current;
    private readonly List<Layer> _exiting = new();

    static FileUploadSwap()
    {
        AffectsArrange<FileUploadSwap>(HorizontalContentAlignmentProperty, VerticalContentAlignmentProperty);
    }

    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    public IDataTemplate? ContentTemplate { get => GetValue(ContentTemplateProperty); set => SetValue(ContentTemplateProperty, value); }

    public HorizontalAlignment HorizontalContentAlignment { get => GetValue(HorizontalContentAlignmentProperty); set => SetValue(HorizontalContentAlignmentProperty, value); }

    public VerticalAlignment VerticalContentAlignment { get => GetValue(VerticalContentAlignmentProperty); set => SetValue(VerticalContentAlignmentProperty, value); }

    /// <inheritdoc cref="EnterOffsetProperty"/>
    public double EnterOffset { get => GetValue(EnterOffsetProperty); set => SetValue(EnterOffsetProperty, value); }

    /// <inheritdoc cref="EnterBlurProperty"/>
    public double EnterBlur { get => GetValue(EnterBlurProperty); set => SetValue(EnterBlurProperty, value); }

    /// <inheritdoc cref="EnterDurationProperty"/>
    public TimeSpan EnterDuration { get => GetValue(EnterDurationProperty); set => SetValue(EnterDurationProperty, value); }

    /// <inheritdoc cref="EnterDelayProperty"/>
    public TimeSpan EnterDelay { get => GetValue(EnterDelayProperty); set => SetValue(EnterDelayProperty, value); }

    /// <summary>Easing of the enter tween (Arc's enter curve).</summary>
    public Easing EnterEasing { get; set; } = ArcEasings.Enter;

    /// <inheritdoc cref="ExitOffsetProperty"/>
    public double ExitOffset { get => GetValue(ExitOffsetProperty); set => SetValue(ExitOffsetProperty, value); }

    /// <inheritdoc cref="ExitBlurProperty"/>
    public double ExitBlur { get => GetValue(ExitBlurProperty); set => SetValue(ExitBlurProperty, value); }

    /// <inheritdoc cref="ExitDurationProperty"/>
    public TimeSpan ExitDuration { get => GetValue(ExitDurationProperty); set => SetValue(ExitDurationProperty, value); }

    /// <summary>Easing of the exit tween (Arc's standard curve).</summary>
    public Easing ExitEasing { get; set; } = ArcEasings.Standard;

    /// <inheritdoc cref="ReducedEnterDurationProperty"/>
    public TimeSpan ReducedEnterDuration { get => GetValue(ReducedEnterDurationProperty); set => SetValue(ReducedEnterDurationProperty, value); }

    /// <inheritdoc cref="ReducedExitDurationProperty"/>
    public TimeSpan ReducedExitDuration { get => GetValue(ReducedExitDurationProperty); set => SetValue(ReducedExitDurationProperty, value); }

    /// <summary>Number of outgoing copies still on screen (tests read it).</summary>
    public int ExitingCount => _exiting.Count;

    /// <summary>The presenter of the current content (tests read it).</summary>
    internal ContentPresenter? CurrentPresenter => _current?.Presenter;

    private sealed class Layer
    {
        public required ContentPresenter Presenter { get; init; }
        public required TranslateTransform Translate { get; init; }
        public MotionValue<double>? Progress { get; set; }
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
            // Only with content: a presenter given a template while its content is null builds it with null.
            _current.Presenter.ContentTemplate = _current.Presenter.Content is null ? null : ContentTemplate;
        }
        else if (change.Property == HorizontalContentAlignmentProperty || change.Property == VerticalContentAlignmentProperty)
        {
            foreach (var layer in Layers())
            {
                layer.Presenter.HorizontalContentAlignment = HorizontalContentAlignment;
                layer.Presenter.VerticalContentAlignment = VerticalContentAlignment;
            }
        }
    }

    private IEnumerable<Layer> Layers()
    {
        foreach (var layer in _exiting)
        {
            yield return layer;
        }
        if (_current is not null)
        {
            yield return _current;
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

    private void SetContent(ContentPresenter presenter, object? content)
    {
        if (content is null)
        {
            presenter.ContentTemplate = null;
            presenter.Content = null;
        }
        else
        {
            presenter.Content = content;
            if (!ReferenceEquals(presenter.ContentTemplate, ContentTemplate))
            {
                presenter.ContentTemplate = ContentTemplate;
            }
        }
    }

    private void RemoveLayer(Layer layer)
    {
        layer.Progress?.Dispose();
        layer.Progress = null;
        LogicalChildren.Remove(layer.Presenter);
        VisualChildren.Remove(layer.Presenter);
        _exiting.Remove(layer);
    }

    private void Swap(object? content)
    {
        if (_current is null)
        {
            _current = CreateLayer(content);
            InvalidateMeasure();
            return;
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            _current.Progress?.Stop();
            ResetLayer(_current);
            SetContent(_current.Presenter, content);
            return;
        }
        var full = level == ArcMotionLevel.Full;
        var outgoing = _current;
        _exiting.Add(outgoing);
        _current = CreateLayer(content);
        StartExit(outgoing, full);
        StartEnter(_current, full);
        InvalidateMeasure();
    }

    private static void ResetLayer(Layer layer)
    {
        layer.Presenter.Opacity = 1;
        layer.Presenter.Effect = null;
        layer.Translate.Y = 0;
    }

    private void StartEnter(Layer layer, bool full)
    {
        var offset = full ? EnterOffset : 0;
        var blur = full ? EnterBlur : 0;
        layer.Presenter.Opacity = 0;
        layer.Translate.Y = offset;
        layer.Presenter.Effect = ArcBlur.Get(blur);
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Presenter.Opacity = p;
            layer.Translate.Y = offset * (1 - p);
            layer.Presenter.Effect = ArcBlur.Get(blur * (1 - p));
        };
        var spec = full
            ? MotionSpec.Tween(EnterDuration, EnterEasing, EnterDelay)
            : MotionSpec.Tween(ReducedEnterDuration, ArcEasings.Standard);
        _ = progress.AnimateTo(1, spec);
    }

    private void StartExit(Layer layer, bool full)
    {
        layer.Progress?.Stop();
        var duration = full ? ExitDuration : ReducedExitDuration;
        if (duration <= TimeSpan.Zero)
        {
            RemoveLayer(layer);
            return;
        }
        var startOpacity = layer.Presenter.Opacity;
        var startY = layer.Translate.Y;
        var endY = full ? ExitOffset : startY;
        var blur = full ? ExitBlur : 0;
        var progress = new MotionValue<double>(this, 0, MotionVectors.Double);
        layer.Progress = progress;
        progress.Changed += p =>
        {
            layer.Presenter.Opacity = startOpacity * (1 - p);
            layer.Translate.Y = startY + (endY - startY) * p;
            layer.Presenter.Effect = ArcBlur.Get(blur * p);
        };
        progress.AnimateTo(1, MotionSpec.Tween(duration, ExitEasing))
            .ContinueWith(_ => Dispatcher.Post(() =>
            {
                if (_exiting.Contains(layer))
                {
                    RemoveLayer(layer);
                }
            }), TaskScheduler.Default);
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
        return _current.Presenter.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var layer in _exiting)
        {
            // The outgoing copy is out of layout and keeps its own size, aligned on the slot like the incoming content.
            var size = layer.Presenter.DesiredSize;
            var x = HorizontalContentAlignment switch
            {
                HorizontalAlignment.Left => 0,
                HorizontalAlignment.Right => finalSize.Width - size.Width,
                HorizontalAlignment.Stretch => 0,
                _ => (finalSize.Width - size.Width) / 2
            };
            var width = HorizontalContentAlignment == HorizontalAlignment.Stretch ? Math.Max(size.Width, finalSize.Width) : size.Width;
            var y = VerticalContentAlignment switch
            {
                VerticalAlignment.Top => 0,
                VerticalAlignment.Bottom => finalSize.Height - size.Height,
                VerticalAlignment.Stretch => 0,
                _ => (finalSize.Height - size.Height) / 2
            };
            layer.Presenter.Arrange(new Rect(x, y, width, size.Height));
        }
        _current?.Presenter.Arrange(new Rect(finalSize));
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
            ResetLayer(_current);
        }
    }
}
