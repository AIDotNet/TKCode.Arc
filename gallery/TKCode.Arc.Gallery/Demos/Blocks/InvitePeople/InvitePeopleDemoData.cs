using AtomUI.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The InvitePeople demos' data (all invented): the Lumen Labs workspace with a directory of sixteen people (five on the
/// team, one invited three days ago, ten who can be invited), three roles, two plans and a sample list to paste.
/// Portraits come from the placeholder service other Gallery demos use.
/// </summary>
public static class InvitePeopleDemoData
{
    public const string Workspace = "Lumen Labs";

    public const string Title = "Invite people";

    public const string Description = "Bring people into Lumen Labs by name or email";

    /// <summary>
    /// Eleven entries: three directory names, one "Name &lt;address&gt;", a directory address, a team member's address,
    /// an address without a full domain, one without a domain, a name that is not in the directory and two duplicates.
    /// Pasted into the preview it adds five people, flags four entries, skips two and runs one seat over the Basic plan.
    /// </summary>
    public const string SampleList =
        "Clara Weiss, Mateus Rocha; Ravi Menon <ravi.menon@lumen.example>\n" +
        "kenji.ito@lumen.example, nadia@lumenlabs, hugo.lambert@, Mina Cho, Clara Weiss, sam rivers, " +
        "elif.kaya@lumen.example, mateus.rocha@lumen.example";

    private static ImageSource? Portrait(int id) =>
        ImageSource.TryParse($"https://i.pravatar.cc/160?img={id}", out var source) ? source : null;

    private static InvitePeoplePerson Person(string name, string title, int portrait) =>
        new(name, name.ToLowerInvariant().Replace(' ', '.') + "@lumen.example", title, Portrait(portrait));

    /// <summary>A fresh set of people (each demo gets its own objects).</summary>
    public sealed class People
    {
        public InvitePeoplePerson Clara { get; } = Person("Clara Weiss", "Brand designer", 47);
        public InvitePeoplePerson Mateus { get; } = Person("Mateus Rocha", "Backend engineer", 13);
        public InvitePeoplePerson Nadia { get; } = Person("Nadia Petrov", "Data engineer", 44);
        public InvitePeoplePerson Hugo { get; } = Person("Hugo Lambert", "Support lead", 59);
        public InvitePeoplePerson Elif { get; } = Person("Elif Kaya", "Sales manager", 49);
        public InvitePeoplePerson Ravi { get; } = Person("Ravi Menon", "Product manager", 53);
        public InvitePeoplePerson Sara { get; } = Person("Sara Lindqvist", "QA engineer", 32);
        public InvitePeoplePerson Diego { get; } = Person("Diego Alves", "iOS engineer", 12);
        public InvitePeoplePerson Mina { get; } = Person("Mina Cho", "Content writer", 27);
        public InvitePeoplePerson Jonas { get; } = Person("Jonas Berg", "Finance analyst", 60);
        public InvitePeoplePerson Iris { get; } = Person("Iris Moreau", "Head of design", 45);
        public InvitePeoplePerson Kenji { get; } = Person("Kenji Ito", "Engineering lead", 68);
        public InvitePeoplePerson Lena { get; } = Person("Lena Fischer", "Product designer", 5);
        public InvitePeoplePerson Omar { get; } = Person("Omar Haddad", "Platform engineer", 51);
        public InvitePeoplePerson Priya { get; } = Person("Priya Nair", "Operations lead", 20);
        public InvitePeoplePerson Theo { get; } = Person("Theo Grant", "Account executive", 57);

        /// <summary>Everyone, in suggestion order: the ten who can be invited, then the team and the invited person.</summary>
        public InvitePeoplePerson[] Directory =>
            [Clara, Mateus, Nadia, Hugo, Elif, Ravi, Sara, Diego, Mina, Jonas, Iris, Kenji, Lena, Omar, Priya, Theo];

        /// <summary>The team, front first.</summary>
        public InvitePeoplePerson[] Team => [Iris, Kenji, Lena, Omar, Priya];
    }

    /// <summary>The three roles; Member is the default.</summary>
    public static InvitePeopleRole[] Roles() =>
    [
        new("Admin", "People, billing and settings"),
        new("Member", "Works on every project", isDefault: true),
        new("Guest", "Sees only shared projects"),
    ];

    /// <summary>Basic (10 seats), then Growth (25 seats, the largest).</summary>
    public static InvitePeoplePlan[] Plans() => [new("Basic", 10), new("Growth", 25)];

    /// <summary>
    /// Fills <paramref name="block"/> with the Lumen Labs data: the directory, the team, Theo's invite from three days ago,
    /// the roles, the plans and the sample list. Returns the people so a demo can refer to them.
    /// </summary>
    public static People Fill(InvitePeople block)
    {
        var people = new People();
        block.Title = Title;
        block.Description = Description;
        block.WorkspaceName = Workspace;
        block.SampleText = SampleList;
        block.Directory.AddRange(people.Directory);
        block.Members.AddRange(people.Team);
        var roles = Roles();
        block.Roles.AddRange(roles);
        var plans = Plans();
        block.Plans.AddRange(plans);
        block.CurrentPlan = plans[0];
        block.PendingInvites.Add(new InvitePeoplePendingInvite(people.Theo, roles[1], "3 days ago"));
        return people;
    }
}
