---
title: 樹狀檢視
summary: 瀏覽巢狀的資料夾與結構化內容。
source-hash: e2907dd4fd1e
---

<Demo name="FileTree" />

## 何時使用

- 資料夾、檔案以及其他可展開、可選取的巢狀分類。
- 選取項目會驅動樹狀結構旁邊內容顯示的導覽，例如 `CodeBlock`。
- 大型階層結構。列會被池化，只建立可視範圍附近的列。

## 何時不用

- 少量可收合的分區：用 `Accordion`。
- 頁面階層中的目前位置：用 `Breadcrumb`。
- 一份 JSON 文件的鍵與值：用 `JsonViewer`。
- 使用者可重新排序的扁平清單：用 `ReorderableList`。

## 用法

節點可以直接在 XAML 中內嵌宣告。帶有子項目的 `TreeViewItem` 即為資料夾。

```xml
<arc:TreeView MinWidth="260" ShowIndentGuides="True">
    <arc:TreeView.Items>
        <arc:TreeViewItem Header="src" IsExpanded="True">
            <arc:TreeViewItem.Items>
                <arc:TreeViewItem Header="components">
                    <arc:TreeViewItem.Items>
                        <arc:TreeViewItem Header="button.tsx" />
                        <arc:TreeViewItem Header="tree-view.tsx" />
                    </arc:TreeViewItem.Items>
                </arc:TreeViewItem>
                <arc:TreeViewItem Header="lib" />
            </arc:TreeViewItem.Items>
        </arc:TreeViewItem>
        <arc:TreeViewItem Header="README.md" />
    </arc:TreeView.Items>
</arc:TreeView>
```

### 使用子項目選擇器繫結資料

繫結 `ItemsSource`，並在程式碼中設定選擇器。`ChildrenSelector` 會傳回節點的子項目，帶有子項目的節點即為資料夾。

<Demo name="DataWithChildrenSelector" />

```csharp
var tree = new TreeView
{
    ItemsSource = nodes,
    ChildrenSelector = item => ((FileNode)item).Children,
    HeaderSelector = item => ((FileNode)item).Name,
    Width = 300
};
tree.SelectionChanged += (_, e) => selected.Text = $"已選取項目：{e.NewItem}";
tree.SelectedItem = nodes[0].Children![2];   // 會展開該節點的上層節點
```

`SelectedItem` 為雙向繫結，因此可以繫結到檢視模型：

```xml
<arc:TreeView ItemsSource="{Binding Files}"
              SelectedItem="{Binding SelectedFile, Mode=TwoWay}" />
```

### 圖示

`TreeViewItem` 上的 `Icon` 接受一個 `Geometry`。`ShowIcons` 控制整棵樹是否顯示圖示，`IconSelector` 為資料節點提供圖示。

```csharp
tree.ShowIcons = true;
tree.IconSelector = item => ((FileNode)item).Children is null ? fileGlyph : folderGlyph;
```

### 從程式碼展開與收合

```csharp
tree.ExpandAll();
tree.CollapseAll();
tree.ToggleExpanded(folderNode);
tree.ScrollIntoView(fileNode);
tree.FocusItem(fileNode);
```

### 事件

`ItemInvoked` 在一列被點選或按下 Enter 啟用時觸發。`ItemExpanded` 和 `ItemCollapsed` 回報展開與收合的資料夾。

```csharp
tree.ItemInvoked += (_, e) => OpenFile(e.Item);
tree.ItemExpanded += (_, e) => log.Text = $"已開啟 {e.Item}";
```

### 數千個節點

這棵樹有 40 個資料夾，每個資料夾含 250 個檔案。列會被虛擬化並池化，只有可視範圍附近的列會播放動畫。

<Demo name="ThousandsOfNodes" />

## API 參考

<!-- api: TreeView -->

<!-- api: TreeViewItem -->

## 無障礙

- 樹狀檢視預設名稱為「檔案樹」。設定 `AutomationProperties.Name` 可依你的內容為其命名。
- 各列公開樹狀項目角色，並提供層級、在同層級中的位置、展開狀態與選取狀態。
- 只有取得焦點的列在 Tab 順序中。方向鍵、Home 與 End、PageUp 與 PageDown 在列之間移動。
- 右方向鍵展開資料夾或進入其第一個子項目，左方向鍵收合資料夾或移到其上層。
- Enter 或 Space 選取該列並切換資料夾的展開狀態。鍵入名稱會移到下一個以該文字開頭的列。
- 焦點框僅在鍵盤聚焦時繪製。圖示與縮排參考線不會被朗讀。

## 動態效果

- 展開資料夾時，其空間以彈簧方式增長，新列從左側依序錯落進入。
- 選取高亮在各列之間平滑滑動，資料夾箭頭以俐落的彈簧轉動。
- 收合資料夾時，其列平滑收起。
- 減少動態效果時列立即顯示或隱藏。關閉動態效果時直接到達最終狀態。

## 相關元件

- `Accordion`：用於少量可展開與收合的內容分區。
- `Breadcrumb`：顯示目前頁面在階層中的位置。
- `JsonViewer`：用於供人檢視、可搜尋與複製的巢狀資料。
- `CodeBlock`：顯示樹狀檢視中所選檔案的原始碼。
