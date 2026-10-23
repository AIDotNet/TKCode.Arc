---
title: Markdown
summary: Render Markdown documents and streamed replies in Arc's style, with fenced code highlighted in Arc's syntax palette.
---

<Demo name="Document" />

## When to use

`Markdown` renders text that an author wrote in Markdown: headings, lists, tables, quotes, links and fenced code. It
sits where a product shows formatted prose that it does not lay out by hand, such as release notes, help articles or a
model's answer. The control takes the Markdown source, parses it and draws it in Arc's type, color and spacing, so the
text in a panel, a chat bubble or a document view looks like the rest of the application.

- **Release notes and in-app help**: a "What's new" panel in Acme Analytics that shows the notes the content team writes
  as Markdown files. Headings, lists and links look the same in every release without a hand-built layout for each one.
- **Assistant replies in a support chat**: an answer that arrives token by token. Call `Append` for each chunk and
  `Complete` at the end, and the text fades in at the end of the reply while the earlier paragraphs stay put.
- **Developer documentation with code samples**: API guides where fenced blocks in C#, TypeScript, JSON or shell read
  with syntax colors from Arc's palette. Long blocks fold to 12 lines, so one sample does not push the rest of the page
  away.
- **Rich text from a source you do not control**: a ticket body, a CMS field or an imported note. Headings, tables and
  links stay consistent with the product, and links open in the system browser unless the app takes them over.
- **Message bubbles with a smaller type size**: a 14 px reply in a support thread, where the line height and the block
  spacing follow the font size so the bubble stays compact.

## When not to use

- **A single code sample with a copy button**: use `CodeBlock`, which is one block with its own header and actions.
  `Markdown` is for a document that mixes prose, lists and code.
- **A long document that needs its own scrolling**: wrap the control in a `ScrollViewer`. The document grows with its
  content and has no scrollbar of its own. For long conversations, use the transcript view of the StreamMark library.
- **Editing Markdown source in place**: use `Textarea`, which holds the raw text, wraps it and lets the person edit it.
  `Markdown` shows the rendered result only.
- **Structured records such as a table of invoices**: use `SortableDataTable` from the Tables group, which sorts and
  keeps the columns aligned as the rows change. A Markdown table is read-only and has no sorting.
- **Math, diagrams or other embedded formats**: `Markdown` renders text, code and tables. Add StreamMark's math or
  diagram plugins to your own `MarkdownServices` when you need them, instead of pasting images into the text.

## Usage

Static text is set with `Text`. The document grows with its content and has no internal scrolling.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Markdown Text="## Release notes&#10;&#10;Arc **0.4** adds `Markdown`." />
</UserControl>
```

`Markdown` derives from Avalonia's `TemplatedControl`. Its template is a frame around a StreamMark `MarkdownView`,
which does the parsing and the layout. Arc supplies the colors, fonts and radii through the `Md.*` resources of that
view, so the document follows the theme scope, light and dark mode and the accent. Because the document is a view and
not a text block, it selects, copies and wraps like any document, and it grows with its content.

### Anatomy

1. **Frame**: a transparent `Border`. The theme sets no border and no padding on it, and the document takes the full
   width of its container (`HorizontalAlignment` is `Stretch`).
2. **Body text**: 16 px in the body font with a line height of 26 px (the font size times 1.65). Lines wrap to the width
   the container gives them.
3. **Blocks**: paragraphs, lists, quotes, code and tables are separated by 12 px (`BlockSpacing`).
4. **Headings and list markers**: headings take the foreground color, and the document view sizes them. List markers
   take the muted text color, so the item text stays the strongest element.
5. **Links**: the strong accent color, underlined. They open in the system browser by default.
6. **Inline code**: the monospace font on the muted surface color.
7. **Fenced code**: the surface color with a border in the border color, the monospace font and the Arc syntax
   palette. A block longer than 12 lines folds, with a control to show every line. Code blocks offer copy, not download.
8. **Quotes**: a bar in the strong border color beside the quoted text.
9. **Tables**: a border in the border color, a header row on the muted surface color, and the cell text in the body
   font.
10. **Marks and selection**: marked text sits on the warning subtle color, and selected text on the accent subtle color.

### Bound document

Bind `Text` to a view model property. Setting it replaces any reply that is still shown, and a `null` or empty string
shows an empty document.

```xml
<arc:Markdown Text="{Binding ReleaseNotes}" FontSize="14" BlockSpacing="10" />
```

`FontSize`, `FontFamily`, `LineHeight` and `BlockSpacing` are the only typography knobs. Leave `LineHeight` at its
default (NaN), so the line height follows the font size. Set it only when a design needs an exact value.

### Binding to a view model

The view model owns the Markdown source and, for a reply, the stream. With CommunityToolkit.Mvvm, the text is an
`ObservableProperty` and the stream is another one.

```csharp
public sealed partial class ReleaseNotesViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _releaseNotes;

    [ObservableProperty]
    private MarkdownStream? _reply;
}
```

```xml
<arc:Markdown Text="{Binding ReleaseNotes}" />
<arc:Markdown Stream="{Binding Reply}" />
```

Keep both bindings one-way. The control itself sets `Text` to `null` and replaces the stream when `Append` starts a
reply, and a two-way binding would write those values back into the view model. `IsStreaming` is read-only and reports
the state of the stream on the control.

### Streamed reply

Call `Append` for each chunk as it arrives, and `Complete` when the reply ends. The text is paced and new words fade in
at the end of the document. `IsStreaming` is `true` until the paced backlog has been shown, so a status line can say
"Writing" until the reply is fully on screen rather than until the last chunk arrives.

<Demo name="StreamedReply" />

```csharp
markdown.Append(chunk);   // as tokens arrive, on the UI thread
markdown.Complete();      // when the reply ends
```

Three properties tune how appended text shows. `RevealFadeDuration` sets how long each new batch fades in at full motion (Arc's
standard duration when null); `RevealFadeInReducedMotion` turns the fade off at reduced motion, which suits a reply that grows
continuously; and `IsPaced` set to false shows every appended piece at once as one batch, for a host that has already paced the
text itself, such as in whole words on a fixed clock.

`Append` starts a new reply after `Complete`, and it replaces whatever is shown: `Text` reads `null` from then on. A
stream you create yourself can be set on `Stream` instead, for example one that a worker thread feeds with
`MarkdownStream.Push` and ends with `Complete`. The control applies the snapshots on the UI thread, so the worker
pushes text and never touches the controls.

### Chat size

Use a smaller `FontSize` for message bubbles. `LineHeight` defaults to the font size times 1.65, so a 14 px reply has a
23 px line height, and code follows the font size.

<Demo name="ChatSize" />

### Links

Absolute `http`, `https` and `mailto` links open with the system launcher. Handle `LinkClicked` to take over, and mark
the event handled to stop the default. Set `OpensLinks="False"` to turn the default off. Links that the link policy
marks as needing confirmation are not opened by the default handler, so confirm them in your own handler. Anchors that
point inside the document scroll to their target by themselves and do not raise `LinkClicked`.

```csharp
markdown.LinkClicked += (_, e) =>
{
    e.Handled = true;
    OpenInsideApp(e.Url);
};
```

### Host integration

A host that builds its own chrome around the document uses the public surface instead of walking the template. The
outline lists every heading with the point it scrolls to; `Find` highlights the matches and returns their count;
`FindNext` / `FindPrevious` move the current match; the selection methods copy what the reader selected.

<Demo name="Outline" />

```csharp
markdown.DocumentReady += (_, _) =>
{
    foreach (var item in markdown.GetOutline())
    {
        AddOutlineRow(item.Level, item.Text, () => markdown.ScrollToPoint(item.Target));
    }
};

var count = markdown.Find(query);      // all matches highlighted
markdown.FindNext();                   // current match moves and scrolls
```

| Member | Use it for |
|---|---|
| `GetOutline()` | The headings in reading order: level, text, id and target point |
| `ScrollToPoint`, `ScrollToBlock`, `ScrollToAnchor` | Moving to an outline item, a block or a heading anchor |
| `Find`, `FindNext`, `FindPrevious`, `FindAt`, `ClearSearch` | A find bar, with `MatchCount` and `CurrentMatchIndex` for its label |
| `SelectAll`, `GetSelectedText`, `GetSelectedMarkdown`, `CopySelectionAsync` | A copy action, with `HasSelection` for its enabled state |
| `IsDocumentReady`, `DocumentReady` | Knowing when the outline, the matches and the scroll targets are final |

Parsing happens on a worker thread, so the timing is part of the contract. Before the template is applied there is no
view: the outline is empty, the scroll methods return `false`, `Find` returns 0 and the selection is empty. A new
`Text` or `Stream` makes `IsDocumentReady` `false` until its snapshot is parsed and applied, then `DocumentReady` runs
once. A find query stays set, so a document that changes under a search runs it again. Applying a new template creates
a new view: the document is handed to it, `DocumentReady` runs again and the search is run again, but a selection does
not survive. Use the `DocumentReady` event rather than a timer when the host restores a scroll position after a load.

### Code highlighting

`MarkdownHighlightPlugin` highlights fenced code with the TextMate grammars that `CodeBlock` also uses, and paints each
role in Arc's syntax palette for light or dark mode. Comments are italic. A language without a grammar stays plain text,
so an unknown tag never fails the document. The plugin is part of the default services. Pass language names to its
constructor to load those grammars in the background before the first block asks for them.

```csharp
markdown.Services = new MarkdownServices
{
    Plugins = [new MarkdownHighlightPlugin("cs", "ts")],
};
```

Leave the plugin out of `Plugins` when a view must show code without colors, and the blocks are then plain text in the
monospace font.

### Services and precedence

The StreamMark services decide how the document is parsed and how code blocks behave. The control picks them in this
order: its own `Services` property, then the services that an ancestor sets with `MarkdownServices.SetServices`, then
`Markdown.DefaultServices`. Set services on the window or the page to give every document in a view the same plugins.
Plugins can change the Markdown dialect, so a change to the services parses the static text again.

`Markdown.DefaultServices` highlights code with `MarkdownHighlightPlugin`, copies code but does not offer a download,
and folds long blocks to 12 lines.

### Example: assistant reply in a support chat

A support assistant answers an Acme Analytics customer in a chat bubble. The reply uses a short list and a link to the
export page, and it is set at 14 px to match the rest of the thread. The bubble has a muted surface and a 16 px inner
padding. The reply is static here, so the screenshot shows the settled text, the state that a streamed reply reaches
once `IsStreaming` turns `false`.

![A support assistant reply with a short list and a link inside a chat bubble](images/ChatReply.png)

```xml
<Border Width="600"
        Padding="16"
        Background="{arc:ArcDesignTokenResource SurfaceMuted}"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}">
    <arc:Markdown FontSize="14"
                  Text="Your March export is ready. The file holds **1,250** invoices in `invoices_2026_03.csv`.&#10;&#10;- Paid: **1,012**, pending: **105**&#10;- Overdue: **86**, failed: **47**&#10;&#10;You can open the [export page](https://uiarc.dev) to download it." />
</Border>
```

### Content guidelines

Rendered text reads as a document, so write it as a document: short headings, complete sentences and links whose text
says where they go.

| Instead of | Write |
|---|---|
| `Here's the thing!` as a heading | `Changes in version 0.4` |
| `Click here` as a link | `Read the migration guide` |
| `Loading...` as the placeholder reply | Leave the reply empty and show a status with `IsStreaming` |
| A table with eight columns | Two or three columns, with the rest in a list under the table |
| A 60-line code sample in the middle of a chat reply | The three lines that matter and a link to the full sample |
| Bare URLs in the middle of a sentence | A link with the page name as its text |

Use sentence case for headings and keep them short enough to scan at a glance. Write code samples so that a
reader can copy them: complete statements, and the language in the fence (`cs`, `ts`, `json`, `xml`, `sh`), so the
highlighter can color them.

### Best practices

**Do**

- Set `FontSize` to match the surrounding text rather than leaving the default, so a document in a panel and a reply in
  a bubble do not show two different type sizes side by side.
- Show `IsStreaming` as a status and disable the send button while it is `true`, so a person does not send a second
  question before the first reply has finished.
- Keep one `MarkdownServices` for the application, set on the window, so every document highlights code the same way.
- Put long documents in a `ScrollViewer`, so the scrolling and the scrollbar come from one container.
- Handle `LinkClicked` when links must open inside the app or need confirmation, so the decision stays in your code.

**Avoid**

- Setting `Text` and `Stream` on one control. The value set last wins, and the other is cleared or ignored.
- Appending one character at a time. Each chunk is parsed and paced, so push chunks of a few words, as a model's token
  stream does.
- Using a fixed `LineHeight` in a multi-size layout. A fixed value does not follow the font size, so a 14 px reply and a
  16 px document drift apart.
- Relying on `Markdown` for a table people must sort or filter. A Markdown table has no sorting and no selection.
- Showing Markdown from a source you do not trust without a `LinkClicked` handler. Absolute links open in the system
  browser by default, so check the URL in your own handler first.

### Common questions

**Why does `Text` read `null` after my streamed reply?** `Append` starts a stream and sets `Text` to `null`, because the
reply replaces the static text. Set `Text` again to show static text, which also ends the stream.

**Why does my document not scroll?** `Markdown` grows with its content and has no internal scrollbar. Wrap it in a
`ScrollViewer`, which scrolls the whole document.

**How do I know when the reply is fully shown?** Read `IsStreaming`, or listen for its change in `PropertyChanged`. It
turns `false` after `Complete` once the paced backlog has been shown, which can be a moment after the last chunk.

**Why are long code blocks folded?** The default services fold a block to 12 lines and show a control to expand it,
which keeps a long sample from pushing the rest of a chat reply out of view. Set your own `CodeBlocks` options in
`MarkdownServices` to change the limit.

**Why does a link not open when I click it?** Only absolute `http`, `https` and `mailto` links open by default. A relative
link, a custom scheme or a link that needs confirmation is left for your `LinkClicked` handler to decide.

## API reference

<!-- api: Markdown -->

| Member | Type | Description |
|---|---|---|
| `Name` | `string` | The plugin's name, `arc-highlight` |
| `Highlighter` | `ISyntaxHighlighter` | The highlighter the plugin registers, shared by all documents |

The `MarkdownHighlightPlugin` constructor takes the language names to prewarm, such as `"cs"` and `"ts"`.

## Accessibility

- Text can be selected and copied. Links are underlined and use the accent color, so they are not told apart by color
  alone.
- Code blocks use the same palette as `CodeBlock`. Color supports the source text and never replaces it: every token
  keeps its characters, so the code reads the same in a screen reader or in plain copy.
- Use `IsStreaming` to show a status while a streamed reply is still open. A status line that says "Writing" until
  `IsStreaming` turns `false` tells people when the reply is complete.

### Keyboard

- `Markdown` takes no keyboard focus of its own and adds no key bindings. Selection and copy come from the document
  view.

## Motion

- New text in a streamed reply fades in over 240 ms. Pacing spreads the text out so it does not arrive in bursts.
- Reduced motion shortens the fade to 160 ms and uses only opacity. Off shows new text at once without a fade.
- Pacing applies in every motion level. It controls when text appears, not an animation.

| Change | Full | Reduced | Off |
|---|---|---|---|
| New text in a streamed reply | Fades in over 240 ms at the end of the document | Fades in over 160 ms, opacity only | Shown at once |
| Pacing of a streamed reply | Applied | Applied | Applied |

## Related

- `CodeBlock` for a single code sample on its own.
- `Textarea` when the user edits multi-line text.

| Component | Choose it when |
|---|---|
| `CodeBlock` | One code sample with its own copy action |
| `Textarea` | The person edits multi-line text, and the raw source is what matters |
| `SortableDataTable` | The records must be sorted, such as a table of invoices |
