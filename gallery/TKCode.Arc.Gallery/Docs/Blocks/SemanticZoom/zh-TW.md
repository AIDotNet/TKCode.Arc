---
title: 語意縮放
summary: 組織地圖，可從團隊縮放到專案，再到單一任務；每個層級顯示新的細節，並提供階層導覽、小地圖、引導式導覽，以及可見範圍內各項目的數字。
source-hash: eb9644ec5a43
---

<Demo name="Preview" />

## 何時使用

`SemanticZoom` 把整個組織畫成一張地圖，你可以放大進入而不會迷失方向。團隊是面板，專案是面板內的卡片，任務是卡片上的 Block，而任務則展開為其細節與清單。每個縮放層級顯示的是不同的資訊，而不只是把同一張圖放大：在總覽中，你會看到各團隊的進度，以及每個任務一個灰色標籤（受阻時為紅色）；拉近後，會看到團隊主管與數量，接著是專案標題、健康度與到期日，然後是任務標題與到期日，最後是任務的摘要、負責人與清單。階層導覽、細節面板與螢幕閱讀器文字，永遠描述位於檢視中心下方的內容，因此在縮放移動時，地圖會告訴你目前位於何處。

- **組合總覽**：在主管會議中，從整個公司開始，飛入落後的團隊，再飛入其中已偏離進度的專案，並查看哪些任務受阻。
- **工作區首頁**：每個人都看到同一張地圖。總覽回答「我們進展如何」，縮放回答「我的專案下一步是什麼」，而且不必切換頁面。
- **狀態逐步說明**：使用引導式導覽時，地圖會自動飛過一個團隊、一個專案與兩項任務，同時由某人進行簡報。
- **新人入門檢視**：向新進人員展示組織如何運作：誰負責哪個團隊、每個團隊執行哪些專案，以及誰負責哪些工作。
- **規劃檢查會議**：人們直接在地圖上勾選清單項目，並即時看到任務、專案與團隊數字的更新。

## 何時不用

- **逐項瀏覽深層階層。** 請使用 `TreeView`；它以列的方式顯示任意深度，比地圖更容易掃描。
- **處理任務本身。** 請使用看板或任務清單；地圖用於定位，而非移動工作。
- **依屬性分群的扁平資料。** 請使用 `CustomerGalaxy`；語意縮放需要容器的階層結構。
- **跨多個專案的精確比較。** 請使用 `DataGrid` 或 `Table`；地圖一次只顯示一個節點的數字。
- **小型卡片或側邊欄。** 地圖至少需要 400 px 高，且周圍要有空間，頁面才能捲動（滾輪永遠用於縮放地圖）。

## 用法

為控制項提供 `Organization`：由 `SemanticZoomTeam`、`SemanticZoomProject` 與 `SemanticZoomTask` 組成的樹狀結構。版面、總計（未完成與受阻任務、下一個到期日、進度、人員）以及每個標籤，都由它衍生而來，因此你只需描述工作本身。

```xml
<arc:SemanticZoom Header="Network map">
    <arc:SemanticZoom.Resources>
        <arc:SemanticZoomPerson x:Key="Rustam" Name="Rustam Aliyev" Role="Fleet lead" />
        <arc:SemanticZoomPerson x:Key="Hana" Name="Hana Sato" Role="Charging specialist" />
    </arc:SemanticZoom.Resources>
    <arc:SemanticZoomOrganization Title="Harbor Line Transit">
        <arc:SemanticZoomTeam Title="Fleet" Description="Vehicles, charging and everything that wears out." Lead="{StaticResource Rustam}">
            <arc:SemanticZoomProject Title="Depot charging bays" Lead="{StaticResource Hana}" Health="AtRisk">
                <arc:SemanticZoomTask Title="Order chargers" Status="InProgress" Assignee="{StaticResource Hana}" Points="3">
                    <arc:SemanticZoomChecklistItem Text="Get three quotes" IsDone="True" />
                    <arc:SemanticZoomChecklistItem Text="Check the grid connection" />
                </arc:SemanticZoomTask>
            </arc:SemanticZoomProject>
        </arc:SemanticZoomTeam>
    </arc:SemanticZoomOrganization>
</arc:SemanticZoom>
```

`SemanticZoom` 衍生自 Avalonia 的 `TemplatedControl`。地圖由控制項自己繪製（每一幀一次繪製，文字與筆刷有快取，靜止時不繪製任何內容）；階層導覽、按鈕、提示卡與細節面板都是一般控制項。控制項會設定 `:fit`、`:touring`、`:side-panel`、`:compact` 與 `:interacted` 這幾個偽類，供樣式使用。

### 結構

1. **標題**（`Header`、`Subtitle`、`SubtitleFormat`）：16 px 的中等粗細標題與 13 px 的淡色副行。若未設定 `Subtitle`，副行會顯示「5 個團隊、22 個專案、151 個任務。縮放以查看更多。」；設為空字串則隱藏副行。
2. **地圖**：團隊面板排列在點格背景上，每放大四倍，點格間距重複一次，使背景呈現景深感。
3. **階層導覽**（`IsBreadcrumbVisible`）：高於目前層級的每一層都是一個按鈕，點擊即飛回該層。
4. **導覽按鈕**（`IsTourButtonVisible`）：開始與停止引導式導覽；在窄版容器中只顯示圖示。
5. **提示卡**：顯示指標（或鍵盤游標）下方團隊、專案或任務的名稱、狀態或描述行，以及四個數字。
6. **小地圖**（`IsMinimapVisible`）：離開總覽後出現；按下或拖曳它即可移動檢視。
7. **提示列**（`IsHintVisible`）：淡色的手勢提示，在第一次互動後淡出。
8. **縮放列**（`IsZoomControlsVisible`）：縮小、縮放讀數、放大，以及顯示全部（當全部都在檢視中時停用）。
9. **細節面板**（`DetailPanelPlacement`，也可單獨作為 `SemanticZoomPanel` 使用）：目前檢視中節點的種類與名稱、其未完成與受阻任務及下一個到期日、進度與人員。

### 資料

| 型別 | 保存內容 |
|---|---|
| `SemanticZoomOrganization` | 根節點：`Title` 與其 `Teams` |
| `SemanticZoomTeam` | `Title`、`Description`（顯示於其提示卡）、`Lead` 與其 `Projects` |
| `SemanticZoomProject` | `Title`、`Description`（用於產生的任務摘要）、`Lead`、`Health`、`Due` 與其 `Tasks` |
| `SemanticZoomTask` | `Title`、`Status`、`Assignee`、`Reviewer`、`Due`、`Points`、`CommentCount`、`Summary` 與其 `Checklist` |
| `SemanticZoomChecklistItem` | `Text` 與 `IsDone` |
| `SemanticZoomPerson` | `Name`、`Role`、選擇性的 `Avatar` 圖片與選擇性的 `Initials` |

每個節點都以唯讀屬性公開其總計：`OpenTasks`、`BlockedTasks`、`DoneTasks`、`TaskCount`、`NextDue`（未完成任務中最早的到期日）、`Progress`（任務已勾選清單項目的比例，完成時為 1；專案、團隊與組織則平均其下方所有任務）、`People`（主管在前，接著是負責人，且不重複）、`ProjectCount`，以及 `NeedAttention`（未達進度的專案）。只要其下方有任何內容變動，這些數字就會立即更新。

沒有 `Summary` 的任務，會根據其狀態、負責人、到期日與專案自動產生摘要。沒有 `Avatar` 的人員會在淡色圓圈中顯示其姓名縮寫。

### 移動

捲動或捏合可以以指標為中心縮放（1× 顯示全部，預設最高可達 53×，即 `MaxZoom`）；拖曳可以平移（輕甩後檢視會滑行，並在邊緣回彈）；點擊團隊、專案或任務即可飛入。點擊你已在其中的節點或其上層節點不會有任何反應；下一次點擊會進入更深一層或移到同層的其他節點。飛行一開始會先稍微減速，接著加速，移動距離越遠，耗時越長。

目前節點（`Scope`，雙向繫結）並非獨立的選取項目：它永遠是檢視中心下方、縮放允許範圍內最深的節點（團隊從 1.1× 起、專案從 2.4× 起、任務從 18× 起）。設定 `Scope` 或呼叫 `FlyTo` 即可飛往某處；`FlyOut`、`ResetView` 與 `ZoomBy` 涵蓋其餘操作。`ScopeChanged` 在飛行與平移期間也會觸發，並以 `IsUserInitiated` 區分使用者的輸入與程式碼或導覽的動作。

### 勾選清單項目

縮放到任務夠近時，會出現其清單；點擊項目，或將鍵盤游標移到項目上並按 Enter，即可勾選或取消勾選。勾選最後一個未完成項目會將任務標記為完成；取消勾選已完成任務的項目，則會將其退回審查中。任務的 Block、其專案的卡片、團隊與細節面板會同時更新，數字也會滾動到新值。`ChecklistItemToggled` 會回報每一次變動。

### 引導式導覽

按下導覽按鈕（或使用 `ToggleTourCommand`、`StartTour`、`IsTourPlaying`）會依序飛過一系列停靠點，並在每個停靠點停留 `TourDwell`（1.5 秒）。預設會依序造訪第一個團隊、其中一個專案、下一個專案的兩項任務、第二個團隊及其中一個專案，最後回到整張地圖。設定 `TourStops` 即可自訂路線；將整個組織作為停靠點，即代表顯示全部。任何手動輸入都會讓導覽停在當下的位置。

<Demo name="GuidedTour" />

### 版面與容器寬度

地圖的高度為控制項寬度的 48%，介於 400 與 580 px 之間（或由 `StageHeight` 指定）。當內部寬度超過 900 px 時，細節面板會移到地圖旁邊（264 px）；否則面板位於地圖下方，並分為兩欄。在寬度小於 600 px 的容器中，地圖高度為 420 px，導覽按鈕只顯示圖示，小地圖縮小，提示列移到縮放列上方，而面板只使用一欄。地圖本身的版面不會隨寬度改變：窄版容器只是把它顯示得更小。

<Demo name="SidePanel" />

<Demo name="Narrow" />

### 自有資料

整棵樹可以像這裡一樣直接寫在 XAML 中，也可以在程式碼中建立後指派給 `Organization`。團隊會排列成共用同一卡片高度的列，每一列會拉伸至最寬的那一個，因此任何團隊大小的組合都能保持整齊。

<Demo name="YourData" />

### 文字與格式

每個標籤、按鈕名稱與播報文字都位於 `Texts`（`SemanticZoomTexts`，預設為英文）中：種類與面板標籤、狀態與健康度名稱、地圖上繪製的文字行、自動產生的摘要、提示卡的列、導覽與縮放按鈕名稱、提示列，以及螢幕閱讀器的播報。指派新的實例即可變更這些文字。日期（「Sep 23」）與百分比（「59%」）則來自 `Formatter`（`ISemanticZoomFormatter`；`SemanticZoomFormatter` 具有 `Culture`）。

## API 參考

<!-- api: SemanticZoom -->

<!-- api: SemanticZoomPanel -->

<!-- api: SemanticZoomOrganization -->

<!-- api: SemanticZoomTeam -->

<!-- api: SemanticZoomProject -->

<!-- api: SemanticZoomTask -->

<!-- api: SemanticZoomChecklistItem -->

<!-- api: SemanticZoomPerson -->

## 無障礙

- 地圖是一個可取得焦點的元素，角色為「可縮放地圖」。其名稱概述組織與目前檢視的節點（「Map of Harbor Line Transit: 5 teams, 22 projects, 151 tasks. Showing Fleet. …」），其說明文字列出按鍵。
- 一個禮貌性即時區域會在鏡頭停止後播報目前檢視的節點（「Now showing Fleet team: 5 projects, 31 open tasks, 52% done.」）、鍵盤游標下的項目、勾選與取消勾選的清單項目（附任務的新數量與狀態），以及導覽的開始與停止。
- 一個隱藏的表格會列出每個專案，包含其團隊、健康度、進度、未完成與受阻任務，以及到期日，讓螢幕閱讀器無需縮放即可讀取整個專案組合。
- 階層導覽是一組具名的按鈕；導覽按鈕會回報是否已按下；縮放按鈕具有名稱，而當全部都在檢視中時，顯示全部按鈕會停用並被 Tab 略過；面板是具名的群組，其頭像堆疊以所有人員的姓名命名。
- 滾動的數字只播報一次，播報最終值。健康度與狀態從不只依靠顏色傳達：提示卡與地圖都會以文字標示。

### 鍵盤

| 按鍵 | 位置 | 動作 |
|---|---|---|
| `Tab` | 任何地方 | 地圖、階層導覽、導覽按鈕、縮放按鈕 |
| `ArrowLeft` / `ArrowRight` / `ArrowUp` / `ArrowDown` | 地圖 | 在目前節點的子節點與同層節點中，將游標移到該方向最近的項目（在任務中則為其清單項目）；第一次按下會選取第一個子節點 |
| `Enter`、`Space` | 地圖 | 飛入游標所在的項目，或勾選其下的清單項目 |
| `Escape` | 地圖 | 停止導覽；清除游標；再按一次則縮小一層 |
| `Backspace` | 地圖 | 縮小一層 |
| `+`、`=` / `−` | 地圖 | 以中心為基準，放大或縮小 1.5 倍 |
| `0`、`Home` | 地圖 | 顯示整個組織 |

## 動態效果

- 飛行沿著平滑的縮放與平移路徑進行（先稍微縮小、移動，再放大），使用緩入緩出曲線，耗時 460 ms 加上每單位路徑長度 380 ms：飛入團隊約 0.6 秒，從專案飛到任務約 1.1 秒。
- 滾輪、捏合、按鈕與按鍵縮放遵循臨界阻尼彈簧（約 0.2 秒穩定），並保持指標下方的點不動。超出縮放上下限時會略微伸展後彈回；拖曳超過邊緣時會伸展，放開後彈回；輕甩會滑行，並在約一秒內減速。
- 每個細節層級會在依螢幕大小的範圍內淡入，並在縮放跳動時於約 140 ms 內緩和過渡。檢視以外的節點會變暗（團隊降至 42%，相鄰的專案與任務降至 50%）；懸停輪廓約 0.1 秒淡入。
- 提示卡淡入並從 96% 放大；小地圖在離開總覽時升起；提示列在第一次互動後淡出；階層導覽淡入（240 ms）與淡出（120 ms）；面板名稱交叉淡化，其數字逐位滾動；進度條緩和地變化到新寬度；新頭像淡入。
- 導覽在每個停靠點停留 1.5 秒。在減少動態效果時，鏡頭會直接跳到每個停靠點，且每個停靠點停留 2.4 秒。
- 減少動態效果會立即完成所有飛行、縮放、滑行、變暗與淡入，並取消介面過渡；關閉動態效果也是如此。勾選清單項目與總計會在每個層級即時更新。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 飛到節點、返回、顯示全部 | 縮放與平移路徑，460 + 380·S ms | 立即完成 | 立即 |
| 滾輪、捏合、按鈕或按鍵縮放 | 彈簧，約 0.2 秒，錨定 | 立即完成 | 立即 |
| 拖曳超過邊緣後放開 | 橡皮筋效果，彈回 | 橡皮筋效果，直接落在邊界內 | 橡皮筋效果，直接落在邊界內 |
| 輕甩 | 滑行（τ ≈ 0.27 秒） | 停止 | 停止 |
| 細節層級 | 依螢幕大小淡入，140 ms 緩和 | 依螢幕大小 | 依螢幕大小 |
| 變暗與懸停輪廓 | 緩和（140 ms / 90 ms） | 立即 | 立即 |
| 提示卡 | 淡入並從 96% 放大 | 淡入 | 立即 |
| 小地圖 | 淡入並上升 6 px | 出現 | 立即 |
| 階層導覽 | 淡入 240 ms，淡出 120 ms | 立即變化 | 立即 |
| 面板數字與進度條 | 數字滾動，進度條緩和（580 ms） | 立即變化 | 立即 |
| 導覽 | 飛行，每個停靠點停留 1.5 秒 | 跳轉，每個停靠點停留 2.4 秒 | 跳轉，每個停靠點停留 2.4 秒 |

## 相關元件

- `TreeView`：以列的方式瀏覽任意階層。
- `CustomerGalaxy`：依屬性分群為叢集的扁平資料。
- `DataGrid` 與 `Table`：並排比較專案。
- `Breadcrumb`：地圖以外的路徑。

| 組件 | 適用時機 |
|---|---|
| `TreeView` | 人們展開與收合深層階層以找到某一項目 |
| `CustomerGalaxy` | 數千個項目依屬性分群，而非巢狀結構 |
| `DataGrid` | 人們需要自行閱讀、排序與編輯專案 |
| `MetricCard` | 儀表板磚塊顯示一個數字及其變化 |
