using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace TKCode.Arc;

/// <summary>
/// The summary line of <see cref="ExpandableCard"/> (P4): when the text changes, words that changed rise in from
/// 0.35em out of a soft blur (standard + enter) and the old ones lift away by 0.3em into a subtle blur (fast); words
/// that stayed hold still. Words wrap like text. Drawn in <see cref="Render"/> with one formatted run per word;
/// assistive technology reads the plain text.
/// </summary>
internal sealed class ExpandableCardSummary : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ExpandableCardSummary, string?>(nameof(Text));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner<ExpandableCardSummary>();
    public static readonly StyledProperty<double> FontSizeProperty = TextElement.FontSizeProperty.AddOwner<ExpandableCardSummary>();
    public static readonly StyledProperty<FontWeight> FontWeightProperty = TextElement.FontWeightProperty.AddOwner<ExpandableCardSummary>();
    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<ExpandableCardSummary>();

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    private static readonly FontFeatureCollection TabularNumbers = new() { FontFeature.Parse("tnum") };

    /// <summary>Blur radii quantized to 0.25px so a running blur allocates nothing per frame.</summary>
    private static readonly ImmutableBlurEffect[] Blurs = Enumerable.Range(1, 16).Select(i => new ImmutableBlurEffect(i * 0.25)).ToArray();

    private sealed class Word
    {
        public required string Key { get; init; }
        public required string Text { get; init; }
        public FormattedText? Layout { get; set; }
        public Point Position { get; set; }
        public MotionValue<double>? Enter { get; set; }
        public MotionValue<double>? Exit { get; set; }
    }

    private readonly List<Word> _words = new();
    private readonly List<Word> _leaving = new();
    private bool _fontDirty = true;
    private double _lineHeight;

    static ExpandableCardSummary()
    {
        AffectsMeasure<ExpandableCardSummary>(FontFamilyProperty, FontSizeProperty, FontWeightProperty, ForegroundProperty);
        AffectsRender<ExpandableCardSummary>(ForegroundProperty);
    }

    /// <summary>Number of words currently leaving (tests).</summary>
    internal int LeavingCount => _leaving.Count;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text ?? string.Empty);
            Update(change.GetOldValue<string?>() ?? string.Empty, Text ?? string.Empty);
        }
        else if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
                 change.Property == FontWeightProperty || change.Property == ForegroundProperty)
        {
            _fontDirty = true;
        }
    }

    /// <summary>Splits on whitespace, keeping the separators as their own tokens (Arc: text.split(/(\s+)/)).</summary>
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var start = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i == text.Length || char.IsWhiteSpace(text[i]) != char.IsWhiteSpace(text[i - 1]))
            {
                tokens.Add(text[start..i]);
                start = i;
            }
        }
        return tokens;
    }

    private void Update(string oldText, string newText)
    {
        var animate = IsLoaded && ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full && _words.Count > 0;
        var previous = _words.ToDictionary(w => w.Key);
        _words.Clear();
        var tokens = Tokenize(newText);
        for (var i = 0; i < tokens.Count; i++)
        {
            var key = $"{i}:{tokens[i]}";
            if (previous.Remove(key, out var kept))
            {
                _words.Add(kept);
                continue;
            }
            var word = new Word { Key = key, Text = tokens[i] };
            _words.Add(word);
            if (animate && !string.IsNullOrWhiteSpace(word.Text))
            {
                word.Enter = new MotionValue<double>(this, 0, MotionVectors.Double);
                word.Enter.Changed += _ => InvalidateVisual();
                _ = word.Enter.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter));
            }
        }
        if (animate)
        {
            foreach (var gone in previous.Values)
            {
                if (string.IsNullOrWhiteSpace(gone.Text) || gone.Layout is null)
                {
                    continue;
                }
                _leaving.Add(gone);
                gone.Enter = null;
                gone.Exit = new MotionValue<double>(this, 0, MotionVectors.Double);
                gone.Exit.Changed += _ => InvalidateVisual();
                RemoveWhenGone(gone);
            }
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    private async void RemoveWhenGone(Word word)
    {
        await word.Exit!.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        _leaving.Remove(word);
        InvalidateVisual();
    }

    private void EnsureLayouts()
    {
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);
        foreach (var word in _words)
        {
            if (word.Layout is null || _fontDirty)
            {
                word.Layout = new FormattedText(word.Text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, FontSize, Foreground);
                word.Layout.SetFontFeatures(TabularNumbers);
            }
        }
        _fontDirty = false;
        _lineHeight = _words.Count > 0 && _words[0].Layout is { } first ? first.Height : FontSize * 1.3;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayouts();
        double x = 0, y = 0, width = 0;
        var max = availableSize.Width;
        foreach (var word in _words)
        {
            var w = word.Layout!.WidthIncludingTrailingWhitespace;
            var space = string.IsNullOrWhiteSpace(word.Text);
            if (!space && x > 0 && x + w > max + 0.5)
            {
                x = 0;
                y += _lineHeight;
            }
            if (space && x == 0)
            {
                continue;
            }
            word.Position = new Point(x, y);
            x += w;
            width = Math.Max(width, x);
        }
        return new Size(Math.Min(width, max), _words.Count == 0 ? 0 : y + _lineHeight);
    }

    public override void Render(DrawingContext context)
    {
        var em = FontSize;
        foreach (var word in _leaving)
        {
            var p = Math.Clamp(word.Exit?.Value ?? 1, 0, 1);
            Draw(context, word, 1 - p, -0.3 * em * p, 2 * p);
        }
        foreach (var word in _words)
        {
            if (word.Layout is null || string.IsNullOrWhiteSpace(word.Text))
            {
                continue;
            }
            var p = Math.Clamp(word.Enter?.Value ?? 1, 0, 1);
            Draw(context, word, p, 0.35 * em * (1 - p), 4 * (1 - p));
        }
    }

    private static void Draw(DrawingContext context, Word word, double opacity, double dy, double blur)
    {
        if (word.Layout is null || opacity <= 0.001)
        {
            return;
        }
        var origin = word.Position + new Point(0, dy);
        using (context.PushOpacity(opacity))
        {
            var index = (int)Math.Round(blur * 4) - 1;
            if (index >= 0)
            {
                var bounds = new Rect(origin, new Size(word.Layout.Width, word.Layout.Height)).Inflate(8);
                using (context.PushEffect(Blurs[Math.Min(index, Blurs.Length - 1)], bounds))
                {
                    context.DrawText(word.Layout, origin);
                }
            }
            else
            {
                context.DrawText(word.Layout, origin);
            }
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new SummaryAutomationPeer(this);

    private sealed class SummaryAutomationPeer : ControlAutomationPeer
    {
        public SummaryAutomationPeer(ExpandableCardSummary owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;

        protected override string? GetNameCore() => ((ExpandableCardSummary)Owner).Text;
    }
}
