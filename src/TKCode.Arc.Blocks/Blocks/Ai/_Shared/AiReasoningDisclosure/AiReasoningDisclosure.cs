using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// The head of an AI reply with its reasoning (Arc chat and answer): an optional 22px lead cell (the model's mark), then a
/// quiet button that reads "Thinking" with a shimmer while the model thinks and "Thought for 3s" once it is done, and under
/// it the reasoning paragraph (14 / 22.4, secondary text, a 1px rule on the left) that the button opens and closes.
/// <code>
/// &lt;arc:AiReasoningDisclosure IsThinking="True" ThinkingText="Thinking" ThoughtText="Thought for 3s" Reasoning="…" /&gt;
/// </code>
/// The person can open it while the model still thinks; with no reasoning yet the chevron turns but nothing shows, and the
/// paragraph opens on its own when the first reasoning arrives. A finished reply without reasoning shows the label alone:
/// no chevron, not a tab stop, nothing to open. Every word comes from the owner (<see cref="ThinkingText"/>,
/// <see cref="ThoughtText"/>). Motion: the label swaps in Arc's label mode while its width follows on
/// <see cref="ArcSpring.Quick"/>; the chevron turns 90° in 200ms; the paragraph opens on <see cref="ArcSpring.Settle"/>
/// while it fades in (<see cref="AiCollapsible"/>). Reduced motion keeps 120ms fades only; Off jumps.
/// </summary>
[TemplatePart(ButtonPart, typeof(AiReasoningButton))]
[TemplatePart(BodyPart, typeof(AiCollapsible))]
[PseudoClasses(ThinkingPseudoClass, ExpandedPseudoClass)]
internal class AiReasoningDisclosure : TemplatedControl
{
    public const string ButtonPart = "PART_Button";
    public const string BodyPart = "PART_Body";
    public const string ReasoningPart = "PART_Reasoning";

    /// <summary>Set while the model thinks.</summary>
    public const string ThinkingPseudoClass = ":thinking";

    /// <summary>Set while the person has the reasoning open.</summary>
    public const string ExpandedPseudoClass = ":expanded";

    #region Properties

    /// <summary>While true the label is the shimmering <see cref="ThinkingText"/>; it can still be opened.</summary>
    public static readonly StyledProperty<bool> IsThinkingProperty =
        AvaloniaProperty.Register<AiReasoningDisclosure, bool>(nameof(IsThinking));

    /// <summary>The label while thinking ("Thinking").</summary>
    public static readonly StyledProperty<string?> ThinkingTextProperty =
        AvaloniaProperty.Register<AiReasoningDisclosure, string?>(nameof(ThinkingText));

    /// <summary>The label once done, formatted by the owner ("Thought for 3s"); null keeps <see cref="ThinkingText"/> without the shimmer.</summary>
    public static readonly StyledProperty<string?> ThoughtTextProperty =
        AvaloniaProperty.Register<AiReasoningDisclosure, string?>(nameof(ThoughtText));

    /// <summary>The reasoning paragraph; it may grow while it streams.</summary>
    public static readonly StyledProperty<string?> ReasoningProperty =
        AvaloniaProperty.Register<AiReasoningDisclosure, string?>(nameof(Reasoning));

    /// <summary>Whether the reasoning is open (two-way); the button toggles it.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiReasoningDisclosure, bool>(nameof(IsExpanded), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>What sits in the 22px cell before the button (the model's mark); null leaves the cell out.</summary>
    public static readonly StyledProperty<object?> LeadProperty =
        AvaloniaProperty.Register<AiReasoningDisclosure, object?>(nameof(Lead));

    /// <summary>Whether there is reasoning text to show (read-only).</summary>
    public static readonly DirectProperty<AiReasoningDisclosure, bool> HasReasoningProperty =
        AvaloniaProperty.RegisterDirect<AiReasoningDisclosure, bool>(nameof(HasReasoning), o => o.HasReasoning);

    /// <inheritdoc cref="IsThinkingProperty"/>
    public bool IsThinking { get => GetValue(IsThinkingProperty); set => SetValue(IsThinkingProperty, value); }

    /// <inheritdoc cref="ThinkingTextProperty"/>
    public string? ThinkingText { get => GetValue(ThinkingTextProperty); set => SetValue(ThinkingTextProperty, value); }

    /// <inheritdoc cref="ThoughtTextProperty"/>
    public string? ThoughtText { get => GetValue(ThoughtTextProperty); set => SetValue(ThoughtTextProperty, value); }

    /// <inheritdoc cref="ReasoningProperty"/>
    public string? Reasoning { get => GetValue(ReasoningProperty); set => SetValue(ReasoningProperty, value); }

    /// <inheritdoc cref="IsExpandedProperty"/>
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }

    /// <inheritdoc cref="LeadProperty"/>
    public object? Lead { get => GetValue(LeadProperty); set => SetValue(LeadProperty, value); }

    /// <inheritdoc cref="HasReasoningProperty"/>
    public bool HasReasoning { get => _hasReasoning; private set => SetAndRaise(HasReasoningProperty, ref _hasReasoning, value); }

    #endregion

    private bool _hasReasoning;
    private AiReasoningButton? _button;
    private AiCollapsible? _body;

    /// <summary>The head button.</summary>
    internal AiReasoningButton? Button => _button;

    /// <summary>The collapsible paragraph.</summary>
    internal AiCollapsible? Body => _body;

    /// <summary>Whether the button can open and close the paragraph: while thinking, or once there is reasoning.</summary>
    internal bool CanToggle => IsThinking || HasReasoning;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_button is not null)
        {
            _button.Click -= OnButtonClick;
        }
        _button = e.NameScope.Find<AiReasoningButton>(ButtonPart);
        _body = e.NameScope.Find<AiCollapsible>(BodyPart);
        if (_button is not null)
        {
            _button.Click += OnButtonClick;
        }
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ReasoningProperty)
        {
            HasReasoning = !string.IsNullOrWhiteSpace(change.GetNewValue<string?>());
            Sync();
        }
        else if (change.Property == IsThinkingProperty)
        {
            PseudoClasses.Set(ThinkingPseudoClass, change.GetNewValue<bool>());
            Sync();
        }
        else if (change.Property == IsExpandedProperty)
        {
            PseudoClasses.Set(ExpandedPseudoClass, change.GetNewValue<bool>());
            Sync();
        }
    }

    /// <summary>The button follows the state; the paragraph is open when the person opened it and there is something to read.</summary>
    private void Sync()
    {
        if (_button is not null)
        {
            _button.CanToggle = CanToggle;
            _button.IsExpanded = IsExpanded && CanToggle;
        }
        if (_body is not null)
        {
            _body.IsExpanded = IsExpanded && HasReasoning;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (CanToggle)
        {
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
        }
    }
}
