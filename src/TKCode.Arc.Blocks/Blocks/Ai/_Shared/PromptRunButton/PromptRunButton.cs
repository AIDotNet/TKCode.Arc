using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// Prompt studio's Run button: a 36px accent pill with a filled Play glyph, the label and the run shortcut as a muted
/// key cap ("⌘↵" on macOS, "Ctrl ↵" elsewhere). While <see cref="IsRunning"/> the glyph becomes a filled square and the
/// label <see cref="StopText"/>: both swap in Arc's label mode (the old one fades out in 100ms, the new one fades in
/// over 160ms on the enter curve, without moving). The label slot keeps the wider of the two labels, so the button does
/// not change width between Run and Stop (Arc 111px); a change of the words themselves springs the width (quick spring).
/// Hover and press darken to AccentStrong; a press scales to 0.97 at full motion.
/// </summary>
/// <remarks>
/// A click raises <see cref="RunRequestedEvent"/> or, while running, <see cref="StopRequestedEvent"/> (after
/// <c>Click</c>); the owner starts or stops the run and sets <see cref="IsRunning"/>. The Ctrl / ⌘ + Enter shortcut is the
/// owner's to handle (it works anywhere in the card), the button only shows it and states it to screen readers as its
/// accelerator key. Screen readers hear <see cref="RunAccessibleName"/> or <see cref="StopAccessibleName"/> (falling
/// back to the visible label); the visible label and the key cap are not exposed separately.
/// </remarks>
[TemplatePart(IconPart, typeof(ArcContentSwap))]
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[TemplatePart(ShortcutPart, typeof(PromptKbd))]
[PseudoClasses(RunningPseudoClass)]
internal class PromptRunButton : AvaloniaButton
{
    public const string IconPart = "PART_Icon";
    public const string LabelPart = "PART_Label";
    public const string ShortcutPart = "PART_Shortcut";
    public const string RunningPseudoClass = ":running";

    /// <summary>Whether a run is going: the button shows Stop and a click asks to stop.</summary>
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<PromptRunButton, bool>(nameof(IsRunning));

    /// <summary>The label while idle ("Run").</summary>
    public static readonly StyledProperty<string?> RunTextProperty =
        AvaloniaProperty.Register<PromptRunButton, string?>(nameof(RunText));

    /// <summary>The label while running ("Stop").</summary>
    public static readonly StyledProperty<string?> StopTextProperty =
        AvaloniaProperty.Register<PromptRunButton, string?>(nameof(StopText));

    /// <summary>The key cap after the label; null shows the platform's run shortcut (<see cref="PromptKbd.RunShortcutText"/>), empty hides it.</summary>
    public static readonly StyledProperty<string?> ShortcutTextProperty =
        AvaloniaProperty.Register<PromptRunButton, string?>(nameof(ShortcutText));

    /// <summary>The accessible name while idle ("Run prompt"); null uses <see cref="RunText"/>.</summary>
    public static readonly StyledProperty<string?> RunAccessibleNameProperty =
        AvaloniaProperty.Register<PromptRunButton, string?>(nameof(RunAccessibleName));

    /// <summary>The accessible name while running ("Stop run"); null uses <see cref="StopText"/>.</summary>
    public static readonly StyledProperty<string?> StopAccessibleNameProperty =
        AvaloniaProperty.Register<PromptRunButton, string?>(nameof(StopAccessibleName));

    /// <summary>The shortcut text actually shown (read-only).</summary>
    public static readonly DirectProperty<PromptRunButton, string> EffectiveShortcutTextProperty =
        AvaloniaProperty.RegisterDirect<PromptRunButton, string>(nameof(EffectiveShortcutText), o => o.EffectiveShortcutText);

    /// <summary>Raised when the button is clicked while idle: start a run.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> RunRequestedEvent =
        RoutedEvent.Register<PromptRunButton, RoutedEventArgs>(nameof(RunRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when the button is clicked while running: stop the run.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> StopRequestedEvent =
        RoutedEvent.Register<PromptRunButton, RoutedEventArgs>(nameof(StopRequested), RoutingStrategies.Bubble);

    private static readonly FuncDataTemplate<Geometry> s_glyphTemplate =
        new((geometry, _) => new PromptRunGlyph { Data = geometry });

    private ArcContentSwap? _icon;
    private ArcContentSwap? _label;
    private string _effectiveShortcutText = string.Empty;

    static PromptRunButton()
    {
        FocusAdornerProperty.OverrideDefaultValue<PromptRunButton>(null);
    }

    public PromptRunButton()
    {
        UpdateShortcut();
        UpdateAccessibleName();
        AutomationProperties.SetAcceleratorKey(this, PromptKbd.RunAcceleratorKey);
    }

    protected override Type StyleKeyOverride => typeof(PromptRunButton);

    /// <inheritdoc cref="IsRunningProperty"/>
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }

    /// <inheritdoc cref="RunTextProperty"/>
    public string? RunText { get => GetValue(RunTextProperty); set => SetValue(RunTextProperty, value); }

    /// <inheritdoc cref="StopTextProperty"/>
    public string? StopText { get => GetValue(StopTextProperty); set => SetValue(StopTextProperty, value); }

    /// <inheritdoc cref="ShortcutTextProperty"/>
    public string? ShortcutText { get => GetValue(ShortcutTextProperty); set => SetValue(ShortcutTextProperty, value); }

    /// <inheritdoc cref="RunAccessibleNameProperty"/>
    public string? RunAccessibleName { get => GetValue(RunAccessibleNameProperty); set => SetValue(RunAccessibleNameProperty, value); }

    /// <inheritdoc cref="StopAccessibleNameProperty"/>
    public string? StopAccessibleName { get => GetValue(StopAccessibleNameProperty); set => SetValue(StopAccessibleNameProperty, value); }

    /// <inheritdoc cref="EffectiveShortcutTextProperty"/>
    public string EffectiveShortcutText
    {
        get => _effectiveShortcutText;
        private set => SetAndRaise(EffectiveShortcutTextProperty, ref _effectiveShortcutText, value);
    }

    /// <summary>Raised when the button is clicked while idle.</summary>
    public event EventHandler<RoutedEventArgs>? RunRequested { add => AddHandler(RunRequestedEvent, value); remove => RemoveHandler(RunRequestedEvent, value); }

    /// <summary>Raised when the button is clicked while running.</summary>
    public event EventHandler<RoutedEventArgs>? StopRequested { add => AddHandler(StopRequestedEvent, value); remove => RemoveHandler(StopRequestedEvent, value); }

    /// <summary>The glyph swap (template part).</summary>
    internal ArcContentSwap? IconSwap => _icon;

    /// <summary>The label swap (template part).</summary>
    internal ArcContentSwap? LabelSwap => _label;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _icon = e.NameScope.Find<ArcContentSwap>(IconPart);
        _label = e.NameScope.Find<ArcContentSwap>(LabelPart);
        // A new template shows the current state at once (the first content of a swap never animates).
        if (_icon is not null)
        {
            _icon.Content = Glyph;
            _icon.ContentTemplate = s_glyphTemplate;
        }
        if (_label is not null)
        {
            _label.Content = LabelText;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRunningProperty)
        {
            PseudoClasses.Set(RunningPseudoClass, change.GetNewValue<bool>());
            if (_icon is not null)
            {
                _icon.Content = Glyph;
            }
            if (_label is not null)
            {
                _label.Content = LabelText;
            }
            UpdateAccessibleName();
        }
        else if (change.Property == RunTextProperty || change.Property == StopTextProperty)
        {
            if (_label is not null)
            {
                _label.Content = LabelText;
            }
            UpdateAccessibleName();
        }
        else if (change.Property == RunAccessibleNameProperty || change.Property == StopAccessibleNameProperty)
        {
            UpdateAccessibleName();
        }
        else if (change.Property == ShortcutTextProperty)
        {
            UpdateShortcut();
        }
    }

    private Geometry Glyph => IsRunning ? ArcIcons.Square : ArcIcons.Play;

    private string LabelText => (IsRunning ? StopText : RunText) ?? string.Empty;

    private void UpdateShortcut() => EffectiveShortcutText = ShortcutText ?? PromptKbd.RunShortcutText;

    private void UpdateAccessibleName()
    {
        var name = IsRunning ? StopAccessibleName ?? StopText : RunAccessibleName ?? RunText;
        AutomationProperties.SetName(this, name ?? string.Empty);
    }

    protected override void OnClick()
    {
        // The state at the moment of the click decides what is asked (the owner flips IsRunning in response).
        var running = IsRunning;
        base.OnClick();
        RaiseEvent(new RoutedEventArgs(running ? StopRequestedEvent : RunRequestedEvent, this));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RunButtonPeer(this);

    /// <summary>A button named by its state; the label and the key cap inside it are not exposed (Arc's <c>aria-hidden</c>).</summary>
    private sealed class RunButtonPeer(PromptRunButton owner) : ButtonAutomationPeer(owner)
    {
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}

/// <summary>
/// A filled Lucide glyph (24 × 24 view box) drawn at the control's size: the Run button's Play and Square, filled and
/// stroked (1.5) in the inherited text color.
/// </summary>
internal sealed class PromptRunGlyph : Control
{
    /// <summary>The glyph's path in the 24 × 24 view box.</summary>
    public static readonly StyledProperty<Geometry?> DataProperty =
        AvaloniaProperty.Register<PromptRunGlyph, Geometry?>(nameof(Data));

    /// <summary>The fill and stroke color (inherited).</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<PromptRunGlyph>();

    private const double StrokeThickness = 1.5;

    private Pen? _pen;
    private IBrush? _penBrush;

    static PromptRunGlyph()
    {
        AffectsRender<PromptRunGlyph>(DataProperty, ForegroundProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PromptRunGlyph>(false);
    }

    /// <inheritdoc cref="DataProperty"/>
    public Geometry? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public override void Render(DrawingContext context)
    {
        if (Data is not { } data || Foreground is not { } brush)
        {
            return;
        }
        if (_pen is null || !ReferenceEquals(_penBrush, brush))
        {
            _pen = new Pen(brush, StrokeThickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            _penBrush = brush;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24.0;
        var offsetX = (Bounds.Width - 24 * scale) / 2;
        var offsetY = (Bounds.Height - 24 * scale) / 2;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            context.DrawGeometry(brush, _pen, data);
        }
    }
}
