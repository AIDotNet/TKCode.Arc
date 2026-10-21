using System.Globalization;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class ScrubInputEventsDemo : UserControl
{
    private const int MaxEntries = 6;

    private readonly List<string> _entries = [];

    public ScrubInputEventsDemo()
    {
        InitializeComponent();
        AddEntry("Scrub, step or type to see the events.");
    }

    // Fires for every change, including the ones made from code.
    private void OnValueChanged(object? sender, ScrubInputValueChangedEventArgs e)
        => AddEntry($"ValueChanged    {Format(e.OldValue)} → {Format(e.NewValue)}  ({e.ChangeSource})");

    // Fires once a change made in the field settles.
    private void OnValueCommitted(object? sender, ScrubInputValueChangedEventArgs e)
        => AddEntry($"ValueCommitted  {Format(e.OldValue)} → {Format(e.NewValue)}  ({e.ChangeSource})");

    private void OnSetFromCodeClick(object? sender, RoutedEventArgs e) => Amount.Value = 25;

    private void AddEntry(string entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
        Log.Text = string.Join('\n', _entries);
    }

    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
