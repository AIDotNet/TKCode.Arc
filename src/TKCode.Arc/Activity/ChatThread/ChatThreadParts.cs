using TKCode.Arc.Motion;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>The kind of small button a <see cref="ChatThreadButton"/> is (each has Arc's own geometry and colors).</summary>
internal enum ChatThreadButtonRole
{
    /// <summary>The add-reaction button beside a bubble (28px, shown on row hover or focus).</summary>
    React,

    /// <summary>An emoji in the reaction picker (34px, 19px emoji).</summary>
    Emoji,

    /// <summary>Retry on a failed message.</summary>
    Retry,

    /// <summary>The "new messages" / "Jump to latest" pill.</summary>
    Pill,

    /// <summary>The composer's attach button (40px).</summary>
    Attach,

    /// <summary>The composer's send button (32px; accent when <see cref="ChatThreadButton.IsReady"/>).</summary>
    Send,

    /// <summary>The remove button on a pending file (20px).</summary>
    Remove,

    /// <summary>A file attachment row that opens the file.</summary>
    File,

    /// <summary>A picture tile that opens the picture.</summary>
    Image
}

/// <summary>
/// The small buttons of <see cref="ChatThread"/> and <see cref="ChatComposer"/>: one Avalonia button whose theme gives
/// each <see cref="Role"/> Arc's geometry, colors, hover and press feedback, and a focus ring.
/// </summary>
internal sealed class ChatThreadButton : AvaloniaButton
{
    public static readonly StyledProperty<ChatThreadButtonRole> RoleProperty =
        AvaloniaProperty.Register<ChatThreadButton, ChatThreadButtonRole>(nameof(Role));

    /// <summary>Send: there is something to send (accent fill). Arc keeps the button focusable either way (aria-disabled).</summary>
    public static readonly StyledProperty<bool> IsReadyProperty =
        AvaloniaProperty.Register<ChatThreadButton, bool>(nameof(IsReady));

    /// <summary>React: its picker is open (holds the hover look).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ChatThreadButton, bool>(nameof(IsOpen));

    public ChatThreadButtonRole Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public bool IsReady { get => GetValue(IsReadyProperty); set => SetValue(IsReadyProperty, value); }
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    protected override Type StyleKeyOverride => typeof(ChatThreadButton);

    /// <summary>A button showing a Lucide glyph.</summary>
    public static ChatThreadButton WithGlyph(ChatThreadButtonRole role, Geometry glyph, double size, double stroke, string name)
    {
        var button = new ChatThreadButton
        {
            Role = role,
            Content = new ArcGlyph { Data = glyph, Size = size, StrokeThickness = stroke, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }
        };
        AutomationProperties.SetName(button, name);
        return button;
    }
}

/// <summary>
/// Three dots that breathe in sequence (Arc <c>@keyframes breathe</c>: 1.2s ease-in-out, 160ms apart; opacity .35→1 and
/// 3px up at 30%). Drawn in <see cref="Render"/> and driven by a <see cref="LiveLoop"/>, so it runs only while visible
/// at full motion; otherwise the dots rest at 0.6 opacity.
/// </summary>
internal sealed class ChatThreadTypingDots : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<ChatThreadTypingDots, IBrush?>(nameof(Fill));

    public IBrush? Fill { get => GetValue(FillProperty); set => SetValue(FillProperty, value); }

    private const double Dot = 6;
    private const double Gap = 4;
    private const double Period = 1.2;
    private const double Stagger = 0.16;

    private readonly LiveLoop _loop;
    private double _time;
    private bool _running;

    static ChatThreadTypingDots()
    {
        AffectsRender<ChatThreadTypingDots>(FillProperty);
    }

    public ChatThreadTypingDots()
    {
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        _loop = new LiveLoop(this, Step);
        _loop.RunningChanged += running =>
        {
            _running = running;
            InvalidateVisual();
        };
        _loop.Start();
    }

    /// <summary>True while the dots are animating (tests).</summary>
    public bool IsBreathing => _running;

    /// <summary>Seconds of breathing so far.</summary>
    public double Time => _time;

    private bool Step(double seconds)
    {
        _time += seconds;
        InvalidateVisual();
        return true;
    }

    protected override Size MeasureOverride(Size availableSize) => new(3 * Dot + 2 * Gap, Dot + 3);

    /// <summary>Opacity and lift of one dot at <paramref name="time"/> seconds (keyframes 0/60/100% rest, 30% peak).</summary>
    internal static (double Opacity, double Lift) Sample(double time, int index)
    {
        var local = time - index * Stagger;
        if (local < 0)
        {
            return (0.35, 0);
        }
        var phase = local % Period / Period;
        double t;
        if (phase < 0.3)
        {
            t = InOut(phase / 0.3);
        }
        else if (phase < 0.6)
        {
            t = 1 - InOut((phase - 0.3) / 0.3);
        }
        else
        {
            t = 0;
        }
        return (0.35 + 0.65 * t, 3 * t);
    }

    private static double InOut(double t) => ArcEasings.InOut.Ease(Math.Clamp(t, 0, 1));

    public override void Render(DrawingContext context)
    {
        var fill = Fill;
        if (fill is null)
        {
            return;
        }
        var top = 3.0;
        for (var i = 0; i < 3; i++)
        {
            var (opacity, lift) = _running ? Sample(_time, i) : (0.6, 0);
            using (context.PushOpacity(opacity))
            {
                var x = i * (Dot + Gap) + Dot / 2;
                context.DrawEllipse(fill, null, new Point(x, top + Dot / 2 - lift), Dot / 2, Dot / 2);
            }
        }
    }
}

/// <summary>
/// Hosts a bubble's text and sizes it like a CSS bubble: as wide as its longest line, up to the width it is given
/// (Arc's <c>max-width: 100%</c>). Markdown documents report the full available width, so the body measures its child
/// unconstrained first to learn its natural width.
/// </summary>
internal sealed class ChatThreadBubbleBody : Decorator
{
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is not { } child)
        {
            return default;
        }
        var padding = Padding;
        var inner = availableSize.Deflate(padding);
        if (child is Markdown && double.IsFinite(inner.Width))
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var natural = child.DesiredSize.Width;
            var width = natural > 0 && double.IsFinite(natural) ? Math.Min(Math.Ceiling(natural), inner.Width) : inner.Width;
            child.Measure(new Size(width, double.PositiveInfinity));
            return new Size(Math.Min(width, child.DesiredSize.Width), child.DesiredSize.Height).Inflate(padding);
        }
        child.Measure(inner);
        return child.DesiredSize.Inflate(padding);
    }
}

/// <summary>
/// Decides whether a message needs the Markdown renderer: text without any Markdown syntax renders the same as plain
/// text (line breaks are kept either way), so it takes the plain path, which measures synchronously.
/// </summary>
internal static class ChatThreadMarkdownSniffer
{
    private static readonly char[] s_inline = ['*', '_', '`', '[', ']', '<', '>', '#', '|', '~', '\\', '&', '!'];

    public static bool HasMarkdown(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        if (text.IndexOfAny(s_inline) >= 0 || text.Contains("://", StringComparison.Ordinal) ||
            text.Contains("www.", StringComparison.OrdinalIgnoreCase) || text.Contains("  \n", StringComparison.Ordinal))
        {
            return true;
        }
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("    ", StringComparison.Ordinal) || line.StartsWith('\t'))
            {
                return true;
            }
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("+ ", StringComparison.Ordinal) ||
                trimmed is "-" or "+")
            {
                return true;
            }
            if (trimmed.Length > 0 && trimmed.Trim('=', ' ').Length == 0 || trimmed.Length > 1 && trimmed.Trim('-', ' ').Length == 0)
            {
                return true;
            }
            var digits = 0;
            while (digits < trimmed.Length && char.IsAsciiDigit(trimmed[digits]))
            {
                digits++;
            }
            if (digits > 0 && digits + 1 < trimmed.Length + 1 && digits < trimmed.Length &&
                trimmed[digits] is '.' or ')' && (digits + 1 == trimmed.Length || trimmed[digits + 1] == ' '))
            {
                return true;
            }
        }
        return false;
    }
}
