using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace TKCode.Arc.Icons.Lucide;

/// <summary>
/// Any Lucide icon, drawn like Arc's built-in glyphs: a 24×24 stroked path whose stroke (1.75 viewBox units by
/// default) scales with <see cref="ArcGlyph.Size"/>, colored by the inherited foreground.
/// <c>&lt;lucide:LucideIcon Kind="Sparkles" Size="20" /&gt;</c>
/// </summary>
public class LucideIcon : ArcGlyph
{
    public static readonly StyledProperty<LucideIconKind> KindProperty =
        AvaloniaProperty.Register<LucideIcon, LucideIconKind>(nameof(Kind));

    public LucideIconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    static LucideIcon()
    {
        KindProperty.Changed.AddClassHandler<LucideIcon>((icon, _) => icon.Data = Lucide.Geometry(icon.Kind));
    }

    public LucideIcon()
    {
        Data = Lucide.Geometry(Kind);
    }
}

/// <summary>Lucide geometry lookup (parsed once per icon and cached).</summary>
public static class Lucide
{
    public static Geometry Geometry(LucideIconKind kind) => ArcIcons.Parse(LucideIconData.Get(kind));
}

/// <summary>
/// Returns a Lucide icon's geometry for properties that take a <see cref="Avalonia.Media.Geometry"/>
/// (menu item icons, button glyphs): <c>Icon="{lucide:LucideGeometry Kind=Trash2}"</c>.
/// </summary>
public sealed class LucideGeometryExtension : MarkupExtension
{
    public LucideGeometryExtension()
    {
    }

    public LucideGeometryExtension(LucideIconKind kind) => Kind = kind;

    public LucideIconKind Kind { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Lucide.Geometry(Kind);
}
