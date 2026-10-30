using Avalonia;

namespace TKCode.Arc;

/// <summary>
/// A simulated suggestion for a <see cref="CodeSuggestion"/>, for demos, prototypes and tests: the card reads
/// <see cref="Code"/> for 0.8s, then each of <see cref="Hunks"/> opens (its removed lines tint red in 240ms) and its new
/// lines stream in one every 38ms. With <see cref="CodeSuggestion.AutoPlay"/> the card then reviews it itself (one hunk at
/// a time and Accept all on alternate runs) and replays 2.6s after it finishes. Any pointer, key or focus inside the card
/// hands control to the person for that run; Stop keeps the hunks that finished and Regenerate replays. Reduced motion
/// and Off open on the finished suggestion with no streaming and no autoplay. Nothing is sent to a model.
/// <para>
/// Set it as the card's <see cref="CodeSuggestion.Simulation"/>: it plays while the card has no <see cref="CodeSuggestion.Code"/>
/// and no <see cref="CodeSuggestion.Hunks"/> of its own, from the top each time the card comes into a window. The file
/// name, the instruction and the model stay on the card. Its values take effect at the next run; the hunks are copied, so
/// one list can be shared between simulations. Use one simulation per card.
/// </para>
/// </summary>
public sealed class CodeSuggestionSimulation : AvaloniaObject
{
    /// <summary>The original file contents, lines split on <c>\n</c>.</summary>
    public static readonly StyledProperty<string?> CodeProperty =
        AvaloniaProperty.Register<CodeSuggestionSimulation, string?>(nameof(Code));

    /// <summary>The complete hunks the simulated model streams, in file order.</summary>
    public static readonly StyledProperty<IReadOnlyList<CodeSuggestionHunk>?> HunksProperty =
        AvaloniaProperty.Register<CodeSuggestionSimulation, IReadOnlyList<CodeSuggestionHunk>?>(nameof(Hunks));

    /// <summary>Creates a simulation with nothing to play; set <see cref="Code"/> and <see cref="Hunks"/>.</summary>
    public CodeSuggestionSimulation()
    {
    }

    /// <inheritdoc cref="CodeProperty"/>
    public string? Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }

    /// <inheritdoc cref="HunksProperty"/>
    public IReadOnlyList<CodeSuggestionHunk>? Hunks { get => GetValue(HunksProperty); set => SetValue(HunksProperty, value); }

    /// <summary>Asks the card that plays it to start a new run from the plain file.</summary>
    public void Restart() => RestartRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Raised by <see cref="Restart"/>; the playing card starts a run.</summary>
    internal event EventHandler? RestartRequested;

    /// <summary>A fresh copy of the hunks, complete, so a run never changes the list it was given.</summary>
    internal List<CodeSuggestionHunk> CopyHunks()
    {
        var copies = new List<CodeSuggestionHunk>();
        foreach (var hunk in Hunks ?? [])
        {
            copies.Add(new CodeSuggestionHunk(hunk.Id, hunk.Start, hunk.Remove, [.. hunk.Insert]) { Summary = hunk.Summary });
        }
        return copies;
    }
}
