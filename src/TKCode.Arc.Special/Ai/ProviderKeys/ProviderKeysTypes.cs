using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>Where a stored provider key stands in <see cref="ProviderKeys"/>.</summary>
public enum ProviderKeyStatus
{
    /// <summary>No key is stored for the provider: the row reads "Not set" and offers Add key.</summary>
    Unset,

    /// <summary>The key passed its last test.</summary>
    Connected,

    /// <summary>The provider rejected the key; <see cref="ProviderKey.Error"/> says why.</summary>
    Invalid
}

/// <summary>Whether a saved key was added to an empty row or replaced an existing key.</summary>
public enum ProviderKeySaveMode
{
    /// <summary>The row had no key.</summary>
    Add,

    /// <summary>The key replaced the one the row had (rotation).</summary>
    Rotate
}

/// <summary>What changed in <see cref="ProviderKeys.Keys"/>.</summary>
public enum ProviderKeysChange
{
    /// <summary>A key was added to an empty row and passed its test.</summary>
    Added,

    /// <summary>A key was replaced by a new one that passed its test.</summary>
    Rotated,

    /// <summary>A key was removed (Undo is still on screen).</summary>
    Removed,

    /// <summary>A removed key was put back with Undo.</summary>
    Restored,

    /// <summary>A stored key was tested again and its status updated.</summary>
    Retested
}

/// <summary>What a pasted key's format looks like, checked as the person types.</summary>
public enum ProviderKeyFormat
{
    /// <summary>Nothing typed yet.</summary>
    Empty,

    /// <summary>The key does not start with the provider's prefix.</summary>
    WrongPrefix,

    /// <summary>The key has characters a key never has (spaces, punctuation).</summary>
    BadCharacters,

    /// <summary>The key is shorter than the provider's keys.</summary>
    TooShort,

    /// <summary>The key looks right; only the connection test can tell more.</summary>
    Valid
}

/// <summary>
/// One provider's stored key in <see cref="ProviderKeys"/>. Pass <see cref="Value"/> to enable reveal and copy, or only
/// <see cref="Last4"/> when your server never returns full keys (the row then shows the masked key and disables both).
/// </summary>
public sealed class ProviderKey
{
    /// <summary>The provider's id (<see cref="ProviderKeyProvider.Id"/>), compared without case.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Connected, Invalid or Unset (an Unset entry reads like a missing one).</summary>
    public ProviderKeyStatus Status { get; set; } = ProviderKeyStatus.Connected;

    /// <summary>The full key. Leave it null in production so the key never reaches the client.</summary>
    public string? Value { get; set; }

    /// <summary>The key's last four characters, shown after the mask when <see cref="Value"/> is not given.</summary>
    public string? Last4 { get; set; }

    /// <summary>When the key was last used for a request; null reads "Never used".</summary>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>When the key was last tested.</summary>
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>Why the provider rejected the key ("401 Unauthorized"), shown on an invalid row.</summary>
    public string? Error { get; set; }

    /// <summary>The last four characters on screen: <see cref="Last4"/>, else the end of <see cref="Value"/>.</summary>
    internal string DisplayLast4 =>
        !string.IsNullOrEmpty(Last4) ? Last4! :
        Value is { Length: > 4 } value ? value[^4..] :
        Value ?? string.Empty;

    /// <summary>A copy, for the card's immutable updates.</summary>
    internal ProviderKey Copy() => new()
    {
        Provider = Provider,
        Status = Status,
        Value = Value,
        Last4 = Last4,
        LastUsedAt = LastUsedAt,
        CheckedAt = CheckedAt,
        Error = Error,
    };
}

/// <summary>
/// A provider row's identity in <see cref="ProviderKeys"/>: its id, name, mark and the shape of its keys (a prefix and a
/// minimum length), which the add and rotate form checks as the person types.
/// </summary>
public sealed class ProviderKeyProvider
{
    /// <summary>The id keys refer to in <see cref="ProviderKey.Provider"/>.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The name on the row and in every label ("Copy the {name} key").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What every key of this provider starts with ("sk-"); empty skips the prefix check.</summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>The shortest key the provider issues, prefix included.</summary>
    public int MinimumLength { get; set; } = 24;

    /// <summary>The provider's mark: a 24 × 24 stroked glyph. Null shows <see cref="Monogram"/> in a tinted square.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>The letters shown when there is no <see cref="Mark"/>; null uses the first letter of <see cref="Name"/>.</summary>
    public string? Monogram { get; set; }

    /// <summary>The mark's color; null uses the foreground.</summary>
    public IBrush? MarkBrush { get; set; }

    /// <summary>Checks a pasted key against <see cref="KeyPrefix"/>, the characters keys use and <see cref="MinimumLength"/>.</summary>
    public ProviderKeyFormat CheckFormat(string? key)
    {
        var text = key?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return ProviderKeyFormat.Empty;
        }
        var prefix = KeyPrefix ?? string.Empty;
        // While the prefix is still being typed, a key that matches it so far is not wrong yet.
        var typed = Math.Min(text.Length, prefix.Length);
        if (!string.Equals(text[..typed], prefix[..typed], StringComparison.Ordinal))
        {
            return ProviderKeyFormat.WrongPrefix;
        }
        foreach (var c in text)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            {
                return ProviderKeyFormat.BadCharacters;
            }
        }
        return text.Length < Math.Max(MinimumLength, prefix.Length + 1) ? ProviderKeyFormat.TooShort : ProviderKeyFormat.Valid;
    }
}

/// <summary>What <see cref="ProviderKeys.TestKey"/> is asked to test.</summary>
public sealed class ProviderKeyTestRequest
{
    /// <summary>Creates a request.</summary>
    public ProviderKeyTestRequest(string provider, string? key, CancellationToken cancellationToken)
    {
        Provider = provider;
        Key = key;
        CancellationToken = cancellationToken;
    }

    /// <summary>The provider's id.</summary>
    public string Provider { get; }

    /// <summary>The pasted key when adding or rotating; null when retesting the stored key (test it on your server by provider).</summary>
    public string? Key { get; }

    /// <summary>Cancelled when the form closes or the card leaves the screen.</summary>
    public CancellationToken CancellationToken { get; }
}

/// <summary>The outcome of a key test.</summary>
public sealed class ProviderKeyTestResult
{
    /// <summary>Creates a result.</summary>
    public ProviderKeyTestResult(bool isOk, string? detail = null, string? error = null)
    {
        IsOk = isOk;
        Detail = detail;
        Error = error;
    }

    /// <summary>Whether the provider accepted the key.</summary>
    public bool IsOk { get; }

    /// <summary>An optional note on success ("12 models available").</summary>
    public string? Detail { get; }

    /// <summary>Why the key was rejected ("401 Unauthorized").</summary>
    public string? Error { get; }

    /// <summary>An accepted key.</summary>
    public static ProviderKeyTestResult Ok(string? detail = null) => new(true, detail);

    /// <summary>A rejected key.</summary>
    public static ProviderKeyTestResult Fail(string error) => new(false, error: error);
}

/// <summary>Arguments of <see cref="ProviderKeys.KeysChanged"/>.</summary>
public sealed class ProviderKeysChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProviderKeysChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<ProviderKey> keys, string provider, ProviderKeysChange change)
        : base(routedEvent)
    {
        Keys = keys;
        Provider = provider;
        Change = change;
    }

    /// <summary>Every key after the change.</summary>
    public IReadOnlyList<ProviderKey> Keys { get; }

    /// <summary>The provider whose key changed.</summary>
    public string Provider { get; }

    /// <summary>What happened.</summary>
    public ProviderKeysChange Change { get; }
}

/// <summary>Arguments of <see cref="ProviderKeys.KeySaved"/>.</summary>
public sealed class ProviderKeySavedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProviderKeySavedEventArgs(RoutedEvent routedEvent, string provider, string key, ProviderKeySaveMode mode)
        : base(routedEvent)
    {
        Provider = provider;
        Key = key;
        Mode = mode;
    }

    /// <summary>The provider's id.</summary>
    public string Provider { get; }

    /// <summary>The new key, which passed its test. Persist it.</summary>
    public string Key { get; }

    /// <summary>Added to an empty row or rotated.</summary>
    public ProviderKeySaveMode Mode { get; }
}

/// <summary>Arguments of <see cref="ProviderKeys.KeyRemoved"/> and <see cref="ProviderKeys.KeyCopied"/>.</summary>
public sealed class ProviderKeyEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProviderKeyEventArgs(RoutedEvent routedEvent, string provider) : base(routedEvent)
    {
        Provider = provider;
    }

    /// <summary>The provider's id.</summary>
    public string Provider { get; }
}
