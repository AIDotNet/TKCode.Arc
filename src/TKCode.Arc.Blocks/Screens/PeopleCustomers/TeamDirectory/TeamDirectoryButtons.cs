using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;
using AvaloniaTextBox = Avalonia.Controls.TextBox;

namespace TKCode.Arc;

/// <summary>
/// The profile's copy button (Arc Team directory): a 28px pill with a 14px copy glyph and a label that is at least 38
/// wide so Copy / Copied / Retry do not change its width. Copied turns the glyph into a check (cross-fade 160ms, scale
/// on the 580ms CSS spring) and the label and check to the success color; Retry turns label and glyph to the danger
/// color and keeps the glyph. Colors change at once. Icon-only it is 28 × 28 and the label is hidden.
/// The public <see cref="CopyButton"/> is not used: its glyphs, label morph, hover fill, 1.9s reset and timer that every
/// click restarts all differ from this block.
/// </summary>
[PseudoClasses(CopiedPseudoClass, ErrorPseudoClass, IconOnlyPseudoClass)]
internal sealed class TeamDirectoryCopyButton : AvaloniaButton
{
    public const string CopiedPseudoClass = ":copied";
    public const string ErrorPseudoClass = ":error";
    public const string IconOnlyPseudoClass = ":icon-only";

    public static readonly StyledProperty<CopyState> StateProperty =
        AvaloniaProperty.Register<TeamDirectoryCopyButton, CopyState>(nameof(State));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<TeamDirectoryCopyButton, string?>(nameof(Label));

    public static readonly StyledProperty<string?> IdleLabelProperty =
        AvaloniaProperty.Register<TeamDirectoryCopyButton, string?>(nameof(IdleLabel));

    public static readonly StyledProperty<string?> CopiedLabelProperty =
        AvaloniaProperty.Register<TeamDirectoryCopyButton, string?>(nameof(CopiedLabel));

    public static readonly StyledProperty<string?> RetryLabelProperty =
        AvaloniaProperty.Register<TeamDirectoryCopyButton, string?>(nameof(RetryLabel));

    public static readonly StyledProperty<bool> IsIconOnlyProperty =
        AvaloniaProperty.Register<TeamDirectoryCopyButton, bool>(nameof(IsIconOnly));

    protected override Type StyleKeyOverride => typeof(TeamDirectoryCopyButton);

    public CopyState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }

    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    public string? IdleLabel { get => GetValue(IdleLabelProperty); set => SetValue(IdleLabelProperty, value); }

    public string? CopiedLabel { get => GetValue(CopiedLabelProperty); set => SetValue(CopiedLabelProperty, value); }

    public string? RetryLabel { get => GetValue(RetryLabelProperty); set => SetValue(RetryLabelProperty, value); }

    public bool IsIconOnly { get => GetValue(IsIconOnlyProperty); set => SetValue(IsIconOnlyProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty)
        {
            PseudoClasses.Set(CopiedPseudoClass, State == CopyState.Copied);
            PseudoClasses.Set(ErrorPseudoClass, State == CopyState.Error);
        }
        else if (change.Property == IsIconOnlyProperty)
        {
            PseudoClasses.Set(IconOnlyPseudoClass, IsIconOnly);
        }
    }
}

/// <summary>
/// A text button: the no-match message's Clear filters (13, medium, an underline 3 below the text in the strong border
/// color) and, plain, the profile's email address (no underline, one line with an ellipsis).
/// </summary>
[PseudoClasses(UnderlinedPseudoClass)]
internal sealed class TeamDirectoryLinkButton : AvaloniaButton
{
    public const string UnderlinedPseudoClass = ":underlined";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TeamDirectoryLinkButton, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsUnderlinedProperty =
        AvaloniaProperty.Register<TeamDirectoryLinkButton, bool>(nameof(IsUnderlined));

    protected override Type StyleKeyOverride => typeof(TeamDirectoryLinkButton);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsUnderlined { get => GetValue(IsUnderlinedProperty); set => SetValue(IsUnderlinedProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsUnderlinedProperty)
        {
            PseudoClasses.Set(UnderlinedPseudoClass, IsUnderlined);
        }
    }
}

/// <summary>The search box's round 24px clear button: a 14px cross, muted, darker on a muted fill when hovered (no transition).</summary>
internal sealed class TeamDirectoryClearButton : AvaloniaButton
{
    protected override Type StyleKeyOverride => typeof(TeamDirectoryClearButton);
}

/// <summary>The search box's text input: no frame of its own (the box draws it), a muted placeholder.</summary>
[TemplatePart(TextPresenterPart, typeof(TextPresenter))]
internal sealed class TeamDirectorySearchInput : AvaloniaTextBox
{
    public const string TextPresenterPart = "PART_TextPresenter";

    /// <summary>The presenter, whose pre-edit text tells whether an input method is composing.</summary>
    public TextPresenter? Presenter { get; private set; }

    /// <summary>True while an input method is composing text (Escape belongs to it then).</summary>
    public bool IsComposing => !string.IsNullOrEmpty(Presenter?.PreeditText);

    protected override Type StyleKeyOverride => typeof(TeamDirectorySearchInput);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Presenter = e.NameScope.Find<TextPresenter>(TextPresenterPart);
    }
}
