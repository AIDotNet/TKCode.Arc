---
title: 可排序清單
summary: 透過直接拖放整理項目。
source-hash: e7d0a1b0e1f1
---

## 何時使用

- 使用者決定順序的短清單，例如優先順序或播放清單。
- 同一項工作需要同時支援拖曳與鍵盤操作的清單。
- 每列保留自己的文字。只有握把可以開始拖曳，因此文字仍可被選取。

## 何時不用

- 順序來自使用者可排序的欄位：用 `SortableDataTable`。
- 列需要滑動操作而非重新排序：用 `SwipeActions`。
- 長清單。列不做虛擬化，因此請保持清單簡短或將其拆分。

## 用法

每一列都會顯示一個握把。拖曳握把即可移動該列。清單會在 `Reordered` 中回報新的順序。

```xml
<arc:ReorderableList Label="優先順序"
                     Width="520"
                     ItemsSource="{Binding Priorities}" />
```

`ItemsSource` 接受 `ReorderableListEntry` 清單，每項包含 `Id`、`Title`，以及選用的 `Detail`。

```csharp
var priorities = new ObservableCollection<ReorderableListEntry>
{
    new("pricing", "發布定價頁", "行銷部 - 週五截止"),
    new("onboarding", "審閱新手導覽流程", "設計 - 3 則未解決的留言"),
    new("billing", "遷移帳單 Webhook", "平台")
};
var list = new ReorderableList { ItemsSource = priorities };
```

### 回應移動

`Reordered` 提供被移動的項目、其原索引與新索引。當 `ItemsSource` 為 `ObservableCollection` 且 `MovesItems` 為 `True`（預設值）時，清單會直接在集合中移動該項目。將 `MovesItems` 設為 `False`，可保持集合不變，並由你自行儲存順序。

```csharp
list.Reordered += (_, e) => status.Text =
    $"已將 {((ReorderableListEntry)e.Item!).Title} 從第 {e.OldIndex + 1} 位移到第 {e.NewIndex + 1} 位";

list.MoveItem(0, 2);   // 將第一列移到第三位
```

### 自訂項目範本

`ItemTemplate` 會取代預設的標題與詳細資料。`ItemTitleSelector` 提供用於無障礙名稱與播報的標題。

```csharp
var colors = new ReorderableList
{
    ItemsSource = new ObservableCollection<string> { "珊瑚色", "藏紅色", "薄荷色" },
    ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name }),
    ItemTitleSelector = item => item.ToString() ?? string.Empty
};
```

### 長度與捲動

當清單高於你提供的空間時，請將其放入 `ScrollViewer`。將某一列拖到捲動區域的邊緣即可捲動。

```xml
<ScrollViewer Height="340">
    <arc:ReorderableList ItemsSource="{Binding Tracks}" />
</ScrollViewer>
```

## API 參考

<!-- api: ReorderableList -->

<!-- api: ReorderableListEntry -->

## 無障礙

- 每個握把都是一個按鈕。其名稱包含項目標題與位置，並在每次移動後更新。
- 讓握把取得焦點後按 Up 或 Down，可將其所在列移動一個位置。焦點保留在被移動列的握把上。
- Space 拿起一列，方向鍵移動它，再按 Space 放下，Escape 則取消移動。
- 用於拖曳的只有握把可以取得焦點。列中的文字是一般可選取的文字區域。
- 設定 `Label` 可為清單指定輔助技術使用的名稱。預設名稱為「重新排序項目」。

## 動態效果

- 按住握把時，該列會輕微抬起。放開後以短促的彈簧回落。
- 拖曳時，該列跟隨指標移動，途經的列則以彈簧滑開讓出位置。
- 位置編號朝該列移動的方向滾動。
- 減少動態效果時，仍保留指標跟隨，但該列立即落位，不再抬起。關閉動態效果的表現與減少動態效果相同。

## 相關元件

- `SortableDataTable`：順序來自欄位數值而非人工選擇時使用。
- `SwipeActions`：為列提供快捷操作，而不是重新排序。
