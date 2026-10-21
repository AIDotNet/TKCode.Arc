---
title: 多行文字框
summary: 用於備註、描述和較長文字的多行輸入欄位。
source-hash: dde42eb11370
---

<Demo name="LiveCount" />

## 何時使用

- 跨越多行的自由文字，例如個人簡介、留言或回饋。
- 較長的內容，且即時字數統計有助於使用者維持在限制之內。
- 使用者可能需要整段回讀和編輯的文字。

## 何時不用

- 姓名、電子郵件地址等單行內容：用 `Input`。
- 機密值：用 `PasswordField`。
- 在頁面上原地編輯標題或描述：用 `InlineEdit`。
- 少量短值的清單：用 `TagInput`。

## 用法

按 Enter 會插入換行。欄位填滿所在欄，最小高度為 110 px。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Textarea Label="簡介"
                  Placeholder="用一兩句話介紹你自己"
                  Description="還可輸入 160 個字元"
                  MaxLength="160"
                  Width="420"
                  HorizontalAlignment="Left" />
</UserControl>
```

### 即時計數與驗證

文字變動時更新描述。只有變動的數字會滾動。設定 `ErrorMessage` 可將欄位標記為無效；值為空時清除錯誤。

<Demo name="Validation" />

```csharp
const int limit = 160;
bio.TextChanged += (_, _) =>
{
    var length = bio.Text?.Length ?? 0;
    bio.Description = $"還可輸入 {limit - length} 個字元";
    bio.ErrorMessage = length < 40 ? "請再多寫一些：至少 40 個字元。" : null;
};
```

### 繫結文字

`Text` 是雙向屬性，將它繫結到檢視模型的字串。

```xml
<arc:Textarea Label="回饋"
              Text="{Binding Feedback}"
              ErrorMessage="{Binding FeedbackError}" />
```

### 調整大小

拖曳右下角的把手可以加高欄位。高度從最小值開始，不隨內容變化；內容超出時欄位會捲動。使用 `IsResizable` 可關閉把手。

```xml
<arc:Textarea Label="備註"
              IsResizable="False"
              Text="固定高度。" />
```

使用者拖曳把手後，`FrameHeight` 會保存所選的高度（以像素為單位）。也可以在程式碼中設定它，讓欄位以指定高度開始。

```csharp
notes.FrameHeight = 200;
```

### 停用

停用的欄位透明度降至 0.6，且沒有把手。

<Demo name="Disabled" />

## API 參考

<!-- api: Textarea -->

## 無障礙

- `Label` 是欄位的名稱。`Description` 與 `ErrorMessage` 會合併為它的說明文字。
- 錯誤出現時會立即被強勢播報。帶動畫的文字對輔助技術隱藏，輔助技術讀取的是純文字版本。
- Enter 插入換行。游標、選取、輸入法、復原與剪貼簿快速鍵都是原生 `TextBox` 的行為。
- 取得焦點時，外框周圍會繪製 3 px 的焦點環。

## 動態效果

- 輔助文字列與錯誤列以平滑彈簧展開高度；變化的詞帶著柔和的模糊升起。
- 字數統計只滾動變動的數字。
- 減少動態效果時，彈簧、數字滾動與模糊都會移除，文案立即切換。關閉動態效果時同樣如此。

## 相關元件

- `Input`：單行文字。
- `InlineEdit`：不顯示外框、原地編輯值。
- `TagInput`：由短值組成的清單，而不是自由文字。
- `RichTextEditor`：需要標題或清單的文字。
</content>
