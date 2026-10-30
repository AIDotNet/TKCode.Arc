using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>Where a <see cref="DesignToCode"/> card is in its run.</summary>
public enum DesignToCodePhase
{
    /// <summary>No design yet: the design side shows the drop zone.</summary>
    Empty,

    /// <summary>A design is in place and nothing has been generated for it yet.</summary>
    Ready,

    /// <summary>The scan line crosses the design and its layout regions appear.</summary>
    Analyzing,

    /// <summary>Code streams in beside the design, region by region.</summary>
    Writing,

    /// <summary>The code is finished.</summary>
    Done,

    /// <summary>The person stopped the run; the partial code is kept.</summary>
    Stopped,

    /// <summary>The run failed; the status names the problem and the fix.</summary>
    Error
}

/// <summary>The layer a <see cref="DesignToCode"/> card shows on its output side.</summary>
public enum DesignToCodeView
{
    /// <summary>The generated code.</summary>
    Code,

    /// <summary>The live preview of the generated code.</summary>
    Preview
}

/// <summary>Where a <see cref="DesignToCodeSource"/> came from.</summary>
public enum DesignToCodeSourceKind
{
    /// <summary>A sample design, such as a simulation's.</summary>
    Sample,

    /// <summary>A screenshot dropped on the card or chosen with a file picker.</summary>
    File,

    /// <summary>A frame imported from a design file.</summary>
    DesignFile
}

/// <summary>
/// The design a <see cref="DesignToCode"/> card turns into code: a name, where it came from and its image, or a
/// <see cref="Sketch"/> that draws it in code in the card's colors.
/// </summary>
public sealed class DesignToCodeSource
{
    /// <summary>Creates an empty source; set <see cref="Name"/> and <see cref="Image"/>.</summary>
    public DesignToCodeSource()
    {
    }

    /// <summary>Creates a source with a name, an optional image and where it came from.</summary>
    /// <param name="name">The file or frame name shown on the design side ("pricing.png").</param>
    /// <param name="image">The rendered design, or null for a source drawn by its <see cref="Sketch"/>.</param>
    /// <param name="kind">Where the design came from.</param>
    public DesignToCodeSource(string name, IImage? image = null, DesignToCodeSourceKind kind = DesignToCodeSourceKind.File)
    {
        Name = name;
        Image = image;
        Kind = kind;
    }

    /// <summary>The file or frame name shown on the design side and used in the screenshot's accessible name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Where the design came from.</summary>
    public DesignToCodeSourceKind Kind { get; init; } = DesignToCodeSourceKind.File;

    /// <summary>The rendered design (a bitmap). Null draws the <see cref="Sketch"/>, if any.</summary>
    public IImage? Image { get; init; }

    /// <summary>
    /// Draws the design in code, in the card's theme colors, when there is no <see cref="Image"/> (mockups and demos).
    /// </summary>
    public DesignToCodeSketch? Sketch { get; init; }

    /// <summary>The sketch's natural size: the design keeps its aspect ratio as it scales to the pane (240 × 300 by default).</summary>
    public Size SketchSize { get; init; } = new(240, 300);

    /// <summary>The file the design was read from, when it came from a drop or a picker; hand it to your backend.</summary>
    public IStorageFile? File { get; init; }

    /// <summary>The file's size in bytes, when known.</summary>
    public long? Size { get; init; }

    /// <summary>Whether the card draws this source's sketch rather than an image.</summary>
    internal bool IsSketch => Image is null;

    /// <inheritdoc />
    public override string ToString() => Name;
}

/// <summary>
/// A layout region found on the design: an id that code chunks refer to, a short label for its tag, and its box in
/// percent of the design's width and height (0 to 100, from the top left).
/// </summary>
public sealed class DesignToCodeRegion
{
    /// <summary>Creates an empty region; set its id, label and box.</summary>
    public DesignToCodeRegion()
    {
    }

    /// <summary>Creates a region.</summary>
    /// <param name="id">The id code chunks pass to link their lines to this box.</param>
    /// <param name="label">The tag's text ("Price").</param>
    /// <param name="x">Left edge, in percent of the design's width.</param>
    /// <param name="y">Top edge, in percent of the design's height.</param>
    /// <param name="width">Width, in percent of the design's width.</param>
    /// <param name="height">Height, in percent of the design's height.</param>
    public DesignToCodeRegion(string id, string label, double x, double y, double width, double height)
    {
        Id = id;
        Label = label;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>The id code chunks pass to link their lines to this box.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>The tag's text ("Price").</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Left edge, in percent of the design's width.</summary>
    public double X { get; init; }

    /// <summary>Top edge, in percent of the design's height.</summary>
    public double Y { get; init; }

    /// <summary>Width, in percent of the design's width.</summary>
    public double Width { get; init; }

    /// <summary>Height, in percent of the design's height.</summary>
    public double Height { get; init; }

    /// <summary>The box as fractions of the design (0 to 1), clamped to the design.</summary>
    internal Rect Fraction
    {
        get
        {
            var x = Math.Clamp(X, 0, 100) / 100;
            var y = Math.Clamp(Y, 0, 100) / 100;
            var w = Math.Clamp(Width, 0, 100 - x * 100) / 100;
            var h = Math.Clamp(Height, 0, 100 - y * 100) / 100;
            return new Rect(x, y, w, h);
        }
    }

    /// <inheritdoc />
    public override string ToString() => Label;
}

/// <summary>
/// What a <see cref="DesignToCode.Generator"/> gets besides the source: a token that is cancelled when the person stops
/// the run (Escape or Stop) or replaces the design, <see cref="AddRegion"/> for each layout region as it is detected, and
/// <see cref="Write"/> for each chunk of code as it streams. Both may be called from any thread; calls after the run
/// ended are ignored.
/// </summary>
public sealed class DesignToCodeContext
{
    private readonly DesignToCode _owner;
    private readonly int _run;

    internal DesignToCodeContext(DesignToCode owner, int run, CancellationToken cancellationToken)
    {
        _owner = owner;
        _run = run;
        CancellationToken = cancellationToken;
    }

    /// <summary>Cancelled when the run is stopped or replaced: abort your request and return.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Adds a detected layout region (its box in percent of the design). A region with an id already added replaces it.</summary>
    public void AddRegion(DesignToCodeRegion region)
    {
        ArgumentNullException.ThrowIfNull(region);
        OnUiThread(() => _owner.AcceptRegion(_run, region));
    }

    /// <summary>
    /// Appends a chunk of code of any size. Pass the id of the region the chunk implements to link its lines to the
    /// region's box; chunks without one belong to no region (imports, the component's frame).
    /// </summary>
    public void Write(string chunk, string? regionId = null)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return;
        }
        OnUiThread(() => _owner.AcceptChunk(_run, chunk, regionId));
    }

    private static void OnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}

/// <summary>Arguments of <see cref="DesignToCode.Completed"/>: the finished code and what it was made from.</summary>
public sealed class DesignToCodeCompletedEventArgs : RoutedEventArgs
{
    internal DesignToCodeCompletedEventArgs(RoutedEvent routedEvent, string code, DesignToCodeSource source, int lineCount, TimeSpan elapsed)
        : base(routedEvent)
    {
        Code = code;
        Design = source;
        LineCount = lineCount;
        Elapsed = elapsed;
    }

    /// <summary>The finished code.</summary>
    public string Code { get; }

    /// <summary>The design the code was generated from.</summary>
    public DesignToCodeSource Design { get; }

    /// <summary>How many lines the code has.</summary>
    public int LineCount { get; }

    /// <summary>How long the run took, from Generate to the last line.</summary>
    public TimeSpan Elapsed { get; }
}
