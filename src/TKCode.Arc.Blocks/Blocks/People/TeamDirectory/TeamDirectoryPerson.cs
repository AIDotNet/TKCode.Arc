using AtomUI.Controls;
using Avalonia;

namespace TKCode.Arc;

/// <summary>Whether a <see cref="TeamDirectoryPerson"/> can be reached now.</summary>
public enum TeamDirectoryAvailability
{
    /// <summary>At work and reachable: a success-colored dot on the avatar and in the profile.</summary>
    Available,

    /// <summary>Away or offline: a muted dot.</summary>
    Away
}

/// <summary>
/// One person in a <see cref="TeamDirectory"/>: who they are, which team they belong to and how to reach them. The name,
/// role, team and city are searched; the other fields only show in the profile. Changing a field updates the block.
/// </summary>
public class TeamDirectoryPerson : AvaloniaObject
{
    /// <summary>The stable key the selection is kept by; null or empty uses <see cref="Name"/>.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string?>(nameof(Id));

    /// <summary>The full name (searched; the first line of the row and the profile heading).</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string>(nameof(Name), string.Empty);

    /// <summary>The job title (searched; the second line of the row and the line under the profile heading).</summary>
    public static readonly StyledProperty<string> RoleProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string>(nameof(Role), string.Empty);

    /// <summary>The team (searched; the key of the team filter and the profile's Team field).</summary>
    public static readonly StyledProperty<string> TeamProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string>(nameof(Team), string.Empty);

    /// <summary>Whether the person is available (the avatar's dot and the profile's Status field).</summary>
    public static readonly StyledProperty<TeamDirectoryAvailability> AvailabilityProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, TeamDirectoryAvailability>(nameof(Availability));

    /// <summary>What the person works on now, a few words (the profile's Working on field; not searched).</summary>
    public static readonly StyledProperty<string?> FocusProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string?>(nameof(Focus));

    /// <summary>Where the person works (searched; the profile's Location field).</summary>
    public static readonly StyledProperty<string?> CityProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string?>(nameof(City));

    /// <summary>A short time zone name shown after the city ("CET"); plain text, not a clock, not searched.</summary>
    public static readonly StyledProperty<string?> TimeZoneProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string?>(nameof(TimeZone));

    /// <summary>The email address: a mail link in the profile and what the copy button copies (not searched).</summary>
    public static readonly StyledProperty<string?> EmailProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string?>(nameof(Email));

    /// <summary>A sentence or two about the person, under the profile heading (not searched).</summary>
    public static readonly StyledProperty<string?> BioProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, string?>(nameof(Bio));

    /// <summary>The portrait; null shows the initials of <see cref="Name"/>.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<TeamDirectoryPerson, ImageSource?>(nameof(Avatar));

    /// <summary>Creates an empty person; set at least <see cref="Name"/>.</summary>
    public TeamDirectoryPerson()
    {
    }

    /// <summary>Creates a person with a name, a role and a team.</summary>
    /// <param name="name">The full name.</param>
    /// <param name="role">The job title.</param>
    /// <param name="team">The team.</param>
    /// <param name="id">The stable key; null uses the name.</param>
    public TeamDirectoryPerson(string name, string role, string team, string? id = null)
    {
        Name = name;
        Role = role;
        Team = team;
        Id = id;
    }

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="RoleProperty"/>
    public string Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }

    /// <inheritdoc cref="TeamProperty"/>
    public string Team { get => GetValue(TeamProperty); set => SetValue(TeamProperty, value); }

    /// <inheritdoc cref="AvailabilityProperty"/>
    public TeamDirectoryAvailability Availability { get => GetValue(AvailabilityProperty); set => SetValue(AvailabilityProperty, value); }

    /// <inheritdoc cref="FocusProperty"/>
    public string? Focus { get => GetValue(FocusProperty); set => SetValue(FocusProperty, value); }

    /// <inheritdoc cref="CityProperty"/>
    public string? City { get => GetValue(CityProperty); set => SetValue(CityProperty, value); }

    /// <inheritdoc cref="TimeZoneProperty"/>
    public string? TimeZone { get => GetValue(TimeZoneProperty); set => SetValue(TimeZoneProperty, value); }

    /// <inheritdoc cref="EmailProperty"/>
    public string? Email { get => GetValue(EmailProperty); set => SetValue(EmailProperty, value); }

    /// <inheritdoc cref="BioProperty"/>
    public string? Bio { get => GetValue(BioProperty); set => SetValue(BioProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }

    /// <summary>The key the block uses: <see cref="Id"/>, or <see cref="Name"/> when no id is set.</summary>
    public string Key => string.IsNullOrEmpty(Id) ? Name : Id;

    /// <inheritdoc />
    public override string ToString() => Name;
}
