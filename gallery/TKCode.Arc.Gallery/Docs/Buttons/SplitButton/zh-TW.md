---
title: 分割按鈕
summary: 一個主要動作，加上一組相近備選項的選單。
source-hash: 47c862821190
---

## 何時使用

- 帶有兩到五個相近備選項的預設動作，例如「壓縮並合併」與「變基並合併」。
- 匯出或分享動作：通常使用某一種格式，其餘格式放在選單中。
- 預設動作的文字會原地變化以確認結果，例如將「複製頁面」變為「已複製」。

## 何時不用

- 沒有預設動作、選項地位對等：使用 Menus 中的 `DropdownMenu`。
- 只有一個動作、沒有可選項：使用 `Button`。
- 針對列或卡片等內容物件的動作：使用 Menus 中的 `ContextMenu`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:SplitButton Label="合併拉取要求">
        <arc:MenuEntry Header="壓縮並合併" />
        <arc:MenuEntry Header="變基並合併" />
        <arc:MenuEntry Header="關閉拉取要求" IsDestructive="True" HasSeparatorBefore="True" />
    </arc:SplitButton>
</UserControl>
```

### 點擊與選單選取

主要區域觸發 `Click` 並執行 `Command`。箭頭區域開啟選單，選取某一列時觸發 `ItemClicked`，並傳入所選的列。

```xml
<arc:SplitButton Label="匯出為 CSV"
                 Variant="Secondary"
                 Command="{Binding ExportCommand}">
    <arc:MenuEntry Header="匯出為 JSON" />
    <arc:MenuEntry Header="匯出為 PDF" />
</arc:SplitButton>
```

```csharp
merge.Click += (_, _) => log.Text = "已透過合併認可完成合併。";
merge.ItemClicked += (_, e) => log.Text = $"選單：{e.Header}";
```

### 來自資料的列

對於來自檢視模型的列，使用 `ItemsSource`；使用 `ItemTemplate` 繪製它們。不是 `MenuEntry` 執行個體的項目會顯示為列的標題。

```xml
<arc:SplitButton Label="分享" ItemsSource="{Binding ShareTargets}" />
```

### 圖示與原地結果

`Icon` 接收 24 乘 24 的 Lucide 幾何圖形，在程式碼中設定。更改 `Label` 與 `Icon` 時，主要區域會變形為新的文字，因此可以直接在按鈕上顯示結果。

```csharp
copy.Click += async (_, _) =>
{
    copy.Label = "已複製";
    copy.Icon = ArcIcons.Check;
    await Task.Delay(1600);
    copy.Label = "複製頁面";
    copy.Icon = ArcIcons.Copy;
};
```

### 停用

將 `IsEnabled` 設為 `False` 可同時停用兩個區域。

```xml
<arc:SplitButton Label="部署" IsEnabled="False">
    <arc:MenuEntry Header="部署到預備環境" />
</arc:SplitButton>
```

## API 參考

<!-- api: SplitButton -->

<!-- api: MenuEntry -->

## 無障礙

- 主要區域與箭頭是兩個按鈕。箭頭的名稱為 `{Label} more actions`，除非 `MenuButtonLabel` 設定了其他名稱；它會回報選單是否展開。
- 選單使用 Menu 與 MenuItem 角色。方向鍵在各列之間移動，並在兩端循環。
- 在主要區域按 Enter 或 Space 執行主要動作；在箭頭上則開啟選單。
- 按 Escape 關閉選單，並將焦點返回到箭頭。
- `Label` 變更時會以禮貌方式通知螢幕閱讀器。

## 動態效果

- 文字逐字母變化：相同的字母平滑滑動，變化的字母從柔和的模糊中升起。圖示交叉淡化，主要區域以彈簧方式過渡到新的寬度。
- 主要區域會輕微下壓。箭頭從不縮放，選單開啟時它會轉動。
- 選單在箭頭下方 4px 處開啟，各列依序淡入。
- 減少動態效果時移除按下效果、寬度彈簧與選單變換，僅保留短暫的淡入。關閉動態效果時選單與文字變化立即呈現。

## 相關元件

- `DropdownMenu`：沒有預設動作、選項地位對等的選單。
- `Button`：沒有備選項的單一動作。
- `ButtonGroup`：並排顯示的多個相關動作。
- `MenuEntry`：分割按鈕與選單共用的列類型。
