---
title: 移動端搜尋列
summary: 帶搜尋圖示的膠囊形文字方塊，有清除按鈕和會觸發 Submitted 的搜尋鍵。
source-hash: d86e958fc965
---

<Demo name="Basic" />

## 何時使用

`Searchbar` 是使用者輸入查詢詞的文字方塊：在清單頂部，或在搜尋頁面上。

- **篩選或搜尋清單**：放在頁面頂部、導覽列下面。
- **搜尋頁面**：它是頁面上的第一個元素，頁面開啟時取得焦點。

即時範例在手機外框裡執行，那是桌面上的模擬；真機上軟鍵盤的搜尋鍵這裡沒有驗證。

## 何時不用

- **普通的文字輸入**：用 `TextField`。
- **帶建議的桌面搜尋框**：用桌面的搜尋控制項。

## 用法

`Searchbar` 是一個 `TextField`，所以 `Text`、`PlaceholderText` 和繫結都和那裡一樣。處理 `Submitted` 來執行搜尋：使用者按 Enter 或軟鍵盤的搜尋鍵時觸發。

```xml
<arcm:Searchbar xmlns="https://github.com/avaloniaui"
                xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                PlaceholderText="搜尋訊息"
                Text="{Binding Query}" />
```

```csharp
searchbar.Submitted += (_, _) => Search(searchbar.Text);
```

搜尋圖示是文字方塊的 `Icon`，預設已設定；清除按鈕在文字方塊有焦點且有文字時顯示。要在輸入時就篩選，請繫結 `Text` 並回應它的變化。

### Token

`Searchbar` 與 `TextField` 共用 `TextFieldToken`：同樣的 44 px 高度、內距和圖示大小。只有圓角不同：用 ArcDesign token 的膠囊圓角。

## API 參考

<!-- api: MobileSearchbar -->

## 無障礙

給文字方塊一個說明搜尋對象的預留位置提示，例如「搜尋訊息」。文字方塊至少 44 px 高。

## 動效

文字方塊取得或失去焦點時，邊框按快速曲線變色。清除按鈕從 0.6 縮放加淡入彈出（P26，透過 `PresenceHost`），離開比出現更快。減弱動效時，邊框立即變色，清除按鈕也立即出現和消失。

## 相關元件

- `MobileTextField`：它所基於的文字方塊。
- `MobileNavbar`：搜尋列通常放在它下面。
- `MobileListGroup`：常用的結果清單。
