---
title: 吸頂
summary: 讓捲動頁面裡的任意元素捲到頂端後停住，就像 CSS sticky：工具列始終可見，分組標題依序把上一個推走。
source-hash: 4472abbe401d
---

<Demo name="Preview" />

## 何時使用

`Sticky` 是一組附加屬性，不是控制項：在 `ScrollViewer` 內的任意元素上設定 `arc:Sticky.IsEnabled="True"`，它會正常捲動，
直到抵達檢視區頂端後停在那裡，效果與 CSS `position: sticky; top: 0` 相同。

- **工具列、篩選列**：在長頁面裡始終伸手可及。
- **分組清單的分組標題**：每個標題佔住頂端，直到下一組把它推走。
- **摘要資訊**：例如隨讀者往下填寫表單的購物車合計。

## 何時不用

- **元素根本不該捲動**：把它放在 `ScrollViewer` 之外，例如放在其上方的 `DockPanel` 中。
- **橫向捲動**：`Sticky` 只跟隨縱向位移；橫向捲動時保持不動的欄需要具凍結欄的表格。
- **元素已經有 `RenderTransform`**：`Sticky` 以自己的轉譯位移移動元素（見下文）。

## 用法

在捲動內容中的元素上設定附加屬性即可，不需要其他東西：不需要佔位元素，也不必改動 `ScrollViewer`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ScrollViewer Height="300">
        <StackPanel>
            <TextBlock Text="Title" Margin="16" />
            <Border arc:Sticky.IsEnabled="True" Background="White" Padding="16,8">
                <TextBlock Text="Toolbar" />
            </Border>
            <TextBlock Text="Long content" Height="1000" />
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

運作方式：

1. 元素監聽自己的有效檢視區（`ScrollViewer` 顯示出來的那部分）。檢視區上緣越過元素後，元素依差值向下位移，因此停在頂端。
2. 只改變元素的 `RenderTransform`，從不改變版面配置：內容高度不變，下方內容也不會跳動。
3. 元素不會離開它的父容器。父容器的下緣抵達元素時，元素隨父容器一起捲走。
4. 元素上方或周圍的內容稍後載入或改變尺寸時，會在下一次版面配置中重新計算。

### 吸頂狀態的樣式

元素停在頂端期間 `Sticky.IsStuck` 為 true。用選取器選中它來加上底色、邊線或陰影：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <UserControl.Styles>
        <Style Selector="Border.toolbar[(arc|Sticky.IsStuck)=True]">
            <Setter Property="BoxShadow" Value="{arc:ArcDesignTokenResource ShadowResting}" />
        </Style>
    </UserControl.Styles>
    <!-- ... -->
</UserControl>
```

內容會從元素下方捲過，所以請為元素設定不透明的背景。每當 `IsStuck` 變化，元素上會引發 `StuckChanged` 事件（冒泡，
參數為 `StickyStuckChangedEventArgs.IsStuck`）；在程式碼中使用 `Sticky.AddStuckChangedHandler` 與
`Sticky.RemoveStuckChangedHandler`。

### 分組清單

把每個標題和它的各列放在各自的面板中。標題只在本組可見時吸頂，所以下一組的標題到來時會把它推走，就像通訊錄一樣。

<Demo name="GroupedList" />

### 位移

`Sticky.Offset` 在檢視區上緣與吸頂元素之間留出間距，例如讓浮動的膠囊留些空隙，或停在另一條列的下方。

<Demo name="Offset" />

### 限制

- **RenderTransform**：元素不能有自己的 `RenderTransform`。若設定 `Sticky.IsEnabled` 時元素已有 `RenderTransform`，
  `Sticky` 不會改動該元素，並記錄一則警告。請把元素包在一個 `Border` 裡，改為讓外層吸頂。
- **ZIndex**：吸頂期間元素的 `ZIndex` 提升為 `Sticky.ZIndex`（預設 1），讓它繪製在下方捲過的內容之上，離開後還原。
  `ZIndex` 只對同一面板中的同層元素排序，所以嵌在某個面板深處的吸頂元素仍可能被後面同層面板的內容蓋住。
  請讓吸頂元素作為捲動內容的直接子元素，或為包含它的面板設定 `ZIndex`。

## API 參考

<!-- api: Sticky -->

## 協助工具

- `Sticky` 只在螢幕上移動元素。閱讀順序與 Tab 順序仍是內容本身的順序，元素裡的控制項在吸頂時照常可用。
- 吸頂的列會蓋住從下方捲過的內容。鍵盤焦點移到列下方的控制項時，`ScrollViewer` 會把它捲到頂端，列可能會擋住它；
  在需要的地方用 `Sticky.Offset` 或內容的上邊界留出空間。

## 動態效果

- 吸頂直接跟隨捲動位置，本身沒有動畫。
- 你附加在 `IsStuck` 上的變化（陰影、底色）可以漸變：像 Preview 那樣在 `:motion-full` 下宣告轉場，並在元素上設定
  `arc:ArcMotion.Tracks="True"`，這樣減少動態效果時它們會關閉。

## 響應式

- 元素保持自己的寬度與版面配置；吸頂時只改變它的縱向位置。
- 檢視區或內容尺寸改變時，會在下一次版面配置中重新計算位置。

## 相關元件

- `ScrollArea`：Arc 風格的捲動區域。
- `TocProgress`：隨讀者往下閱讀而跟進的目錄。
