---
title: Markdown
summary: Render Markdown documents and streamed replies in Arc's style, with fenced code highlighted in Arc's syntax palette.
---

<Demo name="Document" />

## When to use

- Release notes, help text and documents that are stored as Markdown.
- Streamed answers from a model or another service, shown while the text arrives.
- Rich text from a source you do not control, where headings, lists, tables and links must look consistent.

## When not to use

- A single block of code with a copy button: use `CodeBlock`.
- A long document that needs its own scrolling: wrap the control in a `ScrollViewer`, or use the StreamMark `TranscriptView` for long conversations.
- Editing Markdown source in place: use `Textarea`.

## Usage

Static text is set with `Text`. The document grows with its content and has no internal scrolling.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Markdown Text="## Release notes&#10;&#10;Arc **0.4** adds `Markdown`." />
</UserControl>
```

### Bound document

Bind `Text` to a view model property. Setting it replaces any reply that is still shown.

```xml
<arc:Markdown Text="{Binding ReleaseNotes}" FontSize="14" BlockSpacing="10" />
```

### Streamed reply

Call `Append` for each chunk as it arrives, and `Complete` when the reply ends. The text is paced and new words fade in.
`IsStreaming` is `true` until the paced backlog has been shown.

<Demo name="StreamedReply" />

```csharp
markdown.Append(chunk);   // as tokens arrive, on the UI thread
markdown.Complete();      // when the reply ends
```

`Append` starts a new reply after `Complete`. A stream you create yourself can be set on `Stream` instead, for example
one that a worker thread feeds with `MarkdownStream.Push`.

### Chat size

Use a smaller `FontSize` for message bubbles. `LineHeight` defaults to the font size times 1.65, and code follows the
font size.

<Demo name="ChatSize" />

### Links

Absolute `http`, `https` and `mailto` links open with the system launcher. Handle `LinkClicked` to take over, and mark
the event handled to stop the default. Set `OpensLinks="False"` to turn the default off.

```csharp
markdown.LinkClicked += (_, e) =>
{
    e.Handled = true;
    OpenInsideApp(e.Url);
};
```

## API reference

<!-- api: Markdown -->

## Accessibility

- Text can be selected and copied. Links are underlined and use the accent color.
- Code blocks use the same palette as `CodeBlock`. Color supports the source text and never replaces it.
- Use `IsStreaming` to show a status while a streamed reply is still open.

## Motion

- New text in a streamed reply fades in over 240 ms. Pacing spreads the text out so it does not arrive in bursts.
- Reduced motion shortens the fade to 160 ms and uses only opacity. Off shows new text at once without a fade.
- Pacing applies in every motion level. It controls when text appears, not an animation.

## Related

- `CodeBlock` for a single code sample on its own.
- `Textarea` when the user edits multi-line text.
