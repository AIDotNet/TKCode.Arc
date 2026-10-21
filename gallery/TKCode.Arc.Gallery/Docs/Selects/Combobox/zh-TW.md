---
title: 組合方塊
summary: 無需離開欄位即可搜尋並從清單中選取。
source-hash: 7880a8c53e61
---

<Demo name="Searchable" />

## 何時使用

- 使用者會依名稱搜尋的長清單，例如時區或國家。
- 帶別名的選項。`Keywords` 可讓使用者以不在標籤中的詞找到項目。
- 單一值的選擇，且輸入答案比捲動尋找更快。

## 何時不用

- 無須輸入的簡短固定清單：用 `Select`。
- 需要選取多個值：用 `MultiSelect`。
- 搜尋的目的是瀏覽內容而非設定值：用 `ExpandingSearch`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Combobox Label="框架"
                  Width="280"
                  Description="輸入以搜尋。">
        <arc:SelectItem Content="Avalonia" Keywords="xaml dotnet" />
        <arc:SelectItem Content="AtomUI" Keywords="ant design" />
        <arc:SelectItem Content="MAUI" Keywords="xamarin" IsEnabled="False" />
        <arc:SelectItem Content="WinUI" />
    </arc:Combobox>
</UserControl>
```

### 繫結選項與值

將 `ItemsSource` 設為 `SelectOption` 記錄。`Keywords` 會參與搜尋比對，但不會顯示。`SelectedValue` 為雙向繫結，尚未選取任何項目時為 `null`。

```xml
<arc:Combobox Label="時區"
              Width="320"
              ItemsSource="{Binding Zones}"
              SelectedValue="{Binding ZoneId, Mode=TwoWay}" />
```

```csharp
var zone = new Combobox
{
    Label = "時區",
    Width = 320,
    ItemsSource = cities.Select(c => new SelectOption(c.City, c.City)
    {
        Keywords = $"{c.Zone} {c.Offset}"
    }).ToArray()
};
zone.SelectionChanged += (_, _) => Console.WriteLine(zone.SelectedValue);
```

### 自訂篩選

預設情況下，標籤、中繼資訊與關鍵字會以不區分大小寫的子字串進行比對。設定 `FilterPredicate` 可以變更比對方式。它會接收項目與去除前後空白的查詢字詞。

<Demo name="CustomFilter" />

### 預留位置文字與空白文字

欄位為空時會顯示 `Placeholder`。使用者搜尋時，所選的標籤會讓出位置。`EmptyText` 是沒有任何符合項目時顯示的狀態列。

```xml
<arc:Combobox Label="國家"
              Placeholder="搜尋國家"
              EmptyText="沒有符合的國家。" />
```

### 清除與焦點

`Clear()` 會清除選取內容與查詢字詞。`FocusInput()` 會將焦點移到欄位，並開啟清單。

```csharp
zone.Clear();
zone.FocusInput();
```

### 內嵌選項

停用的選項會顯示，但無法選取。選取某項後，清除按鈕會彈出。

<Demo name="InlineOptions" />

### 停用

停用的 `Combobox` 會保留其值，並忽略輸入。

<Demo name="Disabled" />

## API 參考

<!-- api: Combobox -->

<!-- api: SelectOption -->

## 無障礙

- `Label` 是欄位及其清單的名稱，`Description` 是說明文字。
- 此欄位為組合方塊。使用者以方向鍵移動反白時，焦點會保留在文字輸入框中。
- Enter 選取反白的選項。Escape 關閉清單並捨棄查詢字詞，因此先前的值保持不變。
- 空白狀態會以禮貌的方式通知。清除按鈕的名稱為「清除選取」。
- 焦點框繪製在欄位上。游標、選取、輸入法與剪貼簿都是原生 `TextBox` 的行為。

## 動態效果

- 清單以彈簧效果從上方展開，高度隨符合的結果數量變化。
- 所選的標籤從下方升入欄位，並帶著柔和的模糊逐漸清晰。
- 有選取項目時，清除按鈕會彈出顯示；按下時縮小。
- 減少動態效果時只保留淡入淡出，清單高度立即變化。關閉動態效果時，所有變化直接完成。

## 相關元件

- 簡短固定且無須輸入的清單用 `Select`。
- 需要選取多個值時用 `MultiSelect`。
- 搜尋的目的是瀏覽內容而非設定值時用 `ExpandingSearch`。
- 不從清單中選取的自由文字用 `Input`。
