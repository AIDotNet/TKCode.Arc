---
title: Feedback board
summary: 一個公開的功能請求看板：按狀態篩選，按熱門、最新或趨勢排序，投票時票數滾動，打開請求在評論中討論，也可以提出新的請求。
source-hash: bd175dd43029
---

<Demo name="Preview" />

## 何時使用

`FeedbackBoard` 是一個公開的功能請求看板。大家可以看到每個想法的進度（審核中、已計劃、進行中、已完成），為想要的請求投票，打開請求閱讀和討論，並提出新的請求。列表可按票數、最新或趨勢排序；投票或排序改變順序時，各行會滑到新的位置。

- **產品反饋頁**：放在應用裡或網站上。
- **公開路線圖**：只顯示已計劃和進行中的請求。
- **團隊內部的想法看板**。

## 何時不用

- **在幾個選項中快速投票**：使用 `FeatureVoting`。
- **帶字段和附件的問題報告**：使用提交問題的表單。
- **一對一的私密支持**：使用 `SupportConversation`。

## 用法

```xml
<arc:FeedbackBoard x:Name="Board" Title="Product feedback" SortOrder="Top">
    <arc:FeedbackRequest Id="r1" Title="Dark mode for the mobile app" Votes="214" Status="Planned" />
</arc:FeedbackBoard>
```

```csharp
Board.VoteChanged += (_, e) => api.Vote(e.Request.Id, e.Request.HasVoted);
Board.RequestSubmitted += (_, e) => api.Create(e.Request.Title, e.Request.Description);
```

看板會直接修改 `FeedbackRequest` 對象：投票更新 `Votes` 和 `HasVoted`，評論替換 `Comments`（`CommentsChanged` 會給出具體變化），新請求以“審核中”加入，並帶上作者的一票。請求計劃或發布時，由你這邊修改 `Status`。首次加載時沒有請求，會顯示一組示例。

### 路線圖視圖

<Demo name="Roadmap" />

`StatusFilter`、`SortOrder` 和 `SelectedRequest` 都是雙向的。`CanSubmit="False"` 隱藏“提出功能建議”表單。在程式碼中，`ToggleVote(request)` 和 `Submit(title, description)` 與按鈕的作用相同。

## API 參考

<!-- api: FeedbackBoard -->

<!-- api: FeedbackRequest -->

<!-- api: FeedbackRequestEventArgs -->

<!-- api: FeedbackStatus -->

<!-- api: FeedbackSort -->

## 無障礙

- 每一行是一個切換按鈕，名稱包括標題、狀態、票數和評論數；其中的投票按鈕會說明票數以及你是否已投票。
- 狀態總是以文字寫在標籤上，不只靠顏色。投票和新請求會禮貌地讀出。
- 表單的標題為空時會顯示錯誤；`Escape` 關閉表單，`Ctrl+Enter` 提交。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| `Tab` | 依次移過標題區、篩選、各行及其投票按鈕，以及打開的請求。 |
| `Enter` / `Space` | 打開請求、投票或按下標籤。 |
| 表單中的 `Ctrl+Enter` | 提交請求。 |
| 表單中的 `Escape` | 關閉表單。 |

## 動效

- 投票或新的排序改變順序時，各行以平滑彈簧從原位置滑到新位置；滑動中途再次變化會保留速度。
- 票數逐位滾動；表單以高度變化打開和關閉。
- 減少動效時各行直接到位，只保留淡入淡出；關閉動效時一切立即變化。

## 響應式

- 寬度在 760 px 及以上時，打開的請求顯示在列表旁邊（380 px，`DetailWidth`）。更窄時（`:stacked`）它取代列表，並帶返回按鈕。
- 在窄欄中，篩選和排序會分成兩行。

## 相關元件

- `FeatureVoting`：對幾個選項快速投票。
- `CommentThread`：單獨的討論。
- `HelpCenter`：提問前可以先看的答案。
