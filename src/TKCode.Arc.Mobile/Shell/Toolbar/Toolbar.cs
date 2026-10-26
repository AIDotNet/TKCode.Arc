using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using TKCode.Arc.Motion;

namespace TKCode.Arc.Mobile;

/// <summary>
/// The bottom action bar of a <see cref="Page"/>: 44 high with a hairline above it. Put plain <see cref="Button"/>s in
/// it as items and they share the width equally, or fill the <see cref="Leading"/>, <see cref="Center"/> and
/// <see cref="Trailing"/> slots. It is meant for <see cref="Page.Footer"/>, alone or in a stack above a <see cref="Tabbar"/>.
/// <para>
/// <see cref="IsOpen"/> shows and hides the bar: it slides down and fades out (faster than it came), and slides up and
/// fades in. The position is a <see cref="MotionValue{T}"/>, not a ControlTheme transition, because a toggle that flips
/// mid-flight must continue from where the bar is rather than restart. Under reduced motion the bar jumps. A closed
/// bar keeps its place in the layout, so the content does not jump; set IsVisible to false to remove it.
/// </para>
/// </summary>
[TemplatePart("PART_Bar", typeof(Border))]
public class Toolbar : ItemsControl
{
    /// <summary>The distance the bar slides while it hides, in px: its own height.</summary>
    public const double SlideDistance = 44;

    /// <summary>Whether the bar is shown. False slides it away and stops it catching touches.</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<Toolbar, bool>(nameof(IsOpen), true);

    /// <summary>The content at the start of the bar, usually one action.</summary>
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<Toolbar, object?>(nameof(Leading));

    /// <summary>The content in the middle of the bar, next to the items.</summary>
    public static readonly StyledProperty<object?> CenterProperty =
        AvaloniaProperty.Register<Toolbar, object?>(nameof(Center));

    /// <summary>The content at the end of the bar, usually one action.</summary>
    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<Toolbar, object?>(nameof(Trailing));

    private readonly TranslateTransform _translate = new();
    private Border? _bar;
    private MotionValue<double>? _presence;

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="LeadingProperty"/>
    public object? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    /// <inheritdoc cref="CenterProperty"/>
    public object? Center
    {
        get => GetValue(CenterProperty);
        set => SetValue(CenterProperty, value);
    }

    /// <inheritdoc cref="TrailingProperty"/>
    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    /// <summary>How far the bar is shown: 1 open, 0 closed. For tests and diagnostics.</summary>
    public double Presence => _presence?.Value ?? (IsOpen ? 1 : 0);

    protected override Type StyleKeyOverride => typeof(Toolbar);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _presence?.Dispose();
        _presence = null;
        _bar = e.NameScope.Find<Border>("PART_Bar");
        if (_bar is null)
        {
            return;
        }
        _bar.RenderTransform = _translate;
        // No motion on the first frame: the bar starts where IsOpen says.
        _presence = new MotionValue<double>(_bar, IsOpen ? 1 : 0, MotionVectors.Double);
        _presence.Changed += _ => Apply();
        Apply();
        UpdateHitTest();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty)
        {
            Run(change.GetNewValue<bool>());
        }
    }

    private void Run(bool open)
    {
        UpdateHitTest();
        if (_presence is null)
        {
            return;
        }
        var target = open ? 1.0 : 0.0;
        if (ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Full || !IsLoaded)
        {
            _presence.Stop();
            _presence.Jump(target);
            return;
        }
        // Arriving settles on a spring; leaving is a short tween, faster than arriving.
        var spec = open
            ? MotionSpec.Spring(ArcSpring.Snappy)
            : MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard);
        _ = _presence.AnimateTo(target, spec);
    }

    private void UpdateHitTest()
    {
        if (_bar is not null)
        {
            _bar.IsHitTestVisible = IsOpen;
        }
    }

    private void Apply()
    {
        if (_presence is null || _bar is null)
        {
            return;
        }
        var p = Math.Clamp(_presence.Value, 0, 1);
        _bar.Opacity = p;
        _translate.Y = (1 - p) * SlideDistance;
    }
}
