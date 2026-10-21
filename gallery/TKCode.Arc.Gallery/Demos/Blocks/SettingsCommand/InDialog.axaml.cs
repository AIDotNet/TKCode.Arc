using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace TKCode.Arc.Gallery.Demos;

public partial class SettingsCommandInDialogDemo : UserControl
{
    // Command+comma on a Mac, Ctrl+comma elsewhere: the usual shortcut for settings (the gallery keeps Ctrl/⌘+K for its search).
    private static readonly KeyGesture OpenGesture = ShortcutFormatter.Parse("mod+,", ShortcutFormatter.CurrentPlatform);

    private TopLevel? _topLevel;

    public SettingsCommandInDialogDemo()
    {
        InitializeComponent();
        ShortcutHint.Text = $"or press {ShortcutFormatter.Format(OpenGesture, ShortcutFormatter.CurrentPlatform).Text}";
    }

    // The shortcut works anywhere in the window while the demo is on screen, like an app-wide command.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _topLevel?.RemoveHandler(KeyDownEvent, OnWindowKeyDown);
        _topLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (!SettingsDialog.IsOpen && ShortcutFormatter.Matches(e, OpenGesture, ShortcutFormatter.CurrentPlatform))
        {
            e.Handled = true;
            Open();
        }
    }

    private void OnOpenClick(object? sender, RoutedEventArgs e) => Open();

    private void Open()
    {
        Palette.Clear();
        SettingsDialog.Open();
    }

    // The dialog focuses its first control when it opens; the palette wants the keyboard in its search field.
    private void OnOpened(object? sender, EventArgs e)
        => Dispatcher.UIThread.Post(Palette.FocusSearch, DispatcherPriority.Input);

    // Escape with an empty query: close the dialog (handled, so the dialog does not close a second time).
    private void OnCloseRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        SettingsDialog.Close();
    }
}
