---
title: Morph nav
summary: 網站的頂部導覽列：各個區段、區段的連結面板與搜尋共用一塊會變形的表面；頁面捲動後變得更精簡，寬度不夠時把區段收進一個選單。
source-hash: 35f153c896a4
---

<Demo name="Preview" />

## 何時使用

`MorphNav` 位於產品或行銷網站的頂部。從左到右依序是你的品牌、幾個區段、一個搜尋按鈕與一個行動呼籲。它們都不會開啟自己的視窗：
指向某個區段時，導覽列向下伸展出該區段的連結；滑到相鄰區段時，同一塊表面換成相鄰區段的連結；搜尋直接接管導覽列本身，
而不是跳出一個對話框。

- **區段不多的網站**，每個區段通往若干頁面，例如產品網站。
- **希望搜尋始終是頁首的一部分**，而不是在頁面上方開啟。
- **一個希望隨時伸手可及的動作**，例如開始試用，放在導覽列的最末端。

## 何時不用

- **同一個頁面的幾個檢視**：`Tabs` 把它們並排放著。
- **應用程式的頂層區域**而不是網站：`LiquidTabBar` 更合適。
- **指令而不是目的地**：交給 `DropdownMenu`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:MorphNav BrandName="Lantern" ActionLabel="Get started" Current="platform">
        <arc:MorphNavItem Value="platform" Label="Platform">
            <arc:MorphNavLink Label="Notebooks" Description="Notes and photos in one place" Href="/notebooks" />
            <arc:MorphNavLink Label="Maps" Description="Pin every note to a place" Href="/maps" />
        </arc:MorphNavItem>
        <arc:MorphNavItem Value="pricing" Label="Pricing" Href="/pricing" />
        <arc:MorphNav.SearchEntries>
            <arc:MorphNavSearchEntry Label="Maps" Group="Platform" Href="/maps" />
        </arc:MorphNav.SearchEntries>
    </arc:MorphNav>
</UserControl>
```

`MorphNav` 位於 Special 套件中：在 `UseArcControls()` 旁呼叫 `UseArcSpecial()` 註冊。在裡面放 `MorphNavItem`，每個區段一個。
給某個項目加上 `MorphNavLink` 子項或 `Feature`，它就有了面板；兩者都不加，這個項目本身就是一個連結。每個連結需要 `Label` 與 `Href`，
可選用 `Description` 與 `Icon`。不超過三個連結時排成一欄，四個以上分成兩欄；`Columns` 可以改變這一點。

連結旁的 `Feature` 放一張卡片：`MorphNavStory` 顯示圖片、標題與下方一行說明，其他內容則照原樣顯示。`Footer` 在連結下方加一行，
前面有一條細線；放在這裡的 `MorphNavLink` 會顯示成帶箭頭的短連結。面板裡的任何連結或按鈕一被使用，面板就會關閉。

`BrandName` 與 `BrandMark` 組成導覽列打頭的品牌按鈕，`ActionLabel` 加上收尾的行動呼籲按鈕，它會引發 `ActionInvoked` 並執行
`ActionCommand`。`SearchEntries` 有項目後才會顯示搜尋。

### 前往目的地

凡是通往某處的東西都會引發 `Navigate`：面板連結、頁尾連結、特色卡片、搜尋結果、沒有面板的項目，或品牌。`e.Label` 與 `e.Href` 描述目的地，
`e.Section` 是它所屬項目的 `Value`，`e.Destination` 是被選取的物件。在處理常式裡完成路由，並把 `Current` 設為目前所在的區段：
該項目以前景色顯示，並被讀成目前頁面。

<Demo name="Current" />

```csharp
nav.Navigate += (_, e) =>
{
    router.Go(e.Href);
    nav.Current = e.Section;
};
```

`Open(value)`、`ClosePanel()`、`OpenSearch()` 與 `CloseSearch()` 以程式碼控制導覽列；`OpenValue` 與 `IsSearchOpen`
回報目前開啟的內容。

### 搜尋

開啟搜尋會把導覽列變成搜尋欄位：各項目淡出並縮小，搜尋欄位佔據同一塊表面，結果出現在它下方。還沒輸入任何內容時，前五個項目作為建議列出；輸入後，標籤以查詢開頭的排在最前，
其次是標籤中某個詞以它開頭的，再其次是標籤包含它的，最後是說明、群組與 `Keywords` 中包含它的；最多顯示六個結果，結尾顯示其 `Group`。
方向鍵移動目前結果，Enter 開啟它。

### 捲動後變精簡

`IsCompact` 藉由彈簧把導覽列從 56 px 壓到 46 px，並把品牌名稱收起來。依頁面的捲動位置設定它，讀者往下讀時導覽列就少佔一些空間。

<Demo name="TightenOnScroll" />

```csharp
page.ScrollChanged += (_, _) => nav.IsCompact = page.Offset.Y > 24;
```

### 摺疊成選單

導覽本身比 `CollapseBelow`（若不修改則為 720 px）窄時，各個區段由一個 Menu 項目取代。這個項目的面板把每個區段的連結歸到該區段的名稱下，
後面接著沒有面板的項目。`IsCollapsed` 表示是否已經摺疊。

<Demo name="Folded" />

### 最佳實務

**建議**

- 保持三到五個區段，每個面板放少量連結。
- 連結名稱本身不夠清楚時，加一句簡短的說明。
- 讓 `Current` 與頁面保持一致，導覽列就總能說明你在哪裡。

**避免**

- 放多個主要動作：導覽列只容得下一個。
- 把導覽放在會裁切內容的父元素裡：面板會長到導覽列下方的頁面上。

## API 參考

<!-- api: MorphNav -->

## 無障礙

- 導覽是一個導覽地標，名稱由 `Label` 指定（預設 "Main"）。
- 帶面板的項目會回報是否已展開；一般項目、連結與卡片是連結。目前項目會被讀成目前頁面。
- 開啟的面板以區域的形式公開，名稱取自它的項目。搜尋欄位是控制結果清單的下拉式方塊；結果上方的那一行（"Suggested"、"6 results"、"No results for …"）
  位於禮貌型即時區域中，所以螢幕閱讀器會讀出符合的數量。
- 鍵盤焦點顯示統一的焦點環，強調底色跟著鍵盤焦點在導覽列上移動。指標操作不顯示焦點環。
- 在導覽以外的任何地方按下，都會關閉開啟的面板或搜尋；焦點移出導覽時，面板也會關閉。

### 鍵盤互動

| 按鍵 | 動作 |
|---|---|
| 左 / 右 | 沿著各項目逐一移動，到兩端時循環；如果有面板開啟，它會換成新取得焦點的項目的面板。 |
| Home / End | 移到第一個或最後一個項目。 |
| 在帶面板的項目上按下、Enter 或 Space | 顯示面板並把焦點放在最上面的連結；面板已經顯示時，把焦點移進面板。 |
| 面板中的下 / 上 | 逐一經過面板的連結；在最上面的連結上按上會回到該項目。 |
| 搜尋中的下 / 上 | 改變醒目提示的結果。 |
| 搜尋中的 Enter | 前往醒目提示的結果。 |
| Escape | 關閉開啟的面板或搜尋，並把焦點放回開啟它的控制項上。 |

## 動效

- 彈簧調整表面大小以配合每個面板：變大時略微越過，變小時不越過、直接停穩。與此同時，圓角從導覽列的膠囊形變成面板的圓角，陰影加深。
- 從導覽列開啟的第一個面板略微落到定位，同時從模糊變清晰。切換到另一個區段時內容橫向移動：新面板從新項目所在的一側進入，舊面板往另一側退出並淡出。強調底色沿導覽列滑到開啟的項目，否則滑到指標下或鍵盤焦點所在的項目。
- 滑鼠停留 60 ms 後面板才開啟，指標離開導覽後面板再停留 180 ms，所以指標經過空隙時面板不會閃一下就關上。只有滑鼠會停留；觸控時，點一下開啟或關閉面板。
- 開啟搜尋時，導覽列的內容模糊、縮小並淡出，搜尋欄位在同一塊表面裡展開。開啟或關閉精簡模式時，導覽列以彈簧改變大小。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 表面大小與圓角 | 彈簧 | 直接跳轉 | 直接跳轉 |
| 面板與搜尋 | 淡入、模糊與位移 | 短暫淡入淡出 | 直接切換 |
| 強調底色 | 滑動 | 直接跳轉 | 直接跳轉 |
| 精簡模式 | 彈簧 | 直接跳轉，品牌名稱淡出 | 直接跳轉 |

## 響應式行為

- 面板最寬 700 px，搜尋最寬 544 px，兩者都不會比導覽寬度減 16 px 更寬。
- 導覽比 `CollapseBelow`（720 px）窄時，區段換成一個選單，品牌只顯示標誌、不顯示名稱。
- 視窗寬度小於 420 px 時，連結面板只用一欄，摺疊選單省略圖示。

## 相關元件

- `Tabs` 用於同一頁面內的檢視。
- `LiquidTabBar` 用於應用程式的主要區域。
- `DropdownMenu` 用於指令清單。

| 元件 | 何時選擇 |
|---|---|
| `MorphNav` | 網站頂部需要帶面板的區段、搜尋與一個動作 |
| `Tabs` | 同一頁的幾個檢視應當始終可見 |
| `LiquidTabBar` | 應用程式有幾個主要區域需要來回切換 |
