---
title: File upload
summary: A complete file selection flow with constraints, progress, and error feedback.
---

<Demo name="Preview" />

## When to use

Use `FileUpload` for documents or media when people need to see upload progress and recover from invalid files.

- **Uploads with per-file progress**, retry and remove.
- **Attachments that must be validated** by type and size before they upload.
- **Uploads that stop when a file is removed**: removing a file cancels its upload's token.

## When not to use

- **Picking files without upload state**: use `FileDropzone`.
- **Data that needs column mapping** after it is uploaded: use an import flow built for it.

## Usage

```xml
<arc:FileUpload x:Name="Attachments" Accept="image/*,.pdf" MaxSize="10485760" />
```

```csharp
Attachments.Uploader = async (item, progress, token) =>
{
    await using var stream = await item.File!.OpenReadAsync();
    await UploadWithProgressAsync(item.Name, stream, progress, token);   // report 0-100; throw to mark the file failed
};
```

Each accepted file is uploaded through `Uploader` as soon as it is added: report 0 to 100 through the progress, return
when the upload is done, throw to mark the file as failed. The token is cancelled when the file is removed, when a new
file replaces it (`Multiple="False"`) and when the control leaves the visual tree. Without an uploader, files are only
validated and listed, and an accepted file shows a check mark.

### Type and size

<Demo name="Validation" />

`Accept` takes comma-separated extensions (`.pdf`), MIME types (`application/zip`) and wildcards (`image/*`).
Avalonia's files carry no MIME type, so the control looks it up from the file's extension in a built-in table of common
types; an unknown extension matches only by extension. `MaxSize` is the largest size in bytes. A rejected file stays in
the list with its error, "This file type is not accepted." or "File is larger than 1 MB.", and is not uploaded. The
file picker is filtered by `Accept` too.

```xml
<arc:FileUpload Accept=".csv,text/plain" MaxSize="1048576"
                Label="Import contacts" Description="CSV or plain text, up to 1 MB." />
```

### One file

<Demo name="SingleFile" />

With `Multiple="False"` the picker takes one file and a new file replaces the list, cancelling the running upload.

```xml
<arc:FileUpload Multiple="False" Accept="image/*" Label="Upload a cover image" />
```

### Disabled

<Demo name="Disabled" />

`IsEnabled="False"` dims the dropzone and stops it from opening the picker or taking drops.

### The file list

`Files` is the list, newest last, as `FileUploadItem` objects. It binds two-way: the control replaces it when files are
added or removed and raises `FilesChanged` with the new list. Each item has its `File` (an `IStorageFile`), `Name`,
`Size`, `Error`, and the upload's `Status` (`None`, `Uploading`, `Done`, `Failed`) and `Progress`.

```csharp
upload.FilesChanged += (_, e) => SaveButton.IsEnabled = e.Files.All(f => f.Status == FileUploadStatus.Done);
```

`BrowseAsync()` opens the picker, `AddFilesAsync(storageItems)` adds files from your own source (a paste, another
drop target), and `AddFiles(items)` adds items you create with a name and a size. `Retry(item)` and `Remove(item)` do
what the row buttons do.

### Text

`Label` ("Upload files"), `Description` ("Drop files here or browse from your device.") and `BrowseLabel` ("Browse")
are properties. While files are over the dropzone the label reads "Drop to add files" (or "Drop to add a file"). The
status line reads, for example, "2 files added. 1 file needs attention.", "report.pdf uploaded.",
"report.pdf could not be uploaded." and "report.pdf removed."; `Status` holds the last one.

## API reference

<!-- api: FileUpload -->

<!-- api: FileUploadItem -->

<!-- api: FileUploadStatus -->

<!-- api: FileUploadFilesChangedEventArgs -->

## Accessibility

- The dropzone is a button named by `Label` and described by `Description`.
- A polite live region announces added, rejected, uploaded, failed and removed files.
- Retry and Remove are named with the file name ("Retry report.pdf", "Remove report.pdf"); the check is named "Ready" or "Uploaded".
- After a file is removed, focus moves to the next row's Remove button, else the previous row's, else the dropzone.
- Keyboard focus rings are 2 px outside the dropzone and the buttons. Hover colors apply to a mouse or a pen only.

### Keyboard

| Keys | Action |
|---|---|
| `Enter` / `Space` on the dropzone | Open the file picker. |
| `Tab` / `Shift+Tab` | Move through the dropzone and each row's Retry and Remove buttons. |

## Motion

- While files are over the dropzone its edge and fill turn to the accent, the cloud lifts 3 px and the label swaps to "Drop to add files".
- Rows open and close their height on a spring, so the list closes the gap when a file is removed. The status line opens the same way and each new message rises in.
- One spring drives the progress bar and the counted percentage. When the count reads 100% the phase turns to "Uploaded", the bar folds away and a check pops in and draws.
- A failed upload swaps its phase to "Upload failed" and pops in Retry.

| Change | Full | Reduced | Off |
|---|---|---|---|
| Row and status line height | Smooth spring, opacity in 240 ms | Appears at once, fades out in 120 ms | Immediate |
| Label, phase and status swaps | Rise and blur, 240 ms in, 160 ms out | Fade out in 120 ms | Immediate |
| Progress | Smooth spring | Jumps | Jumps |
| Check and Retry | Pop on a snappy spring, the check draws in 240 ms | Appear at once | Immediate |
| Dropzone colors and cloud | 240 ms; the cloud on the CSS spring | Immediate | Immediate |

### Responsive behavior

The dropzone and the list fill their container and file names are cut with an ellipsis on one line. On touch, tapping
the dropzone opens the platform's picker. Dropping files from the operating system works in the desktop hosts and in
the browser host (Avalonia 12.1 turns a browser drop into files).

### Performance

Uploads run through your `Uploader`, one call per accepted file, with no limit on how many run at once: queue large
batches yourself. The list is not virtualized, so keep it to a reasonable number of files.

## Related

- `FileDropzone` for picking files without upload state.
- `Progress` for showing how much of a single known task is complete.
