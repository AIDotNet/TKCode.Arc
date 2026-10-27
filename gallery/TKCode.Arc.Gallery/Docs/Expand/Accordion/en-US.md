---
title: Accordion
summary: Progressively reveal supporting information in place.
---

<Demo name="Default" />

## When to use

`Accordion` is a column of question rows whose answers open in place. It suits content that a reader scans by its
heading and reads only where it matters: the page keeps its length and the reader keeps their place while one answer
opens and the others stay closed.

- **Help center and billing FAQs**: a page that answers "Can I change plans later?" or "How are refunds calculated?"
  for a SaaS product. One answer is open at a time, so the reader compares questions without leaving the page.
- **Settings pages with grouped explanations**: an admin console section such as "Data retention" or "Single sign-on"
  where each heading hides the consequences of a choice. The controls stay visible; the accordion holds the reasons.
- **Checkout and order review**: "Shipping address", "Payment method" and "Tax details" as collapsed rows, so the
  customer opens only the one they want to check before placing the order.
- **Developer documentation sidebars**: a list of "Rate limits", "Pagination" and "Webhook retries" beside an API
  reference, where each answer is a few short paragraphs and a code sample.

## When not to use

- **A single disclosure on its own**, such as a "Show plan details" panel on a pricing card: use `ExpandableCard`,
  which keeps the card's header visible and can grow sideways.
- **Sibling views of the same data**, such as "Overview", "Usage" and "Invoices" for one account: use `Tabs`, which
  shows one panel at a time and exposes the choices as a tab list.
- **One long paragraph** that only needs a "Read more" link, such as a product description or a review: use
  `ExpandableText`, which clamps the text to a number of lines.
- **Steps that must be completed in order**, such as a setup wizard: use `Stepper`, which shows progress and the
  current step.
- **Rows of records that people compare or sort**, such as invoices or usage by day: use `Table`, whose
  columns line up across rows.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
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

Each row is an `AccordionItem`. `Accordion` derives from Avalonia's `ItemsControl`, so the rows can also come from
`Items` or `ItemsSource`, and `AccordionItem` derives from `HeaderedContentControl`: `Header` is the question and
`Content` is the answer. The Arc additions are the open-row state (`ExpandedIndex`, `IsExpanded`), the single or
multiple mode, the size and the keyboard model between the questions. Clicking the open row closes it, and opening
another row closes the open one.

### Anatomy

1. **List rule**: a 1 px line above the first row in the border color. Each row draws its own 1 px rule below it,
   so the list has a rule above and between all of its rows.
2. **Question** (`Header`): the whole row is one button. In the Medium size it is at least 50 px tall, with 8 px of
   vertical padding and the small type size (`TypeSM`) at medium weight. In the Large size it is at least 76 px tall,
   with 20 px of vertical padding and the large type size (`TypeLG`).
3. **Chevron**: a 17 px down chevron that sits 20 px after the question in Medium and 32 px after it in Large. It
   is in the muted text color when the row is closed, changes to the foreground color when the row is open or the
   pointer is over the row, and turns 180 degrees when the row opens.
4. **Answer** (`Content`): the answer text in the secondary text color at the small size with a 1.4 line height in
   Medium. Medium answers keep 32 px of space on the right and 20 px below. Large answers use the base type size
   with a 1.65 line height, keep 48 px on the right and 24 px below, and wrap at about 62 characters (about 595 px).
5. **Row rule**: a 1 px line below each row in the border color.
6. **Focus ring**: a 2 px ring in the focus color, drawn 4 px outside the row horizontally and 2 px vertically, with a
   10 px corner radius. It appears only for keyboard focus.

Hover changes only the question text (to the secondary text color) and the chevron color. Nothing changes size.

### Sizes

`Size` sets the row size for every row of the accordion. `Medium` (the default) is the look for settings panels and
help drawers inside dense admin layouts. `Large` is for a page-level FAQ, where the questions are the headline and
the answers are read at body size.

```xml
<arc:Accordion Size="Large">
    <arc:AccordionItem Header="Can I cancel at any time?">
        Monthly plans stay active until the end of the period.
    </arc:AccordionItem>
</arc:Accordion>
```

Use one size per list. A Large list inside a narrow side panel pushes the answers into a long column, so switch to
Medium there. The size also applies to rows added later, because the accordion sets it on each row it prepares.

<Demo name="Large" />

### Data-driven rows

Set `ItemsSource`, `ItemTemplate` for the question and `ContentTemplate` for the answer. `ExpandedIndex = -1`
starts with every row closed, which is the usual choice for a FAQ, where no answer should be open before the reader
asks for one.

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

Give every data item the same two fields, a question and an answer. Both templates are applied to every row, open or
closed, so keep them light; the section on data shape explains how to load heavier content on demand.

### Single and multiple open rows

`ExpandMode` decides how many rows can be open. `Single` is the Arc default: one row is open at a time, opening
another closes the open one, and the open row can be closed so that no row is open. Use it when the reader is
choosing one answer to read, as in a FAQ.

`Multiple` lets rows open and close independently. Use it when the reader needs two answers side by side, such as
the limits of two plans, or when the rows are independent sections of one form-like page.

<Demo name="Multiple" />

In `Multiple` mode `ExpandedIndex` still exists. It holds the row that was opened last; when that row closes it moves
to the first row that is still open, or to `-1` when none is.

To start several rows open, set `IsExpanded="True"` on each `AccordionItem`. An item written open wins over the
`ExpandedIndex` default, so the two settings do not fight each other.

### Opening a row from code

`ExpandedIndex` and `IsExpanded` are two-way. Bind one to a view model property, or set them in code. `Toggle(int)`
opens or closes the row at an index, and `CollapseAll()` closes every row, which is the reset to use after a search
or a page change.

```csharp
accordion.ExpandedIndex = 2;
item.IsExpanded = false;

accordion.Toggle(0);
accordion.CollapseAll();
```

### Binding to a view model

A FAQ usually comes from the server and the open row belongs to the page's state. With CommunityToolkit.Mvvm, the view
model holds the question list and the open index, and the accordion binds to both.

```csharp
public interface IBillingApi
{
    Task<string[]> GetInvoiceRulesAsync(string question);
}

public sealed partial class FaqEntry : ObservableObject
{
    public FaqEntry(string question, string answer)
    {
        Question = question;
        Answer = answer;
    }

    public string Question { get; }

    public string Answer { get; }

    /// <summary>Detail rows, loaded the first time the row opens.</summary>
    [ObservableProperty]
    private string[]? _details;
}

public partial class BillingHelpViewModel : ObservableObject
{
    private readonly IBillingApi _billing;

    public BillingHelpViewModel(IBillingApi billing) => _billing = billing;

    [ObservableProperty]
    private int _openIndex = -1;

    [ObservableProperty]
    private ObservableCollection<FaqEntry> _entries = new()
    {
        new("Can I change plans later?", "Yes. Plan changes apply immediately and the difference is prorated on your next invoice."),
        new("How do refunds work?", "Annual plans can be refunded within 30 days. Monthly plans stay active until the end of the period.")
    };

    [RelayCommand]
    private void CollapseAll() => OpenIndex = -1;

    /// <summary>Loads the detail rows of one answer the first time its row opens.</summary>
    public async Task LoadDetailsAsync(FaqEntry entry)
    {
        if (entry.Details is null)
        {
            entry.Details = await _billing.GetInvoiceRulesAsync(entry.Question);
        }
    }
}
```

```xml
<StackPanel Width="640" Spacing="12">
    <Button Content="Collapse all" Command="{Binding CollapseAllCommand}" HorizontalAlignment="Right" />
    <arc:Accordion Size="Large"
                   ExpandedIndex="{Binding OpenIndex, Mode=TwoWay}"
                   ItemsSource="{Binding Entries}">
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
</StackPanel>
```

`ExpandedIndex` is the two-way property to bind: the reader's clicks write the index back to `OpenIndex`, and a
change to `OpenIndex` opens or closes the rows. Bind `ExpandMode` and `Size` one way, since they are set once per
page. Use `ExpandedChanged` on an `AccordionItem` (it bubbles, so one handler on the accordion sees every row) when
the view model needs to react to each open, for example to log which answer was read.

### Data shape and performance

Every row is an `AccordionItem` container with its own answer, and the answer stays mounted while the row is
closed. That is what lets a second click reverse an opening mid-flight, and it means the cost of a closed row is
the cost of its content. Keep answers to text and simple controls. When an answer needs data, such as the rows of a
small table, load it in the view model when the row opens instead of loading it up front for every row:

```csharp
// In the view's setup: one handler on the accordion receives every row's event.
accordion.AddHandler(AccordionItem.ExpandedChangedEvent, OnRowExpandedChanged);

private void OnRowExpandedChanged(object? sender, RoutedEventArgs e)
{
    if (e.Source is AccordionItem { IsExpanded: true, DataContext: FaqEntry entry })
    {
        _ = _billingHelp.LoadDetailsAsync(entry);
    }
}
```

A FAQ of a few dozen rows is a normal load for this control. For long lists, group the rows under headings, or filter
them in the view model before they reach the accordion, because each row is a container that is measured and
arranged with the rest of the list.

### Example: billing help center

A help panel on a billing page of a SaaS admin console. It opens with the first answer, because the first question
is the one most people arrive with. The questions are complete sentences that a customer would type, the answers
name the dates and amounts, and the list uses the Large size because it is the page's main content. The width of
680 leaves the answers a comfortable line length.

![A billing help center with the first answer open and three closed questions](images/BillingHelp.png)

```xml
<StackPanel Width="680" Spacing="16">
    <TextBlock Text="Billing questions"
               FontSize="20"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Accordion Size="Large" ExpandedIndex="0">
        <arc:AccordionItem Header="When is the Team plan charged?">
            Acme Analytics charges the Team plan on the first day of each billing period. Annual plans are
            charged once a year on the date the subscription started.
        </arc:AccordionItem>
        <arc:AccordionItem Header="Can I change plans later?">
            Yes. Upgrades apply at once, and the unused part of your current plan is credited on the next invoice.
        </arc:AccordionItem>
        <arc:AccordionItem Header="How do refunds work for annual plans?">
            Annual plans can be refunded within 30 days of the charge. Contact billing with the invoice ID,
            such as inv_2026_0412.
        </arc:AccordionItem>
        <arc:AccordionItem Header="Who can see invoices?">
            Owners and billing admins can open and download every invoice. Members see only the plan name.
        </arc:AccordionItem>
    </arc:Accordion>
</StackPanel>
```

### Content guidelines

- **Question**: a complete question in sentence case, ending with a question mark, written the way a customer
  would ask it. A question names the topic, so the reader can scan the list without opening it.
- **Answer**: the direct answer in the first sentence, then the detail. Name dates, amounts and limits with digits
  ("30 days", "$24 per seat"). Keep an answer to one or two short paragraphs; longer material belongs on a linked
  page.
- **Headers in a settings list**: a noun phrase that names the setting, with the consequence in the answer.
- **Length**: a question should fit on one line at the chosen size. A longer question reads as a sentence to skim
  and wraps in the Large size.

| Instead of | Write |
|---|---|
| Info | How refunds work for annual plans |
| Click to read | Can I change plans later? |
| Yes, you can do that anytime. | Yes. Plan changes apply immediately. |
| Billing | Who can see invoices? |
| Read the docs for more. | Owners and billing admins can download every invoice. |

### Best practices

**Do**

- Put the most useful question first and open it by default (`ExpandedIndex="0"`) when the page is a single topic,
  so the reader sees a real answer without a click.
- Start with `ExpandedIndex="-1"` for a long FAQ, so the page opens as a scannable list of questions.
- Keep one answer per question, so a row does not hide a second topic the reader might miss.
- Use `Multiple` only when the reader must compare answers. Two open rows are easy to compare; five start to
  compete for the screen.

**Avoid**

- Hiding a required step, such as a legal term or an error that needs action, behind a question. The reader may
  never open it, so show required text on the page.
- Mixing `Medium` and `Large` rows in one list. The two sizes differ in height and type, and the list looks
  inconsistent.
- Placing a form inside an answer. Fields inside a collapsed row are unreachable until the row opens, and a
  validation message that appears in a closed answer is missed. Put the form on the page.
- Loading heavy content for every row when the page opens. Load it when the row opens, as shown in
  "Data shape and performance".
- Using the accordion as navigation between pages. Rows toggle in place; use `Tabs` or a navigation list for
  separate views.

### Common questions

**How do I start with two rows open?** Set `IsExpanded="True"` on each `AccordionItem` that should be open. An item
written open wins over the `ExpandedIndex` default, so `ExpandedIndex` is not needed for those rows. Use `Multiple`
if the reader should be able to keep both open.

**Why does the open row close when I click it?** The open row closes on its own question, so the reader can collapse
everything. `ExpandedIndex` then becomes `-1`. To keep one row open at all times, set `OpenIndex` back to its index in
the view model when the index becomes `-1`, or accept the collapse as part of the design.

**How do I know which answer the reader opened?** Subscribe to `AccordionItem.ExpandedChangedEvent` on the
accordion, which bubbles from each row, and read `e.Source` as the `AccordionItem`. Its `DataContext` is the data
item when the rows come from `ItemsSource`.

**Can an answer contain a button or a link?** Yes. `Content` takes any control. A closed answer is inert: it cannot
be clicked or focused until its row opens.

## API reference

<!-- api: Accordion -->

<!-- api: AccordionItem -->

### Enum values

`AccordionExpandMode` sets how many rows can be open.

| Value | Meaning |
|---|---|
| `Single` | One row at a time; opening another closes it, and the open row can be closed (the Arc default) |
| `Multiple` | Rows open and close independently |

`AccordionSize` sets the row size.

| Value | Meaning |
|---|---|
| `Medium` | Questions at the small type size on 50 px rows (the default) |
| `Large` | Page-level FAQ rows: questions at the large type size on 76 px rows, answers at body size |

## Accessibility

- Each row trigger is a button that exposes the expand and collapse pattern, so assistive technology announces
  whether the row is open or closed and the state changes when the row does.
- Each question is a heading at level 3, so screen reader users can move from question to question by heading.
- Each row takes its accessible name from its `Header` when the header is a string.
- Each answer is labeled by its question. A closed answer leaves the accessibility tree, is not hit-testable and
  takes no keyboard focus.
- When the open row closes while focus is inside its answer, focus moves back to the question, so focus never
  disappears into a hidden panel.
- Reduced motion shows the open and closed states at once, with no change in what is announced or focusable.

### Keyboard

The question is a button, so the standard button keys apply, and the accordion adds movement between questions.

| Keys | Action |
|---|---|
| `Enter` / `Space` | Opens or closes the focused row |
| `Down Arrow` / `Up Arrow` | Moves focus to the next or previous question; wraps from the last question to the first and back |
| `Home` / `End` | Moves focus to the first or last question |
| `Tab` / `Shift+Tab` | Moves through the questions and the controls inside open answers; closed answers take no focus |

Arrow keys and `Home` / `End` only move focus; they never open or close a row. Disabled rows are skipped by the
arrow keys.

## Motion

- The row height springs open on a smooth spring that does not overshoot. A second click during the motion turns
  the row around from its current height rather than from either end.
- The answer settles 6 px down into place out of a 2 px blur as it opens. On close, the answer fades quickly while the
  panel closes.
- The chevron turns 180 degrees on a snappy spring as the row opens.
- Reduced and Off motion show the same end states in one step.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Row opens | Height springs open and the answer fades in on a standard tween | Shown open at once | Immediate |
| Row closes | Answer fades out quickly while the panel closes | Closed at once | Immediate |
| Answer settles | Moves 6 px into place out of a 2 px blur | Immediate | Immediate |
| Chevron turns | Turns 180 degrees on a snappy spring | Immediate | Immediate |
| Hover and focus | Question and chevron colors crossfade | Immediate | Immediate |

## Related

- `ExpandableCard` for one disclosure that grows a dense card.
- `ExpandableText` for a single long paragraph that is clamped to a few lines.
- `Tabs` for switching between views of the same content.

| Component | Choose it when |
|---|---|
| `ExpandableCard` | A single summary card hides details that open in place and can grow sideways |
| `ExpandableText` | One paragraph is clamped to a few lines with a "Read more" toggle |
| `Tabs` | The reader switches between sibling views of the same content |
| `Stepper` | The reader works through ordered steps with a visible position |
| `Card` | The content needs no disclosure and should stay visible |
| `Table` | The rows are records that people compare or sort |
