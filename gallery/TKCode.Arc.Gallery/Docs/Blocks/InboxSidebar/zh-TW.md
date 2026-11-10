---
title: 收件匣側邊欄
summary: 郵件應用程式的側邊欄：寫信按鈕、帶未讀數的資料夾、彩色標籤和儲存空間條，可收起為圖示列。
source-hash: a757b7f2b722
---

<Demo name="Preview" />

## 何時使用

`InboxSidebar` 是郵件或訊息應用程式的左側欄。頂部是把側邊欄收起為圖示列的按鈕、信箱名稱和寫信按鈕。下面是資料夾，每個都有圖示和計數；未讀郵件的計數會讓這一列更醒目，而草稿這類一般計數不會。標籤在一個可摺疊的標題下，每個標籤有一個取自主題色盤的彩色圓點，清單最後是「New label」一列。底部顯示已用的儲存空間和登入的帳號。在圖示列上，圖示、圓點、寫信按鈕的筆和頭像都會保留，計數變成小圓點。它以 `Sidebar` 為基礎建構。

- **郵件用戶端**和共用收件匣：人們整天在資料夾和標籤之間切換。
- **客服台和工單佇列**：資料夾是佇列，標籤是主題。
- **任何帶「新增」操作和篩選檢視的應用程式**，例如訊息工具或審核佇列。

## 何時不用

- **沒有計數或新增操作的一般應用程式導覽**：使用 `Sidebar`。
- **文件**：使用 `DocsSidebar`，它帶搜尋和版本切換。
- **對話清單**：把郵件放在側邊欄旁邊的頁面中。

## 用法

```csharp
mail.Folders.Add(new InboxSidebarFolder("inbox", "Inbox", ArcIcons.Inbox, 12));
mail.Folders.Add(new InboxSidebarFolder("drafts", "Drafts", ArcIcons.FileText, 3, isCountUnread: false));
mail.MailLabels.Add(new InboxSidebarLabel("clients", "Clients", InboxSidebarLabelTone.Series1, 4));
mail.ComposeRequested += (_, _) => OpenComposer();
mail.ItemInvoked += (_, e) => ShowFolder(e.Key);
```

資料夾和標籤都是帶屬性的物件：改變 `Count`，對應的列就會跟著變。超過 999 的計數顯示為「999+」。沒有資料夾和標籤時，側邊欄顯示一個虛構的信箱。

<Demo name="OwnData" />

### 圖示列

`IsCollapsed` 切換到圖示列，面板按鈕的作用相同。`ResizeMode="Push"` 讓頁面隨寬度彈簧移動，預設的 `Overlay` 讓側邊欄從頁面上方滑過。

<Demo name="Rail" />

## API 參考

<!-- api: InboxSidebar -->

## 無障礙

- 側邊欄是名為「Mail」的導覽區域。方向鍵在列之間移動，Enter 或空白鍵開啟一列。
- 有未讀郵件的資料夾在名稱後加上「unread」。面板按鈕名為「Collapse sidebar」或「Expand sidebar」，寫信按鈕在圖示列上保留名稱，並顯示提示。

## 動效

- 寬度在展開的側邊欄和圖示列之間以彈簧變化，同時文字淡出；標記在列之間滑動；標籤以高度彈簧摺疊。
- 減少動效時文字快速淡出，不用彈簧；關閉動效時一切立即改變。

## 響應式

- 展開的側邊欄寬 240 px，圖示列寬 64 px。清單自行捲動，底部固定在最下方。
- 視窗較窄時，可從圖示列開始（`IsCollapsed="True"`），或把側邊欄放進 `Drawer`。

## 相關元件

- `Sidebar`：本區塊的基礎；以及 `DocsSidebar`。
- `NotificationCenter` 用於提醒清單，`SelectionToolbar` 用於對選取的郵件執行批次操作。
