using Avalonia;
using Avalonia.Controls;

namespace TKCode.Arc;

/// <summary>The sign in and sign up layouts <see cref="Login"/> can show.</summary>
public enum LoginVariant
{
    /// <summary>A compact card that morphs from an email field to a six digit code to your account (<see cref="SignIn"/>).</summary>
    EmailCode,

    /// <summary>A centered passkey first layout over a ring backdrop (<see cref="LoginCentered"/>).</summary>
    Centered,

    /// <summary>A full screen layout with the form beside a picture (<see cref="LoginSplit"/>).</summary>
    Split,

    /// <summary>A full screen picture with the form on a glass card (<see cref="LoginImmersive"/>).</summary>
    Immersive,

    /// <summary>A create account form with a password strength meter (<see cref="SignupForm"/>).</summary>
    SignUp,
}

/// <summary>
/// The Login block as one control: it shows one of the five sign in and sign up layouts, chosen by
/// <see cref="Variant"/>. Each layout is also a control of its own (<see cref="SignIn"/>, <see cref="LoginCentered"/>,
/// <see cref="LoginSplit"/>, <see cref="LoginImmersive"/>, <see cref="SignupForm"/>) with its own properties and events;
/// <see cref="Form"/> is the one being shown, so a host that switches layouts at run time configures it after each switch.
/// </summary>
/// <remarks>
/// A thin host with no template or token of its own: every property that matters lives on the layout control. Use the
/// layout control directly when the layout never changes.
/// </remarks>
public class Login : Decorator
{
    /// <summary>Defines the <see cref="Variant"/> property.</summary>
    public static readonly StyledProperty<LoginVariant> VariantProperty =
        AvaloniaProperty.Register<Login, LoginVariant>(nameof(Variant), LoginVariant.EmailCode);

    /// <summary>Defines the <see cref="Form"/> property.</summary>
    public static readonly DirectProperty<Login, Control?> FormProperty =
        AvaloniaProperty.RegisterDirect<Login, Control?>(nameof(Form), o => o.Form);

    private Control? _form;

    /// <summary>Gets or sets the layout to show. Changing it replaces <see cref="Form"/> with a new control of that layout.</summary>
    public LoginVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Gets the layout control being shown: a <see cref="SignIn"/>, <see cref="LoginCentered"/>, <see cref="LoginSplit"/>, <see cref="LoginImmersive"/> or <see cref="SignupForm"/>.</summary>
    public Control? Form => _form;

    /// <summary>Raised when <see cref="Variant"/> changed and <see cref="Form"/> is a new control; configure it here.</summary>
    public event EventHandler<LoginFormChangedEventArgs>? FormChanged;

    static Login()
    {
        VariantProperty.Changed.AddClassHandler<Login>((login, _) => login.Rebuild());
    }

    /// <summary>Creates a login host showing the <see cref="LoginVariant.EmailCode"/> layout.</summary>
    public Login()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        Control form = Variant switch
        {
            LoginVariant.Centered => new LoginCentered(),
            LoginVariant.Split => new LoginSplit(),
            LoginVariant.Immersive => new LoginImmersive(),
            LoginVariant.SignUp => new SignupForm(),
            _ => new SignIn(),
        };
        var old = _form;
        _form = form;
        Child = form;
        RaisePropertyChanged(FormProperty, old, form);
        FormChanged?.Invoke(this, new LoginFormChangedEventArgs(form));
    }
}

/// <summary>Data for <see cref="Login.FormChanged"/>.</summary>
public sealed class LoginFormChangedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    public LoginFormChangedEventArgs(Control form)
    {
        Form = form;
    }

    /// <summary>The new layout control.</summary>
    public Control Form { get; }
}
