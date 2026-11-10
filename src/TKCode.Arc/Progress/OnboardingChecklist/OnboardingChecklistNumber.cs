// Ported from Arc Onboarding checklist (MIT, Copyright (c) 2026 Elia Kuratli): RollingNumber.
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Digits turn in the direction the count moved, one column per place value; a column that joins or leaves folds its
/// width. Core's RollingDigits turns an odometer wheel; the source replaces each digit instead.
/// </summary>
internal sealed class OnboardingChecklistNumber : StackPanel
{
    private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

    private sealed class Place
    {
        public required OnboardingChecklistFold Fold { get; init; }
        public required OnboardingChecklistSwap Swap { get; init; }
        public string? Digit;
    }

    // Index 0 is the ones column.
    private readonly List<Place> _places = new();
    private int? _value;

    public OnboardingChecklistNumber()
    {
        Orientation = Orientation.Horizontal;
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>The number being shown.</summary>
    public int Value => _value ?? 0;

    public void Set(int value, bool animate)
    {
        if (_value == value)
        {
            return;
        }
        var direction = value >= (_value ?? value) ? 1 : -1;
        _value = value;
        var digits = value.ToString(CultureInfo.InvariantCulture);
        for (var place = 0; place < digits.Length; place++)
        {
            var slot = PlaceAt(place);
            slot.Fold.SetOpen(true, animate);
            var digit = digits[digits.Length - 1 - place].ToString();
            if (digit != slot.Digit)
            {
                slot.Digit = digit;
                slot.Swap.Show(new TextBlock { Text = digit, FontFeatures = s_tabular }, direction, animate);
            }
        }
        for (var place = digits.Length; place < _places.Count; place++)
        {
            _places[place].Fold.SetOpen(false, animate);
        }
    }

    private Place PlaceAt(int index)
    {
        while (_places.Count <= index)
        {
            var swap = new OnboardingChecklistSwap { Kind = OnboardingChecklistSwapKind.Roll };
            var fold = new OnboardingChecklistFold { Kind = OnboardingChecklistFoldKind.Place, Child = swap };
            // Every column starts folded; opening it plays or jumps as the caller asks.
            fold.SetOpen(false, false);
            _places.Add(new Place { Fold = fold, Swap = swap });
            Children.Insert(0, fold);
        }
        return _places[index];
    }
}
