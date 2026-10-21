---
title: 警告提示
summary: 持續顯示的訊息，協助使用者復原或繼續操作。
source-hash: 5d572d95a7b8
---

<Demo name="Tones" />

## 何時使用

`Alert` 是一則會一直留在頁面上、直到其情況解決為止的通知。它位於頁面或表單的內容流中，緊鄰與其相關的內容，並能隨情況改變語氣、標題與文字。當訊息必須在使用者移開視線後仍然存在時，它是正確的工具。

- **帳單與方案通知**：帳單頁面上信用卡付款失敗，或七天後續訂的方案。通知說明後果，並會一直保留，直到付款方式更新為止。
- **配額與上限警告**：工作區已使用 10 個席位中的 8 個，或匯出佇列接近每日上限。警示位於「邀請」或「匯出」動作上方，讓使用者在撞到上限之前就能看到。
- **長時間變更的狀態**：網域驗證、同步或儲存從「正在儲存變更」變為「所有變更已儲存」或「無法儲存」。請保留同一個 `Alert` 並改變其內容，版面才不會跳動。
- **使用者可以關閉的通知**：儀表板上的維護時段，或設定表單中的小提示。使用者關閉後，下方內容會收合空缺。

## 何時不用

- **操作完成後的確認**，例如按下後的「變更已儲存」：用 `Toast`，它會自行消失。
- **排隊中的應用程式結果**，帶有復原或檢視等動作，且可能同時到達：用 `ToastStack`。
- **使用者必須先作出決定才能繼續的情況**：用 `Dialog`。
- **關於單一欄位的訊息**，例如「請輸入完整的電子郵件地址」：用 `Input` 的 `ErrorMessage` 列，它會附著在所描述的欄位上。
- **位於每個頁面上方的全站訊息**：用 `AnnouncementBar`，它會輪播，並可記住關閉狀態。
- **沒有內容的頁面或清單**：用 `EmptyState`。

## 用法

基本用法會設定 `Title` 與 `Content`。預設的 `Tone` 為 `Info`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Alert Title="你的信用卡下個月到期"
               Content="更新付款方式，以維持方案有效。" />
</UserControl>
```

`Alert` 衍生自 Avalonia 的 `ContentControl`，因此 `Content` 與 `ContentTemplate` 的運作方式與任何內容控制項相同。字串形式的 `Content` 會成為描述列；其他任何內容則會原樣顯示在標題下方。它沒有 `Size` 屬性：寬度由其容器決定，因此請把它放在與所描述內容相符的欄中。

### 結構

1. **語氣圖示**（`Tone`）：18 px 的圖示，描邊 2 px，向下微調 1 px 使其與標題第一行對齊，與文字之間相距 12 px。顏色隨語氣而變。
2. **標題**（`Title`）：14 px，中等字重，行高 19.6 px，使用預設前景色。標題會換行，不會截斷。
3. **描述**（字串形式的 `Content`）：12 px，使用次要文字色，位於標題下方 4 px，行高 16.8 px。空白的描述不佔用空間。
4. **富內容**（非字串形式的 `Content`）：與描述位於同一欄，同樣相距 4 px。其中的文字透過 `TextElement` 繼承 12 px 字級與次要色。
5. **關閉按鈕**（`IsDismissible`）：位於右上角的 28 px 圓形幽靈按鈕。僅在 `IsDismissible` 為 `True` 時顯示，並會被拉入 16 px 的內距中（上下各 5 px、右側 6 px），使其圖示與標題對齊。
6. **表面**：內距 16 px，1 px 框線，圓角為控制項圓角，使用表面色並帶有靜止陰影。

### 語氣

`Tone` 決定圖示、其顏色，以及變化時的宣告方式。請依使用者接下來必須做的事來選擇語氣，而不是依事件看起來有多嚴重。

| 語氣 | 圖示 | 適用於 | 宣告方式 |
|---|---|---|---|
| `Info`（預設） | 資訊 | 中性的事實與提示：方案續訂日期、新功能 | 禮貌 |
| `Success` | 勾號 | 問題已解決：網域已驗證、付款已完成 | 禮貌 |
| `Warning` | 帶驚嘆號的三角形 | 需要盡快處理：配額將滿、信用卡即將到期 | 禮貌 |
| `Danger` | 帶叉的圓形 | 發生失敗且使用者必須處理：付款或同步失敗 | 強烈 |

```xml
<StackPanel Spacing="12">
    <arc:Alert Tone="Success" Title="網域已驗證"
               Content="寄自 acme.dev 的郵件現在可以正常送達。" />
    <arc:Alert Tone="Warning" Title="你即將達到儲存空間上限"
               Content="釋出空間或升級方案，否則上傳將暫停。" />
    <arc:Alert Tone="Danger" Title="付款失敗"
               Content="無法扣除末四碼為 4242 的信用卡款項。" />
</StackPanel>
```

請謹慎使用 `Danger`：若每則通知都用它，人們就會不再留意顏色。`Danger` 會以強烈方式宣告，因此螢幕閱讀器會打斷目前的朗讀來念出它。

### 原位變形

保留同一個執行個體，並在狀態改變時修改其屬性。圖示會替換，新文字會升起出現，高度則依彈簧跟隨變化。若改為替換控制項，就會略過這些轉場，頁面也會跳動。

<Demo name="MorphInPlace" />

```csharp
// 同一個 Alert 執行個體在儲存過程中變更語氣、標題與內容。
Status.Tone = AlertTone.Info;
Status.Title = "正在儲存變更";
Status.Content = "正在將 3 個檔案上傳至工作區。";

await _workspace.SaveAsync(draft);

Status.Tone = AlertTone.Success;
Status.Title = "所有變更已儲存";
Status.Content = "工作區中的每位成員現在都能看到最新版本。";
```

當失敗路徑需要使用者能採取行動的訊息時，請把下一步放進 `Content`（「請檢查網路連線後再試一次。」），而不要另加第二則通知。

### 關閉

設定 `IsDismissible` 即可顯示關閉按鈕。按下後警示會先收合，然後引發 `Dismissed`。將 `IsOpen` 設回 `True` 會再次顯示警示，其圖示也會從較小尺寸回到完整尺寸。

<Demo name="Dismiss" />

```csharp
var notice = new Alert { Tone = AlertTone.Warning, IsDismissible = true, Title = "週日排定維護" };
notice.Dismissed += (_, _) => SaveDismissedState();

// 稍後再次顯示警示。
notice.IsOpen = true;
```

`Dismiss()` 以程式碼執行相同的動作：它會收合警示，並在收合完成後引發 `Dismissed`。將 `IsOpen` 設為 `False` 也會收合警示，但不會引發 `Dismissed`，因為 `Dismissed` 代表使用者的選擇。若是因其他原因隱藏警示（例如某個狀態變化使其過時），請使用 `IsOpen`。

### 自訂內容

任何非文字的 `Content` 都會顯示在標題下方。文字內容會套用詳細說明的樣式；其他內容則維持原樣顯示。

<Demo name="CustomContent" />

```xml
<arc:Alert Title="有新版本可用">
    <WrapPanel HorizontalAlignment="Center" ItemSpacing="12" LineSpacing="12">
        <arc:Button Content="重新載入" Size="Small" Variant="Secondary" />
    </WrapPanel>
</arc:Alert>
```

自訂內容只放一個動作或一份簡短清單。若警示內含表單，它就變成了卡片，而卡片應放在它自己的區段版面中。純數字或其他格式化的值會以其文字顯示，因為 `Alert` 會以 `ToString` 轉換 `IFormattable` 值。

### 由資料驅動

警示只負責顯示，但其語氣、標題與可見性通常來自檢視模型。使用 CommunityToolkit.Mvvm 時，檢視模型擁有狀態，警示負責顯示它。只有當關閉動作必須傳回檢視模型時，才對 `IsOpen` 使用雙向繫結。

```csharp
public record PaymentResult(bool IsSuccess);

public abstract class BillingService
{
    public abstract Task<PaymentResult> RetryAsync(string invoiceId);
}

public partial class BillingViewModel : ObservableObject
{
    private readonly BillingService _billing;

    public BillingViewModel(BillingService billing) => _billing = billing;

    [ObservableProperty]
    private AlertTone _paymentTone = AlertTone.Danger;

    [ObservableProperty]
    private string _paymentTitle = "付款失敗";

    [ObservableProperty]
    private bool _paymentNoticeOpen = true;

    [RelayCommand]
    private async Task RetryPaymentAsync()
    {
        var result = await _billing.RetryAsync("inv_2026_0412");
        PaymentTone = result.IsSuccess ? AlertTone.Success : AlertTone.Danger;
        PaymentTitle = result.IsSuccess ? "付款已收到" : "付款失敗";
    }
}
```

```xml
<StackPanel Spacing="12">
    <arc:Alert Tone="{Binding PaymentTone}"
               Title="{Binding PaymentTitle}"
               IsOpen="{Binding PaymentNoticeOpen, Mode=TwoWay}"
               IsDismissible="True"
               Content="Acme Analytics 的發票 inv_2026_0412 仍未付款。" />
    <arc:Button Content="重試付款" Command="{Binding RetryPaymentCommand}" />
</StackPanel>
```

讓警示的語氣與標題繫結到它所回報的狀態，並讓命令決定下一個狀態。被關閉的警示會保持隱藏，直到檢視模型將 `PaymentNoticeOpen` 設回 `True`。

### 焦點與關閉

警示出現時不會取得焦點，因為通知不應打斷輸入。它唯一的焦點停靠點是關閉按鈕，而該按鈕只在 `IsDismissible` 為 `True` 時存在。關閉後焦點不會移到其他控制項，因此若使用者正在警示中操作，請在 `Dismissed` 處理常式中自行移動焦點。

按情況判斷關閉是一種選擇還是一種修復。使用者無需解決情況即可做出的關閉（例如維護提示）可以允許關閉。使用者仍必須修復的問題（例如付款失敗）應保持開啟，直到其狀態改變，而且根本不應允許關閉。

### 範例：帳單頁面通知

SaaS 管理後台的帳單頁面在方案區段頂端顯示三則通知。付款失敗使用 `Danger`，因為工作區將於 2026 年 3 月 3 日失去存取權。席位警告使用 `Warning` 並說明下一步動作；收據使用 `Success` 且可關閉，因為不需要任何處理。每個標題都說明情況，描述則告訴使用者能對此做什麼。

![帶有付款失敗、席位警告與收據通知的帳單區段](images/BillingNotices.png)

```xml
<StackPanel Width="600" Spacing="12">
    <TextBlock Text="Billing" FontSize="20" FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:Alert Tone="Danger"
               Title="Payment failed for Acme Analytics"
               Content="We couldn't charge the card ending in 4242. Update it by March 3, 2026 to keep Pro features."
               IsDismissible="True" />
    <arc:Alert Tone="Warning"
               Title="You're close to your seat limit"
               Content="8 of 10 seats are in use. Remove an inactive member or add seats before you invite more." />
    <arc:Alert Tone="Success"
               Title="Invoice INV-2026-0412 paid"
               Content="A receipt was sent to billing@acme.com."
               IsDismissible="True" />
</StackPanel>
```

### 文案規範

- **標題**：採用句子式大小寫，以簡短的句子陳述情況，句尾不加句號：「Acme Analytics 付款失敗」。螢幕閱讀器會先讀取標題，因此它必須能獨立理解。
- **描述**：一到兩句，說明後果與下一步：「請於 2026 年 3 月 3 日前更新，以保留 Pro 功能。」日期與數量請使用阿拉伯數字。
- **動作文字**：當警示內含按鈕時，使用動詞加受詞：「更新付款方式」，而不是「按這裡」。
- **語氣**：平實而冷靜。指出問題但不責怪，避免使用驚嘆號。

| 不要寫 | 改為 |
|---|---|
| 錯誤！ | 付款失敗 |
| 出了點問題。 | 無法儲存變更。請檢查網路連線後再試一次。 |
| 你的帳號有危險！！！ | 你的方案將於 2026 年 3 月 3 日結束。 |
| 點這裡修正。 | 更新你的付款方式。 |

### 最佳做法

**建議**

- 每種情況只保留一則警示，情況改變時就改變其語氣與文字。針對同一問題再加一則警示，只會增加雜訊，並留下過時的訊息。
- 把警示放在它所描述的內容旁邊，例如發票表格上方或席位設定之中。它的意義取決於周圍的脈絡。
- 只在使用者必須處理的失敗上使用 `Danger`。在例行事件上使用 `Danger` 會讓人們習慣忽略顏色。
- 在情況解決之前保持警示開啟，除非使用者對此有真正需要做的選擇。

**避免**

- 為已完成的動作使用警示。簡短的確認屬於 `Toast`，它不會停留在頁面上。
- 讓付款或安全性通知可以關閉。使用者可能在不解決問題的情況下把它關掉。
- 每次狀態變化都替換控制項。新的執行個體沒有變形效果，頁面會跳動。
- 在同一處堆疊超過三則警示。頁面會變成警告清單，重要的那則就會被淹沒。

### 常見問題

**設定 `IsOpen` 為 `False` 時，會引發 `Dismissed` 嗎？** 不會。只有在 `Dismiss()` 或關閉按鈕收合警示之後才會引發 `Dismissed`，因此它回報的是使用者所做的選擇。從程式碼隱藏警示只會收合它。

**關閉狀態在重新啟動後會保留嗎？** 不會。`Alert` 不保留任何狀態。若要記住關閉，請在引發 `Dismissed` 時把它儲存到你自己的設定中，然後在啟動時將 `IsOpen` 設為 `False`。若通知是全站性的，`AnnouncementBar` 提供透過 `DismissalKey` 記住關閉狀態的功能。

**為什麼標題與文字之間沒有間距？** 空白或 null 的 `Content` 會完全移除描述列，包括它的 4 px 間距。只有在 `Content` 有文字時才設定它。

**警示會在一段時間後自動隱藏嗎？** 不會。`Alert` 沒有計時器，也沒有持續時間。應該自行消失的訊息屬於 `Toast`。務必設定 `Title`，因為它是整個警示的無障礙名稱。

**如何在警示中放入按鈕？** 將 `Content` 設為包含該按鈕的面板，如同自訂內容範例。按鈕會保留自己的尺寸與命令，警示則提供內距與語氣。

## API 參考

<!-- api: Alert -->

配套型別 `AlertTone`：

| 成員 | 圖示 | 意義 |
|---|---|---|
| `Info` | 資訊 | 中性資訊（強調色）。這是預設值。 |
| `Success` | 勾號 | 某件事已成功。 |
| `Warning` | 帶驚嘆號的三角形 | 需要儘快注意。 |
| `Danger` | 帶叉的圓形 | 某件事失敗。以強烈方式宣告。 |

## 無障礙

- `Title` 為警示命名，`Content` 會被當作其說明文字朗讀。標題改變時會再次宣告。
- `Danger` 警示以強烈方式宣告，其他語氣以禮貌方式宣告。富內容沒有說明文字，因此請把關鍵句子放在標題中，或放在內容中的文字區塊裡。
- 關閉按鈕的名稱為 `Dismiss: {Title}`，讓使用者聽得出它關閉的是哪一則警示。
- 語氣圖示不會對輔助技術公開。語氣由標題與內容傳達，因此即使沒有顏色，標題也必須說明發生了什麼事。
- 關閉按鈕是一般按鈕：Tab 可以移至此處，Enter 或空白鍵即可啟動。
- 顯示警示時不會取得焦點，因此絕不會打斷輸入。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在 `IsDismissible` 為 `True` 時，移至或移出關閉按鈕 |
| `Enter` / `Space` | 關閉按鈕有焦點時，關閉警示 |

## 動態效果

- 顯示與隱藏警示時，其高度會以平滑彈簧展開與收合，下方的內容也隨之收合上移。
- 語氣改變時，圖示會以短暫的縮放與模糊替換。變化的文字會升起並變清晰。
- 減少動態效果時，去掉高度動畫與位移。文字與圖示僅淡入淡出，顯示或隱藏則立即完成。
- 關閉動態效果時，直接跳到最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 警示顯示（將 `IsOpen` 設為 `True`） | 高度以彈簧展開，文字淡入，圖示從較小尺寸過渡到完整尺寸 | 以完整高度顯示，文字與圖示淡入 | 立即完成 |
| 警示被關閉或隱藏 | 高度以平滑彈簧收合，警示淡出，下方內容上移 | 立即完成 | 立即完成 |
| 語氣改變 | 圖示以縮放與短暫模糊替換 | 圖示淡入淡出 | 立即完成 |
| 標題或內容改變 | 新文字從柔和模糊中升起，高度以彈簧調整至符合內容 | 文字淡入淡出 | 立即完成 |

## 相關元件

- `Toast`：用於完成動作後的簡短確認。
- `ToastStack`：用於需要排隊、更新與堆疊的應用程式通知。
- `AnnouncementBar`：用於網站頂部的輪播橫幅。
- `EmptyState`：用於完全沒有內容可顯示的情況。

| 元件 | 適用情境 |
|---|---|
| `Toast` | 已完成的動作需要一則會自行關閉的簡短確認 |
| `ToastStack` | 應用程式的多個結果或復原動作需要堆疊顯示 |
| `AnnouncementBar` | 全站訊息位於每個頁面上方並會輪播 |
| `Dialog` | 使用者必須先回應，頁面才能繼續 |
| `Input` | 訊息屬於某個欄位，位於其描述或錯誤列中 |
| `EmptyState` | 清單或頁面沒有內容可顯示 |
