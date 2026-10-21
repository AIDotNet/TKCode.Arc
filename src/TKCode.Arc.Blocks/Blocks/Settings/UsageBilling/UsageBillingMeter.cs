using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One metered item of a <see cref="UsageBilling"/> period, such as API calls or storage: how much was used, how much the
/// plan includes, and the price of what goes over. <see cref="Overage"/> and <see cref="Cost"/> follow the other values,
/// and every change is announced through <see cref="PropertyChanged"/>, so the block's totals, bars and projection update
/// as soon as usage is added.
/// </summary>
/// <example><code>&lt;arc:UsageBillingMeter Name="API calls" Unit="calls" Used="1240000" Included="1000000" UnitPrice="0.40" PriceUnit="10000" /&gt;</code></example>
public sealed class UsageBillingMeter : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _unit = string.Empty;
    private double _used;
    private double _included;
    private decimal _unitPrice;
    private double _priceUnit = 1;
    private Geometry? _icon;

    /// <summary>Raised when any property changes, including the computed <see cref="Overage"/> and <see cref="Cost"/>.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>What is measured, such as "API calls"; the row's header.</summary>
    public string Name
    {
        get => _name;
        set => Set(ref _name, value ?? string.Empty);
    }

    /// <summary>The unit after the amounts, such as "calls", "GB" or "min".</summary>
    public string Unit
    {
        get => _unit;
        set => Set(ref _unit, value ?? string.Empty);
    }

    /// <summary>The amount used so far this period, in <see cref="Unit"/>.</summary>
    public double Used
    {
        get => _used;
        set
        {
            if (Set(ref _used, Sanitize(value)))
            {
                RaiseComputed();
            }
        }
    }

    /// <summary>The amount the plan includes at no extra cost, in <see cref="Unit"/>; with 0 the row shows the usage without a bar.</summary>
    public double Included
    {
        get => _included;
        set
        {
            if (Set(ref _included, Sanitize(value)))
            {
                RaiseComputed();
            }
        }
    }

    /// <summary>The price of <see cref="PriceUnit"/> units beyond the included amount, in the block's currency.</summary>
    public decimal UnitPrice
    {
        get => _unitPrice;
        set
        {
            if (Set(ref _unitPrice, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(Cost));
            }
        }
    }

    /// <summary>How many units <see cref="UnitPrice"/> pays for, such as 10000 for "$0.40 per 10K calls" (default 1).</summary>
    public double PriceUnit
    {
        get => _priceUnit;
        set
        {
            if (Set(ref _priceUnit, double.IsFinite(value) && value > 0 ? value : 1))
            {
                OnPropertyChanged(nameof(Cost));
            }
        }
    }

    /// <summary>A 24×24 stroke icon (for example an <see cref="ArcIcons"/> member) shown in the row's tile.</summary>
    public Geometry? Icon
    {
        get => _icon;
        set => Set(ref _icon, value);
    }

    /// <summary>The amount past the included one: <c>max(0, Used − Included)</c>.</summary>
    public double Overage => Math.Max(0, _used - _included);

    /// <summary>What the overage costs: <c>Overage ÷ PriceUnit × UnitPrice</c>.</summary>
    public decimal Cost
    {
        get
        {
            var units = Overage / _priceUnit;
            if (units <= 0 || _unitPrice <= 0)
            {
                return 0m;
            }
            // Beyond 1e15 billable units the decimal conversion would overflow; no real bill gets there.
            return (decimal)Math.Min(units, 1e15) * _unitPrice;
        }
    }

    private static double Sanitize(double value) => double.IsFinite(value) ? Math.Max(0, value) : 0;

    private void RaiseComputed()
    {
        OnPropertyChanged(nameof(Overage));
        OnPropertyChanged(nameof(Cost));
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
