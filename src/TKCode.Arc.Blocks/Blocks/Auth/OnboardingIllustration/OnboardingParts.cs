using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A button of <see cref="OnboardingIllustration"/> (Back, the main button, Skip; the look comes from the class
/// <c>back</c>, <c>primary</c> or <c>skip</c>). <see cref="AuthInertButton.IsInert"/> is Arc's <c>aria-disabled</c>: the
/// button looks disabled and ignores presses but keeps its place in the Tab order, so focus never drops when a step makes
/// it unavailable (and reports itself disabled). Hover styles only for a mouse or pen (<c>:fine-hover</c>).
/// </summary>
[PseudoClasses(FineHoverPseudoClass)]
internal sealed class OnboardingButton : AuthInertButton
{
    public const string FineHoverPseudoClass = ":fine-hover";

    protected override Type StyleKeyOverride => typeof(OnboardingButton);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            PseudoClasses.Set(FineHoverPseudoClass, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(FineHoverPseudoClass, false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new InertButtonAutomationPeer(this);

    private sealed class InertButtonAutomationPeer(OnboardingButton owner) : ButtonAutomationPeer(owner)
    {
        protected override bool IsEnabledCore() => !owner.IsInert && base.IsEnabledCore();
    }
}

/// <summary>Where a step dot stands relative to the current step.</summary>
internal enum OnboardingDotState
{
    Upcoming,
    Current,
    Complete
}

/// <summary>
/// One step dot: a 6 px pill (20 wide on the current step) in a 32 high hit area with 5 px at the sides. Its width
/// springs on the shape spring so the dots beside it slide; its color crosses over in the theme.
/// </summary>
/// <remarks>Code-driven width: the mark's width is a layout size that must spring with velocity kept when the step changes again.</remarks>
[TemplatePart(MarkPart, typeof(Control))]
[PseudoClasses(CurrentPseudoClass, CompletePseudoClass, UpcomingPseudoClass, FineHoverPseudoClass)]
internal sealed class OnboardingDot : AvaloniaButton
{
    public const string MarkPart = "PART_Mark";
    public const string CurrentPseudoClass = ":current";
    public const string CompletePseudoClass = ":complete";
    public const string UpcomingPseudoClass = ":upcoming";
    public const string FineHoverPseudoClass = ":fine-hover";

    private static readonly SpringSpec s_width = SpringSpec.FromVisual(0.42, 0);

    public static readonly StyledProperty<OnboardingDotState> StateProperty =
        AvaloniaProperty.Register<OnboardingDot, OnboardingDotState>(nameof(State));

    /// <summary>The mark's width at rest (6).</summary>
    public static readonly StyledProperty<double> MarkWidthProperty =
        AvaloniaProperty.Register<OnboardingDot, double>(nameof(MarkWidth), 6);

    /// <summary>The mark's width on the current step (20).</summary>
    public static readonly StyledProperty<double> CurrentMarkWidthProperty =
        AvaloniaProperty.Register<OnboardingDot, double>(nameof(CurrentMarkWidth), 20);

    private Control? _mark;
    private MotionValue<double>? _width;

    public OnboardingDot()
    {
        UpdatePseudoClasses();
    }

    public OnboardingDotState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public double MarkWidth { get => GetValue(MarkWidthProperty); set => SetValue(MarkWidthProperty, value); }

    public double CurrentMarkWidth { get => GetValue(CurrentMarkWidthProperty); set => SetValue(CurrentMarkWidthProperty, value); }

    /// <summary>The mark's width now (tests).</summary>
    internal double DisplayedMarkWidth => _mark?.Width ?? TargetWidth;

    private double TargetWidth => State == OnboardingDotState.Current ? CurrentMarkWidth : MarkWidth;

    protected override Type StyleKeyOverride => typeof(OnboardingDot);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _mark = e.NameScope.Find<Control>(MarkPart);
        _width?.Stop();
        _width = null;
        if (_mark is not null)
        {
            _mark.Width = TargetWidth;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            UpdatePseudoClasses();
            UpdateWidth(animate: true);
        }
        else if (change.Property == MarkWidthProperty || change.Property == CurrentMarkWidthProperty)
        {
            UpdateWidth(animate: false);
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(CurrentPseudoClass, State == OnboardingDotState.Current);
        PseudoClasses.Set(CompletePseudoClass, State == OnboardingDotState.Complete);
        PseudoClasses.Set(UpcomingPseudoClass, State == OnboardingDotState.Upcoming);
    }

    private void UpdateWidth(bool animate)
    {
        if (_mark is null)
        {
            return;
        }
        var target = TargetWidth;
        if (!animate || !IsLoaded)
        {
            _width?.Jump(target);
            _mark.Width = target;
            return;
        }
        if (_width is null)
        {
            var mark = _mark;
            _width = new MotionValue<double>(this, double.IsNaN(mark.Width) ? target : mark.Width, MotionVectors.Double);
            _width.Changed += v => mark.Width = Math.Max(0, v);
        }
        // A spring jumps under reduced motion (Arc: the dot's width changes at once, its color still crosses over).
        _ = _width.AnimateTo(target, MotionSpec.Spring(s_width));
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (e.Pointer.Type != PointerType.Touch)
        {
            PseudoClasses.Set(FineHoverPseudoClass, true);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(FineHoverPseudoClass, false);
    }
}

/// <summary>The row of step dots: a group named "Onboarding steps", each dot its own Tab stop (no roving focus).</summary>
internal sealed class OnboardingDotRow : StackPanel
{
    public OnboardingDotRow()
    {
        Orientation = Orientation.Horizontal;
    }

    /// <summary>Called with the index of a dot the person pressed.</summary>
    public Action<int>? Invoked { get; set; }

    /// <summary>The dots, in step order.</summary>
    public IReadOnlyList<OnboardingDot> Dots => _dots;

    private readonly List<OnboardingDot> _dots = new();

    /// <summary>Shows <paramref name="count"/> dots, reusing the ones it has.</summary>
    public void SetCount(int count)
    {
        while (_dots.Count > count)
        {
            var last = _dots[^1];
            last.Click -= OnDotClick;
            _dots.RemoveAt(_dots.Count - 1);
            Children.Remove(last);
        }
        while (_dots.Count < count)
        {
            var dot = new OnboardingDot();
            dot.Click += OnDotClick;
            _dots.Add(dot);
            Children.Add(dot);
        }
    }

    private void OnDotClick(object? sender, RoutedEventArgs e)
    {
        if (sender is OnboardingDot dot && _dots.IndexOf(dot) is >= 0 and var index)
        {
            Invoked?.Invoke(index);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GroupAutomationPeer(this);

    private sealed class GroupAutomationPeer(OnboardingDotRow owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>The step's text: the title over the sentence (6 apart); a smaller title in the compact layout.</summary>
[PseudoClasses(CompactPseudoClass)]
internal sealed class OnboardingCopy : TemplatedControl
{
    public const string CompactPseudoClass = ":compact";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<OnboardingCopy, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<OnboardingCopy, string?>(nameof(Description));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<OnboardingCopy, bool>(nameof(IsCompact));

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
    }
}

/// <summary>What the main button says.</summary>
internal enum OnboardingPrimaryKind
{
    Next,
    GetStarted,
    Replay
}

/// <summary>The main button's label: "Next" with an arrow after it, "Get started", or a replay arrow before "Replay intro".</summary>
[PseudoClasses(NextPseudoClass, ReplayPseudoClass)]
internal sealed class OnboardingPrimaryLabel : TemplatedControl
{
    public const string NextPseudoClass = ":next";
    public const string ReplayPseudoClass = ":replay";

    public static readonly StyledProperty<OnboardingPrimaryKind> KindProperty =
        AvaloniaProperty.Register<OnboardingPrimaryLabel, OnboardingPrimaryKind>(nameof(Kind));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<OnboardingPrimaryLabel, string?>(nameof(Text));

    public OnboardingPrimaryLabel()
    {
        UpdatePseudoClasses();
    }

    public OnboardingPrimaryKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            UpdatePseudoClasses();
        }
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(NextPseudoClass, Kind == OnboardingPrimaryKind.Next);
        PseudoClasses.Set(ReplayPseudoClass, Kind == OnboardingPrimaryKind.Replay);
    }
}
