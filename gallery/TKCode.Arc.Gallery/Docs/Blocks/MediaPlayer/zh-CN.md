---
title: 媒体播放器
summary: 带封面、播放列表、进度条、上一首 / 下一首和音量与静音的音频播放器；切换曲目会循环并保持播放或暂停，正在播放的那一行显示跳动的竖条。
source-hash: 294ec276aedb
---

<Demo name="Preview" />

## 何时使用

`MediaPlayer` 在一张卡片里播放一小组音频曲目。上方是当前曲目的封面和标题，标题旁是上一首、播放和下一首按钮，下面是进度条和已播放 / 总时长，再往下是播放列表，列表标题行里有静音按钮和音量滑块。点击某一行会播放该曲目；点击当前行则暂停或继续。

- **节目、课程与试听。** 播客的最新几期、课程某一单元的音频，或放在商品旁边的一组声音预览。
- **合集与原声。** 乐队主页、游戏原声，或专注类应用里的几段环境音循环。
- **录音与语音备忘。** 一小组语音备忘或通话录音，原地回听。

## 何时不用

- **大型曲库。** 播放列表把每首曲目都显示为一行，没有搜索、排序和虚拟化；曲库请用 `DataGrid` 或列表实现，播放器只放待播的曲目。
- **视频。** 区块只播放音频；封面是一张静态图片。
- **单个声音。** 只有一段音频时，一个播放按钮加时间就够了。

## 用法

### 曲目与音频

每个 `MediaTrack` 有 `Title`、`Artist` 和 `Album`（在标题下显示为“艺人 · 专辑”）、`Cover` 封面图及供屏幕阅读器使用的 `CoverDescription`、作为 `AudioSource` 的音频，以及 `Duration`：曲目加载前播放列表显示它，加载后由音频报告的时长取代。`AudioSource.FromUri`、`FromFile` 和 `FromBytes` 分别对应网络地址、本地文件和内存中的音频；`WavFile.Encode` 把生成的采样写成 WAV 文件，Gallery 的三段循环就是这样做出来的。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MediaPlayer>
        <arc:MediaTrack Title="Harbor lights" Artist="Low Static" Album="Night ferry" Duration="0:0:24" />
        <arc:MediaTrack Title="Paper kites" Artist="Low Static" Album="Night ferry" Duration="0:0:24" />
    </arc:MediaPlayer>
</UserControl>
```

```csharp
player.Tracks.Add(new MediaTrack
{
    Title = "Harbor lights",
    Artist = "Low Static",
    Album = "Night ferry",
    Source = AudioSource.FromUri(new Uri("https://cdn.example.dev/audio/harbor-lights.mp3")),
});
```

### 发出声音

播放器通过 `ArcAudio.Backend` 播放；设置了自己的 `AudioBackend` 时则用它。默认后端只计时、不发声，所以播放器在任何平台（包括测试）中的表现都一样；`IsAudible` 表示当前用的是哪一种。要发出声音，请添加对应平台的包，并在应用启动时选用它：

- **桌面**（Windows、macOS、Linux）：`TKCode.Arc.Audio.Native`，基于 miniaudio，播放 WAV、MP3 和 FLAC。
- **WebAssembly**：`TKCode.Arc.Audio.Browser` 通过浏览器的音频元素播放，浏览器能放的它都能放。浏览器只有在用户点过页面上的东西之后才允许开始播放音频。

Gallery 的桌面和浏览器宿主都选用了各自的后端，所以本页的示例会发出声音。

### 播放、切换与曲目结束

`IsPlaying`、`SelectedIndex` 和 `Volume` 都是双向的。`Play`、`Pause`、`TogglePlay`、`NextTrack`、`PreviousTrack`、`PlayTrack` 和 `Seek` 在代码里完成按钮做的事。切换曲目会循环（最后一首之后是第一首），并保持播放器原来的播放或暂停状态；新曲目总是从头开始。上一首总是切到上一首曲目，不管当前曲目播放到了哪里。曲目播放到结尾时会停下并回到 0:00，不会自动切到下一首，并触发 `TrackEnded`；需要连续播放时处理这个事件即可。

<Demo name="Events" />

### 音量与静音

音量从 0 到 1，初始为 0.7。静音按钮把音量设为零并记住原来的值；再按一次恢复这个音量，如果是把滑块拖到零的，则恢复为 `DefaultVolume`。音量为零时 `IsMuted` 为 true。

### 不带卡片

`IsFramed="False"` 去掉边框、背景和圆角，适合放在页面或自有面板上的播放器。

<Demo name="Flat" />

### 窄容器

布局跟随播放器自身的宽度。宽度小于 `CompactWidth`（420）时，内边距从 20 收紧到 14，封面变高（从 2.2:1 变为 1.7:1），标题从 28 缩小到 22，按钮及其间距变小，音量滑块从 88 缩短到 64。播放器最宽 520。

<Demo name="Compact" />

### 事件与文字

`SelectedTrackChanged` 带有旧曲目、新曲目以及切换的来源（`Code`、`Skip` 或 `Playlist`）；`PlaybackFailed` 带有后端给出的消息，同时播放器暂停。播放器显示或读给屏幕阅读器的每个词都在 `Labels` 里，它是一个 `MediaPlayerLabels`，可以整体替换来翻译；带数值的文字是函数，`FormatTime` 决定时间的写法。

## API 参考

<!-- api: MediaPlayer -->

<!-- api: MediaTrack -->

<!-- api: MediaPlayerLabels -->

## 无障碍

- **Tab** 依次经过上一首、播放、下一首、进度条、静音按钮、音量滑块和播放列表的每一行。
- **Enter** 或 **Space** 按下获得焦点的按钮或行。
- 在进度条上，**Left** 和 **Right**（或 **Down** 和 **Up**）每次移动十分之一秒，**Page Up** 和 **Page Down** 移动曲目的十分之一，**Home** 和 **End** 到开头和结尾。在音量滑块上，方向键每次 1%，翻页键每次 10%。
- 播放器的名称是“Audio player”。按钮的名称是“Previous track”、“Play”或“Pause”、“Next track”以及“Mute audio”或“Unmute audio”；滑块的名称是“Seek”加标题（值读作“0:12 of 0:24”）和“Volume”（值读作“70%”）。
- 标题是二级标题，“Playlist”是三级标题。每一行的名称是“Play”加标题，该曲目播放时为“Pause”加标题；跳动的竖条名为“Playing”。
- 封面按 `CoverDescription` 朗读；没有封面的曲目显示一块空底板，屏幕阅读器会跳过。
- 焦点环只在键盘操作时显示。

## 动效

- **切换曲目**：封面在 240 ms 内交叉淡化。旧标题块上移 10 px 并淡出，新标题块从下方 10 px 以 Smooth 弹簧升到原位；无论往哪个方向切换都向上。当前行旁的竖线以弹簧移到新行（Smooth，约 550 ms）。
- **播放与暂停**：旧图标缩小到 60% 并模糊淡出，新图标从 60% 和 2 px 模糊中放大出现，历时 120 ms。按下的按钮在 120 ms 内缩小到 94%；悬停底板在 160 ms 内淡入。
- **播放中**：当前行的三根竖条在 30% 与满高之间起伏，每个方向 900 ms，彼此相差三分之一个周期。它们只在该行处于屏幕内时运行。
- **拖动进度**：后端大约每秒报告四次位置；两次报告之间，进度条随时钟继续前进，所以是平滑移动而不是一格一格跳。拖动时滑块的圆点放大 20%。
- 减少动效时，封面、标题、竖线和播放图标都立即切换，竖条静止在 60%，进度条随每次报告移动。关闭动效时所有变化立即生效。

## 相关组件

- File upload，Media 分组的另一个区块。
- `Slider`：通用的范围输入。
- `Carousel` 和 `ImageCompare`：浏览图片。
