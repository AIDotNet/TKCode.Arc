---
title: 倒數計時
summary: 發布倒數計時，數字逐位滾動，歸零時轉為即時狀態。
source-hash: 7805982abd2e
---

## 何時使用

- 發布或活動頁面，主要訊息是距離某個固定時刻還剩多久。
- 橫幅、表格列或促銷時段中的精簡截止時間，開始時切換為「正在進行」。

## 何時不用

- 含倒數計時與操作按鈕的橫幅：用 `AnnouncementBar`。
- 已完成工作的占比：用 `Progress`。
- 使用者需要在即時區域中逐秒讀取的計時器：`Countdown` 執行時不會被播報。

## 用法

將 `Target` 設為要倒數到的時刻，或將 `Duration` 設為從控制項顯示起倒數的固定時長。預設為大型變體，顯示天、時、分、秒。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Countdown Label="距離發布" />
</UserControl>
```

```csharp
launch.Target = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
```

### 精簡截止時間

將 `Variant` 設為 `Compact`，會顯示一個依字型大小調整的膠囊。適用於空間緊湊的位置。

```xml
<arc:Countdown Variant="Compact"
               Units="Hours, Minutes"
               Label="優惠截止於" />
```

### 單位

`Units` 列出要顯示的單位，由大到小排列。列出的最大單位會吸收未顯示的單位，因此預設為天到秒。

```xml
<arc:Countdown Units="Minutes, Seconds"
               Duration="00:00:12" />
```

### 時區中的當地時間

當文字不含時差偏移時，請用 `ResolveTarget` 解析目標。帶有 `Z` 或時差偏移的目標文字是精確的。當地時間會依你傳入的時區讀取，因此每位訪客都倒數到同一時刻。

```csharp
countdown.Target = Countdown.ResolveTarget("2030-01-01T09:00:00", timeZone);
```

### 暫停、繼續與重新開始

時長倒數計時在暫停期間會保留剩餘時間。目標倒數計時仍會倒數到同一時刻，因此暫停只會凍結顯示。

```xml
<arc:Countdown Duration="00:00:12"
               IsPaused="{Binding IsTimerPaused, Mode=TwoWay}" />
```

```csharp
timer.Pause();
timer.Resume();
timer.Restart(); // 從完整時長重新開始
```

### 完成

`CompleteContent` 在歸零時取代數字，顯示在一個脈動的即時圓點旁。預設為「正在進行」。處理 `Completed` 可在倒數計時到達零的那一刻做出一次回應。

```csharp
timer.CompleteContent = "報名已開始";
timer.Completed += (_, _) => RefreshOffers();
```

## API 參考

<!-- api: Countdown -->

## 無障礙

- `Label` 為倒數計時命名。預設為「剩餘時間」。
- 控制項沒有即時區域。剩餘時間超過一分鐘時，朗讀的文字會省略秒數；倒數結束後朗讀 `CompleteContent`。
- 可見數字變化時不會被播報，因此螢幕閱讀器不會每秒重複。
- 控制項無法取得焦點，也沒有鍵盤互動。

## 動態效果

- 每個數字都是一個轉輪，沿較短的方向旋轉，視窗的上下邊緣柔和過渡。
- 第一次顯示時，數字依序滾入。之後每秒變化一次。
- 單位隨容器以彈簧效果伸縮到新尺寸而出現或消失。
- 歸零時，數字淡出並縮放進入完成標籤，即時圓點脈動。
- 減少動態效果時，轉輪直接跳到新值，容器立即調整尺寸，內容以淡入淡出切換，並停止脈動。

## 相關元件

- `AnnouncementBar`：帶倒數計時與操作按鈕的網站橫幅。
- `Progress`：工作有已知進度而非截止時間時使用。
- `Stepper`：倒數計時只是多步驟流程的一部分時使用。
