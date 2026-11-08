using TKCode.Arc.DesignTokens;
using TKCode.Arc.Gallery.Localization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>A project on the open source page. Versions and licenses are stated as they are in each repository.</summary>
internal sealed record GalleryProject(
    string Name,
    GalleryShellLangResourceKind Kind,
    GalleryShellLangResourceKind Text,
    string Version,
    bool IsStable,
    string License,
    Uri Url,
    GalleryShellLangResourceKind[] Points,
    string Code,
    string Install);

/// <summary>
/// The open source projects beside the components (<c>/projects</c>): what each one is, its state and how to start. To add
/// one, add a line to <see cref="Projects"/> and its text to the shell text.
/// </summary>
internal sealed class GalleryProjectsView : UserControl
{
    public static IReadOnlyList<GalleryProject> Projects { get; } =
    [
        new("StreamMark", GalleryShellLangResourceKind.ProjectKindMarkdown, GalleryShellLangResourceKind.HomeStreamMarkText,
            GallerySite.StreamMarkVersion, true, "MIT", GalleryStreamMarkView.RepositoryUrl,
            [GalleryShellLangResourceKind.StreamMarkPoint1, GalleryShellLangResourceKind.StreamMarkPoint2, GalleryShellLangResourceKind.StreamMarkPoint3],
            """
            markdown.Append(chunk);   // as tokens arrive
            markdown.Complete();      // when the reply ends
            """,
            "dotnet add package TKCode.StreamMark.Avalonia"),
        new("Jaunt", GalleryShellLangResourceKind.ProjectKindScript, GalleryShellLangResourceKind.ProjectJauntText,
            "0.1.0-alpha.1", false, "MIT", new Uri("https://github.com/AIDotNet/Jaunt"),
            [GalleryShellLangResourceKind.ProjectJauntPoint1, GalleryShellLangResourceKind.ProjectJauntPoint2, GalleryShellLangResourceKind.ProjectJauntPoint3, GalleryShellLangResourceKind.ProjectJauntPoint4],
            """
            using var engine = new JsEngine();
            engine.SetValue("price", 100.0);
            engine.SetFunction("tax", (double amount) => amount * 0.13);
            double total = engine
                .Evaluate("price * 2 + tax(price * 2)")
                .AsNumber();   // 226
            """,
            string.Empty),
        new("Spyglass", GalleryShellLangResourceKind.ProjectKindDebug, GalleryShellLangResourceKind.ProjectSpyglassText,
            "0.2.3", true, "MIT", new Uri("https://github.com/AIDotNet/Spyglass"),
            [GalleryShellLangResourceKind.ProjectSpyglassPoint1, GalleryShellLangResourceKind.ProjectSpyglassPoint2, GalleryShellLangResourceKind.ProjectSpyglassPoint3, GalleryShellLangResourceKind.ProjectSpyglassPoint4],
            """
            AppBuilder.Configure<App>().UsePlatformDetect()
            #if DEBUG
                .UseSpyglass()
            #endif
            ;
            """,
            "dotnet tool install -g TKCode.Spyglass")
    ];

    private readonly GalleryShell _shell;

    public GalleryProjectsView(GalleryShell shell)
    {
        _shell = shell;
        var list = new StackPanel { Spacing = 16 };
        foreach (var project in Projects)
        {
            list.Children.Add(BuildCard(project));
        }
        var body = new StackPanel { Spacing = 24 };
        body.Children.Add(GallerySite.Heading(null, GallerySite.Reveal(GalleryShellLangResourceKind.NavProjects, TextLevel.H1), GalleryShellLangResourceKind.ProjectsText));
        body.Children.Add(list);

        var column = new StackPanel();
        column.Children.Add(body);
        column.Children.Add(GallerySiteFooter.Create(shell));
        Content = GallerySite.Page(column, out _);
    }

    private Control BuildCard(GalleryProject project)
    {
        var tags = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 6 };
        tags.Children.Add(new Badge { Tone = project.IsStable ? BadgeTone.Success : BadgeTone.Warning, Content = project.Version });
        tags.Children.Add(new Badge { Tone = BadgeTone.Neutral, Content = project.License });

        var kind = GallerySite.Eyebrow(project.Kind);
        var text = new StackPanel { Spacing = 14 };
        text.Children.Add(kind);
        text.Children.Add(GalleryUi.Text(project.Name, ArcDesignTokenKind.Foreground, 32, FontWeight.SemiBold));
        text.Children.Add(tags);
        text.Children.Add(GallerySite.Text(project.Text, ArcDesignTokenKind.TextSecondary, 15));
        text.Children.Add(GallerySite.Checks(project.Points.Select(point => (Control)GallerySite.Text(point)).ToArray()));

        var actions = new List<Control>();
        if (project.Name == "StreamMark")
        {
            actions.Add(GallerySite.Action(GalleryShellLangResourceKind.StreamMarkLearn, ButtonVariant.Primary, () => _shell.Navigate(GalleryRoute.StreamMark), ArcIcons.ArrowRight, ControlSize.Small));
        }
        actions.Add(GallerySite.Action(GalleryShellLangResourceKind.StreamMarkGitHub, ButtonVariant.Secondary, () => GallerySite.Open(this, project.Url), ArcIcons.ArrowUpRight, ControlSize.Small));
        text.Children.Add(GallerySite.Actions(actions.ToArray()));

        var visual = new StackPanel { Spacing = 12 };
        visual.Children.Add(new CodeBlock { Code = project.Code, Language = "cs" });
        if (project.Install.Length > 0)
        {
            visual.Children.Add(GallerySite.Install(project.Install));
        }

        var card = new GalleryTwoPane { StackBelow = 820, Spacing = 28 };
        card.Children.Add(text);
        card.Children.Add(visual);
        return GallerySite.Panel(card, padding: 24);
    }
}
