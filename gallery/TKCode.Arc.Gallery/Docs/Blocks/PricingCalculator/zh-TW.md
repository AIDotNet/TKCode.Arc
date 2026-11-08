---
title: Pricing calculator
summary: 方案卡依席位滑桿與計費切換器計算價格：拖曳時價格逐位滾動，推薦徽章會滑到最適合團隊的方案。
source-hash: 5cd9742bf813
---

<Demo name="Preview" />

## 何時使用

`PricingCalculator` 是為依席位銷售之產品設計的定價區段。滑桿設定團隊規模，切換器設定月繳或年繳，三張方案卡則顯示每個方案在該團隊規模下的費用：每個席位每月的價格、計費期間的總額，以及該席位數下方案包含的功能。底部的一列會加總目前使用中的方案、顯示年繳可節省的金額，並提供試用。拖動滑桿時，每個數字都會逐位滾動到新值，「Best fit」徽章會滑到最適合團隊的方案，而一個選取框會跟著目前使用中的方案移動。

- **依席位計價的 SaaS 定價。** 每席位價格搭配年繳折扣、大型團隊的大量折扣，以及附有席位上限的小型方案。
- **回答「我們要付多少？」的定價頁。** 使用者看到的是自己的數字，而不必閱讀表格，推薦內容也會告訴他們該從哪裡開始。
- **產品內的升級畫面。** 將滑桿起點設為團隊目前的席位數並選取其方案，讓頁面顯示團隊成長時會有哪些變化。

## 何時不用

- **價格取決於用量，而非席位。** 請求數、儲存空間或分鐘數，較適合以用量估算來呈現（請見 Usage pricing）。
- **逐項功能比較。** 當使用者要比較數十項跨方案的功能時，請使用比較表格（請見 Plan comparison），本區塊只用來呈現價格。
- **只有月繳／年繳的選擇。** 單一價格加上期間切換，只需要 `BillingToggle` 與 `BillingPrice`。
- **結帳。** 本區塊以試用按鈕作結尾。付款資料、稅金與發票屬於結帳流程。

## 用法

未設定任何參數時，區塊會顯示範例內容：從 1 到 300 的 23 個席位檔位、三個方案，以及模擬的試用。若要用於你的產品，請提供你的方案與檔位。

```xml
<arc:PricingCalculator Title="Plans for every crew" Currency="USD" Billing="Yearly">
    <arc:PricingCalculatorPlan Id="crew" Name="Crew" Description="Dispatch and job notes for small teams"
                               MonthlySeatPrice="18" YearlySeatPrice="15" MaxSeats="16" RecommendFromSeats="1">
        <arc:PricingCalculatorFeature Text="Job board and map" />
        <arc:PricingCalculatorFeature Kind="PerSeat" PerSeatAmount="50" Text="{}{0} photo uploads a month" />
    </arc:PricingCalculatorPlan>
    <arc:PricingCalculatorPlan Id="fleet" Name="Fleet" MonthlySeatPrice="30" YearlySeatPrice="24" RecommendFromSeats="12">
        <arc:PricingCalculatorFeature Text="Everything in Crew" />
        <arc:PricingCalculatorFeature Kind="VolumeDiscount" />
        <arc:PricingCalculatorFeature Text="Quarterly route review" IsYearlyOnly="True" />
    </arc:PricingCalculatorPlan>
</arc:PricingCalculator>
```

```csharp
calculator.SeatStops = [1, 2, 3, 5, 8, 12, 16, 24, 32, 48, 64, 100, 150, 250, 500];
calculator.SetSeats(12);
calculator.TrialHandler = async (request, cancellation) =>
    await api.StartTrialAsync(request.Plan.Id, request.Seats, request.Billing, cancellation);
```

### 組成部分

- **標頭。** `Title` 與 `Description`，置中顯示；若頁面已有自己的標頭，請以 `IsHeaderVisible="False"` 隱藏。
- **控制項。** 「Team size」，席位數兩側為圓形的減號與加號按鈕；滑桿上為每個方案標示其開始被推薦的位置；計費切換器的「Annual」旁顯示年繳折扣。
- **方案卡。** 名稱與「Best fit」徽章、一行說明、「per seat / per month」的每席位價格、卡片總額、方案按鈕與功能清單。
- **選取框。** 包住使用中方案的細外框。它位於卡片上方的一層，會在卡片之間滑動。
- **總計。** 方案與席位數、附期間的總額、每席位價格列、年繳節省金額（若目前為月繳，則為一個切換至年繳的按鈕），以及試用按鈕。

### 價格如何計算

每條規則都是資料，因此同一個區塊可為任何依席位計費的產品定價：

- 每月每席位價格為方案的 `MonthlySeatPrice`；若為年繳，則為其 `YearlySeatPrice`。未設定年繳價格的方案，會以月繳價格減去 `YearlyDiscount`（預設 20%）計算。
- `VolumeTiers` 會從某個席位數起，對每個席位減免一筆金額；套用的是已達到的最大檔位，兩種計費期間皆然。
- 方案卡總額為每席位價格乘以席位數；年繳時再乘以 12。
- 節省金額為年繳在一年中所省下的費用：(月繳價格 − 年繳價格) × 12 × 席位數。大量折扣同時套用於兩種價格，因此兩種期間的節省金額相同。
- 推薦方案是席位數所達到的 `RecommendFromSeats` 中最大者。席位數介於兩個門檻之間時，仍停留在較低的方案。若需要其他規則，請改用 `RecommendPlan`。

計算出的值可作為 `Seats`、`RecommendedPlan`、`SelectedPlan`、`Total` 與 `YearlySavings` 取得。

### 選取方案與席位上限

在使用者選取方案之前，使用中的方案為推薦方案，並隨滑桿移動。以按鈕選取的方案，無論滑桿如何移動都會保持使用中（`PickedPlanId` 為雙向繫結）。設定了 `MaxSeats` 的方案，不能用於超過其上限的席位數：其卡片會淡化說明與功能，並以簡短註記取代價格（"Solo covers up to 8 seats"、"You chose 20 seats."），其按鈕則提供回到上限的選項。超過上限時，使用中的方案會退回推薦方案，但選取的紀錄會保留：移回上限以下後，被選取的方案會再次成為使用中。

### 功能列

`PricingCalculatorFeature` 列可隨席位數與計費期間變化：

- `Kind="PerSeat"` 將 `PerSeatAmount` 乘以席位數，並將結果填入 `Text` 的 `{0}`（"500 GB file storage"）。
- `Kind="VolumeDiscount"` 顯示該方案目前的大量折扣，僅在有適用的折扣時顯示。
- `MinSeats` 會從某個席位數起顯示該列；`IsYearlyOnly` 則只在年繳時顯示。
- `TextFactory` 由你自行計算文字；回傳 null 即隱藏該列。

新出現的列會從零高度展開並淡入；移除的列則淡出並收合。每張卡片都會隨其清單一起增長。

### 你的後端、事件與試用

`TrialHandler` 接收方案、席位數、計費期間與總額，並回傳試用是否已開始。執行期間，試用按鈕會顯示載入圖示並忽略點擊；試用開始後，按鈕會在 `TrialSuccessDuration`（2.8 秒）內顯示 "Trial is live"，且螢幕閱讀器會朗讀已開始的是哪一項試用。若沒有處理常式，區塊會等待 1.1 秒後成功，與預覽相同。`PlanPicked`、`SeatsChanged`、`BillingChanged` 與 `TrialStarted` 會回報使用者的操作；`PlanPickedCommand` 與 `TrialStartedCommand` 則是命令版本。

<Demo name="Backend" />

### 窄容器

區塊讀取的是自身寬度，而非視窗寬度。在 860 px 或以下，卡片會堆疊排列，未使用中的卡片會收合為名稱、價格與按鈕；試用按鈕會移到總計下方。在 560 px 或以下，計費切換器會橫跨整個寬度，總計也會堆疊排列。

<Demo name="Narrow" />

### 貨幣、地區與文字

`Currency` 接受 ISO 4217 代碼，`Labels.Culture` 會格式化每個金額與數量。所有句子都位於 `PricingCalculatorLabels` 中；含有滾動金額的句子（例如節省金額那一行）是格式字串，以 `{0}` 作為佔位。

<Demo name="Localized" />

### 減少動態效果

減少動態效果時，數字、選取框、徽章與計費滑塊都會直接跳到新位置，收合與新列會立即展開，文字變更則保留短暫的淡入效果。

<Demo name="ReducedMotion" />

## API 參考

<!-- api: PricingCalculator -->

<!-- api: PricingCalculatorPlan -->

<!-- api: PricingCalculatorFeature -->

<!-- api: PricingCalculatorVolumeTier -->

<!-- api: PricingCalculatorLabels -->

<!-- api: PricingCalculatorTrialRequest -->

<!-- api: PricingCalculatorPlanPickedEventArgs -->

<!-- api: PricingCalculatorSeatsChangedEventArgs -->

<!-- api: PricingCalculatorBillingChangedEventArgs -->

<!-- api: PricingCalculatorTrialStartedEventArgs -->

## 無障礙

- 對輔助技術而言，滑桿是一個名為 "Team size" 的滑桿，並以席位數（"20 seats"）作為其說明文字。
- 步進按鈕名為 "Remove a seat" 與 "Add a seat"；計費選項是一個名為 "Billing period" 群組中的選項按鈕，年繳選項的名稱包含其折扣。
- 每張卡片以其方案命名，其功能清單名為 "Studio features"，每個方案按鈕都以其動作命名（"Pick Scale"、"Switch to 8 seats"）。
- 逐位滾動的數字是以繪製方式呈現的，但其完整文字（"$4,800"）即為其無障礙名稱。
- 一個禮貌的即時區域會朗讀選定的方案（"Studio picked"）、計費變更，以及試用開始（或失敗）。滑動滑桿不會被朗讀；朗讀的是滑桿本身的值。
- 選擇 "Switch to 8 seats" 後，焦點會留在該按鈕上，接著該按鈕會朗讀為 "Current pick"。

### 鍵盤操作

| 按鍵 | 動作 |
| --- | --- |
| Tab | 減號、加號、滑桿、已選取的計費選項、每個方案按鈕、年繳優惠（僅在月繳時）、試用按鈕。 |
| ← / ↓, → / ↑ | 在滑桿上：向下或向上移動一個檔位。在計費切換器上：選擇另一個選項，並將焦點移至該選項。 |
| Page Down / Page Up | 在滑桿上：向下或向上移動兩個檔位（範圍的 10%）。 |
| Home / End | 在滑桿上：移至第一個或最後一個檔位。 |
| Enter / Space | 按下已聚焦的按鈕。 |

鍵盤焦點會在已聚焦的控制項外顯示 2 px 的焦點環；使用滑鼠聚焦時則不顯示。

## 動態效果

| 元素 | 動效 |
| --- | --- |
| 數字 | 每個位數都是一個滾輪。數字增加時，每個滾輪以 (新值 − 舊值) mod 10 的步數向前轉動，經過中間的數字；減少時則向後轉動。所有滾輪共用 Smooth 彈簧，沒有錯開延遲。一個距離一步的字形會淡出至零並模糊 2 px。新的變化會從滾輪目前的位置接續，並保持其速度。 |
| 新位數 | 新的一欄（及其分隔符）從零寬度展開並以 Smooth 彈簧淡入；消失的一欄則收縮並淡出。 |
| 選取框 | 以臨界阻尼彈簧（視覺時長 0.5 秒）跟隨使用中的方案。它會追蹤卡片目前的尺寸，因此在卡片收合與展開時，它也會跟著變化。 |
| 最佳適配徽章 | 從舊徽章的位置開始，並以相同的彈簧彈入定位；若推薦再次改變，則保留其速度。 |
| 計費滑塊 | 在所選選項下方移動並調整大小，使用 Settle 彈簧。 |
| 文字變更 | 新文字在淡入的同時上升 0.35em（240 毫秒）；舊文字在淡出的同時上升 0.3em（160 毫秒）。 |
| 功能列與收合 | 高度使用 Smooth 彈簧；不透明度為 240 毫秒。 |
| 方案標記 | 推薦方案的標記在 240 毫秒內轉為文字顏色。 |

減少動態效果時，數字、選取框、徽章與計費滑塊會直接跳到新位置，高度立即變化，文字變更則只淡入。

## 響應式行為

- **860 px 以上：** 三欄等寬，每張卡片與最高的卡片同高；計費切換器位於滑桿右側；總計為一列。
- **860 px 或以下：** 單欄；未使用中的卡片收合為名稱、價格與按鈕；試用按鈕在總計下方橫跨整個寬度。
- **約 600 px：** 計費切換器移到滑桿下方。
- **560 px 或以下：** 標題較小、內距較緊密，計費切換器橫跨整個寬度，總計堆疊排列。
- **360 px 或以下：** 年繳優惠與節省金額那一行可以換行。

## 相關

- Plan comparison：以表格列出每個方案的功能。
- Usage pricing：依用量而非席位計價。
- `BillingToggle` 與 `BillingPrice`：期間切換器與會滾動的價格。
- `AnimatedCounter`：單一滾動數字。
