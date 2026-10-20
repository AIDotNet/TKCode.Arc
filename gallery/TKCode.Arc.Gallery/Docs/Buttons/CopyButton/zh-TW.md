---
title: 複製按鈕
summary: 複製一個值，並立即給出確認回饋。
source-hash: 2afcc3a81ba6
---

## 何時使用

- 使用者經常複製的值，例如 API 金鑰、安裝指令或連結。
- 程式碼片段與行內值，確認回饋應出現在原地。
- 空間緊湊、只需圖示即可完成複製的列。

## 何時不用

- 除複製值外還需要其他格式，例如複製連結或以 Markdown 複製：用 `SplitButton`。
- 完整的程式碼範例，且自帶複製控制項：用 `CodeBlock`，它已經包含一個複製控制項。

## 用法

把 `Value` 設為要寫入剪貼簿的文字。`Label` 是可見文字，同時也是無障礙名稱。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:CopyButton Value="npx arc add copy-button" />
</UserControl>
```

### 自訂標籤

`Label` 設定閒置狀態的文字。`CopiedLabel` 與 `FailedLabel` 設定複製後顯示的文字。標籤區域會預留三者中最寬的寬度，因此按鈕的寬度永遠不會改變。

```xml
<arc:CopyButton Value="sk_live_51H..."
                Label="複製金鑰"
                CopiedLabel="金鑰已複製"
                FailedLabel="無法複製" />
```

### 純圖示與簡約樣式

`IsIconOnly` 會隱藏標籤，但標籤仍作為無障礙名稱。`Variant="Plain"` 會移除外框，適合緊湊的列與程式碼區塊。

```xml
<arc:CopyButton Value="https://uiarc.dev" IsIconOnly="True" />
<arc:CopyButton Value="#7C5CFF" Label="複製 HEX 值" Variant="Plain" />
```

### 繫結值

把 `Value` 繫結到應被複製的文字。當值為 `null` 時複製會失敗，按鈕會顯示失敗狀態。

```xml
<arc:CopyButton Value="{Binding InstallCommand}" Label="複製指令" />
```

### 重設時間與事件

按鈕會在 `ResetAfter` 之後恢復閒置狀態，預設為 1.9 秒。複製成功後會引發 `Copied`。`State` 的值為 `Idle`、`Copied` 或 `Error`。

```csharp
copy.Copied += (_, _) => log.Text = "已複製到剪貼簿。";
copy.ResetAfter = TimeSpan.FromSeconds(3);
```

## API 參考

<!-- api: CopyButton -->

## 無障礙

- 無障礙名稱永遠是 `Label`，即使按鈕只顯示圖示。
- 一個隱藏的禮貌狀態區域會朗讀結果，例如「複製金鑰：已複製」或「複製金鑰：無法複製」。
- Enter 與 Space 執行複製。
- 標籤區域保留其最寬狀態的寬度，因此按鈕周圍的版面不會移動。
- 鍵盤焦點會顯示標準的焦點框；指標聚焦時不繪製焦點框。

## 動態效果

- 圖示以柔和的模糊切換，並以緩慢、接近臨界阻尼的彈簧縮放進入。勾號會自行繪製出來。
- 只有標籤中變化的字母會移動；按鈕恢復閒置時，這些動畫會反向播放。
- 按下時內容會輕微縮小，放開時以彈簧彈回。
- 減少動態效果時，圖示與文字使用瞬間交叉淡化，略過描邊繪製與按下縮放。關閉動態效果時立即替換圖示與標籤。

## 相關元件

- `SplitButton`：複製只是多個動作之一時使用，例如複製頁面，並提供複製連結與以 Markdown 複製。
- `ActionButton`：非同步提交，帶有等待與成功狀態。
- `Button`：沒有剪貼簿結果的一般動作。
- `CodeBlock`：已包含複製控制項的程式碼範例。
