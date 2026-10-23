---
title: 版面變形
summary: 同一組卡片有六種檢視，從格線與清單到扇形堆疊、3D 環與螺旋，每張卡片都飛到它的新位置。
source-hash: 0b6dc3d6d5e1
---

<Demo name="Preview" />

## 何時使用

`LayoutMorph` 呈現一組卡片，例如商品、人員或專案，並讓讀者選擇要用哪種方式檢視。同一組卡片可以是格線、瀑布牆、清單、扇形堆疊、3D 輪盤或螺旋。檢視改變時，每張卡片都會從目前的位置，依各自的彈簧飛到新位置；飛行從所選的選項附近開始，再向外擴散，讓讀者能跟著一張卡片看完整個變化。指向一張卡片會將它抬起，並在舞台下方的面板中顯示它的數據；選取卡片則會把它變成詳細頁，包含圖片、描述、事實與一個動作。

- **商店的商品集合頁或型錄**：格線便於快速掃視，清單把製作者與價格放在每個名稱旁，輪盤則以大圖呈現商品。詳細頁的按鈕（「加入購物袋」）在讀者繼續瀏覽時會保留其狀態。
- **團隊或講者頁面**：輪盤與螺旋一次轉動一位人員的肖像，面板則顯示正對前方的那一位，不需要指向。
- **作品集或案例研究索引**：瀑布檢視保留每張圖片各自的比例，堆疊則讓少量項目看起來像一手牌。

## 何時不用

- **很長或持續增加的集合。** 每張卡片都是會在每次檢視改變時移動的即時元素，此區塊設計用於約 10 至 40 張卡片。數百筆項目請使用虛擬化清單、`DataGrid` 或分頁格線。
- **需要讓人比較的資料。** 當讀者需要在許多列之間排序、篩選或比較數值時，請使用表格；這裡的清單檢視每列只顯示一個數值，且無法排序。
- **只有單一固定檢視。** 如果內容只適合一種檢視，請使用一般格線或 `Carousel`，並省略檢視切換。
- **導覽。** 卡片會在原位開啟詳細頁，而不是跳到另一頁。當選擇項目應該帶讀者前往其他地方時，請使用連結或卡片清單。

## 用法

### 項目

每個 `LayoutMorphItem` 就是一張卡片。`Title` 在各處都是名稱；`Subtitle` 是格線、輪盤、浮動標籤與面板中名稱下方的那一行；`Meta` 是清單列與詳細頁中的第二行；`Value` 位於清單列右側，以及輪盤說明的右側。有 `Image` 的項目會以填滿的方式裁切顯示；沒有圖片的項目則顯示封面，標題在上方、數值在下方，字級會隨卡片大小調整。`AspectRatio`（高度除以寬度）決定瀑布檢視的形狀。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:LayoutMorph Title="Studio shelf" DefaultPanelTitle="Shelf">
        <arc:LayoutMorphItem Key="ink-tray"
                             Title="Ink pen tray"
                             Subtitle="By Yuki Aoyama"
                             Meta="Yuki Aoyama · Blackened steel"
                             Value="$36"
                             AspectRatio="0.9"
                             Description="A shallow tray for pens and a clip or two, folded from one sheet.">
            <arc:LayoutMorphItem.Facts>
                <arc:LayoutMorphFact Label="Price" Value="$36" />
                <arc:LayoutMorphFact Label="Size" Value="24 × 8 cm" />
            </arc:LayoutMorphItem.Facts>
            <arc:LayoutMorphItem.Stats>
                <arc:LayoutMorphFact Label="Price" Value="$36" />
                <arc:LayoutMorphFact Label="In stock" Value="31" />
                <arc:LayoutMorphFact Label="Rating" Value="4.8" />
            </arc:LayoutMorphItem.Stats>
        </arc:LayoutMorphItem>
        <arc:LayoutMorphItem Key="yuki-aoyama"
                             Title="Yuki Aoyama"
                             Subtitle="Metalsmith, Osaka"
                             Meta="Metalsmith in Osaka"
                             Value="2 pieces"
                             ActionText="Follow"
                             ActiveActionText="Following" />
    </arc:LayoutMorph>
</UserControl>
```

為每個項目設定 `Key`：重新建立集合時，key 不變的卡片會保留它們的位置、焦點與狀態。對於你自己擁有的集合請使用 `ItemsSource`；`ObservableCollection` 會被監看，新增或移除的卡片會讓其他卡片移到新位置。

### 檢視

`Layout` 是目前的檢視，且為雙向繫結，因此區塊外的控制項也能驅動它。`Layouts` 決定切換器提供哪些檢視以及順序；卡片上的方向鍵會沿著同一清單移動。`SetLayout` 可從程式碼切換檢視，並可指定飛行交錯的起點。

- **格線**：寬的容器每列顯示八張附說明的卡片；較窄的容器則顯示方形卡片，依哪種方式能讓卡片最大，排成六、五或四欄。
- **瀑布**：依各自比例排成數欄的卡片；當最高的一欄放不下時，每張卡片會等比例縮小，因此瀑布檢視永遠不需要捲動。
- **清單**：一到三欄的列，每列包含縮圖、兩行文字與數值。高於舞台的清單可以用滾輪或垂直拖曳捲動。
- **堆疊**：扇形排列的卡片；指標所指的卡片會從扇形中向上滑出。
- **輪盤**：大型卡片沿著環排列，從第一張到最後一張。拖曳或滑動後，會停在最近的卡片上；快速甩動則會轉過數張卡片。它不會首尾相接：拖過第一張或最後一張時會被拉伸，放開後彈回。水平滾輪（或按住 Shift 再滾動滾輪）會輕推輪盤。選取正前方旁邊的卡片會把它轉到正前方；選取正前方的卡片才會開啟它。
- **螺旋**：每圈十張卡片的螺旋，朝讀者傾斜。拖曳會旋轉並沿螺旋移動，因此正前方的卡片會保持在中央。

輪盤與螺旋會記住你離開時的位置，回來時仍在原處。`FrontItem` 是正對前方的卡片。

<Demo name="Catalogue" />

### 面板與詳細頁

舞台下方的面板會列出讀者指向、聚焦、置於前方或已開啟的卡片，以及它的三個 `Stats`；否則它會顯示 `DefaultPanelTitle`、`DefaultPanelSubtitle` 與 `DefaultStats`。「$1,240」、「4.8」或「2 days」之類的數值會逐位滾動；當數值的文字不是純數字時，請設定 `NumericValue`。

選取卡片會引發 `ItemInvoked`（將 `Handled` 設為 true 可阻止它）、執行 `OpenCommand`，並開啟詳細頁：卡片本身放大成對話方塊，其他卡片則淡出。`OpenVariant="Hero"` 提供寬版卡片與大圖，適用於有照片的商品；`Profile` 則是小圖配文字。詳細頁的按鈕會切換 `IsActive` 並顯示 `ActiveActionText`；它會引發 `ItemActionInvoked` 並執行 `ActionCommand`。設定 `OpenedItem` 可從程式碼開啟卡片，設為 null 則關閉；也可以呼叫 `Open` 與 `Close`。

```csharp
morph.ItemActionInvoked += (_, e) =>
{
    if (e.Item.IsActive)
    {
        bag.Add(e.Item.Key);
    }
    else
    {
        bag.Remove(e.Item.Key);
    }
};
```

<Demo name="Team" />

### 有框或無框

`IsFramed`（預設值）會把舞台與面板畫成有邊框、圓角的表面，適合放在其他內容之間的區塊。當區塊佔滿整個頁面或全螢幕檢視時，請設為 false；卡片會直接浮在頁面上。`Title` 與 `Lede` 為選用：省略它們，即可自行放置標題。`IsPanelVisible` 與 `IsHintVisible` 分別用來隱藏面板與提示。

<Demo name="Flat" />

### 窄版容器

區塊讀取的是自己的寬度，而不是視窗的寬度。寬度低於 720 時，檢視切換只顯示圖示，並橫跨整個寬度拉伸，名稱則以提示卡呈現；提示移到切換器下方，面板將名稱放在統計數字上方，詳細頁則把圖片堆疊在文字上方。寬度低於 520 時，卡片使用較緊密的間距，瀑布檢視改用四欄。舞台高度為 600；寬度 720 及以下為 540；低於 480 為 500；低於 360 為 460。`StageHeight` 可設定你自己的高度。

<Demo name="Narrow" />

### 文字

每段文字都有英文預設值與對應的屬性：檢視名稱（`GridLabel` 到 `HelixLabel`）、提示（`GridHint` 到 `HelixHint`）、切換器的名稱（`ViewLabel`）、面板的預設文字，以及提供給螢幕閱讀器的格式字串（`StageLabelFormat`、`LayoutChangedFormat`、`OpenedFormat`、`CloseTextFormat`、`ActionOnFormat`、`ActionOffFormat`）。

```xml
<arc:LayoutMorph Title="Ateliers"
                 ViewLabel="Vue de la galerie"
                 GridLabel="Grille"
                 GridHint="Choisissez une carte pour l’ouvrir"
                 DefaultPanelSubtitle="Survolez une carte"
                 CloseTextFormat="Fermer {0}"
                 OpenedFormat="{}{0} ouvert" />
```

### 資料形狀與效能

- 卡片以點陣圖圖片依其在螢幕上顯示的尺寸（以 1.25 倍的級距向上取整）製作的副本來繪製，因此大圖片幾乎不占 GPU 資源，縮小顯示時也不會閃爍；開啟的詳細頁繪製圖片本身。副本只製作一次：要更換圖片，請把 `Image` 設為新的圖片（重新繪製過的點陣圖仍使用舊副本）。請使用約為顯示最大尺寸兩倍的圖片（大圖詳細頁約為 600 × 550），並只解碼一次；示範會依種子以程式碼繪製圖片。
- 卡片自行繪製，且只透過變換移動，因此檢視改變時不會執行版面配置。每張卡片有各自的彈簧；一切靜止後，舞台便停止要求影格，閒置的區塊不耗費資源。
- 讓集合保持在幾十張卡片以內。輪盤一次大約顯示五張卡片，更遠的卡片在環的背面隱藏等候。

## API 參考

<!-- api: LayoutMorph -->

<!-- api: LayoutMorphItem -->

<!-- api: LayoutMorphFact -->

## 無障礙

- 檢視切換是一組單選按鈕，名稱由 `ViewLabel` 提供。只有被選取的選項是 Tab 停駐點；方向鍵（會循環）、Home 與 End 在移動的同時就會選取。在窄版容器中，每個圖示保留其名稱。
- 舞台是一個群組，名稱取自 `StageLabelFormat`：包含標題、檢視、卡片數量與按鍵操作方式。每張卡片是一個按鈕，名稱為「標題，副標題，數值」（或 `AutomationName`），並以其 meta、描述與事實作為說明，因此網頁中隱藏表格所提供的資訊，在逐張卡片時都能取得。
- 只有一張卡片是 Tab 停駐點。上下鍵依資料順序移到上一張或下一張卡片，Home 與 End 移到第一張與最後一張，Enter 或 Space 開啟卡片，左右鍵切換到上一個或下一個檢視，並讓焦點留在同一張卡片上。在輪盤與螺旋中，取得焦點的卡片會轉到前方，正前方的卡片就是 Tab 停駐點；在可捲動的清單中，取得焦點的列會捲動到可見範圍。
- 開啟的詳細頁是一個對話方塊，名稱取自項目的標題，並以其描述作為說明。焦點會移到名稱為「關閉」與標題的關閉按鈕，Tab 會停留在對話方塊內直到關閉。按 Escape、按下關閉按鈕，或在卡片外的舞台上按下，都會關閉詳細頁，焦點則回到卡片。
- 檢視改變、卡片被開啟與詳細頁的按鈕，都會以禮貌模式朗讀（「輪盤檢視」、「Halo floor lamp opened」）。
- 浮動標籤只重複卡片的名稱，不會被朗讀。面板是純文字。

## 動態效果

- **檢視改變**：每張卡片以各自的彈簧（剛度 100、阻尼 20、臨界阻尼，約 1.15 秒），從目前位置移到目的地，包括大小、圓角與圖片。最接近所選選項的卡片先開始，最遠的卡片晚 330 ms（`StaggerDuration`）。飛行時，卡片會向讀者方向抬起（最快時為距離的 0.1 倍），並隨移動方向傾斜。背面朝向讀者的卡片（在螺旋中，或離開環時尚未轉回）只顯示底板，不透明度為 72%。中途打斷變化時，每張卡片會從目前位置重新開始，不會跳動。
- **說明文字**：舊的說明立即淡出；新的說明則在卡片幾乎抵達後淡入，因此兩種版面不會重疊。提示與面板的文字行在 240 ms 內上升；面板的數字會滾動。
- **指向**：卡片以快速彈簧（剛度 500、阻尼 49）抬起並加上陰影；在堆疊中它會從扇形中滑出，在螺旋中則向前移動。在瀑布、堆疊與螺旋檢視中，名稱標籤會浮在卡片上方。
- **輪盤與螺旋**：環會一對一跟隨指標；放開後，會停在甩動所指的卡片上（剛度 110、阻尼 19.5），並保留放開時的速度。拖過第一張或最後一張時會遇到橡皮筋阻力，放開後彈回。螺旋會朝讀者傾斜，並隨旋轉沿螺旋移動。
- **開啟**：卡片放大成詳細頁（剛度 130、阻尼 26），同時舞台後退，其他卡片淡化至 12%。文字在接近結束時淡入，按鈕則在開始後 260 ms 出現。
- 減少動態效果時，每張卡片會立即就位，只保留短暫的淡入淡出（詳細頁的按鈕、標籤）。關閉動態效果時，所有變化都會立即套用。

## 相關元件

- `Carousel`，用於帶有分頁的單列投影片。
- `CardStack`，用於可滑動的卡片堆。
- `SegmentedControl`，用於區塊外的檢視切換。
- `RollingDigits`，用於面板中的滾動數字。
- `ExplodedView` 與 `CustomerGalaxy`，其他的 3D 展示型區塊。
