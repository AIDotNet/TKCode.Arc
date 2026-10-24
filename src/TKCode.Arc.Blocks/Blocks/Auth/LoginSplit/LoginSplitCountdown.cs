using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// The "m:ss" of the resend button. Each character is its own column; when the seconds change only the columns whose
/// character changed roll: counting down, the new digit drops in from .7em above while the old one rolls .7em down and
/// blurs out (counting up after a restart, the other way). Travel rides Arc's snappy spring without bounce; the fades take
/// 160 ms. Text style is inherited (font, size, color). Under reduced motion or Off the digits change at once.
/// </summary>
/// <remarks>Code-driven: each column keeps its leaving glyph alive outside layout while the new one takes its place.</remarks>
internal sealed class LoginSplitCountdown : Control
{
    public static readonly StyledProperty<int> SecondsProperty =
        AvaloniaProperty.Register<LoginSplitCountdown, int>(nameof(Seconds));

    private readonly List<Column> _columns = new();
    private int _shown = -1;

    /// <summary>The seconds shown.</summary>
    public int Seconds { get => GetValue(SecondsProperty); set => SetValue(SecondsProperty, value); }

    /// <summary>The text the columns read now (tests read it).</summary>
    internal string Text => string.Concat(_columns.Select(c => c.Character));

    /// <summary>Glyphs still rolling out (tests read it).</summary>
    internal int LeavingCount => _columns.Sum(c => c.Leaving.Count);

    private sealed class Column
    {
        public char Character;
        public required TextBlock Current;
        public readonly List<TextBlock> Leaving = new();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SecondsProperty)
        {
            Show(change.GetNewValue<int>());
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_shown < 0)
        {
            Show(Seconds);
        }
    }

    private void Show(int seconds)
    {
        var text = LoginSplitIdentity.Countdown(seconds);
        var animate = _shown >= 0 && IsLoaded && ArcMotion.IsFull(this);
        var direction = _shown >= 0 && seconds > _shown ? -1 : 1;
        _shown = seconds;
        // Columns are matched from the right so "9:59" → "10:00" keeps the seconds' places.
        while (_columns.Count < text.Length)
        {
            var glyph = Glyph(' ');
            _columns.Insert(0, new Column { Character = ' ', Current = glyph });
        }
        while (_columns.Count > text.Length)
        {
            Remove(_columns[0]);
            _columns.RemoveAt(0);
        }
        for (var i = 0; i < text.Length; i++)
        {
            var column = _columns[i];
            var character = text[i];
            if (column.Character == character)
            {
                continue;
            }
            column.Character = character;
            if (!animate || column.Current.Text == " ")
            {
                column.Current.Text = character.ToString();
                continue;
            }
            var leaving = column.Current;
            column.Leaving.Add(leaving);
            column.Current = Glyph(character);
            Roll(column, column.Current, leaving, direction);
        }
        InvalidateMeasure();
    }

    private TextBlock Glyph(char character)
    {
        var glyph = new TextBlock { Text = character.ToString(), RenderTransform = new TranslateTransform() };
        LogicalChildren.Add(glyph);
        VisualChildren.Add(glyph);
        return glyph;
    }

    private void Remove(Column column)
    {
        foreach (var glyph in column.Leaving.Append(column.Current).ToArray())
        {
            LogicalChildren.Remove(glyph);
            VisualChildren.Remove(glyph);
        }
        column.Leaving.Clear();
    }

    private async void Roll(Column column, TextBlock entering, TextBlock leaving, int direction)
    {
        var travel = LoginSplitMotion.RollTravel * (entering.FontSize > 0 ? entering.FontSize : 12);
        var enterMove = (TranslateTransform)entering.RenderTransform!;
        var leaveMove = (TranslateTransform)leaving.RenderTransform!;
        var leaveFrom = leaveMove.Y;
        enterMove.Y = -direction * travel;
        entering.Opacity = 0;
        entering.Effect = ArcBlur.Get(2);

        var move = new MotionValue<double>(this, 0, MotionVectors.Double);
        move.Changed += p =>
        {
            enterMove.Y = -direction * travel * (1 - p);
            leaveMove.Y = leaveFrom + direction * travel * p;
        };
        var fade = new MotionValue<double>(this, 0, MotionVectors.Double);
        fade.Changed += p =>
        {
            var t = Math.Clamp(p, 0, 1);
            entering.Opacity = t;
            entering.Effect = ArcBlur.Get(2 * (1 - t));
            leaving.Opacity = Math.Min(leaving.Opacity, 1 - t);
            leaving.Effect = ArcBlur.Get(2 * t);
        };
        _ = fade.AnimateTo(1, MotionSpec.Tween(ArcMotionPresets.Fast, ArcEasings.Standard));
        // Awaited on the UI thread: the leaving glyph goes once the roll has settled (or was cut short).
        await move.AnimateTo(1, MotionSpec.Spring(LoginSplitMotion.Snappy));
        if (column.Leaving.Remove(leaving))
        {
            LogicalChildren.Remove(leaving);
            VisualChildren.Remove(leaving);
        }
        move.Dispose();
        fade.Dispose();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = 0.0;
        var height = 0.0;
        foreach (var column in _columns)
        {
            foreach (var glyph in column.Leaving)
            {
                glyph.Measure(Size.Infinity);
            }
            column.Current.Measure(Size.Infinity);
            width += column.Current.DesiredSize.Width;
            height = Math.Max(height, column.Current.DesiredSize.Height);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        foreach (var column in _columns)
        {
            var size = column.Current.DesiredSize;
            column.Current.Arrange(new Rect(x, 0, size.Width, finalSize.Height));
            foreach (var glyph in column.Leaving)
            {
                var old = glyph.DesiredSize;
                glyph.Arrange(new Rect(x + (size.Width - old.Width) / 2, 0, old.Width, finalSize.Height));
            }
            x += size.Width;
        }
        return finalSize;
    }
}
