using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Gallery.Demos;

public partial class WindowCaptionButtonsPreviewDemo : UserControl
{
    public WindowCaptionButtonsPreviewDemo()
    {
        InitializeComponent();
        // The browser has a single view and no windows to open.
        OpenButton.IsVisible = !OperatingSystem.IsBrowser();
    }

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        // The content reaches the top: no Arc title bar, but the window still resizes from its edges.
        var window = new ArcWindow
        {
            Title = "Inbox",
            Width = 760,
            Height = 480,
            IsTitleBarVisible = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        // The header: a title, a search field and the caption buttons. Its empty part moves the window.
        var search = new Input { Placeholder = "Search", Size = ControlSize.Small, Width = 220 };
        var header = new Grid
        {
            Height = 48,
            Background = Brushes.Transparent,
            ColumnDefinitions = new ColumnDefinitions("*,Auto,*,Auto")
        };
        header.Children.Add(new TextBlock
        {
            Text = "Inbox",
            FontWeight = FontWeight.Medium,
            Margin = new Thickness(20, 0),
            VerticalAlignment = VerticalAlignment.Center,
            // A press on the title is a press on the header.
            IsHitTestVisible = false
        });
        Grid.SetColumn(search, 1);
        search.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(search);
        var buttons = new WindowCaptionButtons { Margin = new Thickness(0, 0, 8, 0) };
        Grid.SetColumn(buttons, 3);
        header.Children.Add(buttons);
        DockPanel.SetDock(header, Avalonia.Controls.Dock.Top);
        if (OperatingSystem.IsWindows())
        {
            // The system moves, snaps and maximizes the window from the header; the search field keeps its clicks.
            WindowDecorationProperties.SetElementRole(header, WindowDecorationsElementRole.TitleBar);
            WindowDecorationProperties.SetElementRole(search, WindowDecorationsElementRole.User);
        }
        header.PointerPressed += (_, args) =>
        {
            // macOS and Linux: the header moves the window itself (on Windows the press never gets here).
            if (args.Handled || !args.GetCurrentPoint(header).Properties.IsLeftButtonPressed)
            {
                return;
            }
            if (args.ClickCount == 2)
            {
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                window.BeginMoveDrag(args);
            }
            args.Handled = true;
        };

        // The app lives in an overlay host beside the header: a dialog covers the host and leaves the header in reach.
        var open = new Button { Content = "Open a dialog", HorizontalAlignment = HorizontalAlignment.Center };
        open.Click += async (_, _) =>
            await Dialog.ConfirmAsync(open, "Discard this draft?",
                "The dialog covers the content only: the title bar and its buttons stay above the scrim.", "Discard",
                isDanger: true);
        var body = new ArcOverlayHost
        {
            Children =
            {
                new StackPanel
                {
                    Spacing = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = "Drag the header to move the window, double-click it to maximize.",
                            HorizontalAlignment = HorizontalAlignment.Center
                        },
                        open
                    }
                }
            }
        };

        window.Content = new DockPanel { Children = { header, body } };
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            window.Show(owner);
        }
        else
        {
            window.Show();
        }
    }
}
