using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

// The parts of ShareAccess, built once when the template applies and then updated in place.
public partial class ShareAccess
{
    private static readonly ShareAccessLinkScope[] s_scopes =
        [ShareAccessLinkScope.Restricted, ShareAccessLinkScope.Organization, ShareAccessLinkScope.Anyone];

    private readonly Dictionary<ShareAccessPerson, (PresenceHost Host, ShareAccessPersonRow Row)> _rows = [];
    private Panel? _root;
    private TextBlock? _title;
    private Grid? _inviteGrid;
    private TagInput? _invite;
    private Select? _inviteRole;
    private Button? _inviteButton;
    private TextBlock? _peopleHeading;
    private ScrollViewer? _scroller;
    private StackPanel? _list;
    private TextBlock? _generalHeading;
    private Border? _scopeTile;
    private ArcContentSwap? _scopeIcon;
    private Select? _scopeSelect;
    private ArcContentSwap? _scopeDescription;
    private PresenceHost? _linkRoleHost;
    private Select? _linkRoleSelect;
    private CopyButton? _copy;
    private Button? _done;
    private Grid? _footer;
    private TextBlock? _status;
    private bool _syncingPickers;
    private bool _lastInputKeyboard;
    private bool? _narrow;
    private ShareAccessLinkScope? _shownScope;

    /// <summary>The address field (tests reach the chips through it).</summary>
    internal TagInput? InviteField => _invite;

    internal Button? InviteButton => _inviteButton;

    internal Select? ScopePicker => _scopeSelect;

    internal Select? LinkRolePicker => _linkRoleSelect;

    internal Select? InviteRolePicker => _inviteRole;

    internal PresenceHost? LinkRoleHost => _linkRoleHost;

    internal CopyButton? CopyLinkButton => _copy;

    internal Button? DoneButton => _done;

    internal string? LastAnnouncement => _status?.Text;

    internal ShareAccessPersonRow? RowFor(ShareAccessPerson person) => _rows.TryGetValue(person, out var entry) ? entry.Row : null;

    private void Build(Panel root)
    {
        _root = root;
        AddHandler(KeyDownEvent, OnAnyKey, RoutingStrategies.Tunnel, handledEventsToo: true);
        AddHandler(PointerPressedEvent, OnAnyPointer, RoutingStrategies.Tunnel, handledEventsToo: true);

        var column = new StackPanel().ShareTok(StackPanel.SpacingProperty, ShareAccessTokenKind.SectionGap);
        _title = new TextBlock { TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis }
            .ShareArc(TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeLG)
            .ShareArc(TextBlock.FontFamilyProperty, ArcDesignTokenKind.DisplayFontFamily)
            .ShareArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _title.FontWeight = FontWeight.SemiBold;
        AutomationProperties.SetHeadingLevel(_title, 2);
        column.Children.Add(_title);
        column.Children.Add(BuildInvite());
        column.Children.Add(BuildPeople());
        column.Children.Add(BuildGeneral());
        column.Children.Add(BuildFooter());
        root.Children.Add(column);

        _status = new TextBlock { Height = 0, Opacity = 0, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        root.Children.Add(_status);

        RefreshWords();
        SyncRows(animate: false);
        ShowLinkAccess(animate: false);
        UpdateNarrow();
    }

    private void Teardown()
    {
        RemoveHandler(KeyDownEvent, OnAnyKey);
        RemoveHandler(PointerPressedEvent, OnAnyPointer);
        if (_invite is not null)
        {
            _invite.ValueChanged -= OnInviteValueChanged;
            _invite.RemoveHandler(KeyDownEvent, OnInviteKeyDown);
        }
        foreach (var person in _rows.Keys)
        {
            person.Changed -= OnPersonChanged;
        }
        _rows.Clear();
        _root?.Children.Clear();
        _root = null;
        _narrow = null;
        _shownScope = null;
    }

    private static TextBlock SectionHeading() =>
        new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            .ShareArc(TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS)
            .ShareArc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .ShareArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);

    private static Select RoleSelect(bool includeOwner = false)
    {
        var select = new Select { Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Top };
        var count = includeOwner ? 4 : 3;
        for (var i = 0; i < count; i++)
        {
            select.Items.Add(new SelectItem());
        }
        return select;
    }

    private Control BuildInvite()
    {
        _invite = new TagInput { IsLabelVisible = false };
        _invite.ValueChanged += OnInviteValueChanged;
        _invite.AddHandler(KeyDownEvent, OnInviteKeyDown, RoutingStrategies.Tunnel);

        _inviteRole = RoleSelect().ShareTok(WidthProperty, ShareAccessTokenKind.InviteRoleWidth);
        _inviteRole.SelectionChanged += (_, _) =>
        {
            if (!_syncingPickers && _inviteRole.SelectedIndex >= 0)
            {
                SetCurrentValue(InviteRoleProperty, (ShareAccessRole)_inviteRole.SelectedIndex);
            }
        };

        _inviteButton = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, VerticalAlignment = VerticalAlignment.Top, IsEnabled = false };
        _inviteButton.Click += async (_, _) => await InviteAsync();

        _inviteGrid = new Grid()
            .ShareTok(Grid.ColumnSpacingProperty, ShareAccessTokenKind.InlineGap)
            .ShareTok(Grid.RowSpacingProperty, ShareAccessTokenKind.InlineGap);
        _inviteGrid.Children.Add(_invite);
        _inviteGrid.Children.Add(_inviteRole);
        _inviteGrid.Children.Add(_inviteButton);
        SyncPickers();
        return _inviteGrid;
    }

    private Control BuildPeople()
    {
        _peopleHeading = SectionHeading();
        _list = new StackPanel();
        AutomationProperties.SetAccessibilityView(_list, AccessibilityView.Content);
        _scroller = new ScrollViewer
        {
            Content = _list,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        }.ShareTok(MaxHeightProperty, ShareAccessTokenKind.ListMaxHeight);
        var section = new StackPanel().ShareTok(StackPanel.SpacingProperty, ShareAccessTokenKind.HeadingGap);
        section.Children.Add(_peopleHeading);
        section.Children.Add(_scroller);
        return section;
    }

    private Control BuildGeneral()
    {
        _generalHeading = SectionHeading();
        _scopeIcon = new ArcContentSwap { Mode = ContentSwapMode.Pop, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _scopeTile = new Border { Child = _scopeIcon, VerticalAlignment = VerticalAlignment.Top }
            .ShareTok(WidthProperty, ShareAccessTokenKind.ScopeTileSize)
            .ShareTok(HeightProperty, ShareAccessTokenKind.ScopeTileSize)
            .BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        AutomationProperties.SetAccessibilityView(_scopeTile, AccessibilityView.Raw);

        _scopeSelect = new Select { Size = ControlSize.Small, HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 180 };
        foreach (var _ in s_scopes)
        {
            _scopeSelect.Items.Add(new SelectItem());
        }
        _scopeSelect.SelectionChanged += (_, _) =>
        {
            if (!_syncingPickers && _scopeSelect.SelectedIndex >= 0)
            {
                SetCurrentValue(LinkScopeProperty, s_scopes[_scopeSelect.SelectedIndex]);
            }
        };
        _scopeDescription = new ArcContentSwap { Mode = ContentSwapMode.Label, HorizontalContentAlignment = HorizontalAlignment.Left };
        _scopeDescription.ShareArc(TextElement.ForegroundProperty, ArcDesignTokenKind.TextSecondary)
            .ShareArc(TextElement.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        var words = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(_scopeSelect);
        words.Children.Add(_scopeDescription);

        _linkRoleSelect = RoleSelect().ShareTok(WidthProperty, ShareAccessTokenKind.LinkRoleWidth);
        _linkRoleSelect.SelectionChanged += (_, _) =>
        {
            if (!_syncingPickers && _linkRoleSelect.SelectedIndex >= 0)
            {
                SetCurrentValue(LinkRoleProperty, (ShareAccessRole)_linkRoleSelect.SelectedIndex);
            }
        };
        _linkRoleHost = new PresenceHost
        {
            Child = _linkRoleSelect,
            Mode = PresenceMode.Fade,
            Collapse = PresenceCollapse.Width,
            IsPresent = LinkScope != ShareAccessLinkScope.Restricted,
            VerticalAlignment = VerticalAlignment.Top
        };

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") }
            .ShareTok(Grid.ColumnSpacingProperty, ShareAccessTokenKind.AvatarGap);
        row.Children.Add(_scopeTile);
        Grid.SetColumn(words, 1);
        row.Children.Add(words);
        Grid.SetColumn(_linkRoleHost, 2);
        row.Children.Add(_linkRoleHost);

        var section = new StackPanel().ShareTok(StackPanel.SpacingProperty, ShareAccessTokenKind.HeadingGap);
        section.Children.Add(_generalHeading);
        section.Children.Add(row);
        return section;
    }

    private Control BuildFooter()
    {
        _copy = new CopyButton { Variant = CopyButtonVariant.Outline, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        _done = new Button { Variant = ButtonVariant.Primary, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
        _done.Click += (_, e) =>
        {
            e.Handled = true;
            RaiseEvent(new RoutedEventArgs(DoneRequestedEvent));
        };
        _footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }
            .ShareTok(Grid.ColumnSpacingProperty, ShareAccessTokenKind.InlineGap);
        _footer.Children.Add(_copy);
        Grid.SetColumn(_done, 1);
        _footer.Children.Add(_done);
        return _footer;
    }

    // ---- Words and state ----

    private void RefreshWords()
    {
        if (_root is null)
        {
            return;
        }
        var labels = Labels;
        _title!.Text = labels.Title(DocumentName);
        _invite!.Placeholder = labels.InvitePlaceholder;
        _invite.Label = labels.InviteField;
        AutomationProperties.SetName(_inviteRole!, labels.InviteRole);
        _inviteButton!.Content = labels.Invite;
        _peopleHeading!.Text = labels.People;
        _generalHeading!.Text = labels.General;
        AutomationProperties.SetName(_scopeSelect!, labels.ScopePicker);
        AutomationProperties.SetName(_linkRoleSelect!, labels.LinkRolePicker);
        AutomationProperties.SetName(_list!, labels.People);
        for (var i = 0; i < 3; i++)
        {
            ((SelectItem)_inviteRole!.Items[i]!).Content = labels.Role((ShareAccessRole)i);
            ((SelectItem)_linkRoleSelect!.Items[i]!).Content = labels.Role((ShareAccessRole)i);
            ((SelectItem)_scopeSelect!.Items[i]!).Content = labels.Scope(s_scopes[i], OrganizationName);
        }
        _copy!.Value = Link;
        _copy.Label = labels.CopyLink;
        _copy.CopiedLabel = labels.Copied;
        _copy.IsEnabled = !string.IsNullOrEmpty(Link);
        _done!.Content = labels.Done;
        foreach (var (_, row) in _rows.Values)
        {
            row.Refresh(animate: false);
        }
        _shownScope = null;
        ShowLinkAccess(animate: false);
        UpdateInviteEnabled();
    }

    private void SyncPickers()
    {
        _syncingPickers = true;
        try
        {
            if (_inviteRole is not null)
            {
                _inviteRole.SelectedIndex = (int)InviteRole;
            }
            if (_linkRoleSelect is not null)
            {
                _linkRoleSelect.SelectedIndex = (int)LinkRole;
            }
            if (_scopeSelect is not null)
            {
                _scopeSelect.SelectedIndex = Array.IndexOf(s_scopes, LinkScope);
            }
        }
        finally
        {
            _syncingPickers = false;
        }
    }

    /// <summary>Shows the link scope: its icon tile, its description and, unless restricted, the link role picker.</summary>
    private void ShowLinkAccess(bool animate)
    {
        SyncPickers();
        if (_root is null)
        {
            return;
        }
        var scope = LinkScope;
        var changed = _shownScope != scope;
        var motion = animate && changed && _shownScope is not null && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        _scopeIcon!.Mode = motion ? ContentSwapMode.Pop : ContentSwapMode.None;
        _scopeDescription!.Mode = motion ? ContentSwapMode.Label : ContentSwapMode.None;
        if (changed)
        {
            _scopeIcon.Content = new ArcGlyph
            {
                Data = scope switch
                {
                    ShareAccessLinkScope.Restricted => ArcIcons.Lock,
                    ShareAccessLinkScope.Organization => ArcIcons.Users,
                    _ => ArcIcons.Globe
                }
            }.ShareTok(ArcGlyph.SizeProperty, ShareAccessTokenKind.ScopeIconSize)
             .ShareArc(ArcGlyph.ForegroundProperty, scope == ShareAccessLinkScope.Restricted ? ArcDesignTokenKind.TextSecondary : ArcDesignTokenKind.Accent);
            _scopeTile!.ShareArc(Border.BackgroundProperty, scope == ShareAccessLinkScope.Restricted ? ArcDesignTokenKind.SurfaceMuted : ArcDesignTokenKind.AccentSubtle);
        }
        var description = Labels.ScopeDescription(scope, OrganizationName);
        if (_scopeDescription.Content as string != description)
        {
            _scopeDescription.Content = description;
        }
        _linkRoleHost!.IsPresent = scope != ShareAccessLinkScope.Restricted;
        _shownScope = scope;
    }

    private void UpdateNarrow()
    {
        if (_inviteGrid is null || _invite is null || _inviteRole is null || _inviteButton is null)
        {
            return;
        }
        var breakpoint = this.TryFindResource(ShareAccessTokens.Key(ShareAccessTokenKind.NarrowWidth), out var value) && value is double w ? w : 460;
        var narrow = Bounds.Width > 0 && Bounds.Width < breakpoint;
        if (_narrow == narrow)
        {
            return;
        }
        _narrow = narrow;
        PseudoClasses.Set(NarrowPseudoClass, narrow);
        _inviteGrid.ColumnDefinitions = narrow ? new ColumnDefinitions("*,Auto") : new ColumnDefinitions("*,Auto,Auto");
        _inviteGrid.RowDefinitions = narrow ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto");
        Grid.SetRow(_invite, 0);
        Grid.SetColumn(_invite, 0);
        Grid.SetColumnSpan(_invite, narrow ? 2 : 1);
        Grid.SetRow(_inviteRole, narrow ? 1 : 0);
        Grid.SetColumn(_inviteRole, narrow ? 0 : 1);
        Grid.SetRow(_inviteButton, narrow ? 1 : 0);
        Grid.SetColumn(_inviteButton, narrow ? 1 : 2);
        _inviteRole.HorizontalAlignment = narrow ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
    }

    private void UpdateInviteEnabled()
    {
        if (_inviteButton is null || _invite is null)
        {
            return;
        }
        _inviteButton.IsEnabled = !_isInviting && (_invite.Value.Count > 0 || ShareAccessEmail.Normalize(_invite.Draft) is not null);
    }

    // ---- People ----

    private void SyncRows(bool animate)
    {
        if (_list is null)
        {
            return;
        }
        var motion = animate && ArcMotion.GetEffectiveLevel(this) != ArcMotionLevel.Off;
        // Rows of people who left go out (collapsing), then leave the panel.
        foreach (var person in _rows.Keys.ToArray())
        {
            if (People.Contains(person))
            {
                continue;
            }
            var (host, _) = _rows[person];
            _rows.Remove(person);
            person.Changed -= OnPersonChanged;
            if (motion)
            {
                host.Exited += (_, _) => _list.Children.Remove(host);
                host.IsPresent = false;
                host.IsHitTestVisible = false;
            }
            else
            {
                _list.Children.Remove(host);
            }
        }
        // New people come in where they belong in the list.
        for (var i = 0; i < People.Count; i++)
        {
            var person = People[i];
            if (_rows.ContainsKey(person))
            {
                continue;
            }
            var row = new ShareAccessPersonRow(this, person);
            var host = new PresenceHost { Child = row, Mode = PresenceMode.Rise, Offset = 6, Collapse = PresenceCollapse.Height, IsPresent = !motion };
            _rows[person] = (host, row);
            person.Changed += OnPersonChanged;
            _list.Children.Insert(Math.Min(PanelIndexFor(i), _list.Children.Count), host);
            if (motion)
            {
                Dispatcher.UIThread.Post(() => host.IsPresent = true, DispatcherPriority.Loaded);
            }
        }
    }

    /// <summary>The panel slot for the person at <paramref name="peopleIndex"/>: after the rows of the people before them.</summary>
    private int PanelIndexFor(int peopleIndex)
    {
        if (peopleIndex == 0 || _list is null)
        {
            return 0;
        }
        var before = People[peopleIndex - 1];
        if (_rows.TryGetValue(before, out var entry))
        {
            return _list.Children.IndexOf(entry.Host) + 1;
        }
        return _list.Children.Count;
    }

    private void OnPersonChanged(ShareAccessPerson person)
    {
        if (_rows.TryGetValue(person, out var entry))
        {
            entry.Row.Refresh(animate: true);
        }
    }

    private void FocusAfterRemoval(int index)
    {
        // Keyboard users land on the next person's role button, or the address field when none is left.
        for (var i = Math.Min(index, People.Count - 1); i >= 0; i--)
        {
            if (_rows.TryGetValue(People[i], out var entry) && entry.Row.RoleButton is { } button)
            {
                button.Focus(NavigationMethod.Tab);
                return;
            }
        }
        FocusInvite();
    }

    // ---- Invite field ----

    private List<string> PendingAddresses()
    {
        var list = new List<string>();
        if (_invite is null)
        {
            return list;
        }
        list.AddRange(_invite.Value);
        if (ShareAccessEmail.Normalize(_invite.Draft) is { } draft)
        {
            list.Add(draft);
        }
        return list;
    }

    private void OnInviteValueChanged(object? sender, TagInputValueChangedEventArgs e)
    {
        if (_invite is null)
        {
            return;
        }
        string? message = null;
        foreach (var tag in e.Added)
        {
            var email = ShareAccessEmail.Normalize(tag);
            if (email is null)
            {
                message = Labels.NotAnEmail(tag);
                _invite.RemoveTag(tag);
            }
            else if (FindPerson(email) is not null)
            {
                message = Labels.AlreadyShared(email);
                _invite.RemoveTag(tag);
            }
            else if (!string.Equals(email, tag, StringComparison.Ordinal))
            {
                // Keep the clean address: "Mira <mira@x.io>" becomes "mira@x.io".
                _invite.RemoveTag(tag);
                _invite.AddTag(email);
            }
        }
        if (message is not null)
        {
            ShowFieldMessage(message);
            Announce(message);
        }
        else if (e.Added.Count > 0)
        {
            ShowFieldMessage(null);
        }
        UpdateInviteEnabled();
    }

    private void ShowFieldMessage(string? message)
    {
        if (_invite is null)
        {
            return;
        }
        _invite.ErrorMessage = message;
        _invite.IsInvalid = message is not null;
    }

    private void ClearInviteField(IReadOnlyCollection<string> sent)
    {
        if (_invite is null)
        {
            return;
        }
        _invite.Value = [];
        if (ShareAccessEmail.Normalize(_invite.Draft) is { } draft && sent.Contains(draft, StringComparer.OrdinalIgnoreCase))
        {
            _invite.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.Clear();
        }
        ShowFieldMessage(null);
        UpdateInviteEnabled();
    }

    private void OnInviteKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _invite is null)
        {
            return;
        }
        var modifier = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        // Enter on an empty draft (or Ctrl/Cmd+Enter at any time) sends the invites; otherwise Enter makes a chip.
        if (modifier || (_invite.Draft.Trim().Length == 0 && _invite.Value.Count > 0))
        {
            e.Handled = true;
            _ = InviteAsync();
        }
        else
        {
            Dispatcher.UIThread.Post(UpdateInviteEnabled, DispatcherPriority.Input);
        }
    }

    private void OnAnyKey(object? sender, KeyEventArgs e)
    {
        _lastInputKeyboard = true;
        if (e.Source is Visual source && _invite is not null && _invite.IsVisualAncestorOf(source))
        {
            Dispatcher.UIThread.Post(UpdateInviteEnabled, DispatcherPriority.Input);
        }
    }

    private void OnAnyPointer(object? sender, PointerPressedEventArgs e) => _lastInputKeyboard = false;

    private void Announce(string message)
    {
        if (_status is null)
        {
            return;
        }
        // The same words twice in a row are still announced: clear first.
        _status.Text = string.Empty;
        _status.Text = message;
    }
}
