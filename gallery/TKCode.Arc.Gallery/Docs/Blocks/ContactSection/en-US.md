---
title: Contact section
summary: A validated contact form that morphs into a confirmation, support channels, and office cards with local times.
---

<Demo name="Preview" />

## When to use

A contact page has one job: get a person's question to the right people. `ContactSection` offers that in three layouts,
so use the one that matches how you want to be reached.

- **One way in**: `Form` asks for a name, an address, a topic and a message, checks them, and turns into a confirmation.
- **Several ways in**: `Channels` lists chat, email, sales and so on, beside a panel with the detail of the chosen one.
- **Real places**: `Offices` shows a card per office with its address, its live local time and whether it is open.

## When not to use

- **Collecting only an email address**: use `NewsletterSignup`.
- **A long form with several steps**: use `MultiStepForm`.
- **Questions you can answer on the page**: use `FaqSection` first, and link here from its contact row.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ContactSection Title="Talk to us" />
</UserControl>
```

```csharp
contact.SubmitHandler = async message =>
{
    var response = await http.PostAsJsonAsync("/api/contact", message);
    response.EnsureSuccessStatusCode();
};
```

`ContactSection` derives from `TemplatedControl`. **Set `SubmitHandler` in an app**: without it the form waits 1.1 seconds and
reports success, without sending anything. The empty `Channels` and `Offices` lists show sample data, so replace them as well.

### Layouts

| `Variant` | What it shows | Set |
|---|---|---|
| `Form` | The heading and two facts beside the form card | `SubmitHandler`, `Topics`, `ResponseNote`, `ContactEmail` |
| `Channels` | A tab list beside a detail panel | `Channels`, `SelectedChannel` |
| `Offices` | One card per office | `Offices` |

`Title` and `Description` replace the heading of any layout. Leave them unset and each layout shows its own.

### The form

A press of **Send message**, or `Enter` in the name or email field, checks the fields. The first wrong field takes focus, and
after that errors update as you type.

| Field | Rule |
|---|---|
| Name | Not empty |
| Work email | No spaces, one @, a dot in the domain and at least two characters after it |
| Message | At least 20 characters, at most 500, with a live count |

`SubmitHandler` then receives a `ContactMessage` with the trimmed values, while the fields are read only and the button
spins. When its task completes, the card springs into a confirmation that names the person and the topic, and focus moves to
its heading. If the task fails, the form stays with everything typed in it and a message under the fields.

<Demo name="FailedSend" />

Topics are a radio group of chips: the highlight glides to the choice, and the arrow keys move it. `Topics` replaces the
four defaults.

### Channels

Each `ContactChannel` has a `Value`, a `Label`, a line of `Meta` under it, a 24 by 24 `Icon` and a `Detail`, which can be text or
any control. `SelectedChannel` is two way and holds the `Value`. The panel springs to the height of each detail while the
faces slide in the direction of travel.

```csharp
contact.Variant = ContactSectionVariant.Channels;
contact.Channels.Add(new ContactChannel
{
    Value = "email",
    Label = "Email the team",
    Meta = "One working day",
    Icon = ArcIcons.Mail,
    Detail = "Attach files or a short recording. A person answers every message."
});
```

### Offices

Each `ContactOffice` has a `City`, a `TimeZone` id, `Address` lines, an optional `Email`, and `OpenHour` and `CloseHour` in local
24 hour time (9 and 18 by default). An office is open from Monday to Friday within those hours. The cards refresh every 15
seconds while they are on screen, and only this layout runs a timer. A time zone the system does not know shows `--:--`
and "Checking hours". Browser builds may need time zone data for this.

<Demo name="Offices" />

### Best practices

**Do**

- Say how long a reply takes, and keep the promise.
- Send errors back through the task, so that the person keeps what they typed.
- Write the open status for people: the card spells it out, it is not only a color.

**Avoid**

- Asking for more than you need. Each extra field loses some people.
- Shipping without `SubmitHandler`. The form looks like it worked and nothing is sent.

## API reference

<!-- api: ContactSection -->

<!-- api: ContactMessage -->

<!-- api: ContactChannel -->

<!-- api: ContactOffice -->

## Accessibility

- Fields have visible labels, and errors appear after the first attempt and update as you type. A failed send is announced at once.
- Focus moves to the first wrong field, to the confirmation's heading, and back to the name field on "Send another message".
- The topics and channels keep one tab stop each, and an office's open status is written out.

### Keyboard

| Keys | Action |
|---|---|
| Arrow keys | Move between topic chips or channels, wrapping at the ends |
| `Home` / `End` | First or last channel |
| `Enter` | Send the form from the name or email field |
| `Tab` / `Shift+Tab` | Move through the fields and actions |

## Motion

- The card springs from its old height to the new one (0.4 s, no bounce). The new face comes in 18 px out of a 4 px blur
  in 240 ms after 40 ms, and the old one leaves 12 px in 160 ms.
- The confirmation's check draws in 420 ms after 160 ms. The highlight of a topic or channel glides on the morph spring.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The card changes face | Height spring, slide and blur | Crossfade | At once |
| The highlight moves | Morph spring | At once | At once |
| The check draws | 420 ms | Shown at once | Shown at once |

## Responsive behavior

- `ContactSection` follows its own width, not the window's. The title scales from 28 px up to 52 px.
- At 820 px or less (`:stacked`) the form moves under its heading and the channel list becomes one row that scrolls sideways
  and hides its meta lines.
- At 520 px or less (`:compact`) the name and email stack, the send button fills the width and the padding tightens.
- Office cards fit as many columns of at least 240 px as they have room for.
- Touch: chips and buttons are at least 36 px tall, and no information depends on hover.

## Related

| Component | Choose it when |
|---|---|
| `ContactSection` | People need to reach you |
| `FaqSection` | The answer may already be on the page |
| `NewsletterSignup` | You only need an email address |
| `MultiStepForm` | The form has several steps |
