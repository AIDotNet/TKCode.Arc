---
title: 移動端底部面板
summary: 從頁面底部升起、帶遮罩的面板，可停在多個高度，點遮罩、下拉把手或快速下滑即可關閉。
source-hash: a5a3d8786439
---

<Demo name="Basic" />

<Demo name="Detents" />

## 何時使用

`Sheet` 是移動端對彈窗的回答：從底部邊緣升起的面板，承載一個簡短的任務，完成後離開。

- **選擇和操作**：分享選項、操作清單、篩選。
- **簡短的表單或詳情**，不想讓人離開目前頁面。
- **桌面應用會放進下拉或選單的內容**：手機上，底部面板更容易搆到、也更容易點中。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上的觸控回饋和拖曳這裡沒有驗證。

## 何時不用

- **有自己返回堆疊的流程步驟**：那是一個頁面，不是面板。
- **簡短的確認**：用對話框。
- **比螢幕還高的內容**：面板最高到頁面的 90%，之後內容捲動；很長的任務適合整頁。

## 用法

把面板放進頁面的 `Overlay` 槽（它蓋住頁首和頁尾），用 `IsOpen` 開啟。面板高度隨內容而定；點遮罩、下拉把手、快速下滑或按 Esc 都能關閉它。

```xml
<arcm:Page xmlns="https://github.com/avaloniaui"
           xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile">
    <arcm:Page.Overlay>
        <arcm:Sheet Title="分享" IsOpen="{Binding ShowShare}">
            <arcm:ListGroup>
                <arcm:ListItem Content="傳送給某人" ShowChevron="True" />
                <arcm:ListItem Content="複製連結" />
            </arcm:ListGroup>
        </arcm:Sheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

`IsOpen` 是雙向繫結，所以手勢關閉面板時會把 `false` 寫回去。需要只能用按鈕關閉的面板時，設定 `IsDismissible="False"`；此時向下拉會像橡皮筋一樣有阻力。

拖曳時面板一比一跟隨手指。放開時，面板按手指的速度向前看 0.2 秒：過了中點就關閉，向下的快速下滑超過 500 px/s 時無論從哪裡都會關閉；否則它彈回開啟的位置。

### 停靠高度

設定 `Detents`（按面板所在區域高度的比例，可從 `"0.4, 0.9"` 這樣的文字解析），面板就會停在多個高度：先露出一部分，再展開到完整檢視。它在 `InitialDetentIndex`（0，最低的一個）處開啟。`DetentIndex` 表示它停在哪裡，停到另一個高度時觸發 `DetentChanged`。`SnapTo(index)`、`Expand()` 和 `Collapse()` 可從程式碼裡移動已開啟的面板。

```xml
<arcm:Sheet Title="附近" Detents="0.4, 0.9" InitialDetentIndex="0">
    ...
</arcm:Sheet>
```

```csharp
nearby.DetentChanged += (_, e) => Log($"{e.Index}: {e.Fraction}");
```

拖曳時面板一比一跟隨手指，越過最高的停靠高度後像橡皮筋一樣有阻力。放開時，面板按手指的速度向前看 0.2 秒，停在最近的停靠高度；若最近的是底部則關閉；速度超過 500 px/s 的快速滑動至少會移動一個停靠高度。只有一個值時是固定高度。內容只在最高的停靠高度捲動：低於它時，手指或滾輪會把面板升起來；內容捲到頂部後再下拉，又會移動面板。沒有設定 `Detents` 時，面板保持隨內容而定的單一高度。

### 底部安全區

面板不會讀取平台資訊。用 `SafeAreaBottom`（單位 px 的數字）給內容下方留出空間，例如繫結到頁面的安全區：

```xml
<arcm:Page x:Name="Screen">
    <arcm:Page.Overlay>
        <arcm:Sheet SafeAreaBottom="{Binding SafeArea.Bottom, ElementName=Screen}" Title="分享">
            ...
        </arcm:Sheet>
    </arcm:Page.Overlay>
</arcm:Page>
```

### Token

`SheetToken` 保存 `ScrimColor`（遮罩完全不透明時的顏色，明暗模式不同）、`PanelRadius`（頂部兩個角為 16）、`GrabberWidth`（36）、`GrabberHeight`（5）和 `HandlePadding`。顏色、圓角和字型來自 ArcDesign token。

## API 參考

<!-- api: MobileSheet -->

## 無障礙

Esc 會關閉可關閉的面板。請給面板設定 `Title`：它是把手區域裡的第一段文字。開啟時焦點會移到面板上。點遮罩只是捷徑，不是唯一的方式：面板裡有表單時，請在裡面保留一個看得見的關閉方式。把手區域可用任何指標拖曳，而內容只在觸控時才會拖動面板，所以滑鼠滾輪或捲動軸不會移動它。有停靠高度時，用 `DetentIndex` 和 `DetentChanged` 告訴應用程式面板停在哪個高度。

## 動效

面板的位置是程式碼驅動的運動值：拖曳時每次移動都會寫入它，放開時手指的速度交給彈簧（開啟 0.4 秒平滑，關閉 0.3 秒），這是主題裡宣告的過渡做不到的。遮罩的不透明度每幀跟隨面板的位置：最高的停靠高度時最深，多個高度中最低的一個稍淺，關閉時完全透明。在停靠高度之間移動是從面板目前位置出發的一次彈簧，所以再次點按或拖曳會無縫打斷它。減弱動效時，面板直接跳到停靠高度而不滑動。

## 相關元件

- `MobilePage`：`Overlay` 槽承載面板。
- `MobileListGroup`：操作面板常用的內容。
- `MobileButton`：開啟面板的常用方式。
- `MobileActionSheet`：現成的操作清單，帶一個取消列。
