using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>
/// What the InstallSnippet demos show: the real install steps of this library's AI components (the
/// <c>TKCode.Arc.Special</c> package at the version in <c>build/Versions.props</c>; keep the two in step on release), in three
/// ways to add a package. Step 2 stores the key of the reader's own model provider, which their app uses for its model
/// calls; Arc's components never see it. Generate key only invents demo keys: nothing is created or sent.
/// </summary>
internal static class InstallSnippetDemoData
{
    /// <summary>The package version the snippets install (build/Versions.props).</summary>
    public const string ArcVersion = "0.3.2-preview.1";

    /// <summary>The version label at the top right.</summary>
    public const string Version = "v" + ArcVersion;

    /// <summary>The line under the title.</summary>
    public const string Description = "Three steps to your first AI screen.";

    /// <summary>The prefix of an invented key: obviously not a real key.</summary>
    public const string KeyPrefix = "demo_";

    /// <summary>A key the signed-in reader already has (invented).</summary>
    public const string SignedInKey = "demo_4f7k2q9xm1p8w3z6t0b5";

    private const string Program =
        "AppBuilder.Configure<App>()\n" +
        "    .UsePlatformDetect()\n" +
        "    .UseArc(arc => arc\n" +
        "        .UseArcControls()\n" +
        "        .UseArcSpecial());";

    private const string ProgramDescription = "Register Arc and its AI components in Program.cs.";

    /// <summary>Three framework tabs, fresh for every card: the .NET CLI, a PackageReference and central package management.</summary>
    public static IReadOnlyList<InstallSnippetFramework> Frameworks =>
    [
        Framework("dotnet", ".NET CLI", ArcIcons.Terminal,
            new InstallSnippetFile("Terminal", "dotnet add package TKCode.Arc.Special --version " + ArcVersion,
                "Add the package to your Avalonia app."),
            new InstallSnippetFile("Terminal", "dotnet user-secrets set \"Chat:ApiKey\" \"{{key}}\"",
                "Keep your model provider's key in user secrets, out of the repository.")),
        Framework("csproj", "PackageReference", ArcIcons.FileCode,
            new InstallSnippetFile("MyApp.csproj", "<PackageReference Include=\"TKCode.Arc.Special\" Version=\"" + ArcVersion + "\" />",
                "Reference the package in your project file.", "xml"),
            new InstallSnippetFile("appsettings.Development.json", "{\n  \"Chat\": { \"ApiKey\": \"{{key}}\" }\n}",
                "Keep the key in development settings. Never commit it.", "json")),
        Framework("cpm", "Central packages", ArcIcons.Package,
            new InstallSnippetFile("Directory.Packages.props",
                "<PackageVersion Include=\"TKCode.Arc.Special\" Version=\"" + ArcVersion + "\" />",
                "Pin the version once for the whole solution.", "xml"),
            new InstallSnippetFile(".env", "Chat__ApiKey={{key}}", "Or pass the key as an environment variable.")),
    ];

    private static InstallSnippetFramework Framework(string id, string label, Geometry mark, InstallSnippetFile install,
        InstallSnippetFile key) =>
        new(id, label)
        {
            Mark = mark,
            Install = install,
            Key = key,
            Code = new InstallSnippetFile("Program.cs", Program, ProgramDescription),
        };
}
