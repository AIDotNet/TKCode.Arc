---
title: Media player
summary: An audio player with cover art, a playlist, a seek bar, previous and next, and volume with mute; skipping wraps around and keeps playing or paused, and the playing row shows moving bars.
---

<Demo name="Preview" />

## When to use

`MediaPlayer` plays a short list of audio tracks in one card. The current track's cover and title sit on top, with
previous, play and next buttons beside the title, a seek bar with the elapsed and total time under it, then the
playlist with a mute button and a volume slider in its heading. Pressing a row plays that track; pressing the current
row pauses or resumes it.

- **Episodes, lessons and samples.** A podcast's latest episodes, the audio of a course module or a set of sound
  previews next to the product they belong to.
- **Mixes and soundtracks.** A band's page, a game's soundtrack or a few ambient loops for a focus app.
- **Recorded calls and notes.** A short list of voice notes or call recordings to review in place.

## When not to use

- **A large library.** The playlist shows every track as a row, without search, sorting or virtualization; build a
  library with a `DataGrid` or a list and use the player for what is queued.
- **Video.** The block plays audio only; the cover is a still picture.
- **A single sound.** For one clip, a play button with the time is enough.

## Usage

### Tracks and audio

Each `MediaTrack` has a `Title`, an `Artist` and an `Album` (shown as "artist · album" under the title), a `Cover` image
with its `CoverDescription` for screen readers, the audio as an `AudioSource` and a `Duration` that the playlist shows
until the track is loaded (the length the audio reports then replaces it). `AudioSource.FromUri`, `FromFile` and
`FromBytes` cover addresses, local files and audio held in memory; `WavFile.Encode` writes generated samples as a WAV
file, which is how the gallery makes its three loops.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MediaPlayer>
        <arc:MediaTrack Title="Harbor lights" Artist="Low Static" Album="Night ferry" Duration="0:0:24" />
        <arc:MediaTrack Title="Paper kites" Artist="Low Static" Album="Night ferry" Duration="0:0:24" />
    </arc:MediaPlayer>
</UserControl>
```

```csharp
player.Tracks.Add(new MediaTrack
{
    Title = "Harbor lights",
    Artist = "Low Static",
    Album = "Night ferry",
    Source = AudioSource.FromUri(new Uri("https://cdn.example.dev/audio/harbor-lights.mp3")),
});
```

### Making sound

The player plays through `ArcAudio.Backend`, or through its own `AudioBackend` when you set one. The default backend
keeps time without making sound, so the player works the same everywhere (and in tests); `IsAudible` tells which one is
in use. For sound, add a platform package and select it when the app starts:

- **Desktop** (Windows, macOS, Linux): `TKCode.Arc.Audio.Native`, built on miniaudio, plays WAV, MP3 and FLAC.
- **WebAssembly**: `TKCode.Arc.Audio.Browser` plays through the browser's audio element, so it plays what the browser
  plays. Browsers only start audio after the person has pressed something on the page.

The gallery's desktop and browser hosts both select their backend, so the demos on this page play.

### Playing, skipping and the end of a track

`IsPlaying`, `SelectedIndex` and `Volume` are two-way. `Play`, `Pause`, `TogglePlay`, `NextTrack`, `PreviousTrack`,
`PlayTrack` and `Seek` do from code what the buttons do. Skipping wraps around (after the last track comes the first)
and keeps the player playing or paused as it was; a new track always starts from the beginning. Previous always moves to
the previous track, wherever the current one is. A track that plays to its end stops and returns to 0:00 without moving
on, and raises `TrackEnded`; handle it to continue with the next track.

<Demo name="Events" />

### Volume and mute

The volume runs from 0 to 1 and starts at 0.7. The mute button sets it to zero and remembers it; pressing it again
restores that volume, or `DefaultVolume` when the slider was dragged to zero. `IsMuted` is true whenever the volume is
zero.

### Without the card

`IsFramed="False"` drops the border, the background and the rounded corners, for a player that sits on a page or a
panel of its own.

<Demo name="Flat" />

### Narrow containers

The layout follows the player's own width. Below `CompactWidth` (420) the padding tightens from 20 to 14, the cover
becomes taller (1.7:1 instead of 2.2:1), the title shrinks from 28 to 22, the buttons and the gaps between them get
smaller and the volume slider shortens from 88 to 64. The player grows to at most 520 wide.

<Demo name="Compact" />

### Events and texts

`SelectedTrackChanged` carries the old and new track and what moved the player (`Code`, `Skip` or `Playlist`);
`PlaybackFailed` carries the backend's message, and the player pauses. Every word the player shows or reads to screen
readers is in `Labels`, a `MediaPlayerLabels` you can replace to translate it; texts with values are functions, and
`FormatTime` decides how times are written.

## API reference

<!-- api: MediaPlayer -->

<!-- api: MediaTrack -->

<!-- api: MediaPlayerLabels -->

## Accessibility

- **Tab** goes through previous, play and next, the seek bar, the mute button, the volume slider and each playlist
  row.
- **Enter** or **Space** presses the focused button or row.
- On the seek bar, **Left** and **Right** (or **Down** and **Up**) move by a tenth of a second, **Page Up** and
  **Page Down** by a tenth of the track, **Home** and **End** to the start and the end. On the volume slider the arrows
  move by 1%, the page keys by 10%.
- The player is named "Audio player". The buttons are named "Previous track", "Play" or "Pause", "Next track" and
  "Mute audio" or "Unmute audio"; the sliders are "Seek" plus the title (its value reads "0:12 of 0:24") and "Volume"
  (its value reads "70%").
- The title is a level-2 heading and "Playlist" a level-3 heading. A row is named "Play" plus its title, or "Pause" plus
  its title while that track plays; the moving bars are named "Playing".
- The cover is read with its `CoverDescription`; a track without a cover shows an empty plate that screen readers skip.
- Focus rings show only for the keyboard.

## Motion

- **Changing track**: the covers crossfade over 240 ms. The old title block lifts 10 px and fades while the new one rises
  10 px into place on the Smooth spring, always upward whichever way you skipped. The rail beside the current row
  springs to the new row (Smooth, about 550 ms).
- **Play and pause**: the old glyph shrinks to 60% and blurs out while the new one grows from 60% out of a 2 px blur, in
  120 ms. Pressed buttons shrink to 94% in 120 ms; hover plates fade in 160 ms.
- **Playing**: three bars in the current row rise and fall between 30% and full height, 900 ms each way, a third of a
  cycle apart. They run only while the row is on screen.
- **Seeking**: the backend reports the position about four times a second; in between, the seek bar moves on with the
  clock, so it glides instead of stepping. The slider's dot grows by 20% while it is dragged.
- Reduced motion changes covers, titles, the rail and the play glyph at once, keeps the bars still at 60% and the seek
  bar moves with each report. Off applies every change at once.

## Related

- File upload, the other Media block.
- `Slider` for a general-purpose range input.
- `Carousel` and `ImageCompare` for browsing pictures.
