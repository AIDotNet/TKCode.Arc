using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TKCode.Arc.Primitives;

namespace TKCode.Arc.Mobile;

public partial class Picker
{
    private void UpdateChecks()
    {
        if (_sheet is null)
        {
            return;
        }
        var index = SelectedIndex;
        for (var i = 0; i < _sheet.Actions.Count; i++)
        {
            _sheet.Actions[i].IsChecked = i == index;
        }
    }

    private ActionSheet EnsureSheet()
    {
        if (_sheet is null)
        {
            _sheet = new ActionSheet();
            _sheet.ActionInvoked += OnActionInvoked;
            _sheet.PropertyChanged += OnSheetPropertyChanged;
            ApplySheetText();
        }
        return _sheet;
    }

    private void ApplySheetText()
    {
        if (_sheet is null)
        {
            return;
        }
        _sheet.Title = SheetTitle ?? Content as string;
        _sheet.Message = SheetMessage;
        _sheet.CancelText = CancelText;
    }

    /// <summary>One sheet row per choice, the selected one checked.</summary>
    private void BuildRows()
    {
        if (_sheet is not { } sheet)
        {
            return;
        }
        sheet.Actions.Clear();
        var template = ItemTemplate;
        var choices = Choices;
        var selected = SelectedIndex;
        for (var i = 0; i < choices.Count; i++)
        {
            var row = new ActionSheetItem { IsChecked = i == selected };
            if (template is not null)
            {
                row.Content = choices[i];
                row.ContentTemplate = template;
            }
            else
            {
                row.Content = TextOf(choices[i]);
            }
            sheet.Actions.Add(row);
        }
    }

    private void Open()
    {
        if (!IsEffectivelyEnabled || Choices.Count == 0 || !this.IsAttachedToVisualTree())
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }
        var sheet = EnsureSheet();
        if (!Mount(sheet))
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }
        ApplySheetText();
        BuildRows();
        // A sheet whose template exists when it opens rises; one that only gets it later would appear in place.
        sheet.ApplyTemplate();
        sheet.IsOpen = true;
        if (_keyboardOpen)
        {
            Dispatcher.UIThread.Post(FocusCheckedRow, DispatcherPriority.Loaded);
        }
    }

    /// <summary>Opened from the keyboard: the current choice (or the first) takes the keyboard focus.</summary>
    private void FocusCheckedRow()
    {
        if (_sheet is not { IsOpen: true } sheet || sheet.Actions.Count == 0)
        {
            return;
        }
        var row = sheet.Actions.FirstOrDefault(a => a.IsChecked) ?? sheet.Actions[0];
        row.Focus(NavigationMethod.Tab);
    }

    /// <summary>
    /// Puts the sheet over the page the picker is in (above the page's overlay slot, with the page's safe area), or else on
    /// the window's overlay layer, sized to it.
    /// </summary>
    private bool Mount(ActionSheet sheet)
    {
        var page = this.FindAncestorOfType<Page>();
        if (page is not null && ReferenceEquals(page, _hostPage) && sheet.GetVisualParent() is not null)
        {
            return true;
        }
        Unmount();
        if (page is not null && page.HostOverlay(sheet))
        {
            _hostPage = page;
            return true;
        }
        if (ArcOverlayLayer.Find(this) is not { } layer)
        {
            return false;
        }
        _hostLayer = layer;
        layer.Children.Add(sheet);
        layer.SizeChanged += OnLayerSizeChanged;
        SizeToLayer();
        return true;
    }

    private void Unmount()
    {
        if (_sheet is null)
        {
            return;
        }
        _sheet.IsOpen = false;
        if (_hostPage is not null)
        {
            _hostPage.UnhostOverlay(_sheet);
            _hostPage = null;
        }
        if (_hostLayer is not null)
        {
            _hostLayer.SizeChanged -= OnLayerSizeChanged;
            _hostLayer.Children.Remove(_sheet);
            _hostLayer = null;
            _sheet.ClearValue(WidthProperty);
            _sheet.ClearValue(HeightProperty);
        }
    }

    private void OnLayerSizeChanged(object? sender, SizeChangedEventArgs e) => SizeToLayer();

    private void SizeToLayer()
    {
        if (_sheet is null || _hostLayer is null)
        {
            return;
        }
        var size = _hostLayer.Bounds.Size;
        if ((size.Width <= 0 || size.Height <= 0) && _hostLayer is ArcOverlayLayer { Host: { } host })
        {
            size = host.Bounds.Size;
        }
        _sheet.Width = size.Width;
        _sheet.Height = size.Height;
    }

    private void OnSheetPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ActionSheet.IsOpenProperty || _sheet is not { IsOpen: false } sheet)
        {
            return;
        }
        if (IsOpen)
        {
            // Closed by a choice, Cancel, the scrim, a pull or Escape: the focus comes back to the row, with a ring only
            // when the keyboard opened the sheet.
            var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
            if (focused is null || sheet.IsVisualAncestorOf(focused))
            {
                Focus(_keyboardOpen ? NavigationMethod.Tab : NavigationMethod.Pointer);
            }
        }
        SetCurrentValue(IsOpenProperty, false);
    }

    private void OnActionInvoked(object? sender, ActionSheetActionEventArgs e)
    {
        if (_sheet is null)
        {
            return;
        }
        var index = _sheet.Actions.IndexOf(e.Action);
        var choices = Choices;
        if (index < 0 || index >= choices.Count)
        {
            return;
        }
        var item = choices[index];
        SetCurrentValue(SelectedIndexProperty, index);
        RaiseEvent(new PickerItemInvokedEventArgs(ItemInvokedEvent, item, index));
    }
}
