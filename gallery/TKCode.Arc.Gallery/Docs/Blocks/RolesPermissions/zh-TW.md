---
title: Roles and permissions
summary: 角色與權限矩陣，具有會產生漣漪的群組核取方塊、鎖定的擁有者權限、角色複製，以及一次儲存所有變更的審閱列。
source-hash: 88e5f5c8c024
---

<Demo name="Preview" />

## 何時使用

`RolesPermissions` 是工作區設定中的存取權限頁面：角色排在上方，權限依群組排在左側，兩者交會處是一個開關。編輯過程中不會套用任何內容。每個被變更的儲存格會出現一個小圓點，角色的標頭也會出現一個，同時審閱列從下方滑出，顯示變更數量、變更涉及的角色，以及這些變更會影響多少人。使用者可以一次儲存所有變更，也可以全部捨棄。

- **產品設定中的角色式存取控制。** 類似擁有者、管理員、成員與訪客的角色，對應幾組權限（文件、團隊、帳單、安全性）。
- **在變更生效前先審閱。** 在儲存之前，審閱列會顯示 "Lead and Member · reaches 13 people"，讓使用者在修改的同時就能看到變更的影響範圍。
- **以現有角色為基礎建立新角色。** 從標頭選單複製一個角色，調整副本所在的欄，再與其餘變更一起儲存這個新角色。

## 何時不用

- **將人員指派到角色。** 矩陣決定角色能做什麼，而不是誰擁有該角色。成員及其角色請使用 `Team members`。
- **少量彼此獨立的設定。** 幾個開關選項更適合在 `Settings page` 中以開關列的形式呈現。
- **數百個權限。** 每個儲存格都會被繪製，沒有虛擬化。請將長清單分組，並讓大多數群組預設摺疊（`IsExpanded="False"`），或拆分頁面。
- **使用者自己的帳號安全性。** 密碼、兩步驟登入與工作階段應放在 `Security settings` 中。

## 用法

此 Block 衍生自 Avalonia 的 `TemplatedControl`。角色是 `RolesPermissionsRole` 物件（包含索引鍵、名稱、成員數與選單中的一行說明；擁有者使用 `IsLocked`），群組是包含 `RolesPermissionsItem` 權限的 `RolesPermissionsGroup` 物件，授權則是由角色索引鍵與權限索引鍵組成的一組 `RolesPermissionsGrant` 配對。`SavedGrants` 是已儲存的狀態；`Grants` 是目前狀態，並在每次變更時寫回。

```xml
<arc:RolesPermissions Title="Roles &amp; access" WorkspaceName="Northwind Studio" MemberCount="31"
                      RolesSource="{Binding Roles}" GroupsSource="{Binding Groups}"
                      SavedGrants="{Binding SavedGrants}" Grants="{Binding Grants, Mode=TwoWay}" />
```

```csharp
matrix.Roles.Add(new RolesPermissionsRole("founder", "Founder", 1, "Lena Hart", isLocked: true));
matrix.Roles.Add(new RolesPermissionsRole("lead", "Lead", 4, "Omar Diaz, Priya Rao and 2 others"));
matrix.Groups.Add(new RolesPermissionsGroup("team", "Team",
    new("team.invite", "Send invitations"), new("team.roles", "Assign roles"), new("team.remove", "Remove people")));
matrix.SavedGrants = new HashSet<RolesPermissionsGrant> { new("lead", "team.invite") };
```

### 結構

- **頁首**：`Title`、一行包含工作區名稱與兩個計數的副標題（計數在變化時會滾動：成員數來自 `MemberCount`，或來自 `Members` 的數量；角色數來自欄），以及右側的成員頭像。當頭像在標題旁放不下時，它們會移到標題下方。
- **標頭列**：標籤欄上方的 "Permission" 標題，以及每個角色一個按鈕：角色名稱（鎖定的角色前面帶鎖圖示）、成員數，以及在選單開啟時會旋轉的箭頭。
- **群組列**：一個顯示群組名稱與權限數量的展開按鈕，以及每個角色一個核取方塊；當群組內所有權限都開啟時為勾選，只有部分開啟時為不確定狀態。
- **權限列**：權限的名稱（欄較窄時會換行），以及每個角色一個開關。
- **頁尾**：兩個圖例，鎖與未儲存的圓點。
- **審閱列**：變更數量、角色與受影響的人數，以及 Discard 與 Save 按鈕。有內容需要儲存時，它會在頁尾下方展開。

### 儲存格、群組與鎖定的角色

儲存格可透過點擊、**Space** 或 **Enter** 切換。群組核取方塊在關閉或不確定時會將該群組的所有權限開啟，在開啟時則全部關閉；發生變化的儲存格依群組順序依次相隔 60 ms 變化。鎖定角色的儲存格永遠開啟：按下它會讓方框抖動，並播報原因。切換回已儲存值的儲存格不再算作變更。

`GrantChanged` 會回報每個發生變化的儲存格，包括角色、權限、新值、現在是否未儲存，以及變更的來源（儲存格、群組、復原、Discard 或程式碼）。`GroupToggled` 在群組核取方塊之後觸發。`SetGrant`、`ToggleGroup`、`IsGranted` 與 `IsUnsaved` 可在程式碼中完成同樣的操作。

### 複製與移除角色

每個標頭都會開啟一個選單，顯示角色的名稱與描述。**Copy role** 會在來源角色右側插入 "Guest (copy)"（該名稱已被使用時為 "Guest (copy 2)"），並帶有來源角色的權限；它的欄會以彈簧展開，焦點移到它的標頭。副本在儲存之前都是新建的：它顯示為 "New role"，算作一項變更，它自己的編輯也會計數並顯示圓點。**Undo edits** 會將該角色的儲存格還原；**Remove role**（僅限新角色）會再次關閉這一欄。副本會插入 `Roles`，當 `RolesSource` 是可新增的清單時則插入 `RolesSource`。`RoleDuplicated`、`RoleDeleted` 與 `ChangesReverted` 會回報每一步；`DuplicateRole`、`DeleteRole` 與 `RevertRole` 可在程式碼中完成。

### 儲存

Save 會等待 `SaveHandler` 處理一個 `RolesPermissionsSaveRequest`：其中包含儲存後的所有授權、發生變化的儲存格、角色與新角色，以及審閱列顯示的數量。執行期間按鈕會旋轉，矩陣不再接受變更（試圖變更的使用者會聽到 "Saving, one moment"）。隨後所有圓點一起消失，審閱列在 `SavedHoldDuration`（1.6 s）內顯示帶勾號的 "Access updated" 並關閉，`SavedGrants` 成為新的基準。Discard 會一次捨棄所有變更，儲存格逐列以漣漪方式還原。

```csharp
matrix.SaveHandler = async (request, cancellation) =>
{
    // Send request.Grants (and request.AddedRoles) to your server; throw to report a failure.
    await Task.Delay(900, cancellation);
};
matrix.Saved += (_, e) => Debug.WriteLine($"{e.Request.ChangeCount} access changes saved");
```

發生例外時會保留所有變更：審閱列顯示 "Couldn't save" 與訊息（或 `ErrorFormatter` 回傳的文字），Save 變為 "Try again"，下一次編輯會清除該失敗狀態。`CancelSave` 會取消處理常式的權杖並回到審閱狀態。

<Demo name="Failure" />

### 繫結狀態與事件

`Grants` 是雙向的：繫結它可讓模型保持同步，也可以寫入一個新的集合，從外部變更儲存格。`PendingChangeCount`、`HasUnsavedChanges` 與 `SaveState` 跟隨審閱列。摺疊群組時會將它的 `IsExpanded` 寫回，因此頁面可以摺疊或展開所有群組。

<Demo name="Bound" />

### 有框線或平鋪

`IsFramed`（預設值）會繪製一張帶 1px 框線與 16 圓角的卡片，就像本頁所示。將它設為 false 即可用於完整的設定頁面：沒有框線，頁面邊距為 32（寬度小於 540 時為 16）。

<Demo name="Flat" />

### 窄容器

此 Block 讀取自身的內容寬度。寬度降到 760 時，標籤欄需要 200，每個角色需要 92；低於 540 時分別為 148 與 80，群組計數與標頭箭頭會消失，權限名稱縮小到 12 並換行，審閱列也會去掉 "pending" 一詞。當角色放不下時，它們會在固定的標籤欄下方橫向捲動，捲動時標籤欄會顯示出邊緣；複製的角色會自行捲動到可見位置。

<Demo name="Narrow" />

### 其他文字

所有可見與被播報的文字都在 `Labels` 中，這是一個帶有英文預設值的 `RolesPermissionsLabels` 物件：副標題格式、"1 person" 與 "{0} people"、選單列、副本名稱、圖例、審閱列的文字與格式、螢幕閱讀器名稱以及每則播報。請用物件初始設定式取代它，只設定需要變更的部分。

## API 參考

<!-- api: RolesPermissions -->

<!-- api: RolesPermissionsRole -->

<!-- api: RolesPermissionsGroup -->

<!-- api: RolesPermissionsItem -->

<!-- api: RolesPermissionsGrant -->

<!-- api: RolesPermissionsSaveRequest -->

<!-- api: RolesPermissionsSaveState -->

<!-- api: RolesPermissionsLabels -->

## 無障礙

- 矩陣是一個 **Tab** 停靠點。它從第一個角色標頭開始，並記住使用者最後操作的部分；隨後 **Tab** 會移到審閱列的按鈕。
- **Left** 與 **Right** 沿列移動並在列的兩端停住；在群組列上，從第一個核取方塊按 **Left** 會到達該群組的按鈕。
- **Up** 與 **Down** 移到同一欄中上方或下方的列；當該列在這一欄沒有儲存格時，會對齊到最近的欄（從群組按鈕向下到第一個角色）；已摺疊的列會被略過。
- **Home** 與 **End** 移到該列的第一個與最後一個部分；**Ctrl+Home** 與 **Ctrl+End** 分別移到第一個角色標頭與矩陣的最後一個儲存格。
- **Space** 或 **Enter** 切換儲存格、摺疊群組或開啟角色的選單。**Escape** 關閉選單並回到它的標頭。
- Save 與 Discard 之後，焦點回到最後操作的儲存格；複製之後回到新角色的標頭；Remove role 之後回到左側的標頭。
- 矩陣是一個以標題命名的資料格線。群組核取方塊是名為 "Lead: all Team permissions" 的三態核取方塊；權限儲存格是名為 "Lead: Send invitations" 的開關。鎖定的儲存格會附加 ", always on"；未儲存的儲存格會附加 ", unsaved"。
- 角色標頭是帶選單的按鈕，以角色與它的成員數命名；鎖定的角色會附加 ", permissions are fixed"，有變更時會附加 ", unsaved changes"。群組按鈕會回報它是否已展開。
- 每次開關、群組切換、複製、移除、復原、捨棄與儲存都會以一句話禮貌地播報；儲存失敗則以強制方式播報。審閱列是一個名為 "Unsaved changes" 的區域。
- 頁首的頭像是一個名為 "Workspace members" 的群組。
- 從不需要懸停：欄的底色帶與列的著色既跟隨指標，也跟隨鍵盤焦點。

## 動態效果

- **儲存格**：方框在 160 ms 內填滿，勾號在約 100 ms 內自己畫出。未儲存的圓點以快速彈簧從 0.3 彈出。指標按下時方框按壓到 0.88，並透過 CSS 彈簧回彈。群組核取方塊使其儲存格以 60 ms 間隔產生漣漪；Discard 與 Undo edits 在整個矩陣中每列間隔 30 ms 產生漣漪；儲存會一次清除所有圓點。
- **鎖定的儲存格**在 330 ms 內左右抖動（3、−2、1 px）。
- **欄的底色帶**：在懸停或取得焦點的角色上於 160 ms 內淡入，並在角色之間以平滑彈簧滑動。懸停的列在 120 ms 內著色，它的固定標籤欄也一樣。
- **群組**以平滑彈簧摺疊與展開，並帶有短暫的淡入淡出；箭頭以略帶彈性的彈簧旋轉。
- **副本**在淡入的同時，其欄以平滑彈簧從零寬度展開；被移除的角色會淡出並關閉。
- **審閱列**：它的空間在 240 ms 內展開，同時列從 24 px 下方以平滑彈簧升起並淡入。計數會滾動，"change" 與 "changes" 逐字母變形，第二行則以短暫的模糊切換。Save 按鈕在儲存時旋轉，然後隨著寬度的彈簧變化變形為 "Saved"。
- 減少動態與關閉動態時，所有變更立即生效，與 Arc 的減少模式一致。

## 相關

- `Team members`、`Invite people` 與 `Team directory`，即其他 People Block。
- `Settings page`，用於帶儲存列、由彼此獨立的開關組成的頁面。
- `Checkbox` 與 `Switch`，用於外觀相同的單一控制項。
- `DropdownMenu` 與 `AvatarGroup`，分別用於角色標頭與頁首。
