---
title: 圖片比較
summary: 拖曳分隔線橫跨兩張圖片，查看變化之處。
source-hash: f705f5fa1e20
---

<Demo name="BeforeAndAfter" />

## 何時使用

`ImageCompare` 在同一個畫面中放置同一張圖片的兩個版本，讓讀者拖曳分隔線橫跨兩者。揭示多少變化由讀者自己決定，因此它適用於任何「變化本身就是重點」的場合：修圖、去背、重新設計或視覺測試。它位於頁面、審閱面板或編輯器之中，緊鄰產生變化的控制項。

- **商品刊登中的去背**：賣家在商品上架前，並排查看同一畫面中的原圖與去背圖。分隔線能顯示商品邊緣是否保留完好。
- **攝影工作室工具中的修圖核准**：客戶拖曳分隔線比較人像的原始膚質與修圖後的膚質，然後核准或要求再修一次。
- **房產刊登工具中的樣板間佈置**：仲介展示同一張照片的空房間與佈置後的版本，並以「空置」和「已佈置」作為說明文字，讓買家看清傢俱帶來的變化。
- **QA 或設計工具中的視覺迴歸審閱**：審閱者把已核准的截圖與頁面的新渲染結果進行比較，拖過頁首，查看間距是否移動。

## 何時不用

- **瀏覽兩張以上的圖片**：用 `Carousel`，它在投影片之間切換，但不做比較。
- **逐一決定佇列中的每張圖片**：用 `CardStack`，它要求對每一項做出保留或略過的決定。
- **把即時應用程式內容拆分為兩個窗格**：用 `ResizablePanels`。那裡的分隔線調整的是真實內容，而不是圖片。
- **同一張圖片的三個或更多版本**：用 `Tabs` 在版本之間切換，因為分隔線只能比較兩側。
- **比較數字或記錄**：用 `SortableDataTable`。圖片無法顯示兩個數值之間的差異。

## 用法

兩側應使用相同的裁切與尺寸，揭示的部分才能對齊。

```xml
<arc:ImageCompare Width="600" AspectRatio="1.5">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#2F4858" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

`ImageCompare` 衍生自 Avalonia 的 `TemplatedControl`，而不是選取型控制項，因此它沒有項目。它的兩側都是內容：`Before` 與 `After` 可以是任意控制項或繪圖，`BeforeSource` 與 `AfterSource` 可以在其上繪製圖片。外框由寬度與 `AspectRatio` 決定，分隔線繪製在兩側之上。前圖一側在分隔線處被裁切，因此拖曳只會移動裁切區域，永遠不會重新配置內容。

### 結構

1. **外框**：圖片區域，尺寸由寬度與 `AspectRatio` 決定（寬度除以高度，預設 1.5，即 3:2）。其圓角使用主題的面板圓角。
2. **後圖一側**：結果，充滿整個外框。`After` 的尺寸會覆蓋外框。
3. **前圖一側**：原圖，在分隔線處裁切。`Position` 是分隔線的位置，以距左側的百分比表示；當 `Orientation` 為垂直時，則以距頂部的百分比表示。預設值為 50。
4. **分隔線**：2 px 的線，帶有 3 px 的淡色邊緣，因此在明亮與昏暗的照片上都清晰可見。淺色模式下線條為凸起的表面色，深色模式下為前景色。
5. **把手**（`Label` 為其命名）：分隔線上直徑 40 px 的圓形，兩側各有一個箭頭。拖曳時它會拉伸為 56 × 36 的膠囊。其點擊區域比圓形向外擴展 8 px；距離外框邊緣 42 px 以內時會減速。
6. **說明文字**：起始角的 `BeforeLabel` 與末端角的 `AfterLabel`，各自位於距外框邊緣 12 px 處的毛玻璃膠囊中，使用超小字級。分隔線靠近時，說明文字會淡出。
7. **圖片**（`BeforeSource`、`AfterSource`）：覆蓋每一側的選用圖片。載入失敗的圖片會讓其下方的內容保持可見。

### 標籤與外框

`BeforeLabel` 與 `AfterLabel` 為兩側加上說明文字，預設分別為「前圖」和「後圖」。分隔線經過某個說明時，該說明會淡出，因此分隔線下方的說明不會遮住讀者正在查看的細節。`ShowLabels` 可同時隱藏兩個說明文字。`AspectRatio` 設定外框的寬度除以高度的比值，預設為 1.5（3 / 2）。請依照片的比例來選擇，使兩側的圖片都不會被大幅裁切。

<Demo name="WideFrame" />

```xml
<arc:ImageCompare AspectRatio="1.7777" ShowLabels="False" Position="30" Label="清理預覽">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#8AA399" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

### 上下比較

<Demo name="Vertical" />

```xml
<arc:ImageCompare Orientation="Vertical" BeforeLabel="原圖" AfterLabel="調色後">
    <arc:ImageCompare.Before>
        <Border Background="#D9D2C5" />
    </arc:ImageCompare.Before>
    <arc:ImageCompare.After>
        <Border Background="#2F4858" />
    </arc:ImageCompare.After>
</arc:ImageCompare>
```

當變化是由上到下延伸的，例如重新調色的寬幅橫幅，或頁首有所改動的頁面，就使用垂直配置。執行時修改 `Orientation` 會讓分隔線旋轉四分之一圈，揭示區域隨之移動。

### 圖片

`BeforeSource` 與 `AfterSource` 會在每一側的內容之上繪製圖片。圖片在背景載入，並從輕微模糊中淡入。載入失敗時會顯示下方的內容，因此圖片缺失的頁面仍能進行有意義的比較。把 `Before` 與 `After` 設為與圖片顏色和裁切一致的預留內容，這樣在圖片載入期間外框看起來也是正確的。

<Demo name="Photos" />

```csharp
var compare = new ImageCompare
{
    Before = new Border { Background = Brushes.Gray },
    After = new Border { Background = Brushes.SteelBlue },
    BeforeSource = ImageSource.Parse("https://example.com/before.jpg"),
    AfterSource = ImageSource.Parse("https://example.com/after.jpg")
};
```

### 位置

`Position` 是分隔線的位置，以百分比計，從左側起算；垂直時從頂部起算。它是雙向繫結的，預設值為 50。`PositionChanged` 回報已確定的值：拖曳時每變化一個整百分點回報一次，放開時再回報一次。按鍵、點擊和連按兩下也會確定一個值。

```csharp
compare.PositionChanged += (_, e) => readout.Text = $"位置 {e.Position:0.#}%";
compare.Position = 75;
```

### 繫結到檢視模型

在編輯器或審閱工具中，檢視模型保存兩張圖片與揭示位置，因此審閱者返回該項目時，已儲存的位置會恢復。此控制項沒有命令：它透過雙向的 `Position` 回報位置。

```csharp
public interface IPhotoService
{
    string OriginalUrl { get; }

    string CutoutUrl { get; }
}

public partial class ListingPhotoViewModel : ObservableObject
{
    public ListingPhotoViewModel(IPhotoService photos)
    {
        OriginalPhoto = ImageSource.Parse(photos.OriginalUrl);
        CutoutPhoto = ImageSource.Parse(photos.CutoutUrl);
    }

    public ImageSource OriginalPhoto { get; }

    public ImageSource CutoutPhoto { get; }

    [ObservableProperty]
    private double _revealPercent = 50;

    [ObservableProperty]
    private string _status = "拖曳分隔線比較兩個版本。";
}
```

```xml
<StackPanel Spacing="12">
    <arc:ImageCompare Label="商品圖片，原圖與去背圖"
                      BeforeLabel="原圖"
                      AfterLabel="已去除背景"
                      BeforeSource="{Binding OriginalPhoto}"
                      AfterSource="{Binding CutoutPhoto}"
                      Position="{Binding RevealPercent, Mode=TwoWay}" />
    <TextBlock Text="{Binding Status}" />
</StackPanel>
```

- 拖曳時，分隔線每越過一個整百分點，`Position` 就會寫回檢視模型；放開時還會再寫一次。因此不需要另外的儲存步驟，已儲存的值就會跟隨審閱者最後的位置。
- 從檢視模型設定的值會以彈簧動畫把分隔線移動到該位置；減少動態效果時則直接放置。
- 拖曳期間 `PositionChanged` 會觸發很多次。請在放開時再寫入統計資料或已儲存的值，或對處理常式做節流，而不是在每個事件中處理。
- `AfterShare` 是唯讀的，以整百分比給出後圖所佔的比例。當措辭有助於審閱者理解時，可以繫結它，在外框旁顯示「後圖 40%」。

### 對齊與裁切

兩側都覆蓋外框，因此分隔線比較的是每張圖片中同一位置的內容。如果兩側的裁切不同，比較的就是裁切而不是變化本身。請為兩側使用相同尺寸與比例的來源圖，並把 `AspectRatio` 設為與這些照片一致。3:2 的照片放入 1.78 的外框時，兩側都會等量地失去頂部與底部，揭示區域因此保持對齊。同一張照片的兩種不同裁切完全無法對齊，因此請從同一個取景框匯出第二個版本。

### 節制地使用動態效果

分隔線與指標一比一跟隨；當數值來自拖曳之外時，它會以彈簧方式移動。動態效果在這裡是為了幫助讀者看清變化所在。如果分隔線自行移動，就會分散注意力，因此不要循環地為 `Position` 加上動畫，也不要把方向切換的擺動當作裝飾。只有當讀者需要在並排檢視與堆疊檢視之間切換時，才使用這種擺動。按鍵推到邊緣時的小幅彈動，是對無處可去的按鍵操作的回饋，因此只為鍵盤保留它；減少動態效果時，它會與彈簧效果一同移除。

### 範例：商品刊登中的去背

一個賣家工具，在發佈商品照片前去除其背景。原圖與去背圖共用同一個外框、同一個商品位置，因此分隔線能顯示瓶身邊緣是否保留完好，以及陰影是否遺失。說明文字依照改動的步驟為每一側命名，而不是使用「前圖」和「後圖」，讓審閱者知道第二張圖是什麼。狀態列告訴審閱者接下來該做什麼。圖形用來代替照片，使截圖自成一體；在應用程式中，請依上文所示繫結兩張照片。

![商品照片上方的分隔線，下方分別是原圖與去除背景後的版本，每一側都有說明文字](images/CutoutCompare.png)

```xml
<StackPanel Width="560" Spacing="16">
    <TextBlock Text="Listing photo, background removed"
               FontSize="16"
               FontWeight="Medium"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:ImageCompare Label="Listing photo, original and cut-out"
                      BeforeLabel="Original"
                      AfterLabel="Background removed"
                      Position="50">
        <arc:ImageCompare.Before>
            <Grid Background="#CFC6B6">
                <Border Height="90"
                        VerticalAlignment="Bottom"
                        Background="#A89C88" />
                <StackPanel HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            Margin="0,0,0,40">
                    <Border Width="64"
                            Height="26"
                            CornerRadius="6"
                            Background="#1E2B33" />
                    <Border Width="150"
                            Height="200"
                            CornerRadius="24"
                            Background="#2F4858" />
                </StackPanel>
            </Grid>
        </arc:ImageCompare.Before>
        <arc:ImageCompare.After>
            <Grid Background="#F6F4F0">
                <StackPanel HorizontalAlignment="Center"
                            VerticalAlignment="Center"
                            Margin="0,0,0,40">
                    <Border Width="64"
                            Height="26"
                            CornerRadius="6"
                            Background="#1E2B33" />
                    <Border Width="150"
                            Height="200"
                            CornerRadius="24"
                            Background="#2F4858" />
                </StackPanel>
            </Grid>
        </arc:ImageCompare.After>
    </arc:ImageCompare>
    <TextBlock Text="Drag the divider to compare the two versions."
               FontSize="13"
               Foreground="{arc:ArcDesignTokenResource TextSecondary}" />
</StackPanel>
```

### 文案規範

- **標籤**（把手的無障礙名稱）：為這對圖片命名，例如「商品圖片，原圖與去背圖」。「比較」之類的泛稱對螢幕閱讀器毫無內容資訊。
- **前後說明文字**：說明每一側的狀態，例如「原圖」和「已去除背景」，或「空置」和「已佈置」。說明文字是明眼讀者在外框上看到的唯一文字，因此必須說明發生了什麼變化。
- **狀態文案**：用一句話說明審閱者應當留意什麼，例如「檢查瓶身邊緣與陰影」。
- 說明文字保持在一到兩個字詞，使其在任何外框寬度下都能放進角落的標籤中。

| 不要寫 | 改為 |
|---|---|
| 前 / 後 | 原圖 / 已去除背景 |
| 比較 | 原圖與修圖後 |
| 圖片 1 / 圖片 2 | 正面檢視，原圖 / 正面檢視，去背圖 |
| 拖我！ | 拖曳分隔線進行比較。 |

### 最佳做法

**建議**

- 透過 `BeforeLabel` 與 `AfterLabel`，用所展示的變化來命名每一側。看到「原圖」和「已去除背景」的審閱者，無需閱讀頁面就知道該問什麼問題。
- 用 `AutomationProperties.Name` 為每張圖片設定無障礙名稱。說明文字只供視覺使用，否則螢幕閱讀器只會讀到兩張沒有名稱的圖片。
- 從同一個取景框匯出兩側，使用相同的尺寸與比例，讓揭示的部分精確對齊。
- 把分隔線放在變化最明顯的位置，例如靠近商品的邊緣，讓讀者無需拖曳就能看到差異。

**避免**

- 比較取景或裁切不同的圖片。分隔線這時量測的是構圖，而不是變化本身。
- 讓分隔線循環往復，或依計時器移動它。讀者需要仔細研究的比較，應由他們自己掌控。
- 當圖片本身無法說明自己是什麼時，用 `ShowLabels="False"` 同時隱藏兩個說明文字。空白的外框讓讀者無從分辨哪一側是哪一側。
- 用於兩個以上的版本。元件沒有提供第二條分隔線，因此讀者無法在一個外框中比較三張圖片。
- 在拖曳期間為每個 `PositionChanged` 事件寫一筆記錄。請在放開時記錄，或對處理常式做節流。

### 常見問題

**為什麼分隔線從中間開始？** `Position` 的預設值為 50。如需從其他位置開始，可在 XAML 中設定它，或繫結到已儲存的值；控制項載入後設定的值會以彈簧方式移動分隔線。

**哪個按鍵可以完整顯示前圖？** `Home` 會把 `Position` 設為 100，使前圖覆蓋整個外框。`End` 會把它設為 0，完整顯示後圖。`PageUp` 與 `PageDown` 每次移動分隔線 10%。

**為什麼分隔線附近的說明文字會消失？** 分隔線靠近時，說明文字會淡出，因此它不會遮住讀者正在比較的細節。分隔線移開後，說明文字會再次出現。

**圖片載入失敗時會怎樣？** 會顯示 `BeforeSource` 或 `AfterSource` 下方的內容。如果把 `Before` 或 `After` 設為顏色相同的預留內容，外框仍然可以當作比較來閱讀。

**如何在並排與堆疊之間切換？** 把 `Orientation` 設為 `Vertical` 或 `Horizontal`。執行時修改它會使分隔線旋轉四分之一圈，位置保持不變。

## API 參考

<!-- api: ImageCompare -->

`ImageComparePositionChangedEventArgs` 是 `PositionChanged` 的事件資料：

| 成員 | 含義 |
|---|---|
| `Position` | 已確定的分隔線位置，以距左側（或頂部）的百分比表示。 |

## 無障礙

- `Label` 為分隔線把手命名，預設為「前後比較」。
- 把手是可取得焦點的滑桿。其值為後圖所佔的比例，例如「後圖 40%」。
- 方向鍵每次移動分隔線 1%，按住 Shift 再按方向鍵移動 10%，PageUp 與 PageDown 每次移動 10%。
- Home 與 End 分別完整顯示前圖或後圖；在把手上連按兩下會使其回到中間。
- 焦點框在按下按鍵後出現，而不是在焦點隨指標移動時出現。
- 說明標籤僅供視覺使用。請為每張圖片設定各自的 `AutomationProperties.Name`，使螢幕閱讀器能分辨兩側。

### 鍵盤

按鍵在把手取得焦點時生效。請先用 Tab 移到把手上；外框本身不接收方向鍵。

| 按鍵 | 操作 |
|---|---|
| `Left` / `Right` | 水平比較時每次移動分隔線 1%；垂直比較時則朝相反方向移動 |
| `Up` / `Down` | 無論哪種方向，每次移動分隔線 1% |
| `Shift` + 方向鍵 | 移動分隔線 10% |
| `PageUp` / `PageDown` | 移動分隔線 10% |
| `Home` / `End` | 完整顯示前圖，或完整顯示後圖 |

按鍵在範圍的任一端按下時，分隔線會向外產生一個小幅彈動，讓讀者知道已經到達極限。

## 動態效果

- 拖曳時分隔線與指標一比一跟隨，越過邊緣時產生阻尼，放開後停留在目前位置。拖曳時把手會拉伸成膠囊形狀。
- 分隔線靠近說明文字時，說明文字會淡出。按鍵推過邊緣時會產生短促的彈動。
- 減少動態效果時，分隔線與把手直接定位，不使用彈簧或邊緣阻尼。關閉動態效果時立即到達最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 放開拖曳 | 以放開時的速度停在目前位置 | 直接放置 | 立即完成 |
| 按鍵、點擊或連按兩下移動 | 以彈簧方式移動到新位置 | 直接放置 | 立即完成 |
| 由程式碼或繫結設定位置 | 以彈簧方式移動到新位置 | 直接放置 | 立即完成 |
| 按鍵推過邊緣 | 向外產生小幅彈動 | 無彈動 | 無彈動 |
| 方向變化 | 旋轉四分之一圈，揭示區域隨之移動 | 直接跳到新方向 | 立即完成 |

## 相關元件

- `Carousel`：瀏覽一組圖片或投影片，而不是比較兩張。
- `CardStack`：逐一瀏覽項目，並對每一項做出決定。
- `Slider`：在沒有圖片的軌道上選擇數值。

| 元件 | 適用情境 |
|---|---|
| `Carousel` | 讀者翻閱多張圖片或投影片 |
| `CardStack` | 每張圖片都需要保留或略過的決定 |
| `Tabs` | 在同一張圖片的三個或更多版本之間切換 |
| `ResizablePanels` | 兩個即時應用程式內容窗格需要互相調整大小 |
| `Slider` | 在軌道上選擇數值，沒有圖片 |
| `SortableDataTable` | 比較兩組數字或記錄 |
