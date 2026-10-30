using System.Globalization;

namespace TKCode.Arc;

/// <summary>
/// Every word <see cref="DesignToCode"/> shows or announces, in English by default. Replace the whole object to localize
/// or reword; sentences with values are format functions so the word order can change.
/// </summary>
public sealed class DesignToCodeLabels
{
    /// <summary>The English defaults.</summary>
    public static DesignToCodeLabels Default { get; } = new();

    /// <summary>The card's accessible name.</summary>
    public string Title { get; init; } = "Design to code";

    /// <summary>The design side's heading and group name.</summary>
    public string Design { get; init; } = "Design";

    /// <summary>The output side's group name.</summary>
    public string Output { get; init; } = "Output";

    /// <summary>The tag after the sample design's name.</summary>
    public string Sample { get; init; } = "Sample";

    /// <summary>The tag after an imported frame's name.</summary>
    public string DesignFile { get; init; } = "Design file";

    /// <summary>The drop zone's heading.</summary>
    public string DropTitle { get; init; } = "Drop a UI screenshot";

    /// <summary>The drop zone's hint, from the accepted types ("PNG, JPEG or WebP") and the size limit ("10 MB").</summary>
    public Func<string, string, string> DropHint { get; init; } = (types, size) => types + ", up to " + size;

    /// <summary>The drop zone's file picker button.</summary>
    public string ChooseFile { get; init; } = "Choose file";

    /// <summary>The drop zone's sample button.</summary>
    public string UseSample { get; init; } = "Use sample";

    /// <summary>The cover shown while a file is dragged over the design side.</summary>
    public string DropCover { get; init; } = "Drop to use this design";

    /// <summary>The name of the button that removes the design.</summary>
    public string Remove { get; init; } = "Remove design";

    /// <summary>The footer's import button (also its accessible name when it shows only its icon).</summary>
    public string Import { get; init; } = "Import design file";

    /// <summary>The main action before a run.</summary>
    public string Generate { get; init; } = "Generate code";

    /// <summary>The main action while a run analyzes or writes.</summary>
    public string Stop { get; init; } = "Stop";

    /// <summary>The main action after a finished or stopped run.</summary>
    public string Regenerate { get; init; } = "Regenerate";

    /// <summary>The main action after a failed run.</summary>
    public string TryAgain { get; init; } = "Try again";

    /// <summary>The Code segment of the view toggle.</summary>
    public string Code { get; init; } = "Code";

    /// <summary>The Preview segment of the view toggle.</summary>
    public string Preview { get; init; } = "Preview";

    /// <summary>The view toggle's accessible name.</summary>
    public string ViewToggle { get; init; } = "Show code or preview";

    /// <summary>The copy button's accessible name, from the file name.</summary>
    public Func<string, string> Copy { get; init; } = file => "Copy " + file;

    /// <summary>The code area's accessible name, from the file name.</summary>
    public Func<string, string> CodeArea { get; init; } = file => "Generated code, " + file;

    /// <summary>The code area's status while code streams in (its busy state).</summary>
    public string Busy { get; init; } = "Writing";

    /// <summary>The screenshot's accessible name, from the design's name.</summary>
    public Func<string, string> Screenshot { get; init; } = name => "Design: " + name;

    /// <summary>A region tag's accessible name, from the region's label ("Price: show its code").</summary>
    public Func<string, string> RegionTag { get; init; } = label => label + ": show its code";

    /// <summary>The status with no design.</summary>
    public string StatusEmpty { get; init; } = "Add a design to start";

    /// <summary>The status with a design and no run yet.</summary>
    public string StatusReady { get; init; } = "Ready";

    /// <summary>The status while the design is analyzed.</summary>
    public string StatusAnalyzing { get; init; } = "Analyzing layout";

    /// <summary>The status while code is written.</summary>
    public string StatusWriting { get; init; } = "Writing";

    /// <summary>The status of a finished run.</summary>
    public string StatusDone { get; init; } = "Done";

    /// <summary>The status of a stopped run.</summary>
    public string StatusStopped { get; init; } = "Stopped";

    /// <summary>The meta text while analyzing or writing, from the regions found so far ("5 regions").</summary>
    public Func<int, string> Regions { get; init; } = count => count == 1 ? "1 region" : count.ToString(CultureInfo.InvariantCulture) + " regions";

    /// <summary>The meta text of a finished run, from its lines and time ("52 lines in 3.4s").</summary>
    public Func<int, TimeSpan, string> DoneMeta { get; init; } = (lines, time) =>
        Lines(lines) + " in " + time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";

    /// <summary>The meta text of a stopped run.</summary>
    public string StoppedMeta { get; init; } = "Partial code kept";

    /// <summary>The code side before a design is added.</summary>
    public string PlaceholderEmpty { get; init; } = "Code appears here once you add a design.";

    /// <summary>The code side with a design and no run yet, from the file name.</summary>
    public Func<string, string> PlaceholderReady { get; init; } = file => "Generate to write " + file + " from this design.";

    /// <summary>The code side while the design is analyzed and no code has arrived.</summary>
    public string PlaceholderAnalyzing { get; init; } = "Finding the layout regions…";

    /// <summary>The preview side before the code is finished.</summary>
    public string PreviewPending { get; init; } = "The preview opens once the code is written.";

    /// <summary>The preview side when there is code but nothing renders it.</summary>
    public string PreviewUnavailable { get; init; } = "No preview for this design yet.";

    /// <summary>The error for a file of another type, from the accepted types ("PNG, JPEG or WebP").</summary>
    public Func<string, string> WrongType { get; init; } = types => "That file type isn't supported. Choose a " + types + " image.";

    /// <summary>The error for a file over the limit, from the limit ("10 MB").</summary>
    public Func<string, string> TooLarge { get; init; } = size => "That file is over " + size + ". Choose a smaller image.";

    /// <summary>The error for a file that cannot be read.</summary>
    public string Unreadable { get; init; } = "That image couldn't be opened. Try another file.";

    /// <summary>The error of a failed run, from the reason ("Generation failed: timeout. Try again.").</summary>
    public Func<string, string> Failed { get; init; } = reason =>
        string.IsNullOrWhiteSpace(reason) ? "Generation failed. Try again." : "Generation failed: " + reason.TrimEnd('.') + ". Try again.";

    /// <summary>The error of a design file import that failed, from the reason.</summary>
    public Func<string, string> ImportFailed { get; init; } = reason =>
        string.IsNullOrWhiteSpace(reason) ? "The design file couldn't be imported. Try again." : "Import failed: " + reason.TrimEnd('.') + ". Try again.";

    /// <summary>Announced when a run starts.</summary>
    public string AnnounceAnalyzing { get; init; } = "Analyzing the design.";

    /// <summary>Announced when writing starts, from the labels of the regions found.</summary>
    public Func<IReadOnlyList<string>, string> AnnounceRegions { get; init; } = labels => labels.Count == 0
        ? "Writing code."
        : "Found " + labels.Count.ToString(CultureInfo.InvariantCulture) + (labels.Count == 1 ? " region: " : " regions: ")
          + string.Join(", ", labels) + ". Writing code.";

    /// <summary>Announced when a run finishes, from its lines and time.</summary>
    public Func<int, TimeSpan, string> AnnounceDone { get; init; } = (lines, time) =>
        "Done. " + Lines(lines) + " in " + time.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " seconds.";

    /// <summary>Announced when a run is stopped.</summary>
    public string AnnounceStopped { get; init; } = "Stopped. The partial code is kept.";

    /// <summary>Announced when a design is added, from its name.</summary>
    public Func<string, string> AnnounceSource { get; init; } = name => name + " added.";

    /// <summary>Added to announcements of a simulated run or import.</summary>
    public string SimulationNote { get; init; } = "This is a sample: nothing was uploaded or opened.";

    private static string Lines(int lines) =>
        lines == 1 ? "1 line" : lines.ToString(CultureInfo.InvariantCulture) + " lines";
}
