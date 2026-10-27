namespace TKCode.Arc;

/// <summary>Why a piece of the invite field did not become a chip (or None when the last commit raised no error).</summary>
internal enum TeamMembersEmailError
{
    None,

    /// <summary>Not a complete address; it stays in the field.</summary>
    Invalid,

    /// <summary>A member already uses the address (the argument is the member's name).</summary>
    AlreadyMember,

    /// <summary>A pending invite already goes to the address.</summary>
    AlreadyInvited,
}

/// <summary>What the line under the invite field says about seats.</summary>
internal enum TeamMembersSeatNotice
{
    /// <summary>Enough seats: the hint shows.</summary>
    None,

    /// <summary>More chips than free seats (some seats are free).</summary>
    OverLimit,

    /// <summary>Every seat is taken and the field holds no chip.</summary>
    Full,

    /// <summary>Every seat is taken and the field holds chips.</summary>
    FullWithChips,
}

/// <summary>The outcome of committing text in the invite field.</summary>
internal sealed class TeamMembersCommitResult
{
    /// <summary>New chips, in order.</summary>
    public List<string> Added { get; } = new();

    /// <summary>What stays in the field: the invalid pieces joined by ", " (empty when every piece was taken or dropped).</summary>
    public string Draft { get; set; } = string.Empty;

    /// <summary>The error to show; invalid pieces win over members and invites.</summary>
    public TeamMembersEmailError Error { get; set; }

    /// <summary>The error's argument: the first invalid piece, the member's name or the invited address.</summary>
    public string? ErrorArgument { get; set; }
}

/// <summary>
/// The rules of the invite field (Arc Team members, design reference): separators, the address check, how each piece is
/// judged (invalid stays, duplicates drop silently, members and pending invites drop with an error), the send count and
/// the seat notice. Pure functions, tested without UI.
/// </summary>
internal static class TeamMembersEmailParser
{
    /// <summary>The characters that end an address while typing (Enter also commits).</summary>
    public static bool IsSeparator(char c) => c is ',' or ';' || char.IsWhiteSpace(c);

    /// <summary>Splits pasted or typed text at commas, semicolons, spaces and line breaks; empty pieces are dropped.</summary>
    public static List<string> Split(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(text))
        {
            return result;
        }
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var end = i == text.Length || IsSeparator(text[i]);
            if (end)
            {
                if (start >= 0)
                {
                    result.Add(text[start..i]);
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }
        return result;
    }

    /// <summary>True when the text contains a separator (a paste that should be split instead of inserted).</summary>
    public static bool HasSeparator(string? text) => text is not null && text.Any(IsSeparator);

    /// <summary>
    /// A complete address: exactly one @ with text on both sides, no white space, a dot in the domain with text on both
    /// sides of every dot, and a last part of at least two letters ("me@host", "a@b.c" and "bad@@x" fail).
    /// </summary>
    public static bool IsValid(string? email)
    {
        if (string.IsNullOrEmpty(email) || email.Any(char.IsWhiteSpace))
        {
            return false;
        }
        var at = email.IndexOf('@');
        if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
        {
            return false;
        }
        var domain = email[(at + 1)..];
        var parts = domain.Split('.');
        if (parts.Length < 2 || parts.Any(p => p.Length == 0))
        {
            return false;
        }
        return parts[^1].Length >= 2;
    }

    /// <summary>
    /// The letters of an invite's avatar: the first letter of each part of the address's name (split at . _ -), at most
    /// two, upper case ("owen.park@…" → "OP", "lena@…" → "L"); "?" when there is nothing to take.
    /// </summary>
    public static string InitialsOf(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "?";
        }
        var at = email.IndexOf('@');
        var local = at > 0 ? email[..at] : email;
        var letters = local.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p.FirstOrDefault(char.IsLetterOrDigit))
                           .Where(c => c != default)
                           .Take(2)
                           .Select(char.ToUpperInvariant)
                           .ToArray();
        return letters.Length == 0 ? "?" : new string(letters);
    }

    /// <summary>
    /// Judges each piece in order (case is ignored): invalid pieces stay in the field, a piece already among the chips
    /// (or added earlier in the same commit) is dropped silently, a member's address is dropped with the member's name, a
    /// pending invite's address is dropped with the address, anything else becomes a chip.
    /// </summary>
    /// <param name="pieces">The pieces to judge.</param>
    /// <param name="chips">The chips already in the field.</param>
    /// <param name="memberNameOf">The member's name for an address, or null when no member uses it.</param>
    /// <param name="isInvited">Whether a pending invite goes to an address.</param>
    /// <param name="validator">Replaces <see cref="IsValid"/> when set.</param>
    public static TeamMembersCommitResult Commit(IEnumerable<string> pieces, IReadOnlyCollection<string> chips,
        Func<string, string?> memberNameOf, Func<string, bool> isInvited, Func<string, bool>? validator = null)
    {
        var result = new TeamMembersCommitResult();
        var invalid = new List<string>();
        var seen = new HashSet<string>(chips, StringComparer.OrdinalIgnoreCase);
        string? memberError = null;
        var memberKind = TeamMembersEmailError.None;
        foreach (var raw in pieces)
        {
            var piece = raw.Trim();
            if (piece.Length == 0)
            {
                continue;
            }
            var valid = validator is not null ? validator(piece) : IsValid(piece);
            if (!valid)
            {
                invalid.Add(piece);
                continue;
            }
            if (seen.Contains(piece))
            {
                continue;
            }
            if (memberNameOf(piece) is { } name)
            {
                if (memberKind == TeamMembersEmailError.None)
                {
                    memberKind = TeamMembersEmailError.AlreadyMember;
                    memberError = name;
                }
                continue;
            }
            if (isInvited(piece))
            {
                if (memberKind == TeamMembersEmailError.None)
                {
                    memberKind = TeamMembersEmailError.AlreadyInvited;
                    memberError = piece;
                }
                continue;
            }
            seen.Add(piece);
            result.Added.Add(piece);
        }
        result.Draft = string.Join(", ", invalid);
        if (invalid.Count > 0)
        {
            result.Error = TeamMembersEmailError.Invalid;
            result.ErrorArgument = invalid[0];
        }
        else if (memberKind != TeamMembersEmailError.None)
        {
            result.Error = memberKind;
            result.ErrorArgument = memberError;
        }
        return result;
    }

    /// <summary>
    /// How many invites Send would send: the chips plus the draft when it is a valid address that is not a chip, a member
    /// or a pending invite.
    /// </summary>
    public static int SendCount(IReadOnlyCollection<string> chips, string? draft, Func<string, string?> memberNameOf,
        Func<string, bool> isInvited, Func<string, bool>? validator = null)
    {
        var count = chips.Count;
        var piece = draft?.Trim();
        if (string.IsNullOrEmpty(piece))
        {
            return count;
        }
        var valid = validator is not null ? validator(piece) : IsValid(piece);
        if (valid && !chips.Contains(piece, StringComparer.OrdinalIgnoreCase) && memberNameOf(piece) is null && !isInvited(piece))
        {
            count++;
        }
        return count;
    }

    /// <summary>What the seat line says: only the chips count (a valid draft does not raise the notice).</summary>
    public static TeamMembersSeatNotice SeatNotice(int remaining, int chipCount)
    {
        if (remaining <= 0)
        {
            return chipCount > 0 ? TeamMembersSeatNotice.FullWithChips : TeamMembersSeatNotice.Full;
        }
        return chipCount > remaining ? TeamMembersSeatNotice.OverLimit : TeamMembersSeatNotice.None;
    }

    /// <summary>Send is allowed when there is something to send and the chips fit in the free seats.</summary>
    public static bool CanSend(int sendCount, int remaining, int chipCount) =>
        sendCount > 0 && remaining > 0 && chipCount <= remaining;

    /// <summary>Case-insensitive substring match of the search on a name or an email (an empty search matches everything).</summary>
    public static bool Matches(string? query, params string?[] fields)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }
        var q = query.Trim();
        return fields.Any(f => f is not null && f.Contains(q, StringComparison.CurrentCultureIgnoreCase));
    }
}
