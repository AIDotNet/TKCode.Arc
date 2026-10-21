---
title: 多區域容錯移轉
summary: 全域負載平衡器將流量切換到備用區域，並在主區域故障時將其資料庫升格為主要資料庫。
source-hash: eb9336626322
---

<Demo name="AutomaticFailover" />

## 何時使用

- 在基礎架構或可靠性頁面上說明某個區域故障時服務如何維持可用。
- 架構文件需要展示哪個區域提供流量、哪個資料庫為主要資料庫時。
- 團隊正式執行前，示範容錯移轉流程。

## 何時不用

- 通知真實服務中斷：用 `Alert` 並填寫實際的狀態文字。
- 只展示一個區域或一台伺服器：故事需要兩到三個區域，因此改用 `Card` 搭配純文字。
- 報告真實的可用性或延遲隨時間的變化：用 `LineChart` 或 `Gauge` 並填入實測值。

## 用法

預設情況下故事自動播放：某個區域未通過健康檢查，流量與主要資料庫切換到備用區域，故障區域復原後以副本身分重新加入，流量最終回到原定區域。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:MultiRegionFailover />
</UserControl>
```

### 區域

`Regions` 接受兩到三個區域名稱。預設值為 `us-east-1`、`eu-west-1` 和 `ap-southeast-1`。少於兩個名稱的清單會回退為預設值。

<Demo name="TwoRegions" />

### 自行驅動故障

將 `IsAutomatic` 設為 `False`，然後用區域索引呼叫 `FailRegion` 和 `RecoverRegion`。每次呼叫都會執行與自動故事相同的狀態機。`PrimaryRegion` 是主要資料庫所在區域的索引，`TrafficRegion` 是接收流量的區域的索引。

<Demo name="DriveItYourself" />

```csharp
var failover = new MultiRegionFailover { IsAutomatic = false };
failover.PrimaryRegionChanged += (_, index) =>
    primaryLabel.Text = $"主要：{MultiRegionFailover.DefaultRegions[index]}";

failButton.Click += (_, _) => failover.FailRegion(0);
recoverButton.Click += (_, _) => failover.RecoverRegion(0);
```

升格完成後會以新主要資料庫的索引觸發 `PrimaryRegionChanged`。讓持有主要資料庫的區域故障即可觸發該事件。

### 標籤

`Labels` 接受一個 `MultiRegionFailoverLabels` 物件，包含圖表中所有可見的文字：節點名稱、健康狀態、標示（`切換中`、`已升為主要`）和步驟說明文字。

```csharp
failover.Labels = new MultiRegionFailoverLabels
{
    Balancer = "全域負載平衡",
    FailingOver = "切換中",
    Promoted = "已升為主要"
};
```

### 播放

`IsPlaying`、`Speed` 和 `Restart()` 與其他插圖一樣，用於控制自動故事。故事共六步，`StepChanged` 會回報每一步。

```xml
<arc:MultiRegionFailover Speed="2" />
```

### 減少動態效果

在減少動態效果下，插圖是健康部署的靜態畫面。

<Demo name="ReducedMotion" />

## API 參考

<!-- api: MultiRegionFailover -->

<!-- api: MultiRegionFailoverLabels -->

## 無障礙

- `Label` 是無障礙名稱，目前的說明文字會作為說明文字提供給輔助技術。
- 插圖可取得焦點，焦點環寬 2px。
- 空白鍵或 Enter 播放與暫停故事，R 鍵從頭開始播放。
- 說明文字的變化不會被播報。手動故障控制項應為真正的按鈕，並帶有各自的名稱，例如「讓 us-east-1 故障」。

## 動態效果

- 故障區域先變為琥珀色，再變為紅色，其傳輸中的封包會被捨棄。
- 負載平衡器將流量切換到健康區域，並顯示「切換中」標示以及切換所需時間。
- 備用資料庫以「已升為主要」標示被升格，複寫從新的主要資料庫開始。
- 復原階段顯示該區域以副本身分追趕資料，隨後自動故事中會進行計畫內的回切。
- 減少動態效果和關閉動態效果時，顯示健康部署的靜態畫面，備用路徑以虛線繪製。播放按鈕會隱藏。

## 相關元件

- `PaymentFlow`：依賴支付服務可用的單筆請求。
- `DataFlow`：將事件存入單一倉儲的管線。
- `Alert`：以文字說明真實服務的狀態。
