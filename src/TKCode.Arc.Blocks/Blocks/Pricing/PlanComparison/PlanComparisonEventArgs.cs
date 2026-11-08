// Ported from Arc's free plan-comparison block (MIT, Copyright (c) 2026 Elia Kuratli).
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="PlanComparison.SelectedPlanChanged"/>.</summary>
public sealed class PlanComparisonSelectionChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PlanComparisonSelectionChangedEventArgs(RoutedEvent routedEvent, PlanComparisonPlan? oldPlan, PlanComparisonPlan? newPlan, bool isUserAction)
        : base(routedEvent)
    {
        OldPlan = oldPlan;
        NewPlan = newPlan;
        IsUserAction = isUserAction;
    }

    /// <summary>The plan that was selected before, or null.</summary>
    public PlanComparisonPlan? OldPlan { get; }

    /// <summary>The plan selected now, or null when the selection was cleared.</summary>
    public PlanComparisonPlan? NewPlan { get; }

    /// <summary>True when someone pressed a select button; false when the app changed the selection.</summary>
    public bool IsUserAction { get; }
}

/// <summary>Arguments of <see cref="PlanComparison.BillingChanged"/>.</summary>
public sealed class PlanComparisonBillingChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PlanComparisonBillingChangedEventArgs(RoutedEvent routedEvent, PlanComparisonBilling oldBilling, PlanComparisonBilling newBilling)
        : base(routedEvent)
    {
        OldBilling = oldBilling;
        NewBilling = newBilling;
    }

    /// <summary>The billing period before the change.</summary>
    public PlanComparisonBilling OldBilling { get; }

    /// <summary>The billing period now shown.</summary>
    public PlanComparisonBilling NewBilling { get; }
}
