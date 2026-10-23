---
title: 文字閃光
summary: 用平緩的光帶掃過文字，表示正在進行的工作。
source-hash: 23c614235a9b
---

<Demo name="StatusLine" />

## 何時使用

`TextShimmer` 是一列狀態文字，工作進行期間，一道柔和的光帶會從上面掃過。它用於應用程式需要回報某件事仍在進行、卻沒有可量測數量可顯示的位置：狀態列、頁面標題、活動列或助理的回覆。光帶表示「仍在工作」，無需旋轉指示器，文字則說明工作的內容。

- **仍在工作的 AI 助理**：回覆上方顯示「思考中」，長篇回答撰寫期間顯示「正在產生摘要」。只有文字在移動，因此回覆區域仍然易讀。
- **管理後台中的背景匯入或同步**：夜間工作執行時，面板顯示「正在匯入 2,340 筆訂單」。工作結束後，同一控制項切換為「匯入完成」，文字在原位穩定下來。
- **只知道步驟名稱的多步驟工作**：工作區匯出依序經過「正在讀取 14 個檔案」、「正在起草計畫」和「正在撰寫摘要」。每一步替換文字，掃光持續進行。
- **建置期間的開發者工具標題**：部署檢視的頂列顯示「正在編譯 212 個模組」，建置通過後掃光停止。
- **結束後保留在畫面上的標籤**：以實色文字結束的狀態列，例如「摘要已就緒」，使結果看起來已經塵埃落定，而不是載入狀態。

## 何時不用

- **尚未送達內容的佔位**：使用 `Skeleton`，它顯示內容的形狀。文字上的掃光傳達的是工作資訊，而不是版面。
- **已知完成量的工作**：使用 `Progress`，它顯示工作進行到哪裡。掃光無法顯示百分比或剩餘時間。
- **不掃光、逐字母變化的標籤**：使用 `TextMorph`，它把字元變形為下一個標籤。
- **段落或多行文字**：使用一般的 `TextBlock`。本控制項為一行短文字而設計，不會換行。
- **需要使用者處理的問題**：匯入失敗、工作階段過期或付款被拒絕。請使用 `Alert` 顯示訊息與下一步。失敗的工作上仍有掃光，等於告訴使用者工作仍在進行，這與事實不符。
- **從不變化且沒有工作在進行的文字**：頁面標題或欄位標籤。掃光只會增加雜訊。

## 用法

`TextShimmer` 衍生自 Avalonia 的 `Control`，而不是 `TextBlock`，並自行繪製文字。它的 `FontFamily`、`FontSize`、`FontWeight` 和 `Foreground` 屬性預設繼承自 `TextElement` 的值，因此無需額外標記即可採用周圍樣式的排版。由於它是一個直接繪製的控制項，忙碌的標籤在頁面上只是一個元素，而不是每個字母一個元素。請讓畫面上有動畫的標籤保持較少：一兩列忙碌文字易於閱讀，六列並列就會互相搶奪注意力。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:TextShimmer Text="思考中" />
</UserControl>
```

只要 `IsActive` 為 `true`（預設值），掃光就會運行。因此新建的控制項一顯示就開始掃光。工作結束時將 `IsActive` 設為 `false`，文字即沉澱為實色。

### 結構

1. **基礎文字**（以 `BaseForeground` 繪製）：該列靜止時的顏色。未設定 `BaseForeground` 時，基礎色為 `Foreground` 顏色的 12%，與弱化文字顏色在 OKLab 色彩空間中混合。主題將弱化顏色設為 TextMuted 權杖，因此靜止的標籤與周圍文字保持同一套色盤。
2. **光帶**：一條鐘形光帶，使用 `Foreground` 顏色，寬 4.4 em（中心兩側各 2.2 em）。中心最強，兩端漸隱為零。在內部，它是文字經由移動遮罩再次繪製的結果，因此光帶會精確地跟隨字母。
3. **實色文字**：全強度的 `Foreground` 顏色。當 `IsActive` 為 `false` 且光帶已離開該列後，它取代基礎文字。
4. **列**：一列文字，字級為 `FontSize`（預設 14 px，來自 TypeSM 權杖），字型為 `BodyFontFamily`，字重為一般。控制項量測文字的寬度與一列的高度，且從不換行。

### 選擇狀態

使用 `IsActive` 表示工作是否仍在進行，而不是用來裝飾頁面。工作已經完成的標籤上仍有掃光，會讓人誤以為需要等待。

- **忙碌**：`IsActive="True"`，即預設值。光帶運行，無障礙的項目狀態為「Busy」。
- **完成**：`IsActive="False"`。光帶滑出，文字變為實色。讓控制項留在原處，使該列在原來的位置穩定下來。
- **首次請求前的閒置狀態**：以 `IsActive="False"` 建立控制項。此時它顯示實色文字，並且在你將 `IsActive` 設為 `true` 之前不運行迴圈。

```xml
<arc:TextShimmer Text="準備匯入" IsActive="False" />
```

### 更改文字

新的 `Text` 值會在原地升起，同時該列以彈簧方式調整到新寬度。步驟變化期間保持 `IsActive` 開啟，光帶就能跨越變化持續運行。

<Demo name="ChangingAndFinishing" />

每個步驟都應寫成簡短的現在分詞加上其對象，並讓每一步在畫面上停留得夠久，以便閱讀。文字變化得比人能讀的速度還快，就會變成一團升起的模糊文字。

### 完成

將 `IsActive` 設為 `false`，而不是移除控制項。光帶滑出，文字變為實色，因此該列不會跳動，周圍的版面也不會位移。

```csharp
summary.Text = "摘要已就緒";
summary.IsActive = false;
```

在同一步中設定最終文字與 `IsActive`，使完成的標籤與其穩定狀態同時出現。

### 掃光速度

`SweepDuration` 設定光帶掃過文字一次所需的時間。預設值為 1.8 秒。每次掃過之後會有一段短暫的停頓，約為該時長的三分之一，然後才開始下一次。頁面平靜、標籤是長時間運行的背景工作（例如為工作區建立索引）時，使用較慢的掃光。助理回覆或按鈕狀態則保留預設值，因為人們期待的是輕快的訊號。

<Demo name="SlowerSweep" />

`SweepDuration` 接受 `TimeSpan`。在程式碼中，`TimeSpan.FromSeconds(3)` 與上面 XAML 中的值相同。

### 尺寸與顏色

文字使用 `TextElement` 的字型屬性。較大的字級適合狀態標題；光帶會隨字級縮放，因此掃光在任何字級下看起來都一樣。

```xml
<arc:TextShimmer Text="正在產生摘要" FontSize="22" FontWeight="SemiBold" />
```

`Foreground` 設定光帶與實色文字的顏色。`BaseForeground` 明確設定靜止時的顏色。當預設的混合無法維持你需要的對比度時設定它，例如在深色標題上，光帶掃過時基礎色仍須保持可讀。

```xml
<arc:TextShimmer Text="正在同步訂單"
                 Foreground="{arc:ArcDesignTokenResource Accent}"
                 BaseForeground="{arc:ArcDesignTokenResource TextMuted}" />
```

### 由程式碼驅動

該控制項只負責顯示，因此檢視模型掌握文字與忙碌旗標，檢視負責繫結。掃光與文字變化由控制項處理。

```csharp
public partial class ImportViewModel : ObservableObject
{
    private readonly IOrderImportService _imports;

    [ObservableProperty]
    private string _statusText = "正在匯入 2,340 筆訂單";

    [ObservableProperty]
    private bool _isBusy = true;

    public ImportViewModel(IOrderImportService imports)
    {
        _imports = imports;
    }

    [RelayCommand]
    private async Task RunImportAsync()
    {
        IsBusy = true;
        StatusText = "正在匯入 2,340 筆訂單";

        var result = await _imports.RunAsync();

        StatusText = result.IsSuccess ? "匯入完成" : "匯入需要處理";
        IsBusy = false;
    }
}
```

```xml
<arc:TextShimmer Text="{Binding StatusText}"
                 IsActive="{Binding IsBusy}"
                 FontSize="16" />
```

匯入失敗時，應在該列旁邊用 `Alert` 顯示原因，而不是只更改掃光的文字。掃光在 `IsBusy` 設為 `false` 時結束，因此它絕不會聲稱工作仍在進行。

### 節制地使用動態效果

動態效果在它標示出頁面上唯一仍在運行的元素時最有幫助。當多列同時掃光、掃光作用於已完成的標籤，或文字本身只是動態效果的裝飾時，動態效果就會分散注意力。

- **適合的情況**：標題或面板中的狀態列，其他元素都不移動，光帶即可告訴人們工作仍在運行，無需旋轉指示器。
- **會分散注意力的情況**：一列忙碌的列全都在掃光，或在人們等待時閱讀的標籤上使用比預設值更快的掃光。
- **減少動態效果**：光帶停止，標籤以一般淡入切換。僅靠文字也必須能傳達狀態，因此要寫「正在讀取 14 個檔案」，而不是只依靠掃光；工作結束時停止掃光。

若要降低介面某部分的動態效果，請在容器上設定 `arc:ArcMotion.Level="Reduced"`。該級別會被繼承，因此其下的每個掃光都會停止。

### 範例：夜間匯入面板

管理後台中顯示夜間訂單匯入的面板。忙碌列說明工作正在做什麼以及涵蓋多少訂單，人們可以僅憑文字判斷還需等待多久。其下是較早一次執行的完成列，它是同一個控制項，但 `IsActive` 設為 `false`，因此兩種狀態讀起來像同一個元件。操作列中，檢視日誌使用次要按鈕，下一步使用主要按鈕。

![夜間匯入面板，一列忙碌狀態與一列已完成狀態](images/ImportPanel.png)

```xml
<Border Width="640"
        Padding="24"
        CornerRadius="{arc:ArcDesignTokenResource RadiusPanel}"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource BorderSubtle}"
        BorderThickness="1">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Nightly order import"
                       FontSize="{arc:ArcDesignTokenResource TypeLG}"
                       FontWeight="SemiBold"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <TextBlock Text="Northwind Commerce, store 014"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        </StackPanel>
        <arc:TextShimmer Text="Importing 2,340 orders" FontSize="16" />
        <StackPanel Spacing="8">
            <TextBlock Text="Last run"
                       Foreground="{arc:ArcDesignTokenResource TextMuted}" />
            <arc:TextShimmer Text="Import complete on 20 October 2026" FontSize="16"
                             IsActive="False" />
        </StackPanel>
        <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
            <arc:Button Content="View log" Variant="Secondary" />
            <arc:Button Content="Open orders" />
        </StackPanel>
    </StackPanel>
</Border>
```

### 文案規範

文字承載含義，因此應把它寫成工作的狀態，而不是情緒。

- **忙碌標籤**：現在分詞加上其對象或數量：「正在讀取 14 個檔案」、「正在產生摘要」。數量使用阿拉伯數字，讓人一眼讀懂：寫「2,340 筆訂單」，而不是「兩千三百四十筆訂單」。
- **完成標籤**：結果用簡短的名詞片語或過去分詞表示：「摘要已就緒」、「匯入完成」。
- **長度**：讓列文字保持在約 40 個字元以內，使其能放進狀態列，或放進窄視窗的標題中。
- **避免含糊的等待文案**：說明工作的內容，而不是等待本身。「請稍候」無法告訴人們正在發生什麼。

| 不要寫 | 改為 |
|---|---|
| 載入中... | 正在讀取 14 個檔案 |
| 請稍候 | 正在同步訂單 |
| 處理中 | 正在匯入 2,340 筆訂單 |
| 完成！ | 摘要已就緒 |
| 正在思考你的請求 | 思考中 |

### 最佳實務

**建議**

- 把含義放在文字中。光帶只表示工作仍在進行；減少動態效果與輔助技術會去掉它，因此標籤仍須說明工作的內容。
- 工作結束時將 `IsActive` 設為 `false`，而不是移除控制項。文字會在原位穩定下來，周圍的版面也不會位移。
- 每個區域保持一個忙碌標籤，每個標籤占一列。本控制項為簡短的狀態列而設計，第二列掃光會與第一列競爭。
- 工作失敗時用 `Alert` 顯示失敗原因並停止掃光。掃光意味著工作仍在進行。

**避免**

- 把掃光當作尚未載入內容的佔位。應使用 `Skeleton`，它顯示內容的形狀，使人們看到即將出現的內容。
- 工作完成後仍保持 `IsActive` 開啟，或用在沒有任何需要等待的標籤上。此時掃光與頁面內容互相矛盾。
- 以比人能閱讀的速度更快的計時器更改文字。每次變化都會帶著模糊升起，連續的變化讀起來像雜訊而不是進度。
- 設定與背景接近的 `BaseForeground`。當光帶位於該列的其他位置時，靜止的基礎色仍須保持可讀。
- 把掃光用在同時也是唯一錯誤狀態訊息的標籤上。錯誤應使用 `Alert`，使訊息不依賴動態效果。

### 常見問題

**標籤捲出視野或其視窗隱藏時，掃光為什麼會停止？** 控制項僅在可見、已掛入樹狀結構、距檢視區 64 px 以內且處於 Full 動態效果級別時運行其迴圈。它不會為沒有人能看到的標籤耗用影格，並會從停止的位置繼續。

**如何在不隱藏文字的情況下停止掃光？** 將 `IsActive` 設為 `false`。文字會變為實色並保持可見，這正是完成狀態應有的樣子。若因使用者要求減少動態效果而需要為整個區域停止掃光，請在容器上設定 `arc:ArcMotion.Level="Reduced"`；掃光停止，標籤以淡入方式切換。

**欄寬較窄時文字會換行嗎？** 不會。本控制項只有一列。請縮短標籤，或為控制項留出更多空間；不要依賴換行。

**如何讓掃光更慢或更改其顏色？** 用 `SweepDuration` 設定速度，用 `Foreground` 設定光帶與實色文字的顏色，用 `BaseForeground` 設定靜止時的顏色。`FontSize` 與 `FontWeight` 改變文字本身。

**控制項會向螢幕閱讀器播報其變化嗎？** 不會。它的文字即為無障礙名稱，掃光時項目狀態為「Busy」。若要播報完成狀態，請在父元素上放置即時區域，或在將 `IsActive` 設為 `false` 的同一步中播報訊息。

## API 參考

<!-- api: TextShimmer -->

## 無障礙

- `Text` 的值即為控制項的無障礙名稱，控制項以文字元素的身分回報自身。
- `IsActive` 為 `true` 時，項目狀態為「Busy」。`IsActive` 為 `false` 時清除。並非所有輔助技術都會公開項目狀態，因此文字本身必須能獨自傳達含義。
- 掃光僅為裝飾。不會逐字母朗讀，光帶也不是獨立的元素。
- 控制項不會播報變化。請依上文常見問題所述，在父元素上放置即時區域以播報完成狀態。
- 控制項不會切換為系統色彩以支援高對比。請從主題中選擇 `Foreground` 與 `BaseForeground`，使光帶和靜止的基礎色都保持頁面所需的對比度。
- 控制項無法取得焦點，也沒有鍵盤互動，因此沒有鍵盤表格。其中沒有任何元素可以透過 `Tab` 到達。

## 動態效果

- 光帶每個 `SweepDuration` 掃過文字一次，隨後短暫停留，再開始下一次掃過。第一次掃過會在控制項開始後稍作等待。
- 掃光僅在控制項處於啟用、可見、距檢視區 64 px 以內且處於 Full 動態效果級別時運行，並從停止的位置繼續。
- 新標籤從下方帶著柔和的模糊升起，同時該列以彈簧方式調整到新寬度。被替換的標籤以較短的模糊向上離開。
- 將 `IsActive` 設為 `false` 時，飛行中的光帶滑出文字，隨後文字變為實色。將其設回 `true` 時，該列回到靜止的基礎色。
- 減少動態效果時停止掃光，標籤以即時淡入切換。關閉動態效果時立即切換標籤，並不加動畫地確定狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 掃光光帶 | 每個 `SweepDuration` 掃過文字一次，然後停留 | 沒有光帶；文字顯示其靜止基礎色 | 沒有光帶；文字顯示其靜止基礎色 |
| 新的 `Text` 值 | 帶著柔和模糊在 240 ms 內升起；舊文字在 160 ms 內離開 | 120 ms 內即時淡入 | 立即 |
| 文字變化後的寬度 | 在標籤變化的同時以彈簧調整到新寬度 | 立即跳到新寬度 | 立即 |
| `IsActive` 設為 `true` | 文字在 240 ms 內回到靜止的基礎色 | 120 ms 內即時淡入 | 立即 |
| `IsActive` 設為 `false` | 光帶最多在 420 ms 內滑出，隨後文字在 360 ms 內變為實色 | 120 ms 內實色文字 | 立即 |

## 相關元件

- `Skeleton`：頁面載入時的佔位內容；`Progress`：工作量可以量測時使用。
- `TextMorph`：字母逐個變化的簡短標籤；`Alert`：需要處理的訊息。

| 元件 | 適用情境 |
|---|---|
| `Skeleton` | 內容正在載入，頁面需要與最終版面相符的佔位形狀 |
| `Progress` | 已完成的工作量已知，可以以進度條或數值顯示 |
| `Stepper` | 工作有固定、有名稱的步驟，人們需要看到自己所處的位置 |
| `TextMorph` | 簡短標籤改變狀態，字母應變形為下一個標籤 |
| `Alert` | 失敗、工作階段過期或某個結果需要人們注意並給出下一步 |
| `TextReveal` | 標題或短段落首次進入頁面 |
