---
title: 视频画面
summary: "只显示视频的画面、上面不叠加任何内容，可用作背景或自定义控制的底层；它和其他控件一样可以裁剪、淡入淡出和变换。"
source-hash: 5ceb3d490648
---

<Demo name="BackgroundLoop" />

## 何时使用

- 放在首屏大图、卡片或登录页背后的静音循环视频。
- 用 `IVideoPlayer` 驱动、由你自己设计控制条的播放器。
- 同一个播放器的多个画面，例如缩略图和大图视图。

## 何时不用

- 需要用户操作的视频：用 `VideoPlayer`，它内置控制条、字幕、键盘和屏幕阅读器支持。
- 静态图片：用 `Image`。

## 用法

从后端创建播放器，把它交给画面控件，用完后释放。画面控件不拥有播放器。

```csharp
var player = ArcVideo.CreatePlayer();
surface.Player = player;
await player.LoadAsync(VideoSource.FromUri(new Uri("avares://MyApp/Assets/loop.mp4")));
player.IsMuted = true;
player.IsLooping = true;
player.Play();
```

`Stretch` 决定画面如何填满控件：`Uniform` 加上黑边（`LetterboxBrush`，默认为黑色），`UniformToFill` 裁剪，`Fill` 拉伸，`None` 保持视频原尺寸。`CornerRadius` 同时圆角化画面和黑边。其他控件可以叠加在画面控件之上，它也可以像其他控件一样被裁剪、淡化和变换。

画面控件只在播放器播放且控件可见时请求新帧，并且只请求不大于其实际绘制尺寸的帧。`RenderPath` 决定帧如何到达：`GpuInterop`（不做复制，直接导入合成器，适用于 macOS 和 Windows）或 `Bitmap`（复制）。`PreferBitmapPath` 强制使用复制路径，例如用来排除显卡驱动问题。

### 自定义控制

<Demo name="CustomChrome" />

## API 参考

<!-- api: VideoSurface -->

<!-- api: IVideoPlayer -->

## 无障碍

- 画面控件只是一张没有名称、也没有控制的图片。当它承载含义时，用 `AutomationProperties.Name` 为它命名，并提供暂停它的方式。
- 自行运动超过五秒的视频需要提供暂停方式（WCAG 2.2.2）。只有在未减弱动效时才开始装饰性循环，如示例所示。
- 自定义控制需要名称、键盘访问，以及语音字幕：`VideoPlayer` 已内置这些功能。

## 动效

- 画面控件绘制播放器交出的帧，本身不添加任何动效。
- 减弱动效不会停止正在播放的视频；是否开始装饰性循环由应用决定（`ArcMotion.GetEffectiveLevel`）。

## 响应式

- 画面控件采用布局给予它的尺寸；它没有首选尺寸，因此需要为它设置高度或宽高比。
- 它按实际绘制的尺寸向播放器请求帧，因此小控件中的大视频开销很小。
- 使用 `Stretch="UniformToFill"` 时，画面会持续填满形状变化的控件，裁剪左右两侧或上下两侧。

## 相关组件

- `VideoPlayer`：带控制条、字幕和键盘支持的视频播放器。
- `Image`：静态图片。
