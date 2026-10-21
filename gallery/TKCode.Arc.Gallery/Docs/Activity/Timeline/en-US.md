---
title: Timeline
summary: Follow what happened, newest first, grouped by day.
---

<Demo name="LiveStream" />

## When to use

- A project history, an audit log or a deployment stream where the order and recency of events matter.
- Events that a person or a system produced, each with a short title and optional context.
- A feed that receives new events while it is on screen.

## When not to use

- Records that the reader sorts or compares: use `SortableDataTable`.
- Fixed steps with a current position: use `Stepper`.
- Threaded discussion on one object: use `CommentThread`, or `ChatThread` for a live conversation.

## Usage

Set `Now` to the reference time. Relative times ("9m", "2h") and day groups ("Today", "Yesterday") are computed from it.
Events can be given in any order, and the newest shows first.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Timeline Label="Release history"
                  Now="2026-10-20T12:00:00+00:00">
        <arc:TimelineEvent Id="build-418"
                           At="2026-10-20T11:48:00+00:00"
                           Actor="Ana Ruiz"
                           Title="deployed web to production"
                           Meta="Build #418"
                           Detail="All 312 checks passed in 4m 12s." />
        <arc:TimelineEvent Id="pr-212"
                           At="2026-10-20T11:02:00+00:00"
                           Actor="Theo Walsh"
                           Title="merged Checkout redesign into main"
                           Meta="PR #212" />
    </arc:Timeline>
</UserControl>
```

### Live feed

Bind `ItemsSource` to an `ObservableCollection<TimelineEvent>`. Events added to it slide in at the top. Advance `Now`
on a timer so relative times stay current. Arc suggests about once a minute.

```csharp
var events = new ObservableCollection<TimelineEvent>();
var timeline = new Timeline { Label = "Deployments", Now = DateTimeOffset.UtcNow, ItemsSource = events };

events.Add(new TimelineEvent("post-1", DateTimeOffset.UtcNow, "merged Checkout redesign into main", "Theo Walsh")
{
    Meta = "PR #212"
});

var clock = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
clock.Tick += (_, _) => timeline.Now = DateTimeOffset.UtcNow;
timeline.AttachedToVisualTree += (_, _) => clock.Start();
timeline.DetachedFromVisualTree += (_, _) => clock.Stop();
```

### Expandable rows and tones

A row with a `Detail` expands when it is activated. Rows without a detail are plain text. `Tone` colors a system event,
so its title must also state the outcome.

<Demo name="ExpandableDetails" />

<Demo name="TonesAndPortraits" />

```xml
<arc:Timeline Label="Pipeline" Now="2026-10-20T12:00:00+00:00">
    <arc:TimelineEvent Id="t1"
                       At="2026-10-20T11:55:00+00:00"
                       Title="Deploy succeeded"
                       Meta="web · production"
                       Tone="Success" />
    <arc:TimelineEvent Id="t2"
                       At="2026-10-20T11:42:00+00:00"
                       Title="Deploy failed: health check timed out"
                       Meta="api · staging"
                       Detail="Retried twice. The failing check is tracked in INC-204."
                       Tone="Danger" />
</arc:Timeline>
```

```csharp
timeline.ExpandedChanged += (_, e) => Status.Text = $"{e.Item.Title}, expanded: {e.IsExpanded}";
timeline.Expand("t2");
```

A person's event shows `Avatar`. A system event shows an `Icon`, such as `ArcIcons.Check`, tinted by `Tone`.

### Long history

Set `MaxHeight` to scroll inside the feed. The current day label stays pinned while its rows scroll, and the list is
virtualized, so long histories stay smooth.

<Demo name="ScrollingFeed" />

<Demo name="ThousandUpdates" />

```xml
<arc:Timeline Label="Audit log"
              Now="2026-10-20T12:00:00+00:00"
              ItemsSource="{Binding AuditEvents}"
              MaxHeight="380" />
```

## API reference

<!-- api: Timeline -->

<!-- api: TimelineEvent -->

## Accessibility

- The feed is a named region. `Label` is its accessible name.
- Each day label is a heading at `HeadingLevel` (3 by default), and each time has its full date as hidden text.
- Expandable rows are buttons that report their expanded state. Rows without a detail are not in the Tab order.
- New events are announced in a status region, without moving focus.
- Up and Down move between expandable rows, Home and End go to the first and last one, and Enter or Space expands or
  collapses the current row.

## Motion

- Rows draw their connector line as they enter the viewport, and their markers pop in.
- A new event slides in from the top while the rows below move down. Expanded details open on a spring.
- Reduced motion replaces movement with a short fade and changes heights at once. Off jumps to the final state.

## Related

- `CommentThread` for replies on one object, and `ChatThread` for a live two-way conversation.
- `SortableDataTable` when the reader needs to sort or compare records.
- `Stepper` for fixed steps with a current position.
