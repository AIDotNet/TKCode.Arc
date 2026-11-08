---
title: 席位管理
summary: 團隊席位卡片，含頭像列與席位條（顯示成員、邀請與空位）、以步進器呈現今日按比例計費金額與新月總額的報價、收費前的確認步驟、可重新傳送或撤銷（附復原）的邀請，以及團隊已滿時提供再加一個席位的狀態。
source-hash: faa5dbe05cf4
---

<Demo name="Preview" />

## 何時使用

`SeatManager` 是團隊帳單頁面中的席位區段。它顯示已付費的席位數量與持有者，讓管理員在收費前就能看到價格影響並變更數量，同時把待接受的邀請（同樣佔用一個席位）保留在同一處。

- **依席位計費的訂閱。** 席位數量就是訂閱數量。步進器會顯示新增席位今天的費用（依計費週期剩餘天數按比例計算），以及續約時定期總額會變成多少。
- **席位用盡的當下給出限制。** 當所有已付費席位都被佔用時，提示列會說明此情況，並提供 "Add a seat"，因此剛嘗試邀請某人的管理員可以一鍵解決。
- **邀請會保留席位。** 每個待接受的邀請在被接受或撤銷之前都會佔用一個席位。撤銷會立即釋出席位，並有一段短暫的復原時間，因此可以馬上減少數量。

## 何時不用

- **多位成員的角色與權限。** 請使用像 `Team members` 這樣的成員表格，並連結到此卡片來管理席位。
- **用量或點數計費。** 當帳單依消耗量計算時，請使用 `Usage billing`、`Usage pricing` 或 `Credit top-up`。
- **在方案之間做選擇。** 請使用 `Plan comparison` 或 `Pricing calculator`；此卡片只在單一方案內運作。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。請提供方案、已購買的席位、成員、邀請以及實際的計費週期，讓按比例計費的結果與你的計費系統一致。然後把三個處理函式連接到你的後端。

```xml
<arc:SeatManager x:Name="Seats"
                 Workspace="Tidewater"
                 Purchased="12"
                 PeriodStart="2026-10-18"
                 PeriodEnd="2026-11-18"
                 ReceiptEmail="accounts@tidewater.example">
    <arc:SeatManager.Plan>
        <arc:SeatManagerPlan Name="Team" PricePerSeat="18" MaxSeats="25" />
    </arc:SeatManager.Plan>
    <arc:SeatManager.PaymentMethod>
        <arc:SeatManagerPaymentMethod Brand="Nova" Last4="1881" ProcessorName="Ledgerline" />
    </arc:SeatManager.PaymentMethod>
</arc:SeatManager>
```

```csharp
Seats.Members.Add(new SeatManagerMember { Id = "m1", Name = "Mira Okafor" });
Seats.Invites.Add(new SeatManagerInvite
{
    Id = "i1",
    Email = "theo.lindqvist@tidewater.example",
    InvitedBy = "Mira Okafor",
    Note = "3 days ago"
});

Seats.UpdateSeatsAsync = async (quote, cancellation) =>
{
    var response = await http.PostAsync("/api/billing/seats", JsonContent.Create(new { seats = quote.Target }), cancellation);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("The card was declined. Nothing was charged.");
    }
};
Seats.ResendInviteAsync = (invite, cancellation) => http.PostAsync($"/api/invites/{invite.Id}/resend", null, cancellation);
Seats.RevokeInviteAsync = (invite, cancellation) => http.DeleteAsync($"/api/invites/{invite.Id}", cancellation);
```

`SeatManagerSamples.Apply(block)` 會以本頁使用的虛構 Tidewater 團隊填入區塊。

### 結構

- **標題列**：標題、以徽章顯示的方案名稱，以及一行包含工作區、單一席位價格和續約日期的資訊。
- **總覽**：前四位成員以頭像顯示，其餘以 "+N" 表示；"12 of 12 seats in use"；每個席位一格的席位條；圖例（成員、已邀請、空位，以及右側正在新增或移除的席位）；以及提示列。
- **報價**：已購買的席位、步進器（減號、席位數、加號）、今日應付金額及其說明，以及從續約日起的定期總額，舊總額作為其說明。
- **待接受的邀請**：每個邀請一列，附重新傳送與撤銷按鈕；沒有邀請時顯示 "No pending invites"。
- **頁尾**：席位計費所用的卡片，或以文字說明的變更，以及 "Update seats" 按鈕。

### 席位條與提示

席位條以強調色繪製成員，以較淺的強調色階繪製已邀請的席位，空位為外框格；正在新增的席位在末端以強調色外框格顯示，正在移除的席位則以淡色外框表示。提示列永遠說明最有用的資訊：先是你剛碰到的上限，然後是正在新增的席位、正在移除的席位、還有多少空位，最後才是團隊已滿。

### 變更席位

步進器的上限由已佔用的席位（成員加上邀請）與方案的 `MaxSeats` 共同決定。超過任一上限時，按鈕仍可使用，並在提示列中說明限制 2.6 秒。移除席位只能減到已佔用的席位數為止，因此移除時一定是取走空位；今天不收費，並於續約時生效。

```csharp
// Today's charge for added seats: delta × price × days left ÷ days in period, rounded to cents on the total.
var quote = SeatManagerQuote.Build(current: 12, target: 15, plan, periodStart, periodEnd, DateTime.Today);
// quote.DueToday == 38.32m, quote.NextTotal == 270m for the Team plan at $18.
```

`SeatManagerQuote.Build` 與卡片使用的計算完全相同，因此伺服器可以核對數字。每當步進器變更報價時都會引發 `QuoteChanged`；如果你想在旁邊顯示自家計費系統的發票預覽，可以使用它。

### 確認、付款、完成

點擊 "Update seats" 會在頁尾開啟確認步驟：問題、"Back" 與 "Pay"（若只移除席位則為 "Confirm"）。在此期間步進器會被鎖定，按下 Escape 會返回。"Pay" 會執行 `UpdateSeatsAsync`，按鈕上顯示旋轉指示器，頁尾顯示閃爍的文字行。成功時，`Purchased` 會變成新的數量，`SeatsChanged` 會引發，頁尾並顯示 2.8 秒的成功訊息。若擲出例外，例外訊息會留在頁尾，並附上 "Try again"。

<Demo name="Backend" />

### 重新傳送與撤銷

重新傳送時，按鈕上會顯示旋轉指示器，然後變為勾號與 "Sent"。撤銷會立即釋出席位，並把該列變成一個在 `UndoDuration`（3 秒）內可用的復原列。只有在該時間視窗關閉後，`RevokeInviteAsync` 才會執行，該列隨之收合；如果處理函式擲出例外，邀請會回到原來的位置。

### 狀態

<Demo name="States" />

### 窄容器

<Demo name="Narrow" />

### 其他文字、貨幣與動態效果

所有文字都位於 `Labels`（`SeatManagerLabels` 類型）中；其 `Culture` 負責金額與日期的格式。方案的 `Currency` 與 `Interval` 決定金額，以及 "/mo" 或 "/yr" 後綴。沒有 `PaymentMethod` 時，頁尾不顯示卡片。

<Demo name="Custom" />

### 示範循環

未設定任何處理函式時，卡片會模擬其後端（收費 900 毫秒、重新傳送 660 毫秒），並播放一段 12.7 秒的循環：新增三個席位、付款、重新傳送第一個邀請、撤銷第二個邀請，然後回到起點。卡片離開螢幕時循環會暫停，對螢幕閱讀器保持靜默，並在卡片內第一次按下指標或鍵盤時永久停止。設定 `IsAutoPlayEnabled="False"` 可將其關閉，設定 `True` 則可在已設定處理函式時保留循環。

### 最佳做法

- 傳入實際的計費週期與「今天」（`Now`），使按比例計算的金額與帳單一致。
- 在 `UpdateSeatsAsync` 中擲出例外時，請使用寫給付款人的訊息：它會原樣顯示。
- 當 `Members` 只是頭像的範例時，請保持 `MemberCount` 準確。
- 讓卡片自行伸展；它最寬為 560 px，低於 460 px 時會收緊。

### 常見問題

- **為什麼不能低於已佔用的席位數？** 移除某人持有的席位會把他鎖在外面。請先撤銷邀請；席位會立即釋出。
- **邀請何時真正被撤銷？** 復原時間視窗關閉時。在此之前，不會有任何東西送到你的後端。
- **沒有處理函式時，卡片會收費嗎？** 不會。它會模擬收費，並向螢幕閱讀器說明這一點。

## API 參考

<!-- api: SeatManager -->

<!-- api: SeatManagerPlan -->

<!-- api: SeatManagerMember -->

<!-- api: SeatManagerInvite -->

<!-- api: SeatManagerPaymentMethod -->

<!-- api: SeatManagerQuote -->

<!-- api: SeatManagerPhase -->

<!-- api: SeatManagerLabels -->

## 無障礙

- 標題是第 2 級標題。頭像列是一張名為 "10 members" 的影像。
- 席位條是一張影像，帶有完整描述（"12 of 15 seats in use: 10 members, 2 invited, 3 open"）；圖例以文字重複數量，因此顏色不是唯一的訊號。
- 席位數量是一個範圍值，具有最小值、最大值與目前值，朗讀為 "15 seats"。
- 減號與加號在達到上限時仍可取得鍵盤焦點，並在提示列中說明原因，而不是靜默。
- 禮貌的即時區域會播報檢視、更新、重新傳送、撤銷、移除與復原。沒有處理函式時，它會加上 "Simulated, nothing was charged"（或已傳送）。示範循環不播報任何內容。
- 數字滾動時會公開其真實數值；數字的滾動條紋對螢幕閱讀器隱藏。
- 每個邀請的操作都有自己的名稱，例如 "Revoke invite to theo.lindqvist@tidewater.example"。
- **Tab** 依序移過 "Add a seat"（團隊已滿時）、減號、席位數、加號、每個邀請的操作，以及頁尾按鈕。沒有可更新的內容時，會跳過 "Update seats"。
- 在席位數上：**Arrow up** 或 **Arrow right** 新增一個席位，**Arrow down** 或 **Arrow left** 移除一個，**Page up** / **Page down** 以五為單位移動，**Home** 跳到已佔用的席位數，**End** 跳到方案的最大值。
- **Space** 或 **Enter** 按下目前焦點所在的按鈕；減號與加號在按住時會重複。
- **Escape** 離開確認步驟。
- 確認步驟開啟時，焦點移到 "Pay"；按下 "Back" 後回到 "Update seats"；更新成功後回到席位數。"Add a seat" 會把焦點交給加號；已撤銷邀請的 "Undo" 會接手撤銷按鈕的焦點，並把它交還回去。

## 動態效果

- **數字**會滾動：每一位數字是 0–9 的條紋，依各自的變化以 240 毫秒的緩出曲線同時滑動。新的位數（以及移動的 "$"）在 160 毫秒內淡入；寬度從不做動畫。
- **席位格**從零開始放大，並以無回彈的 0.24 秒彈簧縮回零，同時相鄰的格子讓出空間；僅改變類型的格子會在 160 毫秒內重新上色。
- **文字替換**（提示、說明、頁尾、邀請列、重新傳送 → Sent）以交叉淡變進行：舊文字在 100 毫秒內淡出，新文字在 160 毫秒內淡入並上移 2 px。
- **已撤銷的邀請**約在 120 毫秒內淡入，同時其列沿同一彈簧收合。
- 減少動態效果與關閉動態效果時，所有變化都立即套用；狀態、數字與循環保持不變。

## 響應式

- `SeatManager` 讀取自身的寬度。它最寬 560 px；低於 460 px（`:narrow`）時內距由 24 px 變為 20 px，金額旁的說明以及圖例中新增或移除的項目會隱藏。
- 地址、說明與頁尾文字行以省略號結尾；步進器保留其 120 px，金額永不換行。

## 相關元件

- `Usage billing` 與 `Billing overview`，即計費頁面其餘部分的設定區塊。
- `Credit top-up` 與 `Checkout summary`，其他帶有確認步驟的定價區塊。
- `Avatar group`、`Button` 與 `Text shimmer`，即此卡片所用的組件。
