---
title: 滑動操作
summary: 透過滑動顯示列操作，或從選單中的同一組操作執行。
source-hash: a1c99fc6511f
---

## 何時使用

- 以觸控為主的待辦清單，例如郵件、通知或工作項目。
- 每一側一兩個快捷操作，例如封存或標為未讀。
- 可以透過完整滑動移除的列，移除後清單會自動填補空隙。

## 何時不用

- 桌面表格或主要用滑鼠操作的清單：用 `ContextMenu` 或 `DropdownMenu`。
- 透過拖曳上下調整列的順序：用 `ReorderableList`。
- 清單之外的單一危險操作：用 `HoldToConfirm`。

## 用法

`SwipeActions` 即清單本身。每一項是一個 `SwipeActionsRow`，或是透過 `ItemTemplate` 顯示的資料項目。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SwipeActions Label="收件匣" ItemsSource="{Binding Messages}">
        <arc:SwipeActions.LeadingActions>
            <arc:SwipeAction Label="未讀" Tone="Accent" KeepsRow="True" />
        </arc:SwipeActions.LeadingActions>
        <arc:SwipeActions.TrailingActions>
            <arc:SwipeAction Label="封存" />
            <arc:SwipeAction Label="刪除" Tone="Danger" />
        </arc:SwipeActions.TrailingActions>
        <arc:SwipeActions.ItemTemplate>
            <DataTemplate>
                <TextBlock Text="{Binding Subject}" />
            </DataTemplate>
        </arc:SwipeActions.ItemTemplate>
    </arc:SwipeActions>
</UserControl>
```

前導與尾隨操作由每一列共用。某一側最外層的操作即為完整滑動時執行的操作。

### 執行操作

選取操作時，`Command` 會執行，可以透過點按、完整滑動或「更多操作」選單觸發。`Invoked` 與 `ActionInvoked` 會告訴你是哪一列、哪一項。`KeepsRow="True"` 會執行操作並讓列回到原位，而不是將其移除，適合「未讀」這類切換操作。

```csharp
inbox.ActionInvoked += (_, e) =>
{
    if (!e.Action.KeepsRow)
    {
        messages.Remove((Message)e.Item!);
    }
};
```

在處理常式中移除資料項目即可。列會先滑出，清單隨後填補空隙，因此移除時不會跳動。

### 列自帶操作

`SwipeActionsRow` 宣告自己的操作。`IsFullSwipeEnabled="False"` 會阻止該列在完整滑動時直接確認執行，因此使用者必須點按露出的操作。

```xml
<arc:SwipeActions Label="待辦" Width="440">
    <arc:SwipeActionsRow Label="審閱拉取要求 #418" IsFullSwipeEnabled="False">
        <arc:SwipeActionsRow.TrailingActions>
            <arc:SwipeAction Label="標記" KeepsRow="True" />
            <arc:SwipeAction Label="刪除" Tone="Danger" />
        </arc:SwipeActionsRow.TrailingActions>
        <TextBlock Text="審閱拉取要求 #418" VerticalAlignment="Center" />
    </arc:SwipeActionsRow>
</arc:SwipeActions>
```

### 保持展開

`Open` 無需手勢即可顯示某一列的操作。同一清單中一次只能展開一列。

```csharp
open.AttachedToVisualTree += (_, _) => open.Open(SwipeActionSide.Trailing);
```

`Close` 收合已展開的列，`SwipeActions.CloseRows()` 則關閉清單中已展開的列。

## API 參考

<!-- api: SwipeActions -->

<!-- api: SwipeAction -->

<!-- api: SwipeActionsRow -->

## 無障礙

- 清單的名稱來自 `Label`，每一列都是清單項目。每一列都有一個「更多操作」按鈕，名稱為「更多操作：」加上該列的 `Label`。
- 滑鼠與鍵盤使用者無需使用手勢：按 Tab 移到「更多操作」按鈕，按 Enter 或 Space 開啟選單，再用方向鍵移動。
- 按 Esc 關閉選單或收合已展開的列。在其他位置按下時也會收合已展開的列。
- 露出的操作對輔助技術隱藏，且不在 Tab 順序中。
- 當某一列透過選單被移除時，焦點移到相鄰的列。

## 動態效果

- 列跟隨指標移動。超過最後一個停靠點後會產生阻力，因此列會稍微拉伸，然後回彈。
- 放開後，列以保留放開速度的彈簧停靠到最近的停靠點。快速輕滑會被視為滑向對應方向的停靠點。
- 完整滑動時，最外層的操作會橫向拉伸鋪滿整列，列隨後滑出，清單再填補空隙。
- 減少動態效果時，列直接跳到停靠點，列的出現與消失改為淡入淡出，不做高度動畫。關閉動態效果時，所有內容一次移動到位。

## 相關元件

- `ContextMenu` 與 `DropdownMenu`：桌面端的列操作。
- `ReorderableList`：透過拖曳將列重新排序。
- `HoldToConfirm`：清單之外的單一破壞性操作。
