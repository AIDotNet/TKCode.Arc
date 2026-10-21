---
title: 卡片
summary: 承載一組相關內容與操作的封閉式容器。
source-hash: 0d186e88d311
---

<Demo name="QuickLook" />

## 何時使用

- 可瀏覽的網格項目，例如帶有媒體、擁有者與狀態的專案、房源或貼文。
- 應作為一個整體呈現的相關內容與底部操作。
- 需要在不離開頁面的情況下展開為更大快速預覽的卡片（設定 `Details`）。

## 何時不用

- 需要上下文說明的單一數字：用 `MetricCard`。
- 原地展開、無需對話方塊的卡片：用 `ExpandableCard`。
- 沒有可展開卡片、需要模態視窗的內容：用 `Dialog`。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:Card Title="港口改版"
              Description="全新的預訂流程與房間頁面。"
              Width="280" />
</UserControl>
```

### 媒體、擁有者與底部操作

`Media` 顯示在頂部，懸停時會輕微漂入。`Avatar` 與 `Meta` 組成作者資訊列，`Action` 是位於末端的底部操作控制項，可單獨點擊。

```xml
<arc:Card Title="港口改版"
          Description="全新的預訂流程與房間頁面。"
          Meta="Maya Chen"
          Status="2 小時前更新"
          Width="280">
    <arc:Card.Media>
        <Border Height="132" Background="#5B8DEF" />
    </arc:Card.Media>
    <arc:Card.Avatar>
        <arc:Avatar DisplayName="Maya Chen" Size="Small" />
    </arc:Card.Avatar>
    <arc:Card.Action>
        <arc:Button Content="分享" Variant="Ghost" Size="Small" />
    </arc:Card.Action>
</arc:Card>
```

### 狀態

`Status` 是一行簡短文字，例如「2 小時前更新」。文字變化時，新詞會依序升起出現。

<Demo name="StatusUpdates" />

```xml
<arc:Card Title="每週摘要"
          Description="每週五彙整各團隊的重點內容。"
          Status="{Binding LastUpdate}"
          Width="300" />
```

### 快速預覽

設定 `Details` 後，標題會變為按鈕；整張卡片隨即在同一頁面中展開為更大的檢視。

<Demo name="Controlled" />

```xml
<arc:Card Title="季度計畫"
          Description="未來三個月的目標、負責人與日期。"
          IsDetailsOpen="{Binding IsPlanOpen, Mode=TwoWay}"
          Width="280">
    <arc:Card.Details>
        <TextBlock Text="由程式碼開啟：IsDetailsOpen 為雙向繫結。" TextWrapping="Wrap" />
    </arc:Card.Details>
</arc:Card>
```

使用 `OpenDetails()` 與 `CloseDetails()` 從程式碼開啟和關閉快速預覽，使用 `DetailsOpenChanged` 得知關閉的原因。

```csharp
card.DetailsOpenChanged += (_, e) => log.Text = e.IsOpen ? "開啟" : $"已關閉（{e.Reason}）";
card.OpenDetails();
card.CloseDetails();
```

### 無快速預覽

不設定 `Details` 時，標題只是一般標題，卡片僅在懸停時抬起。

<Demo name="WithoutQuickLook" />

## API 參考

<!-- api: Card -->

## 無障礙

- 標題為三級標題。設定 `Details` 後，標題變為按鈕；按 Enter 或空白鍵即可開啟快速預覽。
- 按 Esc 關閉快速預覽，並將焦點送回標題。快速預覽開啟時，Tab 鍵焦點會保持在其內部。
- 底部操作保留各自的焦點。僅含圖示的操作請用 `AutomationProperties.Name` 設定名稱，`Media` 中的圖片請設定替代文字。
- `Status` 的變化會以禮貌方式朗讀。`CloseButtonName` 用於設定快速預覽關閉按鈕的名稱。

## 動態效果

- 懸停時卡片抬起 2px，邊框與陰影加深。媒體緩慢放大至 1.04（僅限滑鼠）。
- 設定 `Details` 後，表面、媒體、標題與作者資訊列會透過同一彈簧動畫飛入快速預覽，同時頁面在模糊中變暗。關閉時它們飛回卡片。
- 新的 `Status` 會把舊行推走，同時新詞升起出現。
- 減少動態效果時取消抬起、放大與變形，快速預覽僅淡入淡出。關閉動態效果時變化立即生效。

## 相關元件

- `MetricCard`：單一數字及其上下文與變化。
- `ExpandableCard`：原地展開、無需對話方塊的內容。
- `Dialog`：沒有卡片可展開的模態內容。
- `EmptyState`：尚無卡片的網格或清單。
