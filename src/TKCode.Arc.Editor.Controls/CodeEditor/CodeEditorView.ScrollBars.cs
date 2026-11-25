using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using TKCode.Arc.Editor;
using TKCode.Arc.Editor.ViewModel;

namespace TKCode.Arc;

// The scroll bars (upstream's EditorScrollbar over the view's Scrollable): two Avalonia ScrollBars from the editor's
// template, kept in step with the view layout's scroll state, and scrolling it when the user drags or pages them.
// They overlay the text, as Arc's ScrollArea does, and hide when there is nothing to scroll in their direction.
internal sealed partial class CodeEditorView
{
    private ScrollBar? _verticalScrollBar;
    private ScrollBar? _horizontalScrollBar;
    private bool _updatingScrollBars;

    /// <summary>Connects the template's scroll bars (either may be null).</summary>
    internal void AttachScrollBars(ScrollBar? vertical, ScrollBar? horizontal)
    {
        DetachScrollBars();
        _verticalScrollBar = vertical;
        _horizontalScrollBar = horizontal;
        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.ValueChanged += OnVerticalScroll;
        }

        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.ValueChanged += OnHorizontalScroll;
        }

        UpdateScrollBars();
    }

    internal void DetachScrollBars()
    {
        if (_verticalScrollBar is not null)
        {
            _verticalScrollBar.ValueChanged -= OnVerticalScroll;
        }

        if (_horizontalScrollBar is not null)
        {
            _horizontalScrollBar.ValueChanged -= OnHorizontalScroll;
        }

        _verticalScrollBar = null;
        _horizontalScrollBar = null;
    }

    /// <summary>Copies the scroll state of the layout into the bars.</summary>
    internal void UpdateScrollBars()
    {
        if (_viewModel is null)
        {
            return;
        }

        var layout = _viewModel.ViewLayout;
        var wasVisible = (_verticalScrollBar?.IsVisible ?? false, _horizontalScrollBar?.IsVisible ?? false);
        _updatingScrollBars = true;
        try
        {
            if (_verticalScrollBar is { } vertical)
            {
                var height = Math.Max(0, Bounds.Height);
                vertical.ViewportSize = height;
                vertical.Minimum = 0;
                vertical.Maximum = Math.Max(0, layout.GetScrollHeight() - height);
                vertical.Value = layout.GetCurrentScrollTop();
                vertical.SmallChange = _lineHeight;
                vertical.LargeChange = Math.Max(_lineHeight, height - _lineHeight);
            }

            if (_horizontalScrollBar is { } horizontal)
            {
                var width = Math.Max(0, TextAreaRight - GutterWidth);
                horizontal.ViewportSize = width;
                // under the text only, not under the line numbers (upstream's horizontal bar spans the content area)
                horizontal.Margin = new Thickness(GutterWidth, 0, Math.Max(0, Bounds.Width - TextAreaRight), 0);
                horizontal.Minimum = 0;
                horizontal.Maximum = Math.Max(0, layout.GetScrollWidth() - width);
                horizontal.Value = layout.GetCurrentScrollLeft();
                horizontal.SmallChange = _charWidth;
                horizontal.LargeChange = Math.Max(_charWidth, width - _charWidth);
            }
        }
        finally
        {
            _updatingScrollBars = false;
        }

        // A bar that Visibility="Auto" shows after it was measured hidden keeps a desired size of 0 until it is measured
        // again. The width of the lines is learnt while the text layer draws, and a measure asked for during the draw is
        // lost, so it is asked for once the frame is done.
        if (wasVisible != (_verticalScrollBar?.IsVisible ?? false, _horizontalScrollBar?.IsVisible ?? false))
        {
            var vertical = _verticalScrollBar;
            var horizontal = _horizontalScrollBar;
            Dispatcher.UIThread.Post(() =>
            {
                vertical?.InvalidateMeasure();
                horizontal?.InvalidateMeasure();
            });
        }
    }

    private void OnVerticalScroll(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingScrollBars && _viewModel is not null)
        {
            _viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollTop: e.NewValue), ScrollType.Immediate);
        }
    }

    private void OnHorizontalScroll(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_updatingScrollBars && _viewModel is not null)
        {
            _viewModel.ViewLayout.SetScrollPosition(new NewScrollPosition(ScrollLeft: e.NewValue), ScrollType.Immediate);
        }
    }
}
