using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class SpeculativeDecodingBoundControlsDemo : UserControl
{
    private readonly List<string> _lines = new();

    public SpeculativeDecodingBoundControlsDemo()
    {
        InitializeComponent();
        Write($"Planned: {Describe(Decoder.PlannedStats)}");
    }

    private void OnShortClick(object? sender, RoutedEventArgs e) => Decoder.DraftLength = 2;

    private void OnMiddleClick(object? sender, RoutedEventArgs e) => Decoder.DraftLength = 4;

    private void OnLongClick(object? sender, RoutedEventArgs e) => Decoder.DraftLength = 8;

    private void OnCompareClick(object? sender, RoutedEventArgs e) => Decoder.IsComparing = !Decoder.IsComparing;

    private void OnDecoderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == SpeculativeDecoding.DraftLengthProperty)
        {
            Write($"Draft length {Decoder.DraftLength}, planned: {Describe(Decoder.PlannedStats)}");
        }
        else if (e.Property == SpeculativeDecoding.IsComparingProperty)
        {
            Write(Decoder.IsComparing ? "Racing the target alone" : "Race hidden");
        }
    }

    private void OnCompleted(object? sender, SpeculativeDecodingCompletedEventArgs e) => Write($"Completed: {Describe(e.Stats)}");

    private static string Describe(DecodeStats? stats) => stats is null
        ? "nothing to play"
        : string.Format(CultureInfo.InvariantCulture, "{0} passes, {1:0}% accepted, {2:0.0}× faster",
            stats.Passes, stats.Acceptance * 100, stats.Speedup);

    private void Write(string line)
    {
        _lines.Insert(0, line);
        if (_lines.Count > 4)
        {
            _lines.RemoveAt(_lines.Count - 1);
        }
        Log.Text = string.Join(Environment.NewLine, _lines);
    }
}
