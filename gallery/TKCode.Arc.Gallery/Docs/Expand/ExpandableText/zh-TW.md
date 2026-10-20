---
title: 可展開文字
summary: 長文字截斷為幾行，底部帶柔和漸隱；點擊「閱讀更多」平滑展開，點擊「收合」折回。僅在文字溢出時顯示切換按鈕。
source-hash: bc3ff6e10d1b
---

## 何時使用

- 留言、評價或個人簡介，若全文顯示會讓清單中的卡片高度參差不齊。
- 固定寬度欄位中的長文字，讀者可以選擇是否閱讀其餘部分。
- 閱讀全文並非必要的文字，例如商品描述。

## 何時不用

- 必須完整顯示的文字，例如法律條款或錯誤詳情。不要截斷它們。
- 一組帶標題的段落或問題：用 `Accordion`。
- 隱藏在摘要之後的結構化內容：用 `ExpandableCard`。
- 需要保持固定高度並帶捲軸的文字：用 `ScrollArea`。

## 用法

用 `Text` 設定內容。文字會被截斷為 `Lines` 行，預設為 3 行。只有當文字超出截斷範圍時，切換按鈕才會出現。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandableText Text="我們在一個下午內就把整個設計團隊遷移到新的工作區。匯入過程保留了每一則留言與每個版本。"
                        MaxWidth="520"
                        HorizontalAlignment="Left" />
</UserControl>
```

### 行數

`Lines` 設定文字收合時顯示的行數。留言等高密度清單使用 2 行；個人簡介與商品描述使用 3 或 4 行。

```xml
<arc:ExpandableText Lines="2"
                    Text="{Binding CommentText}"
                    MaxWidth="420" />
```

### 標籤

`MoreLabel` 與 `LessLabel` 設定切換按鈕的文字。預設值為 `閱讀更多` 和 `收合`。

```xml
<arc:ExpandableText MoreLabel="查看完整簡介"
                    LessLabel="收合"
                    Lines="4"
                    Text="{Binding Bio}" />
```

### 繫結展開狀態

`IsExpanded` 為雙向繫結。可以將其繫結到檢視模型屬性，也可以在程式碼中設定。文字展開或收合時會觸發 `ExpandedChanged`。

```xml
<arc:ExpandableText Text="{Binding Bio}"
                    IsExpanded="{Binding BioExpanded, Mode=TwoWay}" />
```

```csharp
bio.ExpandedChanged += (_, e) =>
{
    state.Text = e.IsExpanded ? "已展開" : "已收合";
};

bio.IsExpanded = true;
```

## API 參考

<!-- api: ExpandableText -->

## 無障礙

- 完整文字始終位於無障礙樹中。螢幕閱讀器無需使用切換按鈕即可讀取全部內容。
- 切換按鈕是一個按鈕，遵循展開與收合模式。其名稱為目前的標籤，即 `MoreLabel` 或 `LessLabel`。
- 截斷部分中的連結可以透過鍵盤取得焦點。聚焦到位於截斷線以下的隱藏連結時，文字會展開。
- 切換按鈕支援 Enter 與空白鍵。

## 動態效果

- 高度、末行的漸隱與箭頭沿同一條曲線一起運動。它們緩動收尾，不會過衝。
- 展開約需半秒，收合約需 0.4 秒。距離較長時耗時略長。
- 文字展開時，末行的漸隱隨之消散。新出現的行穿過柔和的邊緣升起。
- 動畫進行中再次點擊，會從目前高度反向運動。
- 減少動態效果時，文字立即展開或收合。當文字在頁面下方收合時，會將文字開頭重新捲回視野。

## 相關元件

- `Accordion`：用於多個帶標題的段落，在原地展開。
- `ExpandableCard`：用於帶隱藏詳細資訊的摘要卡片。
- `ScrollArea`：用於必須保持固定高度的內容。
