---
title: 表情回應
summary: 一鍵回應一則內容，計數會滾動變化。
source-hash: 10dc2dbaae0e
---

<Demo name="Default" />

## 何時使用

- 像聊天中的表情回應一樣，一鍵回應貼文、留言或版本說明。
- 顯示每個選項有多少人回應，以及目前使用者是否在其中。
- 一組簡短且固定的選項，例如讚、愛心與慶祝。

## 何時不用

- 需要文字回覆的內容：應為貼文提供回覆欄位。
- 檢視者無法更改的計數：用 `Badge`。
- 從清單中選擇一個值：用選取器或選單。

## 用法

選項是 `Reactions` 的子元素。計數與檢視者自己的回應是它的 `Value`，在程式碼中設定。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Reactions Label="表情回應">
        <arc:ReactionOption Id="like" Label="讚" />
        <arc:ReactionOption Id="love" Label="愛心" />
        <arc:ReactionOption Id="celebrate" Label="慶祝" />
    </arc:Reactions>
</UserControl>
```

```csharp
var reactions = new Reactions
{
    Value = [new Reaction("like", 3)]
};
reactions.Options.Add(new ReactionOption("like", "讚", ArcIcons.ThumbsUp));
reactions.Options.Add(new ReactionOption("love", "愛心", ArcIcons.Heart));
reactions.Options.Add(new ReactionOption("celebrate", "慶祝", ArcIcons.Smile));
```

`ReactionOption` 透過 `Icon` 接收 24 x 24 的 Lucide 圖形資料。

### 值與變更

`Value` 為雙向繫結。計數為 0 的膠囊會從列中移除。`Reacted` 標記檢視者自己的回應，`ReactedBy` 列出提示框中顯示的名稱。

<Demo name="WhoReacted" />

`ValueChanged` 在值變更後觸發。`Next` 為新值，`Change` 說明變更的選項以及檢視者是否做出了回應。

```csharp
reactions.ValueChanged += (_, e) =>
{
    // 將 e.Next 隨貼文一併儲存。
};
```

### 標籤

`Label` 為整個列命名。`AddLabel` 與 `PickerLabel` 分別為新增按鈕與選取器命名。`TipDelay` 設定指標在膠囊上停留多久後才顯示提示。

```xml
<arc:Reactions Label="對本次版本的回應"
               AddLabel="新增回應"
               TipDelay="0:0:0.6">
    <arc:ReactionOption Id="ship" Label="已發佈" />
    <arc:ReactionOption Id="star" Label="星號" />
</arc:Reactions>
```

### 換行與寬度

膠囊放不下時，整個列會換行。設定 `Width` 即可看到換行效果。

<Demo name="Wrapping" />

### 即時計數

在別處變動的計數只會滾動發生變化的位數：增加時向上滾動，減少時向下滾動。

<Demo name="LiveCounts" />

## API 參考

<!-- api: Reactions -->

<!-- api: ReactionOption -->

<!-- api: Reaction -->

## 無障礙

- 整個列是一個由 `Label` 命名的群組。每個膠囊都是切換按鈕，其名稱包含標籤、計數以及檢視者是否在其中。
- 選取器是一個使用漫遊焦點的工具列。四個方向鍵在選項之間移動並循環，Home 與 End 跳到兩端，每個選項都有名稱。
- 在膠囊上按 Enter 或 Space 可切換檢視者的回應。在新增按鈕上按 Enter 或 Space 會開啟選取器，按 Escape 關閉並將焦點返回新增按鈕。
- 每次變更都會透過禮貌等級的狀態區域播報一次，例如「已新增愛心，共 4 個回應。」
- 當焦點所在的膠囊離開列時，焦點會移到相鄰膠囊或新增按鈕。

## 動態效果

- 新膠囊從半尺寸帶著柔和模糊放大出現。被移除的膠囊會縮小並淡出。其他膠囊平滑滑動到新位置。
- 檢視者回應時圖示會輕微跳動；快速連續點擊會累加，而不是重新開始。
- 計數只滾動發生變化的數字：增加時向上，減少時向下。
- 選取器從新增按鈕中展開，各選項依序出現。
- 減少動態效果時只使用短暫淡入淡出，不使用尺寸彈簧。關閉動態效果時所有變化立即完成。

## 相關元件

- `Badge` 用於檢視者無法更改的固定計數。
- `Popover` 與 `DropdownMenu` 用於由按鈕開啟的其他內容或選項。
- `SwipeActions` 用於觸控清單中的列操作。
