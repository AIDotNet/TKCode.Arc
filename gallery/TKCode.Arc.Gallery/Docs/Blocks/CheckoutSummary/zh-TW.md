---
title: Checkout summary
summary: 訂單摘要：已接受的優惠碼會變成可移除的標籤塊，折扣與稅金行滑入，總計滾動至新金額，付款按鈕依序經過「Processing」到「Paid」。
source-hash: 14d7aeafe553
---

<Demo name="Preview" />

## 何時使用

`CheckoutSummary` 是某人為一項商品付款前的最後一張卡片。它會顯示對方正在購買的內容、所有會影響總計的金額、優惠碼欄位，以及付款按鈕。內容變動時，所有元件都會跟著移動：被接受的優惠碼會變成標籤塊，其折扣行滑入明細，稅金重新計算，總計以滾動數字逐位變成新金額，付款按鈕也會顯示新價格。

- **單一商品結帳。** 方案、通行證、授權或一次性購買，訂單已經確定，只剩優惠碼與付款。
- **升級確認。** 定價頁之後的下一步：使用者選定的方案、其年度折扣、稅金與一個清楚的按鈕。
- **嵌入式付款步驟。** 結帳頁的摘要欄，地址與卡號欄位位於別處；卡片上的付款按鈕會啟動主機自己的付款呼叫。

## 何時不用

- **收集地址與卡號。** 摘要不會要求付款資料；請將這些欄位放在獨立的表單中，並把這張卡片放在旁邊。
- **多項商品或數量。** 當使用者需要新增、移除與計算商品時，請使用 `CartDrawer`。
- **既有訂閱的帳單。** 若要顯示發票、付款方式與下次扣款，請使用 `BillingOverview`。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`，由資料驅動：`Item`（名稱、描述、價格與計費週期）、以子元素寫入的自動 `Adjustments`、選用的 `Tax`、`Currency`（ISO 代碼），以及用來格式化金額的 `CultureName`。提供 `CouponValidator` 後，優惠碼欄位才會出現；付款會執行 `PayAsync`。

```xml
<arc:CheckoutSummary x:Name="Summary" Width="440">
    <arc:CheckoutSummary.Item>
        <arc:CheckoutSummaryItem Name="Sketchbook Pro, annual"
                                 Description="Infinite canvases and backups for one maker"
                                 Price="120"
                                 Period="billed yearly" />
    </arc:CheckoutSummary.Item>
    <arc:CheckoutSummaryLine Id="annual" Label="Annual plan" Note="Three months free" Amount="-30" />
    <arc:CheckoutSummary.Tax>
        <arc:CheckoutSummaryTax Label="Sales tax" Rate="0.075" />
    </arc:CheckoutSummary.Tax>
</arc:CheckoutSummary>
```

```csharp
Summary.CouponValidator = async (code, token) =>
{
    var answer = await api.CheckPromoCodeAsync(code, token);
    return answer.Ok
        ? CheckoutSummaryCouponResult.ValidPercent(answer.Code, answer.Percent, answer.Label)
        : CheckoutSummaryCouponResult.Invalid(answer.Message);
};
Summary.PayAsync = token => api.ChargeAsync(Summary.Total, token);
```

`CheckoutSummarySamples` 提供預覽的內容與一個模擬後端（700 ms 的優惠碼檢查會接受 HELLO15 與 FRIEND5，1400 ms 的付款可設定為失敗）。請將兩者都換成你自己的呼叫。

### 組成部分

- **標題**：預設為「Order summary」（`Title`），同時也是卡片的無障礙名稱。
- **商品列**：名稱佔一行（超出時以省略號截斷）、描述可換行，右側是價格與計費週期，名稱前方可選擇加入 44 px 的 `Media` 格。
- **優惠碼**：一個附有 Apply 的欄位，成功後變成附有移除按鈕的「applied」標籤塊；被拒絕時，欄位下方會開啟錯誤行。
- **明細**：Subtotal、依序排列的每個調整項、優惠碼行，然後是稅金。負數金額為折扣，以綠色顯示。
- **總計**：以展示字體呈現的滾動數字。
- **付款按鈕**：先顯示鎖頭，接著是安全提示（「Encrypted payment」），以及可選的 `Footnote`。

### 計算方式

小計就是商品價格。調整項會加到小計上（調整後的小計永遠不會低於零）。百分比優惠碼套用在調整後的小計上，而不是定價上；固定金額優惠碼則扣除其金額，但不會超過剩餘的金額。稅金是稅率乘以剩餘金額，並在加入前四捨五入到分。以範例資料計算：120 − 30 = 90，稅金 6.75，總計 96.75；若使用 HELLO15，優惠碼會扣除 13.50，稅金 5.74，總計 82.24。

卡片上的所有金額使用相同的小數位數：若每個金額都是整數（$50）則不顯示小數，否則顯示兩位（$96.75）。折扣以真正的負號開頭（−$30.00）。貨幣符號與分隔符號依 `Currency` 與 `CultureName` 而定；表中沒有符號的貨幣會顯示其代碼。

### 優惠碼

輸入的英文字母會在打字時轉為大寫（`UppercaseCoupon`）。按下 Apply，或在欄位中按 Enter，會將去除前後空白的文字送到 `CouponValidator`；空白欄位不會有任何動作。檢查進行時，Apply 會顯示轉圈圖示，並忽略按下動作。

- **接受**：欄位淡出，同時標籤塊放大淡入，該優惠碼的行滑入明細，稅金與總計隨之更新，焦點移到標籤塊的移除按鈕。會引發 `CouponApplied`。
- **拒絕**：區域左右晃動，欄位的框線變成玫瑰色，訊息顯示在欄位下方；焦點留在欄位中，輸入時錯誤會清除。`CouponRejected` 會帶著訊息。驗證器擲出的例外會被視為拒絕，並使用標籤中的一般訊息。
- **移除**：標籤塊淡出，回到空白的欄位並取得焦點，該行滑出，總計滾動回復。會引發 `CouponRemoved`。

`ApplyCouponAsync` 與 `RemoveCoupon` 可從程式碼執行相同的動作；`AppliedCoupon` 與 `CouponDiscount` 會告訴你目前套用了什麼。

### 付款

Pay 會引發 `PayRequested`（將 `Cancel` 設為 true 可停止），把 `PayState` 變為 `Processing`，並等待 `PayAsync`。完成後會進入 `Paid`，這是最終狀態：按鈕變綠、顯示勾號，並保持停用。擲出例外則進入 `Error`：按鈕下方會開啟錯誤行（`PayErrorMessage`，或標籤中的一般文字），按鈕上的文字變成「Try again」，按下後會開始新的嘗試。在處理中與付款完成後，優惠碼會被鎖定，以免收費與顯示的總計不一致。`PayStateChanged` 會回報每一次變更，若有例外也會一併帶上。

沒有 `PayAsync` 時，Pay 只會引發 `PayRequested` 並顯示 Processing；當你的付款表單或重新導向返回時，請自行設定 `PayState`，並呼叫 `ResetPayState` 重新開始。請使用預覽中的開關來查看失敗的情況。

<Demo name="Events" />

### 狀態

`PayState` 可以直接設定，下方的卡片就是這樣顯示處理中、已付款與付款失敗的狀態。第一張卡片還包含一個媒體格，且沒有優惠碼欄位。

<Demo name="States" />

### 窄版容器

卡片會放大到 440 px，並填滿較窄的容器。描述會在名稱下方換行，價格欄保持在一行，標籤塊則讓代碼與移除按鈕保持可見。

<Demo name="Narrow" />

### 其他文字、貨幣與動態效果

所有可見與可朗讀的文字都位於 `Labels`（一個 `CheckoutSummaryLabels`）中；帶有金額的項目是函式，會取得格式化後的值。此範例以德文數字規則格式化歐元，重新命名每一個標籤，省略稅率（稅金行會改為顯示「Added at the next step」，總計也不包含稅金），並以減少動態效果執行。

<Demo name="CustomLabels" />

### 內容指引

- 商品名稱要短到能放在一行；細節放在描述中。
- 以調整項的內容命名（「Annual plan」、「Referral credit」），並用備註說明原因（「Three months free」）。
- 拒絕訊息要說明下一步該做什麼（「Try HELLO15」），而不只是說優惠碼失敗。
- 付款按鈕的標籤只保留動詞與金額；按鈕本身已經表明這是安全的付款。

## API 參考

<!-- api: CheckoutSummary -->

<!-- api: CheckoutSummaryItem -->

<!-- api: CheckoutSummaryLine -->

<!-- api: CheckoutSummaryTax -->

<!-- api: CheckoutSummaryCouponResult -->

<!-- api: CheckoutSummaryLabels -->

<!-- api: CheckoutSummaryCouponEventArgs -->

<!-- api: CheckoutSummaryPayRequestedEventArgs -->

<!-- api: CheckoutSummaryPayStateChangedEventArgs -->

<!-- api: CheckoutSummarySamples -->

## 無障礙

- 卡片以其 `Title` 命名。優惠碼欄位名為「Promo code」；拒絕後，訊息會成為它的說明文字，錯誤行則以強制模式朗讀。
- 檢查代碼時，Apply 的名稱為「Checking code」。標籤塊的移除按鈕名為「Remove code HELLO15」。
- 優惠碼被接受後，焦點會移到移除按鈕，讓鍵盤使用者不會被丟回視窗頂端；移除優惠碼後，焦點會回到欄位。
- 付款按鈕是一個禮貌模式的即時區域，以其目前的標籤命名，因此「Processing」、「Paid」與「Try again」都會被朗讀。付款失敗的錯誤行會以強制模式朗讀。
- 滾動中的總計以完整金額命名（「Total $96.75」），因此螢幕閱讀器不會在數字滾動途中逐位朗讀。
- 鍵盤焦點環為 2 px，位於 Apply、移除按鈕與 Pay 外 2 px；欄位則改以較粗的框線表示焦點。指標按下時不繪製焦點環。

### 鍵盤

| 按鍵 | 動作 |
| --- | --- |
| Enter | 在優惠碼欄位中：套用優惠碼（空白欄位不會有動作）。 |
| Enter / Space | 在 Apply、移除按鈕或 Pay 上：按下該按鈕。移除後焦點會回到欄位。 |
| Tab / Shift+Tab | 沒有優惠碼時依序為欄位、Apply、Pay；有優惠碼時依序為移除按鈕、Pay。 |

## 動態效果

- **明細行**：出現或消失的行會以 0.4 s 的臨界阻尼彈簧調整高度，同時在 240 ms 內淡化（ease-out），因此文字在行完全展開前就能閱讀。下方的各行與卡片底部會跟著高度移動。
- **優惠碼接受**：欄位在 160 ms 內淡出；標籤塊從 0.97 放大，並以同樣的 0.4 s 彈簧淡入。
- **優惠碼移除**：標籤塊在 160 ms 內淡出；欄位從 0.98 放大，並在 240 ms 內以進入曲線淡入。
- **優惠碼拒絕**：區域左右晃動（0、−6、6、−4、3、0 px，共 360 ms）；錯誤行以彈簧展開，框線顏色在 160 ms 內變化。
- **總計**：每一位數字都是一個滾輪；所有滾輪朝金額變動的方向一起轉動，使用同一個彈簧，因此步數較多的滾輪轉得較快，但全部同時停下。字形會依其與中線的距離淡化。
- **付款按鈕標籤**：任何變化（新總計、Processing、Paid）都會向上切換：舊標籤上升 8 px 並在 160 ms 內淡出，新標籤從下方 8 px 處在 240 ms 內上升。Paid 會在 160 ms 內把顏色變為綠色。
- **按下**：Apply 縮放到 0.97，Pay 縮放到 0.98，時間為 160 ms；滑鼠指標移到 Pay 上時，它會稍微變暗。
- **轉圈圖示**每 700 ms 轉一圈。
- 減少動態效果：不晃動、不縮放；各行、錯誤行與標籤塊立即出現，總計直接跳到新值，標籤以 240 ms 交叉淡化，轉圈圖示每圈放慢到 1.6 s。關閉動態效果時，所有變化都立即完成。

## 響應式

- 卡片最寬為 440 px（`MaxWidth`），較窄的容器會填滿其寬度。
- 其內距依視窗寬度調整，與參考實作一致：視窗寬度的 5%，介於 20 與 28 px 之間（560 px 視窗以上為 28 px，手機上為 20 px）。
- 其他部分不會隨寬度改變：描述會換行，總計維持其大小，標籤塊中的「applied」文字會最先讓出空間。

## 相關元件

- `CartDrawer`，適用於含數量的多項商品與結帳按鈕。
- `BillingOverview` 與 `UsageBilling`，適用於既有訂閱。
- `AnimatedCounter` 與 `RollingDigits`，單獨用於滾動數字。
