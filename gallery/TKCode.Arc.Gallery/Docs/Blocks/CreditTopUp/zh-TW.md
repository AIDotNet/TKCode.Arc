---
title: Credit top-up
summary: 用於購買用量點數的卡片：顯示餘額、兩週用量圖表與剩餘天數，附贈送點數標示的預設方案包，搭配即時價格的自訂金額滑桿，設有門檻的自動儲值，已儲存的付款卡片，以及以里程表式滾動呈現的新餘額。
source-hash: b89d9ae3ae0d
---

<Demo name="Preview" />

## 何時使用

`CreditTopUp` 在一張卡片中販售預付點數（AI tokens、API 呼叫、算圖分鐘數）。它回答使用者打開帳務頁面時的三個問題：還剩多少、能用多久，以及補充一次要花多少錢。餘額旁邊是兩週的用量與剩餘天數的估算；三個方案包與一個滑桿決定金額；自動儲值與已儲存的卡片就放在結帳按鈕的正上方，因此一按就能購買，新餘額也會以里程表式滾動呈現。

- **帳務頁面的點數區段**，使用者也在這裡管理自動儲值與付款卡片。
- **「點數不足」對話框或抽屜。** 消耗速度與一鍵補充應該放在一起，因為工作已經停擺。
- **含用量贈送的預付方案。** 每個價格、標示與總額都來自同一個 `CreditTopUpPricing`，因此你販售的贈送層級就是卡片所顯示的內容。

## 何時不用

- **訂閱變更**，涉及席次、方案與計費週期：請使用方案比較或結帳區塊。
- **輸入新的卡號。** 卡片只顯示已儲存的卡片；請使用付款服務商自己的表單取得新卡，再把已儲存的卡片傳入。
- **唯讀的用量報表。** 請使用 `Usage billing` 或圖表；此卡片是為購買而設計的。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。不設定任何屬性時，它會顯示一個範例工作區（1,260 點數），並模擬 1.2 秒的扣款，讓你在接線前就能先試用。在應用程式中，請雙向繫結餘額、每日用量與已儲存的卡片，並提供一個透過你的後端扣款的 `PurchaseHandler`：

```xml
<arc:CreditTopUp Balance="{Binding Credits}"
                 Usage="{Binding DailyUsage}"
                 PaymentMethods="{Binding Cards}"
                 IsAutoTopUpEnabled="{Binding AutoTopUp}"
                 AutoTopUpThreshold="{Binding AutoTopUpThreshold}" />
```

```csharp
public interface IBilling
{
    Task BuyCreditsAsync(CreditTopUpOrder order, CancellationToken cancellation); // throws with a short message on failure
    void SaveAutoTopUp(bool enabled, long threshold, long credits);
    void SetDefaultCard(string cardId);
}

// order: Credits, Bonus, Total, Price, Currency, PaymentMethodId.
card.PurchaseHandler = (order, cancellation) => billing.BuyCreditsAsync(order, cancellation);
card.AutoTopUpChanged += (_, e) => billing.SaveAutoTopUp(e.Settings.IsEnabled, e.Settings.Threshold, e.Settings.Credits);
card.PaymentMethodChanged += (_, e) => billing.SetDefaultCard(e.PaymentMethod.Id);
```

伺服器端以相同的運算下單：`CreditTopUp.Quote(credits, pricing)` 會回傳點數、贈送點數、總額與價格，與卡片所顯示的完全一致。

### 組成

- **餘額**：一個小標題，附健康狀態標示（琥珀色警告圖示的「Low balance」，或綠色勾號的「On track」），以大號等寬數字顯示餘額，後接「credits」，並顯示以目前每日平均用量計算的剩餘天數。
- **用量圖表**：每天一根長條，最後一根（今天）使用強調色。指向某一天會把它的長條變為文字色、淡化其他長條，並在圖表下方讀出該日數值（「Nov 14: 529」、「Today: 362」）。
- **儲值**：標題與費率（「$8 per 1,000」）、三個方案包（金額、價格，以及適用時的綠色贈送標示），以及附滾動讀數的自訂金額滑桿。
- **自動補充**：一個開關，以及以膠囊選擇呈現的門檻。自動儲值關閉時，此列仍保留位置，但會變暗且無法操作。
- **已儲存的卡片**：小型卡組標誌、品牌與末四碼、有效期限，以及一個「Change」按鈕，會依序切換已儲存的卡片。
- **結帳**：含贈送點數在內的總點數，其下為贈送明細，右側為價格，另有購買按鈕與一行狀態文字。

### 金額、方案包與贈送點數

只有一個金額，即 `SelectedCredits`（雙向）。方案包只是某個數值的捷徑：按下方案包會把滑桿移到該值，把滑桿拖到某個方案包的數值上則會選取該方案包。選取環會在方案包之間滑動；當金額從其他地方落到某個方案包上，或離開所有方案包時，選取環會在原地淡入淡出。金額會依 `Range` 的步進值保持在範圍內（預設為 500 到 30,000，每 500 一階）；PageUp 與 PageDown 每次移動範圍的十分之一（3,000）。

贈送點數來自不高於該金額的最高層級（`CreditTopUpBonusTier`，例如金額為 6,000 時為 +10%）。低於所有層級時，總額下方的那一行會說明贈送從哪裡開始。每 1,000 點數的費率是單純的單價：贈送只會增加點數，不會改變價格。

### 餘額、剩餘天數與狀態

剩餘天數那一行，會以 `Usage` 中**完整**天數的平均值去除餘額，並無條件捨去：今天仍在進行中，因此不計入平均值，「4.8 days」會讀為 4。只要餘額低於 `AutoTopUpThreshold`，`Health` 就會是低餘額，即使自動儲值已關閉也一樣；改選另一個門檻會立即重新判定。若將 `Usage` 留空，則會隱藏圖表與剩餘天數那一行。

### 購買

訂單（金額、贈送點數、總額、價格與卡片）在按下按鈕時即固定。扣款進行中，按鈕只顯示載入動畫，並忽略後續的按壓，但保留鍵盤焦點；狀態列會說明正在扣款的卡片。方案包、滑桿與設定仍可使用；變更會套用到下一次購買。處理程式完成後，餘額會滾動增加總額，剩餘天數與健康狀態標示也隨之更新，按鈕顯示「Added」與勾號，狀態列則確認已加入的點數。2.4 秒後，兩者都會回到原本的狀態。`Purchased` 會回報訂單與新餘額；`PurchaseAsync()` 執行的與按鈕相同。

沒有 `PurchaseHandler` 時，卡片會模擬扣款，狀態列會說明並未實際扣款。從視窗移除卡片時，會透過處理程式的權杖取消進行中的扣款。

### 失敗與重試

當處理程式擲出例外時，狀態列會以危險色顯示該例外的訊息，並帶有警示圖示；按鈕顯示「Retry」，`PurchaseFailed` 會回報訂單與訊息。卡片保持原有尺寸。按下 Retry 會以目前的選取內容再次扣款；若先變更金額或卡片，按鈕會回到靜止狀態。

<Demo name="Declined" />

### 餘額自行變動

餘額由你掌控：當用量消耗餘額時，設定 `Balance`，數字就會滾動到新值；購買仍會使餘額向上滾動。滾動進行途中若又出現變動，會從每個數位目前所在的位置重新規劃，因此快速更新時，數字絕不會只顯示一半。

<Demo name="Draining" />

### 窄版容器

卡片寬度最多為 480，並讀取自身的寬度。寬度為 448 或以下時，圖表會移到餘額下方並橫向撐開，標籤位於右側；寬度為 416 或以下時，所有左右內距會從 24 收緊為 16。三個方案包永遠保持在同一列。

<Demo name="Narrow" />

### 貨幣、文化與文字

價格使用 `Pricing` 的 ISO 貨幣，並依 `CultureName` 書寫金額的方式排列（de-DE 下為「48,00 €」），所有計數都使用該文化的分隔符號。每一段可見文字與無障礙名稱都位於 `CreditTopUpLabels` 中；句子以 `{0}`、`{1}` 接收格式化後的數字，而在數字會滾動的地方（總額、贈送那一行），`{0}` 周圍的文字保持靜止。

<Demo name="Euro" />

## API 參考

<!-- api: CreditTopUp -->

<!-- api: CreditTopUpPricing -->

<!-- api: CreditTopUpRange -->

<!-- api: CreditTopUpPaymentMethod -->

<!-- api: CreditTopUpLabels -->

<!-- api: CreditTopUpPhase -->

<!-- api: CreditTopUpHealth -->

<!-- api: CreditTopUpCardBrand -->

## 無障礙

- **Tab** 依序移動經過選取的方案包（未選取時為第一個）、滑桿、自動補充開關、選取的門檻（僅在自動補充開啟時）、Change 與購買按鈕。圖表不是 Tab 停駐點。
- **方向鍵**在方案包與門檻之間移動並選取，到末端時循環；**Home** 與 **End** 會跳到第一個與最後一個。
- 在滑桿上，**Left** 與 **Right** 每次移動 500，**PageUp** 與 **PageDown** 每次移動 3,000，**Home** 與 **End** 移到兩端。
- **Space** 切換開關；**Enter** 或 **Space** 按下 Change 與購買按鈕。
- 卡片是一個名為「Buy credits」的區域（`CardName`）。
- 方案包與門檻會以單選按鈕的形式朗讀：「6,000 credits, $48, 10% bonus」。滑桿以其標題命名，並將數值朗讀為「6,000 credits」。開關以自動補充的標題命名。
- 圖表是一張影像，附帶語音摘要（「Daily usage over the past 14 days, averaging 440 credits a day.」）；其下的讀數是給指標使用者看的，不會被朗讀。
- 滾動中的數字以最終值朗讀，絕不逐位朗讀。
- 狀態列是禮貌模式的即時區域：扣款中、結果、錯誤與設定通知都會被宣布。
- 健康狀態以圖示加文字呈現，絕不只用顏色。
- 鍵盤焦點會在焦點元素外顯示 2px 的環；指標焦點則不顯示。

## 動態效果

- **方案包、門檻與滑桿拇指**以快速、無回彈的彈簧滑動（約 0.25 秒）。當選取出現或消失時，方案包的選取環會在原地淡入（170 毫秒）。
- **結帳數字**以 Arc 標準曲線（.22, 1, .36, 1）在 240 毫秒內逐位滾動：數值增加時向上，減少時向下。
- **餘額**以相同曲線在 900 毫秒內像里程表一樣滾動：最高一個變動的位數走最短路徑，其下每一位多繞一圈，因此看起來像計數器。新增的最高位不會滾動：它們會以快速彈簧撐開寬度，並在 100 毫秒內淡入。
- **文字變化**（健康狀態、剩餘天數、圖表讀數、卡片、狀態、按鈕標籤）會交叉淡化：舊文字在 100 毫秒內淡出，新文字在 160 毫秒內淡入，並上移 2 px。
- **指向某一天**會在 160 毫秒內把其他長條淡化至 55%。按下方案包會在 120 毫秒內縮放至 0.98。
- 減少動態效果時，數字、環、膠囊與拇指會立即就位，只保留短暫的淡入淡出。關閉動態效果時，所有變化都會立即套用。

## 響應式

- 卡片最寬為 480，低於該寬度時會跟隨其容器；斷點使用卡片自身的寬度，絕不使用視窗的寬度。
- 寬度為 448 或以下（`:narrow`）時，圖表會移到餘額下方，橫向撐開，長條高度縮為 32。
- 寬度為 416 或以下（`:compact`）時，左右內距由 24 收緊為 16。
- 狀態列以省略號縮短而非換行，且卡片在每個購買狀態下都保持相同高度。

## 相關

- `Usage billing` 與 `Billing overview`，其他帳務區塊。
- `Slider`、`Switch` 與 `Button`，組成此卡片的控制項。
- 同一群組中的 `Plan comparison` 與 `Pricing calculator`。
