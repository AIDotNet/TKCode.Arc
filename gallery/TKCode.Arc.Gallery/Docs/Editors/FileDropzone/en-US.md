---
title: File dropzone
summary: A generous target for dropping one or more files.
---

<Demo name="Upload" />

## When to use

- Attachments and uploads where the user should see each file, its progress and any failure.
- Uploads that may fail and need a retry, or a row to remove before the upload is done.
- Limits on the file type, the file size and the number of files, shown next to the target.

## When not to use

- Only the system file dialog is needed and no list: call `BrowseAsync` from your own button or use a plain `Button`.
- An empty page where dropping is the only action: use `EmptyState` for a larger call to action.
- Choosing one value from a known set: use `Select` or `RadioGroup`.

## Usage

The whole target is a click area, so the system file dialog opens from anywhere on it. `Accept` limits the file
types, and `MaxFiles` and `MaxFileSize` limit the count and the size per file.

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FileDropzone Label="Add files"
                      Description="Drop files here or choose from your device"
                      Accept=".pdf,image/*"
                      MaxFiles="6" />
</UserControl>
```

### Images up to 2 MB

`Accept` takes extensions and MIME patterns. A refused type, or too many files, shakes the target and shows the error.
A file over `MaxFileSize` fails and is not uploaded. `MaxFileSize` is in bytes.

<Demo name="Images" />

### Single file

With `AllowMultiple` set to `False`, a new file replaces the current one.

<Demo name="SingleFile" />

### Upload handler

`UploadHandler` uploads each file in turn. Report progress through the `IProgress<double>` and stop when the
`CancellationToken` is cancelled. Removing a row that is uploading cancels its upload.

```csharp
zone.UploadHandler = async (item, progress, token) =>
{
    await UploadToServerAsync(item.File!, progress, token);
};

zone.FilesChanged += (_, e) =>
{
    // e.Files holds the files that were added.
};
```

### Application-driven rows

Without an upload handler, the app drives each row. Call `ReportProgress`, `CompleteUpload` and `FailUpload`, or set
the item's `Status` and `Progress` directly. `Retry` and `Remove` act on a row.

<Demo name="AppDriven" />

```csharp
zone.ReportProgress(item, 40);
zone.CompleteUpload(item);
zone.FailUpload(item, "The server closed the connection", retryable: true);
```

### Rows inside the target

`ListPlacement="Inside"` shows the rows within the target's edge. With `CompactAt`, the prompt folds to a slim bar
once that many rows are listed.

<Demo name="ListInside" />

## API reference

<!-- api: FileDropzone -->

<!-- api: FileDropzoneItem -->

## Accessibility

- The target is a button. Its tab stop is the target itself, and Enter or Space opens the file dialog.
- In the list, ArrowUp and ArrowDown move focus to the same action on the neighboring row. Delete and Backspace remove
  the focused row, and focus moves to a neighboring row.
- Mod+V pastes files from the clipboard, such as a screenshot, while the pointer is over the target or focus is in it.
- An uploading row exposes a progress bar from 0 to 100. Remove and retry buttons include the file name in their name.
- Adding, completing and failing are announced politely. Rejected files are announced assertively.

## Motion

- Hovering a file over the target lifts the paper icon and turns the dashed border solid, with a slow pulse while
  files are dragged over it.
- Rows slide in from above and scale up from a slight blur, and new rows enter one after another. Removed rows
  collapse in about 0.3 seconds.
- The progress bar follows its target, and a completed row draws a check mark. A failed row shakes once.
- Reduced motion swaps the height and lift changes for short fades, and progress changes are immediate. The pulse
  does not run.

## Related

- `EmptyState` for an empty page where dropping is the main call to action.
- `Input` and `Textarea` for values that are not files.
- `SignaturePad` for a signature that is drawn rather than attached.
