using Avalonia.Collections;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>The three stations of an <see cref="AgentLoop"/>, in the order the head visits them.</summary>
public enum AgentLoopStation
{
    /// <summary>Gather context: a block joins the context stack.</summary>
    Gather,

    /// <summary>Take action: one to three tool calls drop out of the ring and resolve.</summary>
    Act,

    /// <summary>Verify: a check passes or sends the loop round again.</summary>
    Verify
}

/// <summary>How a tool call of an <see cref="AgentLoop"/> ends.</summary>
public enum AgentLoopCallStatus
{
    /// <summary>The call returned its result (a check mark).</summary>
    Success,

    /// <summary>The call failed (a cross, and the result reads as the reason).</summary>
    Error
}

/// <summary>Where an <see cref="AgentLoop"/> run stands; the status line in the header shows it.</summary>
public enum AgentLoopPhase
{
    /// <summary>Before the first beat (or with no iterations).</summary>
    Ready,

    /// <summary>Playing on its own or playing a step.</summary>
    Running,

    /// <summary>Paused between beats.</summary>
    Paused,

    /// <summary>The last check passed: the ring is drawn in and the summary shows.</summary>
    Done,

    /// <summary>The last check failed: the ring stays neutral and the stopped summary shows.</summary>
    Stopped
}

/// <summary>A block of context gathered at the start of a loop: a label, an optional detail and its size in tokens.</summary>
public sealed class AgentLoopContext
{
    /// <summary>Creates an empty block (set the properties, as XAML does).</summary>
    public AgentLoopContext()
    {
    }

    /// <summary>Creates a block.</summary>
    /// <param name="label">The block's name, one to three words.</param>
    /// <param name="detail">The line under it, or null.</param>
    /// <param name="tokens">Its size in tokens.</param>
    /// <param name="mark">A 24×24 stroked glyph, or null for the default message mark.</param>
    public AgentLoopContext(string label, string? detail, int tokens, Geometry? mark = null)
    {
        Label = label;
        Detail = detail;
        Tokens = tokens;
        Mark = mark;
    }

    /// <summary>The block's name, such as "Build log". Keep it to one to three words.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The line under the name, under about 30 characters; null shows none.</summary>
    public string? Detail { get; set; }

    /// <summary>The block's size in tokens; the stack's total counts up by it when the block joins.</summary>
    public int Tokens { get; set; }

    /// <summary>A 24×24 stroked glyph (for example an <see cref="ArcIcons"/> member); null draws a message mark.</summary>
    public Geometry? Mark { get; set; }
}

/// <summary>A tool call made at the Take action station: the tool, what it was asked to do and what came back.</summary>
public sealed class AgentLoopCall
{
    /// <summary>Creates an empty call (set the properties, as XAML does).</summary>
    public AgentLoopCall()
    {
    }

    /// <summary>Creates a call.</summary>
    /// <param name="tool">The tool's name.</param>
    /// <param name="action">What the agent asked it to do, two to four words.</param>
    /// <param name="result">What came back, or why it failed.</param>
    /// <param name="status">Whether it succeeded.</param>
    /// <param name="mark">A 24×24 stroked glyph for the tool, or null for a wrench.</param>
    public AgentLoopCall(string tool, string action, string result, AgentLoopCallStatus status = AgentLoopCallStatus.Success, Geometry? mark = null)
    {
        Tool = tool;
        Action = action;
        Result = result;
        Status = status;
        Mark = mark;
    }

    /// <summary>The tool's name, such as "Tracker"; the running chip reads "Calling" and this name.</summary>
    public string Tool { get; set; } = string.Empty;

    /// <summary>A 24×24 stroked glyph for the tool; null draws a wrench. Use generic glyphs, not brand marks.</summary>
    public Geometry? Mark { get; set; }

    /// <summary>What the agent asked the tool to do, two to four words; it is the chip's title.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>What came back (or, for an error, why it failed), under about 30 characters.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Whether the call succeeded (a check) or failed (a cross); default success.</summary>
    public AgentLoopCallStatus Status { get; set; } = AgentLoopCallStatus.Success;
}

/// <summary>The check at the Verify station: passed, or a retry note that sends the loop round again.</summary>
public sealed class AgentLoopCheck
{
    /// <summary>Creates a failed check with no note (set the properties, as XAML does).</summary>
    public AgentLoopCheck()
    {
    }

    /// <summary>Creates a check.</summary>
    /// <param name="passed">Whether it passed.</param>
    /// <param name="note">The short note under it.</param>
    public AgentLoopCheck(bool passed, string note)
    {
        Passed = passed;
        Note = note;
    }

    /// <summary>True when the check passed. When the last loop's check fails, the run ends as stopped.</summary>
    public bool Passed { get; set; }

    /// <summary>The short note under the check, such as why the loop goes round again.</summary>
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// One pass round the loop: the context gathered, one to three tool calls made one after another, and the check. Calls
/// are its content in XAML. Calls past the third are ignored; a loop with none still stops at Take action for the time
/// of one call, with nothing dropping into the lane.
/// </summary>
public sealed class AgentLoopIteration
{
    /// <summary>Creates an empty loop (set the properties, as XAML does).</summary>
    public AgentLoopIteration()
    {
    }

    /// <summary>Creates a loop.</summary>
    /// <param name="gather">The context gathered.</param>
    /// <param name="calls">One to three tool calls.</param>
    /// <param name="verify">The check.</param>
    public AgentLoopIteration(AgentLoopContext gather, IEnumerable<AgentLoopCall> calls, AgentLoopCheck verify)
    {
        Gather = gather;
        Calls.AddRange(calls);
        Verify = verify;
    }

    /// <summary>The context block gathered at the start of the loop.</summary>
    public AgentLoopContext Gather { get; set; } = new();

    /// <summary>One to three tool calls, shown one after another in the same lane.</summary>
    [Content]
    public AvaloniaList<AgentLoopCall> Calls { get; } = new();

    /// <summary>The check at the end of the loop.</summary>
    public AgentLoopCheck Verify { get; set; } = new();
}

/// <summary>Arguments of <see cref="AgentLoop.StationChanged"/>: the head reached a station.</summary>
public sealed class AgentLoopStationChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="source">The agent loop.</param>
    /// <param name="station">The station the head reached.</param>
    /// <param name="loop">The zero-based loop.</param>
    public AgentLoopStationChangedEventArgs(RoutedEvent routedEvent, object source, AgentLoopStation station, int loop)
        : base(routedEvent, source)
    {
        Station = station;
        Loop = loop;
    }

    /// <summary>The station the head reached.</summary>
    public AgentLoopStation Station { get; }

    /// <summary>The loop, counted from zero.</summary>
    public int Loop { get; }
}
