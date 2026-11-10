using System.Globalization;
using Avalonia;
using TKCode.Arc.DesignTokens;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// One legend row of an <see cref="ActivityRings"/>: a small Core <see cref="ArcRing"/> in the ring's color, the goal's
/// name, and the value rolling on Core <see cref="RollingDigits"/> next to "/ goal unit".
/// </summary>
internal sealed class ActivityRingsLegendRow : Grid
{
    private readonly ArcRing _mark = new() { VerticalAlignment = VerticalAlignment.Center, ChaseRate = 0.18 };
    private readonly TextBlock _label = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(10, 0, 12, 0)
    };
    private readonly RollingDigits _value = new() { VerticalAlignment = VerticalAlignment.Center, IsArmed = true, UseGrouping = true };
    private readonly TextBlock _goal = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
    private readonly StackPanel _numbers = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    private ActivityRing? _ring;

    public ActivityRingsLegendRow()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        SetColumn(_label, 1);
        SetColumn(_numbers, 2);
        _numbers.Children.Add(_value);
        _numbers.Children.Add(_goal);
        Children.Add(_mark);
        Children.Add(_label);
        Children.Add(_numbers);
        ArcTokens.Bind(_label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_value, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_value, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(_goal, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(_goal, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        _value.FontWeight = FontWeight.SemiBold;
        // The row is read through the rings' own description, not part by part.
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
    }

    /// <summary>How strongly the small ring's track shows the ring's color.</summary>
    private const double TrackOpacity = 0.22;

    public ActivityRing? Ring
    {
        get => _ring;
        set
        {
            _ring = value;
            Update();
        }
    }

    /// <summary>The small ring (tests).</summary>
    internal ArcRing Mark => _mark;

    /// <summary>The rolling value (tests).</summary>
    internal RollingDigits ValueDigits => _value;

    /// <summary>The goal text, "/ 600 kcal" (tests).</summary>
    internal string? GoalText => _goal.Text;

    /// <summary>The goal's name as shown (tests).</summary>
    internal string? LabelText => _label.Text;

    public void SetRingSize(double size, double thickness)
    {
        _mark.Width = size;
        _mark.Height = size;
        _mark.Thickness = thickness;
    }

    public void SetBrush(IBrush? brush)
    {
        _mark.Foreground = brush;
        _mark.TrackBrush = brush is ISolidColorBrush solid
            ? new ImmutableSolidColorBrush(solid.Color, solid.Opacity * TrackOpacity)
            : null;
    }

    /// <summary>The first view: the value rolls up from zero.</summary>
    public void Reveal() => _value.IsArmed = false;

    public void Update()
    {
        if (_ring is not { } ring)
        {
            return;
        }
        _label.Text = ring.Label;
        _value.Value = Math.Round(Math.Max(0, ring.Value));
        var unit = string.IsNullOrEmpty(ring.Unit) ? string.Empty : " " + ring.Unit;
        _goal.Text = string.Create(CultureInfo.InvariantCulture, $"/ {Math.Max(0, ring.Goal):#,0.##}{unit}");
        _mark.Value = Math.Min(1, ring.Progress);
    }
}
