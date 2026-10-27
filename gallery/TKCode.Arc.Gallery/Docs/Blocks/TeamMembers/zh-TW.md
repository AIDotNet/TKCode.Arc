---
title: Team members
summary: 工作區的成員頁：輸入或貼上電子郵件地址即可邀請，地址會飛入待接受的邀請清單並滾動更新席位數；還可變更角色、搜尋，並透過行內確認移除成員。
source-hash: 631db0a7fb43
---

<Demo name="Preview" />

## 何時使用

`TeamMembers` 是工作區或團隊設定頁面中的成員頁。它在同一處回答三個問題：誰擁有存取權限以及是什麼角色，誰已被邀請但尚未加入，以及還剩多少席位。邀請很快速：輸入或貼上地址，選擇角色，傳送。送出的地址會從輸入欄飛入待接受的清單，席位數同時向上滾動，讓人清楚看到邀請去了哪裡。

- **工作區或組織設定。** 顯示成員及其角色、最近活動時間與上線狀態，標出擁有者，並把目前使用者標記為 "You"。
- **一次邀請多人。** 按 Enter、逗號、分號或空格，地址會變成標籤，貼上的清單會被一次拆開。拼字有誤的地址會連同錯誤提示留在輸入欄中；已屬於某人的地址會被拒絕並說明原因。
- **有席位上限的方案。** 成員與待接受的邀請各佔一個席位。傳送按鈕會拒絕超出空閒席位，並說明需要刪去多少個地址；工作區已滿時，席位列會變成警告色。

## 何時不用

- **用於瀏覽的人員通訊錄。** 個人檔案、部門與聯絡資訊請使用 Team directory 區塊；此區塊管理的是存取權限。
- **定義每個角色可以做什麼。** 請使用 Roles and permissions 區塊。這裡的角色只是一個名稱加一行簡短文字，從選單中選取。
- **通用的標籤輸入。** 在邀請流程之外輸入標籤或收件者，請使用 `TagInput`。
- **大型組織。** 列未做虛擬化。成員達到數百人時，請一次只顯示一頁成員（並在伺服器端搜尋）。

## 用法

此區塊衍生自 Avalonia 的 `TemplatedControl`。成員是 `TeamMembersPerson` 物件，待接受的邀請是 `TeamMembersInvite` 物件，兩者都是可以繫結的 `AvaloniaObject`。角色是索引鍵（預設為 `"admin"`、`"member"`、`"viewer"`），由 `TeamMembersRoleOption` 描述；擁有者沒有角色選單。

```xml
<arc:TeamMembers Title="Members"
                 Description="Choose who can work in the Northwind Lab workspace."
                 WorkspaceName="Northwind Lab"
                 SeatLimit="10">
    <arc:TeamMembersPerson Name="Lena Hart" Email="lena@northwind.example" IsOwner="True" IsCurrentUser="True"
                           IsOnline="True" LastActive="Active now" />
    <arc:TeamMembersPerson Name="Theo Brandt" Email="theo@northwind.example" Role="admin" LastActive="Active now" />
    <arc:TeamMembersPerson Name="Ines Kowal" Email="ines@northwind.example" Role="member" LastActive="Active 3 hours ago" />
</arc:TeamMembers>
```

沒有處理常式時，每個動作都會立即生效。請用非同步處理常式把每個動作連接到你的伺服器；處理常式執行時區塊會顯示忙碌狀態，擲出例外時則保持原狀態：

```csharp
public interface IMembersApi
{
    Task InviteAsync(string workspace, IReadOnlyList<string> emails, string? role, CancellationToken cancel);
    Task ResendAsync(string inviteId, CancellationToken cancel);
    Task RevokeAsync(string inviteId, CancellationToken cancel);
    Task RemoveAsync(string memberId, CancellationToken cancel);
    Task SetRoleAsync(string memberId, string? role, CancellationToken cancel);
}

members.SendInvitesAction = async (request, cancel) =>
    await api.InviteAsync(workspaceId, request.Emails, request.Role, cancel);
members.ResendInviteAction = (invite, cancel) => api.ResendAsync(invite.Id!, cancel);
members.RevokeInviteAction = (invite, cancel) => api.RevokeAsync(invite.Id!, cancel);
members.RemoveMemberAction = (person, cancel) => api.RemoveAsync(person.Id!, cancel);
members.ChangeRoleAction = (change, cancel) => api.SetRoleAsync(change.Member.Id!, change.NewRole, cancel);
```

### 結構

- **頁首**：`Title`、`Description` 與席位計量（"7 of 10 seats"，下方是一條 4px 的進度列）。用 `IsSeatMeterVisible` 可隱藏計量。
- **邀請表單**：帶標籤的地址輸入欄、"Invite as" 角色選擇器（`InviteRole`，雙向）與傳送按鈕，按鈕文字為 "Send invite" 或 "Send 3 invites"。輸入欄下方的一行顯示提示、席位警告或錯誤。`IsInviteVisible` 會隱藏整個表單。
- **成員**：帶數量的標題、搜尋欄（`IsSearchVisible`），以及每位成員一列：帶上線狀態點的頭像、姓名與電子郵件、最近活動時間、角色選單（或 "Owner"）與移除按鈕。
- **待接受的邀請**：帶數量的標題，以及每個邀請一列：以地址首字母為縮寫的虛線頭像、地址、"role · detail"，以及 Resend 與 Revoke 按鈕。
- **頁尾**：最近一次動作的結果（"2 invites sent as Member"）。它會保留到下一次動作；`StatusText` 保存該文字。`IsFooterVisible` 會隱藏頁尾，但結果仍會被朗讀。

### 邀請

每段送出的文字都會單獨判斷，不分大小寫：

1. 不是完整地址（只有一個 @、網域帶點、最後一段至少兩個字母）：它留在輸入欄中，輸入欄變紅，下方的一行給出說明。再次輸入會清除錯誤。
2. 已經是標籤：靜默捨棄。
3. 是某位成員的地址：捨棄，下方的一行會指出該成員。
4. 是某個待接受邀請的地址：捨棄，並顯示錯誤。
5. 其他情況都會作為標籤加到末尾。

`EmailValidator` 可取代地址檢查。在空輸入欄中按 Backspace 會移除最後一個標籤，在空輸入欄中按 Enter 則傳送。仍在輸入的有效地址會計入按鈕文字，並在傳送邀請時變成標籤。`AddEmails`、`RemovePendingEmail`、`ClearPendingEmails` 與 `SendInvitesAsync` 可在程式碼中完成同樣的操作；`PendingEmails` 列出這些標籤。

`SendInvitesAction` 會收到一個 `TeamMembersSendRequest`，其中包含地址、角色索引鍵，以及區塊即將加入的 `TeamMembersInvite` 物件（如果你的伺服器回傳了 `Id`，請填入）。處理完成後，這些邀請會加入待接受清單，並觸發 `InvitesSent`。

### 席位

`SeatsUsed` 是成員加上待接受的邀請，`SeatsRemaining` 是 `SeatLimit` 的剩餘部分。只有標籤會佔用空閒席位：標籤多於空閒席位時，傳送按鈕會停用，下方的一行會要求刪去一些；席位全部佔滿時，進度列變成警告色，下方的一行也會這樣說明。撤回邀請或移除成員會立即釋出一個席位。

### 繫結集合與事件

`MembersSource` 與 `InvitesSource` 接受你自己的集合並跟隨其變化。當它們是可寫入清單時，區塊會自行加入已傳送的邀請，並移除被撤回的邀請與被移除的成員；對於唯讀集合，請在 `InvitesSent` 處理常式中加入邀請。每個動作都會引發一個攜帶變更內容的事件（`InvitesSent`、`InviteResent`、`InviteRevoked`、`MemberRemoved`、`MemberRoleChanged`），並執行對應的命令。`SearchText` 與 `InviteRole` 是雙向的。

<Demo name="Bound" />

### 伺服器拒絕時

擲出例外的處理常式會保持之前的狀態，並引發 `ActionFailed`，其中包含動作、動作對象與訊息：

- **傳送**：標籤保留，按鈕恢復，輸入欄下方的一行顯示錯誤（頁尾也會顯示）。
- **重新傳送**：按鈕恢復為 Resend，頁尾顯示錯誤。
- **撤回**與**移除**：該列保留（確認仍保持開啟），頁尾顯示錯誤。
- **角色變更**：新角色立即顯示，處理常式擲出例外時會恢復。

文字取自例外的訊息、`ErrorFormatter` 的文字，或在沒有訊息時使用內建的句子。取消（區塊離開視窗）不會被回報。

<Demo name="Failures" />

### 移除成員

移除按鈕會把該列就地變成確認狀態：該列著色，"Remove Ines Kowal?" 與一行關於將失去存取權限的說明取代姓名，Cancel 與 Remove 取代各欄。同一時間只有一列在詢問。按 Escape 或 Cancel 會返回，並把焦點還給移除按鈕；移除之後，焦點移到下一列的移除按鈕。`RequestRemove`、`CancelRemove` 與 `RemoveMemberAsync`（無需確認）可在程式碼中完成同樣的操作。

### 窄容器

區塊會讀取自身的寬度。寬度不超過 640 時，最近活動時間的文字移到電子郵件下方；600 時，頁首與邀請列堆疊，搜尋欄獨佔一行，內距從 20 收緊到 16；480 時，待接受邀請的按鈕移到地址下方；420 時，每個成員列拆成上方的人員資訊，以及下方的最近活動時間、角色與移除按鈕。

<Demo name="Narrow" />

### 唯讀

`IsReadOnly` 把清單顯示給無權管理的人：沒有邀請表單，角色以文字顯示，沒有移除、重新傳送或撤回按鈕。搜尋仍然可用。`IsFramed="False"` 讓區塊平鋪在頁面上，而不是放在卡片中。

<Demo name="ReadOnly" />

### 文字

區塊顯示或朗讀的每一段文字都在 `Labels` 中，這是一個帶英文預設值的 `TeamMembersLabels`；帶數值的文字是函式，因此在其他語言中可以調整語序。角色名稱與描述來自 `Roles`。

## API 參考

<!-- api: TeamMembers -->

<!-- api: TeamMembersPerson -->

<!-- api: TeamMembersInvite -->

<!-- api: TeamMembersRoleOption -->

<!-- api: TeamMembersLabels -->

<!-- api: TeamMembersSendRequest -->

<!-- api: TeamMembersRoleChange -->

## 無障礙

### 鍵盤

| 按鍵 | 動作 |
| --- | --- |
| Enter | 在輸入欄中：把文字變成標籤；輸入欄為空時則傳送。 |
| , ; Space | 在輸入欄中：把文字變成標籤。 |
| Backspace | 在空輸入欄中：移除最後一個標籤。 |
| Enter, Space, Up, Down | 在角色選擇器上：開啟選單，並聚焦目前角色。 |
| Up, Down, Home, End | 在角色選單中：移動；Enter 或 Space 選擇，Escape 關閉。 |
| Escape | 在請求確認移除的列中：取消。在搜尋欄中：清除。 |

### 螢幕閱讀器與焦點

- 區塊是一個以標題命名的群組，標題為 2 級標題；兩個分區標題為 3 級，並為各自的清單命名。
- 席位列是名為 "Seats" 的唯讀進度列，其描述讀作 "7 of 10 seats used"。
- 輸入欄由 `Labels.EmailFieldName` 命名。每個標籤是以其地址命名的清單項目，帶有一個名為 "Remove … from the invite list" 的移除按鈕。
- 輸入欄下方的一行在出現警告或錯誤時會以禮貌方式朗讀；每個動作的結果以禮貌方式朗讀，失敗則以強制方式朗讀。
- 角色選擇器是名為 "Role for <name>"（或 "Invite as"）的下拉方塊，其值為角色；選單項目會回報哪一項被選中。
- Remove、Resend 與 Revoke 按鈕以人員或地址命名；重新傳送之後，按鈕會說明邀請已再次送出。
- 確認開啟時焦點移到 Cancel，關閉時回到移除按鈕，移除或撤回之後再移到下一列。

## 動態效果

- **標籤**彈入（縮放 0.9 → 1，使用快速彈簧，並伴隨 100 ms 的淡入與模糊）；輸入欄與其他標籤以平滑彈簧滑到新位置。被移除的標籤在 130 ms 內縮小並淡出。
- **傳送**：處理常式執行時按鈕旋轉。隨後每個地址沿彈簧（視覺時長 0.5 s，無回彈）從其標籤飛向新的待接受列，飛行載體是一個色調逐漸淡出的膠囊；新列以平滑彈簧展開，虛線頭像在 170 ms 後依序彈入，間隔 100 ms，席位數與各數量隨之滾動。
- **列**透過在平滑彈簧上展開與收起來進入與離開（離開的列在 120 ms 內淡出）。搜尋會收起不符合的列，並以 200 ms 的淡入把它們帶回來。
- **原處變化的文字**（輸入欄下方的一行、待接受列的詳細資訊、頁尾）帶著模糊升起離開，新文字隨後升起進入；輸入欄下方的一行還會用彈簧調整其高度。變更後的角色逐字變形。
- **席位**：進度列的填色跟隨平滑彈簧，並在 240 ms 內變成警告色。
- **角色選單**：從其觸發器以短暫的縮放與淡入開啟，關閉更快；箭頭翻轉。
- 減少動態效果會保留傳送按鈕的旋轉與短暫的交叉淡入淡出；標籤、列、飛行、數量與進度列則直接跳轉。關閉時所有變更立即生效。

## 相關

- Invite people、Team directory 與 Roles and permissions，是其他 People 區塊。
- `Avatar`、`SearchField`、`TextMorph` 與 `Button`，組成此區塊的控制項。
- `TagInput`，用於邀請流程之外的標籤。
