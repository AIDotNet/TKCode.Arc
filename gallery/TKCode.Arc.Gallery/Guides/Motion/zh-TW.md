---
title: 動態效果
source-hash: 09a655958638
---

# 動態效果

TKCode.Arc 中的動態效果遵循 Arc 的動態效果體系：凡是移動或改變尺寸的內容使用彈簧，顏色和不透明度使用簡短的緩動淡入淡出，文字變化則使用滾動數字和逐詞改寫。動態效果的作用是說明發生了什麼變化（面板從哪裡來、哪個數值移動了、哪個詞是新的），而不是裝飾。程式庫中的每個動畫都遵循動態效果級別，因此對動態敏感的使用者，以及需要保持輕量運作的裝置，都能獲得更平靜的介面，同時不遺失任何資訊。

## 動態效果級別

共有三種級別：`ArcMotionLevel.Full`、`Reduced` 和 `Off`：

| 級別 | 移動的內容 | 適用情境 |
|---|---|---|
| `Full`（預設） | 彈簧、位移、縮放、變形、模糊、錯開入場、滾動數字 | 一般體驗。 |
| `Reduced` | 僅短淡入淡出和顏色變化；位置與尺寸直接跳到終點，裝飾性循環停止 | 要求減少動態效果的使用者、遠端桌面工作階段、簡報。 |
| `Off` | 無；所有變化立即生效 | 測試與截圖、自助服務終端機和低功耗裝置。 |

資訊不會僅由動態效果傳遞：在 `Reduced` 和 `Off` 下，最終狀態與 `Full` 下完全相同。

### 設定級別

`ArcMotion.Level` 是一個可繼承的附加屬性。在視窗上設定會影響其中的全部內容；在任意容器上設定則只影響介面的該部分：

```csharp
ArcMotion.SetLevel(mainWindow, ArcMotionLevel.Reduced);
```

```xml
<StackPanel arc:ArcMotion.Level="Off" Spacing="12">
    <arc:Progress Label="匯入" Value="64" />
    <arc:Switch Content="在背景同步" IsChecked="True" />
</StackPanel>
```

主題也可以為整個應用程式設定級別，方法是透過 `WithArc(arc => arc.MotionMode(...))`（參見 **主題**）。控制項的實際級別取兩者中較低的那個：繼承的 `ArcMotion.Level` 與主題的動態效果模式。在設為 `Reduced` 的主題中，即使視窗設為 `Full`，也會以減弱動態效果執行。

程式庫本身不會讀取作業系統的減少動態效果偏好。請在應用程式中提供設定（圖庫的外觀面板提供 Full、Reduced 和 Off 三個選項），將其與使用者的其他偏好一起儲存，並在啟動時套用到主視窗。

## 動態效果系統

### 彈簧

Arc 的彈簧由它們呈現出的時長與回彈程度定義，而不是由物理常數定義。預設是 `ArcSpring` 值，也可透過 `ArcMotionPresets` 取得：

| 名稱 | 形態 | 用途 |
|---|---|---|
| `Smooth` | 0.4 s，無回彈 | 反映狀態的動態效果：開關滑塊、面板、進度和數值。 |
| `Snappy` | 0.26 s，回彈 0.12 | 按壓、小型指示器、圖示切換、彈入和選單。 |
| `Morph` | 0.42 s，回彈 0.16 | 共用反白、隨內容變化的寬度、形狀變形。 |
| `Responsive` | 剛度 520，阻尼 38 | 必須跟上游標的直接操作。 |
| `Gentle` | 剛度 340，阻尼 34 | 幅度較大、較平緩的移動。 |

動畫執行期間啟動的彈簧，會從目前的位置和速度繼續，因此打斷動畫（連按兩次按鈕、拖回）時永遠不會跳動。

### 時長與緩動

| 權杖 | 數值 | 用途 |
|---|---|---|
| `DurationInstant` | 120 ms | 懸停與按壓的顏色。 |
| `DurationFast` | 160 ms | 小幅淡入淡出，以及大多數位移在減弱動態效果下的替代方案。 |
| `DurationExit` | 180 ms | 元素離開；離場比入場更快。 |
| `DurationStandard` | 240 ms | 大多數淡入淡出和顏色過渡。 |
| `DurationConsidered` | 480 ms | 幅度較大、經過深思的變化。 |
| `DurationSpring` | 580 ms | CSS 風格的彈簧曲線。 |

緩動曲線為 `EaseEnter`、`EaseExit`、`EaseStandard` 和 `EaseInOut`（程式碼中為 `ArcEasings`）。錯開入場在元素之間使用 `StaggerChar`（16 ms）、`StaggerWord`（40 ms）、`StaggerLine`（80 ms）和 `StaggerItem`（35 ms）。

### 效能

動態效果執行於共用的影格排程器上：正在執行的彈簧和循環會在每一個影格合併為一批同步推進，沒有任何內容移動時則不安排任何工作。持續性效果（骨架畫面脈動、閃光、即時指示點）在其控制項捲出可視範圍、被隱藏或從視覺樹中分離時暫停。動態模糊量化為 0.25 px 的步進並可重複使用，因此執行中的模糊不會每個影格配置記憶體。

## 在自己的控制項中使用動態效果

程式庫遵循的規則同樣適用於你自己撰寫的控制項。

### 控制項主題中的過渡

在控制項的 `ControlTheme` 中，於動態效果偽類別下宣告過渡，並設定 `arc:ArcMotion.Tracks="True"`，以便控制項接收這些過渡。載入後，被追蹤的控制項會取得 `:motion-full` 或 `:motion-reduced`，因此第一個畫面永遠不會產生動畫；級別為 `Off` 時兩者都不會取得，因此不會有任何動畫：

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <ControlTheme x:Key="StatusPanelTheme" TargetType="Border">
        <Setter Property="arc:ArcMotion.Tracks" Value="True" />
        <Style Selector="^:motion-full">
            <Setter Property="Transitions">
                <Transitions>
                    <arc:SpringDoubleTransition Property="Opacity" Spring="Smooth" />
                    <arc:SpringThicknessTransition Property="Padding" Spring="Snappy" />
                </Transitions>
            </Setter>
        </Style>
        <Style Selector="^:motion-reduced">
            <Setter Property="Transitions">
                <Transitions>
                    <DoubleTransition Property="Opacity" Duration="0:0:0.16" />
                </Transitions>
            </Setter>
        </Style>
    </ControlTheme>
</ResourceDictionary>
```

彈簧過渡（`SpringDoubleTransition`、`SpringThicknessTransition`、`SpringCornerRadiusTransition`、`SpringSizeTransition`、`SpringPointTransition`、`SpringVectorTransition` 和 `SpringTransformOperationsTransition`）接受 `Spring` 預設，或自訂的 `VisualDuration` 與 `Bounce`。在減弱動態效果下，用短暫的不透明度淡入淡出取代移動，並讓尺寸和位置立即變化。

### 控制項主題無法表達的動態效果

當某個數值必須跟隨游標、在打斷時保持速度，或驅動自訂繪製時，請使用 `MotionValue<T>`。它遵循其所屬物件的實際動態效果級別：在 `Off` 下，以及在 `Reduced` 下的彈簧，都會直接跳到目標值。

```csharp
public sealed class SpendBar : Control
{
    private readonly MotionValue<double> _fill;
    private double _shown;

    public SpendBar()
    {
        _fill = new MotionValue<double>(this, 0, MotionVectors.Double);
        _fill.Changed += value =>
        {
            _shown = value;
            InvalidateVisual();
        };
    }

    public void ShowShare(double share) => _ = _fill.AnimateTo(share, MotionSpec.Spring(ArcSpring.Smooth));

    public override void Render(DrawingContext context)
    {
        var accent = ArcTokens.GetColor(this, ArcDesignTokenKind.Accent);
        context.FillRectangle(new SolidColorBrush(accent), new Rect(0, 0, Bounds.Width * _shown, Bounds.Height));
    }
}
```

`MotionVectors` 涵蓋 `double`、`Point`、`Vector`、`Size`、`Rect`、`Thickness`、`CornerRadius` 和 `Color`；每個分量各自執行自己的彈簧。`MotionSpec.Tween(duration, easing)` 執行定時曲線而非彈簧，`MotionSpec.Instant` 直接跳到目標。對於循環（脈動、掃光），使用 `LiveLoop`：它在螢幕外以及低於其最低動態效果級別時暫停；對於在固定時間內執行到 1 的數值（長按確認的填滿、倒數計時），使用 `MotionTimeline`。

只有當控制項主題無法表達時才使用程式碼驅動的動態效果，並在旁邊的註解中說明原因。主題過渡的成本較低，更容易審查，並且會自動遵循動態效果級別。

## 指導原則

**建議**

- 為變化而設計動畫，而不是為元素而設計：讓移動的內容移動，讓出現的內容淡入，讓變化的數字滾動。
- 入場控制在半秒以內，離場比入場更快。
- 為每個動畫變化提供在沒有動畫時也能正確呈現的終態，然後用 `Off` 檢查。
- 按壓的回饋使用 `Snappy`，回報數值的內容使用 `Smooth`。

**避免**

- 僅用動態效果作為狀態變化的訊號；應與文字、圖示或顏色搭配。
- 同時為大量大型表面加上動畫；應錯開進行，或只對容器做動畫。
- 在代表金額、進度或錯誤的內容上使用回彈：它會顯得輕浮。
- 在主要內容中使用無限循環；應留給載入狀態，並在內容到達時停止。

## 測試

- 使用 `ArcMotionLevel.Off` 執行測試並截圖，使結果顯示最終狀態，而不依賴時序。程式庫自身的測試和圖庫截圖都是這樣做的。
- 用 `Reduced` 檢查每個畫面一次：不應有任何滑動、縮放或回彈，也不應缺少任何內容。
- 注意 `Off` 下的版面跳動：如果某個容器只有依靠尺寸動畫才顯得正確，則需要固定的版面配置。
