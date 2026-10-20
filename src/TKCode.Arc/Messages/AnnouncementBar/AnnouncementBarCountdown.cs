using System.Globalization;
using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;

namespace TKCode.Arc;

/// <summary>
/// The live countdown of an announcement ("Ends in 1d 03:12:45"). It wakes just after each whole second so digits
/// change on the beat; a changed digit drops in from above while the old one falls away inside its cell (0.32s
/// spring, bounce 0.08). Drawn in <c>Render</c> from cached glyphs: one control for the whole countdown.
/// </summary>
internal sealed class AnnouncementBarCountdown : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<AnnouncementBarCountdown>();

    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<AnnouncementBarCountdown, IBrush?>(nameof(MutedForeground));

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<AnnouncementBarCountdown>();

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<AnnouncementBarCountdown>();

    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    private static readonly SpringSpec DropSpring = SpringSpec.FromVisual(0.32, 0.08);

    private struct Cell
    {
        public char Current;
        public char Previous;
        public bool Changed;
    }

    private readonly Cell[] _cells = new Cell[12];
    private readonly FormattedText?[] _digits = new FormattedText?[10];
    private FormattedText? _colon;
    private FormattedText? _dayMark;
    private FormattedText? _label;
    private string? _labelText;
    private int _cellCount;
    private int _dayDigits;
    private double _cellWidth;
    private double _lineHeight;
    private DispatcherTimer? _timer;
    private AnnouncementCountdown? _countdown;
    private Action? _onEnd;
    private bool _ended;
    private readonly MotionValue<double> _drop;
    private readonly MotionValue<double> _fade;

    static AnnouncementBarCountdown()
    {
        AffectsMeasure<AnnouncementBarCountdown>(FontFamilyProperty, FontSizeProperty);
        AffectsRender<AnnouncementBarCountdown>(ForegroundProperty, MutedForegroundProperty);
    }

    public AnnouncementBarCountdown()
    {
        _drop = new MotionValue<double>(this, 1, MotionVectors.Double);
        _drop.Changed += _ => InvalidateVisual();
        _fade = new MotionValue<double>(this, 1, MotionVectors.Double);
        _fade.Changed += _ => InvalidateVisual();
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Off);
    }

    /// <summary>The plain time currently shown (for tests).</summary>
    internal string DisplayText
    {
        get
        {
            var chars = new char[_cellCount];
            for (var i = 0; i < _cellCount; i++)
            {
                chars[i] = _cells[i].Current;
            }
            return new string(chars);
        }
    }

    internal bool HasEnded => _ended;

    public void Start(AnnouncementCountdown countdown, Action onEnd)
    {
        _countdown = countdown;
        _onEnd = onEnd;
        _ended = false;
        _cellCount = 0;
        Tick(animate: false);
        ScheduleTick();
    }

    public void Stop()
    {
        _timer?.Stop();
        _countdown = null;
        _onEnd = null;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_countdown is not null)
        {
            Tick(animate: false);
            ScheduleTick();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
    }

    private void ScheduleTick()
    {
        if (_countdown is null || _ended || TopLevel.GetTopLevel(this) is null)
        {
            return;
        }
        _timer ??= CreateTimer();
        // Wake just after each whole second so digits change on the beat.
        var now = DateTimeOffset.Now;
        _timer.Interval = TimeSpan.FromMilliseconds(1000 - now.Millisecond + 8);
        _timer.Start();
    }

    private DispatcherTimer CreateTimer()
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Tick(animate: true);
            ScheduleTick();
        };
        return timer;
    }

    internal void Tick(bool animate)
    {
        if (_countdown is not { } countdown)
        {
            return;
        }
        var remaining = countdown.To - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
            if (!_ended)
            {
                _ended = true;
                _onEnd?.Invoke();
            }
        }
        var total = (long)Math.Floor(remaining.TotalSeconds);
        var d = total / 86400;
        var h = total % 86400 / 3600;
        var m = total % 3600 / 60;
        var s = total % 60;
        var dayText = d > 0 ? d.ToString(CultureInfo.InvariantCulture) : string.Empty;
        Span<char> next = stackalloc char[12];
        var count = 0;
        foreach (var c in dayText)
        {
            next[count++] = c;
        }
        WriteTwo(next, ref count, h);
        WriteTwo(next, ref count, m);
        WriteTwo(next, ref count, s);
        var layoutChanged = count != _cellCount || dayText.Length != _dayDigits;
        var any = false;
        for (var i = 0; i < count; i++)
        {
            var changed = !layoutChanged && _cells[i].Current != next[i];
            _cells[i].Previous = layoutChanged ? next[i] : _cells[i].Current;
            _cells[i].Changed = changed;
            _cells[i].Current = next[i];
            any |= changed;
        }
        _cellCount = count;
        _dayDigits = dayText.Length;
        var spoken = d > 0 ? $"{d} days {h} hours {m} minutes" : $"{h} hours {m} minutes";
        AutomationProperties.SetName(this, spoken);
        if (layoutChanged)
        {
            InvalidateMeasure();
        }
        var level = ArcMotion.GetEffectiveLevel(this);
        if (!animate || !any || level == ArcMotionLevel.Off || !IsEffectivelyVisible)
        {
            _drop.Jump(1);
            _fade.Jump(1);
        }
        else
        {
            _drop.Jump(0);
            _fade.Jump(0);
            if (level == ArcMotionLevel.Full)
            {
                _ = _drop.AnimateTo(1, MotionSpec.Spring(DropSpring));
                _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
            }
            else
            {
                _drop.Jump(1);
                _ = _fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Instant, ArcEasings.Standard));
            }
        }
        InvalidateVisual();
    }

    private static void WriteTwo(Span<char> buffer, ref int count, long value)
    {
        buffer[count++] = (char)('0' + value / 10 % 10);
        buffer[count++] = (char)('0' + value % 10);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontFamilyProperty || change.Property == FontSizeProperty ||
            change.Property == ForegroundProperty || change.Property == MutedForegroundProperty)
        {
            Array.Clear(_digits);
            _colon = null;
            _dayMark = null;
            _label = null;
        }
    }

    private FormattedText Text(string text, FontWeight weight, IBrush? brush)
        => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
               new Typeface(FontFamily, FontStyle.Normal, weight), FontSize, brush ?? Brushes.Black);

    private void EnsureGlyphs()
    {
        var muted = MutedForeground ?? Foreground;
        for (var i = 0; i < 10; i++)
        {
            _digits[i] ??= Text(((char)('0' + i)).ToString(), FontWeight.Medium, Foreground);
        }
        _colon ??= Text(":", FontWeight.Normal, muted);
        _dayMark ??= Text("d", FontWeight.Normal, muted);
        var label = _countdown?.Label;
        if (!string.IsNullOrEmpty(label) && (_label is null || _labelText != label))
        {
            _label = Text(label, FontWeight.Normal, muted);
            _labelText = label;
        }
        else if (string.IsNullOrEmpty(label))
        {
            _label = null;
            _labelText = null;
        }
        _cellWidth = 0;
        foreach (var digit in _digits)
        {
            _cellWidth = Math.Max(_cellWidth, digit!.WidthIncludingTrailingWhitespace);
        }
        _lineHeight = Math.Max(_digits[0]!.Height, FontSize * 1.4);
    }

    private double LabelGap => _label is null ? 0 : 6;

    private double SeparatorWidth => _colon!.WidthIncludingTrailingWhitespace + 2;

    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureGlyphs();
        var width = (_label?.WidthIncludingTrailingWhitespace ?? 0) + LabelGap + _cellCount * _cellWidth + 2 * SeparatorWidth;
        if (_dayDigits > 0)
        {
            width += 1 + _dayMark!.WidthIncludingTrailingWhitespace + 0.4 * FontSize;
        }
        return new Size(width, _lineHeight);
    }

    public override void Render(DrawingContext context)
    {
        if (_cellCount == 0)
        {
            return;
        }
        EnsureGlyphs();
        var top = (Bounds.Height - _digits[0]!.Height) / 2;
        var x = 0.0;
        if (_label is not null)
        {
            context.DrawText(_label, new Point(x, top));
            x += _label.WidthIncludingTrailingWhitespace + LabelGap;
        }
        var drop = _drop.Value;
        var fade = Math.Clamp(_fade.Value, 0, 1);
        var cellHeight = Bounds.Height;
        for (var i = 0; i < _cellCount; i++)
        {
            if (_dayDigits > 0 && i == _dayDigits)
            {
                context.DrawText(_dayMark!, new Point(x + 1, top));
                x += 1 + _dayMark!.WidthIncludingTrailingWhitespace + 0.4 * FontSize;
            }
            else if (i == _dayDigits + 2 || i == _dayDigits + 4)
            {
                context.DrawText(_colon!, new Point(x + 1, top));
                x += SeparatorWidth;
            }
            ref var cell = ref _cells[i];
            var current = _digits[cell.Current - '0']!;
            var offset = (_cellWidth - current.WidthIncludingTrailingWhitespace) / 2;
            if (!cell.Changed || (drop >= 1 && fade >= 1))
            {
                context.DrawText(current, new Point(x + offset, top));
                x += _cellWidth;
                continue;
            }
            using (context.PushClip(new Rect(x, 0, _cellWidth, cellHeight)))
            {
                // The new digit drops in from above; the old one falls away below.
                using (context.PushOpacity(fade))
                {
                    context.DrawText(current, new Point(x + offset, top - 0.7 * cellHeight * (1 - drop)));
                }
                var previous = _digits[cell.Previous - '0']!;
                var previousOffset = (_cellWidth - previous.WidthIncludingTrailingWhitespace) / 2;
                using (context.PushOpacity(1 - fade))
                {
                    context.DrawText(previous, new Point(x + previousOffset, top + 0.7 * cellHeight * drop));
                }
            }
            x += _cellWidth;
        }
    }
}
