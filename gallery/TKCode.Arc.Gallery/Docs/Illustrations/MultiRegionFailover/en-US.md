---
title: Multi-region failover
summary: A global load balancer shifts traffic to a standby region and promotes its database when the primary region goes down.
---

<Demo name="AutomaticFailover" />

## When to use

- Explaining how a service stays available when one region fails, on an infrastructure or reliability page.
- Architecture docs that need to show which region serves traffic and which database is primary.
- Demonstrating a failover procedure before a team runs it for real.

## When not to use

- A notice that a real service is down: use `Alert` with the actual status text.
- Showing one region or one server: the story needs two or three regions, so use `Card` with plain text instead.
- Reporting real uptime or latency over time: use `LineChart` or `Gauge` with the measured values.

## Usage

By default the story plays by itself: a region fails its health checks, traffic and the primary database move to
a standby region, the failed region recovers as a replica, and traffic returns to the planned region.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiRegionFailover />
</UserControl>
```

### Regions

`Regions` takes two or three region names. The default is `us-east-1`, `eu-west-1` and `ap-southeast-1`. A list
with fewer than two names falls back to the default.

<Demo name="TwoRegions" />

### Driving the failure yourself

Set `IsAutomatic` to `False` and call `FailRegion` and `RecoverRegion` with the region index. Each call runs the
same state machine as the automatic story. `PrimaryRegion` is the index of the region whose database is primary,
and `TrafficRegion` is the index of the region that receives traffic.

<Demo name="DriveItYourself" />

```csharp
var failover = new MultiRegionFailover { IsAutomatic = false };
failover.PrimaryRegionChanged += (_, index) =>
    primaryLabel.Text = $"Primary: {MultiRegionFailover.DefaultRegions[index]}";

failButton.Click += (_, _) => failover.FailRegion(0);
recoverButton.Click += (_, _) => failover.RecoverRegion(0);
```

`PrimaryRegionChanged` is raised with the new primary index after a promotion. Fail the region that holds the
primary to trigger one.

### Labels

`Labels` takes a `MultiRegionFailoverLabels` object with the visible words of the diagram: node names, health
states, badges (`Failing over`, `Promoted`) and the step captions.

```csharp
failover.Labels = new MultiRegionFailoverLabels
{
    Balancer = "Global LB",
    FailingOver = "Switching",
    Promoted = "Now primary"
};
```

### Playback

`IsPlaying`, `Speed` and `Restart()` control the automatic story in the same way as on the other illustrations.
The story has six steps, and `StepChanged` reports each one.

```xml
<arc:MultiRegionFailover Speed="2" />
```

### Reduced motion

Under reduced motion the illustration is a still frame of the healthy deployment.

<Demo name="ReducedMotion" />

## API reference

<!-- api: MultiRegionFailover -->

<!-- api: MultiRegionFailoverLabels -->

## Accessibility

- `Label` is the accessible name, and the current caption is exposed as the help text.
- The illustration is focusable and has a 2px focus ring.
- Space or Enter plays and pauses the story, and R starts it over.
- Caption changes are not announced. Manual failure controls should be real buttons with their own names, such as
  "Fail us-east-1".

## Motion

- The failing region turns amber, then red, and its packets in transit are dropped.
- The load balancer moves traffic to a healthy region with a "Failing over" badge that shows the switch time.
- The standby database is promoted with a "Promoted" badge, and replication runs from the new primary.
- Recovery shows the region catching up as a replica, followed by a planned failback in the automatic story.
- Reduced motion and Off show the healthy deployment as a still frame, with the standby path drawn dashed. The play
  button is hidden.

## Related

- `PaymentFlow` for a single request that depends on the payment service being available.
- `DataFlow` for the pipeline that stores events in one warehouse.
- `Alert` for stating the status of a real service in text.
