using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace TKCode.Arc.Primitives;

/// <summary>
/// A control's keyboard focus ring, built the first time the control shows <c>:focus-visible</c> instead of with every
/// template. A template-declared ring is a hidden Border with four token bindings in every instance; most controls are
/// never focused by keyboard, so a list of buttons paid for rings it never drew.
/// </summary>
/// <remarks>
/// The ring is a <see cref="Border"/> named <c>PART_FocusRing</c> appended to a panel of the owner's template, or to the
/// owner itself (as a logical and visual child, so the template needs no panel just to hold it), with the theme's focus
/// color and edge and the owner's own margin and radius tokens. Call <c>Attach</c> from <c>OnApplyTemplate</c> and
/// <see cref="Update"/> from <c>OnGotFocus</c> and <c>OnLostFocus</c> (and whenever <see cref="IsAllowed"/> changes).
/// </remarks>
public sealed class LazyFocusRing
{
    /// <summary>The name the ring part keeps, as in the templates that declare it.</summary>
    public const string PartName = "PART_FocusRing";

    private const string FocusVisiblePseudoClass = ":focus-visible";

    private readonly Control _owner;
    private readonly object _marginKey;
    private readonly object _radiusKey;
    private Action<Control>? _add;
    private Action<Control>? _remove;
    private Border? _ring;

    /// <param name="owner">The focusable control.</param>
    /// <param name="marginKey">Resource key of the ring margin (negative: the ring sits outside the face).</param>
    /// <param name="radiusKey">Resource key of the ring corner radius.</param>
    public LazyFocusRing(Control owner, object marginKey, object radiusKey)
    {
        _owner = owner;
        _marginKey = marginKey;
        _radiusKey = radiusKey;
    }

    /// <summary>The ring once it was needed; null before.</summary>
    public Border? Ring => _ring;

    /// <summary>Extra condition for showing the ring (besides <c>:focus-visible</c>); true by default.</summary>
    public bool IsAllowed { get; set; } = true;

    /// <summary>
    /// Places the ring in <paramref name="host"/> (the new template's panel); null when the template has none. With
    /// <paramref name="below"/> the ring is inserted first, under the panel's other children.
    /// </summary>
    public void Attach(Panel? host, bool below = false)
    {
        if (host is null)
        {
            Attach(null, null);
            return;
        }
        if (below)
        {
            Attach(ring => host.Children.Insert(0, ring), ring => host.Children.Remove(ring));
            return;
        }
        Attach(ring => host.Children.Add(ring), ring => host.Children.Remove(ring));
    }

    /// <summary>
    /// Places the ring with <paramref name="add"/> once it is needed and takes it out with <paramref name="remove"/> when
    /// the owner attaches again (a new template). A control that hosts the ring itself adds it to its logical children (for
    /// the token lookups) and to its visual children last, so it draws above the template.
    /// </summary>
    public void Attach(Action<Control>? add, Action<Control>? remove)
    {
        if (_ring is not null)
        {
            _remove?.Invoke(_ring);
        }
        _add = add;
        _remove = remove;
        _ring = null;
        Update();
    }

    /// <summary>Shows the ring while the owner is keyboard focused; builds it the first time.</summary>
    public void Update()
    {
        var visible = IsAllowed && _owner.Classes.Contains(FocusVisiblePseudoClass);
        if (_ring is null)
        {
            if (!visible || _add is null)
            {
                return;
            }
            _ring = Create();
            _add(_ring);
        }
        _ring.IsVisible = visible;
    }

    private Border Create()
    {
        var ring = new Border
        {
            Name = PartName,
            IsHitTestVisible = false
        };
        ring.Bind(Avalonia.Layout.Layoutable.MarginProperty, ring.GetResourceObservable(_marginKey), BindingPriority.Template);
        ring.Bind(Border.CornerRadiusProperty, ring.GetResourceObservable(_radiusKey), BindingPriority.Template);
        ArcTokens.Bind(ring, Border.BorderThicknessProperty, ArcDesignTokenKind.FocusEdgeThickness);
        ArcTokens.Bind(ring, Border.BorderBrushProperty, ArcDesignTokenKind.FocusRing);
        return ring;
    }
}
