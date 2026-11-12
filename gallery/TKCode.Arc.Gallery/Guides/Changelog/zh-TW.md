---
title: 更新日誌
source-hash: d98231ccb287
---

# 更新日誌

TKCode.Arc 的所有重要變更，最新的在前。版本遵循[語意化版本](https://semver.org/)。從 1.0.0 起，破壞性 API 變更只在新的主版本中出現；更早的 `-preview` 版本在各發行版本之間可能變更 API。同一份清單也保存在儲存庫的 `CHANGELOG.md` 中。

每次發行附帶的 NuGet 套件：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Foundation`（0.3.2 起）、`TKCode.Arc.Fonts` 、`TKCode.Arc.Icons.Lucide`、`TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts` 、`TKCode.Arc.Special`、`TKCode.Arc.Workflow`、`TKCode.Arc.Dock`（0.4.0-preview.2 起）、`TKCode.Arc.Themes.Pixel`、`TKCode.Arc.Themes.Corners`、`TKCode.Arc.Mobile`、`TKCode.Arc.Terminal`、`TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native`、`TKCode.Arc.Audio.Browser`、`TKCode.Arc.Templates`（0.4.0-preview.6 起）、`TKCode.Arc.Pdf`、`TKCode.Arc.Pdf.Native` 和 `TKCode.Arc.Pdf.Browser`（1.0.0 起），`TKCode.Arc.Video`、`TKCode.Arc.Video.Native` 和 `TKCode.Arc.Video.Browser`（1.1.0-preview.1 起）。

## 1.1.0-preview.5（2026-11-12）

`TKCode.Arc.Special` 的 Docks and tabs 群組重新建構（`OrbitMenu`、`LiquidTabBar`、`ShareSheet`、`ActionMorph` 與 `MorphNav` 的 API 會變更，遷移方式見下文；`ToolDock` 為新增），`AvatarUpload` 移至 Editors 群組，來自應用程式的回饋（#107–#112）新增 `WindowCaptionButtons`、設定頁選項、內容與媒體權杖，並為 `WaterfallPanel` 與 `TilePanel` 加入自動尺寸。從 1.1.0-preview.4 起的破壞性變更僅限於這五個 Special 控制項。安裝時使用 `--version 1.1.0-preview.5`（或 `--prerelease`）。

### 新增

- **`WaterfallPanel` 與 `TilePanel` 自行排列同一範本的項目**（[#112](https://github.com/AIDotNet/TKCode.Arc/issues/112)，源自 [#86](https://github.com/AIDotNet/TKCode.Arc/issues/86)）：`WaterfallPanel.ItemSizing="Varied"` 會為每個子項目指定高度（以列數計的寬度），介於 `MinItemSize` 與 `MaxItemSize` 之間，而不是量測其內容，因此由同一範本建立的卡片仍能組成瀑布流牆；未設定的上下限跟隨欄寬。`TilePanel.IsAutoSpan` 會讓沒有設定跨度的子項目取得 1×1、1×2、2×1 或 2×2，放在第一個空格並讓較大的磚塊彼此分開；同時設定 `IsAutoFill` 與 `Rows` 時，自動磚塊會調整大小以覆蓋整個 `Columns` × `Rows` 格線。兩個面板都依子項目的索引與新的 `Seed` 屬性決定尺寸，配置版面時絕不使用隨機值：相同的項目、種子值與格線永遠得到相同結果，調整大小也不會重新洗牌。預設值（`Content` 尺寸與關閉的自動跨度）保留現有版面。
- **`InlineEdit.ReserveActionSpace`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`False` 會取消單行在文字後保留的 66 px，供儲存與取消按鈕使用，因此方塊貼合文字，標籤或徽章可以接在名稱後面。靜止時框外不繪製任何東西（沒有鉛筆提示、儲存中的旋轉指示或結果標記）；編輯時按鈕覆蓋其後的內容，且控制項會繪製在同一面板中較後面的兄弟之上。編輯時保留區不會回來，因此按鈕出現時文字與標籤都不會跳動。多行欄位仍保留其 38 px。預設值（`True`）不變。
- **內容寬度與媒體權杖**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`ContentMaxWidthSM` / `MD` / `LG` / `XL`（640 / 760 / 880 / 1120）限制頁面的內容欄寬（閱讀欄或單一表單、設定頁、文件頁、全寬區段），`MediaSizeSM` / `MD` / `LG` / `XL`（64 / 128 / 192 / 256）設定正方形媒體的大小（列中的縮圖、卡片預覽、QR code 或封面圖、大型圖片預覽）。以 `{arc:ArcDesignTokenResource ContentMaxWidthMD}` 使用，並可像任何 ArcDesign 權杖 一樣覆寫。兩者都附加在 `ArcDesignTokenKind` 之後，因此既有成員的數值不變。
- **`AvatarSize.LargePlus`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：介於 `Large`（48）與 `XLarge`（88）之間的 64 px 頭像，使用 `TypeLG` 首字母，以及距角落內縮 2 px 的 13 px 狀態點。它附加在列舉之後，因此既有成員的值不變；`AvatarGroup` 會將它繪製為 `Large`。
- **`SettingsSection.BadgeTone`**（[#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)，`TKCode.Arc.Blocks`）：以 `BadgeTone` 為區段的導覽徽章上色（「Needs attention」用 `Warning`，失敗的檢查用 `Danger`）。預設值 `Neutral` 保留現有外觀。
- **`SettingsRow.Details` 與 `SettingsRow.Leading`**（[#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)，`TKCode.Arc.Blocks`）：`Details` 可放置描述下方的任何內容，例如狀態行、錯誤或一排標籤（文字為小字且淡色）。`Leading` 可放置任何內容取代圖示方塊，例如 `Avatar`；它保留自己的尺寸，`LabelWidth` 會將它計算在內。`Icon` 不變。
- **`SettingsPage.SelectedValue`**（[#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)，`TKCode.Arc.Blocks`）：新的 `SettingsSection.Value` 可識別區段（列舉成員或路由鍵），頁面的雙向 `SelectedValue` 即為被選取區段的 `Value`，因此顯示的區段可以繫結到檢視模型屬性。設定 `SelectedValue` 會選取相符的區段。沒有 `Value` 的區段仍像以前一樣回報自己；`SelectedValueBinding` 則以其他方式讀取值。
- **`RadioGroup`、`RadioCards` 與 `ToggleGroup` 的公開項目容器**（[#109](https://github.com/AIDotNet/TKCode.Arc/issues/109)）：`RadioGroupItem`、`RadioCard` 與 `ToggleGroupItem`（以及其基底 `TogglesItem`，只有程式庫本身會衍生它）皆為公開。資料繫結的項目運作方式與 Avalonia 的 `ListBoxItem` 相同：`arc|RadioGroupItem` 上的樣式或群組的 `ItemContainerTheme` 可以繫結項目的 `Description`、`Icon`、`Meta`、`IsEnabled` 與 `AutomationProperties.Name`，因此項目不必是 `RadioOption` 或 `ToggleGroupOption` 也能取得這些功能。每個容器都有自己的權杖（`RadioGroupItem.MinRowHeight`、`RadioCard.GlyphSize`，以及 `ToggleGroupItem.PressScale`、`PressDownDuration` 與 `PressUpDuration`，用於先前固定不變的按壓效果）；預設值保留現有外觀，既有的 `RadioGroup`、`RadioCards` 與 `ToggleGroup` 權杖 不變。
- **`WindowCaptionButtons`**（[#107](https://github.com/AIDotNet/TKCode.Arc/issues/107)）：最小化、最大化/還原與關閉，成為獨立的控制項，適用於內容延伸至頂端的視窗（`IsTitleBarVisible="False"` 的 `ArcWindow`）。可放在你自己的標題區任何位置：它們驅動所在的視窗，最大化時顯示還原，遵循 `CanResize`、`CanMaximize` 與 `CanMinimize`，驅動視窗時帶有系統角色（Windows 11 的 Snap Layouts），並保留已在地化的工具提示。`ShowMinimizeButton`、`ShowMaximizeButton` 與 `ShowCloseButton` 可隱藏按鈕，`IsPreview` 則只繪製按鈕而不驅動視窗。`TitleBar` 現在改用此控制項繪製其標題列按鈕；當 `CaptionButtons` 為 `System` 或 `None` 時，它會放棄這些按鈕的角色，讓視窗只保有一組按鈕。
- **`PageHeader.IsTitleBar`**（[#107](https://github.com/AIDotNet/TKCode.Arc/issues/107)，`TKCode.Arc.Blocks`）：`True` 讓頁首（而非內容）成為視窗的標題列：拖曳其空白部分、標題或說明會移動視窗，連按兩下則會最大化；麵包屑、精簡標題、操作、中繼資料列與標籤頁仍保留其點擊。在 Windows 上，頁首即為系統標題列，這些部分仍是使用者內容。`False`（預設值）不改變任何東西。
- **`ArcOverlayHost` 用於應用程式**（[#107](https://github.com/AIDotNet/TKCode.Arc/issues/107)，`TKCode.Arc.Core`）：文件不再說應用程式不需要它。將應用程式的內容放入覆蓋層主機，標題列放在旁邊，從內容開啟的對話方塊、抽屜或面板只會覆蓋主機，絕不會覆蓋標題列及其標題按鈕（見 Window caption buttons 頁面）。
- **`AvatarUpload` 以檔案開頭的位元組檢查檔案，並儲存編碼後的圖片**：挑選、拖放或載入的檔案，其類型先以開頭位元組判斷，再看檔名（更名為 `.png` 的 PDF 會被拒絕，檔名古怪的照片則不會），且在大小檢查之後進行；即使串流無法說明長度，讀取也會在 `MaxFileSize` 停止。新的 `Accept` 可縮小類型範圍（MIME 類型、`image/*` 或副檔名），選取器也會跟著調整；`LoadAsync(byte[], name)` 接受貼上的圖片或相機拍攝。`AvatarUploadRequest` 新增 `Bytes`、`ContentType`、`SuggestedFileName` 與 `OpenStream()`：儲存的正方形圖片依新的 `OutputFormat` 編碼（`Auto` 保留 PNG 格式，未知類型的圖片以 PNG 儲存，其他圖片以 JPEG 儲存；可選 `Png`、`Jpeg`），以及 `JpegQuality`（92），JPEG 以白色為底。沒有圖片時 `DisplayName` 會顯示首字母，`AllowRemove` 會隱藏移除按鈕，結果與以四分之一為單位的進度會透過即時區域播報，拖曳超出取景區邊緣時會產生阻力並彈回（儲存的裁切永遠不包含拉伸的部分）。預設值不變。文件新增了範例照片與失敗情況的範例。

- **`ToolDock`**（`TKCode.Arc.Special`，Docks and tabs）：用於畫布式編輯器的浮動工具或模式列。被選取的工具會位於色調上，色調以彈簧效果從上一個工具滑過來；名稱與快捷鍵提示標籤會滑過滑鼠或鍵盤焦點所在的工具（觸控時永不出現）；群組（`ToolDockItem.Items`）會從列中展開出其成員的托盤，並包在一條細線框內；徽章會滾動其計數，數字為 0 時消失；`CanReorder` 讓人們可用拖曳或 Alt+左/右鍵重新排列工具（`Reordered`）。`SelectedValue` 為雙向，`ItemChosen` 回報每次的選取；此列為單一 Tab 停駐點，並採用工具列的鍵盤模式。

### 變更

- **`TilePanel.IsAutoFill` 也從側邊與下方填補空隙**（[#112](https://github.com/AIDotNet/TKCode.Arc/issues/112)）：磚塊向右與向下成長後，每個仍為空的空隙都會提供給周圍的磚塊，因此右側或下方的鄰居也能覆蓋過去。磚塊仍不會移動、重疊或縮小，跨度上限依然有效。
- **`AvatarUpload` 從 `TKCode.Arc.Special` 移至 `TKCode.Arc`**（圖庫中的 Editors 群組，位於 `SignaturePad` 與 `FileDropzone` 之間）：命名空間、類別與其 API 不變，因此程式碼的編譯方式與以前相同，而 `UseArcControls()` 現在會註冊它（`UseArcSpecial()` 不再註冊）。其文件頁面從 Special › Inputs 移至 Components › Editors。
- **圖庫中的 Special › Navigation 現在稱為「Docks and tabs」**，列出 Share sheet、Action morph、Morph nav、Tool dock、Liquid tab bar 與 Orbit menu，`MagnifyDock` 也從 Playful 移入此處。原始碼資料夾、命名空間與 slug 不變。
- **破壞性變更：`OrbitMenu` 變成按下—拖曳—放開的選單。** 按下觸發鈕會展開圓弧；拖曳向某個動作並放開即觸發它（指標離開按鈕一小段距離後，朝向動作即已足夠），點按則讓圓弧保持展開，以供懸停與點擊。一個透鏡會滑到目標下方，靠近指標的動作會被吸向它，按鈕的標記會由三個點變成叉號，動作執行後變成勾號；一個膠囊會標示目標，指標不在任何動作上時顯示 `CancelText`，然後顯示該動作的 `DoneText` 1.4 s，若 `Announces` 開啟則會播報。新增：`Direction`、`Spread`、`CancelText`、`Announces`、`OrbitMenuAction.DoneText` 與 `Tone`（`OrbitMenuActionTone.Danger`）、`OrbitMenuActionEventArgs.Item`。遷移：`StartAngle` / `SweepAngle` 改為 `Direction`（圓弧的中央）與 `Spread`（其寬度；舊的預設值為 `Direction="-90" Spread="180"`）；`Glyph`、`ClosesOnInvoke` 與 `OrbitMenuAction.FocusRingBrush` 已移除（圓弧一律收合並確認，請處理 `ActionInvoked`；鍵盤焦點以透鏡顯示）；`Radius` 預設為 104；觸發鈕為 56 px，動作為 46 px；動作不再有工具提示。
- **破壞性變更：`LiquidTabBar` 以透鏡選取。** 選取項目是一個會滑向所選標籤的膠囊，途中採用其寬度而不拉伸，並以反色繪製它所覆蓋的內容，因此線條圖示會變成實心（`ActiveGlyph` 可繪製另一個）；舊標籤的文字立即消失，新標籤的文字則在透鏡到達時出現；透鏡會朝向被懸停的標籤傾斜，且可以被拖曳或輕彈。新增：`Labels`（`Active`、`Always`、`None`）、`LensBackground`、`LensForeground`，以及 `LiquidTabBarItem` 上的 `Value`、`Label`、`Glyph`、`ActiveGlyph`、`Badge`（變更時其數字會滾動）與 `BadgeLabel`。遷移：`Viscosity` 已移除；為每個項目設定 `Glyph` 與 `Label`（沒有圖示的項目為文字標籤）；`SelectedValue` 回報項目的 `Value`；滑鼠在放開時選取；上與下方向鍵不再移動選取；若要保留強調色透鏡，請將 `LensBackground` / `LensForeground` 設為 `Accent` / `AccentForeground`；`ItemsPanel`、`Padding` 與 `CornerRadius` 不再使用。
- **破壞性變更：`ShareSheet` 從自己的按鈕擴展為分享面板。** 單一形狀會從按鈕彈出為一個面板，包含項目的標題、連結與複製，一份存取層級清單，帶有等待中、完成與失敗狀態的頻道按鈕（`ChannelHandler`），以及近期聯絡人，其頭像會飛入「Send to」列；傳送會執行 `SendHandler`，失敗時顯示錯誤並保留面板原樣，成功時收縮為附有復原的確認卡片。寬度最多 520 px 時，面板會從底部升起，覆蓋在變暗的頁面上，並可向下拖曳關閉（`SheetOnPhones`），而 `Align` 決定哪一側保持不動。遷移：觸發鈕不再以 `Content` 設定（請設定 `Label` 與 `Glyph`）；`ShowAt`、`Presentation`、`Subtitle`、`Actions`、`ActionInvoked`、`Shared`、`ClosesOnShare` 以及標題與複製標籤屬性已移除（請改用 `Title`、`Labels`、搭配 `ChannelHandler` 的 `Channels`，以及 `SendHandler` 或 `Sent`）；`People` 持有 `ShareSheetPerson`；`LinkCopied` 傳遞 `ShareSheetLinkCopiedEventArgs`。
- **破壞性變更：`ActionMorph` 從按鈕變為選單、表單與勾號。** 56 px 的按鈕會先展開為一個簡短的建立項目選單（每列有說明與字母快捷鍵，並帶有滑動的反白），再展開為所選動作的表單（單行或多行欄位、可選的選項，可按 Enter 或 Ctrl/⌘+Enter 送出），最後摺回按鈕並以繪製的勾號收尾；加號會在 `ResetAfter`（1.6 s）後回來。`SubmitHandler`（或 `Submitted`）負責儲存，空白送出會抖動，失敗的送出會保留草稿並顯示錯誤，每個動作都會記住其未送出的文字，`Corner` 決定表面展開的方式。遷移：請在 `Actions` 中將每個面板描述為 `ActionMorphAction`（`Title`、`Placeholder`、`IsMultiline`、`Choices`、`SubmitLabel`、`SuccessLabel`、`EmptyHint`），而非以內容描述；`Glyph`、`Variant`、`CollapsesOnOutsidePress`、`ShowsCloseButton` 與 `ActionMorph.Closes` 已移除；此控制項在佈局中佔用 56 × 56，並將其表面繪製於鄰近項目之上，因此請將它放在 `Panel` 中位於角落的最後一個。
- **破壞性變更：`MorphNav` 是網站導覽列。** 單一表面容納品牌、可帶有滑動反白的項目、搜尋與一個主要動作；帶有連結的項目會讓導覽列展開為其面板（連結分為一欄或兩欄，可選的功能區如 `MorphNavStory` 與頁尾），相鄰項目的面板會從其側邊滑入，懸停 60 ms 後開啟、180 ms 後關閉，搜尋從導覽列本身開啟（空白查詢時顯示建議，最多六個排序後的結果），`IsCompact` 讓導覽列更緊湊，而當導覽列窄於 `CollapseBelow`（720 px）時，項目會收合為單一的選單項目。遷移：`MorphNavSection` 是 `MorphNavItem`（`Header` → `Label`，另加 `Value`）；`MorphNavLink.Text` / `Glyph` / `Tag` 是 `Label` / `Icon` / `Href`；`Title` / `Mark` 是 `BrandName` / `BrandMark`；`CurrentLink` 是 `Current`（項目的 `Value`）；`LinkInvoked` 是 `Navigate`（`MorphNavNavigateEventArgs`）；`IsOpen`、`Open()`、`Close()` 與 `Toggle()` 是 `Open(value)`、`ClosePanel()` 與 `OpenValue`；導覽列的 `Footer` 移至 `MorphNavItem.Footer`；`PanelWidth` 與 `CloseLabel` 已移除。

### 修復

- **Avatar 狀態點大小**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：圓點現在依文件所述跟隨頭像的尺寸（`Small` 為 8 px 加 1.5 px 的圓環，`Medium` 為 10 px，`Large` 為 12 px，`XLarge` 為 14 px），忙碌橫槓在所有尺寸下都橫跨圓點。先前每個頭像都繪製 10 px 的中型圓點：舊範本中的區域值隱藏了尺寸樣式。
- **`InlineEdit` 多行保留空間**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：多行欄位現在如文件所述為其堆疊的儲存與取消按鈕保留 38 px，而非 66 px，因此文字換行的位置會再往後 28 px。範本本身的內距曾隱藏多行樣式。
- **`OrbitMenu` 首次開啟**：菜單第一次開啟時，動作會先短暫顯示在圓弧上的位置，然後落回觸發鈕再重新展開；現在它們只會從觸發鈕離開一次。

## 1.1.0-preview.4（2026-11-11）

來自遷移到 Arc 的應用程式的回饋（#102–#110）：`PageHeader`、`Dialog`、`CommandPalette`、`Avatar`、`EmptyState`、`InlineEdit`、`CodeBlock` 與 `Tooltip` 新增了選項，並修正了 `Table`、`ToggleGroup`、`SettingsGroup`、Mobile `ListGroup`、強制回應覆蓋層、`Gauge` 與 `ArcWindow` 的問題。與 1.1.0-preview.3 相比沒有破壞性變更。安裝時使用 `--version 1.1.0-preview.4`（或 `--prerelease`）。

### 新增

- **適合設定頁與側欄的 `PageHeader`**（[#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)，`TKCode.Arc.Blocks`）：`IsHeaderOnly="True"` 只顯示頁首，沒有標籤頁、分隔線與捲動區，用作頁面頂端的標題與操作列；`Size="Small"` 是精簡頁首（`TypeXL` 標題，周圍留白較小）；`PageHeaderAction.IsVisible="False"` 隱藏操作的按鈕與選單列，而不是讓它們變淡。
- **`Dialog.ShowHeader`**（[#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)）：設為 `False` 時隱藏標題列（標題、說明、關閉按鈕與分隔線），適合自帶標題的對話方塊。`Title` 仍是無障礙名稱。
- **由應用程式提供結果的 `CommandPalette`**（[#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)，`TKCode.Arc.Blocks`）：在面板或對話方塊上設定 `IsFilteringEnabled="False"`，會依原樣列出 `ItemsSource`，適合自行篩選與排序結果的應用程式。`CommandPaletteDialog` 新增 `Query`（雙向）與 `ActiveItem`，在對話方塊第一次開啟前就能讀取。從程式碼設定的查詢會把游標放在結尾。
- **更多頭像上線狀態**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`AvatarStatus` 新增 `Busy`（危險色，圓點上帶一條橫槓）、`Away`（警告色）與 `Connecting`（以不透明度呼吸的空心圓環，關閉動態效果時靜止）。每種狀態都是頭像無障礙名稱的一部分。
- **`EmptyState.Size`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`Small` 是用於清單欄與卡片的精簡空狀態（32 px 圖示區塊，上下各 16 px，間距較緊湊），在任何視窗寬度下都生效。
- **`InlineEdit.Labels`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：`InlineEditLabels` 物件可在地化螢幕閱讀器聽到的所有文字：「Saving …」與「… saved」播報、儲存與取消按鈕的名稱以及說明文字。英文預設值不變。
- **程式碼中的 diff 著色**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：語言為 `diff` 或 `patch` 時，`CodeBlock`、Markdown 程式碼圍欄與 RTF 編輯器的程式碼區塊會把新增行顯示為成功色、刪除行顯示為危險色，檔案標頭與區塊標頭顯示為次要文字顏色。之前這些行借用了數字色與字串色。文件中說明了 `text` 與 `plaintext` 顯示為純文字。
- **`Tooltip.ShowOnDisabled`**（[#110](https://github.com/AIDotNet/TKCode.Arc/issues/110)）：在停用的控制項上顯示工具提示，例如說明按鈕為何不能使用。停用的控制項收不到指標輸入，因此工具提示改為追蹤指標是否位於控制項範圍內。之前 Tooltip 文件說這已經可以使用，實際上並不能。

### 修正

- **小尺寸的 `Gauge`**：數字現在隨圓環縮放（最大尺寸時為 `Type3XL`），因此在小錶盤上也不會超出圓環；位於圓環中心的是數字的墨跡而不是行框，因此數字不再偏上。
- **Windows 上 `ArcWindow` 的上邊框**（[#102](https://github.com/AIDotNet/TKCode.Arc/issues/102)）：用戶端區域延伸到標題列時，DWM 不繪製視窗頂端的邊框線，因此視窗在 Windows 上處於一般狀態時會自行以 `Border` 顏色畫出這條線（`:top-edge` 虛擬類別）。四條邊一致；用 `ArcWindow:top-edge /template/ Border#PART_Root` 上的樣式可以移除它。
- **未配置版面時更換資料來源的 Table**（[#103](https://github.com/AIDotNet/TKCode.Arc/issues/103)）：`Table` 在隱藏或不在視覺化樹狀結構上時更換了 `ItemsSource`，之後切換主題（例如頁面重新顯示時）不再擲回 `ArgumentOutOfRangeException`。列會等到下一次配置版面再讀取新的資料來源。
- **使用 `ListItem` 範本的 `ListGroup`**（[#104](https://github.com/AIDotNet/TKCode.Arc/issues/104)，`TKCode.Arc.Mobile`）：當 `ItemTemplate` 的根是 `arcm:ListItem` 時，這一列直接作為容器，不再套第二層，因此可以在範本中繫結 `Title`、`Subtitle`、`Command` 等列屬性。
- **使用自訂 `SelectedValueBinding` 的 `ToggleGroup`**（[#105](https://github.com/AIDotNet/TKCode.Arc/issues/105)）：`SelectedValues`、`SelectedValue` 與 `ValueChanged` 事件回報的是繫結取出的值而不是項目本身，雙向繫結的 `SelectedValue` 在控制項顯示時也不再被覆寫。
- **由 `ItemsSource` 產生列的 `SettingsGroup`**（[#106](https://github.com/AIDotNet/TKCode.Arc/issues/106)，`TKCode.Arc.Blocks`）：第一列上方不再多一條細線，範本產生的列上設定的 `ColumnSpan` 在第一次配置版面時就生效。
- **邏輯樹狀結構中的強制回應內容**（[#108](https://github.com/AIDotNet/TKCode.Arc/issues/108)）：`Dialog`、`Drawer` 或 `BottomSheet` 的內容在 `GetLogicalDescendants()` 中只出現一次（位於覆蓋層之下），不再出現兩次。

## 1.1.0-preview.3（2026-11-10）

效能更新：應用程式可以限制持續循環重繪的頻率。與 1.1.0-preview.2 相比沒有破壞性變更。安裝時使用 `--version 1.1.0-preview.3`（或 `--prerelease`）。

### 新增

- **`ArcMotion.MaxFrameRate`**（`TKCode.Arc.Core`）：可繼承的附加屬性，限制元素之下每個 `LiveLoop`（吉祥物、骨架畫面脈動、閃光、即時指示點）每秒最多推進的次數。每次推進傳入自上次以來經過的時間，因此動作速度不變，略過的影格不繪製任何內容。預設值 0 與之前一樣每影格推進。在始終顯示的小視窗上設定它，例如 `arc:ArcMotion.MaxFrameRate="24"`，緩慢的裝飾動態效果就不會再以顯示器的完整影格率重繪視窗。

## 1.1.0-preview.2（2026-11-10）

NativeAOT 修正：使用 `Sidebar` 的應用程式發布時不再出現修剪警告。與 1.1.0-preview.1 相比沒有破壞性變更。安裝時使用 `--version 1.1.0-preview.2`（或 `--prerelease`）。

### 修復

- **NativeAOT 下的 `Sidebar`**（[#100](https://github.com/AIDotNet/TKCode.Arc/issues/100)）：應用程式使用 `Sidebar` 時，`TKCode.Arc.Blocks` 不再產生修剪警告（IL2026 / IL3053）。新增的 `LabelMemberBinding`、`KeyMemberBinding`、`IconMemberBinding`、`BadgeMemberBinding`、`IsEnabledMemberBinding` 與 `ChildrenMemberBinding` 以編譯繫結對應 `ItemsSource` 的資料，例如 `LabelMemberBinding="{Binding Title, DataType={x:Type local:MenuNode}}"`。字串形式的 `*Member` 路徑仍可使用，但透過反射；修剪與 NativeAOT 應用程式請改用 `*MemberBinding` 屬性。

## 1.1.0-preview.1（2026-11-10）

1.1.0 的首個預覽版：`TKCode.Arc` 新增控制項，`TKCode.Arc.Special` 新增分組，新增 58 個區塊，影片套件發布到 NuGet，並且無需等待 Arc 新版即可在 Avalonia 12 內升級。與 1.0.0 相比沒有破壞性變更。安裝時使用 `--version 1.1.0-preview.1`（或 `--prerelease`）；本版本新增的 API 在 1.1.0 之前仍可能變化。

### 新增

- **影片發布到 NuGet**：發布 `TKCode.Arc.Video`、`TKCode.Arc.Video.Native` 和 `TKCode.Arc.Video.Browser`。`VideoPlayer` 和 `VideoSurface` 在桌面上透過 Media Foundation、AVFoundation 或 GStreamer 播放，在 WebAssembly 中使用瀏覽器的 video 元素（`UseArcVideo()`、`ArcVideoNative.Use()`、`ArcVideoBrowser.UseAsync()`）。
- **`TreeSelect`**（[#80](https://github.com/AIDotNet/TKCode.Arc/issues/80)）：基於樹的選擇框，可單選一個節點或勾選多個，支援三態核取方塊、決定回傳哪些節點的 `CheckStrategy`、保留每個符合項路徑的搜尋、標籤，以及可容納數千節點的虛擬化列。
- **`Sticky`**（[#84](https://github.com/AIDotNet/TKCode.Arc/issues/84)）：附加屬性，讓捲動頁面中的任意元素捲到頂端後固定在那裡，類似 CSS `position: sticky`，支援位移、`IsStuck` 狀態和 `StuckChanged` 事件。
- **`BackToTop`**（[#67](https://github.com/AIDotNet/TKCode.Arc/issues/67)）：清單或動態消息捲動超過門檻後出現的圓形按鈕，點擊後平滑捲回頂端；可自行放置，也可用 `arc:BackToTop.IsAttached` 附加到角落。
- **更多控制項**：`Rating`（半星、清除、唯讀、自訂形狀）、`ToggleGroup` 與 `SlotText`、`RangeHistogramSlider` 與 `RulerSlider`、`OnboardingChecklist`，以及 `SplitBar`（圖表）、`MetricTicker`（卡片）和 `PresenceStack`（頭像）。
- **特殊元件**（`TKCode.Arc.Special`，僅根據名稱獨立設計）新增分組：玻璃（`GlassCard`、`GlassMenu`、`GlassSegmented`、`GlassTabBar`）、導覽（`MorphNav`、`LiquidTabBar`、`OrbitMenu`、`ActionMorph`、`ShareSheet`）、相片（`ProductGallery`、`LightboxGallery`、`PhotoGrid`、`CoverFlow`）、資料（`BookingPill`、`ActivityRings`、`TimeDial`、`DateReel`）、小工具（`KeycapKeyboard`）、輸入（`EmojiPicker`、`AvatarUpload`、`ConfirmPopover`、`UndoToast`）、出版（`MegaMenuMorph`、`TocProgress`、`ShareAccess`），講解動畫 `DnsResolution`、`ModelTraining` 和 `BranchGraph`，以及 `SandText`、`TypewriterTerminal`、`PrizeWheel`、`MagnifyDock` 和 `ImageTrail`。
- **區塊**（`TKCode.Arc.Blocks`）：行銷區塊（`HeroSection`、`FaqSection`、`ContactSection`、`StatsBand`、`BlogGrid`、`ComparisonTable`、`ChangelogFeed`、`LogoMarquee`、`HeroSignup`、`TeamShowcase`、`StudioPerspectives`、`ScrollStory`、`SpotlightGrid`、`FeatureBento`）；AI（`AgentRun`、`ResearchRun`、`AiInlineEdit`、`AgentTasks`、`AgentApprovals`、`AgentRoutines`、`AgentOrchestrator`、`AgentBuilder`、`AgentGallery`、`McpServers`、`KnowledgeBase`、`EvalSuite`、`AiInsightCard`、`AiFeatureLaunch`、`AiSearchBar`、`AiSetupWizard`、`CatchUp`、`SmartScheduler`）；支援（`SupportConversation`、`SupportWidget`、`InboxTriage`、`FeedbackBoard`、`HelpCenter`、`FeatureVoting`、`ActivityFeed`）；商務與金融（`WalletCard`、`SpendAlert`、`InvoiceStudio`、`DeliveryTracker`、`ProductListing`、`ProductDetail`、`SeatMap`）；分析與專案（`CallInsights`、`LeadScoring`、`AccountHealth`、`GoalTracker`、`PivotBuilder`、`TaskBreakdown`）；應用程式外殼與驗證（`DocsSidebar`、`InboxSidebar`、`SelectionToolbar`、`TwoFactorSetup`、`OnboardingSurvey`、`MagicLinkSignIn`）。
- **`Sidebar.ExpandedWidth`**（區塊，[#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)）：側邊欄展開時的寬度，收合動畫從這裡彈出並彈回。

### 變更

- `GhostMascot`、`CubeMascot`、`AiMascot`、`LiquidTabBar`、`AiSpark` 和 `RadarChart` 每幀的記憶體配置大幅減少，外觀不變。

### 修復

- 各個套件不再把 Avalonia 鎖定為一個確切版本（[#97](https://github.com/AIDotNet/TKCode.Arc/issues/97)）：現在接受 Avalonia 12.1.5 及任何更高的 12.x 版本，應用程式可以在 12 版本內升級 Avalonia，無需等待 Arc 發布新版。
- 減少動態效果時載入指示器不再凍結（[#93](https://github.com/AIDotNet/TKCode.Arc/issues/93)）：旋轉圖示和流動線條保持靜止，改為透明度呼吸。
- `Dialog`、`Drawer`、`BottomSheet` 和 `Tooltip` 的圓角下方不再露出深色直角（[#75](https://github.com/AIDotNet/TKCode.Arc/issues/75)）。
- `Sidebar`（區塊）調整大小後可以再次收合，標題中置中的內容保持置中，內建文字跟隨 Arc 語言（[#92](https://github.com/AIDotNet/TKCode.Arc/issues/92)）。請設定 `ExpandedWidth` 而不是 `Width`。
- 指標停留在觸發項上時，`MegaMenuMorph` 不再反覆開啟和關閉。
- `TextFilterBox` 與 `NumberFilterBox` 的輸入框不再重疊或被裁切：過長的標籤會換行，空間較窄時標籤移到輸入框上方。
- `Table` 的緊湊手機版面比列內距更窄時不再擲回 "Invalid Arrange rectangle"，`ChatThread` 氣泡跟隨主題的圓角風格。
- Windows 圖庫應用程式在 21H2 之前的 Windows 10 上可以再次啟動（#76）。

## 1.0.0（2026-11-08）

TKCode.Arc 的首個正式版：以原生 Avalonia 12 控制項呈現 Arc 的視覺語言和彈簧動效，並提供頁面級區塊、資料表格、工作流程畫布、停靠版面、終端機、PDF 檢視器和音訊播放。目標框架為 `net8.0`、`net10.0` 和 `net11.0`，基於 Avalonia 12.1.4（`TKCode.Arc.Terminal` 為 `net10.0` 和 `net11.0`）。

### 穩定性

從這個版本起，Arc 按語意化版本發行，不再帶 `-preview` 後綴：破壞性 API 變更只在新的主版本中出現，並在更新日誌中列出。所有套件以同一版本一起發行，請讓它們保持同一版本。安裝時不再需要 `--prerelease`。

### 套件

以下 21 個套件發行到 NuGet：`TKCode.Arc`、`TKCode.Arc.Core`、`TKCode.Arc.Foundation`、`TKCode.Arc.Fonts`、`TKCode.Arc.Icons.Lucide`、`TKCode.Arc.Blocks`、`TKCode.Arc.AdvancedCharts`、`TKCode.Arc.Special`、`TKCode.Arc.Workflow`、`TKCode.Arc.Dock`、`TKCode.Arc.Terminal`、`TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native`、`TKCode.Arc.Audio.Browser`、`TKCode.Arc.Pdf`、`TKCode.Arc.Pdf.Native`、`TKCode.Arc.Pdf.Browser`、`TKCode.Arc.Themes.Pixel`、`TKCode.Arc.Themes.Corners`、`TKCode.Arc.Mobile`、`TKCode.Arc.Templates`。

### 從 0.4.0-preview.6 升級

對同樣發行過 0.4.0-preview.6 且有 `net10.0` 建構的 11 個套件（`TKCode.Arc`、`.Core`、`.Foundation`、`.Blocks`、`.AdvancedCharts`、`.Special`、`.Workflow`、`.Dock`、`.Mobile`、`.Terminal`、`.Audio`）做了 API 比對，沒有發現破壞性變更。但有一點可能影響你：許多並非一直顯示的部件不再放在控制項範本中，而是在首次使用時由程式碼建立。自訂 `ControlTheme` 或查詢這些 `PART_` 名稱的程式碼需要處理部件不存在的情況。請從安裝命令中去掉 `--prerelease`。

### 新增

- **Gallery 改為產品官網**：首頁不再展示元件和專案，改為介紹 TKCode：做什麼、規模數字、可以做什麼、做事方式、三條命令上手、執行環境和 AI 支援，背景是即時點陣。其餘內容放到兩個新頁面：`/components`（元件、區塊、特效、行動端按標籤切換，可篩選）和 `/projects`（StreamMark、Jaunt、Spyglass）；StreamMark 另有 `/streammark`。頂列改為元件、開源專案、文件，右側改用圖示按鈕。頂部贊助橫條不變。
- **Gallery 的 Web 版與桌面版**：網站說明自身執行在 WebAssembly 中、效能不代表桌面版，並按訪客系統優先列出最新版本的安裝套件。桌面版新增設定選單，包含外觀、語言和關於（版本號、更新檢測、開源授權、原始碼和第三方聲明），首頁改為顯示更新狀態。
- **Gallery 適配手機**：寬度小於 760 像素時側邊欄改為抽屜，文件頁和首頁使用手機邊距，各區塊改為縱向排列。
- **特殊元件 › 趣味**（`TKCode.Arc.Special`，參考 Arc Pro 行為與動效的原創設計）：`TicketTear`，一張活動門票，票根隨手沿齒孔一孔一孔撕開，然後掉落。Enter、Space 或 `IsTorn` 會自動撕完。
- **PDF**（新套件 `TKCode.Arc.Pdf`、`TKCode.Arc.Pdf.Native` 和 `TKCode.Arc.Pdf.Browser`；issue #51）：`PdfViewer` 從檔案、位元組、資料流或位址開啟 PDF，側邊欄帶縮圖和目錄，支援帶計數和醒目提示的搜尋、文字選取和複製、連結（外部連結只接受 `http`、`https` 和 `mailto`）、密碼表單或 `PasswordRequested` 事件，以及載入和失敗狀態。`arc:Pdf.Source` 在一般 `PageViewer` 中只顯示頁面。桌面上由 PDFium 繪製（`ArcPdfNative.Use()`，在處理程序內，Windows、Linux 和 macOS 的 x64 與 arm64），瀏覽器中由 pdf.js 繪製（`await ArcPdfBrowser.UseAsync()`）。沒有後端時文件會失敗並給出提示，絕不偽造頁面。需要了解的限制：沒有內嵌中日韓字型的文件依賴本機字型，不支援觸控拖曳選取，純文字網址只有在 PDFium 下才是連結，瀏覽器套件不含 Liberation 字型。文件和範例提供英文、簡體中文和繁體中文。
- **`PageViewer`**（`TKCode.Arc`，媒體；issue #51）：縮放、平移並翻閱圖片（`ItemsSource`）或按需繪製的頁面（`IPageSource`），可連續或單頁顯示，帶適應模式、工具列，以及讓你在頁面上自行繪製和接收指標輸入的 `PageLayer`。一個自繪表面只繪製可見頁面。文件和範例提供英文、簡體中文和繁體中文。
- **範本**（新套件 `TKCode.Arc.Templates`）：`dotnet new install TKCode.Arc.Templates` 後，以 `dotnet new tkcode-arc` 建立桌面應用程式，或以 `dotnet new tkcode-arc-xplat` 建立同時面向桌面與瀏覽器（WebAssembly）的應用程式。提供四種版面配置（`--layout blank|sidebar|header|dock`），以及這些選項：使用 CommunityToolkit.Mvvm 的 MVVM（`--mvvm toolkit`）、相依性插入（`--di`）、儲存偏好設定（`--settings`，預設開啟：外觀、強調色、視窗位置與版面配置狀態會在下次啟動時還原）、本地化（`--i18n`：English、简体中文與繁體中文的 `.xlf` 檔案加上語言切換；`--ui-language` 選擇介面文字的語言）、Headless 測試專案（`--tests`）、Arc 標題列（`--custom-titlebar`）、`--framework` 與 `--accent`。項目範本可加入頁面（`tkcode-arc-page`）、視窗（`tkcode-arc-window`）、可等待結果的對話方塊（`tkcode-arc-dialog`），以及附 Token 類別與佈景主題檔的 Arc 風格控制項（`tkcode-arc-control`）。另有三個範本從完整的應用程式開始，由 Arc 的 Block 組成，範例資料放在服務介面後面，附 MVVM 與相依性插入：`tkcode-arc-admin`（登入、儀表板、附搜尋、篩選、分頁與詳細資料抽屜的客戶表格、團隊與角色、帳單、設定、命令面板）、`tkcode-arc-ai-chat`（對話清單加上 `AiChat`，回覆由範例 responder 串流產生，可換成你的模型用戶端；對話在兩次執行之間保存）與 `tkcode-arc-devtool`（停靠版面配置：檔案總管、編輯器索引標籤、屬性表格、建置輸出與問題清單，開啟的檔案隨版面配置還原）。此外還有 `tkcode-arc-workflow`（步驟面板、可復原的流程畫布、小地圖與屬性檢查器、模擬執行、JSON 匯入匯出）、`tkcode-arc-projects`（看板、甘特時間軸、週行事曆、發行檢查清單）與 `tkcode-arc-analytics`（KPI 下鑽、營收、留存分群、用量預測、服務狀態）。`tkcode-arc-lib` 用於開始撰寫自己的控制項程式庫：範例控制項及其設計 Token 與佈景主題、`Use<Name>()` 註冊、Headless 測試與範例應用程式，可直接 `dotnet pack`。新增的 **範本** 指南列出了全部內容；安裝指南新增「從範本開始」一節。
- **`EnumSelector`**（`TKCode.Arc`；issue #56）：將列舉成員列為 `RadioGroup`，`[Flags]` 列舉則列為 `Checkbox`，`SelectedEnum` 支援雙向繫結。成員資料來自 Foundation 為標記 `[EnumMetadata]` 的列舉新增的產生器，不使用反映。
- **`FluidTabControl`、`FluidTabItem`**（`TKCode.Arc`；issue #60）：與捲動連動的索引標籤。選擇索引標籤會平滑捲動到對應區段，捲動時會選取錨線所在區段的索引標籤。
- **進階圖表**（`TKCode.Arc.AdvancedCharts`）：`Sunburst`、`SankeyFlow`、`FunnelChart`、`RadarChart`（issue #66：多邊形格線、可調填色透明度）、`RealtimeStream` 和 `RaceBarChart`。文件和範例提供英文、简体中文和繁體中文。
- **版面面板**（`TKCode.Arc`，新增 Layout 群組）：`WaterfallPanel`（不同高度的卡片排入等寬的欄）、`TilePanel`（開始畫面式動態磚，可跨列跨欄並佔據第一個空位）和 `CyclicPanel`（首尾無縫循環的清單）。
- **`TransferBox`**：在兩個清單之間移動項目，可逐一、依選取或全部移動，支援全選、搜尋，並直接修改繫結的集合。
- **`NumberFilterBox`、`TextFilterBox`**：各表示一個篩選條件。數字框有核取方塊、運算子和值，可設容差；文字框支援包含、開頭是、結尾是、等於、規則運算式和區分大小寫。
- **Gallery 社群區域**：首頁和 README 顯示 QQ 群 QR 碼和群號，群號可以複製。

### 變更

- **全庫效能最佳化**：控制項中尚未顯示的部分（焦點環、Info 徽章層、選取選單等）改為首次使用時建立，控制項很多的表單建構量大幅減少。佈景主題改用虛擬類別而不是屬性選取器，每畫格繪製的標籤重複使用已排版的 `TextLayout`，表格保留已排版的儲存格文字，`TerminalView` 快取已算繪的列，動效引擎、內容切換、強制回應呈現、滾動數字、調色盤和權杖查詢的記憶體配置也減少了。這些變更的目標是保持動效和外觀不變。
- **圖片**：`ArcImageHeader` 從檔案標頭讀取圖片尺寸，遠大於顯示區域的圖片可以直接解碼到該尺寸。
- **`GanttTimeline`**：縮放 60 個工作時，每畫格配置從約 1.8 MB 降到約 100 KB，檢視外的列、橫條和箭頭不再繪製。
- **Gallery 瀏覽器版啟動**：音訊和視訊模組只在有播放器的頁面載入，中文訪客在首個畫格前就取得字型，API 說明在首次顯示時依語言載入，側邊欄在頁面首次需要時才建構。
- **Gallery**：更換桌面圖示；關於頁只在預覽建構上顯示預覽徽章。
- `tkcode-arc-lib` 範本的初始版本從 `0.1.0-preview.1` 改為 `0.1.0`。

### 變更

- 相依套件升級為 Avalonia 12.1.5 與 StreamMark 0.1.2。

### 修正

- **`ThemeSwitcher`**（issue #74）：過渡開始前不再閃現新佈景主題。現在從佈景主題開始套用的那一刻起，視窗快照就覆蓋整個視窗，四種過渡都已修正。
- **`Tooltip`**：作為提示內容傳入的控制項會顯示控制項本身，不再顯示型別名稱。
- **`GanttTimeline`**（#72）：在 560 高的區塊中，圖表不再遮住工具列和頁尾。兩個方向的捲軸與圖表同步，今日線位於橫條下方，箭頭在一天間隔和摺疊階段上走線正確，懸停時醒目提示列、橫條和箭頭。
- **`SankeyFlow`**：粒子避讓的標籤清單不再因疊加層單獨重繪而不斷增長。
- **進階圖表標籤**：`RaceBarChart` 的標籤在超越時淡出而不是重疊，`SankeyFlow` 的粒子避開文字，`RadarChart` 的內環在淺色背景上保持可見，`Sunburst` 圖例占比一律顯示一位小數。
- **`InstallSnippet`**：修正接續未在發送器上執行時醒目提示會永久懸置的問題。
- **`InkFluid`**：控制模擬節奏，讓 UI 執行緒可以閒置。
- **範本**（#70）：修正產生的 `admin`、`devtool`、`workflow`、`ai-chat`、`projects`、`sidebar` 和 `header` 應用程式中的版面問題，如文字被裁切、表格欄未對齊、頂端列按鈕被拉伸。
- **Gallery 瀏覽器版**：macOS 瀏覽器上的中文輸入法可以正常組字，先前字母會落入文字框並中斷組字。這項修正以模擬組字驗證過，尚未在真實的 macOS 輸入法上驗證。
- **視訊**（在儲存庫中，尚未發行到 NuGet）：Windows 上沒有取得畫格時（如視窗最小化）播放不再停止，`VideoSurface` 正確讀取 Media Foundation 材質的方向。新增 `Stop`、內嵌文字軌以及 iOS 和 Android 後端。
- **`TKCode.Arc.Templates` 0.4.0-preview.6 無法安裝**：該套件發行時範本檔案已移動，但指向它們的 `template.json` 沒有更新，`dotnet new install` 回報錯誤（`MV012: Source ... does not exist`）。請使用此版本或更新版本。
- **Lucide 圖示**：路徑以移動命令緊接直線開頭的 32 個圖示（`UserCog`、`FileCog`、`ServerCog`、`Coins`、`Watch`、`ZapOff` 等）首次顯示時擲回 “Invalid double value”：產生器把 `16.53.923` 讀成了一個數。現在有測試剖析每個圖示的路徑。

### 已知限制

- `TKCode.Arc.Mobile` 在桌面的手機框架中預覽，尚未在 Android 或 iOS 實機上驗證。
- `TKCode.Arc.Video`、`.Video.Native` 和 `.Video.Browser` 保留在儲存庫中，在 Windows 和實機驗證完成前不發行到 NuGet。
- `PdfViewer`：未嵌入中日韓字型的文件依賴本機字型，不支援觸控拖曳選取，純文字網址只有在 PDFium 下才是連結，瀏覽器套件不含 Liberation 字型。

## 0.4.0-preview.6（2026-11-08）

新增 8 個定價區塊、`dotnet new` 範本套件、SettingsGroup 多欄配置，以及瀏覽器原生影片呈現。

### 新增

- **區塊 › 定價**（`TKCode.Arc.Blocks`）：`PlanComparison`（移植自 Arc：帶月付/年付切換的方案卡片，價格滾動切換，突顯推薦方案並附功能比較表），以及參考 Arc Pro 行為的 7 個原創設計：`PricingCalculator`（依席位滑桿與計費切換定價的方案卡片，推薦徽章滑向合適的方案）、`UsagePricing`（拖曳席位與流量，方案名稱變形、預估金額計數到新總額）、`CartDrawer`（商品飛入購物車、數量滾動，側邊抽屜含步進器、免運費進度列與結帳）、`CheckoutSummary`（優惠碼變為可移除的標籤，折扣與稅額列滑入，總額滾動，付款按鈕經處理中到已付款）、`LiveCheckout`（訂閱結帳，摘要隨每次修改即時重算並展開為收據）、`SeatManager`（頭像列、席位用量條、帶按比例計費的步進器、確認步驟，以及可重寄或撤銷並能復原的邀請）和 `CreditTopUp`（餘額與用量圖、預設儲值包、自訂金額滑桿、自動儲值，新餘額像里程表一樣滾入）。文件與範例提供 English、简体中文與繁體中文。
- **範本**（新套件 `TKCode.Arc.Templates`）：`dotnet new install TKCode.Arc.Templates` 後執行 `dotnet new tkcode-arc`，支援 `--layout blank|sidebar`、`--mvvm none|toolkit`（使用 CommunityToolkit.Mvvm 的 ViewModel 與編譯繫結）、`--settings`（預設開啟：外觀、強調色、視窗位置與側邊欄的收合狀態會在下次啟動時還原）、`--framework` 與 `--accent`。項目範本可加入頁面（`tkcode-arc-page`，可附 ViewModel）、視窗（`tkcode-arc-window`）、可等待結果的對話方塊（`tkcode-arc-dialog`），以及附 Token 類別與佈景主題檔的 Arc 風格控制項（`tkcode-arc-control`）。安裝指南新增「從範本開始」一節。
- `SettingsGroup` 可依固定或自適應欄數排列列（`Columns`、`MinColumnWidth`、`ColumnSpacing`、`RowSpacing`、附加屬性 `ColumnSpan`），並向列共用 `LabelWidth`、`ContentAlignment` 與 `ContentSpacing`，列可個別覆寫。預設仍為單欄配置。
- `VideoPlayer.BrowserPresentation="Native"` 在瀏覽器中改用瀏覽器內建的 video 元素：不必複製影格，瀏覽器能播放的格式都可用（例如 Safari 中的 HLS）。此時控制列位於畫面下方，字幕由瀏覽器顯示。
- `VideoPlayer` 的系統全螢幕在瀏覽器中也可用（Fullscreen API）；透過視窗管理員或 Esc 離開全螢幕時，播放器也會一併離開全螢幕。

### 修正

- Linux 影片（GStreamer）：事件依發生順序送達 .NET；事件批次已滿時捨棄進度事件，而不是狀態、錯誤或結束事件。
- 影片（原生後端）：在其他執行緒讀取影格時載入或釋放，不再釋放正在讀取的影格；上一個媒體的錯誤保留其錯誤訊息。

## 0.4.0-preview.5（2026-11-08）

新增 14 個 Special 元件：介面（Surfaces）、音訊與影片、載入器；Gallery 頁面開啟更快。

### 新增

- **Special 元件 › 介面**（`TKCode.Arc.Special`）：`SheetStack`（逐層升起、帶景深的下鑽面板，下拉關閉，寬螢幕上變為疊放對話框）、`WalletStack`（拉絲金屬、扭索紋黑、霧面玻璃與彩色的支付卡，可展開、輕掃切換並抽出檢視餘額與近期交易）、`ControlCenter`（快速設定磚變形為詳細資訊，專注計時轉盤與橡皮筋式音量和字級滑桿）、`HoloCard`（隨指標透視傾斜、帶全像箔片、亮粉與眩光並可翻面的卡片）、`CardToArticle`（故事卡片變形為完整文章，下拉縮回）和 `LinkUnfurl`（把貼上的網址變成連結、顯示載入並展開為預覽卡片的輸入框）。
- **Special 元件 › 音訊與影片**（`TKCode.Arc.Special`）：`VoiceOrb`（會呼吸、泛起漣漪、旋轉與脈動的語音模式球體，附 `VoiceTranscript` 即時字幕與 `VoiceControls` 通話控制項）、`NowPlaying`（迷你播放列變形為帶波形拖曳列的完整播放器，附 `NowPlayingLevels`）和 `VoiceRecorder`（錄入即時波形、回放拖曳，再以語音氣泡傳送）。音訊為模擬資料，經屬性與事件接入，套件不依賴任何音訊程式庫。
- **Special 元件 › 載入器**（`TKCode.Arc.Special`）：`StretchRefresh`（以滑鼠、觸控或滾輪下拉重新整理）、`MorphLoader`（四筆在圓點、長條、圓環與方塊間變形，並收成勾號或叉號）、`SkeletonMorph` 與 `MorphBlock`（骨架畫面逐塊伸展為載入完成的內容）、`BlackHole`（把項目捲入奇點的刪除轉場，復原時旋出還原）和 `ChargeSession`（即時充電卡片，可拖曳充電上限並顯示漸緩的充電曲線）。
- `TokenTextEditor.TryGetTokenBounds` 傳回標記塊在另一個視覺元素座標系中的邊界。

### 修正

- Gallery：文件頁在首次進入視野時才建立範例，截圖在看到時才解碼，範例多的頁面開啟時間約減半；導覽時側欄與頂欄的工作量更少。
- 瀏覽器中的 Gallery：同時請求兩次的標誌不再顯示為首字母，文件文字與截圖每個工作階段只取得一次。

## 0.4.0-preview.4（2026-11-07）

Lucide 圖示改為按需編譯進應用程式：應用程式只攜帶自己用到的圖示。

### 變更

- **Lucide 圖示依應用程式編譯**（`TKCode.Arc.Icons.Lucide`）：套件不再攜帶全部 2155 個圖示的路徑資料，組件從 655 KB 降到 67 KB（gzip：130 KB 降到 31 KB），應用程式每用到一個圖示約增加 100 到 300 位元組。套件中附帶的原始碼產生器會在 C#（`LucideIconKind.Bell`）和 XAML（`Kind="Bell"`、`{lucide:LucideGeometry Bell}`、`{x:Static lucide:LucideIconKind.Bell}`、樣式 Setter）中找到用到的圖示，只把它們編譯進專案。這樣寫的圖示無需任何改動。
- **執行時才決定的圖示需要宣告**（破壞性變更）：來自繫結、資料或執行時載入的 XAML 的圖示，需要在專案檔中用 `<LucideIcon Include="Bell;arrow-right" />` 宣告（PascalCase 或 Lucide 原名皆可），或用 `<LucideIconsIncludeAll>true</LucideIconsIncludeAll>` 編譯全部圖示。未編譯進來的圖示不繪製任何內容，並記錄一條警告，指出需要加入的那一行；`LucideIconData.Get` 對它回傳空字串。新增 `LucideIconData.IsRegistered` 與 `TryGet`。

### 新增

- **影片**（新套件 `TKCode.Arc.Video`、`TKCode.Arc.Video.Native`、`TKCode.Arc.Video.Browser`，儲存庫內，尚未發布到 NuGet；`UseArcVideo()`）：`VideoPlayer` 在畫面上疊加 Arc 風格的控制列播放影片（帶緩衝範圍、章節與懸停時間的進度列，音量、播放速度、字幕選單、全螢幕、自動隱藏的控制列、鍵盤快速鍵、螢幕閱讀器數值文字），並自行繪製 WebVTT / SRT 字幕；`VideoSurface` 只顯示畫面。影格在合成樹內繪製，所以控制列、圓角與變換都能疊在影片上；macOS 與 Windows 上影格以 GPU 影像傳遞，不經複製。解碼使用系統播放器（Media Foundation、AVFoundation、GStreamer；`ArcVideoNative.Use()`），瀏覽器中使用 HTML video 元素（`ArcVideoBrowser.UseAsync()`）；應用程式不附帶任何解碼器。沒有後端時播放器顯示測試畫面。內建文案有 en-US、zh-CN、zh-TW。Gallery 新增「影片」分組。
- **Lucide 圖示的建置警告**：專案未宣告任何圖示時，XAML 中每處繫結 `LucideIcon.Kind`（屬性、屬性元素或樣式 Setter）的位置都會報 `ARCL002`，因為產生器無法得知繫結會顯示哪些圖示；`ARCL001` 回報 `<LucideIcon Include>` 中不是 Lucide 圖示的名稱。宣告圖示或設定 `LucideIconsIncludeAll` 後 `ARCL002` 不再出現；`<NoWarn>ARCL002</NoWarn>` 也可關閉它。

## 0.4.0-preview.3（2026-11-06）

新增用於規劃專案的 GanttTimeline、由自有資料產生列的 `Sidebar` `ItemsSource`，並修正標記延伸與 ArcWindow 範例的問題。

### 新增

- **GanttTimeline**（Blocks › Projects）：在日曆上排列階段與任務的專案時間線。拖曳長條可移動，拖曳兩端可調整日期（依整天吸附，支援復原與重設）；任務早於其相依任務結束時，相依箭頭變紅；階段可摺疊為一條彙總條；`Zoom` 在日、週、月之間切換；今日線標出計畫進度。編輯透過 `TasksChanged` 回報。鍵盤：方向鍵在長條間移動，Alt+方向鍵移動任務，Alt+Shift+方向鍵調整結束日期。長條以彈簧動畫落到吸附的日期；減少動態時直接跳轉。
- **Sidebar `ItemsSource`**：用自有資料產生列（權限清單、伺服器回傳的選單），資料變動時隨之更新。`LabelMember`、`KeyMember`、`IconMember`、`BadgeMember`、`IsEnabledMember` 與 `ChildrenMember` 對應欄位，`TrailingTemplate` 在列尾繪製額外內容（`DataContext` 為你的物件），你的物件保存在 `SidebarItem.Tag` 中。`ItemsSource` 中的 `SidebarItem` 子類別原樣使用。

### 修正

- **標記延伸**：產生的權杖與文字延伸（`ArcDesignTokenResource` 及在地化文字延伸）的 `ProvideValue` 回傳 `object`，可用於任何屬性型別。
- **Gallery**：ArcWindow 範例把標題列預覽裁切在圓角邊框內部，而不是邊框本身。

## 0.4.0-preview.2（2026-11-06）

新包 `TKCode.Arc.Dock` 提供停靠版面；新增帶 Arc 標題列的 `ArcWindow`；`ResizablePanels` 支援垂直方向；控制項更輕；修正彈出層內再開彈出層的問題和基礎主題的問題（issue #35、#38、#39、#40）。

### 新增

- **停靠版面**（新包 `TKCode.Arc.Dock`，`UseArcDock()`；issue #35）：IDE 式的面板版面。面板（`DockPane`）組成標籤組，巢狀在水平或垂直分割裡，圍繞中央的文件區；分割的分隔條就是 `ResizablePanels`。版面樹可以寫在 XAML 裡（`DockSplit`、`DockGroup`、`DockDocumentArea`），也可以在程式碼裡建構 `DockWorkspace`；模型會自動整理：空組移除，只剩一個子項的分割折疊，同方向的分割合併。面板用 `Move(pane, DockTarget)` 或標籤選單移動（關閉、關閉其他、全部關閉、移動到 › 左/右/上/下新組、停靠到 › 左/右/上/下），移動時保留內容執行個體；可以從標籤、選單或鍵盤關閉（`PaneClosing` 可取消）。標籤放不下時可捲動，並列在溢出選單裡；文件標籤顯示關閉按鈕和已修改圓點。鍵盤：方向鍵、Home、End 在標籤間移動，Ctrl/⌘+Shift+方向鍵調整順序，Delete 和 Ctrl+F4/⌘+W 關閉，Shift+F10 開啟選單。`SaveLayout` 與 `LoadLayout` 以 JSON 讀寫版面（`tkcode-arc-dock` 第 1 版，手寫讀寫器，不用反射）。內建文案有 en-US、zh-CN、zh-TW。Gallery 新增“停靠版面”分組。
- **停靠版面：拖放與浮動**：沿標籤列拖曳標籤可調整順序（其他標籤以彈簧讓位），拖離標籤列則開始停靠：幽靈標籤跟隨指標；指標下方組上的羅盤可合併或分割該組，文件區上的指示環停靠在所有文件旁邊，邊緣按鈕沿整個版面停靠，標籤列裡的豎線指出標籤的落點，預覽框在目標之間滑動。拖曳標籤列的空白處可移動整個組，Esc 取消。`ShowDockIndicators="False"` 時不顯示羅盤，按指標在組內的位置停靠。面板可以用 `Float`、標籤選單（浮動）或拖到版面外浮動，用 `Dock`（回到原處）、選單、雙擊標題列或把視窗拖到羅盤上停回。`FloatingMode`：`Auto`（經典桌面應用程式用附屬視窗，瀏覽器等其他宿主用版面內的浮動卡片）、`Window`、`Overlay`、`Disabled`。浮動視窗與 `Workspace.Floats` 雙向同步，隨儲存的版面一起恢復，並會被拉回可見螢幕；關閉浮動視窗會關閉其中可關閉的面板，其餘面板停回。Wayland 上視窗由系統決定位置，透過選單停回。新增 `FloatActiveCommand`。
- **停靠版面：自動隱藏、資料來源、面板切換、最大化**：`AutoHide`（標籤選單，Shift+Esc）把工具面板收到離其所在組最近的版面邊緣的側邊欄；點按側邊欄按鈕，面板以卡片形式滑出覆蓋在內容上（寬度與停靠時相同，最多為版面的 70%，內側邊緣可調整大小），點按外部、按 Esc 或焦點離開時滑回；`Pin`（卡片上的圖釘、選單、`Dock`）把它放回原組。自動隱藏的面板隨版面儲存，也可以在 XAML 裡透過 `DockWorkspace.LeftHidden` / `RightHidden` / `TopHidden` / `BottomHidden` 宣告。`DocumentsSource` / `ToolsSource` 配合 `DocumentTemplate` / `ToolTemplate` 為每個資料項生成一個面板（`DataContext` 為資料項；可選 `IDockItem` 提供 id 和標題；`DockPane.IdOf`）：新增資料項即開啟面板，移除即關閉，關閉標籤時會從可寫清單中移除該項，儲存的版面按 id 恢復這些面板。Ctrl+Tab 按最近使用順序開啟面板切換器（鬆開 Ctrl 即切換）。雙擊標籤列空白處（或選單中的最大化）讓一個組鋪滿版面，直到還原。新增 `AutoHideActiveCommand`、`ShowPaneSwitcherCommand`、`ToggleMaximizeCommand`。
- **ArcWindow**（Extras）：帶 Arc 標題列的桌面視窗，類似 WPF 的 `WindowChrome`（issue #38）。用戶端區域延伸到視窗裝飾裡，頂部用主題顏色繪製 `TitleBar`，可透過 `LeftContent`、`CenterContent`（放得下時在整個標題列上居中）和 `RightContent` 放入自己的控制項。拖曳標題列移動視窗，雙擊最大化或還原，拖曳邊緣調整大小。在 Windows 上標題列就是系統標題區（系統選單、Aero Snap），最大化按鈕會開啟 Windows 11 的貼靠版面；macOS 上保留紅綠燈按鈕。`CaptionButtons`（`Auto`、`Drawn`、`System`、`None`）決定由誰繪製最小化、最大化和關閉按鈕，標題列的 `Height` 設定向系統申請的標題列高度。標題列按鈕的提示已本地化（en-US、zh-CN、zh-TW）。

### 變更

- **ResizablePanels**：新增 `Orientation`（預設 `Horizontal`，或 `Vertical`）、`Gap`（窗格之間的間距，預設 1），並支援執行時增刪窗格，留下的窗格保持原有尺寸（其他窗格按比例讓出空間）。新增“垂直”範例。停靠版面的分割就是用它實現的。
- **控制項**：很少用到的範本部件改為首次需要時才建立：鍵盤焦點環（Button、Checkbox、Switch、TabItem、Select 等）、Tabs 的捲動按鈕和 Slider 的數值氣泡。Button 的狀態改用偽類。500 個按鈕的清單分配記憶體從 61.7 MB 降到 52.7 MB，常駐從 41.7 MB 降到 35.5 MB。文字框、選擇器、滑桿和開關的動效與 Token 有小幅調整。
- **Gallery（桌面）**：Gallery 視窗改為 `ArcWindow`，原先自帶的標題列類已移除。

### 修正

- **彈出層**：在 `ArcPopup` 對話框（原生彈出視窗）裡開啟 `Select`、提示或其他彈出層，不再擲出 "Unable to create IPopupImpl and no overlay layer is found"，而是以原生彈出視窗開啟（issue #40）。
- **基礎主題**：應用程式為某個內建類型（例如 `PopupRoot`）提供的 `ControlTheme` 不再關掉整個基礎主題，只跳過應用程式自己設定了樣式的類型（issue #39）。
- **Gallery（WebAssembly）**：切換頁面不再讓網站凍結；WebAssembly 宿主預設不使用 AOT 編譯發佈（用 `-p:ArcAot=true` 開啟）。

## 0.4.0-preview.1（2026-11-03）

新增編輯物件屬性的 PropertyGrid、Table 多行儲存格、Select 和 ColorPicker 的尺寸、在執行中的應用程式裡載入套件，並修正瀏覽器中 DataGrid 匯出失敗的問題（issue #30、#34、#36、#37）。

### 新增

- **PropertyGrid**（Extras）：以「標籤 + 編輯器」的列編輯一個或多個物件的屬性，類似 CAD 或建模軟體的屬性面板（issue #30）。為類別標註 `[PropertyGridModel]`，Foundation 原始碼產生器會在編譯期以具型別的委派註冊其屬性（不使用反射，相容 AOT 與裁剪），並讀取 `[Category]`、`[DisplayName]`、`[Description]`、`[ReadOnly]`、`[Browsable]` 和新的 `[PropertyGridOptions]`。其他型別以 `PropertyGridTypes.Register<T>` 在程式碼中註冊，Avalonia 物件則依其已註冊的屬性描述。內建編輯器是 Arc 的小尺寸欄位（`Input`、`NumberField`、`Switch`、`Select`、`ColorPicker`、`DatePicker`）；`Editors` 可依鍵、屬性名稱或型別加入自訂編輯器。列依類別分組、可搜尋篩選、顯示選取列的說明，可展開巢狀值（結構會寫回其擁有者）和清單（附新增與刪除按鈕），跟隨 `INotifyPropertyChanged`，並支援同時編輯多個物件（`SelectedObjects`，附混合值）。列是 `PropertyGridItem` 控制項，附供樣式使用的虛擬類別。新增產生器診斷 ARCF030–ARCF032。
- **Table**：多行儲存格（issue #37）。`DataTableColumn.TextWrapping`（預設 `NoWrap`，單行加省略號）和 `MaxLines` 讓欄位顯示換行字元並折行長文字；每列的高度取列內最高的換行儲存格，以 52 px（精簡版面 64 px）為下限。設定了 `TextWrapping="Wrap"` 的 `CellTemplate` 欄會把列撐到範本的高度。沒有換行欄的表格維持統一列高，沒有額外開銷；有換行欄時，只在資料、字型或欄寬變化時測量換行文字。新增「多行儲存格」範例。
- **Select** 和 **ColorPicker**：`Size`（Select 為 `Small`、`Medium`、`Large`；ColorPicker 為 `Small` 和 `Medium`），可與小尺寸文字方塊對齊。預設仍為 Medium。
- **Foundation**：`ArcRuntime.UsePackages(arc => ...)` 在執行中的應用程式裡啟用套件，例如瀏覽器應用程式在某個頁面首次需要時才下載的組件中的套件（issue #34）。新套件的設定與啟動時相同：註冊其權杖識別、控制項主題、語言目錄、預設集和提交回呼，並連同它們的權杖（包括主題範圍）重新發佈目前主題。已啟用的套件會被略過；衝突或缺少翻譯會在任何變更之前擲回例外。

### 變更

- **Gallery（WebAssembly）**：Special 元件（`TKCode.Arc.Special`）改為在第一個 Special 頁面開啟時下載，而不是啟動時下載：首頁少下載 1.2 MB（brotli），之前為 22.2 MB（issue #34）。Gallery 也不再在啟動時執行每個已文件化控制項的靜態建構函式：API 表只在頁面顯示時才查找其型別和屬性。

### 修正

- **DataGrid**：在瀏覽器中匯出 xlsx 或 CSV 不再得到空檔案並顯示「匯出失敗」（issue #36）。瀏覽器的儲存檔案串流只接受非同步寫入，因此匯出內容先在記憶體中產生，再以 `WriteAsync` 寫入。

## 0.3.2-preview.2（2026-10-30）

選單支援多層子選單，TreeView 支援列內容範本，內嵌選項支援繫結，並修正了 Windows 下的建置問題（issue #28、#29、#31、#32）。

### 新增

- **TreeView**：`ItemTemplate` 以你自己的內容排版每列的標籤區，例如名稱加型別、狀態點或工具提示；列本身、填色、縮排、箭頭和圖示仍由樹繪製，樹仍然是虛擬化的（issue #28）。`DisabledSelector` 和 `TreeViewItem.IsDisabled` 顯示不能被選取的節點；鍵盤焦點仍可到達它們，所以停用的資料夾仍然可以展開。`RowHeight` 設定每一列的高度，例如用於兩行內容。
- **選單**：`DropdownMenu`、`ContextMenu` 和任意 `MenuSurface` 支援多層子選單（issue #29）。在一列內巢狀放入 `MenuEntry` 元素（`MenuEntry.Items`，或 `MenuEntry.ItemsSource`），或為 `ItemsSource` 資料設定 `ChildrenSelector`。帶子選單的列會顯示箭頭，點按、按 ArrowRight、Enter 或 Space，或指標在其上停留 100 ms 後，子選單會在選單旁開啟；指標移向子選單途中經過其他列時不會關閉它。ArrowLeft 或 Escape 關閉一層，Tab 關閉全部，在任一層選擇一列都會關閉整個選單並只引發一次 `ItemClicked`。右側放不下時子選單在左側開啟，更深的層級也沿用這個方向。新增成員：`MenuEntry.Items`、`MenuEntry.ItemsSource`、`MenuEntry.HasSubmenu`、`:submenu-open` 虛擬類別，以及 `MenuSurface`、`DropdownMenu` 和 `ContextMenu` 上的 `ChildrenSelector`。
- **Core**：`SubmenuSafeZone`（`TKCode.Arc.Primitives`），指標移向已開啟子選單時的路徑區域，由選單和 `ModelMenu` 共用。

### 變更

- **破壞性變更：內嵌選項類型支援繫結。** `SegmentedOption`、`RadioOption`、`RadioCardOption`、`BillingToggleOption`、`FilterField`、`FilterOption`、`ReactionOption`、`SliderMark`、`GaugeThreshold`、`ActionSwapState`、`Announcement`、`ChatParticipant` 與 `HoverCardStat` 現在衍生自 `AvaloniaObject`，屬性改為樣式屬性。在 XAML 中內嵌撰寫的選項可以使用 `{Binding ...}` 與 `{DynamicResource ...}`（例如在地化的標籤），顯示它們的控制項也會跟隨之後的變化（issue #32）。建構函式、物件初始設定式與屬性名稱不變，但以舊類型編譯的程式碼需要重新編譯，而且這些物件必須在 UI 執行緒上建立與修改。`FilterChip` 仍是不可變的值類型。
- `BillingToggle.DefaultOptions` 每次讀取都會傳回新的選項，各個切換不再共用同一組選項。
- **選單**：各層子選單中的列都會在最外層選單面板上引發 `MenuSurface.ItemClicked`，因此 `DropdownMenu.ItemClicked` 和 `ContextMenu.ItemClicked` 也會回報巢狀的列。

### 修正

- **Windows 建置**：Windows 下 `src/` 裡的專案重新被識別為程式庫，`dotnet pack` 能正常產生套件，XML 文件（Gallery 的 API 表）也會產生；磁碟上殘留的產生程式碼（`GeneratedFiles/`）不會再被重複編譯，每個目標框架寫入各自的 `GeneratedFiles/<tfm>` 資料夾（issue #31）。CI 也加入了 Windows 建置。
- **圓角預設**：Analytics 分段切換、KPI 下鑽、指標儀表板、小倍數圖、團隊成員、來源引用和引力井的圓角改為取自圓角權杖，`t.Corners(...)` 能像其他控制項一樣把它們改成直角或按比例縮放。
- **用量預測**：關閉動效時，圖表在變化後不再持續執行影格迴圈。

## 0.3.2-preview.1（2026-10-28）

Arc 不再依賴 AtomUI。每個套件現在只依賴 Avalonia 與新的 MIT 套件 `TKCode.Arc.Foundation`，整條相依鏈都採用寬鬆授權，閉源應用程式可以把 Arc 以單一檔案或 NativeAOT 方式發佈（issue #25）。應用程式的進入點與主題 API 有變化；控制項、控制項屬性與 XAML（`{arc:ArcDesignTokenResource ...}`、`{arc:XxxTokenResource ...}`）不變。請參閱下文的*從 0.3.1 遷移到 0.3.2*。

### 新增

- **Foundation**（新套件 `TKCode.Arc.Foundation`）：主題管理員（`ArcThemeManager`、`ArcTheme`、`ArcThemeState`、`ArcThemeBuilder`）、主題預設集（`ArcThemePreset`）、局部主題（`ArcThemeScope`）、套件註冊（`AppBuilder.UseArc(...)`、`ArcBuilder`）、語言（`ArcLanguage`、`ArcLanguageManager`），以及產生權杖、主題檔與語言目錄的原始碼產生器。不使用反射，相容於裁剪與 NativeAOT。控制項的主題在該型別的控制項第一次使用時才載入。
- **ArcImage** 與 `ImageSource`（Core）：在背景載入圖片（http、`avares://`、檔案、`data:`、點陣圖），帶共用快取，桌面與瀏覽器皆可使用。
- **ArcOverlayHost**（Core）：自帶覆疊層的面板，在其中開啟的對話方塊、抽屜與底部面板都限制在它的範圍內。
- **基礎主題**（Core）：Arc 為其範本用到的 Avalonia 控制項提供自己的主題（`ScrollViewer`、`ScrollBar`、`ItemsControl`、`PathIcon`、`TextSelectionHandle` 與快顯視窗宿主）。應用程式使用 `FluentTheme` 或 `SimpleTheme` 時，它會讓位。
- **捲軸**與 `ScrollArea` 一致：細長，指標停留在檢視上或捲動時顯示，靜止時隱藏。`ScrollViewer.AllowAutoHide="False"` 可讓它們一直顯示。
- **圓角主題**：`UseArcCorners()` 與 `t.Corners(style)`；現成的 `ArcCornerPresets.Square` 與 `ArcCornerPresets.Half`。
- **選單**：較長的選單在 `MaxDropDownHeight` 內捲動。
- **Gallery**：桌面視窗自行繪製標題列（標誌、標題、釘選按鈕；Windows 與 Linux 上還有視窗按鈕）。

### 變更

- **不相容：進入點。** `AppBuilder.UseAtomUI(b => b.UseDesktopControls().UseArc())` 改為 `AppBuilder.UseArc(arc => arc.UseArcControls())`。`Application` 類別不再需要 `partial`。
- **不相容：主題 API。** `ThemeConfigBuilder`、`ThemeAlgorithm`、`IThemeManager.ApplyThemeAsync` 與 `WithArc(...)` 由 `ArcTheme.Apply(t => ...)`、`ArcThemeManager.Apply` / `ApplyAsync` 以及 `UseArc` 中的 `WithTheme(...)` 取代。
- **不相容：Pixel 與 Corners** 改為主題預設集：`t.Pixel()` 與 `t.Corners(style)`。`ArcPixelTheme`、`ArcPixelScope`、`WithArcCorners` 與 `ArcCornerScope` 已移除；預設集會略過應用程式沒有註冊的套件。
- **不相容：語言**移到 `TKCode.Arc.Localization`（`ArcLanguage`、`ArcLanguageManager`）；`UseLanguages` 在 `ArcBuilder` 上。
- **不相容：圖片與快顯視窗。** `AsyncImage` 由 `ArcImage` 取代（`IsLoaded` 改為 `IsImageLoaded`）。`ArcPopup` 衍生自 Avalonia 的 `Popup`；`OpenMotion` 與 `CloseMotion` 的型別是 `ArcPopupMotion`，`AdjustPlacementAnchor` 移除了 `ref flipX, ref flipY` 參數，`IsOpen` 會維持為 true 直到退場動態效果結束。`ArcPopupFrame` 取代 `BaseMotionActor`，`ArcOverlayHost` 取代 `ScopeAwareOverlayLayerPanel`。
- **不相容：行動端權杖識別**改為 `MobileButton`、`MobileCheckbox`、`MobileDialog`、`MobileSwitch` 與 `MobileToast`，不再與桌面控制項同名。
- Arc 不再與 AtomUI 控制項共用主題；混用兩者的應用程式請留在 0.3.1-preview.6。
- **效能**：`Button` 在第一次進入載入狀態時才建立載入圈；`Select` 的值與欄位訊息只在需要時才排版文字。

### 修正

- **ToastStack**：在堆疊恢復計時器的過程中關閉某個 Toast，不再中斷恢復。
- **DataGrid**：篩選清單的搜尋框重新顯示（之前它沒有範本）。
- **PasswordField**：較長的強度標籤不再與強度條重疊（#23）。
- **Gallery**：環境變數事件記錄中的文字方塊重新顯示（之前它沒有範本）。
- **浮層**：選單、選擇器與氣泡卡片重新完整顯示陰影；先前浮層宿主把陰影裁掉了，圓角看起來是直角。
- **Gallery**：滑動操作的收件匣範例加上了標題、即時計數與頭像，與 Arc 一致。

### 從 0.3.1 遷移到 0.3.2

1. 刪除所有 `AtomUI.*` 套件參照與 `using AtomUI...;`；Arc 會引入它需要的一切。
2. 把 `UseAtomUI(...)` 換成 `UseArc(...)`，並把 `UseArc()`（控制項套件）改名為 `UseArcControls()`。其他套件的註冊方法名稱不變，移到 `UseArc` 的回呼中。
3. 把主題請求換成 `ArcTheme.Apply`。一次變更只寫要改的部分，狀態的其餘部分維持不變。
4. `App` 類別可以拿掉 `partial` 關鍵字；不再有程式碼產生到它裡面。
5. 使用 .NET 11 SDK 且 `PublishAot` 時，在應用程式專案中設定 `<UseOptimizedPublish>false</UseOptimizedPublish>`。

```csharp
// 0.3.1
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseAtomUI(builder =>
          {
              builder.UseDesktopControls();
              builder.UseArc();
              builder.UseArcBlocks();
              builder.UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN]);
          });

// 0.3.2
AppBuilder.Configure<App>()
          .UsePlatformDetect()
          .UseArc(arc => arc
              .UseArcControls()
              .UseArcBlocks()
              .UseLanguages(LanguageTags.EnUS, [LanguageTags.EnUS, LanguageTags.ZhCN])
              .WithTheme(t => t.Appearance(ArcAppearance.System)));
```

```csharp
// 0.3.1
var builder = new ThemeConfigBuilder()
    .WithAlgorithms(ThemeAlgorithm.Default, ThemeAlgorithm.Dark)
    .WithArc(arc => arc.Accent(ArcAccentPreset.Blue).MotionMode(ArcMotionLevel.Reduced));
await manager.ApplyThemeAsync(new ThemeRequest(IThemeManager.DEFAULT_THEME_ID, builder.Build(),
                                               ThemeTransitionReason.UserRequest));

// 0.3.2
ArcTheme.Apply(t => t
    .Appearance(ArcAppearance.Dark)
    .Accent(ArcAccentPreset.Blue)
    .MotionLevel(ArcMotionLevel.Reduced));
```

| 0.3.1（AtomUI） | 0.3.2 |
|---|---|
| `AppBuilder.UseAtomUI(b => ...)`、`UseDesktopControls()` | `AppBuilder.UseArc(arc => ...)` |
| `UseArc()`（控制項套件） | `UseArcControls()` |
| `IAtomUIBuilder` 上的 `UseArcCore`、`UseArcBlocks`、`UseArcAdvancedCharts`、`UseArcSpecial`、`UseArcWorkflow`、`UseArcMobile`、`UseArcPixelTheme`、`UseArcGallery` | `ArcBuilder` 上的同名方法；新增 `UseArcCorners()` |
| `builder.UseLanguages(...)` | `arc.UseLanguages(...)`，參數相同 |
| `partial class App`、`ApplicationStartup.Attach` | 不再需要 |
| `ThemeConfigBuilder`、`WithAlgorithms(ThemeAlgorithm.Dark)` | `t.Appearance(ArcAppearance.Dark)`（或 `Light`、`System`） |
| `IThemeManager.ApplyThemeAsync(new ThemeRequest(...))` | `ArcTheme.Apply(t => ...)`、`ArcThemeManager.Current.Apply` / `ApplyAsync`（請 await，切勿在 UI 執行緒上封鎖等待） |
| `WithArc(a => a.Accent(preset or color))` | `t.Accent(preset or color)` |
| `WithArc(a => a.Token(name, value))` | `t.Token(name, value)` |
| `WithArc(a => a.ControlToken(catalog, control, name, value))` | `t.Token(identity, name, value)` |
| `WithArc(a => a.MotionMode(level))` | `t.MotionLevel(level)` |
| `WithArcCorners(style, scope)`、`arc.Corners(style, scope)`、`ArcCornerScope` | `t.Corners(style)`、`ArcCornerPresets` |
| `ArcPixelTheme.CreateRequest(...)`、`WithArcPixel(...)`、`ArcPixelScope` | `t.Pixel()`、`ArcPixelPresets`；用 `t.RemovePreset(ArcPixelPresets.Slot)` 回到 Arc |
| `using AtomUI.Localization;` | `using TKCode.Arc.Localization;` |
| `Application.Current.GetLanguageManager()`、`ILanguageManager` | `ArcLanguageManager.Current` |
| `GetLocalizer().Get(kind)`、`Format(kind, args)` | `XxxLangResources.Get(kind)`、`XxxLangResources.Format(kind, args)` |
| `LanguageChanged`、`ChangeLanguage(tag)` | `Changed`、`Set(tag)`（或 `ArcLanguage.Changed`、`ArcLanguage.Set`） |
| `FormattingCulture`、`CurrentLanguage` | `ArcLanguage.Culture`、`ArcLanguage.Current` |
| 依目錄覆寫行動端權杖 | 識別 `MobileButton`、`MobileCheckbox`、`MobileDialog`、`MobileSwitch`、`MobileToast` |
| `AtomUI.Controls.ImageSource`、`AsyncImage`、`IsLoaded` | `TKCode.Arc.ImageSource`、`ArcImage`、`IsImageLoaded` |
| 以 AtomUI `Popup` 為基礎的 `ArcPopup`、`BaseMotionActor` | 以 Avalonia `Popup` 為基礎的 `ArcPopup`、`ArcPopupFrame`；`OpenMotion` / `CloseMotion` 的型別是 `ArcPopupMotion` |
| `AdjustPlacementAnchor(placement, ref flipX, ref flipY)` | `AdjustPlacementAnchor(placement)` |
| `ScopeAwareOverlayLayerPanel` | `ArcOverlayHost` |
| AtomUI 為 `ScrollViewer`、`ScrollBar`、`ItemsControl` 與快顯視窗宿主提供的主題 | Arc 的基礎主題；捲軸靜止時隱藏（`ScrollViewer.AllowAutoHide="False"` 讓它們一直顯示） |

- **視窗與覆疊層。** 少了 AtomUI 的視窗主題，一般的 Avalonia `Window` 只有在範本中含有 `EnableOverlayLayer="True"` 的 `VisualLayerManager` 時才有覆疊層。`ThemeSwitcher` 的轉場效果需要它；沒有時會直接切換。
- **AtomUI 控制項。** 同時使用 AtomUI 控制項的應用程式，無法再讓兩者共用同一個主題。在這些畫面遷移到 Arc 之前，請留在 0.3.1-preview.6。

## 0.3.1-preview.6 (2026-10-28)

### 新增

- **Cascader**(Extras):依層級逐級選擇一個節點,如省、市、區。欄位、選單、滑動醒目提示和清除按鈕與 `Select` 一致;父節點的子級在右側一欄中展開,選單寬度用彈簧過渡到新的寬度。資料透過委派讀取(`ChildrenSelector`、`TextSelector`、`ValueSelector`),不使用反射。`SelectedItem`、`SelectedValue` 與 `SelectedPath` 始終保持一致,資料抵達之前寫入的值會被保留,資料抵達後再解析。選項:`LeafOnly`、`ExpandTrigger`、`ShowFullPath`、`PathSeparator`、`IsClearable`、`IsReadOnly`;完整的鍵盤支援。

## 0.3.1-preview.5 (2026-10-28)

### 新增

- **Audio**(新套件 `TKCode.Arc.Audio`、`TKCode.Arc.Audio.Native` 與 `TKCode.Arc.Audio.Browser`):媒體類 Block 使用的 `IAudioPlayer` 抽象,預設靜音模擬;桌面經 miniaudio 播放(`ArcAudioNative.Use()`),WebAssembly 經 HTML audio 元素播放(`ArcAudioBrowser.UseAsync()`)。
- **Blocks**:`MediaPlayer`(封面、播放清單、進度列、上一首與下一首、音量與靜音)和 `FileUpload`(拖放區、檔案清單、進度、驗證)。
- **Special**:新增 Cursor 分組,包含 `DotGrid`、`GravityWell` 和 `InkFluid` 三個指標效果。
- **Gallery**:`/demo` 下的 Northstar 儀表板展示,複刻 uiarc.dev/demo 的十一個頁面和登入頁;從首頁的「查看演示」或頂欄選單的「演示」進入。

### 修正

- **ArcContentSwap**:控制項被切換出去後再次顯示不再擲出例外(在 Webhooks 檢查面板裡切換 Payload 與 Headers 會導致程式當掉)。
- **Charts**:平均線旁邊的座標刻度保持隱藏,平均值標籤不再與它重疊。
- **Webhooks**:Payload 與 Headers 切換後都從第一行開始顯示。

## 0.3.1-preview.4 (2026-10-27)

### 新增

- **Terminal**(新套件 `TKCode.Arc.Terminal`,net10.0 與 net11.0):`TerminalView` 控制項和 libghostty-vt 的託管封裝(VT 解析、畫面狀態、選取、輸入編碼)。原生函式庫隨套件放在 `runtimes/<rid>/native`,涵蓋 Windows、macOS 與 Linux。

## 0.3.1-preview.3 (2026-10-27)

### 新增

- **Blocks**：Analytics、Developer、People、Finance 與 LineCharts 新增約 25 個區塊（API 金鑰、Webhook、整合、環境變數、團隊與角色、指標探索、MRR 瀑布圖、同期群留存、分級統計地圖、行情終端等），附 Gallery 頁面、範例與測試。
- 新增 **RichTextEditor** 與 **ActionSwap** 控制項。
- **行動端**：`Dialog`、`Toast`、`ActionSheet`、`Toolbar`、`PullToRefresh`、`Radio` 與 `IconButton`，鍵盤避讓與 Arc 動態。
- **Gallery**：桌面端支援檢查更新並設定 macOS Dock 圖示；瀏覽器端透過 HTTP 載入地圖資料。

### 變更

- 重做 **Table**、**Editors**、**Selects** 與 **Toggles**。

### 修復

- **`TextReveal`、`InViewTitle`**：中文、日文和韓文可以在字元之間換行。之前沒有空格的標題在窄欄中會被裁切。
- **TimePicker**、**FilterMenu**：列的空白區域不再回應指標輸入 (#20)；**Combobox**：清除後不再開啟清單。
- **Password**：顯隱選擇與輸入狀態保持一致 (#21)。
- **Gallery**：修復 Windows 上側邊欄搜尋框重複提交輸入法內容的問題。

## 0.3.1-preview.2 (2026-10-26)

### 新增

- **圓角主題**：新套件 `TKCode.Arc.Themes.Corners`（`WithArcCorners(style, scope)`），僅透過令牌覆寫為所有 Arc 控制項切換直角或縮放圓角；顏色、邊框、陰影與動態維持不變。
- **行動端**：`ListGroup` / `ListItem`、`Sheet`、`TextField` 與 `Searchbar`，大標題 `Navbar` 以及更多 `Button` 變體；Gallery 新增列表分組、導覽堆疊與底部面板的頁面與範例。
- **Core**：新增更多設計令牌與 `ArcTokens` 輔助方法，用於繫結圓角半徑。

### 變更

- **像素主題**：圓角與主題資源改由共用的圓角表重新產生。
- **Blocks**：`WeekCalendar` 使用新的邊線令牌繪製邊緣。

### 修復

- **Stepper**：目前步驟的光暈不再被裁切 (#19)。

## 0.3.1-preview.1 (2026-10-26)

### 新增

- **行動端**：新套件 `TKCode.Arc.Mobile`（`UseArcMobile()`），首批觸控優先控制項：`Button`（按壓狀態、44 × 44 觸控目標）、`Switch`、`Page`（頁首、內容與頁尾位於安全區內）、`Navbar` 與 `Tabbar`。只依賴 `TKCode.Arc.Core`，有自己的命名空間。尚未在 Android 或 iOS 真機上驗證，並且仍會經由 `TKCode.Arc.Core` 間接引入 `Avalonia.Desktop` 與 `AtomUI.Desktop.Controls`。
- **Gallery › Mobile**：新增 Mobile 區，以手機框預覽（iPhone 15、Pixel、小螢幕手機、平板，直式與橫式，狀態列、模擬安全區、軟鍵盤佔位）。這是桌面上的模擬，不是真機。
- **像素風主題**：新套件 `TKCode.Arc.Themes.Pixel`（`UseArcPixelTheme()`）：直角、硬邊陰影、受限調色盤，明暗兩套；像素字型 Silkscreen 與 VT323（SIL OFL）隨 `TKCode.Arc.Fonts` 提供（`WithArcPixelFonts()`）。Gallery 的外觀面板新增「風格」切換：Arc 或 Pixel。
- **表格 › TreeTable**：具巢狀列、三態選取、保留階層的排序、延遲載入子節點與 treegrid 鍵盤導覽的表格。Gallery 以「New」標記新元件，持續 60 天。
- **Core**：邊框寬度與圓角的設計 Token（`EdgeThickness`、`FocusEdgeThickness`、`RadiusStep*`、`EdgeThickness*`），主題可以改寫它們；所有預設值與原先的取值一致。

### 變更

- **Gallery**（網頁）：WebAssembly 主機重新改為不做提前編譯，因為編譯版在啟動時當機。圖示網址加上版本號，瀏覽器會丟棄舊圖示。

### 修正

- **Carousel**：計時已耗盡的投影片不再不讓出執行緒就再次推進輪播，而是重新開始一個間隔。

## 0.3.0-preview.5 (2026-10-24)

### 新增

- **工作流程畫布**：新套件 `TKCode.Arc.Workflow`（`UseArcWorkflow()`）：`WorkflowCanvas` 顯示步驟與連線組成的圖並播放執行過程，支援平移縮放、編輯、連線、復原重做、複製貼上、節點範本、縮圖，500 個步驟的版面約 2 毫秒。另有 `WorkflowNodeCard` 與 `WorkflowZoomControls`。
- **Blocks › 驗證**：`SignIn`、`SignupForm`、`LoginSplit`、`LoginCentered`、`LoginImmersive`、`OtpInput`、`OnboardingIllustration` 和 `MultiStepForm`。
- **Core**：`PanZoomTransform`、`WheelRouter` 和 `LayeredGraphLayout`。

### 變更

- **Gallery**（網頁）：網站改為 WebAssembly 預先編譯，慢速 CPU 上動畫更流暢；首次造訪下載量較大。

### 修復

- **記憶體**：許多控制項（`TreeView`、`ChatThread`、`Markdown`、圖表、`DataGrid`、AI、Projects 和 Special 區塊等）在整個生命週期內都監聽其顯示的資料，使頁面一直留在記憶體中；現在只在位於視覺樹時監聽。`SiteFooter`、`SemanticZoom`、`AgentStatus` 和 Gallery 頁面也不再洩漏。
- **選單**：關閉的面板不再把遮罩留在視窗上。
- **ChatThread**：表情選擇器不再向主控台寫入除錯訊息。
- **Accordion** 與 **ExpandableCard**：展開時分隔線開銷更低，外觀不變。
- **Markdown** 與 AI 回答改用 StreamMark `0.1.0`。

## 0.3.0-preview.4 (2026-10-24)

### 新增

- **特別元件 › AI**：`TKCode.Arc.Special` 中的五個智慧代理介面元件：`AgentStatus`（即時狀態膠囊，可展開查看最近的步驟）、`ToolCall` 與 `ToolCallGroup`（顯示輸入與輸出的工具呼叫列，可核准與重試）、`AgentPlan`（可編輯的計畫草稿，核准後成為即時清單）、`AgentTimeline`（在同一時間軸上重播多代理執行）與 `ContextWindow`（具壓縮門檻的 200 格權杖預算網格）。
- **Blocks › 頁首與頁尾**：`SiteHeader`、`SiteFooter`、`CtaSection` 與 `NewsletterSignup`，移植自 Arc 的免費區塊。
- **Blocks › AI**：模型競技場（`ModelArena`，兩個模型的盲測：串流回答、投票與排行）與文件擷取（`DocumentExtraction`，文件旁的表單隨模型閱讀逐項填寫，每個值都連結到其來源文字）。九個 AI 區塊現已全部可用。
- **AnnouncementBar**：`DismissalDuration` 讓記住的關閉狀態在一段時間後失效。

### 變更

- **Gallery**：語言在頂欄有了獨立的選單，首次啟動跟隨系統語言並會被記住。首頁新增友站連結，文件頁載入時顯示佔位內容。

### 修復

- **Combobox**（#9）：聚焦時不再顯示深色的環，只把邊框變為 `BorderStrong`，與 Arc 一致。
- **Gallery**（#8、#10）：範例不再切掉行內編輯框與焦點環的左側。
- **Gallery**（#11）：以 NativeAOT 發佈的桌面 Gallery 能重新載入文件頁與截圖。

## 0.3.0-preview.3 (2026-10-23)

### 修復

- **Select**：選項高亮是直角；現在使用主題的選項圓角（14）。
- **Select**：Full 動態等級下，選取後選單會在欄位下方的文字上淡出。現在依文件約定立即關閉；為此 `ArcPopup` 新增了 `IsExitInstant`。Reduced 等級仍然淡出。
- **Combobox、MorphSelect**：在 macOS 上點擊輸入框可能因 `NSInvalidArgumentException` 導致應用程式結束。無障礙即時區域不再接收空文字。

## 0.3.0-preview.2 (2026-10-23)

### 新增

- **特別元件**：新套件 `TKCode.Arc.Special`（`UseArcSpecial()`）中的五個 AI 動畫講解：`AgentLoop`、`MixtureOfExperts`、`SpeculativeDecoding`、`PromptCache` 與 `ReasoningTree`，以共用的 `Explainer` 基底類別建置。所有數字都由資料計算而得。
- **Blocks**：五個專案區塊（`WeekCalendar`、`AvailabilityPicker`、`ProjectBoard`、`ReleaseReadiness`、`KanbanBoard`），六個展示區塊（`ActivityTerrain`、`RevenueGlobe`、`CustomerGalaxy`、`SemanticZoom`、`LayoutMorph`、`ExplodedView`），以及首批 AI 區塊（模型選擇器、智慧代理記憶、AI 輸入框、AI 聊天、AI 回答，以及側邊面板與提示詞工作台）。
- **Core**：`ArcSurface.Elevation`（對話方塊、抽屜、氣泡卡片與選單的內容知道自己位於抬升的表面上）、`RollingDigitsFalloff.None`、新的彈簧、`ArcContentSwap` 模式、`ArcRing`、`WordDiff`、`WordPacer`、`DecoratedTextBlock`，以及另外 23 個 Lucide 圖示。
- **Button**：抬升表面上的幽靈按鈕現在有清楚可見的懸停與按下填色。
- **特殊輸入**：`TokenTextEditor`，權杖視為單一單位的多行編輯器；`MentionInput` 以它建置。
- **Gallery**：文件網站提供 `llms.txt`、`llms-full.txt`、每個頁面的 Markdown 版本，以及供 AI 助理使用的代理技能。

### 變更

- **破壞性變更，儲存庫搬移**：儲存庫現在位於 `AIDotNet/TKCode.Arc`，XAML 命名空間隨之改變。請將 `xmlns:arc="https://github.com/AtomUICommunity/TKCode.Arc"` 改為 `xmlns:arc="https://github.com/AIDotNet/TKCode.Arc"`（Lucide 命名空間同樣處理）。
- **Sidebar**：`ResizeMode` 現在預設為 `Overlay`；設為 `ResizeMode="Push"` 可恢復先前的行為。
- **效能**：動態排程器每個畫面只推進每個驅動一次，隱藏的迴圈不再產生負擔，快取模糊效果與文字排版，ActivityTerrain 與 LayoutMorph 大幅加速。
- **Gallery**：WebAssembly 網站在第一個畫面之前少下載約 1.5 MB。

### 修正

- **Tabs**、**PageHeader**、**Breadcrumb**、**ArcContentSwap**、**DataGrid**：範本不再以 `null` 資料內容建置。
- **Core**：中途離開視窗的內容切換不再恢復為半透明；錨定彈出層的陰影重新使用圓角。
- **Sidebar**：從側欄軌道展開後，內容不再保留第一個畫面的寬度。
- **Motion**：從 `Transparent` 淡入或淡出填色時不再閃出灰色（`ArcBrushTransition` 使用預乘 alpha 混合）。
- **TagInput**、**Textarea**、**TimePicker**：移除粗重的焦點環；移除 `FieldFrameToken` 的 `FocusRing*` 值。

完整清單請見儲存庫中的 `CHANGELOG.md`。

## 0.3.0-preview.1 (2026-10-22)

### 新增

- **Core**：`ArcZoom.Scale`，一個可繼承的附加屬性，供縮放視窗內容的應用程式使用。錨定彈層（選單、選取器、工具提示、懸停卡片、氣泡卡片）繪製在視窗的覆蓋層裡，位於被縮放的容器之外，因此原先無法跟隨縮放；現在它們會依繼承到的比例排版。`ArcPopup.Surface` 傳回指派給 `Child` 的那個介面元素（彈層現在把它放進一個縮放元素中，因此 `Child` 本身是那個元素）。
- **ExpandableContent**：把 ExpandableText 的截斷、切換按鈕和動態效果用在任意內容上，例如可選取的文字、唯讀編輯器或轉譯後的 Markdown。截斷停在內容中第一段可見文字的某一行上（`TextBlock`、`SelectableTextBlock`，或實作了新增 `IExpandableLineSource` 的控制項）；其他內容依 `Lines × LineHeight` 截斷。`ExpandableText` 與 `ExpandableContent` 共用新增的 `ExpandableClamp` 基底類別。
- **ExpandableText**、**ExpandableContent**：`IsFadeEnabled` 可以關閉最後一行上的漸隱。

### 變更

- AtomUI 固定為 6.2.8；0.2.0-preview.1 以 6.2.7 建置。
- **ExpandableText**：摺疊時，文字只排版到截斷線之後的一行，展開時才完整排版。600 行的文字摺疊時測得約 1ms，而不是約 135ms。帶有內嵌控制項的文字仍會完整排版，以便截斷線以下的連結可以取得焦點。截斷現在停在文字自身的行邊界上，而不是 `Lines × LineHeight`。
- **ExpandableText**：`Lines`、`IsExpanded`、`MoreLabel`、`LessLabel`、`LineHeight`、`IsOverflowing` 和 `ExpandedChanged` 現在宣告在新增的 `ExpandableClamp` 基底類別上。透過 `ExpandableText` 使用它們的程式碼不受影響。

### 修正

- **ExpandableText**：隨新的資料內容到來的 `IsExpanded` 變化（例如在回收的項目範本中）不再產生動畫，也不再捲動頁面。
- **Switch**、**Checkbox**：隨新的資料內容到來的值（例如詳情檢視被重複使用於下一項時）會立即顯示；滑塊不再滑動和拉伸，勾號也不再像使用者點選過那樣畫出。
- **Progress**：`Value` 變化但整數百分比不變時，不再重啟彈簧，也不再更新讀數和無障礙狀態。每秒回報數百次的下載現在每個百分點只更新一次。
- **SegmentedControl** 以及其他帶滑動指示條的切換控制項：隱藏的控制項不再在視窗的每次版面配置時放置指示條；控制項再次顯示時才會放置。
- **GlidingIndicator**：描邊畫筆在影格之間保留，不再每次轉譯都新建。
- TreeView 和 JsonViewer：在下次版面配置之前對列再次做出更改，而第一次更改的列還在關閉過程中，會在 `RowTop` 中擲出 `IndexOutOfRangeException`。面板現在在列變化後立即重新整理列的幾何資訊。

## 0.2.0-preview.1 (2026-10-21)

### 新增

- **目標框架**：函式庫同時面向 `net8.0`，與 `net10.0`、`net11.0` 並列。net8.0 建置需要 StreamMark `0.1.0-alpha.25`，該版本為其套件加入了 net8.0。
- **Markdown**（`TKCode.Arc`）：大綱與搜尋。提供 `GetOutline`、`ScrollToPoint`、`ScrollToAnchor`、`Find` 和 `GetSelectedText`，並有 `IsDocumentReady`、`MatchCount`、`CurrentMatchIndex`、`HasSelection` 屬性，以及 `DocumentReady` 和 `SearchChanged` 事件。宿主可以圍繞文件搭建大綱側欄、搜尋列和複製操作。
- **RichTextEditor**（`TKCode.Arc`）：`TrySetMarkdown` 匯入 Markdown，並回傳 `RichTextImportReport`，列出編輯器無法保留的內容（`RichTextImportIssueKind`：標題層級、程式碼圍欄的資訊字串、波浪線圍欄、表格等）。
- **DataGrid**（`TKCode.Arc.AdvancedCharts`，儲存庫內，尚未發布到 NuGet）：儲存格渲染器（徽章、進度、連結、圖示文字、核取方塊、操作）、驗證規則（必填、範圍、最大長度、模式）、條件格式規則、`CellInvoked` 事件、右鍵選單事件、CSV 匯出選項，以及篩選與版面狀態。
- **Blocks**（`TKCode.Arc.Blocks`，儲存庫內，尚未發布到 NuGet）：命令面板對話框；帶數值與日期篩選、詳情和操作的搜尋結果；側欄的摺疊與調整大小模式；重做的通知中心。

### 變更

- AtomUI 鎖定為 6.2.7；0.1.1-preview.1 基於 6.2.6 建置。AtomUI 相依套件隨 Arc 版本一同變更。
- DataGrid 的 CSV 匯出預設會處理試算表公式（`NeutralizeCsvFormulas`），超過 `MaxPasteCells`（預設 1,000,000 個儲存格）的貼上會被拒絕。
- 在 .NET 8 和 9 上，DataGrid 的自然文字排序使用內建比較器；在 .NET 10 及更高版本上仍使用 `CompareOptions.NumericOrdering`。

### 修正

- `LiveLoop.Start` 在已經在輸出影格時提前返回，因此每次指標移動都啟動迴圈的呼叫端不再每次重新讀取門控條件。

## 0.1.1-preview.1 (2026-10-21)

### 新增

- **元件**：Activity、Content、Editors、Illustrations、Media 與 SpecialInputs 分類，以及 ButtonGroup、SplitButton、Card、Reactions、SwipeActions，還有 Color、Date、DateRange 與 Time 挑選器。
- **Core**：`MotionTimeline` 與 `LiveLoop` 動態效果驅動程式、`SharedElement` 基礎元件、`ArcBlur` 與 `ArcViewport`。
- **Blocks**（`TKCode.Arc.Blocks`）：AppShell 區塊（Sidebar、PageHeader、CommandPalette、SearchResults）以及設定區塊（SettingsPage、SecuritySettings、BillingOverview、UsageBilling、CancelFlow、SettingsCommand）。
- **AdvancedCharts**（`TKCode.Arc.AdvancedCharts`）：一個新專案，首先提供 DataGrid 控制項。
- **圖庫**
  - 元件頁面即其附即時範例的文件頁面；每個範例都是一個 `.axaml` 檔案，其「程式碼」索引標籤會顯示該原始碼。
  - 側邊欄新增文件區，按 `Cmd+K` 或 `Ctrl+K` 可在視窗的任何位置聚焦搜尋。
  - 從 XML 文件產生的 API 表格，附 zh-CN 與 zh-TW 翻譯；外殼文字與文件頁面提供 en-US、zh-CN 與 zh-TW 版本。
  - 透過 StreamMark `0.1.0-alpha.23` 實現 Markdown 渲染與程式碼醒目提示，這是第一個能在瀏覽器中執行的版本。
  - WebAssembly 主機（`TKCode.Arc.Gallery.Browser`），以及桌面與瀏覽器主機的應用程式圖示。
- **發佈**
  - 圖庫的 WebAssembly 映像在 GitHub 上建置，以 `ghcr.io/atomuicommunity/tkcode-arc-gallery` 發佈，並以 brotli、預先壓縮的檔案與瀏覽器快取提供服務。
  - 圖庫桌面應用程式的發行建置：Windows 與 Linux（未簽章），macOS（已簽章並完成公證）。
  - 透過 nuget.org 信任發佈，從 CI 打包並發佈 NuGet 套件。
- **工具**：API 文件翻譯流程的指令碼，以及圖示建置器。
- 簡體中文版 README 與贊助者區段。

### 變更

- 各分類的共用輔助程式由 Core 共用基礎元件取代。
- 圖庫頁面從依分類撰寫的 C# 頁面，改為 Markdown 文件頁面與 `.axaml` 範例。
- 許多控制項的公開屬性與事件新增了 XML `<summary>` 文字；它們會填入圖庫的 API 表格。

### 修正

- 動態效果：等待暫停的迴圈可能讓渲染器閒置，且影格要求懸而未決；現在影格會持續產生。
- HoldToConfirm、ConfirmMorph（長按模式）、Tabs 面板與 Carousel 可視範圍在重複使用剪裁區域時，沒有讓被剪裁的視覺元素失效，因此長按填滿不會移動，成長中的面板或調整大小後的輪播會保留過期的剪裁區域。
- Countdown：靜止的滾輪會繪製為一般字形，使滴答作響的錶面影格時間減半。
- 當 XAML 將 `Minimum` 設為高於目前的 `Maximum` 時（例如 `Minimum="360" Maximum="900"`），Slider 與 ElasticSlider 會因 `Math.Clamp` 擲出例外。
- 切換選取的索引標籤時，Tabs 不再捲動它所在的頁面；索引標籤清單會自行顯示選取的索引標籤。
- 圖庫瀏覽器主機：為 WebAssembly 建置原生 Skia，發佈時略過裁剪。

### 從 0.1.0-preview.1 升級

- 請將所有 TKCode.Arc 套件同時更新到相同版本。
- 新增的分類與控制項除 `UseArc()` 外不需額外設定。區塊與 DataGrid 需要各自的註冊（`UseArcBlocks()`、`UseArcAdvancedCharts()`）以及專案參照，詳見**安裝**。

## 0.1.0-preview.1 (2026-10-20)

首個預覽版本。
