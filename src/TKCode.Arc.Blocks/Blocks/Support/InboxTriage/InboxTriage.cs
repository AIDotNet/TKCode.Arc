using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>
/// An inbox built for getting through it fast: swipe a row right to archive or left to snooze or assign, or keep your
/// hands on the keys (J / K to move, E archive, S snooze, A assign, L label, Z undo). A counter rolls down as the inbox
/// empties, a bar fills with what is done, every action can be undone from the bar at the bottom, and an empty inbox
/// says so.
/// <code>
/// &lt;arc:InboxTriage Title="Support inbox"&gt;
///     &lt;arc:InboxTriageItem Id="t1" Sender="Ines Duarte" Subject="Refund for order 4471" /&gt;
/// &lt;/arc:InboxTriage&gt;
/// </code>
/// Triaged items leave <see cref="Items"/> (and come back on undo); <see cref="Triaged"/> and <see cref="Undone"/>
/// report each step so your server can follow. An inbox that is empty when it first loads shows a sample.
/// </summary>
[TemplatePart(HostPart, typeof(Decorator))]
[PseudoClasses(EmptyPseudoClass)]
public class InboxTriage : TemplatedControl
{
    public const string HostPart = "PART_Host";

    /// <summary>Set while nothing is left to triage.</summary>
    public const string EmptyPseudoClass = ":empty";

    #region Properties

    /// <summary>The heading ("Inbox").</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<InboxTriage, string>(nameof(Title), "Inbox");

    /// <summary>How long the undo bar stays after an action (6 s); zero keeps it until the next action.</summary>
    public static readonly StyledProperty<TimeSpan> UndoDurationProperty =
        AvaloniaProperty.Register<InboxTriage, TimeSpan>(nameof(UndoDuration), TimeSpan.FromSeconds(6));

    /// <summary>When snoozed items come back, from now; null means tomorrow at 9:00.</summary>
    public static readonly StyledProperty<TimeSpan?> SnoozeForProperty =
        AvaloniaProperty.Register<InboxTriage, TimeSpan?>(nameof(SnoozeFor));

    /// <summary>Whether the row of key hints shows under the header (default true).</summary>
    public static readonly StyledProperty<bool> ShowsShortcutsProperty =
        AvaloniaProperty.Register<InboxTriage, bool>(nameof(ShowsShortcuts), true);

    /// <summary>Whether an inbox that is empty when it first loads gets the sample items, teammates and labels (default true).</summary>
    public static readonly StyledProperty<bool> UsesSampleDataProperty =
        AvaloniaProperty.Register<InboxTriage, bool>(nameof(UsesSampleData), true);

    /// <summary>Items still to triage.</summary>
    public static readonly DirectProperty<InboxTriage, int> RemainingProperty =
        AvaloniaProperty.RegisterDirect<InboxTriage, int>(nameof(Remaining), o => o.Remaining);

    /// <summary>Items triaged since the block loaded (undo takes one back).</summary>
    public static readonly DirectProperty<InboxTriage, int> HandledProperty =
        AvaloniaProperty.RegisterDirect<InboxTriage, int>(nameof(Handled), o => o.Handled);

    /// <summary>The item the keyboard acts on (two-way through <see cref="MoveTo"/>); null when the inbox is empty.</summary>
    public static readonly DirectProperty<InboxTriage, InboxTriageItem?> CurrentItemProperty =
        AvaloniaProperty.RegisterDirect<InboxTriage, InboxTriageItem?>(nameof(CurrentItem), o => o.CurrentItem);

    /// <summary>Raised after an item was archived, snoozed, assigned or labelled.</summary>
    public static readonly RoutedEvent<InboxTriageEventArgs> TriagedEvent =
        RoutedEvent.Register<InboxTriage, InboxTriageEventArgs>(nameof(Triaged), RoutingStrategies.Bubble);

    /// <summary>Raised after an action was undone; <see cref="InboxTriageEventArgs.Action"/> is the action taken back.</summary>
    public static readonly RoutedEvent<InboxTriageEventArgs> UndoneEvent =
        RoutedEvent.Register<InboxTriage, InboxTriageEventArgs>(nameof(Undone), RoutingStrategies.Bubble);

    /// <inheritdoc cref="TitleProperty"/>
    public string Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <inheritdoc cref="UndoDurationProperty"/>
    public TimeSpan UndoDuration { get => GetValue(UndoDurationProperty); set => SetValue(UndoDurationProperty, value); }

    /// <inheritdoc cref="SnoozeForProperty"/>
    public TimeSpan? SnoozeFor { get => GetValue(SnoozeForProperty); set => SetValue(SnoozeForProperty, value); }

    /// <inheritdoc cref="ShowsShortcutsProperty"/>
    public bool ShowsShortcuts { get => GetValue(ShowsShortcutsProperty); set => SetValue(ShowsShortcutsProperty, value); }

    /// <inheritdoc cref="UsesSampleDataProperty"/>
    public bool UsesSampleData { get => GetValue(UsesSampleDataProperty); set => SetValue(UsesSampleDataProperty, value); }

    /// <inheritdoc cref="RemainingProperty"/>
    public int Remaining { get => _remaining; private set => SetAndRaise(RemainingProperty, ref _remaining, value); }

    /// <inheritdoc cref="HandledProperty"/>
    public int Handled { get => _handled; private set => SetAndRaise(HandledProperty, ref _handled, value); }

    /// <inheritdoc cref="CurrentItemProperty"/>
    public InboxTriageItem? CurrentItem { get => _current; private set => SetAndRaise(CurrentItemProperty, ref _current, value); }

    /// <summary>The items to triage, newest first. Triaged items leave this list; undo puts them back where they were.</summary>
    [Content]
    public AvaloniaList<InboxTriageItem> Items { get; } = new();

    /// <summary>The people an item can be assigned to.</summary>
    public AvaloniaList<string> Teammates { get; } = new();

    /// <summary>The labels an item can get.</summary>
    public AvaloniaList<string> AvailableLabels { get; } = new();

    /// <inheritdoc cref="TriagedEvent"/>
    public event EventHandler<InboxTriageEventArgs>? Triaged
    {
        add => AddHandler(TriagedEvent, value);
        remove => RemoveHandler(TriagedEvent, value);
    }

    /// <inheritdoc cref="UndoneEvent"/>
    public event EventHandler<InboxTriageEventArgs>? Undone
    {
        add => AddHandler(UndoneEvent, value);
        remove => RemoveHandler(UndoneEvent, value);
    }

    #endregion

    internal static readonly StyledProperty<double> ListMaxHeightProperty =
        AvaloniaProperty.Register<InboxTriage, double>(nameof(ListMaxHeight), 520);

    internal double ListMaxHeight { get => GetValue(ListMaxHeightProperty); set => SetValue(ListMaxHeightProperty, value); }

    private static readonly CultureInfo s_culture = CultureInfo.GetCultureInfo("en-US");

    private int _remaining;
    private int _handled;
    private InboxTriageItem? _current;
    private readonly Stack<InboxTriageStep> _history = new();
    private readonly Grid _root;
    private readonly TextBlock _titleText;
    private readonly RollingDigits _remainingDigits;
    private readonly TextBlock _remainingWord;
    private readonly Progress _progress;
    private readonly WrapPanel _shortcuts;
    private readonly SwipeActions _swipe;
    private readonly ScrollViewer _scroller;
    private readonly PresenceHost _emptyHost;
    private readonly PresenceHost _undoHost;
    private readonly TextBlock _undoText;
    private readonly Button _undoButton;
    private readonly SupportKitPicker _picker = new();
    private readonly SupportKitAnnouncer _announcer = new();
    private readonly SwipeAction _archive;
    private readonly SwipeAction _snooze;
    private readonly SwipeAction _assign;
    private readonly SwipeAction _label;
    private readonly DispatcherTimer _undoTimer = new();
    private Decorator? _host;
    private bool _loaded;

    /// <summary>Creates the block.</summary>
    public InboxTriage()
    {
        BlockFocusModality.Watch();
        Focusable = true;

        _titleText = SupportKit.Heading(Title, ArcDesignTokenKind.TypeLG);
        _remainingDigits = new RollingDigits { UseGrouping = true, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_remainingDigits, RollingDigits.FontSizeProperty, ArcDesignTokenKind.TypeLG);
        ArcTokens.Bind(_remainingDigits, RollingDigits.ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(_remainingDigits, RollingDigits.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily);
        _remainingWord = SupportKit.Text("left", ArcDesignTokenKind.TextSecondary);
        var counter = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _remainingDigits, _remainingWord } };
        AutomationProperties.SetLiveSetting(counter, AutomationLiveSetting.Polite);

        var header = SupportKit.Grid("*,Auto", columnSpacing: 12);
        header.Children.Add(_titleText.At(0));
        header.Children.Add(counter.At(1));

        _progress = new Progress { Minimum = 0, Maximum = 1, Value = 0, Label = "Triaged" };

        _shortcuts = new WrapPanel { ItemSpacing = 12, LineSpacing = 6 };
        foreach (var (key, word) in new[] { ("J K", "move"), ("E", "archive"), ("S", "snooze"), ("A", "assign"), ("L", "label"), ("Z", "undo") })
        {
            _shortcuts.Children.Add(Shortcut(key, word));
        }

        _archive = new SwipeAction { Label = "Archive", Icon = ArcIcons.Archive, Tone = SwipeActionTone.Accent };
        _snooze = new SwipeAction { Label = "Snooze", Icon = ArcIcons.Clock };
        _assign = new SwipeAction { Label = "Assign", Icon = SupportKit.UserPlus, KeepsRow = true };
        _label = new SwipeAction { Label = "Label", Icon = ArcIcons.Tag, KeepsRow = true };
        _swipe = new SwipeActions { Label = "Inbox", ItemsSource = Items, ItemTemplate = new FuncDataTemplate<InboxTriageItem>((item, _) => item is null ? null : new InboxTriageCard(this, item), supportsRecycling: false) };
        _swipe.LeadingActions.Add(_archive);
        _swipe.TrailingActions.Add(_snooze);
        _swipe.TrailingActions.Add(_assign);
        _swipe.TrailingActions.Add(_label);
        _swipe.AddHandler(SwipeActions.ActionInvokedEvent, OnActionInvoked);
        _scroller = new ScrollViewer { Content = _swipe, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        var emptyIcon = new Border { Width = 48, Height = 48, Child = SupportKit.Glyph(ArcIcons.CheckCheck, 22, ArcDesignTokenKind.Success) }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(emptyIcon, Border.BackgroundProperty, ArcDesignTokenKind.SuccessSubtle);
        var empty = new StackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 32),
            Children =
            {
                emptyIcon,
                SupportKit.Heading("Inbox zero", ArcDesignTokenKind.TypeBase, 3),
                SupportKit.Text("Everything is triaged. Undo brings the last item back.", ArcDesignTokenKind.TextSecondary, wrap: true),
            },
        };
        foreach (var child in empty.Children.OfType<Layoutable>())
        {
            child.HorizontalAlignment = HorizontalAlignment.Center;
        }
        ((TextBlock)empty.Children[2]).TextAlignment = TextAlignment.Center;
        _emptyHost = new PresenceHost { Mode = PresenceMode.Rise, Offset = 8, Child = empty, IsPresent = false };

        _undoText = SupportKit.Text(null, ArcDesignTokenKind.Background, ArcDesignTokenKind.TypeSM, trim: true);
        _undoButton = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = "Undo" };
        ArcTokens.Bind(_undoButton, ForegroundProperty, ArcDesignTokenKind.Background);
        AutomationProperties.SetName(_undoButton, "Undo");
        _undoButton.Click += (_, _) => Undo();
        var undoGrid = SupportKit.Grid("Auto,*,Auto", columnSpacing: 10);
        undoGrid.Children.Add(SupportKit.Glyph(ArcIcons.Undo2, 15, ArcDesignTokenKind.Background).At(0));
        undoGrid.Children.Add(_undoText.At(1));
        undoGrid.Children.Add(_undoButton.At(2));
        var undoBar = new Border { Child = undoGrid, Padding = new Thickness(14, 6, 6, 6), HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 440 }
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(undoBar, Border.BackgroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(undoBar, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowFloating);
        _undoHost = new PresenceHost
        {
            Mode = PresenceMode.Rise,
            Offset = 12,
            Child = undoBar,
            IsPresent = false,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 4),
        };

        var top = new StackPanel { Spacing = 12, Children = { header, _progress, _shortcuts } };
        var list = new Panel { Children = { _scroller, _emptyHost } };
        _root = SupportKit.Grid("*", "Auto,*,Auto", rowSpacing: 14);
        _root.Children.Add(top.At(0, 0));
        _root.Children.Add(list.At(0, 1));
        _root.Children.Add(_undoHost.At(0, 2));
        _root.Children.Add(_announcer.At(0, 2));

        _undoTimer.Tick += (_, _) =>
        {
            _undoTimer.Stop();
            _undoHost.IsPresent = false;
        };
        Items.CollectionChanged += OnItemsChanged;
        AddHandler(KeyDownEvent, OnBlockKeyDown, RoutingStrategies.Bubble);
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Refresh();
    }

    #region Internals for tests

    internal SwipeActions List => _swipe;

    internal SupportKitPicker Picker => _picker;

    internal bool IsUndoVisible => _undoHost.IsPresent;

    internal string? UndoMessage => _undoText.Text;

    internal bool IsEmptyShown => _emptyHost.IsPresent;

    internal string? LastAnnouncement => _announcer.Announcement;

    /// <summary>The clock for "now" (tests give their own).</summary>
    internal Func<DateTimeOffset> Clock { get; set; } = static () => DateTimeOffset.Now;

    #endregion

    #region Public actions

    /// <summary>Moves the keyboard cursor to <paramref name="item"/> and scrolls it into view.</summary>
    public void MoveTo(InboxTriageItem? item)
    {
        if (item is not null && !Items.Contains(item))
        {
            return;
        }
        CurrentItem = item;
        if (item is not null && _swipe.ContainerFromItem(item) is Control row)
        {
            row.BringIntoView();
        }
    }

    /// <summary>Archives <paramref name="item"/> (the current item when null).</summary>
    public void Archive(InboxTriageItem? item = null) => RunSwipe(item ?? CurrentItem, _archive, InboxTriageAction.Archive);

    /// <summary>Snoozes <paramref name="item"/> (the current item when null) for <see cref="SnoozeFor"/>.</summary>
    public void Snooze(InboxTriageItem? item = null) => RunSwipe(item ?? CurrentItem, _snooze, InboxTriageAction.Snooze);

    /// <summary>Hands <paramref name="item"/> to <paramref name="teammate"/>: it leaves the inbox.</summary>
    public void Assign(InboxTriageItem item, string teammate)
    {
        if (!Items.Contains(item))
        {
            return;
        }
        var step = new InboxTriageStep(InboxTriageAction.Assign, item, Items.IndexOf(item), item.Labels, item.Assignee, item.SnoozedUntil, teammate);
        item.Assignee = teammate;
        Remove(step, $"Assigned to {teammate}");
    }

    /// <summary>Adds <paramref name="label"/> to <paramref name="item"/>, or removes it when the item has it. The item stays.</summary>
    public void ToggleLabel(InboxTriageItem item, string label)
    {
        if (!Items.Contains(item))
        {
            return;
        }
        var step = new InboxTriageStep(InboxTriageAction.Label, item, Items.IndexOf(item), item.Labels, item.Assignee, item.SnoozedUntil, label);
        var had = item.Labels.Contains(label);
        item.Labels = had ? item.Labels.Where(l => l != label).ToArray() : item.Labels.Append(label).ToArray();
        _history.Push(step);
        ShowUndo(had ? $"Removed label {label}" : $"Labelled {label}");
        RaiseEvent(new InboxTriageEventArgs(TriagedEvent, item, InboxTriageAction.Label, label));
    }

    /// <summary>Takes back the last action: the item returns to where it was with its old labels and assignee.</summary>
    /// <returns>Whether there was anything to undo.</returns>
    public bool Undo()
    {
        if (!_history.TryPop(out var step))
        {
            return false;
        }
        var item = step.Item;
        item.Labels = step.Labels;
        item.Assignee = step.Assignee;
        item.SnoozedUntil = step.SnoozedUntil;
        if (step.Action != InboxTriageAction.Label)
        {
            Items.Insert(Math.Clamp(step.Index, 0, Items.Count), item);
            Handled = Math.Max(0, Handled - 1);
        }
        MoveTo(item);
        _undoHost.IsPresent = false;
        _undoTimer.Stop();
        _announcer.Announce($"Undone. {item.Subject} is back.");
        RaiseEvent(new InboxTriageEventArgs(UndoneEvent, item, step.Action, step.Value));
        return true;
    }

    /// <summary>Opens the teammate list for the current item.</summary>
    public void OpenAssign() => OpenPicker(InboxTriageAction.Assign, CurrentItem);

    /// <summary>Opens the label list for the current item.</summary>
    public void OpenLabels() => OpenPicker(InboxTriageAction.Label, CurrentItem);

    #endregion

    #region Template and properties

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Decorator>(HostPart);
        SupportKit.Mount(_host, _root);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_loaded)
        {
            return;
        }
        _loaded = true;
        if (UsesSampleData)
        {
            var now = Clock();
            if (Items.Count == 0)
            {
                Items.AddRange(InboxTriageSamples.Items(now));
            }
            if (Teammates.Count == 0)
            {
                Teammates.AddRange(InboxTriageSamples.Teammates);
            }
            if (AvailableLabels.Count == 0)
            {
                AvailableLabels.AddRange(InboxTriageSamples.Labels);
            }
        }
        if (CurrentItem is null && Items.Count > 0)
        {
            CurrentItem = Items[0];
        }
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _undoTimer.Stop();
        _picker.Close();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            _titleText.Text = Title;
            _swipe.Label = Title;
        }
        else if (change.Property == ShowsShortcutsProperty)
        {
            _shortcuts.IsVisible = ShowsShortcuts;
        }
        else if (change.Property == ListMaxHeightProperty)
        {
            _scroller.MaxHeight = ListMaxHeight;
        }
        else if (change.Property == CurrentItemProperty)
        {
            foreach (var card in _swipe.GetRealizedContainers().Select(c => (c as ContentControl)?.Presenter?.Child).OfType<InboxTriageCard>())
            {
                card.Update();
            }
        }
    }

    #endregion

    #region Triage

    private void RunSwipe(InboxTriageItem? item, SwipeAction action, InboxTriageAction kind)
    {
        if (item is null || !Items.Contains(item))
        {
            return;
        }
        // Run the row's own action so it slides away and collapses; the list reports it through ActionInvoked.
        if (_swipe.ContainerFromItem(item) is SwipeActionsRow row && VisualRoot is not null)
        {
            row.Run(action);
            return;
        }
        Apply(item, kind);
    }

    private void OnActionInvoked(object? sender, SwipeActionInvokedEventArgs e)
    {
        if (e.Item is not InboxTriageItem item)
        {
            return;
        }
        if (ReferenceEquals(e.Action, _archive))
        {
            Apply(item, InboxTriageAction.Archive);
        }
        else if (ReferenceEquals(e.Action, _snooze))
        {
            Apply(item, InboxTriageAction.Snooze);
        }
        else if (ReferenceEquals(e.Action, _assign))
        {
            OpenPicker(InboxTriageAction.Assign, item);
        }
        else if (ReferenceEquals(e.Action, _label))
        {
            OpenPicker(InboxTriageAction.Label, item);
        }
    }

    private void Apply(InboxTriageItem item, InboxTriageAction kind)
    {
        if (!Items.Contains(item))
        {
            return;
        }
        var step = new InboxTriageStep(kind, item, Items.IndexOf(item), item.Labels, item.Assignee, item.SnoozedUntil, null);
        if (kind == InboxTriageAction.Snooze)
        {
            var now = Clock();
            item.SnoozedUntil = SnoozeFor is { } span ? now + span : new DateTimeOffset(now.Date.AddDays(1).AddHours(9), now.Offset);
            Remove(step, $"Snoozed until {item.SnoozedUntil.Value.ToString("ddd h:mm tt", s_culture)}");
        }
        else
        {
            Remove(step, "Archived");
        }
    }

    private void Remove(InboxTriageStep step, string verb)
    {
        var item = step.Item;
        var index = Items.IndexOf(item);
        var focusWithin = IsKeyboardFocusWithin;
        Items.Remove(item);
        Handled++;
        _history.Push(step);
        // The cursor stays at the same place in the list: on the next item, or the one before at the end.
        var next = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
        MoveTo(next);
        if (focusWithin)
        {
            Focus(NavigationMethod.Tab);
        }
        ShowUndo($"{verb}: {item.Subject}");
        RaiseEvent(new InboxTriageEventArgs(TriagedEvent, item, step.Action, step.Value));
    }

    private void ShowUndo(string message)
    {
        _undoText.Text = message;
        _undoHost.IsPresent = true;
        _announcer.Announce($"{message}. {SupportKit.Count(Items.Count, "item", "items")} left.");
        _undoTimer.Stop();
        if (UndoDuration > TimeSpan.Zero)
        {
            _undoTimer.Interval = UndoDuration;
            _undoTimer.Start();
        }
    }

    private void OpenPicker(InboxTriageAction kind, InboxTriageItem? item)
    {
        if (item is null || !Items.Contains(item))
        {
            return;
        }
        CurrentItem = item;
        Control anchor = _swipe.ContainerFromItem(item) as Control ?? this;
        if (kind == InboxTriageAction.Assign)
        {
            _picker.Open(anchor, "Assign to", Teammates.Select(t => (t, (string?)null)), item.Assignee is { } a ? new HashSet<string> { a } : null,
                teammate => Assign(item, teammate));
        }
        else
        {
            _picker.Open(anchor, "Label", AvailableLabels.Select(l => (l, (string?)null)), new HashSet<string>(item.Labels),
                label => ToggleLabel(item, label));
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (CurrentItem is not null && !Items.Contains(CurrentItem))
        {
            CurrentItem = Items.Count > 0 ? Items[0] : null;
        }
        else if (CurrentItem is null && Items.Count > 0 && _loaded)
        {
            CurrentItem = Items[0];
        }
        Refresh();
    }

    private void Refresh()
    {
        Remaining = Items.Count;
        _remainingDigits.Value = Items.Count;
        var total = Items.Count + Handled;
        _progress.Value = total == 0 ? 0 : (double)Handled / total;
        AutomationProperties.SetName(_progress, $"{Handled} of {total} triaged");
        var empty = Items.Count == 0;
        PseudoClasses.Set(EmptyPseudoClass, empty);
        _emptyHost.IsPresent = empty && _loaded;
        _emptyHost.IsHitTestVisible = empty;
    }

    #endregion

    #region Input

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // A press on a row makes it the keyboard's current item.
        for (var visual = e.Source as Visual; visual is not null && !ReferenceEquals(visual, this); visual = visual.GetVisualParent())
        {
            if (visual is InboxTriageCard card)
            {
                CurrentItem = card.Item;
                return;
            }
        }
    }

    private void OnBlockKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Source is TextBox || _picker.IsOpen)
        {
            return;
        }
        var ctrl = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (ctrl && e.Key != Key.Z)
        {
            return;
        }
        var at = CurrentItem is null ? -1 : Items.IndexOf(CurrentItem);
        switch (e.Key)
        {
            case Key.J or Key.Down:
                MoveTo(Items.Count == 0 ? null : Items[Math.Min(Items.Count - 1, at + 1)]);
                break;
            case Key.K or Key.Up:
                MoveTo(Items.Count == 0 ? null : Items[Math.Max(0, at - 1)]);
                break;
            case Key.Home:
                MoveTo(Items.FirstOrDefault());
                break;
            case Key.End:
                MoveTo(Items.LastOrDefault());
                break;
            case Key.E:
                Archive();
                break;
            case Key.S:
                Snooze();
                break;
            case Key.A:
                OpenAssign();
                break;
            case Key.L:
                OpenLabels();
                break;
            case Key.U when CurrentItem is { } item:
                item.IsUnread = !item.IsUnread;
                _announcer.Announce(item.IsUnread ? "Marked unread." : "Marked read.");
                break;
            case Key.Z:
                Undo();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    #endregion

    private static Control Shortcut(string keys, string word)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        foreach (var key in keys.Split(' '))
        {
            var face = SupportKit.Text(key, ArcDesignTokenKind.TextSecondary, ArcDesignTokenKind.TypeXS, FontWeight.Medium);
            face.HorizontalAlignment = HorizontalAlignment.Center;
            ArcTokens.Bind(face, TextBlock.FontFamilyProperty, ArcDesignTokenKind.MonoFontFamily);
            var cap = new Border { Padding = new Thickness(5, 0), MinWidth = 18, Child = face }
                .BindCornerRadius(ArcDesignTokenKind.RadiusStep4);
            ArcTokens.Bind(cap, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceMuted);
            ArcTokens.Bind(cap, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
            ArcTokens.Bind(cap, Border.BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
            row.Children.Add(cap);
        }
        row.Children.Add(SupportKit.Text(word, ArcDesignTokenKind.TextMuted, ArcDesignTokenKind.TypeXS));
        return row;
    }
}

/// <summary>One undoable step: the item, where it was and what it had before.</summary>
internal sealed record InboxTriageStep(InboxTriageAction Action, InboxTriageItem Item, int Index, IReadOnlyList<string> Labels,
    string? Assignee, DateTimeOffset? SnoozedUntil, string? Value);
