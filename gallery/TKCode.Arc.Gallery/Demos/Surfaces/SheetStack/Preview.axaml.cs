using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SheetStackPreviewDemo : UserControl
{
    // What the page shows, and the draft the profile level edits until "Save changes".
    private string _name = SheetStackSamples.Name;
    private string _role = SheetStackSamples.Role;
    private SheetStackSampleAvatar? _avatar = SheetStackSamples.DefaultAvatar;
    private SheetStackSampleAvatar? _draftAvatar = SheetStackSamples.DefaultAvatar;
    private bool _loading = true;

    public SheetStackPreviewDemo()
    {
        InitializeComponent();
        foreach (var choice in SheetStackSamples.Avatars)
        {
            var radio = new RadioButton
            {
                Theme = (Avalonia.Styling.ControlTheme)Resources["SheetStackDemoChoiceTheme"]!,
                GroupName = "sheet-stack-avatar",
                Tag = choice,
                Content = new Viewbox { Child = SheetStackDemoAvatar.Create(choice, SheetStackSamples.Name, 120) },
            };
            AutomationProperties.SetName(radio, choice.Name);
            Choices.Children.Add(radio);
        }
        ShowProfile();
        Announce(SheetStackSamples.Intro);
        _loading = false;
    }

    private void ShowProfile()
    {
        StageName.Text = _name;
        StageRole.Text = $"{_role} · {SheetStackSamples.Team}";
        StageAvatar.Content = SheetStackDemoAvatar.Create(_avatar, _name, 72);
        RowName.Text = _name;
        RowRole.Text = _role;
        RowAvatar.Content = SheetStackDemoAvatar.Create(_avatar, _name, 44);
        AutomationProperties.SetName(ProfileRow, $"{_name}, {_role}");
    }

    private void OnStackChanged(object? sender, SheetStackChangedEventArgs e)
    {
        // Entering the profile level starts a fresh draft from what the page shows.
        if (e.NewStack.Contains("profile") && !e.OldStack.Contains("profile"))
        {
            _loading = true;
            NameInput.Text = _name;
            RoleInput.Text = _role;
            _draftAvatar = _avatar;
            ProfileAvatar.Content = SheetStackDemoAvatar.Create(_draftAvatar, _name, 76);
            _loading = false;
            UpdateSave();
        }
        // Entering the avatar level selects the draft's choice.
        if (e.NewStack.Contains("avatar") && !e.OldStack.Contains("avatar"))
        {
            foreach (var child in Choices.Children)
            {
                if (child is RadioButton radio)
                {
                    radio.IsChecked = ReferenceEquals(radio.Tag, _draftAvatar);
                }
            }
        }
    }

    private void OnDraftChanged(object? sender, TextChangedEventArgs e) => UpdateSave();

    private void UpdateSave()
    {
        if (_loading)
        {
            return;
        }
        SaveButton.IsEnabled = NameInput.Text?.Trim() is { Length: > 0 } name &&
                               (name != _name || (RoleInput.Text ?? string.Empty).Trim() != _role || !ReferenceEquals(_draftAvatar, _avatar));
    }

    private void OnUsePhoto(object? sender, RoutedEventArgs e)
    {
        foreach (var child in Choices.Children)
        {
            if (child is RadioButton { IsChecked: true, Tag: SheetStackSampleAvatar choice })
            {
                _draftAvatar = choice;
            }
        }
        ProfileAvatar.Content = SheetStackDemoAvatar.Create(_draftAvatar, NameInput.Text ?? _name, 76);
        UpdateSave();
        Stack.Pop();
    }

    private void OnRemovePhoto(object? sender, RoutedEventArgs e)
    {
        _draftAvatar = null;
        ProfileAvatar.Content = SheetStackDemoAvatar.Create(null, NameInput.Text ?? _name, 76);
        UpdateSave();
        Stack.Pop();
        Announce(SheetStackSamples.Cleared);
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        _name = (NameInput.Text ?? _name).Trim();
        _role = (RoleInput.Text ?? _role).Trim();
        _avatar = _draftAvatar;
        ShowProfile();
        SaveButton.IsEnabled = false;
        Stack.Pop();
        Announce(SheetStackSamples.Saved);
    }

    private void OnOfflineChanged(object? sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            Announce(SheetStackSamples.Toggled("Offline copies", OfflineSwitch.IsChecked == true));
        }
    }

    private void OnSpellChanged(object? sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            Announce(SheetStackSamples.Toggled("Spell check", SpellSwitch.IsChecked == true));
        }
    }

    private void OnChoicesSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        // Square cells: three columns, 12 apart (8 when narrow).
        var gap = e.NewSize.Width < 320 ? 8 : 12;
        Choices.ColumnSpacing = gap;
        Choices.RowSpacing = gap;
        var cell = Math.Max(0, (e.NewSize.Width - 2 * gap) / 3);
        foreach (var child in Choices.Children)
        {
            if (child is Control control && Math.Abs(control.Height - cell) > 0.5)
            {
                control.Width = cell;
                control.Height = cell;
            }
        }
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        // The event also fires while the XAML loads, before the named stack exists: read the sender.
        if (Stack is not null && sender is SegmentedControl { SelectedValue: string value } &&
            Enum.TryParse<SheetStackMode>(value, out var mode))
        {
            Stack.Mode = mode;
        }
    }

    private void OnReset(object? sender, RoutedEventArgs e)
    {
        _loading = true;
        _name = SheetStackSamples.Name;
        _role = SheetStackSamples.Role;
        _avatar = SheetStackSamples.DefaultAvatar;
        OfflineSwitch.IsChecked = true;
        SpellSwitch.IsChecked = false;
        ModeSwitch.SelectedValue = "Auto";
        Stack.Close();
        ShowProfile();
        _loading = false;
        Announce(SheetStackSamples.ResetDone);
    }

    private void Announce(string text)
    {
        Status.Text = text;
        AutomationProperties.SetName(Status, text);
    }
}
