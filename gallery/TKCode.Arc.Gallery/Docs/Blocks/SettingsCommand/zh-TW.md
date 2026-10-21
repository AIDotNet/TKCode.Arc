---
title: 設定命令面板
summary: 用一個搜尋欄找到任一設定並直接修改：開關翻轉、選項輪替、連結開啟，全程可用鍵盤操作。
source-hash: 9b4358b56dc1
---

<Demo name="Inline" />

## 何時使用

- 用鍵盤快速找到任一設定，可與完整的設定頁面並存，也可取代設定頁面。
- 立即生效的設定：開關、少數具名的值，以及通往其餘設定所在頁面的連結。
- 透過快捷鍵（例如 Ctrl+, 或 ⌘,）在對話框中開啟的設定面板。

## 何時不用

- 需要表單、驗證或儲存步驟的設定：請使用包含 `SettingsGroup` 與 `SettingsRow` 的設定頁面。
- 選項很多或需要自由輸入的設定，例如時區或顯示名稱：改為連結到一個可執行動作的頁面。
- 不屬於設定的應用程式命令，例如「新增專案」：請使用命令面板。

## 用法

在面板內撰寫設定。`SettingsCommandToggle` 表示開或關，`SettingsCommandChoice` 依序切換其 `Options`，`SettingsCommandAction` 執行一項命令。`Group` 將設定列在某個標題之下，`Keywords` 則加入其他可用來找到該設定的字詞。焦點會留在搜尋欄中：上、下鍵移動反白，Enter 執行反白的列，左、右鍵逐一切換選項。底部列會確認每一次變更。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SettingsCommand MaxWidth="560">
        <arc:SettingsCommandChoice Group="外觀" Header="主題" Options="淺色, 深色, 跟隨系統" Keywords="深色 淺色" />
        <arc:SettingsCommandToggle Group="外觀" Header="精簡模式" Description="設定列更緊密，間距更小。" />
        <arc:SettingsCommandAction Group="帳戶" Header="變更電子郵件" IsNavigation="True" />
    </arc:SettingsCommand>
</UserControl>
```

### 搜尋

查詢的每個字詞都必須出現在標題、說明、群組或關鍵字中，不區分大小寫。排序依序為：標題以查詢開頭的列、標題中有某個字詞以查詢開頭的列、標題包含查詢的列，其餘的列排在最後。同一層級保持宣告順序，群組依其最佳的一列排序。標題中相符的部分保持醒目，其餘部分改為次要色。沒有相符結果時，面板會明說並建議換一個字詞。

### 在對話框中

在 `Dialog` 中將 `IsFramed` 設為 `False`，使面板不再繪製第二層外框。範例還給它一個與對話框內距相同大小的負邊界，讓搜尋列與底部列延伸到對話框邊緣。查詢為空時按下 Escape，會觸發 `CloseRequested`。請在此關閉對話框並將事件標記為已處理，對話框就不會再次處理同一個按鍵。範例也以 Ctrl+,（Mac 上為 ⌘,）開啟對話框，這是設定的常用快捷鍵。

<Demo name="InDialog" />

```csharp
private void OnCloseRequested(object? sender, RoutedEventArgs e)
{
    e.Handled = true;
    SettingsDialog.Close();
}
```

### 繫結值

`IsChecked` 與 `SelectedOption` 為雙向繫結，應用程式修改值時，各列會跟著更新。`SettingChanged` 只會在面板中的變更發生時引發。其 `Value` 對開關為 `bool`，對選項則是所選的選項。動作會先執行其 `Command`，接著引發 `ItemInvoked`。

<Demo name="Bound" />

```csharp
palette.SettingChanged += (_, e) => Save(e.Item.Header, e.Value);
palette.ItemInvoked += (_, e) => Navigate(e.Item.Header);
```

### 快捷鍵與動作

`Shortcut` 會以鍵帽的形式顯示動作的組合鍵。以 `mod` 代表命令鍵，在 Mac 上為 ⌘，其他平台則為 Ctrl。面板只負責顯示快捷鍵，實際的回應由應用程式處理。`IsNavigation` 會為開啟其他頁面的動作加上箭頭。將 `IsEnabled` 設為 `False`，可顯示目前無法變更的設定：該列會變暗，鍵盤也會略過它。

```xml
<arc:SettingsCommand>
    <arc:SettingsCommandAction Header="下載你的資料" Shortcut="mod+shift+e" />
    <arc:SettingsCommandAction Header="兩步驟驗證" IsNavigation="True" />
    <arc:SettingsCommandToggle Header="已讀回條" Description="已由管理員關閉。" IsEnabled="False" />
</arc:SettingsCommand>
```

### 文字

內建文字來自 `Labels`。若要翻譯，請整個替換該物件。

```csharp
palette.Labels = new SettingsCommandLabels
{
    Placeholder = "Instellingen zoeken",
    NoResults = query => $"Geen instellingen voor “{query}”",
    TurnedOn = header => $"{header} aan",
    TurnedOff = header => $"{header} uit",
};
```

## API 參考

<!-- api: SettingsCommand -->
<!-- api: SettingsCommandToggle -->
<!-- api: SettingsCommandChoice -->
<!-- api: SettingsCommandAction -->

### SettingsCommandLabels

| 屬性 | 型別 | 說明 |
|---|---|---|
| `Placeholder` | `string` | 搜尋欄的預留文字：「搜尋設定」。 |
| `SearchName` | `string` | 搜尋欄的無障礙名稱：「搜尋設定」。 |
| `ResultsName` | `string` | 結果清單的無障礙名稱：「設定」。 |
| `NoResults` | `Func<string, string>` | 沒有相符結果時的訊息：「沒有符合『主題』的設定」。 |
| `NoResultsHint` | `string` | 其下方的提示：「換個字詞再試，例如主題或電子郵件。」 |
| `Navigate`, `Toggle`, `Change`, `Open`, `Close` | `string` | 底部列中按鍵旁的操作提示。 |
| `EscapeKey` | `string` | Escape 鍵帽的文字：「Esc」。 |
| `TurnedOn`, `TurnedOff` | `Func<string, string>` | 開關變更後的提示：「精簡模式已開啟」。 |
| `ChangedTo` | `Func<string, string, string>` | 選項變更後的提示：「主題：深色」。 |
| `Ungrouped` | `string` | 未設群組之設定的群組標題，在其他設定有群組時顯示：「設定」。 |
| `On`, `Off` | `string` | 輔助技術朗讀開關狀態時使用的文字。 |
| `RowState` | `Func<string, string, string>` | 列的標題與狀態：「深色模式，開啟」。 |
| `PreviousOption`, `NextOption` | `string` | 選項值兩側箭頭的名稱。 |

### 事件參數

| 型別 | 屬性 | 說明 |
|---|---|---|
| `SettingsCommandChangedEventArgs` | `Item` | 發生變更的設定。 |
| `SettingsCommandChangedEventArgs` | `Value` | 新值：開關為 `bool`，選項為所選的選項。 |
| `SettingsCommandItemEventArgs` | `Item` | 被叫用的動作。 |

## 無障礙

- 搜尋欄名為「搜尋設定」。其說明文字會跟著反白的列變化，例如「精簡模式，關閉」或「主題，深色」。
- 結果是名為「設定」的清單，每一列都是以其標題命名的清單項目。
- 焦點始終留在搜尋欄中。上、下鍵移動反白並略過已停用的列，Page Up 與 Page Down 各移動五列。清單不會循環。
- Enter 會切換開關、將選項移到下一個值，或執行動作。左、右鍵逐一切換選項且不循環。其他情況下，左、右鍵只移動插入點。
- Escape 會清空查詢。查詢為空時按下 Escape 會觸發 `CloseRequested`。
- 變更提示是禮貌的即時區域，因此螢幕閱讀器會朗讀「精簡模式已開啟」。
- 開關、選項旁的箭頭與鍵帽都無法取得焦點。點擊任一列的效果與按 Enter 相同，焦點仍留在搜尋欄中。

## 動態效果

- 反白會以反應靈敏的彈簧在各列之間滑動。結果變動時，反白會立即移動。
- 清單會平滑捲動，讓反白的列及其群組標題保持可見。
- 選項的值會依變化方向向上或向下捲動，開關的滑塊也會滑動。
- 變更提示會伴隨柔和的模糊上移出現，並在 2.5 秒後淡出。
- 減少動態效果時，提示與值淡入淡出，反白立即移動。關閉動態效果時，每項變更都立即顯示。

## 相關元件

- `SettingsPage`：包含區段導覽與儲存列的完整設定頁面。
- `SecuritySettings`、`BillingOverview`、`UsageBilling` 與 `CancelFlow`，即其他設定區塊。
- `Switch`：開關列中的控制項；`Dialog`：透過快捷鍵承載面板的對話框。
- `SearchField` 與 `Select`：用於表單中單一的搜尋或選擇。
