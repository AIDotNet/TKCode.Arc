---
title: 拖曳數值
summary: Figma 風格的數字欄位：拖曳標籤即可調整，也可以鍵入數值、單位與算術運算式。
source-hash: 6ed53abe71b8
---

## 何時使用

- 帶有位置、尺寸、旋轉與不透明度等數值的屬性檢視器。
- 使用者既需要鍵入精確值、也需要拖曳調整的精簡設定。
- 帶單位且使用者可以更改單位的數值，例如 `px` 或 `%`。

## 何時不用

- 帶微調按鈕的一般表單數字：用 `NumberField`。
- 從範圍中選取、且位置比具體數字更重要的值：用 `Slider`。
- 貨幣金額：用 `MoneyInput`。
- 沒有數值意義的單一文字值：用 `Input`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ScrubInput Label="X" Value="120" Unit="px" />
</UserControl>
```

標籤即為拖曳把手。左右拖曳標籤即可更改數值，點選欄位則可以鍵入。

### 限值與循環

`Minimum` 與 `Maximum` 限制數值範圍。超過限值時，數值會以橡皮筋效果回彈。`IsWrapping` 使數值在限值之間循環；它要求兩個限值都是有限值。

```xml
<arc:ScrubInput Label="寬" Value="320" Minimum="0" Unit="px" ResetValue="320" />
<arc:ScrubInput Label="不透明度" Value="80" Minimum="0" Maximum="100" Unit="%" ResetValue="100" />
```

```csharp
var rotation = new ScrubInput
{
    Label = "旋轉",
    Value = 15,
    Minimum = 0,
    Maximum = 360,
    IsWrapping = true,
    IsTrackVisible = false,
    Unit = "°",
    ResetValue = 0
};
```

`ResetValue` 是連按兩下標籤後還原的值。當兩個限值都是有限值時，`IsTrackVisible` 預設顯示軌道。

### 步進

`Step` 為每次方向鍵的變化量。`FineStep`（Alt）與 `CoarseStep`（Shift）預設分別為 `Step` 的十分之一與十倍。`PixelsPerStep` 設定拖曳多少像素對應一個步進。

```xml
<arc:ScrubInput Label="間距" Value="1.5" Step="0.1" Minimum="0" Unit="em" />
```

### 多個單位

設定多個 `Units` 後，單位會變為一個按鈕，點選即可在各單位之間切換。鍵入單位（例如 `50%`）會切換到該單位。無法辨識的單位會被拒絕。

```csharp
var width = new ScrubInput
{
    Label = "寬度",
    Value = 240,
    Minimum = 0,
    Units = ["px", "%", "em"]
};
```

### 尺寸

`Size` 設定高度：`Small` 為 30px，`Medium`（預設）為 36px。

```xml
<arc:ScrubInput Label="小" Size="Small" Value="12" Unit="px" />
<arc:ScrubInput Label="中" Value="12" Unit="px" />
```

### 繫結與事件

`Value` 為雙向繫結。`ValueChanged` 會在每次變化時觸發，`ValueCommitted` 則在欄位中的一次修改穩定後觸發一次。兩者都會回報舊值、新值以及 `ChangeSource`。

```xml
<arc:ScrubInput Label="不透明度" Value="{Binding Opacity, Mode=TwoWay}" Minimum="0" Maximum="100" Unit="%" />
```

```csharp
amount.ValueCommitted += (_, e) =>
{
    Console.WriteLine($"{e.OldValue} -> {e.NewValue} {e.Unit} ({e.ChangeSource})");
};
```

## API 參考

<!-- api: ScrubInput -->

## 無障礙

- 欄位是一個微調按鈕。其值文字會朗讀數值及單位，百分比與角度也會以相應方式朗讀。
- 標籤是真正的標籤：點選會聚焦欄位並選取其文字。ArrowUp 與 ArrowDown 逐步調整，Enter 確認鍵入的內容，Escape 還原上一個值或取消拖曳。
- 無效輸入會在短時間內將欄位設為無效狀態，並保留舊值。該提示會被播報。
- 單位按鈕命名為「單位：<unit>。變更單位。」

## 動態效果

- 超過限值時，欄位會帶阻力地拉伸，放開後彈回，數字隨拉伸傾斜。
- 無效輸入或在限值處的步進會使欄位抖動一次。
- 單位變更時帶著輕微模糊升起。拖曳時軌道填滿跟隨指標，鍵入或步進時則滑動到新值。
- 減少動態效果時，移除拉伸、抖動與滑動效果。單位原地淡入。關閉動態效果時直接跳到最終狀態。

## 相關元件

- `NumberField`：帶微調按鈕與限值提示的表單數字。
- `Slider`：當範圍中的位置比精確數值更重要時使用。
- `MoneyInput`：貨幣數值。
