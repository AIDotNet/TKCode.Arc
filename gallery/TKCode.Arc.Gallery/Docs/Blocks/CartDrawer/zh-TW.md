---
title: Cart drawer
summary: 商品網格，商品會飛入購物車按鈕，數字會滾動增加，側邊的購物車抽屜內含數量步進器、免運費進度條、滾動的小計與結帳。
source-hash: 46bf8925baa1
---

<Demo name="Preview" />

## 何時使用

`CartDrawer` 是單一區塊上的小型商店：一個商品網格、一個帶有數量的購物車按鈕，以及從區塊側邊滑入的購物車。加入商品時，該商品圖片的副本會沿著弧線飛入購物車按鈕；落地時，數量會滾動增加一，按鈕也會彈跳一下。抽屜讓人在付款前確認所選的商品：數量、通往免運費的進度條、優惠碼、總計，以及會變成確認畫面的結帳按鈕。

- **商品列表**：人們加入多件商品，並在結帳前一起檢視。
- **設有免運費門檻的商店**：進度條及其上方的句子會說明還差多少；只要小計達到門檻，運費那一列就會顯示「Free」。
- **結帳前的優惠碼**：已接受的優惠碼會變成綠色的標籤塊，並有一行折扣滑入總計中。
- **顯示商品飛到哪裡**：飛入動畫把卡片上的按下動作與角落的數量連接起來，這是 toast 做不到的。

## 何時不用

- **完整結帳**：包含地址、配送與付款步驟時，請在購物車之後使用多步驟表單（`MultiStepForm`）。
- **只需要訂單摘要**及其總計：請使用結帳摘要區塊。
- **不是購物車的側邊面板**：請使用 `Drawer`，它會覆蓋整個視窗，並可放入任何內容。
- **數百件商品**：網格沒有虛擬化。請每個區塊只顯示一頁商品，或把商品放進你自己的列表，並從中呼叫 `Add`。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。商品是 `CartDrawerProduct`，屬於 `AvaloniaObject`，具有名稱、選填的詳細資訊行、價格、圖片，以及 `MaxQuantity`（預設為 9）。可以在 XAML 中填入 `Products`，或繫結 `ProductsSource`；帶有 `InitialQuantity` 的商品會以該數量放進購物車。金額為 `decimal`，單位由 `Currency`（ISO 4217）指定，依 `Labels.Culture` 格式化，小數位數由 `AmountDecimals` 決定（預設為 0，即整數單位）。

```xml
<arc:CartDrawer Title="Fresh from the workshop"
                Height="760"
                FreeShippingThreshold="240"
                ShippingFee="9">
    <arc:CartDrawerProduct Name="Fluted water jug" Detail="Recycled glass, 0.9 L" Price="58" InitialQuantity="1" />
    <arc:CartDrawerProduct Name="Speckled mugs" Detail="Pair, reactive glaze" Price="42" />
</arc:CartDrawer>
```

`CartDrawerSamples` 保存預覽中虛構的商店：`CreateProducts()`（六件附有繪製圖片的商品）、`CreateLabels()`（預設文字加上優惠碼提示）、優惠碼檢查與訂單櫃檯。

### 組成

- **標題列**：左側是 `Title`；右側是購物車按鈕，包含購物袋圖示、「Cart」與數量徽章。徽章在商品飛入落地時才計入，而不是在按下按鈕時計入。
- **網格**：三欄卡片（寬度為 640 px 及以下時為兩欄）。每張卡片有 4:5 的圖片、名稱與價格、詳細資訊行，以及一個全寬的「Add to cart」按鈕；按下後 1.4 秒內會顯示「Added」。
- **抽屜**：在區塊內以遮罩覆蓋展開，寬 400 px，與區塊邊緣相距 8 px。它包含一個標題列（顯示滾動的商品數量與關閉按鈕）、一張免運費卡片、各個列，以及一個頁尾，內有優惠碼、總計與結帳按鈕。
- **列**：縮圖、名稱、詳細資訊、數量步進器（數量為 1 時，減號是垃圾桶）、該列的價格，以及 ×。
- **收據**：結帳後會出現一個帶勾號的圓盤、感謝語、一句說明，以及 Items、Paid 與 Arrives 等列；頁尾有「Continue shopping」。

### 購物車

`Lines` 依加入順序列出各列，`ItemCount`、`Subtotal`、`Discount`、`Shipping` 與 `Total` 會隨每次變更而更新。當折扣前的小計達到 `FreeShippingThreshold` 時，運費即為免費，因此優惠碼不會取消免運費。折扣為小計乘以優惠碼的百分比，並四捨五入到 `AmountDecimals` 位小數，遇到正好一半時朝遠離零的方向進位（$64 打 10% 折扣為 $6，$186 折扣為 $19）。將門檻設為 0 即可隱藏免運費卡片，並讓所有訂單免運費。

`Add`、`SetQuantity`、`Remove` 與 `Clear` 可從程式碼變更購物車；`Open` 與 `Close`（或雙向的 `IsOpen`）可移動抽屜。`ItemAdded`、`QuantityChanged` 與 `ItemRemoved` 會回報每次變更，內容包括商品，以及舊數量與新數量。列的數量不能超過商品的 `MaxQuantity`：步進器的加號與卡片按鈕會停用於上限；`Add` 會回傳 false，且不會有飛入動畫或朗讀通知。

### 移除列

× 與步進器的垃圾桶會立即移除該列。使用指標時，也可以將列向左滑：滑動時，列後方會露出紅色底層，上面有「Remove」；超過 110 px 後，紅色會加深一階；放開時若超過該點（或快速輕彈），該列便會滑出。較短的滑動會彈回。無論哪種方式，該列都會淡出並收合，使下方的列往上移。只有大致水平的拖曳才會開始左滑，因此垂直拖曳仍會捲動列表。將 `IsSwipeToRemoveEnabled` 設為 false，即可只保留按鈕。

### 優惠碼

輸入優惠碼後按 Apply 或 Enter。`ValidatePromoAsync` 會檢查它：回傳 `CartDrawerPromo` 即可套用，回傳 null 則拒絕。被拒絕的優惠碼會使欄位抖動、邊框變紅，並以「SPRING doesn't match an offer」取代提示文字；下一次編輯會清除錯誤。已接受的優惠碼會變成一個標籤塊，上面有優惠碼、百分比與 Remove 按鈕，並在總計中展開一行 Discount。`ApplyPromoAsync` 與 `RemovePromo` 可從程式碼執行相同的操作；`PromoApplied`、`PromoRejected` 與 `PromoRemoved` 會回報這些事件。`IsPromoVisible="False"` 會隱藏欄位。

```csharp
store.ValidatePromoAsync = async (code, cancellation) =>
{
    var offer = await offers.FindAsync(code, cancellation);
    return offer is null ? null : new CartDrawerPromo(offer.Code, offer.PercentOff);
};
```

### 結帳

結帳會以 `CartDrawerOrder`（包含各列、優惠碼與所有總計）呼叫 `PlaceOrderAsync`，並顯示它回傳的收據。執行期間，`State` 為 `Placing`：按鈕會顯示載入動畫與「Placing order」並保持焦點，各列與優惠碼欄位會被鎖定，抽屜也無法關閉。之後，按鈕會變成確認圓盤，勾號自行描繪，收據隨之出現。若工作擲出例外，抽屜會回到購物車，內容不變。關閉收據時，無論是按下 Continue shopping 還是其他方式，都會開始一個新的空購物車。`OrderPlaced` 會攜帶訂單與收據。

```csharp
store.PlaceOrderAsync = async (order, cancellation) =>
{
    var confirmation = await checkout.PayAsync(order.Lines, order.Total, cancellation);
    return new CartDrawerReceipt($"Thanks, {confirmation.FirstName}", confirmation.Summary,
        order.ItemCount, order.Total, confirmation.DeliveryWindow);
};
```

### 你自己的商店

每段文字都放在 `CartDrawerLabels` 中；包含數字的句子則是函式，會接收已格式化的金額或數量。這家茶飲店使用英鎊與便士，有自己的優惠碼檢查與訂單櫃檯，並把此區塊的事件寫入記錄。

<Demo name="OwnStore" />

### 窄版容器

區塊讀取的是自己的寬度，而不是視窗的寬度。寬度為 640 px 或以下時，網格為兩欄，內距較緊密。寬度為 380 px 或以下時，購物車按鈕只保留購物袋與數量，每張卡片會把價格放在名稱下方，抽屜則填滿區塊，與邊緣相距 4 px，並使用較小的縮圖。

<Demo name="Narrow" />

### 減少動態效果

啟用減少動態效果時，不會有任何飛入動畫，徽章會立即計數；抽屜會原地淡入，而非滑入；列不能左滑（× 仍可移除）；數字變化時也不會滾動。文字、焦點與通知維持不變。

<Demo name="ReducedMotion" />

## API 參考

<!-- api: CartDrawer -->

<!-- api: CartDrawerProduct -->

<!-- api: CartDrawerLine -->

<!-- api: CartDrawerPromo -->

<!-- api: CartDrawerOrder -->

<!-- api: CartDrawerReceipt -->

<!-- api: CartDrawerState -->

<!-- api: CartDrawerItemEventArgs -->

<!-- api: CartDrawerPromoEventArgs -->

<!-- api: CartDrawerOrderPlacedEventArgs -->

## 無障礙

- 購物車按鈕以數量命名（「Cart, 2 items」）；飛入落地時名稱會改變。**Enter** 或 **Space** 可開啟抽屜。
- 抽屜開啟時，焦點會移到其關閉按鈕。**Tab** 與 **Shift+Tab** 會在抽屜內循環，永遠不會跳到後方的商店，而且商店也不接受指標輸入。
- **Escape**、關閉按鈕，以及在遮罩上按下，都會關閉抽屜，焦點會回到購物車按鈕。訂單送出期間，這些操作都不會生效。
- 每張卡片的按鈕命名為「Add Fluted water jug to cart」。步進器是一個群組，名稱為「Fluted water jug quantity」；其按鈕命名為「Decrease …」或「Remove …」（數量為 1 時），以及「Increase …」；×為「Remove …」。滑動的替代操作即是這些按鈕。
- 某列被移除後，焦點會移到下一列的相同按鈕（若沒有下一列，則移到上一列；購物車為空時則移到「Browse products」）。套用優惠碼後，焦點會移到標籤塊的 Remove；移除優惠碼後，則回到欄位。在收據上，焦點會移到「Continue shopping」。
- 滾動數字的無障礙名稱即為其數值。運費進度條命名為「Progress to free shipping」，已達到的金額為其說明文字。
- 禮貌型即時區域會朗讀新增的商品、移除的列、套用或移除的優惠碼，以及已送出的訂單（「Order placed. 2 items, $134.」）。數量變更與被拒絕的優惠碼不會朗讀：欄位會被標示為無效，錯誤訊息則取代提示文字。
- 訂單送出期間，結帳按鈕保持焦點，並以「Placing order」作為名稱。購物車為空時，按鈕會停用並變暗。

### 鍵盤

| 按鍵 | 位置 | 動作 |
|---|---|---|
| Enter, Space | 購物車按鈕 | 開啟抽屜 |
| Tab, Shift+Tab | 抽屜 | 在抽屜的控制項之間移動（在抽屜內循環） |
| Escape | 抽屜 | 關閉抽屜（送出訂單期間不可用） |
| Enter | 優惠碼欄位 | 套用優惠碼 |
| Enter, Space | 任何按鈕 | 按下該按鈕 |

## 動態效果

- **飛入購物車**：單一進度值以 `cubic-bezier(.3, 0, .2, 1)` 執行 780 ms。商品圖片的副本沿著二次貝茲曲線，從圖片中心飛到購物袋中心；控制點位於水平 55% 的位置，並比較高的那一端高出 30 px。飛行到 80% 時縮小為 26 × 26；過半時向上傾斜至 −10°；最後 14% 淡出；陰影則依 sin(πp) 抬起。卡片上的圖片保持原位。多個飛行動畫可同時進行。
- **落地**：徽章的數字以平滑彈簧向上滾動（數量減少時則向下），並隨距離淡化；按鈕在約 430 ms 內依 1 → 1.09 → 0.98 → 1 彈跳，同時一圈 1 px 的強調色環擴大到 1.45，並從 0.45 開始淡出。
- **加入按鈕**：標籤以 100 ms 淡出、240 ms 淡入並上移 2 px 的方式切換為「Added」，寬度則依平滑彈簧變化；1.4 秒後恢復。
- **抽屜**：以平滑彈簧從寬度的 104% 處滑入，以收斂彈簧滑出；遮罩以 280 ms 淡入。
- **數字**：數量、列價格、總計與結帳金額都會依平滑彈簧逐位滾動。免運費進度條依平滑彈簧增長，並在 480 ms 內轉為綠色；卡車圖示則在 240 ms 內轉為綠色。免運費句子與運費數值的切換方式是：舊文字在 160 ms 內上移 5.6 px 並淡出，同時新文字在 240 ms 內升入定位。
- **列**：被移除的列在 160 ms 內淡出，高度在 77 ms 後依平滑彈簧收合。被左滑的列會以放開時的速度同時滑出；較短的滑動則依收斂彈簧彈回。
- **優惠碼**：被拒絕的優惠碼會使欄位抖動（0、−6、5、−3、2、0 px，共 380 ms）。被接受的優惠碼會把欄位交叉淡化為標籤塊，標籤塊從 0.96 開始放大；頁尾高度與 Discount 列則依平滑彈簧跟隨變化。
- **結帳**：標籤切換為載入動畫與「Placing order」。成功時，按鈕的邊界與圓角會以彈簧變成收據上方 64 px 的圓盤（視覺時長 0.6 秒，無彈跳）；勾號在 400 ms 後的 250 ms 內自行描繪；購物車在 150 ms 內淡出；收據在 130 ms 後淡入。
- **減少動態效果**：不會有飛入動畫，徽章立即計數；沒有彈跳與漣漪；抽屜以 180 ms 淡入而非滑入；列不能左滑；數字與進度條變化時不使用彈簧；切換時保留 120 ms 的交叉淡化。**關閉**則會立即套用所有變化。

## 響應式行為

- 響應式斷點讀取的是區塊寬度：640 px 或以下（`:compact`），以及 380 px 或以下（`:narrow`）。
- 請為區塊設定高度（或將它放入會限制高度的容器中）。商店會在區塊內捲動，抽屜的高度等於區塊減去其內距，列則在固定的標題列與頁尾之間捲動。
- 抽屜寬 400 px；若區塊窄於此寬度，抽屜會填滿區塊寬度減去內距。
- 懸停只會改變顏色。所有操作都可以輕觸完成；滑動可用觸控、觸控筆或滑鼠完成。

## 相關

- `Drawer`：視窗層級的側邊面板。
- 結帳摘要區塊：包含優惠碼與付款按鈕的訂單摘要。
- `AnimatedCounter`：單一滾動數字。
- `Button`、`Input` 與 `Progress`：抽屜各部分所依循的控制項。
