---
title: Line replay
summary: 看一天的資料慢慢展開：時鐘走動時折線自己畫出來，你標記的事件在折線抵達時出現在圖上，還可以暫停、拖到任何時刻或調整速度。
source-hash: 46189eaf2cab
---

<Demo name="Preview" />

## 何時使用

`LineReplay` 適合順序本身就是故事的資料。播放後，一個序列沿著它的一天被畫出來；你標記的每個事件在畫到那裡時出現在線上，讀者便能先後看到誘因與反應：先是發布，接著攀升；先是故障，接著下滑。

- **回顧一次發布或行銷活動。** 看看是哪次發布、哪封電子郵件或哪次報導帶動了數字。
- **事故之後。** 帶團隊回看在警示、修復與恢復之間，指標經歷了什麼。
- **資料故事。** 一天、一次工作階段或一場比賽，事件發生的順序正是重點。

## 何時不用

- **比較。** 當多個序列或期間需要並列閱讀時，`LineChart`（或 `Small multiples`）不必等待重播就能呈現。
- **可編輯的註記。** 時刻來自你的資料；讀者無法新增自己的時刻。
- **兩個相關的指標。** 把其中一個對另一個作圖，而不是重播一條線。

## 用法

本區塊衍生自 Avalonia 的 `TemplatedControl`。`Points` 是位於同一時鐘上的 `LineReplayPoint`，以距開始的分鐘數計（範例中從午夜開始），每個點有一個值，並可帶一個 `Baseline`；點不必等距。`Moments` 是同一時鐘上的 `LineReplayMoment`，帶標題、選填的說明，以及決定圖示的 `Kind`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LineReplay Title="Release day"
                    SeriesLabel="Release day"
                    BaselineLabel="Typical Wednesday"
                    ValueLabel="Active">
        <arc:LineReplayMoment Id="notes" Time="390" Title="Release notes published" Kind="Launch" />
        <arc:LineReplayMoment Id="queue" Time="705" Title="Sign-up queue backed up" Kind="Incident" />
    </arc:LineReplay>
</UserControl>
```

```csharp
replay.Points = samples.Select(s => new LineReplayPoint(s.Minute, s.Active, s.TypicalActive)).ToArray();
```

### 組成

- **頁首**：`Title`、`Description`，以及折線（`SeriesLabel`）與虛線對照（`BaselineLabel`）的圖例。
- **讀數**：時間、數值（`ValueLabel`）、與對照的比值（"Versus typical Wednesday 2.6×"），以及到目前為止的峰值與出現時間。播放時每個數值的寬度維持不變，所以各欄不會移動。
- **圖表**：繪圖區上方放標註的車道、數值格線、完整畫出的對照線、到播放頭為止的面積與折線、每個已經過時刻的標記與豎線、線尖上的數值，以及時間軸上的時鐘。
- **播放控制列**：播放按鈕（Play、Pause，到結尾時為 Replay）、每個時刻對應一個刻度的拖曳列，以及速度（0.5×、1×、2×、4×）。
- **關鍵時刻**：每個時刻一列，帶時間與說明；當它的時間窗格播放完後，也會顯示它的效果，也就是之後一小時的上升，或者對於故障，之後 20 分鐘的下滑。寬度達到 1000 時，這些列在圖表旁排成一欄；較窄時位於圖表下方分兩欄，小於 560 時為一欄。

### 播放

`Duration` 是 1× 速度下完整重播所需的時間（預設 16 秒），`Speed` 設定節奏。`DefaultPosition` 決定第一次播放前播放頭停在哪裡：`End`（預設）顯示整條折線與所有時刻，Replay 會倒回並播放；`Start` 顯示一張空白圖表，由第一次播放畫出。如果每個點都不帶 `Baseline`，對照線、它的圖例項目與比值都會消失。

<Demo name="FromStart" />

### 從程式碼控制

`Play()`、`Pause()`、`Replay()` 與 `TogglePlay()` 的作用與按鈕相同。`Seek(minutes)` 讓播放頭滑到某個時間，`SeekToMoment(moment)` 滑到某個時刻；若正在播放，會從那裡繼續。`Position` 與 `IsPlaying` 回報狀態。`MomentReached` 只在播放經過某個時刻時觸發，拖曳或跳轉時不會觸發，因此可以放心用於分析。`AutoPlay` 會在區塊第一次有 40% 進入檢視區時從頭播放（減少動態效果時不會）。

<Demo name="Events" />

### 格式與文字

`TimeFormatter` 輸出時間（預設 "9:40 am"），`TickFormatter` 輸出座標軸標籤（"6 am"），`ValueFormatter` 輸出數值（"2,316"）。按鈕文字與名稱（`PlayText`、`PauseText`、`ReplayText`、`PlayLabel`、`PauseLabel`、`ReplayLabel`）、讀數標籤（`TimeText`、`VersusFormat`、`PeakText`、`PeakFormat`）、效果（`RiseFormat`、`DipFormat`）、`MomentsTitle`、螢幕閱讀器名稱與各種播報，都是帶英文預設值的屬性。

## API 參考

<!-- api: LineReplay -->

<!-- api: LineReplayMoment -->

<!-- api: LineReplayPoint -->

<!-- api: LineReplayMomentKind -->

<!-- api: LineReplayPosition -->

## 無障礙

- **Tab** 依序經過播放按鈕、拖曳列、速度控制項與每個關鍵時刻。
- 在拖曳列上，**Space**、**Enter** 或 **K** 播放或暫停（在結尾時從頭重播）；**Left** 與 **Right** 前後移動一個點，按住 **Shift** 時移動一小時；**Page up** 與 **Page down** 移動一小時；**Home** 與 **End** 跳到開頭或結尾。在速度控制項中，方向鍵選擇速度。
- 在關鍵時刻上按 **Enter** 或 **Space**，播放頭會移到該時刻。
- 拖曳列以分鐘為單位回報自己是一個滑桿。它朗讀的數值（"9:40 am, 2,316 active"）只在播放停止時改變，讓螢幕閱讀器在重播期間保持安靜。
- 折線顯示的一切也以文字出現在讀數中，因此圖形不在無障礙樹中。
- 每經過一個時刻、每次暫停或跳轉，以及重播結束時，都會有一則禮貌播報（"Replay finished. Peak 4,894 at 10:40 am."）。
- 播放按鈕的名稱說明按下它會做什麼：Play replay、Pause replay、Replay from the start。最近經過的時刻標示為目前項目；在那一段播放完之前，它的效果不會被讀出。
- 故障除了顏色之外，還靠警告圖示與自己的標題來區分。

## 動態效果

- **播放** 在所有動態效果等級下都會執行：它是觀看者要求的，也可以隨時停下。每一格一個位置向前推進，折線、播放頭、標籤、拖曳列與讀數都依據它繪製。
- **倒回與跳轉** 讓播放頭沿平滑彈簧移動（無回彈）；已畫出的折線隨之縮短或伸長，第一次跳轉途中再跳一次會從目前位置繼續。
- **抵達時刻**：它的標記在淡入的同時從 40% 放大到完整大小，下方出現一條虛線豎線，卡片上移 8px 落到繪圖區上方；被取代的卡片在 120 毫秒內淡出。
- **小時標籤** 在移動的時鐘附近淡出（160 毫秒），而不是被壓在它下面。時刻列在 160 毫秒內變色，在 240 毫秒內顯示效果。
- 減少動態效果時重播照常播放，但倒回、跳轉、標記與卡片都直接到位，`AutoPlay` 也不起作用。

## 相關

- `LineChart`：在同一張圖上繪製多個序列。
- `Small multiples`：Line charts 中的另一個區塊，用於並列比較多個序列。
- `SegmentedControl` 與 `Button`：播放控制列由它們組成。
