using Avalonia;
using Avalonia.Media;

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
/// <para>With <see cref="ModeProperty"/> set to <see cref="GlassBackdropMode.Native"/> the glass uses the compositor's
/// backdrop effect instead (Avalonia 12.2): it blurs whatever is already drawn behind the glass, needs no source and draws
/// nothing twice. See <see cref="GlassBackdropMode"/> for where it cannot work.</para>
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
    /// the glass moves. In the <see cref="GlassBackdropMode.Native"/> mode nothing is redrawn by the glass (the compositor
    /// sees the change); there it selects the volatile backdrop cache unless <see cref="CacheProperty"/> is set.
    /// </summary>
    public static readonly AttachedProperty<bool> IsLiveProperty =
        AvaloniaProperty.RegisterAttached<GlassBackdrop, Visual, bool>("IsLive", inherits: true);

    /// <summary>
    /// How the glass surfaces inside this element blur what is behind them (inherited): <see cref="GlassBackdropMode.Brush"/>
    /// (the default) paints a blurred copy of <see cref="SourceProperty"/>; <see cref="GlassBackdropMode.Native"/> lets the
    /// compositor blur what is drawn behind the glass, with or without a source.
    /// </summary>
    public static readonly AttachedProperty<GlassBackdropMode> ModeProperty =
        AvaloniaProperty.RegisterAttached<GlassBackdrop, Visual, GlassBackdropMode>("Mode", GlassBackdropMode.Brush, inherits: true);

    /// <summary>
    /// The backdrop cache of the native mode (inherited; null by default). <see cref="RetainedBackdropEffectCache"/> keeps a
    /// snapshot of what is behind the glass and refreshes only its changed parts, so the pointer sheen or a sliding lens do
    /// not read the backdrop again; <see cref="VolatileBackdropEffectCache"/> reads the backdrop again on every draw and
    /// keeps no texture. Null: volatile when <see cref="IsLiveProperty"/> is set, otherwise the compositor's choice
    /// (retained for a blur). Ignored by the brush mode.
    /// </summary>
    public static readonly AttachedProperty<BackdropEffectCache?> CacheProperty =
        AvaloniaProperty.RegisterAttached<GlassBackdrop, Visual, BackdropEffectCache?>("Cache", inherits: true);

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

    /// <summary>Reads <see cref="ModeProperty"/>.</summary>
    public static GlassBackdropMode GetMode(Visual element) => element.GetValue(ModeProperty);

    /// <summary>Sets <see cref="ModeProperty"/>.</summary>
    public static void SetMode(Visual element, GlassBackdropMode value) => element.SetValue(ModeProperty, value);

    /// <summary>Reads <see cref="CacheProperty"/>.</summary>
    public static BackdropEffectCache? GetCache(Visual element) => element.GetValue(CacheProperty);

    /// <summary>Sets <see cref="CacheProperty"/>.</summary>
    public static void SetCache(Visual element, BackdropEffectCache? value) => element.SetValue(CacheProperty, value);
}
