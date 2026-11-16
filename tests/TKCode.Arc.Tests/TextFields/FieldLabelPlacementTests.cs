using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.TextFields;

/// <summary>Field.LabelPlacement / Field.LabelWidth on standalone fields (issue #131).</summary>
public class FieldLabelPlacementTests
{
    private const double Gap = 16;

    private static TextBlock LabelOf(Visual field)
        => field.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == Field.LabelPart);

    private static FieldLayout LayoutOf(Visual field) => field.GetVisualDescendants().OfType<FieldLayout>().First();

    private static Control FrameOf(Visual field)
        => field.GetVisualDescendants().OfType<Control>().First(c => c.Name is "PART_Frame" or "PART_Trigger");

    private static Point Origin(Visual element, Visual root) => element.TranslatePoint(default, root)!.Value;

    private static double CenterY(Visual element, Visual root) => Origin(element, root).Y + element.Bounds.Height / 2;

    [AvaloniaFact]
    public void Top_Is_The_Default_And_Keeps_The_Stacked_Layout()
    {
        var input = new Input { Label = "Email", Description = "We never share it.", Width = 320 };
        var window = TestHelpers.Show(input);
        try
        {
            LayoutOf(input).IsLabelLeft.ShouldBeFalse();
            input.LabelPlacement.ShouldBe(FormLabelPlacement.Top);
            double.IsNaN(input.LabelWidth).ShouldBeTrue();
            var label = LabelOf(input);
            var frame = FrameOf(input);
            Origin(label, input).Y.ShouldBe(0, 0.5);
            Origin(frame, input).X.ShouldBe(0, 0.5);
            // The label row keeps its 8 px gap to the frame.
            Origin(frame, input).Y.ShouldBe(label.Bounds.Height + 8, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(ControlSize.Small, 36)]
    [InlineData(ControlSize.Medium, 44)]
    [InlineData(ControlSize.Large, 50)]
    public void Left_With_A_Fixed_Width_Centres_The_Label_On_The_Frame(ControlSize size, double height)
    {
        var input = new Input
        {
            Label = "Email", Description = "We never share it.", ErrorMessage = "Required",
            LabelPlacement = FormLabelPlacement.Left, LabelWidth = 80, Size = size, Width = 360
        };
        var window = TestHelpers.Show(input);
        try
        {
            LayoutOf(input).IsLabelLeft.ShouldBeTrue();
            var label = LabelOf(input);
            var frame = FrameOf(input);
            Origin(label, input).X.ShouldBe(0, 0.5);
            Origin(frame, input).X.ShouldBe(80 + Gap, 0.5);
            Origin(frame, input).Y.ShouldBe(0, 0.5);
            frame.Bounds.Height.ShouldBe(height, 0.5);
            frame.Bounds.Width.ShouldBe(360 - 80 - Gap, 0.5);
            CenterY(label, input).ShouldBe(height / 2, 0.5);
            // Description and error stay in the editor column.
            foreach (var message in input.GetVisualDescendants().OfType<FieldMessage>())
            {
                Origin(message, input).X.ShouldBe(80 + Gap, 0.5);
                Origin(message, input).Y.ShouldBeGreaterThanOrEqualTo(height - 0.5);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Container_Lines_Up_Every_Field_At_The_Widest_Label()
    {
        var email = new Input { Label = "Email" };
        var region = new Select { Label = "Preferred region", ItemsSource = new[] { "EU", "US" } };
        var zone = new Combobox { Label = "Zone", ItemsSource = new[] { "UTC", "CET" } };
        var panel = new StackPanel { Width = 480, Spacing = 12, Children = { email, region, zone } };
        Field.SetLabelPlacement(panel, FormLabelPlacement.Left);
        var window = TestHelpers.Show(panel);
        try
        {
            var widest = LayoutOf(region).LabelColumnWidth;
            widest.ShouldBeGreaterThan(LabelOf(email).DesiredSize.Width);
            foreach (var field in new Control[] { email, region, zone })
            {
                LayoutOf(field).IsLabelLeft.ShouldBeTrue();
                Origin(FrameOf(field), field).X.ShouldBe(widest + Gap, 0.5);
            }

            // The column follows the labels: a shorter widest label narrows every field's column.
            region.Label = "Region";
            Dispatcher.UIThread.RunJobs();
            var narrower = LayoutOf(email).LabelColumnWidth;
            narrower.ShouldBeLessThan(widest);
            Origin(FrameOf(email), email).X.ShouldBe(Origin(FrameOf(zone), zone).X, 0.5);
            Origin(FrameOf(region), region).X.ShouldBe(narrower + Gap, 0.5);

            // A field that leaves the panel leaves the column.
            region.Label = "A much longer region label";
            Dispatcher.UIThread.RunJobs();
            var wide = LayoutOf(email).LabelColumnWidth;
            panel.Children.Remove(region);
            Dispatcher.UIThread.RunJobs();
            LayoutOf(email).LabelColumnWidth.ShouldBeLessThan(wide);

            // Back to Top.
            Field.SetLabelPlacement(panel, FormLabelPlacement.Top);
            Dispatcher.UIThread.RunJobs();
            LayoutOf(email).IsLabelLeft.ShouldBeFalse();
            Origin(FrameOf(email), email).X.ShouldBe(0, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Automatic_Column_Is_Capped_And_A_Fixed_Width_On_The_Container_Applies_To_All()
    {
        var first = new Input { Label = string.Join(" ", Enumerable.Repeat("Preferred contact method", 6)) };
        var second = new NumberField { Label = "Age" };
        var panel = new StackPanel { Width = 600, Children = { first, second } };
        Field.SetLabelPlacement(panel, FormLabelPlacement.Left);
        var window = TestHelpers.Show(panel);
        try
        {
            LayoutOf(first).LabelColumnWidth.ShouldBe(240, 0.5);
            Origin(FrameOf(second), second).X.ShouldBe(240 + Gap, 0.5);
            // The long label wraps in its column.
            LabelOf(first).Bounds.Height.ShouldBeGreaterThan(44);

            Field.SetLabelWidth(panel, 100);
            Dispatcher.UIThread.RunJobs();
            Origin(FrameOf(first), first).X.ShouldBe(100 + Gap, 0.5);
            Origin(FrameOf(second), second).X.ShouldBe(100 + Gap, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Shared_Size_Scope_Starts_Its_Own_Column()
    {
        var outer = new Input { Label = "A rather long label" };
        var inner = new Input { Label = "Age" };
        var section = new StackPanel { Children = { inner } };
        Grid.SetIsSharedSizeScope(section, true);
        var panel = new StackPanel { Width = 480, Children = { outer, section } };
        Field.SetLabelPlacement(panel, FormLabelPlacement.Left);
        var window = TestHelpers.Show(panel);
        try
        {
            LayoutOf(inner).LabelColumnWidth.ShouldBeLessThan(LayoutOf(outer).LabelColumnWidth);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Field_Without_A_Visible_Label_Keeps_The_Column_Of_Its_Scope()
    {
        var labelled = new Input { Label = "Email" };
        var bare = new Input { Label = "Hidden", IsLabelVisible = false };
        var panel = new StackPanel { Width = 480, Children = { labelled, bare } };
        Field.SetLabelPlacement(panel, FormLabelPlacement.Left);
        var window = TestHelpers.Show(panel);
        try
        {
            Origin(FrameOf(bare), bare).X.ShouldBe(Origin(FrameOf(labelled), labelled).X, 0.5);
            Origin(FrameOf(bare), bare).X.ShouldBeGreaterThan(Gap);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pickers_Follow_The_Placement_But_ColorPicker_Keeps_Its_Swatch_Label()
    {
        var date = new DatePicker { Label = "Start date" };
        var time = new TimePicker { Label = "Start time" };
        var color = new ColorPicker { Label = "Accent" };
        var panel = new StackPanel { Width = 480, Children = { date, time, color } };
        Field.SetLabelPlacement(panel, FormLabelPlacement.Left);
        var window = TestHelpers.Show(panel);
        try
        {
            LayoutOf(date).IsLabelLeft.ShouldBeTrue();
            LayoutOf(time).IsLabelLeft.ShouldBeTrue();
            LayoutOf(date).LabelColumnWidth.ShouldBe(LayoutOf(time).LabelColumnWidth, 0.5);
            Origin(FrameOf(date), date).X.ShouldBe(LayoutOf(date).LabelColumnWidth + Gap, 0.5);
            LayoutOf(color).IsLabelLeft.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Field_In_A_Form_Item_Leaves_The_Label_To_The_Item()
    {
        var input = new Input();
        var item = new FormItem { Label = "Name", Content = input };
        var panel = new StackPanel { Width = 480, Children = { new Form { Content = item } } };
        Field.SetLabelPlacement(panel, FormLabelPlacement.Left);
        var window = TestHelpers.Show(panel);
        try
        {
            LayoutOf(input).IsLabelLeft.ShouldBeFalse();
            // The item itself follows Field.LabelPlacement from the container.
            item.Classes.ShouldContain(FormItem.LabelLeftPseudoClass);
            Origin(input, item).X.ShouldBeGreaterThan(Gap);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Form_Item_Takes_A_Fixed_Field_Label_Width()
    {
        var input = new Input();
        var item = new FormItem { Label = "Name", Content = input, LabelPlacement = FormLabelPlacement.Left, LabelWidth = 120 };
        var window = TestHelpers.Show(new Form { Width = 480, Content = item });
        try
        {
            item.Classes.ShouldContain(FormItem.LabelLeftPseudoClass);
            Origin(input, item).X.ShouldBe(120 + Gap, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Field_In_Another_Controls_Template_Ignores_A_Container_Placement()
    {
        Input? nested = null;
        var host = new TemplatedControl
        {
            Template = new FuncControlTemplate((_, _) => nested = new Input { Label = "Inside" })
        };
        // A template that sets the placement on its own field keeps it.
        Input? own = null;
        var ownHost = new TemplatedControl
        {
            Template = new FuncControlTemplate((_, _) => own = new Input { Label = "Own", LabelPlacement = FormLabelPlacement.Left })
        };
        var outside = new Input { Label = "Outside" };
        var panel = new StackPanel { Width = 480, Children = { outside, host, ownHost } };
        Field.SetLabelPlacement(panel, FormLabelPlacement.Left);
        var window = TestHelpers.Show(panel);
        try
        {
            nested.ShouldNotBeNull();
            nested.TemplatedParent.ShouldBe(host);
            LayoutOf(outside).IsLabelLeft.ShouldBeTrue();
            LayoutOf(nested).IsLabelLeft.ShouldBeFalse();
            own.ShouldNotBeNull();
            LayoutOf(own).IsLabelLeft.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Select_Family_And_Text_Fields_Expose_The_Same_Properties()
    {
        // One attached property behind every CLR property, so XAML on the control and on a container are the same value.
        Select.LabelPlacementProperty.ShouldBeSameAs(Field.LabelPlacementProperty);
        Combobox.LabelPlacementProperty.ShouldBeSameAs(Field.LabelPlacementProperty);
        MultiSelect.LabelPlacementProperty.ShouldBeSameAs(Field.LabelPlacementProperty);
        TreeSelect.LabelPlacementProperty.ShouldBeSameAs(Field.LabelPlacementProperty);
        Cascader.LabelPlacementProperty.ShouldBeSameAs(Field.LabelPlacementProperty);
        MorphSelect.LabelPlacementProperty.ShouldBeSameAs(Field.LabelPlacementProperty);
        FieldTextBox.LabelPlacementProperty.ShouldBeSameAs(Field.LabelPlacementProperty);
        TimePicker.LabelWidthProperty.ShouldBeSameAs(Field.LabelWidthProperty);
        DateRangePicker.LabelWidthProperty.ShouldBeSameAs(Field.LabelWidthProperty);
        TagInput.LabelWidthProperty.ShouldBeSameAs(Field.LabelWidthProperty);
    }

    [AvaloniaFact]
    public void Every_Select_Family_Control_Lays_Its_Label_On_The_Left()
    {
        var controls = new Control[]
        {
            new Select { Label = "Select" }, new Combobox { Label = "Combobox" }, new MultiSelect { Label = "MultiSelect" },
            new TreeSelect { Label = "TreeSelect" }, new Cascader { Label = "Cascader" }, new MorphSelect { Label = "MorphSelect" },
            new Textarea { Label = "Textarea" }, new PasswordField { Label = "PasswordField" },
            new PasswordStrength { Label = "PasswordStrength" }, new SearchField { Label = "SearchField" },
            new PhoneInput { Label = "PhoneInput" }, new TagInput { Label = "TagInput" }, new DateRangePicker { Label = "Range" },
            new Field { Label = "Field", Content = new TextBlock { Text = "editor" } }
        };
        var panel = new StackPanel { Width = 520 };
        foreach (var control in controls)
        {
            panel.Children.Add(control);
        }
        Field.SetLabelPlacement(panel, FormLabelPlacement.Left);
        Field.SetLabelWidth(panel, 120);
        var window = TestHelpers.Show(panel);
        try
        {
            foreach (var control in controls)
            {
                var layout = LayoutOf(control);
                layout.IsLabelLeft.ShouldBeTrue(control.GetType().Name);
                layout.LabelColumnWidth.ShouldBe(120, 0.5, control.GetType().Name);
                var label = LabelOf(control);
                Origin(label, control).X.ShouldBe(0, 0.5, control.GetType().Name);
                var body = layout.Children[1];
                Origin(body, control).X.ShouldBe(120 + Gap, 0.5, control.GetType().Name);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
