using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>
/// The bare text input used inside select fields (Combobox field, MorphSelect and MultiSelect search rows): an
/// Avalonia <see cref="TextBox"/> with a frameless template built in code, so the field frame, focus treatment
/// and placeholder color stay with the owning control. Brushes come from ArcDesign tokens.
/// </summary>
internal static class SelectInputBox
{
    private static readonly FuncControlTemplate<TextBox> s_template = new((box, scope) =>
    {
        var placeholder = new TextBlock
        {
            Name = "PART_Placeholder",
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        placeholder.Bind(TextBlock.TextProperty, new TemplateBinding(TextBox.PlaceholderTextProperty));
        placeholder.Bind(TextBlock.ForegroundProperty, new TemplateBinding(TextBox.PlaceholderForegroundProperty));
        placeholder.Bind(Visual.IsVisibleProperty,
            new TemplateBinding(TextBox.TextProperty) { Converter = StringConverters.IsNullOrEmpty });

        var presenter = new TextPresenter
        {
            Name = "PART_TextPresenter",
            VerticalAlignment = VerticalAlignment.Center
        };
        presenter.Bind(TextPresenter.TextProperty, new TemplateBinding(TextBox.TextProperty) { Mode = BindingMode.TwoWay });
        presenter.Bind(TextPresenter.CaretIndexProperty, new TemplateBinding(TextBox.CaretIndexProperty));
        presenter.Bind(TextPresenter.SelectionStartProperty, new TemplateBinding(TextBox.SelectionStartProperty));
        presenter.Bind(TextPresenter.SelectionEndProperty, new TemplateBinding(TextBox.SelectionEndProperty));
        presenter.Bind(TextPresenter.CaretBrushProperty, new TemplateBinding(TextBox.CaretBrushProperty));
        presenter.Bind(TextPresenter.SelectionBrushProperty, new TemplateBinding(TextBox.SelectionBrushProperty));
        presenter.Bind(TextPresenter.SelectionForegroundBrushProperty, new TemplateBinding(TextBox.SelectionForegroundBrushProperty));
        presenter.Bind(TextPresenter.CaretBlinkIntervalProperty, new TemplateBinding(TextBox.CaretBlinkIntervalProperty));
        presenter.Bind(TextPresenter.TextAlignmentProperty, new TemplateBinding(TextBox.TextAlignmentProperty));
        presenter.Bind(TextPresenter.PasswordCharProperty, new TemplateBinding(TextBox.PasswordCharProperty));
        presenter.RegisterInNameScope(scope);
        placeholder.RegisterInNameScope(scope);
        return new Panel
        {
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Ibeam),
            Children = { placeholder, presenter }
        };
    });

    /// <summary>Creates a frameless single-line input whose text and placeholder use the given token colors.</summary>
    public static TextBox Create()
    {
        var box = new TextBox
        {
            Template = s_template,
            Background = Brushes.Transparent,
            BorderThickness = default,
            Padding = default,
            MinHeight = 0,
            AcceptsReturn = false,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FocusAdorner = null
        };
        ArcTokens.Bind(box, TextBox.ForegroundProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(box, TextBox.CaretBrushProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(box, TextBox.PlaceholderForegroundProperty, DesignTokens.ArcDesignTokenKind.TextMuted);
        ArcTokens.Bind(box, TextBox.SelectionBrushProperty, DesignTokens.ArcDesignTokenKind.AccentSubtle);
        ArcTokens.Bind(box, TextBox.SelectionForegroundBrushProperty, DesignTokens.ArcDesignTokenKind.Foreground);
        return box;
    }
}
