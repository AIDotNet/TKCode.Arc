using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// Lucide glyphs the explainers need that Core <see cref="ArcIcons"/> does not ship (24×24, stroked; draw with
/// <c>ArcGlyph</c>). Path data is lucide-static 1.59.0 as converted by <c>tools/lucide/generate_lucide.py</c> (the
/// same strings as <c>TKCode.Arc.Icons.Lucide</c>, which a control package must not depend on), parsed once through
/// <see cref="ArcIcons.Parse"/>. If Core later gains these glyphs, switch to the <c>ArcIcons</c> members.
/// </summary>
internal static class ExplainerIcons
{
    // Lucide step-forward, ISC.
    private const string StepForwardData =
        "M10.029 4.285A2 2 0 0 0 7 6v12a2 2 0 0 0 3.029 1.715l9.997-5.998a2 2 0 0 0 .003-3.432z M3 4v16";

    // Lucide skip-forward, ISC.
    private const string SkipForwardData =
        "M21 4v16 M6.029 4.285A2 2 0 0 0 3 6v12a2 2 0 0 0 3.029 1.715l9.997-5.998a2 2 0 0 0 .003-3.432z";

    // Lucide loader, ISC.
    private const string LoaderData =
        "M12 2v4 M16.2 7.8l2.9-2.9 M18 12h4 M16.2 16.2l2.9 2.9 M12 18v4 M4.9 19.1l2.9-2.9 M2 12h4 M4.9 4.9l2.9 2.9";

    // Lucide network, ISC.
    private const string NetworkData =
        "M17 16h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z " +
        "M3 16h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z " +
        "M10 2h4a1 1 0 0 1 1 1v4a1 1 0 0 1 -1 1h-4a1 1 0 0 1 -1 -1v-4a1 1 0 0 1 1 -1z " +
        "M5 16v-3a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v3 M12 12V8";

    // Lucide hard-drive, ISC.
    private const string HardDriveData =
        "M10 16h.01 M2.212 11.577a2 2 0 0 0-.212.896V18a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-5.527a2 2 0 0 0-.212-.896L18.55 5.11A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z " +
        "M21.946 12.013H2.054 M6 16h.01";

    // Lucide sigma, ISC.
    private const string SigmaData =
        "M18 7V5a1 1 0 0 0-1-1H6.5a.5.5 0 0 0-.4.8l4.5 6a2 2 0 0 1 0 2.4l-4.5 6a.5.5 0 0 0 .4.8H17a1 1 0 0 0 1-1v-2";

    // Lucide circle-dashed, ISC.
    private const string CircleDashedData =
        "M10.1 2.182a10 10 0 0 1 3.8 0 M13.9 21.818a10 10 0 0 1-3.8 0 M17.609 3.721a10 10 0 0 1 2.69 2.7 " +
        "M2.182 13.9a10 10 0 0 1 0-3.8 M20.279 17.609a10 10 0 0 1-2.7 2.69 M21.818 10.1a10 10 0 0 1 0 3.8 " +
        "M3.721 6.391a10 10 0 0 1 2.7-2.69 M6.391 20.279a10 10 0 0 1-2.69-2.7";

    // Lucide timer-off, ISC.
    private const string TimerOffData =
        "M10 2h4 M4.6 11a8 8 0 0 0 1.7 8.7 8 8 0 0 0 8.7 1.7 M7.4 7.4a8 8 0 0 1 10.3 1 8 8 0 0 1 .9 10.2 M2 2l20 20 M12 12v-2";

    /// <summary>A dashed circle: empty, not started.</summary>
    public static Geometry CircleDashed => ArcIcons.Parse(CircleDashedData);

    /// <summary>A crossed-out timer: expired.</summary>
    public static Geometry TimerOff => ArcIcons.Parse(TimerOffData);

    /// <summary>A bar then a play triangle: play to the next beat and stop.</summary>
    public static Geometry StepForward => ArcIcons.Parse(StepForwardData);

    /// <summary>A play triangle then a bar.</summary>
    public static Geometry SkipForward => ArcIcons.Parse(SkipForwardData);

    /// <summary>Eight rays: work in progress.</summary>
    public static Geometry Loader => ArcIcons.Parse(LoaderData);

    /// <summary>Three linked boxes.</summary>
    public static Geometry Network => ArcIcons.Parse(NetworkData);

    /// <summary>A drive: storage, a cache.</summary>
    public static Geometry HardDrive => ArcIcons.Parse(HardDriveData);

    /// <summary>A sum sign.</summary>
    public static Geometry Sigma => ArcIcons.Parse(SigmaData);
}
