---
title: Agent approvals
summary: 智慧體想執行、需要人點頭的操作佇列：每個請求附有智慧體的理由、事實、風險和選填備註，可核准或拒絕，風險高的還要多做一次確認。
source-hash: b3bfea613306
---

<Demo name="Preview" />

## 何時使用

`AgentApprovals` 是人對 AI 智慧體想做、但不應獨自去做的事情說是或否的地方：向公司外寄送郵件、付款、發布到正式環境、刪除記錄。等待的請求列在一側並標出風險。選取的請求在另一側完整顯示：智慧體想做什麼、為什麼、操作的事實（收件人、金額、環境）、風險有多大及原因、一個給智慧體留言的輸入框，以及 Deny 和 Approve。高風險請求要求使用者先勾選已核對詳情，Approve 才能使用。已決定的請求離開佇列，下一個請求隨即開啟。

- **智慧體的人工把關**：涉及資金、客戶或正式系統的操作。
- **團隊共用的審核收件匣。**

## 何時不用

- **觀看智慧體正在做什麼。** 請使用 `Agent run`。
- **人與人之間的審核**（報帳、請假）。表單或表格更合適。
- **就地確認一個操作。** 請使用 `Dialog` 或 `Confirm morph`。

## 用法

區塊衍生自 Avalonia 的 `TemplatedControl`。不設定任何屬性時，它顯示範例請求並在本地作出決定。在應用程式中，給它你的請求，並把每個決定送到你的後端：

```xml
<arc:AgentApprovals Requests="{Binding PendingApprovals}" />
```

```csharp
queue.DecisionHandler = (request, decision, note, cancellation) =>
    api.AnswerAsync(request.Id, decision == AgentApprovalDecision.Approved, note, cancellation);
queue.Decided += (_, e) => log.Add($"{e.Request.Title}: {e.Decision}");
```

### 請求與決定

`AgentApprovalRequest` 包含智慧體、決定圖示的類型（郵件、付款、發布、資料修改、權限、其他）、標題、放在 `Context` 中的智慧體理由、作為標籤和值的 `Details`、帶 `RiskNote` 的 `Risk`，以及提出的時間。`DecisionHandler` 執行時 Approve 顯示載入圈；如果它擲出例外，請求保留，訊息顯示在按鈕下方。已決定的請求會從螢幕上的佇列中離開，但你的集合不會被修改：可以在後端確認後從中移除，或保留作為記錄。`Reset` 會把所有已決定的請求放回佇列。

<Demo name="Backend" />

## API 參考

<!-- api: AgentApprovals -->

<!-- api: AgentApprovalRequest -->

<!-- api: AgentApprovalDetail -->

<!-- api: AgentApprovalsLabels -->

## 無障礙

- 佇列是名為 "Waiting for approval" 的區域；清單和選取的請求都有標題。
- **Tab** 可到達清單列、備註、確認框（高風險時）和按鈕。**Up** 和 **Down** 在清單中移動選取，**Home** 和 **End** 跳到兩端；**Enter** 或 **Space** 選取一列。
- 清單列作為清單項目讀出標題、智慧體和風險，並報告是否被選取。
- 作出決定後，一個禮貌式即時區域會播報結果，焦點移到下一個請求。
- 風險由文字、圖示和底色表示，從不只靠顏色。

## 動效

- 選取另一個請求時，風險面板的底色在 240ms 內變化；清單列在指標停留和選取時變色。
- 減少動效和關閉動效時顏色變化立即生效。

## 響應式

- 佇列最寬 920，小於這個寬度時跟隨容器。
- 寬度在 680 或以下時清單位於請求上方。
- 清單中的標題最多兩行；詳情自動換行。

## 相關元件

- `Agent tasks`，查看智慧體正在做的工作。
- `Agent run`，詳細查看一次執行。
- `Dialog`，就地確認一個操作。
