using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A field of dots that bulge away from the pointer like a lens, wobble back on their own springs and ripple outward
/// from every click or tap (Arc Dot grid, design reference). Content placed inside sits on top and stays interactive;
/// the field listens to the pointer across the whole control, over the content too.
/// </summary>
/// <remarks>
/// The field is decoration: it is not focusable and is not in the automation tree, while the content keeps its own
/// semantics. Under reduced motion the dots never move: the lens only lights the dots near the pointer and clicks do not
/// ripple. With motion off the field is still.
/// </remarks>
[TemplatePart(FieldPart, typeof(DotGridField))]
public class DotGrid : ContentControl
{
    public const string FieldPart = "PART_Field";

    /// <summary>Distance between dots in px; lower is denser. At least 6.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<DotGrid, double>(nameof(Gap), 22);

    /// <summary>Dot radius in px at rest.</summary>
    public static readonly StyledProperty<double> DotSizeProperty =
        AvaloniaProperty.Register<DotGrid, double>(nameof(DotSize), 1.4);

    /// <summary>Radius of the lens around the pointer in px.</summary>
    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<DotGrid, double>(nameof(Radius), 150);

    /// <summary>How far the lens pushes dots at its strongest point, a third of the way out, in px.</summary>
    public static readonly StyledProperty<double> StrengthProperty =
        AvaloniaProperty.Register<DotGrid, double>(nameof(Strength), 16);

    /// <summary>Whether clicks and taps send a ripple through the field.</summary>
    public static readonly StyledProperty<bool> IsRippleEnabledProperty =
        AvaloniaProperty.Register<DotGrid, bool>(nameof(IsRippleEnabled), true);

    /// <summary>Whether the field fades out toward its edges.</summary>
    public static readonly StyledProperty<bool> IsFadeEnabledProperty =
        AvaloniaProperty.Register<DotGrid, bool>(nameof(IsFadeEnabled), true);

    /// <summary>Color of the dots at rest. The theme uses the strong border color.</summary>
    public static readonly StyledProperty<IBrush?> DotBrushProperty =
        AvaloniaProperty.Register<DotGrid, IBrush?>(nameof(DotBrush));

    /// <summary>Color the dots take under the lens and in ripples. The theme uses the accent color.</summary>
    public static readonly StyledProperty<IBrush?> ActiveDotBrushProperty =
        AvaloniaProperty.Register<DotGrid, IBrush?>(nameof(ActiveDotBrush));

    private DotGridField? _field;

    static DotGrid()
    {
        FocusableProperty.OverrideDefaultValue<DotGrid>(false);
    }

    public DotGrid()
    {
        AddHandler(PointerMovedEvent, OnPointerMovedAnywhere, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnPointerPressedAnywhere, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleasedAnywhere, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => Leave(), handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnCaptureLost, handledEventsToo: true);
    }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <inheritdoc cref="DotSizeProperty"/>
    public double DotSize
    {
        get => GetValue(DotSizeProperty);
        set => SetValue(DotSizeProperty, value);
    }

    /// <inheritdoc cref="RadiusProperty"/>
    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <inheritdoc cref="StrengthProperty"/>
    public double Strength
    {
        get => GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    /// <inheritdoc cref="IsRippleEnabledProperty"/>
    public bool IsRippleEnabled
    {
        get => GetValue(IsRippleEnabledProperty);
        set => SetValue(IsRippleEnabledProperty, value);
    }

    /// <inheritdoc cref="IsFadeEnabledProperty"/>
    public bool IsFadeEnabled
    {
        get => GetValue(IsFadeEnabledProperty);
        set => SetValue(IsFadeEnabledProperty, value);
    }

    /// <inheritdoc cref="DotBrushProperty"/>
    public IBrush? DotBrush
    {
        get => GetValue(DotBrushProperty);
        set => SetValue(DotBrushProperty, value);
    }

    /// <inheritdoc cref="ActiveDotBrushProperty"/>
    public IBrush? ActiveDotBrush
    {
        get => GetValue(ActiveDotBrushProperty);
        set => SetValue(ActiveDotBrushProperty, value);
    }

    internal DotGridField? Field => _field;

    /// <summary>
    /// Sends a ripple from <paramref name="origin"/> (in the control's coordinates), as a click there would. Does
    /// nothing when ripples are off or motion is reduced.
    /// </summary>
    public void Ripple(Point origin)
    {
        if (_field is null || !IsRippleEnabled)
        {
            return;
        }
        _field.Simulation.CanMove = ArcMotion.IsFull(_field);
        var point = this.TranslatePoint(origin, _field) ?? origin;
        if (_field.Simulation.AddRipple(point.X, point.Y))
        {
            _field.Wake();
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _field = e.NameScope.Find<DotGridField>(FieldPart);
        if (_field is not null)
        {
            _field.Gap = Gap;
            _field.DotSize = DotSize;
            _field.Fade = IsFadeEnabled;
            _field.SetBrushes(DotBrush, ActiveDotBrush);
            _field.Simulation.Radius = Radius;
            _field.Simulation.Strength = Strength;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_field is null)
        {
            return;
        }
        var property = change.Property;
        if (property == GapProperty)
        {
            _field.Gap = Gap;
        }
        else if (property == DotSizeProperty)
        {
            _field.DotSize = DotSize;
        }
        else if (property == IsFadeEnabledProperty)
        {
            _field.Fade = IsFadeEnabled;
        }
        else if (property == DotBrushProperty || property == ActiveDotBrushProperty)
        {
            _field.SetBrushes(DotBrush, ActiveDotBrush);
        }
        else if (property == RadiusProperty || property == StrengthProperty)
        {
            _field.Simulation.Radius = Radius;
            _field.Simulation.Strength = Strength;
            if (_field.Simulation.LensPower > 0)
            {
                _field.Wake();
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new DotGridAutomationPeer(this);

    private void OnPointerMovedAnywhere(object? sender, PointerEventArgs e)
    {
        if (_field is null)
        {
            return;
        }
        // A finger only lights the field while it is down.
        if (e.Pointer.Type != PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var point = e.GetPosition(_field);
        _field.Simulation.PointerMoved(point.X, point.Y);
        _field.Wake();
    }

    private void OnPointerPressedAnywhere(object? sender, PointerPressedEventArgs e)
    {
        if (_field is null)
        {
            return;
        }
        var current = e.GetCurrentPoint(_field);
        var point = current.Position;
        if (e.Pointer.Type != PointerType.Mouse)
        {
            _field.Simulation.PointerMoved(point.X, point.Y);
            _field.Simulation.SnapLens();
            _field.Wake();
        }
        if (!current.Properties.IsLeftButtonPressed)
        {
            return;
        }
        Ripple(e.GetPosition(this));
    }

    private void OnPointerReleasedAnywhere(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            Leave();
        }
    }

    private void OnCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            Leave();
        }
    }

    private void Leave()
    {
        if (_field is null)
        {
            return;
        }
        _field.Simulation.PointerLeft();
        _field.Wake();
    }

    private sealed class DotGridAutomationPeer(DotGrid owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => AutomationProperties.GetName(Owner) is { Length: > 0 };
    }
}
