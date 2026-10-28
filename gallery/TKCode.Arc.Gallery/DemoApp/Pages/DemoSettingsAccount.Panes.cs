using TKCode.Arc.DesignTokens;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>The four panes of Settings → Account.</summary>
internal sealed partial class DemoSettingsAccount
{
    private Avatar _avatar = null!;
    private Border _photo = null!;
    private TextBlock _identityName = null!;
    private TextBlock _identityTitle = null!;
    private Button _removePhoto = null!;
    private PresenceHost _photoError = null!;
    private TextBlock _photoErrorText = null!;
    private PresenceHost _twoStepWarning = null!;
    private PresenceHost _quietHours = null!;
    private AnimatedCounter _seatPrice = null!;
    private AnimatedCounter _price = null!;
    private TextMorph _period = null!;
    private ArcContentSwap _cycleNote = null!;
    private string? _cycleNoteText;

    #region Profile

    private Control BuildProfile()
    {
        // Avatar: the draft photo over the initials (the saved photo is a network image, so it shows as initials).
        _avatar = new Avatar { Size = AvatarSize.XLarge, Width = 64, Height = 64 };
        _photo = new Border { Width = 64, Height = 64, ClipToBounds = true, IsVisible = false }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        var avatar = new Panel { Width = 64, Height = 64, VerticalAlignment = VerticalAlignment.Top };
        avatar.Children.Add(_avatar);
        avatar.Children.Add(_photo);

        _identityName = DemoUi.Line(string.Empty, ArcDesignTokenKind.Foreground, 14, FontWeight.Medium);
        _identityTitle = DemoUi.Line(string.Empty, ArcDesignTokenKind.TextSecondary);
        var change = DemoUi.Button("Change photo", ButtonVariant.Secondary, DemoSettingsIcons.ImageUp);
        change.Click += async (_, _) => await ChoosePhotoAsync();
        _removePhoto = DemoUi.Button("Remove", ButtonVariant.Ghost);
        _removePhoto.Click += (_, _) =>
        {
            _photoError.IsPresent = false;
            Set("photo", null);
            Announce("Photo removed. Your initials will show instead.");
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        actions.Children.Add(change);
        actions.Children.Add(_removePhoto);
        _photoErrorText = DemoUi.Text(string.Empty, ArcDesignTokenKind.Danger, 12);
        _photoErrorText.Margin = new Thickness(0, 8, 0, 0);
        _photoError = Expander(_photoErrorText, false);

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        text.Children.AddRange([_identityName, _identityTitle, actions, _photoError]);
        var identity = new DockPanel { Margin = new Thickness(0, 0, 0, 24) };
        avatar.Margin = new Thickness(0, 0, 16, 0);
        DockPanel.SetDock(avatar, Dock.Left);
        identity.Children.Add(avatar);
        identity.Children.Add(text);
        var identityBlock = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 24), Margin = new Thickness(0, 0, 0, 24), Child = identity };
        identity.Margin = default;
        ArcTokens.Bind(identityBlock, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var name = TextField("name", "Full name");
        var email = TextField("email", "Email", "Used for sign-in and notifications.");
        var pane = new StackPanel();
        pane.Children.Add(identityBlock);
        pane.Children.Add(Fields(name, TextField("title", "Job title"), email, Choice("timezone", "Time zone", s_timeZones)));
        return pane;
    }

    private void RefreshProfile()
    {
        var name = ((string?)_draft["name"] ?? string.Empty).Trim();
        _avatar.DisplayName = name.Length > 0 ? name : (string?)_saved["name"];
        _identityName.Text = name.Length > 0 ? name : "Your name";
        var title = ((string?)_draft["title"] ?? string.Empty).Trim();
        _identityTitle.Text = $"{(title.Length > 0 ? title : "No job title")} · Northwind Studio";
        _removePhoto.IsVisible = _draft["photo"] is not null;
        if (_draft["photo"] is Bitmap bitmap)
        {
            _photo.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
            _photo.IsVisible = true;
        }
        else
        {
            _photo.IsVisible = false;
        }
    }

    private async Task ChoosePhotoAsync()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
        {
            return;
        }
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp"]
                }
            ]
        });
        if (files.Count == 0)
        {
            return;
        }
        var file = files[0];
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
        {
            ShowPhotoError("Choose a JPG, PNG, or WebP image");
            return;
        }
        var properties = await file.GetBasicPropertiesAsync();
        if (properties.Size is > MaxPhotoBytes)
        {
            ShowPhotoError("Choose an image under 5 MB");
            return;
        }
        try
        {
            await using var stream = await file.OpenReadAsync();
            var bitmap = new Bitmap(stream);
            _photoError.IsPresent = false;
            Set("photo", bitmap);
            Announce("New photo ready to save");
        }
        catch (Exception)
        {
            ShowPhotoError("Choose a JPG, PNG, or WebP image");
        }
    }

    private void ShowPhotoError(string message)
    {
        _photoErrorText.Text = message;
        _photoError.IsPresent = true;
        Announce(message);
    }

    #endregion

    #region Notifications

    private Control BuildNotifications()
    {
        var quietSelect = Choice("quietHours", "Pause between", s_quietHours);
        quietSelect.MaxWidth = 300;
        quietSelect.HorizontalAlignment = HorizontalAlignment.Left;
        quietSelect.Margin = new Thickness(0, 4, 0, 14);
        _quietHours = Expander(quietSelect, _draft["quiet"] is true);

        var quiet = Group("Quiet hours",
            Row("Pause overnight", "Hold notifications and deliver them together in the morning.", Toggle("quiet")));
        var pane = new StackPanel { Spacing = 24 };
        pane.Children.Add(Group("Email",
            Row("Comments and mentions", "Replies to your comments and anyone who mentions you.", Toggle("mentions")),
            Row("Weekly summary", "A Monday recap of what moved in your projects.", Toggle("summary")),
            Row("Product news", "New features and changes, about once a month.", Toggle("news"))));
        pane.Children.Add(Group("On your devices",
            Row("Direct messages", "Messages sent to you or to a group you are in.", Toggle("messages")),
            Row("Reminders", "Due dates and meetings, 15 minutes ahead.", Toggle("reminders"))));
        var quietBlock = new StackPanel();
        quietBlock.Children.Add(quiet);
        quietBlock.Children.Add(_quietHours);
        pane.Children.Add(quietBlock);
        return pane;
    }

    #endregion

    #region Security

    private Control BuildSecurity()
    {
        var warning = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, -8, 0, 12) };
        warning.Children.Add(DemoUi.Glyph(ArcIcons.TriangleAlert, 14, ArcDesignTokenKind.Warning));
        warning.Children.Add(DemoUi.Text("Anyone with your password could sign in.", ArcDesignTokenKind.Warning, 12));
        _twoStepWarning = Expander(warning, _draft["twoStep"] is false);

        var signIn = new SettingsGroup { Header = "Sign-in", Appearance = SettingsGroupAppearance.Plain };
        signIn.Items.Add(Row("Two-step verification", "Ask for a code from your authenticator app on new devices.", Toggle("twoStep")));
        signIn.Items.Add(_twoStepWarning);
        signIn.Items.Add(Row("Sign-in alerts", "Email you when a new device signs in.", Toggle("alerts")));

        var timeout = Choice("timeout", "Sign out idle sessions", s_timeouts, "Applies to every device except this one.");
        timeout.MaxWidth = 300;
        timeout.HorizontalAlignment = HorizontalAlignment.Left;

        var heading = DemoUi.Text("Where you are signed in", ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium);
        heading.Focusable = true;
        heading.Margin = new Thickness(0, 0, 0, 4);
        var list = new StackPanel();
        foreach (var session in s_sessions)
        {
            var row = SessionRow(session, heading);
            _sessionRows[session.Id] = row;
            list.Children.Add(row);
        }
        var sessions = new StackPanel();
        sessions.Children.Add(heading);
        sessions.Children.Add(list);

        var pane = new StackPanel { Spacing = 24 };
        pane.Children.AddRange([signIn, timeout, sessions]);
        return pane;
    }

    private PresenceHost SessionRow(Session session, Control heading)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*,Auto"), ColumnSpacing = 12, MinHeight = 52 };
        var icon = DemoUi.Glyph(session.Icon, 16);
        icon.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(icon);
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8) };
        text.Children.Add(DemoUi.Line($"{session.Device} · {session.App}", ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium));
        text.Children.Add(DemoUi.Line($"{session.Place} · {session.Seen}", ArcDesignTokenKind.TextMuted, 12));
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        Control trailing;
        if (session.IsCurrent)
        {
            trailing = DemoUi.Text("This device", ArcDesignTokenKind.TextMuted, 12);
        }
        else
        {
            var signOut = DemoUi.Button("Sign out", ButtonVariant.Ghost);
            AutomationProperties.SetName(signOut, $"Sign out {session.Device}");
            signOut.Click += (_, _) =>
            {
                _draftSignedOut.Add(session.Id);
                Announce($"{session.Device} will be signed out when you save");
                var next = NextSignOut(session.Id);
                Refresh();
                Dispatcher.UIThread.Post(() => (next ?? heading).Focus(), DispatcherPriority.Loaded);
            };
            trailing = signOut;
        }
        trailing.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(trailing, 2);
        grid.Children.Add(trailing);
        var row = new Border { BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
        ArcTokens.Bind(row, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        return Expander(row, !_draftSignedOut.Contains(session.Id));
    }

    /// <summary>The Sign out button of the next session still listed after <paramref name="id"/>, else the previous one.</summary>
    private Control? NextSignOut(string id)
    {
        var index = Array.FindIndex(s_sessions, session => session.Id == id);
        var open = s_sessions.Select((session, i) => (session, i))
                             .Where(item => !item.session.IsCurrent && !_draftSignedOut.Contains(item.session.Id))
                             .ToList();
        var target = open.FirstOrDefault(item => item.i > index).session ?? open.LastOrDefault(item => item.i < index).session;
        return target is null ? null : _sessionRows[target.Id].GetVisualDescendantsOfType<Button>().FirstOrDefault();
    }

    #endregion

    #region Billing

    private Control BuildBilling()
    {
        // Plan: "8 seats at $18 each" and the total, both rolling when the cycle changes.
        _seatPrice = new AnimatedCounter { Prefix = "$", FontSize = 12, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_seatPrice, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        ArcTokens.Bind(_seatPrice, TemplatedControl.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var seats = new StackPanel { Orientation = Orientation.Horizontal };
        seats.Children.Add(DemoUi.Text("8 seats at ", ArcDesignTokenKind.TextSecondary, 12));
        seats.Children.Add(_seatPrice);
        seats.Children.Add(DemoUi.Text(" each", ArcDesignTokenKind.TextSecondary, 12));
        var plan = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        plan.Children.Add(DemoUi.Text("Team plan", ArcDesignTokenKind.Foreground, 14, FontWeight.Medium));
        plan.Children.Add(seats);

        _price = new AnimatedCounter { Prefix = "$", FontSize = 26, VerticalAlignment = VerticalAlignment.Center };
        _period = new TextMorph { FontSize = DemoUi.BodySize, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 4, 0, 0) };
        ArcTokens.Bind(_period, TextMorph.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        var price = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        price.Children.Add(_price);
        price.Children.Add(_period);
        var planRow = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(price, Dock.Right);
        planRow.Children.Add(price);
        planRow.Children.Add(plan);

        // Cycle: the segmented switch and a note that swaps with a blur and rise.
        var cycle = new SegmentedControl();
        AutomationProperties.SetName(cycle, "Billing cycle");
        cycle.Items.Add(new SegmentedOption("monthly", "Monthly"));
        cycle.Items.Add(new SegmentedOption("yearly", "Yearly"));
        cycle.SelectedValue = _draft["cycle"];
        cycle.SelectionChanged += (_, _) =>
        {
            if (cycle.SelectedValue is string value)
            {
                Set("cycle", value);
            }
        };
        _show["cycle"] = value => cycle.SelectedValue = value;
        _cycleNote = new ArcContentSwap { Mode = ContentSwapMode.BlurSwap, VerticalAlignment = VerticalAlignment.Center };
        var cycleRow = new WrapPanel { ItemSpacing = 16, LineSpacing = 12 };
        cycleRow.Children.Add(cycle);
        cycleRow.Children.Add(_cycleNote);
        var cycleBlock = new Border { BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 16), Child = cycleRow };
        ArcTokens.Bind(cycleBlock, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        // Payment method.
        var card = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var cardIcon = DemoUi.Glyph(ArcIcons.CreditCard, 16);
        cardIcon.VerticalAlignment = VerticalAlignment.Center;
        card.Children.Add(cardIcon);
        var cardText = new WrapPanel { ItemSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
        cardText.Children.Add(DemoUi.Text("Visa ending in 4242", ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium));
        cardText.Children.Add(DemoUi.Text("Expires 08/2028 · next invoice on October 1", ArcDesignTokenKind.TextMuted));
        card.Children.Add(cardText);
        var payment = new Border { BorderThickness = new Thickness(0, 1), Padding = new Thickness(0, 12), Margin = new Thickness(0, 0, 0, 24), Child = card };
        ArcTokens.Bind(payment, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);

        var pane = new StackPanel();
        pane.Children.AddRange([planRow, cycleBlock, payment,
            Fields(TextField("billingEmail", "Billing email", "Invoices and receipts are sent here."), TextField("company", "Company name"),
                 Choice("country", "Country", s_countries), TextField("vat", "VAT number"))]);
        return pane;
    }

    private void RefreshBilling()
    {
        var yearly = _draft["cycle"] is "yearly";
        _seatPrice.Value = yearly ? 15 : 18;
        _price.Value = yearly ? 1440 : 144;
        _period.Text = yearly ? "per year" : "per month";
        var note = yearly ? "You save $288 a year" : "Save $288 a year with yearly billing";
        if (_cycleNoteText != note)
        {
            _cycleNoteText = note;
            _cycleNote.Content = DemoUi.Text(note, ArcDesignTokenKind.TextSecondary);
        }
    }

    #endregion
}
