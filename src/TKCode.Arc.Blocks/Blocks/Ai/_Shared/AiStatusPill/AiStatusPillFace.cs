using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;

namespace TKCode.Arc;

/// <summary>
/// What an <see cref="AiStatusPill"/> shows for one status: the icon cell (14, or 12 when compact), 6px, then the word. Thinking
/// and Writing turn a LoaderCircle and shimmer the word; Done is a Success check, Finished first an AccentStrong flag on
/// AccentStrong text, Stopped a filled square, Failed a Danger alert circle. A pill makes a new face for every word, so the
/// old one fades out in its own colors while the new one comes in, and a new Thinking or Writing face starts its sweep and
/// spinner from the beginning (Arc remounts them).
/// </summary>
[PseudoClasses(ThinkingPseudoClass, WritingPseudoClass, DonePseudoClass, FinishedFirstPseudoClass, StoppedPseudoClass,
    FailedPseudoClass, CompactPseudoClass)]
internal sealed class AiStatusPillFace : TemplatedControl
{
    public const string ThinkingPseudoClass = ":thinking";
    public const string WritingPseudoClass = ":writing";
    public const string DonePseudoClass = ":done";
    public const string FinishedFirstPseudoClass = ":finished-first";
    public const string StoppedPseudoClass = ":stopped";
    public const string FailedPseudoClass = ":failed";
    public const string CompactPseudoClass = ":compact";

    /// <summary>The status the face shows; fixed for the face's life in a pill.</summary>
    public static readonly StyledProperty<ArenaPaneStatus> StatusProperty =
        AvaloniaProperty.Register<AiStatusPillFace, ArenaPaneStatus>(nameof(Status));

    /// <summary>The word.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AiStatusPillFace, string?>(nameof(Text));

    /// <summary>The narrow tab's size: a 12px icon.</summary>
    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<AiStatusPillFace, bool>(nameof(IsCompact));

    /// <inheritdoc cref="StatusProperty"/>
    public ArenaPaneStatus Status { get => GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    /// <inheritdoc cref="TextProperty"/>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="IsCompactProperty"/>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>Whether the face turns a spinner and shimmers its word.</summary>
    internal bool IsBusy => Status is ArenaPaneStatus.Thinking or ArenaPaneStatus.Writing;

    public AiStatusPillFace()
    {
        UpdatePseudoClasses();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StatusProperty || change.Property == IsCompactProperty)
        {
            UpdatePseudoClasses();
        }
    }

    private void UpdatePseudoClasses()
    {
        var status = Status;
        PseudoClasses.Set(ThinkingPseudoClass, status == ArenaPaneStatus.Thinking);
        PseudoClasses.Set(WritingPseudoClass, status == ArenaPaneStatus.Writing);
        PseudoClasses.Set(DonePseudoClass, status == ArenaPaneStatus.Done);
        PseudoClasses.Set(FinishedFirstPseudoClass, status == ArenaPaneStatus.FinishedFirst);
        PseudoClasses.Set(StoppedPseudoClass, status == ArenaPaneStatus.Stopped);
        PseudoClasses.Set(FailedPseudoClass, status == ArenaPaneStatus.Failed);
        PseudoClasses.Set(CompactPseudoClass, IsCompact);
    }
}
