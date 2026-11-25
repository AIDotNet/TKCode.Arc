// Ported from vscode (src/vs/editor/contrib/gotoError/browser/gotoErrorWidget.ts: MarkerNavigationWidget and
// MessageWidget, commit b57d5c9e), MIT license; the DOM and the peek view's zone widget are a behavior reference only.
// Differences from upstream:
//  - the widget is a view zone after the marker's line whose control is a CodeEditorHoverSurface: a header (severity
//    glyph, file name, "N of M problems", next / previous / close) above the problem (MarkerMessageRenderer), framed
//    above and below in the severity's colour (upstream's editorMarkerNavigation*.background);
//  - the height is upstream's computeRequiredHeight (3 + the message's lines, at most 17) in editor lines;
//  - no arrow pointing at the column, no title menu (the actions are the three buttons), no stale styling beyond
//    fading the message.

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.Contrib.Hover;
using TKCode.Arc.Editor.Platform.Markers;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

/// <summary>The problem widget of next / previous problem (upstream's MarkerNavigationWidget).</summary>
internal sealed class MarkerNavigationWidget : IDisposable
{
    private const int MaxMessageLines = 17;

    private readonly CodeEditor _editor;
    private readonly ScrollViewer _body;
    private readonly TextBlock _title;
    private readonly TextBlock _detail;
    private readonly ContentControl _icon;
    private readonly Border _frame;
    private string? _zoneId;
    private Marker? _marker;
    private bool _disposed;

    public MarkerNavigationWidget(CodeEditor editor)
    {
        _editor = editor;
        Surface = new CodeEditorHoverSurface { Classes = { "marker-navigation" } };

        _icon = new ContentControl { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        _title = new TextBlock { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Name = "PART_Title" };
        _detail = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Name = "PART_Detail" };
        Surface.BindTo(_detail, TextBlock.ForegroundProperty, CodeEditorHoverSurface.MutedForegroundProperty);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        actions.Children.Add(CreateAction("↓", "Next Problem (Error, Warning, Info)", () => NavigateRequested?.Invoke(true), "PART_Next"));
        actions.Children.Add(CreateAction("↑", "Previous Problem (Error, Warning, Info)", () => NavigateRequested?.Invoke(false), "PART_Previous"));
        actions.Children.Add(CreateAction("×", "Close", () => CloseRequested?.Invoke(), "PART_Close"));

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        DockPanel.SetDock(actions, Dock.Right);
        header.Children.Add(actions);
        var title = new StackPanel { Orientation = Orientation.Horizontal, Children = { _icon, _title, _detail } };
        header.Children.Add(title);

        _body = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(_body);

        _frame = new Border { BorderThickness = new Thickness(0, 2), Child = layout };
        Surface.BindTo(_frame, Decorator.PaddingProperty, CodeEditorHoverSurface.RowPaddingProperty);
        Surface.Content = _frame;
        Surface.KeyDown += OnKeyDown;
    }

    /// <summary>The control in the view zone.</summary>
    public CodeEditorHoverSurface Surface { get; }

    /// <summary>The marker shown, or null.</summary>
    public Marker? Marker => _marker;

    /// <summary>Where the widget shows (the position it was opened at), or null.</summary>
    public TextPosition? Position { get; private set; }

    /// <summary>The id of the view zone, or null when hidden.</summary>
    public string? ZoneId => _zoneId;

    /// <summary>The title ("file.ts") and detail ("1 of 3 problems") shown (for tests).</summary>
    public (string? Title, string? Detail) Heading => (_title.Text, _detail.Text);

    /// <summary>Next (true) or previous (false) was pressed.</summary>
    public event Action<bool>? NavigateRequested;

    /// <summary>Close was pressed or Escape typed in the widget (upstream's onDidClose).</summary>
    public event Action? CloseRequested;

    /// <summary>Shows a marker (upstream's showAtMarker): the zone after its line, the editor revealing it.</summary>
    public void ShowAtMarker(Marker marker, int markerIdx, int markerCount)
    {
        _marker = marker;
        UpdateMessage(marker, stale: false);
        ApplySeverity(marker.Severity);

        var range = marker.Range;
        var editorPosition = _editor.GetPosition();
        var position = editorPosition is { } p && range.ContainsPosition(p) ? p : range.Start;
        Position = position;

        _title.Text = MarkerHoverParticipant.Basename(_editor.Model.Uri);
        _detail.Text = markerCount > 1
            ? string.Create(CultureInfo.InvariantCulture, $"{markerIdx} of {markerCount} problems")
            : string.Create(CultureInfo.InvariantCulture, $"{markerIdx} of {markerCount} problem");

        var heightInLines = ComputeRequiredHeight(marker);
        _editor.ChangeViewZones(accessor =>
        {
            if (_zoneId is { } old)
            {
                accessor.RemoveZone(old);
            }

            _zoneId = accessor.AddZone(new CodeEditorViewZone
            {
                AfterLineNumber = position.LineNumber,
                HeightInLines = heightInLines,
                Control = Surface,
                SuppressMouseDown = true,
            });
        });

        _editor.RevealRange(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), VerticalRevealType.CenterIfOutsideViewport);
        RevealZone(position.LineNumber, heightInLines);
        _editor.Focus();
    }

    // upstream's ZoneWidget reveals the zone with its line: scroll so that the line and the whole zone are visible
    private void RevealZone(int afterLineNumber, int heightInLines)
    {
        if (_editor.ViewModel is not { } viewModel || _editor.View is not { } view)
        {
            return;
        }

        var lineHeight = view.LineHeight;
        var lineTop = _editor.GetTopForLineNumber(afterLineNumber);
        var zoneBottom = lineTop + lineHeight + (heightInLines * lineHeight);
        var viewportHeight = _editor.GetLayoutSize().Height;
        var scrollTop = viewModel.ViewLayout.GetCurrentScrollTop();
        var newScrollTop = scrollTop;
        if (zoneBottom > scrollTop + viewportHeight)
        {
            newScrollTop = zoneBottom - viewportHeight;
        }

        newScrollTop = Math.Min(newScrollTop, lineTop);
        if (Math.Abs(newScrollTop - scrollTop) > 0.5)
        {
            viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(null, newScrollTop), ScrollType.Smooth);
        }
    }

    /// <summary>The marker changed but is still there (upstream's updateMarker).</summary>
    public void UpdateMarker(Marker marker)
    {
        _marker = marker;
        UpdateMessage(marker, stale: false);
    }

    /// <summary>The marker went away (upstream's showStale): the message fades.</summary>
    public void ShowStale()
    {
        if (_body.Content is Control content)
        {
            content.Opacity = 0.5;
        }
    }

    // upstream's computeRequiredHeight: three lines for the header and frame, then the message's lines
    private static int ComputeRequiredHeight(Marker marker)
    {
        var lines = marker.Message.Split('\n').Length;
        if (marker.RelatedInformation is { Count: > 0 } related)
        {
            lines += related.Count;
        }

        return 3 + Math.Min(MaxMessageLines, lines);
    }

    private void UpdateMessage(Marker marker, bool stale)
    {
        var content = MarkerMessageRenderer.Render(marker, Surface, _ => false, OpenRelated, withIcon: false);
        content.Opacity = stale ? 0.5 : 1;
        _body.Content = content;
    }

    private bool OpenRelated(RelatedInformation related)
    {
        if (related.Resource.ToString() != _editor.Model.Uri.ToString())
        {
            return false;
        }

        var position = new TextPosition(related.StartLineNumber, related.StartColumn);
        CloseRequested?.Invoke();
        _editor.SetPosition(position);
        _editor.RevealRange(new TextRange(position.LineNumber, position.Column, position.LineNumber, position.Column), VerticalRevealType.CenterIfOutsideViewport);
        return true;
    }

    private void ApplySeverity(MarkerSeverity severity)
    {
        var colour = severity switch
        {
            MarkerSeverity.Error => CodeEditorHoverSurface.ErrorForegroundProperty,
            MarkerSeverity.Warning => CodeEditorHoverSurface.WarningForegroundProperty,
            _ => CodeEditorHoverSurface.InfoForegroundProperty,
        };
        Surface.BindTo(_frame, Border.BorderBrushProperty, colour);
        _icon.Content = CodeEditorProblemIcons.Create(severity, Surface);
    }

    private TextBlock CreateAction(string glyph, string label, Action run, string name)
    {
        var action = new TextBlock
        {
            Text = glyph,
            Width = 20,
            TextAlignment = TextAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Name = name,
        };
        ToolTip.SetTip(action, label);
        Avalonia.Automation.AutomationProperties.SetName(action, label);
        action.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            run();
        };
        return action;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseRequested?.Invoke();
        }
    }

    /// <summary>Removes the zone.</summary>
    public void Hide()
    {
        if (_zoneId is { } id)
        {
            _zoneId = null;
            _editor.ChangeViewZones(accessor => accessor.RemoveZone(id));
        }

        Position = null;
        _marker = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Hide();
        Surface.KeyDown -= OnKeyDown;
        NavigateRequested = null;
        CloseRequested = null;
    }
}
