---
title: Feature voting
summary: 一張小卡片，用來對幾個候選功能投票：選中一項時，它的票數會滾動，佔比條隨之移動；提交後卡片變成感謝頁並顯示結果。
source-hash: 1e2b391eed9f
---

<Demo name="Preview" />

## 何時使用

`FeatureVoting` 是一張只問一個問題的小卡片，例如“下一個做什麼？”，下面列出三到六個候選功能。訪客選中一項（或最多 `MaxVotes` 項），馬上看到自己的一票加進了票數，再按“投票”提交。之後卡片會表示感謝，保留結果，並提供“修改我的投票”。它適合放在更新日誌末尾、應用內公告、設置頁或側欄這類放不下完整反饋看板的地方。

- **快速投票**：讓已登錄的用戶決定下一個功能。
- **排優先級**：每人可以選兩到三項（`MaxVotes`）。
- **先投後看**：投票前不顯示票數（`ShowsResultsBeforeVoting="False"`）。

## 何時不用

- **開放的想法或很長的列表**：使用 `FeedbackBoard`，它有狀態、評論和提交新請求的表單。
- **評分或滿意度**：滑塊或評分控件問的是另一類問題。
- **需要防作弊的公開匿名投票**：卡片只收集選擇，計票由你的服務器負責。

## 用法

```xml
<arc:FeatureVoting Title="What should we build next?" x:Name="Poll" MaxVotes="1">
    <arc:FeatureVotingOption Id="offline" Title="Offline mode" Description="Work without a connection." Votes="412" />
    <arc:FeatureVotingOption Id="calendar" Title="Calendar view" Votes="296" />
</arc:FeatureVoting>
```

```csharp
Poll.Voted += (_, e) =>
{
    var ids = e.Options.Select(o => o.Id);   // send the choice to your server
};
```

沒有選項時卡片顯示一組示例。`Votes` 是其他人的票數；選中一項時，卡片會在其上加上當前訪客的一票，`TotalVotes` 與 `SelectedOptions` 隨之更新。直接修改 `Votes` 即可顯示實時票數。

### 多選與隱藏結果

<Demo name="PickTwo" />

`MaxVotes` 大於 1 時，說明下方的提示會顯示已選數量；超過上限的選擇會被拒絕，並讀出提示。`ShowsResultsBeforeVoting="False"` 會在提交前隱藏票數和佔比條。

### 用程式碼提交

`Toggle(option)` 選中或取消一項，`Submit()` 提交投票（沒有選擇時不做任何事），`ChangeVote()` 重新打開選擇。`IsSubmitted` 是雙向的，頁面可以為已經投過票的人直接顯示感謝頁。

## API 參考

<!-- api: FeatureVoting -->

<!-- api: FeatureVotingOption -->

<!-- api: FeatureVotingVotedEventArgs -->

## 無障礙

- 選項是一個以標題命名的組。每個選項是一個切換按鈕，名稱會讀出標題、票數、佔比，選中時還會讀出“你的投票”。
- 選擇、被拒絕的選擇和感謝頁都會禮貌地讀出。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| `Tab` | 移到選項，再移到按鈕。 |
| `↑` / `↓`、`Home` / `End` | 在選項之間移動。 |
| `Enter` / `Space` | 選中或取消當前選項。 |
| `Ctrl+Enter` | 提交投票。 |

## 動效

- 選中一項時，票數逐位滾動，每個佔比條以平滑彈簧移到新長度；滑動中途收到服務器的新票數會保留速度。
- 勾號彈入被選中的選項；標題區和按鈕以短距離上移切換，感謝頁的勾號會自己畫出來。
- 減少動效時，佔比條和數字直接到位，只保留短淡入淡出；關閉動效時一切立即變化。

## 響應式

- 卡片最寬 420 px（`CardMaxWidth`），更窄時隨容器伸縮。標題和說明會換行，票數始終在右側。
- 卡片沒有自己的斷點；在窄欄中請把說明控制在一行。

## 相關元件

- `FeedbackBoard`：帶狀態和評論的完整請求看板。
- `Reactions`：內容下方的輕量表情反饋。
