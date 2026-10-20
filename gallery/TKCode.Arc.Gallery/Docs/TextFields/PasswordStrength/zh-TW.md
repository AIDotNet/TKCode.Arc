---
title: 密碼強度
summary: 在輸入新密碼時顯示其強度。
source-hash: 9d357e09500b
---

## 何時使用

- 註冊表單，以及使用者選擇新密碼的任何畫面。
- 變更密碼表單，新密碼必須符合一組規則。
- 使用者需要看到哪些規則尚未符合的情境。

## 何時不用

- 登入既有帳號：用 `PasswordField`，它沒有強度計與規則清單。
- 不是密碼的測量量，例如上傳進度：用 `Progress`。
- 非機密文字：用 `Input`。

## 用法

強度計、強度文字與規則清單會在每次按鍵時更新。評分在本機進行，預設規則為：至少 12 個字元、包含大寫與小寫字母、數字和符號。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:PasswordStrength Label="新密碼"
                          Placeholder="請設定密碼"
                          Width="400"
                          HorizontalAlignment="Left" />
</UserControl>
```

### 繫結值

`Text` 是雙向屬性。繫結 `ErrorMessage` 可顯示伺服器端或表單層級的錯誤。

```xml
<arc:PasswordStrength Label="新密碼"
                      Text="{Binding NewPassword}"
                      ErrorMessage="{Binding NewPasswordError}" />
```

### 回應強度變化

每次評分後都會觸發 `StrengthChanged`。`Level` 的範圍從 0（空白）到 4（很強），`Score.MetRuleIds` 列出已符合的規則。

```csharp
create.StrengthChanged += (_, e) =>
{
    result.Text = e.Level == 0
        ? "強度：尚無輸入"
        : $"強度：{e.Label}（等級 {e.Level}，已符合 {e.Score.MetRuleIds.Count} 條規則）";
};
```

### 自訂規則

將規則清單傳給 `Rules`。可以用不同的最小長度建立預設規則，也可以撰寫自己的 `PasswordRule`，包含判定條件以及可選的剩餘數量。

```csharp
custom.Rules = PasswordRules.Create(minLength: 16);
custom.ShortLength = 10;
```

```csharp
var rules = new List<PasswordRule>
{
    new("length", "至少 10 個字元", p => p.Length >= 10),
    new("digit", "包含數字", p => p.Any(char.IsDigit))
};
```

少於 `ShortLength` 個字元時，強度計會停留在第一段，並顯示「太短」。

### 錯誤

設定 `ErrorMessage` 可將欄位標記為無效。每出現一個新錯誤，欄位會晃動一次。

```csharp
withError.ErrorMessage = withError.Level < 3 ? "請設定更強的密碼。" : null;
```

## API 參考

<!-- api: PasswordStrength -->

## 無障礙

- `Label` 是欄位的名稱。規則清單與錯誤是它說明文字的一部分。
- 強度計以 0 到 4 的數值回報，並以強度文字作為其文字。
- 使用者輸入時，強度與已符合規則的摘要會以禮貌方式播報。
- 顯示切換按鈕上按 Enter 或空白鍵可顯示或隱藏密碼。游標、選取、輸入法、復原與剪貼簿快速鍵都是原生 `TextBox` 的行為。

## 動態效果

- 四段強度計按波浪順序依次填滿並變換色調。強度文字隨等級上升或下降。
- 規則符合時會以小幅放大出現並在標記上劃線；剩餘規則的數量會滾動變化。
- 新錯誤出現時，欄位晃動一次。
- 減少動態效果時，晃動、波浪與數字滾動都會移除，數值立即變化。關閉動態效果時同樣如此。

## 相關元件

- `PasswordField`：用於登入，此時密碼是核對而非設定。
- `Input`：非機密文字。
- `Progress`：不是密碼的測量值。
</content>
