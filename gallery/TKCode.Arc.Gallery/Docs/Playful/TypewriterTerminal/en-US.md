---
title: Typewriter terminal
summary: A terminal window that plays a script, typing each command at a person's pace and printing the output line by line, with a caret that blinks while it waits.
---

<Demo name="Preview" />

## When to use

`TypewriterTerminal` is a small terminal window that performs a script. Each command appears after a prompt and is
typed one character at a time, at a slightly uneven, human pace; after a short pause, as if Enter was pressed, its
output follows line by line. A block caret sits where typing happens and blinks whenever the terminal waits. The script
can play once or loop, and it waits until the window scrolls into view, so people see it from the start.

- **Install and quick-start sections** of a product or documentation page: show the commands and what they print.
- **Release notes and changelogs** for command-line tools, where a short session tells the story.
- **Hero illustrations** for developer tools that should feel alive without a video.

## When not to use

- **A real, interactive shell**: this plays a script and takes no input.
- **Code people will copy**: typed text is not selectable while it plays. Show the commands in a code block with a copy
  button too.
- **Long logs**: every line is drawn by the control. Keep the script to a few dozen lines.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TypewriterTerminal Title="~/projects/lantern">
        <arc:TypewriterTerminalLine Kind="Command" Text="lantern init my-site" />
        <arc:TypewriterTerminalLine Text="Creating a new site in ./my-site" Tone="Muted" />
        <arc:TypewriterTerminalLine Text="Ready in 2.3s" Tone="Success" />
    </arc:TypewriterTerminal>
</UserControl>
```

`TypewriterTerminal` is a control in the Special package. Register the package with `UseArcSpecial()` next to
`UseArcControls()`. Its content is the script: `TypewriterTerminalLine`s written inline, or added to `Lines` from code
(`TypewriterTerminalLine.Command("...")` and `TypewriterTerminalLine.Output("...", tone)`). The window reserves the
height of the whole script, so the page does not move while it plays. Give it a `Height` and its text scrolls to follow
the newest line instead.

### Lines and tones

A line's `Kind` is `Output` (printed whole, the default) or `Command` (typed after `Prompt`, which is `$` by default).
`Tone` colors a line: `Default`, `Muted` for progress and comments, `Accent` for links, and `Success`, `Warning` and
`Danger` for results. `Pause` adds a wait before a line, such as a slow build step. A script that plays once shows a
Replay button in the title bar when it ends (`ShowsReplay`, label `ReplayText`).

<Demo name="Tones" />

### Pace

`TypingSpeed` is the typing pace in characters per second (28); each keystroke varies between about half and one and
a half times the mean. `CommandDelay` is the pause between a command's last keystroke and its output (450 ms), and
`OutputDelay` the pause between two output lines (90 ms). `IsLooping` starts the script over after `LoopDelay` (3 s) at
the final prompt.

### Playing from code

`AutoPlay` (true) plays the script when the window is in view. Set it to false and call `Play()` yourself. `Pause()`
holds the script where it is, `Restart()` starts over and `Complete()` shows every line at once. `IsPlaying` and
`IsFinished` report the state, `LineCompleted` fires for every line (with its index) and `Completed` when the last one
is out.

<Demo name="Controls" />

### Best practices

**Do**

- Keep commands short and real-looking, and let the output tell the result: one success line beats ten progress lines.
- Use `Pause` on the line after a slow step, so the timing feels true.
- Name the window with `Title`: it is also the accessible name.

**Avoid**

- Scripts so long that people leave before the end; loop a short one instead.
- Hiding the only copy of an install command inside the animation.

### Common questions

**Can people read the whole script without waiting?** Yes. Assistive technology reads the full transcript at once, End
or Enter shows every line, and with motion off the whole script shows immediately.

**Why does the caret stop blinking after a while?** At the final prompt of a script that does not loop the caret blinks
for 12 seconds, then rests solid so the page stops asking for frames.

## API reference

<!-- api: TypewriterTerminal -->

<!-- api: TypewriterTerminalLine -->

<!-- api: TypewriterTerminalLineEventArgs -->

## Accessibility

- The window is a focusable group named by `Title` and described as a "terminal". Its help text is the whole
  transcript, with prompts, so assistive technology does not have to wait for the typing.
- The window dots are decoration and hidden from assistive technology. The Replay button is a real button named by
  `ReplayText`.
- Keyboard focus shows a focus ring around the window; pointer focus shows none.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` | Focus the terminal |
| `Space` | Pause, or carry on playing |
| `End` / `Enter` | Show every line at once |
| `Home` | Start the script over |

## Motion

- A command's prompt appears first; typing starts 0.5 s later at `TypingSpeed`, each keystroke between 0.55 and 1.45
  times the mean interval.
- The caret is a block one character wide. It is solid while keys are struck and for half a second after, then blinks
  on a 1.06 s cycle while the terminal waits.
- Output lines appear whole; nothing slides or fades, like a real terminal.
- The text is laid out once per line; a command being typed is its full line clipped at the caret, so typing allocates
  nothing. Frames run only while the script plays or the caret blinks, and the script waits while the window is out of
  view.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Command | Typed character by character | Appears whole at its time | Everything shows at once |
| Output | Line by line | Line by line | Everything shows at once |
| Caret | Blinks while waiting | Solid | Solid |

## Responsive behavior

- The window fills the width it is given and long lines wrap inside it, commands after the prompt's width.
- Its height is the whole script's at that width, so a narrow phone gets a taller window; set `Height` to keep it
  compact and let the text scroll to the newest line.
- The text is 13 px monospace; keep commands short so they fit on a phone without wrapping.

## Related

- `CodeBlock` shows code people can read and copy.
- `InstallSnippet` walks a developer from install to a first result in numbered steps with snippets.

| Component | Choose it when |
|---|---|
| `TypewriterTerminal` | A short session should play out like it happens |
| `CodeBlock` | People need to read or copy code |
| `InstallSnippet` | A quick start needs steps people follow and copy |
