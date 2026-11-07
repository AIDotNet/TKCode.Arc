---
title: 停靠版面
summary: "IDE 式版面：帶分頁的面板在可調整大小的巢狀分割中環繞文件區排列：拖放停靠面板、把面板浮動到獨立視窗、把工具自動隱藏到邊欄、把文件繫結到集合、用鍵盤切換面板，並可把版面儲存為 JSON。"
source-hash: dbe1ea863e1e
---

<Demo name="Preview" />

## 何時使用

- IDE、編輯器、儀表板、交易終端機和設計工具：多個長期存在的工作區並排擺放，由使用者自行排列。
- 文件在中央區域開啟、關閉，工具面板停留在四周。
- 使用者希望下次開啟時還是原來的版面：儲存為 JSON 再讀回。

## 何時不用

- 只有兩三個固定窗格、沒人會重新排列：用 `ResizablePanels`。
- 在同一層級的檢視之間切換：用 `Tabs`。
- 帶側欄和主區的應用程式外框：用 App shell 區塊。
- 暫時出現在頁面上方的內容：用 `Popover`、`Drawer` 或 `Dialog`。

## 用法

加入套件並在 `UseArc` 中註冊：

```csharp
AppBuilder.Configure<App>()
    .UseArc(arc => arc.UseArcControls().UseArcDock());
```

把版面樹寫成 `DockLayout` 的內容。`DockSplit` 把子節點左右排列（`Horizontal`）或上下排列（`Vertical`），每個子節點的 `Size` 是它的份額。`DockGroup` 是一張分頁卡片，每個 `DockPane` 一個分頁。`DockDocumentArea` 存放文件，即沒有 `PreferredSide` 的面板。每個面板都需要在版面內唯一的 `Id`，儲存的版面依它參照面板。

```xml
<arc:DockLayout>
    <arc:DockSplit Orientation="Horizontal">
        <arc:DockGroup Size="22">
            <arc:DockPane Id="explorer" Title="Explorer" Icon="{x:Static arc:ArcIcons.Folder}" PreferredSide="Left">
                <TextBlock Text="Explorer" />
            </arc:DockPane>
        </arc:DockGroup>
        <arc:DockSplit Orientation="Vertical" Size="78">
            <arc:DockDocumentArea Size="70">
                <arc:DockGroup>
                    <arc:DockPane Id="doc:readme" Title="README.md">
                        <TextBlock Text="Readme" />
                    </arc:DockPane>
                </arc:DockGroup>
            </arc:DockDocumentArea>
            <arc:DockGroup Size="30">
                <arc:DockPane Id="terminal" Title="Terminal" PreferredSide="Bottom">
                    <TextBlock Text="Terminal" />
                </arc:DockPane>
            </arc:DockGroup>
        </arc:DockSplit>
    </arc:DockSplit>
</arc:DockLayout>
```

面板本身就是內容宿主。移到其他群組時是同一個執行個體換了父層，內容的捲動位置、輸入和選取狀態都會保留。每個群組只有作用中面板在視覺樹中。

### 分頁與分割

分割可以任意巢狀，文件區也可以放一個由文件群組組成的分割，實現並排編輯。拖曳兩張卡片之間的縫即可調整大小；分隔線的行為與 `ResizablePanels` 相同（聚焦分隔線後用方向鍵、Home、End、Enter）。模型會自動整理：最後一個面板離開的群組會被移除，只剩一個子節點的分割由該子節點取代，同方向巢狀的分割會合併。文件區即使為空也會保留，此時顯示 `EmptyDocumentContent`。

<Demo name="TabsAndSplits" />

文件分頁帶關閉按鈕，滑鼠停留、選取或鍵盤聚焦時顯示；`IsModified` 時改為顯示圓點，指標移上去才換成關閉按鈕。工具分頁沒有關閉按鈕，可以從分頁選單或鍵盤關閉。分頁放不下時，分頁列可以用滾輪捲動，右側的箭頭按鈕列出全部分頁。

### 移動面板

在分頁上按右鍵或按 Shift+F10 開啟選單：關閉、關閉其他和全部關閉（文件），移動到 › 左側、右側、上方或下方新群組，以及停靠到 › 左側、右側、頂端或底端（工具；停靠到整個版面的一側）。在程式碼中，`Move` 接收一個 `DockTarget`：

```csharp
Layout.Move(terminal, DockTarget.Split(editorGroup, DockPosition.Bottom)); // a new group below, half of the group
Layout.Move(outline, DockTarget.Edge(DockPosition.Right));                // along the right edge, a quarter of the layout
Layout.Move(search, DockTarget.Into(explorerGroup, 0));                   // first tab of a group
Layout.Move(problems, DockTarget.DocumentArea(DockPosition.Left));        // beside the whole document area
```

文件只能留在文件區內，工具只有在 `CanDockAsDocument` 為 true 時才能進入文件區；違反規則的移動傳回 false，版面不變。`Open` 加入面板：不指定目標時，文件進入最近作用中的文件群組，工具加入 `PreferredSide` 相同的工具群組，沒有則沿該邊新建一個群組，寬或高為 `PreferredSize` 像素。`Close` 先引發可取消的 `PaneClosing`，再引發 `PaneClosed`；群組會選取右側（或左側）的分頁，被清空的群組會消失。

### 拖放停靠

沿分頁列拖曳分頁即可重新排序，其他分頁會讓位。把它拖到分頁列上方或下方超過 16px，或拖出所在的群組，就變成停靠拖曳：分頁的影子跟隨指標，指標下方的群組上出現羅盤，預覽框滑到面板將要落下的位置。

- 羅盤中間的按鈕把面板作為分頁加入該群組；四個方向按鈕分割該群組，新群組佔它的一半。
- 在文件區上方時，外面還有一圈四個按鈕，停靠到整個文件區的旁邊。
- 版面四條邊中點的按鈕停靠到整個版面的一側，佔版面的四分之一。
- 在分頁列上方時，一條直線標出分頁將插入的位置。
- 不在按鈕上時，由指標在群組內的位置決定：離某條邊四分之一以內分割那一側，中間則作為分頁加入。
- 拖曳分頁列的空白處可以移動整個群組。按 Escape 取消，面板回到原處。

規則與 `Move` 相同：文件只能留在文件區內，工具只有在 `CanDockAsDocument` 時才能進入文件區，群組不能落在自己身上，版面 `IsLocked` 時不能拖曳。不接受該面板的按鈕會變暗。把 `ShowDockIndicators` 設為 false 可以得到不顯示羅盤的安靜拖曳：指標在群組內或靠近版面邊緣的位置仍然決定停靠方向，只顯示預覽框。

<Demo name="DragToDock" />

### 浮動

把分頁拖到版面外放開、在分頁選單中選擇「浮動」，或呼叫 `Float`，面板就移到一個浮動視窗裡，出現在原來位置附近。視窗屬於版面所在的視窗：沒有工作列按鈕，始終在所屬視窗之上，主題和動效設定也隨之變化。它可以像版面一樣容納多個面板和群組。要停靠回去，把它的分頁或標題列拖到羅盤按鈕或分頁列上（視窗跟隨指標移動，版面同時顯示羅盤），或按兩下標題列，或在分頁選單中選擇「停靠」。`Dock` 讓面板回到原來的位置：原來的群組；群組已不存在時，回到原來的相鄰節點旁邊並恢復原來的尺寸。關閉視窗會關閉其中可以關閉的面板，其餘的停靠回去。

<Demo name="Floating" />

`FloatingMode` 決定面板如何浮動。`Auto` 在傳統桌面應用程式的桌面視窗中使用真正的視窗，在其他地方（例如瀏覽器）使用版面內的浮動卡片。`Window` 和 `Overlay` 強制使用其中一種，`Disabled` 讓所有面板保持停靠。浮動卡片使用彈出層的表面與陰影，拖曳標題列移動，拖曳右下角調整大小，始終留在版面範圍內，並帶有「停靠」和「關閉」按鈕。浮動面板是模型的一部分（`Workspace.Floats`，每個 `DockFloat` 有自己的樹和 `Bounds`）：在程式碼中新增一個就會開啟它的視窗，儲存的版面也會把它們帶回來；原來所在的顯示器已經拔除時，視窗會被拉回到某個螢幕上。

```csharp
Layout.Float(properties);                       // a window (or card) near the pane
Layout.Dock(properties);                        // back where it came from
Layout.FloatingMode = DockFloatingMode.Overlay; // cards inside the layout, also on the desktop
```

在 Linux 的 Wayland 下，應用程式不能決定視窗的位置：拖到版面外放開的面板出現在系統安排的位置，標題列使用系統的移動方式，浮動視窗只能從選單或按兩下標題列停靠回去。

### 自動隱藏

工具面板可以離開所在的群組，收到離該群組最近的版面邊緣的邊欄上：在分頁上按右鍵選擇「自動隱藏」、在面板內按 Shift+Escape，或呼叫 `AutoHide`。邊欄上顯示一個帶面板圖示和標題的按鈕，左右兩側的按鈕旋轉 90°。點擊按鈕或在按鈕上按 Enter，面板會以卡片形式從該邊滑出、覆蓋在內容上，寬或高等於它停靠時的尺寸（最多為版面的 70%），焦點隨之進入面板。點擊外部、按 Escape 或 Shift+Escape、或焦點移到別處時，卡片收回。拖曳卡片的內側邊緣可以調整尺寸。卡片標題列的圖釘按鈕、選單中的「固定」或 `Pin` 會把面板放回原來的群組；原群組已不存在時，在同一側新建一個群組。

<Demo name="AutoHide" />

自動隱藏的面板會隨版面一起儲存。在 XAML 中，把它們放進工作區的邊欄清單（`LeftHidden`、`RightHidden`、`TopHidden`、`BottomHidden`）；從未停靠過的面板依 `PreferredSize` 滑出：

```xml
<arc:DockLayout>
    <arc:DockLayout.Workspace>
        <arc:DockWorkspace>
            <arc:DockSplit Orientation="Horizontal">
                <arc:DockDocumentArea Size="75">
                    <arc:DockGroup>
                        <arc:DockPane Id="doc:readme" Title="README.md">
                            <TextBlock Text="Readme" />
                        </arc:DockPane>
                    </arc:DockGroup>
                </arc:DockDocumentArea>
                <arc:DockGroup Size="25">
                    <arc:DockPane Id="properties" Title="Properties" PreferredSide="Right">
                        <TextBlock Text="Properties" />
                    </arc:DockPane>
                </arc:DockGroup>
            </arc:DockSplit>
            <arc:DockWorkspace.RightHidden>
                <arc:DockPane Id="outline" Title="Outline" PreferredSide="Right" PreferredSize="220">
                    <TextBlock Text="Outline" />
                </arc:DockPane>
            </arc:DockWorkspace.RightHidden>
        </arc:DockWorkspace>
    </arc:DockLayout.Workspace>
</arc:DockLayout>
```

面板在邊欄上時，`State` 為 `AutoHidden`，`IsPinned` 為 false。文件不會自動隱藏；`CanAutoHide` 為 false 的工具保持停靠。浮動的面板不能自動隱藏（先停靠回去），自動隱藏的面板可以浮動；對自動隱藏的面板呼叫 `Dock` 等於固定它。滑出的卡片不能開始拖曳：先固定面板，或用選單中的「浮動」和「停靠到」。

### 最大化與面板切換器

按兩下群組分頁列的空白處，或在選單中選擇「最大化」，該群組會暫時佔滿整個版面，直到再次執行同一操作（此時選單顯示「還原」）或啟用其他群組的面板；版面本身不會改變。Ctrl+Tab 開啟面板切換器：依最近使用順序列出所有面板，並醒目提示上一個面板。按住 Ctrl 再按 Tab 繼續向後移動（Shift+Tab 向前），放開 Ctrl 即切換過去。`ToggleMaximize`、`ToggleMaximizeCommand`、`ShowPaneSwitcherCommand` 和 `AutoHideActiveCommand` 可以從程式碼或應用程式選單完成同樣的操作。

### 儲存與還原

`SaveLayout` 把版面寫成 JSON：帶份額的版面樹、作用中面板，以及自動隱藏邊欄和浮動視窗中的面板。面板只依 `Id` 儲存；`LoadLayout` 在版面用過的面板中尋找，找不到的交給 `PaneResolver`。未知的 Id 會被略過，儲存的版面中沒有提到的面板依 `PreferredSide` 放回。文字不是版面或來自較新的版本時，`LoadLayout` 傳回 false 並保持版面不變。在 `LayoutChanged` 時儲存即可自動記住版面。

<Demo name="SaveAndRestore" />

```csharp
Layout.LayoutChanged += (_, _) => settings.DockLayout = Layout.SaveLayout();
if (settings.DockLayout is { } saved)
{
    Layout.LoadLayout(saved);
}
```

### 來自集合的文件

把 `DocumentsSource`（以及 `ToolsSource`）繫結到一個集合，每一項都會成為一個面板，面板的 `DataContext` 就是該項。`DocumentTemplate`（`ToolTemplate`）負責產生面板：範本產出 `DockPane` 時直接使用，產出其他內容時包進一個新面板。沒有 `Id` 或 `Title` 的面板從資料項取值：資料項實作 `IDockItem` 時取 `IDockItem.DockId` 和 `DockTitle`，否則取 `ToString()`。新增一項會開啟它的面板（文件進入最近作用中的文件群組，工具依 `PreferredSide` 放置，未設定時放在左側），移除一項會關閉面板，且不觸發 `PaneClosing`。使用者關閉分頁時先觸發 `PaneClosing`；沒有處理常式取消、且集合是可寫入的清單時，該項會從集合中移除。儲存的版面依 Id 還原這些面板。

<Demo name="Mvvm" />

```xml
<arc:DockLayout DocumentsSource="{Binding OpenFiles}">
    <arc:DockLayout.DocumentTemplate>
        <DataTemplate>
            <arc:DockPane Id="{Binding Path}" Title="{Binding Name}" IsModified="{Binding IsDirty}">
                <TextBlock Text="{Binding Text}" />
            </arc:DockPane>
        </DataTemplate>
    </arc:DockLayout.DocumentTemplate>
    <arc:DockDocumentArea />
</arc:DockLayout>
```

作用中的文件是 `ActivePane.DataContext`；`ActivePaneChanged` 會在它變化時通知你。

### 鎖定

`IsLocked` 讓所有面板固定不動：分頁不能關閉、移動、浮動、自動隱藏或拖曳，選單命令無法使用。分隔線仍然可以調整，自動隱藏的面板仍然可以滑出，群組也仍然可以最大化。

<Demo name="Locked" />

## API 參考

<!-- api: DockLayout -->

<!-- api: DockPane -->

<!-- api: DockSplit -->

<!-- api: DockGroup -->

<!-- api: DockDocumentArea -->

## 無障礙

- 版面讀作名為「停靠版面」的群組，除非用 `AutomationProperties.Name` 另行命名。每個群組讀作分頁清單，每個分頁讀作以面板命名的分頁項目；修改過的文件會加上「已修改」。文件分頁的關閉按鈕名為「關閉 README.md」。
- 版面變化會禮貌播報：「Explorer 已停靠到 Terminal 右側」、「Search 已停靠到下邊緣」、「README.md，第 2 個分頁，共 2 個」、「Terminal 已移到新視窗」、「README.md 已關閉」。
- 羅盤按鈕的名稱為「停靠到左側」、「停靠到右側」、「停靠到頂端」、「停靠到底端」和「作為分頁停靠」。
- 邊欄按鈕讀作以面板命名的按鈕，說明文字為「已自動隱藏，按 Enter 顯示」；滑出的卡片以面板命名，面板切換器讀作清單。自動隱藏也會播報：「Explorer 已自動隱藏」。
- 指標能對面板做的每件事都有對應的選單命令（移動到、停靠到、浮動、停靠），選單可以用鍵盤開啟。

### 鍵盤

| 按鍵 | 作用 |
| --- | --- |
| Left / Right、Home / End | 在分頁上：移到群組內上一個、下一個、第一個或最後一個分頁並顯示其面板。 |
| Ctrl / ⌘ + Shift + Left / Right | 在分頁上：把分頁向左或向右移動一位。 |
| Delete | 在分頁上：關閉其面板（可關閉時）。 |
| Ctrl + F4、⌘ + W | 在分頁上或面板內：關閉作用中面板。 |
| Shift + F10、Menu 鍵 | 在分頁上：開啟選單並醒目提示第一列。 |
| 方向鍵、Home、End、Enter | 在分隔線上：調整大小，與 `ResizablePanels` 相同。 |
| Ctrl + Tab、Ctrl + Shift + Tab | 版面內任何位置：開啟面板切換器並醒目提示上一個（或最久未用的）面板；按住 Ctrl 時 Tab 繼續移動，放開 Ctrl 即切換。 |
| Up / Down、Enter、Escape | 在面板切換器中：移動醒目提示、切換，或不切換直接關閉。 |
| Shift + Escape | 在工具面板內：自動隱藏它。在滑出的面板內：把它收回。 |
| Enter、Space | 在邊欄按鈕上：滑出或收回其面板。 |
| Escape | 在滑出的面板內：收回它；焦點回到它的邊欄按鈕。 |
| Escape | 拖曳面板時：取消並放回原處。 |

面板在擁有焦點時被關閉，焦點會移到接替它的面板的分頁上。

## 動效

- 選取分頁的底色在分頁之間滑動，彈簧與 `Tabs` 相同；所在群組包含作用中面板時換成強調色。
- 分頁文字和操作按鈕的顏色在標準時長內漸變。
- 分隔線的跟手與落定與 `ResizablePanels` 相同。
- 沿分頁列拖曳分頁時，其他分頁以 snappy 彈簧讓位，放開後分頁彈回自己的位置。
- 預覽框在目標之間以 snappy 彈簧滑動；羅盤在 120ms 內淡入，同時從 92% 放大。
- 浮動卡片像彈出層一樣進場，從 97% 淡入。拖曳影子和浮動視窗直接跟隨指標。
- 自動隱藏的面板以平滑彈簧滑出，180ms 內收回。
- 透過命令移動的面板直接換位；版面靜止時沒有任何循環動畫。
- 減少動態效果：選取底色、分頁和預覽框直接跳到定位，不滑動；羅盤和浮動卡片只淡入淡出；自動隱藏的面板改為在 120ms 內淡入淡出，不再滑動。

## 響應式

- `DockLayout` 在任何寬度下都不改變形態：沒有斷點，也沒有緊湊版面。分割區共享所給的寬度。每個窗格低於其 `MinSize`（預設 120 px）時會增加阻力；當版面比所有最小值之和還窄時，最小值依比例縮小，窗格變窄。
- 它依自身寬度而不是視窗寬度量測，所以手機預覽或窄側邊欄中的版面與手機上一樣被擠壓。
- 浮動視窗至少 320 px 寬、240 px 高；自動隱藏的窗格滑出覆蓋內容時，最大為版面尺寸的 70%。
- 標籤放不下時，標籤列可用滾輪捲動，箭頭按鈕列出全部標籤。文件標籤的關閉按鈕只在懸停和選取的標籤上顯示；標籤選單以右鍵或 Shift+F10 開啟，所以不要依賴觸控關閉未選取的標籤。
- 在手機上用 `Tabs` 一次只顯示一個檢視，代替分割版面；說明頁建議它用於同一層級的檢視切換。

## 相關元件

- `ResizablePanels`：固定的一列或一欄可調整大小的窗格。
- `Tabs`：在同一層級的檢視之間切換。
- `ArcWindow`：帶 Arc 標題列的桌面視窗。
