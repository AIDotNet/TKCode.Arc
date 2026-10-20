---
title: 活動熱圖
summary: 一眼看清整年的活動，每天一個方格。
source-hash: ad0de977d26e
---

## 何時使用

- 長時間範圍內的每日節奏，例如一年中的貢獻或訓練紀錄。
- 連續性與空檔比精確比較更重要：連續天數與安靜的週才是重點。
- 日曆形態的總覽，每一天都是一個可讀取、可選取的方格。

## 何時不用

- 需要精確比較各天數值：用 `BarChart`。
- 為輸入挑選日期：用 `Calendar`。
- 觀察某個數值隨時間的趨勢：用 `LineChart`。

## 用法

每個 `ActivityDay` 是一個方格，包含 `Date` 與 `Count`。缺少的日期視為零。`Period` 用來補全摘要，例如「2025 年 1,284 次貢獻」。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ActivityHeatmap Label="2025 年的貢獻"
                         Period="2025"
                         ItemsSource="{Binding Days}" />
</UserControl>
```

### 年份與控制項

切換年份時，請同時取代 `ItemsSource`、`Period` 與 `Label`。`Actions` 會把控制項放在摘要旁邊，例如年份切換按鈕。格線會以掃過的方式重新著色，而不是整體重繪。

```xml
<arc:ActivityHeatmap Label="2025 年的貢獻" Period="2025" ItemsSource="{Binding Days}">
    <arc:ActivityHeatmap.Actions>
        <StackPanel Orientation="Horizontal" Spacing="4">
            <arc:Button Content="2024" Variant="Ghost" Size="Small" Command="{Binding ShowPrevious}" />
            <arc:Button Content="2025" Variant="Ghost" Size="Small" Command="{Binding ShowCurrent}" />
        </StackPanel>
    </arc:ActivityHeatmap.Actions>
</arc:ActivityHeatmap>
```

### 單位與週

`UnitOne` 與 `UnitOther` 設定計數的單位名詞。`FirstDayOfWeek` 設定每個週欄的第一列，預設為週日。`Culture` 用於格式化日期與數字。

```xml
<arc:ActivityHeatmap Label="訓練次數，最近六個月"
                     Period="最近六個月"
                     UnitOne="次訓練"
                     UnitOther="次訓練"
                     FirstDayOfWeek="Monday"
                     MaxWidth="520"
                     ItemsSource="{Binding Workouts}" />
```

### 閾值

`Thresholds` 保存第一到第三級的上限。超過最後一個上限的計數為第四級。預設情況下，上限取最忙那一天的四分之一。

```csharp
workouts.Thresholds = [1, 2, 3];
```

### 選取某一天

`SelectedDate` 為目前選取的日期，支援雙向繫結。當使用者以點擊、Enter 或 Space 選取某天時，`DateSelected` 會回報。

```csharp
heatmap.DateSelected += (_, e) => selected.Text =
    $"已選取 {e.Date:MMM d, yyyy}：{e.Count} 次貢獻（第 {e.Level} 級）";
```

## API 參考

<!-- api: ActivityHeatmap -->

<!-- api: ActivityDay -->

## 無障礙

- `Label` 為格線命名，例如「2025 年的貢獻」。摘要會以總數與期間補全名稱。
- 上下方向鍵每次移動一天，左右方向鍵每次移動一週。Home 與 End 分別跳到第一天與最後一天，Enter 或 Space 選取焦點所在的日期。
- Esc 會隱藏提示。單一提示框會在各天之間滑動，而不是為每個方格重複顯示。
- 每一天都是一個具名的格線儲存格。儲存格作為圖形對輔助技術隱藏，資訊由摘要與格線結構承載。
- 總數會透過狀態區域播報，指標或焦點所在的日期會以禮貌方式播報。圖例是一排切換鈕，每個級別一個，因此級別不只靠顏色辨識。

## 動態效果

- 首次顯示時，方格會依序波浪式進場一次，總時長有上限，因此大型格線仍能保持迅速。
- 切換年份或範圍時，格線會以對角掃過的方式重新著色，方格依欄位錯開進行。
- 提示框以靈敏的彈簧在方格之間滑動，文字滾動切換到新的日期。
- 總數以平滑彈簧計數到新值。
- 減少動態效果時，移除波浪、掃過與滑動效果，改用 160 毫秒的短淡入。關閉動態效果時直接到最終狀態。

## 相關元件

- `BarChart`：用於精確比較不同期間。
- `Calendar`：用於選取日期。
- `LineChart`：用於觀察數值隨時間的趨勢。
- `Sparkline`：用於數值旁邊的精簡趨勢。
