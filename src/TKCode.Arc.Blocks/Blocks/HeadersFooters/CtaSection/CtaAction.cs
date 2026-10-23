// Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli): the action rules of cta-section.tsx.

using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The shape of a <see cref="CtaSection"/>.</summary>
public enum CtaVariant
{
    /// <summary>A closing section: headline, text, two actions and a line of small print with faces.</summary>
    Centered,

    /// <summary>Copy on the left, beside a setup card that completes itself once it is in view.</summary>
    Split,

    /// <summary>One dismissible line with a single action.</summary>
    Banner
}

/// <summary>
/// One action of a <see cref="CtaSection"/> (Arc's <c>CtaAction</c>). With <see cref="Href"/> it is a link-style action:
/// pressing it raises <see cref="CtaSection.ActionInvoked"/> and, when nobody handles the event, opens an absolute http,
/// https or mailto address. With a <see cref="Command"/>, or when a handler marks the event handled, it only runs. With
/// none of these it confirms in place (a check and <see cref="ConfirmedLabel"/>; pressing again restores it), so a
/// preview always answers a press.
/// </summary>
/// <remarks>Ported from Arc's free cta-section block (MIT, Copyright (c) 2026 Elia Kuratli).</remarks>
public class CtaAction : AvaloniaObject
{
    /// <summary>The button text.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<CtaAction, string?>(nameof(Label));

    /// <summary>The destination: makes the action a link.</summary>
    public static readonly StyledProperty<string?> HrefProperty =
        AvaloniaProperty.Register<CtaAction, string?>(nameof(Href));

    /// <summary>The text shown after an in-place confirmation (default: <see cref="Label"/>).</summary>
    public static readonly StyledProperty<string?> ConfirmedLabelProperty =
        AvaloniaProperty.Register<CtaAction, string?>(nameof(ConfirmedLabel));

    /// <summary>Runs when the action is pressed, after <see cref="CtaSection.ActionInvoked"/>.</summary>
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<CtaAction, ICommand?>(nameof(Command));

    /// <summary>The parameter passed to <see cref="Command"/>.</summary>
    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<CtaAction, object?>(nameof(CommandParameter));

    /// <inheritdoc cref="LabelProperty"/>
    public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="HrefProperty"/>
    public string? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <inheritdoc cref="ConfirmedLabelProperty"/>
    public string? ConfirmedLabel { get => GetValue(ConfirmedLabelProperty); set => SetValue(ConfirmedLabelProperty, value); }

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <inheritdoc cref="CommandParameterProperty"/>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
}

/// <summary>Event data of <see cref="CtaSection.ActionInvoked"/>.</summary>
public sealed class CtaActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public CtaActionEventArgs(RoutedEvent routedEvent, object source, CtaAction action, bool isPrimary, bool isConfirmed)
        : base(routedEvent, source)
    {
        Action = action;
        IsPrimary = isPrimary;
        IsConfirmed = isConfirmed;
    }

    /// <summary>The action that was pressed (the section's default when none was set).</summary>
    public CtaAction Action { get; }

    /// <summary>True for the primary action, false for the secondary one.</summary>
    public bool IsPrimary { get; }

    /// <summary>
    /// True when this press puts the action into its confirmed state: it has no <see cref="CtaAction.Href"/> and no
    /// <see cref="CtaAction.Command"/>, was not confirmed before, and no handler marks the event handled.
    /// </summary>
    public bool IsConfirmed { get; }
}
