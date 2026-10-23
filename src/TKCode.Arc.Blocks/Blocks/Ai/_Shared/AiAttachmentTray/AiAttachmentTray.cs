using System.Collections;
using System.Collections.Specialized;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TKCode.Arc;

/// <summary>
/// The composer's row of attachment chips (Arc's attachments list): chips 8 apart in a row that scrolls sideways with no
/// scrollbar and never jumps to the newest chip on its own. It follows <see cref="ItemsSource"/> (and its
/// <see cref="INotifyCollectionChanged"/> changes): a new chip grows from 0.92 and fades in; a removed one shrinks and
/// fades, and only then do the chips after it slide over on a spring. With no attachments the row folds to zero height
/// (and fades) on the smooth spring; it then holds nothing to focus.
/// <para>
/// Layout: give the tray the composer's content width. With chips it takes 60px (52 + 8 below); its scrolling viewport
/// reaches 4px past its left, top and right edges (Arc's −4 inset), so focus rings and the entering scale are not cut.
/// </para>
/// The chip texts are forwarded to every chip; the chips' <see cref="RemoveRequested"/> and
/// <see cref="CancelRequested"/> bubble through the tray with the attachment, and the owner changes the collection.
/// Focus inside a chip that leaves moves to the next chip's button (else the previous one); when no chip is left the
/// owner decides where focus goes (the composer: its text box).
/// <para>
/// With <see cref="ChipAppearance"/> <see cref="AiAttachmentChipAppearance.Compact"/> (the AI chat's input) the chips
/// are 32px pills 6 apart, the row sits 2px in from the tray's top, left and right edges and keeps 6px below it (the
/// gap to the text row), so it takes 40px; it opens and folds on the settle spring (Arc's attachment row, spec ai-chat
/// 5.5).
/// </para>
/// </summary>
[TemplatePart(SizePart, typeof(SpringSizeDecorator))]
[TemplatePart(ScrollerPart, typeof(ScrollViewer))]
[TemplatePart(PanelPart, typeof(AiAttachmentTrayPanel))]
[PseudoClasses(EmptyPseudoClass, CompactPseudoClass)]
internal class AiAttachmentTray : TemplatedControl
{
    public const string SizePart = "PART_Size";
    public const string ScrollerPart = "PART_Scroller";
    public const string PanelPart = "PART_Panel";

    /// <summary>Set while there is no attachment.</summary>
    public const string EmptyPseudoClass = ":empty";

    /// <summary>Set while <see cref="ChipAppearance"/> is <see cref="AiAttachmentChipAppearance.Compact"/>.</summary>
    public const string CompactPseudoClass = ":compact";

    #region Properties

    /// <summary>The attachments, in order; a collection that raises <see cref="INotifyCollectionChanged"/> is followed.</summary>
    public static readonly StyledProperty<IEnumerable<AiAttachment>?> ItemsSourceProperty =
        AvaloniaProperty.Register<AiAttachmentTray, IEnumerable<AiAttachment>?>(nameof(ItemsSource));

    /// <summary>The look of every chip: the composer's cards (default) or the chat's compact pills.</summary>
    public static readonly StyledProperty<AiAttachmentChipAppearance> ChipAppearanceProperty =
        AvaloniaProperty.Register<AiAttachmentTray, AiAttachmentChipAppearance>(nameof(ChipAppearance));

    /// <inheritdoc cref="AiAttachmentChip.RemoveTextProperty"/>
    public static readonly StyledProperty<Func<string, string>?> RemoveTextProperty =
        AiAttachmentChip.RemoveTextProperty.AddOwner<AiAttachmentTray>();

    /// <inheritdoc cref="AiAttachmentChip.CancelTextProperty"/>
    public static readonly StyledProperty<Func<string, string>?> CancelTextProperty =
        AiAttachmentChip.CancelTextProperty.AddOwner<AiAttachmentTray>();

    /// <inheritdoc cref="AiAttachmentChip.UploadingTextProperty"/>
    public static readonly StyledProperty<Func<int, string>?> UploadingTextProperty =
        AiAttachmentChip.UploadingTextProperty.AddOwner<AiAttachmentTray>();

    /// <inheritdoc cref="AiAttachmentChip.ErrorTextProperty"/>
    public static readonly StyledProperty<string?> ErrorTextProperty =
        AiAttachmentChip.ErrorTextProperty.AddOwner<AiAttachmentTray>();

    /// <inheritdoc cref="AiAttachmentChip.ImageTextProperty"/>
    public static readonly StyledProperty<string?> ImageTextProperty =
        AiAttachmentChip.ImageTextProperty.AddOwner<AiAttachmentTray>();

    /// <inheritdoc cref="AiAttachmentChip.LinesTextProperty"/>
    public static readonly StyledProperty<Func<int, string>?> LinesTextProperty =
        AiAttachmentChip.LinesTextProperty.AddOwner<AiAttachmentTray>();

    /// <summary>Whether there is at least one attachment (a chip on its way out does not count).</summary>
    public static readonly DirectProperty<AiAttachmentTray, bool> HasItemsProperty =
        AvaloniaProperty.RegisterDirect<AiAttachmentTray, bool>(nameof(HasItems), o => o.HasItems);

    /// <summary>A chip's remove button was pressed (the chip's own event, bubbling through the tray).</summary>
    public static readonly RoutedEvent<AiAttachmentRequestEventArgs> RemoveRequestedEvent = AiAttachmentChip.RemoveRequestedEvent;

    /// <summary>A chip's cancel button was pressed (the chip's own event, bubbling through the tray).</summary>
    public static readonly RoutedEvent<AiAttachmentRequestEventArgs> CancelRequestedEvent = AiAttachmentChip.CancelRequestedEvent;

    /// <inheritdoc cref="ItemsSourceProperty"/>
    public IEnumerable<AiAttachment>? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <inheritdoc cref="ChipAppearanceProperty"/>
    public AiAttachmentChipAppearance ChipAppearance { get => GetValue(ChipAppearanceProperty); set => SetValue(ChipAppearanceProperty, value); }

    /// <inheritdoc cref="RemoveTextProperty"/>
    public Func<string, string>? RemoveText { get => GetValue(RemoveTextProperty); set => SetValue(RemoveTextProperty, value); }

    /// <inheritdoc cref="CancelTextProperty"/>
    public Func<string, string>? CancelText { get => GetValue(CancelTextProperty); set => SetValue(CancelTextProperty, value); }

    /// <inheritdoc cref="UploadingTextProperty"/>
    public Func<int, string>? UploadingText { get => GetValue(UploadingTextProperty); set => SetValue(UploadingTextProperty, value); }

    /// <inheritdoc cref="ErrorTextProperty"/>
    public string? ErrorText { get => GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }

    /// <inheritdoc cref="ImageTextProperty"/>
    public string? ImageText { get => GetValue(ImageTextProperty); set => SetValue(ImageTextProperty, value); }

    /// <inheritdoc cref="LinesTextProperty"/>
    public Func<int, string>? LinesText { get => GetValue(LinesTextProperty); set => SetValue(LinesTextProperty, value); }

    /// <inheritdoc cref="HasItemsProperty"/>
    public bool HasItems { get => _hasItems; private set => SetAndRaise(HasItemsProperty, ref _hasItems, value); }

    /// <inheritdoc cref="RemoveRequestedEvent"/>
    public event EventHandler<AiAttachmentRequestEventArgs>? RemoveRequested
    {
        add => AddHandler(RemoveRequestedEvent, value);
        remove => RemoveHandler(RemoveRequestedEvent, value);
    }

    /// <inheritdoc cref="CancelRequestedEvent"/>
    public event EventHandler<AiAttachmentRequestEventArgs>? CancelRequested
    {
        add => AddHandler(CancelRequestedEvent, value);
        remove => RemoveHandler(CancelRequestedEvent, value);
    }

    #endregion

    // The chips for the attachments, in source order; chips on their way out are only in the panel.
    private readonly List<AiAttachmentTrayItem> _live = new();
    private AiAttachmentTrayPanel? _panel;
    private INotifyCollectionChanged? _observed;
    private bool _hasItems;

    static AiAttachmentTray()
    {
        FocusableProperty.OverrideDefaultValue<AiAttachmentTray>(false);
    }

    public AiAttachmentTray()
    {
        PseudoClasses.Set(EmptyPseudoClass, true);
    }

    /// <summary>The chips showing, leaving ones included, in panel order.</summary>
    internal IEnumerable<AiAttachmentTrayItem> Items => _panel?.Children.OfType<AiAttachmentTrayItem>() ?? _live;

    /// <summary>The chips of the current attachments, in order.</summary>
    internal IReadOnlyList<AiAttachmentTrayItem> LiveItems => _live;

    /// <summary>The chip of <paramref name="attachment"/>, or null.</summary>
    internal AiAttachmentChip? ChipFor(AiAttachment attachment) => _live.FirstOrDefault(i => ReferenceEquals(i.Attachment, attachment))?.Chip;

    /// <summary>The scrolling viewport.</summary>
    internal ScrollViewer? Scroller { get; private set; }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _panel?.Children.Clear();
        _panel = e.NameScope.Find<AiAttachmentTrayPanel>(PanelPart);
        Scroller = e.NameScope.Find<ScrollViewer>(ScrollerPart);
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty)
        {
            Observe(this.IsAttachedToVisualTree() ? ItemsSource : null);
            Rebuild();
        }
        else if (change.Property == RemoveTextProperty || change.Property == CancelTextProperty || change.Property == UploadingTextProperty
                 || change.Property == ErrorTextProperty || change.Property == ImageTextProperty || change.Property == LinesTextProperty)
        {
            foreach (var item in Items)
            {
                ApplyTexts(item.Chip);
            }
        }
        else if (change.Property == ChipAppearanceProperty)
        {
            PseudoClasses.Set(CompactPseudoClass, change.GetNewValue<AiAttachmentChipAppearance>() == AiAttachmentChipAppearance.Compact);
            foreach (var item in Items)
            {
                item.Chip.Appearance = ChipAppearance;
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(ItemsSource);
        // The collection may have changed while the tray was away: start over without motion.
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
    }

    private void Observe(IEnumerable<AiAttachment>? source)
    {
        var observed = source as INotifyCollectionChanged;
        if (ReferenceEquals(observed, _observed))
        {
            return;
        }
        if (_observed is not null)
        {
            _observed.CollectionChanged -= OnCollectionChanged;
        }
        _observed = observed;
        if (_observed is not null)
        {
            _observed.CollectionChanged += OnCollectionChanged;
        }
    }

    #region Items

    /// <summary>One chip per attachment, without motion (first show, a new source, a reset).</summary>
    private void Rebuild()
    {
        _live.Clear();
        if (_panel is null)
        {
            // No template yet: the chips are made when it arrives; only whether there are any is known now.
            HasItems = ItemsSource?.Any() == true;
            PseudoClasses.Set(EmptyPseudoClass, !HasItems);
            return;
        }
        _panel.Children.Clear();
        if (ItemsSource is { } source)
        {
            foreach (var attachment in source)
            {
                Insert(_live.Count, attachment, animate: false);
            }
        }
        UpdateHasItems();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                AddRange(e.NewStartingIndex, e.NewItems);
                break;
            case NotifyCollectionChangedAction.Remove:
                RemoveRange(e.OldStartingIndex, e.OldItems);
                break;
            case NotifyCollectionChangedAction.Replace:
                RemoveRange(e.OldStartingIndex, e.OldItems);
                AddRange(e.NewStartingIndex, e.NewItems);
                break;
            case NotifyCollectionChangedAction.Move:
                Move(e.OldStartingIndex, e.NewStartingIndex, e.OldItems?.Count ?? 0);
                break;
            default:
                Rebuild();
                return;
        }
        UpdateHasItems();
    }

    private void AddRange(int index, IList? items)
    {
        if (items is null)
        {
            return;
        }
        if (index < 0 || index > _live.Count)
        {
            index = _live.Count;
        }
        foreach (var attachment in items.OfType<AiAttachment>())
        {
            Insert(index++, attachment, animate: true);
        }
    }

    private void RemoveRange(int index, IList? items)
    {
        if (items is null)
        {
            return;
        }
        foreach (var attachment in items.OfType<AiAttachment>())
        {
            // The index says which one when the same attachment is listed twice.
            var at = index >= 0 && index < _live.Count && ReferenceEquals(_live[index].Attachment, attachment)
                ? index
                : _live.FindIndex(i => ReferenceEquals(i.Attachment, attachment));
            if (at < 0)
            {
                continue;
            }
            var item = _live[at];
            _live.RemoveAt(at);
            Leave(item, at);
        }
    }

    private void Move(int from, int to, int count)
    {
        if (_panel is null || count <= 0 || from < 0 || to < 0 || from + count > _live.Count)
        {
            Rebuild();
            return;
        }
        var moving = _live.GetRange(from, count);
        _live.RemoveRange(from, count);
        to = Math.Min(to, _live.Count);
        _live.InsertRange(to, moving);
        // Every chip that changes place slides there from where it was.
        _panel.RequestShift();
        foreach (var item in moving)
        {
            _panel.Children.Remove(item);
        }
        var following = to + count < _live.Count ? _live[to + count] : null;
        var panelIndex = following is not null ? _panel.Children.IndexOf(following) : _panel.Children.Count;
        _panel.Children.InsertRange(panelIndex < 0 ? _panel.Children.Count : panelIndex, moving);
    }

    private void Insert(int index, AiAttachment attachment, bool animate)
    {
        var chip = new AiAttachmentChip { Attachment = attachment };
        ApplyTexts(chip);
        var item = new AiAttachmentTrayItem(attachment, chip);
        var level = ArcMotion.GetEffectiveLevel(this);
        var moving = animate && IsLoaded && level != ArcMotionLevel.Off;
        item.Presence.IsPresent = !moving;
        if (_panel is not null)
        {
            // Before the chip that now follows it; after everything (leaving chips included) at the end.
            var panelIndex = index < _live.Count ? _panel.Children.IndexOf(_live[index]) : _panel.Children.Count;
            if (moving)
            {
                _panel.RequestShift();
            }
            _panel.Children.Insert(panelIndex < 0 ? _panel.Children.Count : panelIndex, item);
        }
        _live.Insert(index, item);
        if (moving)
        {
            // PresenceHost plays nothing before it is loaded, so the entrance starts once it is.
            item.EnterWhenLoaded();
        }
    }

    private void Leave(AiAttachmentTrayItem item, int index)
    {
        MoveFocusFrom(item, index);
        var level = ArcMotion.GetEffectiveLevel(this);
        if (_panel is null || !IsLoaded || !item.IsLoaded || level == ArcMotionLevel.Off)
        {
            RemoveFromPanel(item);
            return;
        }
        item.BeginLeave(() => RemoveFromPanel(item));
    }

    private void RemoveFromPanel(AiAttachmentTrayItem item)
    {
        if (_panel is null || !_panel.Children.Contains(item))
        {
            return;
        }
        // The chips after it slide over from where they were (FLIP), now that its slot is gone.
        _panel.RequestShift();
        _panel.Children.Remove(item);
    }

    /// <summary>Focus in a leaving chip moves to the chip now at its place, else the one before it.</summary>
    private void MoveFocusFrom(AiAttachmentTrayItem item, int index)
    {
        if (!item.IsKeyboardFocusWithin)
        {
            return;
        }
        var neighbor = index < _live.Count ? _live[index] : index > 0 ? _live[index - 1] : null;
        if (neighbor?.Chip.ActionButton is not { } button)
        {
            return;
        }
        var keyboard = item.Chip.ActionButton?.Classes.Contains(":focus-visible") == true;
        button.Focus(keyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
    }

    private void ApplyTexts(AiAttachmentChip chip)
    {
        chip.Appearance = ChipAppearance;
        chip.RemoveText = RemoveText;
        chip.CancelText = CancelText;
        chip.UploadingText = UploadingText;
        chip.ErrorText = ErrorText;
        chip.ImageText = ImageText;
        chip.LinesText = LinesText;
    }

    private void UpdateHasItems()
    {
        HasItems = _live.Count > 0;
        PseudoClasses.Set(EmptyPseudoClass, !HasItems);
    }

    #endregion

    protected override AutomationPeer OnCreateAutomationPeer() => new TrayAutomationPeer(this);

    /// <summary>A list ("Attachments", named by the owner) of chip list items.</summary>
    private sealed class TrayAutomationPeer(AiAttachmentTray owner) : ControlAutomationPeer(owner)
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
    }
}
