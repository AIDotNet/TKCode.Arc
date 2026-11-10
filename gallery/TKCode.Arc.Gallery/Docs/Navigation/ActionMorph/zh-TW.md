---
title: Action morph
summary: 一個會長成小型內嵌面板的按鈕，面板裡放它的動作所需的內容，例如一個欄位或幾個選項，完成後再縮回按鈕。
source-hash: 11724fad05dc
---

<Demo name="Preview" />

## 何時使用

`ActionMorph` 是一個會變成動作發生之處的按鈕。按下「New list」不會開啟對話框：按鈕本身會以彈簧動畫變寬變深，成為一個小面板，裡面有一個欄位和一個 Create 按鈕，清單建立後，面板再縮回按鈕。動作留在使用者原本注視的地方，周圍的頁面只會依面板所需而移動。

- **快速建立**：新清單的名稱、標籤、資料夾。
- **簡短的選擇**：延後一小時、今晚或明天再提醒。
- **工具列與列內的內嵌輸入**，用對話框會太重。

## 何時不用

- **有多個欄位或驗證步驟的表單**：請用對話框或頁面。
- **有後果、需要警告的動作**：請用確認對話框。
- **版面不能移動的地方**：面板會佔用鄰近元素旁邊的空間；請改用 `Popover`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActionMorph Label="New list">
        <StackPanel Orientation="Horizontal" Spacing="8">
            <arc:Input Placeholder="List name" Width="180" />
            <arc:Button Content="Create" Variant="Primary" Size="Small" arc:ActionMorph.Closes="True" />
        </StackPanel>
    </arc:ActionMorph>
</UserControl>
```

`ActionMorph` 位於 Special 套件：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。`Label` 和 `Glyph` 組成按鈕，內容則是面板。面板開啟時，焦點會移到它的第一個可取得焦點的元素。它可以用 Escape、它的關閉按鈕、在外面按一下、`Collapse()`，或點擊裡面任何標記了 `arc:ActionMorph.Closes="True"` 的按鈕來收回。`IsExpanded` 是雙向的，開啟和關閉時會觸發 `Expanded` 和 `Collapsed`：請在 `Collapsed` 中清除面板的欄位。

`CollapsesOnOutsidePress` 可以讓使用者點擊別處時保持開啟，而當內容自己有離開的方式時，`ShowsCloseButton` 可以隱藏關閉按鈕。

### 主要外觀與選項

`Variant="Primary"` 會讓按鈕填上強調色。開啟的面板永遠是一般的表面，所以填色會流入其中。幾個標記了 `Closes` 的幽靈按鈕就能構成一個快速選擇。

<Demo name="Options" />

### 最佳實務

**建議**

- 用動作來命名按鈕：「New list」、「Add tag」、「Remind me」。
- 讓面板保持在一行或一個短的欄，這樣看起來就像按鈕的一部分。
- 用 `arc:ActionMorph.Closes="True"` 標記完成動作的那個按鈕。

**避免**

- 把變形元件放在它的擴展會把重要內容擠出視野的地方。
- 在面板中放長內容：它不是一個頁面。

## API 參考

<!-- api: ActionMorph -->

## 無障礙

- 按鈕由 `Label` 命名，並回報它的面板是否已展開。開啟的面板是一個同名的群組，關閉按鈕由 `CloseLabel` 命名。
- 面板開啟期間，按鈕不在 Tab 順序中；焦點會移入面板，關閉時再移回按鈕。
- 鍵盤焦點會在按鈕周圍顯示統一的焦點環；使用指標時則不顯示。

### 鍵盤互動

| 按鍵 | 動作 |
|---|---|
| 按鈕上的 Enter 或 Space | 開啟面板並讓它的第一個元素取得焦點。 |
| Tab | 在面板的內容和它的關閉按鈕之間移動。 |
| Escape | 收起面板並把焦點移回按鈕。 |

## 動效

- 開啟與關閉是同一個表面在以變形彈簧改變寬度和高度，圓角從按鈕的 18 px 變成面板的 20 px，陰影隨之升高。
- 面板淡入時按鈕淡出；收回時面板先保留它的空間直到淡出，然後表面縮回按鈕。
- 移動期間只有這個變形元件會重新配置；它旁邊的內容會跟著一起移動。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 大小 | 變形彈簧 | 直接跳轉 | 直接跳轉 |
| 圓角、填色與陰影 | 彈簧與 240ms | 填色 120ms | 立即變化 |
| 按鈕與面板 | 交叉淡化 | 交叉淡化 | 直接切換 |

## 響應式行為

- 面板至少 240 px 寬，其餘情況與內容一樣寬。請讓內容窄到能放進 320 px 的手機。
- 按鈕保持它的高度（36 px），寬度跟隨它的標籤。
- 面板變大時，同一列的鄰近元素會移動；若這很重要，請把變形元件放在一列的最後。

## 相關元件

- `Popover` 在頁面上方開啟內容，而不移動頁面。
- `OrbitMenu` 在一個按鈕周圍提供幾個動作。

| 元件 | 何時選擇 |
|---|---|
| `ActionMorph` | 一個動作需要少量輸入，就在按鈕所在的地方 |
| `Popover` | 內容不能讓版面移動 |
| `Dialog` | 任務需要多個欄位或一個確認 |
