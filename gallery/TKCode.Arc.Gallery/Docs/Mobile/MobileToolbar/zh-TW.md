---
title: 移動端工具列
summary: 頁面底部 44 px 高的操作列：等分寬度的樸素操作，或起始、置中和末端三個槽。
source-hash: 532381e2349c
---

<Demo name="Basic" />

## 何時使用

`Toolbar` 是屬於目前畫面的操作列，放在拇指搆得到的底部。它作用於使用者正在看的內容，而 `Tabbar` 用於在不同區域之間切換。

- **針對單一項目的操作**：照片或文件下方的分享、編輯、收藏和刪除。
- **選取之後**：選取數列後出現「移動」「複製」「刪除」，中間顯示選取數量。
- **編輯器或表單**：起始處「取消」，末端「傳送」。

即時範例在手機外框裡執行，那是桌面上的模擬；列在 iOS 和 Android 的主畫面指示條之上如何排布，這裡沒有驗證。外框設定的頁面安全區會讓它避開這些區域。

## 何時不用

- **在頂層區域之間切換**：用 `Tabbar`。
- **畫面頂部的操作**：用 `Navbar` 的槽。
- **超過大約五個操作**：把其餘的放進 `Sheet`。

## 用法

把工具列放進頁面的 `Footer`。項目是 `Variant="Plain"` 的樸素 `arcm:Button`，讓列保持安靜；它們等分寬度，每個至少 44 px 高。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Footer>
        <arcm:Toolbar>
            <arcm:Button Variant="Plain" Content="Share" />
            <arcm:Button Variant="Plain" Content="Edit" />
            <arcm:Button Variant="Plain" Content="Delete" />
        </arcm:Toolbar>
    </arcm:Page.Footer>
</arcm:Page>
```

### 槽

`Leading`、`Center` 和 `Trailing` 把內容放在列的起始、中間和末端，而不是等分項目。

<Demo name="Slots" />

```xml
<arcm:Toolbar>
    <arcm:Toolbar.Leading>
        <arcm:Button Variant="Plain" Content="Cancel" />
    </arcm:Toolbar.Leading>
    <arcm:Toolbar.Trailing>
        <arcm:Button Variant="Plain" Content="Send" />
    </arcm:Toolbar.Trailing>
</arcm:Toolbar>
```

### 在標籤列之上，顯示與隱藏

把工具列和 `Tabbar` 放進 `Footer` 裡的同一個 `StackPanel`。`IsOpen` 以滑動加淡入淡出顯示和隱藏工具列；把它繫結到「有沒有可操作對象」的狀態。

<Demo name="WithTabbar" />

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Footer>
        <StackPanel>
            <arcm:Toolbar IsOpen="{Binding HasSelection}">
                <arcm:Button Variant="Plain" Content="Delete" />
            </arcm:Toolbar>
            <arcm:Tabbar SelectedIndex="0" />
        </StackPanel>
    </arcm:Page.Footer>
</arcm:Page>
```

關閉的工具列仍佔著版面中的位置，上方的內容不會跳動。要移除它，把 `IsVisible` 設為 `False`。

### Token

`ToolbarToken` 保存幾何：`BarHeight`（44，含髮絲線）、`BarPadding`（兩側 8）和 `BarBorderThickness`（列上方 1 px 的髮絲線）。顏色和字型來自 ArcDesign token。

## API 參考

<!-- api: MobileToolbar -->

## 無障礙

每個操作都是帶 44 px 觸控目標的一般按鈕；只有圖示的操作要給出可存取名稱。關閉的工具列不攔截觸控。

## 動態效果

關閉時，列向下滑出自身的高度並淡出，用一段短的補間；開啟時，在一個輕快的彈簧上滑回。列在移動時再次切換 `IsOpen`，會從目前位置掉頭。首幀沒有動態效果，減弱動態效果時列直接跳轉。按鈕的按下與放開和 `MobileButton` 一致。

## 相關元件

- `MobilePage`：把工具列放進它的 `Footer`。
- `MobileButton`：通常的內容，使用 `Variant="Plain"`。
- `MobileTabbar`：在不同區域之間切換的列，常常就在工具列下方。
- `MobileSheet`：放不下的操作。
