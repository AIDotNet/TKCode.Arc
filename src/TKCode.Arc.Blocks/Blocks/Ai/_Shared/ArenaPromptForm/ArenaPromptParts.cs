using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>Carries the sample prompt a chip of <see cref="ArenaPromptForm"/> stands for.</summary>
internal sealed class ArenaPromptChosenEventArgs(RoutedEvent routedEvent, object source, ArenaPrompt prompt) : RoutedEventArgs(routedEvent, source)
{
    /// <summary>The chosen prompt.</summary>
    public ArenaPrompt Prompt { get; } = prompt;
}

/// <summary>
/// The arena's single-line prompt field: no frame of its own (the form draws it), 14 / 19.6 in Foreground, the placeholder
/// in TextMuted. While it does not have the focus a text that does not fit ends in an ellipsis (an input's
/// <c>text-overflow: ellipsis</c>): a trimmed copy of the text is shown over the real one, which comes back with the focus.
/// </summary>
internal sealed class ArenaPromptBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(ArenaPromptBox);
}

/// <summary>
/// The arena's Run / Stop button (spec 3.4): 36 high, padding 0 12, a pill in Accent with AccentForeground 14 / 500 while
/// idle (an outlined Play, the label and the run shortcut as a muted key cap 2px further); while <see cref="IsRunning"/>
/// it takes the secondary look (Surface, Border, Foreground) and shows a filled square and the stop label. Both faces sit in
/// one cell: the button is as wide as the wider face, so it keeps its width while running. The visible face crossfades
/// (out 100ms, in 160ms on the standard curve, no movement); colors move in 160ms.
/// </summary>
/// <remarks>
/// Every swap builds a fresh face: a quick Run → Stop → Run would otherwise put one control in two layers of the swap at
/// once. The faces are hidden from screen readers; the button is named by its state (<see cref="RunText"/> or
/// <see cref="StopText"/>).
/// </remarks>
[TemplatePart(FacePart, typeof(ArcContentSwap))]
[TemplatePart(RunReservePart, typeof(Decorator))]
[TemplatePart(StopReservePart, typeof(Decorator))]
[PseudoClasses(RunningPseudoClass)]
internal sealed class ArenaRunButton : AvaloniaButton
{
    public const string FacePart = "PART_Face";
    public const string RunReservePart = "PART_RunReserve";
    public const string StopReservePart = "PART_StopReserve";
    public const string RunningPseudoClass = ":running";

    /// <summary>The space between the label and the key cap on top of the 8px gap (spec 3.4: "left 2").</summary>
    private const double ShortcutGap = 2;

    /// <summary>Whether a run is going: the button shows Stop.</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<ArenaRunButton, bool>(nameof(IsRunning));

    /// <summary>The idle label.</summary>
    public static readonly StyledProperty<string?> RunTextProperty =
        AvaloniaProperty.Register<ArenaRunButton, string?>(nameof(RunText));

    /// <summary>The running label.</summary>
    public static readonly StyledProperty<string?> StopTextProperty =
        AvaloniaProperty.Register<ArenaRunButton, string?>(nameof(StopText));

    /// <summary>The key cap after the idle label; null or empty shows none.</summary>
    public static readonly StyledProperty<string?> ShortcutTextProperty =
        AvaloniaProperty.Register<ArenaRunButton, string?>(nameof(ShortcutText));

    private ArcContentSwap? _face;
    private Decorator? _runReserve;
    private Decorator? _stopReserve;

    static ArenaRunButton()
    {
        FocusAdornerProperty.OverrideDefaultValue<ArenaRunButton>(null);
    }

    public ArenaRunButton()
    {
        AutomationProperties.SetAcceleratorKey(this, PromptKbd.RunAcceleratorKey);
    }

    protected override Type StyleKeyOverride => typeof(ArenaRunButton);

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }

    /// <inheritdoc cref="RunTextProperty"/>
    public string? RunText { get => GetValue(RunTextProperty); set => SetValue(RunTextProperty, value); }

    /// <inheritdoc cref="StopTextProperty"/>
    public string? StopText { get => GetValue(StopTextProperty); set => SetValue(StopTextProperty, value); }

    /// <inheritdoc cref="ShortcutTextProperty"/>
    public string? ShortcutText { get => GetValue(ShortcutTextProperty); set => SetValue(ShortcutTextProperty, value); }

    /// <summary>The visible face's swap (template part).</summary>
    internal ArcContentSwap? FaceSwap => _face;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _face = e.NameScope.Find<ArcContentSwap>(FacePart);
        _runReserve = e.NameScope.Find<Decorator>(RunReservePart);
        _stopReserve = e.NameScope.Find<Decorator>(StopReservePart);
        RefreshReserves();
        // The first content of a swap never animates.
        if (_face is not null)
        {
            _face.Content = BuildFace(IsRunning);
        }
        UpdateName();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRunningProperty)
        {
            PseudoClasses.Set(RunningPseudoClass, change.GetNewValue<bool>());
            SwapFace();
            UpdateName();
        }
        else if (change.Property == RunTextProperty || change.Property == ShortcutTextProperty)
        {
            RefreshReserves();
            if (!IsRunning)
            {
                SwapFace();
            }
            UpdateName();
        }
        else if (change.Property == StopTextProperty)
        {
            RefreshReserves();
            if (IsRunning)
            {
                SwapFace();
            }
            UpdateName();
        }
    }

    private void SwapFace()
    {
        if (_face is not null)
        {
            _face.Content = BuildFace(IsRunning);
        }
    }

    private void RefreshReserves()
    {
        if (_runReserve is not null)
        {
            _runReserve.Child = BuildFace(false);
        }
        if (_stopReserve is not null)
        {
            _stopReserve.Child = BuildFace(true);
        }
    }

    private void UpdateName() => AutomationProperties.SetName(this, (IsRunning ? StopText : RunText) ?? string.Empty);

    /// <summary>
    /// A face: Play 14, the label and the key cap (idle), or a filled 11px square and the label (running), 8px apart. Text
    /// and glyph take the button's text color; the cap is the accent's text color at 62% (it only shows on the accent).
    /// </summary>
    private Control BuildFace(bool running)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        if (running)
        {
            row.Children.Add(new PromptRunGlyph { Data = ArcIcons.Square, Width = 11, Height = 11, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(Label(StopText));
            return row;
        }
        row.Children.Add(new ArcGlyph { Data = ArcIcons.Play, Size = 14, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(Label(RunText));
        if (!string.IsNullOrEmpty(ShortcutText))
        {
            var cap = new PromptKbd { Text = ShortcutText, IsMuted = true, Margin = new Thickness(ShortcutGap, 0, 0, 0) };
            ArcTokens.Bind(cap, ForegroundProperty, ArcDesignTokenKind.AccentForeground);
            row.Children.Add(cap);
        }
        return row;
    }

    private static TextBlock Label(string? text) => new()
    {
        Text = text ?? string.Empty,
        LineHeight = 19.6,
        LetterSpacing = -0.14,
        TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center
    };

    protected override AutomationPeer OnCreateAutomationPeer() => new RunButtonPeer(this);

    /// <summary>A button named by its state; the faces inside it are not read separately (Arc's <c>aria-hidden</c>).</summary>
    private sealed class RunButtonPeer(ArenaRunButton owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// A sample prompt chip (spec 3.4): 28 high, padding 0 11, a pill with a 1px Border, 12 / 16.8 in TextSecondary. Pressed
/// (the box holds this prompt's text): SurfaceMuted, BorderStrong, Foreground. Hover and press: Foreground at 5% over the
/// chip, BorderStrong, Foreground; no scale. Colors move in 160ms. A toggle whose state the form sets: a click asks for
/// the prompt and never flips it, and screen readers hear the toggle state (Arc's <c>aria-pressed</c>).
/// </summary>
internal sealed class ArenaPromptChip : ToggleButton
{
    /// <summary>The chip's text.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArenaPromptChip, string?>(nameof(Label));

    static ArenaPromptChip()
    {
        FocusAdornerProperty.OverrideDefaultValue<ArenaPromptChip>(null);
    }

    public ArenaPromptChip(ArenaPrompt prompt)
    {
        Prompt = prompt;
    }

    protected override Type StyleKeyOverride => typeof(ArenaPromptChip);

    /// <summary>The prompt the chip stands for.</summary>
    public ArenaPrompt Prompt { get; }

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>() ?? string.Empty);
        }
    }

    protected override void Toggle()
    {
        // The form decides which chip is pressed: the one whose prompt is in the box.
    }
}
