// Ported from Arc's free plan-comparison block (MIT, Copyright (c) 2026 Elia Kuratli).
using Avalonia;
using Avalonia.Collections;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>The billing period a <see cref="PlanComparison"/> prices its plans for.</summary>
public enum PlanComparisonBilling
{
    /// <summary>Billed month to month (<see cref="PlanComparisonPlan.MonthlyPrice"/>).</summary>
    Monthly,

    /// <summary>Billed once a year (<see cref="PlanComparisonPlan.YearlyPrice"/>, shown per month).</summary>
    Yearly
}

/// <summary>
/// One plan column of a <see cref="PlanComparison"/>: its name, a one-line description and its price per seat and month
/// for each billing period. Changes reach the block while it is shown.
/// </summary>
/// <example><code>&lt;arc:PlanComparisonPlan Name="Team" Description="For smaller teams" MonthlyPrice="13" YearlyPrice="10" /&gt;</code></example>
public class PlanComparisonPlan : AvaloniaObject
{
    /// <summary>The plan's name, such as "Team"; the column header and the select button's label.</summary>
    public static readonly StyledProperty<string> NameProperty =
        AvaloniaProperty.Register<PlanComparisonPlan, string>(nameof(Name), string.Empty);

    /// <summary>A short line under the name, such as "For smaller teams".</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<PlanComparisonPlan, string?>(nameof(Description));

    /// <summary>The price per seat and month when billed monthly, in the block's currency.</summary>
    public static readonly StyledProperty<decimal> MonthlyPriceProperty =
        AvaloniaProperty.Register<PlanComparisonPlan, decimal>(nameof(MonthlyPrice));

    /// <summary>The price per seat and month when billed yearly, in the block's currency.</summary>
    public static readonly StyledProperty<decimal> YearlyPriceProperty =
        AvaloniaProperty.Register<PlanComparisonPlan, decimal>(nameof(YearlyPrice));

    /// <summary>Creates an empty plan (set its properties in XAML or code).</summary>
    public PlanComparisonPlan()
    {
    }

    /// <summary>Creates a plan with its name, description and prices.</summary>
    public PlanComparisonPlan(string name, string? description, decimal monthlyPrice, decimal yearlyPrice)
    {
        Name = name;
        Description = description;
        MonthlyPrice = monthlyPrice;
        YearlyPrice = yearlyPrice;
    }

    /// <inheritdoc cref="NameProperty"/>
    public string Name
    {
        get => GetValue(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <inheritdoc cref="MonthlyPriceProperty"/>
    public decimal MonthlyPrice
    {
        get => GetValue(MonthlyPriceProperty);
        set => SetValue(MonthlyPriceProperty, value);
    }

    /// <inheritdoc cref="YearlyPriceProperty"/>
    public decimal YearlyPrice
    {
        get => GetValue(YearlyPriceProperty);
        set => SetValue(YearlyPriceProperty, value);
    }

    /// <summary>The price per seat and month for <paramref name="billing"/>.</summary>
    public decimal PriceFor(PlanComparisonBilling billing) => billing == PlanComparisonBilling.Yearly ? YearlyPrice : MonthlyPrice;

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// One row of a <see cref="PlanComparison"/>'s feature matrix: the feature's name and what each plan offers, in the order
/// of the block's plans. A value equal to <see cref="PlanComparisonLabels.Included"/> shows a check, one equal to
/// <see cref="PlanComparisonLabels.NotIncluded"/> is muted. Rows that every plan shares (<see cref="IsShared"/>) are
/// hidden by "Show differences only".
/// </summary>
/// <example><code>
/// &lt;arc:PlanComparisonFeature Label="Version history"&gt;
///     &lt;x:String&gt;30 days&lt;/x:String&gt;
///     &lt;x:String&gt;Unlimited&lt;/x:String&gt;
/// &lt;/arc:PlanComparisonFeature&gt;
/// </code></example>
public class PlanComparisonFeature : AvaloniaObject
{
    /// <summary>The feature's name, such as "Version history"; the row header.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<PlanComparisonFeature, string>(nameof(Label), string.Empty);

    /// <summary>True when every plan offers the same, so the row hides while only differences are shown.</summary>
    public static readonly StyledProperty<bool> IsSharedProperty =
        AvaloniaProperty.Register<PlanComparisonFeature, bool>(nameof(IsShared));

    /// <summary>Creates an empty feature (set its label and add one value per plan).</summary>
    public PlanComparisonFeature()
    {
    }

    /// <summary>Creates a feature with its label, whether every plan shares it, and one value per plan.</summary>
    public PlanComparisonFeature(string label, bool isShared, params string[] values)
    {
        Label = label;
        IsShared = isShared;
        Values.AddRange(values);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <inheritdoc cref="IsSharedProperty"/>
    public bool IsShared
    {
        get => GetValue(IsSharedProperty);
        set => SetValue(IsSharedProperty, value);
    }

    /// <summary>What each plan offers, in the order of the block's plans; a missing value shows as empty.</summary>
    [Content]
    public AvaloniaList<string> Values { get; } = new();

    /// <summary>The value for the plan at <paramref name="index"/>, or an empty string when there is none.</summary>
    public string ValueAt(int index) => index >= 0 && index < Values.Count ? Values[index] ?? string.Empty : string.Empty;

    /// <inheritdoc />
    public override string ToString() => Label;
}
