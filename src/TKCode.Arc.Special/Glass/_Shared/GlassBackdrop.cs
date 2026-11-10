using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// Tells glass surfaces (<see cref="GlassCard"/>, <see cref="GlassMenu"/>, <see cref="GlassSegmented"/>,
/// <see cref="GlassTabBar"/>) what lies behind them. Set <c>arc:GlassBackdrop.Source</c> once on a container and every
/// glass surface inside it shows a blurred, tinted copy of that visual, lined up with the part it covers.
/// </summary>
/// <remarks>
/// <para>Avalonia applies effects to an element's own drawing only; it has no filter for whatever is behind an element.
/// Glass therefore draws the backdrop again under itself (a <c>VisualBrush</c> of the source, aligned to the glass) and
/// blurs that copy with a <c>BlurEffect</c> on its own layer, clipped to its corners. The source must be a layer behind the
/// glass, not one of its ancestors: a page's background image, an illustration, a map or a list scrolled under a bar.
/// </para>
/// <para>The copy is drawn again when the glass moves or resizes relative to the source and when the theme changes. When
/// the source animates or changes on its own (a video, a live chart), set <see cref="IsLiveProperty"/> so the copy is
/// redrawn every frame while the glass is on screen. Without a source, or when the ArcDesign <c>ReduceTransparency</c>
/// token is on, glass is a tinted or an opaque surface instead.</para>
/// </remarks>
public sealed class GlassBackdrop
{
    /// <summary>
    /// The visual seen blurred through the glass surfaces inside this element (inherited). Null: the glass is a tinted
    /// translucent surface without blur.
    /// </summary>
    public static readonly AttachedProperty<Visual?> SourceProperty =
        AvaloniaProperty.RegisterAttached<GlassBackdrop, Visual, Visual?>("Source", inherits: true);

    /// <summary>
    /// Whether the glass redraws its copy of <see cref="SourceProperty"/> every frame while it is on screen (inherited;
    /// false by default). Turn it on for a source that changes on its own; a still source only needs the copy drawn when
    /// the glass moves.
    /// </summary>
    public static readonly AttachedProperty<bool> IsLiveProperty =
        AvaloniaProperty.RegisterAttached<GlassBackdrop, Visual, bool>("IsLive", inherits: true);

    private GlassBackdrop()
    {
    }

    /// <summary>Reads <see cref="SourceProperty"/>.</summary>
    public static Visual? GetSource(Visual element) => element.GetValue(SourceProperty);

    /// <summary>Sets <see cref="SourceProperty"/>.</summary>
    public static void SetSource(Visual element, Visual? value) => element.SetValue(SourceProperty, value);

    /// <summary>Reads <see cref="IsLiveProperty"/>.</summary>
    public static bool GetIsLive(Visual element) => element.GetValue(IsLiveProperty);

    /// <summary>Sets <see cref="IsLiveProperty"/>.</summary>
    public static void SetIsLive(Visual element, bool value) => element.SetValue(IsLiveProperty, value);
}
