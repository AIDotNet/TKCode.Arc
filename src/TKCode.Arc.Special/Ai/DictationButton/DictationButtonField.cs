using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.Utilities;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// The composer's field: a chrome-less multi-line text box. While dictating it is read-only and its presenter lays the live
/// words out after the text as one paragraph: settled words in the text ink, the recognizer's guesses in a muted tone, each
/// word with its own brush so a fade (140ms, opacity only) or a settle only repaints. A corrected guess refades just the
/// words that changed. The newest words stay in view.
/// </summary>
internal sealed class DictationButtonField : TextBox
{
    /// <summary>The ink of words the recognizer is still guessing.</summary>
    public static readonly StyledProperty<IBrush?> InterimForegroundProperty =
        AvaloniaProperty.Register<DictationButtonField, IBrush?>(nameof(InterimForeground));

    /// <summary>A word fades in over 140ms.</summary>
    internal static readonly TimeSpan WordFade = TimeSpan.FromMilliseconds(140);

    private readonly List<DictationLiveWord> _words = new();
    private readonly List<string> _finalWords = new();
    private readonly List<string> _interimWords = new();
    private TextBlock? _placeholder;
    private ScrollViewer? _scroller;

    static DictationButtonField()
    {
        AcceptsReturnProperty.OverrideDefaultValue<DictationButtonField>(true);
        TextWrappingProperty.OverrideDefaultValue<DictationButtonField>(TextWrapping.Wrap);
        NewLineProperty.OverrideDefaultValue<DictationButtonField>("\n");
    }

    public IBrush? InterimForeground { get => GetValue(InterimForegroundProperty); set => SetValue(InterimForegroundProperty, value); }

    internal DictationButtonPresenter? Presenter { get; private set; }

    /// <summary>The live words on show, in order (tests).</summary>
    internal IReadOnlyList<DictationLiveWord> LiveWords => _words;

    /// <summary>The live words as one string (empty when none).</summary>
    internal string LiveText
    {
        get
        {
            if (_words.Count == 0)
            {
                return string.Empty;
            }
            var text = Text ?? string.Empty;
            var lead = text.Length > 0 && !char.IsWhiteSpace(text[^1]) ? " " : string.Empty;
            return lead + string.Join(' ', _words.Select(w => w.Text));
        }
    }

    internal bool HasLive => _words.Count > 0;

    internal bool IsPlaceholderShown => _placeholder?.IsVisible == true;

    protected override Type StyleKeyOverride => typeof(DictationButtonField);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (Presenter is not null)
        {
            Presenter.Field = null;
            Presenter.PropertyChanged -= OnPresenterPropertyChanged;
        }
        Presenter = e.NameScope.Find<DictationButtonPresenter>("PART_TextPresenter");
        _placeholder = e.NameScope.Find<TextBlock>("PART_Placeholder");
        _scroller = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (Presenter is not null)
        {
            Presenter.Field = this;
            Presenter.PropertyChanged += OnPresenterPropertyChanged;
        }
        UpdatePlaceholder();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            UpdatePlaceholder();
            if (_words.Count > 0)
            {
                Presenter?.RebuildLayout();
            }
        }
        else if (change.Property == ForegroundProperty || change.Property == InterimForegroundProperty)
        {
            UpdateBrushes();
        }
    }

    private void OnPresenterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextPresenter.PreeditTextProperty)
        {
            UpdatePlaceholder();
        }
    }

    /// <summary>Shows the live transcript after the text; words that did not change keep their state.</summary>
    internal void SetLive(DictationTranscript transcript)
    {
        _finalWords.Clear();
        _interimWords.Clear();
        DictationText.SplitWords(transcript.Final, _finalWords);
        DictationText.SplitWords(transcript.Interim, _interimWords);
        var count = _finalWords.Count + _interimWords.Count;
        var fades = IsLoaded && ArcMotion.IsFull(this);
        var changed = false;
        for (var i = 0; i < count; i++)
        {
            var final = i < _finalWords.Count;
            var text = final ? _finalWords[i] : _interimWords[i - _finalWords.Count];
            if (i < _words.Count)
            {
                var word = _words[i];
                if (word.Text != text)
                {
                    // A corrected guess: only this word refades.
                    word.Text = text;
                    changed = true;
                    StartFade(word, fades);
                }
                word.IsFinal = final;
            }
            else
            {
                var word = new DictationLiveWord(text) { IsFinal = final };
                _words.Add(word);
                changed = true;
                StartFade(word, fades);
            }
        }
        if (_words.Count > count)
        {
            for (var i = count; i < _words.Count; i++)
            {
                _words[i].Motion?.Stop();
            }
            _words.RemoveRange(count, _words.Count - count);
            changed = true;
        }
        UpdateBrushes();
        UpdatePlaceholder();
        if (changed)
        {
            Presenter?.RebuildLayout();
            KeepNewestInView();
        }
    }

    /// <summary>Drops the live words.</summary>
    internal void ClearLive()
    {
        if (_words.Count == 0)
        {
            return;
        }
        foreach (var word in _words)
        {
            word.Motion?.Stop();
        }
        _words.Clear();
        UpdatePlaceholder();
        Presenter?.RebuildLayout();
    }

    /// <summary>Appends <paramref name="text"/> to the field with a space, then puts the caret at the end.</summary>
    internal void Append(string text)
    {
        var current = Text ?? string.Empty;
        var joined = current.Length == 0 || char.IsWhiteSpace(current[^1]) ? current + text : current + " " + text;
        SetCurrentValue(TextProperty, joined);
        CaretToEnd();
    }

    /// <summary>Puts the caret at the end of the text with nothing selected.</summary>
    internal void CaretToEnd()
    {
        var length = Text?.Length ?? 0;
        SetCurrentValue(SelectionStartProperty, length);
        SetCurrentValue(SelectionEndProperty, length);
        SetCurrentValue(CaretIndexProperty, length);
    }

    /// <summary>Scrolls to the newest words once the layout has caught up.</summary>
    internal void KeepNewestInView()
    {
        if (_scroller is null)
        {
            return;
        }
        Dispatcher.UIThread.Post(() => _scroller?.ScrollToEnd(), DispatcherPriority.Loaded);
    }

    private void StartFade(DictationLiveWord word, bool fades)
    {
        word.Motion?.Stop();
        if (!fades)
        {
            word.Opacity = 1;
            return;
        }
        word.Opacity = 0;
        word.Motion ??= CreateFade(word);
        word.Motion.Jump(0);
        word.Opacity = 0;
        _ = word.Motion.AnimateTo(1, MotionSpec.Tween(WordFade, ArcEasings.Standard));
    }

    private MotionValue<double> CreateFade(DictationLiveWord word)
    {
        var value = new MotionValue<double>(this, 0, MotionVectors.Double);
        value.Changed += v =>
        {
            word.Opacity = v;
            word.Paint(Ink(true), Ink(false));
            Presenter?.InvalidateVisual();
        };
        return value;
    }

    private Color Ink(bool final) =>
        ((final ? Foreground : InterimForeground ?? Foreground) as ISolidColorBrush)?.Color ?? Colors.Gray;

    private void UpdateBrushes()
    {
        var final = Ink(true);
        var interim = Ink(false);
        foreach (var word in _words)
        {
            word.Paint(final, interim);
        }
        Presenter?.InvalidateVisual();
    }

    private void UpdatePlaceholder()
    {
        if (_placeholder is not null)
        {
            _placeholder.IsVisible = string.IsNullOrEmpty(Text) && _words.Count == 0 && string.IsNullOrEmpty(Presenter?.PreeditText);
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TextBoxAutomationPeer(this);
}

/// <summary>One live word: its text, whether it is settled, its fade and the brush the layout draws it with.</summary>
internal sealed class DictationLiveWord
{
    public DictationLiveWord(string text)
    {
        Text = text;
    }

    public string Text { get; set; }

    public bool IsFinal { get; set; }

    public double Opacity { get; set; } = 1;

    public MotionValue<double>? Motion { get; set; }

    /// <summary>Mutable on purpose: a fade or a settle writes its color and repaints without laying the text out again.</summary>
    public SolidColorBrush Brush { get; } = new();

    public void Paint(Color final, Color interim)
    {
        var color = IsFinal ? final : interim;
        Brush.Color = Color.FromArgb((byte)Math.Round(color.A * Math.Clamp(Opacity, 0, 1)), color.R, color.G, color.B);
    }
}

/// <summary>
/// The field's presenter. With live words it lays out the text followed by the words as one paragraph (each word with its
/// own brush), otherwise exactly what a TextBox shows. The field is read-only while the words show, so caret and selection
/// only ever see the text.
/// </summary>
internal sealed class DictationButtonPresenter : TextPresenter
{
    private string _laidOut = string.Empty;

    internal DictationButtonField? Field { get; set; }

    protected override Type StyleKeyOverride => typeof(TextPresenter);

    /// <summary>The characters the layout holds: the text plus the live words (tests).</summary>
    internal string LaidOutText => _laidOut;

    internal void RebuildLayout() => InvalidateTextLayout();

    protected override TextLayout CreateTextLayout()
    {
        var layout = base.CreateTextLayout();
        var field = Field;
        var text = Text ?? string.Empty;
        if (field is null || !field.HasLive || !string.IsNullOrEmpty(PreeditText))
        {
            _laidOut = text;
            return layout;
        }
        var maxWidth = layout.MaxWidth;
        var maxHeight = layout.MaxHeight;
        layout.Dispose();
        var words = field.LiveWords;
        var lead = text.Length > 0 && !char.IsWhiteSpace(text[^1]) ? " " : string.Empty;
        var builder = new System.Text.StringBuilder(text.Length + lead.Length + words.Count * 8);
        builder.Append(text).Append(lead);
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var spans = new List<ValueSpan<TextRunProperties>>(words.Count);
        for (var i = 0; i < words.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(' ');
            }
            var start = builder.Length;
            builder.Append(words[i].Text);
            spans.Add(new ValueSpan<TextRunProperties>(start, words[i].Text.Length,
                new GenericTextRunProperties(typeface, FontSize, null, words[i].Brush, null, BaselineAlignment.Baseline, null, FontFeatures)));
        }
        var combined = builder.ToString();
        _laidOut = combined;
        return new TextLayout(combined, typeface, FontSize, Foreground, TextAlignment, TextWrapping, null, null, FlowDirection,
            maxWidth, maxHeight, LineHeight, LetterSpacing, 0, FontFeatures, spans);
    }
}
