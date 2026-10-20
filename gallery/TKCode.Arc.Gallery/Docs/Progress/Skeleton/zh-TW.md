---
title: 骨架螢幕
summary: 內容仍在載入時預留空間。
source-hash: 4bb5402f1a7d
---

## 何時使用

- 個人檔案、留言或卡片的形狀已知，但資料仍在載入。
- 內容送達時應原地替換，版面不跳動。

## 何時不用

- 有可度量進度的工作：用 `Progress`。
- 已載入完成但沒有內容可顯示：用 `EmptyState`。
- AI 思考狀態或狀態列：用 `TextShimmer`。

## 用法

`Content` 為 null 時，骨架螢幕顯示預留位置。設定 `Lines` 指定文字行數，設定 `IsAvatarVisible` 可在文字前顯示圓形大頭貼預留位置。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Skeleton IsAvatarVisible="True" Lines="3" />
</UserControl>
```

### 留言預留位置

`Lines` 的範圍為 1 到 6。第一行較短、較高，最後一行的寬度為整體的 72%。

```xml
<arc:Skeleton Lines="4" />
```

### 已載入的內容

將 `Content` 設為實際的檢視，並把 `IsLoading` 繫結到載入狀態。`IsLoading` 變為 false 時，預留位置淡出，內容上移就位，高度平滑變化。

```xml
<arc:Skeleton IsAvatarVisible="True"
              Lines="2"
              IsLoading="{Binding IsLoadingProfile}">
    <StackPanel Spacing="8">
        <TextBlock Text="{Binding UserName}" />
        <TextBlock Text="{Binding Bio}" TextWrapping="Wrap" />
    </StackPanel>
</arc:Skeleton>
```

### 無障礙名稱

載入期間，`Label` 為預留位置命名。預設值為 "正在載入內容"。

```xml
<arc:Skeleton Label="正在載入留言" Lines="5" />
```

```csharp
skeleton.IsLoading = false; // 顯示 Content；預留位置隱藏
```

## API 參考

<!-- api: Skeleton -->

## 無障礙

- 預留位置顯示時，`Label` 是無障礙名稱。預設值為 "正在載入內容"。
- 預留位置是禮貌模式的即時區域，狀態朗讀為 "載入中"。
- 預留位置的形狀不會被朗讀。內容載入後，請為實際內容提供各自的標籤。
- 控制項無法取得焦點，也沒有鍵盤互動。

## 動態效果

- 文字行與大頭貼從上到下以平緩的波浪脈動，波浪持續約 20 秒後停止。
- 載入結束時，預留位置淡出，內容上移 4 px 就位，高度以彈簧過渡到內容高度。
- 減少動態效果時，脈動停止，預留位置以短暫淡入切換為內容，高度不做動畫。

## 相關元件

- `Progress`：工作量已知並可回報進度時使用。
- `EmptyState`：載入完成但沒有內容可顯示時使用。
- `TextShimmer`：用於行內狀態或 AI 思考狀態。
