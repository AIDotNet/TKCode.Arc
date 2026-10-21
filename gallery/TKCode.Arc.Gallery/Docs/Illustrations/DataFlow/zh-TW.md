---
title: 資料流
summary: 事件從來源出發，經佇列與批次轉換後進入資料倉儲和即時儀表板。
source-hash: bfddb2b7fcc9
---

<Demo name="EventPipeline" />

## 何時使用

- 在著陸頁或產品文件中說明事件如何從生產者流向儲存空間與儀表板。
- 引導頁和空狀態需要展示一條管線、但沒有真實資料時。
- 展示過濾步驟如何在儲存前移除一部分流量。

## 何時不用

- 繪製真實數值隨時間的變化：用 `LineChart` 或圖表分類中的其他圖表。
- 固定步驟序列且需要標示目前位置：用 `Stepper`。
- 頁面上有大量動畫插圖：每個插圖都會逐影格重繪畫布，因此同一時間只保留少數幾個在播放。

## 用法

插圖載入後自動開始播放。圖表下方的說明文字會隨故事推進而變化，播放按鈕可暫停播放。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DataFlow Label="事件管線" />
</UserControl>
```

### 吞吐量與過濾

`EventsPerSecond` 設定資料來源發出事件的速度（1 到 60，預設 18）。`FilterRatio` 設定轉換環節移除的事件占比（0 到 0.5，預設 0.14）。被過濾的事件會在轉換節點處淡出。

```xml
<arc:DataFlow EventsPerSecond="8" FilterRatio="0.3" />
```

### 來源與標籤

`Sources` 接受一到四個 `DataFlowSource` 值（一個標題和一行可選的詳細資訊）。`Labels` 接受一個 `DataFlowLabels` 物件，用於設定圖表中所有可見的文字。

<Demo name="OwnSources" />

### 播放與速度

`IsPlaying` 用於播放和暫停故事（雙向繫結），`Speed` 縮放場景時鐘（0.25 到 4）。`Play()`、`Pause()` 和 `Restart()` 可在程式碼中執行相同的操作。

```xml
<arc:DataFlow Speed="2" IsPlaying="{Binding IsPipelineRunning}" />
```

```csharp
flow.Speed = 0.5;
flow.Restart();
```

### 跟隨故事進度

`Step` 和 `StepCount` 描述循環中的位置（共三步）。`StepChanged` 提供新的步驟及其說明文字；`RowsStored` 和 `EventsFiltered` 分別統計已寫入倉儲的列數和被移除的事件數。

```csharp
flow.StepChanged += (_, e) => status.Text = $"第 {e.Step + 1} 步，共 {flow.StepCount} 步：{e.Caption}";
```

### 減少動態效果

在減少動態效果（或關閉動態效果）下，插圖是其穩定狀態的靜態畫面，說明文字概括整個故事。

<Demo name="ReducedMotion" />

## API 參考

<!-- api: DataFlow -->

<!-- api: DataFlowLabels -->

## 無障礙

- `Label` 是無障礙名稱，目前的說明文字會作為說明文字提供給輔助技術。
- 插圖可取得焦點，焦點環寬 2px。
- 空白鍵或 Enter 播放與暫停故事，R 鍵從頭開始播放。
- 說明文字的變化不會被播報，因為循環播放的故事會無止境地重複播報。

## 動態效果

- 事件以小型資料封包的形式沿連線傳送，緩衝區在事件流中逐漸填滿，讀數同步遞增。
- 每一步都會點亮目前作用中的連線，其餘連線淡回原色。
- 減少動態效果和關閉動態效果時，顯示穩定狀態的靜態畫面，所有連線均高亮，說明文字概括整個故事。兩種模式下播放按鈕都會隱藏。

## 相關元件

- `PaymentFlow`：單筆請求被核准或被拒絕的情境。
- `MultiRegionFailover`：流量在區域之間切換的情境。
- `VectorSearch`：根據最相近的文件回答問題的情境。
