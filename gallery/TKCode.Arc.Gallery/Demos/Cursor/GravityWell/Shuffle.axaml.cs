using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace TKCode.Arc.Gallery.Demos;

public partial class GravityWellShuffleDemo : UserControl
{
    private readonly ObservableCollection<GravityWellPerson> _people = new(GravityWellDemoData.People);
    private readonly Random _random = new(7);
    private int _shuffles;

    public GravityWellShuffleDemo()
    {
        InitializeComponent();
        Well.ItemsSource = _people;
    }

    private void OnPulsed(object? sender, GravityWellPulseEventArgs e)
    {
        for (var i = 0; i < 4; i++)
        {
            _people.Move(_random.Next(_people.Count), _random.Next(_people.Count));
        }
        _shuffles++;
        Status.Text = $"Shuffled {_shuffles} {(_shuffles == 1 ? "time" : "times")}, last from ({e.Origin.X:0}, {e.Origin.Y:0})";
    }
}
