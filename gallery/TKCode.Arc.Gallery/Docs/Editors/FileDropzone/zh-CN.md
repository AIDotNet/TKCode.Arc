---
title: 文件拖放区
summary: 面向拖放一个或多个文件的大面积投放区域。
source-hash: a6b61cd5ce4d
---

## 何时使用

- 附件与上传场景，用户需要看到每个文件、其上传进度以及任何失败信息。
- 可能失败、需要重试的上传，或在上传完成前需要移除的行。
- 需要在投放区旁边展示文件类型、文件大小和文件数量限制的场景。

## 何时不用

- 只需要系统文件对话框而不需要列表：在自己的按钮中调用 `BrowseAsync`，或使用普通的 `Button`。
- 页面为空，拖放是唯一操作：使用 `EmptyState`，以获得更醒目的行动号召。
- 从一组已知值中选择一个：用 `Select` 或 `RadioGroup`。

## 用法

整个投放区都是点击区域，因此在其任何位置点击都会打开系统文件对话框。`Accept` 限制文件类型，`MaxFiles` 与 `MaxFileSize` 分别限制文件数量与单个文件大小。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:FileDropzone Label="添加文件"
                      Description="将文件拖放到此处，或从设备中选择"
                      Accept=".pdf,image/*"
                      MaxFiles="6" />
</UserControl>
```

### 最大 2 MB 的图片

`Accept` 接受扩展名与 MIME 模式。被拒绝的类型或过多的文件会使投放区抖动并显示错误。超过 `MaxFileSize` 的文件会失败，且不会上传。`MaxFileSize` 的单位为字节。

```xml
<arc:FileDropzone Label="添加图片"
                  Description="PNG、JPG 或 WebP，每个最大 2 MB"
                  Accept="image/*"
                  MaxFileSize="2097152"
                  MaxFiles="3" />
```

### 单个文件

将 `AllowMultiple` 设为 `False` 后，新文件会替换当前文件。

```xml
<arc:FileDropzone Label="添加合同"
                  Accept=".pdf"
                  AllowMultiple="False" />
```

### 上传处理程序

`UploadHandler` 依次上传每个文件。通过 `IProgress<double>` 报告进度，并在 `CancellationToken` 被取消时停止。移除正在上传的行会取消其上传。

```csharp
zone.UploadHandler = async (item, progress, token) =>
{
    await UploadToServerAsync(item.File!, progress, token);
};

zone.FilesChanged += (_, e) =>
{
    // e.Files 保存本次新增的文件。
};
```

### 由应用驱动的行

未设置上传处理程序时，由应用驱动每一行。调用 `ReportProgress`、`CompleteUpload` 和 `FailUpload`，或直接设置项的 `Status` 与 `Progress`。`Retry` 与 `Remove` 作用于某一行。

```csharp
zone.ReportProgress(item, 40);
zone.CompleteUpload(item);
zone.FailUpload(item, "服务器已关闭连接", retryable: true);
```

### 投放区内的列表

`ListPlacement="Inside"` 会将列表显示在投放区的边缘之内。设置 `CompactAt` 后，当列出的行数达到该值时，提示会收缩为一条细栏。

```xml
<arc:FileDropzone ListPlacement="Inside"
                  CompactAt="2" />
```

## API 参考

<!-- api: FileDropzone -->

<!-- api: FileDropzoneItem -->

## 无障碍

- 投放区是一个按钮。它的 Tab 停靠点就是投放区本身，按 Enter 或空格键即可打开文件对话框。
- 在列表中，上下方向键会将焦点移动到相邻行的同一操作上。Delete 与 Backspace 移除当前聚焦的行，焦点移动到相邻行。
- 当指针位于投放区上方或焦点位于其中时，Mod+V 可从剪贴板粘贴文件，例如截图。
- 上传中的行会公开一个 0 到 100 的进度条。移除与重试按钮的名称中包含文件名。
- 添加、完成和失败等状态以礼貌方式朗读。被拒绝的文件则以断言方式朗读。

## 动效

- 文件拖到投放区上方时，纸张图标会抬起，虚线边框变为实线；拖动文件经过时会有缓慢的脉冲效果。
- 新行从上方滑入，并从轻微模糊中放大出现，多个新行依次进入。被移除的行会在约 0.3 秒内收起。
- 进度条跟随其目标变化，已完成的行会绘制一个勾选标记。失败的行会抖动一次。
- 减弱动效时，高度与抬起的变化改为短暂淡入淡出，进度变化立即生效，脉冲效果不再运行。

## 相关组件

- `EmptyState`：页面为空、拖放是主要行动号召时使用。
- `Input` 与 `Textarea`：用于非文件类的值。
- `SignaturePad`：用于手绘签名而非附加文件的场景。
