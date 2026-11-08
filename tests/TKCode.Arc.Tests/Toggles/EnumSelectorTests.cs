using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Toggles;

[EnumMetadata]
public enum TestPriority
{
    [Description("Low priority")] Low,
    [Description("Medium priority")] Medium,
    High,
    [Browsable(false)] Secret,
    Critical = 10
}

[Flags, EnumMetadata]
public enum TestPermissions : byte
{
    [Description("No access")] None = 0,
    Read = 1,
    Write = 2,
    Execute = 4,
    Delete = 8,
    ReadWrite = Read | Write,
    FullControl = Read | Write | Execute | Delete
}

[Flags, EnumMetadata]
public enum TestWideFlags : ulong
{
    None = 0,
    Low = 1,
    Top = 1UL << 63
}

[Flags, EnumMetadata]
public enum TestSignedFlags : short
{
    None = 0,
    A = 1,
    Sign = unchecked((short)0x8000)
}

/// <summary>Not marked: the selector cannot list it.</summary>
public enum TestUnregistered
{
    One,
    Two
}

/// <summary>Registered by hand in a test.</summary>
public enum TestManual
{
    One,
    Two
}

public class EnumSelectorTests
{
    private static List<Checkbox> Checks(EnumSelector selector) => selector.GetVisualDescendants().OfType<Checkbox>().ToList();

    private static Checkbox Check(EnumSelector selector, string name)
        => Checks(selector).First(c => ((EnumSelectorItem)c.Content!).Name == name);

    private static Dictionary<string, CheckboxState> States(EnumSelector selector)
        => Checks(selector).ToDictionary(c => ((EnumSelectorItem)c.Content!).Name, c => c.CheckedState);

    [AvaloniaFact]
    public void A_Plain_Enum_Is_A_Radio_Group_Of_Its_Visible_Members()
    {
        var selector = new EnumSelector { EnumType = typeof(TestPriority) };
        var window = TestHelpers.Show(selector);
        try
        {
            selector.HasFlags.ShouldBeFalse();
            selector.EffectiveDisplayMode.ShouldBe(EnumSelectorDisplayMode.Radio);
            selector.Classes.Contains(":radio").ShouldBeTrue();
            selector.Items.Select(i => i.Text).ShouldBe(["Low priority", "Medium priority", "High", "Critical"]);
            var group = TestHelpers.Descendant<RadioGroup>(selector);
            group.ItemCount.ShouldBe(4);
            group.SelectedIndex.ShouldBe(-1);
            Checks(selector).ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Choosing_A_Radio_Option_Sets_SelectedEnum_And_The_Value_Selects_The_Option()
    {
        var selector = new EnumSelector { EnumType = typeof(TestPriority), SelectedEnum = TestPriority.Medium };
        var raised = new List<(object? Old, object? New)>();
        selector.SelectedEnumChanged += (_, e) => raised.Add((e.OldValue, e.NewValue));
        var window = TestHelpers.Show(selector);
        try
        {
            var group = TestHelpers.Descendant<RadioGroup>(selector);
            group.SelectedIndex.ShouldBe(1);
            group.ContainerFromIndex(1).ShouldBeOfType<RadioGroupItem>().IsSelected.ShouldBeTrue();

            group.SelectedIndex = 3;
            selector.SelectedEnum.ShouldBe(TestPriority.Critical);

            selector.SelectedEnum = TestPriority.Low;
            group.SelectedIndex.ShouldBe(0);

            // Clearing the value clears the choice; a value no option has selects nothing.
            selector.SelectedEnum = null;
            group.SelectedIndex.ShouldBe(-1);
            selector.SelectedEnum = TestPriority.Secret;
            group.SelectedIndex.ShouldBe(-1);

            raised.ShouldBe([
                ((object?)TestPriority.Medium, (object?)TestPriority.Critical),
                (TestPriority.Critical, TestPriority.Low),
                (TestPriority.Low, null),
                (null, TestPriority.Secret)
            ]);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectedEnum_Binds_Two_Way_To_A_Typed_View_Model_Property()
    {
        var model = new Model { Priority = TestPriority.High };
        var selector = new EnumSelector { EnumType = typeof(TestPriority), DataContext = model };
        selector.Bind(EnumSelector.SelectedEnumProperty, new Binding(nameof(Model.Priority)));
        var second = new EnumSelector { EnumType = typeof(TestPriority), SelectedEnum = TestPriority.Low };
        var window = TestHelpers.Show(new StackPanel { Children = { selector, second } });
        try
        {
            var group = TestHelpers.Descendant<RadioGroup>(selector);
            group.SelectedIndex.ShouldBe(2);

            group.SelectedIndex = 0;
            model.Priority.ShouldBe(TestPriority.Low);

            model.Priority = TestPriority.Critical;
            group.SelectedIndex.ShouldBe(3);
            selector.SelectedEnum.ShouldBe(TestPriority.Critical);

            // Two selectors of one enum keep their own values.
            second.SelectedEnum.ShouldBe(TestPriority.Low);
            TestHelpers.Descendant<RadioGroup>(second).SelectedIndex.ShouldBe(0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Flags_Enum_Is_Checkboxes_That_Combine_Bits()
    {
        var selector = new EnumSelector { EnumType = typeof(TestPermissions), SelectedEnum = TestPermissions.None };
        var window = TestHelpers.Show(selector);
        try
        {
            selector.HasFlags.ShouldBeTrue();
            selector.EffectiveDisplayMode.ShouldBe(EnumSelectorDisplayMode.Check);
            selector.Classes.Contains(":flags").ShouldBeTrue();
            Checks(selector).Count.ShouldBe(7);
            States(selector)["None"].ShouldBe(CheckboxState.Checked);

            // A composite member checks its parts; a part leaves the composite partly set (the dash).
            Check(selector, "ReadWrite").IsChecked = true;
            selector.SelectedEnum.ShouldBe(TestPermissions.ReadWrite);
            States(selector).ShouldBe(new Dictionary<string, CheckboxState>
            {
                ["None"] = CheckboxState.Unchecked,
                ["Read"] = CheckboxState.Checked,
                ["Write"] = CheckboxState.Checked,
                ["Execute"] = CheckboxState.Unchecked,
                ["Delete"] = CheckboxState.Unchecked,
                ["ReadWrite"] = CheckboxState.Checked,
                ["FullControl"] = CheckboxState.Indeterminate
            });

            Check(selector, "Read").IsChecked = false;
            selector.SelectedEnum.ShouldBe(TestPermissions.Write);
            States(selector)["ReadWrite"].ShouldBe(CheckboxState.Indeterminate);

            // Clicking a partly set composite completes it.
            Check(selector, "FullControl").IsChecked = true;
            selector.SelectedEnum.ShouldBe(TestPermissions.FullControl);
            States(selector).Where(s => s.Key != "None").ShouldAllBe(s => s.Value == CheckboxState.Checked);

            // Unchecking a composite clears all of its bits.
            Check(selector, "ReadWrite").IsChecked = false;
            selector.SelectedEnum.ShouldBe(TestPermissions.Execute | TestPermissions.Delete);

            // None clears every flag and cannot be unchecked; any flag unchecks None.
            Check(selector, "None").IsChecked = true;
            selector.SelectedEnum.ShouldBe(TestPermissions.None);
            States(selector).Where(s => s.Key != "None").ShouldAllBe(s => s.Value == CheckboxState.Unchecked);
            Check(selector, "None").IsChecked = false;
            selector.SelectedEnum.ShouldBe(TestPermissions.None);
            Check(selector, "None").IsChecked.ShouldBe(true);
            Check(selector, "Execute").IsChecked = true;
            selector.SelectedEnum.ShouldBe(TestPermissions.Execute);
            Check(selector, "None").IsChecked.ShouldBe(false);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Combined_Value_From_The_View_Model_Shows_On_The_Boxes()
    {
        var model = new Model { Permissions = TestPermissions.Read | TestPermissions.Execute };
        var selector = new EnumSelector { EnumType = typeof(TestPermissions), DataContext = model };
        selector.Bind(EnumSelector.SelectedEnumProperty, new Binding(nameof(Model.Permissions)));
        var window = TestHelpers.Show(selector);
        try
        {
            States(selector)["Read"].ShouldBe(CheckboxState.Checked);
            States(selector)["Execute"].ShouldBe(CheckboxState.Checked);
            States(selector)["ReadWrite"].ShouldBe(CheckboxState.Indeterminate);

            Check(selector, "Write").IsChecked = true;
            model.Permissions.ShouldBe(TestPermissions.Read | TestPermissions.Write | TestPermissions.Execute);

            model.Permissions = TestPermissions.Delete;
            States(selector)["Delete"].ShouldBe(CheckboxState.Checked);
            States(selector)["Read"].ShouldBe(CheckboxState.Unchecked);

            // Bits no member names are kept.
            model.Permissions = (TestPermissions)0x41;
            Check(selector, "Write").IsChecked = true;
            model.Permissions.ShouldBe((TestPermissions)0x43);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Flags_Of_Unsigned_And_Signed_64_And_16_Bit_Enums_Keep_Their_High_Bits()
    {
        var wide = new EnumSelector { EnumType = typeof(TestWideFlags) };
        var signed = new EnumSelector { EnumType = typeof(TestSignedFlags) };
        var window = TestHelpers.Show(new StackPanel { Children = { wide, signed } });
        try
        {
            Check(wide, "Top").IsChecked = true;
            Check(wide, "Low").IsChecked = true;
            wide.SelectedEnum.ShouldBe(TestWideFlags.Top | TestWideFlags.Low);
            Check(wide, "Top").IsChecked = false;
            wide.SelectedEnum.ShouldBe(TestWideFlags.Low);

            Check(signed, "Sign").IsChecked = true;
            Check(signed, "A").IsChecked = true;
            signed.SelectedEnum.ShouldBe(TestSignedFlags.Sign | TestSignedFlags.A);
            Check(signed, "A").IsChecked = false;
            signed.SelectedEnum.ShouldBe(TestSignedFlags.Sign);
            States(signed)["None"].ShouldBe(CheckboxState.Unchecked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void DisplayMode_Overrides_The_Look_But_Keeps_The_Meaning()
    {
        var flags = new EnumSelector
        {
            EnumType = typeof(TestPermissions),
            DisplayMode = EnumSelectorDisplayMode.Radio,
            SelectedEnum = TestPermissions.ReadWrite
        };
        var plain = new EnumSelector
        {
            EnumType = typeof(TestPriority),
            DisplayMode = EnumSelectorDisplayMode.Check,
            SelectedEnum = TestPriority.Low
        };
        var window = TestHelpers.Show(new StackPanel { Children = { flags, plain } });
        try
        {
            // Radio for a flags enum: one member, composites included.
            flags.EffectiveDisplayMode.ShouldBe(EnumSelectorDisplayMode.Radio);
            var group = TestHelpers.Descendant<RadioGroup>(flags);
            group.SelectedIndex.ShouldBe(5);
            group.SelectedIndex = 2;
            flags.SelectedEnum.ShouldBe(TestPermissions.Write);

            // Checkboxes for a plain enum: still one member.
            plain.EffectiveDisplayMode.ShouldBe(EnumSelectorDisplayMode.Check);
            plain.Classes.Contains(":check").ShouldBeTrue();
            Check(plain, "High").IsChecked = true;
            plain.SelectedEnum.ShouldBe(TestPriority.High);
            States(plain).Count(s => s.Value == CheckboxState.Checked).ShouldBe(1);
            Check(plain, "High").IsChecked = false;
            plain.SelectedEnum.ShouldBe(TestPriority.High);
            Check(plain, "High").IsChecked.ShouldBe(true);

            // Switching modes keeps the value.
            plain.DisplayMode = EnumSelectorDisplayMode.Auto;
            TestHelpers.Descendant<RadioGroup>(plain).SelectedIndex.ShouldBe(2);
            Checks(plain).ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void UseDisplayNames_Switches_Between_Descriptions_And_Names_And_Keeps_The_Choice()
    {
        var selector = new EnumSelector { EnumType = typeof(TestPriority), SelectedEnum = TestPriority.Medium };
        var window = TestHelpers.Show(selector);
        try
        {
            var label = TestHelpers.Descendant<RadioGroup>(selector).ContainerFromIndex(0)!.GetVisualDescendants().OfType<TextBlock>().First();
            label.Text.ShouldBe("Low priority");

            selector.UseDisplayNames = false;
            selector.Items.Select(i => i.Text).ShouldBe(["Low", "Medium", "High", "Critical"]);
            label.Text.ShouldBe("Low");
            selector.SelectedEnum.ShouldBe(TestPriority.Medium);
            TestHelpers.Descendant<RadioGroup>(selector).SelectedIndex.ShouldBe(1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Rows_And_Columns_Lay_Out_A_Uniform_Grid_And_ItemsPanel_Replaces_It()
    {
        var selector = new EnumSelector { EnumType = typeof(TestPriority) };
        var window = TestHelpers.Show(selector);
        try
        {
            var grid = TestHelpers.Descendant<UniformGrid>(selector);
            grid.Columns.ShouldBe(1);
            grid.RowSpacing.ShouldBe(8);
            grid.ColumnSpacing.ShouldBe(16);

            selector.Columns = 2;
            grid.Columns.ShouldBe(2);
            selector.Columns = 0;
            selector.Rows = 1;
            grid.Rows.ShouldBe(1);
            grid.Columns.ShouldBe(0);
            selector.RowSpacing = 4;
            grid.RowSpacing.ShouldBe(4);

            // Checkboxes sit closer: their hit areas space them.
            selector.DisplayMode = EnumSelectorDisplayMode.Check;
            var checkGrid = TestHelpers.Descendant<UniformGrid>(selector);
            checkGrid.RowSpacing.ShouldBe(4);
            selector.ClearValue(EnumSelector.RowSpacingProperty);
            checkGrid.RowSpacing.ShouldBe(0);

            selector.ItemsPanel = new FuncTemplate<Panel?>(() => new WrapPanel());
            var wrap = TestHelpers.Descendant<WrapPanel>(selector);
            wrap.Children.Count.ShouldBe(4);
            selector.GetVisualDescendants().OfType<UniformGrid>().ShouldBeEmpty();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void ItemTemplate_Receives_The_Item()
    {
        var selector = new EnumSelector
        {
            EnumType = typeof(TestPriority),
            ItemTemplate = new FuncDataTemplate<EnumSelectorItem>((item, _) => new TextBlock { Text = "[" + item?.Name + "]" })
        };
        var window = TestHelpers.Show(selector);
        try
        {
            selector.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("[High]");
            selector.DisplayMode = EnumSelectorDisplayMode.Check;
            window.UpdateLayout();
            selector.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("[Critical]");
            selector.ItemTemplate = null;
            window.UpdateLayout();
            selector.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ShouldContain("Low priority");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Without_EnumType_The_Type_Of_The_Value_Is_Listed_And_An_Unregistered_Enum_Lists_Nothing()
    {
        var selector = new EnumSelector { SelectedEnum = TestPermissions.Read };
        var other = new EnumSelector { EnumType = typeof(TestUnregistered) };
        var window = TestHelpers.Show(new StackPanel { Children = { selector, other } });
        try
        {
            selector.HasFlags.ShouldBeTrue();
            selector.Items.Count.ShouldBe(7);
            States(selector)["Read"].ShouldBe(CheckboxState.Checked);

            selector.SelectedEnum = TestPriority.High;
            selector.HasFlags.ShouldBeFalse();
            selector.Items.Count.ShouldBe(4);
            TestHelpers.Descendant<RadioGroup>(selector).SelectedIndex.ShouldBe(2);

            other.EnumInfo.ShouldBeNull();
            other.Items.ShouldBeEmpty();
            EnumTypes.IsRegistered(typeof(TestUnregistered)).ShouldBeFalse();
            EnumTypes.Find(typeof(TestPriority?)).ShouldNotBeNull().Type.ShouldBe(typeof(TestPriority));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Text_Key_Shows_The_Resource_And_Follows_It()
    {
        var key = new object();
        EnumTypes.Register(new EnumTypeInfo(typeof(TestManual), false,
            static v => (ulong)(int)(TestManual)v, static b => (TestManual)(int)b,
            [
                new EnumMemberInfo("One", TestManual.One, 0) { DisplayName = "First", TextKey = key },
                new EnumMemberInfo("Two", TestManual.Two, 1)
            ]));
        try
        {
            var selector = new EnumSelector { EnumType = typeof(TestManual) };
            var window = TestHelpers.Show(selector);
            try
            {
                // No resource yet: the display name.
                selector.Items[0].Text.ShouldBe("First");
                window.Resources[key] = "Erste";
                selector.Items[0].Text.ShouldBe("Erste");
                window.Resources[key] = "Premier";
                selector.Items[0].Text.ShouldBe("Premier");
                selector.UseDisplayNames = false;
                selector.Items[0].Text.ShouldBe("One");
                selector.Items[1].Text.ShouldBe("Two");
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            EnumTypes.Unregister(typeof(TestManual)).ShouldBeTrue();
        }
    }

    [AvaloniaFact]
    public void Header_Is_The_Legend_And_The_Accessible_Name()
    {
        var selector = new EnumSelector { EnumType = typeof(TestPriority), Header = "Priority" };
        var window = TestHelpers.Show(selector);
        try
        {
            Avalonia.Automation.AutomationProperties.GetName(selector).ShouldBe("Priority");
            Avalonia.Automation.AutomationProperties.GetName(TestHelpers.Descendant<RadioGroup>(selector)).ShouldBe("Priority");
            selector.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First(p => p.Name == "PART_HeaderPresenter").IsVisible.ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Disables_Every_Option()
    {
        var selector = new EnumSelector { EnumType = typeof(TestPermissions), IsEnabled = false };
        var window = TestHelpers.Show(selector);
        try
        {
            Checks(selector).ShouldAllBe(c => !c.IsEffectivelyEnabled);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class Model : INotifyPropertyChanged
    {
        private TestPriority _priority;
        private TestPermissions _permissions;

        public event PropertyChangedEventHandler? PropertyChanged;

        public TestPriority Priority
        {
            get => _priority;
            set => Set(ref _priority, value);
        }

        public TestPermissions Permissions
        {
            get => _permissions;
            set => Set(ref _permissions, value);
        }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
