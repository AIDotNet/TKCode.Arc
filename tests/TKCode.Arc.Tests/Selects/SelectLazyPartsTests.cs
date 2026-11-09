using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using static TKCode.Arc.Tests.FocusRingInput;

namespace TKCode.Arc.Tests.Selects;

/// <summary>Parts built on demand (menus, search boxes, focus rings, range readouts) exist when needed and look the same.</summary>
public class SelectLazyPartsTests
{
    private static readonly string[] s_items = ["One", "Two", "Three"];

    private static Window Show(Control control)
    {
        var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20, 60, 20, 20), Children = { control } });
        window.Width = 600;
        window.Height = 700;
        Pump();
        return window;
    }

    private static object? Resource(Control control, object key)
    {
        control.TryFindResource(key, out var value).ShouldBeTrue();
        return value;
    }

    [AvaloniaFact]
    public void Select_Builds_Its_Menu_On_The_First_Open_With_The_Theme_Values()
    {
        var select = new Select { Width = 300, ItemsSource = s_items };
        var window = Show(select);
        try
        {
            var popup = TestHelpers.Descendant<ArcPopup>(select);
            popup.Child.ShouldBeNull();
            select.IsOpen = true;
            Pump();
            popup.Child.ShouldNotBeNull();
            var surface = TestHelpers.Descendant<Border>(window, Select.SurfacePart);
            surface.CornerRadius.ShouldBe((CornerRadius)Resource(surface, SelectTokens.Key(SelectTokenKind.MenuCornerRadius))!);
            surface.Padding.ShouldBe((Thickness)Resource(surface, SelectTokens.Key(SelectTokenKind.MenuPadding))!);
            surface.Width.ShouldBe(300);
            var options = TestHelpers.Descendant<SelectOptionView>(window);
            options.RowHeight.ShouldBe((double)Resource(options, SelectTokens.Key(SelectTokenKind.OptionHeight))!);
            options.OptionCount.ShouldBe(3);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Combobox_And_Cascader_Build_Their_Menus_On_The_First_Open()
    {
        var combobox = new Combobox { Width = 300, ItemsSource = s_items };
        var cascader = new Cascader { Width = 300, ItemsSource = s_items };
        var window = TestHelpers.Show(new StackPanel { Children = { combobox, cascader } });
        try
        {
            Pump();
            TestHelpers.Descendant<ArcPopup>(combobox).Child.ShouldBeNull();
            TestHelpers.Descendant<ArcPopup>(cascader).Child.ShouldBeNull();
            combobox.IsOpen = true;
            Pump();
            TestHelpers.Descendant<ArcPopup>(combobox).Child.ShouldNotBeNull();
            TestHelpers.Descendant<Border>(window, Combobox.SurfacePart).IsEffectivelyVisible.ShouldBeTrue();
            combobox.IsOpen = false;
            Pump();
            cascader.IsOpen = true;
            Pump();
            TestHelpers.Descendant<ArcPopup>(cascader).Child.ShouldNotBeNull();
            cascader.ColumnsPanel!.Columns.Count.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MultiSelect_Has_A_Search_Box_Only_When_Searchable()
    {
        var plain = new MultiSelect { Width = 300, ItemsSource = s_items };
        var window = Show(plain);
        try
        {
            plain.IsOpen = true;
            Pump();
            var surface = TestHelpers.Descendant<Border>(window, MultiSelect.SurfacePart);
            surface.GetVisualDescendants().OfType<TextBox>().ShouldBeEmpty();

            plain.IsSearchable = true;
            Pump();
            surface.GetVisualDescendants().OfType<TextBox>().ShouldHaveSingleItem();
            TestHelpers.Descendant<Border>(surface, "PART_SearchRow").IsEffectivelyVisible.ShouldBeTrue();

            plain.IsSearchable = false;
            Pump();
            TestHelpers.Descendant<Border>(surface, "PART_SearchRow").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MorphSelect_Builds_Its_Search_Row_Only_When_It_Opens_Searchable()
    {
        var select = new MorphSelect { Label = "Zone", ItemsSource = s_items, SearchMode = MorphSelectSearchMode.Off };
        var window = Show(select);
        try
        {
            select.IsOpen = true;
            Pump();
            select.Surface!.GetVisualDescendants().OfType<TextBox>().ShouldBeEmpty();
            select.IsOpen = false;
            Pump();
            select.SearchMode = MorphSelectSearchMode.On;
            select.IsOpen = true;
            Pump();
            select.Surface!.GetVisualDescendants().OfType<TextBox>().ShouldHaveSingleItem();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Focus_Rings_Appear_On_Keyboard_Focus_Only()
    {
        Control[] fields =
        [
            new MultiSelect { Width = 300, ItemsSource = s_items },
            new Cascader { Width = 300, ItemsSource = s_items },
            new MorphSelect { Label = "Zone", ItemsSource = s_items },
            new DateRangePicker(),
            new ColorPicker()
        ];
        foreach (var field in fields)
        {
            var other = new Button { Content = "Other" };
            var window = TestHelpers.Show(new StackPanel { Margin = new Thickness(20, 60, 20, 20), Children = { field, other } });
            Pump();
            try
            {
                TestHelpers.IsFocusRingVisible(field).ShouldBeFalse();
                field.GetVisualDescendants().OfType<Border>().Any(b => b.Name == LazyFocusRing.PartName).ShouldBeFalse(field.GetType().Name);
                field.Focus(NavigationMethod.Tab);
                Pump();
                TestHelpers.IsFocusRingVisible(field).ShouldBeTrue(field.GetType().Name);
                other.Focus(NavigationMethod.Tab);
                Pump();
                TestHelpers.IsFocusRingVisible(field).ShouldBeFalse(field.GetType().Name);
            }
            finally
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public void MorphSelect_Ring_Hides_While_Open()
    {
        var select = new MorphSelect { Label = "Zone", ItemsSource = s_items };
        var window = Show(select);
        try
        {
            select.Focus(NavigationMethod.Tab);
            Pump();
            TestHelpers.IsFocusRingVisible(select).ShouldBeTrue();
            var ring = TestHelpers.Descendant<Border>(select, LazyFocusRing.PartName);
            select.IsOpen = true;
            Pump();
            ring.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ColorPicker_Ring_Takes_The_Radius_Of_Its_Size()
    {
        var picker = new ColorPicker();
        var window = Show(picker);
        try
        {
            picker.Focus(NavigationMethod.Tab);
            Pump();
            var ring = TestHelpers.Descendant<Border>(picker, LazyFocusRing.PartName);
            ring.CornerRadius.ShouldBe((CornerRadius)Resource(picker, ColorPickerTokens.Key(ColorPickerTokenKind.TriggerFocusRingRadius))!);
            picker.Size = ControlSize.Small;
            Pump();
            ring = TestHelpers.Descendant<Border>(picker, LazyFocusRing.PartName);
            ring.CornerRadius.ShouldBe((CornerRadius)Resource(picker, ColorPickerTokens.Key(ColorPickerTokenKind.TriggerFocusRingRadiusSM))!);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slider_Builds_The_Range_Readout_When_It_Becomes_A_Range()
    {
        var slider = new Slider { Label = "Price", Width = 322, Value = 10, LowerValue = 20, UpperValue = 70 };
        var window = Show(slider);
        try
        {
            slider.GetVisualDescendants().OfType<Control>().Any(c => c.Name == Slider.ReadoutHighPart).ShouldBeFalse();
            slider.IsRange = true;
            Pump();
            var high = TestHelpers.Descendant<SliderNumberText>(slider, Slider.ReadoutHighPart);
            high.IsVisible.ShouldBeTrue();
            high.Text.ShouldBe("70");
            var dash = TestHelpers.Descendant<TextBlock>(slider, Slider.ReadoutDashPart);
            dash.IsVisible.ShouldBeTrue();
            var row = dash.GetVisualParent().ShouldBeAssignableTo<Panel>()!;
            row.Children.IndexOf(TestHelpers.Descendant<SliderNumberText>(slider, Slider.ReadoutLowPart)).ShouldBe(0);
            row.Children.IndexOf(dash).ShouldBe(1);
            row.Children.IndexOf(high).ShouldBe(2);
            slider.IsRange = false;
            Pump();
            high.IsVisible.ShouldBeFalse();
            dash.IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Slider_Commits_At_Once_And_The_Readout_Follows_Within_The_Frame()
    {
        var slider = new Slider { Label = "Volume", Width = 322, Value = 10 };
        var window = Show(slider);
        try
        {
            var rail = slider.Rail!;
            var origin = rail.TranslatePoint(default, window)!.Value;
            var y = origin.Y + rail.Bounds.Height / 2;
            window.MouseDown(new Point(origin.X + 11 + 150, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.MouseMove(new Point(origin.X + 11 + 240, y));
            slider.Value.ShouldBe(80);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.Descendant<SliderNumberText>(slider, Slider.ReadoutLowPart).Text.ShouldBe("80");
            Avalonia.Automation.AutomationProperties.GetItemStatus(slider.Rail!.Thumbs[0]).ShouldBe("80");
            window.MouseUp(new Point(origin.X + 11 + 240, y), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
        }
        finally
        {
            window.Close();
        }
    }
}
