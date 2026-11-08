---
title: Video player
summary: "Play a video with controls over the picture: seek with chapters, volume, speed, captions and full screen, drawn in Arc's style and decoded by the platform's own player."
---

<Demo name="Preview" />

## When to use

- Product tours, tutorials, recorded sessions and generated clips that people watch inside your app.
- Video whose controls should look and behave like the rest of the interface, with captions people can read in their language.
- Several short clips on one page: each player starts paused and loads its media only when it is shown.

## When not to use

- Sound only: use the Media player block or an `IAudioPlayer`.
- A picture with no controls, such as a looping background or controls of your own: use `VideoSurface`.
- Live streams that need low latency, DRM-protected media, or frame-accurate editing: not supported in this version.

## Usage

Add `TKCode.Arc.Video` and register it, then choose a playback backend at startup:

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcVideo());

ArcVideoNative.Use();              // desktop: TKCode.Arc.Video.Native
await ArcVideoBrowser.UseAsync();  // browser: TKCode.Arc.Video.Browser
```

The desktop backend uses the system's own player: Media Foundation on Windows, AVFoundation on macOS and GStreamer on Linux. No codecs ship with your app. MP4 with H.264 video and AAC audio plays everywhere; other formats depend on the platform (WebM does not play on macOS, HEVC needs an extension on Windows, Linux needs the `gst-plugins-good` and `gst-libav` packages). Ask `ArcVideo.Backend.CanPlay("video/webm")` at run time. Without a backend the player shows a generated test picture and keeps time silently.

Set `Source` to an address (`avares://`, `file://`, `http(s)://`) or a `VideoSource`. `AspectRatio` keeps the layout steady before the video's own size is known; the poster shows until the first play.

```xml
<arc:VideoPlayer Source="avares://MyApp/Assets/intro.mp4"
                 Title="Introduction"
                 AspectRatio="1.7778" />
```

`IsPlaying`, `Position`, `Volume`, `IsMuted`, `IsLooping` and `PlaybackRate` bind both ways. `Play()`, `Pause()`, `Stop()`, `Seek()` and the `PlayPauseCommand`, `StopCommand`, `SkipForwardCommand`, `SkipBackwardCommand`, `ToggleMuteCommand` and `ToggleFullScreenCommand` commands drive the player from outside. `Stop()` pauses, returns to the start and shows the poster again; the video stays loaded, so the next play starts at once. `Failed` reports why a video cannot play.

### Animated films

Changing `Source` loads another video in the same player. These are the trailers of two Blender Foundation open movies, © Blender Foundation, under [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/).

<Demo name="Films" />

### Captions and chapters

Add WebVTT or SRT files as `TextTracks`. The captions menu lists every captions and subtitles track, sets the text size, and turns captions off. A `Chapters` track splits the seek bar into segments and names the chapter under the pointer.

Subtitles stored in the video itself (MP4 timed text, Matroska text tracks) join `TextTracks` once the video is loaded, after your own tracks, with `IsEmbedded` set, and leave when the source changes. They are read on macOS and iOS (MP4), Android (MP4, plain text, one cue at a time) and Linux (MP4 and Matroska; the MP4 demuxer there drops bold and italic); Windows and the browser do not list them yet.

<Demo name="Captions" />

### Controls

The bar hides two seconds after the pointer stops while the video plays, and stays while it is paused, while a menu is open, while the pointer is over it and while it has keyboard focus. `ShowControls="Always"` keeps it on screen; `ControlsHideDelay` changes the delay. `AutoPlay` starts the video only when it is muted: browsers and accessibility guidelines do not allow sound that starts on its own.

<Demo name="Controls" />

### Playback backends

The player decodes with the backend in `ArcVideo.Backend` and draws the frames inside the window, so its controls, captions and rounded corners draw over the picture. Where the platform allows, frames move from the decoder to the screen on the GPU without a copy (macOS and Windows); elsewhere they are copied into a bitmap.

On iOS and Android, `ArcVideoNative.Use()` works the same way: iOS plays through AVFoundation and Android through the platform's MediaPlayer, both with frames copied into a bitmap. Reference `TKCode.Arc.Video.Native` from a `net11.0-ios` or `net11.0-android` app (iOS 15, Android API 24 or later).

In a browser, `BrowserPresentation="Native"` shows the browser's own video element instead: nothing is copied, and every format the browser plays works (HLS in Safari, for example). Nothing can be drawn over that element, so the control bar sits below the picture and always shows, the captions are the browser's, and the corners are square; the element hides while the poster, an error, the replay button or a menu is shown. Without such an element (desktop, or the net8.0 build) the player keeps the canvas.

<Demo name="Playback" />

## API reference

<!-- api: VideoPlayer -->

<!-- api: VideoTextTrack -->

<!-- api: VideoSource -->

## Accessibility

- The player reads as a group named after `Title` ("Video" without one). Its buttons are named after what they do and change name with the state: "Play" and "Pause", "Mute" and "Unmute", "Full screen" and "Exit full screen"; each tooltip shows its shortcut.
- The seek bar is a slider whose value reads as "1 minute 5 seconds of 3 minutes 15 seconds" when it gets focus or a drag ends; while the video plays it does not announce every second. The volume slider reads as a percentage.
- Errors, full screen, captions on or off and speed changes are announced politely. With `CaptionAnnouncements`, the captions are announced too.
- Hidden controls stay in the tab order: Tab shows the bar again, and it stays while it has focus.
- Captions follow the W3C requirements for size and background: the menu and `CaptionScale` enlarge the text, and `CaptionBackgroundOpacity` darkens the box behind it.

### Keyboard

| Key | Action |
| --- | --- |
| Space, K | Play or pause (on a button, Space presses the button). |
| Left / Right | Back or forward 5 seconds (`SeekStep`). |
| J / L | Back or forward 10 seconds. |
| Up / Down | Volume up or down 5%. |
| M | Mute or unmute. |
| F | Full screen on or off. Escape leaves full screen. |
| C | Captions on or off. |
| Shift + . / Shift + , | Faster or slower. |
| 0 to 9 | Jump to 0% to 90%. |
| Home / End | Jump to the start or the end. |
| Page Up / Page Down | On the seek bar: the previous or next chapter (a tenth of the video without chapters). |

## Motion

- The control bar fades and rises 8 px over 160ms when it appears, and goes the other way when it hides; the pointer hides with it.
- The play and pause icons cross-fade and scale on a snappy spring. Clicking the picture shows the new state as a large icon in the middle that grows and fades.
- The seek track thickens from 4 to 6 px under the pointer; the big play button dips to 94% when pressed.
- Between position reports the seek thumb moves smoothly at the playback speed.
- Reduced motion: the bar only fades, the icons switch at once, and the centre feedback and the double-tap ripple are left out. The video itself plays as usual; a muted, looping video that starts on its own stays on its first frame.

## Responsive behavior

- The player takes the width it is given and its height from `AspectRatio` (16:9 before the video's size is known).
- Below 420 px (`CompactWidth`) the bar tightens its padding and gaps, the time text gets smaller, and the volume slider no longer slides out beside the mute button.
- It measures its own width, not the window's, so a player in a narrow column or a phone preview takes the compact layout too.
- On touch, a tap shows the bar, a second tap plays or pauses, and a double tap on the left or right third goes back or forward 10 seconds.
- Full screen fills the window (`FullScreenMode="Window"`) or also makes the window full screen (`System`), and returns to the same place in the page.

## Related

- `VideoSurface`: the picture alone, for backgrounds and controls of your own.
- Media player block: an audio player with a playlist.
- `Carousel`: browse a set of pictures.
