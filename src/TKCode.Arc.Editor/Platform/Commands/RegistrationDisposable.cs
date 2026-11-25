// Ported from vscode (src/vs/base/common/lifecycle.ts: toDisposable and combinedDisposable, commit b57d5c9e), MIT
// license. Only what the command and keybinding registries need: a registration that undoes itself once.

namespace TKCode.Arc.Editor.Platform;

/// <summary>Runs its callback on the first <see cref="Dispose"/> and does nothing after that.</summary>
internal sealed class RegistrationDisposable : IDisposable
{
    private Action? _dispose;

    private RegistrationDisposable(Action dispose)
    {
        _dispose = dispose;
    }

    /// <summary>Upstream's toDisposable.</summary>
    public static IDisposable ToDisposable(Action dispose) => new RegistrationDisposable(dispose);

    /// <summary>Upstream's combinedDisposable: disposes each of <paramref name="disposables"/> in order.</summary>
    public static IDisposable Combined(params IDisposable[] disposables) => new RegistrationDisposable(() =>
    {
        foreach (var disposable in disposables)
        {
            disposable.Dispose();
        }
    });

    public void Dispose()
    {
        var dispose = _dispose;
        _dispose = null;
        dispose?.Invoke();
    }
}
