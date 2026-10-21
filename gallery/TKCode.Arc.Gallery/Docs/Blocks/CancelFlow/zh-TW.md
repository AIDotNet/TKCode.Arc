---
title: 取消訂閱流程
summary: 貼心的取消流程：詢問原因、提供合適的挽留方案、說明哪些內容將結束、按住確認，並讓人能快速恢復。
source-hash: 925b43783b23
---

<Demo name="Flow" />

## 何時使用

`CancelFlow` 是使用者點選「取消方案」時開啟的卡片。它位於帳單或方案頁面上，或位於由該頁面開啟的對話方塊中，將一次取消拆分為四個簡短的步驟：原因、與該原因相符的挽留方案、哪些內容在何時結束，以及使用者必須按住才能完成的確認。每一步具體做什麼由宿主決定；流程本身只負責收集答案並回報結果。

- **團隊方案頁面上的取消操作**：管理員在 Team 方案上點選「取消方案」，流程會詢問原因，團隊由此得知是價格、使用量還是缺少某項功能促成了這個決定。
- **挽留方案往往比離開更合適的訂閱**：針對價格抱怨的折扣，或針對使用量低的暫停服務。挽留方案只會在它所回應的原因之後出現，因此不會打擾已經拿定主意的人。
- **需要清楚摘要的不可逆方案變更**：在使用者確認之前，確認步驟會列出將停止運作的內容（唯讀專案、已刪除的歷史記錄、失效的自訂網域）以及存取權限結束的日期。
- **必須提供回頭路的帳號設定**：取消確認之後，已取消步驟會提供「重新啟用方案」，無須聯絡支援人員即可輕鬆撤銷。
- **需要記錄的流程**：事件會攜帶所選原因與自由文字回饋，因此答案可以送到你的分析系統或支援佇列。

## 何時不用

- **原地刪除一個項目或一個席位**：在該項目旁使用 `ConfirmMorph` 或 `HoldToConfirm`。對於一個使用者可以復原的小改動而言，完整流程太重了。
- **改為另一個付費方案**：用 `RadioCards` 或價格表並排展示選項。此流程用於離開，降級不應被當作取消來呈現。
- **法律或服務條款要求一鍵取消的情境**：將 `DefaultOffer` 保持為 null，且不要為原因設定 `Offer`，讓挽留方案不會出現；或直接連結到條款中描述的確認頁面。
- **對小型破壞性操作的輕量確認**：用 `ConfirmMorph`，它會在原處詢問一次，不需要原因或挽留方案。
- **收集冗長的自由文字問卷**：在你自己的表單中使用 `Textarea`。此流程在原因之後只提出一個選用問題，不會將問卷分頁。

## 用法

```csharp
flow.AccessEndsOn = new DateTime(2026, 11, 1);

flow.CancellationConfirmed += (_, e) => billing.Cancel(e.Reason.Title, e.Feedback);
flow.OfferAccepted += (_, e) => billing.Apply(e.Offer.Title);
flow.KeepPlanRequested += (_, _) => page.CloseCancelFlow();
```

`CancelFlow` 衍生自 Avalonia 的 `TemplatedControl`。它是一張卡片，包含步驟列、步驟內容與按鈕，並在各步驟之間切換內容。每個步驟的控制項只建構一次並重複使用。此流程由 `RadioGroup`、`Textarea`、`HoldToConfirm` 與 `Button` 組成，因此繼承了它們的焦點規則與鍵盤行為。預覽中有兩個原因帶有各自的挽留方案（"Take 40% off for 3 months" 與 "Pause instead"），其餘原因則退回到預設挽留方案。`PlanName` 與 `AccessEndsOn` 用於填入句子。

### 結構

卡片的內距為 28 px（寬度小於 440 px，即 `NarrowWidth` 時為 20 px 與 22 px），首選寬度為 520 px。從上到下依序為：

1. **步驟列**（`StepStorePart`）：路徑 Reason、Offer、Confirm 的三段；如果路徑沒有挽留方案，則只有兩段。各段高 4 px，間距 6 px。目前段使用強調色，已完成的段使用前景色的 30% 透明度，其餘段使用淡色邊框顏色。步驟計數「第 {n} 步，共 {total} 步」位於返回按鈕的右側。
2. **返回按鈕**（`BackButtonPart`）：32 px 的圓形按鈕，名稱為「返回」。它出現在挽留方案與確認步驟。Alt+Left 的效果相同。
3. **步驟標題與說明**：標題行高 28 px，說明位於標題下方 6 px 處。每次步驟切換後標題會取得焦點，但它不是 Tab 停駐點。
4. **原因清單**：一個 `RadioGroup`，每一列包含標題與選用的說明。其下方，選擇原因之後，回饋 `Textarea` 會透過高度彈簧動畫展開。此欄位是選用的，收合時不在 Tab 順序中。
5. **挽留方案卡片**：52 px 的圓角卡片（圓角半徑 16 px），包含 24 px 圖示、挽留方案標題及其說明。接受按鈕與「繼續取消」緊接其後。
6. **損失框**：內距為 16 px（左右）與 14 px（上下）、圓角 16 px 的面板，列出每一項損失，列與列之間間距 10 px。`Losses` 為空時隱藏。
7. **長按按鈕**：危險色的 `HoldToConfirm`，標籤為「按住以取消方案」，旁邊是「保留我的方案」。
8. **結果標記**：56 px 的標記，內含 28 px 的勾選圖形，以及結果標題與其句子，在已取消與已保留步驟中顯示。文字寬度限制為 380 px。

操作列位於內容下方 28 px 處。寬度小於 440 px 時，按鈕會縱向堆疊，次要按鈕移到主要按鈕的下方。

### 選擇原因與挽留方案

以使用者自己的說法來撰寫原因，並且只有在存在具體替代方案時，才為原因提供挽留方案。沒有挽留方案的原因會直接進入確認步驟，因此與原因不相符的挽留方案會讓流程看起來像是在推銷。`DefaultOffer` 用於通用的替代方案，例如與你的團隊通話，請將它保留給那些沒有具體對應答案的原因。

當後續問題能讓回饋更有價值時，在原因上設定 `AsksForDetails`，例如針對缺少某項功能，問「你缺少什麼？」。該回答仍然是選用的，並且只有在選擇該原因之後，這個問題才會取代一般提示。

### 沒有挽留方案

當所選原因與 `DefaultOffer` 都沒有挽留方案時，流程會從原因直接進入確認步驟，步驟列只有兩段而不是三段。`Reasons` 留空時，流程使用 `Labels.DefaultReasons` 中的五個原因。下面的示範從一個會追問的原因開始，因此欄位會顯示「你缺少什麼？」。

<Demo name="NoOffer" />

### 在對話方塊中

當流程位於 `Dialog` 中時，將 `Appearance` 設為 `Plain`，即可去掉卡片的邊框、陰影與內距。對話方塊會在 `KeepPlanRequested` 時關閉，「保留我的方案」與「完成」都會觸發該事件。「重新啟用方案」會觸發 `ReactivateRequested`，然後讓流程重新開始；將該事件標記為已處理，即可讓已取消的步驟保持在螢幕上。沒有 `Icon` 的挽留方案會顯示一個禮物圖示。

<Demo name="InDialog" />

### 繫結到檢視模型

流程保存答案，業務規則則由檢視模型保存。在使用 CommunityToolkit.Mvvm 的 MVVM 應用程式中，繫結原因、損失與方案資訊，並在檢視模型中處理結果。帳單服務是應用程式自己的預留實作。

```csharp
public partial class CancelPlanViewModel : ObservableObject
{
    private readonly IBillingService _billing;

    public CancelPlanViewModel(IBillingService billing)
    {
        _billing = billing;
    }

    [ObservableProperty]
    private string? _planName = "Team";

    [ObservableProperty]
    private DateTime? _accessEndsOn = new DateTime(2026, 11, 1);

    [ObservableProperty]
    private CancelFlowReason? _selectedReason;

    [ObservableProperty]
    private string? _feedback;

    public ObservableCollection<CancelFlowReason> Reasons { get; } = new();

    public ObservableCollection<string> Losses { get; } = new();

    [RelayCommand]
    private async Task CancelConfirmedAsync(CancelFlowCancelEventArgs e)
    {
        await _billing.CancelAtPeriodEndAsync(e.Reason.Title, e.Feedback);
    }
}
```

```xml
<arc:CancelFlow PlanName="{Binding PlanName}"
                AccessEndsOn="{Binding AccessEndsOn}"
                Reasons="{Binding Reasons}"
                Losses="{Binding Losses}"
                SelectedReason="{Binding SelectedReason, Mode=TwoWay}"
                Feedback="{Binding Feedback, Mode=TwoWay}" />
```

`SelectedReason` 與 `Feedback` 是雙向繫結的，因此檢視模型能在使用者給出答案時看到它們。`PlanName`、`AccessEndsOn`、`Reasons`、`Losses`、`DefaultOffer` 與 `Labels` 在變更時被讀取。此流程沒有命令：在程式碼後置中連接 `CancellationConfirmed`、`OfferAccepted` 與 `KeepPlanRequested`，並將它們轉送給檢視模型。需要知道使用者目前位於哪一步時，讀取 `Step`（唯讀）。

### 焦點與關閉

每次步驟切換之後，焦點移至新步驟的標題，標題會以禮貌方式朗讀。在使用者離開之前，請將流程保留在它所在的對話方塊或卡片中。只在 `KeepPlanRequested` 時關閉對話方塊；`OfferAccepted` 會將流程切換到已保留步驟，使用者仍需點選「完成」才能離開。呼叫 `Reset()` 可從第一步重新開始，它會清除原因與回饋。`GoBack()` 在原因步驟與已保留步驟上返回 `false`，因為這兩個步驟之前沒有任何步驟。

### 文字與其他語言

流程中所有內建字串都位於 `CancelFlowLabels` 中。替換整個物件即可改寫或翻譯流程；日期遵循 `Labels.Culture`。預設值為英文，例如「Why are you canceling?」與「Hold to cancel plan」。API 參考中的表格列出了所有預設值。

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

包含方案名稱或日期的句子，由標籤根據方案名稱與格式化後的日期產生，因此請翻譯整句，而不是其中的片段。挽留方案的標題與原因屬於你的內容，請在你自己的資料中翻譯它們。

### 範例：Team 方案取消

管理員從帳號設定中取消 Team 方案。流程顯示三個原因；前兩個原因尚無對應的挽留方案，因此預設挽留方案是「先跟我們聊聊」。方案名稱與存取結束日期來自方案資料，步驟列顯示三段，因為該路徑可能包含挽留方案。第一步顯示時「繼續」處於停用狀態，這是使用者在選擇原因之前看到的狀態。

這裡的設計決定是先詢問、再提供挽留。帶有挽留方案的原因只會在被選取之後出現，因此挽留方案是在回應原因，而不是打斷它。在選擇原因之前，「繼續」按鈕保持停用，因此流程絕不會記錄一個空的答案。

![Team 方案取消的第一步，尚未選擇原因](images/TeamCancellation.png)

```xml
<StackPanel Width="560">
    <arc:CancelFlow PlanName="Team" AccessEndsOn="2026-11-01">
    <arc:CancelFlow.Reasons>
        <arc:CancelFlowReason Title="Too expensive"
                              Description="The price is more than it is worth to us right now." />
        <arc:CancelFlowReason Title="Not using it enough"
                              Description="We do not open it often enough to keep paying for it." />
        <arc:CancelFlowReason Title="Missing a feature I need"
                              Description="Something we rely on is not there yet."
                              AsksForDetails="True" />
    </arc:CancelFlow.Reasons>
    <arc:CancelFlow.DefaultOffer>
        <arc:CancelFlowOffer Title="Talk to us first"
                             Description="Fifteen minutes with our team is often enough to fix what is not working."
                             AcceptLabel="Book a 15-minute call" />
    </arc:CancelFlow.DefaultOffer>
</arc:CancelFlow>
</StackPanel>
```

### 文案規範

請把這個流程寫成與即將離開的人之間的一次對話。直接說明將會結束什麼，不要讓人內疚或施壓。預設文案已經遵循了這些規則。

- 在每一句提到方案的話中都使用方案名稱，例如「要取消你的 Team 方案嗎？」，讓使用者知道受影響的是哪一個訂閱。
- 存取結束日期要寫出月份名稱，例如「你可以使用到 2026 年 11 月 1 日。」日期已知時，不要寫「月底」。
- 原因要用使用者自己的話來寫，並且短到一行放得下：「太貴了」，而不是「價格敏感」。
- 挽留方案的標題寫成帶有大小的收益，例如「三個月五折」；接受按鈕寫成動作，例如「領取五折優惠」。
- 按住按鈕要寫成帶受詞的動詞片語：「按住以取消方案」。不要只寫「確認」，因為使用者必須按住這個控制項，標籤應說明將會發生什麼。
- 損失要用平實的語言說明後果，例如「共用專案將對全部 6 位成員變為唯讀」。

| 不要寫 | 改為 |
|---|---|
| 您確定嗎？ | 要取消你的 Team 方案嗎？ |
| 點選以確認取消 | 按住以取消方案 |
| 很遺憾您要離開 | 你的方案已取消 |
| 原因：其他 | 其他情況 |
| 優惠即將到期 | 三個月五折 |

### 最佳做法

**建議**

- 從帳單週期中設定 `AccessEndsOn`，讓確認與結果顯示真實的結束日期。日期為空會讓流程承諾伺服器可能並不提供的存取權限。
- 只在挽留方案真實且可以套用時才顯示它。使用者一接受，流程就會切換到已保留步驟，而不會等待你的程式碼，因此請在 `OfferAccepted` 中可靠地套用該方案。
- 即使之後取消失敗，也要透過 `CancellationConfirmed` 記錄原因與回饋，讓支援團隊看到使用者的意見。
- `Losses` 只列出適用於此帳號的後果；沒有任何損失時保持為空，而不要列出泛泛的警告。
- 在 `Dialog` 中使用 `Appearance="Plain"`，讓對話方塊不會在流程外再畫一層邊框。

**避免**

- 隱藏退出途徑。在原因與確認步驟上保持「保留我的方案」可見；一個困住使用者的流程會導致拒付與投訴。
- 為與原因不相符的情況提供挽留。在「缺少我需要的功能」之後出現折扣，會讓人覺得是在推銷，並降低對流程其餘部分的信任。
- 在伺服器確認之前，於 `CancellationConfirmed` 中變更方案。只有在伺服器接受請求之後，才將方案標記為已取消。
- 移除長按。長按的作用是區分有意的取消與誤點，將它替換為單一按鈕會使這一步失去意義。
- 翻譯了標籤，卻把原因標題與挽留方案留在英文。那樣卡片上會混雜兩種語言，因此請在翻譯標籤的同時翻譯你的內容。

### 常見問題

**為什麼第一步的「繼續」是停用的？** 流程需要先選定原因才能前進，因此必須設定 `SelectedReason`。用方向鍵選取一列，或在程式碼中設定 `SelectedReason`。

**為什麼步驟列只顯示兩段？** 該路徑沒有挽留方案：所選原因沒有挽留方案，且 `DefaultOffer` 為 null。設定 `DefaultOffer`，或為原因設定 `Offer`，即可加入第三段。在選擇原因之前，只要任一原因帶有挽留方案，步驟列就會將該路徑計為有挽留方案。

**為什麼我的「重新啟用方案」按鈕會讓流程重新開始？** 除非處理常式將事件標記為已處理，否則流程會在 `ReactivateRequested` 之後呼叫 `Reset()`。在處理常式中設定 `e.Handled = true`，即可在你還原方案的同時，讓已取消的步驟保持在螢幕上。

**如何從第一步重新開始流程？** 呼叫 `Reset()`。它會清除原因與回饋，並返回第一步。`GoBack()` 會從挽留方案或確認步驟後退一步，在其他步驟上返回 `false`。

**流程會取消訂閱嗎？** 不會。它會以原因與回饋回報 `CancellationConfirmed`，由你的程式碼呼叫帳單 API。伺服器回應之後，更新 `Losses`、`AccessEndsOn` 或頁面的方案狀態。

## API 參考

<!-- api: CancelFlow -->

`CancelFlowReason`

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Title` | `string` | 使用者會說出的原因，例如「太貴了」。 |
| `Description` | `string?` | 標題下方的一行簡短說明。 |
| `Offer` | `CancelFlowOffer?` | 選擇該原因後顯示的挽留方案；為 null 時退回到 `DefaultOffer`。 |
| `AsksForDetails` | `bool` | 為 true 時，回饋欄位會詢問 `DetailsPrompt`，而不是一般問題。回答仍然是選用的。 |
| `DetailsPrompt` | `string?` | 後續問題，例如「你缺少什麼？」；為 null 時使用 `Labels.DetailsPrompt`。 |

`CancelFlowOffer`

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Title` | `string` | 提供的內容，例如「三個月五折」。 |
| `Description` | `string?` | 該挽留方案對方案與帳單意味著什麼。 |
| `AcceptLabel` | `string?` | 接受按鈕，例如「領取五折優惠」；為 null 時使用 `Labels.AcceptOffer`。 |
| `Icon` | `Geometry?` | 標題上方卡片的 24×24 描邊圖示；為 null 時顯示禮物。 |
| `AcceptedTitle` | `string?` | 已保留步驟的標題，例如「五折優惠已生效」；為 null 時使用 `Labels.RetainedTitle`。 |
| `AcceptedDescription` | `string?` | 其下的句子；為 null 時使用 `Labels.RetainedDescription`。 |

`CancelFlowLabels`（皆為 `init`；函式根據方案名稱與格式化後的日期產生句子）

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

`CancelFlowStep`

| 成員 | 顯示時機 |
|---|---|
| `Reason` | 第一步：原因與選用的回饋 |
| `Offer` | 所選原因或 `DefaultOffer` 對應的挽留方案 |
| `Confirm` | 將結束的內容，以及長按按鈕 |
| `Canceled` | 確認取消後的結束頁，帶有「重新啟用方案」 |
| `Retained` | 接受挽留方案、方案得以保留的流程結束頁 |

## 無障礙

- 每次步驟切換之後，焦點移至新步驟的標題，標題會以禮貌方式朗讀。標題可以取得焦點，但不是 Tab 停駐點，因此 Tab 會繼續移到該步驟的第一個控制項。
- 原因是以步驟標題命名的單選群組（「Why are you canceling?」）。方向鍵在原因之間移動，整個群組是單一 Tab 停駐點。
- 取消需要一次有意的長按：按住按鈕，或將焦點放在按鈕上並按住 Space 或 Enter。其說明文字會說明這一點。
- 圓形返回按鈕以「Back」命名，並帶有工具提示。選擇原因之前，收合的回饋欄位不在 Tab 順序中。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 依序在該步驟的控制項之間移動 |
| `Up` / `Down` | 在原因單選群組中的各原因之間移動 |
| `Enter` | 按下該步驟的主要操作：繼續、挽留方案的接受按鈕或完成。在未選取的原因上，Enter 會先選取它 |
| 在回饋欄位中按 `Enter` | 換行 |
| 在回饋欄位中按 `Ctrl+Enter` / `Cmd+Enter` | 繼續 |
| `Alt+Left` | 從挽留方案或確認步驟後退 |
| 在長按按鈕上按住 `Space` 或 `Enter` | 確認取消（`HoldToConfirm` 的行為） |

上一步仍按住的 Enter 鍵不會按下下一步的按鈕。

## 動態效果

- 步驟沿行進方向滑動 12 px（前進時從右側，後退時從左側），並伴隨短暫的淡入淡出，卡片的高度則以彈簧動畫跟隨新步驟。
- 回饋欄位透過高度彈簧展開與收合。步驟列的段在切換到新顏色時交叉淡變，返回按鈕隨著其占用空間收縮而淡入淡出。
- 在最後一步，勾選圖形會在約半秒內自行繪製出來。
- 減少動態效果時只保留淡變：步驟在原位交叉淡變，段的顏色快速變化，勾選圖形立即繪製。關閉動態效果時，所有變化都在原位立即發生。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 步驟切換 | 滑動 12 px 並伴隨淡變；卡片高度以彈簧過渡到新步驟 | 步驟在原位交叉淡變 | 立即 |
| 回饋欄位展開或收合 | 高度以彈簧展開 | 顯示或隱藏，沒有高度彈簧 | 立即 |
| 步驟列的段變化 | 顏色交叉淡變 | 顏色快速變化 | 立即 |
| 返回按鈕出現或消失 | 淡入淡出，同時其占用空間收縮 | 原位淡入淡出 | 立即 |
| 最後一步的結果勾選 | 約半秒內繪製出來 | 立即繪製 | 立即繪製 |

## 相關元件

- 其他設定類區塊：`SettingsPage`、`SettingsCommand`、`SecuritySettings`、`BillingOverview`（其「取消方案」按鈕是進入流程的自然入口）以及 `UsageBilling`。
- `RadioGroup`、`Textarea`、`HoldToConfirm`、`Button` 與 `Dialog`，本流程由它們組成。
- `ConfirmMorph`：用於對較小的破壞性操作進行較輕量的原地確認。

| 元件 | 適用情境 |
|---|---|
| `BillingOverview` | 取消按鈕位於帳單頁面，流程從 `CancelPlanRequested` 開啟 |
| `ConfirmMorph` | 小型破壞性操作需要在原處進行一次確認 |
| `HoldToConfirm` | 單一不可逆操作需要有意的長按，且不需要原因或挽留方案 |
| `RadioCards` | 使用者在方案或選項之間選擇，而不是離開 |
| `Dialog` | 流程在模態視窗中執行，並使用 `Appearance="Plain"` |
| `Textarea` | 在取消流程之外需要自由文字回答 |
