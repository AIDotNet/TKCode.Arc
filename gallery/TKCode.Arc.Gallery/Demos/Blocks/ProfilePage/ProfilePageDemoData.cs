using AtomUI.Controls;
using Avalonia;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// The ProfilePage demos' data (all invented): Rowan Ellery, a product designer at the made-up studio Fieldnote, with
/// three stats, seven activity rows in two groups and four projects. Portraits come from the placeholder service the
/// Gallery already uses; covers are gradients drawn in code.
/// </summary>
public static class ProfilePageDemoData
{
    public const string ProfileUrl = "https://fieldnote.app/@rowan";

    private static ImageSource? Portrait(int n) =>
        ImageSource.TryParse($"https://i.pravatar.cc/160?img={n}", out var source) ? source : null;

    private static IBrush Gradient(string from, string to, double angle = 0.35) => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, angle + 0.65, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.Parse(from), 0),
            new GradientStop(Color.Parse(to), 1),
        },
    };

    /// <summary>The person; <paramref name="withPortraits"/> false leaves the avatar to its initials.</summary>
    public static ProfileDetails Profile(bool withPortraits = true) => new()
    {
        Name = "Rowan Ellery",
        Handle = "rowan",
        Headline = "Product designer at Fieldnote",
        Location = "Bergen, Norway",
        Website = "rowanellery.studio",
        Bio = "I design tools for field researchers and care about honest defaults and quiet motion. Before Fieldnote I worked at Paperlane and Northpass.",
        JoinedText = "June 2020",
        Avatar = withPortraits ? Portrait(32) : null,
        CoverBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.Parse("#F3C9A6"), 0),
                new GradientStop(Color.Parse("#E59A7B"), 0.45),
                new GradientStop(Color.Parse("#7FA7C9"), 1),
            },
        },
    };

    /// <summary>Someone else's profile (shown read-only).</summary>
    public static ProfileDetails Colleague(bool withPortraits = true) => new()
    {
        Name = "Imani Okafor",
        Handle = "imani",
        Headline = "Research lead at Fieldnote",
        Location = "Lagos, Nigeria",
        Website = "imani.research",
        Bio = "I run the interview programme and turn notes into decisions the whole team can read.",
        JoinedText = "March 2021",
        Avatar = withPortraits ? Portrait(45) : null,
        CoverBrush = Gradient("#B9D8C2", "#5E8F7A"),
    };

    public static ProfileStat[] Stats() =>
    [
        new("Followers", 3107),
        new("Following", 212),
        new("Shipped", 41),
    ];

    public static ProfileActivity[] Activity() =>
    [
        new(ProfileActivityKind.Shipped, "shipped", "Color tokens v3", "3h", "This week",
            "New contrast pairs for charts and a calmer dark palette."),
        new(ProfileActivityKind.Commented, "commented on", "Empty cart screen", "Yesterday", "This week",
            "Could the illustration go, so the button leads?"),
        new(ProfileActivityKind.Reviewed, "approved", "Compact table mode", "Wed", "This week"),
        new(ProfileActivityKind.Created, "created", "Research board", "Mon", "This week",
            "One place for interview clips and the open questions."),
        new(ProfileActivityKind.Starred, "starred", "Spacing guide", "Aug 30", "Earlier"),
        new(ProfileActivityKind.Shipped, "shipped", "Glyph pack 1.4", "Aug 26", "Earlier",
            "286 icons redrawn on one grid."),
        new(ProfileActivityKind.Commented, "commented on", "Payment settings", "Aug 21", "Earlier"),
    ];

    public static ProfileProject[] Projects(bool withPortraits = true)
    {
        ProfileCollaborator Person(string name, int img) => new(name, withPortraits ? Portrait(img) : null);
        var kit = new ProfileProject("Fieldnote UI kit", "Components, tokens and usage notes shared by every product team.",
            "Updated 3h ago", 418, isStarred: true) { Id = "kit", CoverBrush = Gradient("#E8E4DC", "#B8B1A4") };
        kit.Collaborators.AddRange([Person("Imani Okafor", 45), Person("Theo Brandt", 12), Person("Lena Park", 5)]);
        var study = new ProfileProject("Signup study", "Interview notes and flows for the shorter signup we are testing.",
            "Updated Tuesday", 34, visibility: ProfileProjectVisibility.Private) { Id = "study", CoverBrush = Gradient("#CFE3D8", "#7FAF98") };
        study.Collaborators.AddRange([Person("Theo Brandt", 12), Person("Mara Quinn", 20)]);
        var motion = new ProfileProject("Animation rules", "When things move, how quickly and how reduced motion changes it.",
            "Updated Aug 30", 205) { Id = "motion", CoverBrush = Gradient("#F0B48A", "#C8553D") };
        motion.Collaborators.Add(Person("Lena Park", 5));
        var glyphs = new ProfileProject("Glyph pack 1.4", "A redrawn icon family on one stroke width and one grid.",
            "Updated Aug 26", 96) { Id = "glyphs", CoverBrush = Gradient("#B7C9E2", "#5B7BA6") };
        glyphs.Collaborators.AddRange([Person("Mara Quinn", 20), Person("Imani Okafor", 45)]);
        return [kit, study, motion, glyphs];
    }

    /// <summary>Fills a page with the sample stats, activity and projects.</summary>
    public static void Fill(ProfilePage page, bool withPortraits = true)
    {
        page.Profile = Profile(withPortraits);
        page.ProfileUrl = ProfileUrl;
        page.Stats.AddRange(Stats());
        page.Activity.AddRange(Activity());
        page.Projects.AddRange(Projects(withPortraits));
    }
}
