namespace TKCode.Arc;

/// <summary>
/// Simulated key tests for a <see cref="ProviderKeys"/> without <see cref="ProviderKeys.TestKey"/>, for demos and
/// prototypes: after <see cref="Delay"/>, retesting a stored key whose status is Invalid fails again with its own error
/// (or <see cref="RejectedError"/>), and every other test connects. Nothing is sent anywhere, and the card adds
/// "Simulated, nothing was sent." to what it announces.
/// </summary>
public sealed class ProviderKeysSimulation
{
    /// <summary>How long a simulated test takes (700ms by default: under a second).</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(700);

    /// <summary>The error a stored invalid key fails with when it has none of its own; null uses the card's unknown error.</summary>
    public string? RejectedError { get; set; }

    /// <summary>
    /// Tests a key: <paramref name="pastedKey"/> when adding or rotating, or the <paramref name="stored"/> key when it is
    /// null. A stored invalid key fails again; everything else connects.
    /// </summary>
    public async Task<ProviderKeyTestResult> TestAsync(ProviderKey? stored, string? pastedKey, CancellationToken cancellationToken)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (pastedKey is null && stored is { Status: ProviderKeyStatus.Invalid })
        {
            return ProviderKeyTestResult.Fail(stored.Error ?? RejectedError ?? string.Empty);
        }
        return ProviderKeyTestResult.Ok();
    }
}
