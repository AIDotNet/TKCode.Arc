---
title: Webhooks
summary: Webhook 主控台：查看每個端點的健康狀況與流量，檢查送出的內容與收到的回應，就地重試失敗的投遞，並新增端點（簽章金鑰只顯示一次）。
source-hash: 103d1356447b
---

<Demo name="Preview" />

## 何時使用

`Webhooks` 是開發者設定區域中的一個頁面，使用者在這裡管理你的產品向其推送事件的 URL。由上而下依序顯示：端點（每個端點附有所訂閱的事件、成功送達的事件占比與健康標記）、所選端點的詳情以及過去一天的流量圖表、它最近的投遞，以及針對清單中所選投遞的檢查器：回應、請求本文與請求標頭。失敗的投遞可以用 Retry 就地重新傳送，"Add endpoint" 會展開為一個表單，並把新端點的簽章金鑰只顯示一次。

- **具備對外 Webhook 的產品。** 付款、物流、票務或排程服務會通知客戶的伺服器，客服人員與開發者需要看到某個端點為什麼停止回應。
- **整合的診斷頁面。** 一個端點及其流量，並附有測試事件，用於在上線前驗證來回流程。
- **內部事件匯流排主控台。** 把事件路由到內部服務的團隊可以觀察出錯的服務，並在修正後重新傳送。

## 何時不用

- **成千上萬筆投遞。** 清單顯示的是最近的投遞，不做虛擬化；完整的投遞歷史請使用附有分頁與篩選的 `DataGrid`。
- **編輯端點。** 區塊負責新增與顯示端點；重新命名、暫停、刪除與輪替金鑰應放在你自己的設定頁面中（簽章金鑰只在建立時顯示）。
- **單純的 URL 清單。** 當沒有流量可以顯示時，表格或卡片清單更簡單。

## 用法

### 端點與投遞

每個 `WebhookEndpoint` 有 `Name`（為空時顯示主機名稱）、`Url`、所訂閱的 `Events`、新增者與新增時間（`AddedBy`、`AddedOn`；此人會取得一個產生的頭像）、遮罩的 `SecretHint`、`ApiVersion`、以 `Volume` 桶表示的流量（最舊的在前；48 個半小時構成 24 小時圖表），以及最近的 `Deliveries`（最新的在前）。`DeliveredRate`、`FailingCount` 與 `Health` 會隨投遞與圖表的變化而變化：成功率統計圖表中未失敗的事件，向下取整到千分之一，當端點有一筆投遞失敗且尚未重試時，該端點處於失敗狀態。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Webhooks Description="Corvid signs each event and sends it to your endpoints.">
        <arc:WebhookEndpoint Name="Shipping sync"
                             Url="https://relay.tallowfield.dev/hooks/corvid/v2"
                             AddedBy="Lena Okafor"
                             SecretHint="sig_••••c41e"
                             ApiVersion="Version 2026.09" />
    </arc:Webhooks>
</UserControl>
```

`WebhookDelivery` 包含 `EventType` 與 `EventId`、`Attempt`、`StatusCode`（0 表示端點沒有回應）與 `StatusText`、`Duration`、`SentAt`、`ResponseBody`、請求的 `Payload`（JSON 會縮排並上色，其他文字原樣顯示），以及它的 `Headers`。對於你自己擁有的集合，請使用 `EndpointsSource`：`ObservableCollection` 會被監看，新端點會插入到其頂端。

`SelectedEndpoint`、`SelectedDelivery` 與 `InspectorTab` 是雙向的。選取端點時，會選取它的第一筆失敗投遞，沒有則選取最新一筆；切換投遞時，檢查器的分頁保持不變。

### 重試、新端點與測試事件

三個伺服器動作都是非同步委派。沒有它們時，區塊會模擬一個始終成功的伺服器，使用固定延遲（1.1 秒、0.76 秒與 0.9 秒），預覽就是這樣。

- `RetryAction` 重新傳送一筆失敗的投遞並回傳新的嘗試。它會帶著短暫的閃爍加到清單頂端；若成功，原本那筆會標記為已解決，端點的成功率與標記隨之更新，選取項移到新的嘗試。當委派擲出例外（或回傳失敗的嘗試）時，會改為加入一筆附有自己 Retry 按鈕的失敗嘗試，原本那筆顯示 "Retried"，並朗讀失敗。
- `CreateEndpointAction` 以 `WebhookEndpointDraft` 的形式接收表單，並回傳附有簽章金鑰的端點（`WebhookEndpointCreated`）。端點會加到清單頂端，閃爍直到面板關閉，金鑰只顯示一次並附有 Copy 按鈕；面板關閉時它被丟棄，也絕不會儲存在端點上。當委派擲出例外時，表單保持開啟，訊息顯示在按鈕上方。等待期間按 Escape 會取消請求。
- `SendTestAction` 由沒有投遞的端點所顯示的 "Send test event" 按鈕呼叫。失敗的測試會作為失敗的投遞加入。

`AppendDelivery` 會加入由你的伺服器回報的投遞，用於即時推送，並把它計入圖表的最後一格。

```csharp
var client = new HttpClient();
hooks.RetryAction = async (delivery, cancellation) =>
{
    var started = DateTimeOffset.Now;
    using var response = await client.PostAsync(
        $"https://api.example.dev/deliveries/{delivery.EventId}/resend", null, cancellation);
    return new WebhookDelivery
    {
        EventType = delivery.EventType,
        EventId = delivery.EventId,
        Attempt = delivery.Attempt + 1,
        StatusCode = (int)response.StatusCode,
        Duration = DateTimeOffset.Now - started,
        SentAt = started,
        ResponseBody = await response.Content.ReadAsStringAsync(cancellation),
        Payload = delivery.Payload,
    };
};
```

<Demo name="Failing" />

### 新端點

沒有投遞的端點，成功率顯示為破折號，標記為 "No traffic"，圖表為空，並附有測試按鈕。`EventTypes` 列出表單中的核取方塊；`IsDefaultSelected` 會在表單開啟時勾選其中一項（否則勾選第一項）。`UrlValidator` 會取代內建的 URL 檢查（必填、https、不能是 localhost）。當區塊模擬伺服器時，`CurrentUser` 指明是誰新增了端點。

<Demo name="NewEndpoint" />

### 窄版容器

版面依循區塊自身的寬度。在 880 以下（含），檢查器移到投遞清單下方；在 760 以下（含），端點列只保留名稱、成功率與標記；在 540 以下（含），內距收緊，工具列的說明文字隱藏。表單在 800 時事件分為兩欄，在 560 時全部變為單欄。寬度不超過 460 的投遞清單會把耗時與時間放在事件下方，窄的請求標頭則把每個名稱放在其值上方。

<Demo name="Compact" />

### 事件與繫結狀態

每一次變更都是一個冒泡的路由事件，並附帶發生了什麼變化：`EndpointSelectionChanged` 與 `DeliverySelectionChanged`（附有舊項與新項，以及是指標、鍵盤還是區塊本身改變了它）、`RetryStarted`、`RetryCompleted`（附有舊成功率與新成功率）、`ActionFailed`、`EndpointCreated`、`TestEventSent`、`ContentCopied`、`AddPanelOpened` 與 `AddPanelClosed`（附有原因）。`RetryCommand`、`SendTestCommand` 與 `EndpointCreatedCommand` 會同時執行。`OpenAddPanel`、`CloseAddPanel`、`RetryAsync` 與 `SendTestAsync` 在程式碼中完成按鈕所做的事；所有文字都在 `Labels` 中。

<Demo name="EventLog" />

## API 參考

<!-- api: Webhooks -->

<!-- api: WebhookEndpoint -->

<!-- api: WebhookDelivery -->

<!-- api: WebhookVolumeBucket -->

<!-- api: WebhookEventType -->

<!-- api: WebhookHeader -->

<!-- api: WebhooksLabels -->

## 無障礙

- **Tab** 依序經過 "Add endpoint" 膠囊、每個端點列、每個投遞列及其 Retry 按鈕、Copy 按鈕、所選分頁與負載面板。
- 在端點或投遞列上按 **上** 與 **下** 會移到上一列或下一列並選取它；**Home** 與 **End** 移到第一列與最後一列。它們不會循環，並且會略過 Retry 按鈕。
- 在分頁上按 **左** 與 **右** 在 Payload 與 Headers 之間切換；負載面板可用方向鍵捲動。
- **Enter** 或 **空白鍵** 選取一列或按下按鈕。在表單中，在欄位裡按 **Enter** 送出。
- 在面板內按 **Escape** 會關閉它並把焦點還給膠囊；表單等待伺服器時，Escape 改為取消請求。
- 端點與投遞都是附有名稱的清單（"Endpoints"、"Recent deliveries to Shipping sync"）。端點列會讀出名稱、主機名稱、成功率、標記與事件；投遞列會讀出狀態、事件、耗時與時間，以及重試是否已將其解決。Retry 按鈕的名稱包含事件及其傳送時間，等待時讀作 "Resending"。
- 圖表是一幅圖片，用一句話描述：事件數、仍有多少失敗，以及最繁忙的半小時。
- 結果以禮貌方式朗讀：重試成功或失敗、新端點、測試事件、複製、表單錯誤。
- 焦點永遠不會遺失：重試之後從 Retry 按鈕移到新的嘗試，傳送測試事件之後移到測試投遞，面板關閉之後移到膠囊，當投遞列持有焦點而端點發生變化時，移到新端點的所選投遞。建立失敗時焦點留在送出按鈕上。
- 焦點環只在鍵盤焦點時顯示；在列上，它採用選取底板的形狀。

## 動態效果

- **選取**：選取底板沿臨界阻尼彈簧滑向新的列（約 550 ms），並跟隨在它下面移動的列。端點的名稱、詳情與圖表標題以短暫的模糊交替替換；長條依序彈到新的高度（間隔 4.2 ms，從第一根到最後一根約 200 ms）。下半部交叉淡化，新的一半上升 6 px；舊的一半立即離開版面，因此它下方的內容不會跳動。
- **檢查**：標題、回應與面板以短暫的模糊交替替換。舊的回應在淡出期間被抬出版面，因此替換過程中分頁不會移動。
- **重試**：按鈕顯示載入動畫與 "Resending"，同時寬度以彈簧過渡到新標籤的寬度。新嘗試的列長出（平滑彈簧，80 ms 後開始改變不透明度），並帶有維持 1.4 秒、再以 1.2 秒淡去的閃爍；"Resolved" 在 Retry 原來的位置從模糊中出現；成功率中變化的數字每次替換一位，由右至左，間隔 30 ms；約 500 ms 之後選取項隨之移動。
- **新增端點**：膠囊沿同一條彈簧長成整寬的面板，加號變成關閉叉號，面板展開到一半時表單淡入。金鑰從柔和的模糊中顯影。新的列在頂端長出，並保持閃爍直到面板關閉。
- **滑過圖表**：指標下的長條變亮，其餘的長條在 160 ms 內變暗到 45%；標題立即顯示那半個小時。
- 減少動態效果只保留短促的淡入淡出：底板、長條、列與面板立即就位，數字原地變化。關閉動態效果則會立即套用每一次變化。

## 相關

- API keys、Integrations 與 Env variables，其他幾個開發者區塊。
- `Badge`、`Button`、`CopyButton`、`Input`、`Checkbox` 與 `SegmentedControl`，即組成本區塊的控制項。
- `JsonViewer`，以樹狀方式瀏覽較大的 JSON 文件。
