---
title: Trial banner
summary: 試用狀態橫幅：圓環倒數剩餘天數，文案說明工作區將失去什麼，升級只需點擊一次；可作為細橫幅、側邊欄卡片或標題列膠囊。
source-hash: 42c8d885a742
---

<Demo name="Preview" />

## 何時使用

`TrialBanner` 告訴處於免費試用期的工作區還剩多久、試用結束時會失去什麼，以及如何保留。圓環倒數天數，文案說明會失去的內容（"SAML SSO and 12 seats"），「新增付款方式」只需點擊一次。在最後幾天它切換為警告色調並說出結束的那一天；試用結束後顯示鎖頭圖示和工作區失去的內容；付款方式登錄後顯示勾號。

- **提供免費試用的 SaaS 應用程式**，需要一條安靜且始終可見的升級途徑。
- **告訴工作區試用結束時會失去什麼**，並在最後幾天更積極地提醒。
- **顯示已結束的試用**，而不阻擋應用程式。

## 何時不用

- **阻擋某項功能的硬性付費牆**：使用對話方塊或升級頁面。
- **一般公告或版本說明**：使用一般橫幅或 Toast。
- **用量限制**（額度、席位）：使用用量儀表。

## 用法

傳入來自計費服務的剩餘天數或結束日期、方案、工作區、它將失去的內容，以及你的結帳流程。`DaysLeft` 與 `EndsAt` 都沒有時，沒有可顯示的試用：橫幅在空的圓環上方顯示 "No trial in progress"，沒有操作按鈕（即預覽中的 "No trial" 選項）。`Plan`、`Workspace`、`LossSummary` 與 `Features` 預設為空；套件不附帶範例試用。預覽傳入的是展示庫虛構的試用：Northwind 工作區的 Pro 試用，到期後將失去 SAML SSO 與 12 個席位。

```csharp
banner.DaysLeft = trial.DaysLeft;            // or: banner.EndsAt = trial.EndsAt;
banner.EndsAt = trial.EndsAt;                // for the date in the copy
banner.AddPaymentHandler = async cancellation =>
{
    // Open checkout or the billing portal; return once a payment method is on file, throw if it failed.
    await billing.AddPaymentMethodAsync(cancellation);
};
banner.ComparePlansRequested += (_, e) => { e.Handled = true; OpenPlansPage(); };
banner.Snoozed += (_, e) => settings.TrialBannerSnoozedUntil = e.Until;
banner.Dismissed += (_, _) => settings.TrialBannerDismissed = true;
```

### 天數與日期

設定了 `DaysLeft` 時以它為準：橫幅不會為天數讀取時鐘，因此顯示的正是計費服務給出的值（0 或更小即為已結束狀態）。將它保留為 null 並設定 `EndsAt`，橫幅會從今天起按本地日曆日計數，並在本地午夜重新計數。`TimeProvider` 決定「今天」從何而來（預設為系統時鐘）；測試中可傳入自己的實作。`EndsAt` 也提供文案中的日期；沒有它時，日期為今天加上剩餘天數。

`WarnAt`（3）表示最後幾天使用警告色調。`TrialDays`（14）是試用期的長度：圓環顯示其中剩餘的天數。

### 變體

<Demo name="Card" />

`Variant` 選擇形態。`Banner` 是放在應用程式頂端的細橫幅。`Card` 是側邊欄卡片，最寬 340px，列出工作區會失去的內容：在 `Features` 中傳入三到四個 `TrialFeature`（名稱、選用的次要說明和 14px 圖示）。`Pill` 是精簡的標題列膠囊，在彈出層中開啟卡片。

<Demo name="Pill" />

### 結帳

<Demo name="Checkout" />

`AddPaymentHandler` 開啟你的結帳流程。執行期間按鈕顯示載入指示，文案為 "Opening checkout…"；傳回後橫幅顯示已升級狀態並設定 `IsUpgraded`；擲回例外時，文案說明結帳未能開啟，按鈕變為 Try again。如果橫幅離開視窗，取消權杖會被觸發。沒有處理常式時，該呼叫為模擬（900ms）。

「比較方案」引發 `ComparePlansRequested`；在已升級狀態下，「管理帳單」引發 `ManageBillingRequested`。請設定 `Handled`；未處理的請求會在文案位置顯示一則簡短的模擬提示。

### 關閉與稍後提醒

關閉會引發 `Dismissed`；稍後提醒選單提供「直到明天」、「三天」或「直到警告開始」，並引發 `Snoozed`，附帶所選選項和 `Until`（那一天本地時間的開始）。請自行儲存這兩者，並在稍後提醒結束前不再顯示橫幅。在此期間，橫幅收合為試用膠囊（`IsCollapsed` 變為 true）；點擊膠囊會再次展開。`CollapseTo="None"` 則什麼也不顯示。`IsDismissible` 和 `IsSnoozable` 用於隱藏對應按鈕。

### 窄容器

<Demo name="Narrow" />

橫幅讀取自身寬度：小於 600px 時，操作按鈕移到文案下方，與文案左緣對齊，第二行文案可以佔兩行（兩行空間都會預留，因此不會跳動）。在 360px 寬度下也不會出現水平捲動。

### 自訂文字

`Labels` 包含橫幅顯示或朗讀的所有文字。帶有數值的行是格式化函式：

```csharp
banner.Labels = new TrialBannerLabels
{
    Days = days => days == 1 ? "1 Tag" : $"{days} Tage",
    ActiveTitle = (days, plan) => $"Noch {days} in Ihrer {plan}-Testphase",
    AddPayment = "Zahlungsmethode hinzufügen",
};
```

### 內容準則

- `LossSummary` 保持在幾個字以內（"SAML SSO and 12 seats"），讓橫幅保持一行。
- `Features` 保持三到四行：這些是付費的理由，而不是功能對照表。
- 寫明工作區正在試用的方案（`Plan`）和工作區名稱（`Workspace`）；兩者都會出現在文案中。

### 最佳實務

- 橫幅放在應用程式內容上方，卡片放在側邊欄底部，膠囊放在精簡的標題列中。
- 從計費服務傳入天數或結束日期（例如訂閱的試用結束時間）。
- 自行儲存關閉與稍後提醒，並在稍後提醒結束前不再顯示橫幅。

## API 參考

<!-- api: TrialBanner -->

<!-- api: TrialFeature -->

<!-- api: TrialSnoozedEventArgs -->

<!-- api: TrialBannerLabels -->

## 無障礙

- 橫幅和卡片是具名稱的區域（"Pro trial"）；圓環僅作裝飾，天數始終寫在文字中。
- 禮貌模式的即時區域會播報計費、稍後提醒和關閉的結果，並說明計費是否為模擬。
- 狀態從不只依賴顏色：警告色調同時會在文案中說出結束的那一天，已結束的試用顯示鎖頭圖示，已升級的顯示勾號。
- 關閉或稍後提醒後，焦點移到試用膠囊；再次展開橫幅時，焦點移到主要操作。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過比較方案、新增付款方式、稍後提醒和關閉 |
| Enter、Space | 執行取得焦點的操作、開啟稍後提醒選單或開啟膠囊的卡片 |
| 上方向鍵、下方向鍵 | 在稍後提醒選項之間移動 |
| Escape | 關閉稍後提醒選單或膠囊的彈出層 |

## 動態效果

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 天數變化 | 圓環的弧以無回彈的彈簧（0.24s）變到新長度 | 立即 | 立即 |
| 色調變化 | 圓環的顏色在 160ms 內跟隨 | 立即 | 立即 |
| 文案、天數與圖示變化 | 在同一格中交叉淡化一次：舊內容 100ms 淡出，新內容 160ms 淡入並上升 2px | 立即 | 立即 |
| 按鈕文字變化 | 寬度無回彈地變形 | 立即 | 立即 |
| 膠囊文字變化 | 寬度以彈簧變到測得的文字寬度 | 立即 | 立即 |
| 關閉、稍後提醒、重新展開 | 表面在橫幅與膠囊之間以彈簧過渡（0.33s，無回彈）；兩種形態交叉淡化 | 立即 | 立即 |

橫幅閒置時不執行任何計時器或迴圈：只有模擬的計費呼叫和簡短的模擬提示使用逾時。

## 相關元件

- `ContextWindow`：顯示預算已用了多少。
- `AgentStatus`：即時狀態膠囊。
