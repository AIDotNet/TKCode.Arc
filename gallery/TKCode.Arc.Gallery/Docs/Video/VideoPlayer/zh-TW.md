---
title: 影片播放器
summary: "在畫面上疊加控制列播放影片：可用章節跳轉、調整音量、播放速度、字幕和全螢幕，外觀依 Arc 風格繪製，由平台自己的播放器解碼。"
source-hash: a73f2874046b
---

<Demo name="Preview" />

## 何時使用

- 產品導覽、教學、錄製的工作階段和產生的短片，需要使用者在應用程式內觀看的情境。
- 希望控制列的外觀和行為與介面其他部分一致，並且需要使用者能以自己的語言閱讀字幕的影片。
- 同一頁有幾段短片：每個播放器一開始為暫停，只在顯示時才載入媒體。

## 何時不用

- 只有聲音：用媒體播放器區塊或 `IAudioPlayer`。
- 只有畫面、沒有控制列，例如循環背景或自訂控制：用 `VideoSurface`。
- 低延遲直播、DRM 保護的媒體或逐格編輯：本版本不支援。

## 用法

加入 `TKCode.Arc.Video` 並註冊，然後在啟動時選擇播放後端：

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcVideo());

ArcVideoNative.Use();              // desktop: TKCode.Arc.Video.Native
await ArcVideoBrowser.UseAsync();  // browser: TKCode.Arc.Video.Browser
```

桌面後端使用系統自帶的播放器：Windows 上是 Media Foundation，macOS 上是 AVFoundation，Linux 上是 GStreamer。應用程式本身不附帶任何編解碼器。MP4 搭配 H.264 影像和 AAC 聲音在所有平台都能播放；其他格式取決於平台（WebM 無法在 macOS 上播放，HEVC 在 Windows 上需要擴充功能，Linux 需要 `gst-plugins-good` 和 `gst-libav` 套件）。執行時可以用 `ArcVideo.Backend.CanPlay("video/webm")` 查詢。沒有後端時，播放器會顯示產生的測試畫面，並靜默計時。

把 `Source` 設為位址（`avares://`、`file://`、`http(s)://`）或 `VideoSource`。在影片本身的尺寸還不知道之前，`AspectRatio` 讓版面保持穩定；首次播放前一直顯示海報。

```xml
<arc:VideoPlayer Source="avares://MyApp/Assets/intro.mp4"
                 Title="Introduction"
                 AspectRatio="1.7778" />
```

`IsPlaying`、`Position`、`Volume`、`IsMuted`、`IsLooping` 和 `PlaybackRate` 都是雙向繫結。`Play()`、`Pause()`、`Stop()`、`Seek()` 以及 `PlayPauseCommand`、`StopCommand`、`SkipForwardCommand`、`SkipBackwardCommand`、`ToggleMuteCommand`、`ToggleFullScreenCommand` 命令可以從外部控制播放器。`Stop()` 會暫停、回到開頭並重新顯示海報；影片保持已載入，所以下次播放會立即開始。`Failed` 會回報影片無法播放的原因。

### 動畫影片

改變 `Source` 會在同一個播放器裡載入另一部影片。這裡是 Blender 基金會兩部開放電影的預告片，© Blender Foundation，採用 [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) 授權。

<Demo name="Films" />

### 字幕與章節

把 WebVTT 或 SRT 檔案加入為 `TextTracks`。字幕選單會列出所有字幕軌，設定文字大小，並可以關閉字幕。`Chapters` 軌會把進度列分成若干段，並在指標下方顯示章節名稱。

影片檔本身帶的字幕（MP4 定時文字、Matroska 文字軌）在影片載入後會加入 `TextTracks`，排在你自己的軌之後，`IsEmbedded` 為 true，換來源時移除。macOS 和 iOS 讀取 MP4 中的字幕，Android 讀取 MP4 中的字幕（純文字，逐條顯示），Linux 讀取 MP4 和 Matroska 中的字幕（Linux 的 MP4 解多工器會丟掉粗體和斜體）；Windows 和瀏覽器暫時不列出內嵌字幕。

<Demo name="Captions" />

### 控制列

影片播放時，指標停止移動兩秒後控制列會隱藏；暫停時、選單開啟時、指標在控制列上方時，以及控制列有鍵盤焦點時，它都會保持顯示。`ShowControls="Always"` 讓控制列一直顯示；`ControlsHideDelay` 可以修改延遲。`AutoPlay` 只在靜音時才會開始播放：瀏覽器和無障礙指南都不允許自動發出聲音。

<Demo name="Controls" />

### 播放後端

播放器使用 `ArcVideo.Backend` 中的後端解碼，並把畫面繪製在視窗內，因此控制列、字幕和圓角都會繪製在畫面之上。平台允許時（macOS 和 Windows），畫面會在 GPU 上從解碼器直接送到螢幕，不做複製；其他情況下會複製到點陣圖中。

在 iOS 和 Android 上同樣呼叫 `ArcVideoNative.Use()`：iOS 透過 AVFoundation 播放，Android 透過系統的 MediaPlayer 播放，畫面都複製到點陣圖中。在 `net11.0-ios` 或 `net11.0-android` 應用程式中參考 `TKCode.Arc.Video.Native` 即可（iOS 15、Android API 24 或更高）。

在瀏覽器中，`BrowserPresentation="Native"` 改為顯示瀏覽器自己的影片元素：不複製畫面，瀏覽器能播放的格式都能播放（例如 Safari 中的 HLS）。這個元素上面不能繪製任何內容，所以控制列放在畫面下方並一直顯示，字幕由瀏覽器繪製，也沒有圓角；顯示海報、錯誤、重播按鈕或選單時，這個元素會暫時隱藏。沒有這種元素時（桌面平台，或 net8.0 版本），播放器仍使用畫布。

<Demo name="Playback" />

## API 參考

<!-- api: VideoPlayer -->

<!-- api: VideoTextTrack -->

<!-- api: VideoSource -->

## 無障礙

- 播放器讀作一個群組，名稱取自 `Title`（沒有則為「影片」）。它的按鈕以功能命名，並隨狀態變化：「播放」與「暫停」、「靜音」與「取消靜音」、「全螢幕」與「結束全螢幕」；每個提示都會顯示對應的快速鍵。
- 進度列是一個滑桿，取得焦點或拖曳結束時讀作「1 分 5 秒，共 3 分 15 秒」；影片播放時不會每秒播報一次。音量滑桿以百分比讀出。
- 錯誤、全螢幕、字幕開關和播放速度變化會禮貌地播報。啟用 `CaptionAnnouncements` 後，字幕內容也會播報。
- 隱藏的控制列仍在 Tab 順序中：按 Tab 會再次顯示控制列，並且在它取得焦點時保持顯示。
- 字幕遵循 W3C 對大小和背景的要求：字幕選單和 `CaptionScale` 可以放大文字，`CaptionBackgroundOpacity` 可以加深文字後面的底框。

### 鍵盤

| 按鍵 | 作用 |
| --- | --- |
| Space, K | 播放或暫停（焦點在按鈕上時，Space 按下該按鈕）。 |
| Left / Right | 倒退或前進 5 秒（`SeekStep`）。 |
| J / L | 倒退或前進 10 秒。 |
| Up / Down | 音量增加或減少 5%。 |
| M | 靜音或取消靜音。 |
| F | 開啟或關閉全螢幕。Escape 結束全螢幕。 |
| C | 開啟或關閉字幕。 |
| Shift + . / Shift + , | 加快或減慢播放速度。 |
| 0 to 9 | 跳轉到 0% 至 90% 處。 |
| Home / End | 跳到開頭或結尾。 |
| Page Up / Page Down | 在進度列上：跳到上一個或下一個章節（沒有章節時為影片的十分之一）。 |

## 動效

- 控制列出現時淡入並上升 8 px，用時 160ms；隱藏時反向進行，指標也隨之隱藏。
- 播放和暫停圖示交叉淡化，並以 snappy 彈簧縮放。單擊畫面時，新狀態會以中央的大圖示顯示，圖示放大並淡出。
- 指標停留時進度軌道從 4 px 加粗到 6 px；按下大播放按鈕時，按鈕下沉到 94%。
- 兩次進度回報之間，進度滑桿的拇指會依播放速度平順移動。
- 減少動態效果：控制列只淡入淡出，圖示立即切換，中央回饋和雙擊漣漪效果省略。影片本身照常播放；靜音、循環且自動開始的影片停在第一格。

## 響應式

- 播放器採用所給的寬度，高度由 `AspectRatio` 決定（影片尺寸未知時為 16:9）。
- 低於 420 px（`CompactWidth`）時，控制列收緊內距和間距，時間文字變小，音量滑桿不再從靜音按鈕旁邊滑出。
- 播放器量測的是自身寬度，而不是視窗寬度，所以窄欄或手機預覽中的播放器也使用緊湊版面。
- 觸控螢幕上，點一下顯示控制列，再點一下播放或暫停；雙擊左側或右側三分之一處，倒退或前進 10 秒。
- 全螢幕可以佔滿視窗（`FullScreenMode="Window"`），也可以讓視窗本身全螢幕（`System`），結束後回到頁面中的原位置。

## 相關元件

- `VideoSurface`：只有畫面，用於背景和自訂控制。
- 媒體播放器區塊：帶播放清單的音訊播放器。
- `Carousel`：瀏覽一組圖片。
