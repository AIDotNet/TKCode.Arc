using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;

namespace TKCode.Arc;

/// <summary>Turns on the browser audio backend.</summary>
public static class ArcAudioBrowser
{
    private static Task<bool>? s_use;

    /// <summary>
    /// Imports the <c>arc-audio</c> JavaScript module and makes <see cref="BrowserAudioBackend"/> the <see cref="ArcAudio.Backend"/>.
    /// Returns false, and leaves the current backend in place, when not running in a browser or the module cannot be imported.
    /// Safe to call more than once.
    /// </summary>
    public static Task<bool> UseAsync() => s_use ??= ImportAsync();

    private static async Task<bool> ImportAsync()
    {
        if (!OperatingSystem.IsBrowser())
        {
            return false;
        }
        try
        {
            await JSHost.ImportAsync(BrowserAudioInterop.Module, ModuleUrl());
            ArcAudio.Backend = BrowserAudioBackend.Instance;
            return true;
        }
        catch (Exception exception)
        {
            Trace.TraceWarning($"TKCode.Arc.Audio.Browser: {exception.Message}; audio stays simulated.");
            s_use = null;
            return false;
        }
    }

    private static string ModuleUrl()
    {
        using var stream = typeof(ArcAudioBrowser).Assembly.GetManifestResourceStream("TKCode.Arc.Audio.Browser.arc-audio.js")
                           ?? throw new InvalidOperationException("arc-audio.js is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return "data:text/javascript;charset=utf-8," + Uri.EscapeDataString(reader.ReadToEnd());
    }
}

/// <summary>Plays through one HTML audio element per player. Turned on by <see cref="ArcAudioBrowser.UseAsync"/>.</summary>
public sealed class BrowserAudioBackend : IAudioBackend
{
    internal static BrowserAudioBackend Instance { get; } = new();

    private BrowserAudioBackend()
    {
    }

    /// <inheritdoc />
    public string Name => "browser";

    /// <inheritdoc />
    public bool IsAudible => true;

    /// <inheritdoc />
    public IAudioPlayer CreatePlayer() => new BrowserAudioPlayer();
}
