using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace TKCode.Arc.Localization;

/// <summary>
/// A BCP 47 language tag such as <c>en-US</c> or <c>zh-Hant-TW</c>, in canonical case (language lower case, script title
/// case, region upper case). Two tags are equal when their canonical values are.
/// </summary>
public sealed class LanguageTag : IEquatable<LanguageTag>
{
    private LanguageTag(string value)
    {
        Value = value;
    }

    /// <summary>The canonical tag, for example <c>zh-CN</c>.</summary>
    public string Value { get; }

    /// <summary>
    /// The tag with its last subtag removed (<c>zh-Hant-TW</c> → <c>zh-Hant</c> → <c>zh</c>), or null for a bare language.
    /// Text missing for a language is looked up in its parents before the fallback languages.
    /// </summary>
    public LanguageTag? Parent
    {
        get
        {
            var at = Value.LastIndexOf('-');
            return at < 0 ? null : new LanguageTag(Value.Substring(0, at));
        }
    }

    /// <summary>Parses <paramref name="tag"/>; hyphens or underscores separate subtags and case does not matter.</summary>
    /// <param name="tag">The tag, for example <c>zh-cn</c> or <c>zh_CN</c>.</param>
    /// <exception cref="FormatException">The text is not a language tag.</exception>
    public static LanguageTag Parse(string tag)
    {
        if (!TryParse(tag, out var result))
        {
            throw new FormatException($"'{tag}' is not a language tag (for example en-US or zh-Hant-TW).");
        }
        return result;
    }

    /// <summary>Parses <paramref name="tag"/>, or returns false when it is not a language tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="result">The parsed tag.</param>
    public static bool TryParse([NotNullWhen(true)] string? tag, [NotNullWhen(true)] out LanguageTag? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }
        var parts = tag.Trim().Split('-', '_');
        var builder = new System.Text.StringBuilder(tag.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length is 0 or > 8 || !IsAlphanumeric(part))
            {
                return false;
            }
            if (i == 0)
            {
                if (part.Length < 2 || !IsLetters(part))
                {
                    return false;
                }
                builder.Append(part.ToLowerInvariant());
                continue;
            }
            builder.Append('-');
            if (i == 1 && part.Length == 4 && IsLetters(part))
            {
                // Script: Hant, Hans, Latn.
                builder.Append(char.ToUpperInvariant(part[0])).Append(part.Substring(1).ToLowerInvariant());
            }
            else if ((part.Length == 2 && IsLetters(part)) || (part.Length == 3 && IsDigits(part)))
            {
                // Region: US, TW, 419.
                builder.Append(part.ToUpperInvariant());
            }
            else
            {
                builder.Append(part.ToLowerInvariant());
            }
        }
        result = new LanguageTag(builder.ToString());
        return true;
    }

    /// <summary>The .NET culture of this tag (formatting of numbers and dates), or the invariant culture when .NET has none.</summary>
    public CultureInfo ToCulture()
    {
        try
        {
            return CultureInfo.GetCultureInfo(Value);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>Whether this tag is <paramref name="other"/> or one of its parents (<c>zh</c> includes <c>zh-CN</c>).</summary>
    /// <param name="other">The tag to test.</param>
    public bool Includes(LanguageTag other)
        => other.Value.Length == Value.Length
            ? other.Value == Value
            : other.Value.Length > Value.Length && other.Value[Value.Length] == '-'
                                                && other.Value.StartsWith(Value, StringComparison.Ordinal);

    /// <inheritdoc />
    public bool Equals(LanguageTag? other) => other is not null && other.Value == Value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as LanguageTag);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <summary>The canonical tag.</summary>
    public override string ToString() => Value;

    /// <summary>Value equality.</summary>
    public static bool operator ==(LanguageTag? left, LanguageTag? right) => left is null ? right is null : left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(LanguageTag? left, LanguageTag? right) => !(left == right);

    private static bool IsAlphanumeric(string text)
    {
        foreach (var c in text)
        {
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsLetters(string text)
    {
        foreach (var c in text)
        {
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsDigits(string text)
    {
        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>The languages Arc ships text for.</summary>
public static class LanguageTags
{
    /// <summary>English (United States), the source language of Arc's catalogs.</summary>
    public static LanguageTag EnUS { get; } = LanguageTag.Parse("en-US");

    /// <summary>Chinese (Simplified, China).</summary>
    public static LanguageTag ZhCN { get; } = LanguageTag.Parse("zh-CN");

    /// <summary>Chinese (Traditional, Taiwan).</summary>
    public static LanguageTag ZhTW { get; } = LanguageTag.Parse("zh-TW");
}
