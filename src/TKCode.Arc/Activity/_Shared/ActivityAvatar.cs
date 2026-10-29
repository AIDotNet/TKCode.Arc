using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Automation;

namespace TKCode.Arc;

/// <summary>
/// Identity portraits of the Activity controls, built on <see cref="Avatar"/> with Arc's activity look: a borderless
/// circle on the muted surface, initials in the secondary text color at 38% of the diameter (weight 500), the photo
/// over them when it loads. The portrait is decorative (the name is in the row's accessible name).
/// </summary>
internal static class ActivityAvatar
{
    /// <summary>Creates a portrait of <paramref name="diameter"/> px for <paramref name="name"/>.</summary>
    public static Avatar Create(double diameter, string? name = null, string? url = null)
    {
        var avatar = new Avatar
        {
            Width = diameter,
            Height = diameter,
            FontSize = Math.Round(diameter * 0.38),
            BorderThickness = default,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
            Focusable = false,
            IsHitTestVisible = false
        };
        ArcTokens.Bind(avatar, Avatar.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        AutomationProperties.SetAccessibilityView(avatar, AccessibilityView.Raw);
        Update(avatar, name, url);
        return avatar;
    }

    /// <summary>Points a portrait at another person (reused rows keep their avatar control).</summary>
    public static void Update(Avatar avatar, string? name, string? url)
    {
        avatar.DisplayName = name;
        avatar.Source = Parse(url);
        AutomationProperties.SetAccessibilityView(avatar, AccessibilityView.Raw);
    }

    /// <summary>An image source for a URL (http(s), file or avares), or null when there is none or it cannot be parsed.</summary>
    public static ImageSource? Parse(string? url) =>
        !string.IsNullOrWhiteSpace(url) && ImageSource.TryParse(url, out var source) ? source : null;
}
