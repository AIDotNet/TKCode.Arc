---
title: Dot grid
summary: 一片點陣，像透鏡一樣在指標周圍向外鼓起，每次點擊都會向外擴散一圈漣漪。
source-hash: eea42c7255b6
---

<Demo name="Preview" />

## 何時使用

`DotGrid` 用一片安靜的點陣鋪滿一塊區域，並對指標做出反應。指標周圍的點像透鏡一樣向外鼓起，並染上強調色；點擊或輕觸會讓一圈波動掃過整片點陣。放在裡面的內容浮在點陣之上，照常可用。

- **登陸頁首屏**：不用影片或厚重插圖，也能讓標題有生氣。
- **登入面板、空狀態與倒數計時**：給等待中的人一個可以把玩的背景。
- **頁面分隔**：在長頁面的兩個部分之間放一條讓人想戳一下的帶子。

## 何時不用

- **密集的文字、表格或圖表背後**：移動的點會干擾閱讀。只在標題與少量操作背後使用。
- **不需要任何反應時**：用筆刷畫一張靜態的點陣圖案成本更低。
- **同一個畫面放好幾個**：一個檢視裡有一個會反應的背景就夠了。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:DotGrid Height="420">
        <TextBlock Text="Built to be poked at" HorizontalAlignment="Center" VerticalAlignment="Center" />
    </arc:DotGrid>
</UserControl>
```

`DotGrid` 是 Special 套件中的 `ContentControl`。在 `UseArcControls()` 旁呼叫 `UseArcSpecial()` 註冊此套件。給控制項一個高度，或讓內容撐開它；最小高度為 240px。點陣本身沒有背景：設定 `Background`、`BorderBrush`、`BorderThickness` 與 `CornerRadius` 為它加上外框，點會被裁切在這個圓角框內。

### 用作首屏

把標題與操作放在裡面。點陣在整個控制項範圍內監聽指標，內容上方也一樣，所以使用者移向按鈕時透鏡仍會跟隨。

<Demo name="Hero" />

### 密度、透鏡與顏色

`Gap` 是點與點的間距（預設 22px，最小 6）。數值越小點越密：720 × 420 的區域在預設間距下約有 660 個點，間距為 14 時約有 1,600 個。`DotSize` 是靜止時點的半徑。`Radius` 是透鏡的作用範圍，`Strength` 是透鏡在最強處（距指標三分之一半徑處）把點推開的距離。`DotBrush` 與 `ActiveDotBrush` 分別設定靜止色與點亮色，預設使用強邊框色與強調色 Token，因此點陣會跟隨主題與強調色變化。

<Demo name="Colors" />

```xml
<arc:DotGrid Height="300"
             Gap="18"
             Radius="200"
             Strength="24"
             IsFadeEnabled="False"
             IsRippleEnabled="False"
             ActiveDotBrush="{arc:ArcDesignTokenResource Series2}" />
```

`IsFadeEnabled` 讓點在靠近邊緣處逐漸淡出（預設開啟），這樣點陣不會在一條硬邊上突然結束。`IsRippleEnabled` 用來關閉點擊漣漪。

### 以程式碼觸發漣漪

`Ripple(point)` 從控制項座標中的某一點發出一圈漣漪，效果與在那裡點擊相同。可以用它回應別處發生的事，例如表單已送出。

```csharp
field.Ripple(new Point(field.Bounds.Width / 2, field.Bounds.Height / 2));
```

### 最佳做法

**建議**

- 內容保持簡短並置中：一個標題、一句話與一兩個操作。
- 全寬首屏與效能較弱的裝置上使用較大的 `Gap`（26 到 32）。
- 用 `ActiveDotBrush` 而不是靜止色來配合品牌，顏色由被點亮的點呈現。

**避免**

- 在很大的區域上使用很小的間距。每個點都要模擬，成本隨點數增加。
- `Strength` 遠大於間距：點會互相穿過，透鏡失去形狀。

### 常見問題

**裡面的按鈕還能用嗎？** 可以。點陣畫在內容下方，從不搶走指標，只是觀察指標經過。

**為什麼點不動？** 減少動態效果時每個點都保持原位，只點亮指標附近的點。關閉動態效果時點陣完全靜止。

## API 參考

<!-- api: DotGrid -->

## 無障礙

- 點陣只是裝飾。它無法取得焦點，也不會出現在自動化樹中；內容保留自己的名稱與角色，並留在閱讀順序中。
- 只有當這塊區域本身有意義時才設定 `AutomationProperties.Name`，此時它會被讀作一個群組。
- 減少動態效果時點從不移動。透鏡只點亮指標附近的點，點擊也不會產生漣漪。

## 動態效果

- 每個點都沿著自己的輕阻尼彈簧移向透鏡與漣漪要求的位移，所以點陣會輕輕晃動後靜止，而不是突然跳動。
- 透鏡以很短的延遲跟隨指標，指標到來時淡入、離開時淡出。它的推力在距指標三分之一半徑處最強，到邊緣處降為零；被它點亮的點會變大並換成點亮色。
- 漣漪以每秒 620px 的速度向外擴散，在 1.5 秒內淡出。最多六圈漣漪可以疊加，第七圈會取代最早的一圈。
- 觸控螢幕上只有手指按下時才出現透鏡，每次輕觸都會產生漣漪。
- 只有在有東西移動時才請求影格，點陣捲出畫面時暫停。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 透鏡 | 推開並點亮點 | 只點亮點 | 無 |
| 漣漪 | 掃過整片點陣 | 無 | 無 |

## 相關元件

- `GravityWell` 讓一格格圖塊（而不是點陣）圍繞指標彎曲。
- `InkFluid` 用隨指標攪動的墨水填滿首屏。

| 元件 | 適用情境 |
|---|---|
| `DotGrid` | 想要一個帶透鏡的輕量幾何背景 |
| `GravityWell` | 網格本身就是內容：照片、頭像或標誌 |
| `InkFluid` | 想要一個柔和、有機的背景 |
