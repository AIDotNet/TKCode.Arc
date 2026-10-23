using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>What the confidence column of a value shows (spec 3.4, the confidence column table).</summary>
internal enum ConfidenceKind
{
    /// <summary>Nothing (a table without values).</summary>
    None,

    /// <summary>No value yet: the type name ("Date"), TextMuted.</summary>
    Type,

    /// <summary>A value the model is sure of: "99%", TextMuted.</summary>
    Percent,

    /// <summary>Below the review threshold: a Warning triangle and the percent in Foreground.</summary>
    Low,

    /// <summary>Marked "Looks right": a Success check and the percent, TextMuted.</summary>
    Checked,

    /// <summary>Accepted: looks like <see cref="Checked"/> (the check pops again when Accept all reaches it).</summary>
    Accepted,

    /// <summary>Edited by the person: a pencil and "Edited", TextMuted.</summary>
    Edited,

    /// <summary>Edited, then accepted: a Success check and "Edited".</summary>
    EditedAccepted
}

/// <summary>
/// The confidence column of a document extraction field (spec 3.4): the type name while the value is pending, the percent
/// once it arrives ("99%"), a Warning triangle with the percent in Foreground below the review threshold, a Success check
/// with the percent once checked or accepted, a pencil with <see cref="EditedText"/> once the person edited it. Right-aligned,
/// 12 / 16.8 with tabular figures, an icon of 13 four pixels before the text. The width is the owner's (60, 54 narrow).
/// </summary>
/// <remarks>
/// <para>
/// The column follows <see cref="Value"/> (its status, confidence and review flag) and <see cref="IsAccepted"/> (set by the
/// owner as Accept all reaches the value). A change of what it shows crossfades: the old content out in 100ms, the new one in
/// over 160ms on the standard curve rising 2px; a check that appears pops from 0.4 on the snappy spring. Reduced motion
/// crossfades in 120ms without the rise or the pop; Off replaces at once.
/// </para>
/// <para>
/// Screen readers get <see cref="AutomationText"/> (the owner's whole sentence, "Low confidence, 64 percent, check this
/// value"); the visible text is not in the automation tree.
/// </para>
/// </remarks>
[TemplatePart(SwapPart, typeof(ArcContentSwap))]
internal sealed class ConfidenceIndicator : TemplatedControl
{
    public const string SwapPart = "PART_Swap";

    /// <summary>The value whose confidence is shown.</summary>
    public static readonly StyledProperty<ExtractionValue?> ValueProperty =
        AvaloniaProperty.Register<ConfidenceIndicator, ExtractionValue?>(nameof(Value));

    /// <summary>The type name shown while the value is pending ("Date"); null shows nothing.</summary>
    public static readonly StyledProperty<string?> TypeNameProperty =
        AvaloniaProperty.Register<ConfidenceIndicator, string?>(nameof(TypeName));

    /// <summary>The word of an edited value ("Edited").</summary>
    public static readonly StyledProperty<string?> EditedTextProperty =
        AvaloniaProperty.Register<ConfidenceIndicator, string?>(nameof(EditedText));

    /// <summary>What screen readers hear for the column (the owner's sentence).</summary>
    public static readonly StyledProperty<string?> AutomationTextProperty =
        AvaloniaProperty.Register<ConfidenceIndicator, string?>(nameof(AutomationText));

    /// <summary>Whether Accept all has reached the value: a check with the percent (or "Edited").</summary>
    public static readonly StyledProperty<bool> IsAcceptedProperty =
        AvaloniaProperty.Register<ConfidenceIndicator, bool>(nameof(IsAccepted));

    private ArcContentSwap? _swap;
    private ExtractionValue? _observed;
    private ConfidenceKind _kind = ConfidenceKind.None;
    private string _text = string.Empty;
    private bool _hasFace;

    static ConfidenceIndicator()
    {
        FocusableProperty.OverrideDefaultValue<ConfidenceIndicator>(false);
    }

    /// <inheritdoc cref="ValueProperty"/>
    public ExtractionValue? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="TypeNameProperty"/>
    public string? TypeName { get => GetValue(TypeNameProperty); set => SetValue(TypeNameProperty, value); }

    /// <inheritdoc cref="EditedTextProperty"/>
    public string? EditedText { get => GetValue(EditedTextProperty); set => SetValue(EditedTextProperty, value); }

    /// <inheritdoc cref="AutomationTextProperty"/>
    public string? AutomationText { get => GetValue(AutomationTextProperty); set => SetValue(AutomationTextProperty, value); }

    /// <inheritdoc cref="IsAcceptedProperty"/>
    public bool IsAccepted { get => GetValue(IsAcceptedProperty); set => SetValue(IsAcceptedProperty, value); }

    /// <summary>What the column shows now.</summary>
    internal ConfidenceKind Kind => _kind;

    /// <summary>The text shown now ("99%", "Date", "Edited").</summary>
    internal string Text => _text;

    /// <summary>The face on screen (the newest one during a crossfade).</summary>
    internal ConfidenceIndicatorFace? Face => _swap?.Content as ConfidenceIndicatorFace;

    /// <summary>The swap that crossfades the faces.</summary>
    internal ArcContentSwap? Swap => _swap;

    /// <summary>What a value shows: the kind and its text.</summary>
    internal static (ConfidenceKind Kind, string Text) Describe(ExtractionValue? value, bool accepted, string? typeName, string? editedText)
    {
        if (value is null || value.Status == ExtractionValueStatus.Pending)
        {
            return string.IsNullOrEmpty(typeName) ? (ConfidenceKind.None, string.Empty) : (ConfidenceKind.Type, typeName);
        }
        accepted |= value.Status == ExtractionValueStatus.Accepted;
        if (value.Status == ExtractionValueStatus.Edited)
        {
            return (accepted ? ConfidenceKind.EditedAccepted : ConfidenceKind.Edited, editedText ?? string.Empty);
        }
        var percent = Percent(value.Confidence);
        if (accepted)
        {
            return (ConfidenceKind.Accepted, percent);
        }
        if (value.Status == ExtractionValueStatus.Checked)
        {
            return (ConfidenceKind.Checked, percent);
        }
        return (value.NeedsCheck ? ConfidenceKind.Low : ConfidenceKind.Percent, percent);
    }

    /// <summary>A confidence as a whole percent ("99%"; 0.935 rounds to 94).</summary>
    internal static string Percent(double confidence)
    {
        var percent = double.IsFinite(confidence) ? Math.Round(Math.Clamp(confidence, 0, 1) * 100, MidpointRounding.AwayFromZero) : 0;
        return percent.ToString("0", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Whether a kind shows the check (it pops when it appears).</summary>
    internal static bool HasCheck(ConfidenceKind kind) => kind is ConfidenceKind.Checked or ConfidenceKind.Accepted or ConfidenceKind.EditedAccepted;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _swap = e.NameScope.Find<ArcContentSwap>(SwapPart);
        _hasFace = false;
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(Value);
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            if (TopLevel.GetTopLevel(this) is not null)
            {
                Observe(change.GetNewValue<ExtractionValue?>());
            }
            Refresh();
        }
        else if (change.Property == TypeNameProperty || change.Property == EditedTextProperty || change.Property == IsAcceptedProperty)
        {
            Refresh();
        }
        else if (change.Property == AutomationTextProperty)
        {
            AutomationProperties.SetName(this, change.GetNewValue<string?>());
        }
    }

    private void Observe(ExtractionValue? value)
    {
        if (ReferenceEquals(_observed, value))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnValuePropertyChanged;
        }
        _observed = value;
        if (_observed is not null)
        {
            _observed.PropertyChanged += OnValuePropertyChanged;
        }
    }

    private void OnValuePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ExtractionValue.StatusProperty || e.Property == ExtractionValue.ConfidenceProperty ||
            e.Property == ExtractionValue.NeedsCheckProperty)
        {
            Refresh();
        }
    }

    /// <summary>Shows a new face when what the column shows changed (the swap crossfades it; the first face is simply there).</summary>
    private void Refresh()
    {
        var (kind, text) = Describe(Value, IsAccepted, TypeName, EditedText);
        if (_hasFace && kind == _kind && string.Equals(text, _text, StringComparison.Ordinal))
        {
            return;
        }
        var previous = _kind;
        _kind = kind;
        _text = text;
        if (_swap is null)
        {
            return;
        }
        var face = new ConfidenceIndicatorFace
        {
            Kind = kind,
            Text = text,
            // A check pops when its state arrives (Looks right, and each value again as Accept all reaches it); a percent
            // that changes next to it keeps it still.
            Pops = _hasFace && HasCheck(kind) && kind != previous
        };
        _hasFace = true;
        _swap.Content = face;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new IndicatorPeer(this);

    /// <summary>One text node with the owner's sentence; the visible text and icon are not read.</summary>
    private sealed class IndicatorPeer(ConfidenceIndicator owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override IReadOnlyList<AutomationPeer> GetChildrenCore() => Array.Empty<AutomationPeer>();

        protected override bool IsContentElementCore() => !string.IsNullOrEmpty(((ConfidenceIndicator)Owner).AutomationText);
    }
}
