---
title: 移動端單選
summary: 44 px 列裡的 22 px 單選，強調色圓點以彈簧彈入，每組單選，支援方向鍵，沒有懸停狀態。
source-hash: 89f6a17574b0
---

<Demo name="Basic" />

## 何時使用

`Radio` 讓使用者在表單或設定頁的一個短清單裡恰好選一項。

- **幾個選項裡選一個**：配送速度、付款方式、排序方式。
- **每個選項都需要一段標籤**：每一列有自己的文字，選項讀起來像一個清單。
- **稍後再確認的選擇**：單選顯示目前選擇，由按鈕提交。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋這裡沒有驗證。

## 何時不用

- **同一內容的幾個檢視的短列**：用 `Segmented`。
- **任意數量的選項**：用 `Checkbox`。
- **立即生效的設定**：用 `Switch`。

## 用法

`Radio` 衍生自 Avalonia 的 `RadioButton`，所以 `IsChecked`、`GroupName`、`Click` 和繫結都和那裡一樣。`GroupName` 相同的單選互斥；把同一組的單選放在同一個面板裡。標籤是內容，整列都是觸控目標。這個設計是帶群組名稱的 `RadioButton`，而不是帶選取值的容器：單選可以放進任何列版面，選擇透過每一項的 `IsChecked` 繫結。

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Radio GroupName="delivery" Content="Standard, free" IsChecked="True" />
    <arcm:Radio GroupName="delivery" Content="Express, 2 days" />
</StackPanel>
```

焦點在某個單選上時，方向鍵會把選擇移到同組、同層的上一個或下一個可用單選，並循環。手指按在列上時會變暗；沒有懸停狀態。

### Token

`RadioToken` 保存幾何：`TouchTarget`（44）、`CircleSize`（22）、`CircleBorderWidth`（1.5）、`DotSize`（10）、`LabelMargin`、`PressedOpacity`（0.6）和 `DisabledOpacity`（0.5）。顏色來自 ArcDesign token。

## API 參考

<!-- api: MobileRadio -->

## 無障礙

這一列是以標籤命名的單選按鈕，所以一定要給它內容；沒有標籤的單選沒有名稱。列高至少 44 px，圓圈不是唯一的目標。方向鍵在組內移動，選取狀態像任何 `RadioButton` 一樣暴露給輔助技術。

## 動效

圓點從小尺寸帶淡入彈簧彈入，離開時同樣；因為是彈簧，選擇在中途反轉時會保留速度。外圈顏色依快速曲線變化。減少動效時，圓點和外圈立即變化。

## 相關元件

- `MobileCheckbox`：任意數量的選擇。
- `MobileSegmented`：幾個檢視的短列。
- `MobileListGroup`：可以在尾部內容裡放單選的列。
