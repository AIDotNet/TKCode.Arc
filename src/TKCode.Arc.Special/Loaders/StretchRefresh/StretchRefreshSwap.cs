using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>How <see cref="StretchRefreshSwap"/> replaces its content.</summary>
internal enum StretchRefreshSwapMode
{
    /// <summary>
    /// Status and subtitle text (Arc's rise swap, measured): the old text lifts 0.3 em, blurs to 2 px and fades in 120 ms
    /// (.22, 1, .36, 1); the new one rises from 0.35 em out of a 4 px blur over 240 ms (.16, 1, .3, 1). Both start
    /// together and stack; the slot takes the new size at once.
    /// </summary>
    Text,

    /// <summary>
    /// The header glyph: the old one shrinks to 0.5, blurs to 2 px and fades in 120 ms (ease-out); the new one fades in
    /// out of a 2 px blur over 160 ms (.16, 1, .3, 1) while it grows from 0.5 on a quick critically damped spring.
    /// </summary>
    Icon
}

/// <summary>
/// The content swap of <see cref="StretchRefresh"/> (status line, subtitle, header glyph). Core's
/// <c>ArcContentSwap</c> modes do not match the measured curves (its blur swap rises 4 px on one curve for both sides),
/// so this host runs its own: every entry has one fade value (and, for glyphs, one growth spring) on
/// <see cref="MotionValue{T}"/>, written to the entry's opacity, blur and transform each frame. Reduced motion keeps
/// only the fades; motion off swaps at once.
/// </summary>
internal sealed class StretchRefreshSwap : Control
{
    public static readonly StyledProperty<StretchRefreshSwapMode> ModeProperty =
        AvaloniaProperty.Register<StretchRefreshSwap, StretchRefreshSwapMode>(nameof(Mode));

    public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
        AvaloniaProperty.Register<StretchRefreshSwap, HorizontalAlignment>(nameof(HorizontalContentAlignment), HorizontalAlignment.Left);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<StretchRefreshSwap>();

    public static readonly StyledProperty<double> FontSizeProperty =
        Avalonia.Controls.Documents.TextElement.FontSizeProperty.AddOwner<StretchRefreshSwap>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        Avalonia.Controls.Documents.TextElement.FontWeightProperty.AddOwner<StretchRefreshSwap>();

    public static readonly StyledProperty<double> LineHeightProperty =
        AvaloniaProperty.Register<StretchRefreshSwap, double>(nameof(LineHeight), double.NaN);

    internal static readonly FontFeatureCollection Tabular = new() { FontFeature.Parse("tnum") };

    private static readonly Easing EaseOut = new SplineEasing(0, 0, 0.58, 1);
    private static readonly SpringSpec GrowSpring = SpringSpec.FromVisual(0.26, 0);

    private static readonly MotionSpec s_textEnter = MotionSpec.Tween(ArcMotionPresets.Standard, ArcEasings.Enter);
    private static readonly MotionSpec s_iconEnter = MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter);
    private static readonly MotionSpec s_textExit = MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard);
    private static readonly MotionSpec s_iconExit = MotionSpec.Tween(ArcMotionPresets.Instant, EaseOut);
    private static readonly MotionSpec s_grow = MotionSpec.Spring(GrowSpring);

    private readonly List<Entry> _entries = new();
    private readonly Stack<Entry> _free = new();
    private Entry? _current;
    private object? _key;

    static StretchRefreshSwap()
    {
        AffectsArrange<StretchRefreshSwap>(HorizontalContentAlignmentProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<StretchRefreshSwap>(false);
    }

    public StretchRefreshSwapMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    /// <summary>The line height of the text entries (NaN: the font's).</summary>
    public double LineHeight
    {
        get => GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    /// <summary>The key of the content shown now (the text, or the caller's key for a glyph).</summary>
    public object? Key => _key;

    /// <summary>The visible text (Text mode), for tests and accessibility.</summary>
    public string? Text => _key as string;

    /// <summary>How many entries are on screen (the current one and any still leaving).</summary>
    internal int EntryCount => _entries.Count;

    /// <summary>Shows <paramref name="text"/>; the same text again does nothing.</summary>
    public void ShowText(string? text, bool animate = true)
    {
        text ??= string.Empty;
        if (_current is not null && Equals(_key, text))
        {
            return;
        }
        var entry = Rent();
        if (entry.Holder.Child is TextBlock pooled)
        {
            pooled.Text = text;
            pooled.LineHeight = LineHeight;
        }
        else
        {
            entry.Holder.Child = new TextBlock
            {
                Text = text,
                FontFeatures = Tabular,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                LineHeight = LineHeight
            };
        }
        Present(text, entry, animate);
    }

    /// <summary>Shows <paramref name="content"/> under <paramref name="key"/>; the same key again does nothing.</summary>
    public void Show(object key, Control content, bool animate = true)
    {
        if (_current is not null && Equals(_key, key))
        {
            return;
        }
        // The same glyph shown again while its previous entry is still leaving: finish that entry first.
        if (content.Parent is Decorator holder)
        {
            var found = false;
            for (var i = 0; i < _entries.Count; i++)
            {
                if (ReferenceEquals(_entries[i].Holder, holder))
                {
                    Remove(_entries[i]);
                    found = true;
                    break;
                }
            }
            if (!found || ReferenceEquals(holder.Child, content))
            {
                // Held by another (old template's) swap, or by a pooled text entry: take it back.
                holder.Child = null;
            }
        }
        var entry = Rent();
        entry.Holder.Child = content;
        Present(key, entry, animate);
    }

    /// <summary>A pooled entry (at most two are kept: one showing, one leaving), or a new one.</summary>
    private Entry Rent()
    {
        var entry = _free.Count > 0 ? _free.Pop() : new Entry(this, Mode);
        entry.Reset(ArcMotion.IsFull(this));
        return entry;
    }

    private void Present(object key, Entry entry, bool animate)
    {
        _key = key;
        var full = ArcMotion.IsFull(this);
        var moving = animate && _current is not null && this.IsAttachedToVisualTree() && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        if (_current is { } old)
        {
            Leave(old, moving, full);
        }
        _entries.Add(entry);
        _current = entry;
        LogicalChildren.Add(entry.Holder);
        VisualChildren.Add(entry.Holder);
        InvalidateMeasure();
        if (!moving)
        {
            entry.Fade.Jump(1);
            entry.Grow?.Jump(1);
            entry.Write(full: false, leaving: false);
            return;
        }
        entry.Fade.Jump(0);
        entry.Grow?.Jump(0.5);
        entry.Write(full, leaving: false);
        if (Mode == StretchRefreshSwapMode.Text)
        {
            _ = entry.Fade.AnimateTo(1, s_textEnter);
        }
        else
        {
            _ = entry.Fade.AnimateTo(1, s_iconEnter);
            if (full)
            {
                _ = entry.Grow!.AnimateTo(1, s_grow);
            }
            else
            {
                entry.Grow!.Jump(1);
            }
        }
    }

    private void Leave(Entry entry, bool moving, bool full)
    {
        entry.Leaving = true;
        entry.Full = full;
        if (!moving)
        {
            Remove(entry);
            return;
        }
        var spec = Mode == StretchRefreshSwapMode.Text ? s_textExit : s_iconExit;
        entry.Grow?.Stop();
        entry.Fade.Stop();
        entry.FadeFrom = entry.Fade.Value;
        _ = Finish(entry, entry.Generation, entry.Fade.AnimateTo(0, spec));
    }

    private async Task Finish(Entry entry, int generation, Task<bool> fading)
    {
        await fading;
        // The entry may have been finished early and reused since.
        if (entry.Generation == generation)
        {
            Remove(entry);
        }
    }

    private void Remove(Entry entry)
    {
        if (!_entries.Remove(entry))
        {
            return;
        }
        entry.Generation++;
        entry.Fade.Stop();
        entry.Grow?.Stop();
        VisualChildren.Remove(entry.Holder);
        LogicalChildren.Remove(entry.Holder);
        if (Mode == StretchRefreshSwapMode.Icon)
        {
            // Give the caller's glyph back.
            entry.Holder.Child = null;
        }
        if (_free.Count < 2)
        {
            _free.Push(entry);
        }
        else
        {
            entry.Fade.Dispose();
            entry.Grow?.Dispose();
        }
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = default(Size);
        foreach (var entry in _entries)
        {
            entry.Holder.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            if (ReferenceEquals(entry, _current))
            {
                size = entry.Holder.DesiredSize;
            }
        }
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var entry in _entries)
        {
            var w = entry.Holder.DesiredSize.Width;
            var x = HorizontalContentAlignment switch
            {
                HorizontalAlignment.Right => finalSize.Width - w,
                HorizontalAlignment.Center or HorizontalAlignment.Stretch => (finalSize.Width - w) / 2,
                _ => 0
            };
            entry.Holder.Arrange(new Rect(x, 0, w, finalSize.Height));
        }
        return finalSize;
    }

    /// <summary>One shown or leaving content and its motion.</summary>
    private sealed class Entry
    {
        private readonly StretchRefreshSwap _owner;
        private readonly StretchRefreshSwapMode _mode;
        private readonly TranslateTransform _shift = new();
        private readonly ScaleTransform _scale = new();

        public Entry(StretchRefreshSwap owner, StretchRefreshSwapMode mode)
        {
            _owner = owner;
            _mode = mode;
            Holder = new Decorator
            {
                RenderTransformOrigin = RelativePoint.Center,
                RenderTransform = new TransformGroup { Children = { _scale, _shift } }
            };
            Fade = new MotionValue<double>(owner, 0, MotionVectors.Double);
            Fade.Changed += _ => Write(Full, Leaving);
            if (mode == StretchRefreshSwapMode.Icon)
            {
                Grow = new MotionValue<double>(owner, 0.5, MotionVectors.Double);
                Grow.Changed += _ => Write(Full, Leaving);
            }
            Full = ArcMotion.IsFull(owner);
        }

        public Decorator Holder { get; }

        public MotionValue<double> Fade { get; }

        public MotionValue<double>? Grow { get; }

        public bool Leaving { get; set; }

        public bool Full { get; set; }

        public double FadeFrom { get; set; } = 1;

        /// <summary>Bumped whenever the entry leaves the screen, so a stale leave cannot remove its next use.</summary>
        public int Generation { get; set; }

        public void Reset(bool full)
        {
            Leaving = false;
            FadeFrom = 1;
            Full = full;
            Holder.Opacity = 1;
            Holder.Effect = null;
            _shift.Y = 0;
            _scale.ScaleX = _scale.ScaleY = 1;
        }

        public void Write(bool full, bool leaving)
        {
            var f = Math.Clamp(Fade.Value, 0, 1);
            Holder.Opacity = f;
            if (!full)
            {
                Holder.Effect = null;
                _shift.Y = 0;
                _scale.ScaleX = _scale.ScaleY = 1;
                return;
            }
            var em = (Holder.Child as TextBlock)?.FontSize ?? 12;
            if (_mode == StretchRefreshSwapMode.Text)
            {
                if (leaving)
                {
                    var q = FadeFrom <= 0 ? 1 : 1 - f / FadeFrom;
                    Holder.Effect = ArcBlur.Get(2 * q);
                    _shift.Y = -0.3 * em * q;
                }
                else
                {
                    Holder.Effect = ArcBlur.Get(4 * (1 - f));
                    _shift.Y = 0.35 * em * (1 - f);
                }
                return;
            }
            if (leaving)
            {
                var q = FadeFrom <= 0 ? 1 : 1 - f / FadeFrom;
                Holder.Effect = ArcBlur.Get(2 * q);
                _scale.ScaleX = _scale.ScaleY = 1 - 0.5 * q;
            }
            else
            {
                Holder.Effect = ArcBlur.Get(2 * (1 - f));
                var g = Grow?.Value ?? 1;
                _scale.ScaleX = _scale.ScaleY = g;
            }
            _ = _owner;
        }
    }
}
