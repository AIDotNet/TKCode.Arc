---
title: Video surface
summary: "Show a video's picture with nothing over it, as a background or under controls of your own; it clips, fades and transforms like any control."
---

<Demo name="BackgroundLoop" />

## When to use

- A muted, looping video behind a hero, a card or a sign-in page.
- A player with controls of your own design, driven through `IVideoPlayer`.
- Several pictures from one player, such as a thumbnail and a large view.

## When not to use

- A video people control: use `VideoPlayer`, which adds the controls, captions, keyboard and screen-reader support.
- A still picture: use `Image`.

## Usage

Create a player from the backend, give it to the surface, and dispose it when you are done. The surface does not own the player.

```csharp
var player = ArcVideo.CreatePlayer();
surface.Player = player;
await player.LoadAsync(VideoSource.FromUri(new Uri("avares://MyApp/Assets/loop.mp4")));
player.IsMuted = true;
player.IsLooping = true;
player.Play();
```

`Stretch` decides how the picture fills the surface: `Uniform` adds bars (`LetterboxBrush`, black by default), `UniformToFill` crops, `Fill` stretches and `None` keeps the video's size. `CornerRadius` rounds the picture and the bars. Other controls can be drawn over the surface, and it can be clipped, faded and transformed like any control.

The surface asks for a new frame only while the player plays and the surface is shown, and asks for frames no larger than it draws them. `RenderPath` says how the frames arrive: `GpuInterop` (imported into the compositor without a copy, on macOS and Windows) or `Bitmap` (copied). `PreferBitmapPath` forces the copy, for example to rule out a graphics driver problem.

### Controls of your own

<Demo name="CustomChrome" />

## API reference

<!-- api: VideoSurface -->

<!-- api: IVideoPlayer -->

## Accessibility

- The surface is a picture with no name and no controls. When it carries meaning, name it with `AutomationProperties.Name` and give people a way to pause it.
- Video that moves on its own for more than five seconds needs a way to pause it (WCAG 2.2.2). Start decorative loops only when motion is not reduced, as the demo does.
- Controls of your own need names, keyboard access and captions for speech: `VideoPlayer` has them built in.

## Motion

- The surface draws the frames the player hands out; it adds no motion of its own.
- Reduced motion does not stop a playing video; the app decides whether a decorative loop starts (`ArcMotion.GetEffectiveLevel`).

## Responsive behavior

- The surface takes the size its layout gives it; it has no preferred size, so give it a height or an aspect ratio.
- It asks the player for frames at the size it draws them, so a large video in a small surface costs little.
- With `Stretch="UniformToFill"` the picture keeps filling a surface that changes shape, cropping the sides or the top and bottom.

## Related

- `VideoPlayer`: the picture with controls, captions and keyboard support.
- `Image`: a still picture.
