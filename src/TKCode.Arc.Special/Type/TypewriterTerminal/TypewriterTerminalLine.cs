using Avalonia;

namespace TKCode.Arc;

/// <summary>Whether a <see cref="TypewriterTerminalLine"/> is typed at a prompt or printed by the program.</summary>
public enum TypewriterTerminalLineKind
{
    /// <summary>Printed whole, as a program's output.</summary>
    Output,

    /// <summary>Typed character by character after the prompt.</summary>
    Command
}

/// <summary>The color of a <see cref="TypewriterTerminalLine"/>.</summary>
public enum TypewriterTerminalTone
{
    /// <summary>The terminal's text color.</summary>
    Default,

    /// <summary>Quieter text, for progress and comments.</summary>
    Muted,

    /// <summary>The accent color, for links and highlights.</summary>
    Accent,

    /// <summary>The success color.</summary>
    Success,

    /// <summary>The warning color.</summary>
    Warning,

    /// <summary>The danger color, for errors.</summary>
    Danger
}

/// <summary>One line of a <see cref="TypewriterTerminal"/> script: a command to type or a line of output.</summary>
public class TypewriterTerminalLine : AvaloniaObject
{
    /// <summary>The text of the line, without the prompt.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<TypewriterTerminalLine, string?>(nameof(Text));

    /// <summary>Whether the line is typed at a prompt (Command) or printed whole (Output, the default).</summary>
    public static readonly StyledProperty<TypewriterTerminalLineKind> KindProperty =
        AvaloniaProperty.Register<TypewriterTerminalLine, TypewriterTerminalLineKind>(nameof(Kind));

    /// <summary>The color of the line (the terminal's text color by default).</summary>
    public static readonly StyledProperty<TypewriterTerminalTone> ToneProperty =
        AvaloniaProperty.Register<TypewriterTerminalLine, TypewriterTerminalTone>(nameof(Tone));

    /// <summary>An extra wait before the line, such as a slow build step.</summary>
    public static readonly StyledProperty<TimeSpan> PauseProperty =
        AvaloniaProperty.Register<TypewriterTerminalLine, TimeSpan>(nameof(Pause));

    public TypewriterTerminalLine()
    {
    }

    public TypewriterTerminalLine(TypewriterTerminalLineKind kind, string text, TypewriterTerminalTone tone = TypewriterTerminalTone.Default)
    {
        Kind = kind;
        Text = text;
        Tone = tone;
    }

    /// <summary>A command typed after the prompt.</summary>
    public static TypewriterTerminalLine Command(string text) => new(TypewriterTerminalLineKind.Command, text);

    /// <summary>A line of output in the given tone.</summary>
    public static TypewriterTerminalLine Output(string text, TypewriterTerminalTone tone = TypewriterTerminalTone.Default) =>
        new(TypewriterTerminalLineKind.Output, text, tone);

    /// <inheritdoc cref="TextProperty"/>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc cref="KindProperty"/>
    public TypewriterTerminalLineKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <inheritdoc cref="ToneProperty"/>
    public TypewriterTerminalTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <inheritdoc cref="PauseProperty"/>
    public TimeSpan Pause
    {
        get => GetValue(PauseProperty);
        set => SetValue(PauseProperty, value);
    }

    internal event EventHandler? Changed;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
