using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The Integrations status pill (the block's own part, not an Arc button; design reference): "+ Connect" while idle, a
/// spinner and "Connecting" while the connect action runs (clicks are ignored, like Arc's <c>aria-busy</c>), a drawn check
/// and "Connected" on a Success tint once connected (then it is a status label: no focus, no clicks), and our failure
/// state: a Danger tint, an alert icon and "Retry". The label blur-swaps and the width follows it on Smooth; fill and
/// border change in 240ms, the press dips to .985. 32 high, padding 10 / 12, one tab stop (Arc had two).
/// </summary>
[TemplatePart(SurfacePart, typeof(Border))]
[TemplatePart(PhasePart, typeof(ArcContentSwap))]
[PseudoClasses(IdlePseudoClass, ConnectingPseudoClass, ConnectedPseudoClass, ErrorPseudoClass)]
internal sealed class IntegrationConnectPill : AvaloniaButton
{
    internal const string SurfacePart = "PART_Surface";
    internal const string PhasePart = "PART_Phase";
    internal const string IdlePseudoClass = ":idle";
    internal const string ConnectingPseudoClass = ":connecting";
    internal const string ConnectedPseudoClass = ":connected";
    internal const string ErrorPseudoClass = ":error";

    /// <summary>Which face it shows.</summary>
    public static readonly StyledProperty<IntegrationStatus> StatusProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, IntegrationStatus>(nameof(Status));

    /// <summary>The app's name (accessible names).</summary>
    public static readonly StyledProperty<string?> AppNameProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, string?>(nameof(AppName));

    /// <summary>The words it shows.</summary>
    public static readonly StyledProperty<IntegrationLabels?> LabelsProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, IntegrationLabels?>(nameof(Labels));

    /// <summary>Space between the icon slot and the text (6).</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, double>(nameof(Gap), 6);

    /// <summary>The icon slot (16).</summary>
    public static readonly StyledProperty<double> IconSlotProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, double>(nameof(IconSlot), 16);

    /// <summary>The plus and alert icons (15, stroke 1.75).</summary>
    public static readonly StyledProperty<double> PlusSizeProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, double>(nameof(PlusSize), 15);

    public static readonly StyledProperty<double> PlusStrokeProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, double>(nameof(PlusStroke), 1.75);

    /// <summary>The check's stroke (2.25).</summary>
    public static readonly StyledProperty<double> CheckStrokeProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, double>(nameof(CheckStroke), 2.25);

    /// <summary>The spinner (13).</summary>
    public static readonly StyledProperty<double> SpinnerSizeProperty =
        AvaloniaProperty.Register<IntegrationConnectPill, double>(nameof(SpinnerSize), 13);

    private Border? _surface;
    private ArcContentSwap? _phase;
    private IntegrationsPalette? _palette;
    private IntegrationStatus? _shown;

    static IntegrationConnectPill()
    {
        FocusableProperty.OverrideDefaultValue<IntegrationConnectPill>(true);
    }

    public IntegrationConnectPill()
    {
        UpdatePseudoClasses();
        // The accent or a theme can change the Success / Danger tints.
        ResourcesChanged += OnThemeResourcesChanged;
        ActualThemeVariantChanged += (_, _) =>
        {
            _palette = null;
            UpdateColors();
        };
    }

    /// <inheritdoc cref="StatusProperty"/>
    public IntegrationStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="AppNameProperty"/>
    public string? AppName { get => GetValue(AppNameProperty); set => SetValue(AppNameProperty, value); }

    /// <inheritdoc cref="LabelsProperty"/>
    public IntegrationLabels? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    /// <inheritdoc cref="IconSlotProperty"/>
    public double IconSlot { get => GetValue(IconSlotProperty); set => SetValue(IconSlotProperty, value); }

    /// <inheritdoc cref="PlusSizeProperty"/>
    public double PlusSize { get => GetValue(PlusSizeProperty); set => SetValue(PlusSizeProperty, value); }

    /// <inheritdoc cref="PlusStrokeProperty"/>
    public double PlusStroke { get => GetValue(PlusStrokeProperty); set => SetValue(PlusStrokeProperty, value); }

    /// <inheritdoc cref="CheckStrokeProperty"/>
    public double CheckStroke { get => GetValue(CheckStrokeProperty); set => SetValue(CheckStrokeProperty, value); }

    /// <inheritdoc cref="SpinnerSizeProperty"/>
    public double SpinnerSize { get => GetValue(SpinnerSizeProperty); set => SetValue(SpinnerSizeProperty, value); }

    /// <summary>True while a click would start (or retry) a connect.</summary>
    public bool IsActionable => Status is IntegrationStatus.Idle or IntegrationStatus.Error;

    /// <summary>The face currently shown (tests).</summary>
    internal Control? Face => _phase?.Content as Control;

    protected override Type StyleKeyOverride => typeof(IntegrationConnectPill);

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _surface = e.NameScope.Find<Border>(SurfacePart);
        _phase = e.NameScope.Find<ArcContentSwap>(PhasePart);
        _shown = null;
        UpdateFace(animate: false);
        UpdateColors();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _palette = null;
        UpdateColors();
    }

    protected override void OnClick()
    {
        // Busy (Arc's aria-busy) and connected (a status label) ignore clicks.
        if (!IsActionable)
        {
            return;
        }
        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty)
        {
            UpdatePseudoClasses();
            UpdateFace(animate: true);
            UpdateColors();
            UpdateAutomation();
        }
        else if (change.Property == AppNameProperty || change.Property == LabelsProperty)
        {
            _shown = null;
            UpdateFace(animate: false);
            UpdateAutomation();
        }
        else if (change.Property == IsPointerOverProperty)
        {
            UpdateColors();
        }
    }

    private void OnThemeResourcesChanged(object? sender, Avalonia.Controls.ResourcesChangedEventArgs e)
    {
        _palette = null;
        UpdateColors();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(IdlePseudoClass, Status == IntegrationStatus.Idle);
        PseudoClasses.Set(ConnectingPseudoClass, Status == IntegrationStatus.Connecting);
        PseudoClasses.Set(ConnectedPseudoClass, Status == IntegrationStatus.Connected);
        PseudoClasses.Set(ErrorPseudoClass, Status == IntegrationStatus.Error);
        // Connected is a status label: not focusable, not clickable (the row's settings button takes the focus).
        var connected = Status == IntegrationStatus.Connected;
        Focusable = !connected;
        IsTabStop = !connected;
        IsHitTestVisible = !connected;
        Cursor = Status switch
        {
            IntegrationStatus.Connecting => new Cursor(StandardCursorType.AppStarting),
            IntegrationStatus.Connected => Cursor.Default,
            _ => new Cursor(StandardCursorType.Hand),
        };
    }

    private void UpdateAutomation()
    {
        var labels = Labels ?? DefaultLabels;
        var app = AppName ?? string.Empty;
        AutomationProperties.SetName(this, Status switch
        {
            IntegrationStatus.Connecting => labels.ConnectingName(app),
            IntegrationStatus.Connected => labels.ConnectedName(app),
            IntegrationStatus.Error => labels.RetryName(app),
            _ => labels.ConnectName(app),
        });
    }

    private static readonly IntegrationLabels DefaultLabels = new();

    private void UpdateColors()
    {
        if (_palette is null && VisualRoot is not null)
        {
            _palette = IntegrationsPalette.TryFor(this);
        }
        if (_palette is not { } p)
        {
            return;
        }
        var hover = IsPointerOver && IsActionable;
        (Background, BorderBrush) = Status switch
        {
            IntegrationStatus.Connected => (p.ConnectedFill, p.ConnectedBorder),
            IntegrationStatus.Error => (hover ? p.ErrorHoverFill : p.ErrorFill, p.ErrorBorder),
            _ => (hover ? p.Hover : p.Rest, hover ? p.HoverBorder : p.RestBorder),
        };
    }

    private void UpdateFace(bool animate)
    {
        if (_phase is null || _shown == Status)
        {
            return;
        }
        var live = animate && _shown is not null;
        _shown = Status;
        if (!live)
        {
            // A first face (or a rebuilt template) never animates: swap without motion.
            var mode = _phase.Mode;
            _phase.Mode = ContentSwapMode.None;
            _phase.Content = BuildFace(Status, drawCheck: false);
            _phase.Mode = mode;
            return;
        }
        _phase.Content = BuildFace(Status, drawCheck: true);
    }

    private Control BuildFace(IntegrationStatus status, bool drawCheck)
    {
        var labels = Labels ?? DefaultLabels;
        var slot = new Panel { Width = IconSlot, Height = IconSlot, VerticalAlignment = VerticalAlignment.Center };
        var text = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Text = status switch
            {
                IntegrationStatus.Connecting => labels.Connecting,
                IntegrationStatus.Connected => labels.Connected,
                IntegrationStatus.Error => labels.Retry,
                _ => labels.Connect,
            },
        };
        ArcTokens.Bind(text, TextBlock.ForegroundProperty,
            status == IntegrationStatus.Connecting ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Foreground);
        switch (status)
        {
            case IntegrationStatus.Connecting:
            {
                var spinner = new ArcSpinner
                {
                    Width = SpinnerSize,
                    Height = SpinnerSize,
                    Thickness = 1,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ArcTokens.Bind(spinner, ArcSpinner.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
                slot.Children.Add(spinner);
                break;
            }
            case IntegrationStatus.Connected:
            {
                var check = new StrokeDrawGlyph
                {
                    Data = ArcIcons.Check,
                    Size = IconSlot,
                    StrokeThickness = CheckStroke,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ArcTokens.Bind(check, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
                slot.Children.Add(check);
                if (drawCheck && ArcMotion.IsFull(this))
                {
                    // Arc draws the check from ~110ms after the swap, approaching its end like an exponential (τ ≈ 48ms).
                    check.Progress = 0;
                    _ = check.Play(TimeSpan.FromMilliseconds(260), new ExponentialEaseOut(), TimeSpan.FromMilliseconds(100));
                }
                break;
            }
            case IntegrationStatus.Error:
            {
                var alert = new ArcGlyph
                {
                    Data = ArcIcons.CircleAlert,
                    Size = PlusSize,
                    StrokeThickness = PlusStroke,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ArcTokens.Bind(alert, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Danger);
                slot.Children.Add(alert);
                break;
            }
            default:
            {
                var plus = new ArcGlyph
                {
                    Data = ArcIcons.Plus,
                    Size = PlusSize,
                    StrokeThickness = PlusStroke,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ArcTokens.Bind(plus, ArcGlyph.ForegroundProperty, ArcDesignTokenKind.Foreground);
                slot.Children.Add(plus);
                break;
            }
        }
        var face = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Gap,
            VerticalAlignment = VerticalAlignment.Center,
        };
        face.Children.Add(slot);
        face.Children.Add(text);
        return face;
    }
}
