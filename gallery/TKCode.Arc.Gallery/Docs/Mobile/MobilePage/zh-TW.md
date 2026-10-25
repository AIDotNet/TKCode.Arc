---
title: 移動端頁面
summary: 手機螢幕：頁首、內容和頁尾，排佈在裝置的安全區之內。
source-hash: c254a865ba00
---

<Demo name="Basic" />

## 何時使用

`Page` 是手機或平板應用程式中一個螢幕的根。它提供螢幕背景，讓內容避開狀態列、瀏海和主畫面指示條，並在內容上方放置可選的頁首（通常是 `Navbar`），下方放置可選的頁尾（通常是 `Tabbar`）。

- **行動應用程式的一個螢幕**：收件匣、設定清單或詳細頁，頂部是導覽列，底部是標籤列。
- **鋪滿的螢幕**：只有內容的歡迎頁或引導頁，背景仍延伸到狀態列之下。
- **需要回應裝置安全區的螢幕**：頁面會自行跟隨平台的安全區邊距，也可以使用你設定的值。

本頁的即時範例執行在手機框中：這是桌面上的模擬，附有裝置切換、直向與橫向、模擬的狀態列和安全區，以及鍵盤佔位。它不是真機，控制項在 Android 和 iOS 上的外觀與行為（包括真實的軟體鍵盤）尚未驗證。

## 何時不用

- **桌面視窗或網頁式版面**：使用桌面控制項（`TKCode.Arc`）和一般版面面板。平台沒有回報安全區時，頁面的安全區為零。
- **螢幕內部的容器**：使用 `Card` 或版面面板。一個螢幕只有一個 `Page`。
- **蓋在螢幕上的強制回應或面板**：使用對話方塊或抽屜；頁面是它們下面的螢幕。

## 用法

設定 `Header`、內容和 `Footer`。移動端控制項有自己的 XAML 命名空間 `https://github.com/AIDotNet/TKCode.Arc.Mobile`，通常使用前綴 `arcm`，因此可以與同名的桌面控制項並存。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
             xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page>
        <arcm:Page.Header>
            <arcm:Navbar Title="Inbox" />
        </arcm:Page.Header>
        <TextBlock Margin="16" Text="Hello" />
    </arcm:Page>
</UserControl>
```

在 `UseArcCore()` 或 `UseArc()` 旁邊用 `UseArcMobile()` 註冊這些控制項。

### 安全區

`SafeArea` 是頁面在每條邊上保留的空間。`UsePlatformSafeArea` 開啟時（預設），頁面跟隨頂層視窗 insets 管理員的安全區邊距，沒有該管理員（例如桌面）時為零。關閉 `UsePlatformSafeArea` 並自行設定 `SafeArea` 即可覆寫。背景鋪滿整個頁面，所以它延伸到狀態列之下，而頁首和內容留在安全區之內。

手機框為範例做了這件事：它依模擬的裝置設定 `SafeArea`，並關閉 `UsePlatformSafeArea`。用手機上方的切換器可以看到類似 iPhone 的瀏海和主畫面指示條、手勢列、小螢幕手機和平板，用鍵盤開關可以看到內容區縮小。

### 僅內容

沒有頁首和頁尾的頁面，內容可以使用整個安全區。

<Demo name="ContentOnly" />

### Token

`PageToken` 保存幾何：`ContentInset`（內容槽四周的內距，0）。背景、前景、字型和字級來自 ArcDesign token（`Background`、`Foreground`、`BodyFontFamily`、`TypeBase`），因此頁面跟隨明暗與強調色。

## API 參考

<!-- api: MobilePage -->

## 無障礙

頁面自己不新增鍵盤行為或角色；頁首、內容和頁尾保留各自的行為。保持內容依閱讀順序排列，並替導覽列設定 `Title`，讓螢幕有名稱。

## 動態效果

背景依標準曲線跟隨明暗切換。減少動態效果時立即切換。

## 相關元件

- `MobileNavbar`：頁面的頂部列。
- `MobileTabbar`：頁面的底部列。
- `MobileButton` 與 `MobileSwitch`：內容裡的觸控控制項。
