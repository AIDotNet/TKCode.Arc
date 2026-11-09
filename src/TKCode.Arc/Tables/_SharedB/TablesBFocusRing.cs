using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;

namespace TKCode.Arc;

/// <summary>
/// A focus ring built the first time it is shown, for the Tables rings whose margin is a literal in the theme rather
/// than a token (<see cref="LazyFocusRing"/> binds its margin to a resource key). Same part: a Border appended to the
/// template's layer panel with the theme's focus color and edge, the control's radius token and the given margin.
/// </summary>
internal sealed class TablesBFocusRing
{
    private readonly Thickness _margin;
    private readonly object _radiusKey;
    private readonly string _name;
    private Panel? _host;
    private Border? _ring;

    /// <param name="margin">The ring's margin (negative: outside the face).</param>
    /// <param name="radiusKey">Resource key of the ring's corner radius.</param>
    /// <param name="name">The part name (<c>PART_FocusRing</c> unless the theme used another).</param>
    public TablesBFocusRing(Thickness margin, object radiusKey, string name = LazyFocusRing.PartName)
    {
        _margin = margin;
        _radiusKey = radiusKey;
        _name = name;
    }

    /// <summary>The ring once it was needed; null before.</summary>
    public Border? Ring => _ring;

    /// <summary>Places the ring in <paramref name="host"/> (the new template's layer panel) once it is needed.</summary>
    public void Attach(Panel? host)
    {
        if (_ring is not null)
        {
            _host?.Children.Remove(_ring);
        }
        _ring = null;
        _host = host;
    }

    /// <summary>Shows or hides the ring; builds it on the first show (so it never animates in).</summary>
    public void Update(bool visible)
    {
        if (_ring is null)
        {
            if (!visible || _host is null)
            {
                return;
            }
            _ring = new Border
            {
                Name = _name,
                IsHitTestVisible = false,
                Margin = _margin
            };
            _ring.Bind(Border.CornerRadiusProperty, _ring.GetResourceObservable(_radiusKey), BindingPriority.Template);
            ArcTokens.Bind(_ring, Border.BorderThicknessProperty, ArcDesignTokenKind.FocusEdgeThickness);
            ArcTokens.Bind(_ring, Border.BorderBrushProperty, ArcDesignTokenKind.FocusRing);
            _host.Children.Add(_ring);
        }
        _ring.IsVisible = visible;
    }
}
