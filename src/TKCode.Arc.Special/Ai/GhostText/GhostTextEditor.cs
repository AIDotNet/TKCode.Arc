using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The field inside <see cref="GhostText"/>: a real Avalonia <see cref="TextBox"/> (caret, selection, IME, undo, clipboard
/// and spellcheck stay native) whose presenter lays the ghost out as a muted tail of the same text layout, right after the
/// last character. Because the text and the ghost are one layout they wrap identically at any width and the ghost always
/// starts at the caret. Every key, text input and pointer operation is bracketed: while the TextBox does its own work the
/// ghost is taken out of the layout (so no hit test or caret move can land inside it), and when the operation ends the owner
/// decides whether the ghost stays (the caret is still at the end) or goes.
/// </summary>
internal sealed class GhostTextEditor : TextBox
{
    /// <summary>The ghost ink (theme: TextMuted).</summary>
    public static readonly StyledProperty<IBrush?> GhostForegroundProperty =
        AvaloniaProperty.Register<GhostTextEditor, IBrush?>(nameof(GhostForeground));

    private readonly List<GhostTextChunk> _chunks = new();
    private readonly List<GhostTextMerge> _merges = new();
    private string _ghost = string.Empty;
    private int _operations;
    private bool _suppressed;
    private MotionValue<double>? _leave;
    private double _leaveOpacity = 1;
    private bool _leaving;

    static GhostTextEditor()
    {
        AcceptsReturnProperty.OverrideDefaultValue<GhostTextEditor>(true);
        TextWrappingProperty.OverrideDefaultValue<GhostTextEditor>(TextWrapping.Wrap);
        NewLineProperty.OverrideDefaultValue<GhostTextEditor>("\n");
    }

    public IBrush? GhostForeground
    {
        get => GetValue(GhostForegroundProperty);
        set => SetValue(GhostForegroundProperty, value);
    }

    /// <summary>The control this field belongs to.</summary>
    internal GhostText? Owner { get; set; }

    internal GhostTextPresenter? Presenter { get; private set; }

    /// <summary>The ghost on screen (empty when none; still set while it fades out).</summary>
    internal string Ghost => _ghost;

    /// <summary>True while the ghost fades out after Escape or blur (it is no longer a suggestion).</summary>
    internal bool IsGhostLeaving => _leaving;

    /// <summary>True between the start and end of a key, text or pointer operation.</summary>
    internal bool InOperation => _operations > 0;

    /// <summary>The streamed pieces of the ghost, each with its own fade (tests).</summary>
    internal IReadOnlyList<GhostTextChunk> Chunks => _chunks;

    /// <summary>The accepted ranges still easing from ghost ink to text ink (tests).</summary>
    internal IReadOnlyList<GhostTextMerge> Merges => _merges;

    /// <summary>Whether the presenter lays the ghost out right now.</summary>
    internal bool LaysOutGhost =>
        !_suppressed && (_ghost.Length > 0 || _merges.Count > 0) && string.IsNullOrEmpty(Presenter?.PreeditText);

    protected override Type StyleKeyOverride => typeof(GhostTextEditor);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (Presenter is not null)
        {
            Presenter.Editor = null;
        }
        Presenter = e.NameScope.Find<GhostTextPresenter>("PART_TextPresenter");
        if (Presenter is not null)
        {
            Presenter.Editor = this;
        }
    }

    /// <summary>Types <paramref name="text"/> at the caret as if from the keyboard, so it joins the undo stack.</summary>
    internal void TypeText(string text) => base.OnTextInput(new TextInputEventArgs { RoutedEvent = TextInputEvent, Text = text, Source = this });

    /// <summary>Types <paramref name="text"/> through the field's own input handling (the sample's keystrokes).</summary>
    internal void SimulateInput(string text) => OnTextInput(new TextInputEventArgs { RoutedEvent = TextInputEvent, Text = text, Source = this });

    /// <summary>Puts the caret at the end of the text with nothing selected.</summary>
    internal void CaretToEnd()
    {
        var length = Text?.Length ?? 0;
        SetCurrentValue(SelectionStartProperty, length);
        SetCurrentValue(SelectionEndProperty, length);
        SetCurrentValue(CaretIndexProperty, length);
    }

    /// <summary>True when the caret is at the end of the text and nothing is selected.</summary>
    internal bool CaretAtEnd
    {
        get
        {
            var length = Text?.Length ?? 0;
            return CaretIndex == length && SelectionStart == SelectionEnd;
        }
    }

    // ----- Ghost ---------------------------------------------------------------------------------------------------

    /// <summary>Appends a streamed chunk; with <paramref name="fade"/> it fades in on its own (140ms, opacity only).</summary>
    internal void AppendGhost(string chunk, bool fade)
    {
        if (chunk.Length == 0)
        {
            return;
        }
        if (_leaving)
        {
            // A new suggestion replaces one that is still fading out.
            ClearGhost();
        }
        var piece = new GhostTextChunk(_ghost.Length, chunk.Length);
        _ghost += chunk;
        _chunks.Add(piece);
        if (fade && IsLoaded && ArcMotion.IsFull(this))
        {
            piece.Opacity = 0;
            piece.Motion = new MotionValue<double>(this, 0, MotionVectors.Double);
            piece.Motion.Changed += value =>
            {
                piece.Opacity = value;
                UpdateGhostBrushes();
            };
            _ = piece.Motion.AnimateTo(1, MotionSpec.Tween(GhostTextMotion.ChunkFadeIn, ArcEasings.Standard));
        }
        UpdateGhostBrushes();
        InvalidateGhostLayout();
    }

    /// <summary>Removes the first <paramref name="count"/> characters of the ghost (typed through or accepted); the rest keeps its fades.</summary>
    internal void ConsumeGhost(int count)
    {
        count = Math.Clamp(count, 0, _ghost.Length);
        if (count == 0)
        {
            return;
        }
        _ghost = _ghost[count..];
        for (var i = _chunks.Count - 1; i >= 0; i--)
        {
            var chunk = _chunks[i];
            var end = chunk.Start + chunk.Length - count;
            var start = Math.Max(0, chunk.Start - count);
            if (end <= 0)
            {
                chunk.Motion?.Stop();
                _chunks.RemoveAt(i);
                continue;
            }
            chunk.Start = start;
            chunk.Length = end - start;
        }
        InvalidateGhostLayout();
    }

    /// <summary>Takes the ghost away at once (typing something else, accepting, a caret move).</summary>
    internal void ClearGhost()
    {
        StopLeaving();
        if (_ghost.Length == 0 && _chunks.Count == 0)
        {
            return;
        }
        foreach (var chunk in _chunks)
        {
            chunk.Motion?.Stop();
        }
        _chunks.Clear();
        _ghost = string.Empty;
        InvalidateGhostLayout();
    }

    /// <summary>Fades the ghost out over 110ms and then takes it away (Escape, leaving the field).</summary>
    internal void FadeOutGhost()
    {
        if (_ghost.Length == 0 || _leaving)
        {
            return;
        }
        if (!IsLoaded || !ArcMotion.IsFull(this))
        {
            ClearGhost();
            return;
        }
        _leaving = true;
        _leave ??= CreateLeave();
        _leave.Jump(1);
        _leaveOpacity = 1;
        _ = Leave();
    }

    private MotionValue<double> CreateLeave()
    {
        var value = new MotionValue<double>(this, 1, MotionVectors.Double);
        value.Changed += opacity =>
        {
            _leaveOpacity = opacity;
            UpdateGhostBrushes();
        };
        return value;
    }

    private async Task Leave()
    {
        var done = await _leave!.AnimateTo(0, MotionSpec.Tween(GhostTextMotion.FadeOut, ArcEasings.Exit));
        if (done && _leaving)
        {
            ClearGhost();
        }
    }

    private void StopLeaving()
    {
        if (!_leaving)
        {
            return;
        }
        _leaving = false;
        _leave?.Stop();
        _leaveOpacity = 1;
        UpdateGhostBrushes();
    }

    /// <summary>The accepted range eases from the ghost ink to the text ink over 200ms in place (full motion only).</summary>
    internal void MergeInk(int start, int length)
    {
        if (length <= 0 || !IsLoaded || !ArcMotion.IsFull(this))
        {
            return;
        }
        var merge = new GhostTextMerge(start, length);
        merge.Motion = new MotionValue<double>(this, 0, MotionVectors.Double);
        merge.Motion.Changed += value =>
        {
            merge.Progress = value;
            UpdateGhostBrushes();
        };
        _merges.Add(merge);
        UpdateGhostBrushes();
        InvalidateGhostLayout();
        _ = RunMerge(merge);
    }

    private async Task RunMerge(GhostTextMerge merge)
    {
        await merge.Motion!.AnimateTo(1, MotionSpec.Tween(GhostTextMotion.InkMerge, ArcEasings.Standard));
        if (_merges.Remove(merge))
        {
            InvalidateGhostLayout();
        }
    }

    /// <summary>Ends every ink merge at once (the text changed under it).</summary>
    internal void EndMerges()
    {
        if (_merges.Count == 0)
        {
            return;
        }
        foreach (var merge in _merges)
        {
            merge.Motion?.Stop();
        }
        _merges.Clear();
        InvalidateGhostLayout();
    }

    /// <summary>Writes the current fades into the brushes the layout draws with and repaints (no new layout).</summary>
    internal void UpdateGhostBrushes()
    {
        var ghost = (GhostForeground as ISolidColorBrush)?.Color ?? Colors.Gray;
        var text = (Foreground as ISolidColorBrush)?.Color ?? Colors.Black;
        foreach (var chunk in _chunks)
        {
            chunk.Brush.Color = WithOpacity(ghost, chunk.Opacity * _leaveOpacity);
        }
        foreach (var merge in _merges)
        {
            merge.Brush.Color = Lerp(ghost, text, merge.Progress);
        }
        Presenter?.InvalidateVisual();
    }

    private static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(opacity, 0, 1)), color.R, color.G, color.B);

    internal static Color Lerp(Color from, Color to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        static byte Mix(byte a, byte b, double t) => (byte)Math.Round(a + (b - a) * t);
        return Color.FromArgb(Mix(from.A, to.A, t), Mix(from.R, to.R, t), Mix(from.G, to.G, t), Mix(from.B, to.B, t));
    }

    private void InvalidateGhostLayout() => Presenter?.RebuildLayout();

    // ----- Operations ----------------------------------------------------------------------------------------------

    private void BeginOperation()
    {
        _operations++;
        if (_operations == 1 && _ghost.Length > 0)
        {
            _suppressed = true;
            InvalidateGhostLayout();
        }
    }

    private void EndOperation()
    {
        _operations--;
        if (_operations > 0)
        {
            return;
        }
        var wasSuppressed = _suppressed;
        _suppressed = false;
        Owner?.OnEditorOperationEnded();
        if (wasSuppressed)
        {
            InvalidateGhostLayout();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Owner is { } owner && owner.HandleEditorKey(e))
        {
            e.Handled = true;
            return;
        }
        BeginOperation();
        try
        {
            base.OnKeyDown(e);
        }
        finally
        {
            EndOperation();
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!AcceptsReturn && e.Text is { } typed && (typed.Contains('\n') || typed.Contains('\r')))
        {
            // A single-line field: line breaks become spaces.
            e.Text = GhostText.SingleLine(typed);
        }
        BeginOperation();
        try
        {
            base.OnTextInput(e);
        }
        finally
        {
            EndOperation();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Owner?.OnEditorPointer(e.Pointer.Type);
        BeginOperation();
        try
        {
            base.OnPointerPressed(e);
        }
        finally
        {
            EndOperation();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        BeginOperation();
        try
        {
            base.OnPointerReleased(e);
        }
        finally
        {
            EndOperation();
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        Owner?.OnEditorFocusChanged(true, e.NavigationMethod);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Owner?.OnEditorFocusChanged(false, NavigationMethod.Unspecified);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            Owner?.OnEditorTextChanged(change.GetOldValue<string?>() ?? string.Empty, change.GetNewValue<string?>() ?? string.Empty);
        }
        else if (change.Property == CaretIndexProperty || change.Property == SelectionStartProperty ||
                 change.Property == SelectionEndProperty)
        {
            Owner?.OnEditorSelectionChanged();
        }
        else if (change.Property == GhostForegroundProperty || change.Property == ForegroundProperty)
        {
            UpdateGhostBrushes();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TextBoxAutomationPeer(this);
}

/// <summary>One streamed piece of the ghost: where it sits in the ghost, its fade and the brush the layout draws it with.</summary>
internal sealed class GhostTextChunk
{
    public GhostTextChunk(int start, int length)
    {
        Start = start;
        Length = length;
    }

    public int Start { get; set; }

    public int Length { get; set; }

    public double Opacity { get; set; } = 1;

    public MotionValue<double>? Motion { get; set; }

    /// <summary>Mutable on purpose: a fade writes its color and repaints without laying the text out again.</summary>
    public SolidColorBrush Brush { get; } = new();
}

/// <summary>An accepted range easing from the ghost ink to the text ink.</summary>
internal sealed class GhostTextMerge
{
    public GhostTextMerge(int start, int length)
    {
        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public double Progress { get; set; }

    public MotionValue<double>? Motion { get; set; }

    public SolidColorBrush Brush { get; } = new();
}

/// <summary>
/// The field's presenter. With a ghost it lays out the text followed by the ghost as one paragraph (the ghost's chunks and
/// any accepted range still merging get their own brushes, so fades only repaint), otherwise exactly what a TextBox shows.
/// Caret, selection and hit testing only ever see indexes inside the text: the caret sits at the end whenever a ghost shows,
/// and the editor takes the ghost out of the layout while the TextBox handles input.
/// </summary>
internal sealed class GhostTextPresenter : TextPresenter
{
    internal GhostTextEditor? Editor { get; set; }

    protected override Type StyleKeyOverride => typeof(TextPresenter);

    /// <summary>Lays the text out again (the ghost or a merge changed).</summary>
    internal void RebuildLayout() => InvalidateTextLayout();

    /// <summary>The characters the layout holds: the text plus the ghost (tests).</summary>
    internal string LaidOutText => TextLayout.TextLines.Count == 0 ? string.Empty : _laidOut;

    private string _laidOut = string.Empty;

    protected override TextLayout CreateTextLayout()
    {
        var layout = base.CreateTextLayout();
        var editor = Editor;
        var text = Text ?? string.Empty;
        if (editor is null || !editor.LaysOutGhost || (PasswordChar != '\0' && !RevealPassword))
        {
            _laidOut = text;
            return layout;
        }
        var maxWidth = layout.MaxWidth;
        var maxHeight = layout.MaxHeight;
        layout.Dispose();
        var ghost = editor.Ghost;
        var combined = text + ghost;
        _laidOut = combined;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var spans = new List<ValueSpan<TextRunProperties>>(editor.Chunks.Count + editor.Merges.Count);
        foreach (var merge in editor.Merges)
        {
            var start = Math.Clamp(merge.Start, 0, text.Length);
            var end = Math.Clamp(merge.Start + merge.Length, 0, text.Length);
            if (end > start)
            {
                spans.Add(Span(typeface, merge.Brush, start, end - start));
            }
        }
        spans.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        foreach (var chunk in editor.Chunks)
        {
            if (chunk.Length > 0)
            {
                spans.Add(Span(typeface, chunk.Brush, text.Length + chunk.Start, chunk.Length));
            }
        }
        return new TextLayout(combined, typeface, FontSize, Foreground, TextAlignment, TextWrapping, null, null, FlowDirection,
            maxWidth, maxHeight, LineHeight, LetterSpacing, 0, FontFeatures, spans.Count == 0 ? null : spans);
    }

    private ValueSpan<TextRunProperties> Span(Typeface typeface, IBrush brush, int start, int length) =>
        new(start, length, new GenericTextRunProperties(typeface, FontSize, null, brush, null, BaselineAlignment.Baseline, null,
            FontFeatures));
}
