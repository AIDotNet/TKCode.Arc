using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// A sentence that swaps word by word: when the text changes, the words that stay do not move; changed words leave
/// (fading and lifting 0.3em in 160ms, where they stood) and their replacements arrive (fading in over 240ms while rising
/// from 0.35em below), staggered 40ms apiece up to 240ms. Under reduced motion changed words cross-fade in 120ms.
/// Wraps at the available width (at most <see cref="MaxTextWidth"/>).
/// </summary>
/// <remarks>
/// Code-driven motion: every word is a separate drawn run with its own delay, and the leaving words have to keep the
/// place they had in the old layout; a ControlTheme cannot animate parts of one line of text.
/// </remarks>
internal sealed class UsageWordText : Control
{
    public static readonly StyledProperty<double> FontSizeProperty = TextBlock.FontSizeProperty.AddOwner<UsageWordText>();
    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<UsageWordText, double>(nameof(LineHeight), 19.6);
    public static readonly StyledProperty<bool> IsMediumProperty =
        AvaloniaProperty.Register<UsageWordText, bool>(nameof(IsMedium));
    public static readonly StyledProperty<bool> IsSecondaryProperty =
        AvaloniaProperty.Register<UsageWordText, bool>(nameof(IsSecondary));
    public static readonly StyledProperty<double> MaxTextWidthProperty =
        AvaloniaProperty.Register<UsageWordText, double>(nameof(MaxTextWidth), double.PositiveInfinity);

    private sealed class Word
    {
        public string Text = string.Empty;
        public double X;
        public double Y;
        public double Width;
        public AnalyticsTween Alpha = new(1);
        public AnalyticsTween Rise = new(0);
    }

    private readonly AnalyticsPalette _palette = new();
    private readonly AnalyticsTextCache _cache = new(256);
    private readonly LiveLoop _loop;
    private List<Word> _words = [];
    private readonly List<Word> _leaving = [];
    private string _text = string.Empty;
    private double _width = double.PositiveInfinity;

    static UsageWordText()
    {
        AffectsMeasure<UsageWordText>(FontSizeProperty, LineHeightProperty, IsMediumProperty, MaxTextWidthProperty);
        AffectsRender<UsageWordText>(IsSecondaryProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<UsageWordText>(false);
    }

    public UsageWordText()
    {
        _loop = new LiveLoop(this, Advance) { MinimumLevel = ArcMotionLevel.Off };
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        ActualThemeVariantChanged += (_, _) => Invalidate();
        ResourcesChanged += (_, _) => Invalidate();
    }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public double LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }
    public bool IsMedium { get => GetValue(IsMediumProperty); set => SetValue(IsMediumProperty, value); }
    public bool IsSecondary { get => GetValue(IsSecondaryProperty); set => SetValue(IsSecondaryProperty, value); }
    public double MaxTextWidth { get => GetValue(MaxTextWidthProperty); set => SetValue(MaxTextWidthProperty, value); }

    /// <summary>The sentence shown (or heading for).</summary>
    public string Text => _text;

    /// <summary>How many words are leaving (tests).</summary>
    internal int LeavingCount => _leaving.Count;

    /// <summary>The words now, with the opacity each has (tests).</summary>
    internal IReadOnlyList<(string Text, double Alpha)> Words => _words.Select(w => (w.Text, w.Alpha.Value)).ToList();

    /// <summary>True while anything animates.</summary>
    internal bool IsMoving => _loop.IsActive;

    private void Invalidate()
    {
        _palette.Refresh(this);
        _cache.Clear();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private Typeface Face => IsMedium ? new Typeface(_palette.BodyFont, FontStyle.Normal, _palette.Medium) : new Typeface(_palette.BodyFont);

    /// <summary>Shows <paramref name="text"/>; with <paramref name="animate"/> only the changed words animate.</summary>
    public void SetText(string? text, bool animate)
    {
        text ??= string.Empty;
        if (text == _text)
        {
            return;
        }
        var old = _words;
        var next = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _text = text;
        var level = ArcMotion.GetEffectiveLevel(this);
        var run = animate && old.Count > 0 && level != ArcMotionLevel.Off && this.IsAttachedToVisualTree();
        var fresh = new List<Word>(next.Length);
        if (!run)
        {
            foreach (var w in next)
            {
                fresh.Add(new Word { Text = w });
            }
            _leaving.Clear();
            _words = fresh;
            Relayout();
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        // Longest common subsequence of words: those stay where the layout puts them, the rest swap.
        var a = old.Select(w => w.Text).ToArray();
        var lcs = new int[a.Length + 1, next.Length + 1];
        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = next.Length - 1; j >= 0; j--)
            {
                lcs[i, j] = a[i] == next[j] ? lcs[i + 1, j + 1] + 1 : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }
        var keptOld = new bool[a.Length];
        var keptNew = new Word?[next.Length];
        for (int i = 0, j = 0; i < a.Length && j < next.Length;)
        {
            if (a[i] == next[j])
            {
                keptOld[i] = true;
                keptNew[j] = old[i];
                i++;
                j++;
            }
            else if (lcs[i + 1, j] >= lcs[i, j + 1])
            {
                i++;
            }
            else
            {
                j++;
            }
        }
        var reduced = level == ArcMotionLevel.Reduced;
        var order = 0;
        for (var i = 0; i < a.Length; i++)
        {
            if (keptOld[i])
            {
                continue;
            }
            var w = old[i];
            var delay = TimeSpan.FromMilliseconds(reduced ? 0 : Math.Min(240, order * 40));
            w.Alpha.Start(0, reduced ? ArcMotionPresets.Instant : ArcMotionPresets.Fast, reduced ? AnalyticsEasings.EaseOut : AnalyticsEasings.Standard, delay);
            if (!reduced)
            {
                w.Rise.Start(-0.3, ArcMotionPresets.Fast, AnalyticsEasings.Standard, delay);
            }
            _leaving.Add(w);
            order++;
        }
        order = 0;
        for (var j = 0; j < next.Length; j++)
        {
            if (keptNew[j] is { } kept)
            {
                fresh.Add(kept);
                continue;
            }
            var w = new Word { Text = next[j] };
            w.Alpha.Jump(0);
            var delay = TimeSpan.FromMilliseconds(reduced ? 0 : Math.Min(240, order * 40));
            w.Alpha.Start(1, reduced ? ArcMotionPresets.Instant : ArcMotionPresets.Standard, reduced ? AnalyticsEasings.EaseOut : AnalyticsEasings.Enter, delay);
            if (!reduced)
            {
                w.Rise.Jump(0.35);
                w.Rise.Start(0, TimeSpan.FromMilliseconds(500), AnalyticsEasings.Enter, delay);
            }
            fresh.Add(w);
            order++;
        }
        // The leaving words keep the place of the old layout; the new ones take the new one.
        _words = fresh;
        Relayout();
        InvalidateMeasure();
        _loop.Start();
        InvalidateVisual();
    }

    private bool Advance(double dt)
    {
        var moving = false;
        foreach (var w in _words)
        {
            moving |= w.Alpha.Advance(dt);
            moving |= w.Rise.Advance(dt);
        }
        for (var i = _leaving.Count - 1; i >= 0; i--)
        {
            var w = _leaving[i];
            moving |= w.Alpha.Advance(dt);
            moving |= w.Rise.Advance(dt);
            if (!w.Alpha.IsRunning && w.Alpha.Value <= 0.001)
            {
                _leaving.RemoveAt(i);
            }
        }
        InvalidateVisual();
        return moving || _leaving.Count > 0;
    }

    private double SpaceWidth()
    {
        var face = Face;
        return _cache.Get("n n", face, FontSize, tabular: true).Width - _cache.Get("nn", face, FontSize, tabular: true).Width;
    }

    /// <summary>Flows the words into lines at the current width; returns the number of lines.</summary>
    private int Relayout()
    {
        if (!_palette.IsReady && this.IsAttachedToVisualTree())
        {
            _palette.Refresh(this);
        }
        var face = Face;
        var space = SpaceWidth();
        var limit = Math.Min(_width, MaxTextWidth);
        double x = 0;
        var line = 0;
        foreach (var w in _words)
        {
            w.Width = _cache.Get(w.Text, face, FontSize, tabular: true).Width;
            if (x > 0 && x + w.Width > limit + 0.01)
            {
                line++;
                x = 0;
            }
            w.X = x;
            w.Y = line * LineHeight;
            x += w.Width + space;
        }
        return Math.Max(1, line + 1);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _width = availableSize.Width;
        if (_words.Count == 0)
        {
            return new Size(0, LineHeight);
        }
        var lines = Relayout();
        var widest = _words.Max(w => w.X + w.Width);
        return new Size(Math.Min(double.IsInfinity(_width) ? widest : _width, Math.Min(MaxTextWidth, widest)), lines * LineHeight);
    }

    public override void Render(DrawingContext context)
    {
        if (!_palette.IsReady)
        {
            if (!this.IsAttachedToVisualTree())
            {
                return;
            }
            _palette.Refresh(this);
        }
        var face = Face;
        var color = IsSecondary ? _palette.TextSecondary : _palette.Foreground;
        foreach (var w in _leaving)
        {
            DrawWord(context, w, face, color);
        }
        foreach (var w in _words)
        {
            DrawWord(context, w, face, color);
        }
    }

    private void DrawWord(DrawingContext context, Word w, Typeface face, Color color)
    {
        var alpha = Math.Clamp(w.Alpha.Value, 0, 1);
        if (alpha <= 0.001)
        {
            return;
        }
        var text = _cache.Get(w.Text, face, FontSize, tabular: true);
        text.Draw(context, _cache.Brush(color, alpha), new Point(w.X, w.Y + (LineHeight - text.Height) / 2 + w.Rise.Value * FontSize));
    }
}
