using Avalonia.Headless.XUnit;
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
}
