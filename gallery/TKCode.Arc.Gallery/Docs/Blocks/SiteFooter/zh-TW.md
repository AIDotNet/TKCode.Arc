---
title: Site footer
summary: 網站頁尾，包含連結欄與電子報、極簡版型，以及一個大型、會淡出的 Arc 標記。
source-hash: 60817fc4175b
---

<Demo name="Preview" />

## 何時使用

`SiteFooter` 為行銷網站或說明文件網站收尾。它放置讀者在頁面底部會尋找的連結（產品、資源、公司）、法律聲明、系統狀態與電子郵件訂閱，共有三種版型。

- **附電子報的產品網站**：Columns 版型把品牌、標語與發行說明訂閱放在三個連結欄旁邊。
- **文件網站或應用程式的說明頁**：Minimal 版型是一列安靜的連結，加上一行版權、狀態與社群連結。
- **以品牌收尾的著陸頁**：Logo 版型不放電子報，並以一個大型拱形收尾；拱形會淡入頁面，並在捲動到可見範圍時升起。

## 何時不用

- **密集的應用程式畫面**：應用程式內的狀態列或側邊欄頁尾較小；此區塊是頁面層級的頁尾。
- **訂閱本身就是頁面重點**：請使用 Newsletter signup 區塊，它會把整個區段交給表單。
- **結尾的行動呼籲**：請在頁尾上方使用 CTA section 區塊，並讓頁尾只負責導覽。

## 用法

未設定任何內容時，頁尾會顯示 Arc 的範例：Arc 品牌、三個欄位、法律與社群連結，以及狀態為「All systems normal」。

```xml
<arc:SiteFooter />
```

所有內容都是資料：設定品牌名稱、提供你自己的欄位與連結，並處理使用者按下的動作。

<Demo name="Custom" />

```xml
<arc:SiteFooter BrandName="Northwind"
                BrandHref="https://northwind.example"
                Tagline="Freight tracking for small fleets."
                IsNewsletterVisible="False"
                StatusLabel="Delays in EU routing"
                StatusTone="Warning">
    <arc:SiteFooterColumn Title="Platform">
        <arc:SiteFooterLink Label="Tracking" Href="https://northwind.example/tracking" />
        <arc:SiteFooterLink Label="API" Href="https://developers.northwind.example" IsExternal="True" />
    </arc:SiteFooterColumn>
</arc:SiteFooter>
```

### 版型

`Variant` 可選擇 `Columns`、`Minimal` 或 `Logo`。Columns 與 Logo 共用簡介、三個連結欄與法律列；只有 Columns 會顯示電子報，只有 Logo 會顯示拱形。Minimal 會先顯示品牌與一列連結，然後是版權與標語，再加上狀態與社群連結。它的連結為 `Links`；當它為 null 時，會使用每一欄的第一個連結，以及前兩個法律連結。

### 連結

`SiteFooterLink` 有 `Label`、選用的 `Href`，以及 `IsExternal`（向外的箭頭）。按下連結、品牌或狀態時，會引發 `Navigate`，並帶出該連結與它所在的位置（`Brand`、`Column`、`Legal`、`Social`、`Status`），同時執行 `NavigateCommand`。若事件未標示為已處理，且 `Href` 是絕對的 http、https 或 mailto 位址，頁尾會以平台的啟動器開啟它。沒有 `Href` 的連結只會引發事件。

`Columns` 保存連結欄（或繫結 `ColumnsSource`，它優先，且會被監看）。`LegalLinks` 與 `SocialLinks` 會取代範例連結，位置分別在版權旁與法律列的末端。

### 電子報

<Demo name="Subscribe" />

訂閱表單會就地驗證。欄位為空時顯示 "Enter your email address"，格式錯誤時顯示 "That email doesn't look right"。欄位有文字且離開該欄位時才開始驗證，之後每次按鍵都會重新驗證。送出時會開始載入（按鈕旋轉，欄位變為唯讀），等待 `SubscribeAsync`，然後進入完成狀態：按鈕顯示 "Subscribed" 並附勾號，其下的文字顯示 "Check {address} to confirm"。`SubscribeAsync` 拋出例外會回到閒置狀態，並顯示失敗訊息。成功時會引發 `Subscribed`。

```csharp
footer.SubscribeAsync = async (email, cancellationToken) =>
{
    await Task.Delay(500, cancellationToken); // call your list here; throw to show the failure message
};
footer.Subscribed += (_, e) => Console.WriteLine($"Subscribed {e.Email}");
```

表單中的每段文字都是一個屬性（`NewsletterTitle`、`NewsletterDescription`、`NewsletterPlaceholder`、`NewsletterSubscribeLabel`、`NewsletterSubscribedLabel`、`NewsletterEmptyEmailMessage`、`NewsletterInvalidEmailMessage`、`NewsletterFailedMessage`、`NewsletterConfirmFormat`），預設值為 Arc 的英文。

### 狀態

`StatusLabel`、`StatusTone`（`Success`、`Warning`、`Danger`）與 `StatusHref` 用來描述狀態連結；設定 `IsStatusVisible="False"` 即可隱藏它。

### 響應式行為

<Demo name="Narrow" />

斷點依照頁尾自身的寬度，而不是視窗的寬度。寬度為 760 或以下時，簡介會位於連結欄上方，間距為 40 px。寬度為 520 或以下時，連結欄改為兩欄並排，間距為 32 / 16；電子報會取消 400 px 的上限；法律列則改為靠左對齊的單欄。

## API 參考

<!-- api: SiteFooter -->

<!-- api: SiteFooterLink -->

<!-- api: SiteFooterColumn -->

<!-- api: SiteFooterNavigateEventArgs -->

<!-- api: SiteFooterSubscribedEventArgs -->

## 無障礙

- 頁尾是 content-info 地標。連結欄是一個以 `NavigationLabel` 命名的導覽；法律與社群連結則是分別以 `LegalLabel` 與 `SocialLabel` 命名的清單。
- 有位址的連結會以連結形式公開；沒有位址的則是按鈕。
- 電子郵件欄位以 `NewsletterEmailLabel` 命名。提示、確認與失敗會以禮貌方式朗讀，錯誤則會以警示方式朗讀。
- 標記、箭頭與狀態點都是裝飾性的。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 在品牌、電子報、連結與狀態之間移動 |
| `Enter` | 在電子郵件欄位中：訂閱。在連結或按鈕上：啟動它 |
| `Space` | 啟動焦點所在的連結或按鈕 |

鍵盤焦點會在連結周圍畫出 2 px 的環；指標按下則會取得焦點，但不畫出環。

## 動態效果

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 連結與狀態顏色 | 160 ms | 立即 | 立即 |
| 懸停時的向外箭頭 | 在 160 ms 內向右、向上移動 1 px | 靜止 | 靜止 |
| 輸入框邊框（聚焦、無效） | 160 ms | 立即 | 立即 |
| 電子報文字行 | 從下方 4 px 處上升並經過模糊，耗時 240 ms；舊文字行在 120 ms 內向上離開 | 僅淡入 | 立即 |
| 訂閱按鈕 | 標籤以模糊方式交叉淡入，寬度以彈簧變化，載入時顯示旋轉指示器 | 按鈕的減少動態規則 | 立即 |
| Logo 拱形 | 僅一次，當 30% 在畫面上時：淡入並在 900 ms 內上升 40 px | 立即顯示 | 立即顯示 |

## 相關元件

- 用於完整寬度訂閱區段的 Newsletter signup 區塊。
- 用於頁尾上方結尾行動呼籲的 CTA section 區塊。
- 與之相配的頂部列 Site header 區塊。
