using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace TKCode.Arc;

/// <summary>
/// A surface of stickers (Arc Sticker peel, design reference). Hovering lifts the nearest corner a few pixels, pulling
/// folds the sticker back along a real fold with its paper backing showing, pulling far enough peels it off to carry
/// around, and letting go sticks it down where it was dropped. It works with the keyboard and with touch, and
/// <see cref="PositionsChanged"/> gives the layout to save.
/// </summary>
/// <remarks>
/// <see cref="ContentControl.Content"/> is drawn on the surface under the stickers. Give the control a size and a
/// background; the stickers are clipped to its rounded box. Under reduced motion there is no peel and no hover lift: a
/// press picks the sticker up and it follows the pointer 1:1.
/// </remarks>
[TemplatePart(LayerPart, typeof(StickerPeelLayer))]
[TemplatePart(StatusPart, typeof(TextBlock))]
public class StickerPeel : ContentControl
{
    public const string LayerPart = "PART_Layer";
    public const string StatusPart = "PART_Status";

    /// <summary>Controlled placements by sticker id. Without it (or for ids it leaves out) the stickers start where their items say.</summary>
    public static readonly StyledProperty<IReadOnlyDictionary<string, StickerPlacement>?> PositionsProperty =
        AvaloniaProperty.Register<StickerPeel, IReadOnlyDictionary<string, StickerPlacement>?>(
            nameof(Positions), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Accessible name of the surface.</summary>
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StickerPeel, string>(nameof(Label), "Stickers");

    /// <summary>Raised once a sticker has settled somewhere new, with every placement.</summary>
    public static readonly RoutedEvent<StickerPeelPositionsChangedEventArgs> PositionsChangedEvent =
        RoutedEvent.Register<StickerPeel, StickerPeelPositionsChangedEventArgs>(nameof(PositionsChanged), RoutingStrategies.Bubble);

    private StickerPeelLayer? _layer;
    private TextBlock? _status;
    private bool _statusFlip;

    static StickerPeel()
    {
        FocusableProperty.OverrideDefaultValue<StickerPeel>(false);
    }

    public StickerPeel()
    {
        Stickers.CollectionChanged += (_, _) => _layer?.Rebuild(Stickers);
    }

    /// <summary>The stickers, each with an id, label, content, starting centre and shape.</summary>
    public AvaloniaList<StickerPeelItem> Stickers { get; } = [];

    /// <inheritdoc cref="PositionsProperty"/>
    public IReadOnlyDictionary<string, StickerPlacement>? Positions
    {
        get => GetValue(PositionsProperty);
        set => SetValue(PositionsProperty, value);
    }

    /// <inheritdoc cref="LabelProperty"/>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Raised once a sticker has settled somewhere new, with every placement. Store them to save the arrangement.</summary>
    public event EventHandler<StickerPeelPositionsChangedEventArgs>? PositionsChanged
    {
        add => AddHandler(PositionsChangedEvent, value);
        remove => RemoveHandler(PositionsChangedEvent, value);
    }

    internal StickerPeelLayer? Layer => _layer;

    /// <summary>Puts every sticker back where its item says; they glide there.</summary>
    public void Reset() => SetCurrentValue(PositionsProperty, null);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (_layer is not null)
        {
            _layer.Owner = null;
        }
        _layer = e.NameScope.Find<StickerPeelLayer>(LayerPart);
        _status = e.NameScope.Find<TextBlock>(StatusPart);
        if (_layer is not null)
        {
            _layer.Owner = this;
            _layer.Rebuild(Stickers);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PositionsProperty)
        {
            _layer?.RequestGlide();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new StickerPeelAutomationPeer(this);

    /// <summary>Where a sticker is stuck: the controlled placement, or the one its item says.</summary>
    internal StickerPlacement GetPlacement(StickerPeelItem item)
        => Positions is { } positions && positions.TryGetValue(item.Id, out var placement)
            ? placement
            : new StickerPlacement(item.X, item.Y, item.Rotate);

    /// <summary>A sticker was stuck down somewhere new: publish every placement.</summary>
    internal void OnPlaced(StickerPeelSticker sticker)
    {
        var size = _layer?.Bounds.Size ?? default;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }
        var all = new Dictionary<string, StickerPlacement>(Stickers.Count);
        foreach (var item in Stickers)
        {
            all[item.Id] = ReferenceEquals(item, sticker.Item)
                ? new StickerPlacement(
                    Math.Clamp(sticker.HomeCenter.X / size.Width, 0, 1),
                    Math.Clamp(sticker.HomeCenter.Y / size.Height, 0, 1),
                    GetPlacement(item).Rotate)
                : GetPlacement(item);
        }
        SetCurrentValue(PositionsProperty, all);
        RaiseEvent(new StickerPeelPositionsChangedEventArgs(PositionsChangedEvent, this, sticker.Item.Id, all));
    }

    /// <summary>Says what happened to a sticker in the polite status region.</summary>
    internal void Announce(string message)
    {
        if (_status is not null)
        {
            // The same message twice in a row must still change the text to be read again.
            _statusFlip = !_statusFlip;
            _status.Text = _statusFlip ? message : message + " ";
        }
    }

    private sealed class StickerPeelAutomationPeer(StickerPeel owner) : ContentControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override string? GetNameCore() => owner.Label;
    }
}
