// The surfaces of the editor's widgets (upstream: the editorWidget.background / editorWidget.border / widget.shadow and
// peekView* theme colors that the widgets' CSS reads). Each is a Border whose control theme (Themes/*.axaml) sets its
// brushes from Arc tokens, so that the widgets built in code (the code action menu, the rename box, messages, peek
// views) follow light and dark without fixed colors: a widget binds its parts to the surface's brush properties.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace TKCode.Arc;

/// <summary>The floating surface of an editor widget (menu, rename box, message): background, border, shadow, text colors.</summary>
internal class CodeEditorWidgetSurface : Border
{
    /// <summary>Defines the <see cref="MutedForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MutedForegroundProperty =
        AvaloniaProperty.Register<CodeEditorWidgetSurface, IBrush?>(nameof(MutedForeground));

    /// <summary>Defines the <see cref="SelectionBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectionBackgroundProperty =
        AvaloniaProperty.Register<CodeEditorWidgetSurface, IBrush?>(nameof(SelectionBackground));

    /// <summary>Defines the <see cref="HoverBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HoverBackgroundProperty =
        AvaloniaProperty.Register<CodeEditorWidgetSurface, IBrush?>(nameof(HoverBackground));

    /// <summary>Defines the <see cref="AccentForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> AccentForegroundProperty =
        AvaloniaProperty.Register<CodeEditorWidgetSurface, IBrush?>(nameof(AccentForeground));

    /// <summary>Secondary text (group headers, keybinding labels, file paths).</summary>
    public IBrush? MutedForeground { get => GetValue(MutedForegroundProperty); set => SetValue(MutedForegroundProperty, value); }

    /// <summary>The background of the selected row.</summary>
    public IBrush? SelectionBackground { get => GetValue(SelectionBackgroundProperty); set => SetValue(SelectionBackgroundProperty, value); }

    /// <summary>The background of the row under the pointer.</summary>
    public IBrush? HoverBackground { get => GetValue(HoverBackgroundProperty); set => SetValue(HoverBackgroundProperty, value); }

    /// <summary>Icons and highlights in accent color.</summary>
    public IBrush? AccentForeground { get => GetValue(AccentForegroundProperty); set => SetValue(AccentForegroundProperty, value); }

    /// <summary>Binds <paramref name="target"/>'s <paramref name="property"/> to a brush property of this surface.</summary>
    public IDisposable BindBrush(AvaloniaObject target, AvaloniaProperty<IBrush?> property, StyledProperty<IBrush?> source) =>
        target.Bind(property, this.GetObservable(source), BindingPriority.Style);
}

/// <summary>The surface of a peek view (upstream's peekView* colors): frame, title, result list and preview colors.</summary>
internal sealed class CodeEditorPeekSurface : CodeEditorWidgetSurface
{
    /// <summary>Defines the <see cref="FrameBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> FrameBrushProperty =
        AvaloniaProperty.Register<CodeEditorPeekSurface, IBrush?>(nameof(FrameBrush));

    /// <summary>Defines the <see cref="HeaderBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
        AvaloniaProperty.Register<CodeEditorPeekSurface, IBrush?>(nameof(HeaderBackground));

    /// <summary>Defines the <see cref="ResultsBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ResultsBackgroundProperty =
        AvaloniaProperty.Register<CodeEditorPeekSurface, IBrush?>(nameof(ResultsBackground));

    /// <summary>Defines the <see cref="PreviewBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> PreviewBackgroundProperty =
        AvaloniaProperty.Register<CodeEditorPeekSurface, IBrush?>(nameof(PreviewBackground));

    /// <summary>Defines the <see cref="MatchHighlightBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> MatchHighlightBackgroundProperty =
        AvaloniaProperty.Register<CodeEditorPeekSurface, IBrush?>(nameof(MatchHighlightBackground));

    /// <summary>The frame lines and the arrow (upstream's peekView.border).</summary>
    public IBrush? FrameBrush { get => GetValue(FrameBrushProperty); set => SetValue(FrameBrushProperty, value); }

    /// <summary>The title area (upstream's peekViewTitle.background).</summary>
    public IBrush? HeaderBackground { get => GetValue(HeaderBackgroundProperty); set => SetValue(HeaderBackgroundProperty, value); }

    /// <summary>The result list (upstream's peekViewResult.background).</summary>
    public IBrush? ResultsBackground { get => GetValue(ResultsBackgroundProperty); set => SetValue(ResultsBackgroundProperty, value); }

    /// <summary>The preview editor (upstream's peekViewEditor.background).</summary>
    public IBrush? PreviewBackground { get => GetValue(PreviewBackgroundProperty); set => SetValue(PreviewBackgroundProperty, value); }

    /// <summary>The match in a result row (upstream's peekViewResult.matchHighlightBackground).</summary>
    public IBrush? MatchHighlightBackground { get => GetValue(MatchHighlightBackgroundProperty); set => SetValue(MatchHighlightBackgroundProperty, value); }
}
