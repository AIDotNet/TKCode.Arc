using Avalonia.Headless.XUnit;
using Avalonia.Media;
using TKCode.Arc.Icons.Lucide;

namespace TKCode.Arc.Tests.Icons;

/// <summary>
/// The Lucide generator compiles into this assembly only the icons it uses: Bell (named in C# below) and the
/// <c>&lt;LucideIcon Include="Sparkles;circle-dashed" /&gt;</c> items of the project file.
/// </summary>
public class LucideIconTests
{
    // Parsed from text so that C# naming them does not make the generator compile them in.
    private static LucideIconKind Kind(string name) => Enum.Parse<LucideIconKind>(name);

    [Fact]
    public void Icons_named_in_code_are_compiled_in()
    {
        LucideIconData.IsRegistered(LucideIconKind.Bell).ShouldBeTrue();
        LucideIconData.Get(LucideIconKind.Bell).ShouldStartWith("M");
    }

    [Fact]
    public void Declared_icons_are_compiled_in_by_either_name()
    {
        LucideIconData.IsRegistered(Kind("Sparkles")).ShouldBeTrue();
        LucideIconData.IsRegistered(Kind("CircleDashed")).ShouldBeTrue();
    }

    [Fact]
    public void Unused_icons_are_left_out()
    {
        LucideIconData.IsRegistered(Kind("ZodiacVirgo")).ShouldBeFalse();
        LucideIconData.Get(Kind("ZodiacVirgo")).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void Icon_draws_registered_kinds_and_nothing_for_missing_ones()
    {
        var icon = new LucideIcon { Kind = LucideIconKind.Bell };
        icon.Data.ShouldNotBeNull();
        icon.Data!.Bounds.Width.ShouldBeGreaterThan(0);

        icon.Kind = Kind("ZodiacVirgo");
        icon.Data.ShouldNotBeNull();
        icon.Data!.Bounds.Width.ShouldBe(0);
    }

    [AvaloniaFact]
    public void Every_shipped_path_parses()
    {
        // The generator's path data for every icon (tools/lucide/generate_lucide.py). A path that does not parse throws
        // when an app first shows the icon; "M14.305 16.53.923l-.382" (UserCog) once did.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TKCode.Arc.slnx")))
        {
            directory = directory.Parent;
        }
        directory.ShouldNotBeNull();
        var file = Path.Combine(directory!.FullName, "src", "TKCode.Arc.Icons.Lucide.Generators", "LucidePaths.txt");
        var broken = new List<string>();
        foreach (var line in File.ReadLines(file))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }
            var parts = line.Split('\t');
            try
            {
                StreamGeometry.Parse(parts[1]);
            }
            catch (Exception e) when (e is InvalidDataException or FormatException)
            {
                broken.Add(parts[0]);
            }
        }
        broken.ShouldBeEmpty();
    }
}
