---
title: 展開式按鈕群組
summary: 緊湊排列的圖示按鈕群組：指向或聚焦的按鈕會展開顯示文字，相鄰按鈕讓位，動作會在原地確認。
source-hash: e8d67520ebca
---

## 何時使用

- 訊息、檔案或卡片上的三到六個相關動作，例如回覆、封存和刪除。
- 緊湊的圖示動作，每個動作只在取得焦點時才需要顯示文字。
- 需要在原地以過去式文字確認的動作，例如從封存變為已封存。

## 何時不用

- 需要保持選取狀態的選擇：用 Toggles 分類中的 `SegmentedControl`。
- 一兩個動作且空間充足：用附文字的 `Button`。
- 超過約六個動作：用 Menus 分類中的 `DropdownMenu`。
- 需要始終顯示文字的圖示動作：用 `FloatingButtonGroup` 或 `ButtonGroup`。

## 用法

每個動作都是一個 `ExpandingButtonGroupItem`。群組需要一個 `Label`，它就是該群組的無障礙名稱。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ExpandingButtonGroup Label="訊息動作">
        <arc:ExpandingButtonGroupItem Label="回覆" />
        <arc:ExpandingButtonGroupItem Label="傳送" DoneLabel="已傳送" />
        <arc:ExpandingButtonGroupItem Label="封存" DoneLabel="已封存" />
    </arc:ExpandingButtonGroup>
</UserControl>
```

### 圖示與靜止時的動作

在程式碼中把 `Icon` 設為 16 × 16 的 Lucide 圖形。靜止時群組只顯示一個文字標籤，預設是第一個可用的動作。`DefaultExpandedItem` 可以指定另一個動作。

```csharp
reply.Icon = ArcIcons.Reply;
archive.Icon = ArcIcons.Archive;

var comment = new ExpandingButtonGroupItem { Label = "留言", Icon = ArcIcons.MessageCircle };
var card = new ExpandingButtonGroup
{
    Label = "卡片動作",
    DefaultExpandedItem = comment
};
```

### 確認

`DoneLabel` 是動作完成後顯示的過去式文字；開啟內容的動作請不要設定它。確認狀態會保留 `ConfirmDuration` 指定的時長，預設為 1.4 秒。

```xml
<arc:ExpandingButtonGroupItem Label="儲存" DoneLabel="已儲存" />
```

### 非同步動作

`AsyncAction` 會傳回 `Task<bool>`。工作結束前該項目處於忙碌狀態。傳回 `false` 或拋出例外都會略過確認。沒有非同步動作時，`Click` 和 `Command` 視為成功。

```csharp
var archive = new ExpandingButtonGroupItem
{
    Label = "封存",
    Icon = ArcIcons.Archive,
    DoneLabel = "已封存",
    AsyncAction = async () =>
    {
        await archiveService.ArchiveAsync();
        return true;
    }
};
```

### 語氣與停用項目

`Tone="Danger"` 會以危險色顯示展開的破壞性動作。停用的項目仍可取得焦點並顯示文字，但不會執行。

```xml
<arc:ExpandingButtonGroup Label="檔案動作" Size="Small">
    <arc:ExpandingButtonGroupItem Label="下載" DoneLabel="已下載" />
    <arc:ExpandingButtonGroupItem Label="鎖定" IsDisabled="True" />
    <arc:ExpandingButtonGroupItem Label="刪除" Tone="Danger" DoneLabel="已刪除" />
</arc:ExpandingButtonGroup>
```

### 事件

`ActionInvoked` 先於該項目自身的 `AsyncAction` 執行。`e.Item` 是即將執行的動作。

```csharp
message.ActionInvoked += (_, e) => log.Text = $"動作：{e.Item.Label}";
```

## API 參考

<!-- api: ExpandingButtonGroup -->

<!-- api: ExpandingButtonGroupItem -->

## 無障礙

- 群組的角色為 ToolBar，名稱由 `Label` 提供。即使標籤處於收合狀態，完整的 `Label` 仍是名稱。
- 整個群組只佔一個 Tab 停駐點。左右方向鍵在動作之間移動並循環。Home 和 End 分別移到第一個與最後一個動作。
- Enter 與 Space 執行目前聚焦的動作。動作處於忙碌狀態時，重複觸發會被忽略。
- 停用的動作仍可取得鍵盤焦點，並回報為無法使用。
- 確認文字會以禮貌方式朗讀，重複確認也會再次朗讀。

## 動態效果

- 聚焦或懸停的動作以彈簧展開，顯示其文字，相鄰動作隨之讓位。
- 文字從圖示後方滑出，伴隨短暫淡入與輕微模糊；收合時收回得更快。
- 確認時圖示繪出勾號，文字切換為完成詞：新詞升起，舊詞移出。
- 指標需要進入相鄰動作幾個像素後，文字才會移動，因此指標靜止時整個群組不會跳動。
- 減少動態效果時，文字與寬度以快速淡入同時改變，勾號直接出現而不繪製。關閉動態效果時以相同的變化直接完成，沒有任何過場。

## 相關元件

- `FloatingButtonGroup`：獨立的柔和按鈕，文字始終顯示在托盤中。
- `ButtonGroup`：把相關動作拼接在同一個表面上，以分隔線區分。
- `ActionButton`：單次非同步提交，按鈕上顯示等待與成功狀態。
- `DropdownMenu`：較長的指令清單。
