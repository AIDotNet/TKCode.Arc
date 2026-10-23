using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The compact capacity meter of the agent memory header: a right-aligned line ("62% full") over a 96 x 4 track whose
/// fill shows <see cref="Used"/> out of <see cref="Capacity"/>. The fill and the number follow one critically damped
/// spring (Smooth), and the number changes only when its whole percent does, in tabular figures in a slot as wide as
/// "100%", so the line never jumps. From <see cref="WarningRatio"/> on (<see cref="IsWarning"/>, <c>:warning</c>) a
/// triangle-alert glyph comes in front of the text, and the text, number, glyph and fill turn to the warning color at
/// once (no color transition). Reduced motion and Off jump. Screen readers get a progress bar (Arc's
/// <c>role="meter"</c>) named <see cref="AccessibleName"/> with the percentage as its value and <see cref="ValueText"/>
/// as its value text; the visible line is not read separately.
/// The meter takes its words from its owner and is never narrower than 96px. A capacity of zero or less shows 0%; hiding
/// the meter then is the owner's choice.
/// </summary>
[TemplatePart(BarPart, typeof(MemoryMeterBar))]
[TemplatePart(ValuePart, typeof(TextBlock))]
[TemplatePart(ValueReservePart, typeof(TextBlock))]
[PseudoClasses(WarningPseudoClass)]
internal class MemoryMeter : TemplatedControl
{
    public const string BarPart = "PART_Bar";
    public const string ValuePart = "PART_Value";
    public const string ValueReservePart = "PART_ValueReserve";

    public const string WarningPseudoClass = ":warning";

    #region Properties

    /// <summary>How much of the capacity is used (characters for the agent memory).</summary>
    public static readonly StyledProperty<double> UsedProperty =
        AvaloniaProperty.Register<MemoryMeter, double>(nameof(Used));

    /// <summary>The whole capacity; zero or less shows 0%.</summary>
    public static readonly StyledProperty<double> CapacityProperty =
        AvaloniaProperty.Register<MemoryMeter, double>(nameof(Capacity));

    /// <summary>From this share of <see cref="Capacity"/> on the meter turns to the warning color (0.9).</summary>
    public static readonly StyledProperty<double> WarningRatioProperty =
        AvaloniaProperty.Register<MemoryMeter, double>(nameof(WarningRatio), 0.9);

    /// <summary>The word after the percentage ("full").</summary>
    public static readonly StyledProperty<string?> FullTextProperty =
        AvaloniaProperty.Register<MemoryMeter, string?>(nameof(FullText));

    /// <summary>The accessible name of the meter ("Memory used").</summary>
    public static readonly StyledProperty<string?> AccessibleNameProperty =
        AvaloniaProperty.Register<MemoryMeter, string?>(nameof(AccessibleName));

    /// <summary>The accessible value text for a percentage ("62% full"); null reads the percentage alone.</summary>
    public static readonly StyledProperty<Func<int, string>?> ValueTextProperty =
        AvaloniaProperty.Register<MemoryMeter, Func<int, string>?>(nameof(ValueText));

    /// <summary>The whole percent on screen right now; it follows the spring, so it lags a change until the fill settles.</summary>
    public static readonly DirectProperty<MemoryMeter, int> DisplayedPercentProperty =
        AvaloniaProperty.RegisterDirect<MemoryMeter, int>(nameof(DisplayedPercent), o => o.DisplayedPercent);

    /// <summary>True from <see cref="WarningRatio"/> of the capacity on (follows the data at once, not the spring).</summary>
    public static readonly DirectProperty<MemoryMeter, bool> IsWarningProperty =
        AvaloniaProperty.RegisterDirect<MemoryMeter, bool>(nameof(IsWarning), o => o.IsWarning);

    /// <inheritdoc cref="UsedProperty"/>
    public double Used { get => GetValue(UsedProperty); set => SetValue(UsedProperty, value); }

    /// <inheritdoc cref="CapacityProperty"/>
    public double Capacity { get => GetValue(CapacityProperty); set => SetValue(CapacityProperty, value); }

    /// <inheritdoc cref="WarningRatioProperty"/>
    public double WarningRatio { get => GetValue(WarningRatioProperty); set => SetValue(WarningRatioProperty, value); }

    /// <inheritdoc cref="FullTextProperty"/>
    public string? FullText { get => GetValue(FullTextProperty); set => SetValue(FullTextProperty, value); }

    /// <inheritdoc cref="AccessibleNameProperty"/>
    public string? AccessibleName { get => GetValue(AccessibleNameProperty); set => SetValue(AccessibleNameProperty, value); }

    /// <inheritdoc cref="ValueTextProperty"/>
    public Func<int, string>? ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    /// <inheritdoc cref="DisplayedPercentProperty"/>
    public int DisplayedPercent
    {
        get => _displayedPercent;
        private set => SetAndRaise(DisplayedPercentProperty, ref _displayedPercent, value);
    }

    /// <inheritdoc cref="IsWarningProperty"/>
    public bool IsWarning
    {
        get => _isWarning;
        private set => SetAndRaise(IsWarningProperty, ref _isWarning, value);
    }

    #endregion

    /// <summary>"0%" to "100%", made once: the number changes text without allocating.</summary>
    private static readonly string[] s_percentTexts = Enumerable.Range(0, 101).Select(static p => p + "%").ToArray();

    private readonly MotionValue<double> _fill;
    private MemoryMeterBar? _bar;
    private TextBlock? _value;
    private int _displayedPercent;
    private bool _isWarning;
    private double _targetRatio;
    private int _targetPercent;
    private bool _attached;

    static MemoryMeter()
    {
        FocusableProperty.OverrideDefaultValue<MemoryMeter>(false);
    }

    public MemoryMeter()
    {
        // Code-driven: the fill's target is the share the data sets, and the number reads the same spring value every
        // frame (it has to round what the fill shows), which a ControlTheme transition cannot express.
        _fill = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fill.Changed += OnFillChanged;
    }

    /// <summary>The share of the track the fill covers right now (0 to 1).</summary>
    internal double FillRatio => _fill.Value;

    /// <summary>The share the fill is heading to (0 to 1).</summary>
    internal double TargetRatio => _targetRatio;

    /// <summary>The whole percent of the data (what screen readers get), clamped to 0..100.</summary>
    internal int TargetPercent => _targetPercent;

    /// <summary>True while the fill is moving.</summary>
    internal bool IsMoving => _fill.IsAnimating;

    /// <summary>The percent text of the number ("62%").</summary>
    internal static string PercentText(int percent) => s_percentTexts[Math.Clamp(percent, 0, 100)];

    /// <summary>
    /// The whole percent of a share, rounded down: "90%" never shows before the warning starts at 0.9, and "100%" only
    /// when the capacity is really used up. A tiny margin keeps 0.29 (28.999...%) at 29.
    /// </summary>
    internal static int ToPercent(double ratio) =>
        double.IsNaN(ratio) ? 0 : (int)Math.Clamp(Math.Floor(ratio * 100 + 1e-7), 0, 100);

    #region Data

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == UsedProperty || change.Property == CapacityProperty || change.Property == WarningRatioProperty)
        {
            Update(animate: true);
        }
        else if (change.Property == AccessibleNameProperty)
        {
            AutomationProperties.SetName(this, AccessibleName);
        }
        else if (change.Property == ValueTextProperty)
        {
            RaiseValueTextChanged(_targetPercent, _targetPercent, force: true);
        }
    }

    /// <summary>Moves the fill (and so the number) to the data, and turns the warning on or off at once.</summary>
    private void Update(bool animate)
    {
        var capacity = Capacity;
        var used = Used;
        var share = capacity > 0 && !double.IsNaN(used) ? used / capacity : 0;
        if (double.IsNaN(share) || share < 0)
        {
            share = 0;
        }
        IsWarning = capacity > 0 && share >= WarningRatio;
        PseudoClasses.Set(WarningPseudoClass, IsWarning);
        var ratio = Math.Min(1, share);
        var oldPercent = _targetPercent;
        _targetRatio = ratio;
        _targetPercent = ToPercent(ratio);
        if (Math.Abs(_fill.Target - ratio) > 1e-9)
        {
            if (animate && _attached)
            {
                // Off and Reduced jump (MotionValue skips springs below Full).
                _ = _fill.AnimateTo(ratio, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                _fill.Jump(ratio);
            }
        }
        RaiseValueTextChanged(oldPercent, _targetPercent, force: false);
    }

    private void OnFillChanged(double ratio)
    {
        if (_bar is not null)
        {
            _bar.Ratio = ratio;
        }
        var percent = ToPercent(ratio);
        if (percent != _displayedPercent)
        {
            // The text first, so whoever hears DisplayedPercent change already sees it on screen.
            if (_value is not null)
            {
                _value.Text = PercentText(percent);
            }
            DisplayedPercent = percent;
        }
    }

    private void RaiseValueTextChanged(int oldPercent, int newPercent, bool force)
    {
        if ((!force && oldPercent == newPercent) || ControlAutomationPeer.FromElement(this) is not MeterAutomationPeer peer)
        {
            return;
        }
        if (oldPercent != newPercent)
        {
            peer.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty, (double)oldPercent, (double)newPercent);
        }
        peer.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, ValueTextFor(oldPercent), ValueTextFor(newPercent));
    }

    /// <summary>The accessible value text for <paramref name="percent"/>.</summary>
    internal string ValueTextFor(int percent) => ValueText?.Invoke(percent) ?? PercentText(percent);

    #endregion

    #region Template and lifetime

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _bar = e.NameScope.Find<MemoryMeterBar>(BarPart);
        _value = e.NameScope.Find<TextBlock>(ValuePart);
        // The reserve keeps the number's slot as wide as the widest value, so a change never moves the line.
        if (e.NameScope.Find<TextBlock>(ValueReservePart) is { } reserve)
        {
            reserve.Text = PercentText(100);
        }
        if (_bar is not null)
        {
            _bar.Ratio = _fill.Value;
        }
        if (_value is not null)
        {
            _value.Text = PercentText(_displayedPercent);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        // A meter that leaves mid-spring comes back showing the data, not a stale frame.
        if (_fill.IsAnimating)
        {
            _fill.Jump(_targetRatio);
        }
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new MeterAutomationPeer(this);

    /// <summary>
    /// Arc's <c>role="meter"</c>: a read-only progress bar from 0 to 100 whose value is the data's percent (not the
    /// spring's), with <see cref="ValueText"/> as its value text (<c>aria-valuetext</c>). The visible line and the glyph are
    /// not exposed (<c>aria-hidden</c>).
    /// </summary>
    private sealed class MeterAutomationPeer(MemoryMeter owner) : ControlAutomationPeer(owner), IRangeValueProvider, IValueProvider
    {
        private MemoryMeter Meter => (MemoryMeter)Owner;

        public bool IsReadOnly => true;

        public double Minimum => 0;

        public double Maximum => 100;

        double IRangeValueProvider.Value => Meter._targetPercent;

        string? IValueProvider.Value => Meter.ValueTextFor(Meter._targetPercent);

        public double SmallChange => 1;

        public double LargeChange => 10;

        public void SetValue(double value)
        {
        }

        public void SetValue(string? value)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ProgressBar;

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
