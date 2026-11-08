---
title: 桑基流向圖
summary: 以色帶呈現各階段之間的流量，粒子沿色帶飄移：懸停或聚焦節點、色帶，即可追蹤其占比貫穿整段旅程。
source-hash: 02cdef11fcdd
---

<Demo name="TrialJourney" />

## 何時使用

`SankeyFlow` 呈現一個數量在各階段流轉時如何分流與匯合。每個節點是途中的一站，每條色帶是一股流量，色帶的粗細就是它的值，讀者一眼就能看出大部分流向哪裡、又在哪裡流失。

- **產品旅程**：從試用來自的管道，經過新手引導，到最後選擇的方案。追蹤一個管道，就能看到它實際帶來了哪些方案。
- **預算**：從收入來源，經過各部門，到錢用在哪裡。
- **能源或物料平衡**：在公用事業或營運報告中，從供應經過儲存到使用。
- **工單流轉**：從工單的來源，經過接手的團隊，到它的結案方式。

## 何時不用

- **只會逐步收窄的單列階段**，例如沒有分支的註冊漏斗：使用 `BarChart`，它能精確比較每一步的大小。
- **沒有階段的整體與部分**：使用 `DonutChart` 或 `Treemap`。
- **隨時間的變化**：使用 `LineChart` 或 `Streamgraph`。流向圖只呈現一個時期。
- **重點在於圖的結構**，只有步驟和分支而沒有數量：使用 `WorkflowCanvas`。
- **超過幾百股流量**：色帶會細到無法閱讀。請先把小流量歸併到一個「其他」節點。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:SankeyFlow Label="Signups by source" Unit=" people" TotalLabel="all signups">
        <arc:SankeyFlow.Nodes>
            <arc:SankeyFlowNode Id="web" Label="Website" />
            <arc:SankeyFlowNode Id="app" Label="Mobile app" />
            <arc:SankeyFlowNode Id="kept" Label="Still active" />
            <arc:SankeyFlowNode Id="left" Label="Left" />
        </arc:SankeyFlow.Nodes>
        <arc:SankeyFlow.Links>
            <arc:SankeyFlowLink Source="web" Target="kept" Value="420" />
            <arc:SankeyFlowLink Source="web" Target="left" Value="180" />
            <arc:SankeyFlowLink Source="app" Target="kept" Value="260" />
            <arc:SankeyFlowLink Source="app" Target="left" Value="40" />
        </arc:SankeyFlow.Links>
    </arc:SankeyFlow>
</UserControl>
```

`SankeyFlow` 衍生自 Avalonia 的 `Control`，由自身繪製。可以像上面這樣內嵌加入節點和連結，也可以在程式碼或繫結中把任意清單指定給 `Nodes` 和 `Links`。替換清單或修改可觀察清單都會重繪圖表。

### 結構

1. **階段名稱**（`Columns`）：繪圖區上方一行，直向版面時在每一列上方。
2. **節點**：長條的長度取流入與流出中較大的一個。圖表的所有欄共用一個數值比例，因此長條和色帶可以跨整張圖比較。
3. **色帶**：每對節點一條。色帶兩端的粗細都等於它的值，顏色從來源階段漸層到目標階段。
4. **粒子**：沿每條色帶朝流動方向飄移的小點。
5. **標籤**：長條旁邊的節點名稱和數值。相鄰節點過近時，先省略數值行，再省略標籤；工具提示裡仍然可以讀到。
6. **粒子開關**：在繪圖區下方靠右。
7. **工具提示**：被追蹤的內容、它的值，以及占總量的比例。

### 欄

沒有 `Column` 的節點放在所有流入它的節點中最靠後的那個的下一欄，因此沒有流入的節點從第一欄開始。設定 `Column` 可以自行放置節點，例如讓一個結束狀態與其他結果對齊。`Columns` 從第一欄起命名各階段。

```xml
<arc:SankeyFlow Label="Home energy">
    <arc:SankeyFlow.Nodes>
        <arc:SankeyFlowNode Id="battery" Label="Home battery" Column="1" />
    </arc:SankeyFlow.Nodes>
</arc:SankeyFlow>
```

### 連結與循環

連結的 `Source` 和 `Target` 是節點 id。找不到對應節點的連結端會得到一個以 id 命名的節點，所以簡單的圖只寫連結就夠了。相同兩個節點之間的連結會累加。自我迴圈和非正數的值會被忽略。

循環不會讓圖表出錯：閉合循環的那條連結會彎回到它的目標。追蹤沿這條連結走一圈後停止。`Column` 也可能讓連結反向，繪製方式相同。

### 格式與總量

`Unit` 跟在每個值後面，`ValueFormatter` 取代整個格式。`TotalLabel` 命名比例行裡的整體，例如把「42% of the total」寫成「42% of the city budget」。總量是進入旅程的量：所有沒有流入的節點的流出總和。

<Demo name="CityBudget" />

### 追蹤

懸停在節點或色帶上、用鍵盤聚焦或輕觸它。它的占比會以強調色點亮前後每個階段，其他內容變暗；每個節點按到達它的相同比例繼續傳遞。光帶的粗細就是占比，所以只供給後續階段一部分的節點，也只覆蓋該階段的相應部分。

### 粒子

粒子除了方向和大致分量外不帶資料：色帶越重，粒子越多，最多 34 個。用 `IsParticlesEnabled` 關閉粒子，它是雙向屬性並會觸發 `ParticlesChanged`。開關寫入的是同一個屬性。把 `IsParticleToggleVisible` 設為 `False` 可移除開關，用 `ParticleToggleLabel` 翻譯開關文字。

<Demo name="ParticlesOff" />

```csharp
chart.ParticlesChanged += (_, e) => settings.ShowParticles = e.IsEnabled;
```

### 繫結到檢視模型

使用 CommunityToolkit.Mvvm 時，由檢視模型持有清單，並在切換時期時替換它們。id 不變的節點和連結會變形到新的大小和位置。

```csharp
public partial class JourneyViewModel : ObservableObject
{
    [ObservableProperty]
    private IList<SankeyFlowLink> _flows = [];

    [ObservableProperty]
    private bool _showParticles = true;

    public void Load(IEnumerable<(string From, string To, double Count)> rows)
        => Flows = rows.Select(row => new SankeyFlowLink(row.From, row.To, row.Count)).ToList();
}
```

```xml
<arc:SankeyFlow Label="Trial journey"
                Links="{Binding Flows}"
                IsParticlesEnabled="{Binding ShowParticles, Mode=TwoWay}" />
```

### 資料形狀與效能

每次資料或尺寸變化只版面配置一次。繪製使用快取的幾何，粒子位於獨立的圖層，因此一影格粒子動畫只重繪這些點，圖表自身的程式碼不產生配置。幾百條連結的圖表依然流暢，粒子總數也限制在幾百個以內。

### 內容準則

- **圖表標籤**：流動的是什麼、在什麼時候，例如「Lumen Notes trials, third quarter」。它為輔助科技命名圖表。
- **節點標籤**：簡短的名詞片語。過長的名稱以省略號截斷，直向版面時截斷得最緊。
- **階段名稱**：盡量一個詞，例如「Channel」或「Outcome」。
- **單位**：單字要包含前導空格（" trials"），符號則不要（"%"）。

| 不要寫 | 應該寫 |
|---|---|
| Node 1 | Search ads |
| Step 2 | Onboarding |
| Dropped | Lapsed |
| Total | all trials |

### 最佳實務

**建議**

- 讓流入節點的量等於流出的量，除非旅程確實有增減，這樣比例才對得上。
- 使用跨時期不變的 id，讓圖表變形而不是替換。
- 色帶細到一兩個像素以下時，把小流量歸併到一個「其他」節點。

**避免**

- 用色帶顏色表達含義。顏色只標記階段，追蹤使用強調色和變暗。
- 在一個階段裡重複計算同一個人。比例是相對總量讀取的，重複會使其虛高。
- 在窄面板裡放很多欄。四五個階段讀起來最好；低於 420 px 時每個階段占一列。

### 常見問題

**為什麼多個節點的比例都是 100%？** 每個節點的比例是它的值與總量之比，所有流量都經過的階段就是 100%。

**為什麼缺少標籤？** 相鄰的標籤會避讓以免重疊。懸停或聚焦節點，可在工具提示裡讀取。

**為什麼按 Tab 進不了圖表？** 圖表只有在有流量時才能取得焦點。空圖表顯示 `EmptyLabel`。

## API 參考

<!-- api: SankeyFlow -->

<!-- api: SankeyFlowNode -->

<!-- api: SankeyFlowLink -->

## 無障礙

- 圖表是一個可聚焦的群組，由 `Label` 命名。追蹤節點或色帶時，名稱會以禮貌方式朗讀它的值、比例和去向。
- 說明文字包含每個節點的摘要和按鍵說明。
- 追蹤使用強調色加變暗，而不只靠色相，取得焦點的節點還有焦點環。
- 在觸控螢幕上，輕觸可追蹤，再次輕觸可清除。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 聚焦圖表並追蹤第一個節點。沒有流量時略過圖表 |
| `Up` / `Down` | 欄中的上一個或下一個節點。直向版面時為上一列或下一列 |
| `Left` / `Right` | 上一欄或下一欄中最近的節點。直向版面時為列中的上一個或下一個節點 |
| `Home` / `End` | 第一欄或最後一欄的第一個節點 |
| `Escape` | 清除追蹤 |

## 動態效果

- 首次顯示時，節點和色帶逐欄生長出來。
- 資料變化時，它們在一個共用的彈簧上變形；新增的從無到有，移除的收縮消失。
- 聚焦時，被追蹤的占比沿色帶在彈簧上向外傾注，其餘部分變暗。
- 粒子由 `LiveLoop` 驅動，圖表離開螢幕時暫停。每條色帶最多 34 個。
- 減少動態效果時直接繪製最終狀態，並移除粒子和開關。

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 首次顯示 | 逐欄生長 | 短暫淡入 | 直接繪製 |
| 資料變化 | 彈簧變形 | 直接繪製 | 直接繪製 |
| 追蹤 | 占比沿色帶傾注 | 直接繪製 | 直接繪製 |
| 粒子 | 沿色帶飄移 | 與開關一併移除 | 與開關一併移除 |

## 響應式

- 寬度低於 420 px 時，圖表由上到下排列：欄變成列，標籤位於節點下方，階段名稱位於每列上方。
- 它測量自身寬度，所以窄面板中的圖表與手機上的圖表變化方式相同。
- 直向版面每列至少 112 px，因此可能比 `PlotHeight` 更高。
- 方向鍵跟隨版面：上下鍵在列之間移動。
- 用手指操作時，輕觸追蹤，再次輕觸清除，因為觸控沒有懸停。

## 相關元件

- `BarChart`：精確比較少量數值。
- `Treemap`：階層結構中的整體與部分。
- `Streamgraph`：隨時間變化的圖層。
- `WorkflowCanvas`：沒有數量的步驟圖。

| 元件 | 何時選擇 |
|---|---|
| `BarChart` | 每一步有一個需要精確比較的值 |
| `DonutChart` | 資料是一個整體的幾個部分 |
| `Treemap` | 各部分按階層巢狀 |
| `Streamgraph` | 各圖層隨時間變化 |
| `WorkflowCanvas` | 重要的是流程的形狀，而不是數量 |
