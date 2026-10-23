using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>How strongly a span of a <see cref="DocumentPage"/> is tinted in its group color.</summary>
internal enum DocumentSpanState
{
    /// <summary>A value cites the span: the group color at 10%.</summary>
    Linked,

    /// <summary>A value just landed on the span: 32%, on at once; the owner turns it back to <see cref="Linked"/> after about 525 ms.</summary>
    Fresh,

    /// <summary>The span's value is pointed at or selected: 24%.</summary>
    Lit
}

/// <summary>
/// The paint of a span: its state, its group's series color (<see cref="Tone"/> 1 to 4, <see cref="PromptTone.SeriesColor"/>)
/// and whether its value needs a check (a 1px dotted underline in the warning color, in any state).
/// </summary>
/// <param name="State">How strongly the span is tinted.</param>
/// <param name="Tone">The series color, 1 to 4; other values use tone 4.</param>
/// <param name="NeedsCheck">Whether the dotted underline is drawn.</param>
internal readonly record struct DocumentSpanPaint(DocumentSpanState State, int Tone, bool NeedsCheck);

/// <summary>A pointer event on a span of a <see cref="DocumentPage"/>.</summary>
internal sealed class DocumentSpanEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="routedEvent">The event.</param>
    /// <param name="spanId">The span's id.</param>
    public DocumentSpanEventArgs(RoutedEvent routedEvent, string spanId)
        : base(routedEvent)
    {
        SpanId = spanId;
    }

    /// <summary>The id of the span (<see cref="DocumentSpan.Id"/>).</summary>
    public string SpanId { get; }
}
