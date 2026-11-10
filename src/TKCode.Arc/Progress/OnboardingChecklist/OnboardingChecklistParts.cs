// Ported from Arc Onboarding checklist (MIT, Copyright (c) 2026 Elia Kuratli): StepItem, CompletedItem, CheckButton and
// ActionIcon, with the source's timings.
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The state of a step's action button.</summary>
internal enum OnboardingChecklistActionState
{
    Idle,
    Pending,
    Done
}

/// <summary>A flat button of the checklist: a keyboard ring on demand and a hover that touch never triggers.</summary>
internal abstract class OnboardingChecklistButton : AvaloniaButton
{
    private LazyFocusRing? _focusRing;

    protected abstract object RingRadiusKey { get; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, OnboardingChecklistTokens.Key(OnboardingChecklistTokenKind.FocusRingMargin), RingRadiusKey);
        _focusRing.Attach(VisualChildren.Count > 0 ? VisualChildren[0] as Panel : null);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(":hovering", e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":hovering", false);
    }
}

/// <summary>A button that opens and closes a region and exposes the expand / collapse pattern.</summary>
internal abstract class OnboardingChecklistDisclosureButton : OnboardingChecklistButton
{
    private bool _isOpen;

    public void SetIsOpen(bool open)
    {
        if (open == _isOpen)
        {
            return;
        }
        _isOpen = open;
        if (ControlAutomationPeer.FromElement(this) is { } peer)
        {
            peer.RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
                open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DisclosurePeer(this);

    private sealed class DisclosurePeer : ButtonAutomationPeer, IExpandCollapseProvider
    {
        public DisclosurePeer(OnboardingChecklistDisclosureButton owner) : base(owner)
        {
        }

        private OnboardingChecklistDisclosureButton Button => (OnboardingChecklistDisclosureButton)Owner;

        public ExpandCollapseState ExpandCollapseState => Button._isOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (!Button._isOpen)
            {
                Button.RaiseEvent(new RoutedEventArgs(ClickEvent));
            }
        }

        public void Collapse()
        {
            if (Button._isOpen)
            {
                Button.RaiseEvent(new RoutedEventArgs(ClickEvent));
            }
        }
    }
}

/// <summary>The button that opens a step (Arc <c>stepTitle</c>).</summary>
internal sealed class OnboardingChecklistTitleButton : OnboardingChecklistDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(OnboardingChecklistTitleButton);

    protected override object RingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusStep10);
}

/// <summary>The quiet text button that hides and shows the body (Arc <c>hide</c>).</summary>
internal sealed class OnboardingChecklistHideButton : OnboardingChecklistDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(OnboardingChecklistHideButton);

    protected override object RingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusControl);
}

/// <summary>The row that opens the completed steps (Arc <c>groupToggle</c>).</summary>
internal sealed class OnboardingChecklistGroupToggle : OnboardingChecklistDisclosureButton
{
    protected override Type StyleKeyOverride => typeof(OnboardingChecklistGroupToggle);

    protected override object RingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPanel);
}

/// <summary>A step's action. While it runs or after it is done it stays focusable but does nothing (Arc <c>aria-disabled</c>).</summary>
internal sealed class OnboardingChecklistActionButton : OnboardingChecklistButton
{
    private static readonly Geometry s_tick = ArcIcons.Parse("M5 12.5l4.5 4.5L19 7.5");
    private static readonly TimeSpan s_tickDelay = TimeSpan.FromSeconds(0.05);

    private OnboardingChecklistSwap? _icon;
    private OnboardingChecklistActionState _state;

    public OnboardingChecklistActionButton()
    {
        PseudoClasses.Set(":idle", true);
    }

    protected override Type StyleKeyOverride => typeof(OnboardingChecklistActionButton);

    protected override object RingRadiusKey => ArcDesignTokens.Key(ArcDesignTokenKind.RadiusControl);

    public OnboardingChecklistActionState State => _state;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _icon = e.NameScope.Find<OnboardingChecklistSwap>("PART_Icon");
        ShowIcon(false);
    }

    public void SetState(OnboardingChecklistActionState state, bool animate)
    {
        if (state == _state)
        {
            return;
        }
        _state = state;
        PseudoClasses.Set(":idle", state == OnboardingChecklistActionState.Idle);
        PseudoClasses.Set(":pending", state == OnboardingChecklistActionState.Pending);
        PseudoClasses.Set(":done", state == OnboardingChecklistActionState.Done);
        AutomationProperties.SetItemStatus(this, state == OnboardingChecklistActionState.Pending ? "Busy" : null);
        ShowIcon(animate);
    }

    private void ShowIcon(bool animate)
    {
        if (_icon is null)
        {
            return;
        }
        if (_state == OnboardingChecklistActionState.Done)
        {
            var tick = new StrokeDrawGlyph { Data = s_tick, Size = 16, StrokeThickness = 2 };
            _icon.Show(tick, 1, animate);
            if (animate && ArcMotion.IsFull(this))
            {
                tick.Progress = 0;
                _ = tick.Play(ArcMotionPresets.Standard, ArcEasings.Enter, s_tickDelay);
            }
            return;
        }
        Control glyph = _state == OnboardingChecklistActionState.Pending
            ? new ArcSpinner { Width = 14, Height = 14, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new ArcGlyph { Data = ArcIcons.ArrowRight, Size = 16, StrokeThickness = 1.75 };
        _icon.Show(glyph, 1, animate);
    }
}

/// <summary>
/// The round check (Arc <c>CheckButton</c>). The state belongs to the checklist, so a press only asks; the fill and the
/// tick are transitions in the theme, and <c>:arming</c> turns on before <c>:checked</c> so the checking timings are in
/// place when the values change (unchecking is quicker).
/// </summary>
internal sealed class OnboardingChecklistCheck : ToggleButton
{
    private LazyFocusRing? _focusRing;

    static OnboardingChecklistCheck()
    {
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<OnboardingChecklistCheck>(AutomationControlType.CheckBox);
    }

    protected override Type StyleKeyOverride => typeof(OnboardingChecklistCheck);

    public void SetChecked(bool value)
    {
        PseudoClasses.Set(":arming", value);
        IsChecked = value;
    }

    /// <summary>Clicking asks the owner, which then sets the state.</summary>
    protected override void Toggle()
    {
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, OnboardingChecklistTokens.Key(OnboardingChecklistTokenKind.FocusRingMargin), ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(VisualChildren.Count > 0 ? VisualChildren[0] as Panel : null);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(":hovering", e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":hovering", false);
    }
}

/// <summary>
/// A title with a one pixel strike of its own color that draws across, line by line when it wraps. Text decorations
/// cannot be partly drawn, so the line is drawn here; the transition in the theme moves <see cref="StrikeProgress"/>.
/// </summary>
internal sealed class OnboardingChecklistStrikeText : TextBlock
{
    public static readonly StyledProperty<double> StrikeProgressProperty =
        AvaloniaProperty.Register<OnboardingChecklistStrikeText, double>(nameof(StrikeProgress));

    static OnboardingChecklistStrikeText()
    {
        AffectsRender<OnboardingChecklistStrikeText>(StrikeProgressProperty);
    }

    public double StrikeProgress { get => GetValue(StrikeProgressProperty); set => SetValue(StrikeProgressProperty, value); }

    protected override void RenderTextLayout(DrawingContext context, Point origin)
    {
        base.RenderTextLayout(context, origin);
        var progress = Math.Clamp(StrikeProgress, 0, 1);
        if (progress <= 0 || Foreground is not { } brush)
        {
            return;
        }
        var lines = TextLayout.TextLines;
        var total = 0.0;
        foreach (var line in lines)
        {
            total += line.Width;
        }
        var left = total * progress;
        var top = origin.Y;
        foreach (var line in lines)
        {
            if (left <= 0)
            {
                break;
            }
            var width = Math.Min(left, line.Width);
            // Arc: background-position 0 60%, one pixel high.
            context.FillRectangle(brush, new Rect(origin.X + line.Start, top + (line.Height - 1) * 0.6, width, 1));
            left -= width;
            top += line.Height;
        }
    }
}

/// <summary>The open part of a step: its description, its error and its action (Arc <c>panel</c>).</summary>
internal sealed class OnboardingChecklistStepPanel : TemplatedControl
{
    private TextBlock? _description;
    private TextBlock? _error;
    private OnboardingChecklistFold? _errorFold;
    private OnboardingChecklistActionButton? _action;
    private string? _descriptionText;
    private string? _errorText;
    private string? _label;
    private OnboardingChecklistActionState _state;

    public Action? ActionClicked { get; set; }

    internal OnboardingChecklistActionButton? ActionButton
    {
        get
        {
            ApplyTemplate();
            return _action is { IsVisible: true } ? _action : null;
        }
    }

    public void Update(string? description, string? error, string? actionLabel, OnboardingChecklistActionState state, bool animate)
    {
        _descriptionText = description;
        _errorText = error;
        _label = actionLabel;
        _state = state;
        Apply(animate);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_action is not null)
        {
            _action.Click -= OnAction;
        }
        _description = e.NameScope.Find<TextBlock>("PART_Description");
        _error = e.NameScope.Find<TextBlock>("PART_Error");
        _errorFold = e.NameScope.Find<OnboardingChecklistFold>("PART_ErrorFold");
        _action = e.NameScope.Find<OnboardingChecklistActionButton>("PART_Action");
        if (_action is not null)
        {
            _action.Click += OnAction;
        }
        _errorFold?.SetOpen(false, false);
        Apply(false);
    }

    private void OnAction(object? sender, RoutedEventArgs e) => ActionClicked?.Invoke();

    private void Apply(bool animate)
    {
        if (_description is not null)
        {
            _description.Text = _descriptionText;
            _description.IsVisible = !string.IsNullOrEmpty(_descriptionText);
        }
        if (_errorFold is not null && _error is not null)
        {
            // The message stays while the slot folds away.
            if (_errorText is not null)
            {
                _error.Text = _errorText;
            }
            _errorFold.SetOpen(_errorText is not null, animate);
        }
        if (_action is not null)
        {
            _action.Content = _label;
            _action.IsVisible = !string.IsNullOrEmpty(_label);
            _action.SetState(_state, animate);
        }
    }
}

/// <summary>An unfinished or just finished step (Arc <c>StepItem</c>).</summary>
internal sealed class OnboardingChecklistStepRow : TemplatedControl
{
    private OnboardingChecklistCheck? _check;
    private OnboardingChecklistTitleButton? _title;
    private OnboardingChecklistStrikeText? _titleText;
    private OnboardingChecklistFold? _panelFold;
    private OnboardingChecklistStepPanel? _panel;
    private bool _done;
    private bool _expanded;
    private bool _pending;
    private string? _error;

    public OnboardingChecklistStepRow(OnboardingChecklistStep step, OnboardingChecklist owner)
    {
        Step = step;
        Owner = owner;
    }

    public OnboardingChecklistStep Step { get; private set; }

    public OnboardingChecklist Owner { get; }

    public bool IsDone => _done;

    public OnboardingChecklistCheck? Check
    {
        get
        {
            ApplyTemplate();
            return _check;
        }
    }

    public OnboardingChecklistTitleButton? TitleButton
    {
        get
        {
            ApplyTemplate();
            return _title;
        }
    }

    public OnboardingChecklistStepPanel? PanelPart => _panel;

    public bool IsLeaving { get; set; }

    public void SetStep(OnboardingChecklistStep step)
    {
        if (ReferenceEquals(step, Step))
        {
            return;
        }
        if (this.IsAttachedToVisualTree())
        {
            Step.PropertyChanged -= OnStepChanged;
            step.PropertyChanged += OnStepChanged;
        }
        Step = step;
        ApplyTexts();
    }

    public void Update(bool done, bool expanded, bool pending, string? error, bool animate)
    {
        _done = done;
        _expanded = expanded;
        _pending = pending;
        _error = error;
        Apply(animate);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Step.PropertyChanged += OnStepChanged;
        ApplyTexts();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Step.PropertyChanged -= OnStepChanged;
    }

    private void OnStepChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        ApplyTexts();
        _panel?.Update(Step.Description, _error, Step.ActionLabel, ActionState, false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_check is not null)
        {
            _check.Click -= OnCheck;
        }
        if (_title is not null)
        {
            _title.Click -= OnTitle;
        }
        _check = e.NameScope.Find<OnboardingChecklistCheck>("PART_Check");
        _title = e.NameScope.Find<OnboardingChecklistTitleButton>("PART_Title");
        _titleText = e.NameScope.Find<OnboardingChecklistStrikeText>("PART_TitleText");
        _panelFold = e.NameScope.Find<OnboardingChecklistFold>("PART_PanelFold");
        _panel = null;
        if (_check is not null)
        {
            _check.Click += OnCheck;
        }
        if (_title is not null)
        {
            _title.Click += OnTitle;
        }
        _panelFold?.SetOpen(false, false);
        ApplyTexts();
        Apply(false);
    }

    private void OnCheck(object? sender, RoutedEventArgs e) => Owner.ToggleStep(Step.Key, fromGroup: false);

    private void OnTitle(object? sender, RoutedEventArgs e) => Owner.ExpandStep(Step.Key);

    private OnboardingChecklistActionState ActionState => _done ? OnboardingChecklistActionState.Done : _pending ? OnboardingChecklistActionState.Pending : OnboardingChecklistActionState.Idle;

    private void ApplyTexts()
    {
        var title = Step.Title ?? string.Empty;
        if (_titleText is not null)
        {
            _titleText.Text = title;
        }
        if (_check is not null)
        {
            AutomationProperties.SetName(_check, title);
        }
        if (_title is not null)
        {
            AutomationProperties.SetName(_title, title);
        }
    }

    private void Apply(bool animate)
    {
        // :arming first: the transitions it selects must be in place when :done changes the values.
        PseudoClasses.Set(":arming", _done);
        PseudoClasses.Set(":done", _done);
        PseudoClasses.Set(":current", _expanded);
        _check?.SetChecked(_done);
        _title?.SetIsOpen(_expanded);
        if (_expanded && _panel is null && _panelFold is not null)
        {
            // Built when first opened: most steps are read one at a time.
            _panel = new OnboardingChecklistStepPanel { ActionClicked = () => Owner.RunStep(Step) };
            _panel.Update(Step.Description, _error, Step.ActionLabel, ActionState, false);
            _panelFold.Child = _panel;
        }
        _panel?.Update(Step.Description, _error, Step.ActionLabel, ActionState, animate);
        _panelFold?.SetOpen(_expanded, animate);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(":hovering", e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":hovering", false);
    }
}

/// <summary>A finished step in the completed group (Arc <c>CompletedItem</c>); its check reopens the step.</summary>
internal sealed class OnboardingChecklistCompletedRow : TemplatedControl
{
    private OnboardingChecklistCheck? _check;
    private TextBlock? _titleText;

    public OnboardingChecklistCompletedRow(OnboardingChecklistStep step, OnboardingChecklist owner)
    {
        Step = step;
        Owner = owner;
    }

    public OnboardingChecklistStep Step { get; private set; }

    public OnboardingChecklist Owner { get; }

    public OnboardingChecklistCheck? Check
    {
        get
        {
            ApplyTemplate();
            return _check;
        }
    }

    public void SetStep(OnboardingChecklistStep step)
    {
        Step = step;
        ApplyTexts();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_check is not null)
        {
            _check.Click -= OnCheck;
        }
        _check = e.NameScope.Find<OnboardingChecklistCheck>("PART_Check");
        _titleText = e.NameScope.Find<TextBlock>("PART_TitleText");
        if (_check is not null)
        {
            _check.Click += OnCheck;
            _check.SetChecked(true);
        }
        ApplyTexts();
    }

    private void OnCheck(object? sender, RoutedEventArgs e) => Owner.ToggleStep(Step.Key, fromGroup: true);

    private void ApplyTexts()
    {
        var title = Step.Title ?? string.Empty;
        if (_titleText is not null)
        {
            _titleText.Text = title;
        }
        if (_check is not null)
        {
            AutomationProperties.SetName(_check, title);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(":hovering", e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(":hovering", false);
    }
}
