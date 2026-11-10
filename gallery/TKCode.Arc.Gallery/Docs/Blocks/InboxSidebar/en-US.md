---
title: Inbox sidebar
summary: A mail sidebar with Compose, folders with unread counts, colored labels and a storage meter, which collapses to an icon rail.
---

<Demo name="Preview" />

## When to use

`InboxSidebar` is the left column of a mail or messaging app. At the top sit a button that folds the sidebar to an
icon rail, the mailbox name and a Compose button. Below come the folders, each with an icon and a count; a count of
unread mail makes the row stronger, while a plain count such as drafts does not. Labels follow under a heading that
folds, each with a colored dot from the theme's palette, and a "New label" row ends the list. The footer shows how
much storage is used and who is signed in. On the rail, the icons, dots, the pencil of Compose and the avatar stay,
and counts become small dots. It is built on `Sidebar`.

- **Email clients** and shared inboxes, where people move between folders and labels all day.
- **Support desks and ticket queues**, where the folders are queues and the labels are topics.
- **Any app with a compose action and filtered views**, such as a messaging tool or a review queue.

## When not to use

- **General app navigation without counts or a compose action**: use `Sidebar`.
- **Documentation**: use `DocsSidebar`, which has search and a version switcher.
- **A list of conversations**: put the messages in the page beside the sidebar.

## Usage

```csharp
mail.Folders.Add(new InboxSidebarFolder("inbox", "Inbox", ArcIcons.Inbox, 12));
mail.Folders.Add(new InboxSidebarFolder("drafts", "Drafts", ArcIcons.FileText, 3, isCountUnread: false));
mail.MailLabels.Add(new InboxSidebarLabel("clients", "Clients", InboxSidebarLabelTone.Series1, 4));
mail.ComposeRequested += (_, _) => OpenComposer();
mail.ItemInvoked += (_, e) => ShowFolder(e.Key);
```

Folders and labels are objects with properties: change `Count` and the row follows. Counts above 999 read "999+".
Without folders and labels the sidebar shows an invented mailbox.

<Demo name="OwnData" />

### The icon rail

`IsCollapsed` switches to the rail; the panel button does the same. `ResizeMode="Push"` moves the page as the width
springs, the default `Overlay` lets the sidebar slide over it.

<Demo name="Rail" />

## API reference

<!-- api: InboxSidebar -->

## Accessibility

- The sidebar is a navigation landmark named "Mail". The arrow keys move between rows; Enter or Space opens one.
- A folder with unread mail adds "unread" to its name. The panel button is named "Collapse sidebar" or "Expand
  sidebar", and Compose keeps its name on the rail, where it also shows a tooltip.

## Motion

- The width springs between the open sidebar and the rail while texts fade; the marker glides between rows; the labels
  fold on a height spring.
- Reduced motion fades the texts quickly without the springs; Off changes everything at once.

## Responsive behavior

- The open sidebar is 240 px wide and the rail 64 px. The list scrolls on its own and the footer stays at the bottom.
- On a narrow window, start on the rail (`IsCollapsed="True"`) or place the sidebar in a `Drawer`.

## Related

- `Sidebar`, which this block is built on, and `DocsSidebar`.
- `NotificationCenter` for a list of alerts and `SelectionToolbar` for bulk actions on selected messages.
