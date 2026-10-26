---
title: 移動端圖示按鈕
summary: 44 乘 44 的純圖示按鈕，有樸素、淺色、實心和懸浮幾種變體，帶彈簧按壓狀態。
source-hash: 3e3134f8f834
---

<Demo name="Basic" />

## 何時使用

`IconButton` 是只顯示圖示的按鈕：用在導覽列裡、卡片上，或懸浮在內容之上。

- **列上的操作**：搜尋、分享、更多。預設的 `Plain` 變體是沒有背景的強調色圖示。
- **卡片或列上的次要操作**：`Tinted`。
- **檢視的主要操作**：`Filled`，懸浮在捲動內容之上時用 `Fab`。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋這裡沒有驗證。

## 何時不用

- **需要文字才能看懂的操作**：用帶文字的 `Button`。
- **在幾個選項之間選擇**：用 `Segmented`、`Radio` 或 `Checkbox`。
- **多個同等的懸浮操作**：懸浮按鈕只有一個主要操作；更多操作用 `Sheet`。

## 用法

`IconButton` 衍生自 Avalonia 的 `Button`，所以 `Click`、`Command` 和繫結都和那裡一樣。把 `Icon` 設為一個幾何，用 `Variant` 選外觀。按鈕是 44 乘 44。

```xml
<arcm:IconButton xmlns="https://github.com/avaloniaui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                 xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                 Icon="{x:Static arc:ArcIcons.Share}"
                 Variant="Filled"
                 AutomationProperties.Name="Share" />
```

### 懸浮操作按鈕

`Variant="Fab"` 得到一個 56 px 的強調色圓，帶 ArcDesign token 的懸浮陰影。把它放在內容之上，例如 `Page` 的覆蓋層槽，或對齊到右下角的 `Panel`。

<Demo name="Fab" />

### Token

`IconButtonToken` 保存幾何：`TouchTarget`（44）、`FabSize`（56）、`IconExtent`（22）、`FabIconExtent`（24）、`PressedOpacity`（0.85）和 `DisabledOpacity`（0.5）。顏色和陰影來自 ArcDesign token。

## API 參考

<!-- api: MobileIconButton -->

## 無障礙

圖示沒有文字，所以一定要設定 `AutomationProperties.Name` 說明按鈕做什麼（「搜尋」、「新訊息」）；沒有它，讀屏軟體什麼有用的都讀不出來。按鈕至少 44 乘 44，懸浮的是 56。

## 動效

按下時按鈕在快速彈簧上縮到 0.97（懸浮的縮到 0.92），向下沒有延遲，並略微變暗；放開時彈回。半途放開的按壓會保留速度。減少動效時按鈕不縮放，只變暗。

## 相關元件

- `MobileButton`：帶文字的按鈕。
- `MobileNavbar`：樸素圖示按鈕在列裡的位置。
- `MobileSheet`：需要比一個懸浮按鈕更多的操作時使用。
