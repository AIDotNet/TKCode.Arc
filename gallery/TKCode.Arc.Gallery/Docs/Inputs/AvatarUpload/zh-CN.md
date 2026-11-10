---
title: Avatar upload
summary: 拖入或选择一张照片，在圆形下移动和缩放它，上传时圆环围绕照片逐渐填满，失败时可以重试。
source-hash: 53d68d3e3206
---

<Demo name="Preview" />

## 何时使用

`AvatarUpload` 是用于个人或团队头像的字段。圆形中显示当前头像（没有头像时是一个邀请拖入的虚线圆），旁边是字段标题、一行关于可接受文件的说明，以及上传、更换或移除按钮。新照片会先进入取景步骤：照片位于一个透明的圆形下，用户拖动照片调整位置，用滚轮、双指缩放、滑块或键盘缩放；照片始终盖满圆形。保存时剪出边长为 `OutputSize` 像素的正方形。设置了上传处理程序时，新头像会立即显示，上传时圆环围绕它逐渐填满，进行中可以取消，失败后可以重试。

- **个人设置**：用户自己的头像。
- **工作区、团队或项目设置**：标志或团队合照。
- **新用户引导**：一个可以跳过、以后再做的头像步骤。

## 何时不用

- **多个文件或任意类型的文件**：用 `FileDropzone`，它会列出文件、检查类型并上传。
- **只显示头像**：用 `Avatar`。
- **封面图或横幅**：圆形剪裁不适合宽图；用与图片形状相同的字段来取景。

## 用法

把 `Source` 绑定到当前头像，并设置 `UploadHandler` 把取景后的照片发送到服务器。

```xml
<arc:AvatarUpload x:Name="Photo" Label="Profile photo" />
```

```csharp
Photo.Source = currentAvatar;
Photo.UploadHandler = async (request, progress, cancel) =>
{
    // request.Image is a square bitmap of OutputSize pixels; request.SourceRect is the part of the original.
    await api.UploadAvatarAsync(request.Image, progress, cancel);
};
```

`AvatarUpload` 位于 Special 包中：在 `UseArcControls()` 旁边用 `UseArcSpecial()` 注册。

照片有三种来源：拖到字段上的文件、文件选择器（圆形或上传按钮），或者在代码中调用 `Edit(bitmap)`。文件必须是不超过 `MaxFileSize`（10 MB）的 PNG、JPEG、WebP、GIF 或 BMP 图片；其他文件会被拒绝，标题下方显示原因，并触发 `FileRejected`。较大的照片在取景前会把长边缩小到 2048 像素。上传前会先触发 `Cropped`，带有取景后的照片；没有 `UploadHandler` 时照片会立即应用。

### 状态

`State` 表示字段正在做什么：`Empty`、`Editing`（取景）、`Uploading`、`Done` 或 `Failed`。不使用处理程序、而是在 `Cropped` 中自己上传的应用，可以用 `ReportProgress`、`CompleteUpload` 和 `FailUpload` 驱动圆环。

<Demo name="States" />

| 状态 | 字段显示 |
|---|---|
| `Empty` | 带人像图标的虚线圆，以及“Upload photo” |
| `Editing` | 取景区域、缩放行、取消和保存 |
| `Uploading` | 新头像上覆盖一层浅色遮罩，圆环逐渐填满，以及“Cancel upload” |
| `Done` | 头像（指针经过时上面显示相机图标）、更换和移除 |
| `Failed` | 带红色边缘的新头像、原因、重试和取消 |

取消上传，或在失败后取消，都会回到之前的头像。

### 取景

取景区域是 256px 的正方形，圆形距离边缘 20px。缩放范围从照片刚好盖满圆形的大小，到它的 `MaxZoom`（4）倍。在取景区域按 Enter 保存；在取景步骤的任何位置按 Escape 都会取消。

### 结构

1. **圆形**（`Size`，96px）：头像或空状态；可以拖入文件，也是打开文件选择器的按钮。
2. **进度圆环**：3px，位于圆形外 5px，使用强调色，上传时显示。
3. **标题**（`Label`）和**状态**（`Description`、进度，或用危险色显示的问题）。
4. **按钮**：根据状态显示“Upload photo”或“Change”、“Remove”、“Retry”、“Cancel upload”。
5. **取景步骤**：边缘变暗、中间是透明圆形的区域，缩小、缩放滑块、放大、取消和保存。

### 文案建议

| 不要写 | 改为 |
|---|---|
| Choose a file to upload as your avatar image | Upload photo |
| Error: upload failed (500) | The connection dropped. Try again. |
| Supported formats: image/png, image/jpeg | PNG, JPG or WebP, up to 10 MB |

## API 参考

<!-- api: AvatarUpload -->

<!-- api: AvatarUploadRequest -->

<!-- api: AvatarUploadCroppedEventArgs -->

<!-- api: AvatarUploadRejectedEventArgs -->

<!-- api: AvatarUploadLabels -->

| 类型 | 取值 |
|---|---|
| `AvatarUploadState` | `Empty`、`Editing`、`Uploading`、`Done`、`Failed` |
| `AvatarUploadRejection` | `Type`、`Size`、`Unreadable` |

## 无障碍

- 按钮是真正的按钮，顺序固定；圆形也可以点击，但用键盘选择文件时使用上传或更换按钮。
- 状态行是礼貌型实时区域：进度、被拒绝的文件、失败和结果都会在变化时读出。
- 取景区域是一个可以获得焦点的元素，名为“Photo position”，帮助文本列出了它的按键；开始取景时焦点移到这里，结束后回到选择按钮。
- 失败除了红色边缘，还会显示原因。

### 键盘

| 按键 | 位置 | 作用 |
|---|---|---|
| `Enter` / `Space` | 按钮 | 按下有焦点的按钮 |
| 方向键 | 取景区域 | 移动照片 8px（按住 `Shift` 为 32px） |
| `+` / `-` | 取景区域 | 放大或缩小一级 |
| `0` | 取景区域 | 回到盖满圆形的大小并居中 |
| `Enter` | 取景区域 | 保存 |
| `Escape` | 取景步骤 | 取消 |

## 动效

- 指针经过时，头像上的遮罩和相机图标在 160ms 内淡入；拖着文件经过时，圆形的边缘和填充色会渐变。
- 进度圆环会平滑地接近每个报告的数值，而不是跳过去。
- 缩放按钮和按键以灵敏的弹簧动画缩放；拖动、滚轮和滑块直接跟随输入。
- 减少动效时遮罩在 120ms 内淡入，边缘立即变化，圆环直接跳到每个数值，缩放立即完成；关闭动效时一切立即完成。

## 响应式

- 圆形保持 `Size` 大小；空间较窄时，旁边的标题、状态和按钮会换行。
- 取景步骤宽 256px；把字段放在放得下的地方，小屏幕上可以放进对话框。

## 相关组件

- `Avatar`：显示头像。
- `FileDropzone`：任意类型、一次多个文件。

| 组件 | 适用场景 |
|---|---|
| `AvatarUpload` | 一张在圆形中取景的照片 |
| `FileDropzone` | 多个任意类型的文件，并列出进度 |
| `Avatar` | 只显示头像 |
