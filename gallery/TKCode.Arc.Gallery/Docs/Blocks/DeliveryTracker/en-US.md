---
title: Delivery tracker
summary: Where an order is: the estimated arrival, five status steps from ordered to delivered, a drawn route with the van on it, and the history of scans, newest first.
---

<Demo name="Preview" />

## When to use

`DeliveryTracker` answers "where is my order?". At the top are the order and the carrier and, large, when it will arrive
(or when it arrived). Five steps follow the parcel from ordered to delivered. A small map draws the route from the hub to
the address with the van on it, and the history lists every scan with its time and place, newest first.

- **Order pages** in a store or a marketplace.
- **Delivery notifications** that open into the full status.
- **Support tools** that need the scan history at a glance.

## When not to use

- **Turn-by-turn tracking on a real map**: use a map component with live coordinates; the drawing here is a diagram.
- **Several parcels**: list them and open one tracker at a time.
- **Workflows that are not deliveries**: use `Stepper` and `Timeline`.

## Usage

The block derives from Avalonia's `TemplatedControl`. Set `Stage`, `RouteProgress` (0 to 1), `EstimatedArrival` and
`ArrivalWindow` as text, and fill `Events` with `DeliveryEvent`s in any order.

```xml
<arc:DeliveryTracker OrderNumber="4821-0937" Carrier="Larkpost" Stage="Shipped" RouteProgress="0.46"
                     EstimatedArrival="Thursday, Nov 12" ArrivalWindow="2 – 6 pm" />
```

### Stages and the route

Changing `Stage` (or calling `Advance`) springs the steps forward, announces "Now: Out for delivery" and raises
`StageChanged`. Out for delivery makes the van pulse; Delivered draws it at the address. The route's shape comes from the
order number, so the same order always gets the same drawing. `IsDelayed` shows a Delayed pill until the parcel arrives,
and an event with `IsException` gets a warning dot.

<Demo name="Narrow" />

## API reference

<!-- api: DeliveryTracker -->

<!-- api: DeliveryEvent -->

## Accessibility

- The steps are named with the current stage and its position ("Shipped, step 3 of 5").
- The route is named with its two ends and how far along the van is.
- Each history row is named with its title, time and place.
- A polite live region announces each new stage.

## Motion

- The steps' fill springs to the new stage on the smooth spring, and the van glides along the route on the gentle spring.
- Out for delivery, a ring pulses out from the van every 1.6 s (a `LiveLoop`: only while visible, in full motion).
- **Reduced motion**: the fill and the van move at once and nothing pulses. **Off** is the same.

## Responsive behavior

- Below 720 px the route sits above the history instead of beside it.
- Step names wrap under their dots; the first and last stay aligned with the block's edges.

## Related

- `Stepper` for steps of any process and `Timeline` for any history.
- The Cart drawer and Checkout summary blocks for the steps before the order.
