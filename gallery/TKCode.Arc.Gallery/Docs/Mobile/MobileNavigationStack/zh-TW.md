---
title: 移動端導覽堆疊
summary: 頁面堆疊：推入的頁面從右側滑入蓋住下面的頁面，從左邊緣滑動或點返回鍵即可滑走。
source-hash: 6309420b3338
---

<Demo name="Basic" />

## 何時使用

`NavigationStack` 是移動應用向更深一層的方式：從清單到詳情，從設定到子頁面，再返回。

- **逐層深入**：每點一列就推入一個頁面來說明它。
- **只有一條返回路徑的流程**，例如表單的幾個步驟。
- **滑動返回**：使用者習慣從左邊緣把頁面拉回來，頁面會跟隨手指。

即時範例在手機外框裡執行，那是桌面上的模擬；滑動和滑入的手感在真機上這裡沒有驗證。

## 何時不用

- **在頂層區域之間切換**：用 `Tabbar`；每個區域可以有自己的堆疊。
- **蓋在目前螢幕上的簡短任務**：用 `Sheet`，它不會離開目前頁面。
- **並排的兩個窗格**：堆疊一次只顯示一個頁面。

## 用法

把第一個頁面放進堆疊裡，之後用程式碼 `Push` 頁面。頁面可以是任何控制項，通常是帶 `Navbar` 的 `Page`。把導覽列的 `BackCommand` 繫結到堆疊的 `BackCommand`：它執行 `Pop`，在第一頁時不可用。

```xml
<arcm:NavigationStack xmlns="https://github.com/avaloniaui"
                      xmlns:arcm="https://github.com/AIDotNet/TKCode.Arc.Mobile"
                      Name="Stack">
    <arcm:Page>
        <arcm:Page.Header>
            <arcm:Navbar Title="收件匣" />
        </arcm:Page.Header>
        <!-- 第一個頁面 -->
    </arcm:Page>
</arcm:NavigationStack>
```

```csharp
stack.Push(new Page
{
    Header = new Navbar { Title = "Ada", ShowBack = true, BackText = "收件匣", BackCommand = stack.BackCommand },
    Content = detail,
});
```

推入時新頁面從右側滑入，下面的頁面向左移動三分之一的寬度並變暗。`Pop` 反過來，`PopToRoot` 回到第一個頁面，只有頂部的頁面滑走。在另一次滑動進行中又收到推入或彈出時，會立即完成前一次。

堆疊靜止時，只有頂部的頁面在視覺樹裡。下面的頁面保持狀態（例如捲動位置），回來時原樣不變。

### 每個標籤一個堆疊

標籤列下面通常是每個標籤各自的堆疊。為每個標籤保留一個 `NavigationStack`，選擇變化時替換頁面的 `Content`：每個標籤都會保留它推入過的頁面，標籤列在每個頁面下面一直可見。

<Demo name="TabsWithStacks" />

### 滑動返回

從左邊緣 24 px 內開始的水平拖曳會拉走頂部頁面，一比一跟隨手指，下面的頁面一邊移動一邊變亮。放開時，堆疊按手指的速度向前看 0.2 秒：過了中點頁面就離開，向右的快速滑動超過 500 px/s 時無論從哪裡都會彈出；否則頁面彈回。對不能被滑動離開的頁面，把 `IsSwipeBackEnabled` 設為 `False`。

`Navigated` 事件在堆疊變化時立即回報每一次推入和彈出，此時滑動可能還在進行。

### Token

`NavigationStackToken` 保存 `DimColor`：頂部頁面還沒移動時蓋在下面頁面上的暗層，明暗模式不同。視差（寬度的 0.3）和邊緣寬度（24）是控制項的常數 `ParallaxFraction` 和 `EdgeWidth`。

## API 參考

<!-- api: MobileNavigationStack -->

## 無障礙

讓導覽列裡的返回按鈕保持可見，名稱說明它回到哪裡，這樣滑動不會是唯一的返回方式。只有頂部頁面在樹裡，所以讀屏器和鍵盤不會碰到藏在下面的內容。

## 動效

滑動是程式碼驅動的運動值：拖曳時每次移動都會寫入它，放開時手指的速度交給平滑彈簧，這是主題裡宣告的過渡做不到的。下面頁面的暗層和位移跟隨頂部頁面的位置。減弱動效時，頁面直接切換。

## 相關元件

- `MobileNavbar`：它的 `BackCommand` 繫結到堆疊的 `BackCommand`。
- `MobilePage`：常用的頁面。
- `MobileSheet`：適合留在目前頁面上的簡短任務。
