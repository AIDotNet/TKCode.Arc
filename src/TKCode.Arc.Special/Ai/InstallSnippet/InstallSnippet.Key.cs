using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>Generate key: the app's key generator or the simulated one, its states, cancellation and the status line.</summary>
public partial class InstallSnippet
{
    private CancellationTokenSource? _generation;
    private string? _generated;
    private bool _generatedSimulated;
    private string? _error;

    /// <summary>The simulated generator's wait (tests replace it).</summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = static (delay, token) => Task.Delay(delay, token);

    /// <summary>The source of simulated keys (tests seed it).</summary>
    internal Random KeyRandom { get; set; } = new();

    /// <summary>Whether a key generated now would be invented (no <see cref="KeyGenerator"/>).</summary>
    internal bool IsSimulated => KeyGenerator is null;

    /// <summary>
    /// Asks for a new key as if the reader pressed Generate key: a running request is cancelled, the button shows a spinner,
    /// and the key wipes into the snippets when it arrives (or the error shows with Try again). Does nothing while
    /// <see cref="ApiKey"/> is set.
    /// </summary>
    public async Task GenerateKeyAsync()
    {
        if (!string.IsNullOrEmpty(ApiKey))
        {
            return;
        }
        CancelGeneration();
        var cts = new CancellationTokenSource();
        _generation = cts;
        var generator = KeyGenerator;
        var simulated = generator is null;
        _error = null;
        KeyState = InstallSnippetKeyState.Generating;
        RefreshKeyControls();
        string? key;
        try
        {
            if (generator is null)
            {
                await Delay(InstallSnippetBuiltIns.GenerateTime, cts.Token);
                cts.Token.ThrowIfCancellationRequested();
                key = InstallSnippetBuiltIns.NewKey(KeyRandom, ActiveSimulation?.KeyPrefix ?? InstallSnippetBuiltIns.DefaultKeyPrefix);
            }
            else
            {
                key = await generator(cts.Token);
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Finish(cts);
            return;
        }
        catch (Exception exception)
        {
            if (!Finish(cts))
            {
                return;
            }
            Fail(string.IsNullOrWhiteSpace(exception.Message) ? null : exception.Message);
            return;
        }
        if (!Finish(cts) || cts.IsCancellationRequested)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(key))
        {
            Fail(null);
            return;
        }
        _generated = key.Trim();
        _generatedSimulated = simulated;
        KeyState = InstallSnippetKeyState.Ready;
        UpdateKey(animate: true);
        Announce(simulated ? Labels.SimulatedKeyAnnouncement : Labels.KeyAnnouncement);
        RaiseEvent(new InstallSnippetKeyGeneratedEventArgs(KeyGeneratedEvent, _generated, simulated));
    }

    /// <summary>Lets a request go; true when it was the current one.</summary>
    private bool Finish(CancellationTokenSource cts)
    {
        var current = ReferenceEquals(_generation, cts);
        if (current)
        {
            _generation = null;
        }
        cts.Dispose();
        return current;
    }

    private void Fail(string? message)
    {
        _error = message;
        KeyState = InstallSnippetKeyState.Failed;
        RefreshKeyControls();
        Announce(message ?? Labels.KeyError);
    }

    /// <summary>Cancels a running request (the card left the window, the app passed its own key or a new key was asked for).</summary>
    private void CancelGeneration()
    {
        if (_generation is not { } cts)
        {
            return;
        }
        _generation = null;
        cts.Cancel();
        if (KeyState == InstallSnippetKeyState.Generating)
        {
            KeyState = _generated is null ? InstallSnippetKeyState.Idle : InstallSnippetKeyState.Ready;
            RefreshKeyControls();
        }
    }

    /// <summary>Puts the current key (the app's, else the generated one) into every snippet.</summary>
    private void UpdateKey(bool animate)
    {
        var key = !string.IsNullOrEmpty(ApiKey) ? ApiKey : _generated;
        if (key == CurrentKey && !animate)
        {
            return;
        }
        CurrentKey = key;
        Refresh(animateKey: animate);
    }

    private void OnGenerateClick(object? sender, RoutedEventArgs e) => _ = GenerateKeyAsync();

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelGeneration();
    }

    /// <summary>The Generate key button and the status line under the key snippet.</summary>
    private void RefreshKeyControls()
    {
        var labels = Labels;
        var own = !string.IsNullOrEmpty(ApiKey);
        var state = KeyState;
        PseudoClasses.Set(GeneratingPseudoClass, state == InstallSnippetKeyState.Generating);
        PseudoClasses.Set(FailedPseudoClass, state == InstallSnippetKeyState.Failed && !own);
        PseudoClasses.Set(ReadyPseudoClass, state == InstallSnippetKeyState.Ready && !own);
        if (_generate is not null)
        {
            _generate.IsVisible = !own;
            var label = state switch
            {
                InstallSnippetKeyState.Failed => labels.TryAgain,
                InstallSnippetKeyState.Ready => labels.NewKey,
                InstallSnippetKeyState.Generating when _generated is not null => labels.NewKey,
                _ => labels.GenerateKey,
            };
            if (!Equals(_generate.Content, label))
            {
                _generate.Content = label;
            }
            if (_generate is Button arc)
            {
                arc.IsLoading = state == InstallSnippetKeyState.Generating;
            }
        }
        var (glyph, text) = state switch
        {
            InstallSnippetKeyState.Generating => (ArcIcons.Key, labels.Creating),
            InstallSnippetKeyState.Ready => (ArcIcons.CircleCheck, _generatedSimulated ? labels.CreatedSimulated : labels.Created),
            InstallSnippetKeyState.Failed => (ArcIcons.CircleAlert, _error ?? labels.KeyError),
            _ => (ArcIcons.Info, IsSimulated ? labels.SimulatedHint : labels.KeyHint),
        };
        if (_keyStatus is not null)
        {
            _keyStatus.IsVisible = !own;
            _keyStatus.Show(glyph, text, state);
        }
    }
}
