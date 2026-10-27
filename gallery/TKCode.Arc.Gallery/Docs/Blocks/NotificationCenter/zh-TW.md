---
title: 通知中心
summary: 集中檢視通知，支援已讀狀態、分組資訊，以及帶動態效果的展開與收合。
source-hash: bf8420de3c62
---

<Demo name="Preview" />

## 何時使用

通知中心是更新等待使用者處理的地方。它是應用程式頁首中帶未讀數量的鈴鐺，以及一個列出更新的面板，面板中有每則更新的已讀狀態、詳細資訊與批次操作。它位於外殼頁首的使用者選單與搜尋旁邊，並且會在時機過去之後保留更新：輕提示會自行消失，而通知會一直留著，直到使用者閱讀或關閉它。

- **協作工具中的提及與留言**：同事在「Checkout redesign」討論串中提到你，而你正在另一個專案裡。更新會留在鈴鐺中，角標告訴你它仍未處理。
- **分析產品中已完成的背景工作**：報表匯出或資料重新整理在使用者瀏覽儀表板時完成。「Your export is ready to download」會連同時間留在面板中，方便之後再找到。
- **營運主控台中的審核與指派**：一張採購單或一次部署正在等待決定。使用者看過後可以標記為已讀，決定做出後再關閉。
- **SaaS 管理後台中的帳號與帳單警告**：方案儲存空間已用 85%，或一張卡下個月到期。`Warning` 語氣會以警告色繪製圖示，使其與例行更新區分開來。
- **值班人員的故障與狀態變化**：「Incident resolved」及其時間，以 `Success` 語氣顯示，供故障發生時不在狀態頁前的工程師查看。

## 何時不用

- **無需後續處理的確認**，例如「Invoice saved」：用 `Toast`，它會自行消失。
- **目前頁面上使用者必須立即修正的問題**，例如結帳表單中的付款失敗：在問題所在處用 `Alert`，使它在頁面被修正前一直可見。
- **可搜尋、可篩選的事件歷史**，例如稽核紀錄：在頁面上用 `Table`。單筆紀錄的有序事件用 `Timeline`。
- **帶回覆的對話**：用 `ChatThread`，它有輸入區與回覆流程。面板對一則更新只提供「Mark read」、「Mark unread」和「Dismiss」。
- **待辦工作的計數**，例如導覽項目上的 14 個未完成工作：用 `Badge`。面板列出的是更新，而不是計數器。

## 用法

`NotificationCenter` 衍生自 Avalonia 的 `TemplatedControl`。它的範本包含鈴鐺按鈕、未讀角標與面板，面板會在彈出層中開啟。`NotificationItem` 物件是資料：每個都是帶有面板所讀屬性的 `AvaloniaObject`。可以把它們作為子元素加入，也可以把 `ItemsSource` 繫結到你自己的集合。每一項都有 `Title`、選用的 `Description`、`Time` 標籤與 `IsRead` 狀態。人發出的更新會設定 `ActorName` 和 `ActorSource`，並顯示其頭像。系統更新會顯示其 `Tone` 對應的圖示：`Info`、`Success` 或 `Warning`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:NotificationCenter>
        <arc:NotificationItem Id="deploy" Title="部署已完成" Description="2.4.1 版本已上線。" Time="18m" Tone="Success" />
        <arc:NotificationItem Id="mention" Title="Mia Chen 提到了你" Time="2m" ActorName="Mia Chen" />
        <arc:NotificationItem Id="digest" Title="你的每週摘要已就緒" Time="昨天" IsRead="True" />
    </arc:NotificationCenter>
</UserControl>
```

鈴鐺會顯示未讀數量，最多顯示到「9+」，沒有未讀項時隱藏。點擊它即可開啟面板。點擊某一列可展開其詳細資訊，並使用「Mark read」（或「Mark unread」）和「Dismiss」。同一時間只展開一列。

### 結構

1. **鈴鐺**：42 px 的方形按鈕，圓角 12 px，表面色上有 1 px 邊框。鈴鐺圖形為 19 px。懸停、按下與開啟時使用較強的邊框與較淡的表面色；任何狀態都不縮放。
2. **未讀角標**：20 px 高的膠囊，左右內距 5 px，表面色的 2 px 環。它位於鈴鐺右上角外側 5 px 處，顯示 `UnreadCount`，從 10 起顯示為「9+」。當所有項目都已讀時不顯示。
3. **面板**：寬 424 px，或視窗寬度減 24 px，高度最多 590 px。它使用抬起的表面色、1 px 邊框、面板圓角與浮動陰影。它錨定在鈴鐺下方，間距 12 px。
4. **標題**（`Label`）：特大字級、中等字重，旁邊是一個 21 px 的計數標籤，顯示未讀數量。
5. **摘要**：標題下方一行，內容來自 `Labels.Summary`，使用次要文字顏色。
6. **關閉按鈕**：30 px 的方形按鈕，圓角 8 px，名稱由 `Labels.Close` 提供。
7. **工具列**：All 與 Unread 兩個檢視按鈕，高 31 px，寬至少 61 px，彼此相距 3 px，高亮區塊在兩者之間滑動。「Mark all read」位於末尾，僅在有未讀項目時出現。
8. **列**：清單的內距為 9 px、11 px 和 10 px。每列顯示發出者的頭像或 36 px 的語氣圖示、中等字重的標題（已讀時為次要顏色）、6 px 的未讀圓點、一行預覽文字，以及弱化文字顏色的時間標籤。
9. **詳細資訊**：展開的列會顯示完整描述，以及「Mark read」或「Mark unread」和「Dismiss」操作。操作按鈕高 30 px，圓角 10 px，縮排到文字欄。
10. **空狀態**：高度至少 190 px，帶有一個勾選圖示，標題與描述來自標籤。
11. **頁尾**：至少有一項已讀時出現。顯示已讀數量（「3 read」）和「Clear read」。

### 已讀狀態與你的資料

控制項會修改項目上的 `IsRead` 並觸發 `ReadChanged`，其引數包含 `Item` 和新的 `IsRead`。「Mark all read」會為它標記的每個項目各觸發一次。「Dismiss」會從 `Items` 中移除該項目；如果 `ItemsSource` 是可修改的清單，則從 `ItemsSource` 中移除，並觸發 `Dismissed`，引數為 `Item`。「Clear read」會為每個已讀項目各觸發一次。事件執行時該項目已經改變，因此請在處理常式中把修改儲存到你的儲存空間。當你的儲存空間變動時，鈴鐺與面板會跟隨項目和集合更新。

<Demo name="ReadState" />

```csharp
center.ItemsSource = store;   // 一個 ObservableCollection<NotificationItem>
center.ReadChanged += (_, e) => _vm.SaveReadStateAsync(e.Item);
center.Dismissed += (_, e) => _vm.DismissAsync(e.Item);
```

唯讀的資料來源會保留其項目，控制項則隱藏已關閉的項目，直到再次設定 `ItemsSource`。

### 繫結到檢視模型

檢視模型擁有更新及其持久化。控制項只負責顯示它們，並回報使用者的變更。檢視模型持有一個 `ObservableCollection<NotificationItem>` 以及面板的開啟狀態。

```csharp
public abstract class ActivityStore
{
    public abstract IEnumerable<NotificationItem> LoadRecent();
    public abstract Task SetReadAsync(string id, bool isRead);
    public abstract Task DismissAsync(string id);
}

public partial class ActivityViewModel : ObservableObject
{
    private readonly ActivityStore _store;

    public ActivityViewModel(ActivityStore store)
    {
        _store = store;
        foreach (var update in store.LoadRecent())
        {
            Updates.Add(update);
        }
    }

    // Dismiss 會從此集合中移除一項；對於可寫入的資料來源，由控制項執行這一步。
    public ObservableCollection<NotificationItem> Updates { get; } = new();

    [ObservableProperty]
    private bool _isActivityOpen;

    public Task SaveReadStateAsync(NotificationItem item) => _store.SetReadAsync(item.Id, item.IsRead);

    public Task DismissAsync(NotificationItem item) => _store.DismissAsync(item.Id);
}
```

```xml
<arc:NotificationCenter x:Name="Activity"
                        Label="活動"
                        ItemsSource="{Binding Updates}"
                        IsOpen="{Binding IsActivityOpen, Mode=TwoWay}" />
```

```csharp
// 在程式碼後置中，控制項名為 Activity：控制項沒有命令，因此使用它的事件。
Activity.ReadChanged += (_, e) => _ = _vm.SaveReadStateAsync(e.Item);
Activity.Dismissed += (_, e) => _ = _vm.DismissAsync(e.Item);
```

- `IsOpen` 與 `View` 是雙向的。當其他介面（例如頁首的快速入口）要開啟面板時，繫結 `IsOpen`。
- `NotificationItem.IsRead` 也是雙向的。檢視模型更新它時，鈴鐺隨之變化；使用者標記某個項目時，控制項會設定它。
- 控制項沒有命令。如上所示，在 `ReadChanged` 和 `Dismissed` 中儲存變更，並讓處理常式保持簡短。

### 資料結構與更新

- **穩定的 `Id`**：列透過 `Id` 比對。用帶有相同 id 的新 `NotificationItem` 物件重新整理清單時，列會保持原位，展開的列也保持展開。沒有 id 的項目透過物件參考比對。
- **用 `Tag` 保存你的紀錄**：`Tag` 可以保存任何值，例如你的儲存空間中的紀錄鍵。事件會提供 `Item`，因此讀取 `e.Item.Tag`，而不是按標題搜尋。
- **`Time` 是顯示文字**：控制項會按原樣顯示字串，例如「2m」。它不會隨時間更新該值，因此請在檢視模型中格式化它，並在重新載入時重新整理。
- **即時更新**：控制項會跟隨集合，以及每個項目的 `Title`、`Description`、`IsRead` 和其他屬性（`Tag` 除外）。新增更新或已讀狀態變化會立即重新整理鈴鐺與面板。
- **批次操作作為一個批次執行**：「Mark all read」和「Clear read」只會重新整理一次計數與清單，然後為每個項目各觸發一次事件。
- **保持清單簡短**：面板最高 590 px，而 Arc 設計沒有清單捲軸。資料儲存只保留最近的更新，其餘內容引導使用者前往歷史頁面。
- **數字格式**：角標使用目前的文化特性。請以應用程式的文化（例如 `CultureInfo.CurrentCulture`）格式化項目文字中的數量與日期。

### 未讀檢視與空狀態

面板會列出所有更新，或只列出未讀的更新。`View` 是雙向的。清單為空時，面板會說明這一點：All 檢視中為「All clear」，Unread 檢視中為「Nothing unread」。存在已讀更新時，會有一個連結返回 All 檢視。

<Demo name="UnreadView" />

### 在應用程式頁首中

面板會在鈴鐺下方 12 像素處開啟，並與視窗邊緣保持 12 像素的距離，因此鈴鐺可以位於頁首的邊緣。未讀角標會超出鈴鐺右上角 5 像素，因此在會裁切內容的容器中，鈴鐺周圍要留出這麼多空間。在寬 380 像素或更窄的視窗中，面板會切換為窄版配置：更緊湊的頁首與列內距、較小的標題，且「Mark all read」只顯示圖示。

<Demo name="AppHeader" />

### 焦點與關閉

- **開啟**時焦點移到關閉按鈕，因此從面板按下第一次 Tab 會到達 All，之後清單跟隨。
- **按 Escape 或點擊關閉按鈕**會關閉面板，並把焦點返回鈴鐺。
- **在面板外按下**會關閉它。只有焦點原本在面板內時，焦點才返回鈴鐺。
- **鈴鐺**會切換面板：再點一次即關閉。
- **離開視窗**：面板開啟時若鈴鐺離開視窗，面板會略過退出動畫直接關閉；若鈴鐺返回且 `IsOpen` 仍為 true，則重新開啟。
- **在 Unread 檢視中關閉或標記已讀**時，焦點依序移到下一列、上一列，然後是 Unread 按鈕。

### 文案

`Label` 為面板與鈴鐺命名。`Labels` 保存其餘所有文字，並帶有英文預設值，型別為 `NotificationCenterLabels` 物件（見 API 參考）。指派新的實例即可修改它們。在 `ReadCount` 中，`{0}` 是滾動的數字。帶數量的文字（例如標題下方的摘要）是可以在程式碼中設定的函式，因此應用程式可以套用自己的複數規則。

<Demo name="CustomWording" />

```csharp
center.Labels = new NotificationCenterLabels
{
    Summary = unread => unread > 0 ? $"{unread} 則新通知" : "沒有新通知",
    Trigger = (label, unread) => $"{label}，{unread} 則新通知"
};
```

### 從程式碼開啟

`IsOpen` 是雙向的。`Open()` 和 `Close()` 作用相同。`MarkAllRead()` 和 `ClearRead()` 執行批次操作，並觸發與按鈕相同的事件。在鈴鐺顯示到視窗中之前設定 `IsOpen`，會在視窗顯示後開啟面板。`Open()` 會把焦點移入面板，因此應在使用者預期時呼叫，而不是在每次背景更新時呼叫。

```csharp
center.Open();
center.MarkAllRead();
```

### 範例：支付後台頁首中的活動鈴鐺

一個支付團隊的管理主控台。頁首包含產品名稱、活動鈴鐺與使用者頭像。鈴鐺顯示兩則未讀更新。第一則有發出者，因此顯示此人的頭像，標題中也寫出了其姓名。第二則是使用 `Success` 語氣的系統更新。第三則已讀，因此面板中會顯示它，但沒有未讀圓點。頁首下方的頁面使用相同的表面色與字型權杖，因此頁首看起來就是產品的一部分。

![管理後台頁首中的活動鈴鐺，顯示兩則未讀更新，下方為頁面標題](images/ActivityHeader.png)

```xml
<StackPanel Width="680" Spacing="16">
    <Border CornerRadius="20"
            Padding="16,12"
            BorderThickness="1"
            BorderBrush="{arc:ArcDesignTokenResource Border}"
            Background="{arc:ArcDesignTokenResource Surface}">
        <DockPanel>
            <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" Spacing="12">
                <arc:NotificationCenter Label="Activity">
                    <arc:NotificationItem Id="assigned"
                                          Title="Priya Shah assigned you Fix checkout totals"
                                          Time="9m"
                                          ActorName="Priya Shah" />
                    <arc:NotificationItem Id="incident"
                                          Title="Incident resolved"
                                          Description="Payments API latency is back to normal."
                                          Time="40m"
                                          Tone="Success" />
                    <arc:NotificationItem Id="sso"
                                          Title="SSO certificate expires in 7 days"
                                          Description="Upload a new certificate in Security settings."
                                          Time="2h"
                                          Tone="Warning"
                                          IsRead="True" />
                </arc:NotificationCenter>
                <arc:Avatar DisplayName="Jordan Lee" VerticalAlignment="Center" />
            </StackPanel>
            <TextBlock Text="Northline"
                       VerticalAlignment="Center"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
        </DockPanel>
    </Border>
    <StackPanel Spacing="6">
        <TextBlock Text="Payments overview"
                   FontSize="{arc:ArcDesignTokenResource TypeXL}"
                   FontWeight="{arc:ArcDesignTokenResource WeightMedium}"
                   Foreground="{arc:ArcDesignTokenResource Foreground}" />
        <TextBlock Text="Checkout conversion and open incidents for the payments team."
                   FontSize="{arc:ArcDesignTokenResource TypeSM}"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **標題**：一行，點明對象與結果，例如「Invoice inv_2026_0412 was paid」。標題會以省略號截斷，因此把關鍵字放在前面。來自人的更新要寫出發出者的姓名。
- **描述**：一句話，包含使用者做決定所需的細節：金額、版本或數量。它在標題下方預覽，展開列時完整顯示。
- **時間**：簡短的標籤，例如「2m」、「40m」、「昨天」或「週一」。所有項目使用相同的單位。
- **標籤**：句首大寫的句子式寫法，操作標籤使用動詞加對象（「Mark read」、「Clear read」）。
- **Dismiss** 只會從此清單中移除更新。當刪除操作同時會刪除資料時，要在產品文案中說明。

| 不要寫 | 改為 |
|---|---|
| 新通知！ | Priya Shah 把「Fix checkout totals」指派給了你 |
| 錯誤 | 發票 inv_2026_0412 付款失敗 |
| 點這裡 | 查看更新詳細資訊 |
| 刪除 | Dismiss |
| 發生了一些事情 | 正式環境部署已完成 |

### 最佳做法

**建議**

- 為每個項目設定穩定的 `Id`，重新整理清單時保持同一個項目物件。列透過 id 比對，因此展開的列和鍵盤位置在重新整理後得以保留。
- 把 `ReadChanged` 和 `Dismissed` 儲存到你的儲存空間。控制項只改變記憶體中的狀態，因此若不處理這些事件，重新載入時會顯示舊的已讀狀態。
- 把紀錄鍵保存在 `Tag` 中，讓事件無需依標題查找即可提供紀錄。
- 需要決定的更新用 `Warning`，已完成的工作用 `Success`。語氣圖示本身承載含義，因此讓每種語氣只對應一類更新，能讓清單更易讀。
- 將 `Label` 設為產品中對清單的稱呼（「Activity」、「Inbox」）；它為鈴鐺與面板命名。

**避免**

- 為會改變資料的工作放置操作按鈕，例如核准請求。面板只提供「Mark read」、「Mark unread」和「Dismiss」；應在你自己的頁面中連結到該紀錄。
- 每次更新都呼叫 `Open()`。它會把焦點移入面板，可能打斷使用者在其他欄位中的輸入。
- 替使用者關閉未讀更新。關閉會把更新從清單中移除，使用者會失去待處理的項目。
- 僅用角標作為故障或付款失敗的唯一提示。應使用 `Alert` 在頁面上顯示這些資訊，讓使用者能在那裡採取行動。
- 包含多句話的長標題。它們會被截斷為一行，其餘內容只在詳細資訊中可見。

### 常見問題

**為什麼 Dismiss 會從我的清單中移除該項目？** 控制項在觸發 `Dismissed` 之前，會從 `Items` 中移除該項目；當 `ItemsSource` 是可寫入的 `IList` 或 `ICollection` 時，則從中移除。唯讀的資料來源會保留該項目，控制項則將其隱藏。若希望刪除是永久的，請在 `Dismissed` 處理常式中刪除紀錄。

**為什麼角標計數與我的儲存空間不一致？** `UnreadCount` 統計控制項所顯示的項目：即 `ItemsSource`（或 `Items`）中的項目，減去控制項從唯讀資料來源中隱藏的項目。請從控制項讀取計數，而不是從你自己的清單讀取，這樣鈴鐺與面板才會一致。

**點擊一列會開啟相關頁面嗎？** 不會。一列只會展開或收合其詳細資訊，面板沒有連結操作。請使用項目的 `Tag` 值，從你自己的頁面引導使用者前往該紀錄。

**如何讓發出者的照片顯示出來？** 將 `ActorSource` 設為 `ImageSource`；在 XAML 中使用 URL 即可。圖片載入期間或載入失敗時，顯示的是姓名字首。

**可以更改面板的大小或位置嗎？** 沒有公開屬性可以設定它們。面板寬 424 px（或視窗寬度減 24 px），最高 590 px，位於鈴鐺下方 12 px 處。請把鈴鐺放在面板可以向下開啟的位置。

## API 參考

<!-- api: NotificationCenter -->

<!-- api: NotificationItem -->

### NotificationCenterLabels

`NotificationCenterLabels` 保存控制項顯示或朗讀的所有文字（`Label` 除外），每項都帶有 Arc 的英文預設值。將新的實例指派給 `NotificationCenter.Labels`。

| 屬性 | 預設值 | 顯示位置 |
|---|---|---|
| `Trigger`（標籤與未讀數的函式） | "Notifications, 3 unread" | 鈴鐺的無障礙名稱 |
| `Summary`（未讀數的函式） | "3 updates waiting for you"、"You’re all caught up" | 標題下方的一行 |
| `Close` | "Close notifications" | 關閉按鈕的無障礙名稱 |
| `ViewSwitch` | "Show notifications" | All / Unread 切換的無障礙名稱 |
| `All` / `Unread` | "All" / "Unread" | 檢視按鈕 |
| `AllList` / `UnreadList` | "All notifications" / "Unread notifications" | 清單的無障礙名稱 |
| `MarkAllRead` | "Mark all read" | 工具列中的批次操作 |
| `UnreadState` | "unread" | 加入未讀列的無障礙名稱中 |
| `ShowDetails` / `HideDetails` | "Show details" / "Hide details" | 列的無障礙名稱結尾 |
| `FromActor`（姓名的函式） | "From Mia Chen" | 沒有描述的人發出之更新的預覽 |
| `ViewDetails` | "View update details" | 沒有描述的系統更新的預覽 |
| `SharedUpdate`（姓名的函式） | "Mia Chen shared an update with you." | 沒有描述的人發出之更新的詳細資訊 |
| `ReadyToReview` | "This update is ready to review." | 沒有描述的系統更新的詳細資訊 |
| `MarkRead` / `MarkUnread` | "Mark read" / "Mark unread" | 列上的已讀狀態操作 |
| `Dismiss` | "Dismiss" | 移除更新的列操作 |
| `EmptyTitle` / `EmptyDescription` | "All clear" / "New updates will appear here." | 空的 All 檢視 |
| `UnreadEmptyTitle` / `UnreadEmptyDescription` | "Nothing unread" / "You’ve seen every update." | 空的 Unread 檢視 |
| `ViewAll` | "View all updates" | 空的 Unread 檢視中返回 All 檢視的連結 |
| `ReadCount` | "{0} read" | 頁尾計數；`{0}` 為滾動的數字 |
| `ClearRead` | "Clear read" | 頁尾中移除已讀更新的操作 |

## 無障礙

- 鈴鐺是一個按鈕，名稱由 `Label` 和未讀數量組成，例如 "Notifications, 3 unread"。它會回報面板是否展開。
- 開啟面板時，焦點移到其關閉按鈕。Tab 依序經過關閉、All、Unread、「Mark all read」、各列、已展開列的按鈕和「Clear read」，並在面板內部循環。
- Escape 和關閉按鈕會關閉面板，並把焦點返回鈴鐺。
- All 與 Unread 是切換按鈕。每一列是一個按鈕，其名稱由標題與狀態組成，例如 "Deploy finished, unread. Show details"，並回報它是否展開。
- 在 Unread 檢視中把某列標記為已讀，或關閉某列之後，焦點依序移到下一列、上一列，然後是 Unread 按鈕。在「Mark all read」之後，焦點返回目前的檢視按鈕。
- 標題下方的摘要是禮貌的即時區域，因此新的計數會被朗讀出來。
- `Label` 同時為鈴鐺與面板標題命名，因此請選擇一個單獨使用也有意義的詞，例如 "Activity" 或 "Inbox"。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 鈴鐺上的 `Enter` 或 `Space` | 開啟面板，並把焦點移到其關閉按鈕 |
| 面板開啟時，鈴鐺上的 `Enter` 或 `Space` | 關閉面板，並把焦點返回鈴鐺 |
| `Escape` | 關閉面板，並把焦點返回鈴鐺 |
| `Tab` / `Shift+Tab` | 依序經過關閉、All、Unread、「Mark all read」、各列、已展開列的按鈕和「Clear read」 |
| All 或 Unread 上的 `Enter` 或 `Space` | 切換檢視；已展開的列會收合 |
| 列標題上的 `Enter` 或 `Space` | 顯示或隱藏該列的詳細資訊；同一時間只保持一列展開 |
| 「Mark read」、「Mark unread」或「Dismiss」上的 `Enter` 或 `Space` | 變更該列，然後依無障礙一節所述移動焦點 |
| 「Mark all read」上的 `Enter` 或 `Space` | 將每則未讀更新標記為已讀；焦點返回目前的檢視按鈕 |
| 「Clear read」上的 `Enter` 或 `Space` | 關閉每則已讀更新；焦點移到 All 按鈕 |

## 動態效果

- 面板從鈴鐺處生長出來：起始時朝鈴鐺方向偏移 6 px、縮放為 0.97，並在 240 ms 內淡入。關閉時在 120 ms 內以一半的距離和 0.98 的縮放離開。
- 面板開啟時，鈴鐺會傾斜。角標彈入與彈出，其數字向上或向下滾動。
- 面板開啟或檢視加入列時，列從下方 4 px 處落定，每列間隔 35 ms。離開的列淡出，並在平滑彈簧上收合高度。批次操作之後，它們依序離開，與第一列的間隔最多 200 ms。
- 計數滾動，摘要上移變為新的措辭，All 與 Unread 的高亮在按鈕之間滑動。詳細資訊、頁尾和空狀態以相同的彈簧展開與收合高度。
- 減少動態效果只保留 120 ms 的短淡入淡出，沒有位移、縮放或模糊。面板立即顯示和隱藏。關閉動態效果時，所有變化立即完成。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 面板開啟 | 從鈴鐺處生長出來，6 px 與 0.97 縮放，240 ms | 立即顯示 | 立即完成 |
| 面板關閉 | 120 ms 內以一半距離離開 | 立即隱藏 | 立即完成 |
| 列進入 | 上升 4 px 並淡入，每列間隔 35 ms | 短淡入，無位移 | 立即完成 |
| 批次操作後列離開 | 高度在平滑彈簧上收合，串接最多 200 ms | 短淡出，無收合 | 立即完成 |
| 開啟時的鈴鐺 | 傾斜，面板關閉後復位 | 保持直立 | 立即完成 |
| 未讀角標 | 彈入與彈出；數字滾動 | 短淡入 | 立即完成 |
| 檢視高亮 | 在 All 與 Unread 之間滑動 | 短淡入 | 立即完成 |

## 相關元件

- `Toast` 用於自行消失的簡短訊息。
- `Popover` 用於錨定在按鈕上的其他內容。
- `Badge` 用於其他控制項上的計數或狀態。

| 元件 | 適用情境 |
|---|---|
| `Toast` / `ToastStack` | 簡短結果只需出現一次，無需操作即可消失 |
| `Alert` | 目前頁面的問題必須保持可見，直到修正 |
| `Popover` | 其他內容需要錨定在按鈕上，例如篩選表單 |
| `Badge` | 計數或狀態附著在其他控制項上，且不需要更新清單 |
| `Timeline` | 單筆紀錄的事件需要按順序閱讀 |
| `ChatThread` | 使用者在對話中彼此回覆 |
