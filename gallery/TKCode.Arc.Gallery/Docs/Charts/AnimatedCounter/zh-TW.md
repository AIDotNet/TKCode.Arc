---
title: 動畫計數器
summary: 讓變動的總數具有清楚的動感。
source-hash: aaf33cd863f2
---

## 何時使用

- 單獨出現、會變動的數字，例如總額、價格或即時計數。
- 首屏統計數字，捲動進入視野時應從零開始計數。
- 儀表板中的總數，需要動態效果表明它已變動，但不需要卡片包覆。

## 何時不用

- 需要卡片、標籤與變化情況的數字：使用 `MetricCard`，它內部即使用本計數器。
- 變動的是文字而非數字：使用 `TextMorph`。
- 作為圖表讀數的數字：圖表的主要數值本身已會捲動顯示。

## 用法

`Value` 接收原始數字，而不是格式化後的字串。`Label` 為數字上方的名稱，`Prefix` 與 `Suffix` 則在數字前後加上文字。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:AnimatedCounter Label="營收"
                         Prefix="$"
                         Value="{Binding Revenue}" />
</UserControl>
```

### 小數與後綴

`Decimals` 固定小數位數，使數字在變動時保持相同的寬度。

```xml
<arc:AnimatedCounter Label="轉換率" Value="{Binding Conversion}" Decimals="2" Suffix="%" />
```

### 更新數值

將 `Value` 設為新的總數。只有發生變動的數位欄會轉動，轉動方向與數字的變動方向一致。

```csharp
revenue.Value += random.Next(100, 4000);
rate.Value = Math.Round(rate.Value + random.NextDouble(), 2);
```

### 進入視野時顯示

`AnimateOnView` 會在計數器首次有 60% 可見時，讓每一位數字從零開始向上滾動。

```xml
<arc:AnimatedCounter Label="首次查看時滾動顯示" Value="{Binding Visitors}" AnimateOnView="True" />
```

### 文化特性

`CultureName` 設定千分位分隔符所用的文化特性。預設值為 `en-US`，因此伺服器端與用戶端的輸出保持一致。

```xml
<arc:AnimatedCounter Label="使用者" Value="{Binding Users}" CultureName="de-DE" />
```

## API 參考

<!-- api: AnimatedCounter -->

## 無障礙

- 計數器不可取得焦點，也不定義鍵盤互動。
- 完整的格式化文字（包括前綴、分隔符、小數與後綴）即為計數器的名稱，因此螢幕閱讀器能一次讀出整個數字。
- 滾動的數位對輔助技術隱藏。計數器本身不會播報變化。若要播報，請在外層元素上設定 `AutomationProperties.LiveSetting="Polite"`。

## 動態效果

- 每一位數字欄都會沿整個數字的變動方向轉動，並在 9 與 0 之間循環。
- 位數變化時，新增的數位欄與分隔符會滑入或滑出，寬度平滑過渡以適應。相鄰內容會隨之輕微移動。
- 使用 `AnimateOnView` 時，計數器有 60% 可見後開始滾動，各位數字以輕微的錯落依序開始。
- 減少動態效果時直接替換為新值，不做滾動。關閉動態效果時直接跳到最終狀態。

## 相關元件

- `MetricCard`：卡片內的數字，帶有標籤與變化情況。
- `Sparkline`：帶趨勢的數字。
- `BarChart` 與 `LineChart` 會更新各自的主要數值，因此無需使用本計數器。
- `TextMorph`：用於變動的文字而非數字。
