---
title: 文件側邊欄
summary: 文件網站的側邊欄：版本切換、邊輸入邊篩選頁面的搜尋框、可展開的分組與頁面，目前頁面會被標出並捲動到可見處。
source-hash: 57ef5deb9eb8
---

<Demo name="Preview" />

## 何時使用

`DocsSidebar` 是文件網站或應用程式內說明中心的左側欄。頂部是產品名稱和一個用來切換文件版本的小按鈕。下面是搜尋框，讀者輸入時清單隨之縮小。頁面依帶標題的分組排列，分組可以摺疊，一個頁面也可以展開出子頁面。正在閱讀的頁面會被標出；無論它因點擊還是因你的路由而改變，所在的分組和上層頁面都會展開，清單也會捲動讓它保持可見。它以 `Sidebar` 為基礎建構，因此共用其選取標記、鍵盤模型和篩選功能。

- **產品文件**：包含指南、API 參考和更新紀錄，讀者會在許多頁面之間跳轉。
- **分版本的文件**：使用舊版本的讀者需要看到對應的頁面。
- **應用程式內的說明中心**：目前文章由連結決定，側邊欄需要跟隨。

## 何時不用

- **應用程式的主導覽**：使用 `Sidebar`，它帶圖示並可收起為圖示列。
- **信箱**：使用 `InboxSidebar`，它增加了寫信、計數和標籤。
- **一篇長文的各級標題**：在文章旁放一份錨點目錄比頁面清單更好讀。

## 用法

```csharp
docs.Sections.Add(new DocsSidebarSection("Guides",
    new DocsSidebarPage("guides/caching", "Caching", "New"),
    new DocsSidebarPage("guides/deploying", "Deploying")));
docs.CurrentKey = "guides/caching";
docs.PageInvoked += (_, e) => router.Open(e.Page.Key);
docs.VersionChanged += (_, e) => LoadPages(e.NewVersion);
```

頁面和分組也可以直接寫在 XAML 中。沒有 `Sections` 時，側邊欄顯示一個虛構的範例網站。`GetAdjacentPage(-1)` 和 `GetAdjacentPage(1)` 依閱讀順序給出上一頁和下一頁，可用於文章底部的連結。

<Demo name="OwnPages" />

## API 參考

<!-- api: DocsSidebar -->

## 無障礙

- 側邊欄是名為「Documentation」的導覽區域。方向鍵在列之間移動，Enter 或空白鍵開啟頁面，上層頁面會展開或收起其子頁面。
- 在視窗任何位置按「/」或 Ctrl+K（Cmd+K）都會把游標放進搜尋框。可用 `IsSearchShortcutEnabled` 關閉。
- 版本切換是一個名為「Version v3.2」的按鈕，開啟一個帶單選項的選單。

## 動效

- 選取標記在列之間滑動，分組以高度彈簧摺疊（來自 `Sidebar`）。
- 顯示目前頁面時，清單會在列就位後捲動一次，並在展開的分組穩定後再捲動一次。
- 減少動效時保留摺疊和標記，但不用彈簧；關閉動效時一切立即改變。

## 響應式

- 側邊欄保持 `Sidebar` 的寬度（240 px）並佔滿給定的高度；清單自行捲動。
- 過長的頁面標題以省略號結尾。視窗較窄時，可把側邊欄放進 `Drawer`。

## 相關元件

- `Sidebar`：本區塊的基礎。
- `InboxSidebar` 用於信箱，`SearchResults` 用於完整的搜尋頁面。
- `Breadcrumb` 用於顯示目前頁面所在的位置。
