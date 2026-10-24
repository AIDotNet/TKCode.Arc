using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The base of the Auth blocks' quiet buttons that can be unavailable without leaving the tab order (Arc's
/// <c>aria-disabled</c>): while <see cref="IsInert"/> is true the button keeps keyboard focus and its place in the Tab
/// order, ignores activation (no Click) and carries <c>:inert</c> for its theme. Each block's subclass keeps its own
/// ControlTheme (its <c>StyleKeyOverride</c>).
/// </summary>
[PseudoClasses(InertPseudoClass)]
internal abstract class AuthInertButton : AvaloniaButton
{
    public const string InertPseudoClass = ":inert";

    /// <summary>Arc's <c>aria-disabled</c>: presses do nothing; focus and the tab stop stay.</summary>
    public static readonly StyledProperty<bool> IsInertProperty =
        AvaloniaProperty.Register<AuthInertButton, bool>(nameof(IsInert));

    /// <inheritdoc cref="IsInertProperty"/>
    public bool IsInert { get => GetValue(IsInertProperty); set => SetValue(IsInertProperty, value); }

    protected override void OnClick()
    {
        if (IsInert)
        {
            return;
        }
        base.OnClick();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsInertProperty)
        {
            PseudoClasses.Set(InertPseudoClass, IsInert);
        }
    }
}
