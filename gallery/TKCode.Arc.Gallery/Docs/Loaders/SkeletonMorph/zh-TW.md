---
title: Skeleton morph
summary: 載入中的佔位會逐區塊伸展成真實內容，同時卡片平滑地調整到新的高度。
source-hash: 431adcc738cf
---

<Demo name="Preview" />

## 何時使用

`SkeletonMorph` 取代從灰色方塊到內容的生硬切換。已載入版面中每個可見的部分都位於 `MorphBlock` 內。區域載入時，每個區塊都是與其內容同樣大小的骨架：圓形頭像、按鈕的膠囊形狀，以及文字每行一條的長條。資料抵達後，區塊會依閱讀順序一個接一個解析。每個骨架會就地伸展到其內容實際佔用的方框並淡出，同時內容會從底下的柔和模糊中逐漸清晰。區域會同時平滑調整到新的高度，因此卡片的邊框不會跳動。

- **在數秒內載入的個人資料卡、動態消息與清單**：版面從第一個影格起就可見，並且不閃爍地穩定下來。
- **版面在資料之前就已知的儀表板**：磁貼保留其位置，只有內部內容改變。
- **生硬切換會顯得突兀的地方**：卡片因為多出或少了一行而變大或變小時，會平滑過渡而不是跳動。

## 何時不用

- **數百列資料**：每個區塊在解析時都會量測並播放動畫。在虛擬化清單中請使用 `Skeleton` 列。
- **載入時間超過數秒**：請使用 `Progress` 顯示進度，讓人知道還有多久。
- **事先無法得知的版面**：與到達內容不相符的骨架，比沒有骨架更糟。請使用 `Skeleton` 或 `EmptyState`。
- **不需要變形的單一佔位**：請使用 `Skeleton`。

## 用法

將已載入的版面放入 `SkeletonMorph`，並把每個可見的部分包在 `MorphBlock` 中。將 `IsLoading` 繫結到你的請求。為文字區塊指定文字本身的行高與行數，並把骨架寬度設為接近文字的典型寬度。加上 `card` 樣式類別，讓區域本身成為卡片：如此其邊框就會跟隨平滑變化的高度。

```xml
<arc:SkeletonMorph Classes="card" IsLoading="{Binding IsLoading}" LoadingLabel="Loading profile">
    <StackPanel Spacing="16">
        <StackPanel Orientation="Horizontal" Spacing="12">
            <arc:MorphBlock IsCircle="True" SkeletonWidth="40">
                <arc:Avatar DisplayName="{Binding Name}" />
            </arc:MorphBlock>
            <arc:MorphBlock SkeletonWidth="120" Lines="1" LineHeight="20">
                <TextBlock Text="{Binding Name}" FontSize="16" LineHeight="20" />
            </arc:MorphBlock>
        </StackPanel>
        <arc:MorphBlock Lines="3" LineHeight="20">
            <TextBlock Text="{Binding Bio}" FontSize="14" LineHeight="20" TextWrapping="Wrap" />
        </arc:MorphBlock>
    </StackPanel>
</arc:SkeletonMorph>
```

兩種型別都位於 Special 套件中：請在 `UseArcControls()` 旁呼叫 `UseArcSpecial()` 來註冊它。`SkeletonMorph` 與 `MorphBlock` 衍生自 Avalonia 的 `ContentControl`。

上方的預覽會重建一張個人資料卡與一份轉帳清單。兩者在頁面開啟時都會載入一次（1.2 s），而 Reload 會再次載入它們並耗時 1.5 s。在區塊仍在解析時按下重新載入：每個區塊會立即回到骨架，同時每張卡片會平滑退回載入時的高度。Follow 切換開關與請求計時器屬於此範例；人名、金額與日期都是虛構的。

### 驅動方式

`IsLoading` 是雙向的，因此開關或檢視模型都能驅動它。`Resolved` 會在最後一個區塊解析完成且其骨架淡出後引發；`IsResolving` 在兩者之間為 true。

<Demo name="Controlled" />

```csharp
private async Task ShowAsync(Func<Task<object>> load)
{
    profile.IsLoading = true;
    DataContext = await load();
    profile.IsLoading = false; // the blocks resolve, then Resolved is raised
}
```

載入中的區塊不顯示其內容：內容不在視覺樹中，因此無法取得焦點，螢幕閱讀器也無法讀到載入一半的文字。繫結的內容在資料抵達前可以是空的。

### 骨架形狀

除非設定了 `Lines`，否則區塊是一個實心形狀。

- **形狀**：`SkeletonWidth` 與 `SkeletonHeight` 以像素為單位，並以 `SkeletonRadius` 設定圓角（預設為 6）。`IsCircle` 會做出圓形頭像佔位；若設定寬度而未設定高度，則為該寬度的圓形。
- **文字**：`Lines` 條長條，每條置中於高度為 `LineHeight` 的行框中（預設為 20）。長條高 12 px；若行高小於 18 px，則比行高少 6 px。多條長條中的最後一條為寬度的 62 %。
- **寬度**：不設定 `SkeletonWidth` 時會填滿可用寬度；`SkeletonWidthRatio` 會以其比例繪製填滿的骨架（`0.6` 表示 60 %）。填滿的區塊在載入後也會佔滿整個寬度。
- **高度**：未設定 `SkeletonHeight` 時，文字區塊的高度為 `Lines` × `LineHeight`，其他區塊為 12 px。

<Demo name="Shapes" />

區塊預設為靠左與靠上對齊，與它們所代表的文字一致。若是右對齊的數字（例如金額），請使用 `HorizontalAlignment="Right"`：骨架會從區塊的新左邊緣開始伸展。

### 錯開

區塊依閱讀順序解析：即視覺樹的順序，深度優先。`Stagger` 設定兩個區塊之間的時間（預設為 40 ms）；`0` 會讓每個區塊在同一個影格中解析。巢狀 `SkeletonMorph` 的區塊屬於該區域，並遵循其 `IsLoading`。

<Demo name="Stagger" />

### 減少動態效果

在減少動態效果下，骨架保留透明度脈動（因此仍看得出正在載入），但不會伸展或模糊，每個區塊會在 120 ms 內同時交叉淡入其內容。區域的高度會立即改變。關閉動態效果時骨架不會脈動。

<Demo name="ReducedMotion" />

### 結構

1. **區域**：一個 `ContentControl`，容納已載入的版面。使用 `card` 類別時，它具有表面背景、1 px 邊框、26 px 圓角與 20 px 內距。
2. **外殼**：內容周圍的部分，其高度依彈簧效果跟隨內容。只在移動時才裁剪。
3. **狀態**：一個隱藏的即時區域，在區域載入時朗讀 `LoadingLabel`。
4. **區塊**：每個可見部分對應一個 `MorphBlock`：其內容，以及覆蓋在上方的骨架。
5. **骨架**：一個實心形狀，或每行文字一條圓角長條，使用柔和的表面色。伸展時可以超出其區塊。

### 狀態

| 狀態 | 區域 | 區塊 |
|---|---|---|
| 載入中 | `:loading`；狀態列朗讀該標籤 | 骨架以各自的大小呈現，一起呼吸；沒有內容 |
| 解析中 | `:resolving`；`IsResolving` 為 true | 依序進行：內容掛載，骨架伸展並淡出 |
| 已載入 | 沒有偽類別 | 只有內容；`IsResolved` 為 true |
| 回到載入中 | 再次為 `:loading` | 每個區塊在同一影格中變為骨架；高度仍平滑變化 |

### 內容指引

- 讓骨架與真實內容的大小一致。為文字設定明確的行高，並把相同的值傳給 `LineHeight`；將 `SkeletonWidth` 設為典型寬度附近，而非最長的寬度。
- 包住每個可見的部分。區塊之外的部分變化時沒有動畫，而且會比它的鄰居更早出現。
- 將 `LoadingLabel` 寫成正在載入的內容，用幾個字即可："Loading profile"、"Loading transfers"。
- 讓已載入的版面與骨架保持相同的形狀。行數不同沒有問題；版面不同則不行。

### 最佳做法

- 把卡片樣式放在區域上（`card` 類別，或你自己的 `Background`、`BorderBrush`、`CornerRadius` 與 `Padding`），讓邊框跟隨平滑變化的高度。
- 保持區域的高度不受約束：它預設為靠上對齊，因為被伸展的區域會遮住緩動效果。
- 將 `IsLoading` 設回 true 即可重新載入。它是立即生效的，因此快速重新載入不會讓區塊停在一半的狀態。
- 區域載入時，不要停用觸發重新載入的按鈕：改為忽略其按下動作，讓鍵盤焦點停留原處。

### 常見問題

**為什麼卡片在載入後高度會改變？** 內容的大小與其骨架不同，例如個人簡介需要兩行而非三行。區域會以與區塊相同的彈簧效果平滑調整到新的高度。

**寬度可以用百分比嗎？** 不設定 `SkeletonWidth`，改設定 `SkeletonWidthRatio`。

**區塊在載入時，其內部的繫結會怎樣？** 內容物件已經存在但不顯示；其範本與繫結會在區塊解析時套用，因此讀取的是已抵達的資料。

## API 參考

<!-- api: SkeletonMorph -->

<!-- api: MorphBlock -->

## 無障礙

- 載入時，區域的狀態會透過禮貌即時區域朗讀 `LoadingLabel`，其項目狀態也帶有相同的文字。載入結束時兩者都會清除。
- 骨架是繪製出來的，不是控制項：螢幕閱讀器看不到它們。
- 內容在其區塊解析之前不在視覺樹中，因此載入一半的內容無法取得焦點或被讀取。
- 區塊保持閱讀順序，因此載入前後的焦點順序相同。區域與區塊本身不可聚焦。

### 鍵盤

區域不新增任何按鍵。區塊內的控制項在顯示後會保留各自的鍵盤行為；載入時則無法以 `Tab` 到達。

## 動態效果

- 載入時骨架會一起呼吸：在 ease-in-out 曲線上，透明度在 1.4 s 內從 1 變為 0.55，再回到 1。
- 區塊依閱讀順序解析，彼此相隔 40 ms。骨架以臨界阻尼彈簧（平滑彈簧，約 0.5 s，無過衝）從其自身方框伸展到內容量測出的方框，並在 50 ms 暫停後於 380 ms 內淡出。內容在 330 ms 內淡入，同時從 4 px 的模糊中逐漸清晰。
- 區域以同一彈簧平滑調整到新的高度，並在移動時裁剪。
- 區塊回到載入狀態是立即的；高度仍會平滑變化。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 脈動 | 1 ↔ 0.55，1.4 s | 1 ↔ 0.55，1.4 s | 無 |
| 解析順序 | 彼此相隔 40 ms | 同時全部 | 同時全部 |
| 骨架到內容 | 以彈簧伸展並淡出 | 120 ms 交叉淡化 | 即時 |
| 內容 | 從 4 px 模糊淡入 | 120 ms 淡入 | 即時 |
| 區域高度 | 彈簧 | 即時 | 即時 |
| 回到載入中 | 立即（高度以彈簧變化） | 立即 | 立即 |

## 響應式行為

- 區塊載入後會依其內容調整大小，因此版面在任何寬度下都會重新排列。
- 填滿的骨架會跟隨可用寬度；`SkeletonWidthRatio` 會讓它維持在可用寬度的某個比例。
- 寬度變化時，區域的高度會立即跟隨；只有內容的變化會平滑過渡。

## 相關

- `Skeleton`：不含變形的簡單佔位。
- `MorphLoader`：一個小型載入指示器，會收合成勾號或叉號。
- `EmptyState`：沒有內容可顯示時，提供有用的下一步。

| 元件 | 適用時機 |
|---|---|
| `SkeletonMorph` | 版面已知，且應該穩定地過渡到內容 |
| `Skeleton` | 你需要佔位，或長的虛擬化清單 |
| `MorphLoader` | 精簡的指示器就已足夠 |
| `Progress` | 載入時間長到需要顯示進度 |
