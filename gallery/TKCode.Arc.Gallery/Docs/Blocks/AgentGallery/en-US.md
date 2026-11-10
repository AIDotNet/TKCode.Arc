---
title: Agent gallery
summary: A browsable gallery of ready-made agents: category pills, a search that marks its hits, a sort switch and a responsive grid of cards, each opening a detail view with abilities, the access it needs and prompts to try, where it installs with progress or uninstalls.
---

<Demo name="Preview" />

## When to use

Agent gallery is the shop window of a product that offers ready-made agents. People browse by category, search by
name or task, sort by popularity, date or rating, and scan a grid of cards: a tinted mark, the name and maker, one line
on what the agent does, its rating and installs, two tags and an Install button. A card opens the agent's page in place
of the grid: a longer description, what it can do, what it needs access to (sensitive access called out with an icon
and a word), prompts to try, and the button that installs it with progress, then shows it as installed with a way to
remove it.

- **A catalog people choose from**: a dozen to a few hundred agents, templates or assistants that a workspace can add.
- **Install with a look first**: the detail view shows what an agent can reach before anyone presses Install.
- **A home for agents your own team builds**: give them a category and they sit beside the others.
- **Starting a chat from a choice**: the prompts in the detail view raise `PromptChosen` so you can open a chat with that agent.

## When not to use

- **Picking one model for a chat**: use `ModelPicker`; a gallery is for adding things, not for switching.
- **Managing what is already installed**: a settings list with switches fits better than a shop window.
- **Thousands of items**: the cards are pooled but not virtualized; past a few hundred, page the data or search on the server.
- **Building or editing an agent**: that is a form with instructions, tools and a test chat, not a gallery.

## Usage

`AgentGallery` derives from Avalonia's `TemplatedControl`. Without data it shows an invented catalog of fourteen agents
in six categories, so it can be dropped on a page as is. Give it your own `Agents` (a list of `AgentGalleryItem`) and
`Categories` (`AgentGalleryCategory` with a key and a label), and bind `InstalledIds`.

```xml
<arc:AgentGallery Agents="{Binding Catalog}"
                  Categories="{Binding Categories}"
                  InstalledIds="{Binding InstalledAgentIds}" />
```

```csharp
gallery.Agents = new[]
{
    new AgentGalleryItem
    {
        Id = "brief-writer", Name = "Brief Writer", Maker = "Your team", IsVerified = true,
        CategoryKey = "writing", Glyph = ArcIcons.PenLine, Tint = AgentGalleryTint.Series2,
        Pitch = "Writes a project brief from a short chat.",
        Rating = 4.7, RatingCount = 120, Installs = 2_400, AddedOn = new DateOnly(2026, 10, 1),
        Tags = ["Briefs", "Planning"],
        Abilities = ["Ask the questions a brief needs", "Write the brief in your template"],
        Permissions = [new AgentGalleryPermission("Read your project folder")],
        SamplePrompts = ["Start a brief for the spring launch"]
    }
};
```

### Anatomy

- **Header**: the title, a subtitle and the search field.
- **Category pills**: All and one pill per category; the selected pill's background glides between them.
- **Count and sort**: how many agents are shown, and a Popular, Newest and Top rated switch.
- **Card grid**: as many equal columns as fit at 248 px (at most four); each row is as tall as its tallest card.
- **Card**: the mark (the agent's glyph on a tile tinted from a theme color), name, maker with a check for verified
  makers, a Featured badge, the pitch (two lines at most), two tags, the rating and installs, and the install button.
- **Detail view**: Back, the large mark with the name, maker and a line of facts, the install actions, the description,
  "What it can do", "Needs access to" and "Try asking".

### Browsing and searching

The pills, the search and the sort combine: the search looks inside the chosen category and matches names, makers,
pitches, tags and category names, ignoring case; hits are marked in the name and the pitch. With no match, the grid
gives way to an empty state with **Clear search** (and **Show all categories** when a category is chosen).
`SelectedCategoryKey`, `SearchText`, `Sort` and `OpenAgentId` are two-way, so the view can be saved and restored.

<Demo name="Filtered" />

### Installing

The card's button and the detail view's button install the agent. Set `InstallAsync` to call your backend: while it runs
the button shows a spinner and "Installing"; when it finishes the id joins `InstalledIds` and `AgentInstalled` is raised.
If it throws, the card says **Retry**, the detail view shows the message and `InstallFailed` is raised. Without a handler
the gallery simulates a 1.2 second install. Installed agents show **Uninstall** in the detail view, which calls
`UninstallAsync` (or removes the id at once).

<Demo name="Install" />

```csharp
gallery.InstallAsync = async (agent, cancellation) =>
{
    await api.InstallAgentAsync(workspaceId, agent.Id, cancellation);
};
```

### Opening an agent

A click on a card, Enter or Space opens its detail view; `Open("agent-id")` does the same from code, and
`AgentOpened` reports it. Back, Escape or `CloseDetail()` returns to the grid and puts the focus back on the card.
Typing in the search while an agent is open also returns to the grid.

### Narrow containers

The gallery reacts to its own width. Below 600 px the search moves under the title, the paddings tighten and the grid
drops to the columns that fit.

<Demo name="Narrow" />

### Wording

All words come from `Labels` (`AgentGalleryLabels`); formatted texts are functions so the word order can change.
`Title` and `Subtitle` on the control win over the labels; an empty `Subtitle` hides the line.

### Content guidelines

- **Pitch**: one sentence that starts with a verb and says what the agent does ("Sorts new mail into folders").
- **Abilities**: three to five short sentences, each a thing a person can ask for.
- **Permissions**: name the data and the action ("Read your mail"), add why in the detail, and mark anything that sends,
  changes or deletes as sensitive.
- **Marks**: pick a glyph that describes the task and a tint from the theme; the gallery never shows brand colors.

## API reference

<!-- api: AgentGallery -->

<!-- api: AgentGalleryItem -->

<!-- api: AgentGalleryCategory -->

<!-- api: AgentGalleryPermission -->

### Events

| Event | Raised when | Arguments |
|---|---|---|
| `AgentOpened` | The detail view of an agent opens | `Agent` |
| `AgentInstalled` | An install finished | `Agent` |
| `AgentUninstalled` | An agent was removed | `Agent` |
| `InstallFailed` | `InstallAsync` threw | `Agent`, `Message` |
| `PromptChosen` | A prompt in "Try asking" was pressed | `Agent`, `Prompt` |

## Accessibility

- The gallery is a region; the title and the agent's name are headings, the detail sections are headings one level down.
- Each card is a button named by the agent, its maker and its install state ("Inbox Sorter by Tidewater Labs,
  Installed"), with the pitch as help text. The install button is named "Install Inbox Sorter".
- Only one card is a tab stop, followed by its install button; the arrow keys move between cards, so the page does not
  cost a Tab per card.
- A polite live region announces how many agents a filter or search shows, and when an install starts, finishes or fails.
- Sensitive permissions carry a warning icon and the word "Sensitive"; a failed install has an icon and a sentence. No state
  relies on color alone.
- After an install from the detail view the focus moves to Uninstall, and back to Install after removing.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Search, the selected category pill, the sort switch, the current card, its install button |
| `ArrowDown` in the search | Moves to the current card |
| `ArrowLeft` / `ArrowRight` on a card | Previous or next card |
| `ArrowUp` / `ArrowDown` on a card | The card above or below |
| `Home` / `End` on a card | First or last card |
| `Enter` or `Space` on a card | Opens its detail view |
| `Escape` in the detail view | Back to the grid, focus on the card |
| `Escape` in the search | Clears the search |

## Motion

- After a filter, search or sort the cards fade back in from 25% over 160 ms (opacity only, no movement).
- The detail view fades in and rises 8 px over 240 ms; leaving it shows the grid at once.
- Card hover tints the background and strengthens the border over 160 ms; the selected category glides on a spring.
- Install buttons swap their label quietly and show a spinner while the install runs.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Cards after a filter | Fade from 25% over 160 ms | Same fade | Immediate |
| Detail view opens | Fade and 8 px rise | Fade only, 120 ms | Immediate |
| Card hover | 160 ms tint | Immediate | Immediate |
| Category pill | Glides on a spring | Jumps | Immediate |

## Responsive behavior

- The gallery follows its own width. Columns: as many as fit at 248 px with 12 px gaps, up to four.
- Below 600 px (`:compact`) the search moves under the title and stretches, and the side paddings drop from 20 to 12.
- Category pills scroll sideways inside their bar when they do not fit; the sort switch scrolls inside itself too.
- In the detail view the install actions wrap under the name, and the text column stays at most 720 px wide.
- The frame is at most 1120 px wide and centered; give it the page's width and let it grow down.

## Related

- `ModelPicker` for choosing the model an agent uses.
- `AgentMemory` for what an installed assistant remembers.
- `PromptStudio` for building and testing a prompt.

| Component | Choose it when |
|---|---|
| `ModelPicker` | People switch between a few models |
| `SearchResults` | The result list is documents, not things to install |
| `PlanComparison` | People compare a handful of plans side by side |
