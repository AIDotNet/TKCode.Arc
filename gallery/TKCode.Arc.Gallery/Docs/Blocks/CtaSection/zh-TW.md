---
title: CTA section
summary: 行動呼籲可以是置中的收尾區段、旁邊有會自行完成之設定卡片的分割區段，或是可關閉的橫幅。
source-hash: f8f3f6f2d2d5
---

<Demo name="Preview" />

## 何時使用

行動呼籲只請讀者做一件事：開始試用、預約示範、註冊。`CtaSection` 提供三種形式，因此同一個控制項可以用來收尾登陸頁、放在頁面中段搭配產品視覺圖，或橫跨在應用程式頂端。未設定任何內容時，它會顯示你所選形式的 Arc 預設文案。

- **登陸頁的最後一個區段**：`Centered` 形式是一張安靜的卡片，包含標題、一句話、兩個動作，以及一行小字與已加入者的頭像。
- **頁面中段、產品視覺圖旁的提示**：`Split` 形式把文案與簡短的好處條列放在左側，右側是設定卡片。卡片進入可視範圍後會逐步完成其步驟，讓讀者先看到按鈕所帶來的結果。
- **位於內容上方的精簡推廣橫幅**：`Banner` 形式是一行文字、一個動作，以及可選的關閉按鈕。關閉後，它所佔的空間會收合，下方的頁面會平滑地往上移。

## 何時不用

- **頁面的第一個畫面**：請使用 hero。行動呼籲是用來收尾或打斷故事的，不是用來開頭的。
- **收集電子郵件地址**：請使用電子報訂閱區塊，它會在原地驗證、送出並確認。
- **持續存在的全站通知**：請使用公告列。這裡的橫幅只有一則訊息與一個動作，由讀者自行關閉，不是輪播或倒數的通知。
- **有後果的決定**：請使用對話方塊或警示對話方塊。行動呼籲是邀請，不會打斷使用者。

## 用法

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:CtaSection />
</UserControl>
```

`CtaSection` 衍生自 `TemplatedControl`。`Variant` 用來選擇形式；每段文字、動作、清單與圖片都是屬性。除了圖片以外，每個屬性都預設為 Arc 的英文文字；圖片要加入後才會顯示，在那之前是空的。此區塊是一個版面配置，因此動作規則、資料與導向的目的地仍由你的應用程式負責。

### 形式

| `Variant` | 顯示內容 | 動作 |
|---|---|---|
| `Centered` | 標題、文字、動作，以及在安靜卡片上最多四張臉孔的備註 | 主要、次要 |
| `Split` | 文案、好處條列與動作，旁邊是舞台；舞台放設定卡片或你自己的 `Visual` | 主要、次要 |
| `Banner` | 一行文字、一個動作，以及可選的關閉按鈕 | 僅主要 |

設定 `Title`、`Description`、`PrimaryAction` 與 `SecondaryAction` 即可取代任何形式的文字。未設定的值會顯示該形式自己的 Arc 文案，因此把 `Variant` 從 `Centered` 換成 `Split`，文字也會跟著改變。

### 動作

每個動作都是一個 `CtaAction`，具有 `Label`、可選的 `Href`、`ConfirmedLabel`、`Command` 與 `CommandParameter`。按下時會發生什麼，取決於你給它的設定。

| 動作具有 | 按下時 |
|---|---|
| 一個 `Href` | 引發 `ActionInvoked`，若有命令則執行它，然後開啟該網址，除非處理常式將事件標記為已處理。只會開啟絕對的 http、https 與 mailto 網址 |
| 一個 `Command`，或處理常式將 `ActionInvoked` 標記為已處理 | 只會執行，畫面上沒有其他變化 |
| 以上皆無 | 原地確認：勾號與 `ConfirmedLabel`（若無則為 `Label`）取代文字，再按一次會還原，變化會以禮貌模式朗讀 |

原地確認是讓預覽能回應每次按下的原因。在應用程式中，請為每個動作設定命令、目的地或處理常式。

```xml
<arc:CtaSection Variant="Split">
    <arc:CtaSection.PrimaryAction>
        <arc:CtaAction Label="Create a workspace" Command="{Binding CreateWorkspace}" />
    </arc:CtaSection.PrimaryAction>
    <arc:CtaSection.SecondaryAction>
        <arc:CtaAction Label="Book a demo" Href="https://example.com/demo" />
    </arc:CtaSection.SecondaryAction>
</arc:CtaSection>
```

```csharp
section.ActionInvoked += (_, e) =>
{
    if (e.IsPrimary)
    {
        analytics.Track("cta_primary", e.Action.Label);
    }
};
```

`CtaActionEventArgs` 帶有被按下的 `Action`、`IsPrimary`，以及 `IsConfirmed`：當按下的動作使一般動作進入已確認狀態時為 true。若要完全接管一般動作，請將事件標記為 `Handled`。

<Demo name="Links" />

主要動作的箭頭會在滑鼠或筆移過時向前傾斜 3 px；觸控不會觸發傾斜。在置中與分割形式中，動作是大型按鈕；在橫幅中則是小型按鈕。

### 置中

`Centered` 形式會把備註放在動作下方。`Note` 預設為 "No credit card required"（不需要信用卡）；若要隱藏文字，請將它設為空字串。將圖片加入 `Faces`，最多四張重疊的臉孔會出現在備註旁；若沒有任何臉孔與備註，此列會隱藏。

```csharp
section.Note = "Join 4,000 teams";
section.Faces.Add(new Bitmap("avatar-1.png"));
section.Faces.Add(new Bitmap("avatar-2.png"));
```

臉孔僅作為螢幕閱讀器的裝飾，因此請在 `Title` 與 `Description` 中描述區段所提供的內容。

### 分割與設定卡片

`Split` 形式會在 `Points` 中列出好處條列（null 會顯示 Arc 的三項；空清單則隱藏此列表），並在右側放置舞台。舞台預設放置一張會自行完成的設定卡片：

1. 舞台只會在可見 35% 時升起進入視野一次。
2. 當卡片可見一半時，三個計時器開始計時。第 *i* 步會在 520 + 620 × *i* ms 後完成（大約在 0.5、1.1 與 1.8 秒時）。
3. 每一步完成時，其標記會以彈入的勾號填滿，進度線會增長，下一步則改為淡化的填色。頂端的狀態會從 "Setting up" 變為 "Ready"。
4. 最後一列會顯示 `SetupFaces`，在該步驟成為目前步驟後，這些臉孔會一個接一個滑入。

設定卡片可用 `SetupWorkspace`（其第一個字母即為標記）、`SetupSteps`（null 會顯示 Arc 的三個步驟）與 `SetupFaces` 來設定。卡片純粹是結果的示意圖。它會對螢幕閱讀器隱藏，旁邊的文字負責傳達訊息。

若要顯示你自己的截圖或控制項，請設定 `Visual`。舞台會保留其大小、邊框與進場動畫；設定卡片與它的計時器則不再使用。

<Demo name="CustomVisual" />

```xml
<arc:CtaSection Variant="Split">
    <arc:CtaSection.Visual>
        <Border Width="320" Height="200" CornerRadius="26" Background="{arc:ArcDesignTokenResource SurfaceRaised}" />
    </arc:CtaSection.Visual>
</arc:CtaSection>
```

### 橫幅

`Banner` 形式是一條圓角的單行。設定 `IsDismissible` 即可顯示關閉按鈕。按下它，或呼叫 `Dismiss()`，會關閉橫幅：它會在 160 ms 內淡出，同時高度以彈簧收縮至零，然後 `IsOpen` 變為 false，並引發 `Dismissed`。`Show()` 會立即將它重新顯示。

```xml
<arc:CtaSection Variant="Banner"
                IsDismissible="True"
                IsOpen="{Binding ShowPromo, Mode=TwoWay}" />
```

`IsOpen` 是雙向繫結。若你從自己的程式碼將它設為 false，會播放相同的離場動畫，但不會引發 `Dismissed`。若橫幅在下次啟動後應保持關閉，請自行記住此選擇（例如設定或相當於 cookie 的機制）。若要以其他語言命名按鈕，請設定 `DismissLabel`。

### 響應式版面

區塊會依自己的寬度而非視窗寬度調整。寬度為 860 px 或更小時，分割形式會變為單欄，舞台位於文案下方。寬度為 560 px 或更小時，間距會收緊，卡片圓角變為 28 px，動作會以全寬堆疊，橫幅會把文字放在動作上方，關閉按鈕則位於右邊緣。標題會隨區塊放大，從 30 px 增至 52 px。

<Demo name="Narrow" />

### 內容指引

- **標題**：用幾個字做出承諾，例如 "Start building in minutes"（幾分鐘內開始建置）。置中的標題大約每 18 個字換行，並使各行長度平衡。
- **動作標籤**：一個動詞加上最多兩個其他字（"Create a workspace"、"Book a demo"）。主要動作是你希望讀者接下來執行的那一件事。
- **確認標籤**：說明發生了什麼（例如 "Workspace created"），而不是 "Done"。
- **好處條列**：三項或更少，每一項都要短到能放進左欄的一行內。

### 最佳實務

**建議**

- 為主要動作設定真正的目的地或命令，且每個區段只保留一個主要動作。
- 在頁面結尾使用 `Centered`，在中段且與相關狀態並列時使用 `Split`，在應用程式或文件頁的頂端使用 `Banner`。
- 對於非必要的橫幅，請保持 `IsDismissible` 開啟，並尊重關閉橫幅之讀者的選擇。

**避免**

- 兩個主要動作。次要動作刻意設計得較為低調。
- 過長的標題。標題會受寬度限制並平衡排版，因此很長的句子會變成很高的區塊。
- 在正式環境中依賴原地確認。它的存在是為了在預覽中回應按下。

## API 參考

<!-- api: CtaSection -->

<!-- api: CtaAction -->

<!-- api: CtaActionEventArgs -->

## 無障礙

- 此區段是一個以其標題命名的區域。
- 每個動作與關閉按鈕都有無障礙名稱。確認會透過隱藏的即時區域以禮貌模式朗讀，再次按下已確認的動作則會朗讀其一般標籤。
- 臉孔與設定卡片屬於裝飾，會對螢幕閱讀器隱藏。
- 橫幅被關閉時會收合其空間，因此關閉後鍵盤焦點順序仍保持乾淨。

### 鍵盤

| 按鍵 | 動作 |
|---|---|
| `Tab` / `Shift+Tab` | 在動作之間移動，橫幅中則接著移到關閉按鈕 |
| `Enter` / `Space` | 執行動作或關閉橫幅 |

鍵盤焦點會在被聚焦的動作或關閉按鈕周圍顯示 2 px 的環；滑鼠焦點則不會。

## 動態效果

- 主要動作會在滑鼠移上時將箭頭向前傾斜 3 px（160 ms），標籤變更時會交叉淡化。
- 分割形式的舞台會進入視野一次（透明度與 24 px 的位移，480 ms）。在設定卡片中，狀態文字會以短暫的模糊切換，進度線在 480 ms 內增長，步驟標記在 240 ms 內填滿，勾號以彈簧在 580 ms 內彈入，團隊成員的臉孔則以 60 ms 的間隔滑入。
- 關閉橫幅時，它會在 160 ms 內淡出，同時高度以彈簧收合。沒有任何縮放。

| 變化 | Full | Reduced | Off |
|---|---|---|---|
| 舞台出現 | 升起進入一次，480 ms | 立即顯示 | 立即顯示 |
| 設定卡片執行 | 步驟逐一勾選、進度線增長、臉孔滑入 | 所有步驟立即完成 | 時間軸照常進行；每次變化都直接跳動 |
| 橫幅被關閉 | 160 ms 內淡出，高度以彈簧收至零 | 120 ms 內淡出，然後移除 | 立即移除 |
| 箭頭傾斜 | 160 ms | 直接跳到位 | 直接跳到位 |

## 相關元件

- `Button` 用於單一動作，`SegmentedControl` 用於預覽中的版面切換。
- 當動作是收集電子郵件地址時，請使用電子報訂閱。

| 元件 | 適用時機 |
|---|---|
| `CtaSection` | 你要在頁面結尾、視覺旁或橫幅中要求試用、示範或註冊 |
| 電子報訂閱 | 動作是電子郵件地址 |
| `Button` | 你只需要一個動作，不需要區段包裹 |
