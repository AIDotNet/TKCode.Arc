---
title: 密碼欄位
summary: 附有可見顯示切換控制項的敏感文字輸入。
source-hash: e883e07b8a43
---

## 何時使用

- 登入表單，以及使用者輸入既有密碼的其他地方。
- 需要與第一次輸入內容比對的確認欄位。
- 預設隱藏、但可隨需查看的值。

## 何時不用

- 使用者正在設定的新密碼：用 `PasswordStrength`，它會顯示密碼強度。
- 非機密文字：用 `Input`。
- 搜尋關鍵字：用 `SearchField`。

## 用法

欄位內的眼睛按鈕可以顯示或隱藏文字。它的名稱會被輔助技術讀取。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PasswordField Label="密碼"
                       Placeholder="請輸入密碼"
                       Description="點選顯示按鈕可核對你輸入的內容。"
                       Width="360"
                       HorizontalAlignment="Left" />
</UserControl>
```

### 繫結值

`Text` 是雙向屬性，可以繫結到檢視模型。使用 `IsRevealed` 讀取或設定可見狀態。

```xml
<arc:PasswordField Label="密碼"
                   Text="{Binding Password}"
                   ErrorMessage="{Binding PasswordError}" />
```

```csharp
signIn.PropertyChanged += (_, e) =>
{
    if (e.Property == PasswordField.IsRevealedProperty)
    {
        state.Text = signIn.IsRevealed ? "已顯示" : "已隱藏";
    }
};
```

### 確認

當兩個值不一致時，從第二個欄位設定 `ErrorMessage`。將其設為 `null` 即可清除。

```csharp
void Check() => confirm.ErrorMessage = !string.IsNullOrEmpty(confirm.Text) && confirm.Text != first.Text
    ? "兩次輸入的密碼還不一致。"
    : null;

first.TextChanged += (_, _) => Check();
confirm.TextChanged += (_, _) => Check();
```

### 切換按鈕名稱

切換按鈕的名稱來自 `ShowPasswordText` 與 `HidePasswordText`。設定它們即可翻譯文案。

```xml
<arc:PasswordField Label="Mot de passe"
                   ShowPasswordText="Afficher"
                   HidePasswordText="Masquer" />
```

### 尺寸與狀態

`Size` 決定外框高度：`Small` 為 36，`Medium`（預設值）為 44。

```xml
<arc:PasswordField Label="小" Size="Small" Text="secret" />
<arc:PasswordField Label="停用" Text="secret" IsEnabled="False" />
```

## API 參考

<!-- api: PasswordField -->

## 無障礙

- `Label` 是欄位的名稱。`Description` 與 `ErrorMessage` 會合併為它的說明文字。
- 顯示切換按鈕是帶按下狀態的切換按鈕，其名稱會在「顯示密碼」與「隱藏密碼」之間切換。
- 在切換按鈕上按 Enter 或空白鍵可顯示或隱藏密碼。Tab 鍵可將焦點移入與移出該按鈕。
- 密碼預設會被遮蔽。游標、選取、輸入法、復原與剪貼簿快速鍵都是原生 `TextBox` 的行為。

## 動態效果

- 狀態切換時，眼睛圖示上的斜線會沿圖示繪製出來。
- 首次切換之後，文字從輕微模糊逐漸變清晰。
- 按下切換按鈕時會輕微縮小。說明文字列與錯誤列的行為與 `Input` 相同。
- 減少動態效果時，斜線立即繪製完成，模糊與按下縮放都會移除。關閉動態效果時同樣如此。

## 相關元件

- `PasswordStrength`：用於設定新密碼並顯示其強度。
- `Input`：非機密文字。
- `SearchField`：附有清除控制項的搜尋。
</content>
