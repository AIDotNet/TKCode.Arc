using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The size of an <see cref="InkFluid"/> simulation.</summary>
public enum InkFluidQuality
{
    /// <summary>96 velocity cells and 192 ink cells on the short side.</summary>
    Low,

    /// <summary>128 velocity cells and 256 ink cells on the short side.</summary>
    Medium,

    /// <summary>192 velocity cells and 384 ink cells on the short side.</summary>
    High
}

/// <summary>
/// A real-time fluid simulation behind your content (Arc Ink fluid, design reference). The pointer stirs soft ink
/// tinted with the accent color, which turns neutral as it spreads and fades over a couple of seconds; left alone, a
/// slow, calm stir keeps it alive. Content placed inside sits on top and stays interactive.
/// </summary>
/// <remarks>
/// The ink is drawn on transparent pixels over the control's background, so give the control a background and a corner
/// radius. Its colors follow the theme: the foreground token for the ink and the accent token for the tint. Under
/// reduced motion it shows one composed still frame. The simulation runs on the CPU and pauses while the control is off
/// screen or hidden.
/// </remarks>
[TemplatePart(CanvasPart, typeof(InkFluidCanvas))]
public class InkFluid : ContentControl
{
    public const string CanvasPart = "PART_Canvas";

    /// <summary>How dark the ink gets, from 0 to 1.</summary>
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<InkFluid, double>(nameof(Intensity), 0.5);

    /// <summary>How much of the fresh ink takes the accent color, from 0 to 1; the tinted share turns neutral as it spreads.</summary>
    public static readonly StyledProperty<double> TintProperty =
        AvaloniaProperty.Register<InkFluid, double>(nameof(Tint), 0.8);

    /// <summary>How hard the pointer stirs, from 0 to 1.</summary>
    public static readonly StyledProperty<double> ForceProperty =
        AvaloniaProperty.Register<InkFluid, double>(nameof(Force), 0.5);

    /// <summary>Whether a slow stir keeps the ink moving while nobody interacts, starting 1.2s after the last pointer move.</summary>
    public static readonly StyledProperty<bool> IsAmbientProperty =
        AvaloniaProperty.Register<InkFluid, bool>(nameof(IsAmbient), true);

    /// <summary>Whether the pointer stirs the ink.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<InkFluid, bool>(nameof(IsInteractive), true);

    /// <summary>The simulation grid and ink resolution.</summary>
    public static readonly StyledProperty<InkFluidQuality> QualityProperty =
        AvaloniaProperty.Register<InkFluid, InkFluidQuality>(nameof(Quality), InkFluidQuality.Medium);

    /// <summary>The color of neutral ink. The theme uses the foreground color.</summary>
    public static readonly StyledProperty<IBrush?> InkBrushProperty =
        AvaloniaProperty.Register<InkFluid, IBrush?>(nameof(InkBrush));

    /// <summary>The color of tinted ink. The theme uses the accent color.</summary>
    public static readonly StyledProperty<IBrush?> AccentInkBrushProperty =
        AvaloniaProperty.Register<InkFluid, IBrush?>(nameof(AccentInkBrush));

    private InkFluidCanvas? _canvas;

    static InkFluid()
    {
        FocusableProperty.OverrideDefaultValue<InkFluid>(false);
    }

    public InkFluid()
    {
        AddHandler(PointerMovedEvent, OnPointerMovedAnywhere, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnPointerMovedAnywhere, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleasedAnywhere, handledEventsToo: true);
        AddHandler(PointerExitedEvent, (_, _) => _canvas?.PointerGone(), handledEventsToo: true);
    }

    /// <inheritdoc cref="IntensityProperty"/>
    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    /// <inheritdoc cref="TintProperty"/>
    public double Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <inheritdoc cref="ForceProperty"/>
    public double Force
    {
        get => GetValue(ForceProperty);
        set => SetValue(ForceProperty, value);
    }

    /// <inheritdoc cref="IsAmbientProperty"/>
    public bool IsAmbient
    {
        get => GetValue(IsAmbientProperty);
        set => SetValue(IsAmbientProperty, value);
    }

    /// <inheritdoc cref="IsInteractiveProperty"/>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <inheritdoc cref="QualityProperty"/>
    public InkFluidQuality Quality
    {
        get => GetValue(QualityProperty);
        set => SetValue(QualityProperty, value);
    }

    /// <inheritdoc cref="InkBrushProperty"/>
    public IBrush? InkBrush
    {
        get => GetValue(InkBrushProperty);
        set => SetValue(InkBrushProperty, value);
    }

    /// <inheritdoc cref="AccentInkBrushProperty"/>
    public IBrush? AccentInkBrush
    {
        get => GetValue(AccentInkBrushProperty);
        set => SetValue(AccentInkBrushProperty, value);
    }

    internal InkFluidCanvas? Canvas => _canvas;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _canvas = e.NameScope.Find<InkFluidCanvas>(CanvasPart);
        if (_canvas is not null)
        {
            PushSettings(_canvas);
            _canvas.Quality = Quality;
            _canvas.SetColors(ColorOf(InkBrush), ColorOf(AccentInkBrush));
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_canvas is null)
        {
            return;
        }
        var property = change.Property;
        if (property == IntensityProperty || property == TintProperty || property == ForceProperty ||
            property == IsAmbientProperty || property == IsInteractiveProperty)
        {
            PushSettings(_canvas);
            _canvas.Refresh();
        }
        else if (property == QualityProperty)
        {
            _canvas.Quality = Quality;
        }
        else if (property == InkBrushProperty || property == AccentInkBrushProperty)
        {
            _canvas.SetColors(ColorOf(InkBrush), ColorOf(AccentInkBrush));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new InkFluidAutomationPeer(this);

    private void PushSettings(InkFluidCanvas canvas)
    {
        canvas.Intensity = Intensity;
        canvas.Tint = Tint;
        canvas.Force = Force;
        canvas.Ambient = IsAmbient;
        canvas.Interactive = IsInteractive;
    }

    private static Color ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : Colors.Gray;

    private void OnPointerMovedAnywhere(object? sender, PointerEventArgs e)
    {
        if (_canvas is null)
        {
            return;
        }
        // A finger stirs only while it is down; a vertical drag still scrolls the page.
        if (e.Pointer.Type != PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        _canvas.PointerMovedTo(e.GetPosition(_canvas));
    }

    private void OnPointerReleasedAnywhere(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse)
        {
            _canvas?.PointerGone();
        }
    }

    private sealed class InkFluidAutomationPeer(InkFluid owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => AutomationProperties.GetName(Owner) is { Length: > 0 };
    }
}
