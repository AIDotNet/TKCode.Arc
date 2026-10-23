using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentMemoryBindingsDemo : UserControl
{
    public AgentMemoryBindingsDemo()
    {
        InitializeComponent();
        Memory.Groups.AddRange(AgentMemoryDemoData.Groups());
        Memory.Capacity = AgentMemoryDemoData.Capacity;

        // A list of your own to follow: the card changes it, and what you change in it shows up in the card.
        var memories = new ObservableCollection<MemoryItem>(AgentMemoryDemoData.Memories());
        Memory.MemoriesSource = memories;

        Preferences.Click += (_, _) => Memory.SelectedGroupKey = "preferences";
        Everything.Click += (_, _) =>
        {
            Memory.IsSearchOpen = false;
            Memory.SelectedGroupKey = null;
        };
        Search.Click += (_, _) =>
        {
            Memory.IsSearchOpen = true;
            Memory.SearchText = "ai";
        };

        void Show() => State.Text = $"{memories.Count} memories · filter: {Memory.SelectedGroupKey ?? "all"}"
            + (Memory.IsSearchOpen ? $" · search: “{Memory.SearchText}”" : string.Empty);
        Memory.PropertyChanged += (_, e) =>
        {
            if (e.Property == AgentMemory.SelectedGroupKeyProperty || e.Property == AgentMemory.SearchTextProperty
                || e.Property == AgentMemory.IsSearchOpenProperty)
            {
                Show();
            }
        };
        memories.CollectionChanged += (_, _) => Show();
        Show();
    }
}
