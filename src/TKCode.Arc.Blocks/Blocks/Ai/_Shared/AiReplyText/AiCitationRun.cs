using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using StreamMark.Avalonia.Plugins;
using StreamMark.Core.Model;

namespace TKCode.Arc;

/// <summary>
/// The citation pill inside the text: an embedded run as wide as its number (at least 18, 1px margin each side) and 18
/// tall, its baseline 13px down so the pill centers on the lowercase letters of 16px text. It only holds its number and
/// key; the owning <see cref="AiReplyText"/> paints it with the state of that citation (source or plain, hover, card,
/// focus ring, fade-in), so a state change only repaints the line, never lays it out again.
/// </summary>
internal sealed class AiCitationRun : DrawableTextRun
{
    /// <summary>Height of the pill.</summary>
    public const double PillHeight = 18;

    /// <summary>Distance from the top of the pill to the text baseline it sits on.</summary>
    public const double BaselineOffset = 13;

    /// <summary>Horizontal margin on each side of the pill.</summary>
    public const double Margin = 1;

    private readonly AiReplyText _owner;
    private readonly Size _size;

    public AiCitationRun(AiReplyText owner, int number, int key, string label, double pillWidth, TextRunProperties properties)
    {
        _owner = owner;
        Number = number;
        Key = key;
        Label = label;
        Properties = properties;
        _size = new Size(pillWidth + 2 * Margin, PillHeight);
    }

    /// <summary>The cited number.</summary>
    public int Number { get; }

    /// <summary>The citation's place in the reply (0, 1, 2…), from the tag's <c>key</c> attribute; -1 without one.</summary>
    public int Key { get; }

    /// <summary>The number as shown.</summary>
    public string Label { get; }

    public override TextRunProperties Properties { get; }

    public override Size Size => _size;

    public override double Baseline => BaselineOffset;

    public override void Draw(DrawingContext drawingContext, Point origin) => _owner.DrawCitation(drawingContext, origin, this);
}

/// <summary>
/// Draws <c>&lt;Citation id="n" key="k" /&gt;</c> as an <see cref="AiCitationRun"/> for one <see cref="AiReplyText"/>. Copied
/// as plain text it is "[n]"; it is clickable only while the reply has a source for its number.
/// </summary>
internal sealed class AiCitationRenderer(AiReplyText owner) : IInlineComponentRenderer
{
    public DrawableTextRun? CreateRun(InlineObject component, InlineComponentContext context)
    {
        var number = Parse(component.Attributes["id"]);
        if (number < 0)
        {
            return null;
        }
        var label = number.ToString(CultureInfo.InvariantCulture);
        return new AiCitationRun(owner, number, Parse(component.Attributes["key"]), label, owner.PillWidth(label), context.Properties);
    }

    public string ToPlainText(InlineObject component) => "[" + component.Attributes["id"] + "]";

    public bool IsClickable(ComponentAttributes attributes) => owner.FindSource(Parse(attributes["id"])) is not null;

    internal static int Parse(string? value)
        => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : -1;
}

/// <summary>Registers <see cref="AiCitationRenderer"/> for the <c>citation</c> component, replacing StreamMark's built-in chip.</summary>
internal sealed class AiCitationPlugin(AiReplyText owner) : IMarkdownPlugin
{
    private readonly AiCitationRenderer _renderer = new(owner);

    public string Name => "arc-ai-citation";

    // The tag is already whitelisted by StreamMark's built-in components; registering again only swaps the renderer.
    public void Configure(MarkdownPluginBuilder builder) => builder.AddInlineComponent("citation", _renderer, registerTag: false);
}
