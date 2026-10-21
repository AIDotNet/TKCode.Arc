---
title: 開關
summary: 用於立即生效設定的觸感式開關。
source-hash: feb4fcc5bdcb
---

<Demo name="SettingsList" />

## 何時使用

- 一經變更即生效的設定，例如通知。
- 設定清單，每一列放置一個開關。
- 只有開或關兩種狀態、且不需要確認按鈕的設定。

## 何時不用

- 需要透過送出按鈕確認的選擇，例如同意條款：用 `Checkbox`。
- 在具名選項之間做選擇：用 `SegmentedControl` 或 `RadioGroup`。
- 使用者可以選取多個選項的情況：為每個選項使用一個 `Checkbox`。

## 用法

`Content` 是開關的標籤。整個標籤都屬於點選範圍。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Switch Content="電子郵件通知" IsChecked="True" />
</UserControl>
```

### 設定清單

將多個開關堆疊起來，即可形成一個設定清單。

```xml
<StackPanel Spacing="4">
    <arc:Switch Content="電子郵件通知" IsChecked="True" />
    <arc:Switch Content="每週摘要" />
    <arc:Switch Content="產品更新" />
</StackPanel>
```

### 繫結

將 `IsChecked` 繫結到檢視模型屬性。繫結 `IsEnabled` 可鎖定開關。

```xml
<arc:Switch Content="電子郵件通知"
            IsChecked="{Binding EmailEnabled}"
            IsEnabled="{Binding CanEditSettings}" />
```

### 停用

停用的開關會保留其狀態，且無法變更。

<Demo name="Disabled" />

### 變更

只要狀態改變，無論來自滑鼠、鍵盤還是程式碼，都會引發 `IsCheckedChanged`。請在此處套用設定，這樣每種變更方式的效果都相同。

```csharp
notifications.IsCheckedChanged += (_, _) =>
{
    settings.EmailEnabled = notifications.IsChecked == true;
};
```

## API 參考

<!-- api: Switch -->

## 無障礙

- `Content` 即開關的名稱。只包含圖示的開關需要設定 `AutomationProperties.Name`。
- 按下空白鍵和 Enter 鍵可切換開關。按住空白鍵時，滑塊保持拉伸的形狀。
- 輔助技術會將開關回報為處於選取或未選取狀態的切換按鈕。Avalonia 沒有獨立的開關控制項類型。
- 鍵盤焦點會繪製一個偏移 2px 的 2px 外框線。滑鼠焦點不會繪製。
- 滑塊對輔助技術隱藏。開和關的狀態同時透過軌道顏色和滑塊位置顯示。

## 動態效果

- 按下開關時，滑塊會向另一側拉寬，遠端邊緣保持不動。放開後，滑塊以無彈跳的彈簧滑過去。
- 並非由按下引起的變更（例如按鍵或程式碼修改），會播放一次短暫的滑塊拉伸。
- 軌道填色會淡入淡出，滑塊顏色隨之變化。
- 減少動態效果時，取消拉伸，滑塊立即移動，並關閉顏色過渡。關閉動態效果時效果相同。

## 相關元件

- `Checkbox`：需要透過送出按鈕確認的選擇。
- `SegmentedControl`：在幾個具名檢視之間切換。
- `RadioGroup`：從一組選項中恰好選取一項。
