---
title: Glass menu
summary: 一個磨砂玻璃選單：醒目提示在指標下逐列滑動，內部一團柔光沿著該列跟隨指標。
source-hash: e2b26fe72d49
---

<Demo name="Preview" />

## 何時使用

`GlassMenu` 是放在磨砂玻璃上的一列命令：選單會模糊並著色身後的內容，因此它浮在圖片、畫布或彩色頁面上，而不會把它們完全遮住。一塊更亮的玻璃醒目提示標出指標下或鍵盤焦點所在的列。它在列與列之間滑動而不是跳動，內部一團柔光沿著該列跟隨指標，讓整個選單像一塊受光的玻璃。它可以放在頁面中，也可以從按鈕彈出，執行命令後關閉。

- **畫布或照片上的操作**：在編輯器裡排列、組合、鎖定或分享一個物件。
- **媒體上的內容選單**：儲存、複製或開啟一張圖片或一段影片。
- **簡短的「更多」選單**：放在玻璃工具列或玻璃卡片上。

## 何時不用

- **很長或有子選單的選單**：玻璃適合少量命令。子選單、可勾選項目和長清單請用 `DropdownMenu`。
- **為表單選擇一個值**：使用 `Select`；選單是用來執行命令的。
- **純色背景上的選單**：沒有可以模糊的內容時，使用 `DropdownMenu` 或 `ContextMenu`。

## 用法

直接寫出各列：命令用 `GlassMenuItem`（可選的 `Icon`、文字和可選的 `Shortcut` 提示），分組之間用 `GlassMenuSeparator`。`GlassMenuItem` 是一個 `Button`，所以它接受 `Command` 並觸發 `Click`；每執行一個命令，選單還會觸發 `ItemInvoked`。

```xml
<Panel arc:GlassBackdrop.Source="{Binding #Scene}">
    <Border Name="Scene" Background="#2B3A55" /><!-- your picture or page -->
    <arc:GlassMenu>
        <arc:GlassMenuItem Content="Duplicate" Icon="{x:Static arc:ArcIcons.Copy}" Shortcut="Ctrl+D" />
        <arc:GlassMenuSeparator />
        <arc:GlassMenuItem Content="Move to trash" Icon="{x:Static arc:ArcIcons.Trash2}" IsDestructive="True" />
    </arc:GlassMenu>
</Panel>
```

`GlassMenu` 衍生自 Avalonia 的 `ItemsControl`，位於 Special 套件中：在 `UseArc()` 旁呼叫 `UseArcSpecial()` 註冊。磨砂效果與所有玻璃表面相同：它如何以公開的 Avalonia API 實作、何時設定 `arc:GlassBackdrop.IsLive`，見 Glass card 頁面。

### 作為彈出選單

用 `arc:GlassMenu.Menu` 把選單附加到按鈕上。點按後選單在按鈕下方開啟；按 Down、Enter 或 Space 開啟時，第一個命令取得焦點。執行命令、按 Escape（焦點回到按鈕）或點按外部時選單關閉。對於不在頁面中的選單，可以在程式碼中呼叫 `Open(anchor)`、`Close()` 或 `Toggle(anchor)`；`Placement` 設定它開啟的方向。

<Demo name="Popup" />

```xml
<arc:Button Content="Arrange">
    <arc:GlassMenu.Menu>
        <arc:GlassMenu>
            <arc:GlassMenuItem Content="Bring to front" Shortcut="Ctrl+]" />
            <arc:GlassMenuItem Content="Send to back" Shortcut="Ctrl+[" />
        </arc:GlassMenu>
    </arc:GlassMenu.Menu>
</arc:Button>
```

### 來自資料的列

`ItemsSource` 中的任何項目都會成為顯示其文字的列。`ItemInvoked` 在 `Item` 中傳回資料項目，在 `Container` 中傳回對應的列。

<Demo name="Data" />

### 內容建議

- 文字以動詞開頭並保持簡短：「Duplicate」「Move to trash」。
- 要嘛每一列都有圖示，要嘛都沒有，這樣文字才能對齊。
- 危險的命令放在最後，用分隔線隔開，並用 `IsDestructive` 標記。
- 快速鍵提示只是文字；快速鍵本身請用 `HotKey` 或按鍵繫結註冊。

## API 參考

<!-- api: GlassMenu -->

<!-- api: GlassMenuItem -->

<!-- api: GlassMenuSeparator -->

<!-- api: GlassMenuItemInvokedEventArgs -->

## 無障礙

- 選單是一個選單，每個命令是一個選單項目，以文字為名稱；`Shortcut` 作為快速鍵回報。分隔線不作為內容朗讀。
- 醒目提示跟隨鍵盤焦點，因此它同時就是焦點指示；停用的列會被略過。
- 以鍵盤開啟彈出選單時第一個命令取得焦點；以 Escape 或執行命令關閉時，焦點回到按鈕，並顯示鍵盤焦點環。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 進入選單（第一個命令）或離開 |
| `Down` / `Up` | 下一個或上一個命令，循環 |
| `Home` / `End` | 第一個或最後一個命令 |
| `Enter` / `Space` | 執行取得焦點的命令 |
| 一個字母 | 下一個以它開頭的命令 |
| `Escape` | 關閉彈出選單並回到按鈕 |
| 在按鈕上按 `Down`、`Enter` 或 `Space` | 開啟選單，第一個命令取得焦點 |

## 動效

- 醒目提示以靈敏彈簧在列之間滑動（約 0.26 秒，輕微回落）；第一次出現時在 160ms 內淡入，指標和焦點都離開時淡出。
- 柔光以平滑彈簧沿列跟隨指標，在 160ms 內淡入。
- 彈出選單淡入時上升 4px，並從 97% 放大到原大小。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 醒目提示移到另一列 | 滑動 | 立即移動 | 立即移動 |
| 醒目提示出現或離開 | 淡入淡出 | 淡入淡出 | 立即 |
| 指標柔光和光澤 | 跟隨指標 | 無 | 無 |

## 響應式

- 選單寬度至少 220px，並與最長的一列一樣寬；空間不夠時，長文字以省略號結尾。
- 按鈕下方沒有空間時，彈出選單會翻到按鈕另一側。
- 觸控時點按即執行命令，沒有懸停醒目提示。

## 相關元件

- `DropdownMenu` 和 `ContextMenu`：帶子選單和可勾選項目的完整選單。
- `GlassCard`、`GlassSegmented` 和 `GlassTabBar`：其他玻璃表面。

| 元件 | 適用情境 |
|---|---|
| `GlassMenu` | 一個簡短的選單要浮在圖片或畫布上 |
| `DropdownMenu` | 一般頁面上的完整選單 |
| `ContextMenu` | 按右鍵時的命令 |
