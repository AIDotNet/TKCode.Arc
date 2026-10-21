---
title: 山脊圖
summary: 重疊的分布，每組一條山脊：懸停可抬起山脊並讀取其四分位數，切換資料集時每條曲線都會變形。
source-hash: bc4375bf40fa
---

<Demo name="DistributionsByMonth" />

## 何時使用

- 並排比較幾組分布的形狀，例如每月的每日最高氣溫或各地區的延遲。
- 單一彙總數值會掩蓋的離散程度、偏斜與第二個峰。
- 最多約二十組，每組都有足夠的觀測值（至少 20 個）以繪製平滑曲線。

## 何時不用

- 每組只有一個彙總數值：用 `BarChart`。
- 每組只有少量資料點或需要精確數值：用 `BarChart` 或表格。
- 單一數值隨時間的變化：用 `LineChart`。

## 用法

每個 `RidgelineSeries` 對應一組資料，包含 `Id`、`Label` 與原始的 `Values`。圖表會為每組計算密度，並依系列順序由上至下繪製山脊。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Ridgeline Label="蘇黎世每日最高氣溫"
                   Unit="°C"
                   DomainMinimum="-5"
                   DomainMaximum="35"
                   ItemsSource="{Binding Months}" />
</UserControl>
```

### 資料集

替換 `ItemsSource` 即可切換資料集。每條曲線都會從目前形狀變形到新形狀，而山脊的順序保持不變。

```csharp
zurich.Click += (_, _) => chart.ItemsSource = Zurich;
lisbon.Click += (_, _) => chart.ItemsSource = Lisbon;
```

### 項目與數值格式

在程式碼中建立的分組可透過 `Items` 加入。`ValueFormatter` 用於格式化座標軸與讀數上的數值。

<Demo name="Untinted" />

### 重疊與平滑度

`Overlap` 設定山脊向上一列延伸的距離，以列高的倍數表示。`RowHeight` 設定每列的高度。`Bandwidth` 以數值的單位設定平滑程度。預設情況下，每組使用 Silverman 規則。

```xml
<arc:Ridgeline Label="延遲" Overlap="2.4" RowHeight="30" Bandwidth="4" ItemsSource="{Binding Regions}" />
```

### 色調與定義域

`IsTinted` 會依每條山脊的中位數上色，顏色從淺色漸層到第一個系列的顏色，並在座標軸下方顯示色階。`DomainMinimum` 與 `DomainMaximum` 固定數值軸，使其不會隨資料集變更而移動。

```xml
<arc:Ridgeline Label="延遲" IsTinted="False" DomainMinimum="0" DomainMaximum="300" ItemsSource="{Binding Regions}" />
```

### 讀取山脊

`ActiveId` 為被抬起的山脊，且為雙向繫結。每次變更都會觸發 `ActiveIdChanged`。

```csharp
chart.ActiveIdChanged += (_, e) => readout.Text = e.Series is null
    ? string.Empty
    : $"{e.Series.Label}: 中位數 {Stats(e.Series).Median:0.0}";
```

## API 參考

<!-- api: Ridgeline -->

<!-- api: RidgelineSeries -->

## 無障礙

- `Label` 為圖表命名。摘要會列出每一組。繪圖區是可聚焦的群組。
- 上、下方向鍵抬起前一條或後一條山脊。左、右方向鍵以一個座標軸步長移動讀數游標。Home 與 End 分別抬起第一條與最後一條山脊，Escape 放下山脊並清除讀數。
- 觸控按下會抬起手指所在的山脊，並讀取該處的數值。
- 禮貌的即時區域會播報被抬起山脊的中位數、中間一半的範圍、取值區間與資料個數。
- 隱藏的資料表列出每組的中位數、四分位數、極值與數量。每一列都有文字標籤，因此不只能透過色調讀取中位數。

## 動態效果

- 資料集或定義域變更時，每條曲線都會在共用網格上重新取樣，並對每個點進行插值。山脊以相同的平滑彈簧膨脹、滑動並分裂。
- 首次顯示時，山脊從頂部開始逐列滾入。
- 被抬起的山脊會以副本的形式繪製在最上層。它上升 6 px，中間一半會加上陰影，並標示中位數。提示跟隨指標移動，並保持在圖表之內。
- 減少動態效果時直接繪製最終狀態。關閉動態效果的表現與減少動態效果相同。

## 相關元件

- `BarChart`：每組只有一個彙總數值時使用。
- `LineChart`：單一數值隨時間的變化。
- `Streamgraph`：多個層的總量隨時間的變化。
- `BrushChart`：單一的高密度時間序列。
