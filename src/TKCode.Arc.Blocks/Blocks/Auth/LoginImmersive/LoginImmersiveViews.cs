using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Base of the three step views: a compact flag and a notice when the template's parts are ready.</summary>
[PseudoClasses(CompactPseudoClass)]
internal abstract class LoginImmersiveView : TemplatedControl
{
    public const string CompactPseudoClass = ":compact";

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<LoginImmersiveView, bool>(nameof(IsCompact));

    /// <summary>The card's inside is narrower than 380: tighter padding, smaller heading and provider text.</summary>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    /// <summary>Raised after the template is applied and its parts are found.</summary>
    public event EventHandler? PartsReady;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        FindParts(e.NameScope);
        PartsReady?.Invoke(this, EventArgs.Empty);
    }

    protected abstract void FindParts(INameScope scope);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsCompactProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, IsCompact);
        }
    }
}

/// <summary>The Form step: the heading (one per mode, swapped in place), the email field, the main button, "or" and the single sign-on buttons.</summary>
[TemplatePart("PART_HeadingHost", typeof(LoginImmersiveStepHost))]
[TemplatePart("PART_SignInHeading", typeof(LoginImmersiveHeading))]
[TemplatePart("PART_SignUpHeading", typeof(LoginImmersiveHeading))]
[TemplatePart("PART_Email", typeof(Input))]
[TemplatePart("PART_Send", typeof(Button))]
[TemplatePart("PART_Divider", typeof(Control))]
[TemplatePart("PART_Providers", typeof(LoginImmersiveEqualRow))]
internal sealed class LoginImmersiveFormView : LoginImmersiveView
{
    public static readonly StyledProperty<string?> DividerTextProperty =
        AvaloniaProperty.Register<LoginImmersiveFormView, string?>(nameof(DividerText), "or");

    public static readonly StyledProperty<bool> AreProvidersVisibleProperty =
        AvaloniaProperty.Register<LoginImmersiveFormView, bool>(nameof(AreProvidersVisible), true);

    public string? DividerText { get => GetValue(DividerTextProperty); set => SetValue(DividerTextProperty, value); }

    public bool AreProvidersVisible { get => GetValue(AreProvidersVisibleProperty); set => SetValue(AreProvidersVisibleProperty, value); }

    public LoginImmersiveStepHost? HeadingHost { get; private set; }

    public LoginImmersiveHeading? SignInHeading { get; private set; }

    public LoginImmersiveHeading? SignUpHeading { get; private set; }

    public Input? Email { get; private set; }

    public Button? Send { get; private set; }

    public LoginImmersiveEqualRow? Providers { get; private set; }

    protected override void FindParts(INameScope scope)
    {
        HeadingHost = scope.Find<LoginImmersiveStepHost>("PART_HeadingHost");
        SignInHeading = scope.Find<LoginImmersiveHeading>("PART_SignInHeading");
        SignUpHeading = scope.Find<LoginImmersiveHeading>("PART_SignUpHeading");
        Email = scope.Find<Input>("PART_Email");
        Send = scope.Find<Button>("PART_Send");
        Providers = scope.Find<LoginImmersiveEqualRow>("PART_Providers");
    }
}

/// <summary>The Sent step: the envelope, the heading, the address with Edit, Open mail app and the resend line.</summary>
[TemplatePart("PART_Envelope", typeof(LoginImmersiveEnvelope))]
[TemplatePart("PART_Heading", typeof(LoginImmersiveHeading))]
[TemplatePart("PART_Edit", typeof(LoginImmersiveTextButton))]
[TemplatePart("PART_OpenMail", typeof(Button))]
[TemplatePart("PART_Resend", typeof(LoginImmersiveTextButton))]
[TemplatePart("PART_ResendLabel", typeof(SiteSwap))]
internal sealed class LoginImmersiveSentView : LoginImmersiveView
{
    public static readonly StyledProperty<string?> AddressProperty =
        AvaloniaProperty.Register<LoginImmersiveSentView, string?>(nameof(Address));

    public static readonly StyledProperty<string?> EditTextProperty =
        AvaloniaProperty.Register<LoginImmersiveSentView, string?>(nameof(EditText));

    public static readonly StyledProperty<string?> ResendPromptProperty =
        AvaloniaProperty.Register<LoginImmersiveSentView, string?>(nameof(ResendPrompt));

    public string? Address { get => GetValue(AddressProperty); set => SetValue(AddressProperty, value); }

    public string? EditText { get => GetValue(EditTextProperty); set => SetValue(EditTextProperty, value); }

    public string? ResendPrompt { get => GetValue(ResendPromptProperty); set => SetValue(ResendPromptProperty, value); }

    public LoginImmersiveEnvelope? Envelope { get; private set; }

    public LoginImmersiveHeading? Heading { get; private set; }

    public LoginImmersiveTextButton? Edit { get; private set; }

    public Button? OpenMail { get; private set; }

    public LoginImmersiveTextButton? Resend { get; private set; }

    public SiteSwap? ResendLabel { get; private set; }

    protected override void FindParts(INameScope scope)
    {
        Envelope = scope.Find<LoginImmersiveEnvelope>("PART_Envelope");
        Heading = scope.Find<LoginImmersiveHeading>("PART_Heading");
        Edit = scope.Find<LoginImmersiveTextButton>("PART_Edit");
        OpenMail = scope.Find<Button>("PART_OpenMail");
        Resend = scope.Find<LoginImmersiveTextButton>("PART_Resend");
        ResendLabel = scope.Find<SiteSwap>("PART_ResendLabel");
    }
}

/// <summary>The Done step: the success mark, the heading, the two detail rows and Sign out.</summary>
[TemplatePart("PART_Mark", typeof(LoginImmersiveDoneMark))]
[TemplatePart("PART_Heading", typeof(LoginImmersiveHeading))]
[TemplatePart("PART_SignOut", typeof(Button))]
internal sealed class LoginImmersiveDoneView : LoginImmersiveView
{
    public static readonly StyledProperty<string?> AccountLabelProperty =
        AvaloniaProperty.Register<LoginImmersiveDoneView, string?>(nameof(AccountLabel));

    public static readonly StyledProperty<string?> AccountProperty =
        AvaloniaProperty.Register<LoginImmersiveDoneView, string?>(nameof(Account));

    public static readonly StyledProperty<string?> MethodLabelProperty =
        AvaloniaProperty.Register<LoginImmersiveDoneView, string?>(nameof(MethodLabel));

    public static readonly StyledProperty<string?> MethodProperty =
        AvaloniaProperty.Register<LoginImmersiveDoneView, string?>(nameof(Method));

    public string? AccountLabel { get => GetValue(AccountLabelProperty); set => SetValue(AccountLabelProperty, value); }

    public string? Account { get => GetValue(AccountProperty); set => SetValue(AccountProperty, value); }

    public string? MethodLabel { get => GetValue(MethodLabelProperty); set => SetValue(MethodLabelProperty, value); }

    public string? Method { get => GetValue(MethodProperty); set => SetValue(MethodProperty, value); }

    public LoginImmersiveDoneMark? Mark { get; private set; }

    public LoginImmersiveHeading? Heading { get; private set; }

    public Button? SignOut { get; private set; }

    protected override void FindParts(INameScope scope)
    {
        Mark = scope.Find<LoginImmersiveDoneMark>("PART_Mark");
        Heading = scope.Find<LoginImmersiveHeading>("PART_Heading");
        SignOut = scope.Find<Button>("PART_SignOut");
    }
}
