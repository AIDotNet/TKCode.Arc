// Ported from Arc's free sign-in block (MIT, Copyright (c) 2026 Elia Kuratli): the three steps of sign-in.tsx (email,
// code, done) and its two text buttons (.textButton, .resend). The steps are thin templated controls: their templates
// (Themes/SignInTheme.axaml) lay out Arc's markup; SignIn fills the text, wires the events and runs the motion.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>A step of the card: its padding and type follow <see cref="IsNarrow"/>; <see cref="PartsReady"/> follows each template.</summary>
[PseudoClasses(NarrowPseudoClass)]
internal abstract class SignInStepBase : TemplatedControl
{
    public const string NarrowPseudoClass = ":narrow";

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<SignInStepBase, bool>(nameof(IsNarrow));

    protected SignInStepBase()
    {
        Focusable = false;
    }

    /// <summary>The card is at most 380 wide (Arc's container query).</summary>
    public bool IsNarrow { get => GetValue(IsNarrowProperty); set => SetValue(IsNarrowProperty, value); }

    private SignInLayer? _motion;

    /// <summary>The step this control shows.</summary>
    public abstract SignInStep Step { get; }

    /// <summary>The step's slide, fade and blur while it enters or leaves.</summary>
    public SignInLayer Motion => _motion ??= new SignInLayer(this);

    /// <summary>Raised after the template is applied and the parts are found.</summary>
    public event EventHandler? PartsReady;

    /// <summary>True once the parts exist.</summary>
    public bool HasParts { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        FindParts(e.NameScope);
        HasParts = true;
        PartsReady?.Invoke(this, EventArgs.Empty);
    }

    protected abstract void FindParts(INameScope scope);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsNarrowProperty)
        {
            PseudoClasses.Set(NarrowPseudoClass, IsNarrow);
        }
    }

    protected static T Part<T>(INameScope scope, string name) where T : class =>
        scope.Find<T>(name) ?? throw new InvalidOperationException($"SignIn template part {name} is missing.");
}

/// <summary>Step one (Arc's <c>step === "email"</c>): heading, email field with a typo suggestion, Continue, the divider, passkey and SSO.</summary>
[TemplatePart("PART_Title", typeof(SignInText))]
[TemplatePart("PART_Description", typeof(SignInText))]
[TemplatePart("PART_Email", typeof(Input))]
[TemplatePart("PART_Suggestion", typeof(SpringSizeDecorator))]
[TemplatePart("PART_SuggestionBody", typeof(Control))]
[TemplatePart("PART_SuggestionBefore", typeof(TextBlock))]
[TemplatePart("PART_SuggestionButton", typeof(SignInTextButton))]
[TemplatePart("PART_SuggestionAfter", typeof(TextBlock))]
[TemplatePart("PART_Continue", typeof(Button))]
[TemplatePart("PART_Divider", typeof(Control))]
[TemplatePart("PART_DividerText", typeof(TextBlock))]
[TemplatePart("PART_Alternatives", typeof(Control))]
[TemplatePart("PART_Passkey", typeof(Button))]
[TemplatePart("PART_PasskeyText", typeof(TextBlock))]
[TemplatePart("PART_Providers", typeof(SignInProviderRow))]
internal sealed class SignInEmailStep : SignInStepBase
{
    public override SignInStep Step => SignInStep.Email;

    public SignInText Title { get; private set; } = null!;
    public SignInText Description { get; private set; } = null!;
    public Input Email { get; private set; } = null!;
    public SpringSizeDecorator Suggestion { get; private set; } = null!;
    public Control SuggestionBody { get; private set; } = null!;
    public TextBlock SuggestionBefore { get; private set; } = null!;
    public SignInTextButton SuggestionButton { get; private set; } = null!;
    public TextBlock SuggestionAfter { get; private set; } = null!;
    public Button Continue { get; private set; } = null!;
    public Control Divider { get; private set; } = null!;
    public TextBlock DividerText { get; private set; } = null!;
    public Control Alternatives { get; private set; } = null!;
    public Button Passkey { get; private set; } = null!;
    public TextBlock PasskeyText { get; private set; } = null!;
    public SignInProviderRow Providers { get; private set; } = null!;

    /// <summary>The provider buttons built for <see cref="Providers"/>, in order.</summary>
    public List<ProviderParts> ProviderButtons { get; } = new();

    /// <summary>One provider button and its pieces.</summary>
    public sealed record ProviderParts(SignInProvider Provider, Button Button, StackPanel Content, AuthProviderMark Glyph, TextBlock Label);

    protected override void FindParts(INameScope scope)
    {
        Title = Part<SignInText>(scope, "PART_Title");
        Description = Part<SignInText>(scope, "PART_Description");
        Email = Part<Input>(scope, "PART_Email");
        Suggestion = Part<SpringSizeDecorator>(scope, "PART_Suggestion");
        SuggestionBody = Part<Control>(scope, "PART_SuggestionBody");
        SuggestionBefore = Part<TextBlock>(scope, "PART_SuggestionBefore");
        SuggestionButton = Part<SignInTextButton>(scope, "PART_SuggestionButton");
        SuggestionAfter = Part<TextBlock>(scope, "PART_SuggestionAfter");
        Continue = Part<Button>(scope, "PART_Continue");
        Divider = Part<Control>(scope, "PART_Divider");
        DividerText = Part<TextBlock>(scope, "PART_DividerText");
        Alternatives = Part<Control>(scope, "PART_Alternatives");
        Passkey = Part<Button>(scope, "PART_Passkey");
        PasskeyText = Part<TextBlock>(scope, "PART_PasskeyText");
        Providers = Part<SignInProviderRow>(scope, "PART_Providers");
    }
}

/// <summary>Step two (Arc's <c>step === "code"</c>): heading, the account row, the six digit code, Verify and resend.</summary>
[TemplatePart("PART_Title", typeof(SignInText))]
[TemplatePart("PART_Description", typeof(SignInText))]
[TemplatePart("PART_Identity", typeof(Border))]
[TemplatePart("PART_Avatar", typeof(SignInAvatar))]
[TemplatePart("PART_IdentityEmail", typeof(TextBlock))]
[TemplatePart("PART_Change", typeof(Button))]
[TemplatePart("PART_Code", typeof(OtpInput))]
[TemplatePart("PART_Verify", typeof(Button))]
[TemplatePart("PART_Resend", typeof(SignInResendButton))]
[TemplatePart("PART_ResendSwap", typeof(SignInSwap))]
internal sealed class SignInCodeStep : SignInStepBase
{
    public override SignInStep Step => SignInStep.Code;

    public SignInText Title { get; private set; } = null!;
    public SignInText Description { get; private set; } = null!;
    public Border Identity { get; private set; } = null!;
    public SignInAvatar Avatar { get; private set; } = null!;
    public TextBlock IdentityEmail { get; private set; } = null!;
    public Button Change { get; private set; } = null!;
    public OtpInput Code { get; private set; } = null!;
    public Button Verify { get; private set; } = null!;
    public SignInResendButton Resend { get; private set; } = null!;
    public SignInSwap ResendSwap { get; private set; } = null!;

    /// <summary>The rolling seconds inside the waiting resend label, while it shows.</summary>
    public SignInRollingTime? Time { get; set; }

    protected override void FindParts(INameScope scope)
    {
        Title = Part<SignInText>(scope, "PART_Title");
        Description = Part<SignInText>(scope, "PART_Description");
        Identity = Part<Border>(scope, "PART_Identity");
        Avatar = Part<SignInAvatar>(scope, "PART_Avatar");
        IdentityEmail = Part<TextBlock>(scope, "PART_IdentityEmail");
        Change = Part<Button>(scope, "PART_Change");
        Code = Part<OtpInput>(scope, "PART_Code");
        Verify = Part<Button>(scope, "PART_Verify");
        Resend = Part<SignInResendButton>(scope, "PART_Resend");
        ResendSwap = Part<SignInSwap>(scope, "PART_ResendSwap");
    }
}

/// <summary>Step three (Arc's <c>step === "done"</c>): the avatar with its ring and badge, the welcome, two details and Sign out.</summary>
[TemplatePart("PART_Stage", typeof(Panel))]
[TemplatePart("PART_Ring", typeof(ArcRing))]
[TemplatePart("PART_Avatar", typeof(SignInAvatar))]
[TemplatePart("PART_Badge", typeof(Border))]
[TemplatePart("PART_Heading", typeof(Control))]
[TemplatePart("PART_Title", typeof(SignInText))]
[TemplatePart("PART_Email", typeof(TextBlock))]
[TemplatePart("PART_Details", typeof(Control))]
[TemplatePart("PART_MethodLabel", typeof(TextBlock))]
[TemplatePart("PART_MethodValue", typeof(TextBlock))]
[TemplatePart("PART_DeviceLabel", typeof(TextBlock))]
[TemplatePart("PART_DeviceValue", typeof(TextBlock))]
[TemplatePart("PART_SignOutHost", typeof(Control))]
[TemplatePart("PART_SignOut", typeof(Button))]
internal sealed class SignInDoneStep : SignInStepBase
{
    public override SignInStep Step => SignInStep.Done;

    public Panel Stage { get; private set; } = null!;
    public ArcRing Ring { get; private set; } = null!;
    public SignInAvatar Avatar { get; private set; } = null!;
    public Border Badge { get; private set; } = null!;
    public Control Heading { get; private set; } = null!;
    public SignInText Title { get; private set; } = null!;
    public TextBlock Email { get; private set; } = null!;
    public Control Details { get; private set; } = null!;
    public TextBlock MethodLabel { get; private set; } = null!;
    public TextBlock MethodValue { get; private set; } = null!;
    public TextBlock DeviceLabel { get; private set; } = null!;
    public TextBlock DeviceValue { get; private set; } = null!;
    public Control SignOutHost { get; private set; } = null!;
    public Button SignOut { get; private set; } = null!;

    protected override void FindParts(INameScope scope)
    {
        Stage = Part<Panel>(scope, "PART_Stage");
        Ring = Part<ArcRing>(scope, "PART_Ring");
        Avatar = Part<SignInAvatar>(scope, "PART_Avatar");
        Badge = Part<Border>(scope, "PART_Badge");
        Heading = Part<Control>(scope, "PART_Heading");
        Title = Part<SignInText>(scope, "PART_Title");
        Email = Part<TextBlock>(scope, "PART_Email");
        Details = Part<Control>(scope, "PART_Details");
        MethodLabel = Part<TextBlock>(scope, "PART_MethodLabel");
        MethodValue = Part<TextBlock>(scope, "PART_MethodValue");
        DeviceLabel = Part<TextBlock>(scope, "PART_DeviceLabel");
        DeviceValue = Part<TextBlock>(scope, "PART_DeviceValue");
        SignOutHost = Part<Control>(scope, "PART_SignOutHost");
        SignOut = Part<Button>(scope, "PART_SignOut");
        // Arc's <h2 tabIndex={-1}>: focusable from code (the welcome takes focus) but not a Tab stop.
        Title.Focusable = true;
        Title.FocusAdorner = null;
        KeyboardNavigation.SetIsTabStop(Title, false);
        AutomationProperties.SetHeadingLevel(Title, 2);
    }
}

/// <summary>
/// Arc's <c>.textButton</c>: inline text in the surrounding size, medium weight, underlined 3px below the baseline in the
/// strong border colour (hover: the text colour). <see cref="IsQuiet"/> hides the underline until hover (the footer's
/// "Sign up"). A 2px focus ring for the keyboard.
/// </summary>
[PseudoClasses(QuietPseudoClass, HoverPseudoClass)]
internal sealed class SignInTextButton : AvaloniaButton
{
    public const string QuietPseudoClass = ":quiet";

    /// <summary>Set while a mouse or a pen is over the button.</summary>
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SignInTextButton, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsQuietProperty =
        AvaloniaProperty.Register<SignInTextButton, bool>(nameof(IsQuiet));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    public bool IsQuiet { get => GetValue(IsQuietProperty); set => SetValue(IsQuietProperty, value); }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover rules sit in (hover: hover) and (pointer: fine): a mouse or a pen, never a finger.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<SignInUnderline>("PART_Underline") is { } underline)
        {
            underline.Target = e.NameScope.Find<TextBlock>("PART_Text");
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsQuietProperty)
        {
            PseudoClasses.Set(QuietPseudoClass, IsQuiet);
        }
        else if (change.Property == TextProperty && AutomationProperties.GetName(this) is null or "")
        {
            AutomationProperties.SetName(this, Text);
        }
    }
}

/// <summary>
/// Arc's <c>.resend</c>: a pill with the resend label. While the timer runs it is <see cref="IsWaiting"/> (Arc's
/// <c>aria-disabled</c>: muted, regular weight, no hover, presses do nothing) but stays focusable.
/// </summary>
[PseudoClasses(WaitingPseudoClass, HoverPseudoClass)]
internal sealed class SignInResendButton : AvaloniaButton
{
    public const string WaitingPseudoClass = ":waiting";

    /// <summary>Set while a mouse or a pen is over the button.</summary>
    public const string HoverPseudoClass = ":hover";

    public static readonly StyledProperty<bool> IsWaitingProperty =
        AvaloniaProperty.Register<SignInResendButton, bool>(nameof(IsWaiting));

    public bool IsWaiting { get => GetValue(IsWaitingProperty); set => SetValue(IsWaitingProperty, value); }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        // Arc's hover rules sit in (hover: hover) and (pointer: fine): a mouse or a pen, never a finger.
        PseudoClasses.Set(HoverPseudoClass, e.Pointer.Type != PointerType.Touch);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        PseudoClasses.Set(HoverPseudoClass, false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsWaitingProperty)
        {
            PseudoClasses.Set(WaitingPseudoClass, IsWaiting);
        }
    }
}
