using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A draft step's title field: a wrapping text area with no frame of its own (notes 3.1): padding 4 8 inside a negative
/// margin of the same size, so its text sits exactly where the running title is drawn; radius 8; SurfaceMuted under the
/// pointer, Surface with an inset 1px BorderStrong line while focused (no focus ring). Enter, Escape, Backspace and
/// Alt+arrows are handled by the row.
/// </summary>
internal sealed class AgentPlanTitleBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(AgentPlanTitleBox);
}

/// <summary>
/// A draft step's drag handle (24 × 28, radius 8, the grip glyph in TextMuted): SurfaceMuted under the pointer and while
/// dragging, with the glyph in Foreground; an inset keyboard focus ring. It takes the focus on press. Up and Down move the
/// step (handled by the row); Space, Enter, Home and End do nothing.
/// </summary>
[Avalonia.Controls.Metadata.PseudoClasses(DraggingPseudoClass)]
internal sealed class AgentPlanGrip : TemplatedControl
{
    public const string DraggingPseudoClass = ":dragging";

    public AgentPlanGrip()
    {
        Focusable = true;
        IsTabStop = true;
    }

    /// <summary>True while the step is lifted.</summary>
    public bool IsDragging
    {
        get => PseudoClasses.Contains(DraggingPseudoClass);
        set => PseudoClasses.Set(DraggingPseudoClass, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GripPeer(this);

    private sealed class GripPeer(AgentPlanGrip owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;
    }
}

/// <summary>
/// The plan's small in-place buttons, styled by class in the theme: <c>remove</c> (24 × 24, radius 8, an x that shows
/// while the row is hovered or focused), <c>add</c> (the Add step row), <c>retry</c> and <c>skip</c> (28 high, radius 10,
/// 12 / 500). Their keyboard focus ring is inset, because the rows clip what overflows them.
/// </summary>
internal sealed class AgentPlanButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(AgentPlanButton);
}

/// <summary>
/// The thin progress line over the header's bottom rule (2px, Accent): its fill grows from the left to the finished
/// fraction on the layout spring (from wherever it is, also when the count changes) and fades in when the plan is approved.
/// A progress bar for assistive technology, valued in finished steps with a text value; hidden from it in a draft.
/// </summary>
internal sealed class AgentPlanProgress : RangeBase
{
    /// <summary>Defines the <see cref="IsShown"/> property.</summary>
    public static readonly StyledProperty<bool> IsShownProperty =
        AvaloniaProperty.Register<AgentPlanProgress, bool>(nameof(IsShown));

    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<AgentPlanProgress, IBrush?>(nameof(Fill));

    private readonly MotionValue<double> _fraction;
    private readonly MotionValue<double> _presence;
    private bool _started;

    static AgentPlanProgress()
    {
        AffectsRender<AgentPlanProgress>(FillProperty);
        FocusableProperty.OverrideDefaultValue<AgentPlanProgress>(false);
        AutomationProperties.ControlTypeOverrideProperty.OverrideDefaultValue<AgentPlanProgress>(AutomationControlType.ProgressBar);
    }

    public AgentPlanProgress()
    {
        IsHitTestVisible = false;
        _fraction = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fraction.Changed += _ => InvalidateVisual();
        _presence = new MotionValue<double>(this, 0, MotionVectors.Double);
        _presence.Changed += _ => InvalidateVisual();
        Maximum = 1;
    }

    /// <summary>Shown while the plan runs, is paused or is done; hidden (faded out) in a draft.</summary>
    public bool IsShown { get => GetValue(IsShownProperty); set => SetValue(IsShownProperty, value); }

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    /// <summary>The drawn fraction now.</summary>
    public double DrawnFraction => _fraction.Value;

    /// <summary>The drawn opacity now.</summary>
    public double DrawnOpacity => _presence.Value;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == MaximumProperty)
        {
            var target = Maximum <= 0 ? 0 : Math.Clamp(Value / Maximum, 0, 1);
            if (!_started || !IsLoaded || !AgentPlanMotion.IsFull(this))
            {
                _started = true;
                _fraction.Jump(target);
            }
            else
            {
                _ = _fraction.AnimateTo(target, MotionSpec.Spring(AgentPlanMotion.Layout));
            }
        }
        else if (change.Property == IsShownProperty)
        {
            var shown = change.GetNewValue<bool>();
            AutomationProperties.SetAccessibilityView(this, shown ? AccessibilityView.Control : AccessibilityView.Raw);
            if (IsLoaded && AgentPlanMotion.Fades(this))
            {
                _ = _presence.AnimateTo(shown ? 1 : 0, AgentPlanMotion.Enter(AgentPlanMotion.ProgressFade));
            }
            else
            {
                _presence.Jump(shown ? 1 : 0);
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        var opacity = _presence.Value;
        var fraction = _fraction.Value;
        if (opacity <= 0.001 || fraction <= 0.0005 || Fill is not { } fill)
        {
            return;
        }
        using (context.PushOpacity(opacity))
        {
            context.FillRectangle(fill, new Rect(0, 0, Bounds.Width * Math.Min(1, fraction), Bounds.Height));
        }
    }
}

/// <summary>
/// One activity line under a step (20 high: 4 above, a 12px icon picked by the first word in TextMuted, 6 apart, the line
/// in 12 / 16 TextSecondary, one line cut with an ellipsis). A new line grows from nothing on the layout spring while it
/// fades in over 100ms.
/// </summary>
internal sealed class AgentPlanActivityLine : Decorator
{
    private readonly ArcGlyph _icon = new() { Size = 12, StrokeThickness = 1.75, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _text = new()
    {
        FontSize = 12,
        LineHeight = 16,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextWrapping = TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center
    };

    public AgentPlanActivityLine(string text, bool enter)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 4, 0, 0),
            Height = 16
        };
        row.Children.Add(_icon);
        row.Children.Add(_text);
        var collapse = new AgentPlanCollapse { EnterFade = AgentPlanMotion.LineEnter, IsOpen = !enter, Child = row };
        Child = collapse;
        Collapse = collapse;
        Text = text;
        ArcTokens.Bind(_icon, ArcGlyph.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_text, TextBlock.ForegroundProperty, DesignTokens.ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.ListItem);
    }

    /// <summary>The part that grows the line in.</summary>
    public AgentPlanCollapse Collapse { get; }

    /// <summary>The line.</summary>
    public string Text
    {
        get => _text.Text ?? string.Empty;
        set
        {
            _text.Text = value;
            _icon.Data = AgentPlanActivityIcons.Get(value);
            AutomationProperties.SetName(this, value);
        }
    }

    /// <summary>Grows the line in (call once it is in the tree).</summary>
    public void Open() => Collapse.IsOpen = true;
}
