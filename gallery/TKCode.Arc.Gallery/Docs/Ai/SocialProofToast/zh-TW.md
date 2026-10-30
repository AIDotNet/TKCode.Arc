---
title: Social proof toast
summary: 用於到達頁與定價頁的社會認同通知堆疊：來自個人與公司的最新試用、升級與已驗證購買每隔幾秒出現一則；滑鼠懸停時暫停並展開，訪客可以關閉任何一則通知。
source-hash: ff6c35ef997f
---

<Demo name="Preview" />

## 何時使用

`SocialProofToast` 呈現其他人此刻正在做什麼：開始了試用、團隊完成了升級、一筆已驗證的購買。每隔幾秒，一則新通知從前方升起，較早的通知依序退到它後面。指標懸停在堆疊上，或鍵盤焦點進入其中，會暫停輪播並把通知展開，方便逐則閱讀或關閉。

- **到達頁與定價頁**：呈現最近的試用、購買或升級。
- **發表頁或候補名單頁**：即時顯示有人加入、有團隊升級。
- **結帳按鈕附近**：突顯知名公司的已驗證購買。

## 何時不用

- **訪客自己操作之後的回饋**（已儲存、已複製、失敗）：使用 `Toast`。
- **已登入的產品內部**，且這些動態不是行銷內容：使用動態清單。
- **沒有真實事件可呈現時。** 捏造的社會認同會損害信任；模擬只用於預覽。

## 用法

加入堆疊，並在 `Events` 中依從舊到新的順序傳入你的動態。在 `InitialDelay`（0.8 秒）之後，它每隔 `Interval`（預設 4 秒）顯示一則通知，最多保留 `MaxVisible` 則（3 則），播到結尾後從頭開始，除非 `Loop` 為 false。沒有事件也沒有模擬（見下文）時，堆疊維持空白，直到你推送一則。

```csharp
// 計費後端近期動態中的一列。
record Signup(string Id, string FirstName, string Action, string City, DateTimeOffset CreatedAt, bool IsPaid);

stack.Events.AddRange(recent.Select(r => new SocialProofEvent(r.Id, r.FirstName, r.Action)
{
    Meta = r.City,
    Time = r.CreatedAt,
    VerifiedLabel = r.IsPaid ? "Verified purchase" : null,
}));
```

### 你自己的事件

<Demo name="YourEvents" />

每個 `SocialProofEvent` 都有穩定的 `Id`、一個 `Name`（名字或公司名稱，以中等字重顯示）以及緊接其後的 `Action`。`Meta` 補充簡短的脈絡，例如城市或席次數。`Time` 顯示為相對時間（Just now、4 min ago、2 h ago）；`TimeText` 原樣顯示。`IsVerified` 或 `VerifiedLabel` 會加上一個附文字的勾選徽章。

個人以帶姓名縮寫的 `Avatar` 呈現，或使用其 `Portrait` 大頭照。把 `Actor` 設為 `Company` 時顯示為圓角方塊，內含名稱的第一個字母，或你在 `Logo` 中提供的標誌（24 × 24 的描邊圖形，以文字顏色繪製，因此會跟隨佈景主題）。

呼叫 `Push` 可以在事件發生的那一刻顯示它，例如來自伺服器推播的事件。它會立刻出現在最前面，下一則通知的倒數也會重新開始。

用於預覽時，讓 `Events` 維持空白，並給堆疊一個 `SocialProofToastSimulation`：它的事件依從舊到新排列，每則都是一個 `SocialProofToastSimulationEvent`，包含事件本身以及它發生在多久之前（`Ago`）。每一輪都會從那一刻往回計算時間，因此動態永遠不會漂移成 "40 min ago"。你自己的 `Events` 會讓它讓位，把 `Simulation` 設為 null 會清空堆疊。這裡預覽中的所有名字、公司與地點都是虛構的。

```csharp
stack.Simulation = new SocialProofToastSimulation
{
    Events =
    [
        new SocialProofToastSimulationEvent { Event = new SocialProofEvent("s1", "Sofia", "started a Pro trial") { Meta = "Madrid" }, Ago = TimeSpan.FromMinutes(14) },
        new SocialProofToastSimulationEvent { Event = new SocialProofEvent("s2", "Mateo", "bought the Team plan") { Meta = "Lisbon" }, Ago = TimeSpan.FromMinutes(11) },
    ],
};
```

### 結構

1. **堆疊**：所有通知共用一個 64px 的位置。最前面的通知貼齊邊緣；其後的每一則後退 10px、縮小 5%，文字淡出。
2. **標識**：個人是 36px 的圓形大頭貼，公司是圓角方塊。
3. **句子**：名稱與動作在同一行，過長時以刪節號截斷。
4. **第二行**：附加資訊與時間，接著是已驗證徽章（圖示加文字）。
5. **關閉**：24px 的按鈕，在懸停或鍵盤聚焦時出現，在觸控螢幕上一律顯示。

### 角落位置

<Demo name="Corner" />

`Position` 把堆疊固定在視窗的某個角落：`BottomLeft`、`BottomRight`、`TopLeft` 或 `TopRight`。它寬 360px，距離邊緣 24px；視窗窄於 480px 時，它會橫跨整個寬度並留下 16px 邊距。頂端角落向下堆疊。`Inline`（預設）讓它留在版面中並填滿容器寬度；文件與預覽用它，真實頁面請用角落位置。

### 選取與限制

<Demo name="Selectable" />

`IsSelectable` 讓每則通知成為一個按鈕並引發 `Selected`，例如用來開啟定價頁或客戶案例。`Limit` 在一個工作階段顯示這麼多則通知後停止，讓頁面保持安靜。每當一則通知出現時觸發 `Shown`（用於曝光統計），訪客關閉一則通知時觸發 `Dismissed`。

### 暫停

堆疊被懸停或持有鍵盤焦點時、捲出視野時以及被隱藏時，輪播都會自動暫停，倒數會保留剩餘時間。強制回應視窗或 Cookie 橫幅開啟期間請設定 `IsPaused`。`ExpandOnHover="False"` 讓堆疊在懸停時保持收合（仍會暫停）。`Prefill` 會立即顯示這麼多則通知且不播放進場動畫，適合預覽與螢幕截圖。`Restart` 清空堆疊並從頭開始播放動態。

### 自訂文字

`Labels` 包含堆疊顯示或朗讀的所有文字。替換整個物件即可在地化或改寫：

```csharp
stack.Labels = new SocialProofToastLabels
{
    Region = "Actividad reciente",
    JustNow = "Ahora mismo",
    MinutesAgo = minutes => $"hace {minutes} min",
    Dismiss = sentence => "Cerrar: " + sentence,
};
```

### 內容指引

- 動作要簡短具體："started a Pro trial"、"upgraded to Business"。
- 個人用名字，團隊用公司名稱。不要顯示姓氏或電子郵件地址。
- `Meta` 只放一項資訊：城市或席次數，不要兩者都放。

### 最佳做法

- 使用來自計費或分析後端的真實、近期事件。
- 保持安靜：間隔 4 到 8 秒，每個工作階段限制在 5 到 8 則左右。
- 強制回應視窗或 Cookie 橫幅開啟時暫停它。

### 常見問題

**新通知會打斷螢幕閱讀器嗎？** 不會。新通知不會被播報；只有關閉操作會以禮貌方式確認。

**畫面上最多有幾則通知？** 最多 `MaxVisible` 則，另加一則正從後方淡出的通知。

## API 參考

<!-- api: SocialProofToast -->

<!-- api: SocialProofEvent -->

<!-- api: SocialProofToastLabels -->

<!-- api: SocialProofToastSimulation -->

<!-- api: SocialProofToastSimulationEvent -->

<!-- api: SocialProofToastEventArgs -->

## 無障礙

- 堆疊是一個有標籤的區域（"Recent activity"）。新通知不會被播報，螢幕閱讀器使用者不會每隔幾秒就被打斷；關閉一則通知後會以禮貌的狀態提示確認。
- 每個關閉按鈕都會說明它關閉的是哪則通知，例如 "Dismiss: Sofia started a Pro trial"。
- 超出限制而正在淡出的通知對輔助科技隱藏，也不接受輸入。懸停與鍵盤焦點都會暫停輪播，因此有人閱讀或操作時不會有東西移動。
- 已驗證狀態是圖示加文字標籤，從不只靠顏色表達。大頭貼不會被單獨朗讀，因為名稱已經在句子裡。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 移到每則可見通知的關閉按鈕（設定 `IsSelectable` 時也會移到通知本身）。焦點進入堆疊會暫停並展開它 |
| Enter、Space | 在關閉按鈕上關閉該通知；設定 `IsSelectable` 時選取取得焦點的通知 |
| Escape | 關閉取得焦點的通知，或最前面的通知。焦點移到下一則通知 |

## 動效

| 變化 | 完整 | 減弱 | 關閉 |
|---|---|---|---|
| 新通知到達 | 在最前面上升 16px 並淡入；其他通知以無回彈彈簧（視覺時長 0.24s）各後退 10px、縮小 5% | 淡入；其他通知立即就位 | 立即 |
| 被遮住的通知 | 內容在 160ms 內淡出，文字從不重疊 | 同樣淡出 | 立即 |
| 懸停或聚焦 | 堆疊以同一彈簧展開 | 立即 | 立即 |
| 關閉 | 在 160ms 內淡出並略微縮小；其餘通知以彈簧補上空位 | 淡出；其餘立即就位 | 立即 |
| 超出數量 | 最後面的通知在 160ms 內淡出 | 同樣淡出 | 立即 |

通知一律是 64px 高，因此各狀態之間沒有尺寸變化；內嵌堆疊預留了展開後的高度，懸停時頁面不會移動。動態效果減弱或關閉時輪播仍會繼續：它是內容，而不是裝飾。

## 相關

- `Toast`：對訪客自身操作的簡短確認。
- `Avatar`：動態中的人物。
