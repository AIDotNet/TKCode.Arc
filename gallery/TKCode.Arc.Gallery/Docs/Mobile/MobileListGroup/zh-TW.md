---
title: 移動端清單
summary: 分組的列，用於設定和選單：圖示、標題、副標題、列尾的值或控制項，以及箭頭。
source-hash: 4ce9e55c4288
---

<Demo name="Basic" />

## 何時使用

`ListGroup` 和 `ListItem` 排出一屏的列：設定、選單和帳戶頁，每一列都是可以點按的標題。

- **設定**：把相關的列放在一個簡短的 `Header` 下，用 `Footer` 作說明。
- **目的地選單**：帶 `Icon` 和箭頭、會開啟其他頁面的列。
- **列尾的值或開關**：放進 `Trailing`。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋這裡沒有驗證。

## 何時不用

- **很長或無限的資料**：應該用虛擬化清單，這個還沒有做。
- **帶圖片和多個操作的卡片**：自己排版。
- **桌面選單**：用桌面的選單控制項；這裡的列沒有懸停狀態。

## 用法

把 `ListItem` 放進 `ListGroup`，再把組放進可捲動的 `Page`。項的內容就是它的標題。`ListItem` 是一個按鈕，給它 `Command` 或處理 `Click`；手指按在列上時，列會變暗。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
           xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
           IsScrollable="True">
    <arcm:ListGroup Header="帳戶">
        <arcm:ListItem Icon="{x:Static arc:ArcIcons.User}" Content="個人資料" Subtitle="Ada Lovelace" ShowChevron="True" />
        <arcm:ListItem Content="每週摘要" Trailing="週日" ShowChevron="True" />
    </arcm:ListGroup>
</arcm:Page>
```

一組的最後一列下面沒有分隔線。

### Token

`ListItemToken` 保存列的幾何：`RowHeight`（48）、`ContentPadding`（16 乘 10）、`IconExtent`（22）、`IconGap`（12）、`TrailingMargin`、`ChevronExtent`（16）和 `SeparatorInset`（16）。`ListGroupToken` 保存組的幾何：`GroupPadding`（16 乘 8）、`CardBorderThickness`（1）、`HeaderMargin` 和 `FooterMargin`。顏色、圓角和字型來自 ArcDesign token。

## API 參考

<!-- api: MobileListGroup -->

<!-- api: MobileListItem -->

## 無障礙

一列是一個按鈕，名稱來自標題和副標題。每一列都要有標題，圖示不能是它唯一的名稱。每一列至少 48 px 高，高於 44 px 的觸控目標。

## 動效

手指按在列上時，背景按快速曲線變暗；減弱動效時，立即變暗。

## 相關元件

- `MobilePage`：可捲動的頁面承載這些組。
- `MobileSwitch`：設定項常用的 `Trailing` 控制項。
- `MobileNavbar`：頂列，根設定頁可用大標題。
