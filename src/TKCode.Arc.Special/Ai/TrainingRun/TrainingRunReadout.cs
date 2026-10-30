using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// A counting value of a <see cref="TrainingRun"/> on Core <see cref="RollingDigits"/> wheels: the step ("940 / 2,400"),
/// the learning rate ("1.84e-4", the mantissa rolls) and the times ("12m 4s", each unit on its own wheels, "About" before
/// a queued ETA), or a dash while the value is not known. <see cref="Text"/> is the plain reading, also its accessible name.
/// </summary>
internal sealed class TrainingRunReadout : StackPanel
{
    private readonly TextBlock _prefix = new() { VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
    private readonly RollingDigits _first = new() { VerticalAlignment = VerticalAlignment.Center, IsShortestTurn = true };
    private readonly RollingDigits _second = new() { VerticalAlignment = VerticalAlignment.Center, IsShortestTurn = true, IsVisible = false };
    private readonly TextBlock _missing = new() { VerticalAlignment = VerticalAlignment.Center, IsVisible = false };

    public TrainingRunReadout()
    {
        Orientation = Orientation.Horizontal;
        Children.Add(_prefix);
        Children.Add(_first);
        Children.Add(_second);
        Children.Add(_missing);
    }

    /// <summary>The plain reading ("12m 4s", "1.84e-4", "–").</summary>
    public string Text { get; private set; } = string.Empty;

    internal RollingDigits First => _first;

    internal RollingDigits Second => _second;

    /// <summary>Shows <paramref name="dash"/> in place of a value.</summary>
    public void ShowMissing(string dash)
    {
        _prefix.IsVisible = false;
        _first.IsVisible = false;
        _second.IsVisible = false;
        _missing.IsVisible = true;
        _missing.Text = dash;
        SetText(dash);
    }

    /// <summary>Shows one number with <paramref name="decimals"/> and a suffix ("940 / 2,400", "1.84e-4").</summary>
    public void ShowNumber(double value, int decimals, string? suffix, string text)
    {
        _prefix.IsVisible = false;
        _missing.IsVisible = false;
        _second.IsVisible = false;
        _first.IsVisible = true;
        _first.Decimals = decimals;
        _first.Suffix = suffix;
        _first.Value = value;
        SetText(text);
    }

    /// <summary>Shows a time ("42s", "12m 4s", "1h 4m"), with <paramref name="prefix"/> before it when given.</summary>
    public void ShowTime(TimeSpan time, TrainingRunLabels labels, string? prefix)
    {
        var (big, bigUnit, small, smallUnit) = TrainingRunFormat.Parts(time);
        _missing.IsVisible = false;
        _prefix.IsVisible = prefix is not null;
        _prefix.Text = prefix is null ? null : prefix + " ";
        _first.Decimals = 0;
        _second.Decimals = 0;
        if (bigUnit == TrainingRunTimeUnit.None)
        {
            _first.IsVisible = false;
            _second.IsVisible = true;
            _second.Suffix = TrainingRunFormat.Unit(smallUnit, labels);
            _second.Value = small;
        }
        else
        {
            _first.IsVisible = true;
            _second.IsVisible = true;
            _first.Suffix = TrainingRunFormat.Unit(bigUnit, labels) + " ";
            _first.Value = big;
            _second.Suffix = TrainingRunFormat.Unit(smallUnit, labels);
            _second.Value = small;
        }
        var text = TrainingRunFormat.Time(time, labels);
        SetText(prefix is null ? text : prefix + " " + text);
    }

    private void SetText(string text)
    {
        if (Text == text)
        {
            return;
        }
        Text = text;
        AutomationProperties.SetName(this, text);
    }
}
