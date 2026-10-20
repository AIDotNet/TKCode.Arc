---
title: 長按確認
summary: 透過長按而不是點按來確認破壞性操作。
source-hash: 2474b37fba46
---

## 何時使用

- 具有破壞性或難以復原的操作，且其後果已在畫面上可見，例如刪除專案。
- 精簡的設定列，彈出確認對話方塊會顯得過於笨重。
- 觸控裝置上，誤觸不應執行操作的場合。

## 何時不用

- 安全或容易復原的小操作：用 `Button` 或 `ActionButton`。
- 需要使用者先閱讀後果的操作：用 `Dialog`。
- 在觸控清單中刪除項目：用 `SwipeActions`，它透過滑動顯示操作。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:HoldToConfirm Label="長按刪除專案"
                       ConfirmedLabel="已刪除"
                       Command="{Binding DeleteProject}" />
</UserControl>
```

`Command` 在長按完成後只執行一次。一般點按或自動化呼叫都不會觸發確認。

### 色調與長按時長

`Tone` 設定填色：`Danger` 用於不可逆的操作，`Accent`（預設）和 `Neutral` 用於其他情況。`HoldDuration` 設定需要按住的時長，預設為 1.2 秒。

```xml
<arc:HoldToConfirm Label="長按刪除"
                   Tone="Danger"
                   ConfirmedLabel="已刪除" />
<arc:HoldToConfirm Label="長按撤銷"
                   Tone="Neutral"
                   HoldDuration="0:0:2"
                   ConfirmedLabel="已撤銷" />
```

### 圖示

`Icon` 是 24 × 24 的 Lucide 圖形，顯示在標籤之前。長按完成後，圖示會變為繪製出的勾號。

```csharp
var archive = new HoldToConfirm
{
    Label = "長按封存",
    Tone = HoldToConfirmTone.Neutral,
    Icon = ArcIcons.Archive,
    ConfirmedLabel = "已封存"
};
```

### 按住時的提示

`HoldingChanged` 在長按開始和結束時觸發。`IsHolding` 表示目前狀態。可以搭配它們在按鈕旁顯示提示。

```csharp
project.HoldingChanged += (_, e) =>
{
    hint.Text = e.IsHolding
        ? "請繼續按住…"
        : project.IsConfirmed ? "已刪除。" : "提前放開會讓填色回退。";
};
```

提前放開時，填色會以彈簧動畫回退，且不會確認任何操作。

### 完成狀態

`IsConfirmed` 為雙向繫結。長按完成時它變為 `true`。將其設回 `false` 即可重設按鈕；標籤變回原樣時，填色會隨之回退。

```xml
<arc:HoldToConfirm Label="長按刪除專案"
                   IsConfirmed="{Binding IsDeleted}" />
```

```csharp
reset.Click += (_, _) => project.IsConfirmed = false;
```

## API 參考

<!-- api: HoldToConfirm -->

## 無障礙

- `Label` 是按鈕的名稱。長按完成後，名稱改用 `ConfirmedLabel`。
- 說明文字會告訴使用者如何確認：按住指定時長，或使用鍵盤按住 Space 或 Enter。
- 完成狀態會透過禮貌的狀態區域播報一次，並告知按鈕已無法使用，但焦點仍保留在按鈕上。
- 用 Space 或 Enter 長按時走鍵盤路徑；按鍵的自動重複不會重新開始長按。焦點移開會取消長按，指標移離按鈕超過 24 px 也會取消。

## 動態效果

- 按住期間，填色以固定速度從按鈕一端推進到另一端，所用時間即剩餘時長。提前放開時，填色以不帶過衝的平滑彈簧回退。
- 長按完成時，標籤與圖示以短暫的模糊和滑動切換，圖示同時繪出勾號。
- 減少動態效果時，移除按壓縮放與回退彈簧。標籤和圖示改為淡入淡出，但填色仍跟隨長按進度，因為它用於顯示進度。
- 關閉動態效果時，只保留填色進度，其他內容一次切換。

## 相關元件

- `Button` 與 `ActionButton`：不需要長按的操作。
- `Dialog`：使用者需要先閱讀後果再確認的場合。
- `SwipeActions`：刪除觸控清單中的某一列。
