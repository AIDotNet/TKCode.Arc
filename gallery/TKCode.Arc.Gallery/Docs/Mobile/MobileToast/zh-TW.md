---
title: 移動端輕提示
summary: 在靠近底部處滑入的短暫訊息，會自行消失，也可以滑開關閉。
source-hash: 34bb56491f76
---

<Demo name="Basic" />

## 何時使用

`Toast` 告訴使用者發生了什麼，而不要求任何回應：郵件已封存、連結已複製、提醒已設定。

- **已完成操作的確認**，不需要回答。
- **輕量的錯誤或狀態**，使用者可以忽略。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋和滑動這裡沒有驗證。

## 何時不用

- **需要做決定**：用 `Dialog`。
- **絕不能錯過的訊息**，或使用者必須據此行動的訊息：Toast 幾秒後會自行消失。
- **長文字**：Toast 只有一兩行。

## 用法

在頁面的 `Overlay` 槽裡放一個 `Toast`，有話要說時呼叫 `Show`。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Toast Name="Toasts" Policy="Stack" MaxVisible="3" />
    </arcm:Page.Overlay>
</arcm:Page>
```

```csharp
Toasts.Show("Message archived");
Toasts.Show("Reminder set", TimeSpan.FromSeconds(5), ArcIcons.Bell);
Toasts.Show("Offline", TimeSpan.Zero); // stays until swiped away or DismissAll()
```

除非給 `Show` 傳入時長，Toast 會停留 `DefaultDuration`（3 秒）。手指按在 Toast 上會暫停它的倒數計時，放開後從停下的地方繼續；向任一側滑過 72 px，或快速一揮，就會把它送走。

`Policy` 決定在已有 Toast 顯示時新來一則會怎樣。`Stack` 保留最新的 `MaxVisible` 則，最新的在最下面，最舊的被送走。`Replace` 一次只顯示一則：新的取代正在顯示的。

Toast 位於頁面頁尾和底部安全區之上 `BottomOffset`（16）處：它會自己找到所在的 `Page`，加上頁尾的高度和 `SafeArea.Bottom`，所以不會壓住 `Tabbar`。浮層的其餘部分不攔截觸控，所以 Toast 顯示時頁面仍可操作。

### 同一頁面上的多個浮層

`Page.Overlay` 只能放一個元素。要讓 Toast 與底部面板或對話框同時存在，把它們放進該槽裡的一個 `Grid`，Toast 放在最後，讓它顯示在其他浮層之上。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <Grid>
            <arcm:Sheet Title="Share" />
            <arcm:Dialog Title="Delete?" />
            <arcm:Toast Name="Toasts" />
        </Grid>
    </arcm:Page.Overlay>
</arcm:Page>
```

### Token

`ToastToken` 包含 `BodyMinHeight`（44）、`BodyMaxWidth`（360）、`BodyPadding`、`StackGap`（8）、`SideInset`（16）和 `GlyphSize`（18）。Toast 是一顆反色膠囊：以 Foreground 色為填充，以 Background 色為文字。

## API 參考

<!-- api: MobileToast -->

## 無障礙

訊息以禮貌的方式朗讀，不搶焦點。因為 Toast 會自行消失，不要把重要資訊只放在其中，文字要短。滑動只是捷徑：Toast 本來也會自己離開。Toast 至少 44 高，手指可以按住它。

## 動效

Toast 上升 16 px 並在一根靈敏的彈簧上淡入，離開時更快，用 0.16 秒且沒有過衝，所以下一則 Toast 不必等上一則。出現狀態和滑動位移是程式碼驅動的運動值：滑動時每次移動都會寫入位移，放手時手指的速度交給彈簧，把沒滑完的 Toast 帶回原位。倒數計時是可暫停的時間軸，所以即使沒有繪製影格，也能準時結束。減弱動效時，Toast 只淡入淡出，用時 0.12 秒，滑動在放手時才判定，不跟隨手指；關閉動效時，它立即出現和消失。

## 相關元件

- `MobilePage`：`Overlay` 槽承載 Toast，頁尾和安全區決定 Toast 的位置。
- `MobileDialog`：用於需要做決定時。
- `MobileSheet`：用於選項和表單。
- `MobileTabbar`：Toast 在它之上升起。
