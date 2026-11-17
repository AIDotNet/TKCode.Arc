using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using TKCode.Arc.DesignTokens;
using TKCode.Arc.Motion;
using TKCode.Arc.Primitives;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using AvaloniaButton = Avalonia.Controls.Button;

namespace TKCode.Arc;

public partial class Table
{
    #region Items

    private void OnItemsSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceCollectionChanged;
            _observedSource = null;
        }
        if (newValue is INotifyCollectionChanged observable)
        {
            _observedSource = observable;
            if (!_sourcesDetached)
            {
                observable.CollectionChanged += OnSourceCollectionChanged;
            }
        }
        RebuildItems(animate: IsLoaded);
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildItems(animate: IsLoaded);

    private bool _sourcesDetached;

    // A view-model collection that outlives the page would keep the table alive and re-sort it on every change while it
    // is not shown: the table listens only while attached and catches up once when it comes back.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_sourcesDetached)
        {
            return;
        }
        _sourcesDetached = true;
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged -= OnSourceCollectionChanged;
        }
        if (_observedSelection is not null)
        {
            _observedSelection.CollectionChanged -= OnSelectedCollectionChanged;
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_sourcesDetached)
        {
            return;
        }
        _sourcesDetached = false;
        if (_observedSelection is not null)
        {
            _observedSelection.CollectionChanged += OnSelectedCollectionChanged;
        }
        if (_observedSource is not null)
        {
            _observedSource.CollectionChanged += OnSourceCollectionChanged;
            RebuildItems(animate: false);
        }
        if (_observedSelection is not null && !_syncingSelection)
        {
            SyncSelectedItemsIn();
        }
    }

    private void RebuildItems(bool animate)
    {
        // Rows that survive the change slide from where they were drawn (FLIP keyed by row key).
        var oldIndex = _indexByKey;
        var oldPosition = _position;
        _presenter?.CaptureFlip(key => oldIndex.TryGetValue(key, out var source) && source < oldPosition.Length ? oldPosition[source] : -1,
            animate && ArcMotion.IsFull(this));
        var items = new List<object>();
        if (ItemsSource is { } source)
        {
            foreach (var item in source)
            {
                if (item is not null)
                {
                    items.Add(item);
                }
            }
        }
        var selector = RowKeySelector;
        var keys = new List<object>(items.Count);
        var index = new Dictionary<object, int>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var key = selector?.Invoke(items[i]) ?? items[i];
            keys.Add(key);
            index.TryAdd(key, i);
        }
        _items = items;
        _keys = keys;
        _indexByKey = index;
        foreach (var state in _states)
        {
            state.SortValues = null;
            state.Numeric = null;
        }
        if (!_naturalLocked)
        {
            foreach (var state in _states)
            {
                state.Natural = -1;
            }
        }
        DataVersion++;
        UpdateCompactLead();
        ApplySort(animate, captureFlip: false);
        SyncSelectedItemsOut(raise: false);
        UpdateCount();
        UpdatePseudoClasses();
        _presenter?.InvalidateMeasure();
        _presenter?.InvalidateVisual();
    }

    #endregion
}
