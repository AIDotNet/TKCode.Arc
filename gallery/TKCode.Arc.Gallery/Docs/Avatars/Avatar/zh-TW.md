---
title: 大頭貼
summary: 用於人員與帳號的精簡身分標示。
source-hash: c09244e66188
---

<Demo name="Initials" />

## 何時使用

- 名稱、留言或紀錄旁的單一人員，由照片或首字母辨識。
- 頁面標頭或清單中的在線狀態，搭配 `Status` 圓點使用。
- 照片可能缺少或載入失敗的情況：由首字母取代顯示。

## 何時不用

- 小空間內顯示多人並帶有溢出數量：用 `AvatarGroup`。
- 點擊後開啟帳號選單的大頭貼：用 `UserMenu`。
- 包含角色的完整人員清單：用 `SortableDataTable`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Avatar DisplayName="Ada Lovelace" />
</UserControl>
```

### 照片與備援

`Source` 用於載入照片。載入期間會顯示首字母；若載入失敗，則保留首字母。

<Demo name="Photos" />

### 尺寸

`Size` 決定直徑：`Small` 為 28，`Medium`（預設）為 36，`Large` 為 48，`XLarge` 為 88。

```xml
<arc:Avatar DisplayName="Ada Lovelace" Size="Small" />
<arc:Avatar DisplayName="Grace Hopper" Size="Medium" />
<arc:Avatar DisplayName="Alan Turing" Size="Large" />
<arc:Avatar DisplayName="Katherine Johnson" Size="XLarge" />
```

### 狀態

`Status` 會在右下角顯示在線或離線圓點。`AvatarStatus.None` 則隱藏該圓點。

<Demo name="Status" />

### 首字母

首字母取自 `DisplayName` 前兩個單字的第一個字母，並轉為大寫："Ada Lovelace" 會得到 "AL"。`Initials` 會傳回相同的值，`Avatar.ComputeInitials` 則可為任何名稱計算首字母。

## API 參考

<!-- api: Avatar -->

## 無障礙

- 大頭貼是一個以 `DisplayName` 為名稱的影像。設定狀態後，名稱中會加入狀態，例如 "Maya Chen，在線"。
- 照片和首字母僅作裝飾，對輔助技術隱藏，因此名稱只會被朗讀一次。
- 狀態圓點已隱藏；狀態透過名稱傳遞給輔助技術。
- 大頭貼無法取得焦點，也沒有鍵盤操作。

## 動態效果

- 仍在載入的照片會從柔和的模糊中淡入。已快取的照片會立即顯示。
- 狀態圓點會以俐落的彈簧動畫從小尺寸彈出，離開時縮小消失。
- 減少動態效果時，狀態圓點立即切換，照片不經淡入直接顯示。關閉動態效果時，所有變化立即完成。

## 相關元件

- `AvatarGroup`：用於帶溢出數量的人員堆疊。
- `UserMenu`：大頭貼需要開啟帳號操作時使用。
- `Badge`：用於非人員的狀態或類別標籤。
