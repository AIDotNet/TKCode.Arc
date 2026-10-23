---
title: Site header
summary: 捲動時會變為實心的網站頂端列，附有滑動的目前連結指示、大型選單面板與行動版選單面板。
source-hash: 2f5e4352dce1
---

<Demo name="Preview" />

## 何時使用

`SiteHeader` 是行銷網站或文件網站的頂端列。它保存品牌、主要連結與一兩個動作，讀者捲動頁面時會停留在頂端；當選單的項目多到一列放不下時，它會開啟一個分組連結的面板。

- **有多個章節的產品網站**：產品與資源會開啟附有說明連結與功能卡片的面板；定價與客戶則是一般連結。
- **文件網站**：`Simple` 配置會把三到四個連結放在品牌旁，目前章節則以其下方的一條線標示。
- **連結不多、外觀沉穩的網站**：`Centered` 配置會把連結放在中央一個安靜的膠囊中，目前連結則以凸起的膠囊呈現。
- **窄的容器**：低於 `CollapseWidth`（預設為 760）時，連結會收合進選單面板。此區塊依自身寬度調整，因此即使視窗很寬，放在窄面板中的頂端列也會收合。

## 何時不用

- **應用程式內部的導覽**：請使用 `Sidebar` 或 `Tabs`。頂端列是給人抵達的頁面用的，不是用來在已在使用的工具之間切換。
- **含麵包屑與動作的頁面標題**：請使用 `PageHeader`。
- **單一的命令選單**：請使用 `DropdownMenu`。

## 用法

```xml
<ScrollViewer>
    <StackPanel>
        <arc:SiteHeader Current="product" />
        <!-- the page -->
    </StackPanel>
</ScrollViewer>
```

沒有項目時，頂端列會顯示 Arc 的範例組合（Product、Resources、Pricing、Customers），因此單獨的 `SiteHeader` 看起來就像 Arc 預覽。請把頂端列放在它應該黏附的 `ScrollViewer` 內；若它覆蓋在某個捲動檢視上，則設定 `ScrollSource`。Avalonia 沒有 `position: sticky`：頂端列會跟隨捲動位移，並停留在視窗頂端。頁面內容可以從頂端列下方開始（使用等於頂端列高度的負上邊距），讓主視覺滑到頂端列底下。

### 項目、連結與功能卡片

```xml
<arc:SiteHeader>
    <arc:SiteHeaderItem Value="guides" Label="Guides">
        <arc:SiteHeaderLink Label="Quick start" Description="Install and run in five minutes" Href="https://example.com/start" />
        <arc:SiteHeaderLink Label="Theming" Description="Tune color, radius and motion" />
        <arc:SiteHeaderItem.Feature>
            <arc:SiteHeaderFeature Title="What's new" Description="The spring release." Href="https://example.com/new" />
        </arc:SiteHeaderItem.Feature>
    </arc:SiteHeaderItem>
    <arc:SiteHeaderItem Value="pricing" Label="Pricing" Href="https://example.com/pricing" />
</arc:SiteHeader>
```

`Items` 是 `AvaloniaList<SiteHeaderItem>`；設定 `ItemsSource` 時以它為準，並在它引發集合變更時被監看。在 `Mega` 變體中，帶有 `Links` 的項目會開啟面板；在其他變體中，它就是一般連結。`Value` 是項目的穩定名稱：`Current` 與 `SiteHeaderNavigateEventArgs.Section` 都使用它。`SiteHeader.CreateExampleItems()` 會回傳 Arc 的項目，可作為起點。功能卡片的 `Image` 可以是任何 `IImage`；它會以 16:10 顯示並裁切以填滿，`ImageDescription` 則是它所呈現的內容。沒有圖片時，功能卡片只顯示文字。

<Demo name="Simple" />

### 選擇目的地

每個目的地（項目、面板連結、功能卡片、品牌或某個動作）都會引發冒泡的 `Navigate` 事件，並以相同的 `SiteHeaderNavigateEventArgs` 執行 `NavigateCommand`。`Kind` 表示選了什麼，`Section` 指出它所屬的項目，`Link`、`Feature` 或 `Item` 則承載資料物件。選擇項目內的內容時，也會把 `Current` 設為該項目，並關閉面板與選單面板。若沒有人設定 `Handled`，且目的地的 `Href` 是絕對的 http、https 或 mailto 位址，頂端列會以平台的啟動器開啟它。

```csharp
header.Navigate += (_, e) =>
{
    router.GoTo(e.Href ?? e.Label);
    e.Handled = true; // do not launch the address
};
```

<Demo name="Events" />

### 捲動與實心狀態

頂端列在頁面頂端時是透明的。捲動位移超過 `ScrollThreshold`（8）後，它會變為實心：有背景、底部邊框與靜止時的陰影。只有在位移低於門檻的一半時，它才會再次變為透明，因此在邊緣不會閃爍。`IsScrolled` 回報目前狀態。面板或選單面板開啟時，頂端列也會保持實心。將 `IsSticky` 設為 false，可讓頂端列隨頁面捲走。

### 大型選單面板

以滑鼠停留在觸發器上時，會在 70 毫秒後開啟其面板；移到另一個觸發器則會立即切換。指標離開頂端列與面板後，面板會在 180 毫秒後關閉。點按觸發器會切換其面板。觸控與觸控筆點按不會因停留而開啟面板。面板的高度會彈向目前內容面的高度，增長與縮小使用不同的彈簧。移到相鄰項目時，新內容會從你移動的方向滑入，舊內容則滑走。可在程式碼中使用 `OpenPanel(value)` 與 `ClosePanel()`；`OpenItemValue` 會回報目前開啟的項目。

### 窄版配置與選單面板

低於 `CollapseWidth` 時，連結、次要動作與面板會讓位給一個選單按鈕。選單面板會從頂端列往下展開：群組一次只展開一個，單純連結排成列，動作則放在最後，以兩個等寬欄排列。遮罩會覆蓋頁面，按下遮罩即關閉選單面板；選單面板開啟期間，頁面不會捲動。可在程式碼中使用 `OpenMenu()`、`CloseMenu()` 與雙向繫結的 `IsMenuOpen`。

<Demo name="Narrow" />

### 內容指引

- 頂端列保留四到五個連結，其餘移入面板。
- 以一行簡短的文字描述面板連結，說明那裡有什麼，而不是說它是連結。
- 只放一個主要動作；次要動作是給已有帳號的人使用。

## API 參考

<!-- api: SiteHeader -->

<!-- api: SiteHeaderItem -->

<!-- api: SiteHeaderLink -->

<!-- api: SiteHeaderFeature -->

<!-- api: SiteHeaderNavigateEventArgs -->

## 無障礙

- 連結是以 `Label` 命名的導覽地標。選單面板是另一個同名的導覽地標。
- 觸發器會回報其面板是否展開；目前連結會回報自己已被選取。面板是以其項目命名的區域。選單按鈕的名稱會在 `OpenMenuLabel` 與 `CloseMenuLabel` 之間切換，並回報選單面板是否開啟；群組列則回報是否展開。
- 鍵盤焦點會顯示 2 px 的環；指標焦點不顯示。懸停效果只套用於滑鼠或觸控筆。
- 每段文字都是屬性，因此在地化的網站可以取代 「Get Arc」、「Sign in」、選單標籤與地標名稱。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 依序在品牌、連結、動作與選單按鈕之間移動 |
| `Left` / `Right` | 在連結上：將焦點移到前一個或下一個連結，到端點時循環；面板開啟時，面板會跟著移到新連結（若是單純連結則關閉） |
| `Down` on a trigger | 開啟其面板並將焦點移到第一個連結 |
| `Up` / `Down` / `Home` / `End` in a panel | 在面板的連結與功能卡片之間移動；在第一個項目上按 `Up` 會關閉面板並回到觸發器 |
| `Enter` / `Space` | 選擇目前焦點所在的連結，或切換觸發器或選單群組 |
| `Escape` | 關閉面板並回到其觸發器；否則關閉選單面板並回到選單按鈕 |

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 頂端列變為實心 | 背景、邊框與陰影在 240 毫秒內淡入 | 立即 | 立即 |
| 懸停區塊 | 出現在第一個連結上，在連結之間滑動，指標離開時消失 | 立即 | 立即 |
| 目前標記 | 以彈簧在連結之間滑動 | 立即 | 立即 |
| 面板開啟 | 在 160 毫秒內淡入，並從上方 6 px 處與輕微縮放中定位 | 出現 | 出現 |
| 面板關閉 | 在 180 毫秒內淡出，並向上移動 4 px | 120 毫秒淡出 | 立即 |
| 面板高度 | 彈向內容面的高度；增長與縮小使用不同的彈簧 | 立即 | 立即 |
| 面板內容變更 | 新內容以短暫模糊向內滑入 36 px；舊內容滑出，並在 120 毫秒內淡入 | 淡入淡出 | 立即 |
| 選單圖示 | 選單與關閉以四分之一圈的旋轉與小幅縮放交替 | 淡入淡出 | 立即 |
| 選單面板 | 以彈簧從頂端列落下；列依序升起；群組以彈簧展開 | 立即 | 立即 |

## 相關元件

- `PageHeader`：應用程式頁面的標題區塊。
- `Sidebar`：應用程式內部的導覽。
- 網站頁尾：與行銷頁面的底部相呼應。
