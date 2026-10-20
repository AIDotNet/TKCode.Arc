---
title: 核取方塊
summary: 二元選擇，狀態精確且清楚易讀。
source-hash: a5763119512c
---

## 何時使用

- 需要透過提交按鈕確認的獨立選擇，例如同意條款。
- 父層列需要以部分選取狀態反映部分子項的情況。
- 使用者可以在一組選項中勾選多個的情境。

## 何時不用

- 立即生效的設定：使用 `Switch`。
- 從一組選項中只能選擇一個：使用 `RadioGroup`。
- 以標籤形式呈現、經常切換的篩選條件：使用 `ChipGroup`。

## 用法

標籤即控制項的內容。`Description` 會在標籤下方新增一行說明文字。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Checkbox Content="同意條款及條件"
                  Description="您同意我們的服務條款與隱私權政策。" />
</UserControl>
```

### 必填與無效

`IsRequired` 會在表單中將該欄位標示為必填。`IsInvalid` 會顯示錯誤狀態，在未勾選時，方框邊框呈危險色。

```xml
<arc:Checkbox Content="同意條款及條件"
              Description="勾選此項以繼續。"
              IsRequired="True"
              IsInvalid="True" />
```

### 部分選取的父項

`CheckedState` 是包含 `Unchecked`、`Checked` 與 `Indeterminate` 的列舉。`IsChecked` 是同一狀態的可為 null 布林值，其中 `null` 表示部分選取。當部分子項被勾選時，將父項設為部分選取。

```xml
<arc:Checkbox Content="全部管道" CheckedState="Indeterminate" />
```

```csharp
var all = new Checkbox { Content = "全部管道" };
var children = new[]
{
    new Checkbox { Content = "電子郵件", IsChecked = true },
    new Checkbox { Content = "推播通知" },
    new Checkbox { Content = "簡訊" }
};

void UpdateParent()
{
    var on = children.Count(c => c.IsChecked == true);
    all.CheckedState = on == 0 ? CheckboxState.Unchecked
        : on == children.Length ? CheckboxState.Checked
        : CheckboxState.Indeterminate;
}

foreach (var child in children)
{
    child.CheckedStateChanged += (_, _) => UpdateParent();
}
```

若要讓點選父項時同時設定所有子項，請在父項上處理 `CheckedStateChanged`，並將每個子項的 `IsChecked` 設為新狀態。在該處理常式中略過部分選取狀態。

### 三態循環

`IsThreeState="True"` 會讓點選在未勾選、勾選與部分選取之間循環，與 Avalonia 的 `CheckBox` 行為一致。

```xml
<arc:Checkbox Content="三態循環" IsThreeState="True" />
```

### 事件

`CheckedStateChanged` 會提供舊狀態與新狀態。每次 `IsChecked` 變動時都會引發 `IsCheckedChanged`。

```csharp
checkbox.CheckedStateChanged += (_, e) =>
{
    Console.WriteLine($"{e.OldState} -> {e.NewState}");
};
```

## API 參考

<!-- api: Checkbox -->

## 無障礙

- 標籤即核取方塊的名稱。沒有標籤時，請設定 `AutomationProperties.Name`；名稱會回退為「核取方塊」。
- `Description` 是說明文字。輔助技術會在名稱之後朗讀它。
- 空白鍵切換勾選狀態。與 WAI-ARIA 核取方塊模式一致，Enter 鍵不會切換。
- 部分選取狀態會回報為混合狀態。繪製的勾號與短橫線不會分別對外公開。
- 鍵盤焦點會繪製 2px 的外框，並與控制項保持 2px 的間距。指標焦點則不顯示。

## 動態效果

- 勾選時，填色從略小的尺寸放大出現；取消勾選時則快速淡出。
- 勾號會逐步繪製出來。切換為部分選取時，勾號會變形為短橫線，切回時則反向變形。
- 按下方框時會略微縮小，放開後彈回。
- 減少動態效果時，所有狀態變化立即套用，並取消按下縮放。關閉動態效果時效果相同。

## 相關元件

- `Switch`：立即生效的設定。
- `RadioGroup`：從一組選項中只選擇一個。
- `ChipGroup`：使用者經常切換的一組篩選條件。
