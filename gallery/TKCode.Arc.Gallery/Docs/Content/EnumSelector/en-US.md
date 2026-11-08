---
title: Enum selector
summary: Pick from the members of an enum: radio options for a plain enum, and checkboxes that combine flags for a [Flags] enum, described at compile time without reflection.
---

<Demo name="Basic" />

## When to use

`EnumSelector` turns an enum into a visible set of choices. Point it at the enum and bind `SelectedEnum`; it lists the
members, shows their `[Description]` texts and keeps the value and the choices in step. A plain enum becomes a
`RadioGroup`, and a `[Flags]` enum becomes a set of `Checkbox`es whose checked members are combined bit by bit.

- **Settings pages**: a theme, a density or a default view that a view model already holds as an enum.
- **Permissions and capabilities**: a `[Flags]` enum such as read, write and execute, including a `None` member and
  composite members such as `ReadWrite`.
- **Filters and states**: a status or a priority picked from a short, fixed list.

## When not to use

- **Long lists**: more than about eight members take too much room; use `Select` or `Combobox`.
- **Choices that are not an enum**: options that come from data belong in `RadioGroup`, `ChipGroup` or `MultiSelect`.
- **Options with descriptions, prices or icons**: use `RadioCards` or a `RadioGroup` with `RadioOption`s.
- **An immediate on and off setting**: a single `bool` is a `Switch` or a `Checkbox`.

## Usage

Mark the enum with `[EnumMetadata]`. The generator that ships with TKCode.Arc.Foundation reads it at compile time and
registers its members, their `[Description]` (or `[Display(Name = ...)]`) texts and its `[Flags]` marker with
`EnumTypes` when the assembly loads. Nothing is reflected at run time, so the selector is safe for Native AOT and
trimming.

```csharp
using System.ComponentModel;
using TKCode.Arc;

[EnumMetadata]
public enum Priority
{
    [Description("Low")] Low,
    [Description("Medium")] Medium,
    [Description("High")] High,
    [Description("Critical")] Critical
}
```

Then set `EnumType` and bind `SelectedEnum`, which is two-way by default:

```xml
<arc:EnumSelector xmlns="https://github.com/avaloniaui"
                  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                  xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                  xmlns:demos="using:TKCode.Arc.Gallery.Demos"
                  Header="Priority"
                  EnumType="{x:Type demos:DemoPriority}"
                  SelectedEnum="{x:Static demos:DemoPriority.Medium}"
                  Columns="2" />
```

`EnumType` can be left out when `SelectedEnum` is set: the selector lists the type of the value. Members marked
`[Browsable(false)]` are not listed, and of two members with the same value only the first is. An enum that is not
marked (and not registered in code) lists nothing.

### Flags

A `[Flags]` enum is shown as checkboxes. Checking a member sets its bits and unchecking it clears them, so:

- checking a composite member such as `ReadWrite` checks `Read` and `Write`, and unchecking it clears both;
- a composite member whose bits are only partly set shows the dash (indeterminate); clicking it sets the rest;
- a zero member such as `None` is checked only when no bit is set, checking it clears every flag, and checking any
  other member unchecks it;
- bits that no listed member names are kept.

`byte`, `short`, `int`, `long` and their unsigned forms are all handled, including the sign bit and the top bit of a
`ulong`.

<Demo name="Flags" />

### Display modes

`DisplayMode` is `Auto` by default: radio options for a plain enum and checkboxes for a `[Flags]` enum.
`EffectiveDisplayMode` is the mode in use, and `HasFlags` says whether the enum is `[Flags]`.

- `Radio` shows radio options for any enum. A `[Flags]` enum then holds one member at a time, composites included.
- `Check` shows checkboxes for any enum. A plain enum still holds one member: checking a box unchecks the others, so the
  value is never a combination the enum does not define.

<Demo name="DisplayModes" />

### Display names

`UseDisplayNames` (true by default) shows each member's display text; set it to `False` to show the names in code. The
texts change at once and the choice is kept.

<Demo name="DisplayNames" />

To translate the texts, name a language catalog. The text of member `Low` of `Priority` is then the catalog member
`PriorityLow` (`KeyPrefix` plus the member name; the prefix is the enum's name by default), and it follows
`ArcLanguage.Set`. A member without a catalog entry keeps its `[Description]`, and the build warns (ARCF037).

```csharp
using TKCode.Arc;
using TKCode.Arc.Localization;

[LanguageCatalog]
public enum AppLangResourceKind
{
    PriorityLow,
    PriorityMedium,
    PriorityHigh,
    PriorityCritical
}

[EnumMetadata(LanguageCatalog = typeof(AppLangResourceKind))]
public enum Priority { Low, Medium, High, Critical }
```

### Layout

The options sit in a uniform grid. `Columns` sets the number of columns and `Rows` the number of rows; when only one
is set the other is computed, and with neither the options form one column. `RowSpacing` and `ColumnSpacing` set the
gaps (8 and 16 for radio options, 0 and 16 for checkboxes, whose 44 px hit areas already space them). For anything else,
give `ItemsPanel` a panel of your own, such as a `WrapPanel` that wraps to the width it is given.

<Demo name="Layout" />

An enum of another assembly, such as `DayOfWeek` above, is described by an assembly attribute in your project:

```csharp
[assembly: TKCode.Arc.EnumMetadata(typeof(System.DayOfWeek))]
```

### Binding to a view model

`SelectedEnum` is an `object?`, so it binds to a property of any enum type, nullable ones included. A choice writes
the property, and a new value from the view model updates the choices, combined flags included. Setting it to null
clears the choice. `SelectedEnumChanged` reports every change with the old and new values.

<Demo name="Binding" />

### Custom labels

`ItemTemplate` builds the label of each option from an `EnumSelectorItem`: `Text` is the shown text, `Name` the member
name, `Value` the enum value and `Member` the full description.

<Demo name="ItemTemplate" />

### Registering an enum in code

`EnumTypes.Register` accepts a hand-written description, for example of an enum from a library you cannot mark and do
not want to name in an assembly attribute:

```csharp
EnumTypes.Register(new EnumTypeInfo(typeof(Size), isFlags: false,
    toBits: static v => (ulong)(int)(Size)v,
    fromBits: static b => (Size)(int)b,
    members:
    [
        new EnumMemberInfo("Small", Size.Small, 0) { DisplayName = "S" },
        new EnumMemberInfo("Large", Size.Large, 1) { DisplayName = "L" }
    ]));
```

## API reference

<!-- api: EnumSelector -->

<!-- api: EnumSelectorItem -->

<!-- api: EnumSelectorDisplayMode -->

<!-- api: EnumSelectorChangedEventArgs -->

<!-- api: EnumMetadataAttribute -->

<!-- api: EnumTypes -->

## Accessibility

- The selector is a group; a string `Header` is its accessible name and the name of the radio group inside it.
- Radio options behave as `RadioGroup`: the group is one tab stop, and the arrow keys, Home and End move the choice.
- Checkboxes behave as `Checkbox`: each is a tab stop and Space toggles it; a partly set composite member reports the
  mixed state.

## Motion

- The selector adds no motion of its own: the radio highlight glides and the dot springs as in `RadioGroup`, and the
  checks draw and morph into dashes as in `Checkbox`.
- A value that arrives from a view model moves the choices the same way; with reduced or no motion they follow those
  controls' rules.

## Responsive behavior

- `EnumSelector` has no breakpoint. The uniform grid divides the width it is given equally between the columns, so a
  label longer than its column wraps.
- On a narrow screen, use fewer `Columns`, or a `WrapPanel` as `ItemsPanel` so the options flow onto new lines.
- Radio rows and checkbox hit areas keep their touch-friendly heights (44 px for checkboxes).

## Related

- `RadioGroup`: one choice from options you list yourself.
- `Checkbox`: a single binary choice.
- `ChipGroup`: filters picked as chips.
- `Select`: a long list of choices in a menu.
- `PropertyGrid`: edits enum properties along with the rest of an object, also from compile-time descriptions.
