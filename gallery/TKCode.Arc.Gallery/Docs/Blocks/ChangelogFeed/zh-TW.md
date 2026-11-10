---
title: Changelog feed
summary: 可篩選、可就地展開、可按月捲動瀏覽的發行說明。
source-hash: c24458c39c41
---

<Demo name="Preview" />

## 何時使用

將 `ChangelogFeed` 用於按月分組的產品更新日誌或發行說明頁，支援按變更類型篩選，並在頁首提供電子郵件訂閱。

## 何時不用

- **沒有篩選的簡短事件清單**：使用 `Timeline`。
- **沒有日期的可展開內容**：使用 `Accordion`。
- **成千上萬條說明**：項目沒有虛擬化，請按需載入較早的月份。

## 用法

```csharp
feed.Months = [new ChangelogMonth { Key = "2026-03", Label = "March 2026", ShortLabel = "Mar" }];
feed.Entries =
[
    new ChangelogEntry
    {
        Id = "tags", Month = "2026-03", Date = "Mar 3", Version = "2.1.0", Kind = ChangelogKind.New,
        Title = "Tags for every note", Summary = "Group notes across folders.",
        Details = ["Type a hash sign to add a tag."],
        Media = new ChangelogCode { File = "tags.json", Code = "{ }" }
    }
];
feed.Subscribed += async (_, e) => await list.AddAsync(e.Email);
```

<Demo name="CustomData" />

項目按鍵歸屬某個月並列在其下，最新優先。沒有相符項目的月份會從月份列和清單中移除。`DefaultOpen` 選擇初始展開的項目；未設定時展開最新一條。`ToggleEntry`、`ToggleFilter`、`ClearFilters` 和 `JumpToMonth` 可在程式碼中完成相同操作。

### 訂閱

膠囊先變成電子郵件輸入框，再變成帶復原的確認。有效地址引發 `Subscribed`；復原引發 `SubscriptionUndone`。區塊本身不傳送任何內容，請在處理常式中把地址提交到郵寄清單。

### 媒體

`Media` 是 `ChangelogPhoto`（裁切為 16:10、帶說明的圖片）或 `ChangelogCode`（帶複製按鈕的程式碼片段）。面板在項目首次展開時才建立。

### 窄容器

<Demo name="Narrow" />

## API 參考

<!-- api: ChangelogFeed -->

<!-- api: ChangelogSubscribeEventArgs -->

## 無障礙

- 每一列是一個按鈕，名稱包含日期、類型、標題和摘要，說明文字指出是否已展開。
- 篩選標籤是切換按鈕，即時區域會讀出顯示的更新數量。
- 月份按鈕的名稱取自 `JumpFormat`。清單可取得焦點以便用按鍵捲動。
- `Escape` 取消訂閱輸入框並回到膠囊；`Enter` 提交。

## 動效

- 列沿 smooth 彈簧展開，沿 `{ visualDuration: 0.28, bounce: 0 }` 收起，箭頭沿 snappy 彈簧旋轉。面板內容延遲 60 ms 後從 4 px 模糊中上升 8 px。
- 切換篩選時列淡出（縮放 .98、模糊 2、180 ms），其餘列沿彈簧移到新位置。計數沿 snappy 彈簧捲動。
- 月份標籤隨捲動方向上升 14 px，月份膠囊沿 morph 彈簧滑動。月份按鈕沿 smooth 彈簧捲動。
- 訂閱膠囊變形寬度；內容以縮放 .96 和模糊 2 切換；無效地址抖動 480 ms；勾號在 420 ms 內繪製。
- 減弱動效時只用短暫淡入淡出，沒有抖動和位移。關閉動效時立即完成。

## 響應式

- 區塊跟隨自身寬度，最寬 920 px。
- 640 px 及以下時頁首堆疊，日期和版本移到文字上方，清單高度從 560 px 降到 520 px。
- 480 px 及以下時隱藏月份計數、年份和顯示計數，讓月份導覽保持一行。
- 懸停樣式只對滑鼠或觸控筆生效。

## 相關元件

- `Timeline`：簡短的縱向事件清單。
- `Accordion`：沒有日期的可展開內容。
- `CopyButton`：程式碼片段的複製操作。
