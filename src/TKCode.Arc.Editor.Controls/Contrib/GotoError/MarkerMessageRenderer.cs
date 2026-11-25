// Ported from vscode (src/vs/editor/contrib/hover/browser/markerHoverParticipant.ts: _renderMarkerHover, and the message
// part of src/vs/editor/contrib/gotoError/browser/gotoErrorWidget.ts: MessageWidget.update, commit b57d5c9e), MIT
// license; the DOM is a behavior reference only. A problem as the hover and the problem widget show it: the severity
// glyph, the message (line breaks kept), the source and code after it (faded; a code with a target is a link), and the
// related information as links "file(line, column): message".

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Platform.Markers;

namespace TKCode.Arc;

/// <summary>Renders a problem (a marker) on a <see cref="CodeEditorHoverSurface"/>.</summary>
internal static class MarkerMessageRenderer
{
    /// <summary>A problem: its severity glyph (when <paramref name="withIcon"/>), message, source and code, related information.</summary>
    public static Control Render(Marker marker, CodeEditorHoverSurface surface, Func<string, bool> openLink, Func<RelatedInformation, bool> openRelated, bool withIcon)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var icon = CodeEditorProblemIcons.Create(marker.Severity, surface);
        icon.Margin = new Thickness(0, 2, 6, 0);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.IsVisible = withIcon;
        grid.Children.Add(icon);

        var message = new StackPanel { Orientation = Orientation.Vertical };
        Grid.SetColumn(message, 1);
        grid.Children.Add(message);

        var text = new HoverTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            OpenLink = target => openLink(target),
            Name = "PART_MarkerMessage",
        };
        text.Inlines!.Add(new Run(marker.Message));
        if (marker.Source is not null || marker.Code is not null)
        {
            // upstream: the source and code after the message, faded; a code with a target is a link
            var offset = marker.Message.Length;
            if (marker.Code is { } code && marker.CodeTarget is { } target)
            {
                if (marker.Source is { } source)
                {
                    var sourceRun = new Run("  " + source);
                    surface.BindTo(sourceRun, TextElement.ForegroundProperty, CodeEditorHoverSurface.MutedForegroundProperty);
                    text.Inlines.Add(sourceRun);
                    offset += sourceRun.Text!.Length;
                }

                var link = new Run((marker.Source is null ? "  " : string.Empty) + "(" + code + ")") { TextDecorations = TextDecorations.Underline };
                surface.BindTo(link, TextElement.ForegroundProperty, CodeEditorHoverSurface.LinkForegroundProperty);
                text.AddLink(offset, link.Text!.Length, target.ToString());
                text.Inlines.Add(link);
            }
            else
            {
                var source = marker.Source;
                var detail = source is not null && marker.Code is not null ? $"{source}({marker.Code})" : source ?? $"({marker.Code})";
                var detailRun = new Run("  " + detail);
                surface.BindTo(detailRun, TextElement.ForegroundProperty, CodeEditorHoverSurface.MutedForegroundProperty);
                text.Inlines.Add(detailRun);
            }
        }

        message.Children.Add(text);

        foreach (var related in marker.RelatedInformation ?? [])
        {
            var relatedText = new HoverTextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
                OpenLink = _ => openRelated(related),
            };
            var label = string.Create(CultureInfo.InvariantCulture, $"{MarkerHoverParticipant.Basename(related.Resource)}({related.StartLineNumber}, {related.StartColumn}): ");
            var linkRun = new Run(label) { TextDecorations = TextDecorations.Underline };
            surface.BindTo(linkRun, TextElement.ForegroundProperty, CodeEditorHoverSurface.LinkForegroundProperty);
            relatedText.AddLink(0, label.Length, related.Resource.ToString());
            relatedText.Inlines!.Add(linkRun);
            relatedText.Inlines.Add(new Run(related.Message));
            message.Children.Add(relatedText);
        }

        Avalonia.Automation.AutomationProperties.SetName(icon, MarkerHoverParticipant.GetSeverityLabel(marker.Severity));
        return grid;
    }
}
