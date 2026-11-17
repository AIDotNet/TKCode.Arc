
namespace TKCode.Arc.Tests.Architecture;

/// <summary>
/// The public type names TKCode.Arc.Mobile shares with the desktop packages (#152). The names stay (the Mobile classes
/// live in their own namespace and xmlns, <c>arcm</c>); a C# file that imports both namespaces aliases one side, e.g.
/// <c>using MobileButton = TKCode.Arc.Mobile.Button;</c>. A new shared name is a deliberate change of this list, and new Mobile types avoid desktop names.
/// </summary>
public class MobileClassNameTests
{
    private static readonly string[] s_shared =
        ["Button", "ButtonVariant", "Checkbox", "Dialog", "RadioCard", "RadioCardOption", "RadioCards", "Switch", "Toast"];

    [Fact]
    public void Mobile_Shares_Only_The_Listed_Class_Names_With_The_Desktop_Packages()
    {
        var desktop = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assembly in new[] { typeof(ArcDesign).Assembly, typeof(TKCode.Arc.Button).Assembly })
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                desktop.Add(type.Name);
            }
        }
        var shared = typeof(TKCode.Arc.Mobile.Sheet).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == "TKCode.Arc.Mobile" && desktop.Contains(t.Name))
            .Select(t => t.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        shared.ShouldBe(s_shared);
    }
}
