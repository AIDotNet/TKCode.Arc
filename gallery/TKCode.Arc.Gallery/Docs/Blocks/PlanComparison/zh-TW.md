---
title: Plan comparison
summary: 方案卡片附有月繳或年繳開關，切換後每個價格都會計數至新的計費週期；另有一個醒目標示的方案，以及顯示各級方案增加哪些功能的功能矩陣。
source-hash: 71d72470075d
---

<Demo name="Preview" />

## 何時使用

方案比較會把兩到三個方案並排放置，讓人看出各方案之間的差異，以及每個方案的費用。切換計費週期時，每個價格都會計數至新金額；選取方案會為該欄上色；一個開關就能隱藏所有方案共有的功能，只留下差異。

- **兩到三個層級的定價頁**：每個方案各有一欄，欄中顯示每個席位的價格與選取按鈕；每一列功能則顯示各層級包含哪些內容。
- **月繳與年繳價格**：計費開關會把每個價格計數至另一個週期，開關下方的說明則依你的價格算出最大的年繳節省金額。
- **功能清單很長**：「Show differences only」會收合所有方案共有的列，篩選器旁的計數會顯示仍然列出的功能數量。
- **應用程式內的升級畫面**：替已登入的使用者選取方案，並監聽選取變更，將他們帶往結帳流程。

## 何時不用

- **價格取決於席位或流量**：請改用計算機，例如 `UsagePricing` 或 `PricingCalculator`，它們會依使用者拖曳的內容算出價格。
- **只需要計費開關**：請單獨使用 `SegmentedControl`。
- **降級或取消**：使用 `CancelFlow`，它會針對取消原因提出優惠。
- **超過三或四個方案**：欄位會變得太窄，難以閱讀。請以卡片呈現方案，並連結到完整的比較表。

## 用法

沒有設定方案與功能時，區塊會顯示 Arc 預覽中的範例內容：Team 與 Studio 兩個方案，以及八項功能。你可以透過 `Plans` 與 `Features` 提供自己的內容（或繫結 `PlansSource` 與 `FeaturesSource`，設定後會優先生效）。每項功能為每個方案列出一個值，順序與方案相同：

```xml
<arc:PlanComparison Currency="EUR" Billing="{Binding Billing}" SelectedPlan="{Binding Plan}">
    <arc:PlanComparison.Plans>
        <arc:PlanComparisonPlan Name="Notebook" Description="For one field researcher" MonthlyPrice="8" YearlyPrice="6" />
        <arc:PlanComparisonPlan Name="Station" Description="For a survey crew" MonthlyPrice="19" YearlyPrice="15" />
    </arc:PlanComparison.Plans>
    <arc:PlanComparison.Features>
        <arc:PlanComparisonFeature Label="Offline maps" IsShared="True">
            <x:String>Included</x:String>
            <x:String>Included</x:String>
        </arc:PlanComparisonFeature>
        <arc:PlanComparisonFeature Label="Observers">
            <x:String>1 person</x:String>
            <x:String>Up to 12</x:String>
        </arc:PlanComparisonFeature>
    </arc:PlanComparison.Features>
</arc:PlanComparison>
```

### 你自己的方案與事件記錄

以歐元定價的三個方案，一開始只列出差異。`SelectedPlanChanged` 回報每一次選取變更（`IsUserAction` 可分辨是使用者按下按鈕，還是應用程式變更），`BillingChanged` 回報每一次計費週期變更，`SelectCommand` 則會在有人按下選取按鈕時執行。

<Demo name="OwnPlans" />

### 區塊自身的卡片

預設情況下，區塊會繪製一張卡片：有邊框、面板圓角、表面色，各區段之間有分隔線，寬度最多 1060 px 並置中。若要像 Arc 預覽那樣把它平貼進頁面區段，請設定 `IsFramed="False"`。

<Demo name="Card" />

### 值

等於 `Labels.Included`（“Included”）的值會顯示綠色勾號；等於 `Labels.NotIncluded`（“Not included”）的值會以淡化樣式顯示。其他文字則照原樣呈現。當每個方案都提供相同功能時，請將該功能標記為 `IsShared`，篩選器便能將它隱藏。

### 文字、文化與幣別

所有文字都放在 `PlanComparisonLabels` 中：若要翻譯此區塊，請替換整個物件。包含數字或方案名稱的句子是函式。價格使用 `Labels.Culture`，並以 `Currency` 的 ISO 代碼顯示；整數價格不顯示小數，含小數的價格則以分為單位計數。

<Demo name="Localized" />

### 窄容器

<Demo name="Narrow" />

### 減少動態效果

<Demo name="ReducedMotion" />

### 文案規範

- 方案名稱請保持一個字，描述只寫幾個字，讓價格與按鈕能在各欄對齊。
- 寫出可以互相比較的值：「30 days」對上「Unlimited」，比兩個完整句子讀得更快。
- 把差異最大的功能放在最前面；共有的功能可以保留，因為篩選器會將它們隱藏。
- 在註腳中說明價格涵蓋的內容：幣別、是否按席位計算、稅金，以及年繳方案的收費方式。

### 最佳做法

- 以雙向方式繫結 `SelectedPlan`，並從 `SelectedPlanChanged` 或 `SelectCommand` 開始結帳；再次按下已選取方案的按鈕，會清除選擇。
- 保持兩到三個方案。每個方案都會增加一欄，而且必須能放在功能名稱旁邊。
- 年繳價格請以每月金額計算。說明文字會依你的月繳與年繳價格算出 “save up to N%”。

## API 參考

<!-- api: PlanComparison -->

<!-- api: PlanComparisonPlan -->

<!-- api: PlanComparisonFeature -->

<!-- api: PlanComparisonSelectionChangedEventArgs -->

<!-- api: PlanComparisonBillingChangedEventArgs -->

## 無障礙

- 矩陣以其所含方案命名（“Team and Studio plan comparison”）。每個方案標頭命名為 “Team plan”，每個值以其所屬方案命名（“Team: 30 days”），每一列則以其功能命名。
- 價格是一個禮貌型即時區域（polite），其名稱永遠是最終價格，因此螢幕閱讀器不會唸出價格計數過程中經過的數字。
- 選取的方案會以禮貌模式播報（“Team selected”）。選取按鈕的名稱維持 “Select Team”，並以 “Selected” 作為狀態回報。
- 計費開關的名稱為 “Billing period”，篩選開關的名稱為 “Show differences only”。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在計費開關、篩選開關與選取按鈕之間移動 |
| `Left` / `Right` | 在計費開關中選擇計費週期 |
| `Space` | 開啟或關閉篩選 |
| `Enter` / `Space` | 選取方案；若已選取則清除選取 |

鍵盤焦點會在目前聚焦的控制項周圍顯示 2 px 的焦點環；滑鼠焦點則不顯示。

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 計費開關切換 | 每個價格以 240 ms 計數至新金額，採用進入曲線 | 直接跳變 | 直接跳變 |
| 功能數量變化 | 以 240 ms 計數 | 直接跳變 | 直接跳變 |
| 方案被選取 | 該欄在 240 ms 內上色；導軌原地出現 | 上色與導軌立即完成 | 立即完成 |
| 選取移動 | 導軌以 Arc 的響應式彈簧（剛度 520、阻尼 44）滑動 | 立即移動 | 立即移動 |
| 列隱藏 | 高度以平滑彈簧收合（0.4 s，無回彈），並在 160 ms 內淡出 | 立即消失 | 立即消失 |
| 列回復 | 高度以平滑彈簧展開，40 ms 後以 240 ms 淡入 | 立即回復 | 立即回復 |

被中斷的計數會從畫面上的數字繼續。

## 響應式

- 版面依區塊自身的寬度調整，而非視窗的寬度。在 720 px 以下（`:compact`）時，計費開關會堆疊在標題下方，邊距收緊為 20 px。
- 在 540 px 以下（`:narrow`）時，功能欄會被移除：每個功能名稱位於其值的上方，而值仍位於各自的方案欄下，標題與價格也會縮小。
- 在 340 px 以下（`:tiny`）時，方案與值的內距會收緊為 12 px 與 16 px，讓兩個方案仍能並排放下。

## 相關元件

| 元件 | 適用情境 |
|---|---|
| `PlanComparison` | 人們要並排比較幾個固定方案 |
| `UsagePricing` | 方案取決於席位數與流量 |
| `PricingCalculator` | 價格取決於席位數，且會隨席位數變動而滾動更新 |
| `SegmentedControl` | 你只需要計費開關 |
| `CancelFlow` | 有人要降級或取消 |
