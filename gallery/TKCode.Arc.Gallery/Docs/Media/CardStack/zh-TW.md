---
title: 卡片堆疊
summary: 逐張審閱卡片，支援划走與復原。
source-hash: 12416cbb0e8f
---

## 何時使用

- 逐項處理一個佇列，例如候選清單、審閱佇列或一組偏好設定。
- 每張卡片都需要明確的「是」或「否」，且閱讀者可以撤回決定。
- 卡片堆清空後，可以從頭再次審閱。

## 何時不用

- 並排比較或排序項目：用 `SortableDataTable`。
- 只瀏覽而不做決定：用 `Carousel`。
- 清單列上的快速操作：用 `SwipeActions`。

## 用法

`ItemsSource` 提供卡片，`ItemTemplate` 繪製每張卡片的正面。`Label` 為整個堆疊命名。

```xml
<arc:CardStack Label="目的地"
               LeftLabel="跳過"
               RightLabel="保留"
               LeftOutcome="已跳過"
               RightOutcome="已入選">
    <arc:CardStack.ItemTemplate>
        <DataTemplate>
            <Border Width="300" Height="360" Padding="20" CornerRadius="12" Background="#F5F1EA">
                <TextBlock Text="{Binding}" FontSize="18" />
            </Border>
        </DataTemplate>
    </arc:CardStack.ItemTemplate>
</arc:CardStack>
```

### 為卡片命名

設定 `LabelSelector` 以傳回每張卡片的名稱。此名稱用作卡片的無障礙標籤，也用於播報。當項目不是字串時，選擇器必須傳回字串。

```csharp
stack.LabelSelector = item => ((Destination)item!).Name;
```

### 自訂按鈕與標記文字

`LeftLabel` 與 `RightLabel` 為按鈕，以及拖曳卡片時出現的標記命名。`LeftOutcome` 與 `RightOutcome` 是播報中使用的過去式詞語，例如「已跳過」。

```xml
<arc:CardStack Label="候選人" LeftLabel="暫不" RightLabel="面試" />
```

### 在程式碼中做決定與復原

`Decide` 會依指定方向划走頂部卡片。`Undo` 將最後一張卡片放回，`CanUndo` 說明是否有可放回的卡片。`Reset` 將所有卡片放回堆疊。

```csharp
stack.Decided += (_, e) => log.Text = $"{((Destination)e.Item!).Name}：{(e.Decision == CardDecision.Right ? "已入選" : "已跳過")}";
stack.DecisionUndone += (_, e) => log.Text = $"{((Destination)e.Item!).Name}：已還原";
stack.StackReset += (_, _) => log.Text = "所有卡片已放回堆疊。";

stack.Decide(CardDecision.Right);
stack.Undo();
stack.Reset();
```

### 空狀態

`EmptyContent` 會取代所有卡片都已做出決定後顯示的預設提示。將按鈕繫結到 `ResetCommand`，即可提供重新審閱的入口。

```xml
<arc:CardStack Label="候選人" ItemsSource="{Binding Candidates}">
    <arc:CardStack.EmptyContent>
        <Button Content="再次審閱" Command="{Binding ResetCommand, RelativeSource={RelativeSource AncestorType=arc:CardStack}}" />
    </arc:CardStack.EmptyContent>
</arc:CardStack>
```

## API 參考

<!-- api: CardStack -->

## 無障礙

- 舞台可取得焦點，並以「卡片堆疊」播報。每張卡片都有名稱，例如「里斯本，第 1 項，共 3 項」。
- 舞台取得焦點時，左右方向鍵決定頂部卡片。Backspace、Ctrl + Z 與 Cmd + Z 可復原。
- 按鈕與每個手勢一一對應，因此划動不是決定卡片的唯一方式。停用的復原按鈕會保留焦點。
- 決定、復原與重置都會以禮貌等級播報。
- 焦點框繪製在頂部卡片上，或空狀態上。

## 動態效果

- 頂部卡片跟隨拖曳，朝移動方向傾斜並顯示標記。快速輕掃或長距離拖曳會讓卡片帶著放開時的速度划出堆疊。
- 頂部卡片離開後，後面的卡片上升到位。
- 復原會讓最後一張卡片從離開的位置飛回。重置時，卡片從最底層開始依序飛回。
- 短距離拖曳會彈回堆疊。
- 減少動態效果時，卡片在原位置淡出再淡入，不產生位移。關閉動態效果時立即到達最終狀態。

## 相關元件

- `Carousel` 用於瀏覽一排項目，而不對每一項做決定。
- `SwipeActions` 用於清單列上的快速操作。
- `SortableDataTable` 用於並排審閱大量記錄。
