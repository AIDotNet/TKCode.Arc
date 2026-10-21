using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class CommandPaletteDataBindingDemo : UserControl
{
    public CommandPaletteDataBindingDemo()
    {
        InitializeComponent();
        DataContext = new CommandPaletteDataBindingViewModel();
    }
}

/// <summary>A view model that owns the commands: the palette follows the collection and runs <see cref="RunCommand"/>.</summary>
public sealed class CommandPaletteDataBindingViewModel : INotifyPropertyChanged
{
    private static readonly string[] Files = ["Q3 planning.md", "Launch checklist.md", "Design review notes.md", "Hiring plan.md"];
    private int _added;
    private string _lastRun = "Nothing run yet.";

    public CommandPaletteDataBindingViewModel()
    {
        Commands =
        [
            new() { Id = "search", Label = "Search the docs", Group = "Help", Icon = ArcIcons.Search },
            new() { Id = "shortcuts", Label = "Keyboard shortcuts", Group = "Help", Icon = ArcIcons.Keyboard, Shortcut = "?" },
            new() { Id = "contact", Label = "Contact support", Description = "Replies within a day", Group = "Help", Icon = ArcIcons.Mail }
        ];
        RunCommand = new Relay(item => LastRun = item is CommandPaletteItem command ? $"Ran “{command.Label}”" : LastRun);
        AddRecentCommand = new Relay(_ => AddRecent());
        ClearRecentCommand = new Relay(_ =>
        {
            foreach (var item in Commands.Where(c => c.Group == "Recent files").ToList())
            {
                Commands.Remove(item);
            }
        });
    }

    public ObservableCollection<CommandPaletteItem> Commands { get; }

    public ICommand RunCommand { get; }

    public ICommand AddRecentCommand { get; }

    public ICommand ClearRecentCommand { get; }

    public string LastRun
    {
        get => _lastRun;
        private set
        {
            _lastRun = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastRun)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void AddRecent()
    {
        var name = Files[_added % Files.Length];
        _added++;
        // Recent files go first, under their own heading.
        Commands.Insert(0, new CommandPaletteItem
        {
            Id = $"file-{_added}",
            Label = name,
            Description = "Opened just now",
            Group = "Recent files",
            Icon = ArcIcons.FileText
        });
    }

    private sealed class Relay(Action<object?> execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => execute(parameter);
    }
}
