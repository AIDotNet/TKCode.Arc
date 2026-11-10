---
title: 評分
summary: 一種簡單、無障礙的方式，用來收集數字化的評價。
source-hash: ec85978b6dba
---

<Demo name="Feedback" />

## 何時使用

評分在一個簡短的量表上詢問分數，並說明這個分數的含義。指標指向星星時預覽分值，按下或拖曳時提交，星星旁邊的文字會捲動到新的含義。同一控制項也可以顯示無人能修改的分數，例如平均分。

- **評價或回饋表單**：一個問題（例如「整體體驗」），點一下就能回答。預設文字從 Poor 到 Excellent，告訴使用者每顆星代表什麼。
- **商品或清單上的平均分**：用 `IsReadOnly` 搭配 `AllowHalf`，在評價數量旁顯示 4.5 顆星，不帶任何輸入行為。
- **工單關閉後的滿意度調查**：`AllowClear="False"` 讓答案一經給出就會保留，適合表單必須有分數才能繼續的情況。
- **有專屬措辭的量表**：`ValueLabels` 為每顆星指定自己的文字，例如從 Too slow 到 Very fast。

## 何時不用

- **連續的數值**：使用 `Slider`，它在一個範圍內移動，而不是幾個檔位。
- **對貼文或留言的快速反應**：使用 `Reactions`，它統計許多人的表情。
- **不構成量表的具名選項**：使用 `RadioGroup`，每個選項都有自己的標籤和說明。
- **在檢視之間選擇**：使用 `SegmentedControl`，它會切換旁邊的內容。

## 用法

設定 `Label` 並繫結 `Value`，在使用者選擇星星之前它是 0。`ValueChanged` 在使用者提交新值時觸發，程式碼設定值時不觸發。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AIDotNet/TKCode.Arc">
    <arc:Rating Label="Overall experience" Value="{Binding Stars}" />
</UserControl>
```

### 半星與唯讀分數

`AllowHalf` 讓指標、觸控和方向鍵以半星為單位移動。`IsReadOnly` 去掉輸入、只留下分數，因此控制項不取得焦點，可以低成本地在長清單中重複：所有星星由一個控制項繪製，只有星星第一次移動時才建立動效。

<Demo name="Average" />

### 尺寸

`Size` 同時設定星星和點擊目標：18 px 的星星配 28 px 的格子，24 配 36（預設），30 配 44。

<Demo name="Sizes" />

### 清除、量表與停用狀態

再次選擇目前的值會清除評分，Backspace 和 Delete 也一樣。答案必須保留時設定 `AllowClear="False"`。`Maximum` 設定星星數量；不是五顆整星的量表顯示為「7 out of 10」，除非提供 `ValueLabels`。

<Demo name="Required" />

### 自訂圖示

`Icon` 把星星換成任意在 24 × 24 框內繪製的圖形，例如 `{x:Static arc:ArcIcons.Heart}` 或路徑資料；描邊、填充、顏色和動效保持不變。
`EmptyIcon` 為未填充的部分設定另一個圖形。任何圖形都支援半星：左半邊畫 `Icon`，右半邊畫 `EmptyIcon`。

<Demo name="Icons" />

## API 參考

<!-- api: Rating -->

## 無障礙

| 按鍵 | 操作 |
|---|---|
| Right、Up | 將分值提高一檔 |
| Left、Down | 將分值降低一檔 |
| Home、End | 轉到第一檔或最後一顆星 |
| 1 到 9 | 選擇對應的星星（量表中存在時） |
| Space、Enter | 未評分時選擇第一檔，否則確認目前的值 |
| Backspace、Delete | 清除評分，除非 `AllowClear` 為 false |

評分是一個定位點，並把自己回報為從 0 到 `Maximum` 的範圍，以 `Label` 作為名稱。它的說明文字用文字描述目前的值，例如「Great, 4 of 5 stars」。唯讀評分不取得焦點。

## 動效

填滿以 snappy 彈簧逐星掃過，每顆比上一顆晚 35 ms；預覽其他值時，已提交的值保留為淡淡的標記。提交時，所選星星依次彈動一下；兩次提交間隔不足 260 ms 時只有最後一顆星彈動。減弱動效時，星星立即變化，只有文字淡入淡出。

## 響應式

一行放不下時，分值文字會換到星星下方，並保持最長文字的寬度，因此旁邊的內容不會移動。觸控時，分值跟隨手指在這一行上移動，放開時提交，拖過第一顆星會清除評分。手機上使用 `Size="Large"` 可得到 44 px 的目標。

## 相關元件

| 元件 | 適用情境 |
|---|---|
| `Slider` | 數值連續或檔位很多 |
| `Reactions` | 人們用表情和計數回應內容 |
| `RadioGroup` | 選項是具名選擇，而不是量表 |
| `SegmentedControl` | 選擇會切換旁邊的檢視 |
