---
title: 付款流程
summary: 一筆信用卡付款從結帳開始，經過權杖化、授權和發卡機構的決定，直到結算。
source-hash: eb2f6d34d5e5
---

<Demo name="CardPayment" />

## 何時使用

`PaymentFlow` 說明客戶按下付款到商店收到款項之間發生的事情。它描繪一筆信用卡付款的路徑：結帳、將卡片資訊權杖化的支付閘道、卡片組織網路、負責核准或拒絕的發卡銀行、沿原路返回的回應，以及結算到特約商店帳戶。它適用於結帳說明頁、支付文件和加入流程的畫面，讀者需要知道付款為何會被核准或拒絕，以及款項何時入帳。

- **信用卡被拒絕的結帳說明**：結帳頁上解釋拒付原因的說明文章。圖示顯示是發卡銀行作出決定，因此客戶知道應該聯絡銀行或改用另一張卡。
- **支付服務商的整合文件**：給開發者的指南，顯示整合時設定的金額、幣別和特約商店，以及權杖化與授權步驟發生的位置。
- **特約商店加入時的結算時程**：說明已核准的款項何時抵達特約商店撥款帳戶的頁面。結算路徑與餘額讀數不需要時程表，就能呈現事件的先後順序。
- **銷售或客服訓練**：給回答「為什麼我的卡被拒絕」問題的客服人員的簡短說明，拒絕路徑以危險色顯示。
- **定價頁上的產品行銷**：一項端到端處理信用卡付款之功能的視覺圖，放在付款方式說明旁邊。

## 何時不用

- **輸入付款金額或卡片資料**：使用者輸入的金額用 `MoneyInput`，卡片欄位用表單。`PaymentFlow` 只說明流程，沒有輸入功能。
- **繪製付款總額隨時間的變化**：營收、交易量或拒付率用圖表分類中的 `BarChart` 或 `LineChart`。插圖中的餘額讀數只服務於故事，不是你的帳本。
- **真實付款的即時狀態**：付款失敗用 `Alert`，狀態用 `Badge`，單筆交易的事件用 `Timeline`。循環播放的插圖無法告訴讀者某筆付款仍在等待處理。
- **過去付款的清單**：用 `DataGrid` 呈現金額、日期和拒付代碼。單一動畫範例無法取代可搜尋的歷史紀錄。
- **客戶完成的多步驟結帳**：結帳本身的步驟用 `Stepper`。`PaymentFlow` 呈現客戶按下付款之後發生的事，不接受輸入。

## 用法

故事會依序播放每一筆付款。圖示下方的說明文字描述目前的步驟，每筆核准的付款都會讓特約商店餘額增加。此控制項執行時不需要資料，預設使用金額 42、特約商店 Northwind，以及尾號 4242 的卡片。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PaymentFlow />
</UserControl>
```

`PaymentFlow` 衍生自 `Illustration`，而 `Illustration` 衍生自 Avalonia 的 `TemplatedControl`。它可以取得焦點，帶有供輔助技術使用的 `Label`，並與其他系統插圖使用相同的外框、說明文字和播放按鈕。每筆付款都是場景時鐘上的固定序列，因此相同的輸入總會產生相同的故事；發卡銀行作出的決定，在動畫播放到之前控制項就已經知道。

### 結構

1. **客戶**（`Labels.Customer`）：左上角的卡片。其詳細資訊列顯示末四碼，例如「•••• 4242」；付款開始時，上方的金額徽章會顯示金額。
2. **結帳**（`Labels.Checkout`）：特約商店的結帳環節，詳細資訊列顯示特約商店名稱（`MerchantName`）。傳送卡片資料時變為強調色，付款結束時變為成功色或危險色。
3. **閘道**（`Labels.Gateway`、`Labels.GatewayDetail`）：將卡片權杖化的支付閘道。閘道持有權杖期間，其上方會出現附鎖頭圖示的「已權杖化」徽章。
4. **卡片組織網路**（`Labels.Network`、`Labels.NetworkDetail`）：負責傳送授權請求的網路。
5. **發卡銀行**（`Labels.Issuer`、`Labels.IssuerDetail`）：位於卡片組織網路下方的銀行。核對資金與風險時，其忙碌圓環會開始轉動；下方的徽章會以圖示顯示「已核准」或「已拒絕」。
6. **特約商店帳戶**（`Labels.Merchant`、`Labels.MerchantDetail`）：位於結帳下方的撥款帳戶。其下方的餘額讀數（`Labels.Balance`）以幣別顯示金額，每次結算都會出現「+ 金額」徽章。
7. **結算線**：一條從閘道出發、向左轉並連到特約商店帳戶的虛線。只有核准的付款才會沿它輸送款項。
8. **說明列**與**播放按鈕**：與其他插圖相同的控制項。靜態海報中會隱藏按鈕。

場景尺寸為 844 × 270 單位，會依控制項所給的寬度縮放。節點尺寸為 150 × 52 單位，圓角半徑為權杖中的 12 px；外框左右與上方的內距為 20 px，底部為 14 px。請求在每條連線的上方通道傳送，回應在下方通道返回，因此兩者不會重疊。

### 金額、幣別與特約商店

`Amount` 為 `decimal` 類型，預設值為 42。`Currency` 為 ISO 幣別代碼，預設值為 USD；留空時改用 USD。`MerchantName` 設定結帳時顯示的特約商店名稱，`CardLast4` 顯示卡號的末四碼。

插圖會依幣別格式化金額。USD、EUR 和 GBP 顯示符號並保留兩位小數；JPY 和 KRW 顯示符號且不保留小數；CNY 和 INR 顯示符號並保留兩位小數；其他代碼則顯示 ISO 代碼，並在金額前加一個空格，例如「CHF 42.00」。數字會以千分位分組，並一律使用不變文化（invariant culture），因此「1,284.00」在任何電腦上都顯示相同。

<Demo name="AmountAndMerchant" />

```xml
<arc:PaymentFlow Amount="129.90"
                 Currency="EUR"
                 MerchantName="Atelier"
                 CardLast4="0119"
                 DeclineEvery="0" />
```

`Amount` 與 `Currency` 應取自結帳實際收費的同一筆訂單，讓圖示上的徽章金額與客戶看到的金額一致。圖示不進行幣別換算；即使特約商店帳戶持有另一種幣別，餘額讀數仍使用 `Currency` 的幣別。

### 拒絕

`DeclineEvery` 每隔 N 筆付款拒絕一筆。預設值為 4，因此故事中的第 4、第 8 和第 12 筆付款會被拒絕。設為 0 則核准每一筆付款；設為 1 則拒絕每一筆付款。負值視為 0。

```xml
<arc:PaymentFlow DeclineEvery="0" />
```

被拒絕的付款與核准的付款沿同一路徑抵達發卡銀行。發卡銀行的徽章以危險色顯示「已拒絕」，回應沿下方通道返回，說明文字解釋拒絕原因：「已拒絕：餘額不足」。結算線不會亮起，特約商店餘額也不會改變，故事會停在提示客戶改用另一張卡的那一步。

### 標籤

`Labels` 接受一個 `PaymentFlowLabels` 物件，其中包含圖示中所有可見的文字：節點名稱與詳細資訊、徽章（`Approved`、`Declined`、`Paid`、`Payment failed`、`Tokenized`）以及各步驟的說明。付款開始時的說明由 `Pay` 產生，它接收格式化後的金額與特約商店名稱，因此新標籤可以同時使用這兩個值。

```csharp
var payment = new PaymentFlow
{
    Amount = 19.00m,
    Labels = new PaymentFlowLabels
    {
        Merchant = "商店",
        Issuer = "發卡銀行",
        Approved = "已核准",
        Declined = "已退回"
    }
};
```

標籤應保持簡短。節點卡片在一行中約可容納 16 個字元，徽章與卡片位於上方和下方，寬度相同。

### 回應決定

發卡銀行對付款作出決定時會觸發 `PaymentCompleted`。其參數 `PaymentCompletedEventArgs` 包含 `Approved` 旗標與該筆付款的 `Amount`。`PaymentCount` 給出故事開始後已作出決定的付款筆數，`Balance` 給出特約商店餘額。

```csharp
payment.PaymentCompleted += (_, e) =>
    log.Text = $"{(e.Approved ? "已核准" : "已拒絕")} {e.Amount:0.00} · 餘額 {payment.Balance:0.00}";
```

`Balance` 會在結算抵達特約商店時才增加，這比作出決定的時間晚。在 `PaymentCompleted` 處理常式內部，`Balance` 仍顯示本筆付款之前的餘額。如果紀錄需要在決定的當下顯示新餘額，應在後續步驟讀取它，或在 `Approved` 為 true 時自行加上金額。

### 播放

`IsPlaying` 用於播放與暫停故事，`Speed` 縮放播放速度（0.25 到 4），`Restart()` 從第一步重新開始，並將特約商店帳戶恢復為開始時的餘額 1,284。`Step`、`StepCount` 與 `StepChanged` 追蹤一筆付款的各個步驟。故事共有七個步驟；被拒絕的付款會停在第 5 步，因為結算步驟只對核准的付款執行。

| 步驟 | 說明 | 顯示內容 |
|---|---|---|
| 0 | 客戶在 Northwind 付款 $42.00（格式化後的金額與特約商店） | 客戶上方的金額徽章 |
| 1 | 卡片資料在支付閘道完成權杖化 | 請求先抵達結帳，再抵達閘道 |
| 2 | 授權請求傳送至發卡銀行 | 閘道徽章顯示「已權杖化」；請求穿過卡片組織網路 |
| 3 | 發卡銀行核對資金與風險 | 發卡銀行的忙碌圓環開始轉動 |
| 4 | 已核准：回應返回；或已拒絕：餘額不足 | 發卡銀行的徽章，以及下方通道上的回應 |
| 5 | 訂單已確認，或請客戶改用另一張卡 | 結帳顯示「已付款」或「付款失敗」 |
| 6 | 款項結算至特約商店帳戶（僅限核准的付款） | 虛線結算線與餘額讀數 |

```xml
<arc:PaymentFlow Speed="0.5" IsPlaying="False" />
```

一筆付款約占 8.4 秒的場景時間，因此故事以預設速度播放時，每筆付款略少於 9 秒。當頁面只說明一筆付款，而動畫會與文字爭奪注意力時，請用 `IsPlaying` 停止故事。

### 繫結到檢視模型

在結帳流程中，訂單擁有金額、幣別與特約商店，插圖只負責呈現。繫結應為單向：控制項從不把金額或幣別寫回。`IsPlaying` 是插圖唯一的雙向屬性。`PaymentCompleted` 事件不是繫結目標，因此應在程式碼後置檔中訂閱它，並把決定轉交給檢視模型。

```csharp
public partial class CheckoutViewModel : ObservableObject
{
    [ObservableProperty]
    private decimal _orderTotal = 129.90m;

    [ObservableProperty]
    private string _currency = "EUR";

    [ObservableProperty]
    private string _merchantName = "Atelier";

    [ObservableProperty]
    private string _cardLast4 = "0119";

    [ObservableProperty]
    private int _declinedCount;

    public void RecordDecision(bool approved)
    {
        if (!approved)
        {
            DeclinedCount++;
        }
    }
}
```

```xml
<arc:PaymentFlow Amount="{Binding OrderTotal}"
                 Currency="{Binding Currency}"
                 MerchantName="{Binding MerchantName}"
                 CardLast4="{Binding CardLast4}"
                 DeclineEvery="0" />
```

在程式碼後置檔中訂閱事件，此時可以存取檢視模型：

```csharp
public CheckoutView()
{
    InitializeComponent();
    Payment.PaymentCompleted += OnPaymentCompleted;
}

private void OnPaymentCompleted(object? sender, PaymentCompletedEventArgs e)
{
    ViewModel.RecordDecision(e.Approved);
}
```

訂單已有的值應使用繫結；插圖的拒絕循環只用於示範。顯示真實訂單的結帳頁面，不應把動畫中的拒絕循環當成該訂單的結果來呈現。

### 減少動態效果

在減少動態效果（或關閉）下，插圖是核准路徑的靜態畫面。每條連線都以成功色點亮，發卡銀行與結帳顯示成功狀態，金額徽章位於客戶上方，餘額讀數顯示開始時的餘額加上金額。播放按鈕會隱藏。當頁面只需說明核准路徑，而不需要整個序列時，請使用靜態畫面。

<Demo name="ReducedMotion" />

### 範例：信用卡付款的結帳說明

某設計用品商店結帳頁面上的說明文章。訂單為 Atelier 的 129.90 EUR，卡片末四碼為 0119，插圖以靜態畫面顯示核准路徑，因此讀者看到的是一筆正常付款。文字說明發卡銀行可以作出的兩種決定，以及讀者唯一能採取行動的地方，也就是卡片。標籤在有助於理解的地方保留特約商店自己的措辭，幣別也與客戶付款時使用的相同。

![Atelier 的一筆 129.90 EUR 信用卡付款，顯示核准路徑與「已付款」徽章](images/CardCheckout.png)

```xml
<StackPanel Width="760" Spacing="16">
    <TextBlock Text="How your card payment is processed"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <TextBlock Text="Your bank approves or declines every payment. A decline means the bank did not approve the amount, not that the store refused the card."
               TextWrapping="Wrap"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    <arc:PaymentFlow Label="Card payment of 129.90 EUR at Atelier"
                     Amount="129.90"
                     Currency="EUR"
                     MerchantName="Atelier"
                     CardLast4="0119"
                     DeclineEvery="0">
        <arc:PaymentFlow.Labels>
            <arc:PaymentFlowLabels Issuer="Your bank"
                                   IssuerDetail="funds · risk"
                                   Merchant="Atelier"
                                   MerchantDetail="payout account"
                                   Approved="Approved"
                                   Declined="Declined"
                                   Paid="Paid" />
        </arc:PaymentFlow.Labels>
    </arc:PaymentFlow>
</StackPanel>
```

截圖是核准路徑的靜態畫面，因此顯示的是最終數值的徽章與餘額讀數。除非頁面設定了減少動態效果，即時控制項會播放完整的序列。

### 文案規範

依讀者在對帳單或收據上看到的方式來稱呼各方：特約商店、卡片組織網路，以及發卡方統一稱為「您的銀行」。金額應附上幣別並保留兩位小數；決定性的用詞只使用銀行實際表達的內容。說明文字使用現在式，描述付款的狀態，而不是描述公司的流程。

| 不要寫 | 改為 |
|---|---|
| 付款處理成功 | 已核准 |
| 您的卡片已被拒絕 | 已拒絕：餘額不足 |
| 交易編號已驗證 | 卡片資料已在支付閘道完成權杖化 |
| 款項已傳送給特約商店 | 款項結算至特約商店帳戶 |
| 129 EUR | EUR 129.90 或 129.90 EUR，依頁面的語系設定 |
| 客戶已成功扣款 | 已付款 |

對於尚未結算的授權，不要使用「扣款」一詞；被拒絕的付款也不要說「退款」：沒有扣款，就沒有東西需要退還。標籤應簡短，說明文字限於一句話。

### 最佳做法

**建議**

- 呈現訂單所使用的金額、幣別與特約商店，讓圖示與客戶正在閱讀的頁面一致。金額不符會讓說明文字看起來像是在描述另一筆付款。
- 在頁面上說明決定是由銀行作出的。圖示把發卡銀行標示為作出決定的一方，讀者因此不會責怪商店。
- 介紹一般付款的頁面保留核准路徑；拒絕循環只用於示範，避免讀者看到一筆正常訂單被拒付。
- 當頁面說明的是讀者已經完成的一筆付款時，用 `IsPlaying` 停止故事，因為同一畫面中的循環動畫會分散讀者對收據的注意力。

**避免**

- 把這段序列當成真實結算的時程。故事約占 8.4 秒的場景時間，結算步驟只表示事件的先後順序。真實結算需要數天，並取決於服務商。
- 把發卡銀行標示為你的公司。發卡銀行是客戶的銀行，圖示若在那裡寫上你的公司，就陳述了你無從得知的事。
- 從插圖統計拒付筆數。`PaymentCount` 與 `Balance` 只統計故事中的內容，若頁面把它們當成銷售數字呈現，報出的數字與你的帳本毫無關係。
- 只用圖示來解釋拒絕原因。應以文字告訴客戶拒絕原因與下一步，因為圖示並不說明觸發了哪一條銀行規則。

### 常見問題

**為什麼 `PaymentCompleted` 處理常式中的 `Balance` 不包含本筆付款？** 作出決定發生在結算抵達特約商店帳戶之前，只有結算款項抵達時餘額才會增加。應在後續步驟讀取 `Balance`，或在核准的決定之後自行加上金額。

**能讓某筆訂單顯示拒絕嗎？** 不能。`DeclineEvery` 統計的是故事中的付款，控制項無法接收特定訂單的決定。若要解釋真實訂單的拒絕，應使用 `Alert`，或在表單旁邊提供銀行給出的拒絕原因。

**按下按鈕後如何重新開始故事？** 呼叫 `Restart()`。它會從第一步重新開始，餘額回到開始時的 1,284，拒絕計數器也會重設，因此下一筆付款會依預設循環核准。

**為什麼 CHF 顯示代碼而不是符號？** 插圖對 USD、EUR、GBP、JPY、CNY、INR 和 KRW 使用符號。其他幣別會顯示 ISO 代碼，並在金額前加一個空格，這樣就不必猜測客戶可能不認得的符號。

**能把標籤改成其他語言嗎？** 可以。將 `Labels` 設為一個 `PaymentFlowLabels` 物件，並填入你自己的字串。`Pay` 與 `Balance` 是接收格式化值的函式，因此參數數量應與預設值保持一致。

## API 參考

<!-- api: PaymentFlow -->

<!-- api: PaymentFlowLabels -->

`PaymentCompleted` 傳遞一個 `PaymentCompletedEventArgs`，包含以下成員。

| 成員 | 類型 | 意義 |
|---|---|---|
| `Approved` | `bool` | 發卡銀行是否核准了該筆付款 |
| `Amount` | `decimal` | 該筆付款的金額，即作出決定時設定的 `Amount` |

## 無障礙

- `Label` 是插圖的無障礙名稱。請在其中寫明流程，例如「訂單金額 129.90 EUR 的信用卡付款」，這樣螢幕閱讀器使用者在朗讀說明之前，就知道圖示在說明什麼。
- 插圖可以取得焦點，取得焦點時外框周圍會顯示 2px 的焦點環。
- 目前的說明會以說明文字的形式提供，因此控制項取得焦點時會朗讀該步驟的描述。
- 說明文字的變化不會被宣告，螢幕閱讀器使用者不會因為同一筆付款每隔幾秒重複一次而受到干擾。
- 決定以文字呈現，不只靠顏色：發卡銀行的徽章顯示「已核准」或「已拒絕」，結帳顯示「已付款」或「付款失敗」。看不到顏色的讀者仍能得知結果。
- 在減少動態效果下，靜態畫面會顯示核准路徑，並附上文字徽章與總結性說明。

### 鍵盤

插圖可透過 Tab 取得焦點。取得焦點後，以下按鍵可控制故事：

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移入或移出插圖 |
| `Space` 或 `Enter` | 播放或暫停故事 |
| `R` | 從第一步與開始時的餘額重新播放故事 |

插圖不提供為某筆付款選擇決定的方式。若要隨需展示拒絕，請設定 `DeclineEvery` 或保留預設循環，並在周圍的文字中說明該循環。

## 動態效果

- 金額在客戶上方浮現，卡片資料傳送至閘道，請求穿過卡片組織網路抵達發卡銀行。
- 發卡銀行的決定以綠色的「已核准」或紅色的「已拒絕」徽章顯示，回應沿下方通道返回。
- 核准的付款沿虛線結算至特約商店帳戶，餘額讀數隨之增加。
- 減少動態效果與關閉動態效果下，以靜態畫面顯示核准路徑，並帶有「已核准」與「已付款」徽章。播放按鈕會隱藏。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 金額與請求傳送 | 每條路徑上有動態資料封包，金額徽章位於客戶上方 | 核准路徑的靜態畫面 | 核准路徑的靜態畫面 |
| 閘道與發卡銀行狀態 | 「已權杖化」徽章彈出；發卡銀行的圓環在核對時轉動 | 以最終狀態顯示 | 以最終狀態顯示 |
| 決定 | 徽章彈出，回應沿下方通道返回 | 已核准徽章與成功路徑 | 已核准徽章與成功路徑 |
| 結算與餘額 | 虛線延伸至特約商店；讀數變為新餘額 | 讀數顯示開始餘額加上金額 | 讀數顯示開始餘額加上金額 |
| 播放按鈕 | 可見；切換播放狀態 | 隱藏 | 隱藏 |

## 相關元件

- `DataFlow`：付款完成後儲存事件的管線。
- `MultiRegionFailover`：區域故障時維持支付服務可用。
- `Stepper`：使用者依序經過的固定步驟序列。

| 元件 | 適用情境 |
|---|---|
| `MoneyInput` | 客戶輸入的金額需要解析與格式化 |
| `Alert` | 需要以文字說明付款失敗或服務中斷 |
| `Timeline` | 依時間順序呈現一筆真實交易的事件 |
| `DataGrid` | 列出含金額、日期與拒付代碼的過去付款 |
| `BarChart` / `LineChart` | 繪製隨時間變化的營收、交易量或拒付率 |
| `Stepper` | 使用者依序經過結帳的各個步驟 |
| `MultiRegionFailover` | 內容講的是某個區域故障，而服務仍可使用 |
