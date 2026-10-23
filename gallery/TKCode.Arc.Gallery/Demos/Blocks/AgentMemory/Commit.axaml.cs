using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class AgentMemoryCommitDemo : UserControl
{
    public AgentMemoryCommitDemo()
    {
        InitializeComponent();
        Memory.Groups.AddRange(AgentMemoryDemoData.Groups());
        Memory.Memories.AddRange(AgentMemoryDemoData.Memories());
        Memory.Capacity = AgentMemoryDemoData.Capacity;

        // Save somewhere first: the card shows a saving state until the task ends, and a failure rolls the change back in place.
        Memory.CommitAsync = async (change, cancellation) =>
        {
            await Task.Delay(700, cancellation);
            if (change.Kind != MemoryChangeKind.Forget && change.Memory.Text.Contains("fail", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The server refused this memory. Try other words.");
            }
        };

        // Sort a typed memory yourself, here by its first letter, instead of the built-in keyword guess.
        Memory.ClassifyAsync = async (text, cancellation) =>
        {
            await Task.Delay(400, cancellation);
            return char.ToLowerInvariant(text[0]) switch
            {
                >= 'a' and <= 'h' => "about",
                >= 'i' and <= 'm' => "preferences",
                >= 'n' and <= 's' => "projects",
                _ => "people"
            };
        };

        Memory.MemoryChanged += (_, e) =>
            Log.Text = $"{e.Change.Kind}: “{e.Change.Memory.Text}” · {e.Memories.Count} memories";
    }
}
