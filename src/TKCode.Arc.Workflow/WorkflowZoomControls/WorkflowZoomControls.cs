using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using TKCode.Arc.Localization;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A small zoom bar for a <see cref="WorkflowCanvas"/>: zoom out, the current zoom (click it for 100%), zoom in, and fit.
/// Set <see cref="Target"/> to the canvas and place the bar where it suits, usually over a corner of the canvas. It
/// matches the library's other zoom bars: a 34px pill with 28px buttons.
/// </summary>
[TemplatePart(ZoomOutPart, typeof(WorkflowZoomButton))]
[TemplatePart(ResetPart, typeof(WorkflowZoomButton))]
[TemplatePart(ZoomInPart, typeof(WorkflowZoomButton))]
[TemplatePart(FitPart, typeof(WorkflowZoomButton))]
public class WorkflowZoomControls : TemplatedControl
{
    internal const string ZoomOutPart = "PART_ZoomOut";
    internal const string ResetPart = "PART_Reset";
    internal const string ZoomInPart = "PART_ZoomIn";
    internal const string FitPart = "PART_Fit";

    /// <summary>The canvas the buttons zoom.</summary>
    public static readonly StyledProperty<WorkflowCanvas?> TargetProperty =
        AvaloniaProperty.Register<WorkflowZoomControls, WorkflowCanvas?>(nameof(Target));

    private WorkflowZoomButton? _zoomOut;
    private WorkflowZoomButton? _reset;
    private WorkflowZoomButton? _zoomIn;
    private WorkflowZoomButton? _fit;
    private WorkflowCanvas? _observed;
    private ArcLanguageManager? _languages;

    /// <inheritdoc cref="TargetProperty"/>
    public WorkflowCanvas? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>The readout's text, such as "100%" (tests).</summary>
    internal string? ReadoutText => _reset?.Text;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Hook(false);
        _zoomOut = e.NameScope.Find<WorkflowZoomButton>(ZoomOutPart);
        _reset = e.NameScope.Find<WorkflowZoomButton>(ResetPart);
        _zoomIn = e.NameScope.Find<WorkflowZoomButton>(ZoomInPart);
        _fit = e.NameScope.Find<WorkflowZoomButton>(FitPart);
        Hook(true);
        Refresh();
    }

    private void Hook(bool on)
    {
        foreach (var (button, handler) in new (AvaloniaButton?, EventHandler<RoutedEventArgs>)[]
                 {
                     (_zoomOut, OnZoomOut), (_reset, OnReset), (_zoomIn, OnZoomIn), (_fit, OnFit)
                 })
        {
            if (button is null)
            {
                continue;
            }
            if (on)
            {
                button.Click += handler;
            }
            else
            {
                button.Click -= handler;
            }
        }
    }

    private void OnZoomOut(object? sender, RoutedEventArgs e) => Target?.ZoomOut();

    private void OnReset(object? sender, RoutedEventArgs e) => Target?.ZoomTo(1);

    private void OnZoomIn(object? sender, RoutedEventArgs e) => Target?.ZoomIn();

    private void OnFit(object? sender, RoutedEventArgs e) => Target?.FitToContent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Target);
        _languages = WorkflowText.Languages;
        if (_languages is not null)
        {
            _languages.Changed += OnLanguageChanged;
        }
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
        if (_languages is not null)
        {
            _languages.Changed -= OnLanguageChanged;
            _languages = null;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TargetProperty)
        {
            if (VisualRoot is not null)
            {
                Observe(Target);
            }
            Refresh();
        }
    }

    private void Observe(WorkflowCanvas? canvas)
    {
        if (ReferenceEquals(_observed, canvas))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnCanvasPropertyChanged;
        }
        _observed = canvas;
        if (canvas is not null)
        {
            canvas.PropertyChanged += OnCanvasPropertyChanged;
        }
    }

    private void OnCanvasPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == WorkflowCanvas.ZoomProperty || e.Property == WorkflowCanvas.MinZoomProperty ||
            e.Property == WorkflowCanvas.MaxZoomProperty)
        {
            Refresh();
        }
    }

    private void OnLanguageChanged(object? sender, LanguageChangedEventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Refresh();
        }
        else
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    /// <summary>The readout, which buttons can act, and the words for tooltips and screen readers.</summary>
    private void Refresh()
    {
        var canvas = Target;
        var zoom = canvas?.Zoom ?? 1;
        var percent = WorkflowText.Format(WorkflowLangResourceKind.ZoomPercent,
            Math.Round(zoom * 100).ToString("N0", WorkflowText.FormattingCulture));
        Label(_zoomOut, WorkflowLangResourceKind.ZoomOut, canvas is not null && zoom > canvas.MinZoom + 1e-6);
        Label(_zoomIn, WorkflowLangResourceKind.ZoomIn, canvas is not null && zoom < canvas.MaxZoom - 1e-6);
        Label(_fit, WorkflowLangResourceKind.ZoomFit, canvas is not null);
        Label(_reset, WorkflowLangResourceKind.ZoomReset, canvas is not null);
        if (_reset is not null)
        {
            _reset.Text = percent;
            AutomationProperties.SetName(_reset, percent + ", " + WorkflowText.Get(WorkflowLangResourceKind.ZoomReset));
        }
    }

    private static void Label(WorkflowZoomButton? button, WorkflowLangResourceKind kind, bool enabled)
    {
        if (button is null)
        {
            return;
        }
        var text = WorkflowText.Get(kind);
        Tooltip.SetTip(button, text);
        AutomationProperties.SetName(button, text);
        button.IsEnabled = enabled;
    }
}

/// <summary>A 28px button of a <see cref="WorkflowZoomControls"/> bar: a Lucide glyph, or the zoom readout's text.</summary>
internal sealed class WorkflowZoomButton : AvaloniaButton
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<WorkflowZoomButton, Geometry?>(nameof(Glyph));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<WorkflowZoomButton, string?>(nameof(Text));

    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(WorkflowZoomButton);
}
