---
title: Live checkout
summary: 訂閱結帳頁面，方案、計費週期、附加項目和資料每次變動時摘要都會重新計算，付款後移到中央並展開成收據。
source-hash: 4e5adf668697
---

<Demo name="Preview" />

## 何時使用

`LiveCheckout` 是用於付費註冊或訂閱升級的單頁結帳。左側的表單一次問完所有內容：快速錢包、方案與計費週期、席位數、附加項目、帳單資料和信用卡。右側的訂單摘要在顧客仍在考慮時就回答「我要付多少？」：每次變動都會重新計算各列、優惠碼、帳單國家的稅以及總計，每個金額都會逐位數滾動到新的值。付款成功後，表單退到一旁，摘要滑到中央並變成收據。

- **以席位計價產品的自助註冊。** 團隊選擇方案、設定使用人數並付款，不必聯絡業務。
- **從免費方案升級。** 以顧客點選的方案和他們已在使用的席位開啟結帳；他們只需加上一張信用卡。
- **含選用附加項目與地區稅的方案。** 附加項目以摘要中的列出現和離開，稅這一列會跟著國家變化（VAT、GST、銷售稅或不收稅）。

## 何時不用

- **有購物車和運送的實體商品。** 請使用購物車（Cart drawer）以及專為地址、配送選項和運費設計的結帳。
- **只需要總計，不需要付款。** 付款在別處進行、你只需要一個帶促銷碼的摘要時，請使用 Checkout summary。
- **在方案之間做選擇。** 在顧客做出決定之前，先用 Plan comparison 或 Pricing calculator 並排比較方案，再以選定的方案開啟結帳。
- **需要自行儲存的信用卡資料。** 這裡的信用卡欄位是一般文字方塊，適合預覽或沙箱環境。在正式環境中，請改放付款服務商託管的信用卡欄位，或讓快速錢包負責信用卡。

## 用法

本區塊衍生自 Avalonia 的 `TemplatedControl`。不做任何設定時，它會顯示範例目錄（三個方案、三個附加項目、六個國家、三個虛構的錢包），並模擬優惠碼檢查和付款，因此你可以直接把它放到頁面上試用。若要做真正的結帳，請提供你的目錄和兩個處理常式。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LiveCheckout x:Name="Checkout"
                      Currency="EUR"
                      SelectedPlanId="studio"
                      Seats="3"
                      Billing="Yearly"
                      CountryCode="FR" />
</UserControl>
```

```csharp
Checkout.Plans =
[
    new LiveCheckoutPlan { Id = "sketch", Name = "Sketch", MonthlySeatPrice = 9m, Blurb = "Boards and comments" },
    new LiveCheckoutPlan { Id = "studio", Name = "Studio", MonthlySeatPrice = 19m, Blurb = "Shared libraries" },
];
Checkout.Addons = [new LiveCheckoutAddon { Id = "review", Name = "Client review", MonthlyPrice = 29m }];

// Your server checks the code and charges the order.
Checkout.ValidateCouponAsync = (code, ct) => api.CheckCouponAsync(code, ct);
Checkout.PayAsync = async (order, ct) =>
{
    var charge = await api.ChargeAsync(order, ct);          // throw to show a message above Pay
    return new LiveCheckoutReceipt { OrderNumber = charge.Number };
};
Checkout.Paid += (_, e) => analytics.Track("checkout_paid", e.Order.Totals.Total);
```

<Demo name="Handlers" />

### 結構

- **快速錢包**：一列最多三個按鈕（44 px，使用錢包自己的顏色，而不是主題的顏色），接著是一行 "Or pay with card"。`Wallets` 清單為空時，會隱藏這一列和那一行。
- **方案**：標題帶有計費切換（Monthly / Yearly，年繳折扣以綠色顯示），接著每個方案一張卡片，顯示名稱、每席位每月的價格（週期變動時會滾動）和一行簡短說明。選取的卡片帶有一圈外框，會從一張卡片滑到另一張。
- **席位**：一張卡片，數字方塊兩側是 − / + 步進按鈕。
- **附加項目**：一份核取方塊列的清單，每列有名稱、一行說明和每月價格。
- **帳單資料**：Email、Name on card、Country（一個 `Select`）、Address、City 和 Postal code。有效的欄位會在末端顯示綠色勾號；無效的欄位會在下方顯示訊息。
- **信用卡**：標題帶有 "Use test card"（設定 `TestCard` 時顯示），接著是 Card number、Expiry 和 CVC。辨識出卡號時，卡片網路的名稱會在其標籤末端淡入。
- **付款**：一個 50 px 的按鈕，顯示 "Pay" 和總計；發生付款錯誤時錯誤會顯示在按鈕上方，按鈕下方是一行安全說明（`LiveCheckoutLabels.SecureNote`；留空即隱藏）。
- **訂單摘要**：一張卡片，方案一列（"8 seats × $22.40 × 12 months"），每個勾選的附加項目各一列，接著是優惠碼欄位（Add coupon、代碼欄位，或已套用的優惠碼）、Subtotal、選用的折扣列、稅這一列和總計。
- **收據**：付款後的同一張卡片，帶有勾號徽章、標題、一句關於方案的說明、各列和總額，並在虛線分隔線下方顯示訂單編號、付款方式、日期和收據電子郵件，接著是 "Receipt PDF" 和 "Start over"。

### 數字的計算方式

所有金額都是以 `Currency` 主要單位表示的 `decimal`，並以 `Labels.Culture` 的文化特性格式化。

| 值 | 月繳 | 年繳（`YearlyDiscount` d，預設 0.2） |
| --- | --- | --- |
| 方案卡片上的席位價格 | `MonthlySeatPrice` | `MonthlySeatPrice × (1 − d)`，四捨五入到分 |
| 方案列 | `seats × MonthlySeatPrice` | `seats × seat price × 12` |
| 附加項目列 | `MonthlyPrice` | `MonthlyPrice × 12 × (1 − d)` |

接著：**小計**是各列的總和；**折扣**是小計乘以優惠碼的百分比，四捨五入到分；**稅**是 `(subtotal − discount) × TaxRate`（該國家的稅率），四捨五入到分（稅率為 0 時顯示 "Not charged"）；**總計**是 `subtotal − discount + tax`。以範例資料而言，在荷蘭以年繳購買 8 個席位的 Crew 並加上 Priority care，金額為 2,150.40 + 432.00 = 2,582.40，再加上 21% VAT 542.30，因此今天應付 3,124.70。

稅這一列以國家及其稅率命名，例如 "VAT 21%" 或 "GST 5%"，並去掉尾端的零（"8.1%"）。席位數不會改變附加項目的價格。`Totals` 和 `Lines` 永遠保存螢幕上顯示的內容，`TotalsChanged` 會回報每一次變動。

請把這些數字當成預覽。扣款之前，請在你的伺服器上依 `LiveCheckoutOrder` 中的 id 重新計算訂單，而不是依其中的金額。

### 付款前的檢查

欄位在被編輯並離開後才會檢查，之後每次按鍵都會檢查；沒有輸入就離開的欄位不會被標記。Pay（或在任何欄位中按 Enter）會一次檢查所有欄位、顯示所有訊息，並依表單順序把焦點移到第一個需要處理的欄位。所有欄位都通過之前不會扣款。

| 欄位 | 規則 |
| --- | --- |
| Email | 必填；格式要像 `name@company.com`。 |
| Name on card | 至少兩個字元。 |
| Address | 至少三個字元。 |
| City | 必填。 |
| Postal code | 必填；必須完整符合該國家的 `PostalPattern`，否則顯示其 `PostalHint`。變更國家時，已編輯過的郵遞區號會立即重新檢查。 |
| Card number | 必填；輸入時自動分組（4-4-4-4，15 位數的卡片為 4-6-5）；長度須符合其卡片網路；須通過 Luhn 檢查碼驗證。 |
| Expiry | MM/YY。第一位數字為 2 到 9 時會變成 "0N/"；斜線會自動加上；月份必須是 01–12，且不能是過去的月份（本月可以）。 |
| CVC | 三位數字，15 位數的卡片為四位數字。 |

卡片網路都是虛構的，依開頭的數字辨識：Novacard（4）、Meridian（51–55、2221–2720）和 Atlas（34、37；十五位數字和四位數安全碼）。可用 `LiveCheckoutLabels.BrandName` 重新命名。

### 優惠碼

"Add coupon" 會變成一個代碼欄位和一個 Apply 按鈕（欄位會取得焦點，並把輸入的內容轉成大寫）。按 Enter 或 Apply 會呼叫 `ValidateCouponAsync`，期間 Apply 會顯示載入指示器；按 Escape 會關閉欄位。被拒絕的代碼會在欄位下方顯示結果的 `Message`（或 `LiveCheckoutLabels.CouponNotFound`）。被接受的代碼會變成一個標籤塊，顯示代碼和 "15% off"，一列 "Discount SPRING15" 會滑入總額區，焦點移到標籤塊的移除按鈕。移除標籤塊會讓 "Add coupon" 回來，總額也會滾動回原值。沒有處理常式時，範例會在 480 ms 後接受 SPRING15（15%）和 CREW10（10%）。擲回例外狀況的處理常式視為拒絕，並顯示例外狀況的訊息。

### 付款

`PayAsync` 會收到一個 `LiveCheckoutOrder`，其中包含方案、席位、週期、附加項目、優惠碼、帳單資料、付款方式、各列和總額。執行期間 `IsProcessing` 為 true：付款按鈕顯示 "Charging card…" 和載入指示器，表單會停用。傳回 `LiveCheckoutReceipt`（或 null）表示成功；你留為 null 的部分會自動填入（六位隨機數字作為訂單編號、時鐘的日期、"Novacard ending 1111"、電子郵件欄位）。擲回例外狀況表示失敗：例外狀況的訊息會顯示在付款按鈕上方，直到下一次編輯為止，並引發 `PaymentFailed`。

快速錢包會略過欄位檢查，並以 `PaymentMethod` 設為 `Wallet` 和錢包的 id 呼叫 `PayAsync`；載入指示器會顯示在該錢包按鈕上，而不是付款按鈕上。沒有處理常式時，本區塊會模擬付款：信用卡 1.5 s，錢包 1.1 s，而卡號 `4000 0000 0000 9995`（`LiveCheckoutSamples.DeclinedCardNumber`）會被拒絕。

成功之後 `IsPaid` 為 true，`Receipt` 保存收據顯示的內容，並引發 `Paid`。"Receipt PDF" 會引發 `ReceiptRequested`（在那裡產生或下載文件），其標籤會變成 "Downloaded"。"Start over" 或 `Reset()` 會把每個值恢復成本區塊第一次顯示時的樣子，並回到表單。`SubmitAsync()` 的作用與付款按鈕相同，並傳回付款是否成功；`FillTestCard()` 的作用與 "Use test card" 相同。

### 其他文字、貨幣和國家

每段可見的文字和每個無障礙名稱都是 `LiveCheckoutLabels` 的屬性，預設為英文；含數字的句子是函式（`PlanDetail`、`ReceiptSentence`、`CouponNotFound`）。它的 `Culture` 用來格式化金額和收據日期。`Currency` 可設為任何 ISO 4217 代碼；在 `Countries` 中為每個國家提供稅的名稱、稅率和郵遞區號規則。

<Demo name="Labels" />

### 內容指南

- 方案名稱保持一個單字，名稱下方的說明保持一個簡短片語；這樣在任何寬度下三個方案都能放進一列。
- 附加項目的說明要寫出效益（"Answers within four hours"），而不是功能清單。
- 郵遞區號提示要寫成正確格式的範例（"Use the A1A 1A1 format"），而不是規則。
- 付款錯誤應說明下一步該做什麼（"Try a different card"），而不是付款服務商傳回的代碼。

### 最佳做法

- 以顧客已在別處選好的方案和席位開啟結帳；表單已經夠長了，不必再重複一次決定。
- 讓 `Currency` 和各國家的稅規則與你的伺服器實際收費保持一致，這樣預覽就不會與發票不符。
- 只在有幫助時才在沙箱環境中使用 `TestCard` 和快速錢包；在正式環境中請把 `TestCard` 設為 null。
- 在小螢幕上把本區塊放進捲動檢視。表單以兩欄捲動時，摘要會停留在檢視頂端；收據出現時會被捲動到可見範圍內。

### 常見問題

**為什麼收據不顯示優惠碼欄位？** 已付款的訂單不能再使用優惠碼；收據出現的那一格畫面中，優惠碼欄位會離開摘要，而已套用的折扣仍保留為一列。

**我可以保留自己的表單欄位嗎？** 本區塊是完整的流程。若需要不同的欄位組合，請自行建立表單，並在旁邊使用 Checkout summary。

**本區塊會把任何資料傳送到別處嗎？** 不會。本區塊之外的一切都透過 `ValidateCouponAsync` 和 `PayAsync` 進行；沒有它們時只會模擬。

## API 參考

<!-- api: LiveCheckout -->

<!-- api: LiveCheckoutPlan -->

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Id` | `string` | 穩定的 Id，與 `SelectedPlanId` 比對。 |
| `Name` | `string` | 卡片和摘要中顯示的方案名稱。 |
| `MonthlySeatPrice` | `decimal` | 一個席位一個月的價格。 |
| `Blurb` | `string?` | 價格下方的一行簡短說明；為 null 時隱藏。 |

<!-- api: LiveCheckoutAddon -->

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Id` | `string` | 穩定的 Id，選取時列在 `SelectedAddonIds` 中。 |
| `Name` | `string` | 附加項目名稱。 |
| `Description` | `string?` | 名稱下方的一行簡短說明。 |
| `MonthlyPrice` | `decimal` | 一個月的固定價格。 |

<!-- api: LiveCheckoutCountry -->

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Code` | `string` | ISO 3166 代碼，與 `CountryCode` 比對。 |
| `Name` | `string` | 國家清單中顯示的名稱。 |
| `TaxRate` | `decimal` | 0 到 1（21% 寫作 0.21）；為 0 時顯示 "Not charged"。 |
| `TaxLabel` | `string?` | 稅名（"VAT"、"GST"）；為空時使用 `TaxFallback`。 |
| `PostalPattern` | `string?` | 郵遞區號必須完整符合的模式；為 null 時接受任何值。 |
| `PostalHint` | `string?` | 郵遞區號不符合時顯示的訊息。 |

<!-- api: LiveCheckoutWallet -->

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Id` / `Name` | `string` | Id 回報在 `WalletId` 中；名稱用作無障礙名稱並顯示在收據上。 |
| `MarkText` / `Glyph` | `string? / Geometry?` | 字標文字，以及 24 單位方框內的小標誌。 |
| `IsGlyphFilled` / `IsMarkItalic` | `bool` | 填滿而不是描邊標誌；字標使用斜體。 |
| `Background` / `Foreground` / `BorderColor` | `Color` | 淺色主題下按鈕自己的顏色。 |
| `DarkBackground` / `DarkForeground` | `Color?` | 深色主題下的顏色；為 null 時沿用淺色值。 |

<!-- api: LiveCheckoutTestCard -->

<!-- api: LiveCheckoutCouponResult -->

| 屬性 | 型別 | 說明 |
|---|---|---|
| `IsValid` | `bool` | 代碼被接受時為 true。 |
| `Code` | `string` | 顯示的代碼。 |
| `Percent` | `decimal` | 折扣百分比（0–100）。 |
| `Message` | `string?` | 拒絕的原因；為 null 時使用 `CouponNotFound`。 |

<!-- api: LiveCheckoutOrder -->

| 屬性 | 型別 | 說明 |
|---|---|---|
| `PlanId`, `Seats`, `Billing`, `AddonIds` |  | 所選內容。 |
| `CouponCode`, `CouponPercent` | `string?, decimal` | 已套用的優惠碼。 |
| `CountryCode`, `Email`, `CardholderName`, `Address`, `City`, `PostalCode` | `string` | 帳單資訊。 |
| `PaymentMethod`, `WalletId` |  | 卡片，或按下的錢包。 |
| `CardNumber`, `Expiry`, `Cvc`, `CardBrand` |  | 卡片資訊（僅卡片付款）。 |
| `Lines`, `Totals`, `Currency` |  | 顯示過的列與金額。 |

<!-- api: LiveCheckoutReceipt -->

| 屬性 | 型別 | 說明 |
|---|---|---|
| `OrderNumber` | `string?` | 為 null 時顯示六位隨機數字。 |
| `PaidAt` | `DateTimeOffset?` | 為 null 時使用 `Clock`。 |
| `PaymentText` | `string?` | 為 null 時產生 "Novacard ending 1111" 或錢包名稱。 |
| `Email` | `string?` | 為 null 時使用電子郵件欄位。 |

<!-- api: LiveCheckoutLine -->

<!-- api: LiveCheckoutTotalsChangedEventArgs -->

<!-- api: LiveCheckoutCouponEventArgs -->

<!-- api: LiveCheckoutPaidEventArgs -->

<!-- api: LiveCheckoutPaymentFailedEventArgs -->

<!-- api: LiveCheckoutLabels -->

## 無障礙

- 方案和計費週期是選項按鈕群組。每個附加項目都是真正的核取方塊。席位方塊、每個欄位和每個按鈕都有來自 `LiveCheckoutLabels` 的名稱；有錯誤的欄位以其訊息作為描述。
- 每個金額都以完整文字朗讀（"$3,124.70"），絕不逐位數朗讀；滾動的數字條對螢幕閱讀器隱藏。
- 處理中狀態、套用或移除優惠碼，以及 "Downloaded" 會以禮貌方式播報；付款錯誤會立即播報。
- 檢查失敗時，焦點會移到第一個需要處理的欄位。付款後焦點移到收據標題，讓螢幕閱讀器從收據開始朗讀。套用優惠碼後焦點移到標籤塊的移除按鈕，按 Escape 後回到 "Add coupon"，按 "Start over" 後移到第一個錢包（或選取的方案）：焦點永遠不會遺失。
- 焦點外框只在使用鍵盤時顯示：2 px，在按鈕和方案卡片外側 2 px。文字欄位則改以較深的邊框標示焦點。

### 鍵盤

| 按鍵 | 動作 |
| --- | --- |
| Tab / Shift+Tab | 依序為錢包、Monthly、Yearly、選取的方案、席位步進器、附加項目、帳單欄位、"Use test card"、信用卡欄位、Pay，然後是優惠碼欄位。 |
| 方向鍵 | 在方案上：移到並選取下一個或上一個方案，到兩端時循環。在計費切換上：切換週期。 |
| Up / Down | 在席位方塊中：增加或減少一個席位（按住 Shift 時為十個）。這是我們額外加入的。 |
| Enter | 在表單欄位中：付款（通過檢查後）。在代碼欄位中：套用優惠碼。 |
| Escape | 在代碼欄位中：關閉欄位並回到 "Add coupon"。 |
| Space | 切換取得焦點的附加項目；按下取得焦點的按鈕。 |

## 動態效果

- **金額**逐位數滾動：每一位數字從舊數字直接跑到新數字（不繞圈），使用約四分之一秒的臨界阻尼彈簧；所有位數同時開始。新增的最高位數會立即出現，金額的寬度也隨之改變。
- **方案外框和計費膠囊**以平滑的臨界阻尼彈簧（約 0.4 s）滑到新的選項。
- **摘要列**、折扣列、欄位訊息和付款錯誤在淡入淡出的同時展開和收合高度，使用同樣的平滑彈簧。
- **有效勾號**從 0.6 倍縮放彈出；附加項目的勾號會自行繪出；付款標籤和 "Downloaded" 標籤以從模糊中短距離上升的方式切換。
- **收據**：摘要從原位置滑到本區塊的中央（只有平移；新尺寸立即套用），其背景和陰影在 240 ms 內變化，收據標題從 75 ms 起在 240 ms 內淡入並上升 8 px，總額下方的明細從 176 ms 起作為一個整體上升 12 px，徽章的勾號從 320 ms 起在 470 ms 內繪出。同一時間，表單以退場曲線在 240 ms 內淡出並向左移動 24 px。在單欄版面中，則改為展開收合的摘要（高度 480 ms，淡入 240 ms）。如果頁面已捲動，會先把收據捲動到可見範圍內，讓滑動成為一個連續的動作。
- **Start over** 會立即讓表單回來，並以同樣的彈簧把摘要滑回它的欄位。
- 減少動態效果和關閉時，所有變動都會立即完成：金額、各列、收據和表單。付款所需的時間相同，焦點的移動方式也相同。

<Demo name="ReducedMotion" />

## 響應式行為

- 本區塊讀取自身的寬度，而不是視窗的寬度。從 760 px 起（低於此寬度為 `:narrow`），表單和 380 px 寬的摘要並排，間距 32 px，表單捲動時摘要停留在捲動檢視的頂端。
- 低於 760 px 時，摘要移到表單上方，並收合成一條顯示 "Show order summary" 和總計的橫條；點按橫條即可展開。收據佔滿整個寬度。
- 低於 520 px（`:stacked`）時，錢包、方案和帳單欄位堆疊成一欄；Expiry 和 CVC 仍然並排，每張方案卡片把價格放在右側。
- 滑鼠懸停只會改變色調；在觸控螢幕上沒有任何功能依賴它。

<Demo name="Narrow" />

## 相關

- Checkout summary，用於在你自己的表單旁邊放一個帶促銷碼的摘要。
- Plan comparison 和 Pricing calculator，用於在結帳前選擇方案。
- Cart drawer，用於實體商品。
- `Input`、`Select` 和 `Checkbox`，構成這些欄位的控制項。
