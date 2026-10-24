// Ported from Arc's free sign-in block (MIT, Copyright (c) 2026 Elia Kuratli): the types and sample data of sign-in.tsx.
using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a <see cref="SignIn"/> card stands.</summary>
public enum SignInStep
{
    /// <summary>The email address and the other ways to sign in.</summary>
    Email,

    /// <summary>The six digit code sent to the address.</summary>
    Code,

    /// <summary>Signed in: the account and how it signed in.</summary>
    Done
}

/// <summary>How a person signed in (Arc's <c>SignInMethod</c>).</summary>
public enum SignInMethodKind
{
    /// <summary>A code sent by email.</summary>
    EmailCode,

    /// <summary>A passkey (WebAuthn on the web).</summary>
    Passkey,

    /// <summary>A single sign-on provider (<see cref="SignInMethod.Provider"/> names it).</summary>
    Provider
}

/// <summary>
/// How a person signed in: an email code, a passkey or a single sign-on provider (Arc's <c>"Email code" | "Passkey" |
/// Provider</c>). <see cref="ToString"/> gives Arc's text ("Email code", "Passkey" or the provider's name).
/// </summary>
/// <param name="Kind">The kind of method.</param>
/// <param name="Provider">The provider's name when <paramref name="Kind"/> is <see cref="SignInMethodKind.Provider"/>.</param>
public readonly record struct SignInMethod(SignInMethodKind Kind, string? Provider)
{
    /// <summary>A code sent by email.</summary>
    public static SignInMethod EmailCode => new(SignInMethodKind.EmailCode, null);

    /// <summary>A passkey.</summary>
    public static SignInMethod Passkey => new(SignInMethodKind.Passkey, null);

    /// <summary>A single sign-on provider by name ("Google").</summary>
    public static SignInMethod FromProvider(string provider) => new(SignInMethodKind.Provider, provider);

    /// <summary>Arc's name for the method: "Email code", "Passkey" or the provider's name.</summary>
    public override string ToString() => Kind switch
    {
        SignInMethodKind.EmailCode => "Email code",
        SignInMethodKind.Passkey => "Passkey",
        _ => Provider ?? string.Empty
    };
}

/// <summary>Which built-in mark a <see cref="SignInProvider"/> button shows.</summary>
public enum SignInProviderMark
{
    /// <summary>No built-in mark: <see cref="SignInProvider.Icon"/> when set, otherwise the name alone.</summary>
    None,

    /// <summary>Google's four colour "G".</summary>
    Google,

    /// <summary>Apple's mark, in the text colour.</summary>
    Apple,

    /// <summary>GitHub's mark, in the text colour.</summary>
    GitHub
}

/// <summary>
/// An account a <see cref="SignIn"/> card signs in to: a name, an address and an optional photo. Without a photo the card
/// shows initials.
/// </summary>
public class SignInAccount : AvaloniaObject
{
    /// <summary>The person's name ("Emma Collins"); the welcome uses its first word.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<SignInAccount, string>(nameof(Name), string.Empty);

    /// <summary>The email address.</summary>
    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<SignInAccount, string>(nameof(Email), string.Empty);

    /// <summary>A photo of the person, drawn in a circle (object-fit cover); null shows initials.</summary>
    public static readonly StyledProperty<IImage?> PhotoProperty =
        AvaloniaProperty.Register<SignInAccount, IImage?>(nameof(Photo));

    /// <summary>Creates an empty account.</summary>
    public SignInAccount()
    {
    }

    /// <summary>Creates an account with a name, an address and an optional photo.</summary>
    public SignInAccount(string name, string email, IImage? photo = null)
    {
        Name = name;
        Email = email;
        Photo = photo;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="PhotoProperty"/>
    public IImage? Photo { get => GetValue(PhotoProperty); set => SetValue(PhotoProperty, value); }

    /// <summary>The first word of <see cref="Name"/> ("Emma").</summary>
    public string FirstName
    {
        get
        {
            var name = Name.Trim();
            var space = name.IndexOf(' ');
            return space < 0 ? name : name[..space];
        }
    }

    /// <summary>
    /// The account for an address (Arc's <c>accountFor</c>): the part before the @ is split at dots, underscores, plus and
    /// minus signs; a known account whose first name matches the first piece keeps its name and photo (with this address);
    /// anyone else gets a name from the first two pieces ("jane.doe@acme.io" is "Jane Doe"), or <paramref name="fallbackName"/>.
    /// </summary>
    /// <param name="email">The address as typed; it is trimmed and lower-cased.</param>
    /// <param name="known">Known accounts (Arc's teammates), or null.</param>
    /// <param name="fallbackName">The name when the address gives none ("Arc member").</param>
    public static SignInAccount ForEmail(string email, IEnumerable<SignInAccount>? known, string fallbackName = "Arc member")
    {
        var address = (email ?? string.Empty).Trim().ToLowerInvariant();
        var at = address.IndexOf('@');
        var local = at < 0 ? address : address[..at];
        var parts = local.Split(s_separators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0 && known is not null)
        {
            foreach (var person in known)
            {
                var first = person.FirstName.ToLowerInvariant();
                if (first.Length > 0 && first == parts[0])
                {
                    return new SignInAccount(person.Name, address, person.Photo);
                }
            }
        }
        var name = string.Join(" ", parts.Take(2).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        return new SignInAccount(name.Length > 0 ? name : fallbackName, address);
    }

    private static readonly char[] s_separators = ['.', '_', '+', '-'];

    /// <summary>Arc's sample teammates (no photos: Arc's portraits are not part of its MIT registry).</summary>
    public static IReadOnlyList<SignInAccount> CreateSamples() =>
    [
        new("Emma Collins", "emma@northwind.studio"),
        new("Marcus Johnson", "marcus@northwind.studio"),
        new("Ava Mitchell", "ava@northwind.studio"),
        new("Jasmine Brooks", "jasmine@northwind.studio"),
        new("Sofia Ramirez", "sofia@northwind.studio")
    ];
}

/// <summary>A single sign-on button of a <see cref="SignIn"/> card ("Continue with Google").</summary>
public class SignInProvider : AvaloniaObject
{
    /// <summary>The provider's name: the button's text and the method shown once signed in ("Google").</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<SignInProvider, string>(nameof(Name), string.Empty);

    /// <summary>A built-in mark (Google, Apple, GitHub) or None.</summary>
    public static readonly StyledProperty<SignInProviderMark> MarkProperty =
        AvaloniaProperty.Register<SignInProvider, SignInProviderMark>(nameof(Mark));

    /// <summary>A mark of your own on a 24 by 24 grid, filled in the text colour (used when <see cref="Mark"/> is None).</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<SignInProvider, Geometry?>(nameof(Icon));

    /// <summary>Creates an empty provider.</summary>
    public SignInProvider()
    {
    }

    /// <summary>Creates a provider with a name and a built-in mark.</summary>
    public SignInProvider(string name, SignInProviderMark mark = SignInProviderMark.None)
    {
        Name = name;
        Mark = mark;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="MarkProperty"/>
    public SignInProviderMark Mark { get => GetValue(MarkProperty); set => SetValue(MarkProperty, value); }

    /// <inheritdoc cref="IconProperty"/>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Arc's three providers: Google, Apple and GitHub.</summary>
    public static IReadOnlyList<SignInProvider> CreateDefaults() =>
    [
        new("Google", SignInProviderMark.Google),
        new("Apple", SignInProviderMark.Apple),
        new("GitHub", SignInProviderMark.GitHub)
    ];
}

/// <summary>Arguments of <see cref="SignIn.SignedIn"/>: who signed in and how (Arc's <c>onSignIn(account, method)</c>).</summary>
public sealed class SignInSignedInEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SignInSignedInEventArgs(RoutedEvent routedEvent, SignInAccount account, SignInMethod method)
        : base(routedEvent)
    {
        Account = account;
        Method = method;
    }

    /// <summary>The account now signed in.</summary>
    public SignInAccount Account { get; }

    /// <summary>How it signed in.</summary>
    public SignInMethod Method { get; }
}

/// <summary>Arguments of <see cref="SignIn.SignedOut"/>.</summary>
public sealed class SignInSignedOutEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SignInSignedOutEventArgs(RoutedEvent routedEvent, SignInAccount account)
        : base(routedEvent)
    {
        Account = account;
    }

    /// <summary>The account that signed out.</summary>
    public SignInAccount Account { get; }
}
