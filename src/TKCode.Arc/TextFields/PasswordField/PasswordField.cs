using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// Arc password field: a labeled password input with a reveal toggle inside the frame. The toggle's eye is
/// crossed out by a slash that draws itself (it is never swapped for a second icon), and after the first toggle
/// every switch between dots and characters resolves in place from a light blur. Enter or Space on the toggle
/// shows or hides the password; the toggle reports its pressed state and switches its name between
/// <see cref="ShowPasswordText"/> and <see cref="HidePasswordText"/>.
/// </summary>
[TemplatePart(RevealButtonPart, typeof(FieldIconButton))]
public class PasswordField : FieldTextBox
{
    public const string RevealButtonPart = "PART_RevealButton";

    /// <summary>Shows the password as text (two-way).</summary>
    public static readonly StyledProperty<bool> IsRevealedProperty =
        AvaloniaProperty.Register<PasswordField, bool>(nameof(IsRevealed), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Name of the toggle while the password is hidden.</summary>
    public static readonly StyledProperty<string> ShowPasswordTextProperty =
        AvaloniaProperty.Register<PasswordField, string>(nameof(ShowPasswordText), "Show password");

    /// <summary>Name of the toggle while the password is shown.</summary>
    public static readonly StyledProperty<string> HidePasswordTextProperty =
        AvaloniaProperty.Register<PasswordField, string>(nameof(HidePasswordText), "Hide password");

    /// <summary>Shows the password as text.</summary>
    public bool IsRevealed { get => GetValue(IsRevealedProperty); set => SetValue(IsRevealedProperty, value); }

    /// <summary>Name of the toggle while the password is hidden.</summary>
    public string ShowPasswordText { get => GetValue(ShowPasswordTextProperty); set => SetValue(ShowPasswordTextProperty, value); }

    /// <summary>Name of the toggle while the password is shown.</summary>
    public string HidePasswordText { get => GetValue(HidePasswordTextProperty); set => SetValue(HidePasswordTextProperty, value); }

    private FieldIconButton? _revealButton;
    private TextPresenter? _presenter;
    private MotionValue<double>? _resolve;

    static PasswordField()
    {
        PasswordCharProperty.OverrideDefaultValue<PasswordField>('•');
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_revealButton is not null)
        {
            _revealButton.Click -= OnRevealClick;
        }
        base.OnApplyTemplate(e);
        _revealButton = e.NameScope.Find<FieldIconButton>(RevealButtonPart);
        _presenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");
        if (_revealButton is not null)
        {
            _revealButton.Click += OnRevealClick;
        }
        UpdateToggleName();
    }

    private void OnRevealClick(object? sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsRevealedProperty, !IsRevealed);
        e.Handled = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsRevealedProperty)
        {
            SetCurrentValue(RevealPasswordProperty, IsRevealed);
            UpdateToggleName();
            Resolve();
        }
        else if (change.Property == ShowPasswordTextProperty || change.Property == HidePasswordTextProperty)
        {
            UpdateToggleName();
        }
    }

    /// <summary>The toggle's accessible name for the current state.</summary>
    protected virtual string ToggleName => IsRevealed ? HidePasswordText : ShowPasswordText;

    private void UpdateToggleName()
    {
        if (_revealButton is not null)
        {
            AutomationProperties.SetName(_revealButton, ToggleName);
        }
    }

    /// <summary>Dots and characters resolve into each other: opacity 0.35 and a 2px blur settle on standard + enter.</summary>
    private void Resolve()
    {
        if (_presenter is null || !IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        if (_resolve is null)
        {
            _resolve = new MotionValue<double>(this, 1, MotionVectors.Double);
            _resolve.Changed += p =>
            {
                if (_presenter is { } presenter)
                {
                    presenter.Opacity = 0.35 + 0.65 * Math.Clamp(p, 0, 1);
                    presenter.Effect = ArcBlur.Get(2 * (1 - Math.Clamp(p, 0, 1)));
                }
            };
        }
        _resolve.Jump(0);
        _ = _resolve.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
    }
}
