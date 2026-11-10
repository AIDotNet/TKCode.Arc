---
title: Team showcase
summary: 團隊介紹區塊：生成的頭像、姓名和職位，懸停、聚焦或輕點時在頭像上顯示簡介，按部門篩選時網格會滑到新位置。
source-hash: b70414df9b0e
---

<Demo name="Preview" />

## 何時使用

`TeamShowcase` 在關於我們、招聘或公司頁面上介紹產品背後的人。每個人是一張卡片，上面是方形頭像、姓名和職位；指針移到卡片上、用鍵盤聚焦或輕點時，頭像下半部分會浮出一段簡短的介紹和所在城市。沒有照片的人會得到一個生成的頭像：用主題的圖表顏色畫出的頭肩人像，由姓名決定，所以同一個人在哪裡都長一樣。網格上方的分段篩選一次只顯示一個部門：其他人淡出，留下的卡片滑到新位置。

- **關於我們頁面**，團隊有五到三十人。
- **招聘頁面**，按部門展示候選人將來的同事。
- **項目或工作室頁面**，列出參與制作的人。

## 何時不用

- **可搜索、帶聯絡方式的公司通訊錄**：請使用 `TeamDirectory`。
- **在應用中管理成員和角色**：請使用 `TeamMembers` 或 `RolesPermissions`。
- **客戶評價**：請使用 `StudioPerspectives`。

## 用法

```xml
<arc:TeamShowcase Title="The people behind Northwind" MaxColumns="4">
    <arc:TeamShowcaseMember Name="Mara Okonjo" Role="Head of Design" Department="Design" Location="Lisbon" Bio="Draws the first version of everything on paper." />
    <arc:TeamShowcaseMember Name="Teo Lindgren" Role="Staff Engineer" Department="Engineering" Location="Stockholm" Bio="Owns the sync engine." />
</arc:TeamShowcase>
```

沒有成員時區塊顯示一個虛構的十人團隊；把 `ShowsSampleTeam` 設為 false 則顯示空網格。成員的 `Image` 會替換生成的頭像，`Seed` 可換一個人像。

### 自己的團隊

<Demo name="CustomTeam" />

部門來自成員，按首次出現的順序排列；至少有兩個部門時才顯示篩選（`IsFilterVisible` 可隱藏它）。`SelectedDepartment` 是雙向的：null 表示顯示全部。`MemberInvoked` 報告在卡片上的點擊、輕點、Enter 或 Space，同時會固定顯示它的簡介。

### 列數

只要每列不窄於 `MinColumnWidth`（200），網格就會增加列，最多 `MaxColumns`（4）列。

## API 參考

<!-- api: TeamShowcase -->

## 無障礙

- 區塊以標題命名，標題是二級標題。
- 每張卡片都可聚焦，以人名命名；幫助文字依次讀出職位、部門、簡介和城市。
- 篩選變化後會播報顯示的人數。被篩掉的卡片如果有焦點，焦點會移到篩選上。
- 頭像僅作裝飾，對螢幕閱讀器隱藏。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| `Tab` | 移到篩選，再依次經過各張卡片。 |
| 方向鍵 | 按行列在卡片之間移動。 |
| `Home` / `End` | 第一張或最後一張卡片。 |
| `Enter` / `Space` | 固定或取消固定簡介。 |
| `Escape` | 取消固定簡介。 |

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 簡介出現 | 160 毫秒淡入並以彈簧上升 8 像素；頭像放大 3% | 淡入 | 立即 |
| 篩選移除卡片 | 160 毫秒淡出 | 淡出 | 立即 |
| 留下的卡片移動 | 以彈簧滑到新位置 | 直接跳到 | 直接跳到 |
| 篩選帶回卡片 | 淡入並略微上升，每張間隔 35 毫秒 | 淡入 | 立即 |

## 響應式

- 斷點按區塊自身的寬度計算：小於 720（`:compact`）時標題變小、間距收緊；小於 480（`:narrow`）時邊距再收緊，篩選占滿寬度並可横向滚動。
- 列數隨寬度變化（`MinColumnWidth`、`MaxColumns`）；在手機上是一到兩列。
- 輕點也能顯示簡介，所以觸控螢幕同樣可以閱讀。

## 相關元件

- `TeamDirectory`：帶個人資料的可搜索通訊錄。
- `StudioPerspectives`：客戶評價。
- `SegmentedControl`：部門篩選就是它。
