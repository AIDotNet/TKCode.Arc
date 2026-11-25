// Ported from vscode (src/vs/editor/contrib/hover/browser/contentHoverRendered.ts: RenderedContentHover and
// RenderedContentHoverParts; the rendering halves of markdownHoverParticipant.ts (renderMarkdownHovers, the verbosity
// actions) and markerHoverParticipant.ts (_renderMarkerHover, _renderMarkerStatusbar); contentHoverStatusBar.ts;
// commit b57d5c9e), MIT license; the DOM is a behavior reference only.
// Differences from upstream:
//  - the parts are Avalonia controls in a vertical panel (one row per part, a separator between rows); the status bar
//    is a row of link buttons under the scrolling content;
//  - a marker row is the severity glyph, the message with source and code after it, and the related information;
//    the quick fix action of the status bar and the menu-contributed actions are not ported, "View Problem" is;
//  - the copy button (hoverCopyButton.ts) and the hover part focus navigation are not ported;
//  - code links and related information open through the controller's link handler; related information in another
//    resource only opens when the application handles the link.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Languages;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc;

/// <summary>What a rendered hover can ask of its widget (upstream's IEditorHoverContext).</summary>
internal sealed record HoverRenderContext(
    Action Hide,
    Action OnContentsChanged,
    Action Focus,
    Func<string, bool, bool> OpenLink,
    Action<Marker>? ShowMarker);

/// <summary>A part as it was rendered: the part, its participant and its row.</summary>
internal sealed record RenderedHoverPart(IEditorHoverParticipant Participant, IHoverPart HoverPart, Control Row);

/// <summary>The controls of a content hover result (upstream's RenderedContentHover).</summary>
internal sealed class RenderedContentHover : IDisposable
{
    private readonly CodeEditor _editor;
    private readonly CodeEditorHoverSurface _surface;
    private readonly HoverRenderContext _context;
    private readonly HoverMarkdownRenderer _markdown;
    private readonly StackPanel _rows = new() { Orientation = Orientation.Vertical };
    private readonly List<RenderedHoverPart> _renderedParts = [];
    private MarkdownHoverParticipant? _markdownParticipant;

    public RenderedContentHover(
        CodeEditor editor,
        ContentHoverResult hoverResult,
        IReadOnlyList<IEditorHoverParticipant> participants,
        CodeEditorHoverSurface surface,
        HoverRenderContext context)
    {
        _editor = editor;
        _surface = surface;
        _context = context;
        _markdown = new HoverMarkdownRenderer(surface, editor, context.OpenLink);

        var anchor = hoverResult.Options.Anchor;
        var parts = hoverResult.HoverParts;
        var positions = HoverUtils.ComputeHoverPositions(new CodeEditorHoverEditor(editor, () => HoverOptions.Default), anchor.Range, parts);
        ShowAtPosition = positions.ShowAtPosition;
        ShowAtSecondaryPosition = positions.ShowAtSecondaryPosition;
        ShouldFocus = hoverResult.Options.ShouldFocus;
        Source = hoverResult.Options.Source;
        InitialMousePosX = anchor.InitialMousePosX;
        InitialMousePosY = anchor.InitialMousePosY;
        IsBeforeContent = parts.Any(p => p.IsBeforeContent);

        foreach (var participant in participants.OrderBy(p => p.HoverOrdinal))
        {
            var own = parts.Where(p => ReferenceEquals(p.Owner, participant)).ToList();
            if (own.Count == 0)
            {
                continue;
            }

            switch (participant)
            {
                case MarkerHoverParticipant:
                    RenderMarkers(participant, own.OfType<MarkerHover>().ToList());
                    break;
                case MarkdownHoverParticipant markdown:
                    _markdownParticipant = markdown;
                    foreach (var part in own.OfType<MarkdownHover>().OrderBy(p => p.Ordinal))
                    {
                        AddRow(participant, part, RenderMarkdownRow(markdown, part));
                    }

                    break;
            }
        }

        Content = _rows;
    }

    /// <summary>Where the hover shows.</summary>
    public TextPosition ShowAtPosition { get; }

    /// <summary>A second position on the same line the hover should touch (the start of the parts' range).</summary>
    public TextPosition ShowAtSecondaryPosition { get; }

    public bool ShouldFocus { get; }

    public HoverStartSource Source { get; }

    public double? InitialMousePosX { get; }

    public double? InitialMousePosY { get; }

    public bool IsBeforeContent { get; }

    /// <summary>The rows.</summary>
    public Control Content { get; }

    /// <summary>The action row at the bottom, or null.</summary>
    public Control? StatusBar { get; private set; }

    /// <summary>The rendered parts in display order.</summary>
    public IReadOnlyList<RenderedHoverPart> RenderedParts => _renderedParts;

    /// <summary>Upstream's domNodeHasChildren.</summary>
    public bool HasContent => _renderedParts.Count > 0;

    /// <summary>The text of every part for a screen reader (upstream's getAccessibleWidgetContent).</summary>
    public string GetAccessibleWidgetContent() =>
        string.Join("\n\n", _renderedParts.Select(p => p.Participant.GetAccessibleContent(p.HoverPart)).Where(s => s.Length > 0));

    /// <summary>The text of one part (upstream's getAccessibleWidgetContentAtIndex).</summary>
    public string? GetAccessibleWidgetContentAtIndex(int index) =>
        index >= 0 && index < _renderedParts.Count ? _renderedParts[index].Participant.GetAccessibleContent(_renderedParts[index].HoverPart) : null;

    /// <summary>Whether the Markdown part at <paramref name="index"/> can be made more or less verbose.</summary>
    public bool DoesHoverAtIndexSupportVerbosityAction(int index, HoverVerbosityAction action) =>
        index >= 0 && index < _renderedParts.Count
        && _renderedParts[index].HoverPart is MarkdownHover { Source: { } source } && source.SupportsVerbosityAction(action);

    /// <summary>Asks the provider of the Markdown part at <paramref name="index"/> for a more or less verbose hover and shows it.</summary>
    public async Task UpdateHoverVerbosityLevel(HoverVerbosityAction action, int index, bool focus = false)
    {
        if (_markdownParticipant is not { } participant || index < 0 || index >= _renderedParts.Count
            || _renderedParts[index].HoverPart is not MarkdownHover part)
        {
            return;
        }

        var updated = await participant.UpdateMarkdownHoverVerbosityLevel(part, action).ConfigureAwait(true);
        if (updated is null || _renderedParts.Count <= index || !ReferenceEquals(_renderedParts[index].HoverPart, part))
        {
            return;
        }

        var row = RenderMarkdownRow(participant, updated);
        var old = _renderedParts[index];
        var rowIndex = _rows.Children.IndexOf(old.Row);
        row.Margin = old.Row.Margin;
        if (old.Row is Border { BorderThickness.Top: > 0 } oldBorder && row is Border newBorder)
        {
            newBorder.BorderThickness = oldBorder.BorderThickness;
            _surface.BindTo(newBorder, Border.BorderBrushProperty, CodeEditorHoverSurface.SeparatorBrushProperty);
        }

        _rows.Children[rowIndex] = row;
        _renderedParts[index] = new RenderedHoverPart(participant, updated, row);
        _context.OnContentsChanged();
        if (focus)
        {
            _context.Focus();
        }
    }

    public void Dispose() => _markdownParticipant?.CancelVerbosityRequests();

    private void AddRow(IEditorHoverParticipant participant, IHoverPart part, Control content)
    {
        // upstream's .hover-row: the rows after the first have a separator above
        var row = new Border { Child = content };
        _surface.BindTo(row, Decorator.PaddingProperty, CodeEditorHoverSurface.RowPaddingProperty);
        if (_rows.Children.Count > 0)
        {
            row.BorderThickness = new Thickness(0, 1, 0, 0);
            _surface.BindTo(row, Border.BorderBrushProperty, CodeEditorHoverSurface.SeparatorBrushProperty);
        }

        _rows.Children.Add(row);
        _renderedParts.Add(new RenderedHoverPart(participant, part, row));
    }

    // ---- Markdown (markdownHoverParticipant.ts)

    private Control RenderMarkdownRow(MarkdownHoverParticipant participant, MarkdownHover part)
    {
        var contents = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        foreach (var markdown in part.Contents)
        {
            if (!HoverMarkdown.IsEmptyMarkdownString(markdown))
            {
                contents.Children.Add(_markdown.Render(markdown));
            }
        }

        if (part.Source is not { } source
            || !(source.SupportsVerbosityAction(HoverVerbosityAction.Increase) || source.SupportsVerbosityAction(HoverVerbosityAction.Decrease)))
        {
            return contents;
        }

        // upstream's verbosity actions: + and - on the left, at the bottom of the row
        var actions = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 6, 0) };
        actions.Children.Add(CreateVerbosityButton("+", "Increase Hover Verbosity", source.SupportsVerbosityAction(HoverVerbosityAction.Increase), () => RunVerbosity(part, HoverVerbosityAction.Increase)));
        actions.Children.Add(CreateVerbosityButton("−", "Decrease Hover Verbosity", source.SupportsVerbosityAction(HoverVerbosityAction.Decrease), () => RunVerbosity(part, HoverVerbosityAction.Decrease)));
        var dock = new DockPanel();
        DockPanel.SetDock(actions, Dock.Left);
        dock.Children.Add(actions);
        dock.Children.Add(contents);
        return dock;
    }

    private void RunVerbosity(MarkdownHover part, HoverVerbosityAction action)
    {
        var index = _renderedParts.FindIndex(p => ReferenceEquals(p.HoverPart, part));
        if (index >= 0)
        {
            _ = UpdateHoverVerbosityLevel(action, index, focus: false);
        }
    }

    private TextBlock CreateVerbosityButton(string glyph, string label, bool enabled, Action run)
    {
        var button = new TextBlock
        {
            Text = glyph,
            FontWeight = FontWeight.Bold,
            Opacity = enabled ? 1 : 0.4,
            Cursor = enabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            Padding = new Thickness(2, 0),
        };
        ToolTip.SetTip(button, label);
        _surface.BindTo(button, TextBlock.ForegroundProperty, enabled ? CodeEditorHoverSurface.LinkForegroundProperty : CodeEditorHoverSurface.MutedForegroundProperty);
        if (enabled)
        {
            button.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                run();
            };
        }

        return button;
    }

    // ---- markers (markerHoverParticipant.ts)

    private void RenderMarkers(IEditorHoverParticipant participant, IReadOnlyList<MarkerHover> parts)
    {
        if (parts.Count == 0)
        {
            return;
        }

        var sorted = MarkerHoverParticipant.SortForRendering(parts);
        foreach (var part in sorted)
        {
            AddRow(participant, part, RenderMarkerRow(part.Marker));
        }

        RenderMarkerStatusBar(sorted[0]);
    }

    /// <summary>A problem: its severity glyph, message, source and code, related information.</summary>
    internal Control RenderMarkerRow(Marker marker) =>
        MarkerMessageRenderer.Render(marker, _surface, target => _context.OpenLink(target, true), OpenRelated, withIcon: true);

    // related information in this editor's document reveals the position; elsewhere the link handler decides
    private bool OpenRelated(RelatedInformation related)
    {
        if (related.Resource.ToString() == _editor.Model.Uri.ToString())
        {
            _context.Hide();
            var position = new TextPosition(related.StartLineNumber, related.StartColumn);
            _editor.SetPosition(position, "api");
            _editor.RevealRange(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), TKCode.Arc.Editor.ViewModel.VerticalRevealType.CenterIfOutsideViewport);
            _editor.Focus();
            return true;
        }

        return _context.OpenLink(related.Resource.ToString(), true);
    }

    private void RenderMarkerStatusBar(MarkerHover markerHover)
    {
        var severity = markerHover.Marker.Severity;
        if (_context.ShowMarker is not { } showMarker
            || severity is not (MarkerSeverity.Error or MarkerSeverity.Warning or MarkerSeverity.Info))
        {
            return;
        }

        var statusBar = new Border();
        _surface.BindTo(statusBar, Border.BackgroundProperty, CodeEditorHoverSurface.StatusBarBackgroundProperty);
        _surface.BindTo(statusBar, Border.BorderBrushProperty, CodeEditorHoverSurface.SeparatorBrushProperty);
        _surface.BindTo(statusBar, Decorator.PaddingProperty, CodeEditorHoverSurface.RowPaddingProperty);
        statusBar.BorderThickness = new Thickness(0, 1, 0, 0);

        var action = new TextBlock
        {
            Text = "View Problem",
            Cursor = new Cursor(StandardCursorType.Hand),
            Name = "PART_ViewProblem",
        };
        _surface.BindTo(action, TextBlock.ForegroundProperty, CodeEditorHoverSurface.LinkForegroundProperty);
        ToolTip.SetTip(action, $"View Problem ({ViewProblemKeybindingLabel})");
        action.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _context.Hide();
            showMarker(markerHover.Marker);
            _editor.Focus();
        };

        var keybinding = new TextBlock { Text = $" ({ViewProblemKeybindingLabel})" };
        _surface.BindTo(keybinding, TextBlock.ForegroundProperty, CodeEditorHoverSurface.MutedForegroundProperty);
        statusBar.Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { action, keybinding } };
        StatusBar = statusBar;
    }

    /// <summary>The label of upstream's NextMarkerAction keybinding (Alt+F8).</summary>
    internal static string ViewProblemKeybindingLabel => OperatingSystem.IsMacOS() ? "⌥F8" : "Alt+F8";
}

/// <summary>The severity glyphs of problems (upstream's codicons error, warning, info) as paths in a 16 x 16 box.</summary>
internal static class CodeEditorProblemIcons
{
    private static readonly Geometry s_error = Geometry.Parse("M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M5.5,5.5 L10.5,10.5 M10.5,5.5 L5.5,10.5");
    private static readonly Geometry s_warning = Geometry.Parse("M8,1.8 L14.6,13.8 L1.4,13.8 Z M8,6 L8,9.6 M8,11.4 L8,11.9");
    private static readonly Geometry s_info = Geometry.Parse("M8,1.5 A6.5,6.5 0 1 1 7.99,1.5 Z M8,7.2 L8,11.4 M8,4.6 L8,5.1");

    /// <summary>The glyph of a severity, stroked in the surface's colour for it.</summary>
    public static Control Create(MarkerSeverity severity, CodeEditorHoverSurface surface)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = severity switch
            {
                MarkerSeverity.Error => s_error,
                MarkerSeverity.Warning => s_warning,
                _ => s_info,
            },
            StrokeThickness = 1.4,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Width = 16,
            Height = 16,
            Stretch = Stretch.None,
            Name = "PART_Severity",
            Opacity = severity == MarkerSeverity.Hint ? 0.6 : 1,
        };
        var brush = severity switch
        {
            MarkerSeverity.Error => CodeEditorHoverSurface.ErrorForegroundProperty,
            MarkerSeverity.Warning => CodeEditorHoverSurface.WarningForegroundProperty,
            MarkerSeverity.Info => CodeEditorHoverSurface.InfoForegroundProperty,
            _ => CodeEditorHoverSurface.MutedForegroundProperty,
        };
        surface.BindTo(path, Shape.StrokeProperty, brush);
        return path;
    }
}
