---
title: File dropzone
summary: A generous target for dropping one or more files.
---

<Demo name="Upload" />

## When to use

`FileDropzone` is the attachment target for a form or a page that collects files. It combines the drop target, the
system file picker, clipboard paste and a list of rows that shows each file with its upload state, so the person
sees what was accepted, what is still uploading and what failed, without leaving the page. It sits where the files
belong: under the field that owns them, inside a card or a settings section.

- **Invoice and receipt attachments on a billing or expense page**: a finance user drops a stack of PDF receipts,
  sees each one with its size, and removes the one that was scanned twice. `MaxFiles` and `MaxFileSize` state the
  limits next to the target, so nobody discovers them only after the upload fails.
- **Screenshots and logs on a support ticket**: a support agent copies a screenshot and presses Ctrl+V while the
  pointer is over the target, which adds the image as a row with a thumbnail. Pasting needs no extra button.
- **Bulk data import in a CRM or an admin console**: a single CSV file with `AllowMultiple` set to `False` and
  `Accept=".csv"`. A new file replaces the old one, so there is never a question of which file the import uses.
- **Identity documents in a fintech onboarding flow**: images and PDFs under a fixed size, with the upload handed to
  the app's own secure endpoint through `UploadHandler`. The failure reason of a rejected upload is shown on its row.
- **Release assets in a developer tool**: the app runs its own upload queue and reports progress for each row with
  `ReportProgress`, `CompleteUpload` and `FailUpload`, so the list reflects the queue the app already has.

## When not to use

- **Only the system file dialog is needed, and no list of files**: call `BrowseAsync` from your own `Button`, or use
  a plain `Button` that opens the picker in the app. A dropzone without a list adds an area the person does not need.
- **An empty page where dropping is the only action**: use `EmptyState` for a larger call to action with a
  heading and a short explanation, such as the first import on a new workspace.
- **Choosing one value from a known set**: use `Select` or `RadioGroup`. A file is a value with an upload life
  cycle, so do not use the dropzone as a picker for a fixed option.
- **Drawing a signature rather than attaching a file**: use `SignaturePad`, which produces a stroke-based image in the
  app and does not read a file from disk.
- **Writing a body that contains pictures and formatting**: use `RichTextEditor`, which keeps the text and the
  formatting in one document. The dropzone only lists files beside the document.

## Usage

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FileDropzone Label="Add files"
                      Description="Drop files here or choose from your device"
                      Accept=".pdf,image/*"
                      MaxFiles="6" />
</UserControl>
```

`FileDropzone` derives from `TemplatedControl`, not from a `Button`. The whole target is a click area: its inner
button opens the system file picker, so a click, Enter or Space anywhere on the target opens it. The rows, the error row and the drop feedback are part of the same template, so the control keeps one
label, one description and one list in every state. Drag and drop, paste and the picker all feed the same
validation path, which means one set of rules (`Accept`, `MaxFiles`, `MaxFileSize`, `AllowMultiple`) applies to
every way a file arrives.

### Anatomy

1. **Target surface**: the drawn zone with the surface background, a dashed edge and rounded corners. It uses the
   surface radius (34 px at the default size) and folds to the panel radius (26 px) in compact mode. The background
   changes to the muted surface color on hover and while the target is pressed.
2. **Paper icon**: three stacked sheets with an arrow. On hover the sheets lift 3 px, and while files are dragged
   over the target the dashed edge closes into a solid edge tinted with the accent color.
3. **Label** (`Label`): the target's heading in the foreground color. While files hover over the target, it shows
   the drop copy instead: `DropLabel` when set, otherwise "Drop to upload" with an upload handler, "Drop to add
   files" for several files and "Drop to add the file" for a single file.
4. **Description** (`Description`): one line under the label that explains what to do.
5. **Note** (`Note`): a small line under the description. By default it lists the accepted types ("Accepted:
   .pdf,image/*") or the limit ("Up to 6 files"). It is hidden in compact mode.
6. **Error row** (`ErrorMessage`, read-only): a line with a danger-colored alert icon under the target. It appears
   when files are refused and stays until the next successful add or a removal.
7. **File list**: the rows below the target (`ListPlacement="Below"`, the default) or inside its edge
   (`Inside`), with 12 px side padding. Rows are listed in the order they were added.
8. **Row**: a thumbnail (a decoded 80 px preview with 10 px corners) or a type icon, the file name, the size, the
   status, a 4 px progress bar, a Retry button 30 px tall and a Remove button 32 px square. A row is at least 58 px
   high. Below 440 px of row width the Retry button shows only its icon, and below 300 px the type icon is hidden.

### Choose the accepted types and the limits

`Accept` takes extensions (".pdf") and MIME patterns ("image/*", "application/pdf"), separated by commas, the same
way an HTML file input does. An extension matches the end of the file name, a pattern with `/*` matches the MIME
type's prefix, and any other value must equal the MIME type exactly. The system picker builds its filter from the same
list. Leave `Accept` empty to accept every file.

Set the limits from the business rule, not from the default:

- `MaxFiles` (default 5) caps the list. A batch that goes over the cap adds the files that fit and refuses the rest.
- `MaxFileSize` (in bytes, default none) fails any larger file on its row. The file is listed with the reason, has no
  Retry button, and is never uploaded. Remove it and add a smaller file instead.
- `AllowMultiple` (default `True`) allows a list. With `False`, a new file replaces the current one and cancels the
  upload of the file it replaces.

<Demo name="Images" />

The Images demo accepts images only, up to 2 MB each (`MaxFileSize="2097152"`), with three files at most. The
description states the limit in the words the person needs: the format and the size.

### Row status

Each `FileDropzoneItem` has a `Status` from the `FileDropzoneStatus` enum, which decides what the row shows.

| Status | Meaning | What the row shows |
|---|---|---|
| `None` | A plain selection; nothing is uploading | The file, its size and no progress bar |
| `Uploading` | The upload is running; `Progress` goes from 0 to 100 | The progress bar and a percentage |
| `Uploaded` | The upload finished; `Progress` is 100 | A check mark on the row |
| `Failed` | The upload or the checks failed; `Error` holds the reason | The reason, a red edge and Retry when it is allowed |

`FileDropzoneItem` raises `PropertyChanged` for every property, so an app can set `Status`, `Progress` and `Error`
directly. The methods on the dropzone do the same and also announce the change to assistive technology, so prefer
them when the app drives the row.

### Single file

With `AllowMultiple` set to `False`, the dropzone holds one file. A new file replaces the current one, cancels the
upload of the old file and raises `FilesChanged`.

<Demo name="SingleFile" />

Use it for a contract or an import, where a second file would be a mistake. The drop copy then speaks in the
singular ("Drop to add the file").

### Upload handler

Set `UploadHandler` to upload each added file. The handler receives the row, an `IProgress<double>` that reports
0 to 100, and a `CancellationToken`:

- Report progress as the bytes go out. The bar never moves back, so a retry that starts from zero does not look
  like a reset to the person; values outside 0 to 100 are clamped.
- Return normally to complete the row. Throw to fail it: the exception message becomes the reason on the row, and
  an empty message shows "Upload failed".
- Observe the token. Removing a row that is still uploading cancels its token, and a cancelled upload neither
  completes nor fails the row.

```csharp
zone.UploadHandler = async (item, progress, token) =>
{
    await UploadToServerAsync(item.File!, progress, token);
};

zone.FilesChanged += (_, e) =>
{
    // e.Files holds the files the visitor added and still has listed.
};
```

Retry is offered only when an `UploadHandler` is set and the row is `Failed` with `IsRetryable` true. Pressing it
starts the handler again for that row. `FilesChanged` is raised after files are added and after files are removed;
its `Files` list holds the visitor's files, not the rows the app created.

### Application-driven rows

Without an upload handler the app drives each row. Call `ReportProgress`, `CompleteUpload` and `FailUpload`, or set
the item's `Status` and `Progress` directly. Remove acts on any row, and `Retry` restarts an upload only through
the handler, so an application-driven failure has no Retry button. The app restarts a failed upload itself and
reports progress on the same row.

<Demo name="AppDriven" />

```csharp
zone.ReportProgress(item, 40);
zone.CompleteUpload(item);
zone.FailUpload(item, "The server closed the connection", retryable: true);
```

`FailUpload` treats an empty reason as "Upload failed". Pass `retryable: false` when the failure cannot be fixed by
sending the same file again, such as a file the server rejects for its format. Such a row shows its reason and
only Remove.

### Rows inside the target

`ListPlacement="Inside"` shows the rows within the target's edge, with 12 px side padding and 12 px at the bottom.
Use it when the dropzone is the whole panel, such as an upload card with its own heading. With `CompactAt`, the
prompt folds to a slim bar once that many rows are listed: the icon and the note are hidden, the corners tighten
and the padding shrinks. A file dragged over the folded target brings the icon back, so the target still invites
a drop.

<Demo name="ListInside" />

### Refusals and limits

A refused file is never listed, and the target shows the reason in its error row. The accepted files of the same
batch are still added.

| Rule | When it is checked | What the person sees |
|---|---|---|
| Type not in `Accept` | When the files are added | "<name> is not an accepted file type." for one file, or "Some files were not added because their type is not accepted." for several; the target shakes |
| Too many files for `MaxFiles` | When the files are added | "You can add up to 6 files." (or "1 file" when the limit is 1); the target shakes |
| Larger than `MaxFileSize` | When the file is added | The row fails with "File is larger than 2 MB" and no Retry button; the file is not uploaded |
| Same file twice | When the files are added | A file with the same name, size and modified time as one already listed is skipped silently |
| Failed upload | While or after the upload runs | The row shows the reason from the handler or `FailUpload`; Retry when it is allowed |

Sizes use `FileDropzoneItem.FormatSize`: bytes, whole kilobytes, or megabytes with one decimal below 10 MB.

### Binding to a view model

The dropzone is a list editor: the app owns the files and their upload, and the control shows them. The
`UploadHandler` is a delegate, not a command, so set it in code behind and keep the upload logic in the view model.
`Items` is a read-only collection property and is not bindable, so the view model keeps the state it needs and
reacts to `FilesChanged`.

```csharp
public partial class ExpenseReportViewModel : ObservableObject
{
    private readonly IReceiptStore _receipts;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private int _receiptCount;

    [ObservableProperty]
    private string? _submitError;

    public ExpenseReportViewModel(IReceiptStore receipts) => _receipts = receipts;

    public Task UploadReceiptAsync(FileDropzoneItem item, IProgress<double> progress, CancellationToken token) =>
        _receipts.UploadAsync(item.File!, progress, token);

    public void OnReceiptsChanged(int count) => ReceiptCount = count;

    private bool CanSubmit() => ReceiptCount > 0;

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        SubmitError = await _receipts.SubmitAsync() ? null : "Could not submit the report. Try again.";
    }
}
```

```csharp
// Code behind of the view that hosts the dropzone.
public partial class ExpenseReportView : UserControl
{
    public ExpenseReportView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ExpenseReportViewModel vm)
        {
            Receipts.UploadHandler = vm.UploadReceiptAsync;
            Receipts.FilesChanged += (_, args) => vm.OnReceiptsChanged(args.Files.Count);
        }
    }
}
```

```xml
<StackPanel Spacing="16">
    <arc:FileDropzone x:Name="Receipts"
                      Label="Add receipts"
                      Accept=".pdf,.jpg,.jpeg"
                      MaxFiles="8" />
    <arc:Button Content="Submit expense report"
                Command="{Binding SubmitCommand}"
                IsDefault="True" />
    <TextBlock Text="{Binding SubmitError}" />
</StackPanel>
```

- `UploadHandler`, `Accept`, `MaxFiles` and `MaxFileSize` are one-way settings: the view model sets them once, and
  the control does not write them back.
- `Label`, `Description`, `Note` and `DropLabel` are one-way: bind them to a view model string when the copy depends
  on the workflow (for example, "Add the signed contract").
- `FilesChanged` and `FilesRejected` are the events to listen to; `IsDragOver` and `ErrorMessage` are read-only.
- The submit command reads `ReceiptCount` from `FilesChanged`. A failed upload stays listed, so an app that needs
  only completed receipts checks each `Status` in `Items` before submitting.

### Example: expense receipts on an approval page

A finance user attaches receipts to an expense report. The label names the object, the description carries the
rule that matters (format and size), the note shows the file types, and the list shows the three states a receipt
can be in at once: uploaded, still uploading and failed. The failed row has no Retry button because this scene
has no upload handler; in the running page the handler supplies Retry.

![A receipts dropzone with an uploaded receipt, an uploading receipt and a failed receipt](images/ExpenseReceipts.png)

```xml
<StackPanel Width="640" Spacing="16">
    <TextBlock Text="Expense report"
               FontSize="18"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:FileDropzone Label="Add receipts"
                      Description="PDF or JPG, up to 10 MB each. Attach every receipt over 25 USD."
                      Accept=".pdf,.jpg,.jpeg"
                      MaxFiles="8"
                      MaxFileSize="10485760">
        <arc:FileDropzone.Items>
            <arc:FileDropzoneItem>
                <x:Arguments>
                    <x:String>taxi_2026-04-02.pdf</x:String>
                    <x:Int64>248000</x:Int64>
                    <arc:FileDropzoneStatus>Uploaded</arc:FileDropzoneStatus>
                </x:Arguments>
            </arc:FileDropzoneItem>
            <arc:FileDropzoneItem Progress="64">
                <x:Arguments>
                    <x:String>hotel_folio_march.pdf</x:String>
                    <x:Int64>1258291</x:Int64>
                    <arc:FileDropzoneStatus>Uploading</arc:FileDropzoneStatus>
                </x:Arguments>
            </arc:FileDropzoneItem>
            <arc:FileDropzoneItem Error="The server closed the connection">
                <x:Arguments>
                    <x:String>client_dinner_scan.jpg</x:String>
                    <x:Int64>3145728</x:Int64>
                    <arc:FileDropzoneStatus>Failed</arc:FileDropzoneStatus>
                </x:Arguments>
            </arc:FileDropzoneItem>
        </arc:FileDropzone.Items>
    </arc:FileDropzone>
</StackPanel>
```

### Content guidelines

- **Label**: name the object in the plural noun the person expects: "Add receipts", "Attach contracts", "Add images".
  Use the verb "Add" for the target, and avoid "Upload", which describes the network step the person does not see.
- **Description**: state the constraint the person must meet: the format and the size. Say the consequence where it
  matters, such as "Attach every receipt over 25 USD".
- **Note**: keep the default unless the rule needs a sentence. Write the accepted types as the person would name
  them ("PDF or JPG") in the description, and the extensions in the note.
- **Row reason**: say what happened and what to do, in words the person understands, not the raw server message.

| Instead of | Write |
|---|---|
| Upload files | Add receipts |
| Invalid file | Use a PDF or JPG file. |
| Max 8 | Up to 8 files |
| File too big | File is larger than 10 MB |
| Something went wrong | Upload failed. Remove the file and add it again. |

### Best practices

**Do**

- Show the limits next to the target in the description and the note, before the person drops anything. A refusal
  after the drop is recoverable, but the rule should already be visible.
- Keep one upload handler per dropzone and let it report progress for each row. A row that stays at 0 looks stuck
  even when the upload is working.
- Pass the real reason to `FailUpload` or throw a message the person can act on. The reason is the only help the
  row can give.
- Set `AllowMultiple` to `False` for a document that replaces the previous one, so the list never holds two
  contracts for the same field.
- Check each row's `Status` before you submit, so a failed file is fixed before the form goes out.

**Avoid**

- Using the dropzone as the only way to attach a file. Keyboard users have the Enter and Space path, but a visible
  button beside the target helps people who expect a button, and it lets a screen reader user find the action.
- Setting `MaxFileSize` without saying it in the description. A file that fails for size with no warning reads as a
  bug.
- Showing a file's raw exception text as the reason. Stack traces and status codes do not help the person; write the
  message for them and log the detail.
- Using `CompactAt` on a review step where the person must check each file. Compact mode suits a crowded panel.
- Relying on paste alone. Paste works only while the pointer is over the target or focus is inside it, so a person
  must know where to paste.

### Common questions

**Why was my file skipped without a message?** A file with the same name, size and modified time as one already in
the list is a duplicate and is skipped without an error. A file that is refused for its type or for the limit
always produces a message on the error row and a `FilesRejected` event.

**How do I remove every file, for example when the person starts over?** Loop over a copy of `Items` and call
`Remove` for each row: `foreach (var item in zone.Items.ToList()) zone.Remove(item);`. Removing a row that is
uploading cancels its upload, and `FilesChanged` is raised once for each visitor file.

**Does the dropzone upload files on its own?** No. It uploads only when `UploadHandler` is set; without a handler it
lists the files and leaves the upload to the app, which calls `ReportProgress`, `CompleteUpload` and `FailUpload`.

**Can I change the label while files are dragged over the target?** Yes. Set `DropLabel` to the copy that should
show while files hover, such as "Drop the signed contract". When it is empty, the default drop copy depends on
whether an upload handler is set and on `AllowMultiple`.

**Why does a failed row have no Retry button?** Retry appears only when an `UploadHandler` is set, the row is
`Failed` and `IsRetryable` is `True`. A file that is too large is marked not retryable, and an app-driven failure
has no handler to call.

## API reference

<!-- api: FileDropzone -->

<!-- api: FileDropzoneItem -->

## Accessibility

- The target is a button named by `Label`. Its help text combines the description and the note, so a screen reader
  reads what the target accepts and how many files it takes.
- The list is a named group ("Files"). It is hidden from assistive technology while it is empty and appears when the
  first row is added.
- Each row's progress bar exposes a range from 0 to 100. The Retry and Remove buttons include the file name in their
  accessible name, so "Remove hotel_folio_march.pdf" is distinct in a list of identical buttons.
- Adding, uploading, completing, failing and removing a file are announced politely. A refusal is announced
  assertively, because the person must act on it.
- Long file names and failure reasons are shortened to one line on screen; the full text is in the row's tooltip and
  its accessible name.
- Focus stays on the control when a row is removed: it moves to the neighboring row's Remove button, or to the target
  when there are no rows left.

### Keyboard

| Keys | Action |
|---|---|
| `Tab` / `Shift+Tab` | Move through the target, then the rows' Retry and Remove buttons |
| `Enter` / `Space` on the target | Open the system file picker, filtered by `Accept` |
| `Up` / `Down` in the list | Move focus to the same action (Retry or Remove) on the neighboring row |
| `Delete` / `Backspace` on a row | Remove the focused row; focus moves to a neighboring row |
| `Ctrl+V` (`Cmd+V` on macOS) | Add the files on the clipboard, such as a screenshot, while the pointer is over the target or focus is inside it; ignored while a text box has focus |

## Motion

- Dragging a file over the target lifts the paper icon, closes the dashed edge into a solid accent-tinted edge and
  starts a slow breathing pulse (scale 1 to 1.012 every 2.2 seconds). The pulse stops as soon as the drag ends.
- Rows slide in from above with a slight blur, one after another; removed rows collapse in about 0.3 seconds.
- The progress bar follows its target on a smooth spring, a completed row draws its check mark, and a failed row or
  a refused batch shakes once, for about 0.42 seconds.
- Reduced motion swaps the height and lift changes for short fades and makes progress immediate. The pulse and the
  shake do not run.

| Change | Full | Reduced | Off |
|---|---|---|---|
| A file is dragged over the target | Paper lifts, dashed edge closes, the target breathes | Edge and tint change at once; no pulse | Immediate |
| A row is added | Row slides in, scales up from a slight blur, staggered | Short fade | Immediate |
| A row is removed | Height collapses in about 0.3 seconds | Short fade | Immediate |
| Upload progress changes | Bar follows the value on a smooth spring | Immediate | Immediate |
| A row fails, or a batch is refused | Target or row shakes once | No shake; color change only | Immediate |

## Related

- `EmptyState` for an empty page where dropping is the main call to action.
- `Button` for a visible action that opens the picker, or for the submit action beside the list.
- `Input` and `Textarea` for values that are not files.
- `SignaturePad` for a signature that is drawn rather than attached.
- `RichTextEditor` for a document body that holds text and pictures together.

| Component | Choose it when |
|---|---|
| `EmptyState` | An empty page needs a larger call to action with a heading |
| `Button` | Only the system picker is needed, with no list of files |
| `Select` / `RadioGroup` | The person picks one value from a known set |
| `SignaturePad` | A signature is drawn in the app rather than read from disk |
| `RichTextEditor` | The content is text and pictures in one document |
| `Input` / `Textarea` | The value is a string, not a file |
