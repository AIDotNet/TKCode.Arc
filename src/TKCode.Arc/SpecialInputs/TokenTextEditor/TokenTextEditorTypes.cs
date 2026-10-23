using Avalonia;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A token inside <see cref="TokenTextEditor.Text"/>: the characters from <see cref="Start"/> to <see cref="End"/> (end
/// exclusive) act as one unit. <see cref="Kind"/> is the caller's own name for what the token is (for example
/// <c>"Command"</c> or <c>"Mention"</c>); it picks the token's <see cref="TokenStyle"/>.
/// </summary>
/// <param name="Kind">What the token is; matches <see cref="TokenTrigger.Kind"/> and <see cref="TokenStyle.Kind"/>.</param>
/// <param name="Id">The id of the thing the token points at, for storage and sending.</param>
/// <param name="Label">The name shown after the trigger symbol.</param>
/// <param name="Start">Offset of the token's first character.</param>
/// <param name="End">Offset just after the token's last character.</param>
public sealed record TokenSpan(string Kind, string Id, string Label, int Start, int End)
{
    /// <summary>The number of characters the token covers.</summary>
    public int Length => End - Start;
}

/// <summary>Where a <see cref="TokenTrigger"/>'s symbol may start a query.</summary>
public enum TokenTriggerPosition
{
    /// <summary>Anywhere, even inside a word.</summary>
    Anywhere,

    /// <summary>At the start of the text, of a line, or after whitespace (Arc's <c>@</c>).</summary>
    AfterWhitespaceOrLineStart,

    /// <summary>Only as the first character of a line (Arc's <c>/</c> commands).</summary>
    LineStart
}

/// <summary>How a token that wraps over a line break is painted (CSS <c>box-decoration-break</c>).</summary>
public enum TokenDecorationBreak
{
    /// <summary>One box cut at the break: only the token's own two ends are rounded and outset (the CSS default).</summary>
    Slice,

    /// <summary>Every line gets a complete box with all corners rounded.</summary>
    Clone
}

/// <summary>
/// A symbol that opens a query in a <see cref="TokenTextEditor"/> (for example <c>/</c> for commands or <c>@</c> for
/// mentions). The editor reports the query through <see cref="TokenTextEditor.TriggerChanged"/>; the owner shows its list
/// and calls <see cref="TokenTextEditor.InsertToken"/> with the choice.
/// </summary>
public class TokenTrigger : AvaloniaObject
{
    /// <summary>Defines the <see cref="Symbol"/> property.</summary>
    public static readonly StyledProperty<char> SymbolProperty =
        AvaloniaProperty.Register<TokenTrigger, char>(nameof(Symbol), '@');

    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<string> KindProperty =
        AvaloniaProperty.Register<TokenTrigger, string>(nameof(Kind), string.Empty);

    /// <summary>Defines the <see cref="Position"/> property.</summary>
    public static readonly StyledProperty<TokenTriggerPosition> PositionProperty =
        AvaloniaProperty.Register<TokenTrigger, TokenTriggerPosition>(nameof(Position), TokenTriggerPosition.AfterWhitespaceOrLineStart);

    /// <summary>Defines the <see cref="AllowSpacesInQuery"/> property.</summary>
    public static readonly StyledProperty<bool> AllowSpacesInQueryProperty =
        AvaloniaProperty.Register<TokenTrigger, bool>(nameof(AllowSpacesInQuery));

    /// <summary>Defines the <see cref="MaxQuerySpaces"/> property.</summary>
    public static readonly StyledProperty<int> MaxQuerySpacesProperty =
        AvaloniaProperty.Register<TokenTrigger, int>(nameof(MaxQuerySpaces), 1, coerce: static (_, v) => Math.Max(0, v));

    /// <summary>Defines the <see cref="AllowAfterOpeningBracket"/> property.</summary>
    public static readonly StyledProperty<bool> AllowAfterOpeningBracketProperty =
        AvaloniaProperty.Register<TokenTrigger, bool>(nameof(AllowAfterOpeningBracket));

    /// <summary>Defines the <see cref="IsEnabled"/> property.</summary>
    public static readonly StyledProperty<bool> IsEnabledProperty =
        AvaloniaProperty.Register<TokenTrigger, bool>(nameof(IsEnabled), true);

    /// <summary>Creates a trigger; set <see cref="Symbol"/> and <see cref="Kind"/>.</summary>
    public TokenTrigger()
    {
    }

    /// <summary>Creates a trigger for <paramref name="symbol"/> that inserts tokens of <paramref name="kind"/>.</summary>
    public TokenTrigger(char symbol, string kind)
    {
        Symbol = symbol;
        Kind = kind;
    }

    /// <summary>The character that opens the query (default <c>@</c>).</summary>
    public char Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>The kind of the tokens this trigger inserts.</summary>
    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Where the symbol may start a query (default after whitespace or at a line start).</summary>
    public TokenTriggerPosition Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    /// <summary>
    /// The query may contain spaces (up to <see cref="MaxQuerySpaces"/>), for names such as "Maya Chen". Off by default:
    /// the first space ends the query.
    /// </summary>
    public bool AllowSpacesInQuery
    {
        get => GetValue(AllowSpacesInQueryProperty);
        set => SetValue(AllowSpacesInQueryProperty, value);
    }

    /// <summary>The most spaces a query may hold when <see cref="AllowSpacesInQuery"/> is on (default 1).</summary>
    public int MaxQuerySpaces
    {
        get => GetValue(MaxQuerySpacesProperty);
        set => SetValue(MaxQuerySpacesProperty, value);
    }

    /// <summary>
    /// With <see cref="TokenTriggerPosition.AfterWhitespaceOrLineStart"/>, the symbol also opens right after
    /// <c>( [ {</c> or a straight quote. Off by default.
    /// </summary>
    public bool AllowAfterOpeningBracket
    {
        get => GetValue(AllowAfterOpeningBracketProperty);
        set => SetValue(AllowAfterOpeningBracketProperty, value);
    }

    /// <summary>
    /// False stops the symbol from opening a query; it still ends the queries of other triggers that pass over it.
    /// </summary>
    public bool IsEnabled
    {
        get => GetValue(IsEnabledProperty);
        set => SetValue(IsEnabledProperty, value);
    }
}

/// <summary>The colors of one token kind in a <see cref="TokenTextEditor"/>. Null brushes use the editor's defaults.</summary>
public class TokenStyle : AvaloniaObject
{
    /// <summary>Defines the <see cref="Kind"/> property.</summary>
    public static readonly StyledProperty<string> KindProperty =
        AvaloniaProperty.Register<TokenStyle, string>(nameof(Kind), string.Empty);

    /// <summary>Defines the <see cref="Fill"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<TokenStyle, IBrush?>(nameof(Fill));

    /// <summary>Defines the <see cref="Foreground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<TokenStyle, IBrush?>(nameof(Foreground));

    /// <summary>The token kind these colors apply to.</summary>
    public string Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>The token's background; null uses <see cref="TokenTextEditor.TokenFill"/>.</summary>
    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>The token's text color; null uses <see cref="TokenTextEditor.TokenForeground"/>.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }
}

/// <summary>
/// The open query of a <see cref="TokenTextEditor"/>: <see cref="Trigger"/>'s symbol at <see cref="Start"/> followed by
/// <see cref="Query"/> up to the caret. The rectangles are in the editor's coordinates (scrolling included), for
/// placing a list next to the text.
/// </summary>
/// <param name="Trigger">The trigger whose symbol opened the query.</param>
/// <param name="Query">The text typed after the symbol, up to the caret.</param>
/// <param name="Start">Offset of the symbol.</param>
/// <param name="TriggerRect">The symbol's line box: its left edge, the line's top and height.</param>
/// <param name="CaretRect">The caret's line box at the end of the query.</param>
public sealed record TokenTriggerState(TokenTrigger Trigger, string Query, int Start, Rect TriggerRect, Rect CaretRect)
{
    /// <summary>The kind of the tokens the trigger inserts (<see cref="TokenTrigger.Kind"/>).</summary>
    public string Kind => Trigger.Kind;
}

/// <summary>Arguments of <see cref="TokenTextEditor.TextChangedEvent"/>.</summary>
public sealed class TokenTextChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TokenTextChangedEventArgs(string oldText, IReadOnlyList<TokenSpan> oldTokens, string text, IReadOnlyList<TokenSpan> tokens)
        : base(TokenTextEditor.TextChangedEvent)
    {
        OldText = oldText;
        OldTokens = oldTokens;
        Text = text;
        Tokens = tokens;
    }

    /// <summary>The text before the change.</summary>
    public string OldText { get; }

    /// <summary>The tokens before the change.</summary>
    public IReadOnlyList<TokenSpan> OldTokens { get; }

    /// <summary>The text after the change.</summary>
    public string Text { get; }

    /// <summary>The tokens after the change, ordered by offset.</summary>
    public IReadOnlyList<TokenSpan> Tokens { get; }
}

/// <summary>Arguments of <see cref="TokenTextEditor.TriggerChangedEvent"/>.</summary>
public sealed class TokenTriggerChangedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TokenTriggerChangedEventArgs(TokenTriggerState? previousState, TokenTriggerState? state)
        : base(TokenTextEditor.TriggerChangedEvent)
    {
        PreviousState = previousState;
        State = state;
    }

    /// <summary>The query before the change; null when none was open.</summary>
    public TokenTriggerState? PreviousState { get; }

    /// <summary>The open query; null when none is open (the owner closes its list).</summary>
    public TokenTriggerState? State { get; }
}

/// <summary>Arguments of <see cref="TokenTextEditor.TokenInsertedEvent"/>.</summary>
public sealed class TokenInsertedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TokenInsertedEventArgs(TokenSpan token)
        : base(TokenTextEditor.TokenInsertedEvent)
    {
        Token = token;
    }

    /// <summary>The token as it now sits in the text.</summary>
    public TokenSpan Token { get; }
}

/// <summary>
/// Arguments of <see cref="TokenTextEditor.SubmitRequestedEvent"/>. A handler that sends the text marks the event
/// handled so outer handlers leave it alone.
/// </summary>
public sealed class TokenTextSubmitRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public TokenTextSubmitRequestedEventArgs(string text, IReadOnlyList<TokenSpan> tokens)
        : base(TokenTextEditor.SubmitRequestedEvent)
    {
        Text = text;
        Tokens = tokens;
    }

    /// <summary>The text when Enter was pressed.</summary>
    public string Text { get; }

    /// <summary>The tokens in <see cref="Text"/>, ordered by offset.</summary>
    public IReadOnlyList<TokenSpan> Tokens { get; }
}
