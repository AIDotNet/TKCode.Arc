using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Arguments of <see cref="PiiRedaction.Redacted"/>: the safe prompt and every entity it replaced.</summary>
public sealed class PiiRedactedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PiiRedactedEventArgs(RoutedEvent routedEvent, string prompt, IReadOnlyList<PiiEntity> entities)
        : base(routedEvent)
    {
        Prompt = prompt;
        Entities = entities;
    }

    /// <summary>The message with every entity swapped for its placeholder.</summary>
    public string Prompt { get; }

    /// <summary>Every entity (type, position, value and token), in the order it appears in the message.</summary>
    public IReadOnlyList<PiiEntity> Entities { get; }
}

/// <summary>Arguments of <see cref="PiiRedaction.ReplyRestored"/>: the model's reply and the text the person sees.</summary>
public sealed class PiiReplyRestoredEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PiiReplyRestoredEventArgs(RoutedEvent routedEvent, string raw, string restored, bool isSimulated)
        : base(routedEvent)
    {
        Raw = raw;
        Restored = restored;
        IsSimulated = isSimulated;
    }

    /// <summary>The reply as the model wrote it, placeholders included.</summary>
    public string Raw { get; }

    /// <summary>The reply with the original values put back.</summary>
    public string Restored { get; }

    /// <summary>Whether the reply was simulated because no <see cref="PiiRedaction.Sender"/> was set.</summary>
    public bool IsSimulated { get; }
}

/// <summary>Arguments of <see cref="PiiRedaction.PhaseChanged"/>: the phase the card left and the one it entered.</summary>
public sealed class PiiRedactionPhaseChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public PiiRedactionPhaseChangedEventArgs(RoutedEvent routedEvent, PiiRedactionPhase oldPhase, PiiRedactionPhase newPhase)
        : base(routedEvent)
    {
        OldPhase = oldPhase;
        NewPhase = newPhase;
    }

    /// <summary>The phase before the change.</summary>
    public PiiRedactionPhase OldPhase { get; }

    /// <summary>The phase now.</summary>
    public PiiRedactionPhase NewPhase { get; }
}
