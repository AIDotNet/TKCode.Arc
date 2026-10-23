using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>Callbacks for a control built on <see cref="TokenTextEditor"/> (MentionInput), at the moments its own logic ran before.</summary>
internal interface ITokenTextEditorHost
{
    /// <summary>A key, text input or pointer operation is about to reach the TextBox.</summary>
    void OnEditorOperationStarting();

    /// <summary>The TextBox gained or lost focus (before the trigger is refreshed).</summary>
    void OnEditorFocusChanged(bool focused);

    /// <summary>The trigger is about to be derived from the text and caret.</summary>
    void OnTriggerRefreshing();

    /// <summary>The trigger was derived again (it may be unchanged).</summary>
    void OnTriggerRefreshed();

    /// <summary>The TextBox laid its text out again (typing, wrapping, a new width).</summary>
    void OnEditorLayoutChanged();

    /// <summary>The TextBox scrolled.</summary>
    void OnEditorScrolled();
}

/// <summary>
/// A multi-line text editor whose tokens (commands, mentions, variables) act as single units: arrow keys hop over a
/// token, one Backspace or Delete next to it removes all of it, and a click inside lands on its nearest edge. Typing a
/// trigger symbol (<see cref="Triggers"/>, such as <c>/</c> or <c>@</c>) opens a query that the owner turns into a list
/// (<see cref="TriggerChanged"/>) and answers with <see cref="InsertToken"/>. The value is plain <see cref="Text"/> plus
/// <see cref="Tokens"/> with offsets.
/// </summary>
/// <remarks>
/// <para>
/// The editor is a real <see cref="TextBox"/> (caret, selection, IME, undo, clipboard) whose own text is transparent; a
/// mirror behind it paints the same text with identical metrics and tints the tokens (<see cref="TokenStyles"/>).
/// Insertions type through the TextBox, so Ctrl/Cmd+Z undoes them, and undo / redo bring back the tokens that went
/// with the text. Editing inside a token (paste, a programmatic edit) dissolves it into plain text.
/// </para>
/// <para>
/// The height follows the text from <see cref="MinLines"/> to <see cref="MaxLines"/> lines, then the text scrolls.
/// Enter raises <see cref="SubmitRequested"/> (Shift+Enter adds a line). The control draws no frame: place it in your
/// own surface.
/// </para>
/// </remarks>
[TemplatePart(TextBoxPart, typeof(TextBox))]
public class TokenTextEditor : TemplatedControl
{
    /// <summary>The TextBox inside the template.</summary>
    public const string TextBoxPart = "PART_TextBox";

    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<TokenTextEditor, string>(nameof(Text), string.Empty,
            defaultBindingMode: BindingMode.TwoWay, coerce: static (_, value) => value ?? string.Empty);

    /// <summary>Defines the <see cref="Tokens"/> property.</summary>
    public static readonly DirectProperty<TokenTextEditor, IReadOnlyList<TokenSpan>> TokensProperty =
        AvaloniaProperty.RegisterDirect<TokenTextEditor, IReadOnlyList<TokenSpan>>(nameof(Tokens), static o => o.Tokens);

    /// <summary>Defines the <see cref="Placeholder"/> property.</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<TokenTextEditor, string?>(nameof(Placeholder));

    /// <summary>Defines the <see cref="PlaceholderForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> PlaceholderForegroundProperty =
        AvaloniaProperty.Register<TokenTextEditor, IBrush?>(nameof(PlaceholderForeground));

    /// <summary>Defines the <see cref="CaretBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CaretBrushProperty =
        AvaloniaProperty.Register<TokenTextEditor, IBrush?>(nameof(CaretBrush));

    /// <summary>Defines the <see cref="IsReadOnly"/> property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<TokenTextEditor, bool>(nameof(IsReadOnly));

    /// <summary>Defines the <see cref="MinLines"/> property.</summary>
    public static readonly StyledProperty<int> MinLinesProperty =
        AvaloniaProperty.Register<TokenTextEditor, int>(nameof(MinLines), 1, coerce: static (_, v) => Math.Max(0, v));

    /// <summary>Defines the <see cref="MaxLines"/> property.</summary>
    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<TokenTextEditor, int>(nameof(MaxLines), 8, coerce: static (_, v) => Math.Max(1, v));

    /// <summary>Defines the <see cref="LineHeight"/> property.</summary>
    public static readonly StyledProperty<double> LineHeightProperty =
        TextBlock.LineHeightProperty.AddOwner<TokenTextEditor>();

    /// <summary>Defines the <see cref="SubmitOnEnter"/> property.</summary>
    public static readonly StyledProperty<bool> SubmitOnEnterProperty =
        AvaloniaProperty.Register<TokenTextEditor, bool>(nameof(SubmitOnEnter), true);

    /// <summary>Defines the <see cref="ExpandSelectionToTokens"/> property.</summary>
    public static readonly StyledProperty<bool> ExpandSelectionToTokensProperty =
        AvaloniaProperty.Register<TokenTextEditor, bool>(nameof(ExpandSelectionToTokens));

    /// <summary>Defines the <see cref="TokenFill"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TokenFillProperty =
        AvaloniaProperty.Register<TokenTextEditor, IBrush?>(nameof(TokenFill));

    /// <summary>Defines the <see cref="TokenForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> TokenForegroundProperty =
        AvaloniaProperty.Register<TokenTextEditor, IBrush?>(nameof(TokenForeground));

    /// <summary>Defines the <see cref="TokenCornerRadius"/> property.</summary>
    public static readonly StyledProperty<double> TokenCornerRadiusProperty =
        AvaloniaProperty.Register<TokenTextEditor, double>(nameof(TokenCornerRadius), 6);

    /// <summary>Defines the <see cref="TokenPadding"/> property.</summary>
    public static readonly StyledProperty<Thickness> TokenPaddingProperty =
        AvaloniaProperty.Register<TokenTextEditor, Thickness>(nameof(TokenPadding));

    /// <summary>Defines the <see cref="TokenOutset"/> property.</summary>
    public static readonly StyledProperty<double> TokenOutsetProperty =
        AvaloniaProperty.Register<TokenTextEditor, double>(nameof(TokenOutset), 2);

    /// <summary>Defines the <see cref="TokenDecorationBreak"/> property.</summary>
    public static readonly StyledProperty<TokenDecorationBreak> TokenDecorationBreakProperty =
        AvaloniaProperty.Register<TokenTextEditor, TokenDecorationBreak>(nameof(TokenDecorationBreak));

    /// <summary>Defines the <see cref="HeightMotion"/> property.</summary>
    public static readonly StyledProperty<MotionSpec?> HeightMotionProperty =
        AvaloniaProperty.Register<TokenTextEditor, MotionSpec?>(nameof(HeightMotion),
            MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));

    /// <summary>Defines the <see cref="SuggestionsExpanded"/> property.</summary>
    public static readonly StyledProperty<bool> SuggestionsExpandedProperty =
        AvaloniaProperty.Register<TokenTextEditor, bool>(nameof(SuggestionsExpanded));

    /// <summary>Defines the <see cref="TriggerState"/> property.</summary>
    public static readonly DirectProperty<TokenTextEditor, TokenTriggerState?> TriggerStateProperty =
        AvaloniaProperty.RegisterDirect<TokenTextEditor, TokenTriggerState?>(nameof(TriggerState), static o => o.TriggerState);

    /// <summary>Defines the <see cref="TokenRenderer"/> property.</summary>
    public static readonly StyledProperty<TokenRenderer?> TokenRendererProperty =
        AvaloniaProperty.Register<TokenTextEditor, TokenRenderer?>(nameof(TokenRenderer));

    /// <summary>Defines the <see cref="TokenParser"/> property.</summary>
    public static readonly StyledProperty<Func<string, IEnumerable<TokenSpan>>?> TokenParserProperty =
        AvaloniaProperty.Register<TokenTextEditor, Func<string, IEnumerable<TokenSpan>>?>(nameof(TokenParser));

    /// <summary>The accent the default token fill and the selection derive from.</summary>
    internal static readonly StyledProperty<IBrush?> AccentSourceProperty =
        AvaloniaProperty.Register<TokenTextEditor, IBrush?>("AccentSource");

    /// <summary>The default token fill is the accent at this opacity (Arc 12%).</summary>
    internal static readonly StyledProperty<double> TokenFillOpacityProperty =
        AvaloniaProperty.Register<TokenTextEditor, double>("TokenFillOpacity", 0.12);

    /// <summary>The selection is the accent at this opacity (Arc 24%).</summary>
    internal static readonly StyledProperty<double> SelectionOpacityProperty =
        AvaloniaProperty.Register<TokenTextEditor, double>("SelectionOpacity", 0.24);

    /// <summary>Raised after the text or its tokens changed (typing, insertion, undo, programmatic).</summary>
    public static readonly RoutedEvent<TokenTextChangedEventArgs> TextChangedEvent =
        RoutedEvent.Register<TokenTextEditor, TokenTextChangedEventArgs>(nameof(TextChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when the open query starts, changes, moves or ends.</summary>
    public static readonly RoutedEvent<TokenTriggerChangedEventArgs> TriggerChangedEvent =
        RoutedEvent.Register<TokenTextEditor, TokenTriggerChangedEventArgs>(nameof(TriggerChanged), RoutingStrategies.Bubble);

    /// <summary>Raised when <see cref="InsertToken"/> or <see cref="InsertTokenAtCaret"/> added a token.</summary>
    public static readonly RoutedEvent<TokenInsertedEventArgs> TokenInsertedEvent =
        RoutedEvent.Register<TokenTextEditor, TokenInsertedEventArgs>(nameof(TokenInserted), RoutingStrategies.Bubble);

    /// <summary>Raised when Enter (without Shift, outside an IME composition) asks to send the text.</summary>
    public static readonly RoutedEvent<TokenTextSubmitRequestedEventArgs> SubmitRequestedEvent =
        RoutedEvent.Register<TokenTextEditor, TokenTextSubmitRequestedEventArgs>(nameof(SubmitRequested), RoutingStrategies.Bubble);

    /// <summary>Raised when assistive technology collapses the owner's list (the expand / collapse pattern).</summary>
    public static readonly RoutedEvent<RoutedEventArgs> CollapseRequestedEvent =
        RoutedEvent.Register<TokenTextEditor, RoutedEventArgs>(nameof(CollapseRequested), RoutingStrategies.Bubble);

    /// <summary>How many text states remember their tokens for undo and redo.</summary>
    private const int HistoryLimit = 100;

    private readonly Dictionary<string, TokenSpan[]> _history = new(StringComparer.Ordinal);
    private readonly Queue<string> _historyOrder = new();
    private readonly List<AvaloniaObject> _watched = new();
    private TokenTextBox? _box;
    private string _text = string.Empty;
    private IReadOnlyList<TokenSpan> _tokens = Array.Empty<TokenSpan>();
    private TokenTriggerState? _triggerState;
    private TokenTriggerMatch? _match;
    private int _lastCaret;
    private int _lastActive;
    private int? _dismissed;
    private TokenSpan? _pending;
    private string? _pendingText;
    private TokenSpan? _inserted;
    private int _operationDepth;
    private bool _pointerOperation;
    private int _restoreDepth;
    private bool _normalizing;
    private bool _syncingBox;
    private bool _settingText;
    private bool _refreshing;
    private bool _refreshPosted;
    private bool _geometryPosted;
    private bool _measuring;
    private MotionValue<double>? _height;
    private double _heightTarget = -1;
    private double _shownHeight;
    private bool _heightPlaced;
    private IBrush? _defaultFill;
    private int _brushVersion;
    private bool _tokenCursor;

    static TokenTextEditor()
    {
        FocusableProperty.OverrideDefaultValue<TokenTextEditor>(true);
        // Focus() lands on the TextBox; Tab goes straight to it.
        KeyboardNavigation.IsTabStopProperty.OverrideDefaultValue<TokenTextEditor>(false);
        AffectsMeasure<TokenTextEditor>(MinLinesProperty, MaxLinesProperty);
        // Before the owner's own tunnel handlers on this element: the trigger they read is current.
        KeyDownEvent.AddClassHandler<TokenTextEditor>(static (editor, e) => editor.OnTunnelKeyDown(e), RoutingStrategies.Tunnel);
    }

    /// <summary>Creates an editor with no triggers and no token styles.</summary>
    public TokenTextEditor()
    {
        Triggers = new AvaloniaList<TokenTrigger>();
        Triggers.CollectionChanged += (_, _) => WatchItems();
        TokenStyles = new AvaloniaList<TokenStyle>();
        TokenStyles.CollectionChanged += (_, _) =>
        {
            WatchItems();
            InvalidateTokenBrushes();
        };
        ArcTokens.Bind(this, AccentSourceProperty, DesignTokens.ArcDesignTokenKind.Accent);
    }

    #region Properties

    /// <summary>The plain text, tokens included. Two-way bindable; typing writes it.</summary>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The tokens in <see cref="Text"/>, ordered by offset. Change them with <see cref="SetTextWithTokens"/> or the insert methods.</summary>
    public IReadOnlyList<TokenSpan> Tokens
    {
        get => _tokens;
        private set => SetAndRaise(TokensProperty, ref _tokens, value);
    }

    /// <summary>Muted text shown while the editor is empty.</summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Color of <see cref="Placeholder"/> (theme: <c>TextMuted</c>).</summary>
    public IBrush? PlaceholderForeground
    {
        get => GetValue(PlaceholderForegroundProperty);
        set => SetValue(PlaceholderForegroundProperty, value);
    }

    /// <summary>Color of the caret (theme: <c>Foreground</c>).</summary>
    public IBrush? CaretBrush
    {
        get => GetValue(CaretBrushProperty);
        set => SetValue(CaretBrushProperty, value);
    }

    /// <summary>The text can be selected and copied but not typed into; the insert methods still apply.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Lines the editor shows at least (default 1).</summary>
    public int MinLines
    {
        get => GetValue(MinLinesProperty);
        set => SetValue(MinLinesProperty, value);
    }

    /// <summary>The editor grows to this many lines, then the text scrolls inside (default 8).</summary>
    public int MaxLines
    {
        get => GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    /// <summary>Height of one line (theme: 24); the editor grows in steps of it. NaN uses 1.5 × the font size.</summary>
    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>
    /// Enter raises <see cref="SubmitRequested"/> instead of adding a line (Shift+Enter always adds one). Default true.
    /// </summary>
    public bool SubmitOnEnter
    {
        get => GetValue(SubmitOnEnterProperty);
        set => SetValue(SubmitOnEnterProperty, value);
    }

    /// <summary>
    /// A selection that starts or ends inside a token grows to the whole token. Off by default: Shift+arrows select
    /// character by character (the caret alone never rests inside a token).
    /// </summary>
    public bool ExpandSelectionToTokens
    {
        get => GetValue(ExpandSelectionToTokensProperty);
        set => SetValue(ExpandSelectionToTokensProperty, value);
    }

    /// <summary>Background of tokens whose kind has no <see cref="TokenStyle.Fill"/>. Null: the accent at 12%.</summary>
    public IBrush? TokenFill
    {
        get => GetValue(TokenFillProperty);
        set => SetValue(TokenFillProperty, value);
    }

    /// <summary>Text color of tokens whose kind has no <see cref="TokenStyle.Foreground"/> (theme: <c>AccentStrong</c>).</summary>
    public IBrush? TokenForeground
    {
        get => GetValue(TokenForegroundProperty);
        set => SetValue(TokenForegroundProperty, value);
    }

    /// <summary>Corner radius of a token's box before the outset (default 6).</summary>
    public double TokenCornerRadius
    {
        get => GetValue(TokenCornerRadiusProperty);
        set => SetValue(TokenCornerRadiusProperty, value);
    }

    /// <summary>Space between a token's glyphs and the edge of its box (default none). Glyphs never move.</summary>
    public Thickness TokenPadding
    {
        get => GetValue(TokenPaddingProperty);
        set => SetValue(TokenPaddingProperty, value);
    }

    /// <summary>
    /// How far the box grows past the padding on every side, rounding its corners by as much (Arc's 2px
    /// <c>box-shadow</c> spread; default 2). It may reach outside the line.
    /// </summary>
    public double TokenOutset
    {
        get => GetValue(TokenOutsetProperty);
        set => SetValue(TokenOutsetProperty, value);
    }

    /// <summary>How a token that wraps over a line break is painted (default <see cref="TokenDecorationBreak.Slice"/>).</summary>
    public TokenDecorationBreak TokenDecorationBreak
    {
        get => GetValue(TokenDecorationBreakProperty);
        set => SetValue(TokenDecorationBreakProperty, value);
    }

    /// <summary>
    /// How the height follows the text at full motion (default Arc's 160 ms standard ease). Null, reduced motion and
    /// no motion jump.
    /// </summary>
    public MotionSpec? HeightMotion
    {
        get => GetValue(HeightMotionProperty);
        set => SetValue(HeightMotionProperty, value);
    }

    /// <summary>
    /// The owner's list for the open query is showing. Set by the owner; assistive technology reads it as the
    /// TextBox's expanded state.
    /// </summary>
    public bool SuggestionsExpanded
    {
        get => GetValue(SuggestionsExpandedProperty);
        set => SetValue(SuggestionsExpandedProperty, value);
    }

    /// <summary>The open query, or null. Only set while the TextBox has focus and no text is selected.</summary>
    public TokenTriggerState? TriggerState
    {
        get => _triggerState;
        private set => SetAndRaise(TriggerStateProperty, ref _triggerState, value);
    }

    /// <summary>The symbols that open a query. Empty by default.</summary>
    public AvaloniaList<TokenTrigger> Triggers { get; }

    /// <summary>Colors per token kind; kinds without a style use <see cref="TokenFill"/> and <see cref="TokenForeground"/>.</summary>
    public AvaloniaList<TokenStyle> TokenStyles { get; }

    /// <summary>
    /// Draws the tokens it measures as single inline objects (chips) in place of their characters; the others keep
    /// their <see cref="TokenStyles"/>. Null (default): every token is tinted text.
    /// </summary>
    public TokenRenderer? TokenRenderer
    {
        get => GetValue(TokenRendererProperty);
        set => SetValue(TokenRendererProperty, value);
    }

    /// <summary>
    /// Finds the tokens in a text. When set, the tokens are always the parser's result for the current
    /// <see cref="Text"/> (typing, pasting, undo and programmatic changes alike), and the tokens passed to
    /// <see cref="SetTextWithTokens"/> or added by the insert methods are ignored. Null (default): tokens are placed by
    /// the insert methods and move with the edits.
    /// </summary>
    public Func<string, IEnumerable<TokenSpan>>? TokenParser
    {
        get => GetValue(TokenParserProperty);
        set => SetValue(TokenParserProperty, value);
    }

    /// <summary>True while an IME composition is in progress (Enter belongs to the IME then).</summary>
    public bool IsComposing => _box?.IsComposing == true;

    /// <summary>The caret position in <see cref="Text"/>.</summary>
    public int CaretIndex => _box?.CaretIndex ?? _text.Length;

    /// <summary>Where the selection starts (its anchor) in <see cref="Text"/>; equal to <see cref="SelectionEnd"/> when nothing is selected.</summary>
    public int SelectionStart => _box?.SelectionStart ?? _text.Length;

    /// <summary>Where the selection ends (its moving end) in <see cref="Text"/>.</summary>
    public int SelectionEnd => _box?.SelectionEnd ?? _text.Length;

    /// <summary>Raised after the text or its tokens changed.</summary>
    public event EventHandler<TokenTextChangedEventArgs>? TextChanged
    {
        add => AddHandler(TextChangedEvent, value);
        remove => RemoveHandler(TextChangedEvent, value);
    }

    /// <summary>Raised when the open query starts, changes, moves or ends.</summary>
    public event EventHandler<TokenTriggerChangedEventArgs>? TriggerChanged
    {
        add => AddHandler(TriggerChangedEvent, value);
        remove => RemoveHandler(TriggerChangedEvent, value);
    }

    /// <summary>Raised when an insert method added a token.</summary>
    public event EventHandler<TokenInsertedEventArgs>? TokenInserted
    {
        add => AddHandler(TokenInsertedEvent, value);
        remove => RemoveHandler(TokenInsertedEvent, value);
    }

    /// <summary>Raised when Enter asks to send the text.</summary>
    public event EventHandler<TokenTextSubmitRequestedEventArgs>? SubmitRequested
    {
        add => AddHandler(SubmitRequestedEvent, value);
        remove => RemoveHandler(SubmitRequestedEvent, value);
    }

    /// <summary>Raised when assistive technology collapses the owner's list.</summary>
    public event EventHandler<RoutedEventArgs>? CollapseRequested
    {
        add => AddHandler(CollapseRequestedEvent, value);
        remove => RemoveHandler(CollapseRequestedEvent, value);
    }

    /// <summary>The control built on this editor, if any (MentionInput).</summary>
    internal ITokenTextEditorHost? Host { get; set; }

    /// <summary>Creates the mirror when the TextBox is templated (MentionInput supplies its own subclass).</summary>
    internal Func<TokenTextMirror>? MirrorFactory { get; set; }

    /// <summary>Undo and redo bring back the tokens of the text they restore. MentionInput keeps its old behaviour.</summary>
    internal bool RestoresTokensOnUndo { get; set; } = true;

    internal TokenTextBox? Box => _box;

    /// <summary>The height the editor is moving to (lines within the limits plus padding).</summary>
    internal double TargetHeight => _heightTarget;

    /// <summary>Bumped when a token or text brush changes; the mirror rebuilds its colored layout.</summary>
    internal int BrushVersion => _brushVersion;

    internal double TokenFillOpacity
    {
        get => GetValue(TokenFillOpacityProperty);
        set => SetValue(TokenFillOpacityProperty, value);
    }

    internal double SelectionOpacity
    {
        get => GetValue(SelectionOpacityProperty);
        set => SetValue(SelectionOpacityProperty, value);
    }

    #endregion

    #region Public methods

    /// <summary>
    /// Replaces the open query (the symbol and what follows it, such as <c>@que</c>) with a token reading the trigger's
    /// symbol and <paramref name="label"/>, plus a space unless one follows, and puts the caret after it. Types through
    /// the TextBox so undo takes it back. Returns the token, or null when no query is open.
    /// </summary>
    public TokenSpan? InsertToken(string kind, string id, string label)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(label);
        if (_match is not { } match)
        {
            return null;
        }
        var end = Math.Min(_text.Length, match.Start + 1 + match.Query.Length);
        var tokenText = match.Trigger.Symbol + label;
        return InsertTokenText(new TokenSpan(kind, id, label, match.Start, match.Start + tokenText.Length), tokenText, match.Start, end);
    }

    /// <summary>
    /// Inserts a token at the caret (over the selection), reading the symbol of the trigger for <paramref name="kind"/>
    /// (none when no trigger has that kind) and <paramref name="label"/>, with a space before it when it would touch a
    /// word and a space after it unless one follows. Returns the token as placed.
    /// </summary>
    public TokenSpan? InsertTokenAtCaret(string kind, string id, string label)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(label);
        var trigger = TokenTextEngine.TriggerForKind(Triggers, kind);
        var tokenText = trigger is null ? label : trigger.Symbol + label;
        var from = SelectionLow();
        var to = SelectionHigh();
        var start = from;
        var lead = string.Empty;
        if (from > 0 && from <= _text.Length && !char.IsWhiteSpace(_text[from - 1]))
        {
            lead = " ";
            start++;
        }
        var token = new TokenSpan(kind, id, label, start, start + tokenText.Length);
        return InsertTokenText(token, lead + tokenText, from, to, prefix: lead.Length);
    }

    /// <summary>
    /// Replaces the text and all tokens (for example to fill in a suggested prompt). Tokens outside the text or
    /// overlapping an earlier one are ignored. When the text changes, the caret moves to the end.
    /// </summary>
    public void SetTextWithTokens(string text, IEnumerable<TokenSpan> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        SetContent(text ?? string.Empty, tokens, refresh: true);
    }

    /// <summary>Types <paramref name="text"/> over the selection, as if from the keyboard.</summary>
    public void InsertText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        TypeText(text, null, null);
    }

    /// <summary>
    /// Types the symbol of the trigger for <paramref name="kind"/> at the caret (at the end when the editor is not
    /// focused) so its query opens: after a space when the trigger needs whitespace before it, on a new line when it
    /// needs a line start. Returns false when no trigger has that kind.
    /// </summary>
    public bool OpenTrigger(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (TokenTextEngine.TriggerForKind(Triggers, kind) is not { } trigger)
        {
            return false;
        }
        var text = _text;
        var focused = _box?.IsFocused == true;
        var at = focused ? SelectionLow() : text.Length;
        var to = focused ? SelectionHigh() : at;
        var before = at > 0 && at <= text.Length ? text[at - 1] : (char?)null;
        _dismissed = null;
        var symbol = trigger.Symbol.ToString();
        var typed = trigger.Position switch
        {
            TokenTriggerPosition.LineStart when before is { } c && c != '\n' => "\n" + symbol,
            TokenTriggerPosition.AfterWhitespaceOrLineStart when before is { } c && !char.IsWhiteSpace(c) => " " + symbol,
            _ => symbol
        };
        TypeText(typed, at, to);
        return true;
    }

    /// <summary>The owner dismissed its list: the open query reports null until the caret enters another trigger.</summary>
    public void CancelTrigger()
    {
        if (_match is { } match)
        {
            _dismissed = match.Start;
        }
        Refresh();
    }

    /// <summary>Selects from <paramref name="anchor"/> to <paramref name="active"/>; equal values place the caret.</summary>
    public void Select(int anchor, int active) => _box?.Select(anchor, active);

    /// <summary>Empties the editor.</summary>
    public void Clear() => SetTextWithTokens(string.Empty, Array.Empty<TokenSpan>());

    /// <summary>Undoes the last edit, bringing back the tokens of the restored text.</summary>
    public void Undo() => RunRestoring(static box => box.Undo());

    /// <summary>Redoes the last undone edit, with its tokens.</summary>
    public void Redo() => RunRestoring(static box => box.Redo());

    /// <summary>
    /// The token drawn under <paramref name="point"/> (in this editor's coordinates), or null: an inline token's box, or
    /// the glyphs of a text token.
    /// </summary>
    public TokenSpan? HitTestToken(Point point)
    {
        if (_box?.Presenter is not { } presenter || _tokens.Count == 0 || this.TranslatePoint(point, presenter) is not { } at)
        {
            return null;
        }
        presenter.CombinedText(out var preeditStart, out var preeditLength);
        var layout = presenter.TextLayout;
        foreach (var token in _tokens)
        {
            var start = preeditLength > 0 && token.Start >= preeditStart ? token.Start + preeditLength : token.Start;
            foreach (var rect in layout.HitTestTextRange(start, token.Length))
            {
                if (rect.Contains(at))
                {
                    return token;
                }
            }
        }
        return null;
    }

    #endregion

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_box is not null)
        {
            _box.Owner = null;
        }
        _box = e.NameScope.Find<TokenTextBox>(TextBoxPart);
        _heightPlaced = false;
        _heightTarget = -1;
        if (_box is not null)
        {
            _box.Owner = this;
            SyncBoxText(moveCaretToEnd: true);
            _box.SetTokens(_tokens);
            UpdateAutomation();
        }
    }

    internal void OnBoxTemplateApplied() => _box?.SetTokens(_tokens);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (ReferenceEquals(e.Source, this) && _box is { } box && box.IsEffectivelyEnabled)
        {
            box.Focus(e.NavigationMethod, e.KeyModifiers);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _heightPlaced = false;
        _heightTarget = -1;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TokenTextEditorAutomationPeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            if (!_settingText)
            {
                SetTextFromProperty(change.GetNewValue<string>() ?? string.Empty);
            }
        }
        else if (change.Property == ForegroundProperty || change.Property == TokenFillProperty ||
                 change.Property == TokenForegroundProperty)
        {
            InvalidateTokenBrushes();
        }
        else if (change.Property == AccentSourceProperty || change.Property == TokenFillOpacityProperty)
        {
            var accent = GetValue(AccentSourceProperty) is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
            _defaultFill = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, TokenFillOpacity));
            InvalidateTokenBrushes();
        }
        else if (change.Property == TokenCornerRadiusProperty || change.Property == TokenPaddingProperty ||
                 change.Property == TokenOutsetProperty || change.Property == TokenDecorationBreakProperty)
        {
            _box?.Mirror?.InvalidateVisual();
        }
        else if (change.Property == SuggestionsExpandedProperty)
        {
            if (_box is not null && ControlAutomationPeer.FromElement(_box) is TokenTextBoxAutomationPeer peer)
            {
                peer.RaiseExpandChanged(change.GetNewValue<bool>());
            }
        }
        else if (change.Property == AutomationProperties.NameProperty || change.Property == AutomationProperties.HelpTextProperty ||
                 change.Property == AutomationProperties.ItemStatusProperty)
        {
            UpdateAutomation();
        }
        else if (change.Property == IsEffectivelyEnabledProperty)
        {
            ScheduleRefresh();
        }
        else if (change.Property == TokenRendererProperty)
        {
            if (change.GetOldValue<TokenRenderer?>() is { } old)
            {
                old.Invalidated -= OnRendererInvalidated;
                old.MeasureInvalidated -= OnRendererMeasureInvalidated;
            }
            if (change.GetNewValue<TokenRenderer?>() is { } renderer)
            {
                renderer.Invalidated += OnRendererInvalidated;
                renderer.MeasureInvalidated += OnRendererMeasureInvalidated;
            }
            OnRendererMeasureInvalidated(null, EventArgs.Empty);
            UpdateTokenCursor(null);
        }
        else if (change.Property == TokenParserProperty && change.GetNewValue<Func<string, IEnumerable<TokenSpan>>?>() is not null)
        {
            // The tokens are the parser's from now on.
            Commit(_text, _tokens, setTextProperty: false);
        }
    }

    private void OnRendererInvalidated(object? sender, EventArgs e) => _box?.Mirror?.InvalidateVisual();

    private void OnRendererMeasureInvalidated(object? sender, EventArgs e)
    {
        _box?.Presenter?.RebuildLayout();
        _box?.Mirror?.InvalidateVisual();
    }

    /// <summary>The pointer is over <paramref name="point"/> (editor coordinates; null: it left): the renderer's cursor over its tokens.</summary>
    internal void UpdateTokenCursor(Point? point)
    {
        if (_box is not { } box)
        {
            return;
        }
        var cursor = TokenRenderer?.Cursor;
        if (cursor is not null && point is { } at && HitTestToken(at) is { } token && box.Presenter?.IsInline(token) == true)
        {
            if (!ReferenceEquals(box.Cursor, cursor))
            {
                box.Cursor = cursor;
            }
            _tokenCursor = true;
        }
        else if (_tokenCursor)
        {
            // Back to the theme's text cursor.
            _tokenCursor = false;
            box.ClearValue(CursorProperty);
        }
    }

    /// <summary>The editor's accessible name, help text and item status belong to the TextBox that has the focus.</summary>
    private void UpdateAutomation()
    {
        if (_box is not { } box)
        {
            return;
        }
        Forward(box, AutomationProperties.NameProperty, AutomationProperties.GetName(this));
        Forward(box, AutomationProperties.HelpTextProperty, AutomationProperties.GetHelpText(this));
        Forward(box, AutomationProperties.ItemStatusProperty, AutomationProperties.GetItemStatus(this));
    }

    private static void Forward(Control target, AvaloniaProperty<string?> property, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            target.ClearValue(property);
        }
        else
        {
            target.SetValue(property, value);
        }
    }

    internal void RequestCollapse() => RaiseEvent(new RoutedEventArgs(CollapseRequestedEvent));

    #region Token colors

    /// <summary>The style for <paramref name="kind"/>, if any.</summary>
    private TokenStyle? StyleFor(string kind)
    {
        foreach (var style in TokenStyles)
        {
            if (string.Equals(style.Kind, kind, StringComparison.Ordinal))
            {
                return style;
            }
        }
        return null;
    }

    internal IBrush? ResolveTokenFill(string kind) => StyleFor(kind)?.Fill ?? TokenFill ?? _defaultFill;

    internal IBrush? ResolveTokenForeground(string kind) => StyleFor(kind)?.Foreground ?? TokenForeground ?? Foreground;

    private void InvalidateTokenBrushes()
    {
        _brushVersion++;
        _box?.Mirror?.InvalidateVisual();
    }

    /// <summary>Follows property changes of the triggers and styles: a trigger change re-derives the query, a style repaints.</summary>
    private void WatchItems()
    {
        foreach (var item in _watched)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }
        _watched.Clear();
        foreach (var trigger in Triggers)
        {
            _watched.Add(trigger);
        }
        foreach (var style in TokenStyles)
        {
            _watched.Add(style);
        }
        foreach (var item in _watched)
        {
            item.PropertyChanged += OnItemPropertyChanged;
        }
        if (!_refreshing)
        {
            ScheduleRefresh();
        }
    }

    private void OnItemPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is TokenStyle)
        {
            InvalidateTokenBrushes();
        }
        else if (!_refreshing)
        {
            ScheduleRefresh();
        }
    }

    #endregion

    #region Autosize

    /// <summary>
    /// The height follows the text between <see cref="MinLines"/> and <see cref="MaxLines"/> lines plus the padding; the
    /// first measure jumps, later changes run <see cref="HeightMotion"/>. Driven from code with a
    /// <see cref="MotionValue{T}"/> because the target comes from the measured text layout (a theme transition would
    /// need a local <c>Height</c>, which belongs to the user).
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        _measuring = true;
        Size size;
        try
        {
            size = base.MeasureOverride(availableSize);
            UpdateHeight();
            if (_heightPlaced)
            {
                // Measure again within the shown height: the TextBox's scroller only scrolls under a finite height (the
                // text itself is laid out with an unbounded height inside the scroller, so it is not rebuilt).
                var height = Math.Max(0, _shownHeight);
                var width = 0.0;
                foreach (var child in VisualChildren)
                {
                    if (child is Layoutable layoutable)
                    {
                        layoutable.Measure(new Size(availableSize.Width, height));
                        width = Math.Max(width, layoutable.DesiredSize.Width);
                    }
                }
                size = new Size(width, height);
            }
        }
        finally
        {
            _measuring = false;
        }
        return size;
    }

    /// <summary>The TextBox laid its text out again (typing, wrapping, a new width).</summary>
    internal void OnBoxLayoutChanged()
    {
        UpdateHeight();
        Host?.OnEditorLayoutChanged();
        PostGeometryCheck();
    }

    internal void OnBoxScrolled()
    {
        Host?.OnEditorScrolled();
        PostGeometryCheck();
    }

    private void UpdateHeight()
    {
        if (_box?.Presenter is not { } presenter)
        {
            return;
        }
        var padding = _box.Padding;
        var line = LineHeightOf(_box);
        var pad = padding.Top + padding.Bottom;
        var text = presenter.TextLayout.Height;
        if (string.IsNullOrEmpty(presenter.Text) && _box.PlaceholderBlock is { IsVisible: true } placeholder)
        {
            // Arc measures the placeholder in its mirror: a hint that wraps in a narrow box shows whole.
            text = Math.Max(text, placeholder.DesiredSize.Height);
        }
        var min = line * MinLines + pad;
        var max = line * Math.Max(MinLines, MaxLines) + pad;
        var target = Math.Clamp(text + pad, min, Math.Max(min, max));
        if (_heightPlaced && Math.Abs(target - _heightTarget) <= 0.5)
        {
            return;
        }
        _heightTarget = target;
        _height ??= CreateHeightMotion();
        if (!_heightPlaced)
        {
            _heightPlaced = true;
            _height.Jump(target);
            return;
        }
        if (HeightMotion is { } motion && ArcMotion.IsFull(this))
        {
            _ = _height.AnimateTo(target, motion);
        }
        else
        {
            _height.Jump(target);
        }
    }

    private static double LineHeightOf(TextBox box) =>
        double.IsNaN(box.LineHeight) || box.LineHeight <= 0 ? Math.Ceiling(box.FontSize * 1.5) : box.LineHeight;

    private MotionValue<double> CreateHeightMotion()
    {
        var motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        motion.Changed += value =>
        {
            _shownHeight = value;
            if (!_measuring)
            {
                InvalidateMeasure();
            }
        };
        return motion;
    }

    #endregion

    #region Text and tokens

    private int SelectionLow() => _box is null ? _text.Length : Math.Min(_box.SelectionStart, _box.SelectionEnd);

    private int SelectionHigh() => _box is null ? _text.Length : Math.Max(_box.SelectionStart, _box.SelectionEnd);

    /// <summary>Remembers the selection before an operation: its low end (the edit hint) and its active end.</summary>
    private void RememberCaret()
    {
        _lastCaret = SelectionLow();
        _lastActive = _box?.SelectionEnd ?? _text.Length;
    }

    /// <summary>Stores a new text and tokens, updates the presenter and raises <see cref="TextChanged"/>.</summary>
    private void Commit(string text, IReadOnlyList<TokenSpan> tokens, bool setTextProperty)
    {
        if (TokenParser is { } parser)
        {
            var parsed = TokenTextEngine.Normalize(text, parser(text));
            if (!setTextProperty && string.Equals(text, _text, StringComparison.Ordinal) && SameTokens(parsed, _tokens))
            {
                return;
            }
            tokens = parsed;
        }
        if (tokens is List<TokenSpan> list)
        {
            // Handed out through Tokens and events: nobody may change it afterwards.
            tokens = list.AsReadOnly();
        }
        var oldText = _text;
        var oldTokens = _tokens;
        _text = text;
        if (setTextProperty)
        {
            _settingText = true;
            try
            {
                SetCurrentValue(TextProperty, text);
            }
            finally
            {
                _settingText = false;
            }
        }
        Tokens = tokens;
        _box?.SetTokens(tokens);
        Remember(text, tokens);
        RaiseEvent(new TokenTextChangedEventArgs(oldText, oldTokens, text, tokens));
    }

    /// <summary>Keeps the tokens of recent texts so undo and redo can bring them back.</summary>
    private void Remember(string text, IReadOnlyList<TokenSpan> tokens)
    {
        if (!RestoresTokensOnUndo)
        {
            return;
        }
        if (!_history.ContainsKey(text))
        {
            _historyOrder.Enqueue(text);
            if (_historyOrder.Count > HistoryLimit)
            {
                _history.Remove(_historyOrder.Dequeue());
            }
        }
        var copy = new TokenSpan[tokens.Count];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = tokens[i];
        }
        _history[text] = copy;
    }

    /// <summary>The TextBox's text changed: move the tokens with the edit, add the pending one and commit.</summary>
    internal void OnBoxTextChanged()
    {
        if (_syncingBox || _box is null)
        {
            return;
        }
        var text = _box.Text ?? string.Empty;
        if (string.Equals(text, _text, StringComparison.Ordinal))
        {
            return;
        }
        var tokens = _restoreDepth > 0 && RestoresTokensOnUndo && _history.TryGetValue(text, out var remembered)
            ? new List<TokenSpan>(remembered)
            : TokenTextEngine.Reconcile(_text, _tokens, text, _lastCaret);
        var added = TakePending(text, tokens);
        Commit(text, tokens, setTextProperty: true);
        if (added is not null)
        {
            _inserted = added;
            RaiseEvent(new TokenInsertedEventArgs(added));
        }
        if (_operationDepth == 0)
        {
            ScheduleRefresh();
        }
    }

    /// <summary>Adds the token an insert method is typing when the new text holds it (sorted in).</summary>
    private TokenSpan? TakePending(string text, List<TokenSpan> tokens)
    {
        var added = _pending;
        var expected = _pendingText;
        _pending = null;
        _pendingText = null;
        if (added is null || expected is null || !TokenTextEngine.Holds(text, added, expected))
        {
            return null;
        }
        var index = 0;
        while (index < tokens.Count && tokens[index].Start < added.Start)
        {
            index++;
        }
        tokens.Insert(index, added);
        return added;
    }

    /// <summary>A programmatic <see cref="Text"/>: tokens the edit did not touch stay.</summary>
    private void SetTextFromProperty(string text)
    {
        if (string.Equals(text, _text, StringComparison.Ordinal))
        {
            return;
        }
        var tokens = TokenTextEngine.Reconcile(_text, _tokens, text, int.MaxValue);
        Commit(text, tokens, setTextProperty: false);
        SyncBoxText(moveCaretToEnd: true);
        Refresh();
    }

    /// <summary>
    /// Replaces the text and tokens. <paramref name="refresh"/> false leaves the trigger to the caller (MentionInput
    /// refreshes after raising its own change event).
    /// </summary>
    internal void SetContent(string text, IEnumerable<TokenSpan> tokens, bool refresh)
    {
        var list = TokenTextEngine.Normalize(text, tokens);
        if (string.Equals(text, _text, StringComparison.Ordinal) && (TokenParser is not null || SameTokens(list, _tokens)))
        {
            if (refresh)
            {
                Refresh();
            }
            return;
        }
        Commit(text, list, setTextProperty: true);
        SyncBoxText(moveCaretToEnd: true);
        if (refresh)
        {
            Refresh();
        }
    }

    private static bool SameTokens(IReadOnlyList<TokenSpan> a, IReadOnlyList<TokenSpan> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (var i = 0; i < a.Count; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }
        return true;
    }

    private void SyncBoxText(bool moveCaretToEnd)
    {
        if (_box is not { } box || string.Equals(box.Text ?? string.Empty, _text, StringComparison.Ordinal))
        {
            return;
        }
        _syncingBox = true;
        try
        {
            box.Text = _text;
            if (moveCaretToEnd)
            {
                // A browser leaves the caret at the end of a programmatically set textarea value.
                box.Select(_text.Length, _text.Length);
            }
        }
        finally
        {
            _syncingBox = false;
        }
        RememberCaret();
    }

    private TokenSpan? InsertTokenText(TokenSpan token, string tokenText, int from, int to, int prefix = 0)
    {
        _pending = token;
        _pendingText = tokenText[prefix..];
        _inserted = null;
        var after = to < _text.Length ? _text[to] : (char?)null;
        TypeText(after is { } c && char.IsWhiteSpace(c) ? tokenText : tokenText + " ", from, to);
        var inserted = _inserted;
        _inserted = null;
        return inserted;
    }

    /// <summary>
    /// Types <paramref name="text"/> over <paramref name="from"/>..<paramref name="to"/> (default the selection) through the
    /// TextBox, so the change joins its undo stack; falls back to a direct edit when the TextBox cannot take it.
    /// </summary>
    internal void TypeText(string text, int? from, int? to)
    {
        if (_box is { } box && box.IsEffectivelyEnabled && !box.IsReadOnly)
        {
            if (!box.IsFocused)
            {
                box.Focus();
            }
            _operationDepth++;
            try
            {
                if (from is { } start)
                {
                    box.Select(start, to ?? start);
                }
                RememberCaret();
                var before = box.Text ?? string.Empty;
                box.TypeText(text);
                if (!string.Equals(before, box.Text ?? string.Empty, StringComparison.Ordinal) || text.Length == 0)
                {
                    return;
                }
            }
            finally
            {
                EndOperation();
            }
        }
        // No usable TextBox (not templated yet, disabled or read-only): edit the text directly.
        var current = _text;
        var low = Math.Clamp(from ?? (_box is null ? current.Length : SelectionLow()), 0, current.Length);
        var high = Math.Clamp(to ?? (from is null && _box is not null ? SelectionHigh() : low), low, current.Length);
        var nextText = string.Concat(current.AsSpan(0, low), text, current.AsSpan(high));
        var tokens = TokenTextEngine.Reconcile(current, _tokens, nextText, low);
        var added = TakePending(nextText, tokens);
        _lastCaret = low;
        Commit(nextText, tokens, setTextProperty: true);
        SyncBoxText(moveCaretToEnd: false);
        _box?.Select(low + text.Length, low + text.Length);
        if (added is not null)
        {
            _inserted = added;
            RaiseEvent(new TokenInsertedEventArgs(added));
        }
        Refresh();
    }

    private void RunRestoring(Action<TextBox> action)
    {
        if (_box is not { } box)
        {
            return;
        }
        BeginOperation(restoring: true);
        try
        {
            action(box);
        }
        finally
        {
            EndOperation(restoring: true);
        }
    }

    #endregion

    #region Editor events

    /// <summary>Before a key, text input or pointer operation: remember the caret (the edit hint, Arc <c>lastCaret</c>).</summary>
    internal void BeginOperation(bool pointer = false, bool restoring = false)
    {
        _operationDepth++;
        if (pointer)
        {
            _pointerOperation = true;
        }
        if (restoring)
        {
            _restoreDepth++;
        }
        Host?.OnEditorOperationStarting();
        if (!pointer)
        {
            RememberCaret();
        }
    }

    /// <summary>After the TextBox finished: keep the caret out of tokens (Arc <c>onSelect</c>) and refresh the query.</summary>
    internal void EndOperation(bool restoring = false)
    {
        if (restoring)
        {
            _restoreDepth = Math.Max(0, _restoreDepth - 1);
        }
        _operationDepth = Math.Max(0, _operationDepth - 1);
        if (_operationDepth > 0)
        {
            return;
        }
        var pointer = _pointerOperation;
        _pointerOperation = false;
        NormalizeSelection(pointer);
        Refresh();
    }

    internal void OnBoxSelectionChanged()
    {
        if (_operationDepth == 0 && !_normalizing && !_syncingBox)
        {
            ScheduleRefresh();
        }
    }

    internal void OnBoxFocusChanged(bool focused)
    {
        Host?.OnEditorFocusChanged(focused);
        if (_operationDepth == 0)
        {
            Refresh();
        }
    }

    /// <summary>
    /// Arc <c>onSelect</c>: the caret never rests inside a token. One arrow step into a token hops over it; any other
    /// landing (a click, a word jump) snaps to the nearer edge. Selections grow to whole tokens only with
    /// <see cref="ExpandSelectionToTokens"/>. Inline tokens (drawn by the <see cref="TokenRenderer"/>) are stricter: a
    /// keyboard move into one always crosses it in its direction (a word jump never sticks before a chip), and no
    /// selection end stays inside one. <paramref name="programmatic"/>: the change came from code, not from a key, so
    /// there is no direction and an inline token snaps to its nearer edge.
    /// </summary>
    private void NormalizeSelection(bool pointer, bool programmatic = false)
    {
        if (_box is not { } box || _normalizing)
        {
            return;
        }
        var anchor = box.SelectionStart;
        var activeEnd = box.SelectionEnd;
        var low = Math.Min(anchor, activeEnd);
        var high = Math.Max(anchor, activeEnd);
        var collapsed = low == high;
        var start = low;
        var end = high;
        var expand = ExpandSelectionToTokens;
        var presenter = box.Presenter;
        var hasInline = TokenRenderer is not null && presenter is not null && presenter.InlineTokens.Count > 0;
        // The active end moved by the keyboard: it crosses an inline token in the direction it travelled.
        var keyboard = !pointer && !programmatic;
        var activeMoved = keyboard && activeEnd != _lastActive;
        foreach (var token in _tokens)
        {
            var inline = hasInline && presenter!.IsInline(token);
            if (collapsed && start > token.Start && start < token.End)
            {
                var stepped = inline
                    ? keyboard && start != _lastCaret
                    : !pointer && Math.Abs(start - _lastCaret) == 1;
                var forward = start > _lastCaret;
                start = end = stepped
                    ? forward ? token.End : token.Start
                    : start - token.Start < token.End - start ? token.Start : token.End;
            }
            else if (!collapsed && inline)
            {
                // An inline token is never half selected: the moved active end crosses it, any other end grows over it.
                var lowIsActive = activeEnd < anchor;
                if (start > token.Start && start < token.End)
                {
                    start = lowIsActive && activeMoved && activeEnd > _lastActive ? token.End : token.Start;
                }
                if (end > token.Start && end < token.End)
                {
                    end = !lowIsActive && activeMoved && activeEnd < _lastActive ? token.Start : token.End;
                }
            }
            else if (!collapsed && expand)
            {
                if (start > token.Start && start < token.End)
                {
                    start = token.Start;
                }
                if (end > token.Start && end < token.End)
                {
                    end = token.End;
                }
            }
        }
        if (start != low || end != high)
        {
            _normalizing = true;
            try
            {
                if (collapsed)
                {
                    box.Select(start, start);
                }
                else if (anchor <= activeEnd)
                {
                    box.Select(start, end);
                }
                else
                {
                    box.Select(end, start);
                }
            }
            finally
            {
                _normalizing = false;
            }
        }
        RememberCaret();
    }

    /// <summary>Tunnel: a refresh still queued runs first, so a list owner handling the key sees the current query.</summary>
    private void OnTunnelKeyDown(KeyEventArgs e)
    {
        if (_box is not { } box || box.IsComposing)
        {
            return;
        }
        if (_refreshPosted)
        {
            Refresh();
        }
        RememberCaret();
    }

    /// <summary>
    /// Arc <c>onKeyDown</c> after the owner's list keys: Backspace / Delete next to a token select it whole (the TextBox
    /// then deletes it in one undoable step); Enter asks to submit. Returns true when the key was consumed.
    /// </summary>
    internal bool HandleBoxKeyDown(KeyEventArgs e)
    {
        if (_box is not { } box || box.IsComposing)
        {
            return false;
        }
        RememberCaret();
        var low = SelectionLow();
        var high = SelectionHigh();
        if (low == high && !e.KeyModifiers.HasFlag(KeyModifiers.Meta) && e.Key is Key.Back or Key.Delete)
        {
            foreach (var token in _tokens)
            {
                var hit = e.Key == Key.Back
                    ? low > token.Start && low <= token.End
                    : low >= token.Start && low < token.End;
                if (hit)
                {
                    _operationDepth++;
                    try
                    {
                        box.Select(token.Start, token.End);
                    }
                    finally
                    {
                        _operationDepth--;
                    }
                    break;
                }
            }
            return false;
        }
        if (e.Key == Key.Enter && SubmitOnEnter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && !IsReadOnly)
        {
            e.Handled = true;
            RaiseEvent(new TokenTextSubmitRequestedEventArgs(_text, _tokens));
            return true;
        }
        return false;
    }

    #endregion

    #region Trigger

    /// <summary>Runs <see cref="Refresh"/> once the current input has settled (programmatic caret moves, text changes).</summary>
    internal void ScheduleRefresh()
    {
        if (_refreshPosted)
        {
            return;
        }
        _refreshPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            if (_refreshPosted)
            {
                if (_operationDepth == 0)
                {
                    NormalizeSelection(pointer: false, programmatic: true);
                }
                Refresh();
            }
        });
    }

    /// <summary>Derives the open query from the text and the caret (Arc's render) and reports a change.</summary>
    internal void Refresh()
    {
        _refreshPosted = false;
        _refreshing = true;
        try
        {
            Host?.OnTriggerRefreshing();
            var box = _box;
            var focused = box is { IsFocused: true };
            int? caret = focused && box!.SelectionStart == box.SelectionEnd ? box.CaretIndex : null;
            var match = caret is { } at ? TokenTextEngine.FindTrigger(_text, at, _tokens, Triggers) : null;
            if (_dismissed is { } dismissed && match?.Start != dismissed)
            {
                _dismissed = null;
            }
            if (match is { } m && m.Start == _dismissed)
            {
                match = null;
            }
            _match = match;
            Publish(match, caret ?? 0);
        }
        finally
        {
            _refreshing = false;
        }
        Host?.OnTriggerRefreshed();
    }

    /// <summary>Raises <see cref="TriggerChanged"/> when the query or its geometry differ from the last report.</summary>
    private void Publish(TokenTriggerMatch? match, int caret)
    {
        TokenTriggerState? state = null;
        if (match is { } m)
        {
            state = new TokenTriggerState(m.Trigger, m.Query, m.Start, CharacterRect(m.Start, this), CharacterRect(caret, this));
        }
        var previous = _triggerState;
        if (Equals(previous, state))
        {
            return;
        }
        TriggerState = state;
        RaiseEvent(new TokenTriggerChangedEventArgs(previous, state));
    }

    /// <summary>After layout or scrolling, the query's rectangles may have moved: report them once things settle.</summary>
    private void PostGeometryCheck()
    {
        if (_match is null || _geometryPosted)
        {
            return;
        }
        _geometryPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _geometryPosted = false;
            if (_match is { } match && !_refreshPosted && _box is { } box)
            {
                Publish(match, box.CaretIndex);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// The line box at character <paramref name="index"/> (its left edge, the line's top and height) in
    /// <paramref name="relativeTo"/>'s coordinates, scrolling included.
    /// </summary>
    internal Rect CharacterRect(int index, Visual relativeTo)
    {
        if (_box?.Presenter is not { } presenter)
        {
            return default;
        }
        var rect = presenter.TextLayout.HitTestTextPosition(Math.Clamp(index, 0, (presenter.Text ?? string.Empty).Length));
        var origin = presenter.TranslatePoint(rect.Position, relativeTo) ?? rect.Position;
        var height = rect.Height > 0 ? rect.Height : LineHeightOf(_box);
        return new Rect(origin.X, origin.Y, Math.Max(1, rect.Width), height);
    }

    #endregion

    /// <summary>The editor itself is a frame around its TextBox, which carries the name and the value.</summary>
    private sealed class TokenTextEditorAutomationPeer : ControlAutomationPeer
    {
        public TokenTextEditorAutomationPeer(TokenTextEditor owner)
            : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsContentElementCore() => false;

        protected override bool IsControlElementCore() => false;
    }
}
