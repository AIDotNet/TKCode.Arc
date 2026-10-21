---
title: Accordion
summary: Progressively reveal supporting information in place.
---

<Demo name="Default" />

## When to use

- A list of questions and answers, such as an FAQ, where one answer is open at a time.
- Long content grouped under short headings, such as a settings or help page.
- Supporting detail that the reader can skip without losing their place on the page.

## When not to use

- A single disclosure on its own: use `ExpandableCard`.
- Switching between sibling views of the same content: use `Tabs`.
- A short block of text that only needs a "Read more" link: use `ExpandableText`.

## Usage

Each row is an `AccordionItem`. The `Header` is the question and the `Content` is the answer.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Accordion>
        <arc:AccordionItem Header="What is included in the free plan?">
            Unlimited projects, three collaborators and a week of version history.
        </arc:AccordionItem>
        <arc:AccordionItem Header="Can I change plans later?">
            Yes. Plan changes apply immediately and the difference is prorated on your next invoice.
        </arc:AccordionItem>
    </arc:Accordion>
</UserControl>
```

Clicking the open row closes it. Opening another row closes the open one.

### Data-driven rows

Set `ItemsSource`, `ItemTemplate` for the question and `ContentTemplate` for the answer. `ExpandedIndex = -1`
starts with every row closed.

<Demo name="Large" />

```xml
<arc:Accordion Size="Large"
               ExpandedIndex="-1"
               ItemsSource="{Binding Questions}">
    <arc:Accordion.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Question}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Accordion.ItemTemplate>
    <arc:Accordion.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Answer}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Accordion.ContentTemplate>
</arc:Accordion>
```

### Sizes

`Size` sets the row size. `Medium` uses 50 px rows with text at the small size. `Large` uses 76 px rows with larger
questions and body-size answers, which suits page-level FAQs.

```xml
<arc:Accordion Size="Large">
    <arc:AccordionItem Header="Can I cancel at any time?">
        Monthly plans stay active until the end of the period.
    </arc:AccordionItem>
</arc:Accordion>
```

### Multiple open rows

`ExpandMode="Multiple"` lets rows open independently. This is an addition to Arc; the Arc default is `Single`.

<Demo name="Multiple" />

### Opening a row from code

`ExpandedIndex` is two-way. Bind it to a view model property, or set it in code. `IsExpanded` on an
`AccordionItem` is also two-way.

```csharp
accordion.ExpandedIndex = 2;
item.IsExpanded = false;
```

## API reference

<!-- api: Accordion -->

<!-- api: AccordionItem -->

## Accessibility

- Each row trigger is a button with the expand and collapse pattern. Its state, expanded or collapsed, is announced.
- Each question is a heading, so screen reader users can jump between questions.
- Each answer is labeled by its question. A closed answer leaves the accessibility tree and takes no focus.
- Arrow Down and Arrow Up move focus between triggers and wrap at the ends. Home and End jump to the first and last trigger.
- Enter and Space toggle the focused row.

## Motion

- The panel height glides on a smooth spring that does not overshoot. Opening a row reveals the answer, and closing it
  hides the answer quickly.
- The answer slides down a few pixels out of a soft blur as it opens. A second click mid-flight reverses from where
  the animation is.
- The chevron turns 180 degrees when the row opens.
- Reduced and Off motion show the same end states in one step.

## Related

- `ExpandableCard` for one disclosure that grows a dense card.
- `ExpandableText` for a single long paragraph that is clamped to a few lines.
- `Tabs` for switching between views of the same content.
