---
title: 按鈕群組
summary: 將相關操作合併到一個帶細分隔線的整體中：懸停高亮在各區段之間滑動，按下的區段就地回應，附加選單還可以收合整排按鈕。
source-hash: 4fde0cd5fbbf
---

<Demo name="DocumentActions" />

## 何時使用

- 同一物件上兩到五個相關操作，例如文件上的分享、複製與封存。
- 縮放控制項或小型編輯工具列，各區段作用於同一個物件。
- 較少使用或具有破壞性的操作，與常用操作放在一起，並收入附加選單。

## 何時不用

- 需要保持選取狀態的選項，例如檢視模式：使用 Toggles 中的 `SegmentedControl`。
- 一個主要操作加幾個相近的替代方案：使用 `SplitButton`。
- 在不同檢視或面板之間切換：使用 `Tabs`。

## 用法

每一個區段都是一個 `ButtonGroupItem`。為群組設定 `Label`，它就是無障礙名稱。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ButtonGroup Label="文件操作">
        <arc:ButtonGroupItem Label="分享" />
        <arc:ButtonGroupItem Label="複製" />
        <arc:ButtonGroupItem Label="封存" />
    </arc:ButtonGroup>
</UserControl>
```

### 圖示與純圖示區段

在程式碼中將 `Icon` 設為 24 乘 24 的 Lucide 圖形。`IsIconOnly` 會隱藏文字，此時文字會成為工具提示與無障礙名稱。

```xml
<arc:ButtonGroup Label="縮放" Size="Small">
    <arc:ButtonGroupItem Label="縮小" IsIconOnly="True" />
    <arc:ButtonGroupItem Label="放大" IsIconOnly="True" />
</arc:ButtonGroup>
```

### 即時數值與文字變更

`Value` 會在文字位置顯示內容，例如縮放比例。`Label` 仍是無障礙名稱。文字也可以在按下後變更；將其他文字列在 `ReserveLabels` 中，這個區段就會保留最寬的寬度。

<Demo name="LiveValue" />

```csharp
var zoomValue = new ButtonGroupItem { Label = "重設縮放", Value = "100%" };
zoomValue.Click += (_, _) => zoomValue.Value = "100%";

var share = new ButtonGroupItem { Label = "分享", ReserveLabels = ["已複製"] };
share.Click += async (_, _) =>
{
    share.Label = "已複製";
    await Task.Delay(1600);
    share.Label = "分享";
};
```

### 附加選單

`Menu` 會在整排末端新增一個箭頭區段。`MenuLabel` 為它命名。選單會與群組的邊緣對齊開啟，`MenuItemClicked` 會回報所選的列。

```xml
<arc:ButtonGroup Label="文件操作" MenuLabel="更多文件操作">
    <arc:ButtonGroupItem Label="分享" />
    <arc:ButtonGroupItem Label="封存" />
    <arc:ButtonGroup.Menu>
        <arc:MenuSurface>
            <arc:MenuEntry Header="釘選" />
            <arc:MenuEntry Header="刪除" IsDestructive="True" HasSeparatorBefore="True" />
        </arc:MenuSurface>
    </arc:ButtonGroup.Menu>
</arc:ButtonGroup>
```

### 變體、方向與事件

`Variant="Solid"` 使用主要填色，每個畫面最多使用一次。`Orientation="Vertical"` 會將各區段垂直堆疊，此時選單對齊到起始邊。`ActionInvoked` 會在某個區段自身的點擊處理常式之後執行。

<Demo name="Solid" />

<Demo name="Vertical" />

```csharp
document.ActionInvoked += (_, e) => log.Text = $"操作：{e.Item.Label}";
```

### 收合文字

當整排放不下時，各區段會隱藏文字並保留圖示；一旦有足夠空間容納完整的一排，文字就會恢復。

<Demo name="CollapsingLabels" />

### 停用

設定了 `IsDisabled` 的區段仍可取得焦點，並回報為無法使用；`IsEnabled` 為 `False` 的群組則整體變暗。

<Demo name="Disabled" />

## API 參考

<!-- api: ButtonGroup -->

## 無障礙

- 群組具有 Group 角色，並由 `Label` 命名。
- 每一個區段都是一個 Tab 停駐點。方向鍵會沿著群組的方向在各區段之間移動，並可循環。Home 與 End 分別移到第一個與最後一個區段。
- Enter 或 Space 會啟動某個區段。在選單區段上，Enter、Space 或 ArrowDown 會開啟選單。
- 純圖示區段與收合的區段以其文字作為名稱與工具提示，其圖示會被隱藏。
- 按下後不久變更的文字只會播報一次。狀態從不只靠顏色來表示。

## 動態效果

- 一個高亮會滑動到指標或鍵盤焦點處。高亮經過時，相鄰的分隔線會淡出。
- 按下的區段會輕微下沉其內容，然後以彈簧回彈。群組外框及相鄰的區段不會移動。
- 文字變更的區段會以彈簧調整尺寸；若已預留寬度，則改為交叉淡化。
- 選單從群組的邊緣展開，開啟期間箭頭會旋轉。
- 減少動態效果時，高亮直接跳到目標位置，取消按下下沉與箭頭旋轉，文字與選單改為交叉淡化。關閉動態效果時，所有內容立即到達最終狀態。

## 相關元件

- `SegmentedControl`：需要保持選取狀態的選擇。
- `SplitButton`：一個預設操作加一組替代方案的選單。
- `FloatingButtonGroup`：托盤中彼此獨立的柔和按鈕。
- `ExpandingButtonGroup`：緊湊的圖示按鈕，焦點時顯示文字。
</content>
