---
title: 捲動區域
summary: 原生捲動容器，配有細長的浮層捲軸與僅在內容溢出時才出現的邊緣漸隱。
source-hash: 3a151cda56de
---

## 何時使用

- 面板、側邊欄或彈出層中可能溢出的內容，同時需要保持頁面版面固定。
- 透過滾輪橫向捲動、並吸附到各項目的卡片或標籤列。
- 需要判斷某一邊緣之外是否還有內容的區域，例如用於載入更多項目。

## 何時不用

- 逐項翻頁瀏覽：用 `Carousel`。
- 包裹整個頁面：應由頁面或 `ScrollViewer` 負責捲動。
- 網格中的大量資料：用支援列虛擬化的表格控制項。

## 用法

用 `Orientation` 設定捲動軸向，預設為 `Vertical`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ScrollArea Width="360"
                    Height="260"
                    Label="版本說明"
                    CornerRadius="18">
        <StackPanel Spacing="0">
            <TextBlock Text="v2.4 · 浮層捲軸" Margin="16,12" />
            <TextBlock Text="v2.3 · 更快的冷啟動" Margin="16,12" />
            <TextBlock Text="v2.2 · 鍵盤快速鍵" Margin="16,12" />
        </StackPanel>
    </arc:ScrollArea>
</UserControl>
```

`Label` 為捲動區域命名。有名稱的區域會被朗讀為一個區域。

### 橫向條帶

垂直滾輪在橫向區域還能繼續捲動時會使其橫向捲動，捲到盡頭後再把滾輪交還給頁面。設定 `HorizontalSnapPointsType` 可讓各項目吸附對齊。

```xml
<arc:ScrollArea Orientation="Horizontal"
                HorizontalSnapPointsType="Mandatory"
                Label="卡片">
    <StackPanel Orientation="Horizontal" Spacing="12" Margin="16">
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
        <Border Width="160" Height="100" CornerRadius="18" Background="#F4F4F5" />
    </StackPanel>
</arc:ScrollArea>
```

### 雙向捲動與捲軸可見性

網格需要雙向捲動時使用 `Both`。`OverlayScrollbarMode="Always"` 會在內容溢出時持續顯示捲軸。預設的 `Auto` 僅在捲動或懸停時顯示。

```xml
<arc:ScrollArea Orientation="Both"
                OverlayScrollbarMode="Always"
                Height="240" />
```

### 邊緣漸隱與隱藏延遲

`FadeLength` 以像素為單位設定每個邊緣漸隱的長度（預設 28）。設為 `0` 可關閉漸隱。`HideDelay` 設定捲動停止後捲軸保持可見的時間（預設 900 ms）。

```xml
<arc:ScrollArea FadeLength="0"
                HideDelay="0:0:1.5" />
```

### 回應邊緣變化

只有當某一邊緣在「有內容」與「無內容」之間切換時才會觸發 `EdgesChanged`。`Edges` 屬性保存目前的狀態。

```csharp
area.EdgesChanged += (_, e) =>
{
    if (!e.Edges.Bottom)
    {
        LoadMoreItems();
    }
};
```

### 透過程式碼捲動

`ScrollBy` 與 `ScrollToOffset` 用於捲動區域。在完整動態效果下它們以彈簧動畫捲動；在減少或關閉動態效果時則直接跳到目標位置。

```csharp
area.ScrollBy(new Vector(0, 240));
area.ScrollToOffset(new Vector(0, 0));
```

## API 參考

<!-- api: ScrollArea -->

## 無障礙

- `Label` 設定區域的無障礙名稱。沒有標籤時，周圍的內容必須說明該區域的用途。
- 該區域可取得焦點。方向鍵、Page Up 與 Page Down、Home、End 及空白鍵均可原生地捲動它。
- 捲動仍保持 `ScrollViewer` 的自動化模式，浮層捲軸僅供視覺呈現。
- 減少動態效果時，軌道翻頁直接跳轉，不做動畫。

## 動態效果

- 內容捲動或指標懸停在區域上時，捲軸淡入；捲動停止 900 ms 後淡出。
- 指標懸停時滑塊會變粗，並且可以拖曳。
- 每個邊緣漸隱隨捲動的最初幾個像素逐步增強，且長度永遠不超過被隱藏的內容。
- 減少動態效果時取消捲軸的淡入淡出，軌道翻頁也改為瞬間完成。

## 相關元件

- `Carousel` 逐項翻頁瀏覽。
- `ExpandableText` 將一段文字截斷為幾行，而不是捲動顯示。
- `ResizablePanels` 在窗格之間分配空間；當某個窗格可能溢出時，在該窗格內放置 `ScrollArea`。
