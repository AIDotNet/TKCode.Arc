---
title: 移動端核取方塊
summary: 44 px 列裡的 22 px 圓角核取方塊，有勾選、不確定狀態的短橫，沒有懸停狀態。
source-hash: 7df472595ce1
---

<Demo name="Basic" />

## 何時使用

`Checkbox` 讓使用者在表單或清單裡選擇任意數量的選項，或者表示同意。

- **同時選多項**：篩選、興趣、要包含的項目。
- **單項同意**：「記住此裝置」、「我接受條款」。
- **全選**：用 `IsThreeState`，父列在只有部分子項被選中時顯示短橫。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋這裡沒有驗證。

## 何時不用

- **立即生效的設定**：用 `Switch`。
- **多個選項裡只能選一個**：用 `Segmented` 或帶勾選的列清單。
- **一個命令**：用 `Button`。

## 用法

`Checkbox` 衍生自 Avalonia 的 `ToggleButton`，所以 `IsChecked`、`IsThreeState`、`Click` 和繫結都和那裡一樣。標籤是內容，整列都是觸控目標。

```xml
<arcm:Checkbox xmlns="https://github.com/avaloniaui"
               xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
               Content="記住此裝置"
               IsChecked="{Binding Remember}" />
```

設定 `IsThreeState`，並把 `IsChecked` 繫結到可為空的值，就有了不確定狀態。手指按在列上時會變暗；沒有懸停狀態。

### Token

`CheckboxToken` 保存幾何：`TouchTarget`（44）、`BoxSize`（22）、`BoxRadius`（6）、`BoxBorderWidth`（1.5）、`CheckExtent`（16）、`LabelMargin`、`PressedOpacity`（0.6）和 `DisabledOpacity`（0.5）。顏色來自 ArcDesign token。

## API 參考

<!-- api: MobileCheckbox -->

## 無障礙

這一列是一個切換按鈕，名稱來自它的標籤，所以一定要給它內容；沒有標籤的核取方塊沒有名稱。這一列至少 44 px 高，核取方塊不是唯一的觸控目標。

## 動效

填充和勾選按快速曲線變化；減弱動效時立即變化。

## 相關元件

- `MobileSwitch`：立即生效的設定。
- `MobileListGroup`：列的 trailing 內容裡可以放核取方塊。
- `MobileButton`：提交表單的常用方式。
