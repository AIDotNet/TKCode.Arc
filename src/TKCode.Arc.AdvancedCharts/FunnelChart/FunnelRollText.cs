using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace TKCode.Arc;

/// <summary>
/// A number label that rolls when it changes: the old text leaves and the new one arrives along the direction of the
/// change. Both are cached <see cref="TextLayout"/>s, so a frame of the roll allocates nothing and any formatter works.
/// </summary>
internal sealed class FunnelRollText
{
    private TextLayout? _old;
    private TextLayout? _now;
    private double _direction = 1;

    public string Text { get; private set; } = string.Empty;

    public double Value { get; private set; }

    /// <summary>Replaces the text. With <paramref name="roll"/> the previous text stays for <see cref="Draw"/> to roll out.</summary>
    public void Set(string text, double value, TextLayout layout, bool roll)
    {
        _old?.Dispose();
        _old = null;
        _direction = value >= Value ? 1 : -1;
        if (roll)
        {
            _old = _now;
        }
        else
        {
            _now?.Dispose();
        }
        _now = layout;
        Text = text;
        Value = value;
    }

    public void Draw(DrawingContext context, double x, double y, double progress, double distance)
    {
        if (_now is null)
        {
            return;
        }
        if (_old is null || progress >= 0.999)
        {
            _now.Draw(context, new Point(x, y));
            return;
        }
        var t = Math.Clamp(progress, 0, 1);
        using (context.PushOpacity(1 - t))
        {
            _old.Draw(context, new Point(x, y - _direction * distance * t));
        }
        using (context.PushOpacity(t))
        {
            _now.Draw(context, new Point(x, y + _direction * distance * (1 - t)));
        }
    }
}
