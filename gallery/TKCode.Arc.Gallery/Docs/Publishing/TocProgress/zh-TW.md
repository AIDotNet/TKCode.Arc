---
title: Table of contents progress
summary: 長文章的目錄：醒目標示正在閱讀的章節，捲動時沿清單向下填滿一條軌道，並可跳到任意章節；在窄螢幕上它會收成一個進度按鈕。
source-hash: 78df3d5880e1
---

<Demo name="Preview" />

## 何時使用

`TocProgress` 放在長文章、指南或條款頁面旁邊，跟隨讀者的位置。正在閱讀的章節使用完整的文字顏色，上面的章節顯示為已讀，下面的章節保持柔和。清單左側有一條細軌道向下填充：它先到達目前章節的那一行，然後隨著讀者讀完這一節在這一行裡繼續變長，所以清單同時也是一條進度條。標題旁邊顯示文章已讀的百分比。選擇一個章節，文章就會捲動到那裡。

- **文件和指南**：人們在章節之間跳轉，並想知道自己在哪裡。
- **長篇閱讀**：文章、報告和更新紀錄，剩下多少內容很重要。
- **政策和條款**：讀者在找某一條，需要看到整體結構。

## 何時不用

- **只有一兩個畫面的短頁面**：清單沒有幫助。
- **讀者必須按順序完成的步驟**：使用 `Stepper`，它顯示完成情況，而不是位置。
- **頁面之間的導覽**：使用 `Tabs` 或側邊導覽；這個清單只在一個頁面內移動。
- **沒有章節的資訊流**：顯示一般的 `Progress` 進度條，或者什麼都不顯示。

## 用法

`TocProgress` 位於 Special 套件中：在 `UseArc()` 旁邊用 `UseArcSpecial()` 註冊它。把 `Target` 指向文章的 `ScrollViewer`，並用 `TocProgress.Heading` 標記其中的標題（子章節再加上 `TocProgress.Level` 2 或 3）。清單按文件順序收集這些標記過的元素。

```xml
<Grid ColumnDefinitions="*,Auto" ColumnSpacing="32" Height="400">
    <ScrollViewer x:Name="Article">
        <StackPanel Spacing="12">
            <TextBlock Text="Install" arc:TocProgress.Heading="Install" FontSize="22" />
            <TextBlock Text="Add the package to your project." TextWrapping="Wrap" />
            <TextBlock Text="Options" arc:TocProgress.Heading="Options" arc:TocProgress.Level="2" FontSize="18" />
            <TextBlock Text="Every option has a sensible default." TextWrapping="Wrap" />
        </StackPanel>
    </ScrollViewer>
    <arc:TocProgress Grid.Column="1" Width="220" Target="{Binding #Article}" />
</Grid>
```

標題越過閱讀線後就算到達了這一節，閱讀線位於視口頂部往下四分之一處（`ActivationRatio`）。當文章無法再往下捲動時，即使最後一節的標題從沒到達閱讀線，最後一節也會成為目前章節。`ActiveIndex`、`Progress` 和 `ActiveChanged` 事件回報讀者的位置，例如可以用來更新網址列。

### 手動列出章節

當清單裡的文字應當和標題不同，或者標題是在程式碼中產生的，可以加入 `TocProgressEntry` 子元素。每個項目指定自己的 `Title`、`Level` 和要捲動到的 `Target` 元素。

<Demo name="Entries" />

### 精簡

`DisplayMode` 為 Compact 時只顯示一個膠囊按鈕，裡面有一個進度環和目前章節的標題；點擊後在彈出層中開啟清單。預設的 Auto 會在視口比 `ArcViewport.CompactWidth`（640px）窄時切換到它。

<Demo name="Compact" />

### 減少動態效果

在減少動態效果下，軌道和文章直接跳到新位置；醒目標示、百分比和精簡標題立即改變。

<Demo name="ReducedMotion" />

### 結構

1. **標題列**：小號中等字重的「On this page」，以及用等寬字型顯示的已讀百分比。
2. **軌道**：20px 槽位中一條 2px、使用邊框顏色的軌道，強調色的填充，目前行旁邊更亮的一段，以及填充末端一個 8px、帶表面顏色外圈的圓點。
3. **行**：章節標題，最多兩行，每深一級向內縮排 14px：未讀時柔和，已讀時為次要顏色，目前時為完整顏色和中等字重。
4. **精簡按鈕**：40px 的膠囊，帶 20px 的進度環、目前標題和一個箭頭。

### 內容建議

- 標題保持簡短；清單在兩行處換行，之後以省略號結束。
- 即使文章層級更多，清單中也最多使用兩級；更深的層級會讓軌道顯得擁擠。
- 在最後一節下面留出足夠的空間（文章末尾的內距），讓它的標題能到達閱讀線。

## API 參考

<!-- api: TocProgress -->

<!-- api: TocProgressEntry -->

<!-- api: TocProgressLabels -->

<!-- api: TocProgressPosition -->

<!-- api: TocProgressActiveChangedEventArgs -->

## 無障礙

- 控制項是一個名為「On this page」（`Labels.Title`）的導覽區域。清單是一組連結：每一行以標題命名；目前行的說明文字是「current section」，已讀的行是「read」。進度透過軌道、文字顏色和百分比表達，從不只靠顏色。
- 只有一行在 Tab 順序中（目前行，或最後取得焦點的行）。
- 精簡按鈕的名稱包含目前章節和百分比，例如「Contents, Feeding every day, 42% read」。
- 鍵盤焦點顯示 2px 的焦點環；指標焦點不顯示。

### 鍵盤

| 按鍵 | 作用 |
|---|---|
| Tab | 進入清單（到目前行）或離開清單 |
| Up / Down | 移到上一行 / 下一行 |
| Home / End | 移到第一行 / 最後一行 |
| Enter / Space | 把文章捲動到該章節 |
| Escape | 關閉精簡彈出層並回到它的按鈕 |

## 動效

- 填充的末端以一個沒有回彈、大約 0.3 秒停穩的彈簧跟隨閱讀位置；讀者改變方向時它保留速度，所以快速捲動也不會卡頓。目前段以 snappy 彈簧移到新的一行。
- 選擇章節時文章以 smooth 彈簧捲動；整個過程中被選取的行一直醒目，任何滾輪或捲軸操作都會立即接手。
- 精簡標題用 Arc 的標籤切換（舊的 100 毫秒淡出，新的 160 毫秒淡入並上升 2px）。
- 減少動態效果和關閉動效時直接跳到最終狀態。

## 響應式

- 清單使用給它的寬度；標題換到兩行後以省略號結束。
- Auto 模式下，視口窄於 `ArcViewport.CompactWidth`（640px）時清單變成精簡按鈕，空間足夠時再變回來；彈出層寬 300px，並保持在視口內。
- 每行至少 30px 高，精簡按鈕 40px，都適合觸控操作。

## 相關元件

- `Progress`：一般的進度條。
- `Stepper`：顯示使用者按順序完成的步驟進度。
- `ScrollArea`：帶 Arc 捲軸的捲動容器，可以放文章。
- `Tabs`：在檢視之間切換，而不是在一個檢視內移動。
