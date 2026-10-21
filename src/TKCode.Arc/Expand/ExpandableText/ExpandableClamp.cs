using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Raised when an <see cref="ExpandableText"/> or <see cref="ExpandableContent"/> opens or closes.</summary>
public sealed class ExpandableTextExpandedChangedEventArgs : RoutedEventArgs
{
    public ExpandableTextExpandedChangedEventArgs(RoutedEvent routedEvent, bool isExpanded) : base(routedEvent) => IsExpanded = isExpanded;

    /// <summary>The new state.</summary>
    public bool IsExpanded { get; }
}

/// <summary>
/// Content of an <see cref="ExpandableContent"/> that lays out its own lines, such as a custom text viewer. The clamp
/// then ends on one of its line boundaries, and a long text is laid out only as far as the collapsed view needs.
/// <see cref="TextBlock"/> and <see cref="SelectableTextBlock"/> need no implementation: the clamp reads their lines.
/// </summary>
public interface IExpandableLineSource
{
    /// <summary>
    /// Distance from the top of the source to the bottom of its first <paramref name="lines"/> lines, after its last
    /// measure; its full height when it has fewer lines.
    /// </summary>
    double GetLinesBottom(int lines);

    /// <summary>
    /// How many lines the source has to lay out: <c>Lines + 1</c> while the clamp rests collapsed (enough to tell that
    /// the text overflows), 0 for all of them. Ignoring the hint is correct, only slower for long text.
    /// </summary>
    void LimitLayout(int lines);
}

/// <summary>
/// The line clamp shared by <see cref="ExpandableText"/> and <see cref="ExpandableContent"/>: content clamped to
/// <see cref="Lines"/> lines with a soft fade over the last one. Read more opens it to its full height with a calm ease
/// out (500–600ms, growing with the distance) and Show less closes it (400–450ms); the height, the fade and the chevron
/// follow one progress value, so they always move in step, and a click mid-flight reverses from the current height. The
/// toggle only appears when the content really overflows, and stays correct as the width or font changes. A change that
/// arrives with a new data context (a recycled item template) applies at once, without motion.
/// </summary>
[TemplatePart(ViewportPart, typeof(Control))]
[TemplatePart(TogglePart, typeof(AvaloniaButton))]
[PseudoClasses(ExpandedPseudoClass, OverflowPseudoClass)]
public abstract class ExpandableClamp : TemplatedControl
{
    public const string ViewportPart = "PART_Viewport";
    public const string TogglePart = "PART_Toggle";
    public const string ExpandedPseudoClass = ":expanded";
    public const string OverflowPseudoClass = ":overflow";

    /// <summary>How many lines show while collapsed (Arc: 3).</summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<ExpandableClamp, int>(nameof(Lines), 3, coerce: (_, v) => Math.Max(1, v));

    /// <summary>Whether the full content shows. Two-way.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ExpandableClamp, bool>(nameof(IsExpanded), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Toggle label while collapsed.</summary>
    public static readonly StyledProperty<string> MoreLabelProperty =
        AvaloniaProperty.Register<ExpandableClamp, string>(nameof(MoreLabel), "Read more");

    /// <summary>Toggle label while open.</summary>
    public static readonly StyledProperty<string> LessLabelProperty =
        AvaloniaProperty.Register<ExpandableClamp, string>(nameof(LessLabel), "Show less");

    /// <summary>True when the content is longer than <see cref="Lines"/> lines (the toggle shows).</summary>
    public static readonly DirectProperty<ExpandableClamp, bool> IsOverflowingProperty =
        AvaloniaProperty.RegisterDirect<ExpandableClamp, bool>(nameof(IsOverflowing), o => o.IsOverflowing);

    /// <summary>
    /// Line height in px. <see cref="ExpandableText"/> lays its text out at this height (theme: text-sm × leading-body);
    /// <see cref="ExpandableContent"/> clamps at <c>Lines × LineHeight</c> when it is set, which is how content that
    /// is not text (rendered Markdown, a list) is clamped.
    /// </summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<ExpandableClamp, double>(nameof(LineHeight), double.NaN);

    /// <summary>Whether the last visible line fades out while collapsed (Arc: on).</summary>
    public static readonly StyledProperty<bool> IsFadeEnabledProperty =
        AvaloniaProperty.Register<ExpandableClamp, bool>(nameof(IsFadeEnabled), true);

    public static readonly RoutedEvent<ExpandableTextExpandedChangedEventArgs> ExpandedChangedEvent =
        RoutedEvent.Register<ExpandableClamp, ExpandableTextExpandedChangedEventArgs>(nameof(ExpandedChanged), RoutingStrategies.Bubble);

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

    /// <inheritdoc cref="IsFadeEnabledProperty"/>
    public bool IsFadeEnabled { get => GetValue(IsFadeEnabledProperty); set => SetValue(IsFadeEnabledProperty, value); }

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

    /// <summary>Raised when the content opens or closes.</summary>
    public event EventHandler<ExpandableTextExpandedChangedEventArgs>? ExpandedChanged
    {
        add => AddHandler(ExpandedChangedEvent, value);
        remove => RemoveHandler(ExpandedChangedEvent, value);
    }

    private ExpandableTextViewport? _viewport;
    private ExpandableTextToggle? _toggle;
    private int _dataContextUpdates;

    static ExpandableClamp()
    {
        FocusableProperty.OverrideDefaultValue<ExpandableClamp>(false);
        AffectsMeasure<ExpandableClamp>(LinesProperty, LineHeightProperty);
    }

    /// <summary>The clamp viewport (tests).</summary>
    internal ExpandableTextViewport? Viewport => _viewport;

    /// <summary>The control whose lines the clamp counts; null finds one in the content.</summary>
    private protected virtual Control? GetLineSource(INameScope scope) => null;

    /// <summary>The clamp's fixed line height; NaN clamps at the line source's own lines.</summary>
    private protected virtual double ClampLineHeight => LineHeight;

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
            _viewport.Source = GetLineSource(e.NameScope);
            _viewport.Lines = Lines;
            _viewport.LineHeight = ClampLineHeight;
            _viewport.IsFadeEnabled = IsFadeEnabled;
            _viewport.Rotated = UpdateChevron;
            _viewport.AddHandler(GotFocusEvent, OnViewportGotFocus);
            _viewport.SetExpanded(IsExpanded, animate: false);
        }
        UpdateLabels();
    }

    private void OnToggleClick(object? sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;

    /// <summary>A link hidden below the clamp opens the content when it receives keyboard focus.</summary>
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

    protected override void OnDataContextBeginUpdate()
    {
        base.OnDataContextBeginUpdate();
        _dataContextUpdates++;
    }

    protected override void OnDataContextEndUpdate()
    {
        base.OnDataContextEndUpdate();
        _dataContextUpdates--;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty)
        {
            var expanded = change.GetNewValue<bool>();
            // A new item bound into a recycled template is not a click: no motion, and no scrolling.
            var animate = IsLoaded && _dataContextUpdates == 0;
            PseudoClasses.Set(ExpandedPseudoClass, expanded);
            if (!expanded && _viewport is not null && animate)
            {
                // Closing from far down the page would leave the reader below the content: bring its start back into view.
                this.BringIntoView(new Rect(0, 0, Math.Max(1, Bounds.Width), 1));
            }
            _viewport?.SetExpanded(expanded, animate);
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
            _viewport.LineHeight = ClampLineHeight;
        }
        else if (change.Property == IsFadeEnabledProperty && _viewport is not null)
        {
            _viewport.IsFadeEnabled = IsFadeEnabled;
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
/// The clamp of <see cref="ExpandableClamp"/>. At rest the height is the bottom of line <c>Lines</c> (collapsed) or the
/// full content (open), so width and font changes need no motion. While moving, one progress value (0 clamped, 1 open)
/// drives the height, the fade over the last line and the chevron. Code-driven: the duration depends on the distance
/// and a reversal resyncs to the real height, which ControlTheme transitions cannot express.
/// </summary>
/// <remarks>
/// The clamp ends on a line of the line source: <see cref="Source"/>, else the first visible text (a
/// <see cref="TextBlock"/> or an <see cref="IExpandableLineSource"/>) inside plain containers of the content. A fixed
/// <see cref="LineHeight"/> clamps at <c>Lines × LineHeight</c> instead. While a text rests collapsed it is laid out
/// only to line <c>Lines + 1</c> (a long text costs as much as a short one); opening lays it out in full first.
/// </remarks>
internal sealed class ExpandableTextViewport : Decorator
{
    /// <summary>Arc: a soft ease out that answers at once and spends most of its time settling.</summary>
    private static readonly Easing Ease = new SplineEasing(0.32, 0.5, 0.2, 1);

    /// <summary>Clamp line height when nothing reports one (a line of text-sm × leading-body, rounded).</summary>
    private const double FallbackLineHeight = 20;

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
    private bool _fade = true;
    private Control? _source;
    private Control? _resolved;
    private double _sourceTop;
    private bool _limited;
    private Size? _constraint;

    public Action<bool>? OverflowChanged { get; set; }

    public Action<double>? Rotated { get; set; }

    public bool IsOverflowing { get; private set; }

    public bool IsMoving => _animating;

    public double Progress => _progress?.Value ?? (_expanded ? 1 : 0);

    /// <summary>True while the line source is laid out only past the clamp (tests).</summary>
    public bool IsLayoutLimited => _limited;

    /// <summary>The control whose lines the clamp counts (ExpandableText: its text); null finds one in the content.</summary>
    public Control? Source
    {
        get => _source;
        set
        {
            _source = value;
            InvalidateMeasure();
        }
    }

    public int Lines
    {
        get => _lines;
        set
        {
            _lines = value;
            InvalidateMeasure();
        }
    }

    /// <summary>Fixed clamp line height; NaN clamps at the line source's own lines.</summary>
    public double LineHeight
    {
        get => _lineHeight;
        set
        {
            _lineHeight = value;
            InvalidateMeasure();
        }
    }

    public bool IsFadeEnabled
    {
        get => _fade;
        set
        {
            _fade = value;
            InvalidateMeasure();
        }
    }

    public ExpandableTextViewport()
    {
        ClipToBounds = true;
    }

    /// <summary>Height of the first <see cref="Lines"/> lines.</summary>
    public double ClampHeight => _clamp;

    private bool HasFixedLineHeight => !double.IsNaN(_lineHeight) && _lineHeight > 0;

    public void SetExpanded(bool expanded, bool animate)
    {
        _expanded = expanded;
        _progress ??= CreateProgress(expanded ? 1 : 0);
        var generation = ++_generation;
        if (expanded)
        {
            // The open height is only known once the whole text is laid out.
            LayOutInFull();
        }
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

    private void LayOutInFull()
    {
        if (!_limited)
        {
            return;
        }
        _limited = false;
        Limit(_resolved, 0);
        if (Child is { } child && _constraint is { } constraint)
        {
            child.Measure(constraint);
            _full = child.DesiredSize.Height;
        }
        InvalidateMeasure();
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
        var constraint = availableSize.WithHeight(double.PositiveInfinity);
        _constraint = constraint;
        var restsCollapsed = !_settledExpanded && !_animating && !_expanded;
        _resolved = HasFixedLineHeight ? null : _source ?? FindLineSource(child);
        ApplyLimit(restsCollapsed);
        child.Measure(constraint);
        if (_resolved is null && !HasFixedLineHeight && _source is null && FindLineSource(child) is { } found)
        {
            // The content was created during this measure: clamp at its lines from now on.
            _resolved = found;
            if (ApplyLimit(restsCollapsed))
            {
                child.Measure(constraint);
            }
        }
        _full = child.DesiredSize.Height;
        _clamp = HasFixedLineHeight ? _lineHeight * _lines
            : _resolved is not null ? _sourceTop + LinesBottom(_resolved, _lines)
            : FallbackLineHeight * _lines;
        var line = _clamp / _lines;
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
        return new Size(child.DesiredSize.Width, Math.Max(0, height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(_full, finalSize.Height)));
            // Lines are counted from the top of the line source; the content may place it lower (padding, a header).
            if (_resolved is { } source && !ReferenceEquals(source, child))
            {
                var top = source.TranslatePoint(default, this)?.Y ?? 0;
                if (Math.Abs(top - _sourceTop) > 0.5)
                {
                    _sourceTop = top;
                    InvalidateMeasure();
                }
            }
            else
            {
                _sourceTop = 0;
            }
        }
        return finalSize;
    }

    /// <summary>Lays the line source out only to line <c>Lines + 1</c> while collapsed, in full otherwise.</summary>
    private bool ApplyLimit(bool restsCollapsed)
    {
        var limit = restsCollapsed && CanLimit(_resolved) ? _lines + 1 : 0;
        _limited = limit > 0;
        return Limit(_resolved, limit);
    }

    private static bool Limit(Control? source, int lines)
    {
        switch (source)
        {
            case IExpandableLineSource custom:
                custom.LimitLayout(lines);
                return true;
            case TextBlock text when text.MaxLines != lines:
                text.SetCurrentValue(TextBlock.MaxLinesProperty, lines);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Controls inside the text (<see cref="InlineUIContainer"/>) are laid out in full, so a link below the clamp can
    /// still take focus and open the text.
    /// </summary>
    private static bool CanLimit(Control? source) =>
        source is IExpandableLineSource || (source is TextBlock text && !HasInlineControls(text.Inlines));

    private static bool HasInlineControls(InlineCollection? inlines)
    {
        if (inlines is null)
        {
            return false;
        }
        foreach (var inline in inlines)
        {
            if (inline is InlineUIContainer || (inline is Span span && HasInlineControls(span.Inlines)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The first visible text of the content. Only plain containers are searched (panels, decorators, presenters,
    /// content controls); other content implements <see cref="IExpandableLineSource"/> or sets a fixed line height.
    /// </summary>
    private static Control? FindLineSource(Visual visual)
    {
        if (visual is Control control and (IExpandableLineSource or TextBlock))
        {
            return control;
        }
        if (visual is ContentPresenter { Child: null, Content: not null } presenter)
        {
            presenter.UpdateChild();
        }
        if (visual is not (Panel or Decorator or ContentPresenter or ContentControl))
        {
            return null;
        }
        foreach (var child in visual.GetVisualChildren())
        {
            if (child.IsVisible && FindLineSource(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static double LinesBottom(Control source, int lines)
    {
        if (source is IExpandableLineSource custom)
        {
            return custom.GetLinesBottom(lines);
        }
        if (source is not TextBlock text)
        {
            return FallbackLineHeight * lines;
        }
        var textLines = text.TextLayout.TextLines;
        var bottom = text.Padding.Top;
        var count = Math.Min(lines, textLines.Count);
        for (var i = 0; i < count; i++)
        {
            bottom += textLines[i].Height;
        }
        return count < lines ? bottom + text.Padding.Bottom : bottom;
    }

    /// <summary>
    /// The fade eases away as the text opens and returns only near the clamp: newly revealed lines rise through a
    /// soft edge (1.6 lines tall) instead of being uncovered by a hard line.
    /// </summary>
    private void UpdateMask(double height, double line, bool overflowing)
    {
        var masked = _fade && overflowing && (!_settledExpanded || _animating);
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

    public ExpandableClamp? Owner { get; set; }

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

        private ExpandableClamp? Clamp => ((ExpandableTextToggle)Owner).Owner;

        public ExpandCollapseState ExpandCollapseState => Clamp?.IsExpanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (Clamp is { } clamp)
            {
                clamp.IsExpanded = true;
            }
        }

        public void Collapse()
        {
            if (Clamp is { } clamp)
            {
                clamp.IsExpanded = false;
            }
        }
    }
}
