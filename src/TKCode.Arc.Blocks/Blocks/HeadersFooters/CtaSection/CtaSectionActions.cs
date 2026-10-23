// Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli): Action and the dismiss button.

using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The content of an action button: the label, then a 16 px arrow that leans 3 px forward while the button is hovered
/// (primary actions), or a check mark before <see cref="Text"/> once the action is confirmed. Its theme animates the lean.
/// </summary>
[PseudoClasses(LeanPseudoClass)]
internal sealed class CtaActionContent : TemplatedControl
{
    public const string LeanPseudoClass = ":lean";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<CtaActionContent, string?>(nameof(Text));

    public static readonly StyledProperty<bool> HasArrowProperty =
        AvaloniaProperty.Register<CtaActionContent, bool>(nameof(HasArrow));

    public static readonly StyledProperty<bool> HasCheckProperty =
        AvaloniaProperty.Register<CtaActionContent, bool>(nameof(HasCheck));

    public static readonly StyledProperty<bool> IsLeaningProperty =
        AvaloniaProperty.Register<CtaActionContent, bool>(nameof(IsLeaning));

    static CtaActionContent()
    {
        FocusableProperty.OverrideDefaultValue<CtaActionContent>(false);
        IsHitTestVisibleProperty.OverrideDefaultValue<CtaActionContent>(false);
    }

    /// <summary>The label.</summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Shows the forward arrow after the label.</summary>
    public bool HasArrow { get => GetValue(HasArrowProperty); set => SetValue(HasArrowProperty, value); }

    /// <summary>Shows the confirmation check before the label.</summary>
    public bool HasCheck { get => GetValue(HasCheckProperty); set => SetValue(HasCheckProperty, value); }

    /// <summary>The arrow leans forward (the button is hovered by a mouse or pen).</summary>
    public bool IsLeaning { get => GetValue(IsLeaningProperty); set => SetValue(IsLeaningProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsLeaningProperty)
        {
            PseudoClasses.Set(LeanPseudoClass, IsLeaning);
        }
    }
}

/// <summary>
/// One action of the section as an Arc button (Large in the centered and split shapes, Small in the banner). It shows the
/// action's label, the arrow on primary actions, and, after an in-place confirmation, a check with the confirmed label;
/// changing the text swaps it with the button's own crossfade. Only a mouse or pen hover makes the arrow lean.
/// </summary>
internal sealed class CtaActionButton : Button
{
    private CtaActionContent? _content;
    private bool _mouseHover;

    public CtaActionButton(CtaAction action, bool isPrimary, ControlSize size)
    {
        Action = action;
        IsPrimary = isPrimary;
        Size = size;
        Variant = isPrimary ? ButtonVariant.Primary : ButtonVariant.Secondary;
        Refresh();
    }

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>The action this button shows.</summary>
    public CtaAction Action { get; }

    /// <summary>True for the primary action.</summary>
    public bool IsPrimary { get; }

    /// <summary>True while the action shows its confirmed state.</summary>
    public bool IsConfirmed { get; private set; }

    /// <summary>The label currently shown (the confirmed one after a confirmation).</summary>
    public string CurrentText => IsConfirmed ? Action.ConfirmedLabel ?? Action.Label ?? string.Empty : Action.Label ?? string.Empty;

    /// <summary>Flips the in-place confirmation and updates the content.</summary>
    public void ToggleConfirmed()
    {
        IsConfirmed = !IsConfirmed;
        Refresh();
    }

    /// <summary>Puts the action back to its plain state.</summary>
    public void Reset()
    {
        if (IsConfirmed)
        {
            IsConfirmed = false;
            Refresh();
        }
    }

    /// <summary>Rebuilds the content from the action's current text and state.</summary>
    public void Refresh()
    {
        _content = new CtaActionContent
        {
            Text = CurrentText,
            HasCheck = IsConfirmed,
            HasArrow = IsPrimary && !IsConfirmed,
            IsLeaning = _mouseHover && IsPointerOver
        };
        Content = _content;
        AutomationProperties.SetName(this, CurrentText);
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        // Arc's (hover: hover) and (pointer: fine): a touch never leans the arrow.
        _mouseHover = e.Pointer.Type != PointerType.Touch;
        base.OnPointerEntered(e);
        UpdateLean();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _mouseHover = false;
        UpdateLean();
    }

    private void UpdateLean()
    {
        if (_content is not null)
        {
            _content.IsLeaning = _mouseHover && IsPointerOver;
        }
    }
}

/// <summary>The banner's dismiss button: a 36 px round ghost button with a 16 px X, TextMuted until hovered, pressing to .94.</summary>
internal sealed class CtaDismissButton : AvaloniaButton
{
    static CtaDismissButton()
    {
        FocusableProperty.OverrideDefaultValue<CtaDismissButton>(true);
    }
}
