---
title: Webhook tester
summary: Webhook 測試器：選擇事件、編輯 JSON 負載並傳送帶簽章的測試事件，觀察它從已簽章、已傳送到端點作出回應的全過程，檢視回應，並在最近投遞清單中重試失敗的投遞。
source-hash: b80e9fc5bad2
---

<Demo name="Preview" />

## 何時使用

`WebhookTester` 是傳送 Webhook 的產品（計費、驗證、郵件、CI）中的「測試端點」頁面。使用者選擇一個事件，調整它的 JSON 負載並傳送帶簽章的測試事件；區塊會顯示它從已簽章、已傳送到收到回應的過程，並在清單中保留最近的幾次嘗試。

- **上線前測試。** 客戶確認自己的處理程式會回傳 2xx，並且會驗證簽章。
- **整合引導。** 區塊在應用程式內以端點的密鑰簽章，因此處理程式收到的內容與正式環境完全一致。
- **重放失敗的投遞。** 失敗的列帶有 Retry 按鈕，會以新的時間戳記和簽章重新傳送同一份內容。

## 何時不用

- **管理大量端點、篩選器和事件訂閱。** 這類情境請使用 Webhooks 區塊，並從那裡連結到本區塊來測試某個端點。
- **通用 HTTP 用戶端。** 本區塊一律傳送帶簽章的 POST 與 JSON 本文，沒有方法或標頭編輯器。
- **成千上萬列的事件記錄。** 清單只保留最近 20 次嘗試。分頁記錄應交給資料表格。

## 用法

本區塊衍生自 Avalonia 的 `TemplatedControl`。給它端點、使用者可選的事件（每個包含類型和預設 JSON 負載）以及端點的簽章密鑰。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:WebhookTester x:Name="Tester"
                       Endpoint="https://api.acme.dev/webhooks/billing"
                       Secret="{Binding SigningSecret}" />
</UserControl>
```

```csharp
Tester.Events.Add(new WebhookTestEvent("invoice.paid", """{"id":"evt_1","type":"invoice.paid","amount":4900}"""));
Tester.Events.Add(new WebhookTestEvent("invoice.failed", """{"id":"evt_2","type":"invoice.failed","amount":4900}"""));
```

`Events` 為空時，區塊會提供五個範例事件。選擇事件會載入它的負載：合法的 JSON 會被格式化，其他內容原樣保留。`SelectedEvent` 與 `Payload` 是雙向的，頁面可以讀取或預設它們。

### 在你的伺服器上傳送

區塊在應用程式內對本文簽章（對 `timestamp.body` 做 HMAC SHA-256，以 `t=…,v1=…` 放入 `SignatureHeader`），但它自己絕不會向客戶的端點發出請求：瀏覽器或桌面應用程式不應代表客戶呼叫任意網址。`SendAction` 收到已簽章的 `WebhookTestRequest` 並交給你的伺服器，由伺服器真正送出 POST，再回傳 `WebhookTestResponse`。伺服器可以用同一份本文重新簽章，核對兩個簽章是否一致。

```csharp
Tester.SendAction = async (request, cancellation) =>
{
    var result = await api.PostAsJsonAsync($"endpoints/{endpointId}/test", request, cancellation);
    return await result.Content.ReadFromJsonAsync<WebhookTestResponse>(cancellation)
           ?? new WebhookTestResponse { Error = "Empty answer" };
};
```

`Status` 為 0 並帶有 `Error`（端點逾時則同時設定 `TimedOut`）表示沒有收到回應；擲回例外同樣算作失敗的投遞。無論哪種情況，該投遞都會以失敗狀態進入清單，時間軸以紅色結束，並播報失敗。`RetryAction` 用於重新傳送失敗的投遞；未設定時，Retry 使用 `SendAction` 並帶上新的簽章。

<Demo name="Failing" />

未設定 `SendAction` 時，每次投遞都是模擬的：經過 `SimulatedLatency` 後回傳 200，播報內容會說明沒有真正傳送任何請求。啟用 `AutoPlay`（預設）時，模擬狀態的區塊在捲動到一半進入視窗約一秒後傳送一個事件，除非使用者已經操作過它。也可以在程式碼中呼叫 `SendAsync` 和 `RetryAsync`。

### 其他產品

`Source` 給出傳送事件的產品名稱（副標題顯示為 "Signed test events from Relay Mail"），並可透過 `MarkFactory` 提供自己的 16 px 標誌；沒有時，區塊會畫一個帶首字母的圓角方塊。把 `SignatureHeader` 設為你的產品所用的標頭。

<Demo name="OtherProduct" />

### 負載編輯器

編輯器在輸入時為 JSON 上色，顯示行號，並用文字說明負載能否解析以及位元組數（"Valid JSON · 327 bytes"；無法解析時會給出行號和原因）。Enter 會保持縮排，並在 `{` 或 `[` 之後加深縮排。**Format** 會格式化合法的 JSON。無法解析的負載不能傳送：編輯器會取得焦點並播報問題。端點必須是 https；`localhost` 和迴路位址允許使用 http。

### 窄容器

區塊會跟隨自身寬度。寬度達到 680 時，請求（端點、事件、密鑰、負載）與回應（時間軸、分頁）並排顯示，編輯器會拉伸到與回應欄等高；更窄時上下堆疊。寬度低於 420 時，清單中的投遞時間會隱藏，內距從 20 收緊為 16。

<Demo name="Narrow" />

### 文案

區塊顯示或播報的每個詞都在 `Labels` 中，它是帶英文預設值的 `WebhookTesterLabels`。包含數值的文案是函式，因此語序可以改變：

```csharp
Tester.Labels = new WebhookTesterLabels
{
    Send = "Send",
    FailedCount = count => $"{count} did not arrive",
};
```

## API 參考

<!-- api: WebhookTester -->

<!-- api: WebhookTestEvent -->

<!-- api: WebhookTestRequest -->

<!-- api: WebhookTestResponse -->

<!-- api: WebhookTestDelivery -->

<!-- api: WebhookTesterSource -->

<!-- api: WebhookTesterLabels -->

## 無障礙

- 每個欄位都有可見標籤。負載編輯器以事件命名（"Payload for order.paid"）；JSON 無法解析時，它的說明文字是問題所在及行號，頁尾也會顯示。
- 簽章密鑰在顯示之前對輔助技術隱藏；顯示按鈕是一個切換開關。
- 回應分頁是帶漫遊焦點的分頁清單；面板可取得焦點且唯讀。
- 投遞列是按鈕，以事件、結果和時間命名，位於一個清單中；選取的列處於選取狀態。狀態標籤把狀態碼或文字與顏色搭配。
- 每次傳送及其結果都會禮貌地播報；未設定 `SendAction` 時附加 "Simulated, nothing was sent."。

### 鍵盤

- **Ctrl+Enter**（Mac 上為 **Command+Enter**）在區塊內任何位置傳送；端點輸入框中按 **Enter** 同樣會傳送。
- 負載編輯器中按 **Enter** 換行並保持相同縮排。
- **左**、**右**方向鍵以及 **Home**、**End** 在 Body、Headers、Request 分頁之間移動。
- **Tab** 依序經過端點、事件清單、顯示按鈕、複製按鈕、負載、Format、Send、目前分頁、回應面板，以及每個失敗列的 Retry 按鈕。

## 動態效果

- 時間軸的圓圈以短促的淡變切換；兩步之間的線從左向右填滿，請求在途時，最後一段線上有一個圓點循環移動，直到收到回應。
- 新的投遞出現在清單頂端並被選取；回應文字立即替換。
- 減少動態效果會保留最終狀態，去掉圓點和填滿的行進過程；關閉動態效果則所有變化立即生效。

## 相關

- `Webhooks`（管理多個端點的主控台）和 `ApiKeys`，同屬 Developer 區塊。
- `CopyButton`、`Button`、`Input` 和 `Select`，組成本區塊的控制項。
