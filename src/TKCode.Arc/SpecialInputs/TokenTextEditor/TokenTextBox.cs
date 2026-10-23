using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>
/// The editor inside <see cref="TokenTextEditor"/>: a real multi-line <see cref="TextBox"/> (caret, selection, IME, undo,
/// clipboard) whose own text is transparent. Every key, text input and pointer operation is bracketed so the editor can
/// keep the caret out of tokens and refresh its trigger once the TextBox has finished its own work.
/// </summary>
internal sealed class TokenTextBox : TextBox
{
    internal static readonly StyledProperty<IBrush?> AccentSourceProperty =
        AvaloniaProperty.Register<TokenTextBox, IBrush?>("AccentSource");

    /// <summary>The selection is the accent at this opacity (Arc 24%).</summary>
    internal static readonly StyledProperty<double> SelectionOpacityProperty =
        AvaloniaProperty.Register<TokenTextBox, double>("SelectionOpacity", 0.24);

    /// <summary>Name of the panel that stacks the placeholder, the mirror and the presenter.</summary>
    internal const string TextLayersPart = "PART_TextLayers";

    private TokenTextMirror? _mirror;

    static TokenTextBox()
    {
        // Arc's textarea: multi-line, wrapping, "\n" line breaks on every platform.
        AcceptsReturnProperty.OverrideDefaultValue<TokenTextBox>(true);
        TextWrappingProperty.OverrideDefaultValue<TokenTextBox>(TextWrapping.Wrap);
        NewLineProperty.OverrideDefaultValue<TokenTextBox>("\n");
    }

    public TokenTextBox()
    {
        ArcTokens.Bind(this, AccentSourceProperty, DesignTokens.ArcDesignTokenKind.Accent);
    }

    /// <summary>The editor this TextBox belongs to.</summary>
    internal TokenTextEditor? Owner { get; set; }

    internal TokenTextPresenter? Presenter { get; private set; }

    internal ScrollViewer? Scroller { get; private set; }

    /// <summary>The placeholder; while the text is empty the editor is at least as tall as it (a wrapped hint shows whole).</summary>
    internal TextBlock? PlaceholderBlock { get; private set; }

    /// <summary>The mirror that paints the text and the tokens behind the transparent text.</summary>
    internal TokenTextMirror? Mirror => _mirror;

    /// <summary>True while an IME composition is active (Arc <c>isComposing</c>).</summary>
    internal bool IsComposing => !string.IsNullOrEmpty(Presenter?.PreeditText);

    internal double SelectionOpacity
    {
        get => GetValue(SelectionOpacityProperty);
        set => SetValue(SelectionOpacityProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(TokenTextBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (Scroller is not null)
        {
            Scroller.ScrollChanged -= OnScrollChanged;
        }
        Presenter = e.NameScope.Find<TokenTextPresenter>("PART_TextPresenter");
        Scroller = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        PlaceholderBlock = e.NameScope.Find<TextBlock>("PART_Placeholder");
        _mirror = null;
        if (e.NameScope.Find<Panel>(TextLayersPart) is { } layers && Presenter is not null)
        {
            // Created here rather than in the template so an owner can supply its own mirror type (MentionInput).
            _mirror = Owner?.MirrorFactory?.Invoke() ?? new TokenTextMirror();
            _mirror.Editor = Owner;
            _mirror.Presenter = Presenter;
            var index = layers.Children.IndexOf(Presenter);
            layers.Children.Insert(index < 0 ? layers.Children.Count : index, _mirror);
        }
        if (Presenter is not null)
        {
            Presenter.Box = this;
            Presenter.Mirror = _mirror;
            Presenter.LayoutChanged = () => Owner?.OnBoxLayoutChanged();
        }
        if (Scroller is not null)
        {
            Scroller.ScrollChanged += OnScrollChanged;
        }
        Owner?.OnBoxTemplateApplied();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => Owner?.OnBoxScrolled();

    /// <summary>Sets the token ranges drawn by the mirror and used to split the presenter's runs identically.</summary>
    internal void SetTokens(IReadOnlyList<TokenSpan> tokens)
    {
        Presenter?.SetTokens(tokens);
        _mirror?.InvalidateVisual();
    }

    /// <summary>Types <paramref name="text"/> over the selection as if from the keyboard, so it joins the undo stack.</summary>
    internal void TypeText(string text) => base.OnTextInput(new TextInputEventArgs { RoutedEvent = TextInputEvent, Text = text, Source = this });

    /// <summary>Selects from <paramref name="anchor"/> to <paramref name="active"/> (equal values place the caret).</summary>
    internal void Select(int anchor, int active)
    {
        var length = Text?.Length ?? 0;
        anchor = Math.Clamp(anchor, 0, length);
        active = Math.Clamp(active, 0, length);
        if (anchor == active)
        {
            SetCurrentValue(CaretIndexProperty, anchor);
            SetCurrentValue(SelectionStartProperty, anchor);
            SetCurrentValue(SelectionEndProperty, anchor);
            return;
        }
        SetCurrentValue(SelectionStartProperty, anchor);
        SetCurrentValue(SelectionEndProperty, active);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var owner = Owner;
        if (owner is not null && owner.HandleBoxKeyDown(e))
        {
            return;
        }
        var restoring = owner is not null && IsUndoOrRedo(e);
        owner?.BeginOperation(restoring: restoring);
        try
        {
            base.OnKeyDown(e);
        }
        finally
        {
            owner?.EndOperation(restoring: restoring);
        }
    }

    /// <summary>The platform's undo and redo shortcuts, the same ones the TextBox itself reacts to.</summary>
    private bool IsUndoOrRedo(KeyEventArgs e)
    {
        var keymap = Application.Current?.PlatformSettings?.HotkeyConfiguration;
        if (keymap is null)
        {
            return false;
        }
        foreach (var gesture in keymap.Undo)
        {
            if (gesture.Matches(e))
            {
                return true;
            }
        }
        foreach (var gesture in keymap.Redo)
        {
            if (gesture.Matches(e))
            {
                return true;
            }
        }
        return false;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        Owner?.BeginOperation();
        try
        {
            base.OnTextInput(e);
        }
        finally
        {
            Owner?.EndOperation();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Owner?.BeginOperation(pointer: true);
        try
        {
            base.OnPointerPressed(e);
        }
        finally
        {
            Owner?.EndOperation();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        Owner?.BeginOperation(pointer: true);
        try
        {
            base.OnPointerReleased(e);
        }
        finally
        {
            Owner?.EndOperation();
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Owner?.UpdateTokenCursor(e.GetPosition(Owner));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Owner?.UpdateTokenCursor(null);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnBoxFocusChanged(true);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.OnBoxFocusChanged(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Owner?.OnBoxTextChanged();
        }
        else if (change.Property == CaretIndexProperty || change.Property == SelectionStartProperty ||
                 change.Property == SelectionEndProperty)
        {
            Owner?.OnBoxSelectionChanged();
        }
        else if (change.Property == AccentSourceProperty || change.Property == SelectionOpacityProperty)
        {
            // Arc: ::selection is color-mix(in oklab, var(--accent) 24%, transparent).
            var accent = GetValue(AccentSourceProperty) is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
            SelectionBrush = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, SelectionOpacity));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TokenTextBoxAutomationPeer(this);
}

/// <summary>
/// The TextBox presenter. Its runs are split at the token boundaries (with a transparent brush, like the rest of the
/// text) so its shaping, wrapping and caret positions are identical to the mirror's colored layout.
/// </summary>
internal sealed class TokenTextPresenter : TextPresenter
{
    private IReadOnlyList<TokenSpan> _tokens = Array.Empty<TokenSpan>();
    private readonly List<TokenSpan> _inlineTokens = new();

    /// <summary>The TextBox this presenter belongs to (its owner supplies the token renderer).</summary>
    internal TokenTextBox? Box { get; set; }

    internal TokenTextMirror? Mirror { get; set; }

    /// <summary>Raised after a measure or arrange that may have changed the text layout (the editor autosizes on it).</summary>
    internal Action? LayoutChanged { get; set; }

    internal IReadOnlyList<TokenSpan> Tokens => _tokens;

    /// <summary>The tokens the last layout drew as inline objects (a <see cref="TokenRenderer"/> measured them).</summary>
    internal IReadOnlyList<TokenSpan> InlineTokens
    {
        get
        {
            // Building the layout records them.
            _ = TextLayout;
            return _inlineTokens;
        }
    }

    /// <summary>Whether <paramref name="token"/> is laid out as one inline object.</summary>
    internal bool IsInline(TokenSpan token)
    {
        foreach (var inline in InlineTokens)
        {
            if (inline.Equals(token))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Lays the text out again (the renderer's sizes changed, or it was replaced).</summary>
    internal void RebuildLayout() => InvalidateTextLayout();

    protected override Type StyleKeyOverride => typeof(TextPresenter);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        LayoutChanged?.Invoke();
        return size;
    }

    internal void SetTokens(IReadOnlyList<TokenSpan> tokens)
    {
        if (ReferenceEquals(tokens, _tokens) || (tokens.Count == 0 && _tokens.Count == 0))
        {
            return;
        }
        _tokens = tokens;
        InvalidateTextLayout();
    }

    protected override TextLayout CreateTextLayout()
    {
        _inlineTokens.Clear();
        var layout = base.CreateTextLayout();
        if (_tokens.Count == 0 || (PasswordChar != '\0' && !RevealPassword))
        {
            return layout;
        }
        var maxWidth = layout.MaxWidth;
        var maxHeight = layout.MaxHeight;
        layout.Dispose();
        return BuildLayout(maxWidth, maxHeight, null);
    }

    protected override void InvalidateTextLayout()
    {
        base.InvalidateTextLayout();
        Mirror?.InvalidateVisual();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        // A new width rebuilt the layout during arrange; the mirror follows it.
        Mirror?.InvalidateVisual();
        LayoutChanged?.Invoke();
        return size;
    }

    /// <summary>The text the layout shows: the text with the IME preedit at the caret.</summary>
    internal string CombinedText(out int preeditStart, out int preeditLength)
    {
        var text = Text ?? string.Empty;
        var preedit = PreeditText;
        if (string.IsNullOrEmpty(preedit))
        {
            preeditStart = -1;
            preeditLength = 0;
            return text;
        }
        var caret = Math.Clamp(CaretIndex, 0, text.Length);
        preeditStart = caret;
        preeditLength = preedit.Length;
        return string.Concat(text.AsSpan(0, caret), preedit, text.AsSpan(caret));
    }

    /// <summary>
    /// Builds the layout with one run per token (and one for the preedit). <paramref name="colors"/> null builds the
    /// editor's own transparent layout; otherwise the mirror's colored copy with exactly the same runs.
    /// </summary>
    internal TextLayout BuildLayout(double maxWidth, double maxHeight, TokenTextMirror? colors)
    {
        var text = CombinedText(out var preeditStart, out var preeditLength);
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var foreground = colors is null ? Foreground : colors.TextBrush;
        var spans = new List<ValueSpan<TextRunProperties>>(_tokens.Count + 1);
        var preeditEnd = preeditStart + preeditLength;
        var preeditAdded = preeditLength == 0;
        var renderer = Box?.Owner?.TokenRenderer;
        List<(int Start, TokenInlineRun Run)>? inlines = null;
        GenericTextRunProperties? defaults = null;
        var context = renderer is not null && Box?.Owner is { } editor ? RenderContext(editor, typeface) : default;
        foreach (var token in _tokens)
        {
            var start = token.Start;
            if (preeditLength > 0 && start >= preeditStart)
            {
                start += preeditLength;
            }
            var end = start + token.Length;
            if (start < 0 || end > text.Length || token.Length <= 0)
            {
                continue;
            }
            if (preeditLength > 0 && start < preeditEnd && end > preeditStart)
            {
                continue;
            }
            if (!preeditAdded && preeditStart < start)
            {
                spans.Add(PreeditSpan(typeface, foreground, preeditStart, preeditLength));
                preeditAdded = true;
            }
            if (renderer is not null && renderer.Measure(token, context) is { } size)
            {
                // Laid out as one object; the mirror's copy asks the renderer to paint it.
                defaults ??= new GenericTextRunProperties(typeface, FontSize, null, foreground, null, BaselineAlignment.Baseline, null, FontFeatures);
                inlines ??= new List<(int, TokenInlineRun)>();
                if (end == text.Length && token.Length > 1)
                {
                    // Avalonia places a caret after a drawable run that ends the text at the run's start; a zero-width
                    // tail for the last character gives the end of the text its own run (the editor never leaves the
                    // caret inside a token, so the extra stop is never seen).
                    inlines.Add((start, new TokenInlineRun(token, token.Length - 1, size, defaults, colors)));
                    inlines.Add((end - 1, new TokenInlineRun(token, 1, new TokenInlineSize(0, size.Ascent, size.Descent), defaults, null)));
                }
                else
                {
                    inlines.Add((start, new TokenInlineRun(token, token.Length, size, defaults, colors)));
                }
                if (colors is null)
                {
                    _inlineTokens.Add(token);
                }
                continue;
            }
            var brush = colors is null ? Foreground : colors.ResolveForeground(token.Kind);
            spans.Add(new ValueSpan<TextRunProperties>(start, token.Length,
                new GenericTextRunProperties(typeface, FontSize, null, brush, null, BaselineAlignment.Baseline, null, FontFeatures)));
        }
        if (!preeditAdded)
        {
            spans.Add(PreeditSpan(typeface, foreground, preeditStart, preeditLength));
        }
        if (inlines is not null && defaults is not null)
        {
            if (colors is not null)
            {
                colors.RenderContext = context;
            }
            var paragraph = new GenericTextParagraphProperties(FlowDirection, TextAlignment, true, false, defaults, TextWrapping,
                LineHeight, 0, LetterSpacing);
            return new TextLayout(new TokenInlineTextSource(text, defaults, spans, inlines), paragraph, null, maxWidth, maxHeight, 0);
        }
        return new TextLayout(text, typeface, FontSize, foreground, TextAlignment, TextWrapping, null, null, FlowDirection,
            maxWidth, maxHeight, LineHeight, LetterSpacing, 0, FontFeatures, spans.Count == 0 ? null : spans);
    }

    /// <summary>The metrics a <see cref="TokenRenderer"/> measures and paints with.</summary>
    private TokenRenderContext RenderContext(TokenTextEditor editor, Typeface typeface)
    {
        var (ascent, descent) = TokenTextMirror.ContentMetrics(this);
        return new TokenRenderContext(editor, typeface, FontSize, LetterSpacing, editor.Foreground, ascent, descent);
    }

    private ValueSpan<TextRunProperties> PreeditSpan(Typeface typeface, IBrush? foreground, int start, int length) =>
        new(start, length, new GenericTextRunProperties(typeface, FontSize, TextDecorations.Underline, foreground, null,
            BaselineAlignment.Baseline, null, FontFeatures));
}

/// <summary>One painted piece of a token: a line segment's box, its corners and its fill.</summary>
internal readonly record struct TokenBox(Rect Rect, CornerRadius Radius, IBrush? Fill, string Kind);

/// <summary>
/// Arc's backdrop: paints the editor's text with identical metrics behind the transparent TextBox text, with every token
/// tinted around its glyphs (the font's content box, grown by the editor's token padding and outset, so no glyph
/// moves). A token that wraps is painted per line, sliced or cloned. It sits in the same scrolling panel as the
/// presenter, so it follows the TextBox's scroll exactly.
/// </summary>
internal class TokenTextMirror : Control
{
    static TokenTextMirror()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<TokenTextMirror>(false);
        FocusableProperty.OverrideDefaultValue<TokenTextMirror>(false);
    }

    private readonly List<Rect> _segments = new();
    private readonly List<TokenBox> _boxes = new();
    private TextLayout? _layout;
    private TextLayout? _source;
    private int _builtBrushVersion = -1;

    /// <summary>The editor whose colors, token shape and tokens this mirror paints.</summary>
    internal TokenTextEditor? Editor { get; set; }

    internal TokenTextPresenter? Presenter { get; set; }

    /// <summary>The colored layout last drawn (tests).</summary>
    internal TextLayout? Layout => _layout;

    /// <summary>The plain text color (the editor's <c>Foreground</c>).</summary>
    internal IBrush? TextBrush => Editor?.Foreground;

    /// <summary>The fill of <paramref name="kind"/>: its <see cref="TokenStyle"/>, else the editor's token fill.</summary>
    internal IBrush? ResolveFill(string kind) => Editor?.ResolveTokenFill(kind);

    /// <summary>The text color of <paramref name="kind"/>: its <see cref="TokenStyle"/>, else the editor's token foreground.</summary>
    internal IBrush? ResolveForeground(string kind) => Editor?.ResolveTokenForeground(kind);

    /// <summary>The metrics the renderer measured the colored layout's inline tokens with (set while it is built).</summary>
    internal TokenRenderContext RenderContext { get; set; }

    /// <summary>Paints an inline token of the colored layout through the editor's <see cref="TokenRenderer"/>.</summary>
    internal void DrawInlineToken(DrawingContext context, TokenSpan token, Rect bounds, double baseline) =>
        Editor?.TokenRenderer?.Render(context, token, bounds, baseline, RenderContext);

    protected override Size MeasureOverride(Size availableSize) => default;

    /// <summary>Rebuilds the colored layout when the presenter's layout or the brushes changed.</summary>
    internal TextLayout? EnsureLayout()
    {
        var presenter = Presenter;
        if (presenter is null)
        {
            return null;
        }
        var source = presenter.TextLayout;
        var version = Editor?.BrushVersion ?? 0;
        if (!ReferenceEquals(source, _source) || _builtBrushVersion != version || _layout is null)
        {
            _layout?.Dispose();
            _layout = presenter.BuildLayout(source.MaxWidth, source.MaxHeight, this);
            _source = source;
            _builtBrushVersion = version;
        }
        return _layout;
    }

    /// <summary>The boxes painted behind the tokens, in this control's coordinates (tests).</summary>
    internal IReadOnlyList<TokenBox> TokenBoxes()
    {
        var layout = EnsureLayout();
        if (layout is not null)
        {
            CollectBoxes(layout);
        }
        return _boxes.ToArray();
    }

    public override void Render(DrawingContext context)
    {
        var layout = EnsureLayout();
        if (layout is null)
        {
            return;
        }
        CollectBoxes(layout);
        foreach (var box in _boxes)
        {
            if (box.Fill is null)
            {
                continue;
            }
            var r = box.Radius;
            if (r.TopLeft == r.TopRight && r.TopLeft == r.BottomRight && r.TopLeft == r.BottomLeft)
            {
                context.DrawRectangle(box.Fill, null, box.Rect, r.TopLeft, r.TopLeft);
            }
            else
            {
                context.DrawRectangle(box.Fill, null, new RoundedRect(box.Rect, r));
            }
        }
        layout.Draw(context, default);
    }

    /// <summary>Fills <see cref="_boxes"/> with every token's line segments (reused lists, no per-render garbage).</summary>
    private void CollectBoxes(TextLayout layout)
    {
        _boxes.Clear();
        var presenter = Presenter;
        var editor = Editor;
        if (presenter is null || editor is null || presenter.Tokens.Count == 0)
        {
            return;
        }
        presenter.CombinedText(out var preeditStart, out var preeditLength);
        var (ascent, descent) = ContentMetrics(presenter);
        var lines = layout.TextLines;
        var padding = editor.TokenPadding;
        var outset = Math.Max(0, editor.TokenOutset);
        var radius = Math.Max(0, editor.TokenCornerRadius);
        var round = radius > 0 ? radius + outset : 0;
        var slice = editor.TokenDecorationBreak == TokenDecorationBreak.Slice;
        var inlines = presenter.InlineTokens;
        foreach (var token in presenter.Tokens)
        {
            if (inlines.Count > 0 && presenter.IsInline(token))
            {
                // The renderer paints inline tokens itself.
                continue;
            }
            var start = token.Start;
            if (preeditLength > 0 && start >= preeditStart)
            {
                start += preeditLength;
            }
            _segments.Clear();
            foreach (var rect in layout.HitTestTextRange(start, token.Length))
            {
                // Pieces of one line (separate runs, such as a fallback font) become one box.
                if (_segments.Count > 0 && Math.Abs(_segments[^1].Y - rect.Y) < 0.5)
                {
                    _segments[^1] = _segments[^1].Union(rect);
                    continue;
                }
                _segments.Add(rect);
            }
            var fill = ResolveFill(token.Kind);
            for (var i = 0; i < _segments.Count; i++)
            {
                var rect = _segments[i];
                var opens = !slice || i == 0;
                var closes = !slice || i == _segments.Count - 1;
                var left = opens ? padding.Left + outset : 0;
                var right = closes ? padding.Right + outset : 0;
                var top = ContentTop(lines, rect, ascent) - padding.Top - outset;
                var height = ascent + descent + padding.Top + padding.Bottom + 2 * outset;
                var box = new Rect(rect.X - left, top, rect.Width + left + right, height);
                var corners = new CornerRadius(opens ? round : 0, closes ? round : 0, closes ? round : 0, opens ? round : 0);
                _boxes.Add(new TokenBox(box, corners, fill, token.Kind));
            }
        }
    }

    /// <summary>
    /// The font's ascent and descent at the editor's size: in CSS a token's background covers the inline content area
    /// (ascent + descent around the baseline), not the whole 24px line box.
    /// </summary>
    internal static (double Ascent, double Descent) ContentMetrics(TextPresenter presenter)
    {
        var metrics = new Typeface(presenter.FontFamily, presenter.FontStyle, presenter.FontWeight, presenter.FontStretch)
            .GlyphTypeface.Metrics;
        var scale = presenter.FontSize / Math.Max(1, (double)metrics.DesignEmHeight);
        return (-metrics.Ascent * scale, metrics.Descent * scale);
    }

    /// <summary>Top of the content area on the line holding <paramref name="rect"/>.</summary>
    private static double ContentTop(IReadOnlyList<TextLine> lines, Rect rect, double ascent)
    {
        var y = 0.0;
        foreach (var line in lines)
        {
            if (rect.Y < y + line.Height - 0.5)
            {
                return y + line.Baseline - ascent;
            }
            y += line.Height;
        }
        return rect.Y;
    }
}

/// <summary>
/// The TextBox is Arc's combobox textarea: editable text with an expand / collapse state for the owner's list
/// (<see cref="TokenTextEditor.SuggestionsExpanded"/>). Without triggers it is a plain edit box.
/// </summary>
internal sealed class TokenTextBoxAutomationPeer : TextBoxAutomationPeer, IExpandCollapseProvider
{
    public TokenTextBoxAutomationPeer(TokenTextBox owner)
        : base(owner)
    {
    }

    private TokenTextEditor? Editor => ((TokenTextBox)Owner).Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        Editor is { Triggers.Count: > 0 } ? AutomationControlType.ComboBox : AutomationControlType.Edit;

    public ExpandCollapseState ExpandCollapseState =>
        Editor?.SuggestionsExpanded == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => false;

    public void Expand()
    {
    }

    public void Collapse() => Editor?.RequestCollapse();

    internal void RaiseExpandChanged(bool open) => RaisePropertyChangedEvent(
        ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
        open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
        open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}
