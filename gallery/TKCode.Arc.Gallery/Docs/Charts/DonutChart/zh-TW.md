---
title: 環圈圖
summary: 一個環圈圖，扇形在資料集之間平滑變形，目前數值滾動顯示在中心。
source-hash: 958aaf8ea471
---

<Demo name="ToggleSegments" />

## 何時使用

- 由兩到六個部分組成一個整體，例如流量來源或支出類別。
- 中心顯示總計，並隨讀者指向某個部分而變化。
- 切換資料集（例如本月與本季）時，扇形應變形為新的大小。

## 何時不用

- 部分較多或需要精確比較：用 `BarChart`。
- 單一數值與已知上限比較：用 `Gauge`；配額情境用 `UsageMeter`。
- 呈現隨時間的變化：用 `LineChart`。

## 用法

每個 `ChartPoint` 對應一個扇形。為每個資料點設定穩定的 `Key`，這樣資料變化時扇形才能保有各自的身分。值為零或負數的資料會被忽略。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:DonutChart Label="依來源統計造訪量"
                    Unit="次造訪"
                    TotalLabel="總計"
                    OtherLabel="其他"
                    ItemsSource="{Binding Sources}" />
</UserControl>
```

### 資料集

替換 `ItemsSource` 即可切換資料集。扇形保持原有順序，並從目前的角度平滑移動到新的角度，因此扇形不會被重新建立。

```csharp
month.Click += (_, _) => chart.ItemsSource = s_month;
quarter.Click += (_, _) => chart.ItemsSource = s_quarter;
```

### 尺寸與分組

`Diameter` 與 `RingThickness` 決定環圈的尺寸。占比低於 `GroupBelow`（占總量的比例，預設 0.04）的部分，以及超出 `MaxSegments`（預設 6，包含「其他」）的部分，會併入「其他」扇形。

```xml
<arc:DonutChart Label="儲存空間"
                Diameter="160"
                RingThickness="18"
                GroupBelow="0.05"
                MaxSegments="5"
                ItemsSource="{Binding Storage}" />
```

### 圖例與隱藏扇形

預設情況下，圖例可以隱藏與顯示扇形。若只需要環圈本身，請設定 `ShowLegend="False"`。`HiddenKeys` 列出被隱藏的鍵，且為雙向繫結，檢視模型可以保存此狀態。

<Demo name="RingOnly" />

```xml
<arc:DonutChart Label="依來源統計造訪量"
                HiddenKeys="{Binding HiddenKeys}"
                ShowLegend="True"
                ItemsSource="{Binding Sources}" />
```

### 固定扇形

設定 `LegendAction="Select"` 後，點選會固定扇形而不是隱藏它。`ActiveKey` 保存被固定的鍵，同樣為雙向繫結。

<Demo name="Select" />

```xml
<arc:DonutChart Label="依類別統計支出"
                LegendAction="Select"
                ValueFormat="$#,##0"
                ActiveKey="{Binding PinnedCategory}"
                ItemsSource="{Binding Spend}" />
```

## API 參考

<!-- api: DonutChart -->

## 無障礙

- `Label` 是圖表的名稱。每一列圖例都是一個切換按鈕，其名稱包含數值、占比；對於「其他」，還包含其所含的各個部分。
- 圖例是鍵盤操作的路徑。Down 與 Right 移到下一列，Up 與 Left 移到上一列，Home 與 End 分別移到第一列與最後一列。Enter 或 Space 顯示或隱藏扇形；在 Select 模式下則為固定或取消固定。
- Escape 清除已固定的扇形。
- 可見扇形發生變化時，會以新的總計進行禮貌播報。
- 沒有圖例時，環圈本身可以取得焦點：方向鍵朗讀每個扇形，Enter 固定該扇形。
- 環圈與中心讀數對輔助技術隱藏。隱藏的資料表會列出每個部分。
- 焦點框僅在鍵盤焦點時顯示。

## 動態效果

- 首次顯示時，所有扇形同時從頂部掃出，每段的終點稍後跟上。
- 新資料集或隱藏某個扇形時，起始角與終止角會從扇形目前的位置開始變形，因此環圈保有其運動軌跡與順序。
- 作用中的扇形沿中線向外移動並略為加粗，其餘扇形變暗。指標在扇形之間的間隙仍保持懸停狀態。
- 中心讀數滾動到作用中的扇形；總計與各占比數字滾動到新的數值。
- 減少動態效果時，扇形、外移效果與文字會立即顯示為最終狀態。關閉動態效果時的表現與減少動態效果相同。

## 相關元件

- 部分較多或需要精確比較時用 `BarChart`。
- `WaffleChart` 用於以格子數量（每格代表一個百分點）表示占比。
- 部分有巢狀結構且需要逐層下鑽時用 `Treemap`。
- `Gauge` 用於單一數值與已知範圍的比較。
