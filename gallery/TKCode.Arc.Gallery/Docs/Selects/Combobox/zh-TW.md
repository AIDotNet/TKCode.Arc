---
title: 組合方塊
summary: 無需離開欄位即可搜尋並從清單中選取。
source-hash: 1f5bd673d3ed
---

<Demo name="Searchable" />

## 何時使用

`Combobox` 是用於過長、難以掃視清單的單一值選擇器，適合使用者已經知道想要什麼名稱的情境。它在表單中的外觀與輸入框相似：可見的標籤、44 px 的欄位與一行說明，使用者輸入時清單會在欄位下方展開。凡是捲動尋找的成本高於直接輸入的清單，都適合使用它。

- **帳號設定中的時區與國家**：團隊從 IANA 時區清單中設定預設時區或帳單國家。使用者輸入「berlin」「new york」或「UTC+01」，即可一步找到符合的項目。
- **CRM 或服務台中的帳號負責人與指派對象**：商機負責人或工單指派對象從團隊通訊錄中選取。`Keywords` 讓使用者可以透過電子郵件別名或團隊名稱找到「Priya Raman」。
- **訂單編輯器中的產品與 SKU 查詢**：一筆訂單明細需要從目錄中選取一項產品。關鍵字包含 SKU 與舊產品名稱，因此搜尋已停售的名稱仍能找到目前的產品。
- **財務工具中的帳本與成本中心選取**：一份費用報告需要從數百個成本中心中選出一個。停用的選項仍然可見，使用者因此能看到某個中心存在，但已不允許新增支出。
- **選取目標的開發者工具**：部署表單需要從長清單中選取一個環境或一個地區。搜尋會縮小範圍，所選的標籤則作為使用者閱讀的值保留在欄位中。

## 何時不用

- **簡短固定、無須輸入的清單**：用 `Select`。三到七個選項在清單中比藏在搜尋欄後面更容易閱讀。
- **需要選取多個值**：用 `MultiSelect`，它會把每個選擇都以標記的形式保持可見，而且選完後不會關閉。
- **使用者經常切換的面向**：用 `ChipGroup`，它的每個選項都可見，一次點選即可改變篩選。
- **搜尋的目的是瀏覽內容而非設定值**：在頁首使用 `ExpandingSearch`，它開啟的是結果，而不是填入欄位。
- **不是從清單中選取的自由文字**：用 `Input`。組合方塊只保存其項目中的值。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Combobox Label="框架"
                  Width="280"
                  Description="輸入以搜尋。">
        <arc:SelectItem Content="Avalonia" Keywords="xaml dotnet" />
        <arc:SelectItem Content="AtomUI" Keywords="ant design" />
        <arc:SelectItem Content="MAUI" Keywords="xamarin" IsEnabled="False" />
        <arc:SelectItem Content="WinUI" />
    </arc:Combobox>
</UserControl>
```

`Combobox` 衍生自 Avalonia 的 `SelectingItemsControl`，因此 `ItemsSource`、`Items`、`SelectedItem`、`SelectedIndex`、`SelectedValue`、`Selection` 與 `SelectionChanged` 的行為和清單方塊一致，所選項目一定是其中的某一項。此控制項新增的是欄位本身：會篩選清單的文字輸入框、標籤與說明列、彈出清單以及清除按鈕。在 `SelectingItemsControl` 中，每個 `SelectItem` 就是一個選項，其上的 `Keywords` 會參與搜尋，但從不顯示。

### 結構

1. **標籤**（`Label`）：欄位上方的可見標籤，使用小號字型尺寸、中等字重，位於欄位上方 8 px。它同時為輔助技術標示欄位及其清單。
2. **欄位**：高 44 px（中等控制項尺寸），1 px 邊框、控制項圓角、表面色背景，水平內距 12 px。懸停與焦點只改變顏色，不改變尺寸。
3. **文字輸入框**：清單開啟時顯示查詢文字。清單關閉時顯示所選的標籤，而不是值。
4. **預留位置文字**（`Placeholder`）：預設文字為「搜尋或選取…」。使用者搜尋時，所選的標籤會以淡化的文字留在欄位中，因此目前的選擇不會遺失。
5. **清除按鈕**：22 px 的按鈕，在已有選取內容時出現。它會清除選取內容與查詢文字。
6. **說明**（`Description`）：欄位下方淡化的提示行，12 px，使用淡化的文字顏色。
7. **清單**（彈出層）：在欄位下方 8 px 處展開，使用面板圓角、5 px 的內距，選項列高 36 px。清單最高 300 px，且不超過視窗高度的 40%（`MaxDropDownHeight`）。
8. **狀態列**（`EmptyText`）：選項下方的結果數量；沒有符合的項目時顯示空白文字（預設為「沒有找到符合的項目」）。使用小號字型尺寸與淡化顏色。

### 選擇資料

無論清單有五個選項還是五百個，欄位都相同。差別在於項目資料：

- 對於在 XAML 中定義的簡短固定清單，使用 `SelectItem` 子元素，如用法範例所示。
- 對於來自程式碼或檢視模型的清單，將 `ItemsSource` 與 `SelectOption` 記錄搭配使用。`SelectOption` 接收一個值與一個標籤；`IsDisabled` 標示顯示但無法使用的選項；`Keywords` 新增搜尋時符合的詞語。
- 當項目是你自己的物件時，設定 `TextSelector` 與 `ValueSelector`，使標籤與儲存的值來自屬性，而不是 `ToString()`。

```csharp
var zone = new Combobox
{
    Label = "時區",
    Width = 320,
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Keywords = $"{c.Zone} {c.Offset}"
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

### 繫結到檢視模型

使用 CommunityToolkit.Mvvm 時，檢視模型同時持有值與選項清單，組合方塊透過繫結編輯這兩者。值是儲存的識別碼，標籤則顯示給使用者看。

```csharp
public partial class TeamSettingsViewModel : ObservableObject
{
    public IReadOnlyList<SelectOption> Owners { get; }

    [ObservableProperty]
    private string? _ownerId;

    [ObservableProperty]
    private string? _timeZoneId = "europe-berlin";

    public TeamSettingsViewModel(IDirectoryService directory)
    {
        Owners = directory.ListMembers()
            .Select(m => new SelectOption(m.Id, m.DisplayName) { Keywords = m.Email })
            .ToArray();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (OwnerId is null)
        {
            return;
        }
        await _settings.SaveAsync(OwnerId, TimeZoneId);
    }
}
```

```xml
<StackPanel Width="360" Spacing="16">
    <arc:Combobox Label="帳號負責人"
                  Placeholder="搜尋成員"
                  ItemsSource="{Binding Owners}"
                  SelectedValue="{Binding OwnerId, Mode=TwoWay}" />
    <arc:Combobox Label="預設時區"
                  ItemsSource="{Binding TimeZones}"
                  SelectedValue="{Binding TimeZoneId, Mode=TwoWay}" />
    <arc:Button Content="儲存設定" Command="{Binding SaveCommand}" />
</StackPanel>
```

- `SelectedValue` 為雙向繫結，尚未選取任何項目時為 `null`。請以 `Mode=TwoWay` 繫結，使檢視模型能接收使用者的選擇，並能從程式碼中設定欄位。
- `SelectedText` 為唯讀，保存所選項目的標籤。請把它顯示在摘要列中，而不要再繫結回去。
- `IsOpen` 為雙向繫結。只有當命令需要在跳轉前關閉清單時，才從程式碼中設定它。
- 不要為來自資料庫的值繫結 `SelectedIndex`。重新載入清單時索引會改變，因此應改為繫結值。

### 搜尋與篩選

預設情況下，搜尋會以不區分大小寫的子字串比對標籤、中繼資訊與關鍵字，結果清單保持項目原有的順序。`SearchText` 回報目前的查詢字詞，清單關閉時為空。

設定 `FilterPredicate` 可以變更比對方式。它會接收來源項目與去除前後空白的查詢字詞，並且會在每次按鍵時對每個項目執行一次，因此應保持開銷很小。

<Demo name="CustomFilter" />

```csharp
combo.FilterPredicate = (item, query) =>
    item is SelectOption option && option.Label.StartsWith(query, StringComparison.CurrentCultureIgnoreCase);
```

對於使用者以首字母掃視的清單，例如國家清單，使用前綴比對。對於名稱與別名，保留預設的子字串比對，使「york」能找到詞中間的「New York」。

### 預留位置文字、空白文字與狀態

欄位為空時顯示 `Placeholder`。`EmptyText` 是沒有任何符合項目時的狀態列，它應指出被搜尋的對象，例如「沒有符合的時區。」。狀態列要簡短，因為它出現時會被播報。

```xml
<arc:Combobox Label="國家"
              Placeholder="搜尋國家"
              EmptyText="沒有符合的國家。" />
```

### 清除與焦點

`Clear()` 會清除選取內容與查詢字詞，使用者按下清除按鈕時也會執行同樣的操作。使用 `FocusInput()` 把焦點移到欄位上，並開啟清單，效果與使用者以 Tab 切換到欄位時相同。

```csharp
zone.Clear();
zone.FocusInput();
```

當空白狀態是合法的結果時，清除是正確的做法，例如一個可以符合全部內容的篩選。對於必填的值，不要在儲存時清空欄位；應保留該值，並在欄位旁顯示訊息。

### 驗證

組合方塊本身沒有訊息列。它的 `Description` 適合放置永遠成立的限制（「用於報表排程。」）。對於可能不符合的規則，請在欄位下方放置一個 `TextBlock`，把它的文字繫結到檢視模型的錯誤，並使用危險色設計權杖對應的文字顏色。應在清單關閉時檢查規則，而不是在每次按鍵時檢查，這樣訊息就不會在使用者仍在搜尋時出現。

| 規則 | 何時檢查 | 顯示在哪裡 |
|---|---|---|
| 必須選取一個值 | 儲存時，或清單關閉且沒有值時 | 欄位下方的訊息，並把焦點移到該處 |
| 值仍然可用 | 儲存時，因為選項之後可能被停用 | 欄位下方的訊息，並指出該選項 |
| 負責人必須屬於團隊 | 伺服器回應時 | 同一訊息列，值變動時清除 |

### 內嵌選項與停用選項

停用的選項會顯示在清單中，但無法選取。方向鍵會跳過它們。這適用於存在但對使用者不可用的選項，例如已關閉的成本中心。請在選項標籤或說明中解釋原因，而不要只把該列變灰。

<Demo name="InlineOptions" />

```xml
<arc:SelectItem Content="MAUI" Keywords="xamarin" IsEnabled="False" />
```

### 停用

停用的 `Combobox` 會保留其值並忽略輸入。它無法開啟，因此使用者仍可讀取該值，卻無法變更它。當值由政策決定時使用它，例如工作區建立後不能變更的會計年度起始月份，並在說明中寫明原因。

<Demo name="Disabled" />

### 範例：團隊設定表單

管理主控台中的團隊設定表單。負責人欄位是一個可搜尋的人員清單，關鍵字包含各人的電子郵件地址，因此使用者可以輸入姓氏或網域。時區欄位已關閉並選定了一個值；會計年度欄位被停用，並在說明中給出原因。標籤是名詞，預留位置文字說明要搜尋什麼，說明文字則寫出每個選擇的效果。

![團隊設定表單，包含可搜尋的負責人欄位、時區欄位與停用的會計年度欄位](images/TeamSettings.png)

```xml
<StackPanel Width="560" Spacing="20">
    <arc:Combobox Label="Account owner"
                  Width="360"
                  Placeholder="Search people"
                  Description="Receives billing and security emails.">
        <arc:SelectItem Content="Priya Raman" Keywords="priya.raman@acme.com finance" />
        <arc:SelectItem Content="Marcus Hale" Keywords="marcus.hale@acme.com operations" />
        <arc:SelectItem Content="Elena Ruiz" Keywords="elena.ruiz@acme.com support" />
        <arc:SelectItem Content="Tomas Berg" Keywords="tomas.berg@acme.com engineering" />
    </arc:Combobox>
    <arc:Combobox Label="Default time zone"
                  Width="360"
                  SelectedIndex="1"
                  Description="Used for report schedules and invoice dates.">
        <arc:SelectItem Content="Pacific time (UTC-08:00)" Keywords="los angeles seattle" />
        <arc:SelectItem Content="Central European time (UTC+01:00)" Keywords="berlin paris rome" />
        <arc:SelectItem Content="Japan time (UTC+09:00)" Keywords="tokyo osaka" />
    </arc:Combobox>
    <arc:Combobox Label="Fiscal year starts"
                  Width="360"
                  IsEnabled="False"
                  SelectedIndex="0"
                  Description="Set when the workspace is created and cannot change.">
        <arc:SelectItem Content="January" />
        <arc:SelectItem Content="April" />
    </arc:Combobox>
    <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
        <arc:Button Content="Cancel" Variant="Secondary" />
        <arc:Button Content="Save settings" />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **標籤**：命名所選值的名詞，使用句首大寫，例如「帳號負責人」或「預設時區」。不要加冒號或問號。
- **預留位置文字**：動作加物件，例如「搜尋成員」或「搜尋國家」。它不應重複標籤。
- **說明**：一句話說明選擇的效果，例如「用於報表排程」，或說明某個選項無法選取的原因。
- **空白狀態文字**：寫明搜尋的對象，並說明沒有符合的項目。只保留一行。
- **選項標籤**：使用者認得的名稱，例如完整的時區名稱或人員的顯示名稱。別名應放在 `Keywords` 中，而不要放在標籤裡。

| 不要寫 | 改為 |
|---|---|
| 選擇 | 搜尋成員 |
| 輸入一個值 | 搜尋或選取 |
| 找不到 | 沒有符合的時區。 |
| 負責人： | 帳號負責人 |
| 點選以選擇時區 | 選擇預設時區 |

### 最佳做法

**建議**

- 使用 `Keywords` 放置使用者實際會輸入的詞，例如電子郵件別名、城市、舊產品名稱或地區，使比對無須標籤中包含這些詞。
- 繫結值（`SelectedValue`）而不是索引，使重新載入清單時選取內容不會改變。
- 依使用者所讀的標籤排序清單；只有當清單是有序目錄時，才依前綴篩選。
- 當選擇會改變總額或排程時，在摘要中顯示所選項目，使其效果無須重新開啟清單即可看到。

**避免**

- 對三到四個選項使用組合方塊。搜尋欄會增加一個步驟，而 `Select` 清單則能直接顯示所有選項。
- 隱藏選項被停用的原因。無法選取「MAUI」的使用者需要知道原因。
- 在使用者於清單中輸入時替換選項。清單會在查詢下重建，因此反白可能移動到使用者並未選擇的選項上。
- 在 `FilterPredicate` 中執行開銷較大的工作，例如伺服器要求。它會在每次按鍵時執行。請在檢視模型中使用防彈跳的查詢從伺服器載入清單。
- 依賴預留位置文字說明哪個欄位是必填的。應在欄位上方放置可見的標籤。

### 常見問題

**為什麼欄位顯示的是標籤而不是值？** `SelectedValue` 是儲存的識別碼，而欄位顯示的是使用者閱讀的文字。清單關閉時，欄位顯示 `SelectedText`。值保存在檢視模型中。

**按下 Escape 時，已輸入的查詢會怎樣？** 清單關閉，查詢被捨棄，先前的值保持不變。欄位會再次顯示所選的標籤。

**如何用鍵盤移動反白或選取選項？** 按 `Down` 開啟清單，再用 `Down` 與 `Up` 移動反白，按 `Enter` 選取。反白會從最後一個選項循環到第一個。停用的選項會被跳過。

**能否從程式碼中開啟清單？** 將 `IsOpen` 設為 `True`，或呼叫 `FocusInput()`，後者還會把焦點移入欄位。欄位取得焦點或使用者輸入時，清單會開啟。

**聚焦欄位會開啟清單嗎？** 會。取得焦點會開啟清單，輸入也會開啟它。`Enter` 選取反白的選項並關閉清單，`Tab` 則關閉清單並把焦點移走。

## API 參考

<!-- api: Combobox -->

<!-- api: SelectOption -->

## 無障礙

- `Label` 為欄位及其清單命名。`Description` 是說明文字，在名稱與值之後朗讀。
- 此欄位以組合方塊的形式對外呈現。它的值是所選的標籤，展開狀態與清單保持一致。
- 使用者以方向鍵移動反白時，焦點保留在文字輸入框中，因此輸入與導覽不會互相爭搶。
- 清除按鈕的名稱為「清除選取」。選項下方的狀態列是禮貌式即時區域，因此結果數量或空白訊息的變化會被播報，而焦點不會移動。
- 游標、選取、輸入法組字與剪貼簿快速鍵都是原生 `TextBox` 的行為。
- 焦點框繪製在欄位外側 3 px 處。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 輸入文字 | 依標籤、中繼資訊與關鍵字篩選選項；清單關閉時將其開啟 |
| `Down` / `Up` | 清單關閉時開啟清單；否則把反白移到下一個或上一個可用的選項，並循環 |
| `Enter` | 選取反白的選項並關閉清單；沒有反白時無任何作用 |
| `Escape` | 關閉清單並捨棄查詢字詞；所選的值保持不變 |
| `Tab` | 關閉清單，並把焦點移到下一個控制項 |
| 焦點進入欄位 | 開啟清單 |
| 滑鼠點選選項 | 選取該選項並關閉清單 |

## 動態效果

- 清單以彈簧效果從上方展開。其高度隨符合的結果數量變化，因此查詢變動時會增高或縮短。
- 選取的標籤從下方升入欄位，升起 0.35em，並從柔和的模糊中逐漸清晰。
- 有選取項目時清除按鈕會彈出；按下時輕微縮小。
- 沒有符合的項目時，選項下方的狀態列會上移 4 px 並淡入。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 清單開啟 | 從上方以彈簧效果彈入 | 淡入 | 立即完成 |
| 結果數量變化 | 清單高度以平滑彈簧跟隨變化 | 高度立即變化 | 立即完成 |
| 選取的標籤出現 | 從下方升起 0.35em，從柔和模糊中清晰顯現 | 淡入 | 立即完成 |
| 清除按鈕出現 | 彈出 | 淡入 | 立即完成 |
| 空白狀態列出現 | 上移 4 px 並淡入 | 淡入 | 立即完成 |

## 相關元件

- 簡短固定、無須輸入的清單用 `Select`。
- 需要選取多個值時用 `MultiSelect`，經常切換的面向用 `ChipGroup`。
- 搜尋的目的是瀏覽內容而非設定值時用 `ExpandingSearch`。
- 不是從清單中選取的自由文字用 `Input`。

| 元件 | 適用情境 |
|---|---|
| `Select` | 清單簡短且固定，使用者無須輸入即可選取 |
| `MultiSelect` | 需要選取多個值，且每個值都以標記的形式保持可見 |
| `ChipGroup` | 少數面向經常切換，且每個選項都應保持可見 |
| `ExpandingSearch` | 搜尋用於開啟結果以供閱讀或導覽，而不是設定欄位 |
| `Input` | 值是自由文字，而不是某個項目 |
| `TagInput` | 使用者建立自己的標籤，而不是選取已存在的選項 |
