---
title: 移動端導覽列
summary: 頁面頂部 44 px 高的列：起始槽、置中的標題和放操作的末端槽。
source-hash: 50823e765d24
---

<Demo name="Basic" />

## 何時使用

`Navbar` 是行動螢幕的頂部列。它顯示使用者所在的位置（`Title`），並提供返回的入口（`Leading`）和本螢幕的操作（`Trailing`）。

- **推入的螢幕**：`Leading` 放返回按鈕，標題是螢幕名稱，`Trailing` 放「完成」或「編輯」按鈕。
- **只有一個操作的根螢幕**：只有標題和末端的一個按鈕。
- **純標題列**：只有標題，空的槽讓它保持置中。

即時範例執行在手機框中，這是桌面上的模擬。該列在 Android 和 iOS 真機的狀態列或瀏海之下如何呈現尚未驗證；這裡由手機框設定的頁面安全區讓它避開這些區域。

## 何時不用

- **頂層區域之間的導覽**：使用 `Tabbar`，它停在拇指碰得到的底部。
- **帶選單和搜尋的桌面頁首**：使用桌面的頁首區塊和控制項。
- **操作很多的工具列**：導覽列兩端各只容得下一兩個觸控目標。

## 用法

把導覽列放進頁面的 `Header`。兩個槽可以放任何內容，通常是 `arcm:Button`，並保持至少 44 的寬度，讓按鈕易於點按。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Header>
        <arcm:Navbar Title="Settings">
            <arcm:Navbar.Leading>
                <arcm:Button Content="Back" />
            </arcm:Navbar.Leading>
            <arcm:Navbar.Trailing>
                <arcm:Button Content="Done" />
            </arcm:Navbar.Trailing>
        </arcm:Navbar>
    </arcm:Page.Header>
</arcm:Page>
```

### 僅標題

槽裡沒有內容時，標題保持置中。

<Demo name="TitleOnly" />

### Token

`NavbarToken` 保存幾何：`BarHeight`（44）、`BarPadding`（兩側 8）、`SlotMinWidth`（44）和 `BarBorderThickness`（列下方 1 px 的細線）。顏色和字型來自 ArcDesign token。

## API 參考

<!-- api: MobileNavbar -->

## 無障礙

標題是純文字；每個螢幕都應設定一個。槽裡的按鈕是有 44 px 觸控目標的一般按鈕。替返回按鈕取一個說明去向的名稱。

## 動態效果

列本身沒有動態效果；槽裡的按鈕按下與放開的表現與 `MobileButton` 相同。

## 相關元件

- `MobilePage`：把導覽列放進它的 `Header`。
- `MobileButton`：槽裡常見的內容。
- `MobileTabbar`：底部列。
