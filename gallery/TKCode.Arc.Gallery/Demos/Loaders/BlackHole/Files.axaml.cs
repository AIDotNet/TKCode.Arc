using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

public partial class BlackHoleFilesDemo : UserControl
{
    private static readonly string[] Names = ["brief.md", "q3-plan.xlsx", "logo-draft.svg", "notes.txt", "export.csv", "sketch.png"];
    private readonly List<Border> _all = new();
    private readonly HashSet<Border> _selected = new();
    private List<Border> _removed = new();
    private readonly List<string> _log = new();

    public BlackHoleFilesDemo()
    {
        InitializeComponent();
        foreach (var name in Names)
        {
            var tile = Tile(name);
            _all.Add(tile);
            Tiles.Children.Add(tile);
        }
    }

    private Border Tile(string name)
    {
        var label = new TextBlock { Text = name, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
        var tile = new Border
        {
            Width = 160,
            Height = 52,
            Padding = new Thickness(12, 0),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { new ArcGlyph { Data = ArcIcons.File, Size = 16 }, label }
            }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep14);
        AutomationProperties.SetName(tile, name);
        Paint(tile);
        tile.PointerReleased += (_, _) =>
        {
            if (Hole.IsRunning)
            {
                return;
            }
            if (!_selected.Remove(tile))
            {
                _selected.Add(tile);
            }
            Paint(tile);
        };
        return tile;
    }

    private readonly Dictionary<Border, IDisposable[]> _paint = new();

    private void Paint(Border tile)
    {
        // Selected tiles take the muted surface and a stronger border (the previous token bindings are let go first).
        if (_paint.TryGetValue(tile, out var old))
        {
            foreach (var binding in old)
            {
                binding.Dispose();
            }
        }
        var on = _selected.Contains(tile);
        _paint[tile] =
        [
            ArcTokens.Bind(tile, Border.BackgroundProperty, on ? ArcDesignTokenKind.SurfaceMuted : ArcDesignTokenKind.Surface),
            ArcTokens.Bind(tile, Border.BorderBrushProperty, on ? ArcDesignTokenKind.BorderStrong : ArcDesignTokenKind.Border)
        ];
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        var targets = _all.Where(_selected.Contains).ToList();
        if (targets.Count == 0 || Hole.IsRunning)
        {
            return;
        }
        // Pull them in; remove them from the panel once the hole has closed.
        await Hole.SwallowAsync(targets);
        foreach (var tile in targets)
        {
            Tiles.Children.Remove(tile);
            _selected.Remove(tile);
            Paint(tile);
        }
        _removed = targets;
    }

    private async void OnRestore(object? sender, RoutedEventArgs e)
    {
        if (_removed.Count == 0 || Hole.IsRunning)
        {
            return;
        }
        var restored = _removed;
        _removed = new();
        // Back at their places first, then released (they stay hidden until they spiral out).
        foreach (var tile in restored)
        {
            var index = _all.TakeWhile(t => t != tile).Count(Tiles.Children.Contains);
            Tiles.Children.Insert(index, tile);
        }
        await Hole.ReleaseAsync(restored);
    }

    private void OnStarted(object? sender, BlackHoleTransitionEventArgs e) => Write($"{e.Direction} started ({e.Count})");

    private void OnCompleted(object? sender, BlackHoleTransitionEventArgs e) => Write($"{e.Direction} completed ({e.Count})");

    private void Write(string line)
    {
        _log.Add(line);
        if (_log.Count > 4)
        {
            _log.RemoveAt(0);
        }
        Log.Text = string.Join("\n", _log);
    }
}
