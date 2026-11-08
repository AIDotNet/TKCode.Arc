using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// A number filter condition: an enable checkbox with the label, an operator (= ≠ &lt; ≤ &gt; ≥) and a
/// <see cref="NumberField"/> for the target. It holds the condition (<see cref="FilterBox.IsActive"/>,
/// <see cref="Operator"/>, <see cref="Value"/>, <see cref="Tolerance"/>) and never filters data itself; read the
/// properties in your view model, or test values with <see cref="Matches(decimal)"/> or <see cref="Condition"/>.
/// <para>Typed values snap to multiples of <see cref="SmallChange"/> counted from <see cref="Minimum"/>; by default that
/// is 1 in <see cref="NumberFilterMode.Integer"/> mode and one unit of the last decimal place in
/// <see cref="NumberFilterMode.Decimal"/> mode.</para>
/// </summary>
[TemplatePart(ValuePart, typeof(NumberField))]
public class NumberFilterBox : FilterBox
{
    public const string ValuePart = "PART_Value";

    private const decimal DefaultMaximum = 9007199254740991m;

    #region 公共属性定义

    /// <summary>The comparison (two-way, default <see cref="NumberOperator.Equal"/>).</summary>
    public static readonly StyledProperty<NumberOperator> OperatorProperty =
        AvaloniaProperty.Register<NumberFilterBox, NumberOperator>(nameof(Operator), NumberOperator.Equal,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The target value (two-way, default 0).</summary>
    public static readonly StyledProperty<decimal> ValueProperty =
        AvaloniaProperty.Register<NumberFilterBox, decimal>(nameof(Value), 0m, defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whole numbers or decimals (default <see cref="NumberFilterMode.Decimal"/>).</summary>
    public static readonly StyledProperty<NumberFilterMode> NumberModeProperty =
        AvaloniaProperty.Register<NumberFilterBox, NumberFilterMode>(nameof(NumberMode), NumberFilterMode.Decimal);

    /// <summary>Fraction digits shown and typed in <see cref="NumberFilterMode.Decimal"/> mode (default 2, at most 10).</summary>
    public static readonly StyledProperty<int> DecimalPlacesProperty =
        AvaloniaProperty.Register<NumberFilterBox, int>(nameof(DecimalPlaces), 2);

    /// <summary>Lowest value that can be entered, and origin of the step grid (default 0).</summary>
    public static readonly StyledProperty<decimal> MinimumProperty =
        AvaloniaProperty.Register<NumberFilterBox, decimal>(nameof(Minimum), 0m);

    /// <summary>Highest value that can be entered (default 9007199254740991).</summary>
    public static readonly StyledProperty<decimal> MaximumProperty =
        AvaloniaProperty.Register<NumberFilterBox, decimal>(nameof(Maximum), DefaultMaximum);

    /// <summary>One step of the arrows and the steppers, and the grid typed values snap to (default null: 1, or one unit of the last decimal place).</summary>
    public static readonly StyledProperty<decimal?> SmallChangeProperty =
        AvaloniaProperty.Register<NumberFilterBox, decimal?>(nameof(SmallChange));

    /// <summary>The step of PageUp, PageDown and Shift with an arrow (default null: 10 in Integer mode, 1 in Decimal mode).</summary>
    public static readonly StyledProperty<decimal?> LargeChangeProperty =
        AvaloniaProperty.Register<NumberFilterBox, decimal?>(nameof(LargeChange));

    /// <summary>How far apart two values may be and still count as equal (default 0); see <see cref="NumberFilterCondition"/>.</summary>
    public static readonly StyledProperty<decimal> ToleranceProperty =
        AvaloniaProperty.Register<NumberFilterBox, decimal>(nameof(Tolerance), 0m);

    /// <summary>Group separators in the value (default true).</summary>
    public static readonly StyledProperty<bool> UseGroupingProperty =
        AvaloniaProperty.Register<NumberFilterBox, bool>(nameof(UseGrouping), true);

    /// <summary>The operators the menu offers (default null: all six).</summary>
    public static readonly StyledProperty<IReadOnlyList<NumberOperator>?> OperatorsProperty =
        AvaloniaProperty.Register<NumberFilterBox, IReadOnlyList<NumberOperator>?>(nameof(Operators));

    /// <summary>The current condition (operator, value and tolerance) as plain data.</summary>
    public static readonly DirectProperty<NumberFilterBox, NumberFilterCondition> ConditionProperty =
        AvaloniaProperty.RegisterDirect<NumberFilterBox, NumberFilterCondition>(nameof(Condition), o => o.Condition);

    /// <inheritdoc cref="OperatorProperty"/>
    public NumberOperator Operator { get => GetValue(OperatorProperty); set => SetValue(OperatorProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public decimal Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="NumberModeProperty"/>
    public NumberFilterMode NumberMode { get => GetValue(NumberModeProperty); set => SetValue(NumberModeProperty, value); }

    /// <inheritdoc cref="DecimalPlacesProperty"/>
    public int DecimalPlaces { get => GetValue(DecimalPlacesProperty); set => SetValue(DecimalPlacesProperty, value); }

    /// <inheritdoc cref="MinimumProperty"/>
    public decimal Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    /// <inheritdoc cref="MaximumProperty"/>
    public decimal Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    /// <inheritdoc cref="SmallChangeProperty"/>
    public decimal? SmallChange { get => GetValue(SmallChangeProperty); set => SetValue(SmallChangeProperty, value); }

    /// <inheritdoc cref="LargeChangeProperty"/>
    public decimal? LargeChange { get => GetValue(LargeChangeProperty); set => SetValue(LargeChangeProperty, value); }

    /// <inheritdoc cref="ToleranceProperty"/>
    public decimal Tolerance { get => GetValue(ToleranceProperty); set => SetValue(ToleranceProperty, value); }

    /// <inheritdoc cref="UseGroupingProperty"/>
    public bool UseGrouping { get => GetValue(UseGroupingProperty); set => SetValue(UseGroupingProperty, value); }

    /// <inheritdoc cref="OperatorsProperty"/>
    public IReadOnlyList<NumberOperator>? Operators { get => GetValue(OperatorsProperty); set => SetValue(OperatorsProperty, value); }

    /// <inheritdoc cref="ConditionProperty"/>
    public NumberFilterCondition Condition
    {
        get => _condition;
        private set => SetAndRaise(ConditionProperty, ref _condition, value);
    }

    #endregion

    private NumberFilterCondition _condition = new(NumberOperator.Equal, 0m, 0m);
    private NumberField? _value;

    /// <summary>The fraction digits in use: 0 in Integer mode, <see cref="DecimalPlaces"/> (0 to 10) in Decimal mode.</summary>
    public int EffectiveDecimalPlaces => NumberMode == NumberFilterMode.Integer ? 0 : Math.Clamp(DecimalPlaces, 0, 10);

    /// <summary>
    /// Whether <paramref name="candidate"/> passes this filter: always true while the filter is off
    /// (<see cref="FilterBox.IsActive"/> false), otherwise <see cref="Condition"/>'s answer.
    /// </summary>
    public bool Matches(decimal candidate) => !IsActive || Condition.Matches(candidate);

    protected override IReadOnlyList<object> OperatorChoices()
    {
        var operators = Operators is { Count: > 0 } chosen ? chosen : FilterOperators.NumberOperators;
        var boxed = new object[operators.Count];
        for (var i = 0; i < operators.Count; i++)
        {
            boxed[i] = operators[i];
        }
        return boxed;
    }

    protected override object CurrentOperator() => Operator;

    protected override void PickOperator(object choice)
    {
        if (choice is NumberOperator op)
        {
            SetCurrentValue(OperatorProperty, op);
        }
    }

    protected override string SymbolOf(object choice) => choice is NumberOperator op ? FilterOperators.Symbol(op) : choice.ToString() ?? string.Empty;

    protected override string NameOf(object choice) => choice is NumberOperator op ? FilterOperators.Name(op) : choice.ToString() ?? string.Empty;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        _value = e.NameScope.Find<NumberField>(ValuePart);
        base.OnApplyTemplate(e);
        UpdateNumberFormat();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OperatorProperty)
        {
            SyncOperator();
            UpdateCondition();
        }
        else if (change.Property == ValueProperty || change.Property == ToleranceProperty)
        {
            UpdateCondition();
        }
        else if (change.Property == NumberModeProperty || change.Property == DecimalPlacesProperty
                 || change.Property == SmallChangeProperty || change.Property == LargeChangeProperty)
        {
            UpdateNumberFormat();
        }
        else if (change.Property == OperatorsProperty)
        {
            RefreshOperatorChoices();
        }
    }

    protected override void UpdateAutomation()
    {
        base.UpdateAutomation();
        if (_value is not null)
        {
            _value.Label = LabelText ?? "Value";
        }
    }

    private void UpdateCondition()
    {
        var next = new NumberFilterCondition(Operator, Value, Tolerance);
        if (next != Condition)
        {
            Condition = next;
            RaiseFilterChanged();
        }
    }

    private void UpdateNumberFormat()
    {
        if (_value is null)
        {
            return;
        }
        var digits = EffectiveDecimalPlaces;
        var unit = digits == 0 ? 1m : 1m / Pow10(digits);
        var step = SmallChange is { } small && small > 0 ? small : unit;
        _value.MinimumFractionDigits = digits;
        _value.MaximumFractionDigits = digits;
        _value.Step = step;
        _value.LargeStep = LargeChange is { } large && large > 0 ? large : NumberMode == NumberFilterMode.Integer ? 10m : 1m;
    }

    private static decimal Pow10(int digits)
    {
        var result = 1m;
        for (var i = 0; i < digits; i++)
        {
            result *= 10m;
        }
        return result;
    }
}
