---
title: Logo marquee
summary: 一排安靜、持續移動的標誌，附帶暫停控制項。
source-hash: 92e7516e9873
---

<Demo name="Preview" />

## 何時使用

使用 `LogoMarquee` 在到達頁上展示客戶或整合標誌列，或用於帶標題和一行說明的社會證明區塊。

## 何時不用

- **標誌需要說明文字或連接操作**：使用 `Integrations` 區塊。
- **內容需要使用者手動瀏覽**：使用 `Carousel`。
- **沒有展示授權的標誌**：預設標誌都是虛構的；只替換為你有權使用的標誌。

## 用法

```xml
<arc:LogoMarquee Title="Trusted by product teams"
                 Description="Teams of every size ship with us." />
```

### 色調

`Tone` 可取 `Color`（每個標誌使用自身顏色，名稱以全對比度顯示）或 `Mono`（所有標誌與名稱都使用次要文字顏色）。

<Demo name="Mono" />

### 自訂標誌

`Brands` 接受 `LogoMarqueeBrand` 項目。`Icon` 是任意座標系下的填滿 `Geometry`，會縮放到合適大小。`Color` 在 `Color` 色調下為其著色。`ColorIcon` 是全彩 `IImage`，在 `Color` 色調下取代 `Icon`。

<Demo name="CustomMarks" />

```csharp
marquee.Brands =
[
    new LogoMarqueeBrand { Name = "Acme", Icon = Geometry.Parse("M12 2 22 22H2z"), Color = Color.Parse("#3C9D6B") },
    new LogoMarqueeBrand { Name = "Globex", ColorIcon = myDrawingImage, Icon = fallbackShape }
];
```

### 暫停

標誌列下方的控制項切換 `IsPaused`，該屬性也可以繫結。所有文字都是帶英文預設值的屬性。

## API 參考

<!-- api: LogoMarquee -->

## 無障礙

- 標誌列只向輔助技術公開一次，名稱取自 `RegionLabel`，各標誌名稱作為說明文字；重複的副本不公開。
- 暫停控制項有操作名稱（`PauseActionName`、`PlayActionName`），暫停時即時區域會讀出 `PausedAnnouncement`。
- 動效低於完整層級時，標誌列完全不移動。

## 動效

- 標誌列以線性速度在 39 秒內移動一個清單的寬度，無縫循環。滑鼠或觸控筆停留其上時保持靜止。
- 每幀只在 `LiveLoop` 上移動一個變換，離開檢視區即暫停，不重繪也不配置記憶體。
- 減弱動效與關閉動效時，標誌列停止移動，每個標誌只繪製一次並置中換行，同時隱藏暫停控制項。

## 響應式

- 區塊跟隨自身寬度。低於 640 px（`:narrow`）時標題與說明上下堆疊，標誌從 22 px 縮小到 20 px，間距從 56 px 收緊到 40 px。
- 標誌列兩側各淡出 7%，窄寬度下標誌不會被生硬截斷。

## 相關元件

- `Carousel`：需要手動切換的投影片。
- `Integrations` 區塊：帶說明和操作的標誌。
