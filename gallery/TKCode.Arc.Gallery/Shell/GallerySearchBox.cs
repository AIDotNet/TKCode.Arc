using TKCode.Arc.DesignTokens;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace TKCode.Arc.Gallery.Shell;

/// <summary>
/// The sidebar's borderless search input: a bare text box (placeholder and text presenter only) whose frame, icon and
/// focus border belong to the sidebar. It carries its own template so it does not depend on a theme for TextBox.
/// </summary>
internal sealed class GallerySearchBox : TextBox
{
    private static readonly ControlTheme s_theme = BuildTheme();

    protected override Type StyleKeyOverride => typeof(TextBox);

    public GallerySearchBox()
    {
        Theme = s_theme;
        ArcTokens.Bind(this, CaretBrushProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, SelectionBrushProperty, ArcDesignTokenKind.AccentSubtle);
    }

    private static ControlTheme BuildTheme()
    {
        var theme = new ControlTheme(typeof(TextBox));
        theme.Setters.Add(new Setter(FocusAdornerProperty, null));
        theme.Setters.Add(new Setter(CursorProperty, new Cursor(StandardCursorType.Ibeam)));
        theme.Setters.Add(new Setter(AcceptsReturnProperty, false));
        theme.Setters.Add(new Setter(TemplateProperty, new FuncControlTemplate<TextBox>(static (box, scope) =>
        {
            var placeholder = new TextBlock
            {
                Name = "PART_Watermark",
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                IsHitTestVisible = false,
                [!TextBlock.TextProperty] = box[!PlaceholderTextProperty],
                [!IsVisibleProperty] = new TemplateBinding(TextProperty) { Converter = StringConverters.IsNullOrEmpty }
            }.RegisterInNameScope(scope);
            ArcTokens.Bind(placeholder, TextBlock.ForegroundProperty, ArcDesignTokenKind.TextMuted);
            var presenter = new TextPresenter
            {
                Name = "PART_TextPresenter",
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                [!TextPresenter.TextProperty] = new TemplateBinding(TextProperty) { Mode = BindingMode.TwoWay },
                [!TextPresenter.CaretIndexProperty] = box[!CaretIndexProperty],
                [!TextPresenter.SelectionStartProperty] = box[!SelectionStartProperty],
                [!TextPresenter.SelectionEndProperty] = box[!SelectionEndProperty],
                [!TextPresenter.TextAlignmentProperty] = box[!TextAlignmentProperty],
                [!TextPresenter.SelectionBrushProperty] = box[!SelectionBrushProperty],
                [!TextPresenter.SelectionForegroundBrushProperty] = box[!SelectionForegroundBrushProperty],
                [!TextPresenter.CaretBrushProperty] = box[!CaretBrushProperty]
            }.RegisterInNameScope(scope);
            return new Panel
            {
                Background = Brushes.Transparent,
                [!MarginProperty] = box[!PaddingProperty],
                Children = { placeholder, presenter }
            };
        })));
        return theme;
    }
}
