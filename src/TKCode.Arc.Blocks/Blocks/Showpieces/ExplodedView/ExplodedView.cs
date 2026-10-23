using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// A scroll-driven teardown of a screen: a stack of equally sized layers, one picture
/// per layer, that tips back and turns as you scroll, separates in depth while a label draws in beside each layer, focuses
/// the layers one at a time (the others dim, the focused one takes the accent and the caption below changes), then closes
/// and stands up again as one screen. A row of stop buttons jumps to the whole screen, each layer and the screen put back
/// together. Use it on a launch or marketing page to show how an interface, a design system or a device is built.
/// </summary>
/// <remarks>
/// <para>
/// The scene is a function of <see cref="Progress"/> (0..1), followed by an overdamped spring (stiffness 330, damping 60)
/// whose value is <see cref="SmoothedProgress"/>. By default the control scrolls in its own rounded frame
/// (<see cref="ExplodedScrollMode.Container"/>): the frame is a native scroll viewer over an empty track
/// <see cref="Length"/> frames tall, with the stage pinned in view. <see cref="ExplodedScrollMode.Page"/> pins the stage to
/// an ancestor scroll viewer instead, and <see cref="ExplodedScrollMode.External"/> leaves <see cref="Progress"/> to you.
/// </para>
/// <para>
/// Keys (container mode, frame focused): ↑/↓ scroll 40 px, PageUp/PageDown/Space/Shift+Space the frame height less 40 px,
/// Home/End the ends. Each stop button scrolls to its stop with Enter, Space or a click. Reduced motion jumps between the
/// flat screen and the fully exploded stack with no in-between, and switches captions and colors at once.
/// </para>
/// </remarks>
[TemplatePart(FramePart, typeof(Border))]
[TemplatePart(FocusRingPart, typeof(Border))]
[TemplatePart(ScrollPart, typeof(ScrollViewer))]
[TemplatePart(TrackPart, typeof(Panel))]
[TemplatePart(LayoutPart, typeof(Panel))]
[TemplatePart(IntroPart, typeof(Control))]
[TemplatePart(TitlePart, typeof(TextBlock))]
[TemplatePart(DescriptionPart, typeof(TextBlock))]
[TemplatePart(StagePart, typeof(Panel))]
[TemplatePart(CaptionsPart, typeof(Panel))]
[TemplatePart(StopsPart, typeof(Panel))]
[TemplatePart(LiveRegionPart, typeof(Control))]
[TemplatePart(LayerListPart, typeof(Panel))]
[PseudoClasses(ExplodedPseudoClass, WidePseudoClass, CompactPseudoClass, TallStagePseudoClass, FramedPseudoClass, RegionFocusPseudoClass)]
public partial class ExplodedView : TemplatedControl
{
    public const string FramePart = "PART_Frame";
    public const string FocusRingPart = "PART_FocusRing";
    public const string ScrollPart = "PART_Scroll";
    public const string TrackPart = "PART_Track";
    public const string LayoutPart = "PART_Layout";
    public const string IntroPart = "PART_Intro";
    public const string TitlePart = "PART_Title";
    public const string DescriptionPart = "PART_Description";
    public const string StagePart = "PART_Stage";
    public const string CaptionsPart = "PART_Captions";
    public const string StopsPart = "PART_Stops";
    public const string LiveRegionPart = "PART_LiveRegion";
    public const string LayerListPart = "PART_LayerList";

    /// <summary>Set while the layers are apart (separation above zero).</summary>
    public const string ExplodedPseudoClass = ":exploded";

    /// <summary>Set while the control is at least 880 px wide: two columns, the stage on the right.</summary>
    public const string WidePseudoClass = ":wide";

    /// <summary>Set while the control is narrower than 560 px: one column, stop buttons above the caption, labels without notes.</summary>
    public const string CompactPseudoClass = ":compact";

    /// <summary>Set while compact with a stage taller than wide: the stack turns less (−20°) and spreads further (150 px).</summary>
    public const string TallStagePseudoClass = ":tall-stage";

    /// <summary>Set while <see cref="IsFramed"/> is true.</summary>
    public const string FramedPseudoClass = ":framed";

    /// <summary>Set while the scroll frame has keyboard focus (the focus ring shows).</summary>
    public const string RegionFocusPseudoClass = ":region-focus";

    /// <summary>Height of an unconstrained control: 88% of the window, between 620 and 860.</summary>
    internal const double AutoHeightRatio = 0.88;
    internal const double AutoHeightMin = 620;
    internal const double AutoHeightMax = 860;

    /// <summary>Width of an unconstrained control (the reference preview).</summary>
    internal const double AutoWidth = 745;

    #region 公共属性定义

    /// <summary>The section title above the stage; null shows none.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ExplodedView, string?>(nameof(Title));

    /// <summary>One or two sentences under the title; null shows none.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ExplodedView, string?>(nameof(Description));

    /// <summary>The caption of the first stop (the whole screen); null shows <see cref="AssembledText"/> alone.</summary>
    public static readonly StyledProperty<ExplodedCaption?> StartCaptionProperty =
        AvaloniaProperty.Register<ExplodedView, ExplodedCaption?>(nameof(StartCaption));

    /// <summary>The caption of the last stop (the screen put back together); null shows <see cref="ReassembledText"/> alone.</summary>
    public static readonly StyledProperty<ExplodedCaption?> EndCaptionProperty =
        AvaloniaProperty.Register<ExplodedView, ExplodedCaption?>(nameof(EndCaption));

    /// <summary>Design size of every layer (default 280 × 620); the layers' content is laid out at it.</summary>
    public static readonly StyledProperty<Size> LayerSizeProperty =
        AvaloniaProperty.Register<ExplodedView, Size>(nameof(LayerSize), new Size(280, 620));

    /// <summary>Corner radius shared by the plate's border, the dashed outlines and the grid's clip (default 40).</summary>
    public static readonly StyledProperty<double> LayerCornerRadiusProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(LayerCornerRadius), 40);

    /// <summary>Where the progress comes from: its own frame (default), the page, or the host.</summary>
    public static readonly StyledProperty<ExplodedScrollMode> ScrollModeProperty =
        AvaloniaProperty.Register<ExplodedView, ExplodedScrollMode>(nameof(ScrollMode));

    /// <summary>Scroll distance in frames (container) or viewports (page): the track is this many times the visible height (default 4).</summary>
    public static readonly StyledProperty<double> LengthProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(Length), 4, coerce: (_, v) => double.IsFinite(v) ? Math.Max(1, v) : 4);

    /// <summary>
    /// The raw progress, 0 (whole screen) to 1 (put back together), two-way. It follows the scroll position in container and
    /// page modes; setting it scrolls there. In external mode it is the only input.
    /// </summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(Progress), 0, defaultBindingMode: BindingMode.TwoWay,
            coerce: (_, v) => double.IsFinite(v) ? Math.Clamp(v, 0, 1) : 0);

    /// <summary>The progress after the follow spring: what the scene shows (read-only).</summary>
    public static readonly DirectProperty<ExplodedView, double> SmoothedProgressProperty =
        AvaloniaProperty.RegisterDirect<ExplodedView, double>(nameof(SmoothedProgress), o => o.SmoothedProgress);

    /// <summary>The current stop: 0 the whole screen, 1..N the layers from the bottom up, N + 1 put back together (read-only).</summary>
    public static readonly DirectProperty<ExplodedView, int> CurrentStopProperty =
        AvaloniaProperty.RegisterDirect<ExplodedView, int>(nameof(CurrentStop), o => o.CurrentStop);

    /// <summary>The number of stops: the layers plus the two ends (read-only).</summary>
    public static readonly DirectProperty<ExplodedView, int> StopCountProperty =
        AvaloniaProperty.RegisterDirect<ExplodedView, int>(nameof(StopCount), o => o.StopCount);

    /// <summary>How far the stack tips back when apart, in degrees (rotateX, default 60).</summary>
    public static readonly StyledProperty<double> MaxTiltProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(MaxTilt), 60);

    /// <summary>How far the stack turns when apart, in degrees (rotateZ, default −40).</summary>
    public static readonly StyledProperty<double> MaxTurnProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(MaxTurn), -40);

    /// <summary>Depth between neighbouring layers when fully apart, in layer pixels (default 136).</summary>
    public static readonly StyledProperty<double> LayerGapProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(LayerGap), 136);

    /// <summary><see cref="MaxTilt"/> while <c>:tall-stage</c> (compact, stage taller than wide; default 62).</summary>
    public static readonly StyledProperty<double> TallMaxTiltProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(TallMaxTilt), 62);

    /// <summary><see cref="MaxTurn"/> while <c>:tall-stage</c> (default −20).</summary>
    public static readonly StyledProperty<double> TallMaxTurnProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(TallMaxTurn), -20);

    /// <summary><see cref="LayerGap"/> while <c>:tall-stage</c> (default 150).</summary>
    public static readonly StyledProperty<double> TallLayerGapProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(TallLayerGap), 150);

    /// <summary>Perspective distance in layer pixels (default 2400; smaller is a stronger perspective).</summary>
    public static readonly StyledProperty<double> PerspectiveProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(Perspective), ExplodedViewGeometry.DefaultPerspective,
            coerce: (_, v) => double.IsFinite(v) || double.IsPositiveInfinity(v) ? Math.Max(200, v) : ExplodedViewGeometry.DefaultPerspective);

    /// <summary>Opacity of the layers that are not focused while one is (default 0.5).</summary>
    public static readonly StyledProperty<double> DimmedLayerOpacityProperty =
        AvaloniaProperty.Register<ExplodedView, double>(nameof(DimmedLayerOpacity), 0.5, coerce: (_, v) => Math.Clamp(v, 0, 1));

    /// <summary>Draws the rounded frame (1px border, surface radius, muted fill). Turn it off for a full-page or page-scroll section.</summary>
    public static readonly StyledProperty<bool> IsFramedProperty =
        AvaloniaProperty.Register<ExplodedView, bool>(nameof(IsFramed), true);

    /// <summary>Accessible name of the stop button row.</summary>
    public static readonly StyledProperty<string> StopsLabelProperty =
        AvaloniaProperty.Register<ExplodedView, string>(nameof(StopsLabel), "Go to a layer");

    /// <summary>Name of the first stop (and its caption title when <see cref="StartCaption"/> is null).</summary>
    public static readonly StyledProperty<string> AssembledTextProperty =
        AvaloniaProperty.Register<ExplodedView, string>(nameof(AssembledText), "Whole screen");

    /// <summary>Name of the last stop (and its caption title when <see cref="EndCaption"/> is null).</summary>
    public static readonly StyledProperty<string> ReassembledTextProperty =
        AvaloniaProperty.Register<ExplodedView, string>(nameof(ReassembledText), "Back together");

    /// <summary>Accessible name of the scroll frame; {0} is the title.</summary>
    public static readonly StyledProperty<string> RegionLabelFormatProperty =
        AvaloniaProperty.Register<ExplodedView, string>(nameof(RegionLabelFormat), "{0}. Scroll to take it apart, one layer at a time.");

    /// <summary>Accessible name of the hidden list of layers ("name: body", bottom to top).</summary>
    public static readonly StyledProperty<string> LayerListLabelProperty =
        AvaloniaProperty.Register<ExplodedView, string>(nameof(LayerListLabel), "Layers, from the bottom up");

    /// <summary>Status read out on the current stop button.</summary>
    public static readonly StyledProperty<string> CurrentStopTextProperty =
        AvaloniaProperty.Register<ExplodedView, string>(nameof(CurrentStopText), "Current step");

    /// <summary>Raised when the shown stop changes (the smoothed progress crosses a stop boundary; the raw one under reduced motion).</summary>
    public static readonly RoutedEvent<ExplodedStopChangedEventArgs> StopChangedEvent =
        RoutedEvent.Register<ExplodedView, ExplodedStopChangedEventArgs>(nameof(StopChanged), RoutingStrategies.Bubble);

    #endregion

    private double _smoothedProgress;
    private int _currentStop;
    private int _stopCount = ExplodedViewProgress.StopCount(0);

    static ExplodedView()
    {
        FocusableProperty.OverrideDefaultValue<ExplodedView>(false);
        AffectsMeasure<ExplodedView>(ScrollModeProperty, LengthProperty, IsFramedProperty);
    }

    public ExplodedView()
    {
        Layers.CollectionChanged += OnLayersChanged;
        Classes.CollectionChanged += OnClassesChanged;
        EffectiveViewportChanged += OnEffectiveViewportChanged;
        PseudoClasses.Set(FramedPseudoClass, true);
    }

    /// <summary>The layers from the bottom up (2 to 8 read best; Arc shows 5).</summary>
    [Content]
    public AvaloniaList<ExplodedLayer> Layers { get; } = [];

    /// <inheritdoc cref="TitleProperty"/>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="StartCaptionProperty"/>
    public ExplodedCaption? StartCaption
    {
        get => GetValue(StartCaptionProperty);
        set => SetValue(StartCaptionProperty, value);
    }

    /// <inheritdoc cref="EndCaptionProperty"/>
    public ExplodedCaption? EndCaption
    {
        get => GetValue(EndCaptionProperty);
        set => SetValue(EndCaptionProperty, value);
    }

    /// <inheritdoc cref="LayerSizeProperty"/>
    public Size LayerSize
    {
        get => GetValue(LayerSizeProperty);
        set => SetValue(LayerSizeProperty, value);
    }

    /// <inheritdoc cref="LayerCornerRadiusProperty"/>
    public double LayerCornerRadius
    {
        get => GetValue(LayerCornerRadiusProperty);
        set => SetValue(LayerCornerRadiusProperty, value);
    }

    /// <inheritdoc cref="ScrollModeProperty"/>
    public ExplodedScrollMode ScrollMode
    {
        get => GetValue(ScrollModeProperty);
        set => SetValue(ScrollModeProperty, value);
    }

    /// <inheritdoc cref="LengthProperty"/>
    public double Length
    {
        get => GetValue(LengthProperty);
        set => SetValue(LengthProperty, value);
    }

    /// <inheritdoc cref="ProgressProperty"/>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <inheritdoc cref="SmoothedProgressProperty"/>
    public double SmoothedProgress
    {
        get => _smoothedProgress;
        private set => SetAndRaise(SmoothedProgressProperty, ref _smoothedProgress, value);
    }

    /// <inheritdoc cref="CurrentStopProperty"/>
    public int CurrentStop
    {
        get => _currentStop;
        private set => SetAndRaise(CurrentStopProperty, ref _currentStop, value);
    }

    /// <inheritdoc cref="StopCountProperty"/>
    public int StopCount
    {
        get => _stopCount;
        private set => SetAndRaise(StopCountProperty, ref _stopCount, value);
    }

    /// <inheritdoc cref="MaxTiltProperty"/>
    public double MaxTilt
    {
        get => GetValue(MaxTiltProperty);
        set => SetValue(MaxTiltProperty, value);
    }

    /// <inheritdoc cref="MaxTurnProperty"/>
    public double MaxTurn
    {
        get => GetValue(MaxTurnProperty);
        set => SetValue(MaxTurnProperty, value);
    }

    /// <inheritdoc cref="LayerGapProperty"/>
    public double LayerGap
    {
        get => GetValue(LayerGapProperty);
        set => SetValue(LayerGapProperty, value);
    }

    /// <inheritdoc cref="TallMaxTiltProperty"/>
    public double TallMaxTilt
    {
        get => GetValue(TallMaxTiltProperty);
        set => SetValue(TallMaxTiltProperty, value);
    }

    /// <inheritdoc cref="TallMaxTurnProperty"/>
    public double TallMaxTurn
    {
        get => GetValue(TallMaxTurnProperty);
        set => SetValue(TallMaxTurnProperty, value);
    }

    /// <inheritdoc cref="TallLayerGapProperty"/>
    public double TallLayerGap
    {
        get => GetValue(TallLayerGapProperty);
        set => SetValue(TallLayerGapProperty, value);
    }

    /// <inheritdoc cref="PerspectiveProperty"/>
    public double Perspective
    {
        get => GetValue(PerspectiveProperty);
        set => SetValue(PerspectiveProperty, value);
    }

    /// <inheritdoc cref="DimmedLayerOpacityProperty"/>
    public double DimmedLayerOpacity
    {
        get => GetValue(DimmedLayerOpacityProperty);
        set => SetValue(DimmedLayerOpacityProperty, value);
    }

    /// <inheritdoc cref="IsFramedProperty"/>
    public bool IsFramed
    {
        get => GetValue(IsFramedProperty);
        set => SetValue(IsFramedProperty, value);
    }

    /// <inheritdoc cref="StopsLabelProperty"/>
    public string StopsLabel
    {
        get => GetValue(StopsLabelProperty);
        set => SetValue(StopsLabelProperty, value);
    }

    /// <inheritdoc cref="AssembledTextProperty"/>
    public string AssembledText
    {
        get => GetValue(AssembledTextProperty);
        set => SetValue(AssembledTextProperty, value);
    }

    /// <inheritdoc cref="ReassembledTextProperty"/>
    public string ReassembledText
    {
        get => GetValue(ReassembledTextProperty);
        set => SetValue(ReassembledTextProperty, value);
    }

    /// <inheritdoc cref="RegionLabelFormatProperty"/>
    public string RegionLabelFormat
    {
        get => GetValue(RegionLabelFormatProperty);
        set => SetValue(RegionLabelFormatProperty, value);
    }

    /// <inheritdoc cref="LayerListLabelProperty"/>
    public string LayerListLabel
    {
        get => GetValue(LayerListLabelProperty);
        set => SetValue(LayerListLabelProperty, value);
    }

    /// <inheritdoc cref="CurrentStopTextProperty"/>
    public string CurrentStopText
    {
        get => GetValue(CurrentStopTextProperty);
        set => SetValue(CurrentStopTextProperty, value);
    }

    /// <inheritdoc cref="StopChangedEvent"/>
    public event EventHandler<ExplodedStopChangedEventArgs>? StopChanged
    {
        add => AddHandler(StopChangedEvent, value);
        remove => RemoveHandler(StopChangedEvent, value);
    }

    /// <summary>The focused layer (null at the two ends).</summary>
    public ExplodedLayer? CurrentLayer => ExplodedViewProgress.LayerAt(_currentStop, Layers.Count) is var i and >= 0 ? Layers[i] : null;

    /// <summary>The name of a stop: <see cref="AssembledText"/>, a layer's name, or <see cref="ReassembledText"/>.</summary>
    public string GetStopName(int stop)
    {
        if (stop <= 0)
        {
            return AssembledText;
        }
        if (stop > Layers.Count)
        {
            return ReassembledText;
        }
        return Layers[stop - 1].Name ?? string.Format(CultureInfo.CurrentCulture, "{0}", stop);
    }

    /// <summary>The caption shown at a stop: (title, body).</summary>
    public (string? Title, string? Body) GetStopCaption(int stop)
    {
        if (stop <= 0)
        {
            return (StartCaption?.Title ?? AssembledText, StartCaption?.Body);
        }
        if (stop > Layers.Count)
        {
            return (EndCaption?.Title ?? ReassembledText, EndCaption?.Body);
        }
        var layer = Layers[stop - 1];
        return (layer.Name, layer.Body);
    }
}
