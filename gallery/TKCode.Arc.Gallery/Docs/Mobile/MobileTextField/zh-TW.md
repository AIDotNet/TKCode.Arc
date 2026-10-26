---
title: 移動端文字方塊
summary: 44 px 高的填充文字方塊，可帶圖示和清除按鈕，取得焦點時邊框變為強調色。
source-hash: 4cdca1c7d9e2
---

<Demo name="Basic" />

## 何時使用

`TextField` 是移動端表單裡的單行文字輸入：姓名、信箱、簡短的回答。

- **手機上的表單**：文字方塊高 44 px，拇指容易點中。
- **有含義的欄位**：文字前的 `Icon` 說明這個欄位是做什麼的。
- **常需要修正的欄位**：清除按鈕一次點按就能清空。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的軟鍵盤這裡沒有驗證。

## 何時不用

- **搜尋**：用 `Searchbar`，它有搜尋圖示、膠囊形狀和搜尋鍵。
- **很長的多行文字**：應該用文字區域，這個控制項只有一行。
- **從幾個值裡選一個**：用開關、分段控制項或帶清單的面板。

## 用法

`TextField` 衍生自 Avalonia 的 `TextBox`，所以 `Text`、選取、`IsReadOnly` 和繫結都和那裡一樣。用 `PlaceholderText` 顯示提示。

```xml
<arcm:TextField xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                PlaceholderText="姓名"
                Icon="{x:Static arc:ArcIcons.User}"
                Text="{Binding Name}" />
```

清除按鈕只在文字方塊有焦點且有文字時顯示；唯讀或 `ShowClearButton` 為 false 時不顯示。點它會清空文字並保持焦點。文字方塊沒有懸停狀態：手指不會懸停。

### Token

`TextFieldToken` 保存幾何：`FieldHeight`（44）、`ContentPadding`（兩側 12）、`BorderWidth`（1）、`GlyphExtent`（18）、`IconMargin` 和 `DisabledOpacity`（0.5）。顏色、圓角和字型來自 ArcDesign token。

## API 參考

<!-- api: MobileTextField -->

## 無障礙

給每個欄位一個看得見的名稱，放在旁邊，或者用圖示加預留位置提示；人開始輸入後，預留位置提示就不再是唯一的標籤。文字方塊至少 44 px 高，清除按鈕是 44 px 的觸控目標。

## 動效

文字方塊取得或失去焦點時，邊框按快速曲線變色；減弱動效時立即變色。

## 相關元件

- `MobileSearchbar`：為搜尋準備的文字方塊。
- `MobilePage`：表單所在的螢幕。
- `MobileButton`：提交表單的常用方式。
