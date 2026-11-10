---
title: AI inline edit
summary: Edit a passage with AI where it stands: select words, say what to change, and review the rewrite inside the paragraph as struck and added words, accepting or rejecting each change.
---

<Demo name="Preview" />

## When to use

`AiInlineEdit` lets people improve a piece of writing with AI without leaving it. They select a passage, press Edit with
AI (or Ctrl+K), and say what they want, or pick a quick instruction such as Make it shorter. The rewrite does not
replace the text at once: it shows inside the paragraph as word-level changes, removed words struck through and added
words tinted, each change with its own accept and reject buttons. Once every change is decided, or Accept all or Reject
all is pressed, the text updates and the edited passage stays selected for the next request.

- **Documents, emails, notes and descriptions** where people want help with wording but keep control of every word.
- **Review flows** where an AI suggestion must be checked before it lands.

## When not to use

- **Writing from scratch.** Use `AI composer` or a chat; there is nothing to compare against.
- **Changes across a whole document or many files.** Use `AI side panel`, which lists changes per section.
- **Plain spell checking.** Use the platform's spell checker.

## Usage

The block derives from Avalonia's `TemplatedControl`. `Text` is two-way and receives the edited text. The rewrite comes
from `RewriteHandler`; without one a simple built-in stand-in rewrites the sample text:

```xml
<arc:AiInlineEdit Text="{Binding Draft}" />
```

```csharp
editor.RewriteHandler = async (request, cancellation) =>
{
    // request.Selection is the passage, request.Instruction what to do, request.Text the whole text for context.
    return await model.RewriteAsync(request.Selection, request.Instruction, request.Text, cancellation);
};
editor.EditApplied += (_, e) => history.Push(e.OldText);
```

### Phases

`Phase` is `Idle` while people read and select, `Instructing` while the field is open, `Thinking` while the rewrite is
written and `Reviewing` while changes are decided. Spaces at the ends of a selection are left out of it. When the handler
throws, its message shows in the bar and the field stays open, so the person can try again. If the rewrite matches the
passage the bar says no changes were suggested.

### Reviewing changes

The rewrite is compared word by word with the passage. Each changed region is one change: the old words struck through
in the danger color, the new words tinted green, and a check and a cross after it. An accepted change shows its new words
underlined until the review ends; a rejected one goes back to the old words. `AcceptChange`, `RejectChange`,
`AcceptAll`, `RejectAll` and `Cancel` do the same from code.

<Demo name="Handler" />

## API reference

<!-- api: AiInlineEdit -->

<!-- api: AiInlineEditRequest -->

<!-- api: AiInlineEditLabels -->

## Accessibility

- The editor is a region named "Text with AI editing"; the text is selectable with the keyboard.
- **Ctrl+K** (or **Cmd+K**) opens the field for the selection; **Enter** sends; **Escape** cancels, or rejects what is left
  during a review.
- In a review, focus moves to the first open change; **Tab** walks through each change's accept and reject buttons,
  which are named after the change ("Accept: replace "utilize" with "use"").
- A polite live region says when changes are ready and what was applied.
- Removed and added words differ by strike-through and tint, not by color alone.

## Motion

- The change buttons tint on hover over 160ms.
- Panels of the bar switch at once; the send button shows a spinner while the rewrite is written.
- Reduced motion and Off apply color changes at once.

## Responsive behavior

- The editor is at most 680 wide and follows its container below that.
- The paragraph and the review wrap; the quick instructions wrap onto more lines.

## Related

- `AI side panel` for suggested changes across a document.
- `AI composer` for writing a new prompt.
- `Prompt studio`, which compares prompt versions word by word.
