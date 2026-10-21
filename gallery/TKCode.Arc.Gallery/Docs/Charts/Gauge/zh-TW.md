---
title: 儀表板
summary: 在已知範圍內顯示一個數值。
source-hash: 9e6018797779
---

<Demo name="Thresholds" />

## 何時使用

`Gauge` 只回答一個問題：這個讀數位於讀者已知範圍中的什麼位置？它繪製一個 270 度的圓環，圓環中央顯示百分比，可選擇從命名區間顯示狀態名稱，圓環下方是說明文字。它適合放在概覽卡片中，緊鄰它所概括的數字，使讀者一眼就能看到讀數、狀態與刻度。

- **管理主控台中的磁碟用量**：工作區擁有者看到資料庫磁碟區佔用了 500 GB 中的 72%，狀態為「即將佔滿」。明細列「360 / 500 GB」給出絕對數量，圓環則給出比例。
- **風控或防詐儀表板中的健康評分**：在 300 到 850 的刻度上，信用評分為 640。`Minimum` 與 `Maximum` 將真實範圍對應到圓環上，讀者無需把評分換算成百分比。
- **可靠性頁面中的錯誤預算**：SRE 團隊看到當月錯誤預算已用掉 97%。危險色調與標籤使狀態在讀者細看數字之前就一目了然。
- **帳務頁面中的配額或容量卡片**：工作區顯示其席位的使用比例。它適用於單次讀數；若客戶在一段期間內消耗的是有上限的方案，則更適合 `UsageMeter`。

## 何時不用

- **正在執行或即將完成的工作進度**：用 `Progress`，它描述的是步驟與完成情況，而不是對照刻度的讀數。
- **使用者在一段期間內消耗、有上限的方案或配額**：用 `UsageMeter`，它顯示已用量相對於方案及其續訂的情況。
- **隨時間變化的數值**：用 `LineChart` 或 `Sparkline`。圓環只呈現一個時刻，因此用一系列儀表板畫出的趨勢會掩蓋其走向。
- **整體的各個組成部分，而不是對照範圍的單一數值**：用 `DonutChart`，它呈現幾個部分如何共同構成一個總量。
- **帶有相對上一期間變化與趨勢的單一數值**：用 `MetricCard`，它把變化與比較放在數字旁邊。

## 用法

`Value` 為讀數。`Minimum` 與 `Maximum` 設定範圍，預設為 0 到 100。`Detail` 會在讀數下方加上一列說明，例如以絕對單位表示的數量。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Gauge Label="磁碟用量"
               Value="72"
               Detail="360 / 500 GB">
        <arc:Gauge.Thresholds>
            <arc:GaugeThreshold From="0" Tone="Success" Label="健康" />
            <arc:GaugeThreshold From="60" Tone="Warning" Label="即將佔滿" />
            <arc:GaugeThreshold From="85" Tone="Danger" Label="嚴重" />
        </arc:Gauge.Thresholds>
    </arc:Gauge>
</UserControl>
```

`Gauge` 衍生自 Avalonia 的 `Control` 並自行繪製，因此它沒有可供重新設定樣式的範本部件，也沒有需要放置的子控制項。它是唯讀顯示：不取得焦點，也不定義任何按鍵。其外觀來自它的佈景主題，尺寸則隨容器變化。

### 結構

1. **圓環**：一條 270 度的軌道，頂部是數值弧線。在 100 × 90 的檢視框中描邊為 9 px，因此會隨圓環一起縮放。圓環從左下角開始，順時針延伸，半徑位於檢視框中心附近。
2. **表盤**：圓環所在方框最寬 176 px（`DialMaxWidth`），高度為寬度的 0.9 倍。容器較窄時它會縮小；儀表板永遠不會超過 176 px。
3. **數字**：百分比，使用展示字型、3XL 字級，`%` 的字級為其一半。其值為 `Percent`，即將讀數四捨五入為範圍的整百分比。
4. **狀態列**（`Thresholds`）：數值所在區間的名稱，使用超小字級的中等字重文字，顏色為該區間的色調，位於圓環內、數字下方，相隔 4 px。沒有門檻時隱藏。其方框至少寬 100 px，使文字不會擠動數字。
5. **標籤**（`Label`）：圓環下方的說明文字，使用小字級的中等字重，位於表盤下方 10 px（`CaptionGap`）處。它同時也是無障礙名稱。
6. **明細**（`Detail`）：位於標籤下方的超小字級一般文字，使用淡化顏色。為空時隱藏。

圓環顏色隨色調而定：`Accent` 使用強調色，狀態文字使用強調色的加強色；`Success`、`Warning` 與 `Danger` 使用各自的權杖。當數值位於某個區間時，該區間的色調會取代 `Tone` 作為圓環顏色。

### 選擇門檻

門檻從 `From` 開始，一直持續到下一個區間開始。數值所達到的最高區間決定顏色並命名狀態。區間在變更時會依 `From` 排序，因此標記中的順序無關緊要，但依升冪書寫可使標記更易讀。

選擇能說明讀者應該做什麼的區間，而不是裝飾性的刻度。通常三個區間就足夠：一個正常狀態、一個警告狀態，以及一個需要採取行動的狀態。對衡量同一指標的每張卡片都使用相同的 `From` 值與標籤，這樣某個頁面上的「嚴重」磁碟與另一個頁面上的「嚴重」磁碟含義相同。標籤保持一到兩個詞，因為狀態列位於圓環內、數字下方。

```csharp
disk.Thresholds.Add(new GaugeThreshold(0, GaugeTone.Success, "健康"));
disk.Thresholds.Add(new GaugeThreshold(60, GaugeTone.Warning, "即將佔滿"));
disk.Thresholds.Add(new GaugeThreshold(85, GaugeTone.Danger, "嚴重"));
```

區間使用儀表板的數值單位，而不是範圍的百分比。對於 300 到 850 的評分，`From="670"` 的區間指的是評分 670，而不是 670%。沒有門檻時，圓環只使用 `Tone`，讀者得不到狀態名稱，因此只要狀態很重要，就加上區間。

### 繫結到檢視模型

儀表板顯示數值，但不會把數值寫回。`Value`、`Minimum`、`Maximum`、`Label` 與 `Detail` 為單向繫結，因此由檢視模型掌握讀數，儀表板反映每一次變化。使用 CommunityToolkit.Mvvm 時：

```csharp
public partial class StorageViewModel : ObservableObject
{
    [ObservableProperty]
    private double _usedGigabytes;

    [ObservableProperty]
    private double _capacityGigabytes = 500;

    [ObservableProperty]
    private double _usedPercent;

    partial void OnUsedGigabytesChanged(double value)
    {
        UsedPercent = Math.Round(value / CapacityGigabytes * 100);
        DetailText = $"{value:0} / {CapacityGigabytes:0} GB";
    }

    [ObservableProperty]
    private string _detailText = string.Empty;
}
```

```xml
<arc:Gauge Label="磁碟用量"
           Value="{Binding UsedPercent}"
           Detail="{Binding DetailText}">
    <arc:Gauge.Thresholds>
        <arc:GaugeThreshold From="0" Tone="Success" Label="健康" />
        <arc:GaugeThreshold From="60" Tone="Warning" Label="即將佔滿" />
        <arc:GaugeThreshold From="85" Tone="Danger" Label="嚴重" />
    </arc:Gauge.Thresholds>
</arc:Gauge>
```

- 在檢視模型中同時更新 `Value` 與 `Detail`，使畫面上的數字與其明細永遠不會不一致。上面的頁面由同一次變更推導出兩者。
- 區間是一個清單，而不是繫結目標。在標記中或啟動時透過程式碼一次設定，只有在政策變更時才修改 `From` 值。
- 超出 `Minimum` 與 `Maximum` 範圍的值會在畫面上被限制在範圍內。請在資料層驗證讀數，因為儀表板會顯示被限制後的值而不報錯。

### 自訂範圍

`Minimum` 與 `Maximum` 可將任意範圍對應到圓環上。數字依該範圍的百分比顯示，因此在 300 到 850 的刻度上，評分 575 顯示為 50%。當讀者以真實單位思考時，使用真實範圍，並在明細列中寫明範圍，使數字有參照。

<Demo name="CustomRange" />

### 沒有門檻時的色調

沒有門檻時，圓環使用 `Tone`：`Accent`、`Success`、`Warning` 或 `Danger`。對於狀態始終屬於同一類的讀數，例如總是好消息的可用率，使用固定色調；當狀態隨數值變化時，則使用門檻。

<Demo name="Tones" />

### 範例：管理主控台中的儲存與健康卡片

Acme Analytics 工作區擁有者開啟管理主控台，在一列中看到三個讀數：分析資料庫的磁碟用量、資料擷取管線的健康評分，以及當月錯誤預算已用掉的比例。每張卡片都有一個標籤說明衡量的內容，以及一列顯示絕對數量的明細。前兩張卡片還會依各自的區間命名狀態。健康評分對應了 300 到 850 的真實範圍，因此圓環顯示該範圍的 62%，明細列則保留評分本身 640。錯誤預算沒有區間：它的色調固定為危險色，因為它已經越過團隊約定要採取行動的界線。

此範例寬 720 px，因此三張卡片在預設圓環尺寸下並排放置。

![三個儀表板分別顯示磁碟用量、健康評分與錯誤預算，每個都帶有狀態名稱](images/StorageHealth.png)

```xml
<StackPanel Width="720" Spacing="20">
    <TextBlock Text="Platform health, Acme Analytics"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <Grid ColumnDefinitions="*,*,*">
        <arc:Gauge Grid.Column="0"
                   Label="Disk usage"
                   Value="72"
                   Detail="360 of 500 GB">
            <arc:Gauge.Thresholds>
                <arc:GaugeThreshold From="0" Tone="Success" Label="Healthy" />
                <arc:GaugeThreshold From="60" Tone="Warning" Label="Filling up" />
                <arc:GaugeThreshold From="85" Tone="Danger" Label="Critical" />
            </arc:Gauge.Thresholds>
        </arc:Gauge>
        <arc:Gauge Grid.Column="1"
                   Label="Ingestion health"
                   Value="640"
                   Minimum="300"
                   Maximum="850"
                   Detail="640 of 850">
            <arc:Gauge.Thresholds>
                <arc:GaugeThreshold From="300" Tone="Danger" Label="Degraded" />
                <arc:GaugeThreshold From="600" Tone="Warning" Label="Watch" />
                <arc:GaugeThreshold From="720" Tone="Success" Label="Healthy" />
            </arc:Gauge.Thresholds>
        </arc:Gauge>
        <arc:Gauge Grid.Column="2"
                   Label="Error budget used"
                   Value="97"
                   Tone="Danger"
                   Detail="97% of the monthly budget spent" />
    </Grid>
</StackPanel>
```

### 文案規範

標籤說明衡量的內容，明細說明數量。讀者應當無需周圍的頁面也能讀懂卡片：「磁碟用量」配上「360 / 500 GB」就說明了它是什麼，而「儲存」則完全不能說明狀態。

| 不要寫 | 改為 |
|---|---|
| 儲存 | 磁碟用量 |
| 360/500 | 360 / 500 GB |
| 評分：640 | 健康評分 |
| 快要沒有空間了 | 即將佔滿 |
| 很糟糕，立即處理 | 嚴重 |

區間標籤是一到兩個詞的狀態名稱，採用句首大寫，例如「健康」「即將佔滿」「觀察」與「嚴重」。區間標籤中不要使用驚嘆號或完整句子，因為標籤會與數字一同播報，並與數字共享一行短文字。建議的動作應放在周圍的頁面中，而不是放在儀表板裡。

### 最佳做法

**建議**

- 將 `Minimum` 與 `Maximum` 設為衡量指標的真實範圍，並在 `Detail` 中顯示絕對數量，使讀者能夠判斷百分比。
- 只要狀態對讀者重要，就用 `Thresholds` 命名狀態。狀態名稱與數字一同被讀出，因此顏色從來不是唯一的提示。
- 對同一衡量指標，在每個顯示它的頁面上都使用相同的區間與標籤。
- 從同一資料來源一次更新 `Value` 與 `Detail`，使數字與其說明永遠不會不一致。
- 將儀表板放在等寬欄位的一列中，使讀數便於一眼比較。

**避免**

- 為隨時間變化、或需要讀者以趨勢觀察的數值使用儀表板。應改用 `LineChart` 或 `Sparkline`。
- 將儀表板作為讀者必須據此行動的數字的唯一呈現。儀表板是概括；精確數值應保留在明細或頁面的表格中。
- 區間過多且標籤幾乎相同。讀者很難一眼分辨「觀察」與「關注」，因此最多使用三到四個區間。
- 對人們習慣視為百分比的讀數使用自訂範圍。這樣圓環顯示的是讀者不會使用的數字。
- `From` 值單位錯誤的門檻，例如在 300 到 850 的刻度上使用百分比。區間將永遠無法對應讀數。

### 常見問題

**為什麼狀態名稱在數值變化片刻之後才改變？** 圓環與數字沿彈簧運動，而區間由畫面上的數字決定。數字跨越區間邊界時狀態才切換，因此名稱與數字始終一致。在減少或關閉動態效果時，變化是立即的。

**儀表板對超出範圍的值如何顯示？** 圓環與數字都會將數值限制在 `Minimum` 與 `Maximum` 之間。`Percent` 屬性同樣受限制，因此在 0 到 100 的儀表板上，值 1000 顯示為 100%。

**能在程式碼中讀取狀態嗎？** 可以。`CurrentThreshold` 傳回數值所屬的區間；若沒有區間或數值低於第一個區間，則傳回 null。`Percent` 給出圓環所顯示的整百分比。

**儀表板支援鍵盤存取嗎？** 儀表板不取得焦點，也不定義任何按鍵，因此它作為頁面的一部分被讀取。它的值以唯讀進度條的形式公開，並帶有範圍；其說明文字為帶狀態的百分比，例如「72%，即將佔滿」。

**只設定 `Tone` 時為什麼沒有狀態？** `Tone` 只設定圓環顏色。狀態列與狀態名稱來自 `Thresholds`，因此至少要加入一個區間才能顯示名稱。

## API 參考

<!-- api: Gauge -->

<!-- api: GaugeThreshold -->

### 配套型別：GaugeTone

| 成員 | 圓環與狀態顏色 |
|---|---|
| `Accent` | 強調色；狀態文字使用加強的強調色（預設值） |
| `Success` | 成功色 |
| `Warning` | 警告色 |
| `Danger` | 危險色 |

## 無障礙

- `Label` 為儀表板命名，數值以唯讀範圍的形式公開，並包含最小值與最大值。
- 說明文字為數值加目前區間標籤，例如「72%，即將佔滿」，因此狀態與數字一同被讀出。沒有門檻時，僅為百分比。
- 儀表板是禮貌等級的即時區域：新的讀數會被播報，焦點不會移動。
- 狀態由門檻標籤以文字說明。顏色從來不是狀態的唯一標誌。
- 圓環與帶動畫的讀數對輔助技術隱藏；螢幕閱讀器取得的是名稱、範圍與說明文字。

### 鍵盤

儀表板不取得焦點，也不定義任何按鍵。請依頁面正常的閱讀順序閱讀它，或使用螢幕閱讀器的審閱命令。

## 動態效果

- 儀表板進入畫面一半時，圓環沿無過衝的平滑彈簧一次填滿到其數值。
- 新數值會將圓環移動到新的長度，同時百分比數字同步計數到新的數字。
- 數字跨越門檻時，顏色與區間標籤在該時刻切換，隨計數變化。
- 減少動態效果時，圓環與數字立即到達目標值。關閉動態效果與減少動態效果的表現相同。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 首次進入畫面（進入一半時） | 圓環沿平滑彈簧一次填滿到數值，無過衝 | 圓環與數字立即到達數值 | 圓環與數字立即到達數值 |
| 新 `Value` | 圓環彈簧式移動到新長度，數字計數 | 立即變化 | 立即變化 |
| 數字跨越區間 | 顏色與狀態名稱隨計數切換 | 立即切換 | 立即切換 |
| `Tone` 變化 | 圓環顏色交叉淡化 | 立即變化 | 立即變化 |

## 相關元件

- `Progress`：工作的完成進度。
- `UsageMeter`：使用者在一個帳期內消耗的配額。
- `DonutChart`：整體的各個組成部分，而不是對照範圍的單一數值。
- `MetricCard`：帶有變化與趨勢的單一數值。

| 元件 | 適用情境 |
|---|---|
| `Progress` | 工作正在執行或即將完成，讀者在等待 |
| `UsageMeter` | 方案或配額在一個帳期內被消耗 |
| `DonutChart` | 多個部分組成一個總量 |
| `Sparkline` / `LineChart` | 數值隨時間的變化才是重點 |
| `MetricCard` | 一個數字需要在旁邊顯示其變化與趨勢 |
