---
title: 视频播放器
summary: "在画面上叠加控制条播放视频：可用章节跳转、调节音量、倍速、字幕和全屏，外观与 Arc 风格一致，由平台自带的播放器解码。"
source-hash: a73f2874046b
---

<Demo name="Preview" />

## 何时使用

- 产品导览、教程、录制的会话和生成的短片，需要用户在应用内观看的场景。
- 希望控制条的外观和行为与界面其他部分一致，并且需要用户能用自己的语言阅读字幕的视频。
- 同一页面有几段短视频：每个播放器初始为暂停，只在显示时才加载媒体。

## 何时不用

- 只有声音：用媒体播放器区块或 `IAudioPlayer`。
- 只有画面、没有控制条，例如循环背景或自定义控制：用 `VideoSurface`。
- 低延迟直播、DRM 保护的媒体或逐帧编辑：本版本不支持。

## 用法

添加 `TKCode.Arc.Video` 并注册，然后在启动时选择播放后端：

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcVideo());

ArcVideoNative.Use();              // desktop: TKCode.Arc.Video.Native
await ArcVideoBrowser.UseAsync();  // browser: TKCode.Arc.Video.Browser
```

桌面后端使用系统自带的播放器：Windows 上是 Media Foundation，macOS 上是 AVFoundation，Linux 上是 GStreamer。应用本身不附带任何编解码器。MP4 加 H.264 视频和 AAC 音频在所有平台都能播放；其他格式取决于平台（WebM 无法在 macOS 上播放，HEVC 在 Windows 上需要扩展，Linux 需要 `gst-plugins-good` 和 `gst-libav` 包）。运行时可以用 `ArcVideo.Backend.CanPlay("video/webm")` 查询。没有后端时，播放器显示生成的测试画面，并静默计时。

把 `Source` 设为地址（`avares://`、`file://`、`http(s)://`）或 `VideoSource`。在视频自身尺寸未知之前，`AspectRatio` 让布局保持稳定；首次播放前一直显示海报。

```xml
<arc:VideoPlayer Source="avares://MyApp/Assets/intro.mp4"
                 Title="Introduction"
                 AspectRatio="1.7778" />
```

`IsPlaying`、`Position`、`Volume`、`IsMuted`、`IsLooping` 和 `PlaybackRate` 都是双向绑定。`Play()`、`Pause()`、`Stop()`、`Seek()` 以及 `PlayPauseCommand`、`StopCommand`、`SkipForwardCommand`、`SkipBackwardCommand`、`ToggleMuteCommand`、`ToggleFullScreenCommand` 命令可以从外部控制播放器。`Stop()` 会暂停、回到开头并重新显示海报；视频保持已加载，所以下次播放会立即开始。`Failed` 会报告视频无法播放的原因。

### 动画影片

改变 `Source` 会在同一个播放器里加载另一个视频。这里是 Blender 基金会两部开放电影的预告片，© Blender Foundation，采用 [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) 许可。

<Demo name="Films" />

### 字幕与章节

把 WebVTT 或 SRT 文件添加为 `TextTracks`。字幕菜单会列出所有字幕轨道，设置文字大小，并可以关闭字幕。`Chapters` 轨道会把进度条分成若干段，并在指针下方显示章节名称。

视频文件本身带的字幕（MP4 定时文本、Matroska 文本轨）在视频加载后会加入 `TextTracks`，排在你自己的轨道之后，`IsEmbedded` 为 true，换源时移除。macOS 和 iOS 读取 MP4 中的字幕，Android 读取 MP4 中的字幕（纯文本，逐条显示），Linux 读取 MP4 和 Matroska 中的字幕（Linux 的 MP4 解复用器会丢掉粗体和斜体）；Windows 和浏览器暂时不列出内嵌字幕。

<Demo name="Captions" />

### 控制条

视频播放时，指针停止移动两秒后控制条会隐藏；暂停时、菜单打开时、指针在控制条上方时，以及控制条有键盘焦点时，它都保持显示。`ShowControls="Always"` 让控制条一直显示；`ControlsHideDelay` 可以修改延迟。`AutoPlay` 只在静音时才会开始播放：浏览器和无障碍指南都不允许自动发出声音。

<Demo name="Controls" />

### 播放后端

播放器用 `ArcVideo.Backend` 中的后端解码，并把画面绘制在窗口内，因此控制条、字幕和圆角都绘制在画面之上。平台允许时（macOS 和 Windows），画面会在 GPU 上从解码器直接送到屏幕，不做复制；其他情况下会复制到位图中。

在 iOS 和 Android 上同样调用 `ArcVideoNative.Use()`：iOS 通过 AVFoundation 播放，Android 通过系统的 MediaPlayer 播放，画面都复制到位图中。在 `net11.0-ios` 或 `net11.0-android` 应用中引用 `TKCode.Arc.Video.Native` 即可（iOS 15、Android API 24 或更高）。

在浏览器中，`BrowserPresentation="Native"` 改为显示浏览器自己的视频元素：不复制画面，浏览器能播放的格式都能播放（例如 Safari 中的 HLS）。这个元素上面不能绘制任何内容，所以控制条放在画面下方并始终显示，字幕由浏览器绘制，也没有圆角；显示海报、错误、重播按钮或菜单时，这个元素会暂时隐藏。没有这种元素时（桌面平台，或 net8.0 版本），播放器仍使用画布。

<Demo name="Playback" />

## API 参考

<!-- api: VideoPlayer -->

<!-- api: VideoTextTrack -->

<!-- api: VideoSource -->

## 无障碍

- 播放器读作一个组，名称取自 `Title`（没有则为"视频"）。它的按钮以功能命名，并随状态变化：“播放”和“暂停”、“静音”和“取消静音”、“全屏”和“退出全屏”；每个提示都显示对应的快捷键。
- 进度条是一个滑块，获得焦点或拖动结束时读作“1 分 5 秒，共 3 分 15 秒”；视频播放时不会每秒播报一次。音量滑块以百分比读出。
- 错误、全屏、字幕开关和倍速变化会礼貌地播报。启用 `CaptionAnnouncements` 后，字幕内容也会播报。
- 隐藏的控制条仍在 Tab 顺序中：按 Tab 会再次显示控制条，并且在它获得焦点时保持显示。
- 字幕遵循 W3C 对大小和背景的要求：字幕菜单和 `CaptionScale` 可以放大文字，`CaptionBackgroundOpacity` 可以加深文字后面的底框。

### 键盘

| 按键 | 作用 |
| --- | --- |
| Space, K | 播放或暂停（焦点在按钮上时，Space 按下该按钮）。 |
| Left / Right | 后退或前进 5 秒（`SeekStep`）。 |
| J / L | 后退或前进 10 秒。 |
| Up / Down | 音量增加或减少 5%。 |
| M | 静音或取消静音。 |
| F | 开启或关闭全屏。Escape 退出全屏。 |
| C | 开启或关闭字幕。 |
| Shift + . / Shift + , | 加快或减慢倍速。 |
| 0 to 9 | 跳转到 0% 至 90% 处。 |
| Home / End | 跳到开头或结尾。 |
| Page Up / Page Down | 在进度条上：跳到上一个或下一个章节（没有章节时为视频的十分之一）。 |

## 动效

- 控制条出现时淡入并上升 8 px，用时 160ms；隐藏时反向进行，指针也随之隐藏。
- 播放和暂停图标交叉淡化，并以 snappy 弹簧缩放。单击画面时，新状态会以中央的大图标显示，图标放大并淡出。
- 指针悬停时进度轨道从 4 px 加粗到 6 px；按下大播放按钮时，按钮下沉到 94%。
- 两次进度报告之间，进度滑块的拇指会按倍速平滑移动。
- 减弱动效：控制条只淡入淡出，图标立即切换，中央反馈和双击涟漪效果省略。视频本身照常播放；静音、循环且自动开始的视频停在第一帧。

## 响应式

- 播放器采用所给的宽度，高度由 `AspectRatio` 决定（视频尺寸未知时为 16:9）。
- 低于 420 px（`CompactWidth`）时，控制条收紧内边距和间距，时间文字变小，音量滑块不再从静音按钮旁边滑出。
- 播放器测量的是自身宽度，而不是窗口宽度，所以窄栏或手机预览中的播放器也使用紧凑布局。
- 触屏上，轻触显示控制条，再轻触一次播放或暂停；双击左侧或右侧三分之一处，后退或前进 10 秒。
- 全屏可以铺满窗口（`FullScreenMode="Window"`），也可以让窗口本身全屏（`System`），退出后回到页面中的原位置。

## 相关组件

- `VideoSurface`：只有画面，用于背景和自定义控制。
- 媒体播放器区块：带播放列表的音频播放器。
- `Carousel`：浏览一组图片。
