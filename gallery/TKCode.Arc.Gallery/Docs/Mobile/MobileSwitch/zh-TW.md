---
title: 移動端開關
summary: 51 乘 31 的開關，用於立即生效的設定，滑塊帶彈簧動態效果，點按區域至少 44 px。
source-hash: 4356e3855f22
---

<Demo name="Settings" />

## 何時使用

`TKCode.Arc.Mobile` 的 `Switch` 用來開啟或關閉一項設定，變更立即生效。它是 51 乘 31 的滑軌，帶 27 px 的滑塊，滑塊沿彈簧滑動，點按區域至少 44 乘 44。沒有懸停狀態。

- **設定清單**：每列一項設定，標籤在左，開關在末端。
- **立即生效的偏好**：通知、同步、深色外觀。

即時範例執行在手機框中，這是桌面上的模擬。Android 和 iOS 真機上的觸控表現尚未驗證。

## 何時不用

- **桌面設定頁**：使用 `TKCode.Arc` 的 `Switch`。
- **需要用按鈕確認的選擇**：使用核取方塊或表單。
- **一個動作**：使用 `MobileButton`；開關表示的是狀態。

## 用法

開關衍生自 Avalonia 的 `ToggleButton`，所以 `IsChecked`、`IsCheckedChanged` 和 `Content` 照常使用。它位於 XAML 命名空間 `https://github.com/AIDotNet/TKCode.Arc.Mobile`（前綴 `arcm`）；桌面套件裡也有 `Switch`，所以 API 表把它稱為 `MobileSwitch`。標籤放在你自己的列裡，開關放在列的末端。

```xml
<Grid xmlns="https://github.com/avaloniaui"
      xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
      ColumnDefinitions="*,Auto" MinHeight="52">
    <TextBlock Text="Notifications" VerticalAlignment="Center" />
    <arcm:Switch Grid.Column="1" IsChecked="True" />
</Grid>
```

選取時滑軌填滿為開啟色。停用的開關變暗到 0.5，並保持其狀態。

### Token

`SwitchToken` 保存幾何：`TrackWidth`（51）、`TrackHeight`（31）、`TrackInset`（2）、`ThumbSize`（27）、`ThumbOnMargin`、`TouchTarget`（44）和 `DisabledOpacity`（0.5）。顏色來自 ArcDesign 的選取控制項 token。

## API 參考

<!-- api: MobileSwitch -->

## 無障礙

它是切換按鈕：可以取得焦點並用鍵盤切換，並公開其狀態。把設定的名稱放在旁邊的標籤裡，並讓這一列至少 44 px 高。

## 動態效果

滑塊沿彈簧滑動，滑軌漸變到開啟色；兩者都在控制項主題中宣告，減少動態效果時立即切換。

## 相關元件

- `MobileButton`：用於動作。
- `MobilePage`：設定清單所在的螢幕。
