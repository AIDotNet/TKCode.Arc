---
title: Referral card
summary: 雙向獎勵的推薦卡片：複製或分享邀請連結，以標籤形式依電子郵件邀請好友，看著邀請從已邀請到已註冊再到已賺取，獎勵金額隨之累加，里程碑獎勵隨之解鎖。
source-hash: c111993428ce
---

<Demo name="Preview" />

## 何時使用

`ReferralCard` 把整個推薦計畫放進一張卡片。頂部是邀約（"Give $20, get $20"），邀請連結放在一個安靜的欄位裡，旁邊是複製按鈕和三個分享按鈕，電子郵件欄位會把輸入的每個地址變成一個標籤。下方三個滾動數字分別統計已邀請、已註冊和已賺取，分段進度列顯示離下一個獎勵還有多遠，被邀請的人在原位從 Invited 變為 Signed up 再變為 Earned。

- **推薦或邀請計畫**，放在帳戶設定、帳單或新手引導清單中。
- **呈現邀請帶來的收益**，以及離額外獎勵還有多遠。

## 何時不用

- **帶角色和權限的團隊成員邀請**：使用成員表格。
- **公開排行榜或多欄的聯盟佣金結算**：使用表格。
- **一次性的分享連結**，沒有需要追蹤的內容：一個欄位加 `CopyButton` 就夠了。

## 用法

把後端傳回的推薦紀錄依由新到舊傳給 `Referrals`，註冊和付款發生時替換這份清單：計數、里程碑和清單都由它推導而來。傳送邀請會呼叫 `InviteHandler`；伺服器接受後返回即可，失敗時拋出例外，例外訊息就是使用者會看到的文字。

```csharp
card.Link = $"https://app.example.com/r/{me.Code}";
card.Reward = 20;
card.Referrals = referrals;            // IReadOnlyList<Referral>, newest first
card.Milestone = new ReferralMilestone
{
    Goal = 5,
    Reward = "3 months free",
    Detail = "Applied to your next invoice",
};
card.InviteHandler = async (emails, token) =>
{
    var response = await http.PostAsJsonAsync("/api/referrals/invite", emails, token);
    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException("Could not send the invites. Nothing was sent.");
    }
};
card.MilestoneReached += (_, e) => analytics.Track("referral_milestone_unlocked");
```

沒有 `Referrals` 也沒有 `Simulation` 時，卡片為空：計數為零，清單顯示 "No invites yet. Friends you invite show up here."，沒有 `Reward` 時標題為 "Invite friends"；`Link` 與 `Milestone` 也為空。套件不附帶範例計畫。要展示一個推進中的計畫（預覽、新手導覽），請給卡片一個 `ReferralCardSimulation`：它起始的清單，以及讓好友依序經歷各個階段的節拍。`Referrals` 為 null 時，卡片顯示模擬的清單，並在卡片有 40% 進入畫面時播放一次節拍；上方的預覽就是這樣播放展示庫撰寫的虛構計畫（一位已註冊的好友完成訂閱，一位已邀請的好友先註冊再訂閱，第五個付費推薦解鎖里程碑）。

```csharp
card.Simulation = new ReferralCardSimulation
{
    Referrals = program,                                   // the list it starts from, newest first
    Beats =
    [
        new(TimeSpan.FromSeconds(1.4), "p4", ReferralStage.Earned),
        new(TimeSpan.FromSeconds(2.2), "p5", ReferralStage.SignedUp, "Ana Souza")
    ]
};
```

設定 `Referrals` 會把卡片交給你的資料並停止計畫；每張卡片使用自己的模擬。`Simulate` 可以關掉播放；對使用你自己清單的卡片也可以開啟它（此時每次模擬傳送的第一個地址會依序經歷各個階段）。

### 結構

1. **邀約**：禮物標記、由 `Reward` 和 `Currency` 產生的標題，以及說明雙方各得什麼的一行文字。
2. **邀請連結**：網域為弱化色、邀請碼為內文色，末端是複製按鈕，另有三個分享按鈕：貼文、人脈和電子郵件。
3. **依電子郵件邀請**：固定高度、可橫向捲動的欄位中的標籤，傳送按鈕，以及下方的提示。
4. **漏斗**：已邀請、已註冊和已賺取，以等寬數字滾動顯示。
5. **里程碑**：每個所需的付費推薦一個分段（最多 12 段），以及一行說明它解鎖的獎勵。
6. **你的邀請**：每個人的頭像、電子郵件、日期和階段（圖示加文字），放在固定高度的清單中。

### 你的資料

<Demo name="YourData" />

當服務回報有人註冊或付款時，用新清單替換 `Referrals`；各列依 `Id` 保持位置，只有變化的部分會動。傳送成功後，這些地址會以 Invited 狀態出現在清單頂部，直到你的下一份清單到來。

### 分享

每個分享按鈕都會觸發 `ShareRequested`，附上管道和一個現成的 URL。開啟它並設定 `Handled`；沒有處理常式這樣做時，分享是模擬的，即時區域會說明什麼也沒有發布。電子郵件按鈕會產生一個 `mailto:` 連結，內文是 `ShareMessage` 和連結。貼文和人脈按鈕使用通用圖示，而不是社群網路的標誌：把你所用網路的分享網址寫進 `PostShareUrlFormat` 和 `NetworkShareUrlFormat`，其中的 `{url}` 和 `{text}` 會被填入。

```csharp
card.ShareMessage = "I use our invoicing app. Join with my link and we both get $20.";
card.PostShareUrlFormat = "https://social.example/share?url={url}&text={text}";
card.ShareRequested += (_, e) =>
{
    Launcher.LaunchUriAsync(new Uri(e.Url));
    e.Handled = true;
};

// The same URLs elsewhere:
var mail = ReferralCard.BuildShareUrl(ReferralShareChannel.Email, link, message);
```

### 依電子郵件邀請

輸入一個地址後按 Enter、逗號、空白鍵或 Tab，它就會變成一個標籤；貼上一串地址會把其中每一個都加進來。無效的地址會變成帶警示圖示的標籤，在修正或移除之前傳送會被拒絕。重複的地址、已在清單中的地址以及超過 `MaxInvites`（10）的部分會被拒絕並附上說明。邀請傳送期間，欄位變為唯讀，傳送按鈕顯示載入動畫；失敗時標籤保留，並顯示原因。

### 窄卡片

<Demo name="Narrow" />

卡片寬度自動調整，最寬 560px。卡片寬度小於 470px 時，分享按鈕單獨佔滿一列，里程碑那一行預留兩行的高度。過長的電子郵件地址以刪節號截斷，日期和階段始終可見。

### 你自己的文字

`Labels` 包含卡片顯示或朗讀的每一個詞；數量、金額和日期是函式。`Title` 和 `Description` 會直接替換標題及其下方的文字。

```csharp
card.Labels = new ReferralCardLabels
{
    Title = reward => $"Offrez {reward}, recevez {reward}",
    Send = "Envoyer",
    StageEarned = reward => $"{reward} gagnés",
};
```

### 內容指引

- `Reward`、`Currency` 和文案要與實際的計畫條款一致；標題由獎勵金額產生。
- 用幾個詞說明里程碑獎勵（"3 months free"），細則放在 `Detail` 裡。
- 預覽中的人物、金額與里程碑都是虛構的；上線前請傳入你自己的計畫。

### 常見問題

**卡片會自己寄信嗎？** 不會。由 `InviteHandler` 傳送；沒有它時傳送是模擬的，提示也會這樣說明。

**為什麼清單高度不變？** 新邀請在固定高度的清單頂部展開，所以卡片不會變高，下方的內容也不會移動。

## API 參考

<!-- api: ReferralCard -->

<!-- api: Referral -->

<!-- api: ReferralMilestone -->

<!-- api: ReferralCardSimulation -->

<!-- api: ReferralCardSimulationBeat -->

<!-- api: ReferralCardLabels -->

<!-- api: ReferralShareEventArgs -->

<!-- api: ReferralInvitesEventArgs -->

<!-- api: ReferralLinkEventArgs -->

<!-- api: ReferralMilestoneEventArgs -->

## 無障礙

- 分享按鈕是帶名稱和工具提示的圖示按鈕（Share in a post、Share with your network、Share by email）；一個禮貌的即時區域會回報發生了什麼，包括 "Simulated, nothing was posted."。
- 電子郵件欄位下方的提示描述該欄位，並以禮貌方式朗讀。出錯時顯示警示圖示加文字，欄位邊框變為危險色，從不只靠顏色；無效的標籤會在名稱中說明。
- 階段是圖示加文字。里程碑進度列以進度列的身分朗讀，數值作為說明文字，例如 "4 of 5 paid referrals"。
- 每個漏斗數字都以它的標籤作為名稱；人員清單的名稱是 "Your invites"。
- 傳送期間欄位變為唯讀而不是停用，所以鍵盤焦點保持不動。

### 鍵盤

| 按鍵 | 操作 |
|---|---|
| Tab | 依序經過複製按鈕、三個分享按鈕、電子郵件欄位和傳送按鈕 |
| Enter、逗號、空白鍵或 Tab | 在電子郵件欄位中把輸入的地址變成標籤 |
| Enter | 在空的電子郵件欄位中傳送標籤 |
| Backspace | 在空的電子郵件欄位中刪除最後一個標籤；每個標籤也有自己的刪除按鈕 |
| Ctrl/Cmd + V | 貼上以逗號、空白或換行分隔的地址，會全部加入 |

## 動效

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 漏斗數字 | 以標準曲線在 600ms 內滾動到新值，使用等寬數字 | 立即變化 | 立即變化 |
| 階段文字、里程碑那一行、分享圖示和提示 | 舊內容 100ms 淡出，新內容 160ms 淡入並上升 2px | 短暫交叉淡化，不上升 | 立即變化 |
| 里程碑分段 | 240ms 內從左側填滿；解鎖時填色變為成功色 | 相同 | 立即變化 |
| 新的邀請 | 在固定高度的清單頂部以無回彈的 0.24s 高度彈簧展開，同時在 160ms 內淡入 | 立即出現 | 立即出現 |
| 模擬計畫 | 進入畫面 40% 時播放一次 | 以兩倍速度播放 | 以兩倍速度播放 |

各個等級的最終狀態都相同；減少動態效果只去掉滾動、上升和高度變化。

## 相關

- `CopyButton`：複製單一值並給出確認。
- `TagInput`：不涉及電子郵件和傳送的自由標籤。
- `Avatar`：清單中的人員。
- `AgentStatus`：另一個精簡的即時狀態卡片。
