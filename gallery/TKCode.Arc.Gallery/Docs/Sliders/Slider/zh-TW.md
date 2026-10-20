---
title: 滑桿
summary: 在軌道上選取一個值或一個範圍，跟隨指標移動。
source-hash: edb1397c9a48
---

## 何時使用

- 近似值，位置比精確數字更重要，例如音量、透明度或價格區間。
- 帶兩個滑桿的範圍選擇，帶有標註刻度與格式化讀數。
- 使用者在軌道上觀察時數值會變動的情境，例如播放進度或亮度。

## 何時不用

- 使用者需要鍵入的精確數字：用 `NumberField`。
- 使用者無法變更的值：用 `Progress`。
- 使用者為了手感拖曳的單一設定，且軌道會越過兩端拉伸：用 `ElasticSlider`。

## 用法

基本用法是單一數值。`Value` 是滑桿在軌道上的位置，`Minimum` 與 `Maximum` 設定其範圍。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Slider Label="音量"
                Value="64"
                Width="360" />
</UserControl>
```

### 起始與結束內容

`StartContent` 與 `EndContent` 分別位於軌道之前與之後，可放置圖示或靜音按鈕。

```xml
<arc:Slider Label="音量"
            Value="64"
            Width="360">
    <arc:Slider.StartContent>
        <TextBlock Text="-" />
    </arc:Slider.StartContent>
    <arc:Slider.EndContent>
        <TextBlock Text="+" />
    </arc:Slider.EndContent>
</arc:Slider>
```

### 範圍

將 `IsRange` 設為 `True` 即可顯示兩個滑桿。透過 `LowerValue` 與 `UpperValue` 讀寫兩端的值，並用 `MinStepsBetweenThumbs` 保持兩個滑桿之間的間距。`ValueFormatter` 用於格式化讀數、氣泡與朗讀的數值。

```xml
<arc:Slider Label="價格"
            IsRange="True"
            Minimum="0"
            Maximum="1000"
            SmallChange="10"
            LowerValue="{Binding MinPrice, Mode=TwoWay}"
            UpperValue="{Binding MaxPrice, Mode=TwoWay}"
            MinStepsBetweenThumbs="5"
            LowerThumbName="最低價格"
            UpperThumbName="最高價格"
            ValueFormatter="{Binding PriceFormatter}" />
```

### 步長與刻度

`SmallChange` 是數值貼齊的步長。`Marks` 會在軌道上加入刻度。帶標籤的刻度會在軌道下方顯示標籤，點按標籤會將最近的滑桿移到該位置。

```csharp
var speed = new Slider
{
    Label = "播放速度",
    Minimum = 0.5,
    Maximum = 2,
    SmallChange = 0.25,
    Value = 1,
    ValueFormatter = v => $"{v:0.##}×"
};
speed.Marks.Add(new SliderMark(0.5, "0.5×"));
speed.Marks.Add(new SliderMark(1, "1×"));
speed.Marks.Add(new SliderMark(2, "2×"));
speed.Marks.Add(1.5);
```

### 事件

`ValueCommitted` 會在每次放開時、每次按鍵使數值改變時，以及每次點按刻度標籤時觸發一次。將耗時作業（例如網路要求）放在此事件中執行。`RangeChanged` 在範圍被拖曳或以鍵盤變更時，每一步都會觸發。

```csharp
price.ValueCommitted += (_, e) => SavePriceFilter(e.LowerValue, e.UpperValue);
price.RangeChanged += (_, e) => committed.Text = $"${e.LowerValue:N0} – ${e.UpperValue:N0}";
```

## API 參考

<!-- api: Slider -->

<!-- api: SliderMark -->

## 無障礙

- `Label` 是滑桿的名稱。在範圍模式下，每個滑桿的名稱來自 `LowerThumbName` 與 `UpperThumbName`，若未設定，則由 `Label` 加上「最小」與「最大」組成。
- 每個滑桿都會以文字回報其數值，因此朗讀的數值遵循 `ValueFormatter`。
- 方向鍵依 `SmallChange` 步進；按住 Shift 加方向鍵、PageUp 或 PageDown 可進行大步長調整；Home 與 End 分別移到允許的最小值與最大值。
- 讀數、氣泡與刻度標籤對輔助技術隱藏。點按刻度是指標操作的捷徑，同樣的數值也能以鍵盤到達。
- 焦點框在按鍵後出現，而不是在指標按下後出現。

## 動態效果

- 滑桿與指標一比一跟隨。放開時，它會貼齊到最近的步長，並保留拖曳的慣性，因此快速輕甩可能多移動一步。
- 按下軌道時，最近的滑桿會以彈簧動畫移向指標。在極限值處按鍵時，滑桿會朝按下的方向拉伸，然後彈回。
- 數值氣泡會在作用中的滑桿上方彈出，數值變動時數字逐位滾動，放開後保留 700 毫秒。被拖曳的滑桿會略微放大。
- 減少動態效果時，取消慣性、拉伸與數字滾動。滑桿直接移到目標值，氣泡僅淡入淡出。關閉動態效果時直接跳到最終狀態。

## 相關元件

- `ElasticSlider`：用於單一設定，可越過兩端拉伸並卡入刻度檔位。
- `NumberField`：使用者需要鍵入精確數字時使用。
- `Progress`：用於顯示但不由使用者變更的數值。
