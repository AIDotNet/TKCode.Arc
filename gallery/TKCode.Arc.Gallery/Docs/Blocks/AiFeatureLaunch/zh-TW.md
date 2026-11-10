---
title: AI feature launch
summary: 在產品內宣布一項新的 AI 功能：一段可以暫停的循環演示、功能說明、一個試用按鈕，以及會記住關閉原因的關閉操作。
source-hash: b1798cb94f14
---

<Demo name="Preview" />

## 何時使用

`AiFeatureLaunch` 在產品內告訴人們一項新的 AI 功能已經可用，並邀請他們試一試。頂部的一段短循環演示展示功能的效果，"New" 標簽和功能名稱引出正文，一兩句話說明它能做什麼，幾條要點說明可以期待什麼，試用按鈕讓人立即開始。還沒準備好的人可以點 "Maybe later" 或關閉按鈕；`Dismissed` 會報告是哪一種，方便應用決定是否再次顯示。

- **發布新功能。** 在功能所在的頁面顯示一次：討論串裡的總結按鈕、編輯器裡的寫作助手。
- **讓不起眼的功能被看見。** 功能已經存在但很少有人發現時，在它能幫上忙的地方顯示。
- **就地的更新說明。** 每個功能一張卡片，代替沒人讀的長列表。

## 何時不用

- **同時介紹多個功能。** 只展示最有用的一個；輪播式的發布很容易被忽略。
- **警告和故障。** 使用 `Alert` 或 `AnnouncementBar`；這張卡片是邀請，不是通知。
- **首次使用的設定。** 使用分步流程，例如 `AI setup wizard` 或 `Multi-step form`。

## 用法

本區塊派生自 Avalonia 的 `TemplatedControl`。不設定任何資料時顯示一份虛構的示例（`AiFeatureLaunchSamples`）。

```xml
<arc:AiFeatureLaunch Title="Thread summaries"
                     Description="Catch up on any long thread in seconds."
                     TryCommand="{Binding OpenSummaries}" />
```

```csharp
launch.Highlights = ["Decisions, owners and open questions", "Links back to every message"];
launch.Dismissed += (_, e) => settings.MarkSeen("thread-summaries", e.Reason);
```

### 結構

- **演示**：內置的循環動畫（一頁消息行被一條柔和的光帶從上到下讀過，變成三條要點的總結），或放在 `Demo` 中的你自己的內容（圖片、影片、任意控制項）。`DemoDescription` 是螢幕閱讀器在此處讀出的內容。`IsDemoVisible` 可隱藏這一區域。
- **暫停與關閉**：演示角上的暫停按鈕切換 `IsDemoPlaying`；關閉按鈕以原因 `Closed` 關閉。
- **文字**："New" 標簽、`Title`、`Description` 和 `Highlights`。
- **按鈕**："Maybe later"（原因 `Later`，用 `IsLaterVisible="False"` 隱藏）和試用按鈕（`TryRequested`、`TryCommand`；開啟 `DismissOnTry` 時也會以原因 `Tried` 關閉）。

### 記住回答

`IsOpen`（雙向）表示卡片是否顯示。`Dismiss(reason)` 從程式碼關閉它，`Show()` 讓它重新出現。直接設定 `IsOpen` 關閉時不會觸發 `Dismissed`。

<Demo name="Remembered" />

### 文字

`Labels`（`AiFeatureLaunchLabels`）包含標簽、按鈕文字，以及暫停、播放、關閉按鈕和要點列表的讀屏名稱。

## API 參考

<!-- api: AiFeatureLaunch -->

<!-- api: AiFeatureLaunchDismissReason -->

<!-- api: AiFeatureLaunchLabels -->

## 無障礙

- 卡片是一個有名稱的區域；標題是二級標題。要點組成一個有名稱的列表。
- 演示是一張以 `DemoDescription` 命名的影像；其中的繪製內容對螢幕閱讀器隱藏。
- 暫停按鈕按狀態命名為 "Pause the demo" 或 "Play the demo"；關閉按鈕命名為 "Dismiss"。
- 在卡片內任意位置按 **Escape** 都會關閉它（原因 `Closed`）。**Tab** 按閱讀順序到達暫停、關閉、稍後和試用按鈕。

## 動效

- 演示每 6.4 秒循環一次：文字行逐行寫出，一條柔和的光帶從上到下讀過，總結卡片升起，要點逐條變長，然後畫面淡出並重新開始。它只在完整動效下、可見且在檢視區內時執行，每幀不分配記憶體。
- 暫停時演示停在當前畫面。減弱動效或關閉動效時，顯示完成後的畫面，從不移動。
- 關閉時卡片在淡出的同時略微縮小，高度隨之收起；再次顯示時反向進行。減弱動效只保留淡出；關閉動效時立即關閉。

## 響應式

- 卡片最寬 420，在更窄的容器中占滿寬度。寬度不超過 360 時（`:compact`）正文內邊距收緊，兩個按鈕改為全寬上下排列，試用按鈕在上。
- 演示保持高度（184），按得到的寬度繪製畫面；需要時標題換到標簽下方。
- 沒有任何功能依賴停留。

## 相關元件

- `AnnouncementBar`，用於橫跨頁面的一行通知。
- `Card` 和 `Badge`，組成這張卡片的部件。
- `AI setup wizard`，用於為整個工作區開啟功能。
