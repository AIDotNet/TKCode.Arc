---
title: 時間軸
summary: 依日期分組、最新在前，追蹤事件的發生經過。
source-hash: e6dcd3991034
---

<Demo name="LiveStream" />

## 何時使用

- 專案歷程、稽核紀錄或部署串流，其中事件的順序與新舊十分重要。
- 由人或系統產生的事件，每則包含簡短標題與選用的脈絡說明。
- 在畫面上時會不斷接收新事件的動態串。

## 何時不用

- 讀者需要排序或比較的紀錄：用 `SortableDataTable`。
- 有目前位置的固定步驟：用 `Stepper`。
- 針對單一物件的串狀討論：用 `CommentThread`，即時對話用 `ChatThread`。

## 用法

將 `Now` 設為參考時間。相對時間（「9m」「2h」）與日期分組（「Today」「Yesterday」）都由它計算得出。事件可以任意順序提供，最新的事件會顯示在最前面。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Timeline Label="發佈歷程"
                  Now="2026-10-20T12:00:00+00:00">
        <arc:TimelineEvent Id="build-418"
                           At="2026-10-20T11:48:00+00:00"
                           Actor="Ana Ruiz"
                           Title="將 web 部署到正式環境"
                           Meta="Build #418"
                           Detail="全部 312 項檢查在 4m 12s 內通過。" />
        <arc:TimelineEvent Id="pr-212"
                           At="2026-10-20T11:02:00+00:00"
                           Actor="Theo Walsh"
                           Title="將 Checkout 重新設計合併到 main"
                           Meta="PR #212" />
    </arc:Timeline>
</UserControl>
```

### 即時動態

將 `ItemsSource` 繫結到一個 `ObservableCollection<TimelineEvent>`。加入其中的事件會從頂端滑入。用計時器推進 `Now`，讓相對時間保持最新。Arc 建議約每分鐘推進一次。

```csharp
var events = new ObservableCollection<TimelineEvent>();
var timeline = new Timeline { Label = "部署紀錄", Now = DateTimeOffset.UtcNow, ItemsSource = events };

events.Add(new TimelineEvent("post-1", DateTimeOffset.UtcNow, "將 Checkout 重新設計合併到 main", "Theo Walsh")
{
    Meta = "PR #212"
});

var clock = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
clock.Tick += (_, _) => timeline.Now = DateTimeOffset.UtcNow;
timeline.AttachedToVisualTree += (_, _) => clock.Start();
timeline.DetachedFromVisualTree += (_, _) => clock.Stop();
```

### 可展開的列與色調

帶有 `Detail` 的列在被啟用時會展開。沒有詳細內容的列只是一般文字。`Tone` 會為系統事件著色，因此其標題必須同時說明結果。

<Demo name="ExpandableDetails" />

<Demo name="TonesAndPortraits" />

```xml
<arc:Timeline Label="流程管線" Now="2026-10-20T12:00:00+00:00">
    <arc:TimelineEvent Id="t1"
                       At="2026-10-20T11:55:00+00:00"
                       Title="部署成功"
                       Meta="web · production"
                       Tone="Success" />
    <arc:TimelineEvent Id="t2"
                       At="2026-10-20T11:42:00+00:00"
                       Title="部署失敗：健康檢查逾時"
                       Meta="api · staging"
                       Detail="已重試兩次。失敗的檢查記錄在 INC-204 中。"
                       Tone="Danger" />
</arc:Timeline>
```

```csharp
timeline.ExpandedChanged += (_, e) => Status.Text = $"{e.Item.Title}，展開狀態：{e.IsExpanded}";
timeline.Expand("t2");
```

人員的事件會顯示 `Avatar`。系統事件會顯示 `Icon`，例如 `ArcIcons.Check`，並由 `Tone` 著色。

### 長歷程

設定 `MaxHeight` 後，動態串會在內部捲動。目前日期的標籤會在其列捲動時保持固定，而且清單是虛擬化的，因此長歷程依然順暢。

<Demo name="ScrollingFeed" />

<Demo name="ThousandUpdates" />

```xml
<arc:Timeline Label="稽核紀錄"
              Now="2026-10-20T12:00:00+00:00"
              ItemsSource="{Binding AuditEvents}"
              MaxHeight="380" />
```

## API 參考

<!-- api: Timeline -->

<!-- api: TimelineEvent -->

## 無障礙

- 動態串是一個有名稱的區域。`Label` 是它的無障礙名稱。
- 每個日期標籤是標題，層級為 `HeadingLevel`（預設為 3），每個時間都有隱藏的完整日期文字。
- 可展開的列是按鈕，會回報其展開狀態。沒有詳細內容的列不在 Tab 順序中。
- 新事件會在狀態區域中朗讀，且不會移動焦點。
- 上下方向鍵在可展開的列之間移動，Home 與 End 分別跳到第一列與最後一列，Enter 或空白鍵展開或收合目前的列。

## 動態效果

- 列進入可視範圍時會繪製其連接線，標記點隨之彈出。
- 新事件從頂端滑入，下方的列隨之下移。展開的詳細內容以彈簧方式開啟。
- 減少動態效果時，移動改為短暫的淡入，高度立即變化。關閉動態效果時直接到最終狀態。

## 相關元件

- `CommentThread`：針對單一物件的回覆；`ChatThread`：即時的雙向對話。
- `SortableDataTable`：讀者需要排序或比較紀錄時使用。
- `Stepper`：有目前位置的固定步驟。
