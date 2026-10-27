---
title: 狀態切換按鈕
summary: 切換操作的標籤，同時保持它的位置與意圖。
source-hash: c42723b9cecd
---

<Demo name="Preview" />

## 何時使用

`ActionSwap` 是一個在幾個相關狀態之間輪替的控制項：追蹤與已追蹤、傳送邀請與已傳送、加入資料庫與已加入。每次按下都會進入下一個狀態，到最後再繞回第一個。膠囊在畫面上保持原位，寬度、色調、圖示和字母一起變化，讓人看到的是同一個操作進入了新狀態，而不是換了一個按鈕。

- **追蹤某個人或某個主題**：「追蹤」變成「已追蹤」並安靜地填色，再按一次就回去。進行中的文字讓請求送出期間的版面保持穩定。
- **傳送邀請或訊息**：「傳送邀請」在郵件服務回應時顯示「傳送中」，接著顯示帶有繪製勾號的「已傳送」，兩秒後自行回到「傳送邀請」，方便繼續邀請下一個人。
- **把內容加入某個集合**（資料庫、購物車、追蹤清單）：伺服器處理時，膠囊摺疊成一個圓形的載入圈，結果出來後重新展開。
- **可能失敗的切換**：動作被拒絕時，在原位顯示一個簡短的失敗詞，然後回到切換前的狀態，使用者可以立刻重試。

## 何時不用

- **以成功勾號收尾的一次儲存或發佈**：使用 `ActionButton`，它有固定的閒置、進行中、成功序列和重設計時器。
- **標籤永遠不變的操作**：使用 `Button`。
- **明暗切換**：使用 `ThemeSwitcher`。
- **需要先詢問的破壞性操作**：使用 `ConfirmMorph`，它在任何操作執行前先在原位提問。
- **每列一個控制項的密集表格**：每個字母都是一個動畫元素，所以一個畫面上會切換標籤的控制項數量要少。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:ActionSwap>
        <arc:ActionSwap.States>
            <arc:ActionSwapState Id="idle" Label="Send invite" Icon="{x:Static arc:ArcIcons.Send}" />
            <arc:ActionSwapState Id="sent" Label="Sent" Icon="{x:Static arc:ArcIcons.Check}" Tone="Success"
                                 PendingLabel="Sending" HoldFor="0:0:2" />
        </arc:ActionSwap.States>
    </arc:ActionSwap>
</UserControl>
```

```csharp
var swap = new ActionSwap { AsyncAction = async (next, current) => await invites.SendAsync() };
swap.States.Add(new ActionSwapState { Id = "idle", Label = "Send invite", Icon = ArcIcons.Send });
swap.States.Add(new ActionSwapState
{
    Id = "sent", Label = "Sent", Icon = ArcIcons.Check, Tone = ActionSwapTone.Success,
    PendingLabel = "Sending", HoldFor = TimeSpan.FromSeconds(2)
});
```

<Demo name="SendInvite" />

`ActionSwap` 衍生自 Avalonia 的 `Button`，保留 `Click`、`Command`、`IsEnabled`、Enter 與空白鍵啟動、焦點模型和自動化 Peer。依序在 `States` 中描述各個狀態；`Value` 是目前顯示狀態的 `Id`（為 null 時顯示第一個）。控制項至少需要兩個狀態，少於兩個時它不回應並變暗。

### 狀態

每個 `ActionSwapState` 是一個面：

- `Id` 與 `Label` 為必填。標籤同時是該狀態顯示期間的無障礙名稱。
- `Icon` 是標籤前 16 px 的 Lucide 圖示。使用 `ArcIcons` 或 `ArcIcons.Parse(pathData)`。
- `Tone` 決定膠囊的顏色：`Neutral`（預設，前景色填滿）、`Accent`、`Success`、`Warning` 與 `Quiet`。填滿色調承載主要操作；著色的 `Success` 與 `Warning` 用圖示上的顏色確認結果，`Quiet` 是帶邊框的表面，適合切換的「已完成」一側。
- `PendingLabel` 在進入該狀態途中 `AsyncAction` 執行時顯示，例如「已傳送」之前的「傳送中」。未設定時，膠囊會摺疊成圓形載入圈。
- `HoldFor` 讓該狀態停留一段時間後自行前進，適合「已傳送」這類短暫確認。計時器只在狀態或時間長度改變時重新啟動，所以資料重新整理不會把它重設。

<Demo name="Tones" />

### 動作

`AsyncAction` 在按下時執行，參數是下一個狀態與目前狀態，執行於控制項前進之前。回傳一個工作會顯示進行中階段，回傳 null 則立即前進。

- 工作執行期間控制項處於**進行中**：忽略按下，回報忙碌，並保留鍵盤焦點。即使回應很快，進行中的形態也至少顯示 450 ms，讓它讀起來是一個步驟，而不是閃一下。
- 工作完成時新狀態**提交**：`Value` 改變，標籤被朗讀，並觸發 `ValueChanged`。
- 工作發生錯誤或動作擲出例外時，控制項顯示 `FailureLabel`（預設 “Try again”）1.8 秒，然後回到離開的狀態，不提交任何內容。

```csharp
follow.AsyncAction = async (next, current) =>
{
    await api.SetFollowingAsync(userId, next.Id == "following");
};
follow.ValueChanged += (_, e) => status.Text = e.State.Label;
```

<Demo name="FollowToggle" />

### 沒有標籤的進行中

沒有 `PendingLabel` 的狀態，會在其動作執行期間把整個膠囊摺疊成一個圓，中間是小載入圈。內距與寬度沿同一條彈簧收攏，字母隨形狀閉合而讓開。結果到來時，膠囊重新展開，新字母隨形狀變寬升起。

<Demo name="Spinner" />

### 失敗

<Demo name="Failure" />

把原因顯示在控制項旁邊。失敗詞刻意寫得很短（“Try again”）；詳細資訊應放在膠囊回到原狀態之後使用者仍能讀到的訊息裡。

### 尺寸

`Size` 為 `Small`（高 36 px）、`Medium`（44 px，預設）或 `Large`（50 px），水平內距分別為 16、20 和 24 px。膠囊緊貼標籤，所以相關狀態的標籤長度要相近，避免擠動鄰近的元素。

<Demo name="Sizes" />

### 停用

把 `IsEnabled` 設為 `False`，控制項會降到 60% 不透明度並退出 Tab 順序。

<Demo name="Disabled" />

### 內容規範

- **標籤**：動詞加受詞，句首大寫，要短：“Send invite”、“Follow”。標籤既是顯示的文字，也是無障礙名稱。
- **進行中標籤**：現在分詞，不加刪節號：“Sending”。載入圈已經表達了進度。
- **結果標籤**：過去式，一到兩個詞：“Sent”、“Added”。
- **數量**：兩到三個狀態。更多就該使用選單或分段控制項。

## API 參考

<!-- api: ActionSwap -->

<!-- api: ActionSwapState -->

<!-- api: ActionSwapValueChangedEventArgs -->

| `ActionSwapTone` 值 | 外觀 |
|---|---|
| `Neutral` | 前景色填滿，背景色文字（預設）。 |
| `Accent` | 強調色填滿，強調前景色文字。 |
| `Success` | 成功色著色的表面，成功色圖示。 |
| `Warning` | 警告色著色的表面，警告色圖示。 |
| `Quiet` | 帶邊框的表面，預設文字顏色。 |

| `ActionSwapPhase` 值 | 含義 |
|---|---|
| `Idle` | 顯示目前狀態。按下會執行動作。 |
| `Working` | `AsyncAction` 正在執行，忽略按下。 |
| `Failed` | 動作失敗，`FailureLabel` 顯示 1.8 秒。 |

## 無障礙

- 控制項是原生按鈕：無障礙名稱是目前顯示面的可見標籤，所以動畫中的字母不會被逐個朗讀。
- 進行中時它回報自己無法使用，但保留鍵盤焦點；隱藏的禮貌狀態區會朗讀新標籤、進行中標籤和失敗。
- 少於兩個狀態時控制項不回應。

### 鍵盤

| 按鍵 | 行為 |
|---|---|
| `Enter` / `Space` | 執行動作並進入下一個狀態。進行中時忽略。 |
| `Tab` / `Shift+Tab` | 移到下一個或上一個控制項。進行中的膠囊保留自己的焦點位置。 |

## 動態效果

- 只有標籤中變化的字母從 4 px 的柔和模糊中升起，保留的字母滑到新位置；離開的字母更快地上移淡出。寬度沿彈簧跟隨。
- 圖示透過模糊換位：舊圖示縮小退去，新圖示從模糊中長出。成功圖示帶著多一點彈性落定，同時筆畫從起始邊擦入。
- 色調、內距與寬度沿同一條彈簧運動，任何變化都可以在途中被打斷。
- 按下時膠囊縮放到 97%（寬於 220 px 時為 98.5%）並彈回。滑鼠遊標停留的樣式只對滑鼠與手寫筆生效；觸控上只有按下縮放作為回饋。

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 新標籤或圖示 | 字母從模糊中升起，圖示長出，寬度彈簧 | 120 ms 交叉淡化，無寬度與摺疊動畫 | 立即 |
| 沒有標籤的進行中 | 膠囊摺疊成圓形，中間是載入圈 | 立即摺疊，靜止的圓環 | 立即 |
| 按下 | 在靈敏的彈簧上縮放到 97% | 無縮放 | 無縮放 |

## 相關元件

| 元件 | 何時選擇 |
|---|---|
| `ActionButton` | 一次儲存或發佈以成功勾號和重設計時器收尾 |
| `Button` | 標籤不會隨狀態改變 |
| `ThemeSwitcher` | 操作是在明暗之間切換 |
| `ConfirmMorph` | 破壞性操作需要先在原位提問 |
