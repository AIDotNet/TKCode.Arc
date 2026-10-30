using System.Text.RegularExpressions;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.DemoApp;

/// <summary>
/// Settings → Account, uiarc.dev's settings page block with its default data (the "Northwind" account): Profile,
/// Notifications, Security and Billing panes in our <see cref="SettingsPage"/>, a draft of every field against the saved
/// values, an unsaved dot on each section that differs, and a floating save bar that counts the changes, saves after
/// 900ms (or Cmd/Ctrl+S) and discards. Our block's own save bar is hidden: it saves one section at a time and has no count.
/// </summary>
internal sealed partial class DemoSettingsAccount : Panel
{
    private const string SavedPhoto = "/media/people/emma-collins.jpg";
    private const long MaxPhotoBytes = 5242880;
    private const double NavWidth = 248;

    private static readonly Regex s_email = new(@"^[^\s@]+@[^\s@]+\.[^\s@]{2,}$", RegexOptions.CultureInvariant);

    private static readonly (string Value, string Label)[] s_timeZones =
    [
        ("lisbon", "Lisbon (GMT+1)"), ("london", "London (GMT+1)"), ("berlin", "Berlin (GMT+2)"),
        ("new-york", "New York (GMT−4)"), ("san-francisco", "San Francisco (GMT−7)"), ("tokyo", "Tokyo (GMT+9)")
    ];

    private static readonly (string Value, string Label)[] s_quietHours =
        [("21-8", "9 PM to 8 AM"), ("22-7", "10 PM to 7 AM"), ("23-6", "11 PM to 6 AM"), ("0-8", "Midnight to 8 AM")];

    private static readonly (string Value, string Label)[] s_timeouts =
        [("1h", "After 1 hour"), ("8h", "After 8 hours"), ("24h", "After 24 hours"), ("7d", "After 7 days")];

    private static readonly (string Value, string Label)[] s_countries =
        [("pt", "Portugal"), ("de", "Germany"), ("nl", "Netherlands"), ("gb", "United Kingdom"), ("us", "United States")];

    private sealed record Session(string Id, Geometry Icon, string Device, string App, string Place, string Seen, bool IsCurrent = false);

    private static readonly Session[] s_sessions =
    [
        new("mac", ArcIcons.Laptop, "MacBook Pro", "Safari", "Lisbon, Portugal", "Active now", true),
        new("iphone", ArcIcons.Smartphone, "iPhone 16", "Northwind for iOS", "Lisbon, Portugal", "2 hours ago"),
        new("ipad", DemoSettingsIcons.Tablet, "iPad Air", "Safari", "Porto, Portugal", "Yesterday"),
        new("windows", ArcIcons.Monitor, "Windows PC", "Chrome", "Berlin, Germany", "12 days ago")
    ];

    /// <summary>Which section each field belongs to (its nav item shows the unsaved dot).</summary>
    private static readonly Dictionary<string, string> s_sectionOf = new()
    {
        ["photo"] = "profile", ["name"] = "profile", ["title"] = "profile", ["email"] = "profile", ["timezone"] = "profile",
        ["mentions"] = "notifications", ["summary"] = "notifications", ["news"] = "notifications", ["messages"] = "notifications",
        ["reminders"] = "notifications", ["quiet"] = "notifications", ["quietHours"] = "notifications",
        ["twoStep"] = "security", ["alerts"] = "security", ["timeout"] = "security",
        ["cycle"] = "billing", ["billingEmail"] = "billing", ["company"] = "billing", ["country"] = "billing", ["vat"] = "billing"
    };

    private enum BarState
    {
        Hidden,
        Edit,
        Saving,
        Saved
    }

    private readonly Dictionary<string, object?> _saved = new()
    {
        ["photo"] = SavedPhoto, ["name"] = "Emma Collins", ["title"] = "Product designer", ["email"] = "emma@northwind.studio",
        ["timezone"] = "lisbon",
        ["mentions"] = true, ["summary"] = true, ["news"] = false, ["messages"] = true, ["reminders"] = false, ["quiet"] = false,
        ["quietHours"] = "22-7",
        ["twoStep"] = true, ["alerts"] = true, ["timeout"] = "8h",
        ["cycle"] = "monthly", ["billingEmail"] = "billing@northwind.studio", ["company"] = "Northwind Studio Lda.",
        ["country"] = "pt", ["vat"] = "PT 516 283 947"
    };

    private readonly Dictionary<string, object?> _draft;
    private HashSet<string> _savedSignedOut = [];
    private HashSet<string> _draftSignedOut = [];

    /// <summary>Pushes a value into the control that edits it (Discard).</summary>
    private readonly Dictionary<string, Action<object?>> _show = new();
    private readonly Dictionary<string, Input> _inputs = new();
    private readonly HashSet<string> _touched = [];
    private readonly Dictionary<string, SettingsSection> _sections = new();
    private readonly List<DemoSettingsFieldGrid> _grids = [];
    private readonly Dictionary<string, PresenceHost> _sessionRows = new();
    private readonly SettingsPage _page;
    private readonly TextBlock _live = DemoSettingsUi.LiveRegion();
    private bool _syncing;
    private BarState _state;
    private bool _savedFlash;
    private int _saveRun;

    public DemoSettingsAccount()
    {
        _draft = new Dictionary<string, object?>(_saved);
        MaxWidth = 980;
        HorizontalAlignment = HorizontalAlignment.Left;

        _page = new SettingsPage { Height = 640 };
        _page.Footer = AccountCard();
        _page.Items.Add(Section("profile", "Profile", DemoSettingsIcons.UserRound, "How you appear to teammates across Northwind.", BuildProfile()));
        _page.Items.Add(Section("notifications", "Notifications", ArcIcons.Bell, "Choose what reaches you, and when.", BuildNotifications()));
        _page.Items.Add(Section("security", "Security", ArcIcons.ShieldCheck, "Protect your account and review where you are signed in.", BuildSecurity()));
        _page.Items.Add(Section("billing", "Billing", ArcIcons.CreditCard, "Your plan, payment method, and invoice details.", BuildBilling()));

        // Our block's save bar saves the selected section only; the demo's bar below counts every change.
        _page.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Control>(SettingsPage.SaveBarPart) is { } builtIn)
            {
                builtIn.IsVisible = false;
            }
        };
        _page.SaveRequested += (_, _) => _ = SaveAsync();
        _page.SelectionChanged += (_, _) => Dispatcher.UIThread.Post(ScrollPaneToTop, DispatcherPriority.Loaded);
        _page.PropertyChanged += (_, e) =>
        {
            if (e.Property == SettingsPage.IsCompactProperty)
            {
                ApplyCompact(_page.IsCompact);
            }
        };

        Children.Add(_page);
        Children.Add(BuildSaveBar());
        Children.Add(_live);
        AddHandler(KeyDownEvent, OnKeyDown);
        SizeChanged += (_, e) => ApplyNarrow(e.NewSize.Width);
        Refresh();
    }

    #region Frame

    /// <summary>The block is <c>min(100%, 980px)</c> wide whatever its content asks for.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? MaxWidth : Math.Min(availableSize.Width, MaxWidth);
        var size = base.MeasureOverride(availableSize.WithWidth(width));
        return new Size(width, size.Height);
    }

    private SettingsSection Section(string id, string label, Geometry icon, string description, Control content)
    {
        var section = new SettingsSection { Header = label, Icon = icon, Description = description, Content = content, MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left };
        _sections[id] = section;
        return section;
    }

    private static Control AccountCard()
    {
        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(DemoUi.Line("Emma Collins", ArcDesignTokenKind.Foreground, DemoUi.BodySize, FontWeight.Medium));
        text.Children.Add(DemoUi.Line("Team plan · Admin", ArcDesignTokenKind.TextMuted, 12));
        var card = new DockPanel();
        var avatar = new Avatar { DisplayName = "Emma Collins", Source = DemoPhotos.For("Emma Collins"), Size = AvatarSize.Medium, Margin = new Thickness(0, 0, 10, 0) };
        DockPanel.SetDock(avatar, Dock.Left);
        card.Children.Add(avatar);
        card.Children.Add(text);
        return card;
    }

    private void ApplyCompact(bool compact)
    {
        _page.Height = compact ? 680 : 640;
        foreach (var grid in _grids)
        {
            grid.Columns = compact ? 1 : 2;
        }
        _barDock.Margin = new Thickness(compact ? 0 : NavWidth, 0, 0, _narrow ? 14 : 20);
    }

    private void ScrollPaneToTop()
    {
        if (_page.GetVisualDescendantsOfType<ScrollViewer>().FirstOrDefault(scroll => scroll.Name == SettingsPage.ContentScrollPart) is { } scroller)
        {
            scroller.Offset = default;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var command = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0;
        if (!e.Handled && command && e.Key == Key.S)
        {
            e.Handled = true;
            _ = SaveAsync();
        }
    }

    #endregion

    #region Fields

    private Input TextField(string key, string label, string? description = null)
    {
        var input = new Input { Label = label, Description = description, Text = (string?)_draft[key] };
        input.TextChanged += (_, _) => Set(key, input.Text ?? string.Empty);
        input.LostFocus += (_, _) =>
        {
            _touched.Add(key);
            RefreshErrors();
        };
        _show[key] = value => input.Text = (string?)value;
        _inputs[key] = input;
        return input;
    }

    private Switch Toggle(string key)
    {
        var toggle = new Switch { IsChecked = (bool)_draft[key]!, VerticalAlignment = VerticalAlignment.Center };
        toggle.IsCheckedChanged += (_, _) => Set(key, toggle.IsChecked == true);
        _show[key] = value => toggle.IsChecked = (bool)value!;
        return toggle;
    }

    /// <summary>A select in a field, so its label and description match the inputs beside it.</summary>
    private Field Choice(string key, string label, (string Value, string Label)[] options, string? description = null)
    {
        var select = new Select { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (value, text) in options)
        {
            select.Items.Add(new SelectItem { Content = text, Value = value });
        }
        select.SelectedValue = _draft[key];
        select.SelectionChanged += (_, _) =>
        {
            if (select.SelectedValue is string value)
            {
                Set(key, value);
            }
        };
        _show[key] = value => select.SelectedValue = value;
        return new Field { Label = label, Description = description, Content = select };
    }

    private DemoSettingsFieldGrid Fields(params Control[] fields)
    {
        var grid = new DemoSettingsFieldGrid();
        grid.Children.AddRange(fields);
        _grids.Add(grid);
        return grid;
    }

    private static SettingsRow Row(string header, string description, Control control)
        => new() { Header = header, Description = description, Content = control, StackWidth = 0 };

    private static SettingsGroup Group(string? header, params Control[] rows)
    {
        var group = new SettingsGroup { Header = header, Appearance = SettingsGroupAppearance.Plain };
        foreach (var row in rows)
        {
            group.Items.Add(row);
        }
        return group;
    }

    /// <summary>Content that grows in (height and opacity) while it shows.</summary>
    private static PresenceHost Expander(Control child, bool isPresent)
        => new() { Mode = PresenceMode.Fade, Collapse = PresenceCollapse.Height, IsPresent = isPresent, Child = child };

    #endregion

    #region Draft

    private void Set(string key, object? value)
    {
        if (_syncing)
        {
            return;
        }
        _draft[key] = value;
        Refresh();
    }

    private IEnumerable<string> ChangedKeys() => _draft.Keys.Where(key => !Equals(_draft[key], _saved[key]));

    private int SignedOutChanges => _draftSignedOut.Count(id => !_savedSignedOut.Contains(id)) + _savedSignedOut.Count(id => !_draftSignedOut.Contains(id));

    private int UnsavedCount => ChangedKeys().Count() + SignedOutChanges;

    /// <summary>The validation errors in field order: name, email, billing email.</summary>
    private List<(string Key, string Message)> Errors()
    {
        var errors = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace((string?)_draft["name"]))
        {
            errors.Add(("name", "Enter your full name"));
        }
        if (!s_email.IsMatch(((string?)_draft["email"] ?? string.Empty).Trim()))
        {
            errors.Add(("email", "Enter a valid email address"));
        }
        if (!s_email.IsMatch(((string?)_draft["billingEmail"] ?? string.Empty).Trim()))
        {
            errors.Add(("billingEmail", "Enter a valid billing email"));
        }
        return errors;
    }

    /// <summary>Brings every view that depends on the draft up to date.</summary>
    private void Refresh()
    {
        var changed = ChangedKeys().ToList();
        foreach (var (id, section) in _sections)
        {
            section.IsDirty = changed.Any(key => s_sectionOf[key] == id) || (id == "security" && SignedOutChanges > 0);
        }
        RefreshProfile();
        _twoStepWarning.IsPresent = _draft["twoStep"] is false;
        _quietHours.IsPresent = _draft["quiet"] is true;
        RefreshBilling();
        foreach (var (id, row) in _sessionRows)
        {
            row.IsPresent = !_draftSignedOut.Contains(id);
        }
        RefreshErrors();
        RefreshBar();
    }

    private void RefreshErrors()
    {
        var errors = Errors();
        foreach (var (key, input) in _inputs)
        {
            var error = errors.FirstOrDefault(item => item.Key == key).Message;
            input.ErrorMessage = _touched.Contains(key) ? error : null;
        }
    }

    private void Announce(string text) => _live.Text = text;

    #endregion

    #region Save bar

    private Border _barDock = null!;
    private PresenceHost _bar = null!;
    private readonly TranslateTransform _shake = new();
    private Control _editContent = null!;
    private Control _savedContent = null!;
    private AnimatedCounter _count = null!;
    private TextMorph _changesWord = null!;
    private TextBlock _unsavedWord = null!;
    private Button _discard = null!;
    private Button _save = null!;
    private TextBlock _saveText = null!;
    private TextBlock _saveSuffix = null!;
    private StrokeDrawGlyph _savedCheck = null!;
    private bool _narrow;

    private Control BuildSaveBar()
    {
        _count = new AnimatedCounter { FontSize = DemoUi.BodySize, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_count, TemplatedControl.FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
        _unsavedWord = new TextBlock { Text = " unsaved", FontSize = DemoUi.BodySize, VerticalAlignment = VerticalAlignment.Center };
        _changesWord = new TextMorph { Text = "changes", FontSize = DemoUi.BodySize, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 12, 0) };
        _discard = DemoUi.Button("Discard", ButtonVariant.Ghost);
        _discard.Click += (_, _) => Discard();
        _saveText = new TextBlock { Text = "Save" };
        _saveSuffix = new TextBlock { Text = " changes" };
        var saveLabel = new StackPanel { Orientation = Orientation.Horizontal };
        saveLabel.Children.Add(_saveText);
        saveLabel.Children.Add(_saveSuffix);
        _save = new Button { Variant = ButtonVariant.Primary, Size = ControlSize.Small, Content = saveLabel };
        _save.Click += (_, _) => _ = SaveAsync();

        var edit = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(16, 4, 4, 4) };
        edit.Children.AddRange([_count, _unsavedWord, _changesWord, _discard, _save]);
        AutomationProperties.SetName(edit, "Unsaved changes");
        _editContent = edit;

        _savedCheck = new StrokeDrawGlyph { Data = ArcIcons.Check, Size = 16, StrokeThickness = 2, VerticalAlignment = VerticalAlignment.Center };
        ArcTokens.Bind(_savedCheck, StrokeDrawGlyph.ForegroundProperty, ArcDesignTokenKind.Success);
        var saved = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Height = 40, Margin = new Thickness(16, 0), IsVisible = false };
        saved.Children.Add(_savedCheck);
        saved.Children.Add(new TextBlock { Text = "Changes saved", FontSize = DemoUi.BodySize, FontWeight = FontWeight.Medium, VerticalAlignment = VerticalAlignment.Center });
        AutomationProperties.SetName(saved, "Save status");
        _savedContent = saved;

        var content = new Panel();
        content.Children.Add(edit);
        content.Children.Add(saved);
        var pill = new Border
        {
            BorderThickness = new Thickness(1),
            RenderTransform = _shake,
            Child = new SpringSizeDecorator { Axis = SpringSizeAxis.Both, Spring = ArcSpring.Morph, Child = content }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(pill, Border.BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(pill, Border.BorderBrushProperty, ArcDesignTokenKind.Border);
        ArcTokens.Bind(pill, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowFloating);

        _bar = new PresenceHost { Mode = PresenceMode.Rise, Offset = 28, IsPresent = false, Child = pill };
        _barDock = new Border
        {
            Child = _bar,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(16, 0),
            Margin = new Thickness(NavWidth, 0, 0, 20)
        };
        _bar.HorizontalAlignment = HorizontalAlignment.Center;
        return _barDock;
    }

    private void RefreshBar()
    {
        var count = UnsavedCount;
        _state = _state == BarState.Saving ? BarState.Saving
            : count > 0 ? BarState.Edit
            : _savedFlash ? BarState.Saved
            : BarState.Hidden;
        if (count > 0)
        {
            _count.Value = count;
            _changesWord.Text = count == 1 ? "change" : "changes";
        }
        var wasSaved = _savedContent.IsVisible;
        _editContent.IsVisible = _state is BarState.Edit or BarState.Saving;
        _savedContent.IsVisible = _state == BarState.Saved;
        if (_savedContent.IsVisible && !wasSaved)
        {
            _savedCheck.Progress = 0;
            _ = _savedCheck.Play(TimeSpan.FromSeconds(0.34), ArcEasings.Standard);
        }
        var saving = _state == BarState.Saving;
        _discard.IsEnabled = !saving;
        _save.IsLoading = saving;
        _saveText.Text = saving ? "Saving" : "Save";
        _saveSuffix.IsVisible = !saving && !_narrow;
        _bar.IsPresent = _state != BarState.Hidden;
    }

    private void ApplyNarrow(double width)
    {
        var narrow = width <= 460;
        if (narrow == _narrow)
        {
            return;
        }
        _narrow = narrow;
        _unsavedWord.IsVisible = !narrow;
        _changesWord.IsVisible = !narrow;
        _editContent.Margin = new Thickness(narrow ? 16 : 16, 4, 4, 4);
        ApplyCompact(_page.IsCompact);
        RefreshBar();
    }

    private async Task SaveAsync()
    {
        if (_state == BarState.Saving)
        {
            return;
        }
        var errors = Errors();
        if (errors.Count > 0)
        {
            foreach (var (key, _) in errors)
            {
                _touched.Add(key);
            }
            RefreshErrors();
            var (firstKey, firstMessage) = errors[0];
            Announce($"{firstMessage} before saving");
            _ = Shake();
            _page.Select(_sections[s_sectionOf[firstKey]]);
            Dispatcher.UIThread.Post(() => _inputs[firstKey].Focus(), DispatcherPriority.Loaded);
            return;
        }
        var count = UnsavedCount;
        if (count == 0)
        {
            return;
        }
        var run = ++_saveRun;
        _state = BarState.Saving;
        Announce("Saving changes");
        RefreshBar();
        await Task.Delay(900);
        foreach (var key in _draft.Keys)
        {
            _saved[key] = _draft[key];
        }
        _savedSignedOut = [.._draftSignedOut];
        _state = BarState.Hidden;
        _savedFlash = true;
        Announce($"{count} {(count == 1 ? "change" : "changes")} saved.");
        Refresh();
        await Task.Delay(1800);
        if (run == _saveRun)
        {
            _savedFlash = false;
            RefreshBar();
        }
    }

    private void Discard()
    {
        var count = UnsavedCount;
        _syncing = true;
        foreach (var key in _saved.Keys)
        {
            _draft[key] = _saved[key];
            if (_show.TryGetValue(key, out var show))
            {
                show(_saved[key]);
            }
        }
        _syncing = false;
        _draftSignedOut = [.._savedSignedOut];
        _touched.Clear();
        _photoError.IsPresent = false;
        Announce($"{count} {(count == 1 ? "change" : "changes")} discarded");
        Refresh();
    }

    /// <summary>The bar shakes on x through 0, −7, 6, −4, 2, 0 when a save finds errors.</summary>
    private Task Shake()
    {
        double[] steps = [0, -7, 6, -4, 2, 0];
        var animation = new Animation { Duration = TimeSpan.FromMilliseconds(420), Easing = new Avalonia.Animation.Easings.LinearEasing() };
        for (var i = 0; i < steps.Length; i++)
        {
            animation.Children.Add(new KeyFrame
            {
                Cue = new Cue(i / (double)(steps.Length - 1)),
                Setters = { new Setter(TranslateTransform.XProperty, steps[i]) }
            });
        }
        return animation.RunAsync(_shake);
    }

    #endregion
}

/// <summary>
/// The settings panes' field grid: two equal columns 16 apart, rows 16 apart (one column on a compact settings page).
/// </summary>
internal sealed class DemoSettingsFieldGrid : Panel
{
    private const double Gap = 16;
    private int _columns = 2;

    public int Columns
    {
        get => _columns;
        set
        {
            if (_columns != value)
            {
                _columns = value;
                InvalidateMeasure();
            }
        }
    }

    private double ColumnWidth(double width) => _columns == 1 ? width : Math.Max(0, (width - Gap) / 2);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        var column = ColumnWidth(width);
        double height = 0;
        for (var start = 0; start < Children.Count; start += _columns)
        {
            double rowHeight = 0;
            for (var i = start; i < Math.Min(Children.Count, start + _columns); i++)
            {
                Children[i].Measure(new Size(column, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            }
            height += rowHeight + (start > 0 ? Gap : 0);
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var column = ColumnWidth(finalSize.Width);
        double y = 0;
        for (var start = 0; start < Children.Count; start += _columns)
        {
            double rowHeight = 0;
            for (var i = start; i < Math.Min(Children.Count, start + _columns); i++)
            {
                rowHeight = Math.Max(rowHeight, Children[i].DesiredSize.Height);
            }
            for (var i = start; i < Math.Min(Children.Count, start + _columns); i++)
            {
                Children[i].Arrange(new Rect((i - start) * (column + Gap), y, column, rowHeight));
            }
            y += rowHeight + Gap;
        }
        return finalSize;
    }
}

/// <summary>The Lucide glyphs the settings views need that <see cref="ArcIcons"/> does not carry.</summary>
internal static class DemoSettingsIcons
{
    public static Geometry UserRound { get; } = ArcIcons.Parse("M7 8a5 5 0 1 0 10 0a5 5 0 1 0 -10 0z M20 21a8 8 0 0 0-16 0");

    public static Geometry ImageUp { get; } = ArcIcons.Parse("M10.3 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10l-3.1-3.1a2 2 0 0 0-2.814.014L6 21 M14 19.5l3-3 3 3 M17 22v-5.5 M7 9a2 2 0 1 0 4 0a2 2 0 1 0 -4 0z");

    public static Geometry Tablet { get; } = ArcIcons.Parse("M6 2h12a2 2 0 0 1 2 2v16a2 2 0 0 1 -2 2h-12a2 2 0 0 1 -2 -2v-16a2 2 0 0 1 2 -2z M12 18L12.01 18");

    public static Geometry ShieldOff { get; } = ArcIcons.Parse("M2 2l20 20 M5 5a1 1 0 0 0-1 1v7c0 5 3.5 7.5 7.67 8.94a1 1 0 0 0 .67.01c2.35-.82 4.48-1.97 5.9-3.71 M9.309 3.652A12.252 12.252 0 0 0 11.24 2.28a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1v7a9.784 9.784 0 0 1-.08 1.264");
}
