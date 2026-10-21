---
title: 時間軸
summary: 依日期分組、最新在前，追蹤事件的發生經過。
source-hash: b232d9751747
---

<Demo name="LiveStream" />

## 何時使用

`Timeline` 是應用程式中的活動動態，其中事件的順序與新近程度比彼此比較更重要。它依日期分組、最新的在前，並且在螢幕上時會接收更新。每一列是一則更新：人員的操作或系統事件，帶有簡短標題、選用的脈絡，以及在讀者需要時原地展開的詳細資料。

- **發布頁面上的專案歷程**：Theo Walsh 將 Checkout 重新設計合併到 main，並以拉取要求作為脈絡。讀者透過日期分組即可看出一次發布是如何完成的。
- **DevOps 主控台中的部署串流**：建置、推出與回復，系統事件依成功或危險色調顯示。標題直接說明結果，因此顏色從來不是唯一的訊號。
- **管理後台中的稽核紀錄**：誰在何時修改了帳單方案或使用者角色。每一列都可展開，顯示修改前後的值，因此清單保持簡短。
- **CRM 中的帳號動態**：某一位客戶的通話、電子郵件與合約變更，每則都附有執行者的大頭照。
- **狀態頁面上的事件進度**：即時動態，回應人員發布更新時，每則都從頂部滑入。

## 何時不用

- **讀者需要排序或比較的紀錄**，例如依金額排序的發票或依最後登入時間排序的使用者：用 `SortableDataTable`。
- **有目前位置的固定步驟**，例如結帳或入職流程：用 `Stepper`。
- **針對單一物件的串狀討論**：用 `CommentThread`；即時的雙向對話用 `ChatThread`。
- **某個項目的一個目前狀態**，例如發票上的「付款失敗」：在該項目旁邊用 `Alert`。只有一個事件的時間軸只是一則訊息，而不是動態串流。

## 用法

`Timeline` 衍生自 Avalonia 的 `ItemsControl`。它的項目是 `TimelineEvent` 物件，可以透過 `ItemsSource` 繫結，也可以直接內嵌撰寫。事件可以依任意順序提供，最新的會顯示在最前面。將 `Now` 設為參考時間：相對時間（如 “9m”、“2h”）以及日期分組 “Today” 與 “Yesterday” 都由它計算得出。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Timeline Label="發布歷程"
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

`Timeline` 衍生自 `ItemsControl`，並自行排列各列，因此應提供 `TimelineEvent` 項目，而不是項目範本。除非設定了 `MaxHeight`，動態串流會隨頁面一起增長，因此應將它放在可捲動的頁面中，或為它設定高度，讓它在內部捲動。

### 結構

1. **日期標籤**（`HeadingLevel`，預設為 3）：「Today」、「Yesterday」，或較早日期的星期與日期，並附上當天的更新數。它是一行 19.6 px 的文字，上下各留 8 px。
2. **標記**：直徑 28 px 的圓形，中間是 14 px 的圖示，位於列頂部下方 8 px。人員的事件顯示其 `Avatar`，系統事件顯示 `Icon`；沒有大頭照的人員顯示空白標記。
3. **連接線**：從標記底部延伸到下一列的線條。列進入視埠時它會自行繪製。
4. **觸發器**：標題位於 20 px 的列中，`Meta` 位於 16.8 px 的列中，`Actor` 以前景色排在最前。觸發器有 12 px 的內距，距左邊緣 36 px。
5. **時間**：位於標題之後，寬度至少 44 px。今天的更新顯示相對時間（「9m」「2h」）；較早的日期顯示時鐘時間（「1:00 PM」）。
6. **箭頭**：位於帶有 `Detail` 的列的時間之後，為 16 px 的箭頭。沒有詳細資料的列不顯示箭頭，也不會展開。
7. **詳細資料**：在觸發器下方展開的 `Detail`，兩側內距 12 px，底部 16 px。
8. **吸頂日期標籤**：在可捲動的動態串流（設定了 `MaxHeight`）中，目前的日期標籤固定在頂部，其下的列捲動經過。
9. **羽化**：可捲動的動態串流下方還有更多列時，其底邊會在 36 px 的範圍內漸隱。

時間軸的日期標籤預設使用 Arc 表面色，它會遮住捲動經過其下方的列。請將 `HeaderBackground` 設為動態串流所在的表面，否則標籤顏色會與頁面不一致。

寬度小於 420 px 時，觸發器的內距降為 8 px，時間與箭頭會更靠近標題。

### 繫結到檢視模型

檢視模型擁有事件、參考時間和展開的列。時間軸負責顯示它們，檢視模型在事件到達時新增。本例使用 CommunityToolkit.Mvvm。

```csharp
public partial class DeploymentFeedViewModel : ObservableObject
{
    public ObservableCollection<TimelineEvent> Events { get; } = [];

    [ObservableProperty]
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private IReadOnlyList<string> _expandedIds = [];

    public void Add(TimelineEvent item) => Events.Add(item);

    public void Tick() => Now = DateTimeOffset.UtcNow;
}
```

```xml
<arc:Timeline Label="部署紀錄"
              ItemsSource="{Binding Events}"
              Now="{Binding Now}"
              ExpandedIds="{Binding ExpandedIds}"
              MaxHeight="420" />
```

- `ItemsSource` 接收事件。向集合新增事件時，新列會從頂部滑入，檢視模型無需重建清單。
- `Now` 為單向繫結。如下一節所示，由計時器設定它；它變動時只有時間標籤會更新。
- `ExpandedIds` 為雙向繫結。它保存已展開列的識別碼，因此頁面重新載入時檢視模型可以還原它們。

### 即時動態

約每分鐘用計時器推進一次 `Now`，讓相對時間保持最新。當動態串流離開螢幕時停止計時器，使未使用的頁面不做任何工作。`IsScrollToNewEnabled`（預設為 true）在讀者向下捲動時有新的最新更新到達，會將動態串流捲回頂部。

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

帶有 `Detail` 的列在被啟用時會展開。`Detail` 可以是文字，也可以是任意控制項，並在原位展開。`Detail` 為空字串時，該列不會變為可展開。`Tone` 為系統事件著色：

| Tone | 用於 | 顯示方式 |
|---|---|---|
| `Neutral`（預設） | 一般更新 | 安靜的標記 |
| `Success` | 已完成的步驟：部署、遷移、付款 | 標記著色，圖示使用成功色 |
| `Danger` | 失敗或回復 | 標記著色，圖示使用危險色 |

標題與色調都要說明結果，使含義不只依賴顏色。

<Demo name="ExpandableDetails" />

<Demo name="TonesAndPortraits" />

```xml
<arc:Timeline Label="流水線" Now="2026-10-20T12:00:00+00:00">
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

`ExpandedChanged` 會報告每一列的展開或收合，無論是使用者操作，還是程式碼呼叫了 `Expand`、`Collapse` 或 `Toggle`。人員的事件顯示 `Avatar`；系統事件在沒有大頭照時顯示 `Icon`，例如 `ArcIcons.Check`。

### 長歷程

設定 `MaxHeight` 後，動態串流會在內部捲動。目前的日期標籤在其下的列捲動時保持固定。清單是虛擬化的，只有可見的列和少量緩衝列存在，因此長歷程依然保持流暢。

<Demo name="ScrollingFeed" />

<Demo name="ThousandUpdates" />

```xml
<arc:Timeline Label="稽核紀錄"
              Now="2026-10-20T12:00:00+00:00"
              ItemsSource="{Binding AuditEvents}"
              MaxHeight="380" />
```

使用 `ScrollIntoView(id)` 將某個事件捲動到可見範圍，例如從通知中的連結跳轉過去。`ScrollToTop(smooth)` 會把動態串流捲回最新的更新，在完整動態效果下平滑捲動。

### 範例：事件進度動態串流

狀態主控台中 INC-204 的事件頁面。動態串流依序展示回應人員的操作與系統事件。失敗的部署是危險事件，標題已說明結果，詳細資料解釋了重試；回復是一位人員的操作，帶有回應人員的姓名；健康檢查則是成功事件。「Yesterday」分組表明頁面會保留較早的歷程。每一列的標題都是完整的句子，因此動態串流讀起來像一份記錄檔。

![帶有危險事件、回復、成功事件與日期分組的事件進度動態串流](images/IncidentFeed.png)

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"
             Width="600">
    <Border Padding="16,12,16,4"
            CornerRadius="26"
            BorderThickness="1"
            Background="{arc:ArcDesignTokenResource Surface}"
            BorderBrush="{arc:ArcDesignTokenResource Border}">
        <arc:Timeline Label="Incident INC-204"
                      Now="2026-10-20T12:00:00+00:00">
            <arc:TimelineEvent Id="rollback"
                               At="2026-10-20T11:58:00+00:00"
                               Actor="Theo Walsh"
                               Title="rolled back api to 1.8.3"
                               Meta="Rollback #77"
                               Detail="Error rate returned to 0.2% within six minutes of the rollback." />
            <arc:TimelineEvent Id="healthy"
                               At="2026-10-20T11:55:00+00:00"
                               Title="Health checks pass in all three regions"
                               Meta="api · production"
                               Icon="{x:Static arc:ArcIcons.CircleCheck}"
                               Tone="Success" />
            <arc:TimelineEvent Id="failed"
                               At="2026-10-20T11:42:00+00:00"
                               Title="Deploy failed: health check timed out"
                               Meta="api · staging"
                               Detail="Retried twice. The failing check is tracked in INC-204."
                               Icon="{x:Static arc:ArcIcons.CircleX}"
                               Tone="Danger" />
            <arc:TimelineEvent Id="opened"
                               At="2026-10-20T11:44:00+00:00"
                               Actor="Ana Ruiz"
                               Title="opened INC-204 and paged on-call"
                               Meta="Severity 2" />
            <arc:TimelineEvent Id="closed"
                               At="2026-10-19T16:20:00+00:00"
                               Actor="Lee Chen"
                               Title="closed INC-198 after the cache fix"
                               Meta="Severity 3" />
        </arc:Timeline>
    </Border>
</UserControl>
```

場景中的事件保持內嵌寫法，因為靜態圖片必須如此。在應用程式中它們來自 `ItemsSource`，時間標籤隨 `Now` 變化。

### 文案規範

- **標題**：用過去式的完整句子，動態串流中執行者排在最前：「將 Checkout 重新設計合併到 main」。當事件由人引起時，執行者必須是人名，而不能是 “System”。
- **結果**：每個成功或危險事件的標題都要寫明結果，使看不到顏色的讀者仍然知道發生了什麼。
- **Meta**：簡短的參照，例如拉取要求、建置或嚴重等級：「PR #212」「Build #418」。
- **Detail**：一兩句話，列出讀者展開該列時需要的事實。當數字較長時，把它放在詳細資料中，而不是標題中。
- **其他語言**：設定 `Culture` 以決定日期標題與時鐘時間的格式。預設為 en-US。

| 不要寫 | 改為 |
|---|---|
| 更新 | Ana Ruiz 將 web 部署到正式環境 |
| 錯誤！ | 部署失敗：健康檢查逾時 |
| 系統：完成 | 資料庫遷移已完成 |
| 點選查看更多 | 已重試兩次。失敗的檢查記錄在 INC-204 中。 |

### 最佳做法

**建議**

- 用計時器設定 `Now`，並在動態串流離開螢幕時停止計時器。相對時間只與 `Now` 一樣新。
- 當受眾共用同一時區（例如某個地區的維運團隊）時設定 `TimeZone`。預設為 UTC，因此每位檢視者看到的時鐘都相同。
- 為每個事件設定穩定的 `Id`。展開狀態、新鮮度提示以及新更新的播報都依賴它。
- 對較長的動態串流使用 `MaxHeight`，使日期標籤保持固定，讀者不會失去位置。
- 在每個成功或危險事件的標題中寫明結果。

**避免**

- 為新增一個事件而取代整個集合。應新增到集合中，使新列滑入。
- 在一般的人員事件上設定 `Tone`。色調用於標記系統結果，而人員的列已經有大頭照。
- 把 `Detail` 用於事件的主要內容。列首先顯示標題，而包含完整故事的詳細資料要等讀者展開後才可見。
- 在放於彩色面板上的動態串流中保留 `HeaderBackground` 的預設值。這樣日期標籤會以錯誤的顏色遮住各列。
- 用時間軸展示讀者需要比較的紀錄。排序與篩選應使用 `SortableDataTable`。

### 常見問題

**為什麼相對時間不會變動？** 只有 `Now` 變動時時間標籤才會更新。請約每分鐘推進一次 `Now`，計時器需在動態串流顯示期間執行。

**為什麼我的列不能展開？** 只有 `Detail` 有內容時，列才會展開。空字串不算內容，因此該列只是一般文字，也不在 Tab 順序中。

**能否讓最舊的事件排在最前？** 不能。動態串流總是把最新的更新排在最前，並依日期分組。如果需要另一種順序，請在另一個檢視中自行排序事件。

**如何透過程式碼開啟某一列？** 呼叫 `Expand(id)`、`Collapse(id)` 或 `Toggle(id)`。每次呼叫都會引發 `ExpandedChanged`；`IsExpanded(id)` 可告訴你該列是否已展開。

## API 參考

<!-- api: Timeline -->

<!-- api: TimelineEvent -->

## 無障礙

- 動態串流是一個有名稱的區域。`Label` 是它的無障礙名稱，例如 “Incident INC-204”。
- 每個日期標籤都是標題，層級為 `HeadingLevel`（預設為 3，範圍 2 到 6）。其無障礙名稱包含更新數，例如 “Friday, October 16, 1 update”。每個時間都有隱藏的完整日期文字。
- 可展開的列會公布其展開狀態。沒有詳細資料的列是一般文字，不在 Tab 順序中。
- 新事件會以 “New update: ...” 在即時區域中播報，且不會移動焦點。
- 色調即顏色。標題必須說明結果，這是文案規範的要求。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Down` / `Up` | 移到下一個或上一個帶詳細資料的列 |
| `Home` / `End` | 移到第一個或最後一個帶詳細資料的列 |
| `Enter` 或 `Space` | 展開或收合目前的列 |

帶詳細資料的列會以 Tab 順序取得焦點；沒有詳細資料的列則不會。方向鍵會跳過無法展開的列。

## 動態效果

- 列進入視埠時繪製連接線，標記隨之彈出。
- 新事件從頂部滑入，下方的列隨之下移。展開的詳細資料以彈簧方式開啟。
- 減少動態效果時，移動改為短暫淡入，高度立即變化。關閉動態效果時直接到最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 列進入視埠 | 連接線繪製，標記彈出 | 短暫淡入 | 立即完成 |
| 新事件到達 | 從頂部滑入，下方的列下移 | 短暫淡入 | 立即完成 |
| 詳細資料展開或收合 | 詳細資料以彈簧方式展開 | 高度立即變化 | 立即完成 |
| 捲動時的日期標籤 | 固定在動態串流頂部 | 固定在頂部 | 固定在頂部 |

## 相關元件

- `CommentThread` 用於針對單一物件的回覆，`ChatThread` 用於即時的雙向對話。
- `SortableDataTable` 用於讀者需要排序或比較紀錄的情境。
- `Stepper` 用於有目前位置的固定步驟。

| 元件 | 適用情境 |
|---|---|
| `CommentThread` | 回覆掛在某個物件上，並且可以標記為已解決 |
| `ChatThread` | 人們輪流對話，並有輸入中提示與已讀回條 |
| `SortableDataTable` | 讀者需要排序或比較紀錄 |
| `Stepper` | 步驟固定，且其中一項是目前步驟 |
| `Alert` | 單一目前狀態應放在某一項目旁邊 |
