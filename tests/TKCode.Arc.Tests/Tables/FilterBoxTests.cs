using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TKCode.Arc.Tests.Tables;

public class FilterBoxTests
{
    private static T Part<T>(Visual root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    // ---- Conditions (no UI) ----

    [Theory]
    [InlineData(NumberOperator.Equal, 100, true)]
    [InlineData(NumberOperator.Equal, 100.01, true)]
    [InlineData(NumberOperator.Equal, 100.02, false)]
    [InlineData(NumberOperator.NotEqual, 100.005, false)]
    [InlineData(NumberOperator.NotEqual, 99, true)]
    [InlineData(NumberOperator.LessThan, 99.99, false)]
    [InlineData(NumberOperator.LessThan, 99.98, true)]
    [InlineData(NumberOperator.LessThanOrEqual, 100.01, true)]
    [InlineData(NumberOperator.GreaterThan, 100.01, false)]
    [InlineData(NumberOperator.GreaterThan, 100.02, true)]
    [InlineData(NumberOperator.GreaterThanOrEqual, 99.99, true)]
    [InlineData(NumberOperator.GreaterThanOrEqual, 99.98, false)]
    public void Number_Condition_Compares_Within_The_Tolerance(NumberOperator op, double candidate, bool expected)
    {
        new NumberFilterCondition(op, 100m, 0.01m).Matches((decimal)candidate).ShouldBe(expected);
    }

    [Fact]
    public void Number_Condition_Without_Tolerance_Is_Exact_And_Ignores_A_Negative_One()
    {
        new NumberFilterCondition(NumberOperator.LessThan, 10m, 0m).Matches(9.999m).ShouldBeTrue();
        new NumberFilterCondition(NumberOperator.LessThan, 10m, -5m).Matches(10m).ShouldBeFalse();
        new NumberFilterCondition(NumberOperator.Equal, 10m, 0m).Matches(10).ShouldBeTrue();
        new NumberFilterCondition(NumberOperator.GreaterThanOrEqual, 100m, 0m).ToString().ShouldBe("≥ 100");
    }

    [Theory]
    [InlineData(TextOperator.Contains, "pro", false, "MacBook Pro", true)]
    [InlineData(TextOperator.Contains, "pro", true, "MacBook Pro", false)]
    [InlineData(TextOperator.NotContains, "pro", false, "MacBook Pro", false)]
    [InlineData(TextOperator.Equal, "keyboard", false, "Keyboard", true)]
    [InlineData(TextOperator.Equal, "keyboard", true, "Keyboard", false)]
    [InlineData(TextOperator.NotEqual, "keyboard", false, "Mouse", true)]
    [InlineData(TextOperator.StartsWith, "mac", false, "MacBook Pro", true)]
    [InlineData(TextOperator.EndsWith, "PRO", false, "MacBook Pro", true)]
    [InlineData(TextOperator.EndsWith, "Air", false, "MacBook Pro", false)]
    [InlineData(TextOperator.Regex, @"^[A-Z]\w+$", true, "Keyboard", true)]
    [InlineData(TextOperator.Regex, @"^[A-Z]\w+$", true, "keyboard", false)]
    [InlineData(TextOperator.Regex, @"^[A-Z]\w+$", false, "keyboard", true)]
    public void Text_Condition_Applies_Its_Operator_And_Case(TextOperator op, string text, bool caseSensitive, string candidate, bool expected)
    {
        new TextFilterCondition(op, text, caseSensitive).Matches(candidate).ShouldBe(expected);
    }

    [Fact]
    public void Text_Condition_With_Empty_Text_Or_A_Bad_Pattern_Constrains_Nothing()
    {
        foreach (var op in FilterOperators.TextOperators)
        {
            var empty = new TextFilterCondition(op, "");
            empty.IsEmpty.ShouldBeTrue();
            empty.Matches("anything").ShouldBeTrue();
            empty.Matches(null).ShouldBeTrue();
        }

        var bad = new TextFilterCondition(TextOperator.Regex, "([a-z");
        bad.IsValid.ShouldBeFalse();
        bad.Error.ShouldNotBeNullOrEmpty();
        bad.Matches("abc").ShouldBeTrue();

        // The same text is no pattern for the other operators.
        new TextFilterCondition(TextOperator.Contains, "([a-z").IsValid.ShouldBeTrue();
        new TextFilterCondition(TextOperator.Contains, "x").Matches(null).ShouldBeFalse();
    }

    [Fact]
    public void Operators_Have_Symbols_And_Names()
    {
        FilterOperators.NumberOperators.Select(FilterOperators.Symbol).ShouldBe(["=", "≠", "<", "≤", ">", "≥"]);
        FilterOperators.TextOperators.Select(FilterOperators.Symbol).ShouldBe(["∋", "∌", "=", "≠", "^", "$", ".*"]);
        FilterOperators.Name(NumberOperator.GreaterThanOrEqual).ShouldBe("Greater than or equal to");
        FilterOperators.Name(TextOperator.StartsWith).ShouldBe("Starts with");
    }

    // ---- NumberFilterBox ----

    [AvaloniaFact]
    public void Number_Box_Lays_Out_Checkbox_Operator_And_Value()
    {
        var box = new NumberFilterBox { Label = "Price", Operator = NumberOperator.GreaterThanOrEqual, Value = 100m };
        var window = TestHelpers.Show(box);
        try
        {
            var toggle = Part<Checkbox>(box, FilterBox.TogglePart);
            var select = Part<Select>(box, FilterBox.OperatorPart);
            var field = Part<NumberField>(box, NumberFilterBox.ValuePart);

            toggle.IsChecked.ShouldBe(true);
            toggle.Content.ShouldBe("Price");
            select.SelectedItem.ShouldBe(NumberOperator.GreaterThanOrEqual);
            select.SelectedText.ShouldBe("≥");
            select.ItemCount.ShouldBe(6);
            field.Value.ShouldBe(100m);
            field.MaximumFractionDigits.ShouldBe(2);

            // Left to right, top aligned.
            var toggleX = toggle.TranslatePoint(default, box)!.Value.X;
            var selectX = select.TranslatePoint(default, box)!.Value.X;
            var fieldX = field.TranslatePoint(default, box)!.Value.X;
            toggleX.ShouldBeLessThan(selectX);
            selectX.ShouldBeLessThan(fieldX);
            select.Bounds.Height.ShouldBe(44, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Turning_The_Filter_Off_Disables_The_Editors_And_Keeps_The_Condition()
    {
        var extra = new Button { Content = "Reset" };
        var box = new NumberFilterBox { Label = "Stock", Operator = NumberOperator.LessThan, Value = 50m, ExtraContent = extra };
        var window = TestHelpers.Show(box);
        try
        {
            var toggle = Part<Checkbox>(box, FilterBox.TogglePart);
            var select = Part<Select>(box, FilterBox.OperatorPart);
            var field = Part<NumberField>(box, NumberFilterBox.ValuePart);

            toggle.IsChecked = false;
            Dispatcher.UIThread.RunJobs();

            box.IsActive.ShouldBeFalse();
            box.Classes.Contains(":inactive").ShouldBeTrue();
            select.IsEffectivelyEnabled.ShouldBeFalse();
            field.IsEffectivelyEnabled.ShouldBeFalse();
            extra.IsEffectivelyEnabled.ShouldBeFalse();
            toggle.IsEffectivelyEnabled.ShouldBeTrue();
            box.Operator.ShouldBe(NumberOperator.LessThan);
            box.Value.ShouldBe(50m);
            box.Matches(9999m).ShouldBeTrue("an inactive filter lets everything through");

            box.IsActive = true;
            Dispatcher.UIThread.RunJobs();
            toggle.IsChecked.ShouldBe(true);
            select.IsEffectivelyEnabled.ShouldBeTrue();
            field.Value.ShouldBe(50m);
            box.Matches(9999m).ShouldBeFalse();
            box.Matches(49m).ShouldBeTrue();
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Picking_An_Operator_And_Editing_The_Value_Write_Back_And_Raise_FilterChanged()
    {
        var box = new NumberFilterBox { Label = "Price" };
        var window = TestHelpers.Show(box);
        try
        {
            var changes = 0;
            box.FilterChanged += (_, _) => changes++;
            var select = Part<Select>(box, FilterBox.OperatorPart);
            var field = Part<NumberField>(box, NumberFilterBox.ValuePart);

            select.SelectedItem = NumberOperator.GreaterThan;
            box.Operator.ShouldBe(NumberOperator.GreaterThan);
            changes.ShouldBe(1);

            field.SetCurrentValue(NumberField.ValueProperty, 25m);
            box.Value.ShouldBe(25m);
            box.Condition.ShouldBe(new NumberFilterCondition(NumberOperator.GreaterThan, 25m, 0m));
            changes.ShouldBe(2);

            box.Operator = NumberOperator.NotEqual;
            select.SelectedItem.ShouldBe(NumberOperator.NotEqual);
            changes.ShouldBe(3);

            box.IsActive = false;
            changes.ShouldBe(4);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Integer_Mode_Shows_Whole_Numbers_And_Steps_By_One()
    {
        var box = new NumberFilterBox { Label = "Stock", NumberMode = NumberFilterMode.Integer };
        var window = TestHelpers.Show(box);
        try
        {
            var field = Part<NumberField>(box, NumberFilterBox.ValuePart);
            field.MaximumFractionDigits.ShouldBe(0);
            field.Step.ShouldBe(1m);
            field.LargeStep.ShouldBe(10m);

            box.NumberMode = NumberFilterMode.Decimal;
            box.DecimalPlaces = 3;
            field.MaximumFractionDigits.ShouldBe(3);
            field.Step.ShouldBe(0.001m);
            field.LargeStep.ShouldBe(1m);

            box.SmallChange = 0.5m;
            box.LargeChange = 5m;
            field.Step.ShouldBe(0.5m);
            field.LargeStep.ShouldBe(5m);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Operators_Limits_The_Menu()
    {
        var box = new NumberFilterBox { Operators = [NumberOperator.LessThanOrEqual, NumberOperator.GreaterThanOrEqual], Operator = NumberOperator.LessThanOrEqual };
        var window = TestHelpers.Show(box);
        try
        {
            var select = Part<Select>(box, FilterBox.OperatorPart);
            select.ItemCount.ShouldBe(2);
            select.SelectedText.ShouldBe("≤");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Number_Box_Binds_Two_Way_To_A_View_Model()
    {
        var vm = new FilterViewModel();
        var box = new NumberFilterBox { Label = "Price" };
        box.Bind(FilterBox.IsActiveProperty, new Binding(nameof(FilterViewModel.PriceActive)) { Source = vm });
        box.Bind(NumberFilterBox.OperatorProperty, new Binding(nameof(FilterViewModel.PriceOperator)) { Source = vm });
        box.Bind(NumberFilterBox.ValueProperty, new Binding(nameof(FilterViewModel.Price)) { Source = vm });
        var window = TestHelpers.Show(box);
        try
        {
            Part<Select>(box, FilterBox.OperatorPart).SelectedItem.ShouldBe(NumberOperator.LessThanOrEqual);
            Part<NumberField>(box, NumberFilterBox.ValuePart).Value.ShouldBe(1000m);

            Part<Checkbox>(box, FilterBox.TogglePart).IsChecked = false;
            vm.PriceActive.ShouldBeFalse();
            Part<Select>(box, FilterBox.OperatorPart).SelectedItem = NumberOperator.GreaterThan;
            vm.PriceOperator.ShouldBe(NumberOperator.GreaterThan);
            Part<NumberField>(box, NumberFilterBox.ValuePart).SetCurrentValue(NumberField.ValueProperty, 12m);
            vm.Price.ShouldBe(12m);

            vm.Price = 30m;
            Dispatcher.UIThread.RunJobs();
            Part<NumberField>(box, NumberFilterBox.ValuePart).Value.ShouldBe(30m);
        }
        finally
        {
            window.Close();
        }
    }

    // ---- TextFilterBox ----

    [AvaloniaFact]
    public void Text_Box_Edits_The_Text_And_Shows_An_Invalid_Pattern()
    {
        var box = new TextFilterBox { Label = "Name", Placeholder = "Type a name", Width = 520 };
        var window = TestHelpers.Show(box);
        try
        {
            var input = Part<Input>(box, TextFilterBox.InputPart);
            var select = Part<Select>(box, FilterBox.OperatorPart);
            select.SelectedText.ShouldBe("∋");
            input.Placeholder.ShouldBe("Type a name");

            input.SetCurrentValue(TextBox.TextProperty, "pro");
            box.Text.ShouldBe("pro");
            box.Matches("MacBook Pro").ShouldBeTrue();
            box.Matches("Mouse").ShouldBeFalse();

            box.IsCaseSensitive = true;
            box.Matches("MacBook Pro").ShouldBeFalse();

            box.Operator = TextOperator.Regex;
            box.Text = "([a-z";
            Dispatcher.UIThread.RunJobs();
            box.IsPatternValid.ShouldBeFalse();
            box.PatternError.ShouldNotBeNullOrEmpty();
            box.Classes.Contains(":invalid").ShouldBeTrue();
            input.IsInvalid.ShouldBeTrue();
            input.EffectiveErrorMessage.ShouldBe(box.PatternError);

            box.Text = "^Mac";
            Dispatcher.UIThread.RunJobs();
            box.IsPatternValid.ShouldBeTrue();
            box.PatternError.ShouldBeNull();
            box.Classes.Contains(":invalid").ShouldBeFalse();
            input.IsInvalid.ShouldBeFalse();
            box.Matches("MacBook Pro").ShouldBeTrue();

            box.Clear();
            input.Text.ShouldBe(string.Empty);
            box.Operator.ShouldBe(TextOperator.Regex);
        }
        finally
        {
            window.Close();
        }
    }

    private const string LongDescription = "Products whose name contains this text, in any of the catalog's languages.";

    private static void AssertNothingOverlapsOrOverflows(TextFilterBox box)
    {
        var select = Part<Select>(box, FilterBox.OperatorPart);
        var input = Part<Input>(box, TextFilterBox.InputPart);
        var toggle = Part<Checkbox>(box, FilterBox.TogglePart);
        var inputRect = input.Bounds;
        var selectRect = select.Bounds;
        selectRect.Right.ShouldBeLessThanOrEqualTo(inputRect.Left - 12 + 0.5, "The operator and the text need their gap.");
        inputRect.Right.ShouldBeLessThanOrEqualTo(box.Bounds.Width + 0.5, "The text field must not be cut off.");
        if (box.ExtraContent is null)
        {
            inputRect.Right.ShouldBe(box.Bounds.Width, 0.5, "With no extra content the text field reaches the edge.");
        }
        if (!box.Classes.Contains(FilterBox.StackedPseudoClass))
        {
            toggle.Bounds.Right.ShouldBeLessThanOrEqualTo(selectRect.Left + 0.5, "The label must not run under the operator.");
        }
    }

    [AvaloniaFact]
    public void A_Long_Description_Wraps_Instead_Of_Pushing_The_Fields_Out()
    {
        var box = new TextFilterBox { Label = "Product name", Description = LongDescription, Width = 520 };
        var window = TestHelpers.Show(box);
        try
        {
            Dispatcher.UIThread.RunJobs();
            box.Classes.Contains(FilterBox.StackedPseudoClass).ShouldBeFalse();
            AssertNothingOverlapsOrOverflows(box);
            // It wrapped: taller than the one line it would be with room to spare.
            Part<Input>(box, TextFilterBox.InputPart).Bounds.Width.ShouldBeGreaterThanOrEqualTo(160 - 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Too_Narrow_For_A_Row_The_Label_Moves_Above_The_Fields_And_Comes_Back()
    {
        var box = new TextFilterBox { Label = "Product name", Description = LongDescription, Width = 320 };
        var window = TestHelpers.Show(box);
        try
        {
            Dispatcher.UIThread.RunJobs();
            var toggle = Part<Checkbox>(box, FilterBox.TogglePart);
            var select = Part<Select>(box, FilterBox.OperatorPart);
            box.Classes.Contains(FilterBox.StackedPseudoClass).ShouldBeTrue();
            select.Bounds.Top.ShouldBeGreaterThanOrEqualTo(toggle.Bounds.Bottom);
            select.Bounds.Left.ShouldBe(0, 0.5);
            AssertNothingOverlapsOrOverflows(box);

            box.Width = 800;
            Dispatcher.UIThread.RunJobs();
            box.Classes.Contains(FilterBox.StackedPseudoClass).ShouldBeFalse();
            select.Bounds.Top.ShouldBeLessThan(toggle.Bounds.Bottom);
            select.Bounds.Left.ShouldBeGreaterThan(0);
            AssertNothingOverlapsOrOverflows(box);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Menu_Names_The_Operators_And_The_Field_Keeps_The_Symbol()
    {
        var box = new TextFilterBox { Label = "Name", Operator = TextOperator.Contains, Width = 520 };
        var window = TestHelpers.Show(box);
        try
        {
            var select = Part<Select>(box, FilterBox.OperatorPart);
            select.SelectedText.ShouldBe("∋");
            select.TextSelector!(TextOperator.Contains).ShouldBe("∋   Contains");
            box.Operator = TextOperator.StartsWith;
            Dispatcher.UIThread.RunJobs();
            select.SelectedText.ShouldBe("^");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Extra_Content_Collapses_When_Null()
    {
        var box = new TextFilterBox { Label = "File", Width = 600 };
        var window = TestHelpers.Show(box);
        try
        {
            var extra = Part<ContentPresenter>(box, "PART_Extra");
            extra.IsVisible.ShouldBeFalse();
            box.Classes.Contains(":extra").ShouldBeFalse();
            var input = Part<Input>(box, TextFilterBox.InputPart);
            var wide = input.Bounds.Width;

            var caseBox = new Checkbox { Content = "Match case" };
            box.ExtraContent = caseBox;
            Dispatcher.UIThread.RunJobs();
            extra.IsVisible.ShouldBeTrue();
            box.Classes.Contains(":extra").ShouldBeTrue();
            input.Bounds.Width.ShouldBeLessThan(wide);

            box.IsActive = false;
            caseBox.IsEffectivelyEnabled.ShouldBeFalse();
        }
        finally
        {
            window.Close();
        }
    }

    // ---- Alignment ----

    [AvaloniaFact]
    public void A_Label_Size_Group_Lines_Up_The_Operators_Of_Stacked_Filters()
    {
        var shortLabel = new NumberFilterBox { Label = "Price", LabelSizeGroup = "FilterLabel" };
        var longLabel = new TextFilterBox { Label = "Product name in the catalog", LabelSizeGroup = "FilterLabel", Width = 640, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var stack = new StackPanel { Spacing = 8, Children = { shortLabel, longLabel } };
        Grid.SetIsSharedSizeScope(stack, true);
        var window = TestHelpers.Show(stack);
        try
        {
            window.Width = 900;
            Settle(window);
            var first = Part<Select>(shortLabel, FilterBox.OperatorPart).TranslatePoint(default, stack)!.Value.X;
            var second = Part<Select>(longLabel, FilterBox.OperatorPart).TranslatePoint(default, stack)!.Value.X;
            first.ShouldBe(second, 0.5);

            // Without the group each box sizes its own label.
            shortLabel.LabelSizeGroup = null;
            Settle(window);
            Part<Select>(shortLabel, FilterBox.OperatorPart).TranslatePoint(default, stack)!.Value.X.ShouldBeLessThan(second);

            // A fixed width wins.
            shortLabel.LabelWidth = 200;
            longLabel.LabelSizeGroup = null;
            longLabel.LabelWidth = 200;
            Settle(window);
            Part<Select>(shortLabel, FilterBox.OperatorPart).TranslatePoint(default, stack)!.Value.X
                .ShouldBe(Part<Select>(longLabel, FilterBox.OperatorPart).TranslatePoint(default, stack)!.Value.X, 0.5);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Parts_Are_Named_For_Assistive_Technology()
    {
        var box = new NumberFilterBox { Label = "Price", Operator = NumberOperator.GreaterThanOrEqual };
        var window = TestHelpers.Show(box);
        try
        {
            var select = Part<Select>(box, FilterBox.OperatorPart);
            Avalonia.Automation.AutomationProperties.GetName(select).ShouldBe("Price operator");
            Avalonia.Automation.AutomationProperties.GetHelpText(select).ShouldBe("Greater than or equal to");
            Part<NumberField>(box, NumberFilterBox.ValuePart).Label.ShouldBe("Price");
        }
        finally
        {
            window.Close();
        }
    }

    // A shared size group settles over a second layout pass (the group's width reaches the other grids after the first).
    private static void Settle(Window window)
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }

    private sealed class FilterViewModel : INotifyPropertyChanged
    {
        private bool _priceActive = true;
        private NumberOperator _priceOperator = NumberOperator.LessThanOrEqual;
        private decimal _price = 1000m;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool PriceActive { get => _priceActive; set => Set(ref _priceActive, value); }

        public NumberOperator PriceOperator { get => _priceOperator; set => Set(ref _priceOperator, value); }

        public decimal Price { get => _price; set => Set(ref _price, value); }

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
