using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TKCode.Arc.Gallery.Demos;

public partial class RichTextEditorOwnToolbarDemo : UserControl
{
    public RichTextEditorOwnToolbarDemo()
    {
        InitializeComponent();
        Editor.PropertyChanged += OnEditorPropertyChanged;
        Refresh();
    }

    private void OnMarkClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RichTextMark mark })
        {
            Editor.ToggleMark(mark);
        }
    }

    private void OnBlockClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RichTextBlockType type })
        {
            Editor.SetBlockType(type);
        }
    }

    private void OnLinkClick(object? sender, RoutedEventArgs e) => Editor.EditLink();

    private void OnUndoClick(object? sender, RoutedEventArgs e) => Editor.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => Editor.Redo();

    private void OnEditorPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == RichTextEditor.FormatsProperty
            || e.Property == RichTextEditor.CanUndoProperty
            || e.Property == RichTextEditor.CanRedoProperty)
        {
            Refresh();
        }
    }

    // Shows what is active at the caret, and which history buttons have something to do.
    private void Refresh()
    {
        var formats = Editor.Formats;
        foreach (var button in Toolbar.Children.OfType<Button>())
        {
            button.Variant = IsActive(button, formats) ? ButtonVariant.Secondary : ButtonVariant.Ghost;
        }
        UndoButton.IsEnabled = Editor.CanUndo;
        RedoButton.IsEnabled = Editor.CanRedo;
    }

    private bool IsActive(Button button, RichTextFormats formats)
        => button == LinkButton
            ? formats.Link is not null
            : button.Tag switch
            {
                RichTextMark.Bold => formats.IsBold,
                RichTextMark.Italic => formats.IsItalic,
                RichTextMark.Strikethrough => formats.IsStrikethrough,
                RichTextMark.Code => formats.IsCode,
                RichTextBlockType type => formats.BlockType == type,
                _ => false
            };
}
