using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The mirror of <see cref="MentionInput"/>'s <see cref="TokenTextEditor"/>: the shared mirror (same text, tinted tokens),
/// with the brushes of its two kinds at hand. The editor creates it through its mirror factory; drawing is the base
/// class's.
/// </summary>
internal sealed class MentionInputMirror : TokenTextMirror
{
    /// <summary>Background of <c>@</c> tokens (theme: <c>AccentSubtle</c>).</summary>
    public IBrush? PersonFill => ResolveFill(MentionInput.PersonKind);

    /// <summary>Text of <c>@</c> tokens (theme: <c>AccentStrong</c>).</summary>
    public IBrush? PersonForeground => ResolveForeground(MentionInput.PersonKind);

    /// <summary>Background of <c>#</c> tokens (Foreground 7%).</summary>
    public IBrush? ChannelFill => ResolveFill(MentionInput.ChannelKind);

    /// <summary>Text of <c>#</c> tokens (theme: <c>Foreground</c>).</summary>
    public IBrush? ChannelForeground => ResolveForeground(MentionInput.ChannelKind);
}
