---
title: Domain verify
summary: 自訂網域設定卡片：客戶新增網域並複製 DNS 記錄，接著即時檢查器把每筆記錄標為有效、找不到或設定錯誤並給出一行修正建議，最後完成網域驗證並簽發 SSL 憑證。
source-hash: ab584178445a
---

<Demo name="Preview" />

## 何時使用

`DomainVerify` 是產品設定中的「自訂網域」一步。客戶輸入網域後會得到需要建立的 DNS 記錄（每筆都附有複製按鈕），然後看著即時檢查器逐筆處理：

- **讓客戶用自己的網域存取應用程式、文件站、狀態頁或商店。**
- **在開啟郵件投遞之前驗證寄信網域**（SPF、DKIM、DMARC）。
- **任何需要使用者把值複製到第三方、再等待生效的設定步驟。**

## 何時不使用

- **購買或搜尋網域。** 請改用附結果清單的搜尋框。
- **同時管理很多網域。** 請用每個網域一列的表格，再為其中一個開啟此卡片。
- **只需一次性複製單一值。** 在值旁邊放一個複製按鈕就夠了。

## 用法

此卡片衍生自 Avalonia 的 `TemplatedControl`，不需要任何資料：沒有設定任何動作時，它出現在畫面上後會播放一段模擬流程（新增網域、檢查、發現衝突記錄、修正、驗證通過），在其中點擊或按鍵就會把控制權交給使用者。此時狀態列會播報「Simulated, no DNS was queried」。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DomainVerify Title="Custom domain" DefaultDomain="" />
</UserControl>
```

### 流程

`Phase` 表示卡片所處的階段，每次變化都會觸發 `PhaseChanged`：

1. **Enter**：網域輸入框和「Add domain」按鈕；後面是變暗的預留列和一句「DNS records appear here after you add a domain」。輸入的內容在檢查之前會先去掉通訊協定、路徑、連接埠和結尾的點。
2. **Adding**：`GetRecordsAction` 執行期間，按鈕顯示載入圈。
3. **Records**：網域列取代輸入框，列出各筆記錄，頁尾提供「Check records」。
4. **Checking**：每隔 `PollInterval` 輪詢解析器。尚未生效的列顯示載入圈；頁尾閃爍並統計已生效的區域數。
5. **Invalid**：某筆記錄的值有衝突。輪詢停止，該列顯示「Misconfigured」，下方展開一則提示，說明找到了什麼以及如何修正。「Check again」開始新一輪檢查。
6. **Verified**：所有記錄均有效。網域列顯示「Issuing SSL certificate」，直到 `IssueCertificateAction` 回傳，隨後顯示「SSL by Let's Encrypt · renews in 60 days」。此時觸發 `Verified`，「Add another」重新開始。

### 連接你的伺服器

三個非同步委派代表你的後端。每個委派都會收到取消權杖，移除網域或卡片離開視窗時會取消它。

```csharp
card.GetRecordsAction = async (domain, cancellation) =>
    await api.AddDomainAsync(projectId, domain, cancellation);          // IReadOnlyList<DomainRecord>

card.VerifyAction = async (domain, records, context, cancellation) =>
    await api.CheckDomainAsync(projectId, domain, cancellation);        // DomainVerifyResult

card.IssueCertificateAction = async (domain, cancellation) =>
    await api.IssueCertificateAsync(projectId, domain, cancellation);   // DomainCertificate

card.Verified += (_, e) => Console.WriteLine($"{e.Domain} is live");
```

`VerifyAction` 只做一次檢查，不要寫成迴圈。記錄仍在生效時回傳 `DomainRecordStatus.Missing`，卡片會繼續輪詢；只有值衝突時才回傳 `DomainRecordStatus.Invalid`，並附上 `Found` 和一句話的 `Fix`。卡片在「全部有效」或出現任何無效記錄時停止。模擬檢查時 `PollInterval` 為 800 毫秒，設定了 `VerifyAction` 時為 5 秒。任一委派丟出的例外會顯示在頁尾、觸發 `Failed`，並讓卡片保持可重試狀態。

<Demo name="Handlers" />

### 窄容器

卡片會填滿容器，最寬 680。寬度低於 544 時欄標題消失，每筆記錄改為堆疊：第一行是類型、名稱和狀態，下面是值和複製按鈕，再下面是用途說明。過長的名稱和值以省略號截斷，完整內容保留在工具提示裡；複製按鈕一律複製完整的值。

<Demo name="Narrow" />

### 寄信網域

對於郵件，請從 `GetRecordsAction` 回傳 MX、SPF（TXT）、DKIM（CNAME 或 TXT）和 DMARC 記錄，把 `Title` 設為「Sending domain」，並用 `Labels` 改寫文字：這樣最後一步顯示「Turning on delivery」，而不是簽發憑證。不知道 DNS 代管方時，設定 `Provider="{x:Null}"` 即可隱藏「DNS hosted at」一行。

<Demo name="SendingDomain" />

### 文字

卡片顯示或播報的每個詞都是 `DomainVerifyLabels` 的屬性；給 `Labels` 指派一個新執行個體即可翻譯或改寫。帶格式的文字（例如頁尾的「Checking records · 2 of 6 regions propagated」）是函式，因此語序可以調整。

## API 參考

<!-- api: DomainVerify -->

<!-- api: DomainRecord -->

<!-- api: DomainRecordCheck -->

<!-- api: DomainVerifyResult -->

<!-- api: DomainCertificate -->

<!-- api: DomainProvider -->

## 無障礙

- 卡片以標題（二級標題）命名。記錄是名為「DNS records」的清單；每一列讀出類型、名稱和狀態（「CNAME app, Valid」）。
- 每種狀態都是圖示加一個簡短的詞，絕不只靠顏色。衝突記錄的提示是該列的一部分。
- 禮貌型即時區域播報每一步：網域已新增、正在檢查、某筆記錄需要修正、網域已驗證、憑證已簽發、失敗。未設定 `VerifyAction` 時會附加「Simulated, no DNS was queried」。
- 網域輸入框的標籤是「Domain」；網域無效時下方顯示錯誤。複製按鈕的名稱是「Copy TXT value」、「Copy CNAME value」等。變暗的預留列不可互動，並對讀屏隱藏。

### 鍵盤

- 在網域輸入框中按 **Enter** 新增網域，與「Add domain」相同。
- **Tab** 依序經過輸入框和按鈕、關閉按鈕、每筆記錄的複製按鈕和頁尾操作。
- 在取得焦點的複製、關閉或頁尾按鈕上按 **Enter** 或 **Space** 即可觸發。
- 新增網域後焦點移到頁尾操作；移除後焦點回到網域輸入框。

## 動態效果

- **狀態**：狀態文字和圖示交叉淡化（100 毫秒淡出、160 毫秒淡入，上升 2 像素）；檢查中的列顯示載入圈，頁尾顯示閃光。
- **列**：記錄在 160 毫秒內淡入，每一列比前一列晚 40 毫秒。修正提示依平滑彈簧展開。
- 減少動態效果時保留所有狀態，去掉錯開效果，只做短暫淡化。關閉動態效果時所有變化立即生效。模擬流程只在卡片位於畫面內時播放。

## 相關

- `CopyButton`、`Button`、`Input` 和 `TextShimmer`，組成此卡片的控制項。
- API keys、Webhooks、Integrations、Env variables 和 Webhook tester，Developer 分組裡的其他區塊。
