using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The panel of an <see cref="OrbitMenu"/>'s actions: every action sits on the trigger's center and takes no room, and
/// the menu moves each one out to its place on the arc with a render transform, so opening never lays anything out.
/// </summary>
internal sealed class OrbitMenuOrbit : Panel
{
    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(double.PositiveInfinity, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(infinite);
        }
        return default;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var center = new Point(finalSize.Width / 2, finalSize.Height / 2);
        foreach (var child in Children)
        {
            var size = child.DesiredSize;
            child.Arrange(new Rect(center.X - size.Width / 2, center.Y - size.Height / 2, size.Width, size.Height));
        }
        return finalSize;
    }
}

/// <summary>The round trigger of an <see cref="OrbitMenu"/>; its glyph turns while the menu is open.</summary>
[PseudoClasses(OpenPseudoClass)]
internal sealed class OrbitMenuTrigger : AvaloniaButton
{
    internal const string OpenPseudoClass = ":open";

    /// <summary>The 24 × 24 stroked glyph.</summary>
    public static readonly StyledProperty<Avalonia.Media.Geometry?> GlyphProperty =
        AvaloniaProperty.Register<OrbitMenuTrigger, Avalonia.Media.Geometry?>(nameof(Glyph));

    public Avalonia.Media.Geometry? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    private LazyFocusRing? _focusRing;

    internal void SetOpen(bool open) => PseudoClasses.Set(OpenPseudoClass, open);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, OrbitMenuTokens.Key(OrbitMenuTokenKind.TriggerFocusRingMargin),
            ArcDesignTokens.Key(ArcDesignTokenKind.RadiusPill));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"), below: true);
    }

    protected override void OnGotFocus(Avalonia.Input.FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(Avalonia.Input.FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>Carries the action an <see cref="OrbitMenu"/> invoked.</summary>
public sealed class OrbitMenuActionEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments for <paramref name="action"/>.</summary>
    public OrbitMenuActionEventArgs(RoutedEvent routedEvent, OrbitMenuAction action) : base(routedEvent)
    {
        Action = action;
    }

    /// <summary>The action that was invoked.</summary>
    public OrbitMenuAction Action { get; }
}
