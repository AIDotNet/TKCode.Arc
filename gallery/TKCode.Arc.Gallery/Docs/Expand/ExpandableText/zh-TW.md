---
title: 可展開文字
summary: 長文字截斷為幾行，底部帶柔和漸隱；點選「閱讀更多」平滑展開，點選「收合」折回。僅在文字溢出時顯示切換按鈕。
source-hash: fa0e404de0a0
---

<Demo name="Default" />

## 何時使用

`ExpandableText` 以幾行呈現一段長文字，並讓讀者展開其餘部分。即使各則文字長短不一，它也能讓清單或欄位的版面保持穩定，當每一項是卡片或列、背後有固定格線時尤其重要。切換按鈕只在文字確實溢出時出現，因此短文字保持原樣。

- **客戶評價與留言**：商品頁的評價清單或支援工具中的留言串，每則只顯示兩行，讀者需要時再展開閱讀全文。
- **個人簡介與團隊介紹**：內部工具中的人員目錄，每張卡片顯示三到四行簡介，完整內容按需展開。
- **目錄中的商品描述**：商品標題下的長描述，截斷後讓價格和加入購物車按鈕保持在首屏，切換按鈕文字為「查看完整描述」。
- **開發者主控台中的發佈說明與更新日誌**：每個版本顯示說明的前幾行，其餘內容在原處展開，無需跳轉到其他頁面。

## 何時不用

- **必須閱讀的文字**，例如法律條款、結帳時的退款政策，或帶有修復步驟的錯誤訊息。不要截斷它們；應顯示全文，或連結到存放全文的頁面。
- **一組帶標題的段落或問題**：用 `Accordion`，它會為每一段提供作為列標題的問題。
- **隱藏在摘要列之後的結構化資訊**，例如帶狀態與商品清單的訂單：用 `ExpandableCard`，它會保持標題與摘要可見。
- **需要保持固定高度並可捲動的文字**，例如日誌或逐字稿：用 `ScrollArea`。
- **本身就很短的文字**，例如標籤或標題：用普通的 `TextBlock`。標題上加切換按鈕只會增加干擾。

## 用法

用 `Text` 設定內容。文字會被截斷為 `Lines` 行，預設為 3 行。只有當文字超出截斷範圍時，切換按鈕才會出現。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ExpandableText Text="我們在一個下午內就把整個設計團隊遷移到新的工作區。匯入過程保留了每一則留言與每個版本。"
                        MaxWidth="520"
                        HorizontalAlignment="Left" />
</UserControl>
```

`ExpandableText` 衍生自 Avalonia 的 `TemplatedControl`，包含三個部分：截斷後的文字、末行上的漸隱以及其下方的切換列。文字是截斷區域內一個普通的自動換行 `TextBlock`，因此文字樣式（`FontSize`、`Foreground`、`LineHeight`）在控制項上設定，文字會繼承它。預設文字為小號字級、次要文字顏色、1.4 倍行高，這是 Arc 在卡片與清單中內文的外觀。

請在控制項上設定 `MaxWidth` 或 `Width`，而不是在文字上設定。截斷以控制項取得的寬度為準，因此可見行數會隨版面變化。

### 結構

1. **文字**（`Text` 或 `Inlines`）：自動換行的文字，次要文字顏色、小號字級，行高 19.6 px（14 px 乘以 1.4）。
2. **截斷**：摺疊時，文字停在第 `Lines` 行的底部，也就是 `Lines` 乘以行高。超出的部分會被隱藏，並且在展開之前不會排版，因此很長的文字與短文字的開銷相同。寬度或字型變化時會重新計算截斷，無需程式碼即可回應尺寸變化。
3. **漸隱**：摺疊時，底部 1.6 行上覆蓋一層柔和的遮罩。文字展開時遮罩消散，收合時在接近截斷處重新出現。
4. **切換列**：文字下方 8 px 的間距。切換按鈕靠左對齊，最小高度 28 px，水平內距 2 px，左外距為負 2 px，使標籤與文字邊緣對齊。
5. **切換標籤**（`MoreLabel` 或 `LessLabel`）：中等字重，使用前景色。兩個標籤共用同一儲存格，因此標籤變化時切換按鈕的寬度不變。游標懸停時標籤變為次要文字顏色，按下時變為淡化文字顏色。
6. **箭頭**：標籤後 16 px 的向下箭頭，使用淡化文字顏色。它隨展開進度旋轉 180 度，與文字的進度保持一致。
7. **焦點環**：切換按鈕周圍 2 px 的焦點環，僅在鍵盤聚焦時顯示。

### 行數

`Lines` 設定文字摺疊時顯示的行數。留言與評價這類需要在一個畫面內放下大量項目的密集清單使用 2 行；個人簡介與商品描述等首句資訊量最大的內容使用 3 或 4 行。最小值為 1 行，小於 1 的值會被提升為 1。

<Demo name="TwoLines" />

```xml
<arc:ExpandableText Lines="2"
                    Text="{Binding CommentText}"
                    MaxWidth="420" />
```

請依真實寬度計算行數。同一段文字在窄卡片與寬欄位中截斷出的行數不同，因此應在文字出現的最窄寬度下檢查截斷效果。

### 標籤

`MoreLabel` 與 `LessLabel` 設定切換按鈕的文字。預設值為 `Read more` 和 `Show less`。當切換按鈕應說明會開啟什麼內容時修改它們，例如「查看完整簡介」或「查看完整描述」，因為具體的標籤能讓讀者清楚將得到什麼。

```xml
<arc:ExpandableText MoreLabel="查看完整簡介"
                    LessLabel="收合"
                    Lines="4"
                    Text="{Binding Bio}" />
```

兩個標籤都會作為切換按鈕的名稱提供給輔助技術，因此每個標籤本身都必須是一個完整的操作說明。

### 繫結展開狀態

`IsExpanded` 為雙向繫結。可以將其繫結到檢視模型屬性，也可以在程式碼中設定。文字展開或收合時會觸發 `ExpandedChanged`，其引數為 `ExpandableTextExpandedChangedEventArgs`，其中的 `IsExpanded` 保存新的狀態。

<Demo name="Controlled" />

```xml
<arc:ExpandableText Text="{Binding Bio}"
                    IsExpanded="{Binding BioExpanded, Mode=TwoWay}" />
```

當頁面需要對變化作出回應時使用事件，例如更新狀態列或儲存讀者的選擇。當其他控制項需要開啟或收合文字時使用繫結，如示範中的按鈕所示。

### 文字能完整顯示

只有文字確實溢出時，切換按鈕才會出現，並會隨寬度和字型的變化而更新。短文字沒有切換按鈕也沒有漸隱，因此混合長短項目的清單無需額外邏輯即可正確顯示。

<Demo name="Fits" />

`IsOverflowing` 為唯讀屬性，回報目前的狀態。它適合用於頁面其他位置顯示數量或提示，而不用於決定是否顯示切換按鈕：控制項已經自行處理了這一點。

### 由資料驅動

評價清單是一組項目，每個項目都有自己的文字和自己的展開狀態。將展開狀態保存在項目上，使展開一則評價時不會影響其他評價。

```csharp
public sealed partial class ReviewItem : ObservableObject
{
    public required string Author { get; init; }

    public required string Date { get; init; }

    public required string Body { get; init; }

    [ObservableProperty]
    private bool _isExpanded;
}

public partial class ProductReviewsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ReviewItem> _reviews = new();
}
```

```xml
<ItemsControl ItemsSource="{Binding Reviews}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <StackPanel Spacing="6" Margin="0,0,0,16">
                <TextBlock Text="{Binding Author}"
                           FontWeight="Medium"
                           Foreground="{arc:ArcDesignTokenResource Foreground}" />
                <arc:ExpandableText Lines="2"
                                    Text="{Binding Body}"
                                    IsExpanded="{Binding IsExpanded, Mode=TwoWay}" />
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

檢視模型持有文字，項目持有展開狀態。因此只要同一批項目物件仍在集合中，讀者的選擇在清單被篩選或重新排序後仍會保留。若以新物件取代項目，它們的展開狀態會重設為收合。

### 富文字與連結

`Text` 接受純文字，段落之間以換行分隔。當文案需要多段格式或連結時，改為設定 `Inlines`；設定後它會取代 `Text` 使用。請在程式碼中建立該集合：此屬性沒有預設集合，因此 XAML 無法向其中加入文字段。文字中的連結是一個內嵌控制項，放在 `InlineUIContainer` 中，因此與其他控制項一樣可以取得鍵盤焦點。

```csharp
var notice = new ExpandableText { Lines = 3, MaxWidth = 520 };
notice.Inlines = new InlineCollection
{
    new Run("你的資料儲存在歐盟地區。請閱讀 "),
    new InlineUIContainer(new Button { Content = "隱私權政策" }),
    new Run(" 了解保留規則與資料處理者清單。")
};
```

即使被截斷的部分讀者看不到，其中的連結仍可透過鍵盤存取。當此類連結在截斷線以下取得焦點時，文字會展開，焦點永遠不會停留在隱藏的內容上。只放讀者真正需要存取的連結，並把最重要的連結放在文案靠前的位置，使其通常可見。

### 可選取的內容與豐富內容

`ExpandableText` 以自己的 `TextBlock` 繪製文字，讀者無法選取其中的文字。當內容需要可選取，或者根本不是純文字時，把它放進 `ExpandableContent`。它具有相同的截斷、漸隱、切換按鈕和動態效果，以及相同的 `Lines`、`IsExpanded`、`MoreLabel`、`LessLabel` 和 `ExpandedChanged`。

<Demo name="Selectable" />

```xml
<arc:ExpandableContent Lines="3" MaxWidth="520" HorizontalAlignment="Left">
    <SelectableTextBlock TextWrapping="Wrap"
                         Text="{Binding Message}" />
</arc:ExpandableContent>
```

截斷停在內容中第一段可見文字的某一行上：`TextBlock` 或 `SelectableTextBlock`（會穿過一般的面板、邊框和呈現器尋找），或者實作了 `IExpandableLineSource` 的控制項。自行排版的唯讀編輯器實作該介面後，截斷會停在它的兩行之間，而不是從一行中間切開。不是文字的內容（例如轉譯後的 Markdown）沒有可回報的行：設定 `LineHeight`，截斷高度就是 `Lines` 乘以 `LineHeight`。

| 內容 | 截斷位置 |
|---|---|
| `TextBlock`、`SelectableTextBlock` | 第 `Lines` 行的底部。文字使用 Arc 內文樣式與行高，除非它自己另行設定 |
| 實作了 `IExpandableLineSource` 的控制項 | 該控制項回報的行底部 |
| 其他內容，並設定了 `LineHeight` | `Lines` 乘以 `LineHeight` |

摺疊時，內容中的 `TextBlock` 只排版到截斷線之後的一行，這足以判斷它是否溢出。很長的訊息（例如貼上的日誌）在讀者展開之前，開銷與短訊息相同。把 `IsFadeEnabled` 設為 `False` 可以去掉最後一行上的漸隱，例如內容本身已經以分隔線結尾時。

### 範例：商品評價

商城商品頁上的評價清單。兩行截斷讓評價保持緊湊，切換按鈕只出現在較長的評價上，因此簡短的評價按原樣顯示。作者、「已購買」標籤與日期位於文字上方的一行小號淡化文字中，讀者在展開前即可判斷評價的參考價值。清單寬 560，即商品欄的寬度。

![商品評價清單：兩則被截斷的評價，其中一則帶有「閱讀更多」切換按鈕，另一則為簡短評價](images/ReviewList.png)

```xml
<StackPanel Width="560" Spacing="18">
    <TextBlock Text="Customer reviews"
               FontSize="16"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <StackPanel Spacing="6">
        <TextBlock Text="Priya N. · Verified purchase · 2026-03-14"
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <arc:ExpandableText Lines="2"
                            Text="We took the Trail Pack on a three-day ridge walk in wet weather. The main pocket kept the sleeping bag dry, the hip belt stayed comfortable under a loaded pack, and the zips still ran smoothly on the last morning. The only change I would make is a larger side pocket for water bottles." />
    </StackPanel>
    <StackPanel Spacing="6">
        <TextBlock Text="Daniel K. · Verified purchase · 2026-02-27"
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <arc:ExpandableText Lines="2"
                            Text="Fits a laptop and a lunch box with room to spare." />
    </StackPanel>
    <StackPanel Spacing="6">
        <TextBlock Text="Mei L. · Verified purchase · 2026-02-08"
                   FontSize="12"
                   Foreground="{arc:ArcDesignTokenResource TextMuted}" />
        <arc:ExpandableText Lines="2"
                            Text="Bought it for the commute. The shoulder straps needed a little adjusting on the first day, and after that it carried the laptop, a change of clothes and a water bottle without any strain on my back. The reflective trim is a welcome detail in the winter months." />
    </StackPanel>
</StackPanel>
```

### 文案規範

- **切換標籤**：用動詞片語寫明將開啟的內容，使用句子式大小寫。`Read more` 是預設值；「查看完整簡介」和「查看完整描述」說明讀者將得到什麼。「收合」用於關閉文字。
- **文案首句**：評價、簡介或描述的第一句應能在截斷狀態下獨立成立。讀者正是根據這幾行決定是否展開全文。
- **文字上方的中繼資料**：作者、日期以及任何驗證或狀態資訊，放在一行簡短的淡化文字中，使讀者在閱讀正文前就能判斷該項目。
- **數字**：日期與數量使用阿拉伯數字，例如「2026-03-14」「14 則評價」，使截斷區域在切換按鈕出現之前就能顯示完整日期。

| 不要寫 | 改為 |
|---|---|
| 更多... | 閱讀更多 |
| 展開 | 查看完整描述 |
| 點這裡查看剩餘文字 | 查看完整簡介 |
| 摺疊 | 收合 |
| 閱讀這則評價的剩餘部分，其中有大段內容談到拉鍊。 | 閱讀更多 |

### 最佳做法

**建議**

- 密集清單使用兩行；單一且讀者很可能會閱讀的項目（例如個人簡介）使用三或四行。截斷是讀者首先看到的內容，應依內容而非版面來選擇行數。
- 把最有用的資訊放在首句，因為其餘部分在讀者展開之前是隱藏的。
- 清單中每個項目的 `Lines` 保持一致，這樣即使不顯示切換按鈕，各列也能對齊。
- 讓控制項自行測量溢出。不要自己拆分文字或計算字元數，因為截斷取決於真實的寬度與字型。

**避免**

- 截斷讀者必須閱讀的文字，例如條款、價格、錯誤或警告。切換按鈕會將其隱藏，直到讀者主動點開，而很多讀者不會點開。
- 為評價或簡介將 `Lines` 設為 1。單行幾乎隱藏了全部內容，切換按鈕就成了主要內容。
- 把重要的表單或按鈕放在文字的隱藏部分。讀者可能永遠不會展開它，而焦點會意外地將文字展開。
- 用 `ExpandableText` 承載一組帶標題的段落。每一段都應是一列 `Accordion`，以便擁有自己的標題。
- 在即時資訊串流的每次更新中修改 `Text`。每次變化都會重新測量截斷，切換按鈕可能隨文字變化而出現或消失。在讀者有理由查看時再更新文字。

### 常見問題

**為什麼我的文字沒有切換按鈕？** 只有完整文字高於截斷高度時才會出現切換按鈕。如果在目前寬度下文字能放進 `Lines` 行，就沒有需要展開的內容，控制項會直接原樣顯示。請檢查 `Lines`、寬度和字級。

**為什麼切換標籤變化時切換按鈕寬度保持不變？** 兩個標籤共用切換按鈕中的同一個儲存格，因此切換按鈕的寬度與較長的那個標籤一致。寬度恆定，標籤在其中交叉淡化。

**讀者能選取文字嗎？** `ExpandableText` 不能。請使用 `ExpandableContent`，並在其中放一個 `SelectableTextBlock`；截斷、切換按鈕和動態效果保持不變。

**如何得知文字何時展開或收合？** 訂閱 `ExpandedChanged`，它會收到一個 `ExpandableTextExpandedChangedEventArgs`，讀取其中的 `IsExpanded` 即可。或者將 `IsExpanded` 雙向繫結，並監看檢視模型屬性。

**隱藏部分中的連結能否展開文字？** 可以。當文字中的連結在截斷線以下取得鍵盤焦點時，文字會展開。位於截斷線以上、可見的連結不會改變狀態；只有切換按鈕和隱藏連結上的鍵盤焦點會改變狀態。

**文字在頁面下方收合時會怎樣？** 控制項會將文字的開頭重新捲回視野，使讀者不會停留在剛剛收合的文字下方。減少動態效果時，這一操作會立即完成。

## API 參考

<!-- api: ExpandableText -->

### 事件引數

`ExpandableTextExpandedChangedEventArgs` 是 `ExpandedChanged` 的引數。

| 成員 | 意義 |
|---|---|
| `IsExpanded` | 新狀態：文字已展開時為 `True`，已收合時為 `False` |

## 無障礙

- 完整文字始終位於無障礙樹中。螢幕閱讀器無需使用切換按鈕即可讀取全部內容，因此截斷僅是視覺效果。
- 切換按鈕是一個按鈕，並公開展開與收合模式。其名稱為目前的標籤（`MoreLabel` 或 `LessLabel`），狀態改變時名稱隨之變化。
- 截斷部分中的連結可以取得鍵盤焦點。聚焦到位於截斷線以下的隱藏連結會展開文字，因此焦點不會停留在隱藏內容上。
- 切換按鈕支援 Enter 與空白鍵。
- 文字本身不接收焦點。在 Tab 順序中，只有切換按鈕和其中的連結會停留。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 在文字中的連結間移動，顯示切換按鈕時再移到切換按鈕 |
| `Enter` / `Space` | 透過切換按鈕展開或收合文字 |
| 焦點位於截斷線以下的連結上 | 展開文字，使取得焦點的連結可見 |

## 動態效果

- 高度、末行漸隱與箭頭遵循同一個進度值，因此始終同步運動。它們以平緩的曲線緩動收尾，不會過衝。
- 展開約需半秒，收合約需 0.4 秒。距離較長時耗時略長，但有一個較小的上限。
- 文字展開時，末行的漸隱隨之消散。新出現的行穿過柔和的邊緣升起，因此不會露出生硬的行邊。
- 動畫進行中再次點選，會從目前高度反向運動，文字不會跳到任何一端。
- 兩個切換標籤以 4 px 位移與 2 px 模糊交叉淡化，時序與展開一致，切換按鈕的寬度保持不變。
- 減少動態效果時，文字立即展開或收合。當文字在頁面下方收合時，會將文字的開頭重新捲回視野。
- 隨新的資料內容到來的 `IsExpanded` 變化（例如清單重複使用的項目）會立即生效，沒有動態效果，也不會捲動頁面。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 文字展開 | 高度以平緩的緩出曲線增長，末行漸隱消散，箭頭旋轉 | 立即展開 | 立即完成 |
| 文字收合 | 高度約在 0.4 秒內收縮，漸隱回歸，箭頭旋回 | 立即收合，並將文字開頭捲回視野 | 立即完成 |
| 切換標籤 | 「Read more」與「Show less」以 4 px 位移和 2 px 模糊交叉淡化 | 立即完成 | 立即完成 |
| 切換按鈕懸停與按下 | 標籤顏色交叉淡化 | 立即完成 | 立即完成 |

## 相關元件

- `Accordion`：用於多個帶標題的段落，在原地展開。
- `ExpandableCard`：用於帶隱藏詳細資訊的摘要卡片。
- `ScrollArea`：用於必須保持固定高度的內容。

| 元件 | 適用情境 |
|---|---|
| `Accordion` | 多個帶標題的段落在原地展開，每段都有自己的問題 |
| `ExpandableCard` | 摘要卡片需要在隱藏的詳細資訊上方顯示標題與摘要列 |
| `ScrollArea` | 內容必須保持固定高度並可捲動 |
| `Textarea` | 讀者需要編輯文字，而不僅是閱讀 |
| `TextBlock` | 文字本身很短，例如標籤或標題 |
