---
title: 原地編輯
summary: 原地重新命名：文字變為欄位，位置不移動。
source-hash: 645c78156d49
---

<Demo name="Rename" />

## 何時使用

- 經常閱讀、很少修改的名稱、標題或說明，例如專案名稱。
- 單獨儲存、不在表單中的單一值。
- 平時應像一般內容一樣顯示，只有使用者選擇編輯時才變為可編輯狀態的文字。

## 何時不用

- 需要一起儲存的多個欄位：使用由 `Input` 或 `Textarea` 組成的表單。
- 必須一律呈現為可編輯狀態的欄位：使用 `Input`。
- 使用者在表單中撰寫的多行內文：使用 `Textarea`。

## 用法

值以文字形式顯示。啟動後，它會變為相同尺寸的欄位。確認編輯時儲存該值。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:InlineEdit Label="專案名稱"
                    Value="Atlas重新設計"
                    HeadingLevel="2" />
</UserControl>
```

### 儲存與驗證

`SaveHandler` 負責持久化確認的值，並在欄位顯示儲存中狀態時執行。工作失敗時，文字會復原，並顯示重試連結。當草稿無法儲存時，`Validator` 會傳回一則訊息，該訊息顯示在欄位下方。

```csharp
title.Validator = value => value.Length == 0 ? "專案需要一個名稱。" : null;

title.SaveHandler = async next =>
{
    await projects.RenameAsync(projectId, next);
};
```

處理 `TextCommitted`、`Saved` 和 `SaveFailed`，即可對結果作出反應。

```csharp
title.TextCommitted += (_, e) => log.Text = $"已確認“{e.NewValue}”（原為“{e.OldValue}”）";
title.Saved += (_, e) => log.Text = $"已儲存“{e.NewValue}”";
title.SaveFailed += (_, e) => log.Text = $"儲存失敗：{e.Error.Message}";
```

### 變體與多行

`Variant` 設定視覺層級。`Title` 是名稱與標題的預設值，`Body` 適合說明文字。設定 `IsMultiline` 後，文字會自動換行，欄位高度隨之增加。Enter 仍然是儲存，Shift+Enter 則插入換行。

<Demo name="Multiline" />

```xml
<arc:InlineEdit Label="說明"
                Variant="Body"
                IsMultiline="True"
                PlaceholderText="新增說明"
                Value="{Binding Description}" />
```

### 繫結

`Value` 是雙向的。使用者未在編輯時，外部傳入的新值會取代目前的文字。

```xml
<arc:InlineEdit Label="專案名稱"
                Value="{Binding ProjectName}"
                HeadingLevel="2" />
```

### 空值

在有值之前，預留位置文字會保留這一行。

<Demo name="Empty" />

## API 參考

<!-- api: InlineEdit -->

## 無障礙

- 靜止狀態下的文字是一個按鈕，其名稱由標籤與值組成，例如「專案名稱：Atlas重新設計」。
- `HeadingLevel` 會將文字回報為標題。是否支援取決於平台。
- 按 Enter 或 Space 開始編輯。Enter 儲存，Escape 取消並還原已儲存的值，焦點離開欄位時 Tab 會儲存。
- 驗證訊息與儲存錯誤會以禮貌的方式通知，並以說明文字的形式與欄位關聯。儲存進度與結果會透過狀態區域通知。

## 動態效果

- 舊值捲出，新值帶著柔和的模糊升起。儲存失敗時，復原方向相反，並短暫為外框上色。
- 輸入時寬度跟隨草稿變化；多行時，外框高度平滑變化。
- 儲存時，小型載入指示器會旋轉。儲存完成後，會繪製一個勾號。
- 減少動態效果時，文字交叉淡化，勾號立即繪製，載入指示器改為靜態標記。關閉動態效果時效果相同，但不做淡化。

## 相關元件

- `Input`：一律可見、隨表單一起儲存的欄位。
- `Textarea`：表單中較長的內文。
- `ExpandingSearch`：可展開搜尋欄位與結果的控制項。
