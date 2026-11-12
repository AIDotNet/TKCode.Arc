using System.Globalization;
using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The default contact schema, the sample file and the sample set of existing emails of <see cref="ImportWizard"/>. All
/// names, companies and addresses are invented: the domains use the reserved <c>.example</c> top-level domain and the phone
/// numbers come from ranges set aside for fiction (US 555-01xx, UK 020 7946 0xxx, Australia 0491 570 1xx) or use 555.
/// </summary>
public static class ImportWizardDefaults
{
    /// <summary>The plan names the default schema accepts.</summary>
    public static IReadOnlyList<string> Plans { get; } = ["Starter", "Team", "Scale"];

    /// <summary>Short country forms the default schema spells out, with their full names.</summary>
    public static IReadOnlyDictionary<string, string> CountryAbbreviations { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = "United States",
        ["U.S."] = "United States",
        ["USA"] = "United States",
        ["U.S.A."] = "United States",
        ["UK"] = "United Kingdom",
        ["U.K."] = "United Kingdom",
        ["GB"] = "United Kingdom",
        ["NL"] = "Netherlands",
        ["DE"] = "Germany",
        ["FR"] = "France",
        ["ES"] = "Spain",
        ["BR"] = "Brazil",
        ["JP"] = "Japan",
        ["AU"] = "Australia",
        ["CA"] = "Canada",
        ["SE"] = "Sweden",
        ["PT"] = "Portugal",
        ["MX"] = "Mexico",
    };

    /// <summary>A fresh copy of the default contact schema: name, email, company, role, phone, country, plan.</summary>
    public static IList<ImportTarget> CreateContactTargets() =>
    [
        new ImportTarget
        {
            Key = "name",
            Label = "Name",
            IsRequired = true,
            Headers = ["full name", "contact name", "contact"],
            Synonyms = ["person", "customer", "client", "member"],
            Validate = value => string.IsNullOrWhiteSpace(value) ? "Add a name" : null,
        },
        new ImportTarget
        {
            Key = "email",
            Label = "Email",
            IsRequired = true,
            IsKey = true,
            Headers = ["email address", "e-mail", "e-mail address", "mail"],
            Synonyms = ["login", "username", "contact email", "work email"],
            Validate = ValidateEmail,
            Fix = FixEmail,
            Sniff = LooksLikeEmail,
        },
        new ImportTarget
        {
            Key = "company",
            Label = "Company",
            Headers = ["company name"],
            Synonyms = ["organization", "organisation", "org", "employer", "account", "business"],
        },
        new ImportTarget
        {
            Key = "role",
            Label = "Role",
            Headers = ["job title"],
            Synonyms = ["title", "position", "job", "function"],
        },
        new ImportTarget
        {
            Key = "phone",
            Label = "Phone",
            Headers = ["phone number", "telephone"],
            Synonyms = ["mobile", "cell", "tel", "work phone"],
            Validate = value => string.IsNullOrWhiteSpace(value) || LooksLikePhone(value) ? null : "Expected a phone number",
            Sniff = LooksLikePhone,
        },
        new ImportTarget
        {
            Key = "country",
            Label = "Country",
            Headers = ["country name"],
            Synonyms = ["nation", "country code", "region"],
            Validate = value => CountryAbbreviations.ContainsKey(value.Trim()) ? "Spell out the country" : null,
            Fix = value => CountryAbbreviations.TryGetValue(value.Trim(), out var full) ? full : null,
        },
        new ImportTarget
        {
            Key = "plan",
            Label = "Plan",
            Synonyms = ["subscription", "tier", "package", "plan tier"],
            Validate = ValidatePlan,
            Fix = FixPlan,
        },
    ];

    /// <summary>A fresh copy of the emails the sample treats as existing records (four rows of the sample file update them).</summary>
    public static ISet<string> CreateSampleExistingKeys() => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "lucas@ferro.example",
        "leila@bramble.example",
        "arjun@halcyon.example",
        "yuki.mori@halcyon.example",
    };

    /// <summary>The sample file's name.</summary>
    public const string SampleFileName = "customers-q3.csv";

    /// <summary>
    /// The sample file: 22 customers in 8 columns. Nine rows have one problem each (two malformed emails, two emails
    /// with spaces or capitals, a missing name, two short country forms, a note in the phone column, a plan in lower
    /// case); five of them have a safe fix. Four clean rows match <see cref="CreateSampleExistingKeys"/>.
    /// </summary>
    public static string SampleCsv { get; } = BuildSample();

    private static string BuildSample()
    {
        string[] lines =
        [
            "Contact name,Email,Organization,Team role,Cell,Country,Subscription,Comments",
            "Amara Osei,amara@larkspur.example,Larkspur,Ops manager,+1 206 555 0142,United States,Team,Renewal in Q1",
            "Kwame Asante,kwame.asante@@larkspur.example,Larkspur,Data engineer,+1 206 555 0177,United States,Scale,",
            "Lucas Ferreira,lucas@ferro.example,Ferro,Account executive,+55 11 5555 0123,Brazil,Team,Wants SSO",
            "Saskia Lindqvist,saskia@tidepool.example,Tidepool,Product lead,+46 8 555 012 34,Sweden,Scale,",
            "Rafael Ortiz,\"  Rafael.Ortiz@Kiln.Example \",Kiln,Solutions architect,+34 91 555 0187,Spain,Team,Asked for a demo",
            "Leila Haddad,leila@bramble.example,Bramble,Research lead,+44 20 7946 0321,United Kingdom,Starter,",
            "Noah Bergmann,noah@ferro.example,Ferro,Finance analyst,+49 30 5550 1234,Germany,Team,",
            ",chen.wei@quarrylight.example,Quarrylight,QA engineer,+61 491 570 156,Australia,Starter,From the events list",
            "Arjun Mehta,arjun@halcyon.example,Halcyon,CTO,+1 312 555 0110,United States,Scale,Met at the summit",
            "Hannah Weiss,hannah@tidepool.example,Tidepool,Recruiter,+1 415 555 0166,U.S.,Team,",
            "Tomás Novak,tomas@saltmarsh.example,Saltmarsh,Brand designer,+420 2 5555 0199,Czechia,Starter,",
            "Freya Holm,freya@bramble.example,Bramble,Customer success,ask reception,United Kingdom,Team,Prefers email",
            "Diego Alvarez,diego@kiln.example,Kiln,Growth analyst,+52 55 5555 0144,Mexico,Starter,",
            "Yuki Mori,yuki.mori@halcyon.example,Halcyon,Infra engineer,+81 3 5555 0107,Japan,Scale,",
            "Mei Tanaka,MEI.TANAKA@BRAMBLE.EXAMPLE,Bramble,Data engineer,+81 6 5555 0133,Japan,Team,",
            "Ines Duarte,ines.duarte@saltmarsh,Saltmarsh,Ops manager,+351 21 555 0170,Portugal,Starter,Follow up in May",
            "Oskar Lund,oskar@tidepool.example,Tidepool,Support lead,+46 31 555 0155,Sweden,Team,",
            "Grace Mensah,grace@larkspur.example,Larkspur,Account executive,+233 30 555 0191,Ghana,Team,",
            "Pieter de Wit,pieter@quarrylight.example,Quarrylight,Product lead,+31 20 555 0138,NL,Scale,",
            "Nadia Rahman,nadia@ferro.example,Ferro,UX writer,+44 20 7946 0512,United Kingdom,Starter,Trial ends soon",
            "Elif Kaya,elif@halcyon.example,Halcyon,Research lead,+90 212 555 0126,Türkiye,Team,",
            "Samuel Okafor,samuel@saltmarsh.example,Saltmarsh,Finance analyst,+234 1 555 0148,Nigeria,\"team \",",
        ];
        return string.Join("\n", lines) + "\n";
    }

    // ---- Rules of the default schema ----

    private static string? ValidateEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Add an email";
        }
        var clean = value.Trim().ToLowerInvariant();
        if (!LooksLikeEmail(clean))
        {
            return "Check the email format";
        }
        return value == clean ? null : "Contains spaces or capitals";
    }

    private static string? FixEmail(string value)
    {
        var clean = value.Trim().ToLowerInvariant();
        return LooksLikeEmail(clean) && clean != value ? clean : null;
    }

    private static string? ValidatePlan(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        foreach (var plan in Plans)
        {
            if (value == plan)
            {
                return null;
            }
        }
        return FixPlan(value) is null ? "Unknown plan" : "Use the plan's exact name";
    }

    private static string? FixPlan(string value)
    {
        var trimmed = value.Trim();
        foreach (var plan in Plans)
        {
            if (string.Equals(trimmed, plan, StringComparison.OrdinalIgnoreCase))
            {
                return plan;
            }
        }
        return null;
    }

    /// <summary>True for <c>local@domain.tld</c> with one @, no spaces and a dot in the domain that is not at its ends.</summary>
    internal static bool LooksLikeEmail(string value)
    {
        var text = value.Trim();
        var at = text.IndexOf('@');
        if (at <= 0 || at != text.LastIndexOf('@') || at == text.Length - 1)
        {
            return false;
        }
        foreach (var ch in text)
        {
            if (char.IsWhiteSpace(ch))
            {
                return false;
            }
        }
        var domain = text[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        return dot > 0 && dot < domain.Length - 2 && !domain.StartsWith('.') && !domain.Contains("..", StringComparison.Ordinal);
    }

    /// <summary>True for 7–15 digits with only spaces, dots, hyphens, parentheses and one leading plus around them.</summary>
    internal static bool LooksLikePhone(string value)
    {
        var text = value.Trim();
        if (text.Length == 0)
        {
            return false;
        }
        var digits = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (char.IsAsciiDigit(ch))
            {
                digits++;
            }
            else if (ch == '+' ? i != 0 : ch is not (' ' or '-' or '.' or '(' or ')'))
            {
                return false;
            }
        }
        return digits is >= 7 and <= 15;
    }

    /// <summary>The UTF-8 size of a text in bytes (the sample's file size).</summary>
    internal static long ByteSize(string text) => Encoding.UTF8.GetByteCount(text);

    internal static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
