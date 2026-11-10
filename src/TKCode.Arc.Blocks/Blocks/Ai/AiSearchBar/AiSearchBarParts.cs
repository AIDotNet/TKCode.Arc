using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// The pressable pieces of an <see cref="AiSearchBar"/> (question chips, citations, source cards and result rows): a
/// button with a title, an optional detail and number, and a focus ring built on first keyboard focus. Each kind has its
/// own subclass so it gets its own theme.
/// </summary>
internal abstract class AiSearchBarItem : AvaloniaButton
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AiSearchBarItem, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AiSearchBarItem, string?>(nameof(Detail));

    public static readonly StyledProperty<string?> NumberProperty =
        AvaloniaProperty.Register<AiSearchBarItem, string?>(nameof(Number));

    private LazyFocusRing? _focusRing;

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }

    public string? Number { get => GetValue(NumberProperty); set => SetValue(NumberProperty, value); }

    /// <summary>What the item stands for (a question, a source, a result).</summary>
    public object? Payload { get; init; }

    protected abstract ArcDesignTokenKind RingRadius { get; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _focusRing ??= new LazyFocusRing(this, AiSearchBarTokens.Key(AiSearchBarTokenKind.FocusMargin), ArcDesignTokens.Key(RingRadius));
        _focusRing.Attach(e.NameScope.Find<Panel>("PART_Root"));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _focusRing?.Update();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _focusRing?.Update();
    }
}

/// <summary>A suggested or follow-up question: a pill with a sparkle.</summary>
internal sealed class AiSearchBarChip : AiSearchBarItem
{
    protected override Type StyleKeyOverride => typeof(AiSearchBarChip);

    protected override ArcDesignTokenKind RingRadius => ArcDesignTokenKind.RadiusPill;
}

/// <summary>A citation inside the answer: a small numbered pill.</summary>
internal sealed class AiSearchBarCitation : AiSearchBarItem
{
    protected override Type StyleKeyOverride => typeof(AiSearchBarCitation);

    protected override ArcDesignTokenKind RingRadius => ArcDesignTokenKind.RadiusPill;
}

/// <summary>A cited source: its number, title and location in a small card.</summary>
internal sealed class AiSearchBarSourceCard : AiSearchBarItem
{
    protected override Type StyleKeyOverride => typeof(AiSearchBarSourceCard);

    protected override ArcDesignTokenKind RingRadius => ArcDesignTokenKind.RadiusStep14;
}

/// <summary>A classic result row: title, location and snippet.</summary>
internal sealed class AiSearchBarResultRow : AiSearchBarItem
{
    protected override Type StyleKeyOverride => typeof(AiSearchBarResultRow);

    protected override ArcDesignTokenKind RingRadius => ArcDesignTokenKind.RadiusStep14;
}

/// <summary>Turns answer text with <c>[n]</c> markers into text runs and citation numbers.</summary>
internal static class AiSearchBarCitations
{
    /// <summary>One piece of the answer: plain text, or a citation of source <c>Number</c> (from 1).</summary>
    public readonly record struct Piece(string? Text, int Number);

    public static List<Piece> Split(string text)
    {
        var pieces = new List<Piece>();
        var start = 0;
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '[')
            {
                var close = text.IndexOf(']', i + 1);
                if (close > i + 1 && close - i <= 4 && int.TryParse(text.AsSpan(i + 1, close - i - 1), out var number) && number > 0)
                {
                    if (i > start)
                    {
                        pieces.Add(new Piece(text[start..i], 0));
                    }
                    pieces.Add(new Piece(null, number));
                    i = close + 1;
                    start = i;
                    continue;
                }
            }
            i++;
        }
        if (start < text.Length)
        {
            pieces.Add(new Piece(text[start..], 0));
        }
        return pieces;
    }
}

/// <summary>Helper to route a click to the right handler without a closure per item.</summary>
internal static class AiSearchBarItemExtensions
{
    public static T With<T>(this T item, EventHandler<RoutedEventArgs> click) where T : AiSearchBarItem
    {
        item.Click += click;
        return item;
    }
}
