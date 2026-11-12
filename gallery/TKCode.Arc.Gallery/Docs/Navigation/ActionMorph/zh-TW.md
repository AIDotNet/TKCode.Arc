---
title: Action morph
summary: 放在角落、用來新增最常用項目的按鈕：它展開成一份簡短的動作清單，所選動作把同一塊表面變成小表單，儲存成功後縮回按鈕並顯示勾號。
source-hash: 53ae8aa1f2da
---

<Demo name="Preview" />

## 何時使用

`ActionMorph` 讓人從固定在頁面角落的圓形按鈕新增一筆內容，例如一筆待辦或一則筆記。按下它不會跳出對話框：按鈕本身展開成一份簡短的動作清單，
選了其中一個，同一塊表面就變成一個精簡的表單，包含標題、輸入框、可選的一排選項和送出按鈕。項目儲存後，表單縮回按鈕，按鈕換上成功色並描出一個勾號；
稍後加號回來。返回上一層、關閉表面或點擊別處，表單裡已輸入的文字都會保留。

- **順手新增**：一筆待辦、筆記或行程，新增時它所在的清單始終看得到。
- **兩到四種新項目**，大家每天要加很多次，應該隨時按一下就到。
- **窄螢幕**：從角落按鈕長出的表單比對話框佔用更少空間。

## 何時不用

- **需要多個輸入框的項目**：放進 `Dialog` 或 `Drawer`。
- **不需要輸入文字的指令**：`DropdownMenu` 直接從清單執行它們。
- **用一個按鈕儲存畫面上已有的內容**：`ActionButton` 自己就會顯示忙碌與完成狀態。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <Panel>
        <!-- The page content goes first; the morph sits last, in a corner. -->
        <arc:ActionMorph HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="16">
            <arc:ActionMorphAction Label="To-do"
                                   Description="Something to finish"
                                   Glyph="{x:Static arc:ArcIcons.ListTodo}"
                                   Shortcut="d"
                                   Title="New to-do"
                                   Placeholder="What needs to get done?"
                                   ChoicesLabel="When"
                                   SubmitLabel="Add to-do"
                                   SuccessLabel="To-do added">
                <arc:ActionMorphAction.Choices>
                    <x:String>Today</x:String>
                    <x:String>This week</x:String>
                </arc:ActionMorphAction.Choices>
            </arc:ActionMorphAction>
            <arc:ActionMorphAction Label="Note"
                                   Glyph="{x:Static arc:ArcIcons.FileText}"
                                   Shortcut="n"
                                   Title="New note"
                                   IsMultiline="True"
                                   SubmitLabel="Save note"
                                   SuccessLabel="Note saved" />
        </arc:ActionMorph>
    </Panel>
</UserControl>
```

`ActionMorph` 位於 Special 套件：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。它不會自己定位：在版面裡只佔 56 px 的按鈕，
選單和表單從按鈕所在的角往外長，蓋在周圍內容之上，所以把它放在面板的最後、貼著某個角，並留出外距。

每個 `ActionMorphAction` 是一列選單以及它打開的表單：`Label`、可選的 `Description`、`Glyph` 和可選的單一字母 `Shortcut`，接著是表單的
`Title`、`Placeholder`、`IsMultiline`，可選的 `ChoicesLabel` 與 `Choices`（顯示為單選群組，先選取 `DefaultChoice`），以及 `SubmitLabel`、
`SuccessLabel` 和 `EmptyHint`。`Id` 和 `Tag` 供你區分各個動作。控制項上的 `Label` 是按鈕的名稱，也是選單的標題。

### 儲存送出的內容

`SubmitHandler` 收到一個 `ActionMorphSubmission`（動作、去掉前後空白的文字和所選項目）以及取消權杖。它的工作執行期間，送出按鈕顯示轉圈，
輸入框無法編輯；它擲出例外時，表單保持開啟、保留草稿並顯示 `ErrorMessage`。返回或關閉表單會取消權杖。送出被接受後引發 `Submitted`，
按鈕顯示勾號 `ResetAfter`（1.6 秒）。

```csharp
morph.SubmitHandler = async (submission, token) =>
{
    await plans.SaveAsync(submission.Action.Id, submission.Text, submission.Choice, token);
};
morph.Submitted += (_, e) => list.Insert(0, e.Submission.Text);
```

空白輸入不會送出：輸入框會被標示、輕輕晃一下，並顯示該動作的 `EmptyHint`（沒有時用控制項的）。開始輸入即清除提示。每個動作各自保留草稿，直到送出成功。

### 從程式碼開啟

`IsExpanded` 是雙向的。`Expand()`、`Collapse()`、`OpenComposer(action)` 和 `Back()` 在各個面之間切換，`View` 和 `CurrentAction`
表示目前顯示的面，按鈕展開和收回時分別引發 `Expanded` 和 `Collapsed`。

### 另一個角

`Corner="Start"` 用於左下角的按鈕：表面往上、往右長。

<Demo name="Corner" />

### 最佳做法

**建議**

- 提供兩到四個動作：該頁面上最常用的那幾個。
- 給每個動作一個字母快速鍵和一句簡短說明。
- 讓 `SubmitHandler` 等待真正的儲存；它擲出例外時，表單保留文字並顯示錯誤。

**避免**

- 需要多於一兩行文字和一個選擇的表單。
- 放在打開的選單會擋住輸入時需要看到的內容的地方。

## API 參考

<!-- api: ActionMorph -->

## 無障礙

- 按鈕以 `Label` 命名，並回報它會打開選單以及是否已展開。送出成功後，該動作的 `SuccessLabel` 會附加在按鈕名稱後面，並透過禮貌狀態訊息讀出。
- 打開的選單是以 `Label` 命名的選單；各列是以動作命名的選單項目，說明作為說明文字，快速鍵作為加速鍵。
- 表單是以標題命名的群組。輸入框同樣以標題命名；送出內容為空或失敗時，輸入框會被標示為無效，並以提示文字描述，提示會被禮貌地朗讀。選項是以 `ChoicesLabel` 命名的單選群組。
- 每個面出現時取得焦點（選單的第一列，或表單的輸入框）；表面縮回按鈕時，焦點回到按鈕。
- 鍵盤焦點環只在鍵盤操作後顯示在按鈕、表單按鈕和選項周圍；選單裡由醒目區塊表示目前的列。

### 鍵盤互動

| 按鍵 | 作用 |
|---|---|
| 按鈕上的 Enter 或 Space | 顯示選單，焦點在第一個動作上。 |
| 下 / 上 | 下一個或上一個動作；最後一個之後回到第一個，第一個之前是最後一個。 |
| Home / End | 第一個或最後一個動作。 |
| 快速鍵字母（選單開啟時） | 直接進入該動作的表單。 |
| 單行輸入框中的 Enter | 送出表單。 |
| 多行輸入框中的 Ctrl+Enter（macOS 上 ⌘+Enter） | 送出表單；單按 Enter 換行。 |
| 選項中的方向鍵 | 選取下一個或上一個選項；只有選取的選項是 Tab 停駐點。 |
| Escape | 往上一層：表單回到選單並聚焦它的動作，選單回到按鈕。 |

## 動態效果

- 一塊表面承載全部三個面。它的寬、高和圓角跟隨彈簧：變深約 0.46 秒到位，返回約 0.38 秒，都沒有過衝。底色、邊線和陰影在 240 到 480 毫秒內變化。
- 新的面從 4 px 模糊中短暫淡入。按鈕與面板之間，它從角落的 90% 長大；面板之間，更深的面從右側滑入，舊的被推向左邊，返回時方向相反。
- 選單各列依序上升 8 px，間隔 35 毫秒，醒目區塊滑到目前作用中的那一列。
- 儲存期間，轉圈取代送出按鈕的文字。儲存完成後表單縮進按鈕，按鈕填上成功色，勾號一筆描出；`ResetAfter` 時間到，勾號淡去，加號轉回原位。
- 送出內容為空或失敗時，輸入框往一側輕推一次。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 表面尺寸與圓角 | 彈簧 | 跳變 | 跳變 |
| 各個面 | 淡入、模糊與縮放或滑動 | 100–160 毫秒淡入淡出 | 直接切換 |
| 選單列 | 依序上升 | 同時出現 | 同時出現 |
| 按下、晃動 | 縮放到 0.94、輕推 | 無 | 無 |
| 勾號 | 彈入並描繪 | 淡入 | 直接顯示 |

## 響應式

- 寬度：選單 256 px，表單 320 px，都不超過檢視區寬度減 32 px；320 px 寬的手機能完整顯示任一者。
- 圓形按鈕為 56 px。只有滑鼠或觸控筆會產生懸停顏色；觸控永遠不會。
- 表面向上、並朝遠離 `Corner` 的一側展開；把那塊空間留給表單。

## 相關元件

- `OrbitMenu` 在按鈕周圍展開幾個動作，沒有表單。
- `DropdownMenu` 列出立即執行的動作。
- `Dialog` 和 `Drawer` 承載較長的表單。

| 元件 | 適用情境 |
|---|---|
| `ActionMorph` | 兩到四個新增動作各需要一行輸入，直接從角落按鈕完成 |
| `OrbitMenu` | 從一個按鈕立即執行幾個動作 |
| `Dialog` | 任務需要多個欄位或確認 |
