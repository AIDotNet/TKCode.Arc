---
title: Blog grid
summary: 一個部落格索引頁，包含精選文章、分類篩選、文章卡片、分頁和就地閱讀器。
source-hash: 1f96c95cf4d9
---

<Demo name="Preview" />

## 何時使用

將 `BlogGrid` 用作部落格、期刊、新聞頁或資源中心的索引頁，適合分類不多且有一篇精選文章的情境。

## 何時不用

- **帶版本號的發行說明**：使用 `ChangelogFeed` 區塊。
- **需要排序和搜尋的大型封存**：使用資料表格。

## 用法

```csharp
grid.Posts = posts;
grid.Categories = ["Product", "Design", "Engineering", "Company"];
grid.HrefSelector = post => $"https://example.com/blog/{post.Id}";
grid.PostOpened += (_, e) => analytics.Track("post", e.Post.Id);
```

文章按最新優先排列。只有 `Category` 與篩選項相符的文章才能被篩選，`Categories` 決定篩選項；「All」排在最前。

### 連結或閱讀器

設定 `HrefSelector` 後，卡片會引發 `PostOpened` 並開啟連結，除非處理常式將事件標記為已處理。未設定時，卡片開啟就地閱讀器；`ReadingPost` 表示目前開啟的文章，`OpenPost` 和 `CloseReader` 可在程式碼中操作。

<Demo name="Links" />

### 繫結篩選和頁碼

`Category` 和 `Page` 支援雙向繫結，應用程式可以把它們放進路由。選擇篩選項會把頁碼重設為 1。

```xml
<arc:BlogGrid Category="{Binding Category}" Page="{Binding Page}" PageSize="9" ShowFeatured="False" />
```

### 窄容器

<Demo name="Narrow" />

## API 參考

<!-- api: BlogGrid -->

<!-- api: BlogPostEventArgs -->

## 無障礙

- 每張卡片是一個可聚焦的連結，名稱包含標題、分類、日期和作者，只讀一次。
- 篩選項和頁碼是切換按鈕；頁碼按鈕的名稱取自 `PageLabelFormat`。
- 閱讀器把焦點移到返回按鈕，關閉後焦點回到卡片。封面使用 `ImageAlt`；頭像是裝飾性的。
- `Tab` 在篩選項、卡片和分頁間移動；`Enter` 開啟聚焦的文章。

## 動效

- 篩選和頁碼醒目標示沿 morph 彈簧滑動。
- 切換篩選時格線交叉淡入並上升 10 px；翻頁時沿方向滑動 24 px：舊頁先在 180 ms 內離開，新頁再以 smooth 彈簧和 240 ms 淡入到達。高度跟隨 smooth 彈簧。
- 開啟文章時封面沿 morph 彈簧飛入閱讀器，關閉時飛回，索引的其餘部分同時淡入。閱讀器內文延遲 120 ms 後上升 12 px。
- 滑鼠懸停時封面在 480 ms 內放大 3%。
- 減弱動效時位移和飛行換成單純淡入淡出。關閉動效時立即完成。

## 響應式

- 區塊跟隨自身寬度：900 px 以上三欄，540 px 以上兩欄，其餘一欄。
- 680 px 及以下時精選文章的封面堆疊在文字上方，間距收緊。540 px 及以下時「上一頁」和「下一頁」收起為箭頭。
- 放不下時篩選項可橫向捲動。

## 相關元件

- `ChangelogFeed` 區塊：帶日期的發行說明。
- `Pagination`：單獨使用的分頁。
