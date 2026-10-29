using Avalonia;
using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>The two sections of a <see cref="ProfilePage"/>.</summary>
public enum ProfilePageTab
{
    /// <summary>The grouped list of recent activity.</summary>
    Activity,

    /// <summary>The grid of project cards.</summary>
    Projects,
}

/// <summary>What happened in a <see cref="ProfileActivity"/>; it picks the row's icon and its color.</summary>
public enum ProfileActivityKind
{
    /// <summary>Something went out (a rocket, in the success color).</summary>
    Shipped,

    /// <summary>A comment (a speech bubble, muted).</summary>
    Commented,

    /// <summary>Something new was started (a plus, muted).</summary>
    Created,

    /// <summary>A review or an approval (a thumbs-up, muted).</summary>
    Reviewed,

    /// <summary>Something was starred (a star outline, in the warning color).</summary>
    Starred,
}

/// <summary>Who can see a <see cref="ProfileProject"/>.</summary>
public enum ProfileProjectVisibility
{
    /// <summary>Anyone.</summary>
    Public,

    /// <summary>Only the people it is shared with.</summary>
    Private,
}

/// <summary>The fields the edit sheet of a <see cref="ProfilePage"/> can change.</summary>
[Flags]
public enum ProfileFields
{
    /// <summary>No field.</summary>
    None = 0,

    /// <summary><see cref="ProfileDetails.Name"/>.</summary>
    Name = 1,

    /// <summary><see cref="ProfileDetails.Headline"/>.</summary>
    Headline = 2,

    /// <summary><see cref="ProfileDetails.Location"/>.</summary>
    Location = 4,

    /// <summary><see cref="ProfileDetails.Website"/>.</summary>
    Website = 8,

    /// <summary><see cref="ProfileDetails.Bio"/>.</summary>
    Bio = 16,
}

/// <summary>
/// The person a <see cref="ProfilePage"/> shows: name, handle, headline, location, website, bio, pictures and when they
/// joined. The page replaces the whole object after a successful save (see <see cref="ProfilePage.Profile"/>).
/// </summary>
public sealed class ProfileDetails : AvaloniaObject
{
    /// <summary>The display name, for example "Rowan Ellery". The activity rows use its first word.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ProfileDetails, string>(nameof(Name), string.Empty);

    /// <summary>The handle without the "@", for example "rowan".</summary>
    public static readonly StyledProperty<string> HandleProperty =
        AvaloniaProperty.Register<ProfileDetails, string>(nameof(Handle), string.Empty);

    /// <summary>A short line about the person's role, shown after the handle.</summary>
    public static readonly StyledProperty<string> HeadlineProperty =
        AvaloniaProperty.Register<ProfileDetails, string>(nameof(Headline), string.Empty);

    /// <summary>Where the person is; the line is hidden while it is empty.</summary>
    public static readonly StyledProperty<string> LocationProperty =
        AvaloniaProperty.Register<ProfileDetails, string>(nameof(Location), string.Empty);

    /// <summary>A website without or with its scheme, for example "rowanellery.studio"; hidden while empty.</summary>
    public static readonly StyledProperty<string> WebsiteProperty =
        AvaloniaProperty.Register<ProfileDetails, string>(nameof(Website), string.Empty);

    /// <summary>A few sentences about the person (the edit sheet caps it at <see cref="ProfilePage.BioMaxLength"/>).</summary>
    public static readonly StyledProperty<string> BioProperty =
        AvaloniaProperty.Register<ProfileDetails, string>(nameof(Bio), string.Empty);

    /// <summary>The portrait; without one the avatar shows the initials of <see cref="Name"/>.</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<ProfileDetails, ImageSource?>(nameof(Avatar));

    /// <summary>The wide picture at the top; drawn over <see cref="CoverBrush"/>.</summary>
    public static readonly StyledProperty<ImageSource?> CoverProperty =
        AvaloniaProperty.Register<ProfileDetails, ImageSource?>(nameof(Cover));

    /// <summary>A fill for the cover area, under <see cref="Cover"/> (default: the muted surface).</summary>
    public static readonly StyledProperty<IBrush?> CoverBrushProperty =
        AvaloniaProperty.Register<ProfileDetails, IBrush?>(nameof(CoverBrush));

    /// <summary>When the person joined, as shown after "Joined", for example "June 2020"; hidden while empty.</summary>
    public static readonly StyledProperty<string> JoinedTextProperty =
        AvaloniaProperty.Register<ProfileDetails, string>(nameof(JoinedText), string.Empty);

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="HandleProperty"/>
    public string Handle { get => GetValue(HandleProperty); set => SetValue(HandleProperty, value); }

    /// <inheritdoc cref="HeadlineProperty"/>
    public string Headline { get => GetValue(HeadlineProperty); set => SetValue(HeadlineProperty, value); }

    /// <inheritdoc cref="LocationProperty"/>
    public string Location { get => GetValue(LocationProperty); set => SetValue(LocationProperty, value); }

    /// <inheritdoc cref="WebsiteProperty"/>
    public string Website { get => GetValue(WebsiteProperty); set => SetValue(WebsiteProperty, value); }

    /// <inheritdoc cref="BioProperty"/>
    public string Bio { get => GetValue(BioProperty); set => SetValue(BioProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }

    /// <inheritdoc cref="CoverProperty"/>
    public ImageSource? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }

    /// <inheritdoc cref="CoverBrushProperty"/>
    public IBrush? CoverBrush { get => GetValue(CoverBrushProperty); set => SetValue(CoverBrushProperty, value); }

    /// <inheritdoc cref="JoinedTextProperty"/>
    public string JoinedText { get => GetValue(JoinedTextProperty); set => SetValue(JoinedTextProperty, value); }

    /// <summary>The first word of <see cref="Name"/> (what the activity rows show).</summary>
    public string FirstName
    {
        get
        {
            var name = (Name ?? string.Empty).Trim();
            var space = name.IndexOfAny([' ', '\t', '\n']);
            return space < 0 ? name : name[..space];
        }
    }

    /// <summary>A copy with the same values (the edit sheet's draft starts from one).</summary>
    public ProfileDetails Clone() => new()
    {
        Name = Name,
        Handle = Handle,
        Headline = Headline,
        Location = Location,
        Website = Website,
        Bio = Bio,
        Avatar = Avatar,
        Cover = Cover,
        CoverBrush = CoverBrush,
        JoinedText = JoinedText,
    };
}

/// <summary>One number under the bio, for example 3,107 followers. The page shows up to three.</summary>
public sealed class ProfileStat : AvaloniaObject
{
    /// <summary>Creates an empty stat (for XAML).</summary>
    public ProfileStat()
    {
    }

    /// <summary>Creates a stat with a label and a value.</summary>
    public ProfileStat(string label, long value, string? id = null)
    {
        Label = label;
        Value = value;
        Id = id ?? label;
    }

    /// <summary>A key you choose (optional).</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<ProfileStat, string?>(nameof(Id));

    /// <summary>The word under the number, for example "Followers".</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ProfileStat, string>(nameof(Label), string.Empty);

    /// <summary>The number, shown with group separators (3,107).</summary>
    public static readonly StyledProperty<long> ValueProperty =
        AvaloniaProperty.Register<ProfileStat, long>(nameof(Value));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public long Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

/// <summary>
/// One row of the Activity tab: "{first name} {Action} <b>{Target}</b>", an optional <see cref="Detail"/> line and a time.
/// Rows are grouped under <see cref="Group"/> headings in the order the groups first appear.
/// </summary>
public sealed class ProfileActivity : AvaloniaObject
{
    /// <summary>Creates an empty row (for XAML).</summary>
    public ProfileActivity()
    {
    }

    /// <summary>Creates a row.</summary>
    public ProfileActivity(ProfileActivityKind kind, string action, string target, string timeText, string group,
        string? detail = null)
    {
        Kind = kind;
        Action = action;
        Target = target;
        TimeText = timeText;
        Group = group;
        Detail = detail;
    }

    /// <summary>What happened; picks the icon.</summary>
    public static readonly StyledProperty<ProfileActivityKind> KindProperty =
        AvaloniaProperty.Register<ProfileActivity, ProfileActivityKind>(nameof(Kind));

    /// <summary>The verb after the name, for example "shipped" or "commented on".</summary>
    public static readonly StyledProperty<string> ActionProperty =
        AvaloniaProperty.Register<ProfileActivity, string>(nameof(Action), string.Empty);

    /// <summary>What it happened to, shown in the strong color, for example "Color tokens v3".</summary>
    public static readonly StyledProperty<string> TargetProperty =
        AvaloniaProperty.Register<ProfileActivity, string>(nameof(Target), string.Empty);

    /// <summary>An optional second line, for example a quote from the comment.</summary>
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<ProfileActivity, string?>(nameof(Detail));

    /// <summary>When, as shown at the end of the row, for example "3h" or "Aug 30".</summary>
    public static readonly StyledProperty<string> TimeTextProperty =
        AvaloniaProperty.Register<ProfileActivity, string>(nameof(TimeText), string.Empty);

    /// <summary>The heading this row is listed under, for example "This week".</summary>
    public static readonly StyledProperty<string> GroupProperty =
        AvaloniaProperty.Register<ProfileActivity, string>(nameof(Group), string.Empty);

    /// <inheritdoc cref="KindProperty"/>
    public ProfileActivityKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }

    /// <inheritdoc cref="ActionProperty"/>
    public string Action { get => GetValue(ActionProperty); set => SetValue(ActionProperty, value); }

    /// <inheritdoc cref="TargetProperty"/>
    public string Target { get => GetValue(TargetProperty); set => SetValue(TargetProperty, value); }

    /// <inheritdoc cref="DetailProperty"/>
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    /// <inheritdoc cref="TimeTextProperty"/>
    public string TimeText { get => GetValue(TimeTextProperty); set => SetValue(TimeTextProperty, value); }

    /// <inheritdoc cref="GroupProperty"/>
    public string Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }
}

/// <summary>A person working on a <see cref="ProfileProject"/>, drawn as a small overlapping face.</summary>
public sealed class ProfileCollaborator : AvaloniaObject
{
    /// <summary>Creates an empty collaborator (for XAML).</summary>
    public ProfileCollaborator()
    {
    }

    /// <summary>Creates a collaborator.</summary>
    public ProfileCollaborator(string name, ImageSource? avatar = null)
    {
        Name = name;
        Avatar = avatar;
    }

    /// <summary>The person's name (initials without a portrait, and the accessible name).</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<ProfileCollaborator, string>(nameof(Name), string.Empty);

    /// <summary>The portrait (optional).</summary>
    public static readonly StyledProperty<ImageSource?> AvatarProperty =
        AvaloniaProperty.Register<ProfileCollaborator, ImageSource?>(nameof(Avatar));

    /// <inheritdoc cref="NameProperty"/>
    public string Name { get => GetValue(NameProperty); set => SetValue(NameProperty, value); }

    /// <inheritdoc cref="AvatarProperty"/>
    public ImageSource? Avatar { get => GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
}

/// <summary>
/// A card of the Projects tab: a cover, the title and visibility, a description, collaborators, when it was updated and a
/// star button. Starring writes <see cref="IsStarred"/> and <see cref="StarCount"/> back.
/// </summary>
public sealed class ProfileProject : AvaloniaObject
{
    /// <summary>Creates an empty project (for XAML).</summary>
    public ProfileProject()
    {
        SetCurrentValue(CollaboratorsProperty, new AvaloniaList<ProfileCollaborator>());
    }

    /// <summary>Creates a project.</summary>
    public ProfileProject(string title, string description, string updatedText, int starCount, bool isStarred = false,
        ProfileProjectVisibility visibility = ProfileProjectVisibility.Public, string? id = null) : this()
    {
        Title = title;
        Description = description;
        UpdatedText = updatedText;
        StarCount = starCount;
        IsStarred = isStarred;
        Visibility = visibility;
        Id = id ?? title;
    }

    /// <summary>A key you choose; reported by <see cref="ProfilePage.StarChanged"/>.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<ProfileProject, string?>(nameof(Id));

    /// <summary>The project's name (one line, cut with an ellipsis).</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ProfileProject, string>(nameof(Title), string.Empty);

    /// <summary>One or two lines about it.</summary>
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ProfileProject, string>(nameof(Description), string.Empty);

    /// <summary>Public or private; shown at the end of the title line.</summary>
    public static readonly StyledProperty<ProfileProjectVisibility> VisibilityProperty =
        AvaloniaProperty.Register<ProfileProject, ProfileProjectVisibility>(nameof(Visibility));

    /// <summary>When it last changed, as shown, for example "Updated 3h ago".</summary>
    public static readonly StyledProperty<string> UpdatedTextProperty =
        AvaloniaProperty.Register<ProfileProject, string>(nameof(UpdatedText), string.Empty);

    /// <summary>How many stars it has (two-way: the star button adds or removes one).</summary>
    public static readonly StyledProperty<int> StarCountProperty =
        AvaloniaProperty.Register<ProfileProject, int>(nameof(StarCount),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Whether the viewer has starred it (two-way).</summary>
    public static readonly StyledProperty<bool> IsStarredProperty =
        AvaloniaProperty.Register<ProfileProject, bool>(nameof(IsStarred),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>The picture at the top of the card; drawn over <see cref="CoverBrush"/>.</summary>
    public static readonly StyledProperty<ImageSource?> CoverProperty =
        AvaloniaProperty.Register<ProfileProject, ImageSource?>(nameof(Cover));

    /// <summary>A fill for the cover area, under <see cref="Cover"/> (default: the muted surface).</summary>
    public static readonly StyledProperty<IBrush?> CoverBrushProperty =
        AvaloniaProperty.Register<ProfileProject, IBrush?>(nameof(CoverBrush));

    /// <summary>The people on it, drawn as overlapping faces.</summary>
    public static readonly StyledProperty<AvaloniaList<ProfileCollaborator>> CollaboratorsProperty =
        AvaloniaProperty.Register<ProfileProject, AvaloniaList<ProfileCollaborator>>(nameof(Collaborators));

    /// <inheritdoc cref="IdProperty"/>
    public string? Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="VisibilityProperty"/>
    public ProfileProjectVisibility Visibility { get => GetValue(VisibilityProperty); set => SetValue(VisibilityProperty, value); }

    /// <inheritdoc cref="UpdatedTextProperty"/>
    public string UpdatedText { get => GetValue(UpdatedTextProperty); set => SetValue(UpdatedTextProperty, value); }

    /// <inheritdoc cref="StarCountProperty"/>
    public int StarCount { get => GetValue(StarCountProperty); set => SetValue(StarCountProperty, value); }

    /// <inheritdoc cref="IsStarredProperty"/>
    public bool IsStarred { get => GetValue(IsStarredProperty); set => SetValue(IsStarredProperty, value); }

    /// <inheritdoc cref="CoverProperty"/>
    public ImageSource? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }

    /// <inheritdoc cref="CoverBrushProperty"/>
    public IBrush? CoverBrush { get => GetValue(CoverBrushProperty); set => SetValue(CoverBrushProperty, value); }

    /// <inheritdoc cref="CollaboratorsProperty"/>
    [Content]
    public AvaloniaList<ProfileCollaborator> Collaborators
    {
        get => GetValue(CollaboratorsProperty);
        set => SetValue(CollaboratorsProperty, value);
    }
}

/// <summary>Arguments of <see cref="ProfilePage.TabChanged"/>.</summary>
public sealed class ProfilePageTabChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProfilePageTabChangedEventArgs(RoutedEvent routedEvent, ProfilePageTab oldTab, ProfilePageTab newTab)
        : base(routedEvent)
    {
        OldTab = oldTab;
        NewTab = newTab;
    }

    /// <summary>The tab shown before.</summary>
    public ProfilePageTab OldTab { get; }

    /// <summary>The tab shown now.</summary>
    public ProfilePageTab NewTab { get; }
}

/// <summary>Arguments of <see cref="ProfilePage.StarChanged"/>.</summary>
public sealed class ProfileStarChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProfileStarChangedEventArgs(RoutedEvent routedEvent, ProfileProject project, bool isStarred, int starCount)
        : base(routedEvent)
    {
        Project = project;
        IsStarred = isStarred;
        StarCount = starCount;
    }

    /// <summary>The project whose star was pressed.</summary>
    public ProfileProject Project { get; }

    /// <summary>Whether it is starred now.</summary>
    public bool IsStarred { get; }

    /// <summary>Its star count now.</summary>
    public int StarCount { get; }
}

/// <summary>Arguments of <see cref="ProfilePage.Saved"/>.</summary>
public sealed class ProfileSavedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProfileSavedEventArgs(RoutedEvent routedEvent, ProfileDetails previous, ProfileDetails profile,
        ProfileFields changedFields) : base(routedEvent)
    {
        Previous = previous;
        Profile = profile;
        ChangedFields = changedFields;
    }

    /// <summary>The profile before the save.</summary>
    public ProfileDetails Previous { get; }

    /// <summary>The saved profile (now <see cref="ProfilePage.Profile"/>), with trimmed values.</summary>
    public ProfileDetails Profile { get; }

    /// <summary>The fields whose trimmed value changed (they glow once).</summary>
    public ProfileFields ChangedFields { get; }
}

/// <summary>Arguments of <see cref="ProfilePage.SaveFailed"/>.</summary>
public sealed class ProfileSaveFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProfileSaveFailedEventArgs(RoutedEvent routedEvent, ProfileDetails draft, Exception error) : base(routedEvent)
    {
        Draft = draft;
        Error = error;
    }

    /// <summary>The values that could not be saved.</summary>
    public ProfileDetails Draft { get; }

    /// <summary>What <see cref="ProfilePage.SaveHandler"/> threw; its message is shown in the sheet.</summary>
    public Exception Error { get; }
}

/// <summary>Arguments of <see cref="ProfilePage.Shared"/>.</summary>
public sealed class ProfileSharedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ProfileSharedEventArgs(RoutedEvent routedEvent, string url, bool succeeded) : base(routedEvent)
    {
        Url = url;
        Succeeded = succeeded;
    }

    /// <summary>The link that was copied (or could not be).</summary>
    public string Url { get; }

    /// <summary>Whether the clipboard took it.</summary>
    public bool Succeeded { get; }
}
