---
title: 彈性滑桿
summary: 滑桿越過兩端時如橡皮筋般拉伸，附帶搖擺的數值標籤，以及滑桿會卡入的檔位。
source-hash: 295f9c37751d
---

## 何時使用

- 使用者憑手感調整的單一設定，例如音量、亮度或透明度。
- 軌道需要有物理感的媒體與創作工具。
- 在有實際意義的數值（例如 0、50 與 100）上設定檔位，滑桿會卡入這些位置。

## 何時不用

- 安靜的表單或設定頁面：用 `Slider`。
- 帶兩個滑桿的範圍選擇：用 `Slider`，它支援 `IsRange`。
- 使用者需要鍵入的精確數字：用 `NumberField`。

## 用法

基本用法是單一數值。`Value` 是滑桿在軌道上的位置，`Minimum` 與 `Maximum` 設定其範圍。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ElasticSlider Label="音量"
                       Value="40"
                       Width="360"
                       ValueFormatter="{Binding VolumeFormatter}" />
</UserControl>
```

### 圖示

`StartIcon` 與 `EndIcon` 分別位於軌道之前與之後。當軌道被拉過某一端時，該端的圖示會隨之向外移動。

```xml
<arc:ElasticSlider Label="亮度"
                   Value="50"
                   Width="360">
    <arc:ElasticSlider.StartIcon>
        <TextBlock Text="-" />
    </arc:ElasticSlider.StartIcon>
    <arc:ElasticSlider.EndIcon>
        <TextBlock Text="+" />
    </arc:ElasticSlider.EndIcon>
</arc:ElasticSlider>
```

### 檔位

將 `SliderMark` 值加入 `Marks`。被拖曳的滑桿會卡入每一個刻度。帶標籤的刻度會在軌道下方顯示標籤，點按標籤會將滑桿移到該位置。

```csharp
var brightness = new ElasticSlider
{
    Label = "亮度",
    Value = 50,
    Width = 360
};
brightness.Marks.Add(new SliderMark(0, "關閉"));
brightness.Marks.Add(new SliderMark(50, "50"));
brightness.Marks.Add(new SliderMark(100, "最大"));
```

### 步長與格式

`SmallChange` 是數值貼齊的步長。`ValueFormatter` 用於格式化數值標籤、讀數與朗讀的數值。`IsValueVisible` 可隱藏標籤旁的讀數。拖曳滑桿或以按鍵變更時，數值標籤仍會顯示。

```xml
<arc:ElasticSlider Label="縮放"
                   Minimum="0.5"
                   Maximum="3"
                   SmallChange="0.1"
                   Value="1"
                   Width="360"
                   ValueFormatter="{Binding ZoomFormatter}" />
```

### 事件

`ValueCommitted` 會在每次放開時、每次按鍵使數值改變時，以及每次點按刻度標籤時觸發一次。將耗時作業（例如網路要求）放在此事件中執行。

```csharp
volume.ValueCommitted += (_, e) => SaveVolume(e.Value);
```

## API 參考

<!-- api: ElasticSlider -->

<!-- api: SliderMark -->

## 無障礙

- `Label` 是滑桿的名稱。朗讀的數值遵循 `ValueFormatter`。
- 方向鍵依 `SmallChange` 步進；按住 Shift 加方向鍵、PageUp 或 PageDown 可進行大步長調整；Home 與 End 分別移到最小值與最大值。
- 在極限值處按鍵時，軌道會回彈，數值不會改變。
- 數值標籤與檔位的彈出效果僅為視覺呈現。點按刻度標籤是指標操作的捷徑，同樣的數值也能以鍵盤到達。
- 焦點框在按鍵後出現，而不是在指標按下後出現。停用的滑桿會移出 Tab 順序。

## 動態效果

- 越過任一端時，軌道如橡皮筋般拉伸，並依放開時的速度彈回。按住時，滑桿會略微膨脹。
- 數值標籤懸掛在滑桿上，並隨其速度搖擺，因此快速拖曳時標籤傾斜得更明顯。
- 滑桿卡入檔位時，軌道上的刻度會彈出一次。
- 減少動態效果時，取消拉伸、搖擺與檔位彈出。滑桿直接移到目標值，標籤僅淡入淡出。關閉動態效果時直接跳到最終狀態。

## 相關元件

- `Slider`：用於安靜的表單控制項，或帶兩個滑桿的範圍選擇。
- `NumberField`：使用者需要鍵入精確數字時使用。
- `Progress`：用於顯示但不由使用者變更的數值。
