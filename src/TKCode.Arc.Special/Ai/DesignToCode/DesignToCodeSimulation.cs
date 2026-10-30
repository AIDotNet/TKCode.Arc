using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A design and the code a model could write for it, which a <see cref="DesignToCode"/> plays while the app wires no
/// <see cref="DesignToCode.Generator"/>: the design (an image or a <see cref="DesignToCodeSource.Sketch"/>), the layout
/// regions the scan finds, the code part by part with the region each part implements, and optionally the model credited,
/// the file name and a preview. With <see cref="DesignToCode.AutoPlay"/> the card loops it while it is on screen
/// (generate, hold on the code, show the preview, start again) until someone presses or types inside it. Nothing is
/// uploaded or sent. Set the data before handing the simulation to the card; to play another design, give the card a new
/// simulation.
/// </summary>
public sealed class DesignToCodeSimulation
{
    /// <summary>The design shown while the card's own <see cref="DesignToCode.Source"/> is left unset, and offered by Use sample.</summary>
    public DesignToCodeSource? Source { get; set; }

    /// <summary>The layout regions the scan finds, in percent of the design.</summary>
    public IList<DesignToCodeRegion>? Regions { get; set; }

    /// <summary>The code, part by part, in order; it streams in at the card's reading pace.</summary>
    public IList<DesignToCodeSimulationPart>? Parts { get; set; }

    /// <summary>The model credited in the code header while the simulation plays (the card's own <see cref="DesignToCode.Model"/> wins).</summary>
    public AgentModel? Model { get; set; }

    /// <summary>The file name in the code header while the simulation plays (the card's own <see cref="DesignToCode.FileName"/> wins).</summary>
    public string? FileName { get; set; }

    /// <summary>The frame name Import design file brings in when the card has no <see cref="DesignToCode.DesignFileImporter"/>.</summary>
    public string? FrameName { get; set; }

    /// <summary>
    /// Renders the finished code of a simulated run in the Preview tab (the card's own
    /// <see cref="DesignToCode.PreviewFactory"/> wins). Null shows the "preview unavailable" note.
    /// </summary>
    public Func<string, DesignToCodeSource, Control?>? PreviewFactory { get; set; }

    /// <summary>
    /// Called on the preview control 1.4 seconds after the loop opens the Preview tab, for example to flip a switch so the
    /// preview visibly works.
    /// </summary>
    public Action<Control>? PreviewAction { get; set; }

    /// <summary>The whole code, all parts joined.</summary>
    internal string Code => Parts is { } parts ? string.Concat(parts.Where(p => p is not null).Select(p => p.Text)) : string.Empty;
}

/// <summary>One part of the code of a <see cref="DesignToCodeSimulation"/>: its text and the region it implements.</summary>
public sealed class DesignToCodeSimulationPart
{
    /// <summary>Creates an empty part.</summary>
    public DesignToCodeSimulationPart()
    {
    }

    /// <summary>Creates a part.</summary>
    /// <param name="text">The code, newlines included.</param>
    /// <param name="regionId">The <see cref="DesignToCodeRegion.Id"/> it implements, or null for the component's frame.</param>
    public DesignToCodeSimulationPart(string text, string? regionId = null)
    {
        Text = text;
        RegionId = regionId;
    }

    /// <summary>The code, newlines included.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>The <see cref="DesignToCodeRegion.Id"/> it implements, or null for imports and the component's frame.</summary>
    public string? RegionId { get; set; }
}

/// <summary>
/// Draws a design in code on the design side of a <see cref="DesignToCode"/> (a mockup or a wireframe), in
/// <paramref name="bounds"/> with the card's theme colors, so it follows light and dark. See
/// <see cref="DesignToCodeSource.Sketch"/>.
/// </summary>
/// <param name="context">The drawing context.</param>
/// <param name="bounds">The design's rectangle, at the aspect ratio of <see cref="DesignToCodeSource.SketchSize"/>.</param>
/// <param name="palette">The card's colors, font and corner radius.</param>
public delegate void DesignToCodeSketch(DrawingContext context, Rect bounds, DesignToCodeSketchPalette palette);

/// <summary>The theme colors, font and corner radius a <see cref="DesignToCodeSketch"/> draws with.</summary>
public sealed class DesignToCodeSketchPalette
{
    internal DesignToCodeSketchPalette(IBrush? paper, IBrush? well, IBrush? ink, IBrush? mutedInk, IBrush? line, IBrush? accent,
        IBrush? accentSubtle, IBrush? accentForeground, FontFamily fontFamily, CornerRadius cornerRadius)
    {
        Paper = paper;
        Well = well;
        Ink = ink;
        MutedInk = mutedInk;
        Line = line;
        Accent = accent;
        AccentSubtle = accentSubtle;
        AccentForeground = accentForeground;
        FontFamily = fontFamily;
        CornerRadius = cornerRadius;
    }

    /// <summary>The page or card surface.</summary>
    public IBrush? Paper { get; }

    /// <summary>A recessed well (tracks, inputs).</summary>
    public IBrush? Well { get; }

    /// <summary>The text color.</summary>
    public IBrush? Ink { get; }

    /// <summary>The muted text color.</summary>
    public IBrush? MutedInk { get; }

    /// <summary>Hairlines and borders.</summary>
    public IBrush? Line { get; }

    /// <summary>The accent.</summary>
    public IBrush? Accent { get; }

    /// <summary>A subtle tint of the accent.</summary>
    public IBrush? AccentSubtle { get; }

    /// <summary>Text on the accent.</summary>
    public IBrush? AccentForeground { get; }

    /// <summary>The body font.</summary>
    public FontFamily FontFamily { get; }

    /// <summary>The design frame's corner radius.</summary>
    public CornerRadius CornerRadius { get; }
}
