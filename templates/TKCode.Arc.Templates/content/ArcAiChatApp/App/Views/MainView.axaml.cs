using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using ArcStarterApp.Settings;
using ArcStarterApp.ViewModels;
using TKCode.Arc;

namespace ArcStarterApp.Views;

/// <summary>
/// The app's content: the conversation list beside the chat. The main window shows it on the desktop. The chat block
/// keeps the conversations while the app runs; this view mirrors them into the sidebar and saves them after every reply.
/// </summary>
public partial class MainView : UserControl
{
    private readonly ObservableCollection<SidebarItem> _rows = new();
    private MainViewModel? _viewModel;

    public MainView()
    {
        InitializeComponent();
        // The sidebar opens as it was left: folded to the icon rail or not.
        Nav.IsCollapsed = AppSettings.Current.IsSidebarCollapsed;
        Nav.PropertyChanged += OnNavPropertyChanged;
        Nav.ItemsSource = _rows;
        Nav.ItemInvoked += (_, e) => Chat.CurrentConversation = Chat.Conversations.FirstOrDefault(c => c.Id == e.Item.Key);

        Chat.PropertyChanged += (_, e) =>
        {
            if (e.Property == AiChat.CurrentConversationProperty)
            {
                Nav.SelectedKey = Chat.CurrentConversation?.Id;
            }
        };
        Chat.MessageSent += (_, e) =>
        {
            // A new conversation is named after its first message.
            if (string.IsNullOrWhiteSpace(e.Conversation.Title))
            {
                e.Conversation.Title = MainViewModel.TitleFor(e.Turn.SelectedVersion?.Prompt.Text ?? string.Empty);
            }
            ShowRows();
        };
        // Deleting or renaming in the chat's conversation menu changes the list as well.
        Chat.Conversations.CollectionChanged += (_, _) =>
        {
            ShowRows();
            Save();
        };
        Chat.ReplyCompleted += (_, _) => Save();
        Chat.ModelChanged += (_, _) => Save();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is not MainViewModel vm || vm == _viewModel)
        {
            return;
        }
        _viewModel = null; // no saving while the saved conversations are put back
        Chat.Providers.AddRange(vm.Providers);
        Chat.Models.AddRange(vm.Models);
        Chat.Suggestions.AddRange(vm.Suggestions);
        Chat.Conversations.AddRange(vm.Restore());
        if (vm.SavedModel is { } model)
        {
            Chat.SelectedModel = model;
        }
        _viewModel = vm;
        ShowRows();
    }

    private void Save() => _viewModel?.Save(Chat.Conversations, Chat.SelectedModel);

    private void ShowRows()
    {
        _rows.Clear();
        foreach (var conversation in Chat.Conversations)
        {
            _rows.Add(new SidebarItem
            {
                Key = conversation.Id,
                Label = string.IsNullOrWhiteSpace(conversation.Title) ? "New chat" : conversation.Title,
                Icon = ArcIcons.MessageSquare,
            });
        }
        Nav.SelectedKey = Chat.CurrentConversation?.Id;
    }

    private void OnNavPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Sidebar.IsCollapsedProperty && AppSettings.Current.IsSidebarCollapsed != Nav.IsCollapsed)
        {
            AppSettings.Current.IsSidebarCollapsed = Nav.IsCollapsed;
            AppSettings.Current.Save();
        }
    }
}
