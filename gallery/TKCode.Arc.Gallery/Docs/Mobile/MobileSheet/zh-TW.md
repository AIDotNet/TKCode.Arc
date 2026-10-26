---
title: 移動端底部面板
summary: 從頁面底部升起、帶遮罩的面板，點遮罩、下拉把手或快速下滑即可關閉。
source-hash: 23b17ea56a33
---

<Demo name="Basic" />

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

底部安全區目前還沒有處理：在有 Home 指示條的手機上，請把這部分空間加進面板的 `Padding`。

### Token

`SheetToken` 保存 `ScrimColor`（遮罩完全不透明時的顏色，明暗模式不同）、`PanelRadius`（頂部兩個角為 16）、`GrabberWidth`（36）、`GrabberHeight`（5）和 `HandlePadding`。顏色、圓角和字型來自 ArcDesign token。

## API 參考

<!-- api: MobileSheet -->

## 無障礙

Esc 會關閉可關閉的面板。請給面板設定 `Title`：它是把手區域裡的第一段文字。開啟時焦點會移到面板上。點遮罩只是捷徑，不是唯一的方式：面板裡有表單時，請在裡面保留一個看得見的關閉方式。只有把手區域可以拖曳，所以捲動內容不會拖動面板。

## 動效

面板的位置是程式碼驅動的運動值：拖曳時每次移動都會寫入它，放開時手指的速度交給彈簧（開啟 0.4 秒平滑，關閉 0.3 秒），這是主題裡宣告的過渡做不到的。遮罩的不透明度跟隨面板的位置。減弱動效時，面板直接跳到位置而不滑動。

## 相關元件

- `MobilePage`：`Overlay` 槽承載面板。
- `MobileListGroup`：操作面板常用的內容。
- `MobileButton`：開啟面板的常用方式。
