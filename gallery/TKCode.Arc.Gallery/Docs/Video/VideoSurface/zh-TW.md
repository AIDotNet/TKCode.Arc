---
title: 影片畫面
summary: "只顯示影片的畫面、上面不疊加任何內容，可用作背景或自訂控制的底層；它和其他控制項一樣可以裁剪、淡入淡出和變換。"
source-hash: 5ceb3d490648
---

<Demo name="BackgroundLoop" />

## 何時使用

- 放在首屏大圖、卡片或登入頁背後的靜音循環影片。
- 用 `IVideoPlayer` 驅動、由你自己設計控制列的播放器。
- 同一個播放器的多個畫面，例如縮圖和大圖檢視。

## 何時不用

- 需要使用者操作的影片：用 `VideoPlayer`，它內建控制列、字幕、鍵盤和螢幕閱讀器支援。
- 靜態圖片：用 `Image`。

## 用法

從後端建立播放器，把它交給畫面控制項，用完後釋放。畫面控制項不擁有播放器。

```csharp
var player = ArcVideo.CreatePlayer();
surface.Player = player;
await player.LoadAsync(VideoSource.FromUri(new Uri("avares://MyApp/Assets/loop.mp4")));
player.IsMuted = true;
player.IsLooping = true;
player.Play();
```

`Stretch` 決定畫面如何填滿控制項：`Uniform` 加上黑邊（`LetterboxBrush`，預設為黑色），`UniformToFill` 裁剪，`Fill` 拉伸，`None` 保持影片原尺寸。`CornerRadius` 同時圓角化畫面和黑邊。其他控制項可以疊加在畫面控制項之上，它也可以像其他控制項一樣被裁剪、淡化和變換。

畫面控制項只在播放器播放且控制項可見時請求新影格，並且只請求不大於其實際繪製尺寸的影格。`RenderPath` 決定影格如何抵達：`GpuInterop`（不做複製，直接匯入合成器，適用於 macOS 和 Windows）或 `Bitmap`（複製）。`PreferBitmapPath` 強制使用複製路徑，例如用來排除顯示卡驅動程式問題。

### 自訂控制

<Demo name="CustomChrome" />

## API 參考

<!-- api: VideoSurface -->

<!-- api: IVideoPlayer -->

## 無障礙

- 畫面控制項只是一張沒有名稱、也沒有控制項的圖片。當它承載含義時，用 `AutomationProperties.Name` 為它命名，並提供暫停它的方式。
- 自行運動超過五秒的影片需要提供暫停方式（WCAG 2.2.2）。只有在未減少動態效果時才開始裝飾性循環，如範例所示。
- 自訂控制項需要名稱、鍵盤存取，以及語音字幕：`VideoPlayer` 已內建這些功能。

## 動效

- 畫面控制項繪製播放器交出的影格，本身不加入任何動效。
- 減少動態效果不會停止正在播放的影片；是否開始裝飾性循環由應用程式決定（`ArcMotion.GetEffectiveLevel`）。

## 響應式

- 畫面控制項採用版面給予它的尺寸；它沒有偏好尺寸，因此需要為它設定高度或寬高比。
- 它依實際繪製的尺寸向播放器請求影格，因此小控制項中的大影片開銷很小。
- 使用 `Stretch="UniformToFill"` 時，畫面會持續填滿形狀變化的控制項，裁剪左右兩側或上下兩側。

## 相關元件

- `VideoPlayer`：帶控制列、字幕和鍵盤支援的影片播放器。
- `Image`：靜態圖片。
