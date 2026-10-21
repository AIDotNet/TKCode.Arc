---
title: 按鈕
summary: 清楚、反應迅速的操作按鈕，次要狀態保持低調。
source-hash: e419533863ca
---

<Demo name="Variants" />

## 何時使用

- 頁面、表單或對話方塊中的任何單一操作，例如儲存、繼續或取消。
- 文字會原地變化的操作，例如從「儲存」變為「已儲存」，寬度同時以彈簧動態適應。
- 需要載入指示的短時間非同步操作，按鈕在此期間保持鍵盤焦點。

## 何時不用

- 需要自行呈現等待與成功狀態的按鈕：用 `ActionButton`。
- 同一操作的兩到五個相近選項：用 `SplitButton`。
- 需要使用者刻意按住才能執行的破壞性操作：用 Gestures 中的 `HoldToConfirm`。
- 需要保持選取狀態的值：用 Toggles 中的 `SegmentedControl`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Button Content="儲存變更" Variant="Primary" />
</UserControl>
```

### 變體

`Variant` 決定視覺權重。每個畫面只使用一個 `Primary` 操作。`Danger` 以危險色外框標示破壞性操作。

```xml
<arc:Button Content="儲存變更" Variant="Primary" />
<arc:Button Content="取消" Variant="Secondary" />
<arc:Button Content="瞭解更多" Variant="Ghost" />
<arc:Button Content="刪除專案" Variant="Danger" />
```

### 尺寸

`Size` 決定最小高度：`Small` 為 36，`Medium` 為 44（預設值），`Large` 為 50。尺寸不會隨視窗寬度改變。

<Demo name="Sizes" />

### 載入與文字變形

設定 `IsLoading` 即可顯示載入指示。按鈕保持鍵盤焦點，但在工作結束前會忽略點按與按鍵。修改 `Content` 即可切換文字：新文字向上浮入，寬度以彈簧動態適應。

<Demo name="LoadingMorph" />

```xml
<arc:Button Content="{Binding SaveLabel}"
            IsLoading="{Binding IsSaving}"
            Variant="Primary" />
```

### 僅圖示按鈕

僅圖示按鈕沒有可見文字，因此需要提供無障礙名稱。

```xml
<arc:Button Size="Small" Variant="Ghost"
            AutomationProperties.Name="關閉">
    <TextBlock Text="×" />
</arc:Button>
```

### 停用

將 `IsEnabled` 設為 `False`。外框變體保持其形狀，因此不可用的操作仍然看起來像按鈕。

<Demo name="Disabled" />

## API 參考

<!-- api: Button -->

## 無障礙

- 按鈕具有 Button 角色。僅圖示按鈕需要 `AutomationProperties.Name`。
- 設定 `IsLoading` 後，按鈕仍可取得焦點，並向輔助技術回報自身忙碌且無法使用的狀態。
- Enter 與 Space 皆可啟動按鈕。載入期間，點按與按鍵會被忽略。
- 鍵盤焦點會顯示 2px 的焦點環，與按鈕相距 2px。指標聚焦時不繪製焦點環。

## 動態效果

- 按下時內容輕微縮小，放開後以彈簧動態回彈。窄的圖示按鈕縮放幅度稍大。
- 文字變化時從柔和的模糊中升起，寬度以彈簧動態過渡到新文字的寬度。載入期間，載入指示持續旋轉。
- 減少動態效果時，移除按下縮放與寬度彈簧。文字與載入指示以短暫的交叉淡化切換，載入指示停止旋轉。關閉動態效果時，文字與狀態立即替換。

## 相關元件

- `ActionButton`：用於非同步送出，並在按鈕上顯示等待與成功狀態。
- `SplitButton`：用於帶有相近備選操作選單的主要操作。
- `ButtonGroup`：用於將多個相關操作組合在同一個容器中。
- `CopyButton`：用於複製一個值並顯示確認狀態。
