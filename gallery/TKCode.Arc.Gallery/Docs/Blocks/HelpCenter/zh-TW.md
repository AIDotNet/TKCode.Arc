---
title: Help center
summary: 幫助中心首頁，有搜索、主題和熱門文章；文章頁有路徑導航、可跳到各節的目錄、相關文章，以及“這篇文章有幫助嗎”的提問。
source-hash: c7e12d080065
---

<Demo name="Preview" />

## 何時使用

`HelpCenter` 是產品幫助文章的首頁。搜索框在輸入時就能找到文章，主題把文章分組，閱讀最多的文章列在前面。打開文章後，有返回所在主題的路徑導航、可跳到各節的目錄、相關文章，以及“這篇文章有幫助嗎？”的提問，回答“否”後會詢問缺了什麼。

- **幫助網站或應用內幫助頁**：幾十到幾百篇文章。
- **產品知識庫**：給客戶或自己的團隊使用。
- **按任務分組的上手指南**。

## 何時不用

- **頁面角落裡的小幫助面板**：使用 `SupportWidget`。
- **單篇長文檔**：使用 `Markdown` 並自己做目錄。
- **數千篇文檔的全文搜索**：在服務器上搜索，用自己的列表顯示結果。

## 用法

```xml
<arc:HelpCenter x:Name="Help" Title="How can we help?" Height="680" />
```

```csharp
Help.FeedbackGiven += (_, e) => api.Rate(e.Article.Id, e.IsHelpful, e.Comment);
Help.Categories.Add(new HelpCenterCategory("billing", "Billing", "Invoices and plans", ArcIcons.CreditCard));
Help.Articles.Add(new HelpCenterArticle("refunds", "billing", "How refunds work", "When the money comes back.", markdown)
{
    IsPopular = true,
    UpdatedAt = DateTimeOffset.Now,
});
```

文章使用 Markdown。正文中的 `##` 和 `###` 標題構成目錄。`IsPopular` 讓文章出現在首頁，閱讀時間按正文計算。搜索要求查詢中的每個詞都出現在標題、摘要或正文中，標題匹配排在前面；按 `Enter` 打開第一條結果。首次加載時沒有文章，會顯示一組示例。

`SearchText`、`CurrentCategory` 和 `CurrentArticle` 是雙向的，`View` 表示正在顯示哪一頁。`OpenArticle`、`OpenCategory`、`GoHome` 和 `GoBack` 可用程式碼導航。請給區塊一個高度，讓頁面在其中滾動。

### 窄欄

<Demo name="Narrow" />

## API 參考

<!-- api: HelpCenter -->

<!-- api: HelpCenterView -->

<!-- api: HelpCenterCategory -->

<!-- api: HelpCenterArticle -->

<!-- api: HelpCenterArticleEventArgs -->

<!-- api: HelpCenterFeedbackEventArgs -->

## 無障礙

- 首頁標題是頁面標題；主題、列表和目錄都是有名稱的組，每一行都是以標題和閱讀時間命名的按鈕。
- 搜索結果的數量和打開的文章會禮貌地讀出；路徑導航是一排按鈕。
- 反饋提問保持焦點不動，並讀出感謝。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| `/` | 聚焦搜索框（首頁和結果頁）。 |
| 搜索框中的 `Enter` | 打開第一條結果。 |
| `Escape` | 在文章或主題中返回上一級；在搜索框中清空內容。 |
| `Alt+←` | 返回上一級。 |

## 動效

- 頁面横向滑動：深入時向前，返回時向後。
- 反饋區以短距離上移切換內容。
- 減少動效時滑動改為淡入淡出；關閉動效時頁面立即切換。

## 響應式

- 寬度小於 720 px 時（`:narrow`，`NarrowWidth`），主題排成一列，目錄隱藏，路徑導航會換行。
- 更寬時，主題最多排成三列，目錄顯示在文章旁邊（200 px，`TocWidth`）。

## 相關元件

- `SupportWidget`：任何頁面角落裡的幫助。
- `Markdown`：顯示單篇文章。
- `SearchResults`：帶篩選的工作區搜索頁。
