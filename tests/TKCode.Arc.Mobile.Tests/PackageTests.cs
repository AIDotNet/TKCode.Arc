using System.Reflection;
using Avalonia.Headless.XUnit;
using Avalonia.Metadata;
using Avalonia.VisualTree;

namespace TKCode.Arc.Mobile.Tests;

public class PackageTests
{
    [AvaloniaFact]
    public void Package_Registers_And_Its_Resources_Are_Found()
    {
        // The test app registered the package in its builder; a control resolves its theme only if registration worked.
        var button = new Button();
        var window = new Avalonia.Controls.Window { Content = button };
        window.Show();
        try
        {
            button.Template.ShouldNotBeNull();
            button.GetVisualChildren().ShouldNotBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Mobile_Assembly_Does_Not_Reference_The_Desktop_Arc_Package()
    {
        var references = typeof(Button).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        references.ShouldContain("TKCode.Arc.Core");
        references.ShouldNotContain("TKCode.Arc");
        references.ShouldNotContain("TKCode.Arc.Blocks");
        references.ShouldNotContain("TKCode.Arc.Special");
        references.ShouldNotContain("TKCode.Arc.Workflow");
    }

    [Fact]
    public void Mobile_Types_Do_Not_Derive_From_Desktop_Arc_Or_AtomUI_Controls()
    {
        foreach (var type in typeof(Button).Assembly.GetExportedTypes().Where(t => t.Namespace == "TKCode.Arc.Mobile"))
        {
            for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
            {
                baseType.Assembly.GetName().Name.ShouldNotBe("TKCode.Arc", $"{type.Name} derives from a desktop Arc control");
                baseType.Namespace?.StartsWith("AtomUI.Desktop.Controls", StringComparison.Ordinal)
                    .ShouldNotBe(true, $"{type.Name} derives from an AtomUI desktop control");
            }
        }
    }

    [Fact]
    public void Mobile_Has_Its_Own_Xaml_Namespace()
    {
        var definitions = typeof(Button).Assembly.GetCustomAttributes<XmlnsDefinitionAttribute>().ToList();
        definitions.ShouldContain(d => d.XmlNamespace == "https://github.com/AIDotNet/TKCode.Arc.Mobile" && d.ClrNamespace == "TKCode.Arc.Mobile");
        definitions.ShouldNotContain(d => d.XmlNamespace == "https://github.com/AIDotNet/TKCode.Arc");
    }

    [Fact]
    public void Public_Controls_Live_In_The_Mobile_Namespace()
    {
        var controls = typeof(Button).Assembly.GetExportedTypes()
            .Where(t => typeof(Avalonia.Controls.Control).IsAssignableFrom(t)).Select(t => t.Name).Order().ToList();
        controls.ShouldBe(["ActionSheet", "ActionSheetItem", "Button", "Checkbox", "Dialog", "IconButton", "ListGroup", "ListItem", "Navbar", "NavigationStack", "Page", "PullToRefresh", "Radio", "Searchbar", "Segmented", "SegmentedItem", "Sheet", "Switch", "Tabbar", "TabbarItem", "TextField", "Toast", "Toolbar"]);
    }
}
