using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The voice a <see cref="ToneTuner"/> writes in.</summary>
public enum ToneVoice
{
    /// <summary>No particular voice: the draft's own.</summary>
    Neutral,

    /// <summary>Warm and approachable.</summary>
    Friendly,

    /// <summary>Direct and sure, without hedging.</summary>
    Confident,

    /// <summary>Acknowledges how the reader feels before the facts.</summary>
    Empathetic
}

/// <summary>Where a <see cref="ToneTuner"/>'s message comes from right now.</summary>
public enum ToneTunerStatus
{
    /// <summary>The original draft, untouched (the tone is at its starting point).</summary>
    Original,

    /// <summary>A rewrite is waiting for the debounce or running.</summary>
    Rewriting,

    /// <summary>The latest rewrite has landed.</summary>
    Rewritten,

    /// <summary>The latest rewrite failed; Retry runs it again.</summary>
    Failed
}

/// <summary>
/// The tone of a <see cref="ToneTuner"/>: formality and detail on three levels each (0, 1, 2) and a voice. Values outside
/// 0–2 are clamped where they are used.
/// </summary>
/// <param name="Formality">0 casual, 1 balanced, 2 formal.</param>
/// <param name="Detail">0 brief, 1 standard, 2 detailed.</param>
/// <param name="Voice">The voice.</param>
public sealed record ToneSettings(int Formality, int Detail, ToneVoice Voice)
{
    /// <summary>The starting tone: balanced, standard, neutral. The original draft is shown at this tone.</summary>
    public static ToneSettings Default { get; } = new(1, 1, ToneVoice.Neutral);

    /// <summary>The same tone with another formality.</summary>
    public ToneSettings WithFormality(int formality) => this with { Formality = Math.Clamp(formality, 0, 2) };

    /// <summary>The same tone with another detail.</summary>
    public ToneSettings WithDetail(int detail) => this with { Detail = Math.Clamp(detail, 0, 2) };

    /// <summary>The same tone with another voice.</summary>
    public ToneSettings WithVoice(ToneVoice voice) => this with { Voice = voice };

    /// <summary>Formality clamped to 0–2.</summary>
    internal int F => Math.Clamp(Formality, 0, 2);

    /// <summary>Detail clamped to 0–2.</summary>
    internal int D => Math.Clamp(Detail, 0, 2);
}

/// <summary>
/// A voice chip of a <see cref="ToneTuner"/>: pressing it sets <see cref="Voice"/> and moves both sliders to
/// <see cref="Formality"/> and <see cref="Detail"/>; pressing it again returns to the neutral voice.
/// </summary>
public sealed class TonePreset
{
    /// <summary>The voice the chip sets.</summary>
    public ToneVoice Voice { get; init; } = ToneVoice.Friendly;

    /// <summary>The chip's text; null uses the voice's name from <see cref="ToneTunerLabels"/>.</summary>
    public string? Label { get; init; }

    /// <summary>The chip's 24 × 24 stroked glyph; null shows no icon.</summary>
    public Geometry? Icon { get; init; }

    /// <summary>The formality the chip moves the slider to (0–2).</summary>
    public int Formality { get; init; } = 1;

    /// <summary>The detail the chip moves the slider to (0–2).</summary>
    public int Detail { get; init; } = 1;

    /// <summary>The three chips the tuner shows by default: Friendly, Confident and Empathetic.</summary>
    public static IReadOnlyList<TonePreset> Defaults { get; } =
    [
        new() { Voice = ToneVoice.Friendly, Icon = ArcIcons.Smile, Formality = 0, Detail = 1 },
        new() { Voice = ToneVoice.Confident, Icon = ArcIcons.Zap, Formality = 1, Detail = 0 },
        new() { Voice = ToneVoice.Empathetic, Icon = ArcIcons.Heart, Formality = 1, Detail = 2 },
    ];
}

/// <summary>One rewritten version of a <see cref="ToneTunerSimulation"/>'s draft: the tone it is written in and its text.</summary>
/// <param name="Settings">The tone (formality and detail are compared after clamping to 0–2).</param>
/// <param name="Text">The draft rewritten in that tone, with the same paragraph breaks.</param>
public sealed record ToneTunerVersion(ToneSettings Settings, string Text);

/// <summary>What a <see cref="ToneTuner"/> asks its <see cref="ToneTuner.Rewrite"/> function for.</summary>
public sealed class ToneRewriteRequest
{
    /// <summary>The original draft, the source of every rewrite.</summary>
    public required string Text { get; init; }

    /// <summary>The tone asked for.</summary>
    public required ToneSettings Settings { get; init; }

    /// <summary>0 casual, 1 balanced, 2 formal.</summary>
    public int Formality => Settings.F;

    /// <summary>0 brief, 1 standard, 2 detailed.</summary>
    public int Detail => Settings.D;

    /// <summary>The voice asked for.</summary>
    public ToneVoice Voice => Settings.Voice;

    /// <summary>
    /// A ready made instruction for the model: the tone in words, keep every fact, number and name, keep the paragraph
    /// breaks, return plain text only.
    /// </summary>
    public required string Instructions { get; init; }
}

/// <summary>The tone of a <see cref="ToneTuner"/> changed (a slider step, a chip, or code).</summary>
public sealed class ToneTunerValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ToneTunerValueChangedEventArgs(RoutedEvent routedEvent, ToneSettings oldValue, ToneSettings newValue) : base(routedEvent)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    /// <summary>The tone before.</summary>
    public ToneSettings OldValue { get; }

    /// <summary>The tone now.</summary>
    public ToneSettings NewValue { get; }
}

/// <summary>A rewrite of a <see cref="ToneTuner"/> landed.</summary>
public sealed class ToneTunerRewrittenEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ToneTunerRewrittenEventArgs(RoutedEvent routedEvent, string text, ToneSettings value, bool isSimulated) : base(routedEvent)
    {
        Text = text;
        Value = value;
        IsSimulated = isSimulated;
    }

    /// <summary>The new plain text.</summary>
    public string Text { get; }

    /// <summary>The tone it was written in.</summary>
    public ToneSettings Value { get; }

    /// <summary>True when a <see cref="ToneTunerSimulation"/> wrote it (nothing was sent).</summary>
    public bool IsSimulated { get; }
}

/// <summary>Copy of a <see cref="ToneTuner"/> succeeded.</summary>
public sealed class ToneTunerCopiedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public ToneTunerCopiedEventArgs(RoutedEvent routedEvent, string text) : base(routedEvent)
    {
        Text = text;
    }

    /// <summary>The text that was copied: the version on screen.</summary>
    public string Text { get; }
}
