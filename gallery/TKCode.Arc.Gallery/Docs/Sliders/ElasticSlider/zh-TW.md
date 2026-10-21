---
title: 彈性滑桿
summary: 滑桿越過兩端時如橡皮筋般拉伸，附帶搖擺的數值標籤，以及滑桿會卡入的檔位。
source-hash: 0d63d9545c7e
---

<Demo name="RubberBand" />

## 何時使用

`ElasticSlider` 在一條會在手指下讓位的軌道上設定一個值。它適合一個需要憑手感調整、並透過觀察軌道來確認的單一設定：媒體播放器列中的控制項、工具檢視器，或是滑桿為該列唯一控制項的設定列。軌道在兩端之外拉伸，放開後回彈，因此使用者無需看到任何訊息，也能感覺到已經到達極限。

- **會議或媒體應用程式中的播放與輸出等級**：播放器列中的音量或麥克風增益。精確的分貝值遠不如「輕」與「響」的手感重要，而在有用的等級（`Off`、`50`、`Max`）上設定檔位，可以提供一個明確的目標。
- **創作工具檢視器中的調整項**：所選圖層的亮度、對比或模糊度。使用者會透過來回拖曳來比較效果，因此跟隨滑桿的數值標籤和在極限處的拉伸很適合這類工作。
- **營運主控台或資訊亭設定中的裝置與顯示控制項**：裝置設定頁中的螢幕亮度，或機架面板上的風扇轉速。這些設定經常變動，而 50% 這類預設值是常見的目標。
- **主題或品牌編輯器中視覺效果的強度**：例如橫幅遮罩的不透明度，或陰影的強度。此時使用者判斷的是效果看起來如何，而不是讀取一個數字。

## 何時不用

- **安靜的表單或只設定一次值的設定頁**：用 `Slider`。它有相同的軌道與數值泡泡，但在兩端保持靜止，更適合使用者由上而下閱讀的表單。
- **需要下限與上限的範圍**，例如價格或日期篩選：用支援 `IsRange` 的 `Slider`。`ElasticSlider` 只有單一的 `Value`，沒有範圍模式。
- **使用者必須鍵入的精確數字**，例如稅率、折扣或數量：用 `NumberField`。軌道只能近似一個值，當使用者需要精確數字時，拉伸效果沒有幫助。
- **使用者無法變更的值**，例如上傳進度、已用儲存空間或工作完成度：用 `Progress`。
- **同一清單中的大量滑桿**，例如混音台中每個聲道一列，或庫存表格中每個商品一列：用 `Slider`。拉伸、搖擺和檔位彈出會在每一列重複出現，把注意力從資料上移開。

## 用法

基本用法是單一數值。`Value` 是滑桿在軌道上的位置，`Minimum` 與 `Maximum` 設定其範圍。

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc">
    <arc:ElasticSlider Label="音量"
                       Value="40"
                       Width="360"
                       ValueFormatter="{Binding VolumeFormatter}" />
</UserControl>
```

`ElasticSlider` 衍生自 Avalonia 的 `RangeBase`。因此 `Value`、`Minimum`、`Maximum`、`SmallChange`、`LargeChange` 以及 `ValueChanged` 事件的行為與任何範圍控制項一致。此控制項另外加入標籤、讀數、數值標籤、橡皮筋式軌道、檔位以及兩個圖示位。`SmallChange` 是數值貼齊的步長，在此控制項上預設為 1。

### 結構

1. **標籤**（`Label`）：中等字重，位於軌道上方一列，行高 19.6 px。寬度不足時以省略號截斷。它同時是滑桿把手的無障礙名稱。
2. **讀數**：格式化後的 `Value`，靠右對齊顯示在標籤列上，使用次要文字顏色，與前面保持 16 px 的間距。數值標籤跟隨滑桿移動時，讀數會淡出，因此同一個數字不會同時顯示兩次。
3. **起始圖示**（`StartIcon`）：位於軌道之前，間距 12 px，使用淡化文字顏色。屬性為 null 時隱藏。
4. **軌道與填滿**：8 px 高的軌道，使用控制項軌道顏色；滑桿之前的部分使用控制項填滿顏色。軌道所在列為中等控制項高度。
5. **滑桿把手**：24 px 的圓盤，使用控制項滑桿顏色與其上浮陰影。點擊區域向圓盤兩側各延伸 12 px，因此稍微偏離也能抓住它。
6. **數值標籤**：位於滑桿把手上方 10 px 的上浮泡泡（觸控或觸控筆按下時再上移 10 px），高度至少 28 px，水平內距 12 px。它以前景色為背景、以背景色為文字顏色。
7. **檔位刻度點**：4 px 的圓點，每個刻度一個。軌道上的刻度點使用前景色、透明度 32%；填滿部分上的刻度點使用控制項字形顏色、透明度 60%，以便在填滿上仍然可見。
8. **結束圖示**（`EndIcon`）：位於軌道之後，間距 12 px，使用淡化文字顏色。
9. **刻度標籤列**：高 20 px，位於軌道下方，使用超小字級與淡化顏色。只有當至少一個刻度設定了 `Label` 時，該列才會出現。

### 圖示

`StartIcon` 與 `EndIcon` 分別位於軌道之前與之後。當軌道被拉過某一端時，該端的圖示會隨之向外移動，因此拉伸看起來是整個控制項在手指下讓位。這裡應使用字符或小圖示按鈕，而不是需要空間伸展的文字。例如音量的減號與加號、亮度的月亮與太陽，都能告訴使用者數值的變化方向。

```xml
<arc:ElasticSlider Label="亮度"
                   Value="50"
                   Width="360">
    <arc:ElasticSlider.StartIcon>
        <TextBlock Text="-" />
    </arc:ElasticSlider.StartIcon>
    <arc:ElasticSlider.EndIcon>
        <TextBlock Text="+" />
    </arc:ElasticSlider.EndIcon>
</arc:ElasticSlider>
```

### 檔位

在 `Marks` 中加入 `SliderMark` 值。被拖曳的滑桿會卡入每一個刻度。帶標籤的刻度會在軌道下方顯示標籤，點按標籤會將滑桿移到該位置。

<Demo name="Detents" />

檔位只在滑桿被拖曳時生效。當指標距離某個刻度在螢幕上 7 px 以內時，數值會保持在該刻度上，滑桿滑向它；移到更遠處，數值才重新跟隨指標。超出 `Minimum` 與 `Maximum` 範圍的刻度會被忽略。當兩個刻度在螢幕上相距不到約 14 px 時，較近的那個總是優先，另一個無法透過拖曳到達。按鍵不會貼齊到刻度：它們依 `SmallChange` 移動，但恰好落在刻度上的按鍵仍會播放刻度的彈出效果。

刻度只用於使用者真正會選擇的值：限制值 `0` 與 `100`、一個中間預設，或具有特定意義的等級，例如單位增益。只為需要名稱的刻度加標籤（`Off`、`Max`）；軌道下方一排裸數字只會增加雜訊。

```xml
<arc:ElasticSlider Label="亮度" Value="50" Width="360">
    <arc:ElasticSlider.Marks>
        <arc:SliderMark Value="0" Label="關閉" />
        <arc:SliderMark Value="50" Label="50" />
        <arc:SliderMark Value="100" Label="最大" />
    </arc:ElasticSlider.Marks>
</arc:ElasticSlider>
```

### 步長與格式

`SmallChange` 是數值貼齊的步長。方向鍵以及沒有檔位的拖曳都會落在它上面。`LargeChange` 是 `PageUp`、`PageDown` 以及方向鍵配合 `Shift` 使用的步長；未設定時為範圍的十分之一。

`ValueFormatter` 格式化數值標籤、讀數與朗讀的數值。它是一個 `Func<double, string>`，因此可以在程式碼中設定，或從檢視模型繫結。未設定時，數值依步長與最小值所需的小數位數顯示，因此步長為 0.1 時顯示 `1.0`，整數步長時顯示 `40`。`IsValueVisible` 可隱藏標籤旁的讀數。拖曳滑桿或以按鍵變更時，數值標籤仍會顯示，因此隱藏讀數並不會隱藏數值。

<Demo name="FineSteps" />

```xml
<arc:ElasticSlider Label="縮放"
                   Minimum="0.5"
                   Maximum="3"
                   SmallChange="0.1"
                   Value="1"
                   Width="360"
                   ValueFormatter="{Binding ZoomFormatter}" />
```

依軌道寬度選擇步長。步長應使滑桿移動數個像素：在約 300 px 寬的軌道上，對於 2.5 的範圍，步長 0.1 會使滑桿移動 12 px，這是看得見的。步長過細會讓每次按鍵看起來毫無變化，步長過粗則會跳過中間的值。

### 事件

`ValueCommitted` 會在每次放開時、每次按鍵使數值改變時，以及每次點按刻度標籤時各觸發一次。其 `SliderValueCommittedEventArgs.Value` 即為提交的值；`LowerValue` 與 `UpperValue` 重複該值，`IsRange` 為 `False`，因為此控制項只有一個值。在極限處按鍵不會改變數值，因此不會提交。應將耗時作業（例如網路要求）放在 `ValueCommitted` 中，而不是 `ValueChanged` 中，後者在拖曳的每一步都會由繼承的事件觸發。

```csharp
volume.ValueCommitted += (_, e) => SaveVolume(e.Value);
volume.ValueChanged += (_, e) => PreviewVolume(e.NewValue);
```

### 繫結到檢視模型

在 MVVM 應用程式中，數值由檢視模型持有，滑桿只負責鏡像它。檢視模型使用 CommunityToolkit.Mvvm。`Value` 是雙向屬性：拖曳、按鍵或檔位都會把新值寫回檢視模型；在程式碼中做出的變更會以彈簧動畫移動滑桿把手。`ValueCommitted` 是事件而非命令，因此應在檢視的程式碼後置檔中訂閱它，再從那裡呼叫檢視模型。

```csharp
public interface IAudioSettings
{
    Task SetVolumeAsync(double level);
}

public partial class PlaybackViewModel : ObservableObject
{
    private readonly IAudioSettings _settings;

    [ObservableProperty]
    private double _volume = 40;

    public PlaybackViewModel(IAudioSettings settings) => _settings = settings;

    public Task SaveVolumeAsync(double value) => _settings.SetVolumeAsync(value);
}
```

```xml
<arc:ElasticSlider x:Name="Volume"
                   Label="音量"
                   Minimum="0"
                   Maximum="100"
                   Value="{Binding Volume, Mode=TwoWay}"
                   Width="360" />
```

```csharp
// 在檢視的程式碼後置檔中，InitializeComponent() 之後。
Volume.ValueCommitted += async (_, e) => await ViewModel.SaveVolumeAsync(e.Value);
```

- `Value` 是要繫結的雙向屬性。
- 明確繫結 `Mode=TwoWay`，讓意圖在 XAML 中一目了然。拖曳的每一步都會更新檢視模型的 `Volume`，這對預覽是正確的，但對昂貴的儲存作業則不適合；應改從 `ValueCommitted` 儲存。
- 將 `Value` 繫結到 `double` 屬性。滑桿以 `double` 運作，因此整數屬性也應為 `double`，或由檢視模型在邊界處轉換。
- `ValueFormatter` 在程式碼中設定，或繫結到檢視模型上的 `Func<double, string>`，如用法範例所示。

### 節制地使用動態效果

此控制項上的動態效果承載著資訊。拉伸表示已到達極限，搖擺表示滑桿移動的速度，檔位彈出確認數值落在了刻度上。應在檢視中只用於一兩個彈性滑桿，即使用者花時間憑手感調整數值的地方。若在清單中放入多個，動態效果就會變成與內容爭奪注意力的圖案；此處應使用 `Slider`。

減少動態效果會移除拉伸、搖擺和檔位彈出。滑桿直接移到目標值，標籤只淡入，因此即使沒有物理回饋，控制項仍能正確表達。不要依賴動態效果來顯示數值：讀數和朗讀的數值始終承載它。

### 停用狀態

停用的滑桿會移出 Tab 順序，並淡化到 50% 不透明度。其軌道顯示「禁止」游標，並忽略指標與按鍵。只在數值暫時無法套用時使用，並在附近說明原因，例如「需 Pro 方案可用」。使用者永遠無法變更的值應使用 `Progress` 或純文字。

<Demo name="Disabled" />

### 範例：照片編輯器調整

一個影像編輯器的檢視器，包含三項調整：以 0 為中心的曝光與對比，以及所選圖層的不透明度。整數不需要格式化器，因此讀數直接顯示原始值。其中最重要的是 0 處的檔位：把曝光拖離中性再拖回的使用者，希望精確回到這裡，而滑桿會停留在該處。

![帶有曝光與對比在 0 處檔位以及圖層不透明度滑桿的調整面板](images/PhotoAdjustments.png)

```xml
<StackPanel Width="560" Spacing="22">
    <TextBlock Text="Adjustments"
               FontSize="14"
               FontWeight="SemiBold"
               Foreground="{arc:ArcDesignTokenResource Foreground}" />
    <arc:ElasticSlider Label="Exposure"
                       Minimum="-100"
                       Maximum="100"
                       Value="25"
                       SmallChange="1">
        <arc:ElasticSlider.StartIcon>
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Moon}"
                          Size="16"
                          Foreground="{arc:ArcDesignTokenResource TextMuted}"
                          VerticalAlignment="Center" />
        </arc:ElasticSlider.StartIcon>
        <arc:ElasticSlider.EndIcon>
            <arc:ArcGlyph Data="{x:Static arc:ArcIcons.Sun}"
                          Size="16"
                          Foreground="{arc:ArcDesignTokenResource TextMuted}"
                          VerticalAlignment="Center" />
        </arc:ElasticSlider.EndIcon>
        <arc:ElasticSlider.Marks>
            <arc:SliderMark Value="-100" Label="-100" />
            <arc:SliderMark Value="0" Label="0" />
            <arc:SliderMark Value="100" Label="100" />
        </arc:ElasticSlider.Marks>
    </arc:ElasticSlider>
    <arc:ElasticSlider Label="Contrast"
                       Minimum="-100"
                       Maximum="100"
                       Value="-10"
                       SmallChange="1">
        <arc:ElasticSlider.Marks>
            <arc:SliderMark Value="0" />
        </arc:ElasticSlider.Marks>
    </arc:ElasticSlider>
    <arc:ElasticSlider Label="Layer opacity"
                       Minimum="0"
                       Maximum="100"
                       Value="80"
                       SmallChange="1"
                       IsValueVisible="True" />
</StackPanel>
```

標籤使用名詞，刻度只標出使用者需要辨認的數值：兩個極限與中性點。對比滑桿在 0 處有一個無標籤的刻度，因此它的檔位仍然有效，而不必增加標籤列。圖層不透明度滑桿沒有刻度，因為對每個圖層而言，任何預設的不透明度都沒有意義。

### 文案規範

- **標籤**：一到兩個句首大寫的名詞，例如「音量」「亮度」「圖層不透明度」。標籤為滑桿把手提供無障礙名稱，因此它必須說明數值是什麼。
- **刻度標籤**：簡短的字或數字，用來命名預設：`Off`、`Max`、`50`。沒有標籤的刻度仍然會吸住滑桿，因此，對於軌道上一目了然的預設，保持無標籤即可。
- **單位**：把單位放在 `ValueFormatter` 的輸出中，而不是標籤裡，使讀數、標籤與朗讀的數值保持一致。單位應簡短；在這麼小的軌道上，百分號比「percent」一字更清楚。
- **提示**：當數值會產生後果時，在控制項旁邊（而不是控制項內部）寫一句話，例如「僅套用於所選圖層。」

| 不要寫 | 改為 |
|---|---|
| 設定音量等級 | 音量 |
| 最大音量 | 最大 |
| 百分之 40 | 40% |
| 亮度（0 到 100） | 亮度 |

### 最佳做法

**建議**

- 每個決策只放一個彈性滑桿，並放在使用者已經在看結果的地方，例如預覽旁邊。拉伸與標籤在效果能隨滑桿移動時顯示時效果最好。
- 在使用者會回到的數值上加入檔位，例如中性值、關閉或標準預設，使他們無需精確操作即可停在那裡。
- 從 `ValueCommitted` 儲存，而不要從 `ValueChanged` 儲存。拖曳會在每一步觸發 `ValueChanged`；在放開時提交，只會為所選的值送出一次要求。
- 選擇與該設定所需精度相符的 `SmallChange`，並讓 `LargeChange` 涵蓋一個有用的跳躍（預設的範圍十分之一是個好的起點）。

**避免**

- 將此控制項用於必須精確輸入的值，例如稅率或數量。軌道只能近似數值，使用者隨後只能逐鍵修正。
- 在同一清單或表格中堆疊多個彈性滑桿。重複的拉伸與搖擺會把注意力從它們所屬的資料上分散開。
- 隱藏讀數、只依賴數值標籤來記錄使用者必須記下的值。數值標籤只在滑桿移動或被按鍵變更時出現，因此穩定下來的數值會變得看不見。
- 刻度間距小於約 14 px。較近的刻度會勝出，另一個則無法用指標到達。
- 把需要空間的文字放在 `StartIcon` 或 `EndIcon` 中。這些位置會隨拉伸移動，長文字會被裁切或擠壓軌道。

### 常見問題

**為什麼我按鍵時滑桿沒有落在刻度上？** 按鍵以 `SmallChange` 為步長移動，從不貼齊到刻度。只有當某一步恰好落在刻度的數值上時，鍵盤才能到達該刻度。對於 0、25、50、75 與 100 的刻度，請設定 `SmallChange="25"`，或使用能將刻度間距均分的 `SmallChange`。

**如何隱藏讀數並讓數值不礙事？** 設定 `IsValueVisible="False"` 即可隱藏標籤旁的讀數。拖曳滑桿或以按鍵變更時數值標籤仍會出現，因此調整過程中數字始終可用，使用者停止操作後則隱藏。

**一個彈性滑桿能選取範圍嗎？** 不能。`ElasticSlider` 只有一個 `Value`，沒有 `IsRange`。應使用設定了 `IsRange="True"` 的 `Slider`，並讀取 `LowerValue` 與 `UpperValue`。

**為什麼在端點處按鍵不觸發 `ValueCommitted`？** 在極限處數值無法改變，因此控制項只會把軌道朝按鍵方向彈一下。數值沒有變化，也就沒有需要提交的內容；在提交時儲存的監聽器會正確地什麼都不做。

**為什麼在減少動態效果的裝置上沒有拉伸或搖擺？** 拉伸、搖擺和檔位彈出只在完整動態等級下執行。在減少等級下，滑桿直接到達目標值，標籤只淡入。應在完整等級下測試控制項的手感，並確認減少等級下數值仍能正確讀取。

## API 參考

<!-- api: ElasticSlider -->

<!-- api: SliderMark -->

## 無障礙

- `Label` 為滑桿把手命名。滑桿朗讀的數值是格式化後的 `Value`，因此回傳 `40%` 的 `ValueFormatter` 會被讀作 `40%`。沒有標籤時滑桿把手沒有名稱，因此應保持標籤可見。
- 控制項本身是一個分組，滑桿把手是 Tab 順序中唯一的停靠點。軌道列、數值標籤、讀數與刻度標籤都不是獨立的停靠點。
- 刻度標籤對輔助技術隱藏。點按標籤是指標操作的捷徑；鍵盤可透過 `SmallChange` 或 `LargeChange` 步進到相同的數值。
- 數值標籤與檔位彈出僅是視覺效果。朗讀的數值承載著相同的數字，因此標籤不會為螢幕閱讀器使用者增加任何所需資訊。
- 焦點框在按鍵後出現，而不是在指標按下後出現，因此滑鼠使用者不會在滑桿把手上看到一個焦點框。

### 鍵盤

滑桿把手透過 `Tab` 取得焦點。控制項本身不接受焦點。

| 按鍵 | 操作 |
|---|---|
| `Tab` / `Shift+Tab` | 將焦點移入或移出滑桿把手；停用的滑桿會被跳過 |
| `Right` / `Up` | 依 `SmallChange` 增加數值 |
| `Left` / `Down` | 依 `SmallChange` 減少數值 |
| `Shift` + 方向鍵、`PageUp` / `PageDown` | 依 `LargeChange` 增加或減少 |
| `Home` / `End` | 移到 `Minimum` 或 `Maximum` |
| 在極限處按方向鍵、`PageUp`、`PageDown`、`Home` 或 `End` | 數值保持不變；軌道朝按鍵方向拉伸後彈回 |

## 動態效果

- 越過任一端時，軌道會如橡皮筋般拉伸，最多 26 px，並依放開時的速度彈回。按住期間，滑桿把手會膨脹。
- 數值標籤懸掛在滑桿把手上方，並以柔和的彈簧跟隨它。其傾斜角隨滑桿把手的速度變化，因此快速拖曳時傾斜更多，最多 11 度。
- 滑桿把手卡入檔位時，軌道上的刻度點會彈出一次，放大到最多兩倍，並在 0.34 s 內穩定。
- 來自拖曳之外的數值變化（例如按鍵、繫結的值或點按的刻度標籤）會以彈簧動畫從滑桿把手目前在螢幕上的位置移動過去。
- 減少動態效果會移除拉伸、搖擺和檔位彈出。滑桿把手直接到達目標值，標籤僅淡入。關閉動態效果時直接跳到最終狀態。

| 變化 | 完整 | 減少 | 關閉 |
|---|---|---|---|
| 越過端點拖曳 | 軌道最多拉伸 26 px，並以放開時的速度彈回（0.5 s，彈性 0.42） | 無拉伸；滑桿把手停在極限處 | 立即完成 |
| 拖曳時的滑桿把手 | 以靈敏的彈簧放大到 1.28 倍 | 立即放大 | 立即完成 |
| 數值標籤 | 以柔和彈簧跟隨滑桿把手，並最多搖擺 11 度 | 停留在滑桿把手處，並在 0.15 s 內淡入 | 立即完成 |
| 讀數 | 數值標籤跟隨滑桿把手時淡出 | 立即切換 | 立即完成 |
| 檔位彈出 | 刻度點在 0.34 s 內從 1 倍變為 2.1 倍再回到 1 倍 | 無彈出 | 無彈出 |

## 相關元件

- `Slider` 用於安靜的表單控制項、帶兩個滑桿把手的範圍，或動態效果會重複出現的滑桿清單。
- 使用者需要鍵入精確數字時使用 `NumberField`。
- `Progress` 用於顯示但不由使用者變更的數值。

| 元件 | 適用情境 |
|---|---|
| `Slider` | 設定位於表單中、數值是範圍，或同一檢視中有多個滑桿重複出現 |
| `NumberField` | 使用者必須鍵入或步進一個精確數字，並帶有單位與界限 |
| `ScrubInput` | 數值透過拖曳標籤本身來變更，沒有軌道 |
| `Progress` | 數值向使用者顯示，並由應用程式設定 |
| `Switch` | 設定只有兩種狀態，沒有中間值 |
