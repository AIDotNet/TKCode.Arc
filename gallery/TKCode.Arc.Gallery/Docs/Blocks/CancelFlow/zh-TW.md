---
title: 取消訂閱流程
summary: 貼心的取消流程：詢問原因、提供合適的挽留方案、說明哪些內容將結束、按住確認，並讓人能快速恢復。
source-hash: e5d8f00ecc9e
---

<Demo name="Flow" />

## 何時使用

- 帳單或方案頁面上的取消按鈕：離開應讓人稍作考慮，但絕不能讓人覺得被困住。
- 訂閱中，優惠、暫停或一次溝通，往往比離開更貼合使用者的原因。
- 任何不可逆的方案變更，都需要清楚說明哪些內容將於何時結束，並請使用者明確確認。

## 何時不用

- 刪除單一項目或單一席位：請在原位使用 `ConfirmMorph` 或 `HoldToConfirm`。
- 降級至另一個付費方案：請用 `RadioCards` 或定價表並排呈現各方案。
- 法律或條款要求一鍵取消的情況：不要提供挽留方案（不設定任何挽留方案），或直接連結至條款所描述的確認頁面。

## 用法

`CancelFlow` 是一張卡片。第一步詢問原因，以 `RadioGroup` 列出各個原因；選定原因後，清單下方會展開一個選填的回饋欄位。按下「繼續」會顯示該原因的 `Offer`（若沒有，則顯示 `DefaultOffer`）。之後在挽留方案頁面操作，或在沒有挽留方案時直接進入確認步驟：確認步驟會列出 `Losses`（即將結束的內容），並請使用者按住一個按鈕。按住到底會進入已取消步驟，並引發 `CancellationConfirmed`；接受挽留方案則進入已保留步驟，並引發 `OfferAccepted`。

預覽為兩個原因設定了各自的挽留方案（「Take 40% off for 3 months」和「Pause instead」），其餘原因則回退至預設挽留方案。`AccessEndsOn`（付費週期的結束日期）和 `PlanName`（方案名稱）用於填入句子。

```csharp
flow.AccessEndsOn = new DateTime(2026, 11, 1);

flow.CancellationConfirmed += (_, e) => billing.Cancel(e.Reason.Title, e.Feedback);
flow.OfferAccepted += (_, e) => billing.Apply(e.Offer.Title);
flow.KeepPlanRequested += (_, _) => page.CloseCancelFlow();
```

### 沒有挽留方案時

當選定的原因與 `DefaultOffer` 都沒有挽留方案時，流程會從原因直接進入確認步驟，步驟列由三段變為兩段。若 `Reasons` 留空，流程會使用 `Labels.DefaultReasons` 中的五個原因。`SelectedReason` 與 `Feedback` 為雙向繫結；本範例的第一個原因會追問一個問題，因此回饋欄位會顯示「What were you missing?」。

<Demo name="NoOffer" />

### 在對話框中

當流程位於 `Dialog` 中時，將 `Appearance` 設為 `Plain`，即可去除卡片的邊框、陰影與內距。「保留我的方案」和「完成」都會引發 `KeepPlanRequested`，對話框會因此關閉。「重新啟用方案」會引發 `ReactivateRequested`，然後重新開始流程；將該事件標記為已處理，即可讓已取消步驟保留在畫面上。沒有 `Icon` 的挽留方案會顯示禮物圖示。

<Demo name="InDialog" />

### 文字與其他語言

`CancelFlowLabels` 包含流程中的所有內建文字。若要改寫或翻譯，請整體取代該物件；日期依 `Labels.Culture` 格式化。

```csharp
flow.Labels = new CancelFlowLabels
{
    Culture = CultureInfo.GetCultureInfo("nl-NL"),
    ReasonTitle = "Waarom zeg je op?",
    Continue = "Doorgaan",
    KeepPlan = "Abonnement houden",
    HoldToCancel = "Houd vast om op te zeggen",
};
```

## API 參考

<!-- api: CancelFlow -->

`CancelFlowReason`

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Title` | `string` | 人們會說出口的原因，例如「Too expensive」。 |
| `Description` | `string?` | 標題下方的一行簡短說明。 |
| `Offer` | `CancelFlowOffer?` | 選擇該原因後顯示的挽留方案；為 null 時回退至 `DefaultOffer`。 |
| `AsksForDetails` | `bool` | 為 true 時，回饋欄位會詢問 `DetailsPrompt`，而不是一般問題。回答仍為選填。 |
| `DetailsPrompt` | `string?` | 追問的問題，例如「What were you missing?」；為 null 時使用 `Labels.DetailsPrompt`。 |

`CancelFlowOffer`

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Title` | `string` | 提供的內容，例如「Take 40% off for 3 months」。 |
| `Description` | `string?` | 該挽留方案對方案與帳單代表什麼。 |
| `AcceptLabel` | `string?` | 接受按鈕的文字，例如「Claim 40% off」；為 null 時使用 `Labels.AcceptOffer`。 |
| `Icon` | `Geometry?` | 標題上方圖塊中的 24×24 描邊圖示；為 null 時顯示禮物圖示。 |
| `AcceptedTitle` | `string?` | 已保留步驟的標題，例如「40% off is on」；為 null 時使用 `Labels.RetainedTitle`。 |
| `AcceptedDescription` | `string?` | 標題下方的句子；為 null 時使用 `Labels.RetainedDescription`。 |

`CancelFlowLabels`（皆為 `init`；函式會依方案名稱與格式化後的日期組成句子）

| 屬性 | 預設值 |
|---|---|
| `Culture` | en-US |
| `StepCounter` | "Step {n} of {total}" |
| `Back` | "Back" |
| `ReasonTitle` / `ReasonDescription` | "Why are you canceling?" / "Your answer helps us improve {plan}." |
| `FeedbackLabel` / `DetailsPrompt` / `FeedbackPlaceholder` | "Anything else we should know?" / "Tell us a little more" / "Optional" |
| `KeepPlan` / `Continue` | "Keep my plan" / "Continue" |
| `AcceptOffer` / `ContinueCanceling` | "Accept offer" / "Continue canceling" |
| `ConfirmTitle` / `AccessUntil` / `AfterThat` | "Cancel your {plan} plan?" / "You keep access until {date}." / "After that:" |
| `LossesName` / `HoldToCancel` / `HoldConfirmed` | "What ends" / "Hold to cancel plan" / "Canceled" |
| `CanceledTitle` / `CanceledDescription` | "Your plan is canceled" / "You can keep using {plan} until {date}. We sent a confirmation to your email." |
| `Reactivate` | "Reactivate plan" |
| `RetainedTitle` / `RetainedDescription` / `Done` | "{offer} is on" / "Thanks for staying. You'll see the change on your next invoice." / "Done" |
| `DefaultReasons` | Too expensive; Not using it enough; Missing a feature I need; Switching to another tool; Something else |

## 無障礙

- 每次步驟切換後，焦點會移至新步驟的標題，標題會以禮貌方式朗讀。標題可接收焦點，但不是 Tab 停駐點，因此 Tab 會直接前往該步驟的第一個控制項。
- 原因清單是以步驟標題命名的單選群組。方向鍵可在各原因之間移動，整個群組只佔一個 Tab 停駐點。
- Enter 會執行步驟的主要動作：選定原因後為「繼續」，挽留方案頁面為接受按鈕，完成步驟為「完成」。尚未選定原因時，Enter 會先選取該原因；在回饋欄位中，Enter 會換行（Ctrl+Enter 或 Cmd+Enter 則繼續）。從上一步帶過來、仍按住的 Enter 會被忽略。
- Alt+Left 可從挽留方案與確認步驟返回。圓形的返回按鈕名稱為「返回」，並帶有工具提示。
- 取消需要刻意按住：按住按鈕，或使其獲得焦點後按住 Space 或 Enter。說明文字會提到這一點。
- 收合的回饋欄位在選定原因前不在 Tab 順序中。

## 動態效果

- 步驟會以 12px 的距離沿行進方向滑動（前進時從右側進入，後退時從左側進入），並伴隨短暫的淡入淡出；卡片高度會以彈簧跟隨新步驟變化。
- 回饋欄位以高度彈簧展開與收合。步驟列的分段會交叉淡入至新顏色，返回按鈕則在其所佔空間收合時淡入淡出。
- 在最後一步，勾號會在約半秒內自行繪製出來。
- 減少動態效果時只保留淡入淡出：步驟在原位交叉淡入，分段顏色快速變化，勾號立即繪製。關閉動態效果時，所有變化都會在原位立即完成。

## 相關元件

- 其他設定區塊：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`BillingOverview`（其中的「取消方案」按鈕是進入此流程的自然途徑）以及 `UsageBilling`。
- 本流程由 `RadioGroup`、`Textarea`、`HoldToConfirm`、`Button` 與 `Dialog` 組成。
- `ConfirmMorph`：用於較小的破壞性操作，提供較輕量的原位確認。
