---
title: Multi-region failover
summary: A global load balancer shifts traffic to a standby region and promotes its database when the primary region goes down.
---

<Demo name="AutomaticFailover" />

## When to use

`MultiRegionFailover` shows how a service stays available when one of its regions fails. It draws the global traffic
path, each region with its application and database, and the replication links between the databases, then plays the
failure, the traffic shift, the database promotion and the recovery. It belongs on architecture and reliability pages,
in runbooks and in training material, where the reader needs to know which region serves traffic and which database
accepts writes at any moment.

- **Reliability page for a payment or billing API**: a page that explains why checkout keeps working when
  `us-east-1` is down. The primary region serves traffic, `eu-west-1` holds a replica that is promoted, and the caption
  describes what happens to in-flight requests.
- **Runbook rehearsal before a game day**: a team walks through a failover of an order database with the manual
  controls, using the same state machine as the diagram, before it runs the real procedure in production.
- **Architecture review of a multi-region deployment**: a design document that compares a two-region active-passive
  layout with a three-region layout, using the `Regions` property to show each variant.
- **Onboarding for on-call engineers**: a short explanation of what "promoted" and "replica" mean for the team's own
  database, placed next to the runbook steps they will follow.
- **Customer documentation for a data residency feature**: an explanation of where data is replicated and which region
  takes over, with the region names the customer's contract uses.

## When not to use

- **A notice that a real service is down**: use `Alert` with the actual status text and the time of the incident. The
  diagram plays a fixed story and must never stand in for the real state of production.
- **A single region or a single server**: the story needs two or three regions to make sense. Use `Card` with plain
  text, or a `Timeline` of the incident, when only one location is involved.
- **Real uptime, latency or error rates**: use `LineChart` or `Gauge` with the measured values. The illustration shows
  the shape of a failover, not how long your failovers took.
- **A live health view of every region**: use `DataGrid` or a list of `Badge` values with the health check results.
  Ten regions or a dozen health checks do not fit the three-region frame.
- **A procedure people follow step by step**: use `Stepper` for the runbook steps and link the diagram beside it. The
  illustration loops on its own and cannot wait for the operator to confirm a step.

## Usage

By default the story plays by itself: a region fails its health checks, traffic and the primary database move to a
standby region, the failed region recovers as a replica, and traffic returns to the planned region. The control needs
no data and no code to run.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiRegionFailover />
</UserControl>
```

`MultiRegionFailover` derives from `Illustration`, which derives from Avalonia's `TemplatedControl`. It takes focus,
has a `Label` for assistive technology, and uses the same frame, playback button and caption as the other system
illustrations. The control keeps a small state machine for each region: healthy, degraded, down or recovering. Every
visible change in the diagram is a transition of that state machine, so the diagram never shows a state the code
cannot reach.

### Anatomy

1. **Users** (`Labels.Users`, `Labels.UsersDetail`): the source of traffic at the top left, 150 by 52 scene units.
2. **Global balancer** (`Labels.Balancer`, `Labels.BalancerDetail`): a wider card at the top centre, 170 by 52 units,
   with the traffic path from the users. Beside it, a readout shows the region that receives traffic
   (`Labels.ServedBy`) and, during a switch, the failover time (`Labels.FailoverTime`).
3. **Region frames**: two or three groups across the lower part of the scene, named by the region. Each frame has a
   health dot, a title and a status word (`Labels.Healthy`, `Labels.Degraded`, `Labels.Down` or `Labels.Recovering`)
   drawn above the connections, so the lines never cross the text.
4. **Application** (`Labels.App`, `Labels.AppDetail`): the service in each region, with the traffic line from the
   balancer.
5. **Database** (`Labels.Database`, and a subtitle that reads `Labels.Primary`, `Labels.Replica`,
   `Labels.Unavailable` or "replica · catching up"): the data store in each region, with a link from the application.
6. **Replication links**: dashed lines between neighbouring databases in the order of the `Regions` list. Changes
   travel along them as green packets.
7. **Badges**: a "Failing over" badge on the balancer during a switch, and a "Promoted" badge on the database that
   became primary. The badge text comes from `Labels.FailingOver`, `Labels.Promoted` and `Labels.Primary`.
8. **Caption line** (`IsCaptionVisible`) and **play button** (`IsPlaybackButtonVisible`): the same controls as the
   other illustrations. The button is hidden in the still poster.

The scene is 844 by 356 units and scales to the width the control is given. The frame keeps 20 px of padding on the
sides and top and 14 px at the bottom, and the nodes use the 12 px corner radius of the token.

Health is shown in three tones: a degraded region turns warning, a down region turns danger and its packets are
dropped, and a recovered region returns to success.

### Regions

`Regions` takes two or three region names. A list with fewer than two names falls back to the default regions
(`us-east-1`, `eu-west-1` and `ap-southeast-1`), and a list with more than three uses the first three. The region
frames share the scene width evenly, so two regions give wider frames than three.

<Demo name="TwoRegions" />

The first region in the list starts as the primary, and the second is the standby. With two regions, every failure of
the primary moves traffic and the primary to the other region, and the failover is the only path the story can take.
With three regions, the failover goes to the next healthy region in the list, wrapping around at the end, so the order
of the list is the order of preference. Put the region that should take over first directly after the primary.

### Driving the failure yourself

Set `IsAutomatic` to `False` to stop the scripted story, and call `FailRegion` and `RecoverRegion` with the index of the
region. Each call runs the same state machine as the automatic story, so the diagram and the readouts behave the same way.

- `FailRegion(index)` takes a region down. It is ignored when the index is out of range or the region is already
  degraded or down.
- `RecoverRegion(index)` brings a region back. It is ignored when the index is out of range or the region is already
  healthy or recovering.
- `PrimaryRegion` is the index of the region whose database is primary, and `TrafficRegion` is the index of the region
  that receives traffic. Both are read-only.
- `PrimaryRegionChanged` is raised with the index of the new primary after a promotion. It is not raised when a
  region that holds no primary role fails.

<Demo name="DriveItYourself" />

```csharp
var failover = new MultiRegionFailover { IsAutomatic = false };
var regionNames = new[] { "us-east-1", "eu-west-1", "ap-southeast-1" };

failover.PrimaryRegionChanged += (_, index) =>
    primaryLabel.Text = $"Primary: {regionNames[index]}";

failButton.Click += (_, _) => failover.FailRegion(0);
recoverButton.Click += (_, _) => failover.RecoverRegion(0);
```

The failure is a sequence, not a single change. A failed region shows a degraded state for 0.8 seconds, then a down
state. If it held traffic or the primary, the load balancer switches to the next healthy region 0.5 seconds later, and
if it held the primary database, the next healthy region's database is promoted 0.7 seconds after that. The failover
time readout counts from the failure to the traffic switch. The timings are scene time, so a paused illustration
finishes the sequence when it plays again.

Use the controls to rehearse a failure. Name them after the action and the region, such as "Fail us-east-1" and
"Recover us-east-1", so each button is clear to a screen reader and to the operator reading the page.

### Labels

`Labels` takes a `MultiRegionFailoverLabels` object with every visible word of the diagram: the node names and details,
the health states, the badges and the step captions. Set only the properties you need, and keep the wording of the
step captions consistent with the runbook, because the caption is the only explanation a reader sees while the story
runs.

```csharp
failover.Labels = new MultiRegionFailoverLabels
{
    Balancer = "Global LB",
    FailingOver = "Switching",
    Promoted = "Now primary"
};
```

### Playback

`IsPlaying`, `Speed` and `Restart()` control the story in the same way as on the other illustrations. `Speed` takes
values from 0.25 to 4. The automatic story has six steps, and `StepChanged` reports each one:

| Step | Caption | When it happens (automatic story) |
|---|---|---|
| 0 | Traffic is served from the planned region; data replicates to the standby regions | Start |
| 1 | A region fails its health checks | At 3.0 s, when the region fails |
| 2 | The load balancer shifts traffic to the next healthy region | About 1.3 s after the failure |
| 3 | The standby database is promoted to primary | About 2.0 s after the failure |
| 4 | The failed region recovers and catches up as a replica | At 8.6 s, when it recovers |
| 5 | A planned failback to the original region | At 11.8 s; the loop is 14.4 s long |

The story ends on a planned failback, which moves traffic and the primary back to the first region. The manual
controls do not fail back on their own; see the common questions below.

```xml
<arc:MultiRegionFailover Speed="2" />
```

### Reduced motion

Under reduced motion (or Off) the illustration is a still frame of the healthy deployment. Traffic is served from the
first region, the standby replication links are drawn dashed, and the caption summarizes the deployment. The play
button is hidden. Use the still frame on pages where the reader needs the topology but not the sequence.

<Demo name="ReducedMotion" />

### Driving it from code

The control is a demonstration of a state machine, not a reader of your infrastructure. Use it to show a failover
that has already happened, or one you want to rehearse, and keep the real state in your monitoring system. A reasonable
pattern is to keep the illustration on a page that explains the design and to drive it from the operator's own
controls, as in the sample above. Do not connect a health check feed directly to `FailRegion`: the control would then
animate a failure that the feed reports, without any of the checks, timeouts and approvals of a real failover.

### Example: payments platform reliability page

A reliability page for a billing service. The regions are the ones the service actually runs in, the balancer is
named with the short form the infrastructure team uses, and the database and application labels describe the roles in the
architecture. The caption explains what the reader sees while the story plays, so the page does not depend on the
animation alone.

![A three-region deployment with a global load balancer and a primary database in us-east-1](images/PaymentsRegions.png)

```xml
<StackPanel Width="760" Spacing="16">
    <TextBlock Text="How billing stays available"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Requests are served from the primary region. When it fails, traffic and the primary database move to the next healthy region."
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:MultiRegionFailover Label="Billing service failover across three regions">
        <arc:MultiRegionFailover.Labels>
            <arc:MultiRegionFailoverLabels Users="Customers"
                                           UsersDetail="worldwide"
                                           Balancer="Global LB"
                                           BalancerDetail="health checks"
                                           App="Billing API"
                                           AppDetail="3 instances"
                                           Database="Ledger DB"
                                           FailingOver="Failing over" />
        </arc:MultiRegionFailover.Labels>
    </arc:MultiRegionFailover>
</StackPanel>
```

The labels are short on purpose: the balancer card is narrower than the region cards, and a longer title is cut off
with an ellipsis. Check the screenshot after you change a label.

### Content guidelines

Name the regions as the infrastructure names them, and name the database roles with the words the team uses in its
runbook (primary, replica). Write the balancer as the component's job ("Global load balancer", "Traffic manager"),
not as a product name, unless the page is about that product. Keep the captions in the present tense, and describe the
state, not the team's response ("Traffic is served from eu-west-1", not "We moved traffic").

| Instead of | Write |
|---|---|
| Highly available across all regions | Traffic is served from eu-west-1 while us-east-1 is down |
| Region 1 | us-east-1 |
| Master DB | Primary database |
| Slave | Replica |
| Failover complete | Traffic is served from eu-west-1 |
| The system heals itself | The failed region recovers and catches up as a replica |

Do not promise a failover time in a label. The failover readout is the time the diagram took, and a customer-facing
claim must come from your own measurements, stated next to the diagram.

### Best practices

**Do**

- Show the same regions as your production deployment. A readout that names regions the service does not run in
  teaches the wrong topology, and a reader cannot tell the difference.
- Keep the failover order in the `Regions` list in the order of preference, and say so on the page, because the next
  healthy region in the list receives the traffic.
- Use `IsAutomatic="False"` with real buttons on runbook pages, so the operator decides when each step happens.
- Provide the state in text beside the diagram. Assistive technology does not read the caption changes, so the page
  must state the current situation in words.

**Avoid**

- Using the diagram as the status of a real service. A story that loops would show a failure that may not exist. Use
  `Alert` for real status.
- Labelling the readouts with measured values you have not measured. The failover time is scene time, not a
  benchmark.
- Leaving the automatic story on a page that explains a specific incident. The story loops and implies the incident is
  still happening.
- Connecting a live health feed to `FailRegion` or `RecoverRegion`. Those calls animate the state machine and do not
  move any real traffic, so the diagram can disagree with production.

### Common questions

**How do I stop the automatic story and drive the failure myself?** Set `IsAutomatic` to `False`. The scripted failure,
recovery and failback stop, and the state changes only when you call `FailRegion` or `RecoverRegion`. Changing
`IsAutomatic` rebuilds the diagram, so set it before the page shows the control.

**Why is the failover time not the time from my monitoring alarm?** The readout is the scene time between the call to
`FailRegion` and the traffic switch. It describes the diagram's sequence, which uses fixed delays (0.8 and 0.5 seconds),
and it is not a measurement of your system.

**Does recovery move traffic back?** No. `RecoverRegion` brings the region back as a replica and catches it up, but
traffic and the primary role stay where the failover put them. Only the automatic story performs a planned failback.
Traffic stays on the promoted region until that region fails.

**What happens when no other region is healthy?** The failed region keeps traffic and the primary. The failover
does not start, so `TrafficRegion` and `PrimaryRegion` do not change and `PrimaryRegionChanged` is not raised.

**Which regions does the replication follow?** Replication runs between neighbouring databases in the order of the
`Regions` list, one hop at a time. A standby beyond a region that is down receives nothing until that region
recovers, and a recovered region receives a short burst of catch-up changes from the primary.

## API reference

<!-- api: MultiRegionFailover -->

<!-- api: MultiRegionFailoverLabels -->

Failover events use the standard `EventHandler<int>` with the index of the new primary as the argument.

## Accessibility

- `Label` is the accessible name of the illustration. Name the deployment in it, for example "Billing service failover
  across three regions".
- The illustration takes focus and shows a 2px focus ring around the frame when it is focused.
- The current caption is exposed as the help text, so a screen reader hears what the step means when the control has
  focus.
- Caption changes are not announced, because the story loops and would repeat the same message without end.
- The region names, health states and badges are the text of the diagram. Keep them meaningful on their own, because
  a reader who cannot see the animation still gets the topology from the labels.
- The manual controls must be real buttons with their own names, such as "Fail us-east-1". A button labelled only
  "Fail" does not say which region it affects.
- Under reduced motion the still frame gives the healthy topology with its summary caption, so the state is clear
  without any movement.

### Keyboard

The illustration takes focus with Tab. Once it has focus, these keys control the automatic story:

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move focus to or from the illustration |
| `Space` or `Enter` | Play or pause the story |
| `R` | Start the story over from its healthy state |

The keyboard does not fail or recover a region. Provide buttons for that, and give them keyboard access in the same
way as the rest of the page.

## Motion

- The failing region turns amber, then red, and the packets in transit to it are dropped.
- The load balancer switches traffic to a healthy region with a "Failing over" badge, and the readout shows the time
  the switch took.
- The standby database is promoted with a "Promoted" badge, and replication runs from the new primary.
- Recovery shows the region catching up as a replica, followed by the planned failback in the automatic story.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A region fails | Frame turns warning, then danger; its packets fade out and its links dim | Shown in the steady state, which is healthy | Shown in the steady state |
| Traffic moves to another region | Traffic lines crossfade over 0.4 s; the failover badge shows during the switch | Not shown | Not shown |
| The database is promoted | "Promoted" badge pops in; the replication links run from the new primary | Not shown | Not shown |
| A region recovers | Frame returns to success after the catch-up burst | Not shown | Not shown |
| Play button | Visible; toggles playback | Hidden | Hidden |

## Related

- `PaymentFlow` for a single request that depends on the payment service being available.
- `DataFlow` for a pipeline that stores events in one warehouse.
- `Alert` for stating the status of a real service in text.

| Component | Choose it when |
|---|---|
| `Alert` | You state the status of a real service or incident in text |
| `Stepper` | The runbook steps are a sequence the operator moves through |
| `Timeline` | You show what happened during a real incident, in order and with times |
| `LineChart` / `Gauge` | You plot measured uptime, latency or error rates |
| `DataGrid` | You list the health of every region or every check in a table |
| `PaymentFlow` | The story is one payment request that depends on the service |
| `DataFlow` | The story is events moving from sources to a warehouse |
