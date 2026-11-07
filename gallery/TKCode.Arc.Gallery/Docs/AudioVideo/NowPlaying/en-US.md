---
title: Now playing
summary: A mini player bar that grows into a full player in one continuous morph, with a waveform scrubber and artwork that slides the way you skip.
---

<Demo name="Preview" />

## When to use

`NowPlaying` is a player that stays put while people do other things. At rest it is a 64px bar: the cover as a
thumbnail, the title and artist, three small level bars, play and next. One press grows the same bar, in place and in
one continuous movement, into the full player: a large cover, the title in display type, a waveform you can scrub, the
elapsed and remaining time, and previous, play and next. The cover and the two buttons fly to their new places while
the shell's height and corners follow the same spring; nothing jumps.

- **Audio that keeps playing while people browse**: music, a podcast, an audio lesson or a recorded meeting, in a
  sidebar, a footer or the corner of a window.
- **A small player that sometimes needs more**: most of the time the bar is enough; the full player is there for
  seeking and skipping, then folds away again.
- **A queue**: next and previous move through a list of tracks and wrap at both ends; the cover slides in from the side
  you skipped to.
- **Touch-friendly layouts**: the full player folds back when the cover is pulled down, as on a phone.

## When not to use

- **A one-off panel that is not a player**: use `Dialog` or a bottom sheet.
- **Recording and playing back short voice notes**: use `VoiceRecorder`.
- **A seek bar on its own, without the player around it**: use `Slider`.
- **Video**: use the video components; this player shows a still cover.
- **A long playlist to browse**: show the list yourself and give the player the queue; it shows one track at a time.

## Usage

Give the player its queue. Everything else has a default: the first track, paused, the mini bar.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:NowPlaying x:Name="Player" />
</UserControl>
```

```csharp
Player.Tracks = new AvaloniaList<NowPlayingTrack>
{
    new("ep-41", "Ep. 41 · Quiet machines", "Field Notes Radio", TimeSpan.FromSeconds(2292))
    {
        Artwork = cover,                       // any IImage: a bitmap, a DrawingImage
        ArtworkAlt = "A ring of warm light in the dark"
    }
};
```

`NowPlaying` derives from Avalonia's `TemplatedControl` and lives in the Special package: register it with
`UseArcSpecial()` next to `UseArcControls()`. The preview plays an invented queue from `NowPlayingSamples`: five
fictional tracks of three to four minutes with covers drawn in code (gradients and simple shapes).

### The full player

`IsExpanded` is two-way. The mini bar, Enter or Space on it, the chevron on the cover, Escape and a pull on the cover
change it; set it, or call `Expand()`, `Collapse()` and `ToggleExpanded()`, to open or fold the player yourself.
`ExpandedChanged` reports every change. Set `IsExpanded="True"` to start on the full player.

<Demo name="FullPlayer" />

Press anywhere on the waveform and the playhead jumps there; drag and it follows the pointer exactly, while the whole
waveform stretches a little taller to show it is held. The clock holds still until you let go, then playback goes on from
there. With the waveform focused, the arrow keys step 5 seconds, Page Up and Page Down 30 seconds, Home goes to the start
and End to one second before the end.

Previous restarts the track when more than 3 seconds have played (`RestartThreshold`): the playhead glides back to the
start. Within the first 3 seconds it goes to the previous track. When a track ends, the next one starts and keeps
playing.

### Playback and real audio

The player runs its own clock: one second per second while `IsPlaying` is true, so it works with no audio at all (a
demo, a prototype, a test). To play real audio, follow the player and report back to it:

- `IsPlaying` and `Index` are two-way. Bind them, or handle `PlayingChanged` (play or pause your audio) and
  `IndexChanged` (load the new track; `Reason` says whether it was a skip or the end of a track, `Direction` which way).
- Set `CurrentTime` from your audio's time updates. Each value corrects the clock, which keeps the waveform moving
  smoothly between updates. It is ignored while someone drags the waveform.
- Handle `SeekRequested` and set your audio's position. It is raised when a drag ends, on every keyboard seek, when
  Previous restarts the track (with zero) and on `SeekTo`.

<Demo name="HostDriven" />

```csharp
player.PlayingChanged += (_, e) => { if (e.IsPlaying) { audio.Play(); } else { audio.Pause(); } };
player.IndexChanged += (_, e) => audio.Load(queue[e.NewIndex]);
player.SeekRequested += (_, e) => audio.Position = e.Position;
audio.PositionChanged += (_, _) => player.CurrentTime = audio.Position;
```

`Position` reads where the playhead is (updated every whole second, on every seek and on a track change).
`Next()`, `Previous()`, `Play()`, `Pause()`, `TogglePlay()` and `SeekTo()` do from code what the buttons do.
`IndexChanged` is raised for skips and track ends, not when your code sets `Index`.

### Anatomy

1. **Shell**: 336px wide at most (the full width of a narrower container), 64px tall as a bar with 22px corners and
   about 411px tall as a player with 30px corners; a raised surface with a 1px border and a floating shadow.
2. **Cover**: 44px in the bar, as wide as the player and 192px tall in the full player. The image is always shown as a
   square as wide as the cover, centred and cropped, so it grows without stretching. Its corners are the shell's minus
   10px. Without an image the muted surface shows.
3. **Mini text**: the title (14px, medium) over the artist (12px, secondary), each on one line ending with an ellipsis.
   The whole area is the button that opens the player.
4. **Level bars**: `NowPlayingLevels`, three 3 × 14 bars that dance while playing.
5. **Play**: a 40px muted circle in the bar; a 52px circle in the text color with the icon in the background color in
   the full player.
6. **Next and previous**: 40px (bar) and 44px (player) round buttons with no fill until hovered.
7. **Collapse**: a 34px dark glass circle with a chevron in the cover's top-right corner, the same in both themes.
8. **Info**: the title in display type (18px) over the artist (14px).
9. **Waveform**: 54 bars, 2px apart. Played bars are in the text color, the rest at 16% of it; the boundary moves
   continuously and can fall inside a bar.
10. **Times**: the elapsed time on the left ("1:02") and the remaining time with a minus sign on the right ("−2:24"),
    12px in tabular figures.

### The level bars on their own

`NowPlayingLevels` is public: put it next to the row that is playing in a list of your own and bind `IsPlaying`.

<Demo name="Levels" />

```xml
<arc:NowPlayingLevels IsPlaying="{Binding IsCurrent}" />
```

### Gestures

- **Pull the cover down** in the full player: the shell follows at 60% of the pointer. Let go past 70px (about 117px of
  pointer travel), or flick down at 600px a second or faster, and the player folds; otherwise it springs back with a
  small overshoot.
- **Pull it up**: the shell resists more and more (a rubber band) and springs back.
- **Press or drag on the waveform**: scrub, as described above. A press on the cover in the bar does nothing; the text
  area opens the player.

### Narrow containers

The shell is as wide as its container up to 336px. In a narrower column the cover keeps its 192px height, the waveform
bars get thinner and long titles end with an ellipsis. In a window 360px wide or less, the bar drops its next button to
make room for the title; the full player keeps it.

<Demo name="Narrow" />

### Reduced motion

Under reduced motion nothing flies: the cover and buttons are in their final places at once and the shell opens in a
short 160ms tween, revealing them. Covers crossfade instead of sliding, text fades without moving or blurring, and the
level bars hold still while playing. Every state, name, focus move and announcement stays.

<Demo name="ReducedMotion" />

### Content guidelines

Keep titles and artist names as people know them; the player cuts them with an ellipsis rather than wrapping. Write
`ArtworkAlt` as a short description of the picture, not of the music ("Teal currents swirling in deep water"), or leave
it out when the cover is purely decorative.

| Instead of | Write |
|---|---|
| Album art | Layered dunes under a pale sky |
| Track 3 | Paper, lanterns |
| Unknown | (the real artist, or leave the line short) |

### Best practices

**Do**

- Give every track its real length: the remaining time and the end of the track come from it.
- Report the real position through `CurrentTime` when real audio plays, and set your audio's position in
  `SeekRequested`.
- Place the player where it can grow: it needs about 411px of height when open (the preview centres it, so it grows
  both ways).
- Use one player per page; it is meant to stay.

**Avoid**

- Rebuilding `Tracks` on every time update. Replace the list only when the queue changes.
- Changing a track object's properties while it is showing; replace the item in the list instead.
- Putting the player inside a scroller that clips it: the open player is tall.

### Common questions

**Does it play sound?** No. It shows and controls playback; your code plays the audio and reports the position. Without
audio its clock still runs, so the player is fully usable as a demo.

**How do I start with a track in the middle?** Set `Index`, then `CurrentTime` (or call `SeekTo`).

**What happens at the end of the queue?** Next and the end of the last track go to the first one; Previous on the first
track (within 3 seconds) goes to the last.

**Can I change the waveform?** Give each track its own `Peaks` (values from 0 to 1, any number: they are resampled to
`PeakCount` bars). Without peaks a fixed shape is generated from the track's `Id`, so the same track always looks the
same.

**Why did Escape not fold the player?** Escape folds it when focus is inside the player or nowhere in particular, so a
player never takes Escape away from another control that has focus.

## API reference

<!-- api: NowPlaying -->

<!-- api: NowPlayingTrack -->

<!-- api: NowPlayingLabels -->

<!-- api: NowPlayingLevels -->

<!-- api: NowPlayingIndexChangedEventArgs -->

<!-- api: NowPlayingSeekEventArgs -->

<!-- api: NowPlayingPlayingChangedEventArgs -->

<!-- api: NowPlayingExpandedChangedEventArgs -->

The enums:

| Type | Values |
|---|---|
| `NowPlayingSkipDirection` | `Forward`, `Backward` |
| `NowPlayingIndexChangeReason` | `Skip`, `Ended` |
| `NowPlayingSeekReason` | `Drag`, `Keyboard`, `Restart`, `Method` |

`NowPlayingSamples` holds the invented sample queue (`CreateTracks()`, `Apply(player)`, `Cover(index)`).

## Accessibility

- The player is a group named by `Label` ("Now playing").
- The mini bar's text area is a button named "Expand player, {title} by {artist}" that reports itself collapsed; the
  chevron is "Collapse player" and reports itself expanded.
- The play button is named "Play {title}" or "Pause {title}"; next and previous are "Next track" and "Previous track".
- The waveform is a slider named "Position" from 0 to the track's length, with a spoken value such as "1:07 of 3:51".
- A polite live region says the track and its state ("Low Orbit by Mara Quell, playing") on every track change, play
  and pause.
- The cover is described by `ArtworkAlt`; without it the cover is decorative and hidden from assistive tech. The level
  bars, the times and the progress line are decorative too.
- Keyboard focus shows a 2px ring 2px outside the control: round on the buttons, square on the mini text area and the
  waveform. Pointer focus draws none.
- When the player opens, focus moves to the collapse button; when it folds, focus returns to the mini bar.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Bar: the text area, play, next. Player: collapse, the waveform, previous, play, next |
| `Enter` / `Space` | On the text area, open the player; on a button, press it |
| `Left` / `Down`, `Right` / `Up` | On the waveform, 5 seconds back or forward |
| `Page Down` / `Page Up` | On the waveform, 30 seconds back or forward |
| `Home` / `End` | On the waveform, the start, or one second before the end |
| `Escape` | Fold the full player (when focus is in the player or nowhere in particular) |

## Motion

- Opening: the shell's height and radius spring from the bar to the player (0.46 s visual duration with a trace of
  bounce); the cover, play and next fly along the same spring, the cover growing from its top-left corner. The bar's
  text, level bars and progress line fade out in 100ms with a slight blur; the info, waveform, previous and collapse
  fade in over 240ms after 80, 120, 140 and 160ms, rising 12, 14 and 10px out of a 2px blur.
- Folding: the same path back on a spring without bounce; the bar's parts come back after 100, 140 and 160ms. Reversing
  half-way keeps the movement's speed, so the shell carries on briefly before it turns.
- Skipping: the old cover slides 24% of its width toward the side it leaves by, shrinks slightly and blurs while the new
  one slides in from the other side (smooth spring); they crossfade over 240ms. The title and artist leave upward and the
  new ones rise from 6px below. The waveform changes at once.
- Play and pause trade icons: the old one fades and shrinks in 120ms, the new one grows in on a firm spring.
- The waveform stretches to 1.18 of its height while held. Level bars dance while playing and settle in 240ms.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Open and fold | Shell springs, cover and buttons fly | Shell tweens in 160ms, parts in place | Instant |
| Bar and player parts | Staggered fade, rise and blur | Fade only | Instant |
| Skip | Cover slides and blurs, text rises | Crossfade | Instant |
| Play and pause icon | Shrink, blur and spring | Crossfade | Instant |
| Level bars | Dance | Still | Still |
| Pull release | Spring back or fold | Instant | Instant |
| Clock, times and waveform cut | Every frame | Every frame | Every frame |

## Responsive behavior

- The shell is as wide as its container, up to 336px; heights, type and button sizes do not change.
- In a window 360px wide or less (the nearest mobile preview frame counts as the window) the bar drops its next button.
- The cover keeps its 192px height in a narrow player; the waveform's bars get thinner.

## Related

- `VoiceRecorder` to record and play back short voice notes.
- `VoiceOrb` for a voice conversation with live captions.
- `Slider` for a plain seek bar.

| Component | Choose it when |
|---|---|
| `NowPlaying` | Audio keeps playing while people browse, with a queue |
| `VoiceRecorder` | People record a short take and send it |
| `Slider` | You need a value on a track, not a player |
