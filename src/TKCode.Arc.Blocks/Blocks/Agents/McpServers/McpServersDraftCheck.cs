namespace TKCode.Arc;

/// <summary>The problems the Add server form found, one per field; all null when the draft is valid.</summary>
internal sealed record McpServersDraftErrors(string? Name, string? Url, string? Command, string? Variables)
{
    public bool IsValid => Name is null && Url is null && Command is null && Variables is null;
}

/// <summary>Checks the Add server form, parses its variables and makes ids (pure functions).</summary>
internal static class McpServersDraftCheck
{
    /// <summary>
    /// Checks a form: a name that is not empty and not already used (ignoring case), a full http or https address for
    /// HTTP, a program for a command, and one <c>KEY=value</c> per non-empty variables line.
    /// </summary>
    public static McpServersDraftErrors Validate(string? name, McpTransport transport, string? url, string? command, string? variables,
        IEnumerable<McpServer> existing, McpServersLabels labels)
    {
        string? nameError = null;
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            nameError = labels.NameRequired;
        }
        else if (existing.Any(s => string.Equals(s.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            nameError = labels.NameTaken(trimmed);
        }

        string? urlError = null;
        string? commandError = null;
        if (transport == McpTransport.Http)
        {
            var address = url?.Trim() ?? string.Empty;
            if (address.Length == 0)
            {
                urlError = labels.UrlRequired;
            }
            else if (!IsHttpAddress(address))
            {
                urlError = labels.UrlInvalid;
            }
        }
        else if (string.IsNullOrWhiteSpace(command))
        {
            commandError = labels.CommandRequired;
        }

        string? variablesError = null;
        if (!TryParseVariables(variables, out _, out var badLine))
        {
            variablesError = labels.VariableInvalid(badLine);
        }
        return new McpServersDraftErrors(nameError, urlError, commandError, variablesError);
    }

    /// <summary>Whether <paramref name="address"/> is an absolute http or https address with a host.</summary>
    public static bool IsHttpAddress(string address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && uri.Host.Length > 0;

    /// <summary>
    /// Parses one <c>KEY=value</c> per line (blank lines skipped, spaces around the key trimmed, the value kept as written
    /// after the first <c>=</c>). On failure <paramref name="badLine"/> is the 1-based number of the first bad line.
    /// </summary>
    public static bool TryParseVariables(string? text, out IReadOnlyList<McpServerVariable> variables, out int badLine)
    {
        var list = new List<McpServerVariable>();
        variables = list;
        badLine = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                continue;
            }
            var equals = line.IndexOf('=');
            var key = equals > 0 ? line[..equals].Trim() : string.Empty;
            if (key.Length == 0 || key.Any(char.IsWhiteSpace))
            {
                badLine = i + 1;
                return false;
            }
            list.Add(new McpServerVariable(key, line[(equals + 1)..]));
        }
        return true;
    }

    /// <summary>An id from <paramref name="name"/> (lower case, dashes), unique among <paramref name="existing"/>.</summary>
    public static string MakeId(string name, IEnumerable<McpServer> existing)
    {
        var chars = new List<char>();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                chars.Add(c);
            }
            else if (chars.Count > 0 && chars[^1] != '-')
            {
                chars.Add('-');
            }
        }
        var stem = new string(chars.ToArray()).Trim('-');
        if (stem.Length == 0)
        {
            stem = "server";
        }
        var taken = new HashSet<string>(existing.Select(s => s.Id), StringComparer.Ordinal);
        var id = stem;
        for (var n = 2; taken.Contains(id); n++)
        {
            id = $"{stem}-{n}";
        }
        return id;
    }

    /// <summary>The one or two letters of a server's mark: the initials of its first two words, or its first letter.</summary>
    public static string Initials(string name)
    {
        var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return "?";
        }
        var first = char.ToUpperInvariant(words[0][0]);
        return words.Length > 1 && char.IsLetterOrDigit(words[1][0])
            ? string.Concat(first, char.ToUpperInvariant(words[1][0]))
            : first.ToString();
    }
}
