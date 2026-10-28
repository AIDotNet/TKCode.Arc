---
title: File upload
summary: 完整的文件选择流程，带类型与大小限制、上传进度和错误反馈。
source-hash: f88aadb60fd9
---

<Demo name="Preview" />

## 何时使用

当用户需要看到上传进度、并能从无效文件中恢复时，用 `FileUpload` 上传文档或媒体。

- **逐个文件显示进度的上传**，可以重试和移除。
- **上传前必须按类型和大小校验的附件**。
- **移除文件即停止上传**：移除文件会取消其上传的令牌。

## 何时不用

- **只选择文件、不需要上传状态**：用 `FileDropzone`。
- **上传后还要映射列的数据**：用专门的导入流程。

## 用法

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

每个被接受的文件在加入后立即通过 `Uploader` 上传：用 progress 报告 0 到 100，上传完成后返回，抛出异常则把文件标为失败。文件被移除、被新文件替换（`Multiple="False"`）或控件离开可视树时，令牌会被取消。没有上传函数时，文件只做校验并列出，被接受的文件显示一个对勾。

### 类型与大小

<Demo name="Validation" />

`Accept` 接受逗号分隔的扩展名（`.pdf`）、MIME 类型（`application/zip`）和通配符（`image/*`）。Avalonia 的文件不带 MIME 类型，所以控件按文件扩展名在内置的常见类型表中查找；未知扩展名只能按扩展名匹配。`MaxSize` 是允许的最大字节数。被拒绝的文件留在列表中并显示错误（"This file type is not accepted." 或 "File is larger than 1 MB."），不会上传。文件选择器同样按 `Accept` 过滤。

```xml
<arc:FileUpload Accept=".csv,text/plain" MaxSize="1048576"
                Label="Import contacts" Description="CSV or plain text, up to 1 MB." />
```

### 单个文件

<Demo name="SingleFile" />

设置 `Multiple="False"` 时，选择器只取一个文件，新文件会替换整个列表并取消正在进行的上传。

```xml
<arc:FileUpload Multiple="False" Accept="image/*" Label="Upload a cover image" />
```

### 禁用

<Demo name="Disabled" />

`IsEnabled="False"` 会让放置区变暗，不再打开选择器，也不接受拖放。

### 文件列表

`Files` 是文件列表（最新的在最后），元素为 `FileUploadItem`。它双向绑定：添加或移除文件时控件会替换它，并以新列表引发 `FilesChanged`。每一项有 `File`（`IStorageFile`）、`Name`、`Size`、`Error`，以及上传的 `Status`（`None`、`Uploading`、`Done`、`Failed`）和 `Progress`。

```csharp
upload.FilesChanged += (_, e) => SaveButton.IsEnabled = e.Files.All(f => f.Status == FileUploadStatus.Done);
```

`BrowseAsync()` 打开选择器，`AddFilesAsync(storageItems)` 从你自己的来源（粘贴、另一个放置目标）添加文件，`AddFiles(items)` 添加你用名称和大小创建的项。`Retry(item)` 和 `Remove(item)` 与行内按钮的作用相同。

### 文本

`Label`（"Upload files"）、`Description`（"Drop files here or browse from your device."）和 `BrowseLabel`（"Browse"）都是属性。文件悬停在放置区上方时，标签变为 "Drop to add files"（或 "Drop to add a file"）。状态行的内容例如 "2 files added. 1 file needs attention."、"report.pdf uploaded."、"report.pdf could not be uploaded." 和 "report.pdf removed."；`Status` 保存最后一条。

## API 参考

<!-- api: FileUpload -->

<!-- api: FileUploadItem -->

<!-- api: FileUploadStatus -->

<!-- api: FileUploadFilesChangedEventArgs -->

## 无障碍

- 放置区是一个按钮，名称取自 `Label`，说明取自 `Description`。
- 礼貌模式的实时区域会播报添加、拒绝、上传完成、上传失败和移除的文件。
- 重试和移除按钮的名称带有文件名（"Retry report.pdf"、"Remove report.pdf"）；对勾的名称是 "Ready" 或 "Uploaded"。
- 移除文件后，焦点移到下一行的移除按钮，没有则移到上一行的，再没有则回到放置区。
- 键盘焦点环位于放置区和按钮外侧 2 px。悬停颜色只对鼠标和触控笔生效。

### 键盘

| 按键 | 操作 |
|---|---|
| 放置区上的 `Enter` / `Space` | 打开文件选择器。 |
| `Tab` / `Shift+Tab` | 在放置区和每一行的重试、移除按钮之间移动。 |

## 动效

- 文件悬停在放置区上方时，边框和填充变为强调色，云图标上移 3 px，标签切换为 "Drop to add files"。
- 行的高度以弹簧展开和收起，所以移除文件后列表会合拢空隙。状态行以同样方式展开，每条新消息上升淡入。
- 进度条和计数百分比由同一个弹簧驱动。计数达到 100% 时阶段变为 "Uploaded"，进度条折叠消失，对勾弹出并描绘出来。
- 上传失败时阶段切换为 "Upload failed"，并弹出重试按钮。

| 变化 | 完整 | 减弱 | 关闭 |
|---|---|---|---|
| 行与状态行的高度 | 平滑弹簧，不透明度 240 ms 进入 | 立即出现，120 ms 淡出 | 立即 |
| 标签、阶段与状态的切换 | 上升并模糊，进入 240 ms，退出 160 ms | 120 ms 淡出 | 立即 |
| 进度 | 平滑弹簧 | 直接跳到 | 直接跳到 |
| 对勾与重试 | 以灵敏弹簧弹出，对勾 240 ms 描绘 | 立即出现 | 立即 |
| 放置区颜色与云图标 | 240 ms；云图标用 CSS 弹簧 | 立即 | 立即 |

### 响应式行为

放置区和列表填满容器，文件名单行显示，超出部分以省略号截断。在触摸设备上，点击放置区会打开平台的文件选择器。桌面宿主和浏览器宿主都支持从操作系统拖入文件（Avalonia 12.1 会把浏览器中的拖放转换为文件）。

### 性能

上传通过你的 `Uploader` 进行，每个被接受的文件调用一次，同时进行的上传数量没有限制：大批量文件请自行排队。列表没有虚拟化，文件数量应保持在合理范围内。

## 相关组件

- `FileDropzone`：只选择文件、不需要上传状态。
- `Progress`：显示单个已知任务的完成程度。
