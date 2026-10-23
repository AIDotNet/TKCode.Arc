using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The prompt studio's version pill: the open version's label ("v4", medium) and its state, a green dot and
/// <see cref="ProductionText"/> while the open version is in production (<see cref="IsProduction"/>), otherwise
/// <see cref="LatestText"/> without a dot, then a ChevronDown 14 that does not turn. 32px high, padding 0 9 0 11, the pill
/// radius on the surface; hover and the open popover tint it SurfaceMuted with the strong border (background 120ms,
/// border 160ms). Pressing it raises <see cref="PromptHeaderPill.OpenRequested"/> (the studio opens its version menu).
/// <para>
/// A new label or state crossfades as a label (old out 100ms, new in 160ms rising 2px) and the pill's width follows the new
/// text on the quick spring (about 150 ↔ 107 for "Production" ↔ "Latest"), revealing it as the pill grows. Reduced motion
/// crossfades in 120ms and jumps the width; Off changes at once. The pill holds no words of its own: the studio sets
/// <see cref="ProductionText"/>, <see cref="LatestText"/> and <see cref="NameFormat"/>.
/// </para>
/// </summary>
[TemplatePart(LabelPart, typeof(ArcContentSwap))]
[TemplatePart(StatePart, typeof(ArcContentSwap))]
[TemplatePart(TextSizePart, typeof(SpringSizeDecorator))]
[PseudoClasses(ProductionPseudoClass)]
internal sealed class PromptVersionPill : PromptHeaderPill
{
    public const string LabelPart = "PART_Label";
    public const string StatePart = "PART_State";
    public const string TextSizePart = "PART_TextSize";
    public const string ProductionPseudoClass = ":production";

    /// <summary>The open version's short name ("v4").</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<PromptVersionPill, string?>(nameof(Label));

    /// <summary>Whether the open version is the one in production (a green dot and <see cref="ProductionText"/>).</summary>
    public static readonly StyledProperty<bool> IsProductionProperty =
        AvaloniaProperty.Register<PromptVersionPill, bool>(nameof(IsProduction));

    /// <summary>The state word while the open version is in production ("Production").</summary>
    public static readonly StyledProperty<string?> ProductionTextProperty =
        AvaloniaProperty.Register<PromptVersionPill, string?>(nameof(ProductionText));

    /// <summary>The state word otherwise ("Latest").</summary>
    public static readonly StyledProperty<string?> LatestTextProperty =
        AvaloniaProperty.Register<PromptVersionPill, string?>(nameof(LatestText));

    /// <summary>
    /// Makes the accessible name from the label and whether it is in production ("Version v4, in production"); null names
    /// the pill by what it shows ("v4 Production").
    /// </summary>
    public static readonly StyledProperty<Func<string, bool, string>?> NameFormatProperty =
        AvaloniaProperty.Register<PromptVersionPill, Func<string, bool, string>?>(nameof(NameFormat));

    private ArcContentSwap? _labelSwap;
    private ArcContentSwap? _stateSwap;
    private SpringSizeDecorator? _textSize;
    private PromptPillText? _label;
    private PromptVersionPillState? _state;

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="IsProductionProperty"/>
    public bool IsProduction { get => GetValue(IsProductionProperty); set => SetValue(IsProductionProperty, value); }

    /// <inheritdoc cref="ProductionTextProperty"/>
    public string? ProductionText { get => GetValue(ProductionTextProperty); set => SetValue(ProductionTextProperty, value); }

    /// <inheritdoc cref="LatestTextProperty"/>
    public string? LatestText { get => GetValue(LatestTextProperty); set => SetValue(LatestTextProperty, value); }

    /// <inheritdoc cref="NameFormatProperty"/>
    public Func<string, bool, string>? NameFormat { get => GetValue(NameFormatProperty); set => SetValue(NameFormatProperty, value); }

    protected override Type StyleKeyOverride => typeof(PromptVersionPill);

    /// <summary>The label layer on screen.</summary>
    internal PromptPillText? CurrentLabel => _label;

    /// <summary>The state layer on screen.</summary>
    internal PromptVersionPillState? CurrentState => _state;

    /// <summary>The width spring of the text.</summary>
    internal SpringSizeDecorator? TextSize => _textSize;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _labelSwap = e.NameScope.Find<ArcContentSwap>(LabelPart);
        _stateSwap = e.NameScope.Find<ArcContentSwap>(StatePart);
        _textSize = e.NameScope.Find<SpringSizeDecorator>(TextSizePart);
        // New parts start with the current text (their first content never animates).
        _label = null;
        _state = null;
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LabelProperty || change.Property == IsProductionProperty || change.Property == ProductionTextProperty ||
            change.Property == LatestTextProperty || change.Property == NameFormatProperty)
        {
            if (change.Property == IsProductionProperty)
            {
                PseudoClasses.Set(ProductionPseudoClass, change.GetNewValue<bool>());
            }
            Refresh();
        }
    }

    /// <summary>A new layer (crossfaded by its swap) only where the words change; the accessible name always follows.</summary>
    private void Refresh()
    {
        var label = Label ?? string.Empty;
        var production = IsProduction;
        var state = (production ? ProductionText : LatestText) ?? string.Empty;
        AutomationProperties.SetName(this, NameFormat is { } format ? format(label, production) : JoinName(label, state));
        if (_labelSwap is not null && (_label is null || _label.Text != label))
        {
            _label = new PromptPillText { Text = label };
            _labelSwap.Content = _label;
        }
        if (_stateSwap is not null && (_state is null || _state.Text != state || _state.ShowsDot != production))
        {
            _state = new PromptVersionPillState { Text = state, ShowsDot = production };
            _stateSwap.Content = _state;
        }
    }

    private static string JoinName(string label, string state) =>
        label.Length == 0 ? state : state.Length == 0 ? label : label + " " + state;
}

/// <summary>
/// The state layer of the version pill: a 6px Success dot and the word 6px after it (production), or the word alone
/// (latest); 14px in the secondary text color. A new layer is made for every change so the pill can crossfade.
/// </summary>
internal sealed class PromptVersionPillState : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<PromptVersionPillState, string?>(nameof(Text));

    public static readonly StyledProperty<bool> ShowsDotProperty =
        AvaloniaProperty.Register<PromptVersionPillState, bool>(nameof(ShowsDot));

    static PromptVersionPillState()
    {
        FocusableProperty.OverrideDefaultValue<PromptVersionPillState>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<PromptVersionPillState>(false);
    }

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool ShowsDot { get => GetValue(ShowsDotProperty); set => SetValue(ShowsDotProperty, value); }
}
