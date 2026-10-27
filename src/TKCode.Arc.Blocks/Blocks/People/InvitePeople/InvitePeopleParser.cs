using System.Text;

namespace TKCode.Arc;

/// <summary>
/// The people <see cref="InvitePeople"/> checks entries against: the directory, the team and the pending invites. Emails
/// compare case-insensitively; full names compare case-insensitively with runs of white space folded to one space.
/// </summary>
internal sealed class InvitePeopleRoster
{
    private readonly Dictionary<string, InvitePeoplePerson> _byEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, InvitePeoplePerson> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _members = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _invited = new(StringComparer.OrdinalIgnoreCase);

    public InvitePeopleRoster(IEnumerable<InvitePeoplePerson> directory, IEnumerable<InvitePeoplePerson> members,
                              IEnumerable<InvitePeoplePerson?> invited)
    {
        var list = new List<InvitePeoplePerson>();
        foreach (var person in directory)
        {
            list.Add(person);
            if (!string.IsNullOrWhiteSpace(person.Email))
            {
                _byEmail.TryAdd(person.Email.Trim(), person);
            }
            if (!string.IsNullOrWhiteSpace(person.Name))
            {
                _byName.TryAdd(FoldSpaces(person.Name), person);
            }
        }
        Directory = list;
        foreach (var person in members)
        {
            if (!string.IsNullOrWhiteSpace(person.Email))
            {
                _members.Add(person.Email.Trim());
            }
        }
        foreach (var person in invited)
        {
            if (!string.IsNullOrWhiteSpace(person?.Email))
            {
                _invited.Add(person.Email.Trim());
            }
        }
    }

    /// <summary>The directory in its own order.</summary>
    public IReadOnlyList<InvitePeoplePerson> Directory { get; }

    public InvitePeoplePerson? FindByEmail(string email) => _byEmail.TryGetValue(email.Trim(), out var person) ? person : null;

    public InvitePeoplePerson? FindByName(string name) => _byName.TryGetValue(FoldSpaces(name), out var person) ? person : null;

    /// <summary>Why this address cannot be invited (on the team, invited already), or <see cref="InvitePeopleEntryError.None"/>.</summary>
    public InvitePeopleEntryError BlockOf(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return InvitePeopleEntryError.None;
        }
        if (_members.Contains(email.Trim()))
        {
            return InvitePeopleEntryError.AlreadyMember;
        }
        return _invited.Contains(email.Trim()) ? InvitePeopleEntryError.AlreadyInvited : InvitePeopleEntryError.None;
    }

    internal static string FoldSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }
            if (space && builder.Length > 0)
            {
                builder.Append(' ');
            }
            space = false;
            builder.Append(c);
        }
        return builder.ToString();
    }
}

/// <summary>One recognized entry: the person (valid, or blocked as a member / invitee), the text and the error.</summary>
internal readonly record struct InvitePeopleParsedEntry(InvitePeoplePerson? Person, string Text, InvitePeopleEntryError Error)
{
    public bool IsValid => Error == InvitePeopleEntryError.None;

    /// <summary>What makes two entries the same: the address for people, the folded text for anything else.</summary>
    public string Key => Person?.Email is { Length: > 0 } email && Error is InvitePeopleEntryError.None
        or InvitePeopleEntryError.AlreadyMember or InvitePeopleEntryError.AlreadyInvited
        ? email.Trim().ToLowerInvariant()
        : "!" + InvitePeopleRoster.FoldSpaces(Text).ToLowerInvariant();
}

/// <summary>The result of parsing a list: entries in order, without duplicates, and how many duplicates were skipped.</summary>
internal sealed record InvitePeopleParseResult(IReadOnlyList<InvitePeopleParsedEntry> Entries, int Duplicates)
{
    public int ValidCount => Entries.Count(e => e.IsValid);

    public int InvalidCount => Entries.Count - ValidCount;
}

/// <summary>
/// Turns typed or pasted text into chips. A list is split at commas, semicolons and line breaks (tabs and spaces are not
/// separators); each item is a directory person's full name or email, a new address, or "Name &lt;address&gt;" (only the
/// address counts). Anything else becomes an entry with an <see cref="InvitePeopleEntryError"/>.
/// </summary>
internal static class InvitePeopleParser
{
    private static readonly char[] s_separators = [',', ';', '\n', '\r'];

    /// <summary>True when <paramref name="text"/> holds a separator (a paste that should become several chips).</summary>
    public static bool HasSeparator(string? text) => text is not null && text.IndexOfAny(s_separators) >= 0;

    /// <summary>The non-empty items of a list, trimmed, in order.</summary>
    public static IReadOnlyList<string> Split(string? text)
    {
        var items = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return items;
        }
        foreach (var part in text.Split(s_separators))
        {
            var item = part.Trim();
            if (item.Length > 0)
            {
                items.Add(item);
            }
        }
        return items;
    }

    /// <summary>
    /// Checks the shape of an address: <c>local@domain.tld</c> with one @, no white space, a dotted domain without empty
    /// labels and a top-level domain of at least two letters.
    /// </summary>
    public static InvitePeopleEntryError ValidateEmail(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        var at = value.IndexOf('@');
        if (at < 0)
        {
            return InvitePeopleEntryError.NotEmail;
        }
        if (at == 0)
        {
            return InvitePeopleEntryError.MissingLocal;
        }
        if (at == value.Length - 1)
        {
            return InvitePeopleEntryError.MissingDomain;
        }
        if (value.IndexOf('@', at + 1) >= 0 || value.Any(char.IsWhiteSpace) || value.IndexOfAny(['<', '>', '(', ')', '"', ',', ';']) >= 0 ||
            value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return InvitePeopleEntryError.Invalid;
        }
        var domain = value[(at + 1)..];
        if (domain.IndexOf('.') < 0)
        {
            return InvitePeopleEntryError.IncompleteDomain;
        }
        var labels = domain.Split('.');
        if (labels.Any(l => l.Length == 0))
        {
            return InvitePeopleEntryError.Invalid;
        }
        var tld = labels[^1];
        if (tld.Length < 2 || !tld.All(char.IsLetter))
        {
            return InvitePeopleEntryError.Invalid;
        }
        return InvitePeopleEntryError.None;
    }

    /// <summary>True when <paramref name="text"/> is a well-formed address.</summary>
    public static bool IsEmail(string? text) => ValidateEmail(text) == InvitePeopleEntryError.None;

    /// <summary>Recognizes one item (already split off a list).</summary>
    public static InvitePeopleParsedEntry Recognize(string item, InvitePeopleRoster roster)
    {
        var text = item.Trim();
        // "Name <address>": only the part in angle brackets counts; the name is dropped.
        var open = text.LastIndexOf('<');
        if (open >= 0 && text.EndsWith('>') && open < text.Length - 1)
        {
            var inner = text[(open + 1)..^1].Trim();
            if (inner.Length > 0)
            {
                return RecognizeAddressOrName(inner, roster, allowName: false);
            }
        }
        return RecognizeAddressOrName(text, roster, allowName: true);
    }

    private static InvitePeopleParsedEntry RecognizeAddressOrName(string text, InvitePeopleRoster roster, bool allowName)
    {
        var person = (allowName ? roster.FindByName(text) : null) ?? roster.FindByEmail(text);
        if (person is not null)
        {
            return new InvitePeopleParsedEntry(person, person.DisplayName, roster.BlockOf(person.Email));
        }
        var error = ValidateEmail(text);
        if (error != InvitePeopleEntryError.None)
        {
            return new InvitePeopleParsedEntry(null, text, error);
        }
        var block = roster.BlockOf(text);
        var created = new InvitePeoplePerson(null, text);
        return new InvitePeopleParsedEntry(created, text, block);
    }

    /// <summary>
    /// Parses a list. Items that repeat an earlier item of the list or one of <paramref name="existingKeys"/> (the chips
    /// already in the field) are skipped and counted.
    /// </summary>
    public static InvitePeopleParseResult Parse(string? text, InvitePeopleRoster roster, IEnumerable<string>? existingKeys = null)
    {
        var seen = new HashSet<string>(existingKeys ?? [], StringComparer.Ordinal);
        var entries = new List<InvitePeopleParsedEntry>();
        var duplicates = 0;
        foreach (var item in Split(text))
        {
            var entry = Recognize(item, roster);
            if (!seen.Add(entry.Key))
            {
                duplicates++;
                continue;
            }
            entries.Add(entry);
        }
        return new InvitePeopleParseResult(entries, duplicates);
    }
}

/// <summary>What a suggestion row offers.</summary>
internal enum InvitePeopleSuggestionKind
{
    /// <summary>A directory person (maybe blocked: on the team or invited already).</summary>
    Person,

    /// <summary>"Invite by email" for a typed address that is not in the directory.</summary>
    Email
}

/// <summary>A row of the suggestion list.</summary>
internal sealed record InvitePeopleSuggestion(InvitePeopleSuggestionKind Kind, InvitePeoplePerson? Person, string? Email,
                                              InvitePeopleEntryError Block)
{
    public bool IsBlocked => Block != InvitePeopleEntryError.None;
}

/// <summary>The suggestion list for a query: its rows and whether it is the "Suggested" list of an empty field.</summary>
internal sealed record InvitePeopleSuggestionList(IReadOnlyList<InvitePeopleSuggestion> Rows, bool IsSuggested)
{
    public static readonly InvitePeopleSuggestionList Empty = new([], false);
}

/// <summary>
/// The combobox's filter: the query (as typed, not trimmed) matches a directory person's name or email as a
/// case-insensitive substring (the title is not searched). People who can be invited come first, blocked people after
/// them, then the list is cut to <c>max</c>. An empty query lists the first <c>emptyMax</c> people who can be invited.
/// A query that is a well-formed address not in the directory offers one "Invite by email" row. People already in the
/// field never show.
/// </summary>
internal static class InvitePeopleSuggestions
{
    public static InvitePeopleSuggestionList Build(string? query, InvitePeopleRoster roster, ISet<string> selectedEmails,
                                                   int max, int emptyMax)
    {
        query ??= string.Empty;
        var rows = new List<InvitePeopleSuggestion>();
        if (query.Length == 0)
        {
            foreach (var person in roster.Directory)
            {
                if (rows.Count >= emptyMax)
                {
                    break;
                }
                if (IsSelected(person, selectedEmails) || roster.BlockOf(person.Email) != InvitePeopleEntryError.None)
                {
                    continue;
                }
                rows.Add(new InvitePeopleSuggestion(InvitePeopleSuggestionKind.Person, person, person.Email, InvitePeopleEntryError.None));
            }
            return new InvitePeopleSuggestionList(rows, true);
        }

        if (InvitePeopleParser.IsEmail(query) && roster.FindByEmail(query) is null)
        {
            var email = query.Trim();
            if (!selectedEmails.Contains(email))
            {
                rows.Add(new InvitePeopleSuggestion(InvitePeopleSuggestionKind.Email, null, email, roster.BlockOf(email)));
            }
            return new InvitePeopleSuggestionList(rows, false);
        }

        var blocked = new List<InvitePeopleSuggestion>();
        foreach (var person in roster.Directory)
        {
            if (IsSelected(person, selectedEmails) || !Matches(person, query))
            {
                continue;
            }
            var block = roster.BlockOf(person.Email);
            var row = new InvitePeopleSuggestion(InvitePeopleSuggestionKind.Person, person, person.Email, block);
            (block == InvitePeopleEntryError.None ? rows : blocked).Add(row);
        }
        rows.AddRange(blocked);
        if (rows.Count > max)
        {
            rows.RemoveRange(max, rows.Count - max);
        }
        return new InvitePeopleSuggestionList(rows, false);
    }

    private static bool IsSelected(InvitePeoplePerson person, ISet<string> selected) =>
        !string.IsNullOrWhiteSpace(person.Email) && selected.Contains(person.Email.Trim());

    private static bool Matches(InvitePeoplePerson person, string query) =>
        (person.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
        (person.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false);
}

/// <summary>
/// Seats: members + pending invites + new (valid chips) against the plan's limit. The meter's three segments are
/// cumulative from the left (members, members + pending, everything), so they overlap rather than sit side by side.
/// </summary>
internal readonly record struct InvitePeopleSeats(int Members, int Pending, int New, int Limit)
{
    public int Total => Members + Pending + New;

    public int Over => Math.Max(0, Total - Limit);

    public bool IsOver => Total > Limit;

    public double MembersFraction => Fraction(Members);

    public double PendingFraction => Fraction(Members + Pending);

    public double NewFraction => Fraction(Total);

    private double Fraction(int seats) => Limit <= 0 ? (seats > 0 ? 1 : 0) : Math.Clamp((double)seats / Limit, 0, 1);
}
