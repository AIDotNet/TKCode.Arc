---
title: Avatar upload
summary: Drop or pick a photo, move and zoom it under a circle, and watch a ring fill around it while it uploads, with Retry when it fails.
---

<Demo name="Preview" />

## When to use

`AvatarUpload` is the field for a person's or a team's picture. A circle shows the current picture (or a dashed
outline that invites a drop), with the field's title, a short line about the accepted files, and buttons to upload,
change or remove. A new picture first opens a framing step: the photo sits under a clear circle, and the person drags
it into place and zooms with the wheel, a pinch, the slider or the keyboard; the photo always covers the circle. Save
cuts a square of `OutputSize` pixels. When an upload handler is set, the new picture shows at once with a ring that
fills around it while it uploads, a Cancel button while it runs, and Retry when it fails.

- **Profile settings**: the person's own picture.
- **Workspace, team or project settings**: a logo or a group picture.
- **Onboarding**: a picture step that can be skipped and done later.

## When not to use

- **Several files or files of any kind**: use `FileDropzone`, which lists files, checks their types and uploads them.
- **Only showing a picture**: use `Avatar`.
- **A cover image or a banner**: a circle crop does not fit wide pictures; frame them in a field of their own shape.

## Usage

Bind `Source` to the current picture and set `UploadHandler` to send the framed picture to your server.

```xml
<arc:AvatarUpload x:Name="Photo" Label="Profile photo" DisplayName="Mina Okafor" />
```

```csharp
Photo.Source = currentAvatar;
Photo.UploadHandler = async (request, progress, cancel) =>
{
    // request.Bytes is the square picture as PNG or JPEG (request.ContentType), OutputSize pixels on a side;
    // request.Image is the same picture as a bitmap, and request.SourceRect the part of the original it was cut from.
    await api.UploadAvatarAsync(request.OpenStream(), request.ContentType, progress, cancel);
};
```

`AvatarUpload` is part of the `TKCode.Arc` package: `UseArcControls()` registers it.

A picture comes in four ways: a file dropped on the field, the file picker (the circle or the Upload button), code with
`Edit(bitmap)`, or code with `LoadAsync(file)`, `LoadAsync(stream, name)` or `LoadAsync(bytes, name)` for a file, a stream or
bytes you already hold (a pasted image, a camera capture). A file is checked before anything is decoded: first its size
against `MaxFileSize` (10 MB; the read stops at the limit even when the stream cannot say how long it is), then its type
by its first bytes, so a PDF renamed to `.png` is refused and a photo with an odd name is not. The default types are PNG,
JPEG, WebP, GIF and BMP; `Accept` narrows them, with MIME types, `image/*` or extensions as in an HTML file input. A
refused file shows its reason under the title and raises `FileRejected`. Large photos are scaled down to 2048 pixels on
their long side before framing. `Cropped` is raised with the framed picture before it uploads; without an
`UploadHandler` the picture is applied at once.

`DisplayName` gives the initials shown in the circle while there is no picture. `AllowRemove="False"` hides the Remove
button (the `Remove()` method still works).

### Sample photos

Set `Source` to give the field a current picture, and `Edit(bitmap)` to frame one the app already has. The three
people below are invented and drawn in code.

<Demo name="SamplePhotos" />

### Refused files and failed uploads

A refused file never reaches the framing step: the reason replaces the line under the title, in the danger color, and
is announced. A failed upload keeps the new picture with a red edge, shows the message the handler threw as it is, and
offers Retry. Throw an exception whose message says what happened and what did not change.

<Demo name="Failures" />

| What happens | Shown | Event |
|---|---|---|
| The file is larger than `MaxFileSize` | "holiday.png is larger than 5 MB" | `FileRejected` (`Size`) |
| The type is not accepted | "statement.pdf is not a PNG, JPG, WebP or GIF image" | `FileRejected` (`Type`) |
| The file is not a picture the platform can decode | "broken.png could not be read" | `FileRejected` (`Unreadable`) |
| The upload handler throws | The exception's message, with Retry and Cancel | none: `State` becomes `Failed` |

### The saved picture

`Cropped` and `UploadHandler` receive an `AvatarUploadRequest`. `Bytes` is the square picture encoded as
`ContentType` and `OpenStream()` a stream over it. `OutputFormat` chooses the format: `Auto` (the default) saves a PNG, and a picture
of unknown type (one handed to `Edit` without a file name), as PNG, and any other picture as JPEG; `Png` and `Jpeg` force one. A JPEG is saved at
`JpegQuality` (92) and, having no transparency, on white. `SuggestedFileName` is the original name with the matching
extension.

### States

`State` tells what the field is doing: `Empty`, `Editing` (framing), `Uploading`, `Done` or `Failed`. An app that
uploads in `Cropped` instead of through a handler drives the ring itself with `ReportProgress`, `CompleteUpload` and
`FailUpload`.

<Demo name="States" />

| State | The field shows |
|---|---|
| `Empty` | A dashed circle with a person glyph, and Upload photo |
| `Editing` | The framing area, the zoom row, Cancel and Save |
| `Uploading` | The new picture under a light scrim, the ring filling, and Cancel upload |
| `Done` | The picture (a camera shows over it under the pointer), Change and Remove |
| `Failed` | The new picture with a red edge, the reason, Retry and Cancel |

Cancel upload, or Cancel after a failure, goes back to the picture from before.

### Framing

The framing area is 256px square with the circle 20px inside its edges. The zoom goes from the size where the photo
just covers the circle up to `MaxZoom` (4) times that. Enter in the framing area saves; Escape cancels from anywhere in
the step.

### Anatomy

1. **Circle** (`Size`, 96px): the picture, or the empty state (the initials from `DisplayName`, else a person glyph); a drop target and a button that opens the file picker.
2. **Progress ring**: 3px, 5px outside the circle, in the accent color, while uploading.
3. **Title** (`Label`) and **status** (`Description`, the progress, or the problem in the danger color).
4. **Buttons**: Upload photo or Change, Remove, Retry, Cancel upload, as the state needs.
5. **Framing step**: the area with its dimmed edges and clear circle, zoom out, the zoom slider, zoom in, Cancel and Save.

### Content guidelines

| Instead of | Write |
|---|---|
| Choose a file to upload as your avatar image | Upload photo |
| Error: upload failed (500) | The connection dropped. Try again. |
| Supported formats: image/png, image/jpeg | PNG, JPG or WebP, up to 10 MB |

## API reference

<!-- api: AvatarUpload -->

<!-- api: AvatarUploadRequest -->

<!-- api: AvatarUploadCroppedEventArgs -->

<!-- api: AvatarUploadRejectedEventArgs -->

<!-- api: AvatarUploadLabels -->

| Type | Values |
|---|---|
| `AvatarUploadState` | `Empty`, `Editing`, `Uploading`, `Done`, `Failed` |
| `AvatarUploadRejection` | `Type`, `Size`, `Unreadable` |
| `AvatarImageFormat` | `Auto`, `Png`, `Jpeg` |

## Accessibility

- The buttons are real buttons in a fixed order; the circle is also clickable, but the Upload or Change button is the
  keyboard way to pick a file.
- A polite live region announces results (photo updated, photo removed) and progress in quarters ("Uploading 50%"); a
  refused file or a failed upload is announced with its reason.
- The framing area is one focusable element named "Photo position", with help text that lists its keys; focus moves
  to it when framing starts and back to the pick button when it ends.
- A failure is shown by its message as well as the red edge.

### Keyboard

| Key | Where | Action |
|---|---|---|
| `Enter` / `Space` | Buttons | Press the focused button |
| Arrow keys | Framing area | Move the photo 8px (32px with `Shift`) |
| `+` / `-` | Framing area | Zoom in or out a step |
| `0` | Framing area | Back to the covering zoom, centered |
| `Enter` | Framing area | Save |
| `Escape` | Framing step | Cancel |

## Motion

- The scrim and the camera over the picture fade in 160ms under the pointer; the circle's edge and fill fade when a
  file is dragged over it.
- The progress ring eases toward each reported value instead of jumping.
- The zoom buttons and keys zoom on a snappy spring; dragging, the wheel and the slider follow the input directly.
- Under reduced motion the scrim fades in 120ms, the edge changes at once, the ring jumps to each value and the zoom
  steps at once; with motion off everything is instant.

## Responsive behavior

- The circle keeps its `Size`; the title, status and buttons beside it wrap when the space is narrow.
- The framing step is 256px wide; place the field where that fits, or in a dialog on small screens.

## Related

- `Avatar` to show a picture.
- `FileDropzone` for files of any kind and several at once.

| Component | Choose it when |
|---|---|
| `AvatarUpload` | One picture, framed in a circle |
| `FileDropzone` | Many files, any type, listed with their progress |
| `Avatar` | The picture is only shown |
