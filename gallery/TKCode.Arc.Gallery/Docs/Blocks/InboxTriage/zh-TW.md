---
title: Inbox triage
summary: 一個為快速處理而做的收件箱：滑動或按一個鍵即可歸檔、稍後提醒、分配或加標籤，底部的條可以撤銷任何一步，剩餘數量會一路滾到清空。
source-hash: a186fdaed653
---

<Demo name="Preview" />

## 何時使用

`InboxTriage` 是為清空而做的收件箱：工單、審核請求、消息或提醒，每一項只需要一個快速決定。每一行都可以通過滑動或一個按鍵來歸檔、稍後提醒、分配給同事或加標籤，每一步都能在底部的條上撤銷。滾動的計數和進度條顯示還剩多少，清空時會明確告訴你。

- **客服隊列**：一次處理一個工單。
- **審核或審批請求**：每項需要同意、稍後或交給別人。
- **手機上的通知整理**：滑動操作很自然。

## 何時不用

- **閱讀長消息**：請把列表和閱讀區配合使用；整理行只顯示主題和一行預覽。
- **一次處理數百項**：行沒有虛擬化，請給收件箱分頁。
- **對多項選擇批量操作**：使用帶選擇和工具欄的 `Table`。

## 用法

```xml
<arc:InboxTriage x:Name="Inbox" Title="Support inbox">
    <arc:InboxTriageItem Id="t1" Sender="Ines Duarte" Subject="Refund for order 4471" />
</arc:InboxTriage>
```

```csharp
Inbox.Triaged += (_, e) => api.Apply(e.Item.Id, e.Action, e.Value);   // Value is the teammate or the label
Inbox.Undone += (_, e) => api.Revert(e.Item.Id, e.Action);
```

處理過的項目會離開 `Items`，撤銷時回到原來的位置，並恢復原有的標籤和負責人。`Teammates` 和 `AvailableLabels` 提供“分配”和“標籤”列表。首次加載時收件箱為空，會得到一組示例項目、同事和標籤；設置 `UsesSampleData="False"` 可從空開始。

### 操作

| 操作 | 滑動 | 按鍵 | 結果 |
|---|---|---|---|
| 歸檔 | 向右 | `E` | 離開收件箱。 |
| 稍後提醒 | 向左 | `S` | 離開收件箱，並設置 `SnoozedUntil`（`SnoozeFor`，或明天 9:00）。 |
| 分配 | 向左 | `A` | 打開同事列表；項目離開並設置 `Assignee`。 |
| 標籤 | 向左 | `L` | 打開標籤列表；添加或移除標籤，項目保留。 |
| 撤銷 | | `Z` / `Ctrl+Z` | 撤回上一步。 |

同樣的操作也在每一行的“更多操作”菜單裡，並且可以用程式碼調用：`Archive`、`Snooze`、`Assign`、`ToggleLabel`、`Undo`、`OpenAssign`、`OpenLabels` 和 `MoveTo`。

### 窄欄且不顯示提示

<Demo name="Narrow" />

`ShowsShortcuts="False"` 隱藏按鍵提示。`UndoDuration` 設置撤銷條停留多久（為零時一直保留）。

## API 參考

<!-- api: InboxTriage -->

<!-- api: InboxTriageItem -->

<!-- api: InboxTriageEventArgs -->

## 無障礙

- 列表以標題命名；每一行以主題命名，並有包含全部操作的“更多操作”菜單，所以不需要手勢。
- 當前項目用一條強調色豎條標出，而不只是改變文字顏色。每一步都會讀出剩餘數量（“已歸檔：訂單 4471 退款。還剩 5 項。”）。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| `J` / `↓`、`K` / `↑` | 下一項或上一項。 |
| `Home` / `End` | 第一項或最後一項。 |
| `E`、`S`、`A`、`L` | 對當前項目歸檔、稍後提醒、分配、加標籤。 |
| `U` | 標為已讀或未讀。 |
| `Z` 或 `Ctrl+Z` | 撤銷。 |
| 列表中的 `↑` / `↓`、`Enter`、`Escape` | 選擇同事或標籤，或關閉列表。 |

## 動效

- 處理過的行朝操作的方向滑走，列表隨後合攏；撤銷的項目在原位回來。
- 計數逐位向下滾動，進度條增長，撤銷條從底部升起。
- 減少動效時，行改為淡出而不是滑走，計數不再滾動；關閉動效時變化立即發生。

## 響應式

- 區塊最寬 560 px，列表超過 520 px（`ListMaxHeight`）後滾動。
- 行內的主題和預覽各截斷為一行；在窄寬度下，標籤會換到發件人下方。
- 滑動支持觸摸、筆和鼠標拖動，所以在手機上用法相同。

## 相關元件

- `SwipeActions`：單獨的可滑動列表。
- `NotificationCenter`：只讀的更新列表。
- `Toast`：一條可撤銷的消息。
