using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>What a <see cref="ShowpieceZoomControls"/> button asks for.</summary>
internal enum ShowpieceZoomAction
{
    ZoomOut,
    ZoomIn,
    Fit
}

/// <summary>A zoom bar button press: the action and its factor (<see cref="ShowpieceZoomControls.ZoomStep"/>, its inverse, or 1 for Fit).</summary>
internal sealed class ShowpieceZoomRequestedEventArgs : RoutedEventArgs
{
    public ShowpieceZoomRequestedEventArgs(RoutedEvent routedEvent, ShowpieceZoomAction action, double factor)
        : base(routedEvent)
    {
        Action = action;
        Factor = factor;
    }

    public ShowpieceZoomAction Action { get; }

    public double Factor { get; }
}

/// <summary>
/// The floating zoom bar of the Showpiece stages: − / optional readout / + / Fit. Customer galaxy: 94×34, three 28px
/// buttons; Semantic zoom: 142×34 with a 46px readout (one decimal under 10×, whole numbers above: "1.0×", "53×"; 38px
/// when <see cref="IsCompact"/>). Buttons zoom by <see cref="ZoomStep"/> (Arc 1.6) or its inverse; Fit is disabled
/// (35% opacity, skipped by Tab) while the view is at fit. A press raises <see cref="ZoomRequested"/>, then runs the
/// matching command, or else drives <see cref="Stage"/> directly.
/// </summary>
[TemplatePart(ZoomOutPart, typeof(ShowpieceZoomButton))]
[TemplatePart(ZoomInPart, typeof(ShowpieceZoomButton))]
[TemplatePart(FitPart, typeof(ShowpieceZoomButton))]
[PseudoClasses(CompactPseudoClass, AtFitPseudoClass)]
internal sealed class ShowpieceZoomControls : TemplatedControl
{
    public const string ZoomOutPart = "PART_ZoomOut";
    public const string ZoomInPart = "PART_ZoomIn";
    public const string FitPart = "PART_Fit";
    public const string CompactPseudoClass = ":compact";
    public const string AtFitPseudoClass = ":at-fit";

    public static readonly StyledProperty<double> ZoomProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, double>(nameof(Zoom), 1);

    public static readonly StyledProperty<bool?> IsAtFitProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, bool?>(nameof(IsAtFit));

    public static readonly StyledProperty<bool> ShowReadoutProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, bool>(nameof(ShowReadout));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, bool>(nameof(IsCompact));

    public static readonly StyledProperty<double> ZoomStepProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, double>(nameof(ZoomStep), 1.6);

    public static readonly StyledProperty<string> ZoomInTextProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, string>(nameof(ZoomInText), "Zoom in");

    public static readonly StyledProperty<string> ZoomOutTextProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, string>(nameof(ZoomOutText), "Zoom out");

    public static readonly StyledProperty<string> FitTextProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, string>(nameof(FitText), "Fit to view");

    public static readonly StyledProperty<ICommand?> ZoomInCommandProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, ICommand?>(nameof(ZoomInCommand));

    public static readonly StyledProperty<ICommand?> ZoomOutCommandProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, ICommand?>(nameof(ZoomOutCommand));

    public static readonly StyledProperty<ICommand?> FitCommandProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, ICommand?>(nameof(FitCommand));

    public static readonly StyledProperty<ShowpieceStage?> StageProperty =
        AvaloniaProperty.Register<ShowpieceZoomControls, ShowpieceStage?>(nameof(Stage));

    public static readonly DirectProperty<ShowpieceZoomControls, string> ReadoutTextProperty =
        AvaloniaProperty.RegisterDirect<ShowpieceZoomControls, string>(nameof(ReadoutText), o => o.ReadoutText);

    public static readonly DirectProperty<ShowpieceZoomControls, bool> CanFitProperty =
        AvaloniaProperty.RegisterDirect<ShowpieceZoomControls, bool>(nameof(CanFit), o => o.CanFit);

    public static readonly RoutedEvent<ShowpieceZoomRequestedEventArgs> ZoomRequestedEvent =
        RoutedEvent.Register<ShowpieceZoomControls, ShowpieceZoomRequestedEventArgs>(nameof(ZoomRequested), RoutingStrategies.Bubble);

    /// <summary>The readout zoom (1 = fit).</summary>
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }

    /// <summary>The view is at fit (Fit disabled). Null derives it from <see cref="Zoom"/> = 1.</summary>
    public bool? IsAtFit { get => GetValue(IsAtFitProperty); set => SetValue(IsAtFitProperty, value); }

    /// <summary>Shows the readout between − and + (Semantic zoom).</summary>
    public bool ShowReadout { get => GetValue(ShowReadoutProperty); set => SetValue(ShowReadoutProperty, value); }

    /// <summary>The narrow readout (containers under 600px).</summary>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>Button zoom factor (default 1.6; − uses its inverse 0.625).</summary>
    public double ZoomStep { get => GetValue(ZoomStepProperty); set => SetValue(ZoomStepProperty, value); }

    /// <summary>Screen reader name of +.</summary>
    public string ZoomInText { get => GetValue(ZoomInTextProperty); set => SetValue(ZoomInTextProperty, value); }

    /// <summary>Screen reader name of −.</summary>
    public string ZoomOutText { get => GetValue(ZoomOutTextProperty); set => SetValue(ZoomOutTextProperty, value); }

    /// <summary>Screen reader name of Fit ("Fit all accounts", "Show the whole studio").</summary>
    public string FitText { get => GetValue(FitTextProperty); set => SetValue(FitTextProperty, value); }

    /// <summary>Run by +, with the factor as parameter.</summary>
    public ICommand? ZoomInCommand { get => GetValue(ZoomInCommandProperty); set => SetValue(ZoomInCommandProperty, value); }

    /// <summary>Run by −, with the factor as parameter.</summary>
    public ICommand? ZoomOutCommand { get => GetValue(ZoomOutCommandProperty); set => SetValue(ZoomOutCommandProperty, value); }

    /// <summary>Run by Fit.</summary>
    public ICommand? FitCommand { get => GetValue(FitCommandProperty); set => SetValue(FitCommandProperty, value); }

    /// <summary>A stage driven directly when no command is set (<see cref="ShowpieceStage.ZoomFromButton"/>, <see cref="ShowpieceStage.ResetView"/>).</summary>
    public ShowpieceStage? Stage { get => GetValue(StageProperty); set => SetValue(StageProperty, value); }

    /// <summary>The readout text ("2.4×").</summary>
    public string ReadoutText
    {
        get => _readoutText;
        private set => SetAndRaise(ReadoutTextProperty, ref _readoutText, value);
    }

    /// <summary>Fit is available (the view is away from fit).</summary>
    public bool CanFit
    {
        get => _canFit;
        private set => SetAndRaise(CanFitProperty, ref _canFit, value);
    }

    /// <summary>Raised by a button press before any command runs; mark it handled to stop the default.</summary>
    public event EventHandler<ShowpieceZoomRequestedEventArgs>? ZoomRequested
    {
        add => AddHandler(ZoomRequestedEvent, value);
        remove => RemoveHandler(ZoomRequestedEvent, value);
    }

    private string _readoutText = ShowpieceFormat.ZoomReadout(1);
    private bool _canFit;
    private ShowpieceZoomButton? _zoomOut;
    private ShowpieceZoomButton? _zoomIn;
    private ShowpieceZoomButton? _fit;

    public ShowpieceZoomControls()
    {
        Sync();
    }

    protected override Type StyleKeyOverride => typeof(ShowpieceZoomControls);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Hook(ref _zoomOut, e.NameScope.Find<ShowpieceZoomButton>(ZoomOutPart));
        Hook(ref _zoomIn, e.NameScope.Find<ShowpieceZoomButton>(ZoomInPart));
        Hook(ref _fit, e.NameScope.Find<ShowpieceZoomButton>(FitPart));
        Sync();
    }

    private void Hook(ref ShowpieceZoomButton? field, ShowpieceZoomButton? button)
    {
        if (field is not null)
        {
            field.Click -= OnButtonClick;
        }
        field = button;
        if (button is not null)
        {
            button.Click += OnButtonClick;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        var action = ReferenceEquals(sender, _zoomIn) ? ShowpieceZoomAction.ZoomIn
            : ReferenceEquals(sender, _zoomOut) ? ShowpieceZoomAction.ZoomOut
            : ShowpieceZoomAction.Fit;
        Request(action);
        e.Handled = true;
    }

    /// <summary>Performs a button's action (as if pressed).</summary>
    public void Request(ShowpieceZoomAction action)
    {
        if (action == ShowpieceZoomAction.Fit && !CanFit)
        {
            return;
        }
        var step = ZoomStep > 0 ? ZoomStep : 1.6;
        var factor = action switch
        {
            ShowpieceZoomAction.ZoomIn => step,
            ShowpieceZoomAction.ZoomOut => 1 / step,
            _ => 1
        };
        var args = new ShowpieceZoomRequestedEventArgs(ZoomRequestedEvent, action, factor);
        RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        var command = action switch
        {
            ShowpieceZoomAction.ZoomIn => ZoomInCommand,
            ShowpieceZoomAction.ZoomOut => ZoomOutCommand,
            _ => FitCommand
        };
        if (command is not null)
        {
            if (command.CanExecute(factor))
            {
                command.Execute(factor);
            }
            return;
        }
        if (Stage is { } stage)
        {
            if (action == ShowpieceZoomAction.Fit)
            {
                stage.ResetView();
            }
            else
            {
                stage.ZoomFromButton(factor);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ZoomProperty || change.Property == IsAtFitProperty || change.Property == IsCompactProperty ||
            change.Property == ZoomInTextProperty || change.Property == ZoomOutTextProperty || change.Property == FitTextProperty)
        {
            Sync();
        }
    }

    private void Sync()
    {
        ReadoutText = ShowpieceFormat.ZoomReadout(Zoom);
        var atFit = IsAtFit ?? Math.Abs(Zoom - 1) < 5e-4;
        CanFit = !atFit;
        PseudoClasses.Set(AtFitPseudoClass, atFit);
        PseudoClasses.Set(CompactPseudoClass, IsCompact);
        if (_fit is not null)
        {
            _fit.IsEnabled = !atFit;
            AutomationProperties.SetName(_fit, FitText);
        }
        if (_zoomIn is not null)
        {
            AutomationProperties.SetName(_zoomIn, ZoomInText);
        }
        if (_zoomOut is not null)
        {
            AutomationProperties.SetName(_zoomOut, ZoomOutText);
        }
    }
}

/// <summary>A 28px icon button of <see cref="ShowpieceZoomControls"/> (themed in the same file).</summary>
internal sealed class ShowpieceZoomButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<ShowpieceZoomButton, Geometry?>(nameof(Glyph));

    /// <summary>The Lucide glyph (minus, plus, maximize-2).</summary>
    public Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    protected override Type StyleKeyOverride => typeof(ShowpieceZoomButton);
}
