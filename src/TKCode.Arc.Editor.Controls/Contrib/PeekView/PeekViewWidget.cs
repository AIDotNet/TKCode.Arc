// Ported from vscode (src/vs/editor/contrib/peekView/browser/peekView.ts: PeekViewWidget with its head (title,
// secondary title, meta title, close action) and body; the PeekContext keys; commit b57d5c9e), MIT license.
// Differences from upstream: the head is a Grid of TextBlocks and a close button drawn from a geometry; there is no
// action bar besides close and no title click; the colors are the CodeEditorPeekSurface's.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor.Platform;

namespace TKCode.Arc;

/// <summary>The context keys of peek views (upstream's PeekContext).</summary>
internal static class PeekContext
{
    public static readonly RawContextKey<bool> InPeekEditor = new("inReferenceSearchEditor", true, "Whether the current code editor is embedded inside peek");

    public static readonly Editor.Platform.ContextKeyExpression NotInPeekEditor = InPeekEditor.ToNegated();
}

/// <summary>A zone widget with a title bar and a close button (upstream's PeekViewWidget).</summary>
internal abstract class PeekViewWidget : ZoneWidget
{
    private static readonly Geometry s_close = Geometry.Parse("M4,4 L12,12 M12,4 L4,12");

    private readonly TextBlock _primaryHeading;
    private readonly TextBlock _secondaryHeading;
    private readonly TextBlock _metaHeading;
    private readonly Grid _head;

    protected PeekViewWidget(CodeEditor editor, ZoneWidgetOptions options)
        : base(editor, options)
    {
        _primaryHeading = new TextBlock { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        _secondaryHeading = new TextBlock { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        _metaHeading = new TextBlock { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        Surface.BindBrush(_secondaryHeading, TextBlock.ForegroundProperty, CodeEditorWidgetSurface.MutedForegroundProperty);
        Surface.BindBrush(_metaHeading, TextBlock.ForegroundProperty, CodeEditorWidgetSurface.MutedForegroundProperty);

        var closeGlyph = new Avalonia.Controls.Shapes.Path { Data = s_close, StrokeThickness = 1.4, Width = 16, Height = 16, Stretch = Stretch.None };
        Surface.BindBrush(closeGlyph, Shape.StrokeProperty, CodeEditorWidgetSurface.MutedForegroundProperty);
        CloseButton = new Border
        {
            Child = closeGlyph,
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
        };
        IDisposable? hover = null;
        CloseButton.PointerEntered += (_, _) =>
        {
            CloseButton.ClearValue(Border.BackgroundProperty);
            hover ??= Surface.BindBrush(CloseButton, Border.BackgroundProperty, CodeEditorWidgetSurface.HoverBackgroundProperty);
        };
        CloseButton.PointerExited += (_, _) =>
        {
            hover?.Dispose();
            hover = null;
            CloseButton.Background = Brushes.Transparent;
        };
        CloseButton.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            OnCloseRequested();
        };

        _head = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            Margin = new Thickness(0),
        };
        var titles = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 0), Children = { _primaryHeading, _secondaryHeading } };
        _head.Children.Add(titles);
        Grid.SetColumn(_metaHeading, 2);
        _head.Children.Add(_metaHeading);
        Grid.SetColumn(CloseButton, 3);
        CloseButton.Margin = new Thickness(4, 0, 6, 0);
        _head.Children.Add(CloseButton);
        var headBorder = new Border { Child = _head };
        Surface.BindBrush(headBorder, Border.BackgroundProperty, CodeEditorPeekSurface.HeaderBackgroundProperty);

        Body = new Border();
        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(headBorder, Dock.Top);
        layout.Children.Add(headBorder);
        layout.Children.Add(Body);
        HeadBorder = headBorder;
        Container.Child = layout;
    }

    /// <summary>The area below the head.</summary>
    protected Border Body { get; }

    protected Border HeadBorder { get; }

    /// <summary>The close button (for tests).</summary>
    internal Border CloseButton { get; }

    /// <summary>The title, the path and the count shown in the head (for tests).</summary>
    internal (string? Title, string? Secondary, string? Meta) Titles => (_primaryHeading.Text, _secondaryHeading.Text, _metaHeading.Text);

    /// <summary>Sets the title and the secondary title (upstream's setTitle).</summary>
    public void SetTitle(string primaryHeading, string? secondaryHeading = null)
    {
        _primaryHeading.Text = primaryHeading;
        _secondaryHeading.Text = secondaryHeading;
    }

    /// <summary>Sets the text at the end of the head (upstream's setMetaTitle).</summary>
    public void SetMetaTitle(string value) => _metaHeading.Text = value;

    /// <summary>The close button was pressed.</summary>
    protected abstract void OnCloseRequested();

    /// <inheritdoc />
    protected override void DoLayout(double heightInPixel)
    {
        // upstream's _headHeight: 1.2 lines
        var headHeight = Math.Ceiling((Editor.View?.LineHeight ?? 18) * 1.2);
        HeadBorder.Height = headHeight;
        DoLayoutBody(Math.Max(0, heightInPixel - headHeight));
    }

    /// <summary>Lays the body out for its height (upstream's _doLayoutBody).</summary>
    protected abstract void DoLayoutBody(double heightInPixel);
}
