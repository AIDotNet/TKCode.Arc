using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

/// <summary>
/// A share sheet for one item: the link with a copy button, the people you share with most, the places you can send it
/// and a few actions. On a phone-sized viewport it rises from the bottom as a sheet you can drag away; on a wider one it
/// opens as a popover under its trigger. <see cref="ContentControl.Content"/> is the trigger: a button there opens the
/// sheet, or call <see cref="Open"/>.
/// </summary>
/// <remarks>
/// Choosing a person or a destination raises <see cref="Shared"/> and closes the sheet; the actions raise
/// <see cref="ActionInvoked"/> and run their command. The sheet does not send anything itself: it tells you what was
/// chosen.
/// </remarks>
public class ShareSheet : ContentControl
{
    /// <summary>Whether the sheet is open (two-way).</summary>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ShareSheet, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>The heading of the sheet.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ShareSheet, string>(nameof(Title), "Share");

    /// <summary>A line under the heading, usually the name of what is shared; null leaves it out.</summary>
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ShareSheet, string?>(nameof(Subtitle));

    /// <summary>The address to share; it shows with a copy button. Null hides the link row.</summary>
    public static readonly StyledProperty<string?> LinkProperty =
        AvaloniaProperty.Register<ShareSheet, string?>(nameof(Link));

    /// <summary>Where the sheet opens: a popover, a bottom sheet, or by the width of the viewport (default).</summary>
    public static readonly StyledProperty<ShareSheetPresentation> PresentationProperty =
        AvaloniaProperty.Register<ShareSheet, ShareSheetPresentation>(nameof(Presentation));

    /// <summary>Whether choosing a person or a destination closes the sheet (default true).</summary>
    public static readonly StyledProperty<bool> ClosesOnShareProperty =
        AvaloniaProperty.Register<ShareSheet, bool>(nameof(ClosesOnShare), true);

    /// <summary>The heading over the people.</summary>
    public static readonly StyledProperty<string> PeopleHeaderProperty =
        AvaloniaProperty.Register<ShareSheet, string>(nameof(PeopleHeader), "People");

    /// <summary>The heading over the destinations.</summary>
    public static readonly StyledProperty<string> DestinationsHeaderProperty =
        AvaloniaProperty.Register<ShareSheet, string>(nameof(DestinationsHeader), "Send to");

    /// <summary>Label of the copy button.</summary>
    public static readonly StyledProperty<string> CopyLinkLabelProperty =
        AvaloniaProperty.Register<ShareSheet, string>(nameof(CopyLinkLabel), "Copy link");

    /// <summary>Label of the copy button after a copy.</summary>
    public static readonly StyledProperty<string> CopiedLabelProperty =
        AvaloniaProperty.Register<ShareSheet, string>(nameof(CopiedLabel), "Copied");

    /// <summary>Raised when a person or a destination is chosen. Bubbles.</summary>
    public static readonly RoutedEvent<ShareSheetTargetEventArgs> SharedEvent =
        RoutedEvent.Register<ShareSheet, ShareSheetTargetEventArgs>(nameof(Shared), RoutingStrategies.Bubble);

    /// <summary>Raised when an action row is chosen, before its command runs. Bubbles.</summary>
    public static readonly RoutedEvent<ShareSheetActionEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<ShareSheet, ShareSheetActionEventArgs>(nameof(ActionInvoked), RoutingStrategies.Bubble);

    /// <summary>Raised when the link was copied to the clipboard. Bubbles.</summary>
    public static readonly RoutedEvent<RoutedEventArgs> LinkCopiedEvent =
        RoutedEvent.Register<ShareSheet, RoutedEventArgs>(nameof(LinkCopied), RoutingStrategies.Bubble);

    private ShareSheetPanel? _panel;
    private Popover? _popover;
    private BottomSheet? _sheet;
    private Control? _anchor;
    private bool _itemsDirty = true;
    private bool _syncing;

    static ShareSheet()
    {
        FocusableProperty.OverrideDefaultValue<ShareSheet>(false);
    }

    public ShareSheet()
    {
        People.CollectionChanged += OnItemsChanged;
        Destinations.CollectionChanged += OnItemsChanged;
        Actions.CollectionChanged += OnItemsChanged;
        AddHandler(AvaloniaButton.ClickEvent, OnClick);
    }

    /// <summary>People to share with, drawn as soft circles with their initials (or their glyph).</summary>
    public AvaloniaList<ShareSheetTarget> People { get; } = new();

    /// <summary>Places to send to (apps, channels, folders), drawn as glyph tiles.</summary>
    public AvaloniaList<ShareSheetTarget> Destinations { get; } = new();

    /// <summary>Rows under the strips for things to do with the item, such as embedding it or showing a code.</summary>
    public AvaloniaList<ShareSheetAction> Actions { get; } = new();

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="SubtitleProperty"/>
    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }

    /// <inheritdoc cref="LinkProperty"/>
    public string? Link { get => GetValue(LinkProperty); set => SetValue(LinkProperty, value); }

    /// <inheritdoc cref="PresentationProperty"/>
    public ShareSheetPresentation Presentation { get => GetValue(PresentationProperty); set => SetValue(PresentationProperty, value); }

    /// <inheritdoc cref="ClosesOnShareProperty"/>
    public bool ClosesOnShare { get => GetValue(ClosesOnShareProperty); set => SetValue(ClosesOnShareProperty, value); }

    /// <inheritdoc cref="PeopleHeaderProperty"/>
    public string PeopleHeader { get => GetValue(PeopleHeaderProperty); set => SetValue(PeopleHeaderProperty, value); }

    /// <inheritdoc cref="DestinationsHeaderProperty"/>
    public string DestinationsHeader { get => GetValue(DestinationsHeaderProperty); set => SetValue(DestinationsHeaderProperty, value); }

    /// <inheritdoc cref="CopyLinkLabelProperty"/>
    public string CopyLinkLabel { get => GetValue(CopyLinkLabelProperty); set => SetValue(CopyLinkLabelProperty, value); }

    /// <inheritdoc cref="CopiedLabelProperty"/>
    public string CopiedLabel { get => GetValue(CopiedLabelProperty); set => SetValue(CopiedLabelProperty, value); }

    /// <inheritdoc cref="SharedEvent"/>
    public event EventHandler<ShareSheetTargetEventArgs>? Shared
    {
        add => AddHandler(SharedEvent, value);
        remove => RemoveHandler(SharedEvent, value);
    }

    /// <inheritdoc cref="ActionInvokedEvent"/>
    public event EventHandler<ShareSheetActionEventArgs>? ActionInvoked
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    /// <inheritdoc cref="LinkCopiedEvent"/>
    public event EventHandler<RoutedEventArgs>? LinkCopied
    {
        add => AddHandler(LinkCopiedEvent, value);
        remove => RemoveHandler(LinkCopiedEvent, value);
    }

    /// <summary>The panel shown in the popover or the sheet (created on first open).</summary>
    internal ShareSheetPanel? Panel => _panel;

    /// <summary>The popover host (created the first time the sheet opens as a popover).</summary>
    internal Popover? PopoverHost => _popover;

    /// <summary>The bottom sheet host (created the first time the sheet opens from the bottom).</summary>
    internal BottomSheet? SheetHost => _sheet;

    /// <summary>Opens the sheet, anchored to the trigger.</summary>
    public void Open() => SetCurrentValue(IsOpenProperty, true);

    /// <summary>Opens the sheet anchored to <paramref name="anchor"/> instead of the trigger.</summary>
    public void ShowAt(Control anchor)
    {
        _anchor = anchor;
        SetCurrentValue(IsOpenProperty, true);
    }

    /// <summary>Closes the sheet.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <summary>Whether the sheet opens from the bottom now: by <see cref="Presentation"/>, or by the viewport's width.</summary>
    internal bool UsesSheet => Presentation switch
    {
        ShareSheetPresentation.Sheet => true,
        ShareSheetPresentation.Popover => false,
        _ => ArcViewport.IsCompact(this)
    };

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsOpenProperty && !_syncing)
        {
            if (change.GetNewValue<bool>())
            {
                OpenCore();
            }
            else
            {
                CloseCore();
            }
        }
        else if (change.Property == TitleProperty && _sheet is not null)
        {
            _sheet.Title = Title;
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _itemsDirty = true;
        if (_panel is not null && IsOpen)
        {
            RefreshItems();
        }
    }

    private void RefreshItems()
    {
        if (_panel is null || !_itemsDirty)
        {
            return;
        }
        _itemsDirty = false;
        _panel.SetItems(People, Destinations, Actions);
    }

    private ShareSheetPanel EnsurePanel()
    {
        if (_panel is not null)
        {
            return _panel;
        }
        var panel = new ShareSheetPanel
        {
            TileInvoked = OnTileInvoked,
            RowInvoked = OnRowInvoked,
            Copied = () => RaiseEvent(new RoutedEventArgs(LinkCopiedEvent))
        };
        panel.Bind(ShareSheetPanel.TitleProperty, this.GetObservable(TitleProperty));
        panel.Bind(ShareSheetPanel.SubtitleProperty, this.GetObservable(SubtitleProperty));
        panel.Bind(ShareSheetPanel.LinkProperty, this.GetObservable(LinkProperty));
        panel.Bind(ShareSheetPanel.PeopleHeaderProperty, this.GetObservable(PeopleHeaderProperty));
        panel.Bind(ShareSheetPanel.DestinationsHeaderProperty, this.GetObservable(DestinationsHeaderProperty));
        panel.Bind(ShareSheetPanel.CopyLinkLabelProperty, this.GetObservable(CopyLinkLabelProperty));
        panel.Bind(ShareSheetPanel.CopiedLabelProperty, this.GetObservable(CopiedLabelProperty));
        _panel = panel;
        return panel;
    }

    private Control Anchor => _anchor ?? Content as Control ?? this;

    private void OpenCore()
    {
        var panel = EnsurePanel();
        RefreshItems();
        if (UsesSheet)
        {
            ShowSheet(panel);
        }
        else
        {
            ShowPopover(panel);
        }
        if (Content is Control trigger)
        {
            AutomationProperties.SetItemStatus(trigger, "Expanded");
        }
    }

    private void ShowPopover(ShareSheetPanel panel)
    {
        if (_sheet is not null)
        {
            _sheet.Content = null;
        }
        if (_popover is null)
        {
            _popover = new Popover { Placement = PopoverPlacement.Bottom, Alignment = PopoverAlignment.Start };
            LogicalChildren.Add(_popover);
            _popover.Closed += (_, _) => OnHostClosed();
        }
        panel.SetSheet(false);
        AutomationProperties.SetName(_popover, Title);
        if (!ReferenceEquals(_popover.Content, panel))
        {
            _popover.Content = panel;
        }
        _popover.ShowAt(Anchor);
        FocusFirstSoon();
    }

    private void ShowSheet(ShareSheetPanel panel)
    {
        if (_popover is not null)
        {
            _popover.Content = null;
        }
        if (_sheet is null)
        {
            _sheet = new BottomSheet { Title = Title };
            _sheet.Closed += (_, _) => OnHostClosed();
        }
        panel.SetSheet(true);
        if (!ReferenceEquals(_sheet.Content, panel))
        {
            _sheet.Content = panel;
        }
        _sheet.Detents = new BottomSheetDetents(new[] { SheetFraction(panel) });
        _ = _sheet.ShowAsync(this);
    }

    /// <summary>The share of the viewport's height the sheet needs to show the whole panel, within 0.3 and 0.92.</summary>
    private double SheetFraction(ShareSheetPanel panel)
    {
        if (ArcViewport.GetSize(this) is not { Height: > 0 } viewport)
        {
            return 0.6;
        }
        panel.Measure(new Size(viewport.Width - 32, double.PositiveInfinity));
        // The sheet's grabber, title and padding take about 110px above and below the panel.
        return Math.Clamp((panel.DesiredSize.Height + 110) / viewport.Height, 0.3, 0.92);
    }

    private void FocusFirstSoon()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsOpen && _panel?.FirstFocusTarget() is { } target)
            {
                target.Focus(SpecialFocusModality.For(this, Avalonia.Input.NavigationMethod.Tab));
            }
        }, DispatcherPriority.Loaded);
    }

    private void CloseCore()
    {
        if (_popover is { IsOpen: true })
        {
            _popover.Close();
        }
        if (_sheet is { IsOpen: true })
        {
            _sheet.Close();
        }
        _anchor = null;
        if (Content is Control trigger)
        {
            AutomationProperties.SetItemStatus(trigger, "Collapsed");
        }
    }

    /// <summary>The popover or the sheet closed by itself (Escape, a press outside, a drag down): follow it.</summary>
    private void OnHostClosed()
    {
        if (!IsOpen || (_popover?.IsOpen ?? false) || (_sheet?.IsOpen ?? false))
        {
            return;
        }
        _syncing = true;
        try
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        finally
        {
            _syncing = false;
        }
        _anchor = null;
        if (Content is Control trigger)
        {
            AutomationProperties.SetItemStatus(trigger, "Collapsed");
        }
    }

    private void OnClick(object? sender, RoutedEventArgs e)
    {
        // Clicks inside the popover or the sheet bubble here through their logical parent: only the trigger toggles.
        if (Content is not Visual trigger || e.Source is not Visual source
            || !(ReferenceEquals(source, trigger) || trigger.IsVisualAncestorOf(source)))
        {
            return;
        }
        e.Handled = true;
        if (IsOpen)
        {
            Close();
        }
        else
        {
            _anchor = trigger as Control;
            Open();
        }
    }

    private void OnTileInvoked(ShareSheetTile tile)
    {
        RaiseEvent(new ShareSheetTargetEventArgs(SharedEvent, tile.Target, tile.IsPerson));
        if (ClosesOnShare)
        {
            Close();
        }
    }

    private void OnRowInvoked(ShareSheetActionRow row)
    {
        var action = row.Action;
        RaiseEvent(new ShareSheetActionEventArgs(ActionInvokedEvent, action));
        Close();
        if (action.Command?.CanExecute(action.CommandParameter) == true)
        {
            action.Command.Execute(action.CommandParameter);
        }
    }
}
