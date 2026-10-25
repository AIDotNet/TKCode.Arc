---
title: 移動端按鈕
summary: 觸控優先的按鈕：用按壓狀態取代懸停狀態，最小觸控目標 44 px。
source-hash: 1cdfa285426e
---

<Demo name="Basic" />

## 何時使用

`TKCode.Arc.Mobile` 的 `Button` 是給觸控螢幕用的按鈕。它是填滿樣式，至少 44 乘 44，並有按壓狀態：手指按在上面時，按鈕縮小到 0.97 並變暗到 0.85。沒有懸停狀態，因為手指不會懸停。

- **螢幕的主要操作**：內容底部通欄的「繼續」或「儲存」。
- **導覽列槽裡的操作**：「編輯」或「完成」。
- **表單的提交按鈕**。

即時範例執行在手機框中，這是桌面上的模擬。用滑鼠按住按鈕即可看到按壓狀態。手指在 Android 和 iOS 上的手感尚未驗證。

## 何時不用

- **桌面介面**：使用 `TKCode.Arc` 的 `Button`，它有變體、尺寸和懸停狀態。
- **保持選取的選擇**：設定用 `MobileSwitch`，多個選項用清單。

## 用法

移動端的 `Button` 位於 XAML 命名空間 `https://github.com/AIDotNet/TKCode.Arc.Mobile`（前綴 `arcm`）和 .NET 命名空間 `TKCode.Arc.Mobile`，因此不會與桌面的 `Button` 衝突；基於同樣的原因，本頁的 API 表把它稱為 `MobileButton`。它衍生自 Avalonia 的 `Button`，所以 `Content`、`Command` 和 `Click` 照常使用。

```xml
<StackPanel xmlns="https://github.com/avaloniaui"
            xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
            Spacing="12">
    <arcm:Button Content="Continue" HorizontalAlignment="Stretch" />
    <arcm:Button Content="Not available" IsEnabled="False" />
</StackPanel>
```

停用的按鈕變暗到 0.5。

### Token

`ButtonToken` 保存幾何：`TouchTarget`（44）、`ContentPadding`（16 乘 10）、`PressedScale`（0.97）、`PressedOpacity`（0.85）和 `DisabledOpacity`（0.5）。顏色、圓角和字型來自 ArcDesign token，因此按鈕跟隨明暗、強調色和 Pixel 風格。

## API 參考

<!-- api: MobileButton -->

## 無障礙

它是一般的 Avalonia 按鈕：可以取得焦點並用鍵盤觸發，並以內容作為名稱。44 px 的最小尺寸讓目標容易點中。

## 動態效果

按壓的縮放和變暗在控制項主題中宣告，減少動態效果時立即切換。

## 相關元件

- `MobileSwitch`：設定用的開關。
- `MobileNavbar`：按鈕放在它的槽裡。
- `MobilePage`：按鈕所在的螢幕。
