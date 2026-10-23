---
title: 斜率圖
summary: 兩個座標軸上的前後比較：線條逐條繪出，每個數值旁顯示排名變化，切換資料集時每條線都會滑動到新的斜率。
source-hash: 9f597ae7dfcc
---

<Demo name="BeforeAndAfter" />

## 何時使用

`SlopeChart` 比較一小組項目在兩個時間點的情況。每一項是一條線，從第一個時期的數值連到第二個時期的數值，所有數值共用同一條數值軸，因此讀者一眼就能看出誰上升了、誰下降了，以及排序如何變化。它適合問題為「這兩個日期之間發生了什麼變化」的檢討頁面，例如季度業務回顧或同期群比較。

- **成長檢討中按獲客通路看的轉換率，Q1 對比 Q2**：從第四升到第二的自然搜尋無需表格即可看出，每個數值旁的排名變化也用文字說明了這一點。
- **SaaS 董事會資料中兩個同期群按方案看的留存率**：六個方案放在兩個座標軸上，可以看出哪些方案有所提升，哪些有所下滑。
- **銷售檢討中按地區看的今年與去年營收**：斜率呈現成長，交叉的線條顯示哪些地區超過了其他地區。
- **營運報告中流程調整前後各支援團隊的滿意度評分**：圖表能顯示這次調整惠及了所有團隊，還是只惠及部分團隊。
- **競爭分析中幾家供應商兩個季度的市場占有率**：排序與規模同樣重要。

## 何時不用

- **三個或更多時間點**，例如一年中的月度數值：用 `LineChart`，它保留時間軸。
- **只需要每項的最新數值，沒有比較**：用 `BarChart`。
- **整體中的各部分，而非兩個時間點之間的變化**：用 `DonutChart`。
- **超過約十項**：標籤會在兩個座標軸上互相擠壓。較長的清單請用 `BarChart`，並依第二個數值排序。
- **每組有許多觀測值的分布**，例如各地區的回應時間：用 `Ridgeline`。

## 用法

每個 `SlopeItem` 是一條線，包含 `Key`、`Label`、`Start` 數值與 `End` 數值。`StartLabel` 與 `EndLabel` 用於命名兩個時間點，預設值為 "Before" 與 "After"。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SlopeChart Label="各通路轉換率"
                    StartLabel="Q1"
                    EndLabel="Q2"
                    HighlightKey="search"
                    ItemsSource="{Binding Channels}" />
</UserControl>
```

`SlopeChart` 衍生自 Avalonia 的 `Control`，自行繪製，因此線條、標籤與排名變化是同一幅繪圖，資料變化時會平順地移動。整個控制項區域都能接收指標，圖表作為一個整體可以取得焦點。

### 結構

1. **欄標題**（`StartLabel`、`EndLabel`）：每個數值軸上方各有一個標題，並以該軸為中心對齊。
2. **數值軸**：兩條 1 px 的直線，顏色為邊框色。兩欄共用同一個數值刻度，因此線條越陡，變化越大。
3. **線條**：2 px、端點為圓形的中性色線條。由 `HighlightKey` 指定的項目使用第一個系列顏色；當某一項目取得焦點時，其餘項目淡出。
4. **圓點**：兩端各有一個 4 px 的圓點，描邊使用表面色，使其在線條上清楚可辨。
5. **起點標籤**：名稱在前、數值在後，右對齊，位於起點軸左側 10 px。
6. **終點標籤**：數值位於終點軸右側 10 px；開啟 `IsRankVisible` 時，其後還有排名變化：帶名次數的箭頭，排名沒有變化時顯示 "="。
7. **引導線**：從每個標籤到其圓點的細線。為避免碰撞而被推開的標籤，仍透過這些引導線與圓點相連。
8. **提示**：名稱、兩個數值、變化量以及排名文字，例如 "上升 1 名至第 1 名"。提示至少 168 px 寬。

未設定 `PlotHeight` 時，繪圖高度為每項 44 px，且至少 200 px。寬度小於 440 px 時，標籤欄會變窄，名稱也會被縮短；完整名稱保留在提示與資料表中。

### 資料集

替換 `ItemsSource` 即可切換資料集。每條線、每個圓點與每個標籤都會滑動到新的位置。在同一步驟中變更 `Label`，使文字與資料保持一致。

```csharp
conversion.Click += (_, _) =>
{
    chart.Label = "各通路轉換率";
    chart.ItemsSource = Conversion;
};
```

只要 `Key` 相同，項目在資料集之間切換時會保留各自的線條，因此線條是變形過渡，而不是被替換。請使用能識別實體的鍵，例如通路 ID，而不是會隨清單重新排序而變動的列號。

### 數值與變化的格式

`ValueFormatter` 格式化兩端的數值。`ChangeFormatter` 格式化排名旁的變化量，它接收變化量與對應的項目。

```csharp
chart.ValueFormatter = v => $"{v:0.0}%";
chart.ChangeFormatter = (change, _) => $"{(change >= 0 ? "+" : "−")}{Math.Abs(change):0.0} 個百分點";
```

預設情況下，數值依最多一位小數分組顯示，變化量帶正負號，並使用真正的減號，例如 "+2" 或 "−2"。請在圖表上一次設定格式化器，使每個項目的顯示方式保持一致。

### 高亮與排名

`HighlightKey` 以第一個系列顏色繪製一個項目，其餘項目保持中性色。`IsRankVisible` 用於開啟或關閉排名變化。

```xml
<arc:SlopeChart Label="收盤價"
                StartLabel="2023"
                EndLabel="2024"
                IsRankVisible="False"
                MaxWidth="420"
                ItemsSource="{Binding Values}" />
```

當順序與問題無關，或排名只會重複斜率已經表達的資訊時，請關閉排名。

### 讀取項目

`ActiveKey` 為指標或鍵盤所在的項目，且為雙向繫結。`ActiveKeyChanged` 會回報每一次變更，項目透過 `Item` 提供。

```csharp
chart.ActiveKeyChanged += (_, e) => reading.Text = e.Item is { } item
    ? $"{item.Label}: {item.Start} → {item.End}"
    : "沒有聚焦的項目";
```

### 繫結到檢視模型

使用 CommunityToolkit.Mvvm 時，檢視模型同時持有項目清單與目前聚焦項目的鍵。每一列是一個 record，圖表透過成員繫結讀取其欄位，因此列不必是 `SlopeItem` 物件。

```csharp
public sealed record ChannelRate(string Key, string Name, double Q1, double Q2);

public partial class ChannelsViewModel : ObservableObject
{
    [ObservableProperty]
    private ChannelRate[] _channels = [];

    [ObservableProperty]
    private string? _activeChannel;
}
```

```xml
<arc:SlopeChart Label="各通路轉換率"
                StartLabel="Q1"
                EndLabel="Q2"
                ItemsSource="{Binding Channels}"
                KeyMemberBinding="{Binding Key}"
                LabelMemberBinding="{Binding Name}"
                StartMemberBinding="{Binding Q1}"
                EndMemberBinding="{Binding Q2}"
                ActiveKey="{Binding ActiveChannel, Mode=TwoWay}" />
```

- `ActiveKey` 為雙向繫結：圖表寫入目前聚焦項目的鍵，檢視模型也可以在程式碼中讓某一項目取得焦點。
- 同時設定時，選取器（`KeySelector`、`LabelSelector`、`StartSelector`、`EndSelector`）優先於成員繫結，當某個值需要計算時這很有用。
- 為求簡潔，範例使用一般的 `Binding`。編譯繫結需要在檢視上設定 `x:DataType`。

### 資料結構與標籤衝突

每一項都需要穩定的 `Key`、`Label` 以及兩個數值。兩欄共用一條數值軸，因此線條的斜率直接比較兩個時間點。每一項都必須有兩個數值，因為每個數值都會被繪製在其對應的位置上。

當兩個標籤在同一欄中會重疊時，它們會依標籤間距（20 px）彼此分開，並透過引導線與各自的圓點保持連接。相互接觸的標籤構成一組，並圍繞其成員希望停留的位置排布。下面的示例在第一欄中有四個相近的數值；標籤會分散開，而不是堆疊在一起，因此名稱依然清晰可讀。

<Demo name="LabelCollisions" />

### 範例：成長檢討中的通路轉換

某成長檢討為 Acme Analytics 比較了六個獲客通路在第一季與第二季的轉換率。圖表突出顯示了自然搜尋這個團隊投入過的通路，使讀者的視線有一個明確的焦點。Email 通路在兩個季度中都保持領先，這一點無需標籤即可從斜率中看出。圖說直接說明了結論，因此卡片無需懸停即可讀懂。

![六個通路的斜率圖，突出顯示自然搜尋，且從第四名上升到第二名](images/ChannelConversion.png)

```xml
<Border xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
        Width="680"
        Padding="24"
        CornerRadius="12"
        Background="{arc:ArcDesignTokenResource Surface}"
        BorderBrush="{arc:ArcDesignTokenResource Border}"
        BorderThickness="1">
    <StackPanel Spacing="16">
        <StackPanel Spacing="4">
            <TextBlock Text="Conversion rate by channel" FontSize="16" FontWeight="SemiBold" />
            <TextBlock Text="Percent of sessions that bought, Q1 and Q2 2026. Organic search moved from fourth to second."
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
        </StackPanel>
        <arc:SlopeChart Label="Conversion rate by channel"
                        StartLabel="Q1"
                        EndLabel="Q2"
                        HighlightKey="search">
            <arc:SlopeChart.Items>
                <arc:SlopeItem Key="search" Label="Organic search" Start="3.1" End="4.6" />
                <arc:SlopeItem Key="email" Label="Email" Start="5.8" End="5.2" />
                <arc:SlopeItem Key="paid" Label="Paid social" Start="2.2" End="3.4" />
                <arc:SlopeItem Key="referral" Label="Referral" Start="4.4" End="4.1" />
                <arc:SlopeItem Key="direct" Label="Direct" Start="3.6" End="3.7" />
                <arc:SlopeItem Key="display" Label="Display ads" Start="1.2" End="0.9" />
            </arc:SlopeChart.Items>
        </arc:SlopeChart>
    </StackPanel>
</Border>
```

高亮的線條就是故事的主線，每個數值旁的排名變化說明了哪些通路上升或下降。Email 與 Referral 略有下滑，Display ads 在兩個季度中都是最低值。截圖中的數值為一般數字，因此單位由圖說說明；在應用程式中，請設定 `ValueFormatter` 來加上百分號。

### 文案規範

- **圖表標籤**：指標及其範圍，例如「各通路轉換率」。標籤為輔助技術與摘要命名該圖表。
- **欄標題**：兩個時間點的真實名稱，例如 "Q1" 與 "Q2" 或 "2025" 與 "2026"。只有當兩個時間點是事件（例如一次上線）時，才使用 "Before" 與 "After"。
- **項目標籤**：實體名稱，採用句首大寫，且夠短，能與其數值並排顯示，例如 "Organic search"、"Paid social"。較長的名稱保留在提示與資料表中。
- **變化量**：連同正負號與單位一起書寫，例如 "+1.5 pts"，使讀者無需依賴座標軸。

| 不要寫 | 改為 |
|---|---|
| Before / After | Q1 / Q2 |
| Conversion | Conversion rate by channel, Q1 and Q2 |
| +1.5 | +1.5 pts |
| SEO | Organic search |

### 最佳做法

**建議**

- 兩個時間點使用同一單位。兩個不同度量之間的斜率並不構成比較，而讀者從圖中無法察覺這一點。
- 將清單控制在約三到十項，使每欄的兩組標籤都保持可讀。
- 用 `HighlightKey` 突出故事所講的那一項。其餘線條保持中性色，高亮才有意義。
- 為每個項目設定能識別實體的穩定 `Key`。資料集變更時，同一條線會變形過渡，而不是被替換。

**避免**

- 將兩個以上的時間點繪製為斜率。第三個點會讓線條看起來像一種並不存在的趨勢；這時請用 `LineChart`。
- 同時高亮多個項目。當每條線都使用第一個系列顏色時，該顏色就失去了意義。
- 當讀者需要比較幅度時隱藏變化量。這種情況下請保留變化量或排名的顯示。
- 在不同步驟中變更 `Label` 與資料。這樣標題會在一段時間內描述上一個資料集。

### 常見問題

**為什麼欄標題顯示的是 "Before" 和 "After"？** 這是 `StartLabel` 與 `EndLabel` 的預設值。請將它們設為真實的時間點，例如 "Q1" 與 "Q2"。

**為什麼鍵盤和摘要列出的項目順序與我的資料不同？** 鍵盤與摘要依結束數值由高到低排列，而繪圖保持你的資料順序。當兩者的順序需要一致時，請依結束數值排序資料。

**所有數值都相同的資料集會怎樣顯示？** 當所有數值相同時，數值軸兩側各延伸一個單位，因此線條位於繪圖區的中間。

**聚焦的項目從資料中移除後，讀數會怎樣？** 當其項目不再存在於 `ItemsSource` 中時，`ActiveKey` 會被清空為 null，讀數也就不再描述一個已經不存在的項目。

## API 參考

<!-- api: SlopeChart -->

<!-- api: SlopeItem -->

## 無障礙

- `Label` 為圖表命名。圖表是帶有角色描述與按鍵說明的可聚焦群組。
- Tab 鍵將焦點移到圖表上，並先讀取結束數值最高的項目。下鍵與右鍵移到下一項，上鍵與左鍵移到上一項，順序依據結束數值。
- Home 與 End 分別移到第一項與最後一項，Escape 清除讀數。觸控按下並拖曳會讀取最近的線條。
- 禮貌的即時區域會播報兩個數值、變化量與排名變化，例如 "Search, Q1 3, Q2 5, +2, up 1 to rank 1"。隱藏的資料表依結束數值的順序列出各項目。
- 排名變化使用帶數字的箭頭符號，因此不只依靠顏色表示。線條與標籤對輔助技術隱藏。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 聚焦圖表；首先讀取結束數值最高的項目 |
| `Down` / `Right` | 依結束數值順序移到下一項 |
| `Up` / `Left` | 依結束數值順序移到上一項 |
| `Home` / `End` | 移到第一項（結束數值最高）或最後一項 |
| `Escape` | 清除讀數 |

在靜止狀態下，任意方向鍵都會先移到第一項。滑鼠讀取離指標最近的線條，並在離開圖表時清除讀數。

## 動態效果

- 首次顯示時，每條線從起點繪製到終點，並以短暫的錯落時差進行。圓點與標籤隨線條落定而出現。
- 切換資料集時，線條、圓點、標籤與引導線在共用的變形彈簧上移動。排名數字會朝變化的方向滾動。
- 取得焦點的項目移到最前，其餘項目淡出。提示滑動到其所在線條的中點。
- 減少動態效果時，所有線條一次繪製完成，變化時也一次移動。關閉動態效果時直接到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 首次顯示 | 線條從起點繪製到終點，並以短暫的錯落時差進行；圓點與標籤隨線條落定 | 所有線條立即繪製完成 | 最終狀態 |
| 資料集變更 | 線條、圓點、標籤與引導線在共用彈簧上移動；排名數字滾動 | 立即移動 | 最終狀態 |

## 相關元件

- `LineChart`：三個或更多時間點，且時間軸重要時。
- `BarChart`：單一時間點上，每項一個數值。
- `Ridgeline`：各組的分布如何隨時間變化。

| 元件 | 適用情境 |
|---|---|
| `LineChart` | 有三個或更多時間點，且時間軸很重要 |
| `BarChart` | 每項在單一時間點只有一個數值，或清單超過約十項 |
| `Ridgeline` | 每組是許多觀測值的分布，而不是一個數值 |
| `DonutChart` | 資料是整體的各部分，而不是兩個時間點之間的變化 |
| `Sparkline` | 一個緊湊的趨勢與單一數字並排顯示 |
