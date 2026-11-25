// Ported from vscode (src/vs/editor/common/viewModelEventDispatcher.ts, and IContentSizeChangedEvent of
// src/vs/editor/common/editorCommon.ts, commit b57d5c9e), MIT license.
// Differences from upstream:
//  - onEvent is the .NET event OutgoingEvent, over Core/Emitter so that it keeps upstream's delivery order;
//  - the union type OutgoingViewModelEvent is an abstract class whose Kind tells the events apart;
//  - the outgoing events whose upstream names are taken in this namespace or by the model events they wrap are prefixed
//    with Outgoing: OutgoingViewZonesChangedEvent (ViewZonesChangedEvent is the view event in ViewEvents.cs) and
//    OutgoingModelDecorationsChangedEvent, OutgoingModelLanguageChangedEvent,
//    OutgoingModelLanguageConfigurationChangedEvent, OutgoingModelContentChangedEvent, OutgoingModelOptionsChangedEvent,
//    OutgoingModelTokensChangedEvent, OutgoingModelLineHeightChangedEvent and OutgoingModelFontChangedEvent (the model's
//    ModelDecorationsChangedEvent, ... in Model/). These eight share the base OutgoingModelEvent<TEvent> (never a no-op,
//    never merged; the model event is Event). OutgoingViewModelEventKind keeps upstream's member names;
//  - addViewEventHandler's console.warn for a handler added twice is left out (the handler is still added twice);
//  - upstream's WidgetFocusChangedEvent.attemptToMerge returns a FocusChangedEvent (so two merged widget focus events
//    become a FocusChanged event); this keeps that behaviour.

using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;

namespace TKCode.Arc.Editor.ViewModel;

/// <summary>
/// Sends the view model's view events to its view event handlers, and its outgoing events to the editor. Events raised
/// between <see cref="BeginEmitViewEvents"/> and <see cref="EndEmitViewEvents"/> are collected and sent at the end;
/// outgoing events are merged with the ones of the same kind still waiting, and wait while view events are collected or
/// delivered.
/// </summary>
internal sealed class ViewModelEventDispatcher : IDisposable
{
    private readonly Emitter<OutgoingViewModelEvent> _onEvent = new();

    private readonly List<ViewEventHandler> _eventHandlers;
    private List<ViewEvent>? _viewEventQueue;
    private bool _isConsumingViewEventQueue;
    private ViewModelEventsCollector? _collector;
    private int _collectorCnt;
    private readonly List<OutgoingViewModelEvent> _outgoingEvents;

    public ViewModelEventDispatcher()
    {
        _eventHandlers = new List<ViewEventHandler>();
        _viewEventQueue = null;
        _isConsumingViewEventQueue = false;
        _collector = null;
        _collectorCnt = 0;
        _outgoingEvents = new List<OutgoingViewModelEvent>();
    }

    /// <summary>An outgoing event (upstream's onEvent).</summary>
    public event Action<OutgoingViewModelEvent>? OutgoingEvent
    {
        add
        {
            if (value is not null)
            {
                _onEvent.Add(value);
            }
        }
        remove => _onEvent.Remove(value);
    }

    public void Dispose()
    {
        _onEvent.Clear();
    }

    public void EmitOutgoingEvent(OutgoingViewModelEvent e)
    {
        AddOutgoingEvent(e);
        EmitOutgoingEvents();
    }

    private void AddOutgoingEvent(OutgoingViewModelEvent e)
    {
        for (int i = 0, len = _outgoingEvents.Count; i < len; i++)
        {
            var mergeResult = _outgoingEvents[i].Kind == e.Kind ? _outgoingEvents[i].AttemptToMerge(e) : null;
            if (mergeResult is not null)
            {
                _outgoingEvents[i] = mergeResult;
                return;
            }
        }

        // not merged
        _outgoingEvents.Add(e);
    }

    private void EmitOutgoingEvents()
    {
        while (_outgoingEvents.Count > 0)
        {
            if (_collector is not null || _isConsumingViewEventQueue)
            {
                // right now collecting or emitting view events, so let's postpone emitting
                return;
            }

            var e = _outgoingEvents[0];
            _outgoingEvents.RemoveAt(0);
            if (e.IsNoOp())
            {
                continue;
            }

            _onEvent.Fire(e);
        }
    }

    public void AddViewEventHandler(ViewEventHandler eventHandler)
    {
        _eventHandlers.Add(eventHandler);
    }

    public void RemoveViewEventHandler(ViewEventHandler eventHandler)
    {
        for (var i = 0; i < _eventHandlers.Count; i++)
        {
            if (ReferenceEquals(_eventHandlers[i], eventHandler))
            {
                _eventHandlers.RemoveAt(i);
                break;
            }
        }
    }

    public ViewModelEventsCollector BeginEmitViewEvents()
    {
        _collectorCnt++;
        if (_collectorCnt == 1)
        {
            _collector = new ViewModelEventsCollector();
        }

        return _collector!;
    }

    public void EndEmitViewEvents()
    {
        _collectorCnt--;
        if (_collectorCnt == 0)
        {
            var outgoingEvents = _collector!.OutgoingEvents;
            var viewEvents = _collector.ViewEvents;
            _collector = null;

            foreach (var outgoingEvent in outgoingEvents)
            {
                AddOutgoingEvent(outgoingEvent);
            }

            if (viewEvents.Count > 0)
            {
                EmitMany(viewEvents);
            }
        }

        EmitOutgoingEvents();
    }

    public void EmitSingleViewEvent(ViewEvent e)
    {
        try
        {
            var eventsCollector = BeginEmitViewEvents();
            eventsCollector.EmitViewEvent(e);
        }
        finally
        {
            EndEmitViewEvents();
        }
    }

    private void EmitMany(List<ViewEvent> events)
    {
        if (_viewEventQueue is not null)
        {
            var combined = new List<ViewEvent>(_viewEventQueue.Count + events.Count);
            combined.AddRange(_viewEventQueue);
            combined.AddRange(events);
            _viewEventQueue = combined;
        }
        else
        {
            _viewEventQueue = events;
        }

        if (!_isConsumingViewEventQueue)
        {
            ConsumeViewEventQueue();
        }
    }

    private void ConsumeViewEventQueue()
    {
        try
        {
            _isConsumingViewEventQueue = true;
            DoConsumeQueue();
        }
        finally
        {
            _isConsumingViewEventQueue = false;
        }
    }

    private void DoConsumeQueue()
    {
        while (_viewEventQueue is not null)
        {
            // Empty event queue, as events might come in while sending these off
            var events = _viewEventQueue;
            _viewEventQueue = null;

            // Use a clone of the event handlers list, as they might remove themselves
            var eventHandlers = _eventHandlers.ToArray();
            foreach (var eventHandler in eventHandlers)
            {
                eventHandler.HandleEvents(events);
            }
        }
    }
}

/// <summary>Collects the view events and outgoing events raised between a begin and an end of emitting.</summary>
internal sealed class ViewModelEventsCollector
{
    public ViewModelEventsCollector()
    {
        ViewEvents = new List<ViewEvent>();
        OutgoingEvents = new List<OutgoingViewModelEvent>();
    }

    public List<ViewEvent> ViewEvents { get; }

    public List<OutgoingViewModelEvent> OutgoingEvents { get; }

    public void EmitViewEvent(ViewEvent e)
    {
        ViewEvents.Add(e);
    }

    public void EmitOutgoingEvent(OutgoingViewModelEvent e)
    {
        OutgoingEvents.Add(e);
    }
}

/// <summary>The kinds of outgoing view model events.</summary>
internal enum OutgoingViewModelEventKind
{
    ContentSizeChanged,
    FocusChanged,
    WidgetFocusChanged,
    ScrollChanged,
    ViewZonesChanged,
    HiddenAreasChanged,
    ReadOnlyEditAttempt,
    CursorStateChanged,
    ModelDecorationsChanged,
    ModelLanguageChanged,
    ModelLanguageConfigurationChanged,
    ModelContentChanged,
    ModelOptionsChanged,
    ModelTokensChanged,
    ModelLineHeightChanged,
    ModelFontChangedEvent,
}

/// <summary>An event the view model sends to the editor (upstream's union type OutgoingViewModelEvent).</summary>
internal abstract class OutgoingViewModelEvent
{
    public abstract OutgoingViewModelEventKind Kind { get; }

    /// <summary>Whether the event changes nothing and need not be sent.</summary>
    public abstract bool IsNoOp();

    /// <summary>The event that has the effect of this one followed by <paramref name="other"/>, or null when they do not merge.</summary>
    public abstract OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other);
}

/// <summary>The content size of an editor changed (upstream's IContentSizeChangedEvent in editorCommon.ts).</summary>
internal interface IContentSizeChangedEvent
{
    double ContentWidth { get; }

    double ContentHeight { get; }

    bool ContentWidthChanged { get; }

    bool ContentHeightChanged { get; }
}

internal sealed class ContentSizeChangedEvent : OutgoingViewModelEvent, IContentSizeChangedEvent
{
    private readonly double _oldContentWidth;
    private readonly double _oldContentHeight;

    public ContentSizeChangedEvent(double oldContentWidth, double oldContentHeight, double contentWidth, double contentHeight)
    {
        _oldContentWidth = oldContentWidth;
        _oldContentHeight = oldContentHeight;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
        ContentWidthChanged = _oldContentWidth != ContentWidth;
        ContentHeightChanged = _oldContentHeight != ContentHeight;
    }

    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ContentSizeChanged;

    public double ContentWidth { get; }

    public double ContentHeight { get; }

    public bool ContentWidthChanged { get; }

    public bool ContentHeightChanged { get; }

    public override bool IsNoOp()
    {
        return !ContentWidthChanged && !ContentHeightChanged;
    }

    public override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        if (other.Kind != Kind)
        {
            return null;
        }

        var o = (ContentSizeChangedEvent)other;
        return new ContentSizeChangedEvent(_oldContentWidth, _oldContentHeight, o.ContentWidth, o.ContentHeight);
    }
}

internal sealed class FocusChangedEvent : OutgoingViewModelEvent
{
    public FocusChangedEvent(bool oldHasFocus, bool hasFocus)
    {
        OldHasFocus = oldHasFocus;
        HasFocus = hasFocus;
    }

    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.FocusChanged;

    public bool OldHasFocus { get; }

    public bool HasFocus { get; }

    public override bool IsNoOp()
    {
        return OldHasFocus == HasFocus;
    }

    public override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        if (other.Kind != Kind)
        {
            return null;
        }

        return new FocusChangedEvent(OldHasFocus, ((FocusChangedEvent)other).HasFocus);
    }
}

internal sealed class WidgetFocusChangedEvent : OutgoingViewModelEvent
{
    public WidgetFocusChangedEvent(bool oldHasFocus, bool hasFocus)
    {
        OldHasFocus = oldHasFocus;
        HasFocus = hasFocus;
    }

    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.WidgetFocusChanged;

    public bool OldHasFocus { get; }

    public bool HasFocus { get; }

    public override bool IsNoOp()
    {
        return OldHasFocus == HasFocus;
    }

    public override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        if (other.Kind != Kind)
        {
            return null;
        }

        // As upstream: the merged event is a FocusChangedEvent.
        return new FocusChangedEvent(OldHasFocus, ((WidgetFocusChangedEvent)other).HasFocus);
    }
}

internal sealed class ScrollChangedEvent : OutgoingViewModelEvent
{
    private readonly double _oldScrollWidth;
    private readonly double _oldScrollLeft;
    private readonly double _oldScrollHeight;
    private readonly double _oldScrollTop;

    public ScrollChangedEvent(
        double oldScrollWidth, double oldScrollLeft, double oldScrollHeight, double oldScrollTop,
        double scrollWidth, double scrollLeft, double scrollHeight, double scrollTop)
    {
        _oldScrollWidth = oldScrollWidth;
        _oldScrollLeft = oldScrollLeft;
        _oldScrollHeight = oldScrollHeight;
        _oldScrollTop = oldScrollTop;

        ScrollWidth = scrollWidth;
        ScrollLeft = scrollLeft;
        ScrollHeight = scrollHeight;
        ScrollTop = scrollTop;

        ScrollWidthChanged = _oldScrollWidth != ScrollWidth;
        ScrollLeftChanged = _oldScrollLeft != ScrollLeft;
        ScrollHeightChanged = _oldScrollHeight != ScrollHeight;
        ScrollTopChanged = _oldScrollTop != ScrollTop;
    }

    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ScrollChanged;

    public double ScrollWidth { get; }

    public double ScrollLeft { get; }

    public double ScrollHeight { get; }

    public double ScrollTop { get; }

    public bool ScrollWidthChanged { get; }

    public bool ScrollLeftChanged { get; }

    public bool ScrollHeightChanged { get; }

    public bool ScrollTopChanged { get; }

    public override bool IsNoOp()
    {
        return !ScrollWidthChanged && !ScrollLeftChanged && !ScrollHeightChanged && !ScrollTopChanged;
    }

    public override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        if (other.Kind != Kind)
        {
            return null;
        }

        var o = (ScrollChangedEvent)other;
        return new ScrollChangedEvent(
            _oldScrollWidth, _oldScrollLeft, _oldScrollHeight, _oldScrollTop,
            o.ScrollWidth, o.ScrollLeft, o.ScrollHeight, o.ScrollTop);
    }
}

/// <summary>The view zones changed (upstream's outgoing ViewZonesChangedEvent).</summary>
internal sealed class OutgoingViewZonesChangedEvent : OutgoingViewModelEvent
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ViewZonesChanged;

    public override bool IsNoOp()
    {
        return false;
    }

    public override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        if (other.Kind != Kind)
        {
            return null;
        }

        return this;
    }
}

internal sealed class HiddenAreasChangedEvent : OutgoingViewModelEvent
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.HiddenAreasChanged;

    public override bool IsNoOp()
    {
        return false;
    }

    public override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        if (other.Kind != Kind)
        {
            return null;
        }

        return this;
    }
}

internal sealed class CursorStateChangedEvent : OutgoingViewModelEvent
{
    public CursorStateChangedEvent(
        IReadOnlyList<TextSelection>? oldSelections,
        IReadOnlyList<TextSelection> selections,
        int oldModelVersionId,
        int modelVersionId,
        string source,
        CursorChangeReason reason,
        bool reachedMaxCursorCount)
    {
        OldSelections = oldSelections;
        Selections = selections;
        OldModelVersionId = oldModelVersionId;
        ModelVersionId = modelVersionId;
        Source = source;
        Reason = reason;
        ReachedMaxCursorCount = reachedMaxCursorCount;
    }

    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.CursorStateChanged;

    public IReadOnlyList<TextSelection>? OldSelections { get; }

    public IReadOnlyList<TextSelection> Selections { get; }

    public int OldModelVersionId { get; }

    public int ModelVersionId { get; }

    public string Source { get; }

    public CursorChangeReason Reason { get; }

    public bool ReachedMaxCursorCount { get; }

    private static bool SelectionsAreEqual(IReadOnlyList<TextSelection>? a, IReadOnlyList<TextSelection>? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        var aLen = a.Count;
        var bLen = b.Count;
        if (aLen != bLen)
        {
            return false;
        }

        for (var i = 0; i < aLen; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool IsNoOp()
    {
        return SelectionsAreEqual(OldSelections, Selections) && OldModelVersionId == ModelVersionId;
    }

    public override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        if (other.Kind != Kind)
        {
            return null;
        }

        var o = (CursorStateChangedEvent)other;
        return new CursorStateChangedEvent(
            OldSelections, o.Selections, OldModelVersionId, o.ModelVersionId, o.Source, o.Reason, ReachedMaxCursorCount || o.ReachedMaxCursorCount);
    }
}

internal sealed class ReadOnlyEditAttemptEvent : OutgoingViewModelEvent
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ReadOnlyEditAttempt;

    public override bool IsNoOp()
    {
        return false;
    }

    public override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        if (other.Kind != Kind)
        {
            return null;
        }

        return this;
    }
}

/// <summary>An outgoing event that forwards a model event; it is always sent and never merges.</summary>
internal abstract class OutgoingModelEvent<TEvent> : OutgoingViewModelEvent
{
    protected OutgoingModelEvent(TEvent e)
    {
        Event = e;
    }

    /// <summary>The model event.</summary>
    public TEvent Event { get; }

    public sealed override bool IsNoOp()
    {
        return false;
    }

    public sealed override OutgoingViewModelEvent? AttemptToMerge(OutgoingViewModelEvent other)
    {
        return null;
    }
}

/// <summary>Upstream's outgoing ModelDecorationsChangedEvent.</summary>
internal sealed class OutgoingModelDecorationsChangedEvent(ModelDecorationsChangedEvent e) : OutgoingModelEvent<ModelDecorationsChangedEvent>(e)
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ModelDecorationsChanged;
}

/// <summary>Upstream's outgoing ModelLanguageChangedEvent.</summary>
internal sealed class OutgoingModelLanguageChangedEvent(ModelLanguageChangedEvent e) : OutgoingModelEvent<ModelLanguageChangedEvent>(e)
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ModelLanguageChanged;
}

/// <summary>Upstream's outgoing ModelLanguageConfigurationChangedEvent.</summary>
internal sealed class OutgoingModelLanguageConfigurationChangedEvent(ModelLanguageConfigurationChangedEvent e)
    : OutgoingModelEvent<ModelLanguageConfigurationChangedEvent>(e)
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ModelLanguageConfigurationChanged;
}

/// <summary>Upstream's outgoing ModelContentChangedEvent.</summary>
internal sealed class OutgoingModelContentChangedEvent(ModelContentChangedEvent e) : OutgoingModelEvent<ModelContentChangedEvent>(e)
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ModelContentChanged;
}

/// <summary>Upstream's outgoing ModelOptionsChangedEvent.</summary>
internal sealed class OutgoingModelOptionsChangedEvent(ModelOptionsChangedEvent e) : OutgoingModelEvent<ModelOptionsChangedEvent>(e)
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ModelOptionsChanged;
}

/// <summary>Upstream's outgoing ModelTokensChangedEvent.</summary>
internal sealed class OutgoingModelTokensChangedEvent(ModelTokensChangedEvent e) : OutgoingModelEvent<ModelTokensChangedEvent>(e)
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ModelTokensChanged;
}

/// <summary>Upstream's outgoing ModelLineHeightChangedEvent.</summary>
internal sealed class OutgoingModelLineHeightChangedEvent(ModelLineHeightChangedEvent e) : OutgoingModelEvent<ModelLineHeightChangedEvent>(e)
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ModelLineHeightChanged;
}

/// <summary>Upstream's outgoing ModelFontChangedEvent.</summary>
internal sealed class OutgoingModelFontChangedEvent(ModelFontChangedEvent e) : OutgoingModelEvent<ModelFontChangedEvent>(e)
{
    public override OutgoingViewModelEventKind Kind => OutgoingViewModelEventKind.ModelFontChangedEvent;
}
