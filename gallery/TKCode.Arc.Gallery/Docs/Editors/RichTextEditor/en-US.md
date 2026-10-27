---
title: Rich text editor
summary: A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.
---

<Demo name="Shortcuts" />

## When to use

`RichTextEditor` is the writing surface for content that needs light formatting: bold and italic text, headings,
lists, quotes and code blocks. It holds the document, the floating toolbar and the block menu, so a form needs no
extra layout around it. The content leaves the editor as clean HTML or Markdown, which the app stores and reads
back later.

- **Release notes and changelog drafts in a developer tool**: an engineer types `## ` for a heading and `- ` for a
  list, and the editor saves Markdown that the release pipeline publishes unchanged.
- **Comments and replies in a project tracker or a CRM note**: short notes with bold text, lists and quotes. The
  HTML output is safe to render because the editor keeps only its own tags.
- **Knowledge base or help center articles**: an author writes an article in the admin console and saves HTML that
  the public site renders, with headings and code blocks for steps and commands.
- **Incident write-ups and postmortems**: headings, quotes for customer-facing wording and code blocks for log lines,
  set `IsReadOnly` once the report is published so the text can be read and copied but not changed.
- **Announcements in a billing or account console**: a short message with a link to the invoice page, written by a
  finance or support user who needs formatting but not layout.

## When not to use

- **Plain multi-line text with no formatting**: use `Textarea`, which wraps and resizes without a toolbar or blocks.
- **Mentions of people or tags inside a text field**: use `MentionInput`, which suggests and stores the mentions.
- **A single line that is renamed in place on a page**: use `InlineEdit`, which looks like text until it is edited.
- **Source code or a read-only code listing**: use `CodeBlock`, which shows the code with its own copy action.
- **Layout-heavy documents**: tables hold inline text only (no nested blocks), and images sit in their own block. For page layout, columns or files that must be uploaded with progress, use a document editor or `FileDropzone`.

## Usage

Set `Markdown` for the initial content. `Markdown` and `Html` are both two-way: assigning either one reloads the
editor, and both follow every edit.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RichTextEditor Markdown="## Notes&#10;&#10;Type **Markdown** as you go, or `/` for blocks." />
</UserControl>
```

`RichTextEditor` derives from `TemplatedControl`, not from `TextBox`. It is a block editor: the document is a list of
blocks (paragraphs, headings, list items, quotes, code blocks and dividers), and each block lays out its own text
with its marks. That gives the editor its own undo history, its own clipboard rules and its own output, so the text
is not stored as a raw string that the control edits in place. It is not a `TextBox`, so it has no caret index or
`MaxLength`; read the text with `GetText()` and use the members listed in the API reference.

### Anatomy

1. **Writing surface**: the editable document, with a minimum height of 128 px. It draws the text, the list markers,
   the quote bar, the code background and the selection, and it places the caret.
2. **Placeholder** (`Placeholder`, default "Start writing"): shown in the muted text color while the document is
   empty.
3. **Block hint** (`BlockHint`, default "Type / for blocks"): shown on the empty line that holds the caret.
4. **Floating toolbar**: appears above a selection, with 32 px square buttons on a 16 px corner radius. It has the
   inline marks (bold, italic, strikethrough, inline code), a link button and the block types. Its link field
   replaces the buttons in place when a link is edited.
5. **Link field**: a one-line field in the toolbar that takes a URL for the selection or the link at the caret.
6. **Block menu**: the list that opens at a `/` and filters as you type. Its rows are 34 px high on a 12 px radius, in
   a panel with an 18 px radius.
7. **Live region**: a hidden element that announces results such as "Formatted" or "Link added" to assistive
   technology.

### Block types and marks

The document is built from blocks of one `RichTextBlockType` and inline marks from `RichTextMark`. The toolbar and
the block menu use these values with `ToggleMark`, `SetBlockType` and the `Formats` report.

| Type | Written as | Notes |
|---|---|---|
| `Paragraph` | Plain text | The default block |
| `Heading1`, `Heading2`, `Heading3` | `# `, `## `, `### ` | The `Arc` profile imports h4 to h6 as `Heading3` |
| `Heading4`, `Heading5`, `Heading6` | `#### `, `##### `, `###### ` | `Docs` profile only; `Heading6` is drawn in the secondary text color |
| `BulletedList`, `NumberedList` | `- `, `1. ` | Nest with Tab; a level holds the same list kind |
| `Quote` | `> ` | A block quote with a bar at its start |
| `CodeBlock` | Three backticks | Holds plain text only; lines do not wrap |
| `Divider` | `---` | A horizontal rule; `SetBlockType` inserts one in place of an empty block |

| Mark | Written as | Notes |
|---|---|---|
| `Bold` | `**text**` | |
| `Italic` | `_text_` or `*text*` | |
| `Strikethrough` | `~~text~~` | |
| `Code` | `` `text` `` | Kept within one block |

Links are not a mark in this list: `SetLink` and the link field attach an address to the selected text.

### Markdown shortcuts and the supported subset

The editor converts Markdown as the person types. A shortcut runs as soon as the typed text completes it, so the
person sees the result without a preview. Shortcuts do not run inside a code block.

| Type | Result |
|---|---|
| `# `, `## `, `### ` at the start of a line | Heading 1, 2 or 3 |
| `- `, `* `, `+ ` | Bulleted list item |
| `1. `, `1) ` | Numbered list item |
| `> ` | Quote |
| `---` | Divider |
| Three backticks | Code block |
| `**text**`, `_text_`, `~~text~~`, `` `text` `` | Bold, italic, strikethrough, inline code |

The editor writes and reads a fixed subset: paragraphs, three heading levels, nested bulleted and numbered lists,
quotes, code blocks, dividers, and the four inline marks and links. Pasted HTML is sanitized to this subset, so
unknown tags and styles disappear while their text is kept. Use the subset as the content contract for the feature:
a document that needs anything else must be stored elsewhere.

### The Docs profile

Set `Profile="Docs"` for a document-style editor. The default profile, `Arc`, stays exactly as described above; `Docs`
adds the features a longer document needs:

<Demo name="DocsProfile" />

| Feature | How to use it | Written as |
|---|---|---|
| Underline | `Ctrl+U`, the toolbar button, `ToggleMark(RichTextMark.Underline)` | `<u>` in HTML |
| Superscript and subscript | `Ctrl+.` and `Ctrl+,`, the toolbar buttons, `ToggleMark(RichTextMark.Superscript)` or `ToggleMark(RichTextMark.Subscript)`; turning one on turns the other off | `<sup>`, `<sub>` |
| Text size | The toolbar's size button (small, default, large, extra large, relative to the block's text); `SetTextSize` | `<span data-size="large">` |
| Line spacing | The block menu's Line spacing entries (compact, default, relaxed, loose); `SetLineSpacing` for the selected blocks. Code blocks and table cells keep their own | `data-spacing="relaxed"` on `p`, headings, list items, quotes and callout paragraphs |
| Headings 4 to 6 | `#### `, `##### ` or `###### ` at the start of a line, the slash menu, the block menu, `SetBlockType(RichTextBlockType.Heading4)` | `<h4>` to `<h6>`, `####` to `######` in Markdown |
| Text and highlight color | The toolbar's color button opens a palette of seven colors; `SetTextColor`, `SetHighlight` | `<span data-color="red">`, `<mark data-bg="yellow">` |
| To-do list | `[] ` or `[x] ` at the start of a line, the slash menu, `SetBlockType(RichTextBlockType.TodoList)`; click the checkbox to check it | `<ul data-todo="true"><li data-checked="true">`, `- [x] ` in Markdown |
| Toggle list | The slash menu or the block menu, `SetBlockType(RichTextBlockType.Toggle)`. Click the arrow, press `Ctrl+Enter` or call `ToggleFold()` to fold away the items nested under it; Enter at the end of an open toggle starts a nested item, and `Tab` nests any list item under the toggle above it. Folded items take no room, the caret skips them, and an edit that reaches into them (indenting under the toggle, Delete at its end) opens it | `<ul data-toggle="true"><li data-collapsed="true">`; a plain `- ` item in Markdown |
| Text alignment | `Ctrl+Shift+L`, `Ctrl+Shift+E`, `Ctrl+Shift+R`; `SetAlign` | `data-align="center"` or `"end"` on `p`, headings and quotes |
| `Ctrl+Shift+Up`, `Ctrl+Shift+Down` | Move the block that holds the caret up or down (`Docs` profile) |
| `Ctrl+Enter` on a toggle item | Fold or unfold it (`Docs` profile) |
| Code language | The label above a code block opens a list of languages; `SetCodeLanguage("csharp")`. The code is highlighted in that language (Plain text, or a language without a grammar, stays plain) | `<pre data-lang="csharp">`, a fenced block with an info string in Markdown |
| Callout | The `/` menu or the block menu; the block menu sets its color and icon; `SetCalloutColor`, `SetCalloutIcon`. Enter continues the box, an empty line leaves it | `<aside data-callout="blue" data-icon="💡">`, a GitHub alert (`> [!NOTE]`) in Markdown, which keeps only the five alert kinds |

The colors come from a fixed palette (`RichTextColor`: gray, red, orange, yellow, green, blue and purple) that follows
the theme in light and dark mode, so stored content never holds a raw color value. `Formats` also reports
`IsUnderline`, `IsSuperscript`, `IsSubscript`, `TextColor`, `Highlight`, `Size`, `Align`, `LineSpacing` and `CodeLanguage` for your own toolbar.
A line is as tall as the largest text on it needs, so a large word opens up its whole block.

```xml
<arc:RichTextEditor Profile="Docs" Html="{Binding Body, Mode=TwoWay}" />
```

- **Markdown is lossy for color, size and spacing.** Markdown has none of them, so `GetMarkdown()` keeps the text and drops them. Underline,
  superscript and subscript have no Markdown syntax either; they are written as inline `<u>`, `<sup>` and `<sub>` tags,
  which the `Docs` profile reads back. Store `Html` when the content must keep its colors.
- **Assign `Profile` first or in any order.** Content assigned before the profile is read again under the Docs rules.
- **Your own languages.** The language list ends with a "Custom…" row that takes any name, and `CodeLanguages` replaces the list with your own labels and ids, such as `new RichTextCodeLanguage("Zig", "zig")`. Highlighting needs a grammar for the id; a name without one is kept and shown, and the code stays plain.
- **Task lists from elsewhere import.** Markdown `- [ ]` and `- [x]` items and HTML lists whose items begin with a
  checkbox input become to-do items; in the `Arc` profile they stay plain list items.
- **Nothing else changes.** Dividers, quotes, links, the slash menu and the undo history behave as in the `Arc`
  profile, and a document written by the `Arc` profile loads unchanged.

### Images and tables

The `Docs` profile also stores images and tables. An image is one block that holds an address and a description; a
table is a grid of cells; a new one starts with a header row. Both come in and go out through Markdown (`![alt](src)` on
its own line, pipe tables) and HTML (`img`, `table`). The `Arc` profile keeps both out of the document.

<Demo name="ImagesAndTables" />

| Feature | How to use it |
|---|---|
| Insert an image | `/` then Image (a file picker), paste an image file or a screenshot, drop image files on the editor, paste a bare image address, type `![alt](address)`, or call `InsertImage(address, alt)` |
| Resize an image | Click it, then drag the grip at its bottom-right corner; the proportions stay, and the width is stored (`width` in HTML; Markdown writes a resized image as an `<img>` tag). |
| Select and remove an image | Click it, then press Backspace or Delete; Backspace at the start of the next paragraph removes it too |
| Insert a table | `/` then Table (3 by 3), or `InsertTable(rows, columns)` |
| Move in a table | Tab and Shift+Tab move between cells (Tab in the last cell adds a row); Enter moves down the column; Up and Down stay in the column |
| Edit rows and columns | Right-click a cell, or call `AddTableRow`, `AddTableColumn`, `DeleteTableRow`, `DeleteTableColumn`, `DeleteTable` |
| Merge and split cells | Drag across the cells, right-click and choose Merge cells, or call `MergeTableCells`; right-click a merged cell and choose Split cell, or call `SplitTableCell`. `CanMergeTableCells` and `CanSplitTableCell` tell your own toolbar whether either applies |
| Resize columns | Drag the border between two columns; double-click it, choose Distribute columns or call `DistributeTableColumns` to share the width evenly again |
| Header row and column | Right-click and toggle Header row or Header column, or call `SetTableHeaderRow` and `SetTableHeaderColumn`; `TableHasHeaderRow` and `TableHasHeaderColumn` read them |
| Color cells | Select the cells, right-click and choose a color, or call `SetTableCellColor`; `TableCellColor` reads the shared color |
| Select cells, rows and columns | Drag from one cell to another to select the rectangle between them; click a grip (top of a column, left of a row) or call `SelectTableRow` and `SelectTableColumn` |
| Move rows and columns | Drag a grip to a new place, right-click and choose Move row up, Move row down, Move column left or Move column right, or call `MoveTableRow` and `MoveTableColumn` (`CanMoveTableRow` and `CanMoveTableColumn` tell your toolbar whether they can) |
| Copy and paste cells | Copy a rectangle to get tab-separated text, which spreadsheets read; paste a table or tab-separated text into a cell to fill cells from there, adding rows and columns as needed |
| Align a column | `Ctrl+Shift+L`, `Ctrl+Shift+E`, `Ctrl+Shift+R` or `SetAlign` aligns the whole column of the caret's cell |

**You decide where pictures live.** The editor never uploads anything. When a person picks, pastes or drops a picture,
it raises `ImageSourceRequested` with the file name, the media type and the bytes (`OpenReadAsync`,
`ReadAllBytesAsync`). Answer with `Source` when you already know the address, or with `SourceTask` while an upload is
in progress; the editor inserts the image when the task finishes, and a task that fails or returns null inserts
nothing. Set `Cancel` to refuse the picture. With no handler the picture is embedded as a `data:` address, which keeps
the document self-contained but large.

```csharp
editor.ImageSourceRequested += (_, e) =>
{
    e.SourceTask = UploadAsync(e.FileName, e.ContentType, e.OpenReadAsync());   // returns the CDN address
};
```

**You can also decide how pictures are loaded.** `ImageLoading` is raised when the editor needs the pixels of an
address. Set `Bitmap` or `StreamTask` to load it yourself, for example from a store that needs a token or from your own
scheme; with no answer the editor reads `data:`, http(s), `avares:` and local file addresses. `ReloadImages()` forgets
what was loaded.

- **Addresses are checked.** Script schemes and `data:` addresses that are not raster images are refused when
  content loads and when an image is inserted; your own schemes (`app://files/1`) pass.
- **Images shrink to the editor's width** and keep their proportions. While a picture loads, or when it fails, a
  placeholder of the same width stands in.
- **Table cells hold inline text**: marks, links and line breaks (Shift+Enter), not lists or nested blocks. Imported
  cells with such content are flattened to lines. Short rows are padded.
- **Merged cells** span several rows and columns, and the texts they join become lines. HTML stores them as `colspan` and `rowspan`. Markdown has no merged cells, so it writes the covered cells empty; keep `Html` when merges must survive. Tab skips the covered cells, and Enter moves below the merged cell.
- **Column widths** are proportions: the table always fills the editor's width, and a column without a width shares what the others leave (at least 48 px). HTML stores widths as `<col width>` and cell colors as `data-bg`, and writes header cells as `th`; a `table` without `th` cells in its first row reads with no header row. Markdown keeps none of these, and its tables always have a header row.
- **A rectangle of selected cells** draws as a block. Deleting or typing empties those cells and never merges them, and pasting fills them from the first one; covered cells are skipped. A selection from the table's first character to its last, or one that runs out of the table, still deletes whole tables.
- **A merged cell holds its rows or columns together**: a row cannot move while a merged cell spans rows through it or across the place it would land, and the same goes for columns. The first row stays the header row whatever moves there.

### The block handle

In the `Docs` profile, hovering a block shows two buttons in a gutter to its left. The grip (⋮⋮) drags the block to a new
place: a line shows where it will land, and a list item takes its nested items along while a table moves whole. A click on
the grip opens the block menu, which turns the block into another type (text, headings, lists, to-do, quote, code) or
duplicates or deletes it. The plus button adds an empty paragraph below and opens the block menu there. From the keyboard,
`Ctrl+Shift+Up` and `Ctrl+Shift+Down` move the block that holds the caret. Every move is one undo step.

The handle is not shown in `IsReadOnly` mode or in the `Arc` profile, which keeps the editor's text flush with its edge.
The gutter is a left margin of 56 px on the writing surface.

### Find and replace

`Ctrl+F` opens a find bar at the top right of the editor, with the selected text (a piece of one line) already in it. Every
match is highlighted as you type and the first one at or after the selection is selected; Enter and Shift+Enter (or `F3`
and `Shift+F3`) step through the rest, and the bar shows where you are ("2 of 5"). `Ctrl+H`, or the bar's replace button,
adds the replace row: Enter in it replaces the current match and moves to the next one, and Replace all changes every match
as one undo step. Escape closes the bar and returns to the text with the last match selected.

- **Matches stay inside one block.** A paragraph, list item, table cell or code block is searched on its own, so a phrase
  broken across two blocks is not found.
- **A replacement takes the style of the text it replaces**: the marks, color and link of the match's first character.
- **Case.** The search ignores case until you turn on Match case.
- **From code**: `OpenFind(replace)`, `CloseFind()`, `IsFindOpen`, `CountMatches(text, matchCase)` and
  `ReplaceAll(text, replacement, matchCase)`, which returns how many it replaced. A read-only editor finds but never replaces.

### Placeholder and block hint

`Placeholder` shows in an empty editor and says what the field is for. `BlockHint` shows on the empty line where the
caret sits and tells the person how to add structure.

<Demo name="Placeholder" />

Set both when the field is a comment or a note, so the empty state explains the next step. Keep the placeholder
short: it disappears as soon as text is typed, and the hint disappears with it.

### Read only

With `IsReadOnly` set, editing, the toolbar and the shortcuts are off. The text can still be selected and copied,
and assistive technology reads it.

<Demo name="ReadOnly" />

Use read-only mode for a published article, a postmortem or a signed note that people must be able to quote. Show
an edit action outside the editor that switches `IsReadOnly` back to `False`, so the change is deliberate.

### Content formats

Store the content in the format the consumer reads:

| Format | Read it with | Use it for |
|---|---|---|
| Markdown (`Markdown`, `GetMarkdown()`) | Your Markdown renderer or the editor | Editable text in a pipeline, diffs and version history |
| HTML (`Html`, `GetHtml()`) | A browser or a web view | Pages that render the content directly |
| Plain text (`GetText()`) | Search and previews | One line per block, without marks |

Both `Html` and `Markdown` are sanitized when they are loaded. `SetHtml` and `SetMarkdown` load the text as one undo
step, and assigning the `Html` or `Markdown` property reloads the document only when the value differs from the one
the editor last wrote, so a two-way binding does not reset the caret while the person types.

### Importing existing Markdown

The editor stores a fixed subset, so opening a file written elsewhere can change it. `RichTextEditorMarkdown.Analyze`
reports every construct the editor cannot store, with the line, the column and a reason, without converting anything.
`TrySetMarkdown` imports only a source that loses nothing and leaves the editor untouched when it refuses, so a host can
keep the original file, warn, or open a source editor first.

<Demo name="ImportCheck" />

```csharp
if (!editor.TrySetMarkdown(fileText, out var report))
{
    Warn($"{report.Issues.Count} constructs would be lost; the file is unchanged.");
    foreach (var issue in report.Issues.Where(i => i.Severity == RichTextImportSeverity.Lossy))
    {
        Warn($"Line {issue.Line}, column {issue.Column}: {issue.Reason}");
    }
}
```

| Reported as a loss | Why |
|---|---|
| Heading level 4 to 6 (`Arc` profile) | The `Arc` profile has three heading levels; they become Heading 3 |
| A code fence's info string | The editor does not store a code block's language |
| A tilde fence | The editor reads only backtick fences |
| A table | Kept by the `Docs` profile; in the `Arc` profile the cells become one paragraph |
| A setext heading | The editor reads only `#` headings |
| Indented code | The editor reads only fenced code |
| A list starting above 1 | The editor numbers lists from 1 |
| A hard line break, block HTML, an image inside a line of text (or any image in the `Arc` profile), a reference link, a footnote, a backslash escape | The editor keeps the source as text |

Markers written another way are reported as `RichTextImportSeverity.Normalized` and do not block the import: `*` and
`+` bullets, `1)` numbering, `__bold__`, `*italic*` and `***` dividers are written back as `-`, `1.`, `**`, `_` and
`---`. Everything in the supported subset produces no issue at all. The permissive path is unchanged: `Markdown`,
`SetMarkdown` and a pasted Markdown-looking text still convert as before, and only callers that opt in to
`TrySetMarkdown` get the gate.

### Reading the content

`ContentChanged` runs after every edit, undo, redo and load. Its `Value` is a `RichTextValue`, and the HTML, Markdown
and text are computed when they are first read. `GetHtml()`, `GetMarkdown()` and `GetText()` read the content at any
time and are always synchronous.

| Member of `RichTextValue` | Type | Meaning |
|---|---|---|
| `Html` | `string` | The content as clean HTML |
| `Markdown` | `string` | The content as Markdown |
| `Text` | `string` | Plain text, one line per block |
| `IsEmpty` | `bool` | True when the document has no text |

<Demo name="MarkdownAndHtml" />

```csharp
editor.ContentChanged += (_, e) =>
{
    draft.Markdown = e.Value.Markdown;
    canPost = !e.Value.IsEmpty;
};

var html = editor.GetHtml();
```

### Your own toolbar

`Formats` (a `RichTextFormats` record) reports the marks, the link and the block type at the caret. `CanUndo` and
`CanRedo` report the history. Use them to set the state of your own buttons, and call `ToggleMark`, `SetBlockType`,
`SetLink` and `EditLink` to act on the selection.

Bind `UndoCommand` and `RedoCommand` to buttons, or call `Undo()` and `Redo()` directly. The toolbar buttons should
not take focus, so the editor keeps the caret; the demo sets `Focusable="False"` on each button for that reason.

<Demo name="OwnToolbar" />

### Binding to a view model

In an MVVM app the view model owns the document. Bind `Markdown` two-way, read `IsEmpty` from `ContentChanged`, and
use the undo and redo commands from the view.

```csharp
public partial class ReleaseNoteViewModel : ObservableObject
{
    private readonly IReleaseNotes _notes;

    [ObservableProperty]
    private string? _bodyMarkdown;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PublishCommand))]
    private bool _hasContent;

    public ReleaseNoteViewModel(IReleaseNotes notes) => _notes = notes;

    [RelayCommand(CanExecute = nameof(CanPublish))]
    private async Task PublishAsync()
    {
        await _notes.PublishAsync(BodyMarkdown ?? string.Empty);
    }

    private bool CanPublish() => HasContent;
}
```

```xml
<StackPanel Spacing="16">
    <arc:RichTextEditor x:Name="Editor"
                        Markdown="{Binding BodyMarkdown, Mode=TwoWay}"
                        Placeholder="Describe the release" />
    <arc:Button Content="Publish notes"
                Command="{Binding PublishCommand}"
                IsDefault="True" />
</StackPanel>
```

```csharp
// Code behind: the editor reports emptiness through ContentChanged; vm is the view's ReleaseNoteViewModel.
Editor.ContentChanged += (_, args) => vm.HasContent = !args.Value.IsEmpty;
```

- `Markdown` (and `Html`) is two-way: the view model receives every edit, and the editor reloads when the view model
  sets a different value. For a long document the value follows the typing after a short pause (see the performance
  section), so read `GetMarkdown()` in the publish command rather than trusting a cached copy.
- `Placeholder` and `BlockHint` are one-way; `IsReadOnly` is one-way and typically follows a published state.
- `UndoCommand` and `RedoCommand` are commands for your own toolbar. `CanUndo` and `CanRedo` are read-only and raise
  change notifications.
- `ContentChanged` is the event to listen to for emptiness, counts or autosave. Use `Value` inside the handler; the
  HTML and Markdown are computed the first time they are read.

### Undo and history

The editor keeps its own history of up to 300 steps, separate from the platform's undo. Typing within one second
merges into one step; a space or a change of input type starts a new one, and each command (a mark, a block type, a
link) is one step. `SetHtml`, `SetMarkdown` and `Clear` are one step each, so a load can be undone. Setting the
`Markdown` or `Html` property while the editor starts up does not enter the history.

### Long documents and performance

The editor redraws only the blocks that changed, and it draws only the part of the document in and near the
viewport. The HTML and Markdown values are written after every edit while the document has up to 400 blocks. Longer
documents write them once the person pauses for about 250 milliseconds, or when the editor loses focus or is
removed. In a 5,000-block document the serialization waits for the pause, so typing stays cheap.

`GetHtml()`, `GetMarkdown()` and `GetText()` always serialize the current document, so call them when you save and
not on every keystroke.

### Example: release notes draft

A release manager writes the notes for a version of a developer tool. The placeholder tells the author what goes
here, the body already has a heading, a list, a bold phrase and a quote for the known issue, and the publish button
sits beneath it at the default size. The text in the editor is the same Markdown the release pipeline stores.

![A release notes draft with a heading, bold and list text, and a quoted known issue](images/ReleaseNotes.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Release notes"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Border Padding="24,20"
            CornerRadius="26"
            BorderThickness="1"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}">
        <arc:RichTextEditor Markdown="## Acme Analytics 4.2&#10;&#10;- Dashboards load **twice as fast**&#10;- Export charts as _PNG_ from the menu&#10;&#10;> Known issue: CSV exports above 500 MB can time out." />
    </Border>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Discard" Variant="Secondary" />
        <arc:Button Content="Publish notes" />
    </StackPanel>
</StackPanel>
```

### Content guidelines

- **Placeholder**: name the content the field holds and what to write: "Describe the release", not "Enter text".
- **Block hint**: name the one gesture that matters, such as "Type / for blocks". Do not list every shortcut.
- **Buttons**: use a verb and an object in sentence case: "Publish notes", "Save draft". Avoid "Submit" for a note
  that is published, because the person expects the text to go live.
- **Link text**: write the words the person reads, and keep the URL in the link field, not in the sentence.
- **Headings**: keep them short and in sentence case, and use `Heading2` for a section inside a document with a
  title outside the editor.

| Instead of | Write |
|---|---|
| Enter text here | Describe the release |
| Click here | Read the migration guide |
| Submit | Publish notes |
| Type /// for blocks | Type / for blocks |

### Best practices

**Do**

- Set `Placeholder` and `BlockHint` for every empty field, so the first line explains the editor.
- Save `Markdown` when the content is edited again, and `Html` when another page only renders it. The two are equal
  for the supported subset, so pick one as the source of truth.
- Read the content with `GetMarkdown()` or `GetHtml()` in the save command. A handler on `ContentChanged` is right for
  emptiness and counts, not for every write.
- Switch to `IsReadOnly` for a published document, and offer an explicit edit action that turns it back on.
- Keep the toolbar buttons out of the tab order when they act on the selection, as the own-toolbar demo does, so the
  caret stays in the text.

**Avoid**

- Storing HTML from another editor and expecting its styling to survive. The loader keeps only the editor's subset,
  so unknown tags and inline styles are dropped and the layout changes.
- Using images and tables in the `Arc` profile. Only the `Docs` profile stores them, so the content loses them on
  the next save.
- Reading `GetHtml()` on every keystroke in a long document. Serialization on each key costs more than the edit.
- Replacing the document you are editing by assigning `Markdown` on each change. Use `SetMarkdown()` or `Clear()`,
  which are one undo step each, so the person can undo a replacement.
- Expecting screen reader users to navigate the text by character, word or line. Assistive technology reads the
  whole document as one value, so offer `GetText()` output for review when the text is long.

### Common questions

**Why does Ctrl+U (Cmd+U on macOS) do nothing?** In the default `Arc` profile underline is not part of the editor's subset. The key is
swallowed, so the text does not change, and the Markdown output has no underline syntax either. Set
`Profile="Docs"` to make `Ctrl+U` underline the selection.

**How do I add or change a link from code?** Call `SetLink(url)` to link the selection, or the link at the caret.
Pass `null` or an empty string to remove the link. `EditLink()` opens the same link field the toolbar uses, for the
selection or the link at the caret. Ctrl+K (Cmd+K on macOS) does the same from the keyboard.

**Why did my bound `Html` not reload when I set the same HTML again?** The editor reloads only when the value differs
from the one it last wrote. A two-way binding that echoes the editor's own output therefore does not move the caret.
To force a reload with identical content, call `SetHtml` or `SetMarkdown` instead.

**Why does a shortcut not convert text inside a code block?** Code blocks hold plain text only. Shortcuts such as
`# ` and `**` are off there, and Tab inserts two spaces instead of changing the list level.

**How do I start a new line inside a list item or a quote?** Press Shift+Enter for a line break in the same block.
Press Enter on an empty list item or quote to leave the list, and press Enter twice at the end of a code block to
leave it.

## API reference

<!-- api: RichTextEditor -->

## Accessibility

- The writing surface is a multi-line editable text control named "Editor" by default. Set
  `AutomationProperties.Name` to name it for your form, such as "Release notes body".
- Keyboard (Cmd on macOS): `Ctrl+B`, `Ctrl+I`, `Ctrl+Shift+X` and `Ctrl+E` apply marks, and `Ctrl+K` edits a link. `Ctrl+Z` and
  `Ctrl+Shift+Z` undo and redo. `Alt+F10` moves focus into the floating toolbar, and `Escape` returns it to the text.
- Editing results such as "Formatted", "Link added", "Link removed", "Undone", "Redone" and "Numbered list added" are
  announced politely, so they do not interrupt speech.
- The floating toolbar is a toolbar, and its format buttons are toggle buttons that report their state. Their
  tooltips show the keyboard shortcut.
- The block menu is a list. Moving through it announces the current item.
- Limitation: the editor does not expose character, word or line navigation to screen readers. They read the whole
  text as one value.

### Keyboard

| Keys | Action |
|---|---|
| `Ctrl+B`, `Ctrl+I` | Toggle bold, italic |
| `Ctrl+Shift+X` | Toggle strikethrough |
| `Ctrl+E` | Toggle inline code, within one block |
| `Ctrl+K` | Open the link field for the selection or the link at the caret; `Enter` applies it, `Escape` closes it |
| `Ctrl+F`, `Ctrl+H` | Open the find bar, or the find bar with its replace row |
| `F3`, `Shift+F3` | Next and previous match while the find bar is open (`Enter` and `Shift+Enter` in its search field) |
| `Ctrl+U` | Toggle underline (`Docs` profile) |
| `Ctrl+.`, `Ctrl+,` | Toggle superscript, subscript (`Docs` profile) |
| `Ctrl+Shift+L`, `Ctrl+Shift+E`, `Ctrl+Shift+R` | Align left, center, right (`Docs` profile) |
| `Ctrl+Z`, `Ctrl+Shift+Z` (or `Ctrl+Y`) | Undo, redo |
| `Ctrl+A` | Select all |
| `Enter` | New block; leaves an empty list item or quote; twice at the end of a code block leaves the code |
| `Shift+Enter` | Line break inside the block |
| `Tab`, `Shift+Tab` | Indent or outdent a list item; in a code block, `Tab` inserts two spaces |
| `Backspace` at the start of a block | Turns a heading, quote, code block or list item into a paragraph, or outdents it; after a divider, deletes the divider |
| `/` at the start of a line or after a space | Opens the block menu; typing filters it, `Up` and `Down` move, `Enter` or `Tab` choose, `Escape` closes |
| `Alt+F10` | Moves focus into the floating toolbar; `Left` and `Right` move between its buttons, `Escape` returns to the text |

Ctrl is Command on macOS and Control on other platforms. Word movement follows the platform's text navigation
(Option on macOS, Control elsewhere). `Ctrl+U` is swallowed in the `Arc` profile because it has no underline; the `Docs` profile underlines with it.

## Motion

- The floating toolbar glides to follow the selection with a small bounce. When it changes between the format buttons
  and the link field, its size morphs, and the two faces slide past each other in opposite directions.
- The block menu opens from the `/` position. Its highlight glides between rows, and its height changes on a smooth
  spring when the filter changes the list.
- Reduced motion moves and resizes the toolbar in one step and cross-fades its two faces. The block menu only fades
  in and out, without scaling or moving.

| Change | Full | Reduced | Off |
|---|---|---|---|
| The toolbar follows a new selection | Glides to the new position with a small bounce | Jumps to the position | Immediate |
| The toolbar changes to or from the link field | Size morphs, faces slide past each other | Instant position and size; faces cross-fade | Immediate |
| The block menu opens | Pops out from the `/` position | Fades in | Immediate |
| The block menu's highlight moves | Glides between rows | Moves in one step | Immediate |
| The menu's list changes while filtering | Height changes on a smooth spring | Immediate | Immediate |

## Related

- `Textarea` for plain text without formatting.
- `MentionInput` for text that mentions people or tags.
- `CodeBlock` for displaying code rather than writing it.
- `FileDropzone` for images and files that accompany the text.

| Component | Choose it when |
|---|---|
| `Textarea` | The text is plain and runs over several lines, with no bold, lists or blocks |
| `MentionInput` | The text mentions people, and the mention must be suggested and stored |
| `InlineEdit` | A single title or name is renamed where it is shown |
| `CodeBlock` | The code is displayed, copied or read, not written |
| `FileDropzone` | Images or files accompany the text and must be uploaded |
| `Input` | The value is one line in a form, such as a subject or a title |
