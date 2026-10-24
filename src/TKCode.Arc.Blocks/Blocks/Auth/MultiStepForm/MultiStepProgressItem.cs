using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One entry of the progress list: the round marker and the step's label. Upcoming steps are muted, the current one uses
/// the accent with a medium label, done ones show a green check and a secondary label. The label keeps the width of its
/// medium weight, so the list does not shift when the weight changes. Colors move over 240 ms (standard curve) under full
/// motion and at once otherwise; the entry is not clickable (as in Arc).
/// </summary>
[TemplatePart(MarkerPart, typeof(MultiStepMarker))]
[PseudoClasses(CurrentPseudoClass, CompletePseudoClass)]
internal sealed class MultiStepProgressItem : TemplatedControl
{
    public const string MarkerPart = "PART_Marker";

    /// <summary>The step being filled in (Arc's <c>aria-current="step"</c>).</summary>
    public const string CurrentPseudoClass = ":current";

    /// <summary>A step that is done (check mark).</summary>
    public const string CompletePseudoClass = ":complete";

    /// <summary>Defines the <see cref="Number"/> property.</summary>
    public static readonly StyledProperty<int> NumberProperty =
        AvaloniaProperty.Register<MultiStepProgressItem, int>(nameof(Number), 1);

    /// <summary>Defines the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<MultiStepProgressItem, string?>(nameof(Title));

    /// <summary>Defines the <see cref="IsCurrent"/> property.</summary>
    public static readonly StyledProperty<bool> IsCurrentProperty =
        AvaloniaProperty.Register<MultiStepProgressItem, bool>(nameof(IsCurrent));

    /// <summary>Defines the <see cref="IsDone"/> property.</summary>
    public static readonly StyledProperty<bool> IsDoneProperty =
        AvaloniaProperty.Register<MultiStepProgressItem, bool>(nameof(IsDone));

    private MultiStepMarker? _marker;

    static MultiStepProgressItem()
    {
        FocusableProperty.OverrideDefaultValue<MultiStepProgressItem>(false);
    }

    /// <summary>The one-based step number.</summary>
    public int Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    /// <summary>The step's label.</summary>
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>True for the step being filled in (also the last step after completion, as in Arc).</summary>
    public bool IsCurrent { get => GetValue(IsCurrentProperty); set => SetValue(IsCurrentProperty, value); }

    /// <summary>True once the step is done.</summary>
    public bool IsDone { get => GetValue(IsDoneProperty); set => SetValue(IsDoneProperty, value); }

    /// <summary>Spoken after the label for the current step and for a done one (set by the form).</summary>
    public string? CurrentText { get; set; }

    public string? DoneText { get; set; }

    /// <summary>The marker part (tests read it).</summary>
    internal MultiStepMarker? Marker => _marker;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _marker = e.NameScope.Find<MultiStepMarker>(MarkerPart);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCurrentProperty || change.Property == IsDoneProperty)
        {
            // Done wins the look (Arc keeps aria-current on the last step after completion, but draws it done).
            PseudoClasses.Set(CompletePseudoClass, IsDone);
            PseudoClasses.Set(CurrentPseudoClass, IsCurrent && !IsDone);
            UpdateStatus();
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
    }

    /// <summary>Refreshes the spoken status after the form changed <see cref="CurrentText"/> or <see cref="DoneText"/>.</summary>
    public void UpdateStatus()
    {
        var parts = new List<string>(2);
        if (IsCurrent && !string.IsNullOrEmpty(CurrentText))
        {
            parts.Add(CurrentText);
        }
        if (IsDone && !string.IsNullOrEmpty(DoneText))
        {
            parts.Add(DoneText);
        }
        AutomationProperties.SetItemStatus(this, parts.Count == 0 ? null : string.Join(", ", parts));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MultiStepListItemPeer(this);
}

/// <summary>Reports a progress entry as a list item.</summary>
internal sealed class MultiStepListItemPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>Reports the progress list as a list (named by the form's <see cref="MultiStepForm.ProgressNameFormat"/>).</summary>
internal sealed class MultiStepListPeer(Control owner) : ControlAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}

/// <summary>The row of progress entries (a list for assistive technology).</summary>
internal sealed class MultiStepProgressList : StackPanel
{
    public MultiStepProgressList()
    {
        Orientation = Avalonia.Layout.Orientation.Horizontal;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MultiStepListPeer(this);
}
