using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>
/// Arc expandable text: long text clamped to <see cref="ExpandableClamp.Lines"/> lines with a soft fade over the last
/// one. Read more opens it to its full height with a calm ease out (500–600ms, growing with the distance) and Show less
/// closes it (400–450ms); the height, the fade and the chevron follow one progress value, so they always move in step,
/// and a click mid-flight reverses from the current height. The toggle only appears when the text really overflows, and
/// stays correct as the width or font changes. While collapsed the text is laid out only past the clamp, so a very long
/// text costs as much as a short one. The whole text is always available to assistive technology. For selectable or
/// rich content, use <see cref="ExpandableContent"/>.
/// </summary>
[TemplatePart(TextPart, typeof(TextBlock))]
public class ExpandableText : ExpandableClamp
{
    public const string TextPart = "PART_Text";

    /// <summary>The text (paragraphs separated by line breaks).</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ExpandableText, string?>(nameof(Text));

    /// <summary>Rich text with runs and inline links (<c>InlineUIContainer</c>); used instead of <see cref="Text"/> when set.</summary>
    public static readonly StyledProperty<InlineCollection?> InlinesProperty =
        AvaloniaProperty.Register<ExpandableText, InlineCollection?>(nameof(Inlines));

    /// <inheritdoc cref="TextProperty"/>
    [Content]
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <inheritdoc cref="InlinesProperty"/>
    public InlineCollection? Inlines { get => GetValue(InlinesProperty); set => SetValue(InlinesProperty, value); }

    /// <summary>The clamp ends on a line of the template's text, which is laid out at <see cref="ExpandableClamp.LineHeight"/>.</summary>
    private protected override Control? GetLineSource(INameScope scope) => scope.Find<TextBlock>(TextPart);

    private protected override double ClampLineHeight => double.NaN;
}
