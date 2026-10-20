---
title: 儀表板
summary: 在已知範圍內顯示一個數值。
source-hash: 5a9fa20aa21b
---

## 何時使用

- 對照已知範圍的單一數值，例如配額中的磁碟用量或健康評分。
- 帶有狀態的數值，由門檻區間命名狀態，例如健康、即將佔滿和嚴重。
- 適合放進儀表板卡片、需要一個百分比與一行簡短說明的讀數。

## 何時不用

- 正在執行或即將完成的工作進度：用 `Progress`。
- 使用者正在消耗、有上限的方案或配額：用 `UsageMeter`。
- 隨時間變化的數值：用 `LineChart` 或 `Sparkline`。

## 用法

`Value` 為讀數。`Minimum` 與 `Maximum` 設定範圍，預設為 0 到 100。`Detail` 會在讀數下方加上一行說明，例如以絕對單位表示的數量。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Gauge Label="磁碟用量"
               Value="72"
               Detail="360 / 500 GB">
        <arc:Gauge.Thresholds>
            <arc:GaugeThreshold From="0" Tone="Success" Label="健康" />
            <arc:GaugeThreshold From="60" Tone="Warning" Label="即將佔滿" />
            <arc:GaugeThreshold From="85" Tone="Danger" Label="嚴重" />
        </arc:Gauge.Thresholds>
    </arc:Gauge>
</UserControl>
```

### 門檻

門檻從 `From` 開始，一直持續到下一個門檻。螢幕上的數值所達到的最高區間決定顏色，並顯示該區間的 `Label`。區間由螢幕上的數值選定，因此數值跨越區間時，標籤隨之變化。

```csharp
disk.Thresholds.Add(new GaugeThreshold(0, GaugeTone.Success, "健康"));
disk.Thresholds.Add(new GaugeThreshold(60, GaugeTone.Warning, "即將佔滿"));
disk.Thresholds.Add(new GaugeThreshold(85, GaugeTone.Danger, "嚴重"));
```

### 更新讀數

同時設定 `Value` 與 `Detail`，讓數字與其說明保持一致。

```csharp
disk.Value = Math.Round(used / 5.0);
disk.Detail = $"已用 {used} / 500 GB";
```

### 自訂範圍

`Minimum` 與 `Maximum` 可將任意範圍對應到圓環上。數字依該範圍的百分比顯示。

```xml
<arc:Gauge Label="健康評分" Value="640" Minimum="300" Maximum="850" Detail="640 / 850" />
```

### 沒有門檻時的色調

沒有門檻時，圓環使用 `Tone`：`Accent`、`Success`、`Warning` 或 `Danger`。

```xml
<arc:Gauge Label="可用率" Value="91" Tone="Success" />
```

## API 參考

<!-- api: Gauge -->

<!-- api: GaugeThreshold -->

## 無障礙

- `Label` 為儀表板命名。數值以唯讀範圍的形式公開，並包含最小值與最大值。
- 數值文字包含目前區間的標籤，因此狀態會與數字一同被讀出。
- 狀態由門檻標籤以文字說明，顏色從來不是唯一的狀態提示。
- 儀表板為唯讀，不定義鍵盤互動。
- 圓環與帶動畫的讀數對輔助技術隱藏。

## 動態效果

- 儀表板進入畫面一半時，圓環以無過衝的平滑彈簧一次填滿到其數值。
- 新數值會將圓環移動到新的長度，同時百分比數字計數到新的數字。
- 數值跨越門檻時，顏色與區間標籤在該時刻切換，跟隨計數變化。
- 減少動態效果時，圓環與數字立即到達目標值；關閉動態效果的表現與減少動態效果相同。

## 相關元件

- `Progress` 表示工作的完成進度。
- `UsageMeter` 表示使用者在一段期間內消耗的配額。
- `DonutChart` 表示整體的各個部分，而非對照範圍的單一數值。
- `MetricCard` 表示帶有變化與趨勢的單一數值。
