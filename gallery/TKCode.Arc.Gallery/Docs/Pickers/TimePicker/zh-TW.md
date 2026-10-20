---
title: 時間選擇器
summary: 以合宜的鍵盤行為選取時間。
source-hash: aa3abfbf3231
---

## 何時使用

- 從等距選項中選取一天中的時間，例如會議或提醒。
- 以 `TimeOnly` 值儲存、並依使用者地區的時鐘格式顯示的時間。
- 與 `DatePicker` 搭配，組成日期與時間的表單欄位。

## 何時不用

- 少數有名稱的時段，如「上午」或「晚上」：用 `Select`。
- 日期：用 `DatePicker`；停留在頁面上的月份用 `Calendar`。
- 日期範圍：用 `DateRangePicker`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:TimePicker Label="會議時間"
                    Description="每 15 分鐘一個時間點。"
                    SelectedTime="{Binding MeetingTime, Mode=TwoWay}" />
</UserControl>
```

### 間隔與時鐘

`MinuteStep` 設定選項之間的間隔，預設為 15 分鐘。`ClockFormat` 選擇顯示格式：`TwelveHour` 顯示 "9:15 AM"，`TwentyFourHour` 顯示 "09:15"，`Auto` 跟隨 `Culture` 的短時間格式。無論顯示格式為何，值一律是 `TimeOnly`。

```xml
<arc:TimePicker Label="每 5 分鐘" MinuteStep="5" />
<arc:TimePicker Label="24 小時制" MinuteStep="30" ClockFormat="TwentyFourHour" />
```

### 空值

`SelectedTime` 是 `TimeOnly?`。為 `null` 時，欄位會顯示 `Placeholder`。

```xml
<arc:TimePicker Label="提醒" Placeholder="選取時間" />
```

```csharp
picker.SelectedTime = new TimeOnly(18, 30);
picker.SelectedTimeChanged += (_, e) =>
{
    status.Text = e.NewTime is { } time ? $"已設為 {time:HH:mm}" : "無時間";
};
```

### 尺寸與狀態

`Size` 設定欄位高度：`Small` 為 36，`Medium` 為 44（Arc 的預設外觀），`Large` 為 50。非空的 `ErrorMessage` 會將欄位標記為無效。`IsEnabled="False"` 會停用它。

```xml
<arc:TimePicker Label="小" Size="Small" />
<arc:TimePicker Label="入住" ErrorMessage="入住時間從下午 3:00 開始。" />
<arc:TimePicker Label="已鎖定" IsEnabled="False" />
```

## API 參考

<!-- api: TimePicker -->

## 無障礙

- `Label` 為欄位命名。`Description` 與 `ErrorMessage` 是說明文字。
- 清單開啟時，焦點保留在觸發元件上。觸發元件會回報其展開狀態與目前的值，因此清單變動時，螢幕閱讀器會播報目前的時間。
- Enter、Space 或 ArrowDown 開啟清單，所選時間位於中央並處於作用中狀態。ArrowUp 與 ArrowDown 在各個時間之間移動，並在兩端循環。
- Escape 關閉清單。在清單外按下指標也會將其關閉。
- 觸發元件在鍵盤焦點下會顯示焦點框。

## 動態效果

- 數值依時間方向滾動：較晚的時間從下方升起，較早的時間從上方落下。
- 清單以短促的彈簧從觸發元件展開並淡入，關閉時幾乎立即完成。
- 作用中的列會快速變換背景，一個小圓點標示所選時間，因此同一時間只有一列會反白。
- 減少動態效果時，數值只做交叉淡化，清單也僅淡入淡出。關閉動態效果時直接到最終狀態。

## 相關元件

- `DatePicker`：將日期與時間配對。
- `Select`：少數有名稱的選項。
- `DateRangePicker`：日期範圍。
