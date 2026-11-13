using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shouldly;

namespace TKCode.Arc.Tests.Selects;

/// <summary>The select-like fields share the text fields' Small 36 / Medium 44 / Large 50 trigger heights (issue #115).</summary>
public class SelectFieldSizeTests
{
    public static TheoryData<string> Fields => ["Select", "Combobox", "MultiSelect", "TreeSelect", "Cascader"];

    private static Control Create(string name) => name switch
    {
        "Select" => new Select(),
        "Combobox" => new Combobox(),
        "MultiSelect" => new MultiSelect(),
        "TreeSelect" => new TreeSelect(),
        _ => new Cascader(),
    };

    private static ControlSize GetSize(Control c) => (ControlSize)c.GetValue(Field.SizeProperty)!;

    private static double TriggerHeight(Control field, ControlSize size)
    {
        field.SetValue(Field.SizeProperty, size);
        var window = TestHelpers.Show(new StackPanel { Children = { field } }, Motion.ArcMotionLevel.Off);
        Dispatcher.UIThread.RunJobs();
        var trigger = field.GetVisualDescendants().OfType<Border>()
            .First(b => b.Name is "PART_Trigger" or "PART_Frame");
        var height = trigger.Bounds.Height;
        window.Close();
        return height;
    }

    [AvaloniaTheory]
    [MemberData(nameof(Fields))]
    public void Size_Sets_The_Trigger_Height(string name)
    {
        TriggerHeight(Create(name), ControlSize.Small).ShouldBe(36);
        TriggerHeight(Create(name), ControlSize.Medium).ShouldBe(44);
        TriggerHeight(Create(name), ControlSize.Large).ShouldBe(50);
    }

    /// <summary>
    /// Without a label or a description the field is exactly as tall as its trigger, so it centers with a button of the
    /// same size. The Combobox's popup used to sit in the template's StackPanel and added its spacing below (#121).
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(Fields))]
    public void Without_Label_Or_Description_The_Field_Is_As_Tall_As_Its_Trigger(string name)
    {
        foreach (var (size, expected) in new[] { (ControlSize.Small, 36d), (ControlSize.Medium, 44d), (ControlSize.Large, 50d) })
        {
            var field = Create(name);
            field.SetValue(Field.SizeProperty, size);
            var window = TestHelpers.Show(new StackPanel { Children = { field } }, Motion.ArcMotionLevel.Off);
            Dispatcher.UIThread.RunJobs();
            field.DesiredSize.Height.ShouldBe(expected, $"{name} {size}");
            field.Bounds.Height.ShouldBe(expected, $"{name} {size}");
            window.Close();
        }
    }

    [AvaloniaTheory]
    [MemberData(nameof(Fields))]
    public void Small_Fits_A_36px_Slot_So_Text_Centers_With_A_Neighbouring_Label(string name)
    {
        var field = Create(name);
        field.Height = 36;
        TriggerHeight(field, ControlSize.Small).ShouldBe(36);
    }
}
