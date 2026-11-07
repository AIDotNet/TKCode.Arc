using Avalonia;

namespace TKCode.Arc;

/// <summary>The leading mark of a <see cref="StretchRefreshActivity"/> row.</summary>
public enum StretchRefreshActivityTone
{
    /// <summary>A round avatar for <see cref="StretchRefreshActivity.Actor"/> (a generated gradient).</summary>
    Person,

    /// <summary>A green circled check (a passing build, a finished job).</summary>
    Success,

    /// <summary>A red circled cross (a failed build, a rejected payment).</summary>
    Danger,

    /// <summary>A neutral receipt glyph (a payment, an invoice).</summary>
    Neutral
}

/// <summary>
/// One entry of an activity feed, shown by <see cref="StretchRefreshActivityRow"/> (the row
/// <see cref="StretchRefresh"/> uses when it has no <see cref="StretchRefresh.ItemTemplate"/>): a sentence
/// "<b>Actor</b> action <b>Target</b>", an optional quote and photo, a meta line ("Billing · 12 min ago") and an unread
/// dot. <see cref="IsUnread"/> is a styled property, so marking an entry read fades its dot on screen.
/// </summary>
public class StretchRefreshActivity : AvaloniaObject
{
    /// <summary>Whether the entry is new to the reader: the row shows an accent dot and reads "New." first.</summary>
    public static readonly StyledProperty<bool> IsUnreadProperty =
        AvaloniaProperty.Register<StretchRefreshActivity, bool>(nameof(IsUnread));

    /// <summary>A stable identity; the feed matches rows by it across refreshes.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Who or what acted, shown in the emphasis weight ("Priya Okafor", "Build 1307").</summary>
    public string Actor { get; init; } = string.Empty;

    /// <summary>What happened, between the two emphasized parts ("merged", "passed on", "paid invoice").</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>What it happened to, shown in the emphasis weight (optional).</summary>
    public string? Target { get; init; }

    /// <summary>A quoted comment under the sentence, without the quotation marks (optional).</summary>
    public string? Quote { get; init; }

    /// <summary>The meta line, such as "Billing · 12 min ago" or "$2,375.00 · 1 hr ago".</summary>
    public string? Meta { get; init; }

    /// <summary>The leading mark: an avatar or a status glyph.</summary>
    public StretchRefreshActivityTone Tone { get; init; }

    /// <summary>Seeds the generated avatar gradient (the same seed draws the same avatar).</summary>
    public int AvatarSeed { get; init; }

    /// <summary>Seeds a generated 4:3 photo attachment; null shows none.</summary>
    public int? PhotoSeed { get; init; }

    /// <inheritdoc cref="IsUnreadProperty"/>
    public bool IsUnread
    {
        get => GetValue(IsUnreadProperty);
        set => SetValue(IsUnreadProperty, value);
    }

    /// <summary>The sentence as plain text ("Priya Okafor merged Fix duplicate invoice emails").</summary>
    public string Sentence => string.IsNullOrEmpty(Target) ? $"{Actor} {Action}".Trim() : $"{Actor} {Action} {Target}".Trim();
}
