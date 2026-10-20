using System.Windows.Input;
using Avalonia;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>The edge an <see cref="ExpandingSearch"/> button sits on; the field grows away from it.</summary>
public enum ExpandingSearchAnchor
{
    Start,
    End
}

/// <summary>One searchable entry of an <see cref="ExpandingSearch"/>.</summary>
public class ExpandingSearchItem : AvaloniaObject
{
    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<ExpandingSearchItem, string>(nameof(Id), string.Empty);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ExpandingSearchItem, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<ExpandingSearchItem, string?>(nameof(Meta));

    public static readonly StyledProperty<string?> GroupProperty =
        AvaloniaProperty.Register<ExpandingSearchItem, string?>(nameof(Group));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<ExpandingSearchItem, object?>(nameof(Icon));

    public static readonly StyledProperty<IList<string>?> KeywordsProperty =
        AvaloniaProperty.Register<ExpandingSearchItem, IList<string>?>(nameof(Keywords));

    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<ExpandingSearchItem, object?>(nameof(Tag));

    /// <summary>Stable id (defaults to the title).</summary>
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }

    /// <summary>The line that is searched first and shown in bold where it matches.</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>One short line under the title (type, owner, last edit); also searched.</summary>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <summary>Results gather under this heading, in the order groups first appear.</summary>
    public string? Group { get => GetValue(GroupProperty); set => SetValue(GroupProperty, value); }

    /// <summary>An 18px leading icon: a Lucide <see cref="Avalonia.Media.Geometry"/> or any control.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Extra words that also find this item.</summary>
    public IList<string>? Keywords { get => GetValue(KeywordsProperty); set => SetValue(KeywordsProperty, value); }

    /// <summary>Any value you want back when the item is chosen.</summary>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    internal string Key => string.IsNullOrEmpty(Id) ? Title : Id;

    public override string ToString() => Title;
}

/// <summary>Arguments of <see cref="ExpandingSearch.ItemSelected"/>.</summary>
public sealed class ExpandingSearchItemSelectedEventArgs : RoutedEventArgs
{
    public ExpandingSearchItemSelectedEventArgs(RoutedEvent routedEvent, ExpandingSearchItem item)
        : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The chosen item.</summary>
    public ExpandingSearchItem Item { get; }
}

/// <summary>Arc's ranking and grouping for <see cref="ExpandingSearch"/> results.</summary>
internal static class ExpandingSearchRanking
{
    /// <summary>
    /// Title starts beat word starts, which beat matches inside a word, which beat keyword and meta matches.
    /// Ties keep the original order.
    /// </summary>
    public static List<ExpandingSearchItem> Rank(IReadOnlyList<ExpandingSearchItem> items, string query, int limit)
    {
        var scored = new List<(ExpandingSearchItem Item, int Score, int Order)>();
        for (var order = 0; order < items.Count; order++)
        {
            var item = items[order];
            var title = item.Title;
            var at = title.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
            var extra = Contains(item.Meta, query);
            if (!extra && item.Keywords is { } keywords)
            {
                foreach (var keyword in keywords)
                {
                    if (Contains(keyword, query))
                    {
                        extra = true;
                        break;
                    }
                }
            }
            var score = at == 0 ? 0
                : at > 0 && IsBoundary(title[at - 1]) ? 1
                : at > 0 ? 2
                : extra ? 3
                : -1;
            if (score >= 0)
            {
                scored.Add((item, score, order));
            }
        }
        scored.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score) : a.Order.CompareTo(b.Order));
        var result = new List<ExpandingSearchItem>(Math.Min(limit, scored.Count));
        for (var i = 0; i < scored.Count && i < limit; i++)
        {
            result.Add(scored[i].Item);
        }
        return result;
    }

    private static bool Contains(string? text, string query)
        => text is not null && text.Contains(query, StringComparison.CurrentCultureIgnoreCase);

    private static bool IsBoundary(char c) => char.IsWhiteSpace(c) || c == '-' || c == '/';

    /// <summary>Groups <paramref name="list"/> in the order each group first appears in <paramref name="source"/>.</summary>
    public static List<(string Name, List<ExpandingSearchItem> Items)> Group(List<ExpandingSearchItem> list, IReadOnlyList<ExpandingSearchItem> source)
    {
        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < source.Count; i++)
        {
            order.TryAdd(source[i].Group ?? string.Empty, i);
        }
        var groups = new List<(string Name, List<ExpandingSearchItem> Items)>();
        foreach (var item in list)
        {
            var name = item.Group ?? string.Empty;
            var index = groups.FindIndex(g => g.Name == name);
            if (index < 0)
            {
                groups.Add((name, new List<ExpandingSearchItem> { item }));
            }
            else
            {
                groups[index].Items.Add(item);
            }
        }
        groups.Sort((a, b) => order.GetValueOrDefault(a.Name).CompareTo(order.GetValueOrDefault(b.Name)));
        return groups;
    }
}

/// <summary>Command helper: executes a command when it can.</summary>
internal static class ExpandingSearchCommands
{
    public static void Execute(ICommand? command, object? parameter)
    {
        if (command is not null && command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }
}
