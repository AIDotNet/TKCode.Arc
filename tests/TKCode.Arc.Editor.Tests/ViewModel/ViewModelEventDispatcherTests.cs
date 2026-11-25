// Tests for ViewModelEventDispatcher, the outgoing view model events and ViewEventHandler (vscode's
// src/vs/editor/common/viewModelEventDispatcher.ts and viewEventHandler.ts, commit b57d5c9e, MIT license). Upstream has
// no test of its own for them; these check upstream's behaviour as its code states it: outgoing events merge with the
// waiting event of the same kind, no-ops are dropped, outgoing events wait while view events are collected or delivered,
// view events raised while delivering are queued behind the batch in flight, and handlers may remove themselves.

using Shouldly;
using TKCode.Arc.Editor.Cursor;
using TKCode.Arc.Editor.Model;
using TKCode.Arc.Editor.ViewModel;
using Xunit;

namespace TKCode.Arc.Editor.Tests;

public sealed class ViewModelEventDispatcherTests
{
    /// <summary>Records every view event it gets, in batches, and can act on an event while handling it.</summary>
    private sealed class RecordingHandler(string name, List<string> log) : ViewEventHandler
    {
        public Action<ViewEvent>? OnEvent { get; set; }

        public bool Render { get; set; }

        private bool Record(ViewEvent e)
        {
            log.Add($"{name}:{e.Type}");
            OnEvent?.Invoke(e);
            return Render;
        }

        public override bool OnFlushed(ViewFlushedEvent e) => Record(e);

        public override bool OnFocusChanged(ViewFocusChangedEvent e) => Record(e);

        public override bool OnLinesChanged(ViewLinesChangedEvent e) => Record(e);

        public override bool OnZonesChanged(ViewZonesChangedEvent e) => Record(e);

        public override bool OnLineMappingChanged(ViewLineMappingChangedEvent e) => Record(e);
    }

    private static ViewModelEventDispatcher CreateDispatcher(List<string> log)
    {
        var dispatcher = new ViewModelEventDispatcher();
        dispatcher.OutgoingEvent += e => log.Add($"out:{e.Kind}");
        return dispatcher;
    }

    [Fact]
    public void Outgoing_Event_Is_Emitted_At_Once_When_Nothing_Is_Collected()
    {
        var log = new List<string>();
        using var dispatcher = CreateDispatcher(log);

        dispatcher.EmitOutgoingEvent(new FocusChangedEvent(false, true));

        log.ShouldBe(["out:FocusChanged"]);
    }

    [Fact]
    public void NoOp_Outgoing_Events_Are_Dropped()
    {
        var log = new List<string>();
        using var dispatcher = CreateDispatcher(log);

        dispatcher.EmitOutgoingEvent(new FocusChangedEvent(true, true));
        dispatcher.EmitOutgoingEvent(new ContentSizeChangedEvent(10, 20, 10, 20));
        dispatcher.EmitOutgoingEvent(new ScrollChangedEvent(1, 2, 3, 4, 1, 2, 3, 4));
        dispatcher.EmitOutgoingEvent(new CursorStateChangedEvent([], [], 1, 1, "api", CursorChangeReason.NotSet, false));

        log.ShouldBeEmpty();
    }

    [Fact]
    public void CursorStateChangedEvent_IsNoOp_Compares_Selections_And_Versions()
    {
        var a = new TextSelection(1, 1, 1, 3);
        var b = new TextSelection(1, 3, 1, 1);

        new CursorStateChangedEvent(null, [a], 1, 1, "api", CursorChangeReason.NotSet, false).IsNoOp().ShouldBeFalse();
        new CursorStateChangedEvent(null, [], 1, 1, "api", CursorChangeReason.NotSet, false).IsNoOp().ShouldBeFalse();
        new CursorStateChangedEvent([a], [a], 1, 1, "api", CursorChangeReason.NotSet, false).IsNoOp().ShouldBeTrue();
        new CursorStateChangedEvent([a], [b], 1, 1, "api", CursorChangeReason.NotSet, false).IsNoOp().ShouldBeFalse();
        new CursorStateChangedEvent([a], [a, a], 1, 1, "api", CursorChangeReason.NotSet, false).IsNoOp().ShouldBeFalse();
        new CursorStateChangedEvent([a], [a], 1, 2, "api", CursorChangeReason.NotSet, false).IsNoOp().ShouldBeFalse();
    }

    [Fact]
    public void Outgoing_Events_Wait_While_Collecting_And_Merge_By_Kind()
    {
        var received = new List<OutgoingViewModelEvent>();
        using var dispatcher = new ViewModelEventDispatcher();
        dispatcher.OutgoingEvent += received.Add;

        var collector = dispatcher.BeginEmitViewEvents();
        collector.EmitOutgoingEvent(new ScrollChangedEvent(100, 0, 1000, 0, 100, 0, 1000, 50));
        collector.EmitOutgoingEvent(new FocusChangedEvent(false, true));
        collector.EmitOutgoingEvent(new ScrollChangedEvent(100, 0, 1000, 50, 200, 10, 1000, 70));
        collector.EmitOutgoingEvent(new ContentSizeChangedEvent(10, 20, 30, 20));
        collector.EmitOutgoingEvent(new ContentSizeChangedEvent(30, 20, 30, 40));
        received.ShouldBeEmpty();

        // Emitted directly while collecting: added to the waiting events, merged with none yet, and held back.
        dispatcher.EmitOutgoingEvent(new ReadOnlyEditAttemptEvent());
        received.ShouldBeEmpty();

        dispatcher.EndEmitViewEvents();

        // The direct one was added first (the collector's events are added at the end), then the collector's in order,
        // each merged into the first of its kind.
        received.Select(e => e.Kind).ShouldBe(
        [
            OutgoingViewModelEventKind.ReadOnlyEditAttempt,
            OutgoingViewModelEventKind.ScrollChanged,
            OutgoingViewModelEventKind.FocusChanged,
            OutgoingViewModelEventKind.ContentSizeChanged,
        ]);

        var scroll = (ScrollChangedEvent)received[1];
        scroll.ScrollWidth.ShouldBe(200);
        scroll.ScrollLeft.ShouldBe(10);
        scroll.ScrollHeight.ShouldBe(1000);
        scroll.ScrollTop.ShouldBe(70);
        scroll.ScrollWidthChanged.ShouldBeTrue();
        scroll.ScrollLeftChanged.ShouldBeTrue();
        scroll.ScrollHeightChanged.ShouldBeFalse();
        scroll.ScrollTopChanged.ShouldBeTrue();

        var size = (ContentSizeChangedEvent)received[3];
        size.ContentWidth.ShouldBe(30);
        size.ContentHeight.ShouldBe(40);
        size.ContentWidthChanged.ShouldBeTrue();
        size.ContentHeightChanged.ShouldBeTrue();
    }

    [Fact]
    public void Merged_Events_That_Cancel_Out_Are_Dropped()
    {
        var received = new List<OutgoingViewModelEvent>();
        using var dispatcher = new ViewModelEventDispatcher();
        dispatcher.OutgoingEvent += received.Add;

        var collector = dispatcher.BeginEmitViewEvents();
        collector.EmitOutgoingEvent(new FocusChangedEvent(false, true));
        collector.EmitOutgoingEvent(new FocusChangedEvent(true, false));
        collector.EmitOutgoingEvent(new ScrollChangedEvent(0, 0, 0, 0, 0, 0, 0, 10));
        collector.EmitOutgoingEvent(new ScrollChangedEvent(0, 0, 0, 10, 0, 0, 0, 0));
        collector.EmitOutgoingEvent(new ContentSizeChangedEvent(1, 1, 2, 2));
        collector.EmitOutgoingEvent(new ContentSizeChangedEvent(2, 2, 1, 1));
        dispatcher.EndEmitViewEvents();

        received.ShouldBeEmpty();
    }

    [Fact]
    public void CursorStateChangedEvent_Merge_Keeps_The_First_Old_State_And_The_Last_New_State()
    {
        var received = new List<OutgoingViewModelEvent>();
        using var dispatcher = new ViewModelEventDispatcher();
        dispatcher.OutgoingEvent += received.Add;

        var s1 = new TextSelection(1, 1, 1, 1);
        var s2 = new TextSelection(1, 2, 1, 2);
        var s3 = new TextSelection(2, 1, 2, 1);

        var collector = dispatcher.BeginEmitViewEvents();
        collector.EmitOutgoingEvent(new CursorStateChangedEvent([s1], [s2], 1, 2, "keyboard", CursorChangeReason.Explicit, true));
        collector.EmitOutgoingEvent(new CursorStateChangedEvent([s2], [s3], 2, 3, "mouse", CursorChangeReason.Paste, false));
        dispatcher.EndEmitViewEvents();

        var e = received.ShouldHaveSingleItem().ShouldBeOfType<CursorStateChangedEvent>();
        e.OldSelections.ShouldBe([s1]);
        e.Selections.ShouldBe([s3]);
        e.OldModelVersionId.ShouldBe(1);
        e.ModelVersionId.ShouldBe(3);
        e.Source.ShouldBe("mouse");
        e.Reason.ShouldBe(CursorChangeReason.Paste);
        e.ReachedMaxCursorCount.ShouldBeTrue();
    }

    [Fact]
    public void Events_Without_State_Merge_Into_The_First_And_Model_Events_Never_Merge()
    {
        var received = new List<OutgoingViewModelEvent>();
        using var dispatcher = new ViewModelEventDispatcher();
        dispatcher.OutgoingEvent += received.Add;

        var zones1 = new OutgoingViewZonesChangedEvent();
        var hidden1 = new HiddenAreasChangedEvent();
        var readOnly1 = new ReadOnlyEditAttemptEvent();
        var decorations1 = new OutgoingModelDecorationsChangedEvent(new ModelDecorationsChangedEvent(true, false, false, false));
        var decorations2 = new OutgoingModelDecorationsChangedEvent(new ModelDecorationsChangedEvent(false, true, false, false));
        var tokens = new OutgoingModelTokensChangedEvent(new ModelTokensChangedEvent(false, [new ModelTokensChangedRange(1, 2)]));
        var languageConfiguration = new OutgoingModelLanguageConfigurationChangedEvent(new ModelLanguageConfigurationChangedEvent());

        var collector = dispatcher.BeginEmitViewEvents();
        collector.EmitOutgoingEvent(zones1);
        collector.EmitOutgoingEvent(hidden1);
        collector.EmitOutgoingEvent(readOnly1);
        collector.EmitOutgoingEvent(decorations1);
        collector.EmitOutgoingEvent(new OutgoingViewZonesChangedEvent());
        collector.EmitOutgoingEvent(new HiddenAreasChangedEvent());
        collector.EmitOutgoingEvent(new ReadOnlyEditAttemptEvent());
        collector.EmitOutgoingEvent(decorations2);
        collector.EmitOutgoingEvent(tokens);
        collector.EmitOutgoingEvent(languageConfiguration);
        dispatcher.EndEmitViewEvents();

        received.Count.ShouldBe(7);
        received[0].ShouldBeSameAs(zones1);
        received[1].ShouldBeSameAs(hidden1);
        received[2].ShouldBeSameAs(readOnly1);
        received[3].ShouldBeSameAs(decorations1);
        received[4].ShouldBeSameAs(decorations2);
        received[5].ShouldBeSameAs(tokens);
        received[6].ShouldBeSameAs(languageConfiguration);
        ((OutgoingModelDecorationsChangedEvent)received[4]).Event.AffectsOverviewRuler.ShouldBeTrue();
    }

    [Fact]
    public void WidgetFocusChangedEvent_Merges_Into_A_FocusChangedEvent_As_Upstream()
    {
        // Upstream's WidgetFocusChangedEvent.attemptToMerge returns a FocusChangedEvent; the port keeps it.
        var merged = new WidgetFocusChangedEvent(false, true).AttemptToMerge(new WidgetFocusChangedEvent(true, false));
        merged.ShouldBeOfType<FocusChangedEvent>();
        merged!.IsNoOp().ShouldBeTrue();

        new WidgetFocusChangedEvent(false, true).AttemptToMerge(new FocusChangedEvent(true, false)).ShouldBeNull();
    }

    [Fact]
    public void AttemptToMerge_Refuses_Other_Kinds()
    {
        var focus = new FocusChangedEvent(false, true);
        new ScrollChangedEvent(0, 0, 0, 0, 0, 0, 0, 1).AttemptToMerge(focus).ShouldBeNull();
        new ContentSizeChangedEvent(0, 0, 1, 1).AttemptToMerge(focus).ShouldBeNull();
        new CursorStateChangedEvent(null, [], 1, 1, "api", CursorChangeReason.NotSet, false).AttemptToMerge(focus).ShouldBeNull();
        new OutgoingViewZonesChangedEvent().AttemptToMerge(focus).ShouldBeNull();
        new HiddenAreasChangedEvent().AttemptToMerge(focus).ShouldBeNull();
        new ReadOnlyEditAttemptEvent().AttemptToMerge(focus).ShouldBeNull();
        focus.AttemptToMerge(new WidgetFocusChangedEvent(true, false)).ShouldBeNull();
    }

    [Fact]
    public void View_Events_Are_Delivered_At_The_End_Before_The_Outgoing_Events()
    {
        var log = new List<string>();
        using var dispatcher = CreateDispatcher(log);
        var a = new RecordingHandler("a", log);
        var b = new RecordingHandler("b", log);
        dispatcher.AddViewEventHandler(a);
        dispatcher.AddViewEventHandler(b);

        var collector = dispatcher.BeginEmitViewEvents();
        collector.EmitViewEvent(new ViewFlushedEvent());
        collector.EmitOutgoingEvent(new HiddenAreasChangedEvent());
        collector.EmitViewEvent(new ViewLinesChangedEvent(1, 2));
        log.ShouldBeEmpty();

        dispatcher.EndEmitViewEvents();

        log.ShouldBe(
        [
            "a:ViewFlushed",
            "a:ViewLinesChanged",
            "b:ViewFlushed",
            "b:ViewLinesChanged",
            "out:HiddenAreasChanged",
        ]);
    }

    [Fact]
    public void Nested_Begin_Returns_The_Same_Collector_And_Only_The_Outer_End_Delivers()
    {
        var log = new List<string>();
        using var dispatcher = CreateDispatcher(log);
        dispatcher.AddViewEventHandler(new RecordingHandler("a", log));

        var outer = dispatcher.BeginEmitViewEvents();
        outer.EmitViewEvent(new ViewFlushedEvent());
        var inner = dispatcher.BeginEmitViewEvents();
        inner.ShouldBeSameAs(outer);
        inner.EmitViewEvent(new ViewZonesChangedEvent());
        inner.EmitOutgoingEvent(new OutgoingViewZonesChangedEvent());
        dispatcher.EndEmitViewEvents();
        log.ShouldBeEmpty();

        // An outgoing event emitted directly still waits for the outer end.
        dispatcher.EmitOutgoingEvent(new ReadOnlyEditAttemptEvent());
        log.ShouldBeEmpty();

        dispatcher.EndEmitViewEvents();
        log.ShouldBe(["a:ViewFlushed", "a:ViewZonesChanged", "out:ReadOnlyEditAttempt", "out:ViewZonesChanged"]);

        // A new collector after the end.
        dispatcher.BeginEmitViewEvents().ShouldNotBeSameAs(outer);
        dispatcher.EndEmitViewEvents();
    }

    [Fact]
    public void EmitSingleViewEvent_Delivers_At_Once()
    {
        var log = new List<string>();
        using var dispatcher = CreateDispatcher(log);
        dispatcher.AddViewEventHandler(new RecordingHandler("a", log));

        dispatcher.EmitSingleViewEvent(new ViewFocusChangedEvent(true));

        log.ShouldBe(["a:ViewFocusChanged"]);
    }

    [Fact]
    public void View_Events_Raised_While_Delivering_Are_Queued_Behind_The_Batch_In_Flight()
    {
        var log = new List<string>();
        using var dispatcher = CreateDispatcher(log);
        var a = new RecordingHandler("a", log);
        var b = new RecordingHandler("b", log);
        dispatcher.AddViewEventHandler(a);
        dispatcher.AddViewEventHandler(b);

        a.OnEvent = e =>
        {
            if (e is ViewFlushedEvent)
            {
                // Collected and delivered after b got the flush, not nested inside a's handling.
                dispatcher.EmitSingleViewEvent(new ViewLineMappingChangedEvent());
                // Outgoing events wait until the queue is empty.
                dispatcher.EmitOutgoingEvent(new HiddenAreasChangedEvent());
                log.Add("a:emitted");
            }
        };

        dispatcher.EmitSingleViewEvent(new ViewFlushedEvent());

        log.ShouldBe(
        [
            "a:ViewFlushed",
            "a:emitted",
            "b:ViewFlushed",
            "a:ViewLineMappingChanged",
            "b:ViewLineMappingChanged",
            "out:HiddenAreasChanged",
        ]);
    }

    [Fact]
    public void A_Handler_May_Remove_Itself_While_Handling_And_Others_Still_Get_The_Batch()
    {
        var log = new List<string>();
        using var dispatcher = CreateDispatcher(log);
        var a = new RecordingHandler("a", log);
        var b = new RecordingHandler("b", log);
        dispatcher.AddViewEventHandler(a);
        dispatcher.AddViewEventHandler(b);
        a.OnEvent = _ => dispatcher.RemoveViewEventHandler(a);

        dispatcher.EmitSingleViewEvent(new ViewFlushedEvent());
        dispatcher.EmitSingleViewEvent(new ViewZonesChangedEvent());

        log.ShouldBe(["a:ViewFlushed", "b:ViewFlushed", "b:ViewZonesChanged"]);
    }

    [Fact]
    public void A_Handler_That_Throws_Does_Not_Leave_The_Dispatcher_Consuming()
    {
        var log = new List<string>();
        using var dispatcher = CreateDispatcher(log);
        var a = new RecordingHandler("a", log) { OnEvent = _ => throw new InvalidOperationException("boom") };
        dispatcher.AddViewEventHandler(a);

        Should.Throw<InvalidOperationException>(() => dispatcher.EmitSingleViewEvent(new ViewFlushedEvent()));

        dispatcher.RemoveViewEventHandler(a);
        dispatcher.EmitOutgoingEvent(new ReadOnlyEditAttemptEvent());
        log.ShouldBe(["a:ViewFlushed", "out:ReadOnlyEditAttempt"]);
    }

    [Fact]
    public void Removing_The_Outgoing_Listener_Stops_Delivery()
    {
        var received = new List<OutgoingViewModelEvent>();
        using var dispatcher = new ViewModelEventDispatcher();
        Action<OutgoingViewModelEvent> listener = received.Add;
        dispatcher.OutgoingEvent += listener;
        dispatcher.EmitOutgoingEvent(new ReadOnlyEditAttemptEvent());
        dispatcher.OutgoingEvent -= listener;
        dispatcher.EmitOutgoingEvent(new ReadOnlyEditAttemptEvent());

        received.Count.ShouldBe(1);
    }
}

public sealed class ViewEventHandlerTests
{
    /// <summary>Answers true for the event types it is told to, and records every call.</summary>
    private sealed class AllHandler(HashSet<ViewEventType> renderFor) : ViewEventHandler
    {
        public List<ViewEventType> Calls { get; } = new();

        private bool Record(ViewEvent e)
        {
            Calls.Add(e.Type);
            return renderFor.Contains(e.Type);
        }

        public override bool OnCompositionStart(ViewCompositionStartEvent e) => Record(e);
        public override bool OnCompositionEnd(ViewCompositionEndEvent e) => Record(e);
        public override bool OnConfigurationChanged(ViewConfigurationChangedEvent e) => Record(e);
        public override bool OnCursorStateChanged(ViewCursorStateChangedEvent e) => Record(e);
        public override bool OnDecorationsChanged(ViewDecorationsChangedEvent e) => Record(e);
        public override bool OnFlushed(ViewFlushedEvent e) => Record(e);
        public override bool OnFocusChanged(ViewFocusChangedEvent e) => Record(e);
        public override bool OnLanguageConfigurationChanged(ViewLanguageConfigurationEvent e) => Record(e);
        public override bool OnLineMappingChanged(ViewLineMappingChangedEvent e) => Record(e);
        public override bool OnLinesChanged(ViewLinesChangedEvent e) => Record(e);
        public override bool OnLinesDeleted(ViewLinesDeletedEvent e) => Record(e);
        public override bool OnLinesInserted(ViewLinesInsertedEvent e) => Record(e);
        public override bool OnRevealRangeRequest(ViewRevealRangeRequestEvent e) => Record(e);
        public override bool OnScrollChanged(ViewScrollChangedEvent e) => Record(e);
        public override bool OnThemeChanged(ViewThemeChangedEvent e) => Record(e);
        public override bool OnTokensChanged(ViewTokensChangedEvent e) => Record(e);
        public override bool OnTokensColorsChanged(ViewTokensColorsChangedEvent e) => Record(e);
        public override bool OnZonesChanged(ViewZonesChangedEvent e) => Record(e);
    }

    private static ViewEvent[] OneOfEach() =>
    [
        new ViewCompositionStartEvent(),
        new ViewCompositionEndEvent(),
        new ViewConfigurationChangedEvent(new ConfigurationChangedEvent([EditorOptionNames.LineHeight])),
        new ViewCursorStateChangedEvent([new TextSelection(1, 1, 1, 1)], [new TextSelection(1, 1, 1, 1)], CursorChangeReason.Explicit),
        new ViewDecorationsChangedEvent(null),
        new ViewFlushedEvent(),
        new ViewFocusChangedEvent(true),
        new ViewLanguageConfigurationEvent(),
        new ViewLineMappingChangedEvent(),
        new ViewLinesChangedEvent(1, 1),
        new ViewLinesDeletedEvent(1, 1),
        new ViewLinesInsertedEvent(1, 1),
        new ViewRevealRangeRequestEvent("test", false, new TextRange(1, 1, 1, 1), null, VerticalRevealType.Simple, true, ScrollType.Smooth),
        new ViewScrollChangedEvent(new ScrollEvent { ScrollTop = 10, ScrollTopChanged = true }),
        new ViewThemeChangedEvent(new object()),
        new ViewTokensChangedEvent([new ViewTokensChangedRange(1, 1)]),
        new ViewTokensColorsChangedEvent(),
        new ViewZonesChangedEvent(),
    ];

    [Fact]
    public void Every_Event_Type_Reaches_Its_Method()
    {
        var events = OneOfEach();
        events.Select(e => e.Type).ShouldBe(Enum.GetValues<ViewEventType>(), ignoreOrder: true);

        var handler = new AllHandler([]);
        handler.HandleEvents(events);

        handler.Calls.ShouldBe(events.Select(e => e.Type));
    }

    [Fact]
    public void ShouldRender_Follows_The_Handlers_Answers()
    {
        var handler = new AllHandler([ViewEventType.ViewScrollChanged]);
        handler.ShouldRender().ShouldBeTrue();

        handler.OnDidRender();
        handler.ShouldRender().ShouldBeFalse();

        handler.HandleEvents([new ViewFlushedEvent(), new ViewZonesChangedEvent()]);
        handler.ShouldRender().ShouldBeFalse();

        handler.HandleEvents([new ViewFlushedEvent(), new ViewScrollChangedEvent(new ScrollEvent())]);
        handler.ShouldRender().ShouldBeTrue();

        handler.OnDidRender();
        handler.ForceShouldRender();
        handler.ShouldRender().ShouldBeTrue();
    }

    [Fact]
    public void The_Base_Handler_Asks_For_No_Render()
    {
        var handler = new ViewEventHandler();
        handler.OnDidRender();
        handler.HandleEvents(OneOfEach());
        handler.ShouldRender().ShouldBeFalse();
    }

    [Fact]
    public void Extra_View_Events_Carry_Their_Values()
    {
        var configuration = new ViewConfigurationChangedEvent(new ConfigurationChangedEvent([EditorOptionNames.LayoutInfo, EditorOptionNames.Padding]));
        configuration.HasChanged(EditorOptionNames.LayoutInfo).ShouldBeTrue();
        configuration.HasChanged(EditorOptionNames.Padding).ShouldBeTrue();
        configuration.HasChanged(EditorOptionNames.LineHeight).ShouldBeFalse();

        var scroll = new ViewScrollChangedEvent(new ScrollEvent
        {
            ScrollWidth = 1,
            ScrollLeft = 2,
            ScrollHeight = 3,
            ScrollTop = 4,
            ScrollWidthChanged = true,
            ScrollLeftChanged = false,
            ScrollHeightChanged = true,
            ScrollTopChanged = false,
        });
        (scroll.ScrollWidth, scroll.ScrollLeft, scroll.ScrollHeight, scroll.ScrollTop).ShouldBe((1d, 2d, 3d, 4d));
        (scroll.ScrollWidthChanged, scroll.ScrollLeftChanged, scroll.ScrollHeightChanged, scroll.ScrollTopChanged).ShouldBe((true, false, true, false));
    }
}
