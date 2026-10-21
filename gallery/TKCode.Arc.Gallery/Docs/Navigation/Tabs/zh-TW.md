---
title: 標籤頁
summary: 在同一脈絡中切換相關內容。
source-hash: cb17c9687110
---

<Demo name="Default" />

## 何時使用

- 同一物件的多個檢視，例如總覽、活動與設定。
- 並列的檢視：使用者每次選擇其中一個，各自有獨立的面板。
- 標籤較多的清單，可橫向捲動並使用邊緣按鈕。

## 何時不用

- 不需要面板、只選一個精簡的值：用 `SegmentedControl`。
- 依序閱讀的堆疊內容區塊：用 `Accordion`。
- 在多頁結果之間翻頁：用 `Pagination`。

## 用法

項目是 `TabItem` 容器。標題是觸發器，內容是面板。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Tabs AutomationProperties.Name="工作區">
        <arc:TabItem Header="總覽">
            <TextBlock Text="本季營收成長 12%。" />
        </arc:TabItem>
        <arc:TabItem Header="活動">
            <TextBlock Text="Maya 在兩小時前合併了 Billing v2。" />
        </arc:TabItem>
        <arc:TabItem Header="設定">
            <CheckBox Content="電子郵件摘要" IsChecked="True" />
        </arc:TabItem>
        <arc:TabItem Header="已封存" IsEnabled="False" />
    </arc:Tabs>
</UserControl>
```

### 資料繫結標籤頁

設定 `ItemsSource`。`ItemTemplate` 負責呈現觸發器，`ContentTemplate` 負責呈現所選項目的面板。透過 `SelectedItem` 或 `SelectionChanged` 讀取選取狀態。

<Demo name="Overflow" />

```xml
<arc:Tabs ItemsSource="{Binding Folders}"
          SelectedItem="{Binding CurrentFolder}"
          MaxWidth="460"
          HorizontalAlignment="Left">
    <arc:Tabs.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}" />
        </DataTemplate>
    </arc:Tabs.ItemTemplate>
    <arc:Tabs.ContentTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Summary}" />
        </DataTemplate>
    </arc:Tabs.ContentTemplate>
</arc:Tabs>
```

### 啟用模式

`ActivationMode` 決定方向鍵的行為。`Automatic`（預設）會選取獲得焦點的標籤。`Manual` 只移動焦點；按 Enter 或空白鍵才會選取標籤。

<Demo name="ManualActivation" />

### 在程式碼中選取

```csharp
tabs.SelectTab(2);
tabs.SelectionChanged += (_, _) =>
{
    status.Text = $"已選取：{tabs.SelectedItem}";
};
```

`SelectTab` 在索引處的標籤存在且可用時將其選取，否則回傳 `false`。

## API 參考

<!-- api: Tabs -->

## 無障礙

- 清單具有 Tab 角色，每個 `TabItem` 都會公開其選取狀態。使用 `AutomationProperties.Name` 為清單命名。
- 只有選取的標籤在 Tab 鍵順序中。方向鍵在標籤之間移動，Home 與 End 分別跳到第一個與最後一個。
- Tab 鍵從清單移到所選標籤的面板。
- 溢出清單上的捲動按鈕由 `ScrollLeftLabel` 與 `ScrollRightLabel` 命名。
- 正在離開的標籤面板對輔助技術隱藏，並且在淡出期間無法取得焦點。

## 動態效果

- 選取指示條以 morph 彈簧在觸發器之間滑動。
- 新面板從移動方向的一側滑入 8px 並淡入。舊面板保持原位，向回移動 6px 並快速淡出。
- 面板區域從舊面板高度以彈簧過渡到新高度，因此下方內容隨之移動。
- 減少動態效果時，指示條立即切換，面板原位交叉淡化，不做高度動畫。關閉動態效果時，所有變化立即完成。

## 相關元件

- `SegmentedControl`：用於不帶面板的精簡值選擇。
- `Accordion`：用於依序閱讀的堆疊區塊。
- `Pagination`：用於在同一集合的多個頁面之間翻頁。
