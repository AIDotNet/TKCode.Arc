using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc.Gallery.Demos;

public partial class OrbitMenuPreviewDemo : UserControl
{
    private const int BaseLikes = 212;

    private bool _liked;
    private bool _inAlbum;
    private bool _sent;
    private bool _saved;
    private bool _deleted;

    public OrbitMenuPreviewDemo()
    {
        InitializeComponent();
        Photo.Source = PhotosDemoArt.Scene(2, 716, 400);
        Menu.ActionInvoked += OnAction;
        // The card steps back while the actions are out.
        Menu.PropertyChanged += (_, e) =>
        {
            if (e.Property == OrbitMenu.IsOpenProperty)
            {
                CardHost.Classes.Set("dim", Menu.IsOpen);
            }
        };
        Reset.Click += (_, _) => ResetPhoto();
        Restore.Click += (_, _) => ResetPhoto();
    }

    private void OnAction(object? sender, OrbitMenuActionEventArgs e)
    {
        var action = e.Action;
        if (ReferenceEquals(action, LikeAction))
        {
            _liked = true;
        }
        else if (ReferenceEquals(action, AlbumAction))
        {
            _inAlbum = true;
        }
        else if (ReferenceEquals(action, SendAction))
        {
            _sent = true;
        }
        else if (ReferenceEquals(action, SaveAction))
        {
            _saved = true;
        }
        else if (ReferenceEquals(action, DeleteAction))
        {
            _deleted = true;
        }
        Update();
    }

    private void ResetPhoto()
    {
        _liked = _inAlbum = _sent = _saved = _deleted = false;
        Update();
    }

    private void Update()
    {
        Card.IsVisible = !_deleted;
        Deleted.IsVisible = _deleted;
        Menu.IsEnabled = !_deleted;
        Reset.IsEnabled = _liked || _inAlbum || _sent || _saved || _deleted;
        Chips.Children.Clear();
        if (_liked)
        {
            Chips.Children.Add(Chip(ArcIcons.Heart, $"Liked by you and {BaseLikes}"));
        }
        if (_inAlbum)
        {
            Chips.Children.Add(Chip(ArcIcons.Folder, "Weekend hikes"));
        }
        if (_sent)
        {
            Chips.Children.Add(Chip(null, "Theo Marsh"));
        }
        if (_saved)
        {
            Chips.Children.Add(Chip(ArcIcons.Download, "Saved"));
        }
        Chips.IsVisible = Chips.Children.Count > 0;
        Placeholder.IsVisible = Chips.Children.Count == 0;
    }

    private static Border Chip(Geometry? glyph, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        if (glyph is not null)
        {
            row.Children.Add(new ArcGlyph { Data = glyph });
        }
        else
        {
            // A person: a small gradient avatar instead of a glyph.
            row.Children.Add(new Ellipse
            {
                Width = 14,
                Height = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = new LinearGradientBrush
                {
                    StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                    EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.Parse("#7FD6B4"), 0), new GradientStop(Color.Parse("#4C8DF0"), 1) }
                }
            });
        }
        row.Children.Add(new TextBlock { Text = text });
        return new Border { Classes = { "chip" }, Child = row };
    }
}
