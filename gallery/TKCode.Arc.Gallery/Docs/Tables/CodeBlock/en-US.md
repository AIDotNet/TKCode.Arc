---
title: Code block
summary: Present code with legible hierarchy and copy access.
---

<Demo name="Default" />

## When to use

- Source snippets and install commands in documentation or a settings page.
- Long files that should fold to a few lines, with a Show all action.
- Code that people copy, with a file name that tells them where it goes.

## When not to use

- Copying one value such as an API key: use `CopyButton` on its own.
- JSON that people explore by key: use `JsonViewer`, which shows a tree with search.
- Folders and file names as a navigable hierarchy: use `TreeView`.

## Usage

```xml
<arc:CodeBlock FileName="greeting.tsx"
               Language="tsx"
               Code="{Binding Source}" />
```

### Collapsing long code

`IsCollapsible` adds a toggle. `MaxLines` sets how many lines show while the block is collapsed.

<Demo name="Collapsible" />

`IsExpanded` is two-way and tells you whether the block is open.

```csharp
block.IsCollapsible = true;
block.MaxLines = 6;
block.IsExpanded = false;
```

### Language

`Language` sets the highlighting and the label in the header. It accepts common names and aliases such as
`cs`, `ts`, `py`, `sh` and `axaml`. A language without a grammar shows plain text.

<Demo name="Languages" />

```xml
<arc:CodeBlock FileName="stats.py" Language="py" Code="{Binding PythonSource}" />
<arc:CodeBlock FileName="notes.txt" Language="text" Code="Plain text." />
```

### Copy and line numbers

The copy button is on the header and copies `Code`. `ShowLineNumbers` adds a line number column that stays in
place when the code scrolls sideways.

<Demo name="LineNumbers" />

```xml
<arc:CodeBlock FileName="deploy.sh"
               Language="bash"
               ShowLineNumbers="True"
               Code="{Binding Script}" />
```

```csharp
block.Copied += (_, _) => status.Text = "Code copied";
```

### Labels

The copy label and the collapse text are properties, so you can translate them.

```xml
<arc:CodeBlock Code="{Binding Source}"
               CopyLabel="Copy snippet"
               ShowAllLinesFormat="Show all {0} lines"
               ShowFewerLinesLabel="Show fewer lines" />
```

### Switching files

Setting a new `FileName`, `Language` and `Code` swaps the file in place. The file name slot follows the new width, the
new source rises in and the height follows on a spring.

<Demo name="SwitchingFiles" />

### Narrow

Below 420px the padding and text shrink and the copy action keeps only its icon.

<Demo name="Narrow" />

## API reference

<!-- api: CodeBlock -->

## Accessibility

- The block is a labeled region. Its name comes from `FileName`, so each block on a page has its own name.
- Tab moves to the copy button, to the code area and to the expand toggle, in that order.
- Enter or Space copies the code or toggles the expanded state, depending on the focused control.
- The code area can take focus, so keyboard users can scroll long lines with the arrow keys.
- The expand toggle exposes its expanded state to assistive technology. The copy result is announced by `CopyButton`.

## Motion

- Expanding or collapsing changes the height of the code on a smooth spring.
- New code fades in and rises slightly, so an update is visible without a jump.
- The toggle label swaps with a short blur and its arrow turns.
- Reduced motion sets the height at once and swaps the label in place without movement. Off jumps straight to the final state.

## Related

- `CopyButton` to copy a single value, such as a token or a URL.
- `JsonViewer` for JSON that people search and explore.
- `TreeView` next to a block, so the selected file drives the code shown.
