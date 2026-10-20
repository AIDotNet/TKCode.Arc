---
title: 指標卡
summary: 為需要上下文說明的數字提供精簡的摘要。
source-hash: 10afd81f27bc
---

## 何時使用

- 單一數字，搭配標籤與說明它的上下文，例如可用率、訂單量或延遲。
- 會即時更新的數字，數字位會朝變化的方向滾動。
- 需要在上下文旁顯示變化標籤（例如「+12%」）的數字。

## 何時不用

- 趨勢比最新數值更重要：用 `Sparkline`。
- 不需要卡片外框的數字，例如內文中的數據：用 `AnimatedCounter`。
- 帶有媒體、擁有者與操作的卡片：用 `Card`。

## 用法

`Label`、`Value` 與 `Context` 是常用部分。`Change` 為選填；不設定時變化標籤隱藏。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MetricCard Label="可用率"
                    Value="99.98"
                    Decimals="2"
                    Suffix="%"
                    Context="近 30 天"
                    Change="+0.02%"
                    Width="280" />
</UserControl>
```

### 變化標籤

以「+」開頭的 `Change` 顯示為上升，以「-」或「−」開頭的顯示為下降。可透過 `ChangeDirection` 讀回方向。設定 `IsChangeGlyphVisible` 可在標籤內加入趨勢圖示。

```xml
<arc:MetricCard Label="p95 延遲"
                Value="182"
                Suffix=" ms"
                Context="對比昨天"
                Change="−8%"
                IsChangeGlyphVisible="True"
                Width="280" />
```

### 資料繫結

將數值與文字繫結到檢視模型屬性。每次更新時，數字都會滾動到新值。

```xml
<arc:MetricCard Label="訂單量"
                Value="{Binding OrderCount}"
                Context="{Binding OrderContext}"
                Change="{Binding OrderChange}"
                Width="280" />
```

### 趨勢線

`Sparkline` 接收一組數值，在數字旁繪製一條小趨勢線。卡片首次出現時繪製一次，資料變化時重新繪製。

```csharp
orders.Sparkline = new List<double> { 820, 910, 870, 990, 1040, 1010, 1204 };
```

### 窄視窗

當視窗寬度為 380px 或更窄時，卡片使用更緊湊的內距，並將變化標籤移到標籤下方。此行為不需要任何程式碼。

## API 參考

<!-- api: MetricCard -->

## 無障礙

- 卡片是以 `Label` 命名的群組。其說明文字為變化與上下文。
- 數字由計數器朗讀。變化標籤為純文字，並以「+」或「−」表明方向，因此方向不依賴顏色。
- 數值變化本身不會被播報。如需播報，請在卡片或其外層容器上設定 `AutomationProperties.LiveSetting`。

## 動態效果

- 數字在卡片首次捲入檢視時從零開始滾動，之後逐步轉向每個新值。
- 標籤、上下文與變化標籤的文字從數字移動的方向滾入。
- 變化標籤的寬度會透過彈簧適應新文字，標籤以縮放與淡入淡出的方式出現或消失。
- 趨勢線首次顯示時在 0.9s 內繪製完成，資料變化時交叉淡化。
- 減少動態效果時數字直接跳到新值，文字淡入；變化標籤與趨勢線的變化不帶動畫。關閉動態效果時所有變化立即生效。

## 相關元件

- `AnimatedCounter`：不需要卡片外框的單獨數字。
- `Sparkline`：趨勢比最新數值更重要時使用。
- `Card`：圍繞數字的一組內容與操作。
