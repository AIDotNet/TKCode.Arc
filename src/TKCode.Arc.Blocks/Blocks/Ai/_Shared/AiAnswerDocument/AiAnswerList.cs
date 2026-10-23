using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using TKCode.Arc.Motion;

namespace TKCode.Arc;

/// <summary>
/// An ordered or bullet list of an answer: items 10 apart, each text 28 in from the left with its marker in the first 18
/// (right-aligned "n." in the muted color with tabular digits, on the first line; a bullet for unordered lists, a style
/// of our own since Arc's answers never showed one).
/// </summary>
internal sealed class AiAnswerList : StackPanel
{
    private readonly AiAnswerDocument _owner;
    private readonly List<AiAnswerListItem> _items = [];

    public AiAnswerList(AiAnswerDocument owner, bool ordered)
    {
        _owner = owner;
        Ordered = ordered;
        Spacing = 10;
    }

    /// <summary>Whether the items are numbered.</summary>
    public bool Ordered { get; }

    /// <summary>The texts of the items, in order.</summary>
    public IEnumerable<AiAnswerText> Texts => _items.Select(i => i.Text);

    /// <summary>The items (tests).</summary>
    internal IReadOnlyList<AiAnswerListItem> Items => _items;

    /// <summary>Shows the items of <paramref name="block"/>: existing items update in place, new ones are added (fading in with <paramref name="fade"/>).</summary>
    public void Update(AiAnswerBlock block, bool fade)
    {
        for (var i = 0; i < block.Texts.Count; i++)
        {
            var marker = Ordered ? (block.ListStart + i).ToString(CultureInfo.InvariantCulture) + "." : "•";
            if (i == _items.Count)
            {
                var item = new AiAnswerListItem(_owner, marker);
                _items.Add(item);
                Children.Add(item);
                if (fade)
                {
                    AiAnswerFades.FadeIn(item.Marker);
                }
            }
            else
            {
                _items[i].Marker.Text = marker;
            }
            _items[i].Text.SetContent(block.Texts[i], fade);
        }
        while (_items.Count > block.Texts.Count)
        {
            var last = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            Children.Remove(last);
            foreach (var pill in last.Text.Pills)
            {
                _owner.ForgetCitation(pill);
            }
        }
    }
}

/// <summary>One item of an <see cref="AiAnswerList"/>: its marker and its text.</summary>
internal sealed class AiAnswerListItem : Panel
{
    /// <summary>Where an item's text starts.</summary>
    internal const double TextIndent = 28;

    public AiAnswerListItem(AiAnswerDocument owner, string marker)
    {
        Marker = new AiAnswerListMarker { Text = marker };
        Text = new AiAnswerText(owner, AiAnswerTextRole.ListItem) { Margin = new Thickness(TextIndent, 0, 0, 0) };
        Children.Add(Marker);
        Children.Add(Text);
    }

    /// <summary>The number or bullet.</summary>
    public AiAnswerListMarker Marker { get; }

    /// <summary>The item's text.</summary>
    public AiAnswerText Text { get; }
}

/// <summary>The marker of a list item: 18 wide, right-aligned, in the muted text color with tabular digits, on the item's first line.</summary>
internal sealed class AiAnswerListMarker : TextBlock
{
    protected override Type StyleKeyOverride => typeof(AiAnswerListMarker);
}
