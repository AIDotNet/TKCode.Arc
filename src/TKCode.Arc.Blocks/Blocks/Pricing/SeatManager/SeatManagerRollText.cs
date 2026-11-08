using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// One line of <see cref="SeatManager"/> text with rolling numbers in it: a template such as "{0} of {1} seats in use"
/// becomes plain text runs and <see cref="SeatManagerRoll"/> strips for the placeholders. The numbers can take their own
/// weight and color (Arc sets them in medium Foreground inside TextSecondary text).
/// </summary>
internal sealed class SeatManagerRollText : StackPanel
{
    private string? _template;
    private readonly List<(SeatManagerRoll Roll, int Index)> _rolls = new();
    private readonly List<TextBlock> _texts = new();
    private readonly string?[] _values = new string?[2];
    private double _lineHeight = 20;

    public SeatManagerRollText()
    {
        Orientation = Orientation.Horizontal;
    }

    /// <summary>The rolling strips, in order (tests).</summary>
    internal IEnumerable<SeatManagerRoll> Rolls => _rolls.Select(r => r.Roll);

    /// <summary>The whole line as read aloud.</summary>
    internal string PlainText { get; private set; } = string.Empty;

    public double LineHeight
    {
        get => _lineHeight;
        set
        {
            _lineHeight = value;
            foreach (var t in _texts)
            {
                t.LineHeight = value;
            }
            foreach (var (roll, _) in _rolls)
            {
                roll.LineHeight = value;
            }
        }
    }

    /// <summary>Brush of the numbers; null inherits the text color.</summary>
    public static readonly StyledProperty<IBrush?> NumberBrushProperty =
        AvaloniaProperty.Register<SeatManagerRollText, IBrush?>(nameof(NumberBrush));

    /// <summary>Weight of the numbers.</summary>
    public static readonly StyledProperty<FontWeight> NumberWeightProperty =
        AvaloniaProperty.Register<SeatManagerRollText, FontWeight>(nameof(NumberWeight), FontWeight.Normal);

    public IBrush? NumberBrush
    {
        get => GetValue(NumberBrushProperty);
        set => SetValue(NumberBrushProperty, value);
    }

    public FontWeight NumberWeight
    {
        get => GetValue(NumberWeightProperty);
        set => SetValue(NumberWeightProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NumberBrushProperty || change.Property == NumberWeightProperty)
        {
            foreach (var (roll, _) in _rolls)
            {
                ApplyNumber(roll);
            }
        }
    }

    /// <summary>Shows <paramref name="template"/> with <paramref name="first"/> (and <paramref name="second"/>) rolling into place.</summary>
    public void Show(string template, string first, string? second = null)
    {
        _values[0] = first;
        _values[1] = second;
        if (!string.Equals(template, _template, StringComparison.Ordinal))
        {
            _template = template;
            Build(template);
        }
        foreach (var (roll, index) in _rolls)
        {
            roll.Text = _values[index] ?? string.Empty;
        }
        PlainText = template.Replace("{0}", first, StringComparison.Ordinal).Replace("{1}", second ?? string.Empty, StringComparison.Ordinal);
        AutomationProperties.SetName(this, PlainText);
    }

    private void Build(string template)
    {
        // Keep the strips that survive (same order) so their numbers still roll when only the words change.
        var spare = new Queue<SeatManagerRoll>(_rolls.Select(r => r.Roll));
        Children.Clear();
        _rolls.Clear();
        _texts.Clear();
        var i = 0;
        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            if (open < 0 || open + 2 >= template.Length || template[open + 2] != '}' || template[open + 1] is not ('0' or '1'))
            {
                if (open < 0)
                {
                    AddText(template[i..]);
                    break;
                }
                AddText(template[i..(open + 1)]);
                i = open + 1;
                continue;
            }
            if (open > i)
            {
                AddText(template[i..open]);
            }
            var roll = spare.Count > 0 ? spare.Dequeue() : new SeatManagerRoll();
            roll.LineHeight = _lineHeight;
            roll.VerticalAlignment = VerticalAlignment.Center;
            ApplyNumber(roll);
            AutomationProperties.SetAccessibilityView(roll, AccessibilityView.Raw);
            _rolls.Add((roll, template[open + 1] - '0'));
            Children.Add(roll);
            i = open + 3;
        }
    }

    private void AddText(string text)
    {
        if (text.Length == 0)
        {
            return;
        }
        // Spaces at the edges of a run would be trimmed as trailing whitespace: keep their width.
        if (text[0] == ' ')
        {
            text = ' ' + text[1..];
        }
        if (text[^1] == ' ')
        {
            text = text[..^1] + ' ';
        }
        var block = new TextBlock
        {
            Text = text,
            LineHeight = _lineHeight,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetAccessibilityView(block, AccessibilityView.Raw);
        _texts.Add(block);
        Children.Add(block);
    }

    private void ApplyNumber(SeatManagerRoll roll)
    {
        roll.FontWeight = NumberWeight;
        if (NumberBrush is { } brush)
        {
            roll.Foreground = brush;
        }
        else
        {
            roll.ClearValue(SeatManagerRoll.ForegroundProperty);
        }
    }
}
