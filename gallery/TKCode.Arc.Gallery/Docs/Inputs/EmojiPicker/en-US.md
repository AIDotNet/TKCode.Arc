---
title: Emoji picker
summary: Find an emoji by name or browse every category, with recently used emoji first, a skin tone for people and hands, and a grid you can move through with the keyboard.
---

<Demo name="Preview" />

## When to use

`EmojiPicker` is a panel for choosing one emoji at a time. A search field at the top filters by name and keyword as the
person types; below it, a strip of category glyphs jumps through the Unicode groups, and the grid shows every emoji,
with the ones picked most recently in a section of their own at the top. A hand next to the search sets the skin tone
for every person and hand that has tones. The grid is fully keyboard operable, draws only the rows in view, and a line
at the bottom names the emoji under the pointer or the keyboard.

- **Reactions and replies in a chat or a comment thread**: open it from a smiley button next to the message field.
- **Status and profile fields**: a status message or a workspace name that takes one emoji in front.
- **Tagging and labels**: an emoji as the icon of a channel, a project or a saved view.

## When not to use

- **A fixed handful of reactions**: show those few as buttons; a full picker is a detour for a thumbs up.
- **Picking an icon for the interface**: emoji follow the platform's font and look different on every system; use an
  icon set when the result must look the same everywhere.
- **Typing text that may contain emoji**: the system's own emoji keyboard already works in every text field; offer the
  picker as a shortcut, not as the only way.

## Usage

Place the picker where the person can reach it, usually in a `Popover` next to a message field, and handle
`EmojiSelected`. `e.Emoji` is the text to insert, with the chosen skin tone applied.

```xml
<arc:EmojiPicker MaxRecent="16" />
```

```csharp
picker.EmojiSelected += (_, e) => message.Text += e.Emoji;
```

`EmojiPicker` lives in the Special package: register it with `UseArcSpecial()` next to `UseArcControls()`.

### In a popover

The picker brings its own raised panel. Inside a `Popover`, whose surface is already the panel, set `Background` to
`Transparent`, `BorderThickness` and `Padding` to 0. Focus goes to the search field when the popover opens, so the
person can type at once; close the popover in `EmojiSelected`.

<Demo name="InComposer" />

### Recently used

Every pick moves the emoji to the front of `Recent` (without its skin tone), up to `MaxRecent` (24). The section shows
the list as it was when the picker was last shown, so emoji do not move while the person is choosing. Fill `Recent` to
restore a saved list and watch its `CollectionChanged` to save it; set `MaxRecent` to 0 to hide the section.

```csharp
foreach (var emoji in settings.RecentEmoji)
{
    picker.Recent.Add(emoji);
}
picker.Recent.CollectionChanged += (_, _) => settings.RecentEmoji = picker.Recent.ToList();
```

### Skin tone

`SkinTone` applies one of five tones, or the default yellow, to every emoji that has tones; the others are unchanged.
It is two-way, so the hand in the picker and your own setting stay in step.

<Demo name="SkinTone" />

### Search

The search matches the start of words: "hea" finds every heart and "red hea" finds the red heart. Matches in the name
come first, then matches in the CLDR keywords ("happy", "lol"). `SearchText` is two-way, so a page can open the picker
with a query. Down moves from the search into the results and Enter picks the first match.

### Emoji versions

The emoji come from Unicode 16.0, but the glyphs come from the platform's emoji font. `MaxEmojiVersion` (15.0 by
default) leaves out newer emoji, so systems that lack them never show empty boxes. Raise it when you target recent
systems only.

### Anatomy

1. **Search** (`IsSearchVisible`): a search field and the skin tone hand.
2. **Categories**: one glyph per section; the bar under the section in view glides as the grid scrolls.
3. **Grid**: section titles (the one at the top stays pinned) and rows of 36px cells.
4. **Preview** (`IsPreviewVisible`): the emoji under the pointer or the keyboard, larger, with its name.

### Content guidelines

| Instead of | Write |
|---|---|
| Emoji | Add emoji (the trigger's accessible name) |
| Type to search for an emoji... | Search emoji |

## API reference

<!-- api: EmojiPicker -->

<!-- api: EmojiSelectedEventArgs -->

<!-- api: EmojiPickerLabels -->

The enums:

| Type | Values |
|---|---|
| `EmojiCategory` | `Recent`, `Smileys`, `People`, `Nature`, `Food`, `Travel`, `Activities`, `Objects`, `Symbols`, `Flags` |
| `EmojiSkinTone` | `Default`, `Light`, `MediumLight`, `Medium`, `MediumDark`, `Dark` |

## Accessibility

- The search field is named by `Labels.Search`; after each change it reports the number of matches ("12 emoji found")
  or that nothing matched.
- The category strip is one tab stop named after the focused category. Left and Right move between categories and jump
  the grid there; Home and End go to the first and last; Enter or Space jumps to the focused one.
- The grid is one tab stop. Its accessible name is the active emoji's name, and its help text says the section and the
  position ("Smileys and emotion, 3 of 168").
- The skin tone hand is a button named "Skin tone: Medium"; it opens into the six tones in a row.

### Keyboard

| Key | Where | Action |
|---|---|---|
| Typing | Search | Filters the grid |
| `Down` | Search | Moves into the grid |
| `Enter` | Search | Picks the first match |
| `Escape` | Search | Clears the search |
| Arrow keys | Grid | Move the active emoji, across section titles |
| `Home` / `End` | Grid | First or last emoji of the row |
| `Ctrl` + `Home` / `End` | Grid | First or last emoji of all |
| `Page Up` / `Page Down` | Grid | Move a screen of rows |
| `Enter` / `Space` | Grid | Pick the active emoji |
| Letters | Grid | Continue typing in the search |
| `Left` / `Right` | Skin tones (open) | Move between tones; `Enter` chooses, `Escape` closes |

## Motion

- Choosing a category scrolls the grid to it on a smooth spring; a wheel or a drag takes over at once.
- The bar under the categories glides to the section in view on a snappy spring.
- The skin tone row opens and closes with a width spring.
- While searching, the category strip fades to half opacity over 160ms.
- Under reduced motion the jumps and the bar move at once and the fade is 120ms; with motion off everything is instant.

## Responsive behavior

- The picker is 352px wide by default; the grid fits as many 36px columns as the width allows and centers them.
- The grid is 288px tall and scrolls; only the rows in view are drawn, and each emoji's text layout is made the first
  time it shows.
- Inside a narrow popover set `Width` to fit; the categories spread across whatever width there is.

## Related

- `Popover` to open the picker from a button.
- `MentionInput` and `TokenTextEditor` for text fields with inline tokens.

| Component | Choose it when |
|---|---|
| `EmojiPicker` | The person picks any emoji, by name or by browsing |
| `Popover` | You show a few fixed reactions of your own |
| `SegmentedControl` | One of a few emoji-like choices is a setting |
