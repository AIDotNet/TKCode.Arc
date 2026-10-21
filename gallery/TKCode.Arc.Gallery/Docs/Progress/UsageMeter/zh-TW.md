---
title: 用量表
summary: 顯示什麼占用了配額，以及距離上限還剩多少。
source-hash: da12e4afa9b4
---

<Demo name="Storage" />

## 何時使用

- 以固定配額衡量的儲存空間、席次或 API 呼叫次數。
- 帳單頁面，列出哪些內容占用了方案，以及還剩多少。
- 方案可能被超出的情境，超出的部分必須不依賴顏色也能清楚辨識。

## 何時不用

- 朝向完成狀態推進的工作：用 `Progress`。
- 沒有上限、需要彼此比較的類別：用 `BarChart`。
- 單一的核心指標：用 `Gauge` 或 `MetricCard`。

## 用法

為用量表提供 `Label`、`Limit` 以及各個類別。每個類別是一個 `UsageSegment`，包含 `Id`、`Label` 與 `Value`，`Value` 的單位與上限相同。請保持 `Id` 穩定，讓類別在清單變動時仍保有各自的動態效果。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:UsageMeter Label="工作區儲存空間" Limit="50" Unit="GB">
        <arc:UsageSegment Id="media" Label="媒體" Value="14.2" />
        <arc:UsageSegment Id="docs" Label="文件" Value="9.6" />
        <arc:UsageSegment Id="backups" Label="備份" Value="6.1" />
        <arc:UsageSegment Id="other" Label="其他" Value="2.4" />
    </arc:UsageMeter>
</UserControl>
```

### 繫結類別

`ItemsSource` 優先於內嵌的 `UsageSegment`。繫結 `UsageSegment` 物件的集合。用量表會監看集合以及每個分段，因此修改 `Value` 會重新排版進度條。

```xml
<arc:UsageMeter Label="工作區儲存空間"
                Limit="50"
                Unit="GB"
                ItemsSource="{Binding Categories}" />
```

```csharp
media.Value = 17.4; // 進度條、總計與標章都會更新
```

### 接近上限與超出上限

標章顯示剩餘量。達到上限的 90% 時，會變為帶有警告圖示的「即將用完」；超過上限後則顯示超出的部分。`WarnThreshold` 可更改觸發警告的比例。

<Demo name="AlmostFull" />

<Demo name="OverTheLimit" />

```xml
<arc:UsageMeter Label="本月 API 呼叫"
                Limit="100"
                Unit="K"
                WarnThreshold="0.8" />
```

### 單位與小數位

`Unit` 會顯示在每個數值之後。`Decimals` 設定小數點後的位數。對於席次等整數計數，請使用 `Decimals="0"`。

```xml
<arc:UsageMeter Label="席次" Limit="20" Decimals="0">
    <arc:UsageSegment Id="eng" Label="工程" Value="12" />
    <arc:UsageSegment Id="design" Label="設計" Value="6" />
    <arc:UsageSegment Id="sales" Label="業務" Value="5" />
</arc:UsageMeter>
```

### 剩餘與超出部分的標籤

`FreeLabel` 為圖例中的剩餘配額命名，`OverLabel` 則在總量超過上限後為超出部分命名。

```xml
<arc:UsageMeter Label="專案"
                Limit="10"
                FreeLabel="剩餘"
                OverLabel="超出方案" />
```

### 程式碼中的狀態

讀取 `Status` 可取得目前狀態（`Ok`、`Near` 或 `Over`），讀取 `Total` 可取得所有類別的總和。

```csharp
if (meter.Status == UsageMeterStatus.Over)
{
    ShowUpgradePrompt();
}
meter.ClearPin(); // 清除從圖例中釘選的類別
```

## API 參考

<!-- api: UsageMeter -->

<!-- api: UsageSegment -->

## 無障礙

- 用量表是一個以 `Label` 命名的群組。進度條是一張影像，其名稱為每個類別、總計與狀態的完整文字摘要。
- 「即將用完」或超出量增加等狀態變化會以禮貌方式播報。
- 每個圖例項目都是一個切換按鈕，以類別與數量命名，例如「媒體，14.2 GB」。
- 圖例只占一個 Tab 停駐點。方向鍵在各項目之間移動，Home 與 End 分別前往第一項與最後一項。
- Enter 或 Space 會釘選取得焦點的類別，Escape 則清除釘選。

## 動態效果

- 用量表第一次有 40% 可見時，各類別從左到右依序增長。
- 之後，類別的值變化時，各自滑動到新寬度，數字滾動到新值。
- 超出上限後，超出斜紋與上限標記淡入。
- 懸停或聚焦某個類別會使其他類別變暗，按下則會將其釘選。
- 減少動態效果時，直接設定數值與進度條，文字以短暫淡入切換。

## 相關元件

- `Progress`：工作朝向完成推進且沒有配額時使用。
- `BarChart`：沒有上限、需要彼此比較的類別。
- `Gauge` 或 `MetricCard`：單一的核心數字。
