using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// A card's 30px round icon button (collapse, remove): a 16px Lucide glyph (stroke 1.75) in the muted text color; hover
/// and keyboard focus fill it surface-muted with the text color, and keyboard focus adds the 2px ring. It takes a 44px
/// target (7px around the circle) without taking more room.
/// </summary>
internal sealed class LinkUnfurlIconButton : Button
{
    public static readonly StyledProperty<Geometry?> GlyphProperty =
        AvaloniaProperty.Register<LinkUnfurlIconButton, Geometry?>(nameof(Glyph));

    public Geometry? Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }
}

/// <summary>
/// The card title: a real link (it opens the address with the platform launcher), Inter 16/22.4 medium, wrapping,
/// underlined on hover.
/// </summary>
internal sealed class LinkUnfurlTitleLink : Button
{
}

/// <summary>
/// A sample button under the message (32 high, padding 0 11 0 9, a 14px favicon, 6px gap, 12px label): transparent with
/// an inner border-subtle ring and secondary text until its link is in the message (<c>:added</c>) or hovered, then
/// surface-muted with the text color. Presses scale to 0.94 on Arc's spring.
/// </summary>
[PseudoClasses(ActivePseudoClass)]
internal sealed class LinkUnfurlSampleButton : Button
{
    public const string ActivePseudoClass = ":added";

    public LinkUnfurlSampleButton(LinkUnfurlPreview preview)
    {
        Preview = preview;
        var icon = new LinkUnfurlFavicon { Size = 14, Preview = preview, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var text = new TextBlock { Text = preview.Site, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Content = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6,
            Children = { icon, text }
        };
    }

    public LinkUnfurlPreview Preview { get; }

    public bool IsActive
    {
        get => PseudoClasses.Contains(ActivePseudoClass);
        set => PseudoClasses.Set(ActivePseudoClass, value);
    }
}

/// <summary>
/// The send button: a 36px circle in the text color with an 18px arrow in the background color. While a send is pending
/// it holds (<c>:sending</c>, a spinner); once sent it turns the success color and the arrow gives way to a check for
/// 1.6s (<c>:sent</c>). Presses scale to 0.94 on Arc's spring.
/// </summary>
[PseudoClasses(SendingPseudoClass, SentPseudoClass)]
internal sealed class LinkUnfurlSendButton : Button
{
    public const string SendingPseudoClass = ":sending";
    public const string SentPseudoClass = ":sent";

    public bool IsSending
    {
        get => PseudoClasses.Contains(SendingPseudoClass);
        set => PseudoClasses.Set(SendingPseudoClass, value);
    }

    public bool IsSent
    {
        get => PseudoClasses.Contains(SentPseudoClass);
        set => PseudoClasses.Set(SentPseudoClass, value);
    }
}

/// <summary>
/// Holds the composer's message area at a height that eases from the old content's to the new one's after a send
/// (480ms, standard), so the composer shrinks instead of jumping; otherwise it is as tall as its child.
/// </summary>
internal sealed class LinkUnfurlHeightHost : Decorator
{
    private double? _from;
    private double _progress = 1;

    /// <summary>The height to ease from (progress 0); null follows the child again.</summary>
    public double? From
    {
        get => _from;
        set
        {
            _from = value;
            InvalidateMeasure();
        }
    }

    /// <summary>How far the height has eased from <see cref="From"/> to the child's (0 to 1).</summary>
    public double Progress
    {
        get => _progress;
        set
        {
            _progress = value;
            InvalidateMeasure();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);
        if (_from is not { } from)
        {
            return size;
        }
        return new Size(size.Width, Math.Max(0, from + (size.Height - from) * Math.Clamp(_progress, 0, 1)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is { } child)
        {
            child.Arrange(new Rect(0, 0, finalSize.Width, Math.Max(finalSize.Height, child.DesiredSize.Height)));
        }
        return finalSize;
    }
}
