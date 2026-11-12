using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc.Gallery.Demos;

public partial class ToolDockPreviewDemo : UserControl
{
    private const string Select = "select";

    private readonly TranslateTransform _pan = new();
    private readonly List<Control> _added = new();
    private readonly List<Control> _comments = new();
    private Point? _panFrom;
    private int _placed;

    public ToolDockPreviewDemo()
    {
        InitializeComponent();
        Sheet.RenderTransform = _pan;
        Dots.Background = DotBrush();
        ActualThemeVariantChanged += (_, _) => Dots.Background = DotBrush();
        BuildSheet();
        Dock.ItemChosen += (_, _) => Board.Focus(NavigationMethod.Pointer);
        Dock.PropertyChanged += (_, e) =>
        {
            if (e.Property == ToolDock.SelectedValueProperty)
            {
                OnToolChanged();
            }
        };
        Board.PointerPressed += OnBoardPressed;
        Board.PointerMoved += OnBoardMoved;
        Board.PointerReleased += (_, _) => _panFrom = null;
        Board.PointerCaptureLost += (_, _) => _panFrom = null;
        Board.KeyDown += OnBoardKey;
        Reset.Click += (_, _) => ResetBoard();
        OnToolChanged();
    }

    private string Tool => Dock.SelectedValue as string ?? Select;

    // ---- The board ---------------------------------------------------------------------------------------------------

    private void BuildSheet()
    {
        Add(Photo(7, 168, 210, "Still lake"), 64, 40, permanent: true);
        Add(Photo(1, 220, 146, "Harbour dusk"), 372, 58, permanent: true);
        AddComment("AK", "Crop it a little tighter?", 196, 30, permanent: true);
        AddComment("JL", "Love these colors", 470, 34, permanent: true);
        UpdateBadge();
    }

    private static Control Photo(int scene, int width, int height, string caption)
    {
        var picture = new Border
        {
            Width = width,
            Height = height,
            ClipToBounds = true,
            BorderThickness = new Thickness(1),
            Child = new Image { Source = PhotosDemoArt.Scene(scene, width * 2, height * 2), Stretch = Stretch.UniformToFill }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        ArcTokens.Bind(picture, Border.BorderBrushProperty, ArcDesignTokenKind.BorderSubtle);
        ArcTokens.Bind(picture, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
        var label = new TextBlock { Text = caption, Margin = new Thickness(0, 6, 0, 0) };
        ArcTokens.Bind(label, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXS);
        ArcTokens.Bind(label, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextSecondary);
        return new StackPanel { Children = { picture, label } };
    }

    private void AddComment(string initials, string text, double x, double y, bool permanent = false)
    {
        var avatar = new Border
        {
            Width = 22,
            Height = 22,
            Child = new TextBlock
            {
                Text = initials,
                FontSize = 9,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        }.BindCornerRadius(ArcDesignTokenKind.RadiusPill);
        ArcTokens.Bind(avatar, Border.BackgroundProperty, ArcDesignTokenKind.AccentSubtle);
        var pin = new Button
        {
            Variant = ButtonVariant.Secondary,
            Size = ControlSize.Small,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children = { avatar, new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center } }
            }
        };
        AutomationProperties.SetName(pin, $"{initials}: {text}. Resolve");
        pin.Click += (_, _) => Resolve(pin);
        _comments.Add(pin);
        Add(pin, x, y, permanent);
        UpdateBadge();
    }

    private void Resolve(Control pin)
    {
        _comments.Remove(pin);
        Sheet.Children.Remove(pin);
        _added.Remove(pin);
        Reset.IsVisible = true;
        UpdateBadge();
        Status.Text = "Resolved a comment.";
    }

    private void Add(Control child, double x, double y, bool permanent)
    {
        Canvas.SetLeft(child, x);
        Canvas.SetTop(child, y);
        Sheet.Children.Add(child);
        if (!permanent)
        {
            _added.Add(child);
            Reset.IsVisible = true;
        }
    }

    private void UpdateBadge() => CommentTool.Badge = _comments.Count;

    private void ResetBoard()
    {
        Sheet.Children.Clear();
        _added.Clear();
        _comments.Clear();
        _pan.X = 0;
        _pan.Y = 0;
        BuildSheet();
        Reset.IsVisible = false;
        Dock.SelectedValue = Select;
        Status.Text = "The board is back as it was.";
    }

    // ---- Tools --------------------------------------------------------------------------------------------------------

    private void OnToolChanged()
    {
        Board.Cursor = Tool switch
        {
            Select => Cursor.Default,
            "pan" => new Cursor(StandardCursorType.SizeAll),
            _ => new Cursor(StandardCursorType.Cross)
        };
        Status.Text = Tool switch
        {
            Select => "Select is on. Pick a tool in the dock, or press its key.",
            "pan" => "Drag the board to look around it.",
            "text" => "Click the board to add a text label.",
            "note" => "Click the board to stick a note on it.",
            "comment" => "Click the board to leave a comment, or click a comment to resolve it.",
            var shape => $"Click the board to add {Article(shape)} {shape}."
        };
    }

    private static string Article(string word) => "aeiou".Contains(word[0]) ? "an" : "a";

    private void OnBoardKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }
        string? tool = e.Key switch
        {
            Key.V => Select,
            Key.H => "pan",
            Key.R => "rectangle",
            Key.O => "ellipse",
            Key.T => "text",
            Key.N => "note",
            Key.C => "comment",
            _ => null
        };
        if (tool is not null)
        {
            e.Handled = true;
            Dock.SelectedValue = tool;
        }
    }

    private void OnBoardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && (Dock.IsVisualAncestorOf(source) || ReferenceEquals(source, Dock) || source.FindAncestorOfType<Button>() is not null))
        {
            return;
        }
        Board.Focus(NavigationMethod.Pointer);
        var point = e.GetPosition(Sheet);
        switch (Tool)
        {
            case Select:
                return;
            case "pan":
                _panFrom = e.GetPosition(Board) - new Point(_pan.X, _pan.Y);
                e.Pointer.Capture(Board);
                return;
            case "comment":
                AddComment("ME", "New comment", point.X - 12, point.Y - 15);
                Status.Text = "Comment added. Click it to resolve it.";
                break;
            case "text":
                Add(TextLabel(), point.X, point.Y - 14, permanent: false);
                Status.Text = "Added a text label. Back to Select.";
                break;
            case "note":
                Add(Note(), point.X - 60, point.Y - 44, permanent: false);
                Status.Text = "Stuck a note on the board. Back to Select.";
                break;
            case var shape:
                Add(ShapeOf(shape), point.X - 44, point.Y - 30, permanent: false);
                Status.Text = $"Added {Article(shape)} {shape}. Back to Select.";
                break;
        }
        _placed++;
        e.Handled = true;
        var message = Status.Text;
        Dock.SelectedValue = Select;
        Status.Text = message;
    }

    private void OnBoardMoved(object? sender, PointerEventArgs e)
    {
        if (_panFrom is not { } from)
        {
            return;
        }
        var position = e.GetPosition(Board);
        _pan.X = position.X - from.X;
        _pan.Y = position.Y - from.Y;
    }

    private Control TextLabel()
    {
        var text = new TextBlock { Text = _placed % 2 == 0 ? "Moodboard, first pass" : "Shortlist", FontWeight = FontWeight.Medium };
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeXL);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        return text;
    }

    private static Control Note()
    {
        var text = new TextBlock { Text = "Pick the cover shot by Friday", TextWrapping = TextWrapping.Wrap };
        ArcTokens.Bind(text, TextBlock.FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(text, TextBlock.ForegroundProperty, ArcDesignTokenKind.Foreground);
        var note = new Border { Width = 128, Height = 96, Padding = new Thickness(12), Child = text }
            .BindCornerRadius(ArcDesignTokenKind.RadiusStep6);
        ArcTokens.Bind(note, Border.BackgroundProperty, ArcDesignTokenKind.WarningSubtle);
        ArcTokens.Bind(note, Border.BoxShadowProperty, ArcDesignTokenKind.ShadowResting);
        return note;
    }

    private static Control ShapeOf(string kind)
    {
        Geometry geometry = kind switch
        {
            "ellipse" => new EllipseGeometry(new Rect(0, 0, 88, 60)),
            "diamond" => StreamGeometry.Parse("M44,0 L88,30 L44,60 L0,30 Z"),
            "hexagon" => StreamGeometry.Parse("M22,0 L66,0 L88,30 L66,60 L22,60 L0,30 Z"),
            _ => new RectangleGeometry(new Rect(0, 0, 88, 60), 10, 10)
        };
        var path = new Avalonia.Controls.Shapes.Path { Data = geometry, StrokeThickness = 1.5, Width = 88, Height = 60 };
        ArcTokens.Bind(path, Avalonia.Controls.Shapes.Shape.FillProperty, ArcDesignTokenKind.AccentSubtle);
        ArcTokens.Bind(path, Avalonia.Controls.Shapes.Shape.StrokeProperty, ArcDesignTokenKind.Accent);
        return path;
    }

    /// <summary>The board's dot grid: one dot every 22 px in the text color at 9%.</summary>
    private IBrush DotBrush()
    {
        var color = ArcTokens.GetColor(this, ArcDesignTokenKind.Foreground);
        var dot = Color.FromArgb(ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark ? (byte)20 : (byte)23, color.R, color.G, color.B);
        return new DrawingBrush
        {
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            DestinationRect = new RelativeRect(0, 0, 22, 22, RelativeUnit.Absolute),
            Drawing = new DrawingGroup
            {
                Children =
                {
                    new GeometryDrawing { Brush = Brushes.Transparent, Geometry = new RectangleGeometry(new Rect(0, 0, 22, 22)) },
                    new GeometryDrawing { Brush = new SolidColorBrush(dot), Geometry = new EllipseGeometry(new Rect(10, 10, 2, 2)) }
                }
            }
        };
    }
}
