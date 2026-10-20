---
title: 空狀態
summary: 尚無內容時，提供清楚的下一步操作。
source-hash: 3cddac5cfe97
---

## 何時使用

- 空清單、空資料夾或首次使用的畫面，且下一步很明確。
- 搜尋或篩選沒有結果時，提供修正的操作。
- 原地變化的狀態，例如「尚無專案」變為「專案已建立」。

## 何時不用

- 資料仍在載入：用 `Skeleton`。
- 已有內容的頁面出現錯誤：用 `Alert`。
- 卡片形式的內容或操作群組：用 `Card`。

## 用法

`Title` 與 `Description` 為必填。需要行動呼籲時加上 `Action`。預設圖示為資料夾。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:EmptyState Label="專案"
                    Title="尚無專案"
                    Description="專案會將你的檔案、成員與對話集中保存在同一處。">
        <arc:EmptyState.Action>
            <arc:Button Content="新增專案" Variant="Primary" />
        </arc:EmptyState.Action>
    </arc:EmptyState>
</UserControl>
```

### 圖示

`Icon` 可接受一個控制項，或一份 24 × 24 的 Lucide 幾何圖形，例如 `ArcIcons` 中的某個圖示。圖示變化時，舊圖示淡出，新圖示彈入。

```xml
<arc:EmptyState Title="沒有結果"
                Description="檢查拼字，或嘗試更廣泛的搜尋詞。"
                Icon="{Binding SearchIcon}" />
```

```csharp
empty.Icon = ArcIcons.Search;
```

### 操作列

`Action` 可接受一個按鈕，也可接受包含多個按鈕的面板。該列在描述下方置中排列。

```xml
<arc:EmptyState Title="沒有與「季度計畫」相關的結果"
                Description="檢查拼字，或嘗試更廣泛的搜尋詞。"
                Icon="{Binding SearchIcon}">
    <arc:EmptyState.Action>
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:Button Content="清除搜尋" Variant="Secondary" />
            <arc:Button Content="瀏覽全部" Variant="Ghost" />
        </StackPanel>
    </arc:EmptyState.Action>
</arc:EmptyState>
```

### 狀態切換

保持控制項掛載，只修改其屬性即可。標題與描述會滾動切換為新文案，高度以彈簧方式調整以符合新內容。將 `Action` 設為 `null` 可移除按鈕。

```csharp
empty.Icon = ArcIcons.Check;
empty.Title = "專案已建立";
empty.Description = "準備好後，邀請團隊成員或開始新增檔案吧。";
empty.Action = null;
```

## API 參考

<!-- api: EmptyState -->

## 無障礙

- 此控制項是由 `Label` 命名的區域地標。頁面上每個空狀態都應設定 `Label`。
- 標題為三級標題。描述緊接其後，位於閱讀順序中。
- 圖示不向輔助技術公開，含義由文字承載。
- `Action` 中的按鈕保留各自的焦點與鍵盤行為。

## 動態效果

- 新標題或描述帶著柔和的模糊從下方升起，舊文字向上淡出。
- 文字區域以平滑彈簧改變高度，以符合新文案。
- 新的 `Icon` 彈入；圖示區域首次出現時淡入並縮放。
- 減少動態效果時，文字直接淡入切換，高度即時變化；圖示區域不做進場動畫。關閉動態效果時所有變化立即生效。

## 相關元件

- `Skeleton`：內容仍在載入時使用。
- `Alert`：已顯示內容的頁面中出現錯誤或提示時使用。
- `Card`：需要包含媒體、擁有者與操作的網格項目時使用。
