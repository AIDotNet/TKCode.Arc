---
title: 媒體播放器
summary: 附封面、播放清單、進度列、上一首 / 下一首與音量和靜音的音訊播放器；切換曲目會循環並維持播放或暫停，正在播放的那一列顯示跳動的直條。
source-hash: 294ec276aedb
---

<Demo name="Preview" />

## 何時使用

`MediaPlayer` 在一張卡片裡播放一小組音訊曲目。上方是目前曲目的封面和標題，標題旁是上一首、播放和下一首按鈕，下面是進度列和已播放 / 總長度，再往下是播放清單，清單標題列裡有靜音按鈕和音量滑桿。點選某一列會播放該曲目；點選目前那一列則暫停或繼續。

- **節目、課程與試聽。** Podcast 的最新幾集、課程某一單元的音訊，或放在商品旁邊的一組聲音預覽。
- **合輯與原聲帶。** 樂團首頁、遊戲原聲帶，或專注類 App 裡的幾段環境音循環。
- **錄音與語音備忘。** 一小組語音備忘或通話錄音，就地回聽。

## 何時不用

- **大型曲庫。** 播放清單把每首曲目都顯示為一列，沒有搜尋、排序和虛擬化；曲庫請用 `DataGrid` 或清單實作，播放器只放待播的曲目。
- **影片。** 區塊只播放音訊；封面是一張靜態圖片。
- **單一聲音。** 只有一段音訊時，一個播放按鈕加上時間就夠了。

## 用法

### 曲目與音訊

每個 `MediaTrack` 有 `Title`、`Artist` 和 `Album`（在標題下顯示為「藝人 · 專輯」）、`Cover` 封面圖及供螢幕閱讀器使用的 `CoverDescription`、作為 `AudioSource` 的音訊，以及 `Duration`：曲目載入前播放清單顯示它，載入後由音訊回報的長度取代。`AudioSource.FromUri`、`FromFile` 和 `FromBytes` 分別對應網路位址、本機檔案和記憶體中的音訊；`WavFile.Encode` 把產生的取樣寫成 WAV 檔，Gallery 的三段循環就是這樣做出來的。

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

### 發出聲音

播放器透過 `ArcAudio.Backend` 播放；設定了自己的 `AudioBackend` 時則用它。預設後端只計時、不發聲，所以播放器在任何平台（包括測試）中的表現都一樣；`IsAudible` 表示目前用的是哪一種。要發出聲音，請加入對應平台的套件，並在應用程式啟動時選用它：

- **桌面**（Windows、macOS、Linux）：`TKCode.Arc.Audio.Native`，以 miniaudio 為基礎，播放 WAV、MP3 和 FLAC。
- **WebAssembly**：`TKCode.Arc.Audio.Browser` 透過瀏覽器的音訊元素播放，瀏覽器能播的它都能播。瀏覽器只有在使用者點過頁面上的東西之後才允許開始播放音訊。

Gallery 的桌面和瀏覽器宿主都選用了各自的後端，所以本頁的範例會發出聲音。

### 播放、切換與曲目結束

`IsPlaying`、`SelectedIndex` 和 `Volume` 都是雙向的。`Play`、`Pause`、`TogglePlay`、`NextTrack`、`PreviousTrack`、`PlayTrack` 和 `Seek` 在程式碼裡完成按鈕做的事。切換曲目會循環（最後一首之後是第一首），並維持播放器原本的播放或暫停狀態；新曲目一律從頭開始。上一首一律切到上一首曲目，不論目前曲目播放到哪裡。曲目播放到結尾時會停下並回到 0:00，不會自動切到下一首，並觸發 `TrackEnded`；需要連續播放時處理這個事件即可。

<Demo name="Events" />

### 音量與靜音

音量從 0 到 1，初始為 0.7。靜音按鈕把音量設為零並記住原本的值；再按一次會還原這個音量，如果是把滑桿拖到零的，則還原為 `DefaultVolume`。音量為零時 `IsMuted` 為 true。

### 不帶卡片

`IsFramed="False"` 拿掉邊框、背景和圓角，適合放在頁面或自有面板上的播放器。

<Demo name="Flat" />

### 窄容器

版面跟隨播放器本身的寬度。寬度小於 `CompactWidth`（420）時，內距從 20 收緊到 14，封面變高（從 2.2:1 變為 1.7:1），標題從 28 縮小到 22，按鈕及其間距變小，音量滑桿從 88 縮短到 64。播放器最寬 520。

<Demo name="Compact" />

### 事件與文字

`SelectedTrackChanged` 帶有舊曲目、新曲目以及切換的來源（`Code`、`Skip` 或 `Playlist`）；`PlaybackFailed` 帶有後端提供的訊息，同時播放器暫停。播放器顯示或讀給螢幕閱讀器的每個字都在 `Labels` 裡，它是一個 `MediaPlayerLabels`，可以整個替換來翻譯；帶數值的文字是函式，`FormatTime` 決定時間的寫法。

## API 參考

<!-- api: MediaPlayer -->

<!-- api: MediaTrack -->

<!-- api: MediaPlayerLabels -->

## 無障礙

- **Tab** 依序經過上一首、播放、下一首、進度列、靜音按鈕、音量滑桿和播放清單的每一列。
- **Enter** 或 **Space** 按下取得焦點的按鈕或列。
- 在進度列上，**Left** 和 **Right**（或 **Down** 和 **Up**）每次移動十分之一秒，**Page Up** 和 **Page Down** 移動曲目的十分之一，**Home** 和 **End** 到開頭和結尾。在音量滑桿上，方向鍵每次 1%，翻頁鍵每次 10%。
- 播放器的名稱是「Audio player」。按鈕的名稱是「Previous track」、「Play」或「Pause」、「Next track」以及「Mute audio」或「Unmute audio」；滑桿的名稱是「Seek」加上標題（值讀作「0:12 of 0:24」）和「Volume」（值讀作「70%」）。
- 標題是二級標題，「Playlist」是三級標題。每一列的名稱是「Play」加上標題，該曲目播放時為「Pause」加上標題；跳動的直條名為「Playing」。
- 封面依 `CoverDescription` 朗讀；沒有封面的曲目顯示一塊空底板，螢幕閱讀器會略過。
- 焦點環只在鍵盤操作時顯示。

## 動態效果

- **切換曲目**：封面在 240 ms 內交叉淡化。舊標題區塊上移 10 px 並淡出，新標題區塊從下方 10 px 以 Smooth 彈簧升到原位；不論往哪個方向切換都向上。目前那一列旁的直線以彈簧移到新的一列（Smooth，約 550 ms）。
- **播放與暫停**：舊圖示縮小到 60% 並模糊淡出，新圖示從 60% 和 2 px 模糊中放大出現，歷時 120 ms。按下的按鈕在 120 ms 內縮小到 94%；懸停底板在 160 ms 內淡入。
- **播放中**：目前那一列的三根直條在 30% 與全高之間起伏，每個方向 900 ms，彼此相差三分之一個週期。它們只在該列位於畫面內時執行。
- **拖曳進度**：後端大約每秒回報四次位置；兩次回報之間，進度列隨時鐘繼續前進，所以是平滑移動而不是一格一格跳。拖曳時滑桿的圓點放大 20%。
- 減少動態效果時，封面、標題、直線和播放圖示都立即切換，直條靜止在 60%，進度列隨每次回報移動。關閉動態效果時所有變化立即生效。

## 相關

- File upload，Media 分組的另一個區塊。
- `Slider`：通用的範圍輸入。
- `Carousel` 和 `ImageCompare`：瀏覽圖片。
