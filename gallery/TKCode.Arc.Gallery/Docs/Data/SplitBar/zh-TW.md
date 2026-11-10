---
title: Split bar
summary: 一條長條依各部分在總量中的占比分段，下方圖例列出名稱、數值與百分比；資料變化時，各段以彈簧動畫移到新的占比。
source-hash: ff0ca4153563
---

<Demo name="Storage" />

## 何時使用

`SplitBar` 用一行回答「這個總量是由什麼組成的」。它繪製一條水平長條，切成數段，每段的寬度等於它在總量中的占比，段與段之間留有小間隔；
下方的圖例寫出每一段的名稱、數值與百分比。總量可以是各部分的總和，也可以是固定的量，例如磁碟容量或預算，此時長條剩下的部分保持空白，
由圖例為它命名（"Free"、"Remaining"）。當某個數值改變、新增一段或移除一段時，每一段都會以彈簧動畫移到新的寬度；動畫進行中收到新資料時，
彈簧會保持目前的速度，因此即時資料不會讓長條跳動。

- **用量與配額**：依檔案類型區分的儲存空間、依方案區分的席位、依模型區分的 token，並顯示剩餘空間。
- **預算與成本**：依類別區分的支出，與每月預算對照。
- **測試與工作結果**：通過、略過與失敗，依含義著色。
- **即時組成**：依來源區分的流量或依地區區分的負載，每隔幾秒更新一次。

## 何時不用

- **在多個總量之間比較各部分**：`BarChart` 中的堆疊長條可將多個總量並排顯示。
- **許多細小的部分**：超過六、七段後，細條與圖例都會不易閱讀。請把尾端的小項目合併為 "Other"。
- **需要精確讀取每個數值**：當使用者在意數字多於比例時，請使用 `Table` 或清單。
- **朝單一目標的進度**：`Progress` 進度條比分段條更直接地表達「完成了多少」。

## 用法

加入帶有 `Label` 與 `Value` 的 `SplitBarSegment` 項目。未設定 `Total` 時，整條長條就是各部分的總和。

```xml
<arc:SplitBar Title="Storage" Total="256" ValueDecimals="1" ValueSuffix=" GB" RemainderLabel="Free">
    <arc:SplitBarSegment Label="Photos" Value="82.4" />
    <arc:SplitBarSegment Label="Apps" Value="46.1" />
    <arc:SplitBarSegment Label="Documents" Value="21.7" />
</arc:SplitBar>
```

標題列顯示 `Title` 與滾動變化的總計（"168.2 GB of 256 GB"）；`IsSumVisible="False"` 會隱藏總計，`IsLegendVisible="False"` 會隱藏圖例。
數值依 `ValuePrefix`、`ValueDecimals` 與 `ValueSuffix` 格式化；`FormatValue(value)` 可為你自己的標籤產生相同的文字。

### 即時資料

將 `ItemsSource` 繫結到一組分段，並直接修改它們的 `Value`：分段顯示在螢幕上時，長條會監聽每一段的變化。新增的分段會從無到有地展開，
移除的分段會在原位置收縮消失。

<Demo name="LiveShares" />

```csharp
foreach (var segment in segments)
{
    segment.Value = Math.Round(segment.Value * 1.1);
}
```

### 醒目提示與觸發

指向某一段或它的圖例項目時，該段會浮起，其餘各段變暗；`HighlightedIndex` 會回報目前醒目提示的段，也可以繫結。按一下，或在長條取得焦點時按
Enter，會引發 `SegmentInvoked`，並附上 `Segment`、它的 `Index` 與 `Share`。

### 色彩

未設定 `Brush` 的分段會依序使用圖表系列色彩（系列 1 到 4，之後是它們較淺的幾輪）。當色彩帶有含義時，請為每一段設定 `Brush`，
如下方的結果條所示。

<Demo name="Compact" />

## API 參考

<!-- api: SplitBar -->

<!-- api: SplitBarSegment -->

<!-- api: SplitBarSegmentEventArgs -->

## 無障礙

- 控制項是以 `Title` 命名的群組（沒有標題時為 "Split bar"）。長條本身是一個 Tab 停駐點，它的協助工具名稱會列出每一段
  （"Photos, 82.4 GB, 32 percent; ..."），有段被醒目提示時只說出該段。
- 左右鍵（或上下鍵）移動醒目提示，Home 與 End 跳到第一段與最後一段，Enter 或空白鍵觸發醒目提示的段，Escape 清除醒目提示。鍵盤焦點框繪製在醒目提示的段周圍。
- 圖例以文字重複每個名稱與數值，因此色彩從來不是區分各段的唯一方式。

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 新的占比 | 每一段以彈簧動畫移到新寬度（約 0.45 s，略有回彈，被打斷時保持速度） | 直接跳到 | 直接跳到 |
| 新增 / 移除一段 | 從無到有地展開 / 在原位置收縮消失 | 出現 / 消失 | 出現 / 消失 |
| 醒目提示 | 其餘各段在 160 ms 內變暗 | 立即變暗 | 立即變暗 |
| 圖例數值 | 數字滾動 | 立即改變 | 立即改變 |

第一次的資料會立即顯示；只有之後的變化才有動畫。只有占比在變動時，畫格迴圈才會執行。

## 響應式

- 長條填滿可用寬度；放不下時，圖例項目會換到更多行，過長的名稱以省略號截斷。
- 標題列把總計放在標題右側；請給控制項至少 240 px 的寬度，兩者才能同時放下。
- 在觸控螢幕上沒有滑鼠懸停的醒目提示；輕觸會觸發手指下方的那一段。

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `BarChart` | 比較多個總量或隨時間變化的系列 |
| `Progress` | 顯示一項工作完成了多少 |
| `MetricTicker` | 顯示多個即時數字，而不是單一總量的各部分 |
