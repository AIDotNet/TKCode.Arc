---
title: OTP input
summary: 六位數驗證流程，支援貼上與鍵盤導覽。
source-hash: f4f04a455025
---

<Demo name="Preview" />

## 何時使用

在寄出一次性驗證碼之後的步驟使用 `OtpInput`：一排單字元的格子，每個字元一格，上方有可見的標籤，下方有說明文字。輸入會填入一格並移到下一格，貼上的驗證碼會分散到各格中，方向鍵與 Backspace 則以人們預期的方式在這一排中移動。請在你的伺服器上檢查驗證碼；此控制項只負責收集。

- **驗證碼**，透過電子郵件或簡訊寄出。
- **雙重驗證**的驗證碼，來自驗證器應用程式。
- **短 PIN 與復原碼**，設定另一個 `Length` 或 `InputMode="Text"`。

## 何時不用

- **需要憑記憶輸入的機密資料**（密碼、通行詞組）：使用 `PasswordField`。
- **其他任何文字**：使用 `Input`。

## 用法

```xml
<arc:OtpInput x:Name="Code"
              Description="We sent a 6-digit code to your email."
              AutoFocus="True" />
```

```csharp
Code.Completed += async (_, e) =>
{
    Code.IsReadOnly = true;                       // keep focus, ignore typing while the code is checked
    var ok = await CheckCodeAsync(e.Code);       // your server call
    Code.IsReadOnly = false;
    if (!ok)
    {
        Code.Value = "";
        Code.ErrorMessage = "That code didn't match. Check the latest email and try again.";
        Code.FocusSlot(0);
    }
};

static Task<bool> CheckCodeAsync(string code) => Task.FromResult(code == "123456");
```

`Value` 儲存驗證碼，並為雙向繫結。`Completed` 會在每次編輯後、所有格子都已填滿時引發（輸入最後一個字元，或貼上時），事件參數 `OtpInputCompletedEventArgs.Code` 帶有驗證碼；`CompletedCommand` 會以相同的驗證碼執行。從程式碼設定 `Value` 永遠不會引發 `Completed`，因此重設格子不會再啟動一次檢查。`ValueChanged` 會透過 `ChangeSource` 回報每一次變更：使用者的編輯為 `Typing`、`Paste` 與 `Deletion`，你的程式碼所做的變更為 `Programmatic`。

沒有任何設定時，欄位會有六個數字格子，標籤為 "Verification code"。`Label` 同時也是螢幕閱讀器看到的格子群組名稱，因此即使外圍頁面已說明驗證碼的用途，仍請設定它。

### 驗證碼檢查

<Demo name="Verify" />

示範會在最後一格填滿時檢查驗證碼（700 ms，模擬）。錯誤的驗證碼會清空格子、顯示錯誤，並讓這一排晃動；重新輸入會清除錯誤，做法與 Arc 登入流程中的驗證碼步驟相同：

```csharp
Code.ValueChanged += (_, e) =>
{
    if (e.IsUserEdit && e.NewValue.Length > 0)
    {
        Code.ErrorMessage = null;
    }
};
```

### 錯誤

<Demo name="Error" />

非空的 `ErrorMessage` 會顯示在說明文字下方，讓每一格與焦點環變成危險色，並使 `IsInvalid` 為 true。每個新錯誤都會讓這一排晃動一次，因此被拒絕的驗證碼會被讀成對這次嘗試的回應；清除錯誤後再設定相同的文字，也會再次晃動。若只要無效的外觀而不顯示訊息，請單獨設定 `IsInvalid`；或呼叫 `Shake()`，自行讓這一排晃動。

### 成功

<Demo name="Success" />

`IsSuccess` 會讓格子與焦點環呈現成功色。Arc 的欄位沒有成功的外觀（其流程會直接進入下一步）；當正確的驗證碼之後欄位仍留在畫面上時，再使用它。錯誤優先於成功。

### 停用

<Demo name="Disabled" />

`IsEnabled="False"` 會停用每一格：以 48% 的不透明度淡化在靜音表面上，`Tab` 會略過，且沒有焦點環。`IsReadOnly` 則是忙碌狀態：格子保留焦點與方向鍵，但輸入、貼上與刪除都會被忽略。

### 長度與字元

<Demo name="Length" />

```xml
<arc:OtpInput Label="PIN" Length="4" />
<arc:OtpInput Label="Recovery code" Length="8" InputMode="Text" />
```

`Length` 設定格子的數量（1 到 32）。`InputMode="Numeric"`（預設）會捨棄任何非數字的字元，無論來自輸入或貼上，並要求觸控鍵盤顯示數字鍵台。`InputMode="Text"` 則接受任何字元。

### 驗證碼如何填入格子

驗證碼是一個字串，與 Arc 相同：第 `i` 格顯示 `Value[i]`。清除中間的一格（Delete）時，其後的字元會向左移動一格；輸入到超過驗證碼末端的格子時，該字元會成為驗證碼的下一個字元。已填入的格子在其字元被選取（連按兩下，或以 `Tab` 到達）或被刪除之前，不接受更多輸入。貼上會從焦點所在的格子開始填入，並忽略放不下的部分；焦點會移到最後一個被貼上字元之後的格子。輸入法，或平台的簡訊自動填入一次插入多個字元時，會以同樣方式分散到各格中。

### 窄版容器

<Demo name="Narrow" />

欄位依其被給予的寬度調整，而非視窗的寬度。在 328 px 或以下（Arc 的 360 px 手機斷點減去兩側各 16 px 的邊距），格子之間的間距會變為 6 px，高度降為 44 px。若連這樣都放不下，每一格會以相同幅度縮小；間距維持不變。未拉伸的欄位（`HorizontalAlignment` 不是 `Stretch`）寬度與其最寬的部分相同，就像 Arc 置中的預覽一樣。

### 文字與在地化

`Label`、`Description` 與 `ErrorMessage` 都是純文字。每一格的無障礙名稱由 `SlotLabelFormat` 提供（預設為 `"{0}, digit {1} of {2}"`：標籤、格子的位置與格子的總數）。

## API 參考

<!-- api: OtpInput -->

<!-- api: OtpInputValueChangedEventArgs -->

<!-- api: OtpInputCompletedEventArgs -->

## 無障礙

- 格子組成一個群組，名稱由 `Label` 提供；每一格的名稱如 "Verification code, digit 1 of 6"。
- 說明文字與錯誤訊息是每一格的說明文字。錯誤出現時會以強烈模式朗讀。
- 焦點環是這一排共用的同一個滑動焦點環，無論使用鍵盤或指標皆然。懸停樣式只套用於滑鼠或觸控筆。
- 在 `InputMode="Numeric"` 下，觸控鍵盤會顯示數字鍵台。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `0`-`9` / 字元 | 填入格子並移到下一格。已填入的格子在其字元被選取之前，會忽略輸入。 |
| `ArrowLeft` / `ArrowRight` | 移到上一格或下一格。 |
| `Backspace` | 在空格上，清除前一格並往回移；在已填入的格子上，刪除游標前的字元。 |
| `Delete` | 清除目前的格子。 |
| `Ctrl+V` / `Cmd+V` | 從焦點所在的格子開始，將驗證碼貼上到各格中。 |
| `Tab` / `Shift+Tab` | 逐格移動；以 `Tab` 到達的格子會選取其字元。 |

## 動態效果

- 輸入的字元會從柔焦中上升進入格子；貼上則像一道由左至右的短波浪。
- 焦點留在這一排時，一個焦點環會在格子之間滑動；焦點從其他地方移入時，焦點環則淡入。
- 新的錯誤會讓這一排左右晃動一次。說明與錯誤列會展開高度，且只改寫有變化的文字。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 字元出現 | 上升 0.35 em 並去除模糊，240 ms；貼上時每格等待 35 ms | 淡入，120 ms | 立即 |
| 字元離開 | 淡出、縮小至 90% 並模糊，120 ms | 立即 | 立即 |
| 焦點環移動 | 彈簧（視覺時長 0.26 s，無彈跳） | 立即 | 立即 |
| 焦點環出現或離開 | 160 ms 內淡入淡出，並從 94% 縮放 | 立即 | 立即 |
| 格子邊框與填色 | 160 ms | 立即 | 立即 |
| 晃動 | 360 ms | 無 | 無 |

### 回應式行為

只有一個斷點，依欄位被給予的寬度判斷：328 px。格子寬 42 px，在這一排放不下時會縮小。

## 相關

- `Input`，用於附標籤與訊息的單行文字。
- `PasswordField`，用於需要憑記憶輸入的機密資料。
- 登入區塊，其電子郵件驗證碼步驟使用此欄位。
