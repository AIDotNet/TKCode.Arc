using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Shared behavior of the morph's buttons: a <c>:hover</c> pseudo-class for mouse and pen only (a touch shows no hover),
/// and a keyboard focus ring built on first use in the template's <c>PART_Root</c> panel.
/// </summary>
[PseudoClasses(HoverPseudoClass)]
internal abstract class ActionMorphButtonBase : AvaloniaButton
{
    internal const string HoverPseudoClass = ":hover";
    internal const string RootPart = "PART_Root";

    private LazyFocusRing? _focusRing;

    static ActionMorphButtonBase()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ActionMorphButtonBase>(false);
    }

    /// <summary>Whether the button draws its own focus ring (the menu rows show the highlight instead).</summary>
    protected virtual bool HasFocusRing => true;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (HasFocusRing)
        {
            _focusRing = new LazyFocusRing(this, ActionMorphTokens.Key(ActionMorphTokenKind.FocusRingMargin),
                ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
            _focusRing.Attach(e.NameScope.Find<Panel>(RootPart));
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
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

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }
}

/// <summary>The round 32 px back and close buttons of the menu and composer title rows.</summary>
internal sealed class ActionMorphIconButton : ActionMorphButtonBase
{
    /// <summary>The 24 × 24 stroked glyph.</summary>
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ActionMorphIconButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
}

/// <summary>
/// The round button face: a plus, or a check that draws itself after a successful submit. The surface behind it paints
/// the fill; the morph draws the keyboard ring around the surface.
/// </summary>
[TemplatePart(PlusPart, typeof(Control))]
[TemplatePart(CheckPart, typeof(StrokeDrawGlyph))]
internal sealed class ActionMorphTrigger : AvaloniaButton
{
    internal const string PlusPart = "PART_Plus";
    internal const string CheckPart = "PART_Check";

    // Arc Action morph (design reference): the check springs in (0.20 s) and draws after 130 ms over 340 ms on the enter
    // curve; at reset it leaves in 120 ms ease-out while the plus turns in from -90° on a 0.36 s spring.
    private static readonly SpringSpec CheckSpring = SpringSpec.FromVisual(0.20, 0);
    private static readonly SpringSpec PlusSpring = SpringSpec.FromVisual(0.36, 0);
    private static readonly TimeSpan CheckExit = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan DrawDelay = TimeSpan.FromMilliseconds(130);
    private static readonly TimeSpan DrawDuration = TimeSpan.FromMilliseconds(340);

    private readonly ActionMorph _owner;
    private readonly RotateTransform _plusRotate = new();
    private readonly ScaleTransform _plusScale = new();
    private readonly ScaleTransform _checkScale = new();
    private Control? _plus;
    private StrokeDrawGlyph? _check;
    private MotionValue<double>? _plusProgress;
    private MotionValue<double>? _checkProgress;
    private bool _success;

    public ActionMorphTrigger(ActionMorph owner)
    {
        _owner = owner;
    }

    /// <summary>Whether the check shows instead of the plus.</summary>
    public bool IsSuccess => _success;

    /// <summary>The check part (null before the template).</summary>
    internal StrokeDrawGlyph? Check => _check;

    /// <summary>The plus part (null before the template).</summary>
    internal Control? Plus => _plus;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _plus = e.NameScope.Find<Control>(PlusPart);
        _check = e.NameScope.Find<StrokeDrawGlyph>(CheckPart);
        if (_plus is not null)
        {
            _plus.RenderTransform = new TransformGroup { Children = { _plusScale, _plusRotate } };
        }
        if (_check is not null)
        {
            _check.RenderTransform = _checkScale;
        }
        ApplyPlus(_success ? 0 : 1);
        ApplyCheck(_success ? 1 : 0);
    }

    /// <summary>
    /// Shows the check (<paramref name="success"/>) or the plus. With <paramref name="animate"/> the check springs in and
    /// draws itself, or leaves while the plus turns back in.
    /// </summary>
    public void SetSuccess(bool success, bool animate)
    {
        _success = success;
        _plusProgress ??= CreatePlus();
        _checkProgress ??= CreateCheck();
        var level = ArcMotion.GetEffectiveLevel(_owner);
        if (!animate || level == ArcMotionLevel.Off || !IsLoaded)
        {
            _plusProgress.Jump(success ? 0 : 1);
            _checkProgress.Jump(success ? 1 : 0);
            if (_check is not null)
            {
                _check.Progress = 1;
            }
            return;
        }
        var full = level == ArcMotionLevel.Full;
        if (success)
        {
            _plusProgress.Jump(0);
            _checkProgress.Jump(0);
            if (full)
            {
                _ = _checkProgress.AnimateTo(1, MotionSpec.Spring(CheckSpring));
                if (_check is not null)
                {
                    _check.Progress = 0;
                    _ = _check.Play(DrawDuration, ArcEasings.Enter, DrawDelay);
                }
            }
            else
            {
                _ = _checkProgress.AnimateTo(1, MotionSpec.Tween(CheckExit, ActionMorphSurface.EaseOut));
                if (_check is not null)
                {
                    _check.Progress = 1;
                }
            }
            return;
        }
        _ = _checkProgress.AnimateTo(0, MotionSpec.Tween(CheckExit, ActionMorphSurface.EaseOut));
        _ = full
            ? _plusProgress.AnimateTo(1, MotionSpec.Spring(PlusSpring))
            : _plusProgress.AnimateTo(1, MotionSpec.Tween(CheckExit, ActionMorphSurface.EaseOut));
    }

    private MotionValue<double> CreatePlus()
    {
        var value = new MotionValue<double>(_owner, _success ? 0 : 1, MotionVectors.Double);
        value.Changed += ApplyPlus;
        return value;
    }

    private MotionValue<double> CreateCheck()
    {
        var value = new MotionValue<double>(_owner, _success ? 1 : 0, MotionVectors.Double);
        value.Changed += ApplyCheck;
        return value;
    }

    private void ApplyPlus(double progress)
    {
        if (_plus is null)
        {
            return;
        }
        _plus.Opacity = Math.Clamp(progress, 0, 1);
        _plus.IsVisible = progress > 0.001;
        var scale = 0.6 + 0.4 * progress;
        _plusScale.ScaleX = scale;
        _plusScale.ScaleY = scale;
        _plusRotate.Angle = -90 * (1 - progress);
    }

    private void ApplyCheck(double progress)
    {
        if (_check is null)
        {
            return;
        }
        _check.Opacity = Math.Clamp(progress, 0, 1);
        _check.IsVisible = progress > 0.001;
        var scale = 0.6 + 0.4 * progress;
        _checkScale.ScaleX = scale;
        _checkScale.ScaleY = scale;
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _owner.SetTriggerHover(e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _owner.SetTriggerHover(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPressedProperty)
        {
            _owner.SetTriggerPressed(change.GetNewValue<bool>());
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _owner.UpdateFocusRing();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _owner.UpdateFocusRing();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _owner.SetTriggerHover(false);
        _owner.SetTriggerPressed(false);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TriggerPeer(this);

    private sealed class TriggerPeer(ActionMorphTrigger owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
    {
        private ActionMorph Morph => ((ActionMorphTrigger)Owner)._owner;

        public ExpandCollapseState ExpandCollapseState =>
            Morph.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => true;

        public void Expand() => Morph.Expand();

        public void Collapse() => Morph.Collapse();
    }
}

/// <summary>
/// The submit button: its label with an Enter key glyph, swapped for a spinner and the busy text while the submit runs.
/// Unavailable while busy but still focusable, so focus does not drop to the window.
/// </summary>
[PseudoClasses(BusyPseudoClass)]
internal sealed class ActionMorphSubmit : ActionMorphButtonBase
{
    internal const string BusyPseudoClass = ":busy";

    /// <summary>The button's text.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ActionMorphSubmit, string?>(nameof(Label));

    /// <summary>The text beside the spinner while busy.</summary>
    public static readonly StyledProperty<string?> BusyLabelProperty =
        AvaloniaProperty.Register<ActionMorphSubmit, string?>(nameof(BusyLabel));

    /// <summary>Whether the submit runs.</summary>
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<ActionMorphSubmit, bool>(nameof(IsBusy));

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? BusyLabel { get => GetValue(BusyLabelProperty); set => SetValue(BusyLabelProperty, value); }

    public bool IsBusy { get => GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsBusyProperty)
        {
            var busy = change.GetNewValue<bool>();
            PseudoClasses.Set(BusyPseudoClass, busy);
            AutomationProperties.SetItemStatus(this, busy ? BusyLabel : null);
        }
    }

    protected override void OnClick()
    {
        if (IsBusy)
        {
            return;
        }
        base.OnClick();
    }
}

/// <summary>
/// Lays out its first child and centers the others over it without letting them change its size: the busy content of
/// the submit button sits over the label even when it is wider.
/// </summary>
internal sealed class ActionMorphSwapPanel : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.Measure(i == 0 ? availableSize : Size.Infinity);
            if (i == 0)
            {
                size = child.DesiredSize;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            var size = i == 0 ? finalSize : child.DesiredSize;
            child.Arrange(new Rect((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}

/// <summary>One option of a composer's radio group.</summary>
internal sealed class ActionMorphChoice : RadioButton
{
    private LazyFocusRing? _focusRing;

    static ActionMorphChoice()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ActionMorphChoice>(false);
    }

    protected override Type StyleKeyOverride => typeof(ActionMorphChoice);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing = new LazyFocusRing(this, ActionMorphTokens.Key(ActionMorphTokenKind.FocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>(ActionMorphButtonBase.RootPart));
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Arrow keys are the group's (move and select); keep RadioButton from toggling on them.
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
        {
            return;
        }
        base.OnKeyDown(e);
    }
}

/// <summary>
/// A composer's radio group: equal options on a soft track, with a raised block that glides to the picked one. Arrow keys
/// move and select (wrapping); only the picked option is a tab stop.
/// </summary>
[TemplatePart(ThumbPart, typeof(GlidingIndicator))]
[TemplatePart(ItemsPart, typeof(Panel))]
internal sealed class ActionMorphChoices : TemplatedControl
{
    internal const string ThumbPart = "PART_Thumb";
    internal const string ItemsPart = "PART_Items";

    // Arc Action morph (design reference): the selected block glides on a 0.28 s spring without bounce.
    private static readonly SpringSpec GlideSpring = SpringSpec.FromVisual(0.28, 0);

    private readonly List<ActionMorphChoice> _choices = new();
    private GlidingIndicator? _thumb;
    private Panel? _items;
    private string? _selected;
    private bool _placed;

    public ActionMorphChoices()
    {
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    /// <summary>Raised when the picked option changes (by pointer or keys).</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>The picked option.</summary>
    public string? Selected => _selected;

    internal IReadOnlyList<ActionMorphChoice> Choices => _choices;

    /// <summary>Builds one option per string; <paramref name="selected"/> starts picked.</summary>
    public void SetChoices(IReadOnlyList<string> options, string? selected)
    {
        foreach (var choice in _choices)
        {
            choice.IsCheckedChanged -= OnChoiceChecked;
        }
        _choices.Clear();
        _items?.Children.Clear();
        foreach (var option in options)
        {
            var choice = new ActionMorphChoice { Content = option };
            AutomationProperties.SetName(choice, option);
            choice.IsCheckedChanged += OnChoiceChecked;
            _choices.Add(choice);
            _items?.Children.Add(choice);
        }
        Select(selected, animate: false);
    }

    /// <summary>Picks <paramref name="option"/> (null picks nothing).</summary>
    public void Select(string? option, bool animate)
    {
        _selected = option;
        foreach (var choice in _choices)
        {
            var on = Equals(choice.Content, option);
            choice.IsChecked = on;
            choice.IsTabStop = on;
        }
        if (_selected is null && _choices.Count > 0)
        {
            _choices[0].IsTabStop = true;
        }
        PlaceThumb(animate);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _thumb = e.NameScope.Find<GlidingIndicator>(ThumbPart);
        _items = e.NameScope.Find<Panel>(ItemsPart);
        if (_items is not null)
        {
            _items.Children.Clear();
            foreach (var choice in _choices)
            {
                _items.Children.Add(choice);
            }
        }
        _placed = false;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        PlaceThumb(animate: _placed);
        return size;
    }

    private void PlaceThumb(bool animate)
    {
        if (_thumb is null)
        {
            return;
        }
        var picked = _choices.FirstOrDefault(c => c.IsChecked == true);
        if (picked is null || picked.Bounds.Width <= 0)
        {
            return;
        }
        if (picked.TranslatePoint(default, _thumb) is not { } origin)
        {
            return;
        }
        var rect = new Rect(origin, picked.Bounds.Size);
        if (_placed && _thumb.CurrentRect == rect && _thumb.HasTarget)
        {
            return;
        }
        _thumb.MoveTo(rect, GlideSpring, animate && _placed);
        _placed = true;
    }

    private void OnChoiceChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not ActionMorphChoice { IsChecked: true } choice)
        {
            return;
        }
        var option = choice.Content as string;
        if (option == _selected)
        {
            return;
        }
        Select(option, animate: true);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (_choices.Count == 0 || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        var step = e.Key switch
        {
            Key.Left or Key.Up => -1,
            Key.Right or Key.Down => 1,
            _ => 0
        };
        if (step == 0)
        {
            return;
        }
        e.Handled = true;
        var current = _choices.FindIndex(c => c.IsFocused);
        if (current < 0)
        {
            current = Math.Max(0, _choices.FindIndex(c => c.IsChecked == true));
        }
        var next = _choices[(current + step + _choices.Count) % _choices.Count];
        next.Focus(NavigationMethod.Directional);
        next.IsChecked = true;
    }
}
