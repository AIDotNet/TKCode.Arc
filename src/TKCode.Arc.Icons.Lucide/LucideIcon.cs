using System.ComponentModel;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Logging;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace TKCode.Arc.Icons.Lucide;

/// <summary>
/// Any Lucide icon, drawn like Arc's built-in glyphs: a 24×24 stroked path whose stroke (1.75 viewBox units by
/// default) scales with <see cref="ArcGlyph.Size"/>, colored by the inherited foreground.
/// <c>&lt;lucide:LucideIcon Kind="Sparkles" Size="20" /&gt;</c>
/// </summary>
/// <remarks>
/// Only the icons a project uses are compiled into it (see <see cref="LucideIconData"/>); a <see cref="Kind"/> that comes
/// from a binding or from data must be declared with <c>&lt;LucideIcon Include="..." /&gt;</c> in the project.
/// </remarks>
public class LucideIcon : ArcGlyph
{
    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<LucideIconKind> KindProperty =
        AvaloniaProperty.Register<LucideIcon, LucideIconKind>(nameof(Kind));

    /// <summary>The icon to draw.</summary>
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
        // The default kind draws only when the project uses it; an unset Kind is not worth a warning.
        Data = LucideIconData.IsRegistered(Kind) ? Lucide.Geometry(Kind) : null;
    }
}

/// <summary>Lucide geometry lookup (parsed once per icon and cached).</summary>
public static class Lucide
{
    private static readonly Geometry s_empty = new StreamGeometry();

    /// <summary>
    /// The icon's geometry, or an empty geometry (with a warning in Avalonia's log) when the icon is not compiled into
    /// the app; declare such icons with <c>&lt;LucideIcon Include="..." /&gt;</c>.
    /// </summary>
    public static Geometry Geometry(LucideIconKind kind)
    {
        if (LucideIconData.TryGet(kind, out var data))
        {
            return ArcIcons.Parse(data);
        }
        Logger.TryGet(LogEventLevel.Warning, "Lucide")?.Log(null,
            "Lucide icon {Kind} is not compiled into the app: it is chosen at run time or used where the generator does not look. "
            + "Add <LucideIcon Include=\"{Kind}\" /> to the project (or set LucideIconsIncludeAll=true).", kind, kind);
        return s_empty;
    }
}

/// <summary>
/// The Lucide path data available at run time. The package carries none: the TKCode.Arc.Icons.Lucide generator finds
/// the icons each project uses (C#, XAML, <c>&lt;LucideIcon Include="..." /&gt;</c> items, or every icon with
/// <c>LucideIconsIncludeAll=true</c>), compiles their path data into that project and registers it here when the
/// assembly loads.
/// </summary>
public static partial class LucideIconData
{
    private static readonly string?[] s_paths = new string?[KindCount];

    /// <summary>Registers an icon's 24×24 SVG path data; called by generated code.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(LucideIconKind kind, string data)
    {
        var index = (int)kind;
        if ((uint)index < (uint)s_paths.Length)
        {
            Volatile.Write(ref s_paths[index], data);
        }
    }

    /// <summary>Whether the icon's path data is compiled into the app.</summary>
    public static bool IsRegistered(LucideIconKind kind) => TryGet(kind, out _);

    /// <summary>Gets the icon's 24×24 SVG path data when it is compiled into the app.</summary>
    public static bool TryGet(LucideIconKind kind, out string data)
    {
        var index = (int)kind;
        data = (uint)index < (uint)s_paths.Length ? Volatile.Read(ref s_paths[index]) ?? string.Empty : string.Empty;
        return data.Length > 0;
    }

    /// <summary>The icon's 24×24 SVG path data, or an empty string when it is not compiled into the app.</summary>
    public static string Get(LucideIconKind kind) => TryGet(kind, out var data) ? data : string.Empty;
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

    /// <summary>The icon whose geometry is returned.</summary>
    public LucideIconKind Kind { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Lucide.Geometry(Kind);
}
