using System.Collections.Generic;
using System.Threading;

namespace TKCode.Arc;

/// <summary>Streams one model's answer to a prompt.</summary>
public interface IArenaRunner
{
    /// <summary>
    /// Produces the answer's events; runs off the UI thread and the arena consumes them on it. The token is cancelled when the
    /// run is stopped, replaced by a new round or retried, and when the arena leaves the visual tree. Throw
    /// <see cref="ArenaRunException"/> (or any exception other than <see cref="OperationCanceledException"/>) to fail the answer.
    /// </summary>
    /// <param name="model">The model to ask.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="cancellationToken">Cancelled when the answer is no longer wanted.</param>
    IAsyncEnumerable<ArenaStreamEvent> RunAsync(AiModel model, string prompt, CancellationToken cancellationToken);
}

/// <summary>Chooses the two models of a round.</summary>
public interface IArenaPairPicker
{
    /// <summary>Picks two different models; the arena calls it for the first round and for "Next round".</summary>
    /// <param name="contestants">The pool.</param>
    /// <param name="previous">The pair of the last round, if any.</param>
    (AiModel A, AiModel B) Pick(IReadOnlyList<AiModel> contestants, (AiModel A, AiModel B)? previous);
}

/// <summary>One event of a model's stream: some text, some token counts, or both.</summary>
/// <param name="Text">The next text increment, of any size.</param>
/// <param name="InputTokens">The input tokens used, when the provider reports them.</param>
/// <param name="OutputTokens">The output tokens used so far, when the provider reports them.</param>
public readonly record struct ArenaStreamEvent(string? Text = null, int? InputTokens = null, int? OutputTokens = null)
{
    /// <summary>A text increment.</summary>
    public static implicit operator ArenaStreamEvent(string text) => new(text);
}

/// <summary>A failure of one model's stream that the runner can describe.</summary>
public class ArenaRunException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What the error card says.</param>
    /// <param name="title">The error card's title; null uses the labels' default.</param>
    /// <param name="isRetryable">Whether "Retry" is offered (otherwise the card offers the sample prompts).</param>
    public ArenaRunException(string message, string? title = null, bool isRetryable = true)
        : base(message)
    {
        Title = title;
        IsRetryable = isRetryable;
    }

    /// <summary>The error card's title.</summary>
    public string? Title { get; }

    /// <summary>Whether the answer can be retried; other exceptions count as retryable.</summary>
    public bool IsRetryable { get; }
}
