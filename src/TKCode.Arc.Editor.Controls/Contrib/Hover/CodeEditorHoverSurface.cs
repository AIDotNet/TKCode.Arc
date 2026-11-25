// The card of the editor's floating widgets: the content hover, parameter hints and the problem widget (upstream's
// .monaco-hover / .editor-widget CSS: editorHoverWidget.background, .border, .foreground, textLink.foreground,
// textCodeBlock.background, editorHoverWidget.highlightForeground, the problem colours). Its brushes and fonts come
// from Arc tokens in Themes/CodeEditorHoverSurfaceTheme.axaml; what the widgets draw inside it (Markdown, problem
// rows, signatures) binds to them, so a new theme changes the widgets.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The themed card the editor's floating widgets (hover, parameter hints, problems) are drawn on.</summary>
internal sealed class CodeEditorHoverSurface : ContentControl
{
    /// <summary>Defines the <see cref="Shadow"/> property.</summary>
    public static readonly StyledProperty<BoxShadows> ShadowProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, BoxShadows>(nameof(Shadow));

    /// <summary>Defines the <see cref="MutedForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(MutedForeground));

    /// <summary>Defines the <see cref="LinkForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> LinkForegroundProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(LinkForeground));

    /// <summary>Defines the <see cref="HighlightForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HighlightForegroundProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(HighlightForeground));

    /// <summary>Defines the <see cref="CodeBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CodeBackgroundProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(CodeBackground));

    /// <summary>Defines the <see cref="SeparatorBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SeparatorBrushProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(SeparatorBrush));

    /// <summary>Defines the <see cref="StatusBarBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> StatusBarBackgroundProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(StatusBarBackground));

    /// <summary>Defines the <see cref="ErrorForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ErrorForegroundProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(ErrorForeground));

    /// <summary>Defines the <see cref="WarningForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> WarningForegroundProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(WarningForeground));

    /// <summary>Defines the <see cref="InfoForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> InfoForegroundProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, IBrush?>(nameof(InfoForeground));

    /// <summary>Defines the <see cref="CodeFontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, FontFamily>(nameof(CodeFontFamily), FontFamily.Default);

    /// <summary>Defines the <see cref="RowPadding"/> property.</summary>
    public static readonly StyledProperty<Thickness> RowPaddingProperty =
        AvaloniaProperty.Register<CodeEditorHoverSurface, Thickness>(nameof(RowPadding));

    /// <summary>The card's shadow.</summary>
    public BoxShadows Shadow { get => GetValue(ShadowProperty); set => SetValue(ShadowProperty, value); }

    /// <summary>Secondary text: a problem's source and code, the signature counter.</summary>
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }

    /// <summary>Links.</summary>
    public IBrush? LinkForeground { get => GetValue(LinkForegroundProperty); set => SetValue(LinkForegroundProperty, value); }

    /// <summary>The active parameter of a signature.</summary>
    public IBrush? HighlightForeground { get => GetValue(HighlightForegroundProperty); set => SetValue(HighlightForegroundProperty, value); }

    /// <summary>The background of code spans and code blocks.</summary>
    public IBrush? CodeBackground { get => GetValue(CodeBackgroundProperty); set => SetValue(CodeBackgroundProperty, value); }

    /// <summary>The lines between rows.</summary>
    public IBrush? SeparatorBrush { get => GetValue(SeparatorBrushProperty); set => SetValue(SeparatorBrushProperty, value); }

    /// <summary>The background of the action row at the bottom of a hover.</summary>
    public IBrush? StatusBarBackground { get => GetValue(StatusBarBackgroundProperty); set => SetValue(StatusBarBackgroundProperty, value); }

    /// <summary>Errors.</summary>
    public IBrush? ErrorForeground { get => GetValue(ErrorForegroundProperty); set => SetValue(ErrorForegroundProperty, value); }

    /// <summary>Warnings.</summary>
    public IBrush? WarningForeground { get => GetValue(WarningForegroundProperty); set => SetValue(WarningForegroundProperty, value); }

    /// <summary>Information and hints.</summary>
    public IBrush? InfoForeground { get => GetValue(InfoForegroundProperty); set => SetValue(InfoForegroundProperty, value); }

    /// <summary>The font of code spans and signatures.</summary>
    public FontFamily CodeFontFamily { get => GetValue(CodeFontFamilyProperty); set => SetValue(CodeFontFamilyProperty, value); }

    /// <summary>The padding of a row.</summary>
    public Thickness RowPadding { get => GetValue(RowPaddingProperty); set => SetValue(RowPaddingProperty, value); }

    /// <summary>Binds a property of <paramref name="target"/> to a property of this surface (theme changes follow).</summary>
    internal void BindTo<T>(AvaloniaObject target, AvaloniaProperty<T> targetProperty, StyledProperty<T> sourceProperty) =>
        target.Bind(targetProperty, this.GetObservable(sourceProperty));
}
