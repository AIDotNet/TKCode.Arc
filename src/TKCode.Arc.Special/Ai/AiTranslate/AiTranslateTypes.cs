using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>A language <see cref="AiTranslate"/> reads and translates into.</summary>
public enum AiTranslateLanguage
{
    /// <summary>English (en).</summary>
    English,

    /// <summary>German, Deutsch (de).</summary>
    German,

    /// <summary>French, Français (fr).</summary>
    French,

    /// <summary>Spanish, Español (es).</summary>
    Spanish,

    /// <summary>Japanese, 日本語 (ja).</summary>
    Japanese,

    /// <summary>Portuguese, Português (pt).</summary>
    Portuguese
}

/// <summary>What an <see cref="AiTranslate"/> card shows.</summary>
public enum AiTranslateState
{
    /// <summary>The text in its own language.</summary>
    Original,

    /// <summary>A translation is streaming in over the dimmed text.</summary>
    Translating,

    /// <summary>A finished translation.</summary>
    Translated,

    /// <summary>The last translation failed: the previous text is back, with Couldn't translate and Retry.</summary>
    Failed
}

/// <summary>Who wrote the text of an <see cref="AiTranslate"/> card, shown in its byline.</summary>
public sealed class AiTranslateAuthor
{
    /// <summary>The person's name; the avatar shows its initials.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>A role or title shown after the name, for example "Product lead".</summary>
    public string? Role { get; init; }

    /// <summary>A photo for the avatar; initials are shown without one.</summary>
    public ImageSource? Avatar { get; init; }
}

/// <summary>One translation the card asks your <see cref="AiTranslate.Translator"/> for.</summary>
/// <param name="Text">The text to translate, in its original language.</param>
/// <param name="From">The language of <paramref name="Text"/>.</param>
/// <param name="To">The language to translate into.</param>
/// <param name="CancellationToken">Cancelled when the reader picks another language, shows the original or the card leaves the window.</param>
public sealed record AiTranslateRequest(string Text, AiTranslateLanguage From, AiTranslateLanguage To, CancellationToken CancellationToken);

/// <summary>Arguments of <see cref="AiTranslate.ValueChanged"/>: the language the reader picked.</summary>
public sealed class AiTranslateValueChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiTranslateValueChangedEventArgs(RoutedEvent routedEvent, AiTranslateLanguage language, bool isOriginal)
        : base(routedEvent)
    {
        Language = language;
        IsOriginal = isOriginal;
    }

    /// <summary>The language now on screen (the source language when the original is shown).</summary>
    public AiTranslateLanguage Language { get; }

    /// <summary>Whether the reader went back to the original.</summary>
    public bool IsOriginal { get; }
}

/// <summary>Arguments of <see cref="AiTranslate.Translated"/>: a translation that has fully arrived.</summary>
public sealed class AiTranslateCompletedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiTranslateCompletedEventArgs(RoutedEvent routedEvent, AiTranslateLanguage from, AiTranslateLanguage to, string text)
        : base(routedEvent)
    {
        From = from;
        To = to;
        Text = text;
    }

    /// <summary>The language of the original.</summary>
    public AiTranslateLanguage From { get; }

    /// <summary>The language of the translation.</summary>
    public AiTranslateLanguage To { get; }

    /// <summary>The whole translation.</summary>
    public string Text { get; }
}

/// <summary>Arguments of <see cref="AiTranslate.TranslationFailed"/>. Cancelled requests never raise it.</summary>
public sealed class AiTranslateFailedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public AiTranslateFailedEventArgs(RoutedEvent routedEvent, AiTranslateLanguage from, AiTranslateLanguage to, string message, Exception? exception)
        : base(routedEvent)
    {
        From = from;
        To = to;
        Message = message;
        Exception = exception;
    }

    /// <summary>The language of the original.</summary>
    public AiTranslateLanguage From { get; }

    /// <summary>The language that failed.</summary>
    public AiTranslateLanguage To { get; }

    /// <summary>Why it failed: the exception's message, kept as the tooltip of the status.</summary>
    public string Message { get; }

    /// <summary>The exception your translator threw, or null when a ready translation was missing.</summary>
    public Exception? Exception { get; }
}
