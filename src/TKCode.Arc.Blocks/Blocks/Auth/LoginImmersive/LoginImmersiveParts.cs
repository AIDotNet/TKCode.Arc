using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>How a <see cref="LoginImmersiveTextButton"/> looks.</summary>
internal enum LoginImmersiveTextButtonKind
{
    /// <summary>A word in a line, underlined (Edit).</summary>
    Inline,

    /// <summary>A pill-shaped text button in the card (the resend button).</summary>
    Pill,

    /// <summary>A pill-shaped text button on the picture (Terms, Privacy).</summary>
    OnPhoto
}

/// <summary>
/// The page's quiet text buttons (Edit, the resend button, Terms and Privacy). While <see cref="AuthInertButton.IsInert"/>
/// is true (the resend timer runs) the button looks muted and ignores activation but keeps keyboard focus (Arc's
/// <c>aria-disabled</c>).
/// </summary>
internal sealed class LoginImmersiveTextButton : AuthInertButton
{
    public static readonly StyledProperty<LoginImmersiveTextButtonKind> KindProperty =
        AvaloniaProperty.Register<LoginImmersiveTextButton, LoginImmersiveTextButtonKind>(nameof(Kind));

    public LoginImmersiveTextButtonKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override Type StyleKeyOverride => typeof(LoginImmersiveTextButton);
}

/// <summary>
/// The header switch between signing in and creating an account: one pill button holding both sentences ("No
/// account? Sign up" and "Have an account? Sign in") in the same cell, so its width never changes; the inactive one is
/// faded, lifted 6px and blurred 2px, and they cross over in 240 ms on the enter curve. Hover and keyboard focus
/// underline the action word.
/// </summary>
/// <remarks>Code-driven: both sentences animate at once from wherever they are; the theme only sets colours.</remarks>
[TemplatePart(SignInGroupPart, typeof(Control))]
[TemplatePart(SignUpGroupPart, typeof(Control))]
[PseudoClasses(SignUpPseudoClass)]
internal sealed class LoginImmersiveSwitch : AvaloniaButton
{
    public const string SignInGroupPart = "PART_SignInGroup";
    public const string SignUpGroupPart = "PART_SignUpGroup";
    public const string SignUpPseudoClass = ":signup";

    public static readonly StyledProperty<bool> IsSignUpProperty =
        AvaloniaProperty.Register<LoginImmersiveSwitch, bool>(nameof(IsSignUp));

    public static readonly StyledProperty<string?> SignUpPromptProperty =
        AvaloniaProperty.Register<LoginImmersiveSwitch, string?>(nameof(SignUpPrompt));

    public static readonly StyledProperty<string?> SignUpActionProperty =
        AvaloniaProperty.Register<LoginImmersiveSwitch, string?>(nameof(SignUpAction));

    public static readonly StyledProperty<string?> SignInPromptProperty =
        AvaloniaProperty.Register<LoginImmersiveSwitch, string?>(nameof(SignInPrompt));

    public static readonly StyledProperty<string?> SignInActionProperty =
        AvaloniaProperty.Register<LoginImmersiveSwitch, string?>(nameof(SignInAction));

    private Control? _signInGroup;
    private Control? _signUpGroup;
    private MotionValue<double>? _signInState;
    private MotionValue<double>? _signUpState;

    /// <summary>True shows "Have an account? Sign in" (the page is creating an account).</summary>
    public bool IsSignUp { get => GetValue(IsSignUpProperty); set => SetValue(IsSignUpProperty, value); }

    /// <summary>The question shown while signing in ("No account?").</summary>
    public string? SignUpPrompt { get => GetValue(SignUpPromptProperty); set => SetValue(SignUpPromptProperty, value); }

    /// <summary>The action shown while signing in ("Sign up").</summary>
    public string? SignUpAction { get => GetValue(SignUpActionProperty); set => SetValue(SignUpActionProperty, value); }

    /// <summary>The question shown while creating an account ("Have an account?").</summary>
    public string? SignInPrompt { get => GetValue(SignInPromptProperty); set => SetValue(SignInPromptProperty, value); }

    /// <summary>The action shown while creating an account ("Sign in").</summary>
    public string? SignInAction { get => GetValue(SignInActionProperty); set => SetValue(SignInActionProperty, value); }

    protected override Type StyleKeyOverride => typeof(LoginImmersiveSwitch);

    /// <summary>The visible sentence's group opacity: sign-in sentence, sign-up sentence (tests read it).</summary>
    internal (double SignIn, double SignUp) GroupOpacity => (_signInGroup?.Opacity ?? 0, _signUpGroup?.Opacity ?? 0);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _signInState?.Dispose();
        _signUpState?.Dispose();
        _signInGroup = e.NameScope.Find<Control>(SignInGroupPart);
        _signUpGroup = e.NameScope.Find<Control>(SignUpGroupPart);
        _signInState = Track(_signInGroup, IsSignUp ? 0 : 1);
        _signUpState = Track(_signUpGroup, IsSignUp ? 1 : 0);
        UpdateName();
    }

    private MotionValue<double>? Track(Control? group, double initial)
    {
        if (group is null)
        {
            return null;
        }
        var translate = new TranslateTransform();
        group.RenderTransform = translate;
        var state = new MotionValue<double>(this, initial, MotionVectors.Double);
        state.Changed += v => Apply(group, translate, v);
        Apply(group, translate, initial);
        return state;
    }

    private void Apply(Control group, TranslateTransform translate, double active)
    {
        // Under reduced motion the inactive sentence only fades (no lift, no blur).
        var full = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        group.Opacity = active;
        translate.Y = full ? -6 * (1 - active) : 0;
        group.Effect = full ? ArcBlur.Get(2 * (1 - active)) : null;
        group.IsHitTestVisible = active > 0.5;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSignUpProperty)
        {
            PseudoClasses.Set(SignUpPseudoClass, IsSignUp);
            var level = ArcMotion.GetEffectiveLevel(this);
            var spec = level == ArcMotionLevel.Full
                ? MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter)
                : MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Enter);
            if (!IsLoaded || level == ArcMotionLevel.Off)
            {
                _signInState?.Jump(IsSignUp ? 0 : 1);
                _signUpState?.Jump(IsSignUp ? 1 : 0);
            }
            else
            {
                _ = _signInState?.AnimateTo(IsSignUp ? 0 : 1, spec);
                _ = _signUpState?.AnimateTo(IsSignUp ? 1 : 0, spec);
            }
            UpdateName();
        }
        else if (change.Property == SignUpPromptProperty || change.Property == SignUpActionProperty ||
                 change.Property == SignInPromptProperty || change.Property == SignInActionProperty)
        {
            UpdateName();
        }
    }

    private void UpdateName()
    {
        // The accessible name is the visible sentence; the text spans themselves are not read.
        var name = IsSignUp ? $"{SignInPrompt} {SignInAction}" : $"{SignUpPrompt} {SignUpAction}";
        AutomationProperties.SetName(this, name.Trim());
    }
}

/// <summary>
/// The footer: the note on the left and the legal links on the right. Like a wrapping flex row whose note has a basis
/// of 220px: when the note's basis, the 24px gap and the links do not fit, the links drop to a second line at the
/// left; otherwise the note takes the rest of the line and wraps inside it.
/// </summary>
internal sealed class LoginImmersiveFooterPanel : Panel
{
    /// <summary>The note's basis (220).</summary>
    public const double NoteBasis = 220;

    /// <summary>Space between the note and the links on one line (24).</summary>
    public const double ColumnGap = 24;

    /// <summary>Space between the two lines when they wrap (8).</summary>
    public const double RowGap = 8;

    private bool _wrapped;

    /// <summary>Whether the links are on their own line (tests read it).</summary>
    internal bool IsWrapped => _wrapped;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count < 2)
        {
            return default;
        }
        var note = Children[0];
        var legal = Children[1];
        legal.Measure(Size.Infinity);
        var width = availableSize.Width;
        var legalWidth = legal.DesiredSize.Width;
        _wrapped = !double.IsInfinity(width) && NoteBasis + ColumnGap + legalWidth > width;
        if (_wrapped)
        {
            note.Measure(new Size(width, double.PositiveInfinity));
            var height = note.DesiredSize.Height > 0 ? note.DesiredSize.Height + RowGap + legal.DesiredSize.Height : legal.DesiredSize.Height;
            return new Size(width, height);
        }
        var noteWidth = double.IsInfinity(width) ? double.PositiveInfinity : Math.Max(0, width - ColumnGap - legalWidth);
        note.Measure(new Size(noteWidth, double.PositiveInfinity));
        var lineHeight = Math.Max(note.DesiredSize.Height, legal.DesiredSize.Height);
        return new Size(double.IsInfinity(width) ? note.DesiredSize.Width + ColumnGap + legalWidth : width, lineHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count < 2)
        {
            return finalSize;
        }
        var note = Children[0];
        var legal = Children[1];
        var legalSize = legal.DesiredSize;
        if (_wrapped)
        {
            var noteHeight = note.DesiredSize.Height;
            note.Arrange(new Rect(0, 0, finalSize.Width, noteHeight));
            var top = noteHeight > 0 ? noteHeight + RowGap : 0;
            legal.Arrange(new Rect(0, top, legalSize.Width, legalSize.Height));
            return finalSize;
        }
        var noteWidth = Math.Max(0, finalSize.Width - ColumnGap - legalSize.Width);
        var noteSize = note.DesiredSize;
        note.Arrange(new Rect(0, (finalSize.Height - noteSize.Height) / 2, noteWidth, noteSize.Height));
        legal.Arrange(new Rect(finalSize.Width - legalSize.Width, (finalSize.Height - legalSize.Height) / 2, legalSize.Width, legalSize.Height));
        return finalSize;
    }
}

/// <summary>A row of equally wide children with a fixed gap (the single sign-on buttons: three columns 8px apart).</summary>
internal sealed class LoginImmersiveEqualRow : Panel
{
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<LoginImmersiveEqualRow, double>(nameof(Gap), 8);

    static LoginImmersiveEqualRow()
    {
        AffectsMeasure<LoginImmersiveEqualRow>(GapProperty);
    }

    public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }

    private int VisibleCount => Children.Count(c => c.IsVisible);

    protected override Size MeasureOverride(Size availableSize)
    {
        var count = VisibleCount;
        if (count == 0)
        {
            return default;
        }
        var cell = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : Math.Max(0, (availableSize.Width - Gap * (count - 1)) / count);
        var height = 0.0;
        var widest = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Measure(new Size(cell, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
        }
        var width = double.IsInfinity(availableSize.Width) ? widest * count + Gap * (count - 1) : availableSize.Width;
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var count = VisibleCount;
        if (count == 0)
        {
            return finalSize;
        }
        var cell = Math.Max(0, (finalSize.Width - Gap * (count - 1)) / count);
        var x = 0.0;
        foreach (var child in Children)
        {
            if (!child.IsVisible)
            {
                continue;
            }
            child.Arrange(new Rect(x, 0, cell, finalSize.Height));
            x += cell + Gap;
        }
        return finalSize;
    }
}

/// <summary>
/// A step's heading: the title (display face, 36px; 28 in a compact card) over its sentence (14px, secondary). The
/// heading takes focus when its step opens (without a visible ring), so screen readers start there.
/// </summary>
[PseudoClasses(CompactPseudoClass)]
internal sealed class LoginImmersiveHeading : TemplatedControl
{
    public const string CompactPseudoClass = ":compact";

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<LoginImmersiveHeading, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<LoginImmersiveHeading, string?>(nameof(Description));

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<LoginImmersiveHeading, bool>(nameof(IsCompact));

    static LoginImmersiveHeading()
    {
        FocusableProperty.OverrideDefaultValue<LoginImmersiveHeading>(true);
        IsTabStopProperty.OverrideDefaultValue<LoginImmersiveHeading>(false);
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
        else if (change.Property == TitleProperty)
        {
            AutomationProperties.SetName(this, Title);
        }
    }
}
