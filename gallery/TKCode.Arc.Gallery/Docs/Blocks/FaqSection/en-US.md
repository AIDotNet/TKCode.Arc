---
title: FAQ section
summary: FAQs as an accordion, a topic rail, or a searchable list that highlights matches.
---

<Demo name="Preview" />

## When to use

Answer the same questions before people have to ask them. `FaqSection` takes a list of questions with plain text answers
and sets them in the layout that fits how many you have.

- **A short list on a pricing or product page**: `Accordion`, one centered column.
- **Questions that fall into topics**: `Columns`, a rail of topics with counts beside the questions of the chosen topic.
- **A long list people will search**: `Search`, a field that filters as you type and highlights what it found, even
  inside a closed answer.

## When not to use

- **Collapsible content that is not a question list**: use `Accordion`.
- **Answers that need code, images or long structure**: use a documentation page.
- **Thousands of questions**: the search runs in memory on every key press, which suits dozens.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:FaqSection Variant="Search" Title="Questions about billing">
        <arc:FaqSection.Items>
            <arc:FaqItem Question="Can I cancel at any time?" Answer="Yes. Your plan runs until the end of the period." />
            <arc:FaqItem Question="Do you offer refunds?" Answer="Annual plans can be refunded within 30 days." />
        </arc:FaqSection.Items>
    </arc:FaqSection>
</UserControl>
```

`FaqSection` derives from `TemplatedControl`. With an empty `Items` list it shows sample questions, so replace them before
shipping. `Answer` is plain text: search matches and highlights inside it.

### Layouts

| `Variant` | What it shows | Good for |
|---|---|---|
| `Accordion` | Heading, questions and the contact row in one centered column, at most 720 wide | Up to about ten questions |
| `Columns` | Heading, topics and the contact row beside the chosen topic's questions | Questions with a `Category` |
| `Search` | A search field and a live count above one filtered list | Long lists |

### Open questions

`OpenItems` holds the ids of the open questions and is two way. By default the first question is open and opening another
closes it. Set `IsMultiple` to allow several. An id is `FaqItem.Id`, or the question's text. Pass a stable `Id` if questions
may be reworded, so the state survives the edit.

```csharp
faq.OpenItems = ["refunds", "seats"];
```

<Demo name="OwnQuestions" />

### Topics and search

`SelectedCategory` is the chosen topic in `Columns`, and `Query` is the search text in `Search`. Both are two way. A question
with no `Category` falls under "General".

In `Search`, a question stays in the list when its text or its answer matches. A closed question that matches only in its
answer shows a short excerpt, so people can see why it is there. Matching ignores case and accents: "cafe" finds "Café".
When nothing matches, a line and a button that clears the search take the list's place. `TotalFormat`, `ResultsFormat`,
`OneResultFormat`, `NoResultsFormat` and the label properties translate this text.

<Demo name="Searchable" />

### The contact row

The row under the list offers a way to reach a person. `Contact` takes a `FaqContact` with `Label`, `Description` and either
`Href` or `Command`. A press raises `ContactInvoked`, runs the command, then opens the address unless a handler marked the
event handled. Set `IsContactVisible` to false to hide the row.

### Best practices

**Do**

- Write questions the way people ask them, and answer in the first sentence.
- Keep answers plain text, so search can read them.
- Use `Columns` only when each topic has at least two questions.

**Avoid**

- Important content that appears only in a closed answer.
- More than about five topics. The rail is for finding a place, not for browsing.

## API reference

<!-- api: FaqSection -->

<!-- api: FaqItem -->

<!-- api: FaqContact -->

<!-- api: FaqContactEventArgs -->

## Accessibility

- Each question is a button named by its text whose help text says whether it is expanded or collapsed.
- The search field has a real label, and the count line is announced politely as it changes.
- A filtered row leaves the Tab order while it is out of the list.

### Keyboard

| Keys | Action |
|---|---|
| `Up` / `Down` | Move between the questions of one list, wrapping at the ends |
| `Home` / `End` | First or last question |
| `Enter` / `Space` | Open or close the focused answer |
| `Tab` / `Shift+Tab` | Move through the questions, topics, field and contact row |

## Motion

- An answer opens on a height spring (0.4 s, no bounce) while its text comes down 6 px out of a 2 px blur in 160 ms. It
  closes in 120 ms. The plus turns 45 degrees on Arc's CSS spring in 580 ms.
- Changing topic moves the highlight on the morph spring, and the questions swap along the direction of travel: 16 px in,
  12 px out.
- A filtered row collapses on the same height spring, so the rest slide up.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Answer opens | Height spring, travel and blur | Height at once, no travel | At once |
| Topic changes | Slide and blur | Swap at once | Swap at once |
| Search filters | Rows collapse | Rows close at once | Rows close at once |

## Responsive behavior

- `FaqSection` follows its own width, not the window's. At 720 px and above the title takes the larger type size.
- At 760 px or less (`:narrow`) the rail moves above the questions and its topics run in one row that scrolls sideways.
- At 480 px or less (`:compact`) the padding tightens and answers use the full width.
- Touch: a question is at least 64 px tall, and hover only changes colors, so nothing depends on it.

## Related

| Component | Choose it when |
|---|---|
| `FaqSection` | A list of questions and plain text answers |
| `Accordion` | Collapsible content that is not a question list |
| `SearchField` | Search over something other than an FAQ |
| `ContactSection` | The next step is writing to you |
