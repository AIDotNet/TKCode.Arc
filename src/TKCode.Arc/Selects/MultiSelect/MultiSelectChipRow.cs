using TKCode.Arc.Primitives;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The read-only chip row in a <see cref="MultiSelect"/> trigger. Every chip sits in a slot whose width springs
/// open and closed (smooth), so neighbours travel with it and nothing overlaps (P5); the chip itself grows from
/// 0.9 out of a soft blur on a snappy spring (P26). The overflow chip "+N" rolls its count (P14).
/// Chip slots shrink (clipped) when the row runs out of room; the overflow chip never shrinks.
/// </summary>
internal sealed class MultiSelectChipRow : Panel
{
    public static readonly StyledProperty<IBrush?> ChipBackgroundProperty =
        AvaloniaProperty.Register<MultiSelectChipRow, IBrush?>(nameof(ChipBackground));

    public static readonly StyledProperty<IBrush?> ChipBorderBrushProperty =
        AvaloniaProperty.Register<MultiSelectChipRow, IBrush?>(nameof(ChipBorderBrush));

    public static readonly StyledProperty<IBrush?> ChipForegroundProperty =
        AvaloniaProperty.Register<MultiSelectChipRow, IBrush?>(nameof(ChipForeground));

    public static readonly StyledProperty<IBrush?> MoreForegroundProperty =
        AvaloniaProperty.Register<MultiSelectChipRow, IBrush?>(nameof(MoreForeground));

    public static readonly StyledProperty<double> ChipMaxWidthProperty =
        AvaloniaProperty.Register<MultiSelectChipRow, double>(nameof(ChipMaxWidth), 144);

    public static readonly StyledProperty<Thickness> ChipPaddingProperty =
        AvaloniaProperty.Register<MultiSelectChipRow, Thickness>(nameof(ChipPadding), new Thickness(7, 3));

    public static readonly StyledProperty<double> ChipSpacingProperty =
        AvaloniaProperty.Register<MultiSelectChipRow, double>(nameof(ChipSpacing), 5);

    public static readonly StyledProperty<double> ChipFontSizeProperty =
        AvaloniaProperty.Register<MultiSelectChipRow, double>(nameof(ChipFontSize), 12);

    public IBrush? ChipBackground { get => GetValue(ChipBackgroundProperty); set => SetValue(ChipBackgroundProperty, value); }
    public IBrush? ChipBorderBrush { get => GetValue(ChipBorderBrushProperty); set => SetValue(ChipBorderBrushProperty, value); }
    public IBrush? ChipForeground { get => GetValue(ChipForegroundProperty); set => SetValue(ChipForegroundProperty, value); }
    public IBrush? MoreForeground { get => GetValue(MoreForegroundProperty); set => SetValue(MoreForegroundProperty, value); }
    public double ChipMaxWidth { get => GetValue(ChipMaxWidthProperty); set => SetValue(ChipMaxWidthProperty, value); }
    public Thickness ChipPadding { get => GetValue(ChipPaddingProperty); set => SetValue(ChipPaddingProperty, value); }
    public double ChipSpacing { get => GetValue(ChipSpacingProperty); set => SetValue(ChipSpacingProperty, value); }
    public double ChipFontSize { get => GetValue(ChipFontSizeProperty); set => SetValue(ChipFontSizeProperty, value); }

    private static readonly object s_moreKey = new();
    private readonly List<Slot> _slots = new();
    private Slot? _more;

    /// <summary>Labels of the chips that are present (leaving chips excluded), in order.</summary>
    internal IReadOnlyList<string> ChipLabels => _slots.Where(s => s.IsPresent && !s.IsMore).Select(s => s.Label).ToList();

    /// <summary>The overflow count shown, or 0.</summary>
    internal int MoreCount => _more is { IsPresent: true } more ? more.Count : 0;

    static MultiSelectChipRow()
    {
        IsHitTestVisibleProperty.OverrideDefaultValue<MultiSelectChipRow>(false);
        ClipToBoundsProperty.OverrideDefaultValue<MultiSelectChipRow>(true);
    }

    /// <summary>Shows <paramref name="chips"/> (key, label) followed by "+<paramref name="remaining"/>".</summary>
    public void Update(IReadOnlyList<(object Key, string Label)> chips, int remaining)
    {
        var animate = IsLoaded && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        var full = ArcMotion.GetEffectiveLevel(this) == ArcMotionLevel.Full;
        var keep = new HashSet<object>(chips.Select(c => c.Key));
        foreach (var slot in _slots.Where(s => !s.IsMore && s.IsPresent && !keep.Contains(s.Key)).ToList())
        {
            Leave(slot, animate, full);
        }
        // Present chips are re-ordered to the selection order; a new chip enters at its place.
        var ordered = new List<Slot>();
        foreach (var (key, label) in chips)
        {
            var slot = _slots.FirstOrDefault(s => !s.IsMore && Equals(s.Key, key) && s.IsPresent);
            if (slot is null)
            {
                slot = CreateSlot(key, label, more: false);
                Enter(slot, animate, full);
            }
            else
            {
                slot.SetLabel(label);
            }
            ordered.Add(slot);
        }
        // Leaving chips keep their position relative to their neighbours until they are gone.
        var result = new List<Slot>();
        var queue = new Queue<Slot>(ordered);
        foreach (var slot in _slots.Where(s => !s.IsMore))
        {
            if (!slot.IsPresent)
            {
                result.Add(slot);
            }
            else if (queue.Count > 0)
            {
                result.Add(queue.Dequeue());
            }
        }
        while (queue.Count > 0)
        {
            result.Add(queue.Dequeue());
        }
        if (remaining > 0)
        {
            if (_more is null || !_more.IsPresent)
            {
                _more = CreateSlot(s_moreKey, string.Empty, more: true);
                _more.SetCount(remaining, animate: false);
                Enter(_more, animate, full);
            }
            else
            {
                _more.SetCount(remaining, animate);
            }
        }
        else if (_more is { IsPresent: true })
        {
            Leave(_more, animate, full);
        }
        _slots.Clear();
        _slots.AddRange(result);
        if (_more is not null)
        {
            _slots.Remove(_more);
            _slots.Add(_more);
        }
        foreach (var leaving in Children.OfType<Slot>().Where(s => !_slots.Contains(s)).ToList())
        {
            _slots.Add(leaving);
        }
        InvalidateMeasure();
    }

    private Slot CreateSlot(object key, string label, bool more)
    {
        var slot = new Slot(this, key, more);
        slot.SetLabel(label);
        Children.Add(slot);
        return slot;
    }

    private void Enter(Slot slot, bool animate, bool full)
    {
        slot.IsPresent = true;
        if (!animate)
        {
            slot.Jump(1);
            return;
        }
        slot.PlayEnter(full);
    }

    private void Leave(Slot slot, bool animate, bool full)
    {
        slot.IsPresent = false;
        if (!animate)
        {
            Remove(slot);
            return;
        }
        slot.PlayExit(full, () => Remove(slot));
    }

    private void Remove(Slot slot)
    {
        if (slot.IsPresent)
        {
            return;
        }
        _slots.Remove(slot);
        Children.Remove(slot);
        if (ReferenceEquals(slot, _more))
        {
            _more = null;
        }
        InvalidateMeasure();
    }

    internal void OnChipStyleChanged()
    {
        foreach (var slot in Children.OfType<Slot>())
        {
            slot.ApplyStyle();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ChipBackgroundProperty || change.Property == ChipBorderBrushProperty ||
            change.Property == ChipForegroundProperty || change.Property == MoreForegroundProperty ||
            change.Property == ChipPaddingProperty || change.Property == ChipFontSizeProperty ||
            change.Property == ChipMaxWidthProperty)
        {
            OnChipStyleChanged();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double height = 0;
        foreach (var slot in _slots)
        {
            slot.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            height = Math.Max(height, slot.DesiredSize.Height);
        }
        var total = _slots.Sum(s => s.DesiredSize.Width);
        return new Size(double.IsFinite(availableSize.Width) ? Math.Min(total, availableSize.Width) : total, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = _slots.Select(s => s.DesiredSize.Width).ToArray();
        var total = widths.Sum();
        if (total > finalSize.Width + 0.5)
        {
            // flex: 0 1 auto — chip slots give up room in proportion to their width; the overflow chip keeps its own.
            var fixedWidth = _slots.Where(s => s.IsMore).Sum(s => s.DesiredSize.Width);
            var shrinkable = total - fixedWidth;
            var room = Math.Max(0, finalSize.Width - fixedWidth);
            var factor = shrinkable > 0 ? Math.Min(1, room / shrinkable) : 1;
            for (var i = 0; i < _slots.Count; i++)
            {
                if (!_slots[i].IsMore)
                {
                    widths[i] *= factor;
                }
            }
        }
        double x = 0;
        for (var i = 0; i < _slots.Count; i++)
        {
            _slots[i].Arrange(new Rect(x, 0, widths[i], finalSize.Height));
            x += widths[i];
        }
        return finalSize;
    }

    /// <summary>One chip in its clipping slot.</summary>
    private sealed class Slot : Decorator
    {
        private readonly MultiSelectChipRow _row;
        private readonly Border _chip;
        private readonly TextBlock _label;
        private readonly SelectValueRoll? _count;
        private readonly ScaleTransform _scale = new(1, 1);
        private MotionValue<double>? _width;
        private MotionValue<double>? _opacity;
        private MotionValue<double>? _grow;
        private double _widthFraction = 1;

        public Slot(MultiSelectChipRow row, object key, bool more)
        {
            _row = row;
            Key = key;
            IsMore = more;
            ClipToBounds = true;
            _label = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Control content = _label;
            if (more)
            {
                _label.Text = "+";
                _count = new SelectValueRoll
                {
                    EnterTravel = 0.5,
                    ExitTravel = 0.5,
                    EnterBlur = 2,
                    ExitBlur = 2,
                    ExitDuration = ArcMotionPresets.Instant,
                    VerticalAlignment = VerticalAlignment.Center
                };
                content = new StackPanel { Orientation = Orientation.Horizontal, Children = { _label, _count } };
            }
            _chip = new Border
            {
                Child = content,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                RenderTransform = _scale,
                RenderTransformOrigin = RelativePoint.Center,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Child = _chip;
            ApplyStyle();
        }

        public object Key { get; }
        public bool IsMore { get; }
        public bool IsPresent { get; set; }
        public string Label => _label.Text ?? string.Empty;
        public int Count { get; private set; }

        public void SetLabel(string label)
        {
            if (!IsMore)
            {
                _label.Text = label;
            }
        }

        public void SetCount(int count, bool animate)
        {
            var direction = count >= Count ? 1 : -1;
            Count = count;
            _count?.Show(count.ToString(System.Globalization.CultureInfo.CurrentCulture), null, false, direction, animate: animate);
        }

        public void ApplyStyle()
        {
            _chip.Background = _row.ChipBackground;
            _chip.BorderBrush = _row.ChipBorderBrush;
            _chip.Padding = _row.ChipPadding;
            _chip.MaxWidth = _row.ChipMaxWidth;
            _chip.Margin = new Thickness(0, 0, _row.ChipSpacing, 0);
            var foreground = IsMore ? _row.MoreForeground ?? _row.ChipForeground : _row.ChipForeground;
            _label.Foreground = foreground;
            _label.FontSize = _row.ChipFontSize;
            _label.LineHeight = Math.Round(_row.ChipFontSize * 1.5);
            if (_count is not null)
            {
                _count.Foreground = foreground;
                TextElement.SetFontSize(_count, _row.ChipFontSize);
                _label.FontFeatures = s_tabular;
                _count.SetValue(TextElement.FontFeaturesProperty, s_tabular);
            }
        }

        private static readonly FontFeatureCollection s_tabular = new() { FontFeature.Parse("tnum") };

        public void Jump(double presence)
        {
            _width?.Jump(presence);
            _opacity?.Jump(presence);
            _grow?.Jump(presence);
            _widthFraction = presence;
            Opacity = presence;
            SetGrow(presence, presence < 1 ? 4 : 0);
            InvalidateMeasure();
        }

        public void PlayEnter(bool full)
        {
            EnsureValues();
            _widthFraction = full ? 0 : 1;
            _width!.Jump(_widthFraction);
            _opacity!.Jump(0);
            Opacity = 0;
            if (full)
            {
                _grow!.Jump(0);
                SetGrow(0, 4);
                _ = _width.AnimateTo(1, MotionSpec.Spring(ArcSpring.Smooth));
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Enter));
                _ = _grow.AnimateTo(1, MotionSpec.Spring(ArcSpring.Snappy));
                _blurIn = 4;
            }
            else
            {
                SetGrow(1, 0);
                _ = _opacity.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            InvalidateMeasure();
        }

        public void PlayExit(bool full, Action done)
        {
            EnsureValues();
            _ = _opacity!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            Task<bool> finished;
            if (full)
            {
                _blurIn = 0;
                _exiting = true;
                _ = _grow!.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
                finished = _width!.AnimateTo(0, MotionSpec.Spring(ArcSpring.Smooth));
            }
            else
            {
                finished = _opacity.AnimateTo(0, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
            finished.ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(done), TaskScheduler.Default);
        }

        private double _blurIn;
        private bool _exiting;

        private void EnsureValues()
        {
            if (_width is not null)
            {
                return;
            }
            _width = new MotionValue<double>(this, 1, MotionVectors.Double);
            _width.Changed += v =>
            {
                _widthFraction = Math.Max(0, v);
                InvalidateMeasure();
                _row.InvalidateMeasure();
            };
            _opacity = new MotionValue<double>(this, 1, MotionVectors.Double);
            _opacity.Changed += v => Opacity = Math.Clamp(v, 0, 1);
            _grow = new MotionValue<double>(this, 1, MotionVectors.Double);
            _grow.Changed += v => SetGrow(v, _exiting ? 2 * (1 - Math.Clamp(v, 0, 1)) : _blurIn * (1 - Math.Clamp(v, 0, 1)));
        }

        private void SetGrow(double progress, double blur)
        {
            var scale = 0.9 + 0.1 * progress;
            _scale.ScaleX = scale;
            _scale.ScaleY = scale;
            if (blur < 0.05)
            {
                _chip.Effect = null;
            }
            else
            {
                _chip.Effect = ArcBlur.Get(blur);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _chip.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            var size = _chip.DesiredSize;
            return new Size(size.Width * _widthFraction, size.Height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // The chip keeps its natural width while the slot clips, so the label never re-truncates mid-flight.
            var natural = _chip.DesiredSize;
            _chip.Arrange(new Rect(0, (finalSize.Height - natural.Height) / 2, natural.Width, natural.Height));
            return finalSize;
        }
    }
}
