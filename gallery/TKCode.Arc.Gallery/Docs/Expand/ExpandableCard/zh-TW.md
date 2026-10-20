---
title: 可展開卡片
summary: 在需要時為密集的卡片提供更多空間。
source-hash: d64e7cfac845
---

## 何時使用

- 摘要卡片，例如方案、訂單或記錄項目，其詳細資訊有用但並非總是需要。
- 展開時既需要變高也需要變寬，以便容納詳細資訊的卡片。
- 摘要列會隨時間變化的狀態卡片，例如配送狀態。

## 何時不用

- 多個互斥的區塊：用 `Accordion`。
- 沒有可展開詳細資訊的卡片：用 `Card`。
- 在同一位置瀏覽多張卡片：用 `CardStack`。

## 用法

`Header` 是卡片標題，`Description` 是其下方的摘要列，`Content` 存放詳細資訊。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
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

未設定寬度時，卡片填滿其容器。

### 橫向展開

`ExpandedWidth` 限制卡片展開時的寬度上限。卡片會以與高度相同的彈簧，從 `CollapsedWidth` 增長到 `ExpandedWidth`。未設定 `ExpandedWidth` 時，展開後的卡片使用 `CollapsedWidth`。

```xml
<arc:ExpandableCard Header="訂單 #4821"
                    Description="已出貨 · 週四送達"
                    CollapsedWidth="360"
                    ExpandedWidth="520">
    <TextBlock Text="2 × 亞麻襯衫，1 × 帆布托特包" />
</arc:ExpandableCard>
```

### 預設展開

`IsExpanded` 為雙向繫結。設為 `True` 即可預設展開，也可以繫結到檢視模型屬性。

```xml
<arc:ExpandableCard Header="部署失敗"
                    Description="api-gateway · 2 分鐘前"
                    IsExpanded="True">
    <TextBlock Text="/ready 的健康檢查在 30 秒後逾時。" TextWrapping="Wrap" />
</arc:ExpandableCard>
```

### 更新摘要

在程式碼中修改 `Description`。只有發生變化的詞會滾入，其餘部分保持不動。

```csharp
order.Description = "運送中 · 今天送達";
```

### 事件

卡片展開或收合之後會觸發 `ExpandedChanged`。

```csharp
card.ExpandedChanged += (_, _) =>
{
    logger.Info($"方案詳情已展開：{card.IsExpanded}");
};
```

## API 參考

<!-- api: ExpandableCard -->

## 無障礙

- 標題是一個按鈕，遵循展開與收合模式。其展開或收合狀態會被朗讀。
- 卡片的名稱即 `Header`。`Description` 作為標題按鈕的說明文字對外提供。
- Enter 與空白鍵可在標題上切換卡片的展開狀態。
- 焦點位於卡片內部時，Escape 會收合卡片，並將焦點返回到標題。
- 卡片收合時詳細內容處於惰性狀態，其中的控制項無法取得焦點。

## 動態效果

- 展開是一個連續的動作：寬度與高度以不過衝的平滑彈簧一同增長，折疊箭頭隨之轉動。
- 卡片騰出空間後詳細內容才淡入。收合時，詳細內容先淡出，卡片隨後收縮。
- 摘要中變化的詞從下方升起，舊詞向上飄離並淡出。
- 動畫進行中再次點擊，會從目前尺寸反向運動。
- 減少動態效果時直接套用最終狀態，摘要文字無動畫地替換。

## 相關元件

- `Accordion`：用於一組互斥的區塊清單。
- `Card`：用於沒有可展開詳細資訊的卡片。
- `ExpandableText`：用於在原地展開的長段文字。
