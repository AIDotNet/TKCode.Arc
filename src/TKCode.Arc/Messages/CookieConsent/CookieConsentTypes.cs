using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>One group of cookies a visitor can allow or refuse.</summary>
public sealed class CookieCategory
{
    /// <summary>Stable id, the key of the consent dictionary.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Name shown in the preferences panel (and the switch's accessible name).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Hint under the name (the switch's help text).</summary>
    public string? Description { get; set; }

    /// <summary>Needed for the site to work: always on, shown as "Always on" instead of a switch.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Starting position of the switch before a choice is saved (optional categories default to off).</summary>
    public bool IsEnabledByDefault { get; set; }
}

/// <summary>How a <see cref="CookieConsent"/> choice was made.</summary>
public enum CookieConsentAction
{
    AcceptAll,
    RejectAll,
    Save,
    Reset
}

/// <summary>Where the consent bar sits.</summary>
public enum CookieConsentPosition
{
    Bottom,
    BottomLeft,
    BottomRight
}

/// <summary>Corner of the reopen button.</summary>
public enum CookieConsentSide
{
    Left,
    Right
}

/// <summary>A choice read back from storage (<see cref="CookieConsent.Read"/>).</summary>
public sealed class CookieConsentRecord
{
    public CookieConsentRecord(IReadOnlyDictionary<string, bool> value, string version, DateTimeOffset? updatedAt)
    {
        Value = value;
        Version = version;
        UpdatedAt = updatedAt;
    }

    /// <summary>Category id → allowed.</summary>
    public IReadOnlyDictionary<string, bool> Value { get; }

    /// <summary>Version the choice was saved for.</summary>
    public string Version { get; }

    /// <summary>When the choice was saved.</summary>
    public DateTimeOffset? UpdatedAt { get; }

    internal string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("value");
            foreach (var pair in Value)
            {
                writer.WriteBoolean(pair.Key, pair.Value);
            }
            writer.WriteEndObject();
            writer.WriteString("version", Version);
            writer.WriteString("updatedAt", (UpdatedAt ?? DateTimeOffset.UtcNow).ToUniversalTime().ToString("O"));
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static CookieConsentRecord? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String)
            {
                return null;
            }
            var map = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    map[property.Name] = property.Value.GetBoolean();
                }
            }
            DateTimeOffset? updatedAt = null;
            if (root.TryGetProperty("updatedAt", out var at) && at.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(at.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                                        System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                updatedAt = parsed;
            }
            return new CookieConsentRecord(map, version.GetString() ?? string.Empty, updatedAt);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Arguments of <see cref="CookieConsent.ConsentChanged"/>.</summary>
public class CookieConsentChangedEventArgs : RoutedEventArgs
{
    public CookieConsentChangedEventArgs(RoutedEvent routedEvent, IReadOnlyDictionary<string, bool>? consent,
                                         CookieConsentAction action, DateTimeOffset updatedAt)
        : base(routedEvent)
    {
        Consent = consent;
        Action = action;
        UpdatedAt = updatedAt;
    }

    /// <summary>The new choice (category id → allowed), or null after a reset.</summary>
    public IReadOnlyDictionary<string, bool>? Consent { get; }

    /// <summary>How the choice was made.</summary>
    public CookieConsentAction Action { get; }

    /// <summary>When the choice was made.</summary>
    public DateTimeOffset UpdatedAt { get; }
}

/// <summary>Link to the full policy, shown after the bar message and the panel description.</summary>
public sealed class CookieConsentPolicy
{
    /// <summary>Link text.</summary>
    public string Label { get; set; } = "Cookie policy";

    /// <summary>Opened with the system launcher.</summary>
    public Uri? Href { get; set; }

    /// <summary>Optional command run when the link is invoked.</summary>
    public ICommand? Command { get; set; }
}

/// <summary>Copy of a <see cref="CookieConsent"/> (Arc's English defaults).</summary>
public sealed class CookieConsentLabels
{
    /// <summary>Bar message.</summary>
    public string Message { get; set; } =
        "We use essential cookies to run this site. With your OK, we'd also use optional ones to remember settings and learn what helps.";

    /// <summary>Button that opens the preferences panel.</summary>
    public string Customize { get; set; } = "Customize";

    /// <summary>Allows every category.</summary>
    public string AcceptAll { get; set; } = "Accept all";

    /// <summary>Allows only required categories.</summary>
    public string RejectAll { get; set; } = "Reject all";

    /// <summary>Saves the switches of the panel.</summary>
    public string Save { get; set; } = "Save choices";

    /// <summary>Panel title (also the bar's accessible name).</summary>
    public string PreferencesTitle { get; set; } = "Cookie preferences";

    /// <summary>Panel description.</summary>
    public string PreferencesDescription { get; set; } = "Choose which optional cookies we can use. You can change this any time.";

    /// <summary>Accessible name of the reopen button.</summary>
    public string Reopen { get; set; } = "Cookie preferences";

    /// <summary>Shown instead of a switch for required categories.</summary>
    public string AlwaysOn { get; set; } = "Always on";

    /// <summary>Accessible name of the panel's close button.</summary>
    public string Close { get; set; } = "Close preferences";
}
