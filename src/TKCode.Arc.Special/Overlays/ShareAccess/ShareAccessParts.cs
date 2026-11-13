using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;

namespace TKCode.Arc;

/// <summary>Token binding helpers for the parts <see cref="ShareAccess"/> builds in code.</summary>
internal static class ShareAccessTokenBinding
{
    /// <summary>Binds <paramref name="property"/> to a <see cref="ShareAccessToken"/> value and returns the target.</summary>
    public static T ShareTok<T>(this T target, AvaloniaProperty property, ShareAccessTokenKind kind) where T : StyledElement
    {
        target.Bind(property, target.GetResourceObservable(ShareAccessTokens.Key(kind)));
        return target;
    }

    /// <summary>Binds <paramref name="property"/> to an ArcDesign token and returns the target.</summary>
    public static T ShareArc<T>(this T target, AvaloniaProperty property, ArcDesignTokenKind kind) where T : StyledElement
    {
        ArcTokens.Bind(target, property, kind);
        return target;
    }
}

/// <summary>The address rules of <see cref="ShareAccess"/>.</summary>
internal static partial class ShareAccessEmail
{
    [GeneratedRegex(@"^[^\s@,;<>()\[\]""]+@[^\s@,;<>()\[\]""]+\.[A-Za-z]{2,}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();

    /// <summary>
    /// The address in <paramref name="text"/> trimmed and lower-cased, or null when it is not one. "Name &lt;address&gt;"
    /// (as pasted from a mail client) yields the address between the brackets.
    /// </summary>
    public static string? Normalize(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        var open = value.LastIndexOf('<');
        if (open >= 0 && value.EndsWith('>'))
        {
            value = value[(open + 1)..^1].Trim();
        }
        return value.Length > 0 && value.Length <= 254 && Pattern().IsMatch(value) ? value.ToLowerInvariant() : null;
    }
}

/// <summary>
/// One person in the <see cref="ShareAccess"/> list: the avatar, the name (or the address) with the address or "Invite
/// sent" under it, and on the right a role button that opens a menu of roles and "Remove access" (the owner shows its
/// role as plain text).
/// </summary>
internal sealed class ShareAccessPersonRow : Grid
{
    private readonly ShareAccess _owner;
    private readonly Avatar _avatar;
    private readonly TextBlock _name;
    private readonly TextBlock _detail;
    private readonly Button? _roleButton;
    private readonly ArcContentSwap? _roleText;
    private readonly TextBlock? _ownerText;
    private readonly MenuSurface? _menu;
    private readonly MenuEntry[] _roleEntries = new MenuEntry[3];
    private MenuEntry? _removeEntry;
    private ShareAccessRole _shownRole;

    public ShareAccessPersonRow(ShareAccess owner, ShareAccessPerson person)
    {
        _owner = owner;
        Person = person;
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto");
        this.ShareTok(ColumnSpacingProperty, ShareAccessTokenKind.AvatarGap)
            .ShareTok(MinHeightProperty, ShareAccessTokenKind.RowHeight);

        _avatar = new Avatar { Size = AvatarSize.Small, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetAccessibilityView(_avatar, AccessibilityView.Raw);
        Children.Add(_avatar);

        _name = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            .ShareArc(TextBlock.FontWeightProperty, ArcDesignTokenKind.WeightMedium)
            .ShareArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        _detail = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis }
            .ShareArc(TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS)
            .ShareArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var words = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(_name);
        words.Children.Add(_detail);
        SetColumn(words, 1);
        Children.Add(words);

        if (person.Role == ShareAccessRole.Owner)
        {
            _ownerText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) }
                .ShareArc(TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            SetColumn(_ownerText, 2);
            Children.Add(_ownerText);
        }
        else
        {
            _roleText = new ArcContentSwap { Mode = ContentSwapMode.Label, VerticalAlignment = VerticalAlignment.Center };
            var chevron = new ArcGlyph { Data = ArcIcons.ChevronDown, Size = 14, VerticalAlignment = VerticalAlignment.Center }
                .ShareArc(ArcGlyph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            content.Children.Add(_roleText);
            content.Children.Add(chevron);
            _roleButton = new Button { Variant = ButtonVariant.Ghost, Size = ControlSize.Small, Content = content, VerticalAlignment = VerticalAlignment.Center };
            SetColumn(_roleButton, 2);
            Children.Add(_roleButton);

            _menu = new MenuSurface();
            for (var i = 0; i < _roleEntries.Length; i++)
            {
                var role = (ShareAccessRole)i;
                var entry = new MenuEntry { ToggleType = MenuEntryToggleType.Radio, GroupName = "role", Tag = role };
                _roleEntries[i] = entry;
                _menu.Items.Add(entry);
            }
            _removeEntry = new MenuEntry { IsDestructive = true, HasSeparatorBefore = true, Icon = ArcIcons.Trash2 };
            _menu.Items.Add(_removeEntry);
            _menu.ItemClicked += OnMenuItemClicked;
            DropdownMenu.SetMenu(_roleButton, _menu);
        }

        _shownRole = person.Role;
        Refresh(animate: false);
    }

    public ShareAccessPerson Person { get; }

    /// <summary>The role button (null for the owner).</summary>
    public Button? RoleButton => _roleButton;

    /// <summary>The role menu (null for the owner).</summary>
    public MenuSurface? Menu => _menu;

    /// <summary>Shows the person's current name, address, state and role.</summary>
    public void Refresh(bool animate)
    {
        var labels = _owner.Labels;
        var person = Person;
        var isYou = _owner.CurrentUserEmail is { Length: > 0 } me && string.Equals(me, person.Email, StringComparison.OrdinalIgnoreCase);
        var hasName = !string.IsNullOrWhiteSpace(person.Name);
        var primary = hasName ? person.Name! : person.Email;
        _name.Text = isYou ? $"{primary} {labels.You}" : primary;
        _detail.Text = person.IsPending ? labels.Pending : hasName ? person.Email : string.Empty;
        _detail.IsVisible = _detail.Text.Length > 0;
        _avatar.DisplayName = hasName ? person.Name : person.Email;
        _avatar.Source = person.Avatar;
        var roleName = labels.Role(person.Role);
        if (_ownerText is not null)
        {
            _ownerText.Text = roleName;
        }
        if (_roleText is not null && _roleButton is not null)
        {
            if (_roleText.Content as string != roleName)
            {
                _roleText.Mode = animate && _shownRole != person.Role ? ContentSwapMode.Label : ContentSwapMode.None;
                _roleText.Content = roleName;
            }
            _shownRole = person.Role;
            AutomationProperties.SetName(_roleButton, labels.RoleButton(primary, roleName));
            for (var i = 0; i < _roleEntries.Length; i++)
            {
                _roleEntries[i].Header = labels.Role((ShareAccessRole)i);
                _roleEntries[i].IsChecked = (int)person.Role == i;
            }
            if (_removeEntry is not null)
            {
                _removeEntry.Header = labels.Remove;
            }
        }
        AutomationProperties.SetName(this, primary);
    }

    private void OnMenuItemClicked(object? sender, MenuEntryClickedEventArgs e)
    {
        if (e.Entry == _removeEntry)
        {
            _owner.RemovePerson(Person, fromKeyboard: _roleButton?.IsKeyboardFocusWithin == true);
        }
        else if (e.Entry.Tag is ShareAccessRole role)
        {
            _owner.ChangeRole(Person, role);
        }
    }
}
