---
title: Activity feed
summary: 工作區裡發生了什麼，按天分組：誰做了什麼，每類動作有自己的圖標，可按類型和人員篩選；新動態在實時模式下滑入，暫停時在提示條後面等待。
source-hash: 8d85db799894
---

<Demo name="Preview" />

## 何時使用

`ActivityFeed` 顯示工作區裡發生的事：誰評論、修改、發布或加入，最新的在前，按天分組。標籤按類型篩選，選擇框按人員篩選。實時模式下，新動態一發生就從頂部滑入；暫停時，它們在“新動態”提示條後面等待，列表不會在讀者眼前移動。

- **項目或工作區首頁**：在內容旁邊顯示最新動態。
- **審計視圖**：只看某個人或某類變化。
- **發布日誌**：構建和發布實時到達。

## 何時不用

- **需要處理的個人收件箱**：使用 `NotificationCenter` 或 `InboxTriage`。
- **聊天**：使用 `ChatThread`。
- **需要搜索的數萬條事件**：使用帶篩選和分頁的 `Table`。

## 用法

```xml
<arc:ActivityFeed x:Name="Feed" Title="Workspace activity" KindFilter="Deploy" />
```

```csharp
Feed.Push(new ActivityFeedItem("e42", DateTimeOffset.Now, "Rosa Lind", "commented on Launch plan", ActivityKind.Comment)
{
    Target = "Spring release",
    Detail = "Can we move the screenshots first?",
});
```

`Items` 保存全部動態，順序不限。`Push` 在事情發生時加入一項：`IsLive` 時直接進入列表（如果通過篩選）；暫停時它會等待，`PendingCount` 增加，點擊提示條或調用 `ShowPending()` 後才加入。每一項有執行人（系統事件為 null）、動作、對象說明、決定圖標和篩選標籤的類型（`Comment`、`Change`、`Deploy`、`Member`、`Other`）、可選頭像、表示結果的 `Tone`，以及展開後顯示的可選 `Detail`。首次加載時沒有動態，會顯示一個示例工作區。

### 已篩選並暫停

<Demo name="Comments" />

`KindFilter` 和 `ActorFilter` 是雙向的，標籤和人員選擇框會設置它們。

## API 參考

<!-- api: ActivityFeed -->

<!-- api: ActivityFeedItem -->

## 無障礙

- 動態是一個以標題命名的 `Timeline` 區域；日期是標題，帶詳情的行可用 `Enter` 或 `Space` 展開。
- 實時標籤會說明是否在實時更新，以及按下後會怎樣。暫停時到達的新動態會讀出（“3 條新動態”）；發布失敗這類結果寫在動作文字裡，而不只靠顏色。
- 提示條和標籤都能用 `Tab` 到達；在時間線中用 `↑` / `↓` 在可展開的行之間移動。

## 動效

- 新動態從頂部滑入，下方的行隨之下移；行進入視野時，標記之間的線會畫出來。
- 實時圓點每 1.6 秒向外發出一圈柔和的光環。它運行在按幀循環上，離開屏幕、被隱藏或減少動效時會停止。
- “新動態”提示條從上方落下。減少動效時只用淡入淡出；關閉動效時變化立即發生。

## 響應式

- 動態最寬 640 px，時間線超過 560 px（`FeedMaxHeight`）後滾動。
- 在窄欄中，標籤和人員選擇框會換成兩行；時間線在自身較窄時會收緊排版。

## 相關元件

- `Timeline`：單獨的按天分組列表。
- `NotificationCenter`：帶已讀狀態的個人更新。
- `InboxTriage`：每項都需要決定的事項。
