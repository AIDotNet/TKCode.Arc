using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Color of an <see cref="ExpandingButtonGroupItem"/> while expanded.</summary>
public enum ExpandingButtonGroupTone
{
    Neutral,

    /// <summary>Danger color while expanded, for destructive actions such as Delete.</summary>
    Danger
}

/// <summary>
/// One action of an <see cref="ExpandingButtonGroup"/>: an icon that grows to reveal its label. The width rides the
/// morph spring between the icon and the icon plus the longer of <see cref="Label"/> and <see cref="DoneLabel"/>; the
/// label slides out from behind the icon and tucks back faster. Code-driven because the reveal is asymmetric (240ms
/// with a delay in, 120ms out) and blurs, which a ControlTheme transition cannot express.
/// </summary>
[TemplatePart(IconPart, typeof(Control))]
[TemplatePart(LabelPart, typeof(Control))]
[TemplatePart(RowPart, typeof(StackPanel))]
[PseudoClasses(ExpandedPseudoClass, DonePseudoClass, BusyPseudoClass, UnavailablePseudoClass, SmallPseudoClass)]
public class ExpandingButtonGroupItem : AvaloniaButton
{
    public const string IconPart = "PART_Icon";
    public const string LabelPart = "PART_LabelHost";
    public const string RowPart = "PART_Row";
    public const string ExpandedPseudoClass = ":expanded";
    public const string DonePseudoClass = ":done";
    public const string BusyPseudoClass = ":busy";
    public const string UnavailablePseudoClass = ":unavailable";
    public const string SmallPseudoClass = ":small";

    #region 公共属性定义

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ExpandingButtonGroupItem, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<ExpandingButtonGroupItem, Geometry?>(nameof(Icon));

    public static readonly StyledProperty<string?> DoneLabelProperty =
        AvaloniaProperty.Register<ExpandingButtonGroupItem, string?>(nameof(DoneLabel));

    public static readonly StyledProperty<ExpandingButtonGroupTone> ToneProperty =
        AvaloniaProperty.Register<ExpandingButtonGroupItem, ExpandingButtonGroupTone>(nameof(Tone));

    public static readonly StyledProperty<bool> IsDisabledProperty =
        AvaloniaProperty.Register<ExpandingButtonGroupItem, bool>(nameof(IsDisabled));

    public static readonly StyledProperty<Func<Task<bool>>?> AsyncActionProperty =
        AvaloniaProperty.Register<ExpandingButtonGroupItem, Func<Task<bool>>?>(nameof(AsyncAction));

    /// <summary>Inset after the label when expanded (theme: 14 / 11px).</summary>
    internal static readonly StyledProperty<double> TrailingInsetProperty =
        AvaloniaProperty.Register<ExpandingButtonGroupItem, double>(nameof(TrailingInset), 14);

    public static readonly DirectProperty<ExpandingButtonGroupItem, bool> IsExpandedProperty =
        AvaloniaProperty.RegisterDirect<ExpandingButtonGroupItem, bool>(nameof(IsExpanded), o => o.IsExpanded);

    public static readonly DirectProperty<ExpandingButtonGroupItem, bool> IsDoneProperty =
        AvaloniaProperty.RegisterDirect<ExpandingButtonGroupItem, bool>(nameof(IsDone), o => o.IsDone);

    public static readonly DirectProperty<ExpandingButtonGroupItem, bool> IsBusyProperty =
        AvaloniaProperty.RegisterDirect<ExpandingButtonGroupItem, bool>(nameof(IsBusy), o => o.IsBusy);

    internal static readonly DirectProperty<ExpandingButtonGroupItem, string> WordProperty =
        AvaloniaProperty.RegisterDirect<ExpandingButtonGroupItem, string>(nameof(Word), o => o.Word);

    internal static readonly DirectProperty<ExpandingButtonGroupItem, IReadOnlyList<string>> SizerLabelsProperty =
        AvaloniaProperty.RegisterDirect<ExpandingButtonGroupItem, IReadOnlyList<string>>(nameof(SizerLabels), o => o.SizerLabels);

    /// <summary>A short verb, visible once expanded and the accessible name at all times.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>16px Lucide glyph shown while collapsed (sized by the group).</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Past tense word shown in place after the action (such as "Archived"); omit for actions that open something.</summary>
    public string? DoneLabel { get => GetValue(DoneLabelProperty); set => SetValue(DoneLabelProperty, value); }

    public ExpandingButtonGroupTone Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }

    /// <summary>Dimmed and inert, but still focusable and still reveals its label, so people can learn what it would do.</summary>
    public bool IsDisabled { get => GetValue(IsDisabledProperty); set => SetValue(IsDisabledProperty, value); }

    /// <summary>
    /// Runs the action. The button is busy until the task completes; returning false, or throwing, skips the
    /// confirmation. Without it, <see cref="AvaloniaButton.Click"/> and <see cref="AvaloniaButton.Command"/> count as success.
    /// </summary>
    public Func<Task<bool>>? AsyncAction { get => GetValue(AsyncActionProperty); set => SetValue(AsyncActionProperty, value); }

    internal double TrailingInset { get => GetValue(TrailingInsetProperty); set => SetValue(TrailingInsetProperty, value); }

    private bool _isExpanded;

    /// <summary>True while this action shows its label.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        private set => SetAndRaise(IsExpandedProperty, ref _isExpanded, value);
    }

    private bool _isDone;

    /// <summary>True while the confirmation shows.</summary>
    public bool IsDone
    {
        get => _isDone;
        private set => SetAndRaise(IsDoneProperty, ref _isDone, value);
    }

    private bool _isBusy;

    /// <summary>True while <see cref="AsyncAction"/> runs.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetAndRaise(IsBusyProperty, ref _isBusy, value);
    }

    private string _word = string.Empty;

    internal string Word
    {
        get => _word;
        private set => SetAndRaise(WordProperty, ref _word, value);
    }

    private IReadOnlyList<string> _sizerLabels = [];

    internal IReadOnlyList<string> SizerLabels
    {
        get => _sizerLabels;
        private set => SetAndRaise(SizerLabelsProperty, ref _sizerLabels, value);
    }

    #endregion

    internal ExpandingButtonGroup? Group { get; set; }

    private ButtonsGlyphSwap? _icon;
    private Control? _labelHost;
    private StackPanel? _row;
    private readonly TranslateTransform _labelShift = new();
    private readonly BlurEffect _labelBlur = new() { Radius = 0 };
    private MotionValue<double>? _width;
    private MotionValue<double>? _reveal;
    private double _target;
    private bool _small;

    static ExpandingButtonGroupItem()
    {
        AffectsMeasure<ExpandingButtonGroupItem>(TrailingInsetProperty);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _icon = e.NameScope.Find<ButtonsGlyphSwap>(IconPart);
        _labelHost = e.NameScope.Find<Control>(LabelPart);
        _row = e.NameScope.Find<StackPanel>(RowPart);
        if (_labelHost is not null)
        {
            _labelHost.RenderTransform = _labelShift;
            ApplyReveal(IsExpanded ? 1 : 0, full: false);
        }
        _icon?.Show(CreateGlyph(), default, default);
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        UpdateText();
    }

    private void UpdateText()
    {
        Word = IsDone && !string.IsNullOrEmpty(DoneLabel) ? DoneLabel! : Label;
        SizerLabels = string.IsNullOrEmpty(DoneLabel) ? [Label] : [Label, DoneLabel!];
        // Collapsed it is still named by the full label; the visible label and icon are hidden from assistive tech.
        AutomationProperties.SetName(this, Label);
    }

    private Control? CreateGlyph()
    {
        var size = _small ? 14 : 16;
        if (IsDone && !string.IsNullOrEmpty(DoneLabel))
        {
            var check = new StrokeDrawGlyph { Data = ArcIcons.Parse("M4 12.5l5 5L20 6.5"), Size = size, StrokeThickness = 2 };
            if (IsLoaded && ArcMotion.IsFull(this))
            {
                // The confirmation tick draws itself from its short stroke.
                check.Progress = 0;
                check.AttachedToVisualTree += (_, _) => _ = check.Play(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromSeconds(0.06));
            }
            return check;
        }
        return Icon is null ? null : new ArcGlyph { Data = Icon, Size = size, StrokeThickness = 1.75 };
    }

    private static readonly ButtonsGlyphPose IconPose = new(Scale: 0.55, Blur: 2);

    internal void SetSmall(bool small)
    {
        if (_small == small && PseudoClasses.Contains(SmallPseudoClass) == small)
        {
            return;
        }
        _small = small;
        PseudoClasses.Set(SmallPseudoClass, small);
        _icon?.Show(CreateGlyph(), default, default);
    }

    internal void SetExpanded(bool expanded)
    {
        if (IsExpanded == expanded && _reveal is not null)
        {
            return;
        }
        IsExpanded = expanded;
        PseudoClasses.Set(ExpandedPseudoClass, expanded);
        _reveal ??= CreateReveal();
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!IsLoaded || level == ArcMotionLevel.Off)
        {
            _reveal.Jump(expanded ? 1 : 0);
        }
        else if (level == ArcMotionLevel.Reduced)
        {
            _ = _reveal.AnimateTo(expanded ? 1 : 0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        else
        {
            // The label slides out from behind the icon as the width opens, and tucks back a little faster than it came.
            _ = _reveal.AnimateTo(expanded ? 1 : 0, expanded
                ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter, TimeSpan.FromSeconds(0.04))
                : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
        }
        InvalidateMeasure();
    }

    private MotionValue<double> CreateReveal()
    {
        var value = new MotionValue<double>(this, IsExpanded ? 1 : 0, MotionVectors.Double);
        value.Changed += v => ApplyReveal(v, ArcMotion.IsFull(this));
        return value;
    }

    private void ApplyReveal(double v, bool full)
    {
        if (_labelHost is null)
        {
            return;
        }
        _labelHost.Opacity = Math.Clamp(v, 0, 1);
        _labelShift.X = full ? -6 * (1 - v) : 0;
        var blur = full ? 2 * (1 - v) : 0;
        if (blur < 0.05)
        {
            _labelHost.Effect = null;
        }
        else
        {
            _labelBlur.Radius = blur;
            _labelHost.Effect = _labelBlur;
        }
    }

    internal void SetDone(bool done)
    {
        if (IsDone == done)
        {
            return;
        }
        IsDone = done;
        PseudoClasses.Set(DonePseudoClass, done && !string.IsNullOrEmpty(DoneLabel));
        UpdateText();
        _icon?.Show(CreateGlyph(), IconPose, IconPose);
    }

    internal void SetBusy(bool busy)
    {
        IsBusy = busy;
        PseudoClasses.Set(BusyPseudoClass, busy);
    }

    /// <summary>Raises <see cref="AvaloniaButton.Click"/> and runs the command (the group calls this when the action runs).</summary>
    internal void RaiseClick() => base.OnClick();

    protected override void OnClick()
    {
        if (Group is { } group)
        {
            group.OnItemClicked(this);
            return;
        }
        if (!IsDisabled)
        {
            base.OnClick();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Group?.OnItemPointerPressed(this, e.Pointer.Type);
        base.OnPointerPressed(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Group?.OnItemFocused(this, e.NavigationMethod);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == DoneLabelProperty)
        {
            UpdateText();
        }
        else if (change.Property == IconProperty)
        {
            _icon?.Show(CreateGlyph(), IconPose, IconPose);
        }
        else if (change.Property == IsDisabledProperty)
        {
            PseudoClasses.Set(UnavailablePseudoClass, IsDisabled);
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // The content is measured at its natural width; the box width is the animation and clips it.
        base.MeasureOverride(new Size(double.PositiveInfinity, availableSize.Height));
        var height = double.IsNaN(Height) ? 36 : Height;
        var text = _labelHost?.DesiredSize.Width ?? 0;
        var start = _row?.Margin.Left ?? 0;
        var spacing = _row?.Spacing ?? 0;
        var icon = _small ? 14 : 16;
        var target = IsExpanded && text > 0 ? start + icon + spacing + text + TrailingInset : height;
        if (_width is null)
        {
            _width = new MotionValue<double>(this, target, MotionVectors.Double);
            _width.Changed += _ => InvalidateMeasure();
            _target = target;
        }
        else if (Math.Abs(target - _target) > 0.5)
        {
            _target = target;
            // Neighbours slide and the group follows without a jump; the first width and reduced motion place at once.
            if (IsLoaded && text > 0 && ArcMotion.IsFull(this))
            {
                _ = _width.AnimateTo(target, MotionSpec.Spring(ArcSpring.Morph));
            }
            else
            {
                _width.Jump(target);
            }
        }
        // min-width keeps the spring's settle from pinching a collapsed button.
        return new Size(Math.Max(height, _width.Value), height);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ExpandingButtonGroupItemAutomationPeer(this);

    private sealed class ExpandingButtonGroupItemAutomationPeer : ButtonAutomationPeer
    {
        public ExpandingButtonGroupItemAutomationPeer(ExpandingButtonGroupItem owner) : base(owner)
        {
        }

        protected override bool IsEnabledCore() => base.IsEnabledCore() && !((ExpandingButtonGroupItem)Owner).IsDisabled;

        protected override string? GetNameCore() => ((ExpandingButtonGroupItem)Owner).Label;

        protected override string? GetItemStatusCore() => ((ExpandingButtonGroupItem)Owner).IsBusy ? "Busy" : base.GetItemStatusCore();
    }
}
