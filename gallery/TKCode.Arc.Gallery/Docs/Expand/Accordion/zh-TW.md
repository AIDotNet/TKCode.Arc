---
title: 手風琴
summary: 在原位逐步展示補充資訊。
source-hash: 51581e99173c
---

<Demo name="Default" />

## 何時使用

- 問答清單，例如常見問題（FAQ），同一時間只展開一個答案。
- 以簡短標題分組的長內容，例如設定頁或說明頁。
- 讀者可以跳過、又不會失去頁面位置的補充細節。

## 何時不用

- 單獨一個展開區塊：用 `ExpandableCard`。
- 在同一內容的並列檢視之間切換：用 `Tabs`。
- 只需要「閱讀更多」連結的短段文字：用 `ExpandableText`。

## 用法

每一列是一個 `AccordionItem`。`Header` 是問題，`Content` 是答案。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Accordion>
        <arc:AccordionItem Header="免費方案包含哪些內容？">
            無限專案、三位協作者，以及一週的版本歷程。
        </arc:AccordionItem>
        <arc:AccordionItem Header="之後可以更改方案嗎？">
            可以。方案變更立即生效，差額會按比例計入下一期帳單。
        </arc:AccordionItem>
    </arc:Accordion>
</UserControl>
```

點擊已展開的列會將其收合。展開另一列時，目前展開的列會自動收合。

### 資料驅動的列

設定 `ItemsSource`，問題使用 `ItemTemplate`，答案使用 `ContentTemplate`。`ExpandedIndex = -1` 表示初始時所有列都處於收合狀態。

<Demo name="Large" />

```xml
<arc:Accordion Size="Large"
               ExpandedIndex="-1"
               ItemsSource="{Binding Questions}">
    <arc:Accordion.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Question}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Accordion.ItemTemplate>
    <arc:Accordion.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Answer}" TextWrapping="Wrap" />
        </DataTemplate>
    </arc:Accordion.ContentTemplate>
</arc:Accordion>
```

### 尺寸

`Size` 設定列的尺寸。`Medium` 使用 50 px 高的列，文字為小號；`Large` 使用 76 px 高的列，問題與答案均較大，適合頁面層級的常見問題。

```xml
<arc:Accordion Size="Large">
    <arc:AccordionItem Header="可以隨時取消嗎？">
        月付方案會保持有效，直到目前的計費週期結束。
    </arc:AccordionItem>
</arc:Accordion>
```

### 同時展開多列

`ExpandMode="Multiple"` 允許各列獨立展開。這是 Arc 的附加選項；Arc 預設為 `Single`。

<Demo name="Multiple" />

### 透過程式碼展開某一列

`ExpandedIndex` 為雙向繫結。可以將其繫結到檢視模型屬性，也可以在程式碼中設定。`AccordionItem` 上的 `IsExpanded` 同樣為雙向繫結。

```csharp
accordion.ExpandedIndex = 2;
item.IsExpanded = false;
```

## API 參考

<!-- api: Accordion -->

<!-- api: AccordionItem -->

## 無障礙

- 每一列的觸發器是一個按鈕，遵循展開與收合模式。其展開或收合狀態會被朗讀。
- 每個問題都是一個標題，因此螢幕閱讀器使用者可以在各問題之間快速跳轉。
- 每個答案都由其問題標示。收合的答案會離開無障礙樹，也不會取得焦點。
- 下方向鍵與上方向鍵在觸發器之間移動焦點，到達兩端時循環。Home 與 End 跳到第一個與最後一個觸發器。
- Enter 與空白鍵切換目前焦點所在的列。

## 動態效果

- 面板高度以不過衝的平滑彈簧滑動。展開一列時答案隨之顯現，收合時答案快速隱去。
- 展開時答案會從柔和的模糊中向下滑動數個像素。動畫進行中再次點擊，會從目前位置反向運動。
- 列展開時，折疊箭頭旋轉 180 度。
- 減少與關閉動態效果時，直接以一步到達相同的最終狀態。

## 相關元件

- `ExpandableCard`：用於單一展開區塊，使密集的卡片展開。
- `ExpandableText`：用於將一段長文字截斷為幾行。
- `Tabs`：用於在同一內容的不同檢視之間切換。
