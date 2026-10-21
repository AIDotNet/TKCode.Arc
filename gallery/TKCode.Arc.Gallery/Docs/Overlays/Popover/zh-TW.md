---
title: 彈出框
summary: 用於提供情境資訊的小型定位浮層。
source-hash: 144bfe7cbde0
---

<Demo name="Share" />

## 何時使用

`Popover` 在開啟它的控制項旁邊承載一小塊可互動的內容：分享設定、清單工具列上的篩選表單，或由你自己的控制項組成的挑選器。它錨定在觸發器上，因此閱讀者能保有頁面的情境；它預設為非模態，因此開啟時頁面其餘部分仍可操作。

- **文件或記錄的分享與存取設定。** 文件頁首中的「分享」按鈕會開啟一個面板，包含連結、複製控制項和權限選擇，無需帶閱讀者跳到設定頁。
- **清單工具列上的篩選。** 訂單或發票清單上的「狀態」與日期範圍篩選，從「篩選」按鈕開啟，套用其值後關閉，因此閱讀者調整條件時表格仍然可見。
- **資料表格中的欄位或檢視選項。** 「欄位」按鈕把可選欄位列為核取方塊，閱讀者無需離開表格即可顯示或隱藏欄位。
- **畫布或圖表中由程式碼驅動的面板。** 應用程式在選取某個節點時呼叫 `ShowAt` 並傳入該節點的控制項，在旁邊開啟一個小型編輯器，面板隨選取項目移動。

## 何時不用

- **懸停或聚焦時的單行標籤：** 用 `Tooltip`。彈出框由點選開啟並承載控制項；工具提示是唯讀文字，在懸停時出現。
- **人員或連結的唯讀預覽：** 用 `HoverCard`，它在懸停時開啟，且從不取得焦點。
- **使用者繼續之前必須作出的決定：** 用 `Dialog`。點選彈出框外部會將其關閉，因此它無法承載「刪除工作區」這類確認操作。
- **僅有指令清單：** 用 `DropdownMenu`，它為操作提供方向鍵導覽和選單語意。
- **長表單或多步驟流程：** 使用頁面、`Drawer` 或 `Dialog`。彈出框的尺寸只適合幾個欄位；欄位很多的表單會把下面的頁面推到螢幕之外。

## 用法

`Popover` 是一個 `ContentControl`。它的 `Content` 是面板，`Trigger` 是開啟面板的控制項。面板顯示在承載於彈出層中的 `PopoverSurface` 裡，因此它不參與版面配置，並且可以超出觸發器的邊緣。觸發器與面板相互關聯：面板依據觸發器測量位置，因此觸發器只以顏色回應按壓，不縮放。

### 結構

1. **觸發器**（`Trigger`）：開啟面板的控制項。它以自身尺寸保留在頁面中；彈出框不會在它周圍加上任何版面配置。
2. **面板表面**：`PopoverSurface`，使用浮起表面色（`SurfaceRaised`）、前景文字色、1 px 的邊框（顏色為邊框色 `Border`）以及面板圓角（`RadiusPanel`）。四周各有 16 px 的內距，寬度至少 192 px（12 rem），最多 352 px（22 rem），使用內文字型的小號字級（`TypeSM`）。浮起陰影由彈出層繪製。
3. **內容**（`Content`）：面板的內容。字串會在表面的預設文字範本中以自動換行的文字顯示；其他任何控制項則原樣顯示。
4. **間距**（`Offset`）：預設情況下觸發器與面板之間為 6 px，面板的進場動態效果從此距離開始。

### 觸發器與內容

將 `Trigger` 設為開啟面板的控制項。彈出框的內容即為面板。

```xml
<arc:Popover>
    <arc:Popover.Trigger>
        <arc:Button Variant="Secondary" Content="分享" />
    </arc:Popover.Trigger>
    <StackPanel Spacing="8">
        <TextBlock Text="分享連結" FontWeight="Medium" />
        <TextBlock Text="任何擁有連結的人都可以檢視。" />
    </StackPanel>
</arc:Popover>
```

啟動觸發器會切換彈出框：點選、輕觸或以鍵盤啟動按鈕都會開啟它，同一操作會再次將其關閉。非按鈕觸發器則在按下 `Enter` 或 `Space` 時開啟彈出框。

### 附加到控制項

`arc:Popover.Attach` 可以為任意控制項加上彈出框。點選該控制項會切換面板。當控制項本身就是閱讀者預期的按鈕、面板無需單獨的觸發器時使用它。

```xml
<arc:Button Content="篩選">
    <arc:Popover.Attach>
        <arc:Popover Placement="Top">
            <TextBlock Text="在頂部一側開啟。" />
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

附加的彈出框開啟時會加入其目標的邏輯樹，因此目標的樣式和 `DataContext` 會在面板內生效。關閉時它會離開該樹。

### 位置與對齊

`Placement` 是偏好的一側：`Top`、`Bottom`（預設）、`Left` 或 `Right`。當偏好的一側在視窗中放不下時，面板會翻轉到另一側，其開啟動態效果跟隨它實際落定的那一側。`Alignment` 決定面板沿觸發器邊緣的位置：`Start`（預設）、`Center` 或 `End`。`Offset` 是與觸發器之間的間距，單位為像素，預設為 6。彈出框開啟時修改 `Placement`、`Alignment` 或 `Offset`，面板會立即移動。

<Demo name="Sides" />

```xml
<arc:Popover Placement="Bottom" Alignment="Center" Offset="8">
    <arc:Popover.Trigger>
        <arc:Button Variant="Ghost" Content="篩選" />
    </arc:Popover.Trigger>
    <TextBlock Text="狀態：任意" />
</arc:Popover>
```

對於清單左側的工具列按鈕，使用 `Bottom` 搭配 `Start`，讓面板與按鈕的左邊緣對齊。對於一列中間的控制項使用 `Center`，對於頁面右邊緣的按鈕使用 `End`，這樣面板不會超出視窗。預設的 6 px 間距是 Arc 的外觀。

### 從程式碼中開啟

`ShowAt` 將彈出框錨定到視窗中的任意控制項，因此同一個彈出框可以服務於多個觸發器，或使用者選取的某個節點。`IsOpen` 是雙向屬性，`Opened` 與 `Closed` 會回報狀態變化。彈出框開啟時以另一個錨點呼叫 `ShowAt`，會先將其關閉，再在新錨點處重新開啟。`Close` 從程式碼中關閉它，`OpenCommand` 與 `CloseCommand` 則以 `ICommand` 的形式提供同樣兩個操作，供按鈕與繫結使用。

<Demo name="FromCode" />

```csharp
share.ShowAt(shareButton);
share.Closed += (_, e) => status.Text = $"已關閉：{e.Reason}";
```

`Closed` 攜帶一個 `Reason`：閱讀者按下 Escape 時為 `Escape`，在面板外按下時為 `PressOutside`，觸發器切換、程式碼呼叫 `Close` 或值被設為 false 時為 `Programmatic`。

### 模態彈出框

將 `IsModal` 設為 true 後，面板外的按壓會被攔截，它們只會關閉彈出框，不會到達下面的控制項。面板外的按壓被攔截：它會關閉彈出框，且不會到達下面的控制項。適用於閱讀者在點選其他地方之前必須作出或取消的簡短選擇；當內容需要確認或自己的遮罩時，使用 `Dialog`。

```xml
<arc:Popover IsOpen="{Binding ShowPicker}" IsModal="True">
    <TextBlock Text="請先選擇一個選項。" />
</arc:Popover>
```

### 小型表單

彈出框可以承載小型表單，例如篩選條件。開啟時頁面仍可操作。

<Demo name="SmallForm" />

### 繫結到檢視模型

當檢視模型決定面板是否開啟時繫結 `IsOpen`，例如工具列指令應開啟一個篩選面板。`IsOpen` 是雙向的，因此在面板外按壓也會更新檢視模型。

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class OrdersViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isFilterOpen;

    [ObservableProperty]
    private string? _status;

    [RelayCommand]
    private void ApplyFilters()
    {
        IsFilterOpen = false;
        // 依所選狀態與日期重新載入訂單。
    }
}
```

```xml
<arc:Button Content="篩選" Variant="Ghost">
    <arc:Popover.Attach>
        <arc:Popover IsOpen="{Binding IsFilterOpen}" Placement="Bottom" Alignment="Start">
            <StackPanel Width="240" Spacing="12">
                <TextBlock Text="狀態" FontWeight="Medium" />
                <arc:Button Content="套用篩選" Command="{Binding ApplyFiltersCommand}" />
            </StackPanel>
        </arc:Popover>
    </arc:Popover.Attach>
</arc:Button>
```

附加的彈出框透過點選切換，因此閱讀者用按鈕即可開啟和關閉面板。讓 `IsOpen` 作為狀態的唯一來源，避免同時從繫結和程式碼中設定它。

### 焦點與關閉

面板開啟時焦點會移入其中：移到其中第一個可取得焦點的控制項，若沒有則移到面板本身。彈出框關閉時，焦點在以下情況下會返回觸發器：

- **Escape** 關閉彈出框，並一律將焦點返回觸發器。
- **面板外按壓**僅當焦點原本位於面板內時才將焦點返回觸發器。落在其他控制項上的按壓會讓焦點留在閱讀者所點選的地方。
- **從程式碼關閉**時，如果焦點原本位於面板內，則將焦點返回觸發器。

面板會在自身的控制項之間循環 `Tab`，因此閱讀者不會在未先關閉面板的情況下，透過 Tab 跳出面板進入下面的頁面。巢狀於 `Dialog` 中的彈出框同樣可以運作：焦點可以進入彈出框，面板外的按壓只關閉彈出框，對話方塊保持開啟。

### 範例：訂單清單上的篩選面板

後台主控台中位於訂單表格上方的工具列。「篩選」按鈕開啟一個小表單，包含狀態選擇和兩個日期欄位，閱讀者可以縮小清單，並在面板背後看到表格更新。面板有一個主要操作和一個文字操作，寬 240 px，因此閱讀者仍能看到背後的表格。主要按鈕的文字為「套用篩選」，它是一個帶受詞的動詞片語，因此閱讀者點選前就清楚操作的含義。

```xml
<StackPanel Width="560" Spacing="12">
    <arc:Popover Placement="Bottom" Alignment="Start">
        <arc:Popover.Trigger>
            <arc:Button Variant="Secondary" Content="篩選訂單" />
        </arc:Popover.Trigger>
        <StackPanel Width="240" Spacing="12"
                    AutomationProperties.Name="篩選訂單">
            <TextBlock Text="狀態"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Select Placeholder="任意狀態" />
            <TextBlock Text="建立日期"
                       FontWeight="Medium"
                       Foreground="{arc:ArcDesignTokenResource Foreground}" />
            <arc:Input Placeholder="起始日期" />
            <arc:Input Placeholder="截止日期" />
            <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right">
                <arc:Button Content="重設" Variant="Ghost" />
                <arc:Button Content="套用篩選" />
            </StackPanel>
        </StackPanel>
    </arc:Popover>
</StackPanel>
```

### 文案規範

- **觸發器文字**：當面板會變更資料時使用動詞加受詞（「篩選訂單」「分享報告」）；當它只是開啟一個檢視時，使用設定項目的名稱（「欄位」）。單獨的「更多」會隱藏面板的作用。
- **面板標題**：句首大寫的簡短名詞片語，例如「分享連結」。當內容沒有標題時，為彈出框設定 `AutomationProperties.Name`，這樣面板在焦點移入時會以名稱被播報。
- **面板中的按鈕文字**：主要操作使用動詞加受詞（「套用篩選」「複製連結」），關閉操作使用單純的動詞（「重設」「取消」）。
- **欄位標籤**：與任何表單一樣可見且簡短；彈出框不是只有預留位置文字欄位的地方。
- **輔助文字**：用一句話說明設定的效果，例如「任何擁有連結的人都可以檢視。」

| 不要寫 | 改為 |
|---|---|
| 更多 | 篩選訂單 |
| 點選此處分享此文件 | 分享連結 |
| 送出 | 套用篩選 |
| 欄位顯示選項 | 欄位 |
| 請從下方清單中選擇一個狀態。 | 狀態 |

### 最佳做法

**建議**

- 面板只包含閱讀者一次即可操作的少量控制項。兩個欄位加一個主要按鈕很合適；更多內容應放在 `Dialog` 或頁面中。
- 為每個彈出框提供名稱：在面板內放一個標題，或在其上設定 `AutomationProperties.Name`，這樣焦點移入面板時它會被播報。
- 把最重要的操作放在面板的第一位，並把關閉操作放在它旁邊，靠右對齊。
- 對於位於頁面右邊緣的觸發器使用 `Alignment="End"`，讓面板留在視窗之內。
- 在面板中完成一次操作後關閉彈出框，讓閱讀者在頁面上看到結果。

**避免**

- 在彈出框中放置確認或破壞性操作。點選外部會在沒有選擇的情況下關閉它，閱讀者可能失去輸入；任何需要確認的內容請使用 `Dialog`。
- 長表單或可捲動的內容。面板的尺寸適合少量內容，過高的面板會遮住開啟它的表格或頁面。
- 從 `HoverCard` 或 `Tooltip` 觸發器開啟彈出框。懸停只顯示預覽；點選應開啟一個承載控制項的面板。
- 從兩個地方同時設定 `IsOpen`，例如繫結與爭搶同一狀態的 `ShowAt` 呼叫。
- 把彈出框當作導覽選單。指令清單應使用 `DropdownMenu`，它提供方向鍵和選單語意。

### 常見問題

**在彈出框還未進入視窗之前設定 `IsOpen` 為 true 會怎樣？** 彈出框會記錄這個要求，並在附加到視覺樹後開啟，因此檢視模型可以在檢視顯示之前設定狀態。

**為什麼按下觸發器時它不縮放？** 面板是依據觸發器測量的，因此按下時縮放會在面板開啟期間移動錨點。用作彈出框觸發器的 Arc 按鈕只以顏色變化表示按下。

**如何從其中的按鈕關閉彈出框？** 在程式碼中呼叫 `Close()`，或將按鈕繫結到檢視模型指令，由它把 `IsOpen` 設為 false。無論哪種方式，焦點原本位於面板內時都會返回觸發器。

**在面板內按下會關閉它嗎？** 不會。只有 Escape、面板外按壓、觸發器切換、`Close` 以及把 `IsOpen` 設為 false 才會關閉它。面板內的按壓會像平常一樣到達其控制項。

**能把彈出框附加到非按鈕的元素上嗎？** 可以。任何控制項都可以使用 `arc:Popover.Attach`。點選會切換面板；對於非按鈕控制項，`Enter` 與 `Space` 可以從鍵盤開啟它。

## API 參考

<!-- api: Popover -->

## 無障礙

- 在觸發器上按 Enter 或空白鍵會開啟彈出框。按 Escape 關閉它，並將焦點返回觸發器。
- 面板開啟時焦點移入其中。面板外按壓會關閉彈出框，並在焦點原本位於其中時將焦點返回觸發器。
- 當內容沒有標題時，為彈出框設定 `AutomationProperties.Name`。該名稱為面板提供標籤。
- 非模態彈出框不會限制焦點。面板在自身的控制項之間循環 `Tab`，使閱讀者在關閉前一直停留在面板內。
- 觸發器會公開彈出框是否展開：開啟時其項目狀態為 `Expanded`，關閉時為 `Collapsed`。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| 非按鈕觸發器上的 `Enter` 或 `Space` | 開啟彈出框並將焦點移入面板 |
| 面板內的 `Tab` / `Shift+Tab` | 在面板的控制項之間移動；焦點在面板內循環 |
| `Escape` | 關閉彈出框，並將焦點返回觸發器 |
| 面板外按壓 | 關閉彈出框；焦點原本位於面板內時返回觸發器 |

## 動態效果

- 面板以 0.97 的縮放、從朝向觸發器 5px 的位置開始，快速淡入，並以彈簧從落定的一側就位。
- 關閉約需 160ms，移動距離為一半。開啟與關閉在動畫中途可以相互逆轉。
- 觸發器按下時改變顏色，但不縮放，因為面板是依據它測量的。
- 減少動態效果時移除移動與縮放，僅保留短暫淡入。關閉動態效果時面板立即顯示或移除。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 面板開啟 | 從朝向觸發器 5px、縮放 0.97 的位置開始，快速淡入，並以彈簧就位 | 僅短暫淡入 | 立即顯示 |
| 面板關閉 | 約 160ms 內以一半的移動距離淡出 | 僅短暫淡入 | 立即移除 |
| 開啟過程中被關閉 | 從目前位置反向運動 | 短暫淡入 | 立即完成 |
| 觸發器按下 | 僅顏色變化，不縮放 | 僅顏色變化 | 僅顏色變化 |

## 相關元件

- `Tooltip`：懸停或聚焦時的單行標籤。
- `HoverCard`：懸停時開啟的唯讀預覽。
- `Dialog`：必須先處理才能繼續使用頁面的內容。
- `DropdownMenu`：指令清單。

| 元件 | 適用情境 |
|---|---|
| `Tooltip` | 懸停或聚焦時顯示一行標籤即可，且其中沒有可點選的內容 |
| `HoverCard` | 應在懸停時開啟人員或連結的唯讀預覽，且不取得焦點 |
| `Dialog` | 閱讀者在繼續之前必須確認或完成某事 |
| `DropdownMenu` | 內容是帶有方向鍵導覽的指令清單 |
| `Drawer` | 內容是從視窗邊緣滑入的較長面板 |
