namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>The account signed in to the Northstar demo.</summary>
public sealed record DemoAccount(string Name, string Email)
{
    /// <summary>
    /// The first name the overview greets: the first word of the name, unless the name is one word that only repeats the
    /// email's local part (then there is no name to greet with).
    /// </summary>
    public string FirstName
    {
        get
        {
            var name = Name.Trim();
            var first = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            var local = Letters(Email.Split('@')[0]);
            return first.Length > 0 && (name.Contains(' ') || Letters(first) != local) ? first : string.Empty;
        }
    }

    private static string Letters(string text) => new(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}

/// <summary>
/// Who is signed in to the demo. uiarc.dev keeps this in the browser's storage and sends a first visit to the sign-in page;
/// here the gallery's "Live demo" button opens the overview, so a visit starts signed in as Emma and signing out leads to
/// the sign-in page. The session lives as long as the app.
/// </summary>
public static class DemoSession
{
    public const string DemoEmail = "emma@northstar.studio";
    public const string DemoPassword = "northstar";

    public static DemoAccount Emma { get; } = new("Emma Collins", DemoEmail);

    /// <summary>The signed-in account, or null after signing out.</summary>
    public static DemoAccount? Account { get; private set; } = Emma;

    public static bool IsSignedIn => Account is not null;

    /// <summary>Raised when someone signs in or out.</summary>
    public static event Action? Changed;

    public static void SignIn(DemoAccount account)
    {
        // The sign-in block's fallback account is on northwind.studio; the workspace is Northstar's.
        Account = account with { Email = account.Email.Replace("@northwind.studio", "@northstar.studio", StringComparison.OrdinalIgnoreCase) };
        Changed?.Invoke();
    }

    public static void SignOut()
    {
        Account = null;
        Changed?.Invoke();
    }
}

/// <summary>
/// Where a search result or a menu item leads inside a page: one of its views (<c>projects/board</c>) or a section, and
/// for a project, which one. The page reads it when it opens (uiarc.dev's intent bus).
/// </summary>
public sealed record DemoIntent(string Page, string? View = null, string? Project = null);
