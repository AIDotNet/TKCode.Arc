using Avalonia;
using Avalonia.Collections;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The six arrangements of a <see cref="LayoutMorph"/>.</summary>
public enum LayoutMorphMode
{
    /// <summary>Even rows of cards; wide containers show a two-line caption under each picture.</summary>
    Grid,

    /// <summary>Columns of cards that keep each item's own <see cref="LayoutMorphItem.AspectRatio"/>.</summary>
    Masonry,

    /// <summary>Rows with a thumbnail, two lines of text and a value on the right.</summary>
    List,

    /// <summary>A fanned hand of cards; the card under the pointer lifts out of the fan.</summary>
    Stack,

    /// <summary>A ring of large cards seen from inside; drag, swipe or use the keys to turn it.</summary>
    Carousel,

    /// <summary>A spiral of cards, ten to a turn; drag or flick to spin it.</summary>
    Helix
}

/// <summary>How an opened <see cref="LayoutMorphItem"/> lays out its detail.</summary>
public enum LayoutMorphOpenVariant
{
    /// <summary>A small picture on the left of the text (people and items without a picture).</summary>
    Profile,

    /// <summary>A wide card with a large picture on the left (products with a photo).</summary>
    Hero
}

/// <summary>A label and a value: a fact in the detail or a statistic in the bottom panel.</summary>
public class LayoutMorphFact : AvaloniaObject
{
    /// <summary>The short label above the value ("Price", "In stock").</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<LayoutMorphFact, string>(nameof(Label), string.Empty);

    /// <summary>The value as shown ("$96", "1,386", "2 days"). Numbers with an optional prefix or suffix roll in the panel.</summary>
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<LayoutMorphFact, string>(nameof(Value), string.Empty);

    /// <summary>
    /// The number behind <see cref="Value"/>, when it should roll digit by digit in the panel. When null, a
    /// <see cref="Value"/> such as "$1,240" or "4.8" is read as a number with its prefix and suffix kept.
    /// </summary>
    public static readonly StyledProperty<double?> NumericValueProperty =
        AvaloniaProperty.Register<LayoutMorphFact, double?>(nameof(NumericValue));

    /// <summary>Decimals shown for <see cref="NumericValue"/> (ignored when the value is read from <see cref="Value"/>).</summary>
    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<LayoutMorphFact, int>(nameof(Decimals));

    public LayoutMorphFact()
    {
    }

    public LayoutMorphFact(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="NumericValueProperty"/>
    public double? NumericValue { get => GetValue(NumericValueProperty); set => SetValue(NumericValueProperty, value); }

    /// <inheritdoc cref="DecimalsProperty"/>
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
}

/// <summary>
/// One card of a <see cref="LayoutMorph"/>: a product, a person or any item with a title. Items with an
/// <see cref="Image"/> show it; items without one show a cover with the title and the value. Every layout, caption,
/// detail and panel statistic is derived from these properties.
/// </summary>
public class LayoutMorphItem : AvaloniaObject
{
    /// <summary>A stable identifier: the card, its focus and its state are kept when the collection is rebuilt.</summary>
    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string?>(nameof(Key));

    /// <summary>The name on the card, the floating tag, the detail and the bottom panel.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string>(nameof(Title), string.Empty);

    /// <summary>The second line under the title in the grid, the tag, the carousel and the panel; <see cref="Meta"/> when null.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string?>(nameof(Subtitle));

    /// <summary>The second line of a list row and of the detail ("Maker · material").</summary>
    public static readonly StyledProperty<string?> MetaProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string?>(nameof(Meta));

    /// <summary>The value on the right of a list row and a carousel caption, and on a cover ("$96", "2 pieces").</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string?>(nameof(Value));

    /// <summary>The picture, drawn to fill (cropped to) every frame; null shows a cover with the title and the value.</summary>
    public static readonly StyledProperty<IImage?> ImageProperty =
        AvaloniaProperty.Register<LayoutMorphItem, IImage?>(nameof(Image));

    /// <summary>Height over width in the masonry layout (0.75 to 1.5 reads well); the other layouts ignore it.</summary>
    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<LayoutMorphItem, double>(nameof(AspectRatio), 1.0);

    /// <summary>One to three sentences in the opened detail (left out on narrow screens).</summary>
    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string?>(nameof(Description));

    /// <summary>Up to four facts at the foot of the detail (two on narrow screens).</summary>
    public static readonly StyledProperty<AvaloniaList<LayoutMorphFact>> FactsProperty =
        AvaloniaProperty.Register<LayoutMorphItem, AvaloniaList<LayoutMorphFact>>(nameof(Facts));

    /// <summary>The three statistics the bottom panel shows while this card is pointed at, focused, in front or open.</summary>
    public static readonly StyledProperty<AvaloniaList<LayoutMorphFact>> StatsProperty =
        AvaloniaProperty.Register<LayoutMorphItem, AvaloniaList<LayoutMorphFact>>(nameof(Stats));

    /// <summary>The shape of the opened detail: <see cref="LayoutMorphOpenVariant.Hero"/> for photos, <see cref="LayoutMorphOpenVariant.Profile"/> otherwise.</summary>
    public static readonly StyledProperty<LayoutMorphOpenVariant> OpenVariantProperty =
        AvaloniaProperty.Register<LayoutMorphItem, LayoutMorphOpenVariant>(nameof(OpenVariant));

    /// <summary>The detail's main button while <see cref="IsActive"/> is false ("Add to bag", "Follow").</summary>
    public static readonly StyledProperty<string> ActionTextProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string>(nameof(ActionText), "Save");

    /// <summary>The detail's main button while <see cref="IsActive"/> is true ("Added to bag", "Following").</summary>
    public static readonly StyledProperty<string> ActiveActionTextProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string>(nameof(ActiveActionText), "Saved");

    /// <summary>The main button's pressed state (saved, followed); kept while the detail is closed and reopened.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<LayoutMorphItem, bool>(nameof(IsActive), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The card's screen reader name; "Title, Subtitle, Value" when null.</summary>
    public static readonly StyledProperty<string?> AutomationNameProperty =
        AvaloniaProperty.Register<LayoutMorphItem, string?>(nameof(AutomationName));

    /// <summary>Any object of yours (the record behind the card).</summary>
    public static readonly StyledProperty<object?> TagProperty =
        AvaloniaProperty.Register<LayoutMorphItem, object?>(nameof(Tag));

    public LayoutMorphItem()
    {
        SetCurrentValue(FactsProperty, new AvaloniaList<LayoutMorphFact>());
        SetCurrentValue(StatsProperty, new AvaloniaList<LayoutMorphFact>());
    }

    /// <inheritdoc cref="KeyProperty"/>
    public string? Key { get => GetValue(KeyProperty); set => SetValue(KeyProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="MetaProperty"/>
    public string? Meta { get => GetValue(MetaProperty); set => SetValue(MetaProperty, value); }

    /// <inheritdoc cref="ValueProperty"/>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <inheritdoc cref="ImageProperty"/>
    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <inheritdoc cref="AspectRatioProperty"/>
    public double AspectRatio { get => GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }

    /// <inheritdoc cref="DescriptionProperty"/>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <inheritdoc cref="FactsProperty"/>
    public AvaloniaList<LayoutMorphFact> Facts { get => GetValue(FactsProperty); set => SetValue(FactsProperty, value); }

    /// <inheritdoc cref="StatsProperty"/>
    public AvaloniaList<LayoutMorphFact> Stats { get => GetValue(StatsProperty); set => SetValue(StatsProperty, value); }

    /// <inheritdoc cref="OpenVariantProperty"/>
    public LayoutMorphOpenVariant OpenVariant { get => GetValue(OpenVariantProperty); set => SetValue(OpenVariantProperty, value); }

    /// <inheritdoc cref="ActionTextProperty"/>
    public string ActionText { get => GetValue(ActionTextProperty); set => SetValue(ActionTextProperty, value); }

    /// <inheritdoc cref="ActiveActionTextProperty"/>
    public string ActiveActionText { get => GetValue(ActiveActionTextProperty); set => SetValue(ActiveActionTextProperty, value); }

    /// <inheritdoc cref="IsActiveProperty"/>
    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    /// <inheritdoc cref="AutomationNameProperty"/>
    public string? AutomationName { get => GetValue(AutomationNameProperty); set => SetValue(AutomationNameProperty, value); }

    /// <inheritdoc cref="TagProperty"/>
    public object? Tag { get => GetValue(TagProperty); set => SetValue(TagProperty, value); }

    /// <summary>The line under the title: <see cref="Subtitle"/>, else <see cref="Meta"/>.</summary>
    internal string? SecondLine => Subtitle ?? Meta;

    /// <summary>The card's screen reader name.</summary>
    internal string AccessibleName
    {
        get
        {
            if (!string.IsNullOrEmpty(AutomationName))
            {
                return AutomationName!;
            }
            var parts = new List<string>(3);
            if (!string.IsNullOrEmpty(Title))
            {
                parts.Add(Title);
            }
            if (!string.IsNullOrEmpty(SecondLine))
            {
                parts.Add(SecondLine!);
            }
            if (!string.IsNullOrEmpty(Value))
            {
                parts.Add(Value!);
            }
            return string.Join(", ", parts);
        }
    }

    /// <summary>The detail text a screen reader gets as help: meta, description and every fact ("Label: value").</summary>
    internal string AccessibleDetails
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Meta))
            {
                parts.Add(Meta!);
            }
            if (!string.IsNullOrEmpty(Description))
            {
                parts.Add(Description!);
            }
            foreach (var fact in Facts)
            {
                parts.Add(fact.Label + ": " + fact.Value);
            }
            return string.Join(". ", parts);
        }
    }
}

/// <summary>Arguments of <see cref="LayoutMorph.LayoutChanged"/>.</summary>
public sealed class LayoutMorphLayoutChangedEventArgs : RoutedEventArgs
{
    public LayoutMorphLayoutChangedEventArgs(RoutedEvent routedEvent, LayoutMorphMode oldLayout, LayoutMorphMode newLayout)
        : base(routedEvent)
    {
        OldLayout = oldLayout;
        NewLayout = newLayout;
    }

    /// <summary>The layout the cards leave.</summary>
    public LayoutMorphMode OldLayout { get; }

    /// <summary>The layout the cards travel to.</summary>
    public LayoutMorphMode NewLayout { get; }
}

/// <summary>Arguments of the <see cref="LayoutMorph"/> item events.</summary>
public sealed class LayoutMorphItemEventArgs : RoutedEventArgs
{
    public LayoutMorphItemEventArgs(RoutedEvent routedEvent, LayoutMorphItem item) : base(routedEvent)
    {
        Item = item;
    }

    /// <summary>The card's item.</summary>
    public LayoutMorphItem Item { get; }
}
