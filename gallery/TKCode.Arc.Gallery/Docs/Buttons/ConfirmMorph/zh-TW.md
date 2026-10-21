---
title: 確認變形
summary: 一個破壞性按鈕，可變形為就地確認、載入指示與帶復原的結果。
source-hash: d51545d5f9eb
---

<Demo name="Press" />

## 何時使用

- 針對某一物件的破壞性操作，例如刪除所選檔案或移除成員。
- 後果可以由一個問題說清的操作，例如「刪除 3 個檔案？」。
- 執行後短時間內可以復原的操作，並在結果中提供復原。

## 何時不用

- 後果需要多行說明或輸入名稱才能確認：使用 `Dialog`。
- 沒有確認問題、安全的非同步提交：使用 `ActionButton`。
- 絕不能被誤觸發的操作：使用 Gestures 中的 `HoldToConfirm`。
- 沒有單一預設操作的命令清單：使用 Menus 中的 `DropdownMenu`。

## 用法

將 `Label` 設為靜止時顯示的文字。問題由 `Prompt` 提供；若未設定 `Prompt`，則使用加上問號的 `Label`。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ConfirmMorph Label="刪除專案" Prompt="刪除此專案？" />
</UserControl>
```

### 操作與復原

問題被回答時會執行 `ConfirmAction`。任務執行期間，膠囊形按鈕顯示等待狀態。若任務擲出例外，則顯示錯誤狀態並提供重試。設定 `UndoAction` 可在結果中提供復原。

<Demo name="ErrorAndRetry" />

```csharp
delete.ConfirmAction = () => documentService.DeleteAsync(selected);
delete.UndoAction = () => documentService.RestoreAsync(selected);
```

### 確認模式

`ConfirmMode` 決定如何回答問題。`Press` 按一次即執行。`Hold` 要求按住確認按鈕達到 `HoldDuration`，預設為一秒。`Countdown` 會啟動 `CountdownSeconds` 倒數，期間仍可透過取消或 Escape 停止。

<Demo name="Hold" />

<Demo name="Countdown" />

```xml
<arc:ConfirmMorph Label="移除成員"
                  ConfirmLabel="移除"
                  ConfirmMode="Hold"
                  HoldDuration="00:00:01" />

<arc:ConfirmMorph Label="放棄草稿"
                  ConfirmLabel="放棄"
                  ConfirmMode="Countdown"
                  CountdownSeconds="3" />
```

### 文字與色調

八個文字屬性（`ConfirmLabel`、`CancelLabel`、`PendingLabel`、`DoneLabel`、`ErrorLabel`、`RetryLabel`、`UndoLabel` 與 `UndoingLabel`）分別設定每種狀態的用字。`Tone="Neutral"` 會以強調色顯示確認回答，適用於可以復原的重要操作。預設的 `Danger` 適用於破壞性操作。

<Demo name="NeutralTone" />

### 受控狀態與逾時

`State` 為雙向繫結。從程式碼設定它會移動控制項，但不會執行操作。`StateChanged` 會在每次狀態變化時回報，問題被關閉時會引發 `Cancelled`。`ConfirmTimeout` 會將未回答的問題恢復為靜止狀態，`ResultTimeout` 會移除結果。將 `IsOutsidePressCancelEnabled` 設為 `False`，可在外部按下時保持問題開啟。

```csharp
confirm.StateChanged += (_, e) => log.Text = $"狀態：{e.NewState}";
confirm.Cancelled += (_, _) => log.Text = "已取消";
confirm.State = ConfirmMorphState.Idle;
```

### 停用

`IsEnabled` 為 `False` 時，靜止的膠囊不再顯示陰影。

<Demo name="Disabled" />

## API 參考

<!-- api: ConfirmMorph -->

## 無障礙

- 問題是一個以其提示命名的群組。靜止的按鈕保留其標籤的名稱。
- 焦點隨變形移動：問題開啟時在確認按鈕上，操作執行時在根元素上，結果出現時在復原或重試按鈕上，靜止時回到觸發按鈕。
- 問題開啟後的前 280 毫秒內的按下會被忽略，因此連按兩下或按住 Enter 不會誤將其確認。
- 在 `Hold` 模式下，說明文字為「按住」。提前放開會播報需要更長時間的按住。
- 提示、等待與結果的文字，以及「可以復原」，都會以禮貌方式播報。
- Enter 與 Space 會按下目前狀態下的按鈕。Escape 會取消問題或倒數。

## 動態效果

- 只有膠囊的寬度會變化。它以彈簧展開，收合則較為平順，文字不會縮放。
- 每個新狀態的各部分依閱讀順序逐步清除一層輕微模糊，舊狀態則原地淡出。
- 結果的勾號會自行繪製。倒數數字滾動，等待指示器旋轉。
- 在 `Hold` 模式下，按住確認時，危險色的填滿會從左側顯露；提前放開則以彈簧回彈。
- 減少動態效果時，取消寬度彈簧與模糊，狀態之間交叉淡化，並放慢指示器旋轉。按住填滿與倒數圓環仍會顯示進度。關閉動態效果時，狀態立即切換。

## 相關元件

- `Dialog`：後果需要更多說明或輸入名稱時使用。
- `ActionButton`：沒有確認問題的非同步提交。
- `HoldToConfirm`：絕不能在單擊時觸發的操作。
- `DropdownMenu`：沒有單一預設操作的命令清單。
</content>
