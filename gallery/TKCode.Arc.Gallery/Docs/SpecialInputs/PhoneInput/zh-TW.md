---
title: 電話輸入框
summary: 帶國家或地區選擇器、輸入時即時格式化並輸出 E.164 格式的電話欄位。
source-hash: c869790b76d3
---

<Demo name="Default" />

## 何時使用

- 註冊、結帳與聯絡表單中需要收集電話號碼的場景。
- 需要接收簡訊或來電、且國家或地區很重要的號碼。
- 需要以 E.164 格式（`+14155550132`）儲存號碼的表單。

## 何時不用

- 不會被撥打的自由文字：用 `Input`。
- 驗證碼：請在你自己的介面中使用一次性驗證碼輸入。它不屬於本函式庫。
- 電信業者或號碼類型驗證：請使用完整的號碼中繼資料服務。`PhoneInput` 只檢查長度與格式。
- 只從很長的固定值清單中選取：用 `Select`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PhoneInput Label="電話號碼"
                    Country="US"
                    Description="我們會向此號碼傳送登入驗證碼簡訊。" />
</UserControl>
```

### 預設值

`Value` 接受 E.164 字串。國家或地區會根據號碼自動選定，欄位顯示格式化後的本地號碼。

<Demo name="PresetValue" />

### 限定國家或地區

`Countries` 限制選擇器中的選項。`PreferredCountries` 列出在完整清單上方「建議」分組中顯示的國家或地區。清單為空時，顯示依名稱排序的單一清單。

<Demo name="RestrictedCountries" />

<Demo name="NoPreferredCountries" />

### 驗證與錯誤

欄位失去焦點後執行驗證。`IsValidationEnabled="False"` 可將其關閉。`ErrorMessage` 會取代內建的提示訊息。

<Demo name="ValidationOff" />

<Demo name="DescriptionAndError" />

### 繫結與事件

`Value` 與 `Country` 為雙向繫結。`ValueChanged` 會在每次編輯時回報 E.164 值、格式化後的號碼、國家或地區以及狀態。

<Demo name="ValueChanged" />

```xml
<arc:PhoneInput Label="電話號碼"
                Value="{Binding Phone, Mode=TwoWay}"
                Country="{Binding Country, Mode=TwoWay}" />
```

```csharp
phone.ValueChanged += (_, e) =>
{
    Console.WriteLine($"{e.NewValue} ({e.Status}, valid: {e.IsValid})");
};

phone.OpenPicker();
```

### 停用與隱藏標籤

`IsEnabled="False"` 會讓欄位變淡。`IsLabelVisible="False"` 會隱藏標籤，但標籤仍為輔助技術提供號碼的名稱。

<Demo name="States" />

### 鍵盤

在國家或地區按鈕上，ArrowDown 與 ArrowUp 會開啟選擇器，輸入字母則開啟搜尋。在分隔符號上按 Backspace 會刪除其旁邊的數字。

<Demo name="Keyboard" />

## API 參考

<!-- api: PhoneInput -->

## 無障礙

- `Label` 為欄位命名。`IsLabelVisible="False"` 時標籤隱藏，但仍作為名稱。
- 國家或地區按鈕命名為「國家或地區，<name> +<code>」。搜尋方塊是組合方塊，清單包含「建議」與「所有國家或地區」兩個分組。
- ArrowDown 與 ArrowUp 在國家或地區按鈕上開啟選擇器。在搜尋方塊內，方向鍵、PageUp 與 PageDown 用於移動，Home 與 End 跳到第一項與最後一項，Enter 選定，Escape 將焦點返回按鈕。
- 透過貼上或選擇器更改國家或地區時，會以禮貌方式播報。
- 號碼錯誤會在失去焦點後以輔助文字顯示。有效號碼會播報為「有效的 <country> 號碼」。

## 動態效果

- 欄位的表面從國家或地區按鈕展開為國家或地區清單，其圓角與寬度同步變化。
- 旗幟與撥號代碼滾動切換到新的國家或地區。反白在各列之間滑動。
- 有效標記以輕微的彈出效果出現。
- 減少動態效果時，表面直接跳到目標尺寸，內容以短暫淡入切換。關閉動態效果時立即切換。

## 相關元件

- `Input`：非電話號碼的文字。
- `Select`：固定值清單；`Field` 或 `FieldFrame`：用於建立類似的帶標籤欄位。
- `MoneyInput` 與 `NumberField`：具有各自格式規則的數值。
