---
title: 頁首
summary: 專案頁首會隨捲動收合為精簡列，標籤頁的數量會滾動更新。
source-hash: f0053fa759d8
---

<Demo name="Preview" />

## 何時使用

- 專案、儲存庫或記錄頁面的頂部，用於說明頁面位於何處、頁面是什麼、狀態如何，以及該頁面有哪些檢視。
- 頁面主體可捲動，且標題應以精簡列的形式保持在手邊的頁面。
- 記錄上有少量操作的頁面，例如追蹤、分享，以及一個主要操作。

## 何時不用

- 應用程式內各頁面之間的導覽：使用 `Sidebar`。
- 在卡片或面板內切換檢視：使用 `Tabs`。
- 帶有自身標題的對話方塊或設定表單：使用 `Dialog` 的標題，或一般標題。

## 用法

`PageHeader` 是整個頁面的框架。它顯示頁首，並在其下方顯示所選標籤頁的可捲動主體。請為它設定固定高度（或將它放在一個具有固定高度的列中），以便主體可以捲動。每個 `PageHeaderTab` 都有一個 `Header`、一個選用的 `Count`，以及作為 `Content` 的主體。

```xml
<arc:PageHeader Title="結帳改版"
                Status="按計畫進行"
                Description="圍繞已儲存的付款方式，重新打造行動版結帳。"
                Height="480">
    <arc:PageHeaderTab Header="總覽">
        <TextBlock Text="進度、里程碑與近期動態。" />
    </arc:PageHeaderTab>
    <arc:PageHeaderTab Header="問題" Count="9">
        <TextBlock Text="未關閉的問題。" />
    </arc:PageHeaderTab>
</arc:PageHeader>
```

### 標題與標籤頁

頁首只需要標題和標籤頁。沒有麵包屑、狀態或操作時，頁首列會保持空白，直到頁首收合並顯示精簡標題。

<Demo name="Minimal" />

### 麵包屑、狀態與中繼資訊

`Breadcrumbs` 接受通往頁面的路徑，根在前。最後一項是目前的頁面。選取其他麵包屑會引發 `BreadcrumbClick`，並附上該項目及其索引。`Status` 會在標題旁加上一個帶圓點的徽章，顏色由 `StatusTone` 決定（預設為成功色）。`Meta` 可接受任何內容，用於描述標題下方的那一列，例如成員、負責人與日期。其文字為特小號，並以弱化色顯示。

```xml
<arc:PageHeader Title="結帳改版" Status="有風險" StatusTone="Warning">
    <arc:PageHeader.Meta>
        <TextBlock Text="由 Emma Collins 負責" />
    </arc:PageHeader.Meta>
</arc:PageHeader>
```

```csharp
header.Breadcrumbs = new[] { "Northline", "專案", "結帳改版" };
header.BreadcrumbClick += (_, e) => router.Open(e.Index);
```

### 操作

`Actions` 是頁首列中的小型次要按鈕。`MoreActions` 一律放在「更多操作」選單中。`PrimaryAction` 是列尾的小型主要按鈕。每個 `PageHeaderAction` 都有 `Label`、`Icon`（Lucide 圖形，例如 `ArcIcons.Bell`），以及 `Command` 或 `Click` 事件。`PageHeader.ActionInvoked` 在同一個事件中回報所有操作。

當麵包屑區域的寬度將小於 240 像素時，次要操作會收進“更多操作”選單的頂端。當寬度再次達到 272 像素時，它們會重新展開，因此頁首不會在臨界點閃爍。系統只監看頁首的寬度，所以文字變長的標籤（例如“追蹤”變為“正在追蹤”）永遠不會把你剛按下的按鈕收進選單。

<Demo name="FoldingActions" />

修改 `Label` 與 `Icon` 即可原地更新按鈕。若選單列需要顯示不同的文字，請設定 `MenuLabel` 與 `MenuIcon`。`IsLoading` 會顯示載入指示器，並忽略點擊。

```csharp
follow.Click += (_, _) =>
{
    following = !following;
    follow.Label = following ? "正在追蹤" : "追蹤";
    follow.MenuLabel = following ? "取消追蹤" : "追蹤";
};
share.IsLoading = true;
```

### 收合

當主體捲動超過 16 像素時，頁首會收合為一個精簡列，只顯示標題與狀態。只有當主體回到距頂部 4 像素以內時，它才會再次展開。`IsCondensed` 會告訴你目前的狀態。選取精簡標題會呼叫 `ScrollToTop()`，它會捲回頂部，並把焦點移到標題上。只有當主體夠長、頁首縮小後仍可捲動時，頁首才會收合。

頁首收合時切換標籤頁，會顯示新標籤頁的頂部，並保留精簡列。`RevealTab(index)` 可在程式碼中做同樣的事，例如在其他標籤頁中新增項目之後。

### 資料項目

將 `ItemsSource` 繫結到你自己的項目。`ItemTemplate` 負責渲染標籤頁的標籤，`ContentTemplate` 負責渲染主體。`SelectedIndex` 與 `SelectedItem` 可以雙向繫結。

<Demo name="DataItems" />

### 窄版配置

版面配置會跟隨控制項的寬度，而非視窗的寬度。寬度低於 560 像素時，內距會變小，根層麵包屑與精簡徽章會隱藏，標題會降低一級字級，標籤列遠端的邊緣會淡出。寬度低於 460 像素時，主要操作只顯示圖示。

## API 參考

<!-- api: PageHeader -->

<!-- api: PageHeaderTab -->

<!-- api: PageHeaderAction -->

## 無障礙

- 此控制項是一個區域，名稱由 `Title` 提供。標題為二級標題。
- 麵包屑是導覽地標。目前的頁面會被標示，且不是連結。
- 標籤列由 `TabsLabel` 命名。Left 與 Right 在標籤頁之間移動並選取它們，到達兩端時會循環。Home 與 End 跳到第一個與最後一個標籤頁。只有選取的標籤頁位於 Tab 鍵順序中。
- 「更多操作」按鈕由 `MoreActionsLabel` 命名。選單支援方向鍵、Home、End、輸入字元跳轉、Enter，以及按 Escape 關閉並將焦點移回。
- 精簡標題的預設名稱為 “Title, back to top”（`BackToTopLabel`）。頁首中被隱藏的部分會離開 Tab 鍵順序，並忽略指標。
- 寬度低於 460 像素時，主要按鈕仍以其標籤作為無障礙名稱。

## 動態效果

- 收合：頁首的介紹區以平滑彈簧收起高度。標題以左上角為基準縮小至 62%，並伴隨輕微模糊淡出。麵包屑向上移開，精簡標題從柔和的模糊中升起。
- 操作收合：次要操作以平滑彈簧收起寬度並淡出。只裁切右側邊緣，因此它們會收進「更多操作」按鈕之下。
- 標籤頁：指示器在變形彈簧上於標籤頁之間滑動。數量會逐位滾動。新的主體從移動方向的一側滑入 12 像素，舊的主體同時淡出。
- 動態效果等級為 Reduced 時，只保留淡入淡出。高度與寬度立即變化，主體交叉淡化。等級為 Off 時，所有變化會立即套用。

## 相關元件

- `Breadcrumb`：用於單獨的路徑。
- `Tabs`：用於卡片或面板內的檢視。
- `Badge`：用於狀態徽章。
- `DropdownMenu`：用於操作選單。
