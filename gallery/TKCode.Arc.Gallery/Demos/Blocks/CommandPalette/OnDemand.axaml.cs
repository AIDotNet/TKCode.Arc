using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Gallery.Demos;

public partial class CommandPaletteOnDemandDemo : UserControl
{
    private readonly CommandPalette _palette;
    private readonly ModalPresence _modal;
    private TopLevel? _topLevel;

    public CommandPaletteOnDemandDemo()
    {
        InitializeComponent();
        Hint.Text = OperatingSystem.IsMacOS() ? "or press ⌘ ⇧ P" : "or press Ctrl+Shift+P";
        _palette = new CommandPalette
        {
            ItemsSource = Commands(),
            AutoFocus = true,
            ShowCloseButton = true,
            MaxWidth = 560,
            Margin = new Thickness(16, 96, 16, 16),
            VerticalAlignment = VerticalAlignment.Top
        };
        _palette.ItemSelected += OnItemSelected;
        _palette.CloseRequested += (_, _) => _modal!.Close();
        // ModalPresence (Core) adds the scrim, keeps the focus inside, returns it to the opener and closes on a
        // scrim press. The palette brings its own rise, so the modal only fades.
        _modal = new ModalPresence(_palette, this) { Entrance = ModalEntrance.Fade, BackdropBlur = 4 };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
        _topLevel = null;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (e.Key == Key.P && command && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && IsEffectivelyVisible)
        {
            e.Handled = true;
            Open();
        }
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e) => Open();

    private void Open()
    {
        if (_modal.IsOpen)
        {
            _palette.FocusInput();
            return;
        }
        _palette.Clear();
        _modal.Open();
    }

    private void OnItemSelected(object? sender, CommandPaletteItemSelectedEventArgs e)
    {
        Result.Text = $"Ran “{e.Item.Label}”";
        _modal.Close(DismissReason.Action);
    }

    private static List<CommandPaletteItem> Commands() =>
    [
        new() { Id = "new-doc", Label = "New document", Group = "Create", Icon = ArcIcons.FilePlus, Shortcut = "N" },
        new() { Id = "new-project", Label = "New project", Group = "Create", Icon = ArcIcons.Folder },
        new() { Id = "invite", Label = "Invite teammate", Description = "Send an email invite", Group = "Create", Icon = ArcIcons.Users },
        new() { Id = "inbox", Label = "Go to inbox", Group = "Go to", Icon = ArcIcons.Inbox, Shortcut = "G I" },
        new() { Id = "calendar", Label = "Go to calendar", Group = "Go to", Icon = ArcIcons.Calendar, Shortcut = "G C" },
        new() { Id = "reports", Label = "Go to reports", Group = "Go to", Icon = ArcIcons.ChartBar },
        new() { Id = "theme", Label = "Toggle dark mode", Group = "Preferences", Icon = ArcIcons.Moon, Keywords = ["theme", "appearance"] },
        new() { Id = "shortcuts", Label = "Keyboard shortcuts", Group = "Preferences", Icon = ArcIcons.Keyboard, Shortcut = "?" }
    ];
}
