---
title: 移動端標籤列
summary: 頁面底部的標籤列：圖示在上、文字在下的標籤平分寬度，選中的一個醒目顯示。
source-hash: ec2854917835
---

<Demo name="Basic" />

## 何時使用

`Tabbar` 是行動螢幕的底部導覽：使用者用拇指在幾個頂層區域之間切換。

- **應用程式的頂層區域**：首頁、搜尋、提醒和我的，始終觸手可及。
- **兩到五個目的地**：各項平分寬度，所以標籤要短。

每個 `TabbarItem` 在標籤文字上方顯示一個 `Icon`。即時範例執行在手機框中，這是桌面上的模擬；真機上的觸控回饋尚未驗證。

## 何時不用

- **同一物件的多個檢視**：在螢幕內部使用分段控制項或標籤頁。
- **超過五個目的地**：把其餘放進「更多」螢幕或選單。
- **桌面側邊欄**：使用桌面套件裡的 `Sidebar`。

## 用法

把 `TabbarItem` 放進列裡，用 `SelectedIndex`（或 `SelectedItem`）設定開啟的一項。`Tabbar` 衍生自 Avalonia 的 `ListBox`，所以選取、`SelectionChanged` 和繫結 `ItemsSource` 的用法與那裡相同。把這個列用作頁面的 `Footer`。

```xml
<arcm:Tabbar xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
             SelectedIndex="0">
    <arcm:TabbarItem Content="Home" Icon="{x:Static arc:ArcIcons.House}" />
    <arcm:TabbarItem Content="Search" Icon="{x:Static arc:ArcIcons.Search}" />
    <arcm:TabbarItem Content="Profile" Icon="{x:Static arc:ArcIcons.User}" />
</arcm:Tabbar>
```

選中的項使用前景色，其餘使用弱化的文字色。手指按在某一項上時它會變暗；沒有懸停狀態。

### Token

`TabbarToken` 保存列的幾何：`BarHeight`（49）和 `BarBorderThickness`（上方 1 px 的細線）。`TabbarItemToken` 保存每一項的幾何：`TouchTarget`（44）、`IconExtent`（24）、`IconGap`（2）、`PressedOpacity`（0.6）和 `ItemPadding`（4）。顏色來自 ArcDesign token。

## API 參考

<!-- api: MobileTabbar -->

<!-- api: MobileTabbarItem -->

## 無障礙

各項是附文字的可選取清單項，以其內容命名。保留文字，圖示就不會是唯一的名稱。每一項至少 44 px 高。

## 動態效果

一個強調色膠囊在各項之間沿彈簧滑動（P7，`GlidingIndicator`）：第一次顯示時在落點原地淡入，列尺寸變化時直接吸附而不滑動，減少動效時直接跳到目標。圖示和文字顏色按快速曲線變化。手指按下時該項變暗，內容用快速彈簧縮小到 0.96（P1）。減少動效時不縮放，一切立即切換。

## 相關元件

- `MobilePage`：把標籤列放進它的 `Footer`。
- `MobileNavbar`：頂部列。
