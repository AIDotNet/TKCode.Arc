using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace TKCode.Arc;

/// <summary>A token range for the editor's text layout (offsets in the text without the IME preedit).</summary>
internal readonly record struct MentionInputTokenSpan(int Start, int Length, MentionKind Kind);

/// <summary>
/// The editor inside <see cref="MentionInput"/>: a real multi-line <see cref="TextBox"/> (caret, selection, IME, undo,
/// clipboard) whose own text is transparent. Every key, text input and pointer operation is bracketed so the owner
/// can port Arc's <c>onKeyDown</c> and <c>onSelect</c> (atomic tokens) and refresh the suggestion state once the
/// TextBox has finished its own work.
/// </summary>
internal sealed class MentionInputTextBox : TextBox
{
    internal static readonly StyledProperty<IBrush?> AccentSourceProperty =
        AvaloniaProperty.Register<MentionInputTextBox, IBrush?>("AccentSource");

    private MentionInputMirror? _mirror;

    static MentionInputTextBox()
    {
        // Arc's textarea: multi-line, wrapping, "\n" line breaks on every platform.
        AcceptsReturnProperty.OverrideDefaultValue<MentionInputTextBox>(true);
        TextWrappingProperty.OverrideDefaultValue<MentionInputTextBox>(TextWrapping.Wrap);
        NewLineProperty.OverrideDefaultValue<MentionInputTextBox>("\n");
    }

    public MentionInputTextBox()
    {
        ArcTokens.Bind(this, AccentSourceProperty, DesignTokens.ArcDesignTokenKind.Accent);
    }

    /// <summary>The control this editor belongs to.</summary>
    internal MentionInput? Owner { get; set; }

    internal MentionInputTextPresenter? Presenter { get; private set; }

    internal ScrollViewer? Scroller { get; private set; }

    /// <summary>True while an IME composition is active (Arc <c>isComposing</c>).</summary>
    internal bool IsComposing => !string.IsNullOrEmpty(Presenter?.PreeditText);

    protected override Type StyleKeyOverride => typeof(MentionInputTextBox);

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (Scroller is not null)
        {
            Scroller.ScrollChanged -= OnScrollChanged;
        }
        Presenter = e.NameScope.Find<MentionInputTextPresenter>("PART_TextPresenter");
        Scroller = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _mirror = e.NameScope.Find<MentionInputMirror>("PART_Mirror");
        if (_mirror is not null)
        {
            _mirror.Presenter = Presenter;
        }
        if (Presenter is not null)
        {
            Presenter.Mirror = _mirror;
            Presenter.LayoutChanged = () => Owner?.OnEditorLayoutChanged();
        }
        if (Scroller is not null)
        {
            Scroller.ScrollChanged += OnScrollChanged;
        }
        Owner?.OnEditorTemplateApplied();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => Owner?.OnEditorScrolled();

    /// <summary>Sets the token ranges drawn by the mirror and used to split the presenter's runs identically.</summary>
    internal void SetTokens(IReadOnlyList<MentionInputTokenSpan> tokens)
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
        if (Owner is { } owner && owner.HandleEditorKeyDown(e))
        {
            return;
        }
        Owner?.BeginEditorOperation();
        try
        {
            base.OnKeyDown(e);
        }
        finally
        {
            Owner?.EndEditorOperation();
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        Owner?.BeginEditorOperation();
        try
        {
            base.OnTextInput(e);
        }
        finally
        {
            Owner?.EndEditorOperation();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Owner?.BeginEditorOperation(pointer: true);
        try
        {
            base.OnPointerPressed(e);
        }
        finally
        {
            Owner?.EndEditorOperation();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        Owner?.BeginEditorOperation(pointer: true);
        try
        {
            base.OnPointerReleased(e);
        }
        finally
        {
            Owner?.EndEditorOperation();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnEditorFocusChanged(true);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.OnEditorFocusChanged(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Owner?.OnEditorTextChanged();
        }
        else if (change.Property == CaretIndexProperty || change.Property == SelectionStartProperty ||
                 change.Property == SelectionEndProperty)
        {
            Owner?.OnEditorSelectionChanged();
        }
        else if (change.Property == AccentSourceProperty)
        {
            // Arc: ::selection is color-mix(in oklab, var(--accent) 24%, transparent).
            var accent = change.GetNewValue<IBrush?>() is ISolidColorBrush solid ? solid.Color : Colors.Transparent;
            SelectionBrush = new ImmutableSolidColorBrush(Oklch.WithAlpha(accent, 0.24));
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new MentionInputEditorAutomationPeer(this);
}

/// <summary>
/// The TextBox presenter. Its runs are split at the token boundaries (with a transparent brush, like the rest of the
/// text) so its shaping, wrapping and caret positions are identical to the mirror's colored layout.
/// </summary>
internal sealed class MentionInputTextPresenter : TextPresenter
{
    private IReadOnlyList<MentionInputTokenSpan> _tokens = Array.Empty<MentionInputTokenSpan>();

    internal MentionInputMirror? Mirror { get; set; }

    /// <summary>Raised after a measure or arrange that may have changed the text layout (the field autosizes on it).</summary>
    internal Action? LayoutChanged { get; set; }

    protected override Type StyleKeyOverride => typeof(TextPresenter);

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        LayoutChanged?.Invoke();
        return size;
    }

    internal void SetTokens(IReadOnlyList<MentionInputTokenSpan> tokens)
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
    internal TextLayout BuildLayout(double maxWidth, double maxHeight, MentionInputMirror? colors)
    {
        var text = CombinedText(out var preeditStart, out var preeditLength);
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var foreground = colors?.TextBrush ?? Foreground;
        var spans = new List<ValueSpan<TextRunProperties>>(_tokens.Count + 1);
        var preeditEnd = preeditStart + preeditLength;
        var preeditAdded = preeditLength == 0;
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
            var brush = colors is null
                ? Foreground
                : token.Kind == MentionKind.Person ? colors.PersonForeground : colors.ChannelForeground;
            spans.Add(new ValueSpan<TextRunProperties>(start, token.Length,
                new GenericTextRunProperties(typeface, FontSize, null, brush, null, BaselineAlignment.Baseline, null, FontFeatures)));
        }
        if (!preeditAdded)
        {
            spans.Add(PreeditSpan(typeface, foreground, preeditStart, preeditLength));
        }
        return new TextLayout(text, typeface, FontSize, foreground, TextAlignment, TextWrapping, null, null, FlowDirection,
            maxWidth, maxHeight, LineHeight, LetterSpacing, 0, FontFeatures, spans.Count == 0 ? null : spans);
    }

    private ValueSpan<TextRunProperties> PreeditSpan(Typeface typeface, IBrush? foreground, int start, int length) =>
        new(start, length, new GenericTextRunProperties(typeface, FontSize, TextDecorations.Underline, foreground, null,
            BaselineAlignment.Baseline, null, FontFeatures));

    internal IReadOnlyList<MentionInputTokenSpan> Tokens => _tokens;
}

/// <summary>
/// Arc's backdrop: paints the editor's text with identical metrics behind the transparent TextBox text, with the
/// mention tokens tinted around their glyphs (radius 6, 1px / 3px padding with -3px margins so no glyph moves). It
/// sits in the same scrolling panel as the presenter, so it follows the TextBox's scroll exactly.
/// </summary>
internal sealed class MentionInputMirror : Control
{
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<MentionInputMirror, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> PersonFillProperty =
        AvaloniaProperty.Register<MentionInputMirror, IBrush?>(nameof(PersonFill));

    public static readonly StyledProperty<IBrush?> PersonForegroundProperty =
        AvaloniaProperty.Register<MentionInputMirror, IBrush?>(nameof(PersonForeground));

    public static readonly StyledProperty<IBrush?> ChannelFillProperty =
        AvaloniaProperty.Register<MentionInputMirror, IBrush?>(nameof(ChannelFill));

    public static readonly StyledProperty<IBrush?> ChannelForegroundProperty =
        AvaloniaProperty.Register<MentionInputMirror, IBrush?>(nameof(ChannelForeground));

    /// <summary>Token corner radius (Arc 6px).</summary>
    public static readonly StyledProperty<double> TokenRadiusProperty =
        AvaloniaProperty.Register<MentionInputMirror, double>(nameof(TokenRadius), 6);

    /// <summary>Token padding around the glyphs (Arc <c>padding: 1px 3px</c>, horizontal part cancelled by margins).</summary>
    public static readonly StyledProperty<Thickness> TokenPaddingProperty =
        AvaloniaProperty.Register<MentionInputMirror, Thickness>(nameof(TokenPadding), new Thickness(3, 1));

    static MentionInputMirror()
    {
        AffectsRender<MentionInputMirror>(TextBrushProperty, PersonFillProperty, PersonForegroundProperty, ChannelFillProperty,
            ChannelForegroundProperty, TokenRadiusProperty, TokenPaddingProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<MentionInputMirror>(false);
        FocusableProperty.OverrideDefaultValue<MentionInputMirror>(false);
    }

    private TextLayout? _layout;
    private TextLayout? _source;
    private int _brushVersion;
    private int _builtBrushVersion = -1;

    public IBrush? TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }

    public IBrush? PersonFill { get => GetValue(PersonFillProperty); set => SetValue(PersonFillProperty, value); }

    public IBrush? PersonForeground { get => GetValue(PersonForegroundProperty); set => SetValue(PersonForegroundProperty, value); }

    public IBrush? ChannelFill { get => GetValue(ChannelFillProperty); set => SetValue(ChannelFillProperty, value); }

    public IBrush? ChannelForeground { get => GetValue(ChannelForegroundProperty); set => SetValue(ChannelForegroundProperty, value); }

    public double TokenRadius { get => GetValue(TokenRadiusProperty); set => SetValue(TokenRadiusProperty, value); }

    public Thickness TokenPadding { get => GetValue(TokenPaddingProperty); set => SetValue(TokenPaddingProperty, value); }

    internal MentionInputTextPresenter? Presenter { get; set; }

    /// <summary>The colored layout last drawn (tests).</summary>
    internal TextLayout? Layout => _layout;

    protected override Size MeasureOverride(Size availableSize) => default;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextBrushProperty || change.Property == PersonForegroundProperty ||
            change.Property == ChannelForegroundProperty)
        {
            _brushVersion++;
        }
    }

    /// <summary>Rebuilds the colored layout when the presenter's layout or the brushes changed.</summary>
    internal TextLayout? EnsureLayout()
    {
        var presenter = Presenter;
        if (presenter is null)
        {
            return null;
        }
        var source = presenter.TextLayout;
        if (!ReferenceEquals(source, _source) || _builtBrushVersion != _brushVersion || _layout is null)
        {
            _layout?.Dispose();
            _layout = presenter.BuildLayout(source.MaxWidth, source.MaxHeight, this);
            _source = source;
            _builtBrushVersion = _brushVersion;
        }
        return _layout;
    }

    public override void Render(DrawingContext context)
    {
        var presenter = Presenter;
        var layout = EnsureLayout();
        if (presenter is null || layout is null)
        {
            return;
        }
        var tokens = presenter.Tokens;
        if (tokens.Count > 0)
        {
            presenter.CombinedText(out var preeditStart, out var preeditLength);
            var (ascent, descent) = ContentMetrics(presenter);
            var lines = layout.TextLines;
            var padding = TokenPadding;
            var radius = TokenRadius;
            foreach (var token in tokens)
            {
                var fill = token.Kind == MentionKind.Person ? PersonFill : ChannelFill;
                if (fill is null)
                {
                    continue;
                }
                var start = token.Start;
                if (preeditLength > 0 && start >= preeditStart)
                {
                    start += preeditLength;
                }
                foreach (var rect in layout.HitTestTextRange(start, token.Length))
                {
                    var top = ContentTop(lines, rect, ascent);
                    var box = new Rect(rect.X - padding.Left, top - padding.Top,
                        rect.Width + padding.Left + padding.Right, ascent + descent + padding.Top + padding.Bottom);
                    context.DrawRectangle(fill, null, box, radius, radius);
                }
            }
        }
        layout.Draw(context, default);
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
/// The editor is Arc's combobox textarea: editable text with an expand / collapse state for the suggestion list.
/// The active suggestion's name is published as the item status (Arc <c>aria-activedescendant</c>).
/// </summary>
internal sealed class MentionInputEditorAutomationPeer : TextBoxAutomationPeer, IExpandCollapseProvider
{
    public MentionInputEditorAutomationPeer(MentionInputTextBox owner)
        : base(owner)
    {
    }

    private MentionInput? Field => ((MentionInputTextBox)Owner).Owner;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;

    public ExpandCollapseState ExpandCollapseState =>
        Field?.IsSuggestionListOpen == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public bool ShowsMenu => false;

    public void Expand()
    {
    }

    public void Collapse() => Field?.DismissSuggestions();

    internal void RaiseExpandChanged(bool open) => RaisePropertyChangedEvent(
        ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
        open ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded,
        open ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}
