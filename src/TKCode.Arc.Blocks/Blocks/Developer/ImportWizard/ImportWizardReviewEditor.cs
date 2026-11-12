using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.DesignTokens;

namespace TKCode.Arc;

/// <summary>How the cell editor closed.</summary>
internal enum ImportReviewEditorClose
{
    /// <summary>Enter: save, focus back to the cell.</summary>
    Enter,

    /// <summary>Escape: discard, focus back to the cell.</summary>
    Escape,

    /// <summary>Focus went elsewhere: save, focus stays where it went.</summary>
    Blur
}

/// <summary>
/// The text field that covers a review cell while it is edited: 40 tall, radius 10, padding 0 12, a 1px Accent edge on
/// SurfaceRaised, at least 140 wide, 14px text with the caret in the text color and no extra focus ring (the accent edge is
/// the cue). Enter saves, Escape cancels, losing focus saves. A TextBox with its template built in code (the block's
/// parts are code-built; Avalonia's TextBox has no Arc theme).
/// </summary>
internal sealed class ImportWizardReviewEditor : TextBox
{
    private static readonly FuncControlTemplate<TextBox> s_template = new((box, scope) =>
    {
        var presenter = new TextPresenter
        {
            Name = "PART_TextPresenter",
            VerticalAlignment = VerticalAlignment.Center,
        };
        presenter.Bind(TextPresenter.TextProperty, new TemplateBinding(TextProperty) { Mode = BindingMode.TwoWay });
        presenter.Bind(TextPresenter.CaretIndexProperty, new TemplateBinding(CaretIndexProperty));
        presenter.Bind(TextPresenter.SelectionStartProperty, new TemplateBinding(SelectionStartProperty));
        presenter.Bind(TextPresenter.SelectionEndProperty, new TemplateBinding(SelectionEndProperty));
        presenter.Bind(TextPresenter.CaretBrushProperty, new TemplateBinding(CaretBrushProperty));
        presenter.Bind(TextPresenter.SelectionBrushProperty, new TemplateBinding(SelectionBrushProperty));
        presenter.Bind(TextPresenter.SelectionForegroundBrushProperty, new TemplateBinding(SelectionForegroundBrushProperty));
        presenter.Bind(TextPresenter.CaretBlinkIntervalProperty, new TemplateBinding(CaretBlinkIntervalProperty));
        presenter.Bind(TextPresenter.LineHeightProperty, new TemplateBinding(LineHeightProperty));
        presenter.Bind(TextPresenter.LetterSpacingProperty, new TemplateBinding(LetterSpacingProperty));
        presenter.RegisterInNameScope(scope);
        var scroll = new ScrollViewer
        {
            Name = "PART_ScrollViewer",
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Center,
            Content = presenter,
        };
        scroll.RegisterInNameScope(scope);
        var frame = new Border { Name = "PART_Frame", Child = scroll, Cursor = new Cursor(StandardCursorType.Ibeam) };
        frame.Bind(Border.BackgroundProperty, new TemplateBinding(BackgroundProperty));
        frame.Bind(Border.BorderBrushProperty, new TemplateBinding(BorderBrushProperty));
        frame.Bind(Border.BorderThicknessProperty, new TemplateBinding(BorderThicknessProperty));
        frame.Bind(Border.CornerRadiusProperty, new TemplateBinding(CornerRadiusProperty));
        frame.Bind(Border.PaddingProperty, new TemplateBinding(PaddingProperty));
        frame.RegisterInNameScope(scope);
        return frame;
    });

    private bool _closing;

    public ImportWizardReviewEditor()
    {
        Template = s_template;
        Height = 40;
        MinWidth = 140;
        Padding = new Thickness(12, 0);
        AcceptsReturn = false;
        TextWrapping = TextWrapping.NoWrap;
        VerticalContentAlignment = VerticalAlignment.Center;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        FocusAdorner = null;
        LineHeight = 19.6;
        LetterSpacing = -0.14;
        this.BindCornerRadius(ArcDesignTokenKind.RadiusStep10);
        ArcTokens.Bind(this, BackgroundProperty, ArcDesignTokenKind.SurfaceRaised);
        ArcTokens.Bind(this, BorderBrushProperty, ArcDesignTokenKind.Accent);
        ArcTokens.Bind(this, BorderThicknessProperty, ArcDesignTokenKind.EdgeThickness);
        ArcTokens.Bind(this, ForegroundProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, CaretBrushProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, SelectionBrushProperty, ArcDesignTokenKind.AccentSubtle);
        ArcTokens.Bind(this, SelectionForegroundBrushProperty, ArcDesignTokenKind.Foreground);
        ArcTokens.Bind(this, FontSizeProperty, ArcDesignTokenKind.TypeSM);
        ArcTokens.Bind(this, FontFamilyProperty, ArcDesignTokenKind.BodyFontFamily);
    }

    /// <summary>Raised once when the editor closes, with how.</summary>
    public event Action<ImportReviewEditorClose>? Closed;

    /// <summary>Starts a new edit (the next close is reported again).</summary>
    public void Begin(string text)
    {
        _closing = false;
        Text = text;
        CaretIndex = text.Length;
        SelectionStart = text.Length;
        SelectionEnd = text.Length;
    }

    /// <summary>Reports a close once per edit.</summary>
    public void Close(ImportReviewEditorClose how)
    {
        if (_closing)
        {
            return;
        }
        _closing = true;
        Closed?.Invoke(how);
    }

    protected override Type StyleKeyOverride => typeof(ImportWizardReviewEditor);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Close(ImportReviewEditorClose.Enter);
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(ImportReviewEditorClose.Escape);
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Close(ImportReviewEditorClose.Blur);
    }
}
