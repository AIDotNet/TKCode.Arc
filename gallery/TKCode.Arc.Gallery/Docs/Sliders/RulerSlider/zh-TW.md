---
title: 刻度尺滑桿
summary: 一條在固定指針下滑動的刻度帶，具有慣性、吸附和捲動讀數。
source-hash: a5bafeaf46e9
---

<Demo name="Height" />

## 何時使用

`RulerSlider` 在一條從固定指針下移動的刻度上設定一個數字。刻度可以比控制項長得多，因此人們能在很寬的範圍內精細控制，大型讀數則在刻度帶移動時顯示目前值。

- **引導流程或健康應用程式中的身體資料**：身高、體重或目標，範圍很寬，而在手機上表單欄位顯得笨重。
- **預訂或結帳中的數量或金額**：以整數步進的每月預算或借款金額。
- **帶真實單位的設定**：焦距、距離或溫度，刻度本身就顯示了量程。

## 何時不用

- **軌道上的位置才是重點的設定**，例如音量：使用 `Slider` 或 `ElasticSlider`，整個範圍都在視野內。
- **有兩端的範圍**：使用 `RangeHistogramSlider`，或帶 `IsRange` 的 `Slider`。
- **使用者已經知道並想直接輸入的數字**：使用 `NumberField`。
- **緊湊的列和密集的表單**：此控制項需要整列寬度，連同標籤和讀數約 150 px 高。這些場合請使用 `Slider` 或 `ScrubInput`。

## 用法

設定 `Label`、上下限和 `Value`。`Unit` 跟在讀數後面，並與值一起朗讀。`RulerSlider` 衍生自 Avalonia 的 `RangeBase`，因此 `Value`、`Minimum`、`Maximum`、`SmallChange`、`LargeChange` 和 `ValueChanged` 的行為與任何範圍控制項相同。`ValueChanged` 在每個刻度經過指針時觸發；`ValueCommitted` 在拖曳或輕掃後刻度帶靜止時觸發一次，點按或按鍵時立即觸發。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:RulerSlider Label="Height"
                     Unit="cm"
                     Minimum="120"
                     Maximum="220"
                     Value="{Binding Height}" />
</UserControl>
```

### 步長、刻度與間距

`SmallChange` 是一個小刻度代表的值，刻度帶總是停在刻度上。`MajorEvery` 設定帶標籤的刻度之間有多少個小刻度，偶數時中間會多一個較高的刻度。`TickSpacing` 設定小刻度之間的像素數：間距越寬，每個刻度的行程越長，控制也就越精細。

<Demo name="FineSteps" />

### 格式化

`ValueFormatter` 格式化讀數、刻度標籤和朗讀的值。文字中的數字仍會捲動，貨幣符號等周圍的文字保持不動。沒有格式化函式時，刻度標籤只顯示其自身間距所需的小數位，因此每 5 一個標籤、步長半公斤的刻度顯示為 30、35、40。`LargeChange` 設定 `PageUp`、`PageDown` 和 `Shift` 加方向鍵的距離，預設為一個帶標籤的刻度。

<Demo name="Formatted" />

### 停用

`IsEnabled="False"` 以一半的不透明度顯示值，並把控制項移出 Tab 順序。

<Demo name="Disabled" />

## API 參考

<!-- api: RulerSlider -->

## 無障礙

- 控制項是一個定位點，並把自己回報為滑桿，以 `Label` 作為名稱，上下限作為範圍。
- 朗讀的值是格式化後的值加上 `Unit`，例如「172 cm」。
- 刻度帶、標籤和指針都繪製在控制項內，不是獨立的元素。
- 鍵盤聚焦時，焦點環出現在刻度帶周圍。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Right` / `Up` | 把值提高一個刻度 |
| `Left` / `Down` | 把值降低一個刻度 |
| `Shift` + 方向鍵、`PageUp` / `PageDown` | 移動 `LargeChange` |
| `Home` / `End` | 轉到 `Minimum` 或 `Maximum` |

## 動效

- 刻度帶 1:1 跟隨拖曳。超過任一端時最多多走 44 px，放開後以彈簧（0.5 s，回彈 0.1）帶著拋出的速度返回。
- 輕掃會滑行：行程為其速度走 0.42 s 的距離，吸附到一個刻度，並以 260 ms 的衰減緩入。每個刻度經過指針時，值和讀數隨之變化。按住刻度帶會讓它停在目前位置。
- 點按、按鍵或新的值讓刻度帶以 snappy 彈簧滑動，並保留已有的速度。
- 讀數的每一位數字各自在 240 ms 內滑動，出現或離開的數位以 morph 彈簧展開或收起。
- 減弱動效時，刻度帶在兩端停住並直接跳到目標值，數字立即變化。關閉動效時一切立即完成。

## 響應式

控制項填滿父層給出的寬度，沒有中斷點：視野顯示更多或更少的刻度，指針保持在正中。高度固定，刻度帶為 72 px。刻度向兩側邊緣淡出，因此狹窄的視野仍像一條延續的刻度帶。觸控時，水平拖曳移動刻度帶，垂直拖曳留給頁面，因此控制項可以放在捲動頁面中。它不依賴懸停。

## 相關元件

| 元件 | 適用情境 |
|---|---|
| `Slider` | 在整個範圍上的位置很重要，或列很緊湊 |
| `RangeHistogramSlider` | 值是資料上的一個範圍 |
| `NumberField` | 輸入或步進精確數字 |
| `ScrubInput` | 透過在標籤上拖曳來改變數字 |
