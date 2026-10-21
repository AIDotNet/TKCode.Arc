---
title: 通知中心
summary: 集中查看通知，支援已讀狀態、分組資訊，以及帶動態效果的展開與收合。
source-hash: 25d3d3b869e5
---

<Demo name="Preview" />

## 何時使用

- 通知需要一個地方暫存，等使用者有空時查看：提及、已完成的工作、核准與帳號警告。
- 使用者需要將通知標為已讀或未讀、開啟其中一則的詳細資訊，或清除舊通知。
- 應用程式頁首有空間放一個圖示按鈕，並能顯示未讀通知的數量。

## 何時不用

- 不需後續動作的簡短確認，例如“已儲存”：使用 `Toast`。
- 目前頁面上必須立即讓使用者看到的問題：使用 `Alert`。
- 含搜尋與篩選的完整歷史紀錄：請以 `Timeline` 或表格建立頁面。

## 用法

新增 `NotificationItem` 子元素，或設定 `ItemsSource`。每個項目都有 `Title`、可選的 `Description`、`Time` 時間文字與 `IsRead` 狀態。來自某位使用者的通知會設定 `ActorName` 與 `ActorSource`，並顯示其大頭貼。系統通知會顯示其 `Tone` 對應的圖示：Info、Success 或 Warning。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:NotificationCenter>
        <arc:NotificationItem Id="deploy" Title="部署已完成" Description="版本 2.4.1 已上線。" Time="18 分鐘前" Tone="Success" />
        <arc:NotificationItem Id="mention" Title="Mia Chen 提到了你" Time="2 分鐘前" ActorName="Mia Chen" />
        <arc:NotificationItem Id="digest" Title="你的每週摘要已就緒" Time="昨天" IsRead="True" />
    </arc:NotificationCenter>
</UserControl>
```

鈴鐺會顯示未讀數量，最多顯示到“9+”。點選鈴鐺即可開啟面板。點選條目可顯示其詳細資訊，並提供“標為已讀”和“關閉”按鈕。同一時間只會展開一個條目。

### 已讀狀態與你的資料

控制項會變更項目上的 `IsRead`，並引發 `ReadChanged`。“全部標為已讀”會為它標記的每個項目各引發一次。“關閉”（移除通知）會將項目從 `Items` 移除；若 `ItemsSource` 是你可以修改的清單，則從 `ItemsSource` 移除，並引發 `Dismissed`。“清除已讀”會為每個已讀項目各引發一次。請在這些處理常式中將變更儲存到你的資料存放區。資料存放區變更時，鈴鐺與面板會跟著項目與集合更新。

<Demo name="ReadState" />

```csharp
center.ItemsSource = store;   // 一個 ObservableCollection<NotificationItem>
center.ReadChanged += (_, e) => api.SetRead(e.Item.Id, e.IsRead);
center.Dismissed += (_, e) => api.Dismiss(e.Item.Id);
```

唯讀的資料來源會保留其項目，控制項則會隱藏已關閉的項目。

### 未讀檢視與空狀態

面板會列出所有通知，或只列出未讀通知。`View` 是雙向的。清單為空時，面板會顯示“暫無通知”或“沒有未讀通知”。存在已讀通知時，會出現一個連結，可返回“全部”檢視。

<Demo name="UnreadView" />

### 在應用程式頁首中

面板會在鈴鐺下方 12 像素處開啟，並與視窗邊緣保持 12 像素的距離，因此鈴鐺可以位於頁首的邊緣。未讀徽章會超出鈴鐺右上角 5 像素，因此在會裁切內容的容器中，請在鈴鐺周圍預留同樣的 5 像素空間。視窗寬度為 380 像素或更窄時，面板會使用較緊湊的間距，“全部標為已讀”只顯示圖示。

<Demo name="AppHeader" />

### 文字

`Label` 為面板與鈴鐺命名。`Labels` 保存其餘所有文字，並帶有英文預設值。指派一個新的 `NotificationCenterLabels` 即可變更這些文字。在 `ReadCount` 中，`{0}` 是會滾動變化的數字。帶有數量的文字（例如標題下方的摘要）是可在程式碼中設定的函式。

<Demo name="CustomWording" />

```csharp
center.Labels = new NotificationCenterLabels
{
    Summary = unread => unread > 0 ? $"{unread} 則新通知" : "暫無新通知",
    Trigger = (label, unread) => $"{label}，{unread} 則新通知"
};
```

### 從程式碼開啟

`IsOpen` 是雙向的，`Open()` 與 `Close()` 的作用與之相同。`MarkAllRead()` 與 `ClearRead()` 會執行批次動作，並引發與按鈕相同的事件。

```csharp
center.Open();
center.MarkAllRead();
```

## API 參考

<!-- api: NotificationCenter -->

<!-- api: NotificationItem -->

## 無障礙

- 鈴鐺是一個按鈕，其無障礙名稱由 `Label` 與未讀數量組成，例如“通知，3 則未讀”。它會回報面板是否已展開。
- 開啟面板時，焦點會移到其關閉按鈕。Tab 依序經過關閉按鈕、“全部”與“未讀”、“全部標為已讀”、各條目、已展開條目的按鈕，以及“清除已讀”，並在面板內循環。
- Escape 與關閉按鈕會關閉面板，並將焦點返回鈴鐺。
- “全部”與“未讀”是切換按鈕。每個條目是一個按鈕，其無障礙名稱包含標題與狀態，例如“部署已完成，未讀。顯示詳細資訊”，並會回報它是否已展開。
- 在“未讀”檢視中將條目標為已讀，或關閉某個條目後，焦點會依序移到下一個條目、上一個條目，然後是“未讀”按鈕。執行“全部標為已讀”後，焦點會返回目前檢視的按鈕。
- 標題下方的摘要是禮貌式即時區域，因此新的數量會被朗讀出來。

## 動態效果

- 面板從鈴鐺處展開：開始時向鈴鐺方向偏移 6 像素，縮放為 0.97，並在 240 毫秒內淡入。關閉時用 120 毫秒離開，移動距離減半，縮放為 0.98。
- 面板開啟時鈴鐺會微微傾斜。徽章以彈跳方式出現與消失，其數字會向上或向下滾動。
- 面板開啟或檢視新增條目時，條目會從下方 4 像素處滑入就位，每個相隔 35 毫秒。離開的條目會淡出，並以平滑的彈簧動畫收合高度。執行批次動作後，條目會依序離開，與第一個條目的間隔最多為 200 毫秒。
- 數量會滾動，摘要會向上滑入新文字，“全部”與“未讀”的醒目提示會在兩個按鈕之間滑動。詳細資訊、頁尾與空狀態會以相同的彈簧展開或收合高度。
- 動態效果等級為 Reduced 時，只保留 120 毫秒的短淡入淡出，沒有位移、縮放或模糊。面板會立即顯示或隱藏。等級為 Off 時，所有變化會立即套用。

## 相關元件

- `Toast`：用於會自行消失的簡短訊息。
- `Popover`：用於錨定在按鈕上的其他內容。
- `Badge`：用於在其他控制項上顯示數量或狀態。
