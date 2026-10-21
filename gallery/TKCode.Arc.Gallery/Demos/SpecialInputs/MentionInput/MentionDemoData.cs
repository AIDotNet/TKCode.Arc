namespace TKCode.Arc.Gallery.Demos;

/// <summary>The people and channels that the MentionInput demos suggest.</summary>
public static class MentionDemoData
{
    public static IReadOnlyList<MentionPerson> People { get; } =
    [
        new("u_maya", "Maya Chen") { Role = "Product designer" },
        new("u_jordan", "Jordan Lee") { Role = "Engineering manager" },
        new("u_priya", "Priya Natarajan") { Role = "Frontend engineer" },
        new("u_sam", "Sam Okafor") { Role = "Data scientist" },
        new("u_alex", "Alex Rivera") { Role = "Support lead" },
        new("u_emma", "Emma Schultz") { Role = "Marketing" },
        new("u_noah", "Noah Kim") { Role = "Design systems" }
    ];

    public static IReadOnlyList<MentionChannel> Channels { get; } =
    [
        new("c_design", "design") { Description = "Critiques, reviews and handoff", Members = 48 },
        new("c_engineering", "engineering") { Description = "Builds, deploys and code review", Members = 132 },
        new("c_launch", "launch-q3") { Description = "Q3 launch coordination", Members = 19 },
        new("c_support", "support") { Description = "Customer escalations", Members = 41 },
        new("c_random", "random") { Description = "Everything else", Members = 256 }
    ];
}
