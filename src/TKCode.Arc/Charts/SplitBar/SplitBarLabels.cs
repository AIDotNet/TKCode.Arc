using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The label inside a part of a <see cref="SplitBar"/>: its name (which opens and closes beside the share) and its share,
/// rolling. The track arranges it over the part's visible area, so it slides with the part and is cut cleanly at its
/// edge instead of spilling into a neighbour. Hidden from screen readers: the bar's name carries the same numbers.
/// </summary>
internal sealed class SplitBarInsideLabel : Control
{
    private readonly Border _nameClip = new() { ClipToBounds = true };
    private readonly TextBlock _name = new() { TextWrapping = TextWrapping.NoWrap };
    private readonly RollingDigits _share = new() { Decimals = 1, Suffix = "%", Falloff = RollingDigitsFalloff.Linear };
    private double _open = 1;
    private double _gap = 5;
    private double _offset;
    private double _lineHeight = 16.8;

    public SplitBarInsideLabel(SplitBarTrack track)
    {
        ClipToBounds = true;
        IsHitTestVisible = false;
        _name.FontSize = track.LabelFontSize;
        _name.FontWeight = FontWeight.Normal;
        _share.FontSize = track.LabelFontSize;
        _share.FontWeight = FontWeight.Medium;
        _share.WheelMotion = SplitBar.DigitMotion;
        ArcTokens.Bind(_name, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_share, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _nameClip.Child = _name;
        VisualChildren.Add(_nameClip);
        LogicalChildren.Add(_nameClip);
        VisualChildren.Add(_share);
        LogicalChildren.Add(_share);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        IsFresh = true;
    }

    /// <summary>True until the first texts are set (they show at once).</summary>
    public bool IsFresh { get; private set; }

    /// <summary>The name and share shown (tests).</summary>
    public string? NameText => _name.Text;

    public double ShareValue => _share.Value;

    /// <summary>How open the name is now (tests).</summary>
    public double Open => _open;

    public void Set(string? label, double share, bool instant)
    {
        _name.Text = label;
        _share.IsInstant = instant;
        _share.Value = Math.Round(share * 1000) / 10;
        IsFresh = false;
    }

    /// <summary>Lays the content out: the name <paramref name="open"/> wide, starting <paramref name="offset"/> into the box.</summary>
    public void Layout(double open, double gap, double offset, double lineHeight)
    {
        _open = open;
        _gap = gap;
        _offset = offset;
        _lineHeight = lineHeight;
        // The name fades a little faster than it closes, as in Arc.
        _nameClip.Opacity = Math.Pow(open, 1.3);
        _nameClip.IsVisible = open > 0.001;
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _nameClip.Measure(Size.Infinity);
        _share.Measure(Size.Infinity);
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var name = _nameClip.DesiredSize;
        var share = _share.DesiredSize;
        var y = (finalSize.Height - _lineHeight) / 2;
        var nameWidth = name.Width * _open;
        _nameClip.Arrange(new Rect(_offset, y + (_lineHeight - name.Height) / 2, nameWidth, name.Height));
        var x = _offset + nameWidth + _gap * _open;
        _share.Arrange(new Rect(x, y + (_lineHeight - share.Height) / 2, share.Width, share.Height));
        return finalSize;
    }
}

/// <summary>
/// A label in the lane under a <see cref="SplitBar"/> for a part too narrow to hold its own: the name and the share,
/// joined to the part by a leader line the track draws.
/// </summary>
internal sealed class SplitBarLaneLabel : Control
{
    private readonly TextBlock _name = new() { TextWrapping = TextWrapping.NoWrap };
    private readonly RollingDigits _share = new() { Decimals = 1, Suffix = "%", Falloff = RollingDigitsFalloff.Linear };
    private readonly double _gap;

    public SplitBarLaneLabel(SplitBarTrack track)
    {
        IsHitTestVisible = false;
        _gap = track.LaneNameGap;
        _name.FontSize = track.LabelFontSize;
        _name.FontWeight = FontWeight.Normal;
        _share.FontSize = track.LabelFontSize;
        _share.FontWeight = FontWeight.Medium;
        _share.WheelMotion = SplitBar.DigitMotion;
        ArcTokens.Bind(_name, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        ArcTokens.Bind(_share, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        VisualChildren.Add(_name);
        LogicalChildren.Add(_name);
        VisualChildren.Add(_share);
        LogicalChildren.Add(_share);
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        IsFresh = true;
    }

    public bool IsFresh { get; private set; }

    public string? NameText => _name.Text;

    public double ShareValue => _share.Value;

    public void Set(string? label, double share, bool instant)
    {
        _name.Text = label;
        _share.IsInstant = instant;
        _share.Value = Math.Round(share * 1000) / 10;
        IsFresh = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _name.Measure(Size.Infinity);
        _share.Measure(Size.Infinity);
        return new Size(_name.DesiredSize.Width + _gap + _share.DesiredSize.Width,
            Math.Max(_name.DesiredSize.Height, _share.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var name = _name.DesiredSize;
        var share = _share.DesiredSize;
        _name.Arrange(new Rect(0, (finalSize.Height - name.Height) / 2, name.Width, name.Height));
        _share.Arrange(new Rect(name.Width + _gap, (finalSize.Height - share.Height) / 2, share.Width, share.Height));
        return finalSize;
    }
}
