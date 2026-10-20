---
title: 顏色選擇器
summary: 色塊展開為選擇器，支援格式切換、滴管、已儲存色塊與對比度讀數。
source-hash: d1062b7bf35e
---

## 何時使用

- 主題編輯器、品牌設定或設計工具中，需要以 HEX、RGB、HSL 或 OKLCH 輸入顏色的場景。
- 帶透明度的顏色，其中 Alpha 通道是值的一部分。
- 需要與背景做對比檢查，並顯示 WCAG 對比度讀數的顏色。
- 使用者逐步累積的一小組已儲存顏色。

## 何時不用

- 少量固定顏色：用 `SegmentedControl` 或 `RadioGroup`。
- 沒有選擇器的一般 HEX 文字值：用 `Input`。
- 用於篩選清單的小型預設顏色標籤：用 `ChipGroup`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ColorPicker Label="強調色"
                     Color="#2F6BFF"
                     Format="Hex" />
</UserControl>
```

### 值與格式

`Color` 是 Avalonia 的 `Color`，且為雙向繫結。`Format` 設定文字欄位中顯示的格式，使用者也可以在面板中切換。`HexValue` 為唯讀，以 CSS 使用的順序給出 `#RRGGBB` 或 `#RRGGBBAA` 形式的顏色。

```xml
<arc:ColorPicker Label="品牌色" Color="#E5484D" Format="Oklch" />
```

```csharp
picker.ColorChanged += (_, e) => status.Text = $"顏色：{e.Hex}";
```

### 已儲存色塊

`Swatches` 是一組 `ColorSwatch` 記錄。色塊從面板中儲存，每次變更都會建立新的清單，因此請繫結集合或處理 `SwatchesChanged`。`MaxSwatches` 預設為 7；清單已滿時，最早的色塊會被移除。

```csharp
picker.Swatches =
[
    ColorSwatch.Create(Color.Parse("#E5484D")),
    ColorSwatch.Create(Color.Parse("#30A46C")),
    ColorSwatch.Create(Color.Parse("#0090FF")),
];
picker.MaxSwatches = 7;
```

### 對比度讀數

`ContrastBackground` 是 WCAG 讀數用來與目前值比較的顏色。面板會顯示對比度比值與等級。

```xml
<arc:ColorPicker Label="深色背景上的文字"
                 Color="#A1A1AA"
                 ContrastBackground="#111113"
                 Description="對比度讀數以 #111113 進行比較。" />
```

### 錯誤與停用狀態

`ErrorMessage` 不為空時欄位會被標示為無效。`IsEnabled="False"` 會停用選擇器。

```xml
<arc:ColorPicker Label="已鎖定" IsEnabled="False" />
```

## API 參考

<!-- api: ColorPicker -->

<!-- api: ColorSwatch -->

## 無障礙

- `Label` 為觸發器命名，面板以該標籤命名。觸發器會公開其展開狀態。
- 飽和度與明度區域是一個二維滑桿，其值同時回報飽和度與明度。色相與不透明度滑桿分別回報角度與百分比。
- 方向鍵移動把手，按住 Shift 或使用 PageUp 與 PageDown 則以十為步幅移動。Home 與 End 分別移到最小值與最大值。
- 在文字欄位中，Enter 套用該值。無效值會使欄位抖動，並隨錯誤訊息一併播報。Escape 先捨棄草稿，再次按下 Escape 則關閉面板並將焦點返回觸發器。
- 對比度比值及其等級以文字形式提供。已儲存色塊組成一個清單，其中 Delete 刪除選取的色塊，Alt 加方向鍵可移動它。

## 動態效果

- 面板從色塊中展開：裁剪區域從觸發器的角落生長為完整面板，陰影隨裁剪區域變化。
- 把手以快速彈簧跟隨指標，拖曳時會收緊。
- 格式按鈕使文字在 HEX、RGB、HSL 與 OKLCH 之間變形。對比度比值計數到新的數值。
- 無效輸入會使欄位抖動。新色塊彈出，被移動的色塊落定到新位置。
- 減少動態效果時把手直接跳轉，面板以淡入代替裁剪展開，並取消格式變形與抖動。關閉動態效果時直接到達最終狀態。

## 相關元件

- `SegmentedControl` 或 `RadioGroup` 用於少量固定顏色。
- `Input` 用於一般文字值。
- `ChipGroup` 用於小型預設標籤。
