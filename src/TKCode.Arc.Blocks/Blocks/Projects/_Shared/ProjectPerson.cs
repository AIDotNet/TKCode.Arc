using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A person on a project block: a board card's owner, a calendar attendee, a meeting guest or a release owner. Without a
/// picture the blocks draw a generated avatar: the initials on a soft color picked from the person's key or name, so the
/// same person keeps the same color everywhere.
/// </summary>
public sealed class ProjectPerson : AvaloniaObject
{
    /// <summary>A stable id (cards and events refer to it); empty uses <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string> KeyProperty =
        AvaloniaProperty.Register<ProjectPerson, string>(nameof(Key), string.Empty);

    /// <summary>The full name, read by screen readers and shown in lists.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ProjectPerson, string>(nameof(Name), string.Empty);

    /// <summary>The name used in short sentences ("Noor is busy"); null takes the first word of <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string?> ShortNameProperty =
        AvaloniaProperty.Register<ProjectPerson, string?>(nameof(ShortName));

    /// <summary>The initials of the generated avatar; null takes the first letters of the first two words of the name.</summary>
    public static readonly StyledProperty<string?> InitialsProperty =
        AvaloniaProperty.Register<ProjectPerson, string?>(nameof(Initials));

    /// <summary>A picture drawn in the avatar circle; null draws the generated avatar.</summary>
    public static readonly StyledProperty<IImage?> AvatarProperty =
        AvaloniaProperty.Register<ProjectPerson, IImage?>(nameof(Avatar));

    /// <summary>The hue (0–360 degrees) of the generated avatar; null derives one from the key or name.</summary>
    public static readonly StyledProperty<double?> HueProperty =
        AvaloniaProperty.Register<ProjectPerson, double?>(nameof(Hue));

    /// <summary>Any object the app wants to keep with the person.</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<ProjectPerson, object?>(nameof(Tag));

    public ProjectPerson()
    {
    }

    /// <summary>Creates a person with a name and an optional key.</summary>
    /// <param name="name">The full name.</param>
    /// <param name="key">A stable id; null uses the name.</param>
    public ProjectPerson(string name, string? key = null)
    {
        Name = name;
        if (key is not null)
        {
            Key = key;
        }
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="ShortNameProperty"/>
    public string? ShortName { get => GetValue(ShortNameProperty); set => SetValue(ShortNameProperty, value); }

    /// <inheritdoc cref="InitialsProperty"/>
    public string? Initials { get => GetValue(InitialsProperty); set => SetValue(InitialsProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public IImage? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }

    /// <inheritdoc cref="HueProperty"/>
    public double? Hue { get => GetValue(HueProperty); set => SetValue(HueProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The key, or the name when no key is set.</summary>
    public string Identity => string.IsNullOrEmpty(Key) ? Name : Key;

    /// <summary>The short name, or the first word of the name.</summary>
    public string DisplayShortName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ShortName))
            {
                return ShortName!;
            }
            var name = Name.Trim();
            var space = name.IndexOf(' ');
            return space > 0 ? name[..space] : name;
        }
    }

    /// <summary>The initials shown on the generated avatar (at most two letters, upper case).</summary>
    public string DisplayInitials
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Initials))
            {
                return Initials!;
            }
            var words = Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (words.Length == 0)
            {
                return string.Empty;
            }
            var first = char.ToUpperInvariant(words[0][0]).ToString();
            return words.Length == 1 ? first : first + char.ToUpperInvariant(words[^1][0]);
        }
    }

    /// <summary>The hue of the generated avatar: <see cref="Hue"/>, or a stable hash of <see cref="Identity"/>.</summary>
    public double DisplayHue
    {
        get
        {
            if (Hue is { } hue && double.IsFinite(hue))
            {
                return ((hue % 360) + 360) % 360;
            }
            // FNV-1a over the identity: stable across runs and platforms (string.GetHashCode is randomized per process).
            var hash = 2166136261u;
            foreach (var ch in Identity)
            {
                hash ^= ch;
                hash *= 16777619u;
            }
            // Twelve evenly spaced hues, so neighbours in a small team rarely look alike.
            return (hash % 12) * 30.0 + 12;
        }
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
