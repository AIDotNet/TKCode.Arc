// Ported from Arc's free site-header block (MIT, Copyright (c) 2026 Elia Kuratli).
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;

namespace TKCode.Arc;

/// <summary>The three layouts of the <see cref="SiteHeader"/> (Arc's <c>variant</c>).</summary>
public enum SiteHeaderVariant
{
    /// <summary>Links beside the brand; items that have links open a mega menu panel.</summary>
    Mega,

    /// <summary>Links beside the brand; every item is a plain link.</summary>
    Simple,

    /// <summary>The links sit centered in a quiet capsule; every item is a plain link.</summary>
    Centered
}

/// <summary>What a <see cref="SiteHeader.Navigate"/> event reports was chosen.</summary>
public enum SiteHeaderNavigateKind
{
    /// <summary>A top level item of the bar or of the menu sheet.</summary>
    Item,

    /// <summary>A link inside a mega menu panel (or a group of the menu sheet).</summary>
    PanelLink,

    /// <summary>The feature card of a mega menu panel.</summary>
    Feature,

    /// <summary>The brand.</summary>
    Brand,

    /// <summary>The primary action.</summary>
    PrimaryAction,

    /// <summary>The secondary action.</summary>
    SecondaryAction
}

/// <summary>Base of the header's data items: tells the header to rebuild when a property changes.</summary>
public abstract class SiteHeaderData : AvaloniaObject
{
    internal event EventHandler? Invalidated;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    internal void RaiseInvalidated() => Invalidated?.Invoke(this, EventArgs.Empty);
}

/// <summary>One destination inside a mega menu panel (Arc's <c>SiteHeaderLink</c>).</summary>
public sealed class SiteHeaderLink : SiteHeaderData
{
    /// <summary>Identifies <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<SiteHeaderLink, string>(nameof(Label), string.Empty);

    /// <summary>Identifies <see cref="Description"/>.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<SiteHeaderLink, string?>(nameof(Description));

    /// <summary>Identifies <see cref="Icon"/>.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<SiteHeaderLink, Geometry?>(nameof(Icon));

    /// <summary>Identifies <see cref="Href"/>.</summary>
    public static readonly StyledProperty<string?> HrefProperty = AvaloniaProperty.Register<SiteHeaderLink, string?>(nameof(Href));

    /// <summary>The link's title.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>One short line under the title.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>A decorative 24×24 Lucide path drawn 16px wide with a 1.75 stroke beside the title.</summary>
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>The address to open. An absolute http, https or mailto address is opened when nobody handles <see cref="SiteHeader.Navigate"/>.</summary>
    public string? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }
}

/// <summary>The card beside the links of a mega menu panel (Arc's <c>SiteHeaderFeature</c>).</summary>
public sealed class SiteHeaderFeature : SiteHeaderData
{
    /// <summary>Identifies <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<SiteHeaderFeature, string>(nameof(Title), string.Empty);

    /// <summary>Identifies <see cref="Description"/>.</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<SiteHeaderFeature, string?>(nameof(Description));

    /// <summary>Identifies <see cref="Href"/>.</summary>
    public static readonly StyledProperty<string?> HrefProperty = AvaloniaProperty.Register<SiteHeaderFeature, string?>(nameof(Href));

    /// <summary>Identifies <see cref="Image"/>.</summary>
    public static readonly StyledProperty<IImage?> ImageProperty = AvaloniaProperty.Register<SiteHeaderFeature, IImage?>(nameof(Image));

    /// <summary>Identifies <see cref="ImageDescription"/>.</summary>
    public static readonly StyledProperty<string?> ImageDescriptionProperty = AvaloniaProperty.Register<SiteHeaderFeature, string?>(nameof(ImageDescription));

    /// <summary>The card's title.</summary>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>A short sentence under the title.</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>The address to open (see <see cref="SiteHeaderLink.Href"/>).</summary>
    public string? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>The picture above the title (16:10, cropped to fill). Without one the card shows no picture.</summary>
    public IImage? Image { get => GetValue(ImageProperty); set => SetValue(ImageProperty, value); }

    /// <summary>What the picture shows (the alternative text; read by screen readers with the card).</summary>
    public string? ImageDescription { get => GetValue(ImageDescriptionProperty); set => SetValue(ImageDescriptionProperty, value); }
}

/// <summary>
/// A top level destination of the <see cref="SiteHeader"/> (Arc's <c>SiteHeaderItem</c>). With <see cref="Links"/> and the
/// <see cref="SiteHeaderVariant.Mega"/> variant it opens a panel; otherwise it is a plain link.
/// </summary>
public sealed class SiteHeaderItem : SiteHeaderData
{
    /// <summary>Identifies <see cref="Value"/>.</summary>
    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<SiteHeaderItem, string>(nameof(Value), string.Empty);

    /// <summary>Identifies <see cref="Label"/>.</summary>
    public static readonly StyledProperty<string> LabelProperty = AvaloniaProperty.Register<SiteHeaderItem, string>(nameof(Label), string.Empty);

    /// <summary>Identifies <see cref="Href"/>.</summary>
    public static readonly StyledProperty<string?> HrefProperty = AvaloniaProperty.Register<SiteHeaderItem, string?>(nameof(Href));

    /// <summary>Identifies <see cref="Feature"/>.</summary>
    public static readonly StyledProperty<SiteHeaderFeature?> FeatureProperty = AvaloniaProperty.Register<SiteHeaderItem, SiteHeaderFeature?>(nameof(Feature));

    private readonly AvaloniaList<SiteHeaderLink> _links = new();

    /// <summary>Creates an empty item.</summary>
    public SiteHeaderItem()
    {
        _links.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is { } old)
            {
                foreach (SiteHeaderLink link in old)
                {
                    link.Invalidated -= OnChildInvalidated;
                }
            }
            if (e.NewItems is { } added)
            {
                foreach (SiteHeaderLink link in added)
                {
                    link.Invalidated += OnChildInvalidated;
                }
            }
            RaiseInvalidated();
        };
    }

    /// <summary>The stable value of the item; <see cref="SiteHeader.Current"/> and <see cref="SiteHeaderNavigateEventArgs.Section"/> use it.</summary>
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>The text of the item.</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The address to open (see <see cref="SiteHeaderLink.Href"/>); an item without one only raises <see cref="SiteHeader.Navigate"/>.</summary>
    public string? Href { get => GetValue(HrefProperty); set => SetValue(HrefProperty, value); }

    /// <summary>The links of the item's mega menu panel.</summary>
    [Content]
    public AvaloniaList<SiteHeaderLink> Links => _links;

    /// <summary>The card beside the links of the panel.</summary>
    public SiteHeaderFeature? Feature { get => GetValue(FeatureProperty); set => SetValue(FeatureProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FeatureProperty)
        {
            if (change.OldValue is SiteHeaderFeature old)
            {
                old.Invalidated -= OnChildInvalidated;
            }
            if (change.NewValue is SiteHeaderFeature added)
            {
                added.Invalidated += OnChildInvalidated;
            }
        }
    }

    private void OnChildInvalidated(object? sender, EventArgs e) => RaiseInvalidated();
}

/// <summary>Arguments of <see cref="SiteHeader.Navigate"/>.</summary>
public sealed class SiteHeaderNavigateEventArgs : RoutedEventArgs
{
    /// <summary>Creates the arguments.</summary>
    public SiteHeaderNavigateEventArgs(RoutedEvent routedEvent, object source, SiteHeaderNavigateKind kind, string label, string? href,
                                       SiteHeaderItem? item = null, SiteHeaderLink? link = null, SiteHeaderFeature? feature = null)
        : base(routedEvent, source)
    {
        Kind = kind;
        Label = label;
        Href = href;
        Item = item;
        Link = link;
        Feature = feature;
    }

    /// <summary>What was chosen.</summary>
    public SiteHeaderNavigateKind Kind { get; }

    /// <summary>The text of the destination.</summary>
    public string Label { get; }

    /// <summary>The destination's address, when it has one.</summary>
    public string? Href { get; }

    /// <summary>The <see cref="SiteHeaderItem.Value"/> of the top level item the destination belongs to (Arc's <c>section</c>), or null.</summary>
    public string? Section => Item?.Value;

    /// <summary>The top level item the destination belongs to, or null (brand and actions).</summary>
    public SiteHeaderItem? Item { get; }

    /// <summary>The panel link, for <see cref="SiteHeaderNavigateKind.PanelLink"/>.</summary>
    public SiteHeaderLink? Link { get; }

    /// <summary>The feature card, for <see cref="SiteHeaderNavigateKind.Feature"/>.</summary>
    public SiteHeaderFeature? Feature { get; }
}
