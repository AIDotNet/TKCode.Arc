---
title: 用量定價
summary: 一個為你挑選方案的定價計算器：拖動席次數與每月流量，方案名稱會變形，估算金額會計數到新總額，行動按鈕隨之變化。
source-hash: c9c11477274e
---

<Demo name="Preview" />

## 何時使用

`UsagePricing` 是按席次計費、並在其上疊加計量部分的產品定價頁中的「我要付多少」區塊。人們只需設定兩個已知的數字：多少席次，以及大致的每月用量。區塊會完成其餘工作：挑出合適的方案，用一行話說明原因，顯示每月估算並加以拆解。行動按鈕永遠寫明推薦的方案。

- **席次加用量定價。** API 平台、分析、訊息與儲存空間：每個席次一個價格，每個方案包含一定額度的用量，超出部分依固定費率計費。
- **依上限劃分的方案。** 每個方案推薦適用於一定數量的席次和一定額度的用量。區塊會挑出第一個能同時涵蓋兩個值的方案，因此團隊在超過任一上限時，都會看到下一個方案。
- **月付與年付。** 切換開關會顯示年付為席次節省的金額，在明細中加入折扣列，並把估算滾動更新。

## 何時不用

- **並排比較的固定檔位。** 如果人們需要逐項比較各方案，請使用 Plan comparison 區塊。
- **既有客戶的用量預測。** 已經付費的客戶希望預測自己的用量時，請使用用量預測（usage forecast），或帳單頁面上的 `UsageBilling`。
- **超過兩個變數的定價。** 區塊恰好有兩個滑桿。附加項目、地區或支援等級應放在設定器或結帳頁中。
- **尋找最便宜的方案。** 推薦遵循各方案的上限，而不是最低總價。如果你的方案有重疊，使較大的方案反而更便宜，請相應設定上限，或改用比較表呈現方案。

## 用法

該區塊衍生自 Avalonia 的 `TemplatedControl`。不做任何設定時，它顯示範例：三個方案（Spark、Studio 和 Fleet）、23 個席次刻度與 27 個每月呼叫次數刻度，以美元計價。

```xml
<arc:UsagePricing SeatIndex="10" UsageIndex="14" BillingPeriod="Monthly" />
```

你自己的方案放在 `Plans` 中，由小到大排列。每個 `UsagePricingPlan` 都有席次價格、它所包含的用量、超出該用量後每個 `OverageUnit` 的價格、每個席次的年付折扣、推薦所涵蓋的上限以及它的功能。最後一個方案通常沒有上限。

```xml
<arc:UsagePricing Currency="EUR">
    <arc:UsagePricingPlan Name="Solo" SeatPrice="9" YearlySeatDiscount="1"
                          IncludedUsage="1000000" OveragePrice="6" MaxSeats="3" MaxUsage="2000000">
        <arc:UsagePricingFeature Key="history" Label="Event history" Value="7 days" />
    </arc:UsagePricingPlan>
    <arc:UsagePricingPlan Name="Team" SeatPrice="19" YearlySeatDiscount="3"
                          IncludedUsage="10000000" OveragePrice="3">
        <arc:UsagePricingFeature Key="sso" Label="Single sign-on" />
        <arc:UsagePricingFeature Key="history" Label="Event history" Value="90 days" />
    </arc:UsagePricingPlan>
</arc:UsagePricing>
```

滑桿沿 `SeatStops` 與 `UsageStops` 逐步移動。刻度沿軌道均勻分布，無論其數值為多少，因此每一步按固定倍數增長的清單會帶來對數般的手感。`SeatIndex` 與 `UsageIndex` 是選取的刻度，雙向繫結；`Seats` 與 `Usage` 讀取對應的數值。

### 結構

1. **標題與說明**：`Labels.Title` 與 `Labels.Description`。
2. **席次滑桿與用量滑桿**：Arc 的 `Slider`，數值位於右上角；拖曳或聚焦時，滑桿拇指上會出現氣泡；每個第一個方案之後的方案，在其起點處都有一個帶標籤的刻度。
3. **計費開關**：一個 `SegmentedControl`，包含 Monthly 與 Annual，旁邊是年付節省的金額。年付生效後，節省金額前會長出一個勾，文字變為綠色。
4. **明細**：席次列（"Studio seats"，16 × $26）、用量列（包含在內，或超出的用量及其費率）、依年付計費時顯示的年付折扣列，以及預估總額。
5. **推薦卡片**：一個計量條（每個方案一段）、方案名稱、理由、每月估算、計費說明、功能清單，以及試用按鈕及其說明。

### 推薦與理由

推薦的方案是第一個 `MaxSeats` 與 `MaxUsage` 都能涵蓋所選值的方案（上限為 null 時涵蓋一切）。理由列將所選值與上一個方案的上限相比較，共有四種形式：

| 理由 | 何時 | 預設文字 |
|---|---|---|
| `WithinLimits` | 第一個方案 | "Fits up to 4 seats and 1.5M calls a month" |
| `SeatsAbove` | 只有席次超過上一個方案 | "Suggested beyond 36 seats" |
| `UsageAbove` | 只有用量超過上一個方案 | "Suggested beyond 20M calls a month" |
| `BothAbove` | 席次與用量都超過上一個方案 | "Suggested beyond 4 seats and 1.5M calls" |

理由可以在方案不變的情況下變化，例如在 Studio 內從 4 個席次變為 5 個；此時只替換句子。`RecommendationChanged` 對兩種變化都會引發。

### 估算

| 數值 | 公式 |
|---|---|
| 席次列 | 席次數 × 席次價格 |
| 用量列 | max(0, 用量 − 包含量) ÷ `OverageUnit` × 超額價格；沒有超出時為 "Bundled" |
| 年付折扣 | 席次數 × 每個席次的年付折扣（僅在依年付計費時顯示並扣減） |
| 預估總額與卡片價格 | 席次列 + 用量列 − 年付折扣 |
| 年付節省 | 席次數 × 每個席次的年付折扣 × 12（僅計席次，用量不計入） |
| 年付計費說明 | 席次數 × (席次價格 − 折扣) × 12 |

金額為整數，使用 `Labels.Culture` 的文化區域與 `Currency` 的貨幣；負數帶有真正的減號（"−$80"）。

### 自有資料與事件

<Demo name="Live" />

該範例為一個儲存空間產品依編輯者與每 100 GB 定價（`OverageUnit = 100`，`UsageFormatter` 寫出 GB 與 TB），取代了文字，並透過一個會拒絕最大方案的模擬註冊流程開始試用。`SelectionChanged`、`RecommendationChanged`、`TrialStarted`、`TrialUndone` 與 `TrialFailed` 都是帶型別參數的路由事件。

### 試用

按鈕上顯示 "Try {plan}"。按下後 `IsTrialStarted` 開啟：按鈕變為次要樣式並帶一個勾，顯示 "Trial is on"，其下方的說明變為 "Trial is on. Undo"。試用歸屬於開啟時所在的方案：在該方案內移動滑桿會保留試用，而推薦的方案變更則會將其關閉。撤銷會關閉試用，並把鍵盤焦點交還給按鈕。

將 `TrialHandler` 設為呼叫你的註冊流程。工作執行期間按鈕顯示旋轉指示；工作完成時試用開啟，拋出例外時試用保持關閉（並引發 `TrialFailed`）。

```csharp
pricing.TrialHandler = async (request, cancellation) =>
    await api.StartTrialAsync(request.Plan.Key, request.Seats, request.Usage, cancellation);
```

`StartTrialAsync()` 與 `UndoTrial()` 在程式碼中執行相同的操作。

### 窄容器

<Demo name="Narrow" />

寬度達到 780px 時，卡片在右側佔有自己的 392px 欄。更窄時，標題會縮小，卡片在控制項與明細之間移動。

### 貨幣、文化與文字

<Demo name="Localized" />

每句文字都是 `UsagePricingLabels` 的一個項目。帶有滾動金額或變形方案名稱的句子（"{amount} saved per year"、"{plan} seats"、"{amount} per year for seats, calls monthly"）可以把該值放在任何位置：區塊會把句子拆開，在其間插入該值。

### 減少動態效果

<Demo name="ReducedMotion" />

### 內容指南

- 用簡短的詞為方案命名；名稱會出現在卡片、計量條、席次列與按鈕上。
- 理由句要足夠短，在 392px 寬度下能放在一行內。
- 列出在方案之間會變化的功能。本方案新增的列排在最前，並以強調色繪製；從上一個方案繼承的列依其原有順序排在其後，並以較淡的顏色繪製。
- 使用人們熟悉的刻度：整齊的團隊規模，以及與你的方案上限相符的用量數額。

### 最佳實務

- 讓每個方案的上限與下一個方案的上限銜接：超過某個上限的第一個刻度，正是下一個方案的刻度標記所在之處。
- 對 `IncludedUsage`、`MaxUsage`、用量刻度與 `OverageUnit` 使用同一單位。
- 保持估算誠實：總計是未含稅金額，正如總計列所寫。

### 常見問題

**為什麼推薦的是較大的方案，而較小的方案可能更便宜？** 推薦遵循你設定的上限，而不是價格。如果某個方案應當被推薦更長時間，請提高它的上限。

**滑桿能從別的位置開始嗎？** 設定 `SeatIndex` 與 `UsageIndex`，或將它們繫結到你的檢視模型。

**能否把人們引導到註冊頁面，而不是就地開始試用？** 將 `TrialHandler` 設為開啟你的註冊流程；返回時試用開啟，拋出例外時保持關閉。

## API 參考

<!-- api: UsagePricing -->

### UsagePricingPlan

<!-- api: UsagePricingPlan -->

### UsagePricingFeature

<!-- api: UsagePricingFeature -->

### UsagePricingPeriod

<!-- api: UsagePricingPeriod -->

### UsagePricingReason

<!-- api: UsagePricingReason -->

### UsagePricingRecommendationChangedEventArgs

<!-- api: UsagePricingRecommendationChangedEventArgs -->

### UsagePricingSelectionEventArgs

<!-- api: UsagePricingSelectionEventArgs -->

### UsagePricingTrialRequest

<!-- api: UsagePricingTrialRequest -->

### UsagePricingLabels

<!-- api: UsagePricingLabels -->

### UsagePricingSamples

<!-- api: UsagePricingSamples -->

## 無障礙

- 該區塊是以其標題命名的區域；卡片是以方案名稱命名的補充性地標。
- 每個滑桿以其標籤命名，並朗讀格式化後的數值（"16"、"12M"），其值為刻度索引。
- 計費開關名為 "Billing period"；每個分段朗讀其標籤以及是否被選取。
- 功能清單名為 "Included in {plan}"；每一列朗讀其標籤與數值。計量條對輔助技術隱藏，因為方案名稱已表達相同的內容。
- 當方案或理由變更時，一個禮貌的即時區域會朗讀 "{plan}. {reason}."。
- 試用按鈕由其下方的說明描述。點選撤銷後，焦點回到按鈕。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 席次滑桿、用量滑桿、計費開關、試用按鈕，試用開啟時再到撤銷 |
| Left / Down，Right / Up | 向下或向上移動一個刻度 |
| Page Down / Page Up | 向下或向上移動三個刻度 |
| Home / End | 第一個或最後一個刻度 |
| 開關上的 Left / Right | 切換到 Monthly 或 Annual，立即生效 |
| Enter / Space | 開始試用，或撤銷試用 |

## 動態效果

| 元素 | 動態效果 |
|---|---|
| 滑桿拇指 | 滑桿自身的彈簧；數值與氣泡數字以緩動淡入方式滾動 |
| 金額 | 每一位數字在 Smooth 彈簧上依序轉過中間的數字，淡出並模糊至距離基線最多 2px；新數字與分隔符逐漸長出 |
| 方案名稱、"{plan} seats"、功能值 | 文字變形：保留的字母滑動，新字母升入，舊字母落下 |
| 理由、計費說明、試用說明、用量值 | 舊文字在 120ms 內向上淡出，新文字在 200ms 內上升 6px |
| 計量條 | 條形在柔和的 Smooth 彈簧上從左側填滿；當前名稱在 240ms 內轉為強調色 |
| 功能清單 | 保留的列滑動到新位置，新列在 240ms 內從上方 8px 處淡入，離開的列在 120ms 內原地淡出；清單高度依彈簧跟隨變化 |
| 年付計費 | 開關的膠囊滑動，節省金額的勾位在 Smooth 彈簧上展開，其筆畫在 350ms 內繪製，文字在 240ms 內變為綠色，折扣列展開並淡入 |
| 試用 | 按鈕變色，標籤與繪製的勾互換，寬度依 Smooth 彈簧跟隨變化 |

減少動態效果時保留顏色，以及新列和替換句子的短淡入；數字、變形、清單的滑動與高度，以及計量條都直接跳轉。關閉動態效果時，一切都會立即完成。

## 響應式

該區塊使用自身寬度，而非視窗寬度。從 780px 起，它排為兩欄（卡片寬 392px）；低於此寬度時，它依標題、控制項、卡片和明細的順序堆疊，標題為 28px，卡片內距更緊湊。當節省金額放不進開關旁邊時，計費列會換行。該區塊沒有最大寬度：請給它頁面允許的寬度。

## 相關元件

- Plan comparison：逐項比較的固定方案。
- Pricing calculator：以席次滑桿定價的方案卡片。
- `UsageBilling`：既有客戶當前週期的用量。
- `Slider`、`SegmentedControl`、`TextMorph`：該區塊所用的控制項。
