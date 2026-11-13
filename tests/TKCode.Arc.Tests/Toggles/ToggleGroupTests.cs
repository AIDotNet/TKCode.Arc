using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc.Tests.Toggles;

public class ToggleGroupTests
{
    private static ToggleGroup Group(ToggleGroupMode mode = ToggleGroupMode.Single) => new()
    {
        Label = "Style",
        Mode = mode,
        ItemsSource = new[]
        {
            new ToggleGroupOption("a", "A"),
            new ToggleGroupOption("b", "B"),
            new ToggleGroupOption("c", "C"),
            new ToggleGroupOption("d", "D")
        }
    };

    private static ToggleGroupItem Item(ToggleGroup group, int index) => (ToggleGroupItem)group.ContainerFromIndex(index)!;

    private static void Key(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window window, Control target)
    {
        var center = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Single_Mode_Moves_The_Pressed_State_And_Keeps_The_Pressed_Option()
    {
        var group = Group();
        var events = new List<(object? Value, bool Pressed, int Count)>();
        group.ValueChanged += (_, e) => events.Add((e.Value, e.IsPressed, e.Values.Count));
        var window = TestHelpers.Show(group);
        try
        {
            Click(window, Item(group, 1));
            group.SelectedValue.ShouldBe("b");
            Click(window, Item(group, 1));
            group.SelectedValue.ShouldBe("b");
            Click(window, Item(group, 2));
            group.SelectedValue.ShouldBe("c");
            group.SelectedValues.ShouldBe(["c"]);
            Item(group, 1).IsSelected.ShouldBeFalse();
            events.ShouldBe([("b", true, 1), ("c", true, 1)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Multiple_Mode_Toggles_Each_Option_And_Keeps_The_Order_Of_Pressing()
    {
        var group = Group(ToggleGroupMode.Multiple);
        var events = new List<(object? Value, bool Pressed)>();
        group.ValueChanged += (_, e) => events.Add((e.Value, e.IsPressed));
        var window = TestHelpers.Show(group);
        try
        {
            Click(window, Item(group, 2));
            Click(window, Item(group, 0));
            group.SelectedValues.ShouldBe(["c", "a"]);
            Item(group, 0).IsSelected.ShouldBeTrue();
            Item(group, 2).IsSelected.ShouldBeTrue();
            Click(window, Item(group, 2));
            group.SelectedValues.ShouldBe(["a"]);
            Item(group, 2).IsSelected.ShouldBeFalse();
            events.ShouldBe([("c", true), ("a", true), ("c", false)]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Code_Sets_The_Values_Without_Raising_The_Event()
    {
        var group = Group(ToggleGroupMode.Multiple);
        var raised = 0;
        group.ValueChanged += (_, _) => raised++;
        group.SelectedValues = ["b", "d"];
        var window = TestHelpers.Show(group);
        try
        {
            Item(group, 1).IsSelected.ShouldBeTrue();
            Item(group, 3).IsSelected.ShouldBeTrue();
            group.SelectedValues = ["a"];
            Dispatcher.UIThread.RunJobs();
            Item(group, 0).IsSelected.ShouldBeTrue();
            Item(group, 1).IsSelected.ShouldBeFalse();
            raised.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Comma_Separated_List_In_Xaml_Presses_Inline_Options()
    {
        var group = AvaloniaRuntimeXamlLoader.Parse<ToggleGroup>("""
            <arc:ToggleGroup xmlns="https://github.com/avaloniaui" xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                             Mode="Multiple" SelectedValues="b,c">
                <arc:ToggleGroupOption Value="a" Label="A" />
                <arc:ToggleGroupOption Value="b" Label="B" />
                <arc:ToggleGroupOption Value="c">Sea</arc:ToggleGroupOption>
            </arc:ToggleGroup>
            """);
        var window = TestHelpers.Show(group);
        try
        {
            Item(group, 0).IsSelected.ShouldBeFalse();
            Item(group, 1).IsSelected.ShouldBeTrue();
            Item(group, 2).IsSelected.ShouldBeTrue();
            Item(group, 2).Content.ShouldBe("Sea");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Value_With_No_Option_Yet_Applies_When_The_Option_Arrives()
    {
        var options = new Avalonia.Collections.AvaloniaList<ToggleGroupOption> { new("a", "A") };
        var group = new ToggleGroup { Mode = ToggleGroupMode.Multiple, ItemsSource = options, SelectedValues = ["later", "a"] };
        var window = TestHelpers.Show(group);
        try
        {
            Item(group, 0).IsSelected.ShouldBeTrue();
            options.Add(new ToggleGroupOption("later", "Later"));
            Dispatcher.UIThread.RunJobs();
            group.UpdateLayout();
            Item(group, 1).IsSelected.ShouldBeTrue();
            group.SelectedValues.ShouldBe(["later", "a"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Pressed_Neighbours_Join_Into_One_Shape_Only_In_Multiple_Mode()
    {
        var group = Group(ToggleGroupMode.Multiple);
        group.SelectedValues = ["a", "b", "d"];
        var window = TestHelpers.Show(group);
        try
        {
            bool Has(int index, string pseudo) => Item(group, index).Classes.Contains(pseudo);
            Has(0, ToggleGroupItem.JoinEndPseudoClass).ShouldBeTrue();
            Has(0, ToggleGroupItem.JoinStartPseudoClass).ShouldBeFalse();
            Has(1, ToggleGroupItem.JoinStartPseudoClass).ShouldBeTrue();
            Has(1, ToggleGroupItem.JoinEndPseudoClass).ShouldBeFalse();
            Has(3, ToggleGroupItem.JoinStartPseudoClass).ShouldBeFalse();

            var fill = TestHelpers.Descendant<ToggleGroupFill>(Item(group, 0), "PART_Fill");
            fill.IsVisible.ShouldBeTrue();
            fill.JoinEnd.ShouldBe(1);
            fill.Gap.ShouldBe(2);
            TestHelpers.Descendant<ToggleGroupFill>(Item(group, 1), "PART_Fill").JoinStart.ShouldBe(1);

            group.SelectedValues = ["a", "b", "c", "d"];
            Dispatcher.UIThread.RunJobs();
            Has(2, ToggleGroupItem.JoinStartPseudoClass).ShouldBeTrue();
            Has(2, ToggleGroupItem.JoinEndPseudoClass).ShouldBeTrue();
            Has(3, ToggleGroupItem.JoinStartPseudoClass).ShouldBeTrue();

            group.Mode = ToggleGroupMode.Single;
            Dispatcher.UIThread.RunJobs();
            Has(0, ToggleGroupItem.JoinEndPseudoClass).ShouldBeFalse();
            TestHelpers.Descendant<ToggleGroupFill>(Item(group, 0), "PART_Fill").IsVisible.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Single_Mode_Has_One_Gliding_Fill_And_Multiple_Mode_Has_None()
    {
        var group = Group();
        group.SelectedValue = "b";
        var window = TestHelpers.Show(group);
        try
        {
            var indicator = TestHelpers.Descendant<GlidingIndicator>(group, "PART_Selection");
            indicator.IsVisible.ShouldBeTrue();
            indicator.HasTarget.ShouldBeTrue();
            var item = Item(group, 1);
            indicator.CurrentRect.X.ShouldBe(item.TranslatePoint(default, indicator)!.Value.X);
            indicator.CurrentRect.Width.ShouldBe(item.Bounds.Width);

            group.Mode = ToggleGroupMode.Multiple;
            Dispatcher.UIThread.RunJobs();
            indicator.IsVisible.ShouldBeFalse();
            indicator.HasTarget.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Arrows_Move_Focus_Without_Pressing_And_Skip_Disabled_Options()
    {
        var options = new[]
        {
            new ToggleGroupOption("a", "A"),
            new ToggleGroupOption("b", "B") { IsEnabled = false },
            new ToggleGroupOption("c", "C"),
        };
        var group = new ToggleGroup { Label = "Style", ItemsSource = options };
        var window = TestHelpers.Show(group);
        try
        {
            Item(group, 0).Focus(NavigationMethod.Tab);
            Key(window, PhysicalKey.ArrowRight);
            Item(group, 2).IsFocused.ShouldBeTrue();
            group.SelectedIndex.ShouldBe(-1);
            Key(window, PhysicalKey.ArrowRight);
            Item(group, 0).IsFocused.ShouldBeTrue();
            Key(window, PhysicalKey.End);
            Item(group, 2).IsFocused.ShouldBeTrue();
            Key(window, PhysicalKey.Home);
            Item(group, 0).IsFocused.ShouldBeTrue();
            Key(window, PhysicalKey.ArrowLeft);
            Item(group, 2).IsFocused.ShouldBeTrue();
            Key(window, PhysicalKey.Space);
            group.SelectedValue.ShouldBe("c");
            Key(window, PhysicalKey.Home);
            Key(window, PhysicalKey.Enter);
            group.SelectedValue.ShouldBe("a");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Tab_Stop_Is_The_Last_Focused_Then_The_First_Pressed_Then_The_First()
    {
        var group = Group(ToggleGroupMode.Multiple);
        var window = TestHelpers.Show(group);
        try
        {
            KeyboardNavigation.GetTabOnceActiveElement(group).ShouldBe(Item(group, 0));
            group.SelectedValues = ["c", "b"];
            Dispatcher.UIThread.RunJobs();
            KeyboardNavigation.GetTabOnceActiveElement(group).ShouldBe(Item(group, 2));
            Item(group, 3).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            KeyboardNavigation.GetTabOnceActiveElement(group).ShouldBe(Item(group, 3));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Keyboard_Focus_Shows_The_Ring_And_Pointer_Focus_Does_Not()
    {
        var group = Group();
        var window = TestHelpers.Show(group);
        try
        {
            Click(window, Item(group, 1));
            TestHelpers.IsFocusRingVisible(Item(group, 1)).ShouldBeFalse();
            Item(group, 2).Focus(NavigationMethod.Tab);
            Dispatcher.UIThread.RunJobs();
            TestHelpers.IsFocusRingVisible(Item(group, 2)).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Disabled_Group_Ignores_Input_And_Fades()
    {
        var group = Group();
        group.IsEnabled = false;
        var window = TestHelpers.Show(group);
        try
        {
            Click(window, Item(group, 1));
            group.SelectedIndex.ShouldBe(-1);
            group.Opacity.ShouldBe(0.52);
            Item(group, 1).Opacity.ShouldBe(0.45);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Sizes_Follow_Arc()
    {
        var group = Group();
        var window = TestHelpers.Show(group);
        try
        {
            Item(group, 0).Bounds.Height.ShouldBe(36);
            Item(group, 0).Bounds.Width.ShouldBeGreaterThanOrEqualTo(36);
            group.Size = ControlSize.Small;
            Dispatcher.UIThread.RunJobs();
            group.UpdateLayout();
            Item(group, 0).Bounds.Height.ShouldBe(28);
            Item(group, 0).Bounds.Width.ShouldBeGreaterThanOrEqualTo(28);
            group.FontSize.ShouldBe(12);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Fill_Tints_The_Surface_With_ControlOn()
    {
        var group = Group();
        var window = TestHelpers.Show(group);
        try
        {
            var surface = ArcTokens.GetColor(group, DesignTokens.ArcDesignTokenKind.Surface);
            var on = ArcTokens.GetColor(group, DesignTokens.ArcDesignTokenKind.ControlOn);
            group.PressedFill.ShouldBeAssignableTo<ISolidColorBrush>();
            ((ISolidColorBrush)group.PressedFill!).Color.ShouldBe(Oklch.MixOklab(surface, on, group.FillMix));
            group.FillMix.ShouldBe(0.11);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Icon_Option_Takes_Its_Name_And_Tooltip_From_The_Label()
    {
        var group = new ToggleGroup
        {
            ItemsSource = new[]
            {
                new ToggleGroupOption("bold", "Bold", ArcIcons.Bold),
                new ToggleGroupOption("plain", "Plain"),
                new ToggleGroupOption("titled", "Named", ArcIcons.Italic) { Title = "Tip" }
            }
        };
        var window = TestHelpers.Show(group);
        try
        {
            AutomationProperties.GetName(Item(group, 0)).ShouldBe("Bold");
            Tooltip.GetTip(Item(group, 0)).ShouldBe("Bold");
            Item(group, 0).Content.ShouldBeNull();
            TestHelpers.Descendant<ArcGlyph>(Item(group, 0), "PART_Icon").Size.ShouldBe(16);

            // Text that is already on screen needs no tooltip.
            Item(group, 1).Content.ShouldBe("Plain");
            Tooltip.GetTip(Item(group, 1)).ShouldBeNull();
            TestHelpers.Descendant<ArcGlyph>(Item(group, 1), "PART_Icon").IsVisible.ShouldBeFalse();

            Tooltip.GetTip(Item(group, 2)).ShouldBe("Tip");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Press_Goes_Down_In_90ms_And_Comes_Back_In_320ms_On_The_Content_Only()
    {
        var clock = new Motion.ManualFrameClock();
        using var _ = clock.Install();
        var group = Group();
        var window = TestHelpers.Show(group, Motion.ArcMotionLevel.Full);
        try
        {
            var item = Item(group, 0);
            var content = TestHelpers.Descendant<StackPanel>(item, ToggleGroupItem.ContentPart);
            var center = item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value;
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            void Advance(int ms)
            {
                clock.Advance(TimeSpan.FromMilliseconds(ms));
                Dispatcher.UIThread.RunJobs();
            }
            double Scale() => ((ScaleTransform)content.RenderTransform!).ScaleX;

            window.MouseDown(center, MouseButton.Left);
            Advance(40);
            Scale().ShouldBeInRange(0.92, 1);
            Advance(60);
            Scale().ShouldBe(0.92);

            window.MouseUp(center, MouseButton.Left);
            Advance(100);
            Scale().ShouldBeGreaterThan(0.92);
            Advance(240);
            Scale().ShouldBe(1);
            item.RenderTransform.ShouldBeNull();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reduced_Motion_Has_No_Press_Scale()
    {
        var group = Group();
        var window = TestHelpers.Show(group, Motion.ArcMotionLevel.Reduced);
        try
        {
            Click(window, Item(group, 0));
            TestHelpers.Descendant<StackPanel>(Item(group, 0), ToggleGroupItem.ContentPart).RenderTransform.ShouldBeNull();
            group.SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Options_Later_Changes_Reach_Its_Container()
    {
        var option = new ToggleGroupOption("a", "A");
        var group = new ToggleGroup { ItemsSource = new[] { option } };
        var window = TestHelpers.Show(group);
        try
        {
            option.Label = "Renamed";
            option.IsEnabled = false;
            Dispatcher.UIThread.RunJobs();
            Item(group, 0).Content.ShouldBe("Renamed");
            Item(group, 0).IsEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Automation_Reports_Radios_In_Single_Mode_And_Toggles_In_Multiple_Mode()
    {
        var single = Group();
        var multiple = Group(ToggleGroupMode.Multiple);
        var window = TestHelpers.Show(new StackPanel { Children = { single, multiple } });
        try
        {
            var group = ControlAutomationPeer.CreatePeerForElement(single);
            group.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            group.GetName().ShouldBe("Style");

            var radio = ControlAutomationPeer.CreatePeerForElement(Item(single, 1));
            radio.GetAutomationControlType().ShouldBe(AutomationControlType.RadioButton);
            radio.GetProvider<ISelectionItemProvider>()!.Select();
            single.SelectedValue.ShouldBe("b");

            var toggle = ControlAutomationPeer.CreatePeerForElement(Item(multiple, 2));
            toggle.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            var provider = toggle.GetProvider<IToggleProvider>()!;
            provider.Toggle();
            provider.ToggleState.ShouldBe(ToggleState.On);
            multiple.SelectedValues.ShouldBe(["c"]);
            provider.Toggle();
            provider.ToggleState.ShouldBe(ToggleState.Off);
        }
        finally
        {
            window.Close();
        }
    }

    public sealed record ColorOption(string Key, string Name)
    {
        public override string ToString() => Name;
    }

    public sealed class ColorState : System.ComponentModel.INotifyPropertyChanged
    {
        private object? _colorKey = "green";

        public object? ColorKey
        {
            get => _colorKey;
            set
            {
                _colorKey = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ColorKey)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    [AvaloniaFact]
    public void A_Custom_Value_Binding_Reports_Values_And_Keeps_The_Bound_Value()
    {
        // Issue #105: the values ignored SelectedValueBinding and the item itself was written back into SelectedValue.
        var state = new ColorState();
        var group = new ToggleGroup
        {
            Label = "Color",
            ItemsSource = new[] { new ColorOption("red", "Red"), new ColorOption("green", "Green"), new ColorOption("blue", "Blue") },
            SelectedValueBinding = new Binding(nameof(ColorOption.Key))
        };
        group.Bind(ToggleGroup.SelectedValueProperty, new Binding(nameof(ColorState.ColorKey)) { Source = state, Mode = BindingMode.TwoWay });
        var window = TestHelpers.Show(group);
        try
        {
            state.ColorKey.ShouldBe("green");
            group.SelectedIndex.ShouldBe(1);
            group.SelectedValues.ShouldBe(["green"]);

            object? reported = null;
            group.ValueChanged += (_, e) => reported = e.Value;
            Click(window, Item(group, 2));
            state.ColorKey.ShouldBe("blue");
            group.SelectedValues.ShouldBe(["blue"]);
            reported.ShouldBe("blue");

            state.ColorKey = "red";
            Dispatcher.UIThread.RunJobs();
            group.SelectedIndex.ShouldBe(0);
            group.SelectedValues.ShouldBe(["red"]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Data_Bound_Options_Take_Their_Icon_And_Name_From_A_Style()
    {
        var group = AvaloniaRuntimeXamlLoader.Parse<ToggleGroup>("""
            <arc:ToggleGroup xmlns="https://github.com/avaloniaui" xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                             Label="View" SelectedValueBinding="{Binding Key}">
                <arc:ToggleGroup.Styles>
                    <Style Selector="arc|ToggleGroupItem">
                        <Setter Property="Icon" Value="{Binding Glyph}" />
                        <Setter Property="AutomationProperties.Name" Value="{Binding Title}" />
                        <Setter Property="arc:Tooltip.Tip" Value="{Binding Title}" />
                    </Style>
                </arc:ToggleGroup.Styles>
                <arc:ToggleGroup.ItemTemplate>
                    <DataTemplate>
                        <Panel />
                    </DataTemplate>
                </arc:ToggleGroup.ItemTemplate>
            </arc:ToggleGroup>
            """);
        group.ItemsSource = new[] { new ViewChoice("list", "List", ArcIcons.List), new ViewChoice("grid", "Grid", ArcIcons.LayoutGrid) };
        group.SelectedValue = "grid";
        var window = TestHelpers.Show(group);
        try
        {
            Item(group, 1).IsSelected.ShouldBeTrue();
            Item(group, 0).Icon.ShouldBe(ArcIcons.List);
            AutomationProperties.GetName(Item(group, 0)).ShouldBe("List");
            ControlAutomationPeer.CreatePeerForElement(Item(group, 0)).GetName().ShouldBe("List");
            Tooltip.GetTip(Item(group, 1)).ShouldBe("Grid");
            Click(window, Item(group, 0));
            group.SelectedValue.ShouldBe("list");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void An_Item_Container_Theme_Based_On_The_Default_Keeps_The_Look()
    {
        var group = Group();
        var window = TestHelpers.Show(group);
        try
        {
            group.TryFindResource(typeof(ToggleGroupItem), out var found).ShouldBeTrue();
            var theme = new Avalonia.Styling.ControlTheme(typeof(ToggleGroupItem))
            {
                BasedOn = (Avalonia.Styling.ControlTheme)found!,
                Setters = { new Avalonia.Styling.Setter(Avalonia.Layout.Layoutable.MinWidthProperty, 80d) }
            };
            group.ItemContainerTheme = theme;
            Dispatcher.UIThread.RunJobs();
            Item(group, 0).MinWidth.ShouldBe(80);
            TestHelpers.Descendant<ToggleGroupFill>(Item(group, 0)).ShouldNotBeNull();
            Item(group, 0).Bounds.Width.ShouldBeGreaterThanOrEqualTo(80);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Press_Comes_From_The_Item_Tokens()
    {
        var group = Group();
        var window = TestHelpers.Show(group);
        try
        {
            var item = Item(group, 0);
            item.TryFindResource(ToggleGroupItemTokens.Key(ToggleGroupItemTokenKind.PressScale), out var scale).ShouldBeTrue();
            scale.ShouldBe(0.92);
            item.TryFindResource(ToggleGroupItemTokens.Key(ToggleGroupItemTokenKind.PressDownDuration), out var down).ShouldBeTrue();
            down.ShouldBe(TimeSpan.FromMilliseconds(90));
            item.TryFindResource(ToggleGroupItemTokens.Key(ToggleGroupItemTokenKind.PressUpDuration), out var up).ShouldBeTrue();
            up.ShouldBe(TimeSpan.FromMilliseconds(320));
        }
        finally
        {
            window.Close();
        }
    }

    public sealed record ViewChoice(string Key, string Title, Geometry Glyph);
}
