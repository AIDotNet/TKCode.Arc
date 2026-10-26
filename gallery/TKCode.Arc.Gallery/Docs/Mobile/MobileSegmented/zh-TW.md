---
title: 移動端分段控制項
summary: 一排共享寬度的分段，選中其中一個，用於在同一內容的幾個檢視之間切換。
source-hash: e4bee4b6bafb
---

<Demo name="Basic" />

## 何時使用

`Segmented` 在同一事物的幾個檢視之間切換：時間範圍、排序方式、同一批地點的清單和地圖。

- **兩到四個選項**，標籤很短，同時可見。
- **同一內容的不同檢視**：下面的內容變化，螢幕不變。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋這裡沒有驗證。

## 何時不用

- **應用的頂層區域**：用 `Tabbar`。
- **超過四個選項**或標籤很長：用清單或面板。
- **同時選多項**：用核取方塊。

## 用法

把 `SegmentedItem` 放進控制項，並設定 `SelectedIndex`。`Segmented` 衍生自 Avalonia 的 `ListBox`，所以 `SelectedItem`、`SelectionChanged` 和繫結到 `ItemsSource` 都和那裡一樣。

```xml
<arcm:Segmented xmlns="https://github.com/avaloniaui"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                SelectedIndex="1">
    <arcm:SegmentedItem Content="日" />
    <arcm:SegmentedItem Content="週" />
    <arcm:SegmentedItem Content="月" />
</arcm:Segmented>
```

各分段平分寬度。選中的一個浮在帶描邊的 Surface 填充上，用前景色；其他的用次要文字色。手指按在未選中的分段上時會變暗；沒有懸停狀態。

### Token

`SegmentedToken` 保存軌道的幾何：`TrackPadding`（2）和 `TrackRadius`（10）。`SegmentedItemToken` 保存分段的幾何：`ItemHeight`（36）、`ItemPadding`、`ItemRadius`（8）和 `PressedOpacity`（0.6）。加上內距，控制項高 40。顏色和字型來自 ArcDesign token。

## API 參考

<!-- api: MobileSegmented -->

<!-- api: MobileSegmentedItem -->

## 無障礙

分段是可選取的清單項，名稱來自標籤。標籤要簡短且互不相同。分段高 36 px，寬度共享，所以四個選項時窄螢幕手機上每個分段仍有可用的觸控目標。

## 動效

填充、描邊和文字顏色按快速曲線變化；減弱動效時立即變化。

## 相關元件

- `MobileTabbar`：用於頂層區域。
- `MobileNavbar`：分段控制項常放在它下面。
- `MobilePage`：它所在的螢幕。
