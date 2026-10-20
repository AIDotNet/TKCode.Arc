---
title: 矩形樹狀圖
summary: 方形化的矩形樹狀圖：點擊即可下鑽，色塊放大填滿檢視，麵包屑可返回，指標變化時每個色塊平順過渡。
source-hash: 41b94359ed01
---

## 何時使用

- 葉節點很多的階層資料，例如依地區、國家與方案劃分的營收，需要看清每一部分所佔的比例。
- 需要在大小之外再讀取的第二個指標，例如成長率，以每個色塊顏色的深淺表示。
- 向下鑽取某個分支再返回的情境，麵包屑會保持路徑可見。

## 何時不用

- 數值彼此接近、需要精確比較：用 `BarChart`。
- 只有少數幾個部分構成整體，角度即可說明問題：用 `DonutChart`。
- 只是一組普通的導覽步驟：單獨使用 `Breadcrumb`。

## 用法

色塊的大小取自 `Value`，深淺取自 `ColorValue`。分支本身沒有數值，其大小是子節點之和。`Label` 為整體命名，同時也是麵包屑的第一級。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Treemap Label="營收"
                 ColorLabel="成長率"
                 ItemsSource="{Binding Regions}"
                 KeyMemberBinding="{Binding Code}"
                 LabelMemberBinding="{Binding Name}"
                 ValueMemberBinding="{Binding Revenue}"
                 ColorValueMemberBinding="{Binding Growth}"
                 ChildrenMemberBinding="{Binding Countries}" />
</UserControl>
```

### 節點樹

資料在程式碼中建立時，將 `Root` 設為由 `TreemapNode` 物件組成的樹。樹中每個 `Id` 都必須唯一：保留相同 `Id` 的色塊在資料變化時會平順過渡到新的大小。

```csharp
var root = new TreemapNode("all", "全部地區", children:
[
    new TreemapNode("amer", "美洲", children:
    [
        new TreemapNode("us", "美國", 420, 18),
        new TreemapNode("ca", "加拿大", 80, 12)
    ]),
    new TreemapNode("emea", "歐洲", children:
    [
        new TreemapNode("de", "德國", 190, 7),
        new TreemapNode("uk", "英國", 150, -2)
    ])
]);

chart.Label = "營收";
chart.Root = root;
```

如果不設定 `ColorLabel`，每個色塊都以同一深度繪製。若只需要一組扁平的頂層葉節點，使用 `Items`：

```xml
<arc:Treemap Label="依資料夾統計的儲存空間" PlotHeight="260" />
```

```csharp
chart.Items.Add(new TreemapNode("video", "影片", 112));
chart.Items.Add(new TreemapNode("photos", "相片", 48));
chart.Items.Add(new TreemapNode("docs", "文件", 12));
```

### 格式化與著色

`ValueFormatter` 用於格式化大小，`ColorFormatter` 用於格式化著色指標。`ColorDomainMinimum` 與 `ColorDomainMaximum` 固定著色範圍的兩端；預設情況下，它們分別是最小與最大的葉節點數值。

```xml
<arc:Treemap Label="席次"
             ColorLabel="成長率"
             ValueFormatter="{Binding FormatSeats}"
             ColorFormatter="{Binding FormatGrowth}"
             ColorDomainMinimum="-10"
             ColorDomainMaximum="40" />
```

### 切換指標

同時修改 `Label`、`ValueFormatter` 與 `Root`。`Id` 相同的色塊會保有其身分，因此它們會在原處改變大小與顏色，而不是被取代。

```csharp
void ShowSeats()
{
    chart.Label = "席次";
    chart.ValueFormatter = v => $"{v:#,0}";
    chart.Root = seatsRoot;
}
```

### 焦點與縮放

點擊某個分支會讓它填滿檢視。`FocusedNodeId` 保存目前檢視中節點的 id（`null` 表示根節點），也可以在程式碼中設定它，將檢視縮放到指定節點。`FocusedNodeChanged` 會在每次變化時發出通知。

```xml
<arc:Treemap Label="營收" Root="{Binding Tree}" FocusedNodeId="{Binding FocusedId, Mode=TwoWay}" />
```

```csharp
chart.FocusedNodeChanged += (_, e) => status.Text = $"目前檢視：{e.Node?.Label}";
```

## API 參考

<!-- api: Treemap -->

<!-- api: TreemapNode -->

## 無障礙

- `Label` 為圖表命名。每個色塊都是可聚焦的群組，其名稱說明路徑、數值及其佔父層級的比例。
- 方向鍵移動到該方向上最近的色塊；Home 與 End 分別跳到第一個與最後一個頂層色塊；Enter 或空白鍵會縮放到目前聚焦的分支。
- Escape 或 Backspace 縮小一級。麵包屑是導覽地標，並標示目前層級。
- 縮放變化以及聚焦色塊的路徑、數值與著色，會以禮貌方式朗讀。
- 一個隱藏的資料表列出每個節點，因此即使看不到色塊，也能讀取整個階層結構。

## 動態效果

- 放大時，所選色塊透過一次仿射移動填滿檢視：其同層色塊飛出，其子節點在其中展開。縮小時沿相同路徑反向進行。
- 更改指標時，每個色塊在原處移動到新的大小與顏色，總計數值捲動到新值。
- 縮放過程中，標籤保持其大小。只有標籤能放入色塊時才會顯示。
- 減少動態效果時，色塊立即定位到最終位置。關閉動態效果的表現與減少動態效果相同。

## 相關元件

- `BarChart`：數值彼此接近時的精確比較。
- `DonutChart`：少數幾個部分構成整體的情境。
- `Breadcrumb`：不屬於圖表的導覽路徑。
