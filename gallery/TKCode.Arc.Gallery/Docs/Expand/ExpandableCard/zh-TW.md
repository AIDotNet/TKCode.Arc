---
title: 可展開卡片
summary: 在需要時為密集的卡片提供更多空間。
source-hash: 79906be72989
---

<Demo name="Default" />

## 何時使用

`ExpandableCard` 是一張摘要卡片，其標題會在下方開啟一個詳細資訊面板。卡片始終保留標題與一行摘要，因此一欄卡片仍便於掃讀，而詳細資訊只在需要的人面前原地出現。卡片還可以橫向展開，為寬幅的詳細資訊（例如一條記錄行或一條狀態時間軸）提供空間，而無需離開清單。

- **開發者主控台中的部署歷程**：每次發布是一張卡片，標題中顯示建置、時間與結果。部署失敗時卡片展開，顯示失敗的健康檢查；部署成功則保持收合。
- **客戶帳戶中的訂單與配送狀態**：標題為「訂單 #4821」，其下是「已出貨，週四送達」。詳細資訊列出商品、承運商與追蹤號碼；狀態變動時，摘要隨之更新。
- **帳單中的方案與訂閱摘要**：方案卡片顯示價格與計費週期，展開後列出包含的席位、限額與發票紀錄。
- **事件與警示佇列**：一排警示，標題中寫明服務與時間，詳細資訊中包含錯誤訊息與最近幾行記錄。

## 何時不用

- **多個互斥的文字區塊**，例如 FAQ 中的問題：用 `Accordion`，它同一時間只保持一個答案展開，並把各列排成單一清單。
- **沒有可顯示內容的卡片**：用 `Card`。標題沒有任何作用的卡片，就是一個沒有用途的控制項。
- **在同一位置瀏覽多張卡片**，例如產品或專案卡片組：用 `CardStack`，它在卡片之間切換，而不是原地展開每一張。
- **編輯記錄**：用 `Drawer` 或 `Dialog`。卡片中的表單會與卡片標題及周圍版面互相爭奪空間，也沒有放置驗證訊息的位置。
- **只需要「閱讀更多」切換的長段落**：用 `ExpandableText`，它會把文字截斷為設定的行數。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExpandableCard Header="Pro 方案"
                        Description="每月 NT$750 · 按年付費"
                        CollapsedWidth="420">
        <StackPanel Spacing="8">
            <TextBlock Text="無限專案與協作者。" />
            <TextBlock Text="一整年的版本歷程。" />
            <TextBlock Text="優先支援，四小時內回應。" />
        </StackPanel>
    </arc:ExpandableCard>
</UserControl>
```

`ExpandableCard` 衍生自 Avalonia 的 `HeaderedContentControl`，因此 `Header` 與 `Content` 的用法與任何帶標題的內容控制項相同。卡片額外提供摘要列（`Description`）、涵蓋整個標題的觸發器、箭頭、詳細資訊面板，以及收合與展開兩種尺寸之間的過渡變形。`Header` 是卡片標題，`Description` 是其下方的摘要列，`Content` 存放詳細資訊。

未設定寬度時，卡片填滿其容器。

### 結構

1. **卡片表面**：表面顏色，1 px 的邊框顏色描邊，表面圓角與靜止陰影。表面會裁剪其內容，因此標題的懸停填色會跟隨卡片的圓角。
2. **標題觸發器**：整個標題是一個按鈕，至少 64 px 高，左右各 20 px 內距，上下各 16 px。懸停與按下時，背景變為淡化的表面顏色。
3. **標題**（`Header`）：小號字級、中等字重，使用前景色。字串類型的標題同時成為卡片的無障礙名稱。
4. **摘要列**（`Description`）：特小號字級，淡化文字顏色，位於標題下方 4 px。當 `Description` 為 null 或空時，該列隱藏，標題收縮為只剩標題。
5. **箭頭**：18 px 的向下箭頭，位於文案之後 20 px，卡片收合時為淡化文字顏色。卡片展開或指標懸停在標題上時變為前景色，卡片開啟時旋轉 180 度。
6. **詳細資訊面板**（`Content`）：淡化的表面顏色，上方有 1 px 的分隔線。左右內距 20 px，上方 16 px，下方 20 px。文字使用次要文字顏色、小號字級，行高 1.4。收合的面板高度恰好為 0 像素，因為分隔線是內嵌線，而不是面板本身的邊框。
7. **焦點環**：標題周圍 2 px 的焦點顏色環，僅在鍵盤焦點時顯示。

### 寬度：CollapsedWidth 與 ExpandedWidth

`CollapsedWidth` 限制收合卡片的寬度（像素），卡片在其容器中置中。不設定時，收合的卡片填滿容器。`ExpandedWidth` 限制卡片展開時的寬度，使卡片可以橫向增長。未設定 `ExpandedWidth` 時，展開的卡片使用 `CollapsedWidth`。若未設定 `CollapsedWidth` 而設定了 `ExpandedWidth`，則收合的卡片填滿容器，展開的卡片以 `ExpandedWidth` 為上限。

<Demo name="GrowingSideways" />

寬度與高度在同一個彈簧上一同變化，因此會橫向增長的卡片，其詳細資訊應當在較寬的尺寸下也便於閱讀。詳細資訊在展開寬度下始終保持版面配置，因此卡片移動時文字不會重新排版。

### 預設展開

`IsExpanded` 是雙向的。設為 `True` 即可預設展開，也可以繫結到檢視模型屬性。當卡片存在的理由就是其詳細資訊時，預設展開是正確的選擇，例如一次需要在清單中其他內容之前先處理的部署失敗。

<Demo name="OpenByDefault" />

### 更新摘要

在程式碼中或透過繫結修改 `Description`。只有發生變化的詞會滾入，其餘部分保持不動，因此狀態變動讀起來是「變化」，而不是「重繪」。

```csharp
order.Description = "運送中 · 今天送達";
```

讓摘要保持同一種格式，例如「狀態 · 時間」或「商品 · 金額」。如果每次更新的格式都不同，所有詞都會滾動，比只有列中某一部分變化更難讀。

### 事件

`ExpandedChanged` 是一個會冒泡的路由事件。它在卡片展開或收合之後觸發，因此處理常式看到的是新的 `IsExpanded` 值。

```csharp
card.ExpandedChanged += (_, _) =>
{
    logger.Info($"方案詳情已展開：{card.IsExpanded}");
};
```

### 繫結到檢視模型

部署卡片通常屬於一個檢視模型，由它持有摘要以及卡片是否展開。使用 CommunityToolkit.Mvvm 時，檢視模型透過屬性通知卡片，而卡片則透過雙向的 `IsExpanded` 繫結把使用者的點按回報回去。

```csharp
public partial class DeploymentViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isDetailsOpen;

    [ObservableProperty]
    private string _summary = "api-gateway · 2 分鐘前";

    [RelayCommand]
    private void Retry() => Summary = "api-gateway · 正在重試";
}
```

```xml
<arc:ExpandableCard Header="部署失敗"
                    Description="{Binding Summary}"
                    IsExpanded="{Binding IsDetailsOpen, Mode=TwoWay}">
    <StackPanel Spacing="12">
        <TextBlock Text="對 /ready 的健康檢查在 30 秒後逾時。" TextWrapping="Wrap" />
        <arc:Button Content="重試部署"
                    Variant="Secondary"
                    Size="Small"
                    Command="{Binding RetryCommand}"
                    HorizontalAlignment="Left" />
    </StackPanel>
</arc:ExpandableCard>
```

`Description` 應使用單向繫結，因為卡片只負責顯示它。`IsExpanded` 應使用雙向繫結，使得透過鍵盤、Escape 或使用者點按收合卡片時，檢視模型也隨之更新。不要因為 `Description` 變動而從檢視模型寫入 `IsDetailsOpen`，因為卡片的展開狀態由使用者決定。

### 清單與堆疊中的版面配置

在垂直清單或 `StackPanel` 中，卡片取得面板分配給它的寬度，因此一欄卡片無需在卡片本身設定寬度即可對齊。在水平堆疊中，可用寬度沒有上限，因此由上限決定寬度：請為這些卡片設定 `CollapsedWidth` 與 `ExpandedWidth`，否則卡片會依內容決定自身尺寸。容器尺寸變動會立即生效且沒有動畫，因此視窗縮放永遠不會觸發動態效果。

### 範例：部署歷程

開發者主控台中的部署歷程。清單顯示三個服務的最近三次發布。第一張卡片是展開的，因為它的部署失敗了，需要優先閱讀健康檢查的輸出。每個標題都寫明服務與時間，使清單的順序與團隊處理問題的順序一致。寬度為 560，足以讓詳細資訊文字在一到兩行內顯示。

![部署歷程：一次展開的失敗部署與兩次收合的成功部署](images/DeployHistory.png)

```xml
<StackPanel Width="560" Spacing="12">
    <arc:ExpandableCard Header="Deploy failed"
                        Description="api-gateway · 2 minutes ago"
                        IsExpanded="True">
        <StackPanel Spacing="12">
            <TextBlock Text="Health check on /ready timed out after 30s. Build 4821 is not serving traffic; build 4818 still is."
                       FontSize="14"
                       TextWrapping="Wrap"
                       Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
            <arc:Button Content="Retry deploy"
                        Variant="Secondary"
                        Size="Small"
                        HorizontalAlignment="Left" />
        </StackPanel>
    </arc:ExpandableCard>
    <arc:ExpandableCard Header="Deploy succeeded"
                        Description="web-app · 14 minutes ago">
        <TextBlock Text="Build 4820 deployed to 6 regions in 4 minutes."
                   FontSize="14"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </arc:ExpandableCard>
    <arc:ExpandableCard Header="Deploy succeeded"
                        Description="billing-worker · 1 hour ago">
        <TextBlock Text="Build 4812 deployed to 2 regions in 3 minutes."
                   FontSize="14"
                   TextWrapping="Wrap"
                   Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
    </arc:ExpandableCard>
</StackPanel>
```

### 文案規範

- **標題**：句首大寫的名詞或結果，例如「Pro 方案」「訂單 #4821」或「部署失敗」。保持在一行內；其餘資訊交給摘要。
- **摘要**：一行事實，格式固定，以中點分隔：「api-gateway · 2 分鐘前」「每月 NT$750 · 按年付費」。清單依時間或狀態排序時，把時間或狀態放在最前面。
- **詳細資訊**：使用者需要據此行動的資訊，用完整的句子表達。詳細資訊面板應包含狀態的原因與下一步，而不是重複標題。
- **操作**：詳細資訊中的按鈕使用動詞加受詞，例如「重試部署」或「下載發票」。

| 不要寫 | 改為 |
|---|---|
| 點按查看更多 | 方案詳情 |
| 狀態：失敗！！！ | 部署失敗 |
| 更多 | 重試部署 |
| 詳細資訊 | /ready 的健康檢查在 30 秒後逾時 |
| 訂單資訊 | 訂單 #4821 · 已出貨，週四送達 |

### 最佳做法

**建議**

- 讓標題和摘要足夠簡短，使一欄卡片一眼就能掃讀。未展開任何內容時，使用者讀到的就是標題。
- 只有當卡片的詳細資訊需要優先於清單中其他內容處理時（例如失敗），才預設展開它。
- 詳細資訊保持在一到兩段簡短文字加一個操作。過長的詳細資訊面板會把清單其餘部分推向頁面下方，而這正是卡片想要避免的。
- 清單中的每張卡片都設定 `Description`，要麼全都不設。有摘要列與無摘要列的卡片混在一起，標題的高度會跳動。

**避免**

- 在詳細資訊中放置表單。收合面板中的驗證訊息與 Tab 順序都難以觸及；表單應放在 `Drawer` 或 `Dialog` 中。
- 每次更新都改變摘要的格式。詞只在變化時滾動，新的格式會讓整列都滾動，讀起來就是雜訊。
- 清單中只有一張卡片設定了 `CollapsedWidth`，其他卡片不設定。卡片寬度會不一致，欄看起來參差不齊。
- 把卡片當作所有人都需要的某項詳細資訊的唯一入口。從不展開它的使用者會錯過，因此必要的資訊應保留在頁面上。
- 依賴動畫來說明變化。卡片在靜止時就是正確的，減少動態效果時會立即顯示相同的最終狀態。

### 常見問題

**如何隱藏摘要列？** 將 `Description` 設為 `null` 或空字串。摘要列會收合，標題列只剩標題。

**為什麼按 Escape 不能關閉卡片？** 只有在卡片展開、並且其內部沒有控制項已經處理該按鍵時，Escape 才會關閉卡片。詳細資訊中使用 Escape 的文字方塊或對話框會保留該按鍵。

**頁面顯示之前能否在程式碼中展開卡片？** 可以。在卡片載入前設定 `IsExpanded`，會立即顯示最終狀態，因此第一個畫面就是正確的。卡片載入之後，`IsExpanded` 的變動才會產生動畫。

**為什麼視窗縮放時卡片不做動畫？** 容器尺寸變動會立即生效。變形動畫用於使用者的操作（展開、收合以及摘要更新），而不用於版面配置的變動，因此視窗縮放永遠不會觸發動態效果。

**如何在卡片外部得知它的狀態？** 讀取 `IsExpanded`，或處理 `ExpandedChanged`。處理常式在變動之後執行，因此 `IsExpanded` 已經是新值。

## API 參考

<!-- api: ExpandableCard -->

## 無障礙

- 標題是一個按鈕，遵循展開與收合模式，因此輔助技術會朗讀卡片是展開還是收合，卡片狀態變動時也會同步朗讀。
- 標題為字串時，卡片的無障礙名稱即為其 `Header`。`Description` 作為標題按鈕的說明文字對外提供，因此它在名稱之後朗讀。
- 摘要列也以文字元素的形式公開完整內容，因此螢幕閱讀器讀的是整列，而不只是滾入的那幾個詞。
- 卡片收合時，詳細資訊處於惰性狀態：其中的控制項無法取得焦點，也不接收指標輸入。
- 焦點位於展開卡片的任何位置時，Escape 都會收合卡片，並把焦點返回到標題，因此焦點不會停留在已經收合的面板中。

### 鍵盤

標題是一個按鈕，因此標準按鈕按鍵同樣適用。卡片額外提供 Escape 收合。

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 移到標題；僅在卡片展開時才能進入詳細資訊 |
| `Enter` / `Space` | 從標題展開或收合卡片 |
| `Escape` | 焦點位於卡片內且沒有控制項處理該按鍵時，收合展開的卡片，然後把焦點移到標題 |

## 動態效果

- 展開是一個完整的動作：寬度與高度以不過衝的平滑彈簧一同增長，箭頭隨之轉動。卡片透過改變實際尺寸而不是縮放來變化，因此文字與圓角都不會被拉伸。
- 卡片騰出空間之後，詳細資訊才淡入。收合時，詳細資訊先淡出，卡片隨後收縮。
- 動畫進行中再次點按，會從目前尺寸與速度反向運動。在約 450 ms 內、緊隨另一次變動之後發生的收合，會跳過卡片收縮前的停頓。
- 在摘要列中，變動的詞從下方帶著柔和模糊升起。它們所取代的舊詞向上飄離並淡出。未變動的詞保持不動。
- 減少動態效果時直接套用最終狀態，摘要文字無動畫地替換。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 卡片展開 | 寬度與高度以平滑彈簧一同增長；箭頭轉動 | 直接展開 | 立即完成 |
| 詳細資訊出現 | 卡片騰出空間後淡入，延遲 120 ms | 直接顯示 | 立即完成 |
| 卡片收合 | 詳細資訊在 100 ms 內淡出；卡片在 60 ms 停頓後收縮 | 直接收合 | 立即完成 |
| 摘要變化 | 變動的詞升起，舊詞飄離並淡出 | 直接替換 | 立即完成 |
| 標題懸停與按下 | 背景漸變為淡化的表面顏色 | 立即完成 | 立即完成 |

## 相關元件

- `Accordion`：用於由互斥區塊組成的清單。
- `Card`：用於沒有可展開詳細資訊的卡片。
- `ExpandableText`：用於原地展開的長段文字。

| 元件 | 適用情境 |
|---|---|
| `Accordion` | 多個文字區塊作為一個清單，每次展開一個 |
| `Card` | 卡片沒有需要展示的詳細資訊，因此內容始終可見 |
| `CardStack` | 使用者在卡片之間切換，而不是逐一展開 |
| `ExpandableText` | 一段長文字顯示幾行，並帶有「閱讀更多」切換 |
| `MetricCard` | 單一數字及其趨勢就是卡片的全部意義 |
