---
title: 開關
summary: 用於立即生效設定的觸感式開關。
source-hash: 57c51b4e68a2
---

<Demo name="SettingsList" />

## 何時使用

開關用於開啟或關閉一項設定，使用者一做出變更，設定立即生效。它屬於設定頁、偏好設定面板或帳號區域，其中每一列都是可以單獨修改的設定，無需等待儲存按鈕。軌道與滑塊一眼即可顯示狀態，使用者無需兩次閱讀標籤就能看清目前的設定。

- **管理主控台中的通知偏好**：「付款失敗時寄電子郵件通知我」或「寄送每週營收摘要」。每個開關都會立即套用其設定，頁面無需確認步驟。
- **SaaS 工作區中的功能可用性**：「啟用測試版報告」或「顯示已封存專案」。開關是該列中唯一的控制項，因此標籤說明了會改變的內容。
- **帳號頁面中的隱私與資料分享**：「與產品團隊分享使用資料」。開關為使用者提供清楚的開或關選擇，並顯示目前的狀態。
- **每列一個開關的設定清單**：通知頁面中，每個管道（例如電子郵件、簡訊或應用程式內通知）各有一個開關和各自的說明。
- **開發者工具中的營運開關**：「暫停此專案的部署」或「將記錄鏡像到稽核串流」。效果會立即開始，因此不能用於需要審核步驟的情境。

## 何時不用

- **需要透過送出按鈕確認的選擇**，例如接受條款或在結帳時選擇加入某個方案：用 `Checkbox`，其值只在表單送出時才儲存。
- **在具名選項之間做選擇**，例如日、週或月：短檢視用 `SegmentedControl`，每個選項都需要說明時用 `RadioGroup`。
- **使用者可以選取多個選項的情況**：為每個選項使用一個 `Checkbox`，讓表單讀起來是一組選擇，而不是一組含相互依賴意義的開關。
- **立即執行的動作**，例如「重新啟動服務」或「寄送發票」：用 `Button`。開關表示一種狀態，按鈕表示一個動作。
- **在生效前需要確認或說明理由的設定**，例如停用單一登入：由執行變更的動作彈出對話方塊，而不是用一個立即套用變更的開關。

## 用法

`Content` 是開關的標籤。整個標籤都屬於點選範圍。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Switch Content="電子郵件通知" IsChecked="True" />
</UserControl>
```

`Switch` 衍生自 Avalonia 的 `ToggleButton`，因此 `IsChecked`、`IsCheckedChanged`、`Content` 以及切換按鈕的點選和鍵盤行為都保留著。控制項在此之上增加了軌道與滑塊的設計、滑塊的按壓拉伸以及 Arc 動態效果。狀態是二值的：`IsChecked` 為 `true` 或 `false`，開關沒有混合狀態。

### 結構

1. **軌道**（42 × 24 px，全圓角）：滑塊移動的底面。關閉時為控制項的軌道顏色，關閉時懸停為懸停軌道顏色，開啟時為強調色（`ControlOn`）。狀態變更時淡變為強調色。
2. **滑塊**（18 px，全圓角，帶 Arc 滑塊陰影）：位於軌道內側 3 px。從關閉位置到開啟位置移動 18 px。關閉時為滑塊顏色，開啟時為開啟滑塊顏色。
3. **按下的滑塊**：指標或空白鍵按住時，滑塊向另一側加寬到 23 px，其遠端邊緣保持不動。這是按下的回饋，並顯示滑塊將往哪一側移動。
4. **標籤**（`Content`）：位於軌道右側 12 px，為小型字級和前景色。`Content` 為空時隱藏。
5. **焦點環**：僅用於鍵盤焦點的 2 px 環，其內邊緣位於軌道外側 2 px 處。

開關的最小高度為 Arc 中型控制項尺寸。停用的開關淡化為 50% 不透明度，並顯示禁止游標。

### 設定清單

將開關堆疊起來，即形成一個設定清單。每個開關的標籤都說明設定的名稱，各列之間靠清單的間距分隔，而不是用線條。

```xml
<StackPanel Spacing="4">
    <arc:Switch Content="電子郵件通知" IsChecked="True" />
    <arc:Switch Content="每週摘要" />
    <arc:Switch Content="產品更新" />
</StackPanel>
```

標籤應保持為設定的名稱，而不是描述其狀態的句子。「每週摘要」在兩種狀態下都清楚，而「開啟後寄送每週摘要」在開關關閉時讀起來就很彆扭。當效果需要解釋時，在標籤下方的文字區塊中放一段簡短說明，並讓它與同一標籤保持在同一欄中。

### 繫結

將 `IsChecked` 繫結到檢視模型屬性。繫結 `IsEnabled` 可鎖定開關。

```xml
<arc:Switch Content="電子郵件通知"
            IsChecked="{Binding EmailEnabled}"
            IsEnabled="{Binding CanEditSettings}" />
```

`IsChecked` 是雙向的，因此滑鼠或鍵盤做出的變更會立即更新該屬性。將 `IsEnabled` 繫結到權限，或繫結到阻擋變更的狀態（例如由組織管理的設定），並在附近說明原因。

### 繫結到檢視模型

使用 CommunityToolkit.Mvvm 時，檢視模型將每項設定保存為一個屬性，修改即儲存。開關本身不儲存任何內容，由處理常式儲存使用者設定的值。

```csharp
public interface ISettingsService
{
    Task SetAsync(string key, bool enabled);
}

public partial class NotificationSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    public NotificationSettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
    }

    [ObservableProperty]
    private bool _emailNotifications;

    [ObservableProperty]
    private bool _canEditSettings = true;

    partial void OnEmailNotificationsChanged(bool value)
    {
        _ = _settings.SetAsync("notifications.email", value);
    }
}
```

```xml
<arc:Switch Content="電子郵件通知"
            IsChecked="{Binding EmailNotifications}"
            IsEnabled="{Binding CanEditSettings}" />
```

- `IsChecked` 是要繫結的值。請使用一般的 `bool` 屬性，因為開關從不產生 null 值。
- 只要屬性變動，產生的 `OnEmailNotificationsChanged` 方法就會執行，包括已儲存的值被載入進來的時候。請在顯示頁面之前載入已儲存的值，或在值與已儲存的值相同時略過儲存，這樣載入不會把設定寫回去。
- 將儲存邏輯放在檢視模型中，而不是檢視中。這樣，無論使用者是用滑鼠、鍵盤還是程式碼變更，同一設定都會以相同方式儲存。

### 停用

停用的開關保留其狀態，且無法變更。

<Demo name="Disabled" />

當設定目前無法套用時停用開關，並在旁邊的說明中說明原因，例如「由管理員開啟」。清單中被停用的開關仍會被讀成清單的一部分，因此應保留它的位置，而不是隱藏。

### 變更

每當狀態改變時，無論來自指標、鍵盤還是程式碼，都會引發 `IsCheckedChanged`。在此處套用設定，這樣每種變更方式的效果都相同。

```csharp
notifications.IsCheckedChanged += (_, _) =>
{
    settings.EmailEnabled = notifications.IsChecked == true;
};
```

無論是程式碼變更還是使用者的輸入，該事件都會觸發。當儲存失敗時，請在同一處理常式中把 `IsChecked` 設回先前的值，並在開關附近顯示錯誤，使開關顯示的始終是真正儲存下來的值。

### 範例：通知設定

SaaS 管理主控台中為帳單管理員準備的通知頁面。每一列用一個簡短的名詞片語命名一項設定，其下的說明描述該設定寄送什麼內容以及何時寄送。各列之間以線條分隔，並對齊在同一欄上。發票提醒一列被停用，因為由組織設定，頁尾說明變更會立即儲存，使使用者知道不會再有儲存步驟。

![一張通知設定卡片，其中三個開關分別為開、關，以及一個被停用的開關](images/NotificationSettings.png)

```xml
<Border Width="640"
        Padding="24"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1"
        CornerRadius="{arc:ArcDesignTokenResource RadiusControl}">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Billing notifications"
                       FontSize="18"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Choose which billing events email the workspace owners."
                       FontSize="13"
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Payment failures"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Sent at once when a card payment fails."
                           FontSize="12"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:Switch Grid.Column="1"
                        AutomationProperties.Name="Payment failures"
                        IsChecked="True" />
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="*,Auto">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Weekly revenue digest"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Mondays at 08:00 UTC, with the previous week's totals."
                           FontSize="12"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:Switch Grid.Column="1"
                        AutomationProperties.Name="Weekly revenue digest" />
        </Grid>
        <Border Height="1" Background="{arc:ArcDesignTokenResource Border}" />
        <Grid ColumnDefinitions="*,Auto">
            <StackPanel Spacing="2" VerticalAlignment="Center">
                <TextBlock Text="Invoice reminders"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <TextBlock Text="Set by your organization. Ask an owner to change it."
                           FontSize="12"
                           Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            </StackPanel>
            <arc:Switch Grid.Column="1"
                        AutomationProperties.Name="Invoice reminders"
                        IsChecked="True"
                        IsEnabled="False" />
        </Grid>
        <TextBlock Text="Changes save as soon as you switch them."
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
    </StackPanel>
</Border>
```

### 文案規範

- **標籤**：設定的名稱，使用句首大寫，不加冒號，也不加問號。寫「電子郵件通知」，而不是「要接收電子郵件通知嗎？」。
- **說明**：一句話，說明該設定的作用以及何時生效。時間和數量用阿拉伯數字書寫。
- **停用原因**：說明誰可以變更該設定，例如「由組織設定」，以及如何申請變更。
- **狀態文字**：沒有儲存按鈕時，應說明變更會立即儲存。例如「切換後變更即刻儲存。」

| 不要寫 | 改為 |
|---|---|
| 啟用 | 電子郵件通知 |
| 切換 | 開啟後即可接收每週摘要。 |
| 要不要接收電子郵件？ | 電子郵件通知 |
| 設定已停用 | 由貴組織設定。請聯絡擁有者變更。 |

### 最佳做法

**建議**

- 使用者一做出變更就立即套用，若儲存可能失敗，則在開關附近顯示結果。顯示著伺服器尚未接受的狀態，比先等待的開關更糟。
- 在每種狀態下都保持標籤相同，讓使用者在兩種位置都能讀到設定的名稱。
- 將相關的開關歸在同一標題下，並且每次造訪頁面時保持順序不變。
- 為僅含圖示的開關設定 `AutomationProperties.Name`，因為它沒有標籤可供命名。

**避免**

- 用開關來表示需要確認或說明理由的選擇。對話方塊或送出按鈕能讓這一步驟清楚可見。
- 在檢視模型之外的多個程式碼路徑中變更狀態，而沒有統一的儲存入口。這樣設定就取決於它被變更的方式。
- 為了精簡清單而隱藏停用的開關。使用者便無從得知該設定存在，也不知道它為何關閉。
- 在標籤中寫入狀態（「開：電子郵件通知」）。開關本身已經顯示狀態，標籤重複說明會造成重複閱讀。

### 常見問題

**程式碼變更狀態時，滑塊會有動畫嗎？** 會，在完整動態效果下會。並非由按壓引起的變更會播放一次短暫的滑塊拉伸，使程式碼變更可以被看見。在按壓放開後 250 ms 之內發生的變更不會拉伸，因為按壓本身已經展示了動態效果。

**開關可以沒有標籤嗎？** 可以。沒有 `Content` 時，只顯示軌道和滑塊，開關沒有可見的名稱。請設定 `AutomationProperties.Name`，以便輔助技術讀取。

**在程式碼中設定 `IsChecked` 會儲存設定嗎？** 不會。設定它只會改變狀態、引發 `IsCheckedChanged` 並播放拉伸動態效果。儲存只在你的處理常式或檢視模型呼叫之處進行。

**儲存過程中開關會顯示載入狀態嗎？** 不會。如果在結果確定之前使用者不得再次變更，請在儲存期間用 `IsEnabled` 停用開關。

**為什麼螢幕閱讀器把開關識別為切換按鈕？** Avalonia 沒有獨立的開關控制項類型，因此開關會以其選取或未選取狀態被回報為切換按鈕。

## API 參考

<!-- api: Switch -->

## 無障礙

- `Content` 即開關的名稱。只有圖示的開關需要設定 `AutomationProperties.Name`。
- 空白鍵和 Enter 鍵可切換開關。按住空白鍵時，滑塊保持拉伸的形狀。
- 輔助技術會將開關回報為處於選取或未選取狀態的切換按鈕。Avalonia 沒有獨立的開關控制項類型。
- 鍵盤焦點會繪製一個偏移 2px 的 2px 外框線。滑鼠焦點不會繪製。
- 滑塊對輔助技術隱藏。開和關的狀態同時透過軌道顏色和滑塊位置顯示。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移到開關，再移到下一個或上一個控制項 |
| `Space` | 切換開關；按住 `Space` 時滑塊保持拉伸 |
| `Enter` | 切換開關 |

## 動態效果

- 按下開關時，滑塊會向另一側加寬，遠端邊緣保持不動。放開後，滑塊以無彈跳的彈簧滑過去。
- 並非由按下引起的變更（例如按鍵或程式碼修改），會播放一次短暫的滑塊拉伸。
- 軌道填色會淡入淡出，滑塊顏色隨之變化。
- 減少動態效果時，取消拉伸，滑塊立即移動，並關閉顏色過渡。關閉動態效果時效果相同。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 按下後滑塊移動 | 以 0.3 s 的無彈跳彈簧滑過去 | 立即移動 | 立即移動 |
| 按下時滑塊加寬 | 彈到 23 px 寬；遠端邊緣保持不動 | 不加寬 | 不加寬 |
| 由按鍵或程式碼引起的變更 | 拉伸到 116% 再回彈，約 0.34 s | 無拉伸 | 無拉伸 |
| 軌道填色與滑塊顏色 | 在標準緩動上交叉淡變 | 立即完成 | 立即完成 |

## 相關元件

- `Checkbox`：用於需要透過送出按鈕確認的選擇。
- `SegmentedControl`：用於在幾個具名檢視之間切換。
- `RadioGroup`：用於從一組選項中恰好選取一項。

| 元件 | 適用情境 |
|---|---|
| `Checkbox` | 該值隨表單或送出按鈕一起儲存 |
| `SegmentedControl` | 選擇的是同一內容的幾個具名檢視之一 |
| `RadioGroup` | 選擇的是一組選項中的一項，且選項帶有說明 |
| `Button` | 控制項立即執行一個動作，而不是設定狀態 |
