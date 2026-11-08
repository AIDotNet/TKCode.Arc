using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

/// <summary>A note card of the waterfall demo.</summary>
public sealed record WaterfallNote(string Title, string Body);

public partial class WaterfallPanelItemsControlDemo : UserControl
{
    private static readonly string[] s_bodies =
    [
        "Ship it.",
        "Move the weekly sync to Thursday so design review has the morning.",
        "Pricing page copy is final; legal wants one more look at the footnotes before it goes live.",
        "Ask Priya about the export bug.",
        "The onboarding checklist now has five steps instead of eight, and the drop-off after step two is down by a third.",
        "Order more coffee.",
    ];

    private readonly ObservableCollection<WaterfallNote> _notes = [];
    private int _next;

    public WaterfallPanelItemsControlDemo()
    {
        InitializeComponent();
        for (var i = 0; i < 8; i++)
        {
            AddNote();
        }
        Notes.ItemsSource = _notes;
        Add.Click += (_, _) => AddNote();
        Remove.Click += (_, _) =>
        {
            if (_notes.Count > 0)
            {
                _notes.RemoveAt(0);
            }
        };
    }

    private void AddNote()
    {
        _next++;
        _notes.Add(new WaterfallNote($"Note {_next}", s_bodies[_next % s_bodies.Length]));
    }
}
