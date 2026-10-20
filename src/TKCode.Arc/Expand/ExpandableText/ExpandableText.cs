using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Raised when an <see cref="ExpandableText"/> opens or closes.</summary>
public sealed class ExpandableTextExpandedChangedEventArgs : RoutedEventArgs
{
    public ExpandableTextExpandedChangedEventArgs(RoutedEvent routedEvent, bool isExpanded) : base(routedEvent) => IsExpanded = isExpanded;

    /// <summary>The new state.</summary>
    public bool IsExpanded { get; }
}

/// <summary>
/// Arc expandable text: long text clamped to <see cref="Lines"/> lines with a soft fade over the last one. Read more
/// opens it to its full height with a calm ease out (500–600ms, growing with the distance) and Show less closes it
/// (400–450ms); the height, the fade and the chevron follow one progress value, so they always move in step, and a
/// click mid-flight reverses from the current height. The toggle only appears when the text really overflows, and
/// stays correct as the width or font changes. The whole text is always available to assistive technology.
/// </summary>
[TemplatePart(ViewportPart, typeof(Control))]
[TemplatePart(TogglePart, typeof(AvaloniaButton))]
[PseudoClasses(ExpandedPseudoClass, OverflowPseudoClass)]
public class ExpandableText : TemplatedControl
{
    public const string ViewportPart = "PART_Viewport";
    public const string TogglePart = "PART_Toggle";
    public const string ExpandedPseudoClass = ":expanded";
    public const string OverflowPseudoClass = ":overflow";

    /// <summary>The text (paragraphs separated by line breaks).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ExpandableText, string?>(nameof(Text));

    /// <summary>Rich text with runs and inline links (<c>InlineUIContainer</c>); used instead of <see cref="Text"/> when set.</summary>
    public static readonly StyledProperty<InlineCollection?> InlinesProperty =
        AvaloniaProperty.Register<ExpandableText, InlineCollection?>(nameof(Inlines));

    /// <summary>How many lines show while collapsed (Arc: 3).</summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<ExpandableText, int>(nameof(Lines), 3, coerce: (_, v) => Math.Max(1, v));

    /// <summary>Whether the full text shows. Two-way.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ExpandableText, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Toggle label while collapsed.</summary>
    public static readonly StyledProperty<string> MoreLabelProperty =
        AvaloniaProperty.Register<ExpandableText, string>(nameof(MoreLabel), "Read more");

    /// <summary>Toggle label while open.</summary>
    public static readonly StyledProperty<string> LessLabelProperty =
        AvaloniaProperty.Register<ExpandableText, string>(nameof(LessLabel), "Show less");

    /// <summary>True when the text is longer than <see cref="Lines"/> lines (the toggle shows).</summary>
    public static readonly DirectProperty<ExpandableText, bool> IsOverflowingProperty =
        AvaloniaProperty.RegisterDirect<ExpandableText, bool>(nameof(IsOverflowing), o => o.IsOverflowing);

    /// <summary>Line height of the text, in px (theme: text-sm × leading-body).</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ExpandableText, double>(nameof(LineHeight), double.NaN);

    public static readonly RoutedEvent<ExpandableTextExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ExpandableText, ExpandableTextExpandedChangedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TextProperty"/>
    [Content]
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="InlinesProperty"/>
    public InlineCollection? Inlines { get => GetValue(InlinesProperty); set => SetValue(InlinesProperty, value); }

    /// <inheritdoc cref="LinesProperty"/>
    public int Lines { get => GetValue(LinesProperty); set => SetValue(LinesProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="MoreLabelProperty"/>
    public string MoreLabel { get => GetValue(MoreLabelProperty); set => SetValue(MoreLabelProperty, value); }

    /// <inheritdoc cref="LessLabelProperty"/>
    public string LessLabel { get => GetValue(LessLabelProperty); set => SetValue(LessLabelProperty, value); }

    /// <inheritdoc cref="LineHeightProperty"/>
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    private bool _isOverflowing;

    /// <inheritdoc cref="IsOverflowingProperty"/>
    public bool IsOverflowing
    {
        get => _isOverflowing;
        private set
        {
            if (SetAndRaise(IsOverflowingProperty, ref _isOverflowing, value))
            {
                PseudoClasses.Set(OverflowPseudoClass, value);
            }
        }
    }

    /// <summary>Raised when the text opens or closes.</summary>
    public event EventHandler<ExpandableTextExpandedChangedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    private ExpandableTextViewport? _viewport;
    private ExpandableTextToggle? _toggle;

    static ExpandableText()
    {
        FocusableProperty.OverrideDefaultValue<ExpandableText>(false);
        AffectsMeasure<ExpandableText>(LinesProperty, LineHeightProperty);
    }

    /// <summary>The clamp viewport (tests).</summary>
    internal ExpandableTextViewport? Viewport => _viewport;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_toggle is not null)
        {
            _toggle.Click -= OnToggleClick;
        }
        if (_viewport is not null)
        {
            _viewport.OverflowChanged = null;
            _viewport.RemoveHandler(GotFocusEvent, OnViewportGotFocus);
        }
        base.OnApplyTemplate(e);
        _viewport = e.NameScope.Find<ExpandableTextViewport>(ViewportPart);
        _toggle = e.NameScope.Find<ExpandableTextToggle>(TogglePart);
        if (_toggle is not null)
        {
            _toggle.Click += OnToggleClick;
            _toggle.Owner = this;
        }
        if (_viewport is not null)
        {
            _viewport.OverflowChanged = overflowing => IsOverflowing = overflowing;
            _viewport.Lines = Lines;
            _viewport.LineHeight = LineHeight;
            _viewport.Rotated = UpdateChevron;
            _viewport.AddHandler(GotFocusEvent, OnViewportGotFocus);
            _viewport.SetExpanded(IsExpanded, animate: false);
        }
        UpdateLabels();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

    /// <summary>A link hidden below the clamp opens the text when it receives keyboard focus.</summary>
    private void OnViewportGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (IsExpanded || !IsOverflowing || _viewport is null || e.Source is not Visual target || target == _viewport)
        {
            return;
        }
        var origin = target.TranslatePoint(new Point(0, target.Bounds.Height), _viewport);
        if (origin is { } bottom && bottom.Y > _viewport.Bounds.Height + 1)
        {
            IsExpanded = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            var expanded = change.GetNewValue<bool>();
            PseudoClasses.Set(ExpandedPseudoClass, expanded);
            if (!expanded && _viewport is not null && IsLoaded)
            {
                // Closing from far down the page would leave the reader below the text: bring its start back into view.
                this.BringIntoView(new Rect(0, 0, Math.Max(1, Bounds.Width), 1));
            }
            _viewport?.SetExpanded(expanded, animate: IsLoaded);
            UpdateLabels();
            _toggle?.RaiseExpandCollapseChanged(!expanded, expanded);
            RaiseEvent(new ExpandableTextExpandedChangedEventArgs(ExpandedChangedEvent, expanded));
        }
        else if (change.Property == LinesProperty && _viewport is not null)
        {
            _viewport.Lines = Lines;
        }
        else if (change.Property == LineHeightProperty && _viewport is not null)
        {
            _viewport.LineHeight = LineHeight;
        }
        else if (change.Property == MoreLabelProperty || change.Property == LessLabelProperty)
        {
            UpdateLabels();
        }
    }

    private void UpdateLabels()
    {
        if (_toggle is not null)
        {
            AutomationProperties.SetName(_toggle, IsExpanded ? LessLabel : MoreLabel);
        }
    }

    private void UpdateChevron(double progress) => _toggle?.SetChevronAngle(180 * Math.Clamp(progress, 0, 1));
}

/// <summary>
/// The clamp of <see cref="ExpandableText"/>. At rest the height is <c>Lines × line height</c> (collapsed) or the full
/// text (open), so width and font changes need no motion. While moving, one progress value (0 clamped, 1 open) drives
/// the height, the fade over the last line and the chevron. Code-driven: the duration depends on the distance and a
/// reversal resyncs to the real height, which ControlTheme transitions cannot express.
/// </summary>
internal sealed class ExpandableTextViewport : Decorator
{
    /// <summary>Arc: a soft ease out that answers at once and spends most of its time settling.</summary>
    private static readonly Easing Ease = new SplineEasing(0.32, 0.5, 0.2, 1);

    private MotionValue<double>? _progress;
    private bool _expanded;
    private bool _animating;
    private bool _settledExpanded;
    private double _from;
    private double _to;
    private double _full;
    private double _clamp;
    private int _generation;
    private LinearGradientBrush? _mask;
    private int _lines = 3;
    private double _lineHeight = double.NaN;

    public Action<bool>? OverflowChanged { get; set; }

    public Action<double>? Rotated { get; set; }

    public bool IsOverflowing { get; private set; }

    public bool IsMoving => _animating;

    public double Progress => _progress?.Value ?? (_expanded ? 1 : 0);

    public int Lines
    {
        get => _lines;
        set
        {
            _lines = value;
            InvalidateMeasure();
        }
    }

    public double LineHeight
    {
        get => _lineHeight;
        set
        {
            _lineHeight = value;
            InvalidateMeasure();
        }
    }

    public ExpandableTextViewport()
    {
        ClipToBounds = true;
    }

    /// <summary>Height of <see cref="Lines"/> lines in the text's own line height.</summary>
    public double ClampHeight => _clamp;

    public void SetExpanded(bool expanded, bool animate)
    {
        _expanded = expanded;
        _progress ??= CreateProgress(expanded ? 1 : 0);
        var generation = ++_generation;
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || level != ArcMotionLevel.Full || !IsOverflowing || _full <= _clamp)
        {
            // Reduced motion and Off open and close at once (Arc: no motion under reduced).
            _animating = false;
            _settledExpanded = expanded;
            _progress.Jump(expanded ? 1 : 0);
            InvalidateMeasure();
            return;
        }
        var current = Bounds.Height;
        _from = _clamp;
        _to = _full;
        var span = Math.Max(_to - _from, 1);
        // Progress maps the clamp to 0 and the full height to 1; resync to the real height in case the text reflowed.
        _progress.Jump(Math.Clamp((current - _from) / span, 0, 1));
        _animating = true;
        var target = expanded ? 1.0 : 0.0;
        var distance = Math.Abs(target - _progress.Value) * span;
        var seconds = expanded ? 0.5 + Math.Min(distance / 1600, 0.1) : 0.4 + Math.Min(distance / 2000, 0.05);
        Run(target, TimeSpan.FromSeconds(seconds), generation);
    }

    private async void Run(double target, TimeSpan duration, int generation)
    {
        var done = await _progress!.AnimateTo(target, MotionSpec.Tween(duration, Ease));
        if (done && generation == _generation)
        {
            _animating = false;
            _settledExpanded = _expanded;
            InvalidateMeasure();
        }
    }

    private MotionValue<double> CreateProgress(double initial)
    {
        var value = new MotionValue<double>(this, initial, MotionVectors.Double);
        value.Changed += p =>
        {
            Rotated?.Invoke(p);
            if (_animating)
            {
                InvalidateMeasure();
            }
        };
        Rotated?.Invoke(initial);
        return value;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        child.Measure(availableSize.WithHeight(double.PositiveInfinity));
        var desired = child.DesiredSize;
        _full = desired.Height;
        var line = double.IsNaN(_lineHeight) || _lineHeight <= 0 ? MeasureLine(child) : _lineHeight;
        _clamp = line * _lines;
        var overflowing = _full > _clamp + 1;
        if (overflowing != IsOverflowing)
        {
            IsOverflowing = overflowing;
            OverflowChanged?.Invoke(overflowing);
        }
        double height;
        if (_animating)
        {
            var p = _progress?.Value ?? 0;
            height = _from + (_to - _from) * p;
        }
        else
        {
            height = _settledExpanded || !overflowing ? _full : _clamp;
        }
        UpdateMask(height, line, overflowing);
        return new Size(desired.Width, Math.Max(0, height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_full, finalSize.Height)));
        return finalSize;
    }

    private static double MeasureLine(Control child)
        => child is TextBlock { TextLayout.TextLines.Count: > 0 } text ? text.TextLayout.TextLines[0].Height : 20;

    /// <summary>
    /// The fade eases away as the text opens and returns only near the clamp: newly revealed lines rise through a
    /// soft edge (1.6 lines tall) instead of being uncovered by a hard line.
    /// </summary>
    private void UpdateMask(double height, double line, bool overflowing)
    {
        var masked = overflowing && (!_settledExpanded || _animating);
        var p = Math.Clamp(_progress?.Value ?? 0, 0, 1);
        var fade = Math.Pow(1 - p, 1.35);
        if (!masked || fade <= 0.002 || height <= 0)
        {
            if (OpacityMask is not null)
            {
                OpacityMask = null;
            }
            return;
        }
        if (_mask is null)
        {
            _mask = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative)
            };
            _mask.GradientStops.Add(new GradientStop(Colors.Black, 0));
            _mask.GradientStops.Add(new GradientStop(Colors.Black, 0));
            _mask.GradientStops.Add(new GradientStop(Colors.Black, 1));
        }
        var stops = _mask.GradientStops;
        stops[1].Offset = Math.Clamp((height - 1.6 * line) / height, 0, 1);
        stops[2].Color = Color.FromArgb((byte)Math.Round(255 * (1 - fade * 0.96)), 0, 0, 0);
        if (!ReferenceEquals(OpacityMask, _mask))
        {
            OpacityMask = _mask;
        }
    }
}

/// <summary>The Read more / Show less button: both labels share one cell so the width never changes.</summary>
[PseudoClasses(ExpandedPseudoClass)]
internal sealed class ExpandableTextToggle : AvaloniaButton
{
    public const string ExpandedPseudoClass = ":expanded";

    public static readonly StyledProperty<string?> MoreTextProperty =
        AvaloniaProperty.Register<ExpandableTextToggle, string?>(nameof(MoreText));

    public static readonly StyledProperty<string?> LessTextProperty =
        AvaloniaProperty.Register<ExpandableTextToggle, string?>(nameof(LessText));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ExpandableTextToggle, bool>(nameof(IsOpen));

    public string? MoreText { get => GetValue(MoreTextProperty); set => SetValue(MoreTextProperty, value); }

    public string? LessText { get => GetValue(LessTextProperty); set => SetValue(LessTextProperty, value); }

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    private RotateTransform? _rotate;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, IsOpen);
        }
    }

    public ExpandableText? Owner { get; set; }

    protected override Type StyleKeyOverride => typeof(ExpandableTextToggle);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Control>("PART_Chevron") is { } chevron)
        {
            _rotate = new RotateTransform();
            chevron.RenderTransformOrigin = RelativePoint.Center;
            chevron.RenderTransform = _rotate;
            if (Owner?.Viewport is { } viewport)
            {
                _rotate.Angle = 180 * Math.Clamp(viewport.Progress, 0, 1);
            }
        }
    }

    public void SetChevronAngle(double angle)
    {
        if (_rotate is not null)
        {
            _rotate.Angle = angle;
        }
    }

    internal void RaiseExpandCollapseChanged(bool oldValue, bool newValue)
    {
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ToggleAutomationPeer(this);

    private sealed class ToggleAutomationPeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public ToggleAutomationPeer(ExpandableTextToggle owner) : base(owner)
        {
        }

        private ExpandableText? Text => ((ExpandableTextToggle)Owner).Owner;

        public ExpandCollapseState ExpandCollapseState => Text?.IsExpanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Text is { } text)
            {
                text.IsExpanded = true;
            }
        }

        public void Collapse()
        {
            if (Text is { } text)
            {
                text.IsExpanded = false;
            }
        }
    }
}
