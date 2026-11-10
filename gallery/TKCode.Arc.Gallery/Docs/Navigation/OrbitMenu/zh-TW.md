---
title: Orbit menu
summary: 一個圓形按鈕，把它的動作沿著圓弧一個接一個地散開在四周，並可用方向鍵繞著圓環移動。
source-hash: e91e24dd53ba
---

<Demo name="Preview" />

## 何時使用

`OrbitMenu` 是一個圓形的觸發按鈕，裡面收著幾個相關的動作。按下它，動作會一個接一個送出，各自以彈簧彈到觸發按鈕四周圓弧上的位置，同時觸發按鈕的加號變成關閉記號。選擇動作、在外面按一下或按 Escape，動作就會收回去。因為每個動作到拇指或指標的距離都相同，選擇起來又快又容易搆到。

- **建立選單**：一個浮動的加號，提供照片、語音、文字或檔案。
- **快速回應**：用在訊息或貼文上，排成完整的圓環。
- **畫布上的工具面板**，在手所在的地方開啟。

## 何時不用

- **超過大約八個動作**：圓弧會變擠；請改用帶標籤的選單。
- **需要名稱才能看懂的動作**：圓環只顯示圖示，名稱只以提示文字呈現。
- **主要導覽**：要前往的地方應當放在分頁列或導覽中，而不是藏在按鈕後面。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:OrbitMenu Label="Create">
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Camera}" Label="Photo" />
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.Mic}" Label="Voice note" />
        <arc:OrbitMenuAction Glyph="{x:Static arc:ArcIcons.PenLine}" Label="Text note" />
    </arc:OrbitMenu>
</UserControl>
```

`OrbitMenu` 位於 Special 套件：在 `UseArcControls()` 旁邊用 `UseArcSpecial()` 註冊。它的項目是 `OrbitMenuAction` 元素，每個都有 `Glyph` 和 `Label`（提示文字和無障礙名稱），也可以透過 `ItemsSource` 繫結。動作會觸發自己的 `Click` 並執行它的 `Command`；選單會觸發帶有該動作的 `ActionInvoked` 並收回，除非 `ClosesOnInvoke` 為 false。`IsOpen`（雙向）、`Open()`、`Close()` 和 `Toggle()` 可以在程式碼中控制它；`Label` 為觸發按鈕命名，`Glyph` 可更換它的圖示。

選單只佔觸發按鈕的空間。動作是畫在周圍內容之上的，所以請為它們留出空間：`Radius`（預設 88 px）加上在展開那幾側的半個動作大小。

### 圓弧

`StartAngle` 是第一個動作的位置，`SweepAngle` 是其餘動作沿著圓弧延伸的角度，單位是度，從右方開始順時針計算（-90 是正上方，180 是左方）。預設是從左到右在上方展開。掃過 360 時，動作會均勻分布在整個圓環上。

<Demo name="FullRing" />

### 最佳實務

**建議**

- 給每個動作一個簡短的 `Label`：那是輔助技術唯一能得到的名稱。
- 讓圓弧朝向空白處，例如在螢幕底部的觸發按鈕向上展開。
- 保持三到六個動作。

**避免**

- 把重要事情唯一的做法藏在圓環後面。
- 把觸發按鈕放在圓弧會超出視窗的位置。

## API 參考

<!-- api: OrbitMenu -->

<!-- api: OrbitMenuAction -->

## 無障礙

- 觸發按鈕是一個由 `Label` 命名的按鈕，並回報圓環是否已展開。
- 每個動作被朗讀為以 `Label` 命名的選單項目，輔助技術可以呼叫它。收起的動作對輔助技術和 Tab 順序都是隱藏的。
- 從鍵盤開啟時，焦點移到第一個動作；關閉時移回觸發按鈕。使用指標時不顯示焦點環；使用鍵盤時，在觸發按鈕或動作周圍顯示統一的焦點環。

### 鍵盤互動

| 按鍵 | 動作 |
|---|---|
| 觸發按鈕上的 Enter 或 Space | 開啟或關閉圓環。 |
| 右或下 | 沿圓環移到下一個動作，到尾端會繞回。 |
| 左或上 | 移到上一個動作，到開頭會繞回。 |
| Home / End | 移到第一個或最後一個動作。 |
| 動作上的 Enter 或 Space | 執行該動作並收起圓環。 |
| Escape | 收起圓環並把焦點移回觸發按鈕。 |
| Tab 離開 | 收起圓環。 |

## 動效

- 開啟時，每個動作比前一個晚 35ms 離開觸發按鈕，從原大小的 40% 長大，並帶著輕微的回彈以彈簧彈到它的位置。關閉時它們以反向順序，用不帶回彈的快速彈簧返回。
- 觸發按鈕的圖示以俐落的彈簧轉動 45 度，加號就變成了關閉記號。
- 動作只用繪製變換來移動；移動期間不觸發版面配置。

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 動作送出 | 從觸發按鈕錯開依序的彈簧 | 在原地淡入，160ms | 直接出現 |
| 動作收回 | 以反向順序快速彈回 | 淡出，180ms | 直接消失 |
| 觸發按鈕圖示 | 以彈簧轉動 | 立即轉動 | 立即轉動 |

## 響應式行為

- 觸發按鈕在任何寬度下都保持它的大小（52 px），拇指按起來很舒適。
- 圓弧保持它的 `Radius`；在手機上，請讓它朝向螢幕內側（例如從底部的觸發按鈕向上），並留出它需要的空間。
- 動作的直徑是 44 px，是舒適的觸控目標。

## 相關元件

- `DropdownMenu` 會連同名稱一起列出動作。
- `ActionMorph` 把一個動作展開成一個小面板。

| 元件 | 何時選擇 |
|---|---|
| `OrbitMenu` | 幾個大家熟悉的動作，一伸手就能快速選到 |
| `DropdownMenu` | 動作需要名稱，或數量很多 |
| `ActionMorph` | 一個動作需要一個欄位或幾個選項 |
