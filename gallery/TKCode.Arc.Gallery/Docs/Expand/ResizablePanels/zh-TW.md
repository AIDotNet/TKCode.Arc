---
title: 可調整大小的面板
summary: 拖曳窗格之間的分隔線，在各窗格之間調配空間。
source-hash: e535bd1502d7
---

## 何時使用

- 編輯器、郵件或儀表板版面，使用者需要自行決定每個窗格佔用多少空間。
- 可以收合的側邊窗格，收合後為主窗格騰出空間。
- 需要在工作階段之間儲存並還原的版面。

## 何時不用

- 以浮層形式覆蓋頁面的窗格：用 `Drawer`。
- 一次只顯示一個窗格：用 `Tabs`。
- 上下堆疊的窗格：Arc 只支援水平分割。請改用版面容器。
- 窄螢幕下窗格需要堆疊顯示：用 `Tabs`，並讓頁面改變其版面。

## 用法

為 `Panels` 中的每個窗格加入一個 `ResizablePanel`。每個窗格都有一個 `DefaultSize` 份額。各份額是相對值，不必加總為 100。該群組需要父層提供明確的高度。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ResizablePanels Label="郵件版面" Height="320">
        <arc:ResizablePanel Id="folders"
                            Label="資料夾"
                            DefaultSize="22"
                            MinSize="140"
                            IsCollapsible="True">
            <TextBlock Text="收件匣、草稿、已傳送、封存" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="messages"
                            Label="郵件清單"
                            DefaultSize="33"
                            MinSize="200">
            <TextBlock Text="42 則對話" />
        </arc:ResizablePanel>
        <arc:ResizablePanel Id="reading"
                            Label="閱讀窗格"
                            DefaultSize="45"
                            MinSize="220"
                            IsCollapsible="True">
            <TextBlock Text="季度回顧" />
        </arc:ResizablePanel>
    </arc:ResizablePanels>
</UserControl>
```

`Label` 為輔助技術提供該群組的名稱。每個窗格的 `Label` 為其分隔線命名。

### 預設收合

在可收合的窗格上設定 `IsCollapsed="True"`，即可使其初始時處於隱藏狀態。其分隔線會顯示一個用於還原該窗格的標籤。

```xml
<arc:ResizablePanel Id="files"
                    Label="檔案"
                    DefaultSize="25"
                    IsCollapsible="True"
                    IsCollapsed="True">
    <TextBlock Text="src / components / panels.tsx" />
</arc:ResizablePanel>
```

### 限制

`MinSize` 是窗格開始產生阻力時的寬度（以像素計）。`MaxSize` 限定寬度上限，預設不限制。

```xml
<arc:ResizablePanel Id="code" Label="程式碼" DefaultSize="50" MinSize="240" MaxSize="800" />
```

### 儲存與還原版面

`Sizes` 以百分比保存每個窗格的份額，且為雙向繫結。在使用者完成一次調整後儲存它，再次設定即可還原版面。`LayoutChanged` 會在調整結束、版面穩定後觸發。

```csharp
panels.LayoutChanged += (_, e) =>
{
    settings.PaneSizes = e.Sizes.ToArray();
};

panels.Sizes = settings.PaneSizes;
```

### 重設

`Reset` 會將各窗格還原為預設份額，並適配目前的寬度。

```csharp
panels.Reset();
```

## API 參考

<!-- api: ResizablePanels -->

<!-- api: ResizablePanel -->

## 無障礙

- 每個分隔線都可取得焦點。其值為該窗格的份額；對於隱藏的窗格，會以「按 Enter 顯示」的提示進行朗讀。
- 該群組以其 `Label` 命名並作為一個群組朗讀。收合的窗格對輔助技術隱藏。
- 左右方向鍵以小步幅調整分隔線。按住 Shift 再按方向鍵則以大步幅調整。
- Home 與 End 將分隔線移動到最小與最大位置。Enter 可隱藏或還原可收合的相鄰窗格。
- 指標拖曳過程中按 Escape，分隔線會返回到拖曳開始時的位置。

## 動態效果

- 拖曳時分隔線直接跟隨指標。超過窗格的最小尺寸後，分隔線會以短促的橡皮筋效果產生阻力。
- 放開時，窗格以保留指標速度的彈簧回落到位。將可收合的窗格拖到遠低於其最小尺寸，或朝邊緣快速甩動，即會將其收合。
- 鍵盤調整使用靈敏的彈簧。
- 窗格收合時會彈出一個還原標籤，窗格縮小時其內容淡出。
- 減少動態效果時，窗格直接移動到目標位置，還原標籤僅淡入。

## 相關元件

- `Drawer`：用於以浮層形式覆蓋頁面的窗格。
- `Tabs`：用於一次只顯示一個窗格，或用於窄螢幕版面。
- `ScrollArea`：用於在窗格內溢出的內容。
